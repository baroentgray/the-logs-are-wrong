using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Runtime;

public sealed record ActiveConfirmationTest
{
    public ActiveConfirmationTest(LogId logId, AnomalyId anomalyId, ConfirmationTestPlan plan, SimulationDuration accumulatedValidDuration, ServerTick? segmentStartedAt, ServerTick? dueAt, bool isRunning, ServerTick lastConditionBoundaryAt)
    {
        if (logId.IsDefault || anomalyId.IsDefault || plan is null || plan.AnomalyId != anomalyId || accumulatedValidDuration.IsDefault || accumulatedValidDuration < SimulationDuration.Zero || accumulatedValidDuration >= plan.Duration || lastConditionBoundaryAt.IsDefault) throw new ArgumentException("Invalid active confirmation test.");
        if (isRunning != (segmentStartedAt.HasValue && dueAt.HasValue)) throw new ArgumentException("Running confirmation state requires exact segment ticks.");
        var remaining = SimulationDuration.FromTicks(plan.Duration.Value - accumulatedValidDuration.Value);
        if (isRunning && (segmentStartedAt!.Value.IsDefault || dueAt!.Value.IsDefault || dueAt.Value != segmentStartedAt.Value + remaining)) throw new ArgumentException("Confirmation due tick is inconsistent.");
        LogId = logId; AnomalyId = anomalyId; Plan = plan; AccumulatedValidDuration = accumulatedValidDuration; SegmentStartedAt = segmentStartedAt; DueAt = dueAt; IsRunning = isRunning; LastConditionBoundaryAt = lastConditionBoundaryAt;
    }
    public LogId LogId { get; } public AnomalyId AnomalyId { get; } public ConfirmationTestPlan Plan { get; } public SimulationDuration AccumulatedValidDuration { get; } public ServerTick? SegmentStartedAt { get; } public ServerTick? DueAt { get; } public bool IsRunning { get; } public ServerTick LastConditionBoundaryAt { get; }
}
public sealed record ConfirmationTestResult(LogId LogId, AnomalyId AnomalyId, string Result, ImmutableHashSet<ItemId> RequiredTools, SimulationDuration Duration, ServerTick CompletedAt);
public enum ConfirmationTestStartRejectionReason { TargetNotFound, TargetNotAtIntake, ActiveConfirmationAlreadyExists, AlreadyConfirmed, NoConfirmationPlan, MissingRequiredTool, RequiredLineNoiseNotMet }
public abstract record ConfirmationTestStartResult(ShiftRuntimeState State);
public sealed record ConfirmationTestStarted(ShiftRuntimeState State) : ConfirmationTestStartResult(State);
public sealed record ConfirmationTestStartRejected(ShiftRuntimeState State, ConfirmationTestStartRejectionReason Reason) : ConfirmationTestStartResult(State);
public abstract record ConfirmationTestConditionResult(ShiftRuntimeState State);
public sealed record ConfirmationTestConditionNoChange(ShiftRuntimeState State) : ConfirmationTestConditionResult(State);
public sealed record ConfirmationTestConditionUpdated(ShiftRuntimeState State) : ConfirmationTestConditionResult(State);
public sealed record ConfirmationTestDueCompletionRequired(ShiftRuntimeState State) : ConfirmationTestConditionResult(State);
public abstract record ConfirmationTestDueCompletionResult(ShiftRuntimeState State);
public sealed record ConfirmationTestNoActive(ShiftRuntimeState State) : ConfirmationTestDueCompletionResult(State);
public sealed record ConfirmationTestPaused(ShiftRuntimeState State) : ConfirmationTestDueCompletionResult(State);
public sealed record ConfirmationTestNotDue(ShiftRuntimeState State) : ConfirmationTestDueCompletionResult(State);
public sealed record ConfirmationTestDueCompleted(ShiftRuntimeState State) : ConfirmationTestDueCompletionResult(State);
public sealed record ConfirmationTestDueFailed(ShiftRuntimeState State) : ConfirmationTestDueCompletionResult(State);
public enum ConfirmationTestCancellationRejectionReason { NoActiveConfirmation, TargetMismatch }
public abstract record ConfirmationTestCancellationResult(ShiftRuntimeState State);
public sealed record ConfirmationTestCancelled(ShiftRuntimeState State) : ConfirmationTestCancellationResult(State);
public sealed record ConfirmationTestCancellationRejected(ShiftRuntimeState State, ConfirmationTestCancellationRejectionReason Reason) : ConfirmationTestCancellationResult(State);

