using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Scheduler;

public sealed record ActiveSawCycle
{
    public ActiveSawCycle(LogId logId, ServerTick startedAt, SimulationDuration duration)
    {
        if (logId.IsDefault || startedAt.IsDefault || duration.IsDefault || duration <= SimulationDuration.Zero)
        {
            throw new ArgumentException("An active saw cycle requires initialized identity and timing with a positive duration.");
        }

        LogId = logId;
        StartedAt = startedAt;
        Duration = duration;
        DueAt = startedAt + duration;
    }

    public LogId LogId { get; }
    public ServerTick StartedAt { get; }
    public SimulationDuration Duration { get; }
    public ServerTick DueAt { get; }
}

public abstract record SawCycleStartResult(ShiftRuntimeState State);

public sealed record SawCycleStarted(
    ShiftRuntimeState State,
    ActiveSawCycle Cycle,
    StateVersion PriorStateVersion,
    StateVersion CurrentStateVersion) : SawCycleStartResult(State);

public sealed record SawCycleStartNoQueuedOwner(ShiftRuntimeState State) : SawCycleStartResult(State);

public sealed record SawCycleStartAlreadyActive(ShiftRuntimeState State, ActiveSawCycle Cycle) : SawCycleStartResult(State);

public sealed class SawCycleStartService
{
    public SawCycleStartResult Start(ShiftRuntimeState state, ServerTick currentTick, SchedulerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(configuration);
        if (currentTick.IsDefault)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Current scheduler tick must be initialized.");
        }

        var duration = configuration.SawCycleSeconds > 0
            ? SimulationDuration.FromTicks(configuration.SawCycleSeconds)
            : throw new ArgumentOutOfRangeException(nameof(configuration.SawCycleSeconds), "Saw cycle duration must be positive.");

        if (state.ActiveSawCycle is { } active)
        {
            ValidateActiveState(state, active);
            return new SawCycleStartAlreadyActive(state, active);
        }

        if (state.GetNodeOccupancy(NodeId.SAW) != 0)
        {
            throw new InvalidOperationException("A saw occupant requires an active saw cycle.");
        }

        var queued = state.Logs.Where(log => log.State == LogState.QUEUED_FOR_SAW).ToArray();
        if (queued.Length == 0)
        {
            return new SawCycleStartNoQueuedOwner(state);
        }

        if (queued.Length != 1)
        {
            throw new InvalidOperationException("The saw queue must have exactly one owner before a cycle starts.");
        }

        var cycle = new ActiveSawCycle(queued[0].LogId, currentTick, duration);
        var after = state.StartSawCycle(cycle);
        return new SawCycleStarted(after, cycle, state.StateVersion, after.StateVersion);
    }

    private static void ValidateActiveState(ShiftRuntimeState state, ActiveSawCycle active)
    {
        if (!state.TryGetLog(active.LogId, out var owner) || owner.State != LogState.IN_SAW || state.GetNodeOccupancy(NodeId.SAW) != 1)
        {
            throw new InvalidOperationException("The active saw cycle must own the only saw occupant.");
        }

        if (state.GetNodeOccupancy(NodeId.SAW_QUEUE) > 1)
        {
            throw new InvalidOperationException("The saw queue cannot exceed one waiting owner.");
        }
    }
}

public abstract record SawCycleCompletionResult(ShiftRuntimeState State);

public sealed record SawCycleNoActive(ShiftRuntimeState State) : SawCycleCompletionResult(State);

public sealed record SawCycleNotDue(ShiftRuntimeState State, ActiveSawCycle Cycle) : SawCycleCompletionResult(State);

public sealed record SawCycleCompleted(
    ShiftRuntimeState State,
    ActiveSawCycle Cycle,
    ProcessingResolution Resolution,
    ServerTick CompletedAt,
    StateVersion PriorStateVersion,
    StateVersion CurrentStateVersion) : SawCycleCompletionResult(State);

public sealed class SawCycleCompletionService
{
    public SawCycleCompletionResult Complete(ShiftRuntimeState state, ServerTick currentTick, AnomalyCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        if (currentTick.IsDefault)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Current scheduler tick must be initialized.");
        }

        if (state.ActiveSawCycle is not { } active)
        {
            if (state.GetNodeOccupancy(NodeId.SAW) != 0)
            {
                throw new InvalidOperationException("A saw occupant requires an active saw cycle.");
            }

            return new SawCycleNoActive(state);
        }

        ValidateActiveState(state, active);
        if (currentTick < active.StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "A saw cycle cannot complete before it starts.");
        }

        if (currentTick < active.DueAt)
        {
            return new SawCycleNotDue(state, active);
        }

        if (!state.TryGetLog(active.LogId, out var owner))
        {
            throw new InvalidOperationException("The active saw owner must exist.");
        }

        var resolution = AnomalyProcessingResolver.Resolve(owner, catalog);
        var after = state.CompleteSawCycle(active);
        return new SawCycleCompleted(after, active, resolution, currentTick, state.StateVersion, after.StateVersion);
    }

    private static void ValidateActiveState(ShiftRuntimeState state, ActiveSawCycle active)
    {
        if (!state.TryGetLog(active.LogId, out var owner) || owner.State != LogState.IN_SAW || state.GetNodeOccupancy(NodeId.SAW) != 1)
        {
            throw new InvalidOperationException("The active saw cycle must own the only saw occupant.");
        }
    }
}
