using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Line;

public enum MovementNoiseAcceptedSource
{
    ManualLogIntent,
    HostLogTransition,
    FeedDueResolved,
    DefaultIntakeAutoRoute,
    RepairPendingTransition,
    SawCycleStarted,
    SawCycleCompleted
}

public sealed record MovementNoiseAcceptedMovement
{
    internal MovementNoiseAcceptedMovement(
        MovementNoiseAcceptedSource source,
        LogId logId,
        LogState sourceState,
        LogState destinationState,
        StateVersion priorStateVersion,
        StateVersion currentStateVersion,
        ServerTick acceptedAt)
    {
        if (!Enum.IsDefined(source) || logId.IsDefault || !Enum.IsDefined(sourceState) || !Enum.IsDefined(destinationState) ||
            priorStateVersion.IsDefault || currentStateVersion.IsDefault || currentStateVersion != priorStateVersion.Next() || acceptedAt.IsDefault)
        {
            throw new ArgumentException("Accepted movement evidence must retain initialized, sequential, and typed values.");
        }

        Source = source;
        LogId = logId;
        SourceState = sourceState;
        DestinationState = destinationState;
        PriorStateVersion = priorStateVersion;
        CurrentStateVersion = currentStateVersion;
        AcceptedAt = acceptedAt;
    }

    public MovementNoiseAcceptedSource Source { get; }
    public LogId LogId { get; }
    public LogState SourceState { get; }
    public LogState DestinationState { get; }
    public StateVersion PriorStateVersion { get; }
    public StateVersion CurrentStateVersion { get; }
    public ServerTick AcceptedAt { get; }
}

public sealed class MovementNoiseRuntimeState
{
    private MovementNoiseRuntimeState(ShiftId shiftId, MovementNoiseAcceptedMovement? lastAcceptedMovement, ServerTick startedAt, ServerTick dueAt)
    {
        ShiftId = shiftId;
        LastAcceptedMovement = lastAcceptedMovement;
        StartedAt = startedAt;
        DueAt = dueAt;
    }

    public ShiftId ShiftId { get; }
    public MovementNoiseAcceptedMovement? LastAcceptedMovement { get; }
    public ServerTick StartedAt { get; }
    public ServerTick DueAt { get; }
    public bool HasAcceptedMovement => LastAcceptedMovement is not null;

    public static MovementNoiseRuntimeState Create(ShiftId shiftId)
    {
        if (shiftId.IsDefault)
        {
            throw new ArgumentException("Movement-noise runtime requires an initialized shift identifier.", nameof(shiftId));
        }

        return new MovementNoiseRuntimeState(shiftId, null, default, default);
    }

    public bool IsActiveAt(ServerTick tick)
    {
        if (tick.IsDefault)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), "Activity queries require an initialized authoritative tick.");
        }

        return LastAcceptedMovement is not null && tick >= StartedAt && tick < DueAt;
    }

    public bool ValueEquals(MovementNoiseRuntimeState? other) =>
        other is not null && ShiftId == other.ShiftId && LastAcceptedMovement == other.LastAcceptedMovement && StartedAt == other.StartedAt && DueAt == other.DueAt;

    internal MovementNoiseRuntimeState Apply(MovementNoiseAcceptedMovement movement, ServerTick startedAt, ServerTick dueAt) =>
        new(ShiftId, movement, startedAt, dueAt);
}

public abstract record MovementNoiseApplicationResult(MovementNoiseRuntimeState State);

public sealed record MovementNoiseApplied(MovementNoiseRuntimeState State) : MovementNoiseApplicationResult(State);

public sealed record MovementNoiseAlreadyApplied(MovementNoiseRuntimeState State) : MovementNoiseApplicationResult(State);