public sealed class ConfirmationTestStartService
{
    public ConfirmationTestStartResult Start(ShiftRuntimeState state, LogId logId, ImmutableHashSet<ItemId> activeTools, ServerTick tick, LineNoise noise, AnomalyCatalog catalog)
    {
        Guard(state, activeTools, tick, catalog); if (logId.IsDefault) throw new ArgumentException(nameof(logId));
        if (!state.TryGetLog(logId, out var log)) return new ConfirmationTestStartRejected(state, ConfirmationTestStartRejectionReason.TargetNotFound);
        if (log.State != LogState.AT_INTAKE) return new ConfirmationTestStartRejected(state, ConfirmationTestStartRejectionReason.TargetNotAtIntake);
        if (state.ActiveConfirmationTest is not null) return new ConfirmationTestStartRejected(state, ConfirmationTestStartRejectionReason.ActiveConfirmationAlreadyExists);
        if (state.TryGetConfirmationResult(logId, out _)) return new ConfirmationTestStartRejected(state, ConfirmationTestStartRejectionReason.AlreadyConfirmed);
        var resolver = new ConfirmationTestPlanResolver(catalog); if (!resolver.TryGetPlan(log, out var plan)) return new ConfirmationTestStartRejected(state, ConfirmationTestStartRejectionReason.NoConfirmationPlan);
        var resolvedPlan = plan!;
        if (!Valid(state, resolvedPlan, activeTools, noise, out var tools)) return new ConfirmationTestStartRejected(state, tools ? ConfirmationTestStartRejectionReason.RequiredLineNoiseNotMet : ConfirmationTestStartRejectionReason.MissingRequiredTool);
        var active = new ActiveConfirmationTest(logId, resolvedPlan.AnomalyId, resolvedPlan, SimulationDuration.Zero, tick, tick + resolvedPlan.Duration, true, tick);
        return new ConfirmationTestStarted(state.WithActiveConfirmation(active));
    }
    internal static void Guard(ShiftRuntimeState state, ImmutableHashSet<ItemId> tools, ServerTick tick, AnomalyCatalog catalog) { ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(tools); ArgumentNullException.ThrowIfNull(catalog); if (tick.IsDefault || tools.Any(tool => tool.IsDefault)) throw new ArgumentException("Invalid confirmation input."); }
    internal static bool Valid(ShiftRuntimeState state, ConfirmationTestPlan plan, ImmutableHashSet<ItemId> tools, LineNoise noise, out bool toolsValid) { toolsValid = plan.RequiredTools.All(tool => tools.Contains(tool) && state.Inventory.IsAvailable(tool)); return toolsValid && (plan.RequiredLineNoise is null || plan.RequiredLineNoise == noise); }
}
public sealed class ConfirmationTestConditionService
{
    public ConfirmationTestConditionResult Update(ShiftRuntimeState state, ServerTick tick, LineNoise noise, ImmutableHashSet<ItemId> tools, AnomalyCatalog catalog)
    {
        ConfirmationTestStartService.Guard(state, tools, tick, catalog); if (state.ActiveConfirmationTest is not { } active) return new ConfirmationTestConditionNoChange(state); if (tick < active.LastConditionBoundaryAt) throw new ArgumentOutOfRangeException(nameof(tick)); if (active.IsRunning && tick >= active.DueAt!.Value) return new ConfirmationTestDueCompletionRequired(state);
        var valid = ConfirmationTestStartService.Valid(state, active.Plan, tools, noise, out _);
        if (active.IsRunning && !valid) { var accumulated = active.Plan.Continuous ? SimulationDuration.Zero : active.AccumulatedValidDuration + (tick - active.SegmentStartedAt!.Value); return new ConfirmationTestConditionUpdated(state.WithActiveConfirmation(new ActiveConfirmationTest(active.LogId, active.AnomalyId, active.Plan, accumulated, null, null, false, tick))); }
        if (!active.IsRunning && valid) { var due = tick + SimulationDuration.FromTicks(active.Plan.Duration.Value - active.AccumulatedValidDuration.Value); return new ConfirmationTestConditionUpdated(state.WithActiveConfirmation(new ActiveConfirmationTest(active.LogId, active.AnomalyId, active.Plan, active.AccumulatedValidDuration, tick, due, true, tick))); }
        return new ConfirmationTestConditionNoChange(state);
    }
}
public sealed class ConfirmationTestDueCompletionService
{
    public ConfirmationTestDueCompletionResult CompleteDue(ShiftRuntimeState state, ServerTick tick, AnomalyCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(catalog); if (tick.IsDefault) throw new ArgumentException(nameof(tick)); if (state.ActiveConfirmationTest is not { } active) return new ConfirmationTestNoActive(state); if (!active.IsRunning) return new ConfirmationTestPaused(state); if (tick < active.DueAt!.Value) return new ConfirmationTestNotDue(state);
        if (!state.TryGetLog(active.LogId, out var log) || log.State != LogState.AT_INTAKE || log.Anomaly != active.AnomalyId || state.TryGetConfirmationResult(active.LogId, out _)) return new ConfirmationTestDueFailed(state);
        var result = new ConfirmationTestResult(active.LogId, active.AnomalyId, active.Plan.Result, active.Plan.RequiredTools, active.Plan.Duration, tick);
        return new ConfirmationTestDueCompleted(state.WithActiveConfirmation(null, result));
    }
}
public sealed class ConfirmationTestCancellationService
{
    public ConfirmationTestCancellationResult Cancel(ShiftRuntimeState state, LogId logId)
    { ArgumentNullException.ThrowIfNull(state); if (logId.IsDefault) throw new ArgumentException(nameof(logId)); if (state.ActiveConfirmationTest is not { } active) return new ConfirmationTestCancellationRejected(state, ConfirmationTestCancellationRejectionReason.NoActiveConfirmation); return active.LogId != logId ? new ConfirmationTestCancellationRejected(state, ConfirmationTestCancellationRejectionReason.TargetMismatch) : new ConfirmationTestCancelled(state.WithActiveConfirmation(null)); }
}
