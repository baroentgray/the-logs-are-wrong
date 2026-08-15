using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Runtime;

public sealed record ActiveConfirmationTest
{
    public ActiveConfirmationTest(LogId logId, AnomalyId anomalyId, ConfirmationTestPlan plan, SimulationDuration accumulatedValidDuration, ServerTick? segmentStartedAt, ServerTick? dueAt, bool isRunning, ServerTick lastConditionBoundaryAt)
    {
        if (logId.IsDefault || anomalyId.IsDefault || plan is null || plan.AnomalyId != anomalyId || accumulatedValidDuration.IsDefault || accumulatedValidDuration < SimulationDuration.Zero || accumulatedValidDuration >= plan.Duration || lastConditionBoundaryAt.IsDefault) throw new ArgumentException("Invalid active confirmation test.");
        if (segmentStartedAt.HasValue != dueAt.HasValue || isRunning != segmentStartedAt.HasValue) throw new ArgumentException("Running confirmation state requires both segment ticks; paused state requires neither.");
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
    public ConfirmationTestStartResult Start(ShiftRuntimeState state, LogId logId, ImmutableHashSet<ItemId> activeTools, ServerTick tick, LineNoiseRuntimeState lineNoiseRuntime, AnomalyCatalog catalog)
    {
        return StartCore(state, logId, activeTools, tick, lineNoiseRuntime, catalog, null);
    }

    internal ConfirmationTestStartResult StartForAuthoritativeIntent(ShiftRuntimeState state, LogId logId, ImmutableHashSet<ItemId> activeTools, ServerTick tick, LineNoiseRuntimeState lineNoiseRuntime, AnomalyCatalog catalog, IntentId processedIntentId)
    {
        if (processedIntentId.IsDefault)
        {
            throw new ArgumentException("Processed intent identifier must be initialized.", nameof(processedIntentId));
        }

        return StartCore(state, logId, activeTools, tick, lineNoiseRuntime, catalog, processedIntentId);
    }

    private static ConfirmationTestStartResult StartCore(ShiftRuntimeState state, LogId logId, ImmutableHashSet<ItemId> activeTools, ServerTick tick, LineNoiseRuntimeState lineNoiseRuntime, AnomalyCatalog catalog, IntentId? processedIntentId)
    {
        Guard(state, activeTools, tick, catalog); ValidateStartLineNoise(state, lineNoiseRuntime, tick); if (logId.IsDefault) throw new ArgumentException(nameof(logId));
        if (!state.TryGetLog(logId, out var log)) return new ConfirmationTestStartRejected(state, ConfirmationTestStartRejectionReason.TargetNotFound);
        if (log.State != LogState.AT_INTAKE) return new ConfirmationTestStartRejected(state, ConfirmationTestStartRejectionReason.TargetNotAtIntake);
        if (state.ActiveConfirmationTest is not null) return new ConfirmationTestStartRejected(state, ConfirmationTestStartRejectionReason.ActiveConfirmationAlreadyExists);
        if (state.TryGetConfirmationResult(logId, out _)) return new ConfirmationTestStartRejected(state, ConfirmationTestStartRejectionReason.AlreadyConfirmed);
        var resolver = new ConfirmationTestPlanResolver(catalog); if (!resolver.TryGetPlan(log, out var plan)) return new ConfirmationTestStartRejected(state, ConfirmationTestStartRejectionReason.NoConfirmationPlan);
        var resolvedPlan = plan!;
        if (!Valid(state, resolvedPlan, activeTools, lineNoiseRuntime.Current, out var tools)) return new ConfirmationTestStartRejected(state, tools ? ConfirmationTestStartRejectionReason.RequiredLineNoiseNotMet : ConfirmationTestStartRejectionReason.MissingRequiredTool);
        var active = new ActiveConfirmationTest(logId, resolvedPlan.AnomalyId, resolvedPlan, SimulationDuration.Zero, tick, tick + resolvedPlan.Duration, true, tick);
        return new ConfirmationTestStarted(processedIntentId is { } intentId
            ? state.StartActiveConfirmationForAuthoritativeIntent(active, intentId)
            : state.WithActiveConfirmation(active));
    }
    internal static void Guard(ShiftRuntimeState state, ImmutableHashSet<ItemId> tools, ServerTick tick, AnomalyCatalog catalog) { if (state is null) { throw new ArgumentNullException("state"); } if (tools is null) { throw new ArgumentNullException("tools"); } if (catalog is null) { throw new ArgumentNullException("catalog"); } if (tick.IsDefault || tools.Any(tool => tool.IsDefault)) throw new ArgumentException("Invalid confirmation input."); }
    private static void ValidateStartLineNoise(ShiftRuntimeState state, LineNoiseRuntimeState runtime, ServerTick tick)
    {
        if (runtime is null) { throw new ArgumentNullException("runtime"); }
        if (state.ShiftId.IsDefault || runtime.ShiftId != state.ShiftId)
        {
            throw new ArgumentException("Line-noise runtime must belong to the exact confirmation shift.", nameof(runtime));
        }

        LineNoiseDerivationService.ValidateRuntime(runtime);
        if (runtime.LastEvaluatedAt is { } evaluatedAt && evaluatedAt > tick)
        {
            throw new ArgumentOutOfRangeException(nameof(runtime), "Confirmation start cannot consume future line-noise evidence.");
        }
    }
    internal static bool Valid(ShiftRuntimeState state, ConfirmationTestPlan plan, ImmutableHashSet<ItemId> tools, LineNoise noise, out bool toolsValid) { toolsValid = plan.RequiredTools.All(tool => tools.Contains(tool) && state.Inventory.IsAvailable(tool)); return toolsValid && (plan.RequiredLineNoise is null || plan.RequiredLineNoise == noise); }
}
public sealed class ConfirmationTestConditionService
{
    public ConfirmationTestConditionResult Update(ShiftRuntimeState state, ServerTick tick, LineNoiseEvaluationResult lineNoiseEvaluation, ImmutableHashSet<ItemId> tools, AnomalyCatalog catalog)
    {
        ConfirmationTestStartService.Guard(state, tools, tick, catalog); ValidateCurrentLineNoiseEvaluation(state, tick, lineNoiseEvaluation); if (state.ActiveConfirmationTest is not { } active) return new ConfirmationTestConditionNoChange(state); if (tick < active.LastConditionBoundaryAt) throw new ArgumentOutOfRangeException(nameof(tick)); if (active.IsRunning && tick >= active.DueAt!.Value) return new ConfirmationTestDueCompletionRequired(state);
        var valid = ConfirmationTestStartService.Valid(state, active.Plan, tools, lineNoiseEvaluation.State.Current, out _);
        if (active.IsRunning && !valid) { var accumulated = active.Plan.Continuous ? SimulationDuration.Zero : active.AccumulatedValidDuration + (tick - active.SegmentStartedAt!.Value); return new ConfirmationTestConditionUpdated(state.WithActiveConfirmation(new ActiveConfirmationTest(active.LogId, active.AnomalyId, active.Plan, accumulated, null, null, false, tick))); }
        if (!active.IsRunning && valid) { var due = tick + SimulationDuration.FromTicks(active.Plan.Duration.Value - active.AccumulatedValidDuration.Value); return new ConfirmationTestConditionUpdated(state.WithActiveConfirmation(new ActiveConfirmationTest(active.LogId, active.AnomalyId, active.Plan, active.AccumulatedValidDuration, tick, due, true, tick))); }
        return new ConfirmationTestConditionNoChange(state);
    }