public sealed class MovementNoiseApplicationService
{
    public MovementNoiseApplicationResult Apply(
        MovementNoiseRuntimeState runtime,
        ManualLogIntentAccepted accepted,
        ServerTick acceptedAt,
        SchedulerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        var transition = accepted.Transition;
        return ApplyNormalized(runtime, accepted.State, configuration, new MovementNoiseAcceptedMovement(
            MovementNoiseAcceptedSource.ManualLogIntent,
            transition.LogId,
            transition.FromState,
            transition.ToState,
            transition.PriorStateVersion,
            transition.CurrentStateVersion,
            acceptedAt));
    }

    public MovementNoiseApplicationResult Apply(
        MovementNoiseRuntimeState runtime,
        HostLogTransitionAccepted accepted,
        ServerTick acceptedAt,
        SchedulerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        var transition = accepted.Descriptor;
        return ApplyNormalized(runtime, accepted.State, configuration, new MovementNoiseAcceptedMovement(
            MovementNoiseAcceptedSource.HostLogTransition,
            transition.LogId,
            transition.FromState,
            transition.ToState,
            transition.PriorStateVersion,
            transition.CurrentStateVersion,
            acceptedAt));
    }

    public MovementNoiseApplicationResult Apply(
        MovementNoiseRuntimeState runtime,
        FeedDueResolved accepted,
        SchedulerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        var destination = accepted.Disposition switch
        {
            FeedDueDisposition.AdmittedToIntake => LogState.AT_INTAKE,
            FeedDueDisposition.PlacedAtFeedGate => LogState.AT_FEED_GATE,
            _ => throw new ArgumentException("Feed-due evidence has an unknown disposition.", nameof(accepted))
        };
        var expectedFollowUp = accepted.Disposition == FeedDueDisposition.AdmittedToIntake
            ? FeedDueFollowUpRequirement.IntakeDeadlineStartRequired
            : FeedDueFollowUpRequirement.FeedGateJamDerivationRequired;
        if (accepted.FollowUpRequirement != expectedFollowUp || accepted.ResolvedAt < accepted.ConsumedSchedule.DueAt)
        {
            throw new ArgumentException("Feed-due evidence contradicts its retained disposition, follow-up, or due tick.", nameof(accepted));
        }

        return ApplyNormalized(runtime, accepted.State, configuration, new MovementNoiseAcceptedMovement(
            MovementNoiseAcceptedSource.FeedDueResolved,
            accepted.ConsumedSchedule.LogId,
            LogState.SCHEDULED,
            destination,
            accepted.PriorStateVersion,
            accepted.CurrentStateVersion,
            accepted.ResolvedAt));
    }

    public MovementNoiseApplicationResult Apply(
        MovementNoiseRuntimeState runtime,
        DefaultIntakeAutoRouteApplied accepted,
        SchedulerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        return ApplyNormalized(runtime, accepted.State, configuration, new MovementNoiseAcceptedMovement(
            MovementNoiseAcceptedSource.DefaultIntakeAutoRoute,
            accepted.LogId,
            accepted.Source,
            accepted.Destination,
            accepted.PriorStateVersion,
            accepted.CurrentStateVersion,
            accepted.AttemptedAt));
    }

    public MovementNoiseApplicationResult Apply(
        MovementNoiseRuntimeState runtime,
        RepairPendingTransitionExecuted accepted,
        SchedulerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        if (accepted.PendingTransition.LogId != accepted.LogId || accepted.PendingTransition.FromState != accepted.Source || accepted.PendingTransition.ToState != accepted.Destination)
        {
            throw new ArgumentException("Repair execution evidence contradicts its retained pending transition.", nameof(accepted));
        }

        return ApplyNormalized(runtime, accepted.State, configuration, new MovementNoiseAcceptedMovement(
            MovementNoiseAcceptedSource.RepairPendingTransition,
            accepted.LogId,
            accepted.Source,
            accepted.Destination,
            accepted.PriorStateVersion,
            accepted.CurrentStateVersion,
            accepted.AppliedAt));
    }