    private static void ValidateCurrentLineNoiseEvaluation(ShiftRuntimeState state, ServerTick tick, LineNoiseEvaluationResult evaluation)
    {
        if (evaluation is null) { throw new ArgumentNullException("evaluation"); }
        if (evaluation.State is null) { throw new ArgumentNullException("evaluation.State"); }
        var runtime = evaluation.State;
        if (state.ShiftId.IsDefault || runtime.ShiftId != state.ShiftId || runtime.LastEvaluatedAt != tick)
        {
            throw new ArgumentException("Confirmation condition handling requires exact current-tick line-noise evidence.", nameof(evaluation));
        }

        LineNoiseDerivationService.ValidateRuntime(runtime);
        switch (evaluation)
        {
            case LineNoiseEvaluatedWithChange withChange:
                if (withChange.Change is null) { throw new ArgumentNullException("withChange.Change"); }
                var change = withChange.Change;
                if (change.ShiftId != runtime.ShiftId || change.Current != runtime.Current || change.Sources != runtime.LatestSources || change.ChangedAt != tick || runtime.LastChangedAt != tick)
                {
                    throw new ArgumentException("Line-noise change evidence must match the exact evaluated runtime.", nameof(evaluation));
                }
                break;
            case LineNoiseEvaluatedWithoutChange:
                if (runtime.LastChangedAt == tick)
                {
                    throw new ArgumentException("A no-change evaluation cannot fabricate a same-tick line-noise change.", nameof(evaluation));
                }
                break;
            case LineNoiseAlreadyEvaluated:
                break;
            default:
                throw new ArgumentException("Confirmation condition handling accepts only authoritative line-noise evaluation results.", nameof(evaluation));
        }
    }
}
public sealed class ConfirmationTestDueCompletionService
{
    public ConfirmationTestDueCompletionResult CompleteDue(ShiftRuntimeState state, ServerTick tick, AnomalyCatalog catalog)
    {
        if (state is null) { throw new ArgumentNullException("state"); } if (catalog is null) { throw new ArgumentNullException("catalog"); } if (tick.IsDefault) throw new ArgumentException(nameof(tick)); if (state.ActiveConfirmationTest is not { } active) return new ConfirmationTestNoActive(state); if (!active.IsRunning) return new ConfirmationTestPaused(state); if (tick < active.DueAt!.Value) return new ConfirmationTestNotDue(state);
        if (!state.TryGetLog(active.LogId, out var log) || log.State != LogState.AT_INTAKE || log.Anomaly != active.AnomalyId || state.TryGetConfirmationResult(active.LogId, out _)) return new ConfirmationTestDueFailed(state);
        var result = new ConfirmationTestResult(active.LogId, active.AnomalyId, active.Plan.Result, active.Plan.RequiredTools, active.Plan.Duration, tick);
        return new ConfirmationTestDueCompleted(state.WithActiveConfirmation(null, result));
    }
}
public sealed class ConfirmationTestCancellationService
{
    public ConfirmationTestCancellationResult Cancel(ShiftRuntimeState state, LogId logId)
    { if (state is null) { throw new ArgumentNullException("state"); } if (logId.IsDefault) throw new ArgumentException(nameof(logId)); if (state.ActiveConfirmationTest is not { } active) return new ConfirmationTestCancellationRejected(state, ConfirmationTestCancellationRejectionReason.NoActiveConfirmation); return active.LogId != logId ? new ConfirmationTestCancellationRejected(state, ConfirmationTestCancellationRejectionReason.TargetMismatch) : new ConfirmationTestCancelled(state.WithActiveConfirmation(null)); }
}