    public MovementNoiseApplicationResult Apply(
        MovementNoiseRuntimeState runtime,
        SawCycleStarted accepted,
        SchedulerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        ValidateCycle(accepted.Cycle);
        if (accepted.State.ActiveSawCycle != accepted.Cycle)
        {
            throw new ArgumentException("Saw-start evidence must retain its exact active cycle.", nameof(accepted));
        }

        return ApplyNormalized(runtime, accepted.State, configuration, new MovementNoiseAcceptedMovement(
            MovementNoiseAcceptedSource.SawCycleStarted,
            accepted.Cycle.LogId,
            LogState.QUEUED_FOR_SAW,
            LogState.IN_SAW,
            accepted.PriorStateVersion,
            accepted.CurrentStateVersion,
            accepted.Cycle.StartedAt));
    }

    public MovementNoiseApplicationResult Apply(
        MovementNoiseRuntimeState runtime,
        SawCycleCompleted accepted,
        SchedulerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        ValidateCycle(accepted.Cycle);
        if (accepted.CompletedAt < accepted.Cycle.DueAt || accepted.State.ActiveSawCycle is not null || accepted.Resolution.LogId != accepted.Cycle.LogId || accepted.Resolution.TerminalState != LogState.PROCESSED)
        {
            throw new ArgumentException("Saw-completion evidence contradicts its retained cycle, resolution, state, or timing.", nameof(accepted));
        }

        return ApplyNormalized(runtime, accepted.State, configuration, new MovementNoiseAcceptedMovement(
            MovementNoiseAcceptedSource.SawCycleCompleted,
            accepted.Cycle.LogId,
            LogState.IN_SAW,
            LogState.PROCESSED,
            accepted.PriorStateVersion,
            accepted.CurrentStateVersion,
            accepted.CompletedAt));
    }

    private static MovementNoiseApplicationResult ApplyNormalized(
        MovementNoiseRuntimeState runtime,
        ShiftRuntimeState acceptedState,
        SchedulerConfiguration configuration,
        MovementNoiseAcceptedMovement movement)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(configuration);
        if (runtime.ShiftId != acceptedState.ShiftId || acceptedState.StateVersion != movement.CurrentStateVersion ||
            !acceptedState.TryGetLog(movement.LogId, out var owner) || owner.State != movement.DestinationState)
        {
            throw new ArgumentException("Movement evidence must belong to the runtime shift and exactly match its accepted state.");
        }

        var duration = GetDuration(configuration);
        if (runtime.LastAcceptedMovement is { } last)
        {
            var comparison = movement.CurrentStateVersion.CompareTo(last.CurrentStateVersion);
            if (comparison == 0)
            {
                return movement == last
                    ? new MovementNoiseAlreadyApplied(runtime)
                    : throw new ArgumentException("Same-version movement evidence must be exactly equivalent to the retained acceptance.");
            }

            if (comparison < 0)
            {
                throw new ArgumentException("Older movement evidence cannot be applied after a newer accepted movement.");
            }
        }

        var candidateDueAt = movement.AcceptedAt + duration;
        if (runtime.LastAcceptedMovement is null || movement.AcceptedAt >= runtime.DueAt)
        {
            return new MovementNoiseApplied(runtime.Apply(movement, movement.AcceptedAt, candidateDueAt));
        }

        var dueAt = candidateDueAt > runtime.DueAt ? candidateDueAt : runtime.DueAt;
        return new MovementNoiseApplied(runtime.Apply(movement, runtime.StartedAt, dueAt));
    }

    private static SimulationDuration GetDuration(SchedulerConfiguration configuration)
    {
        if (configuration.MovementNoiseSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Configured movement-noise duration must be positive.");
        }

        return SimulationDuration.FromTicks(configuration.MovementNoiseSeconds);
    }

    private static void ValidateCycle(ActiveSawCycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        if (cycle.LogId.IsDefault || cycle.StartedAt.IsDefault || cycle.Duration.IsDefault || cycle.Duration <= SimulationDuration.Zero || cycle.DueAt != cycle.StartedAt + cycle.Duration)
        {
            throw new ArgumentException("Saw movement evidence requires a valid retained cycle.", nameof(cycle));
        }
    }
}
