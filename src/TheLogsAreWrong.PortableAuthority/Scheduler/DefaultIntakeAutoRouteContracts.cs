using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Scheduler;

public abstract record DefaultIntakeAutoRouteResult(ShiftRuntimeState State);

public sealed record DefaultIntakeAutoRouteOwnerMissing(ShiftRuntimeState State, LogId LogId)
    : DefaultIntakeAutoRouteResult(State);

public sealed record DefaultIntakeAutoRouteNoLongerApplicable(ShiftRuntimeState State, LogId LogId)
    : DefaultIntakeAutoRouteResult(State);

public enum DefaultIntakeAutoRouteBlockReason
{
    SawQueueOccupied
}

public enum DefaultIntakeAutoRouteFollowUp
{
    IntakeAutoFeedJamDerivationRequired,
    ExistingLineConditionRetained
}

public sealed record DefaultIntakeAutoRouteBlocked(
    ShiftRuntimeState State,
    LogId LogId,
    ServerTick AttemptedAt,
    DefaultIntakeAutoRouteBlockReason Reason,
    DefaultIntakeAutoRouteFollowUp FollowUp)
    : DefaultIntakeAutoRouteResult(State);

public sealed record DefaultIntakeAutoRouteApplied(
    ShiftRuntimeState State,
    LogId LogId,
    ServerTick AttemptedAt,
    LogState Source,
    LogState Destination,
    StateVersion PriorStateVersion,
    StateVersion CurrentStateVersion)
    : DefaultIntakeAutoRouteResult(State);

/// <summary>Host-only feed-and-auto-routes seam. It delegates the frozen log graph to the shared host transition service.</summary>
public sealed class DefaultIntakeAutoRouteService
{
    private readonly HostLogTransitionService _transitions = new();

    public DefaultIntakeAutoRouteResult Attempt(ShiftRuntimeState state, DefaultAutoRouteRequired requirement, ServerTick currentTick)
    {
        if (state is null) { throw new ArgumentNullException("state"); }
        if (requirement is null) { throw new ArgumentNullException("requirement"); }
        if (currentTick.IsDefault || currentTick < requirement.DueAt)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "The authoritative route tick must be initialized and not precede the requirement due tick.");
        }

        if (!state.TryGetLog(requirement.LogId, out var owner))
        {
            return new DefaultIntakeAutoRouteOwnerMissing(state, requirement.LogId);
        }

        if (owner.State != LogState.AT_INTAKE)
        {
            return new DefaultIntakeAutoRouteNoLongerApplicable(state, requirement.LogId);
        }

        if (state.ActiveIntakeDeadline is not null)
        {
            throw new InvalidOperationException("A default intake auto-route requires the deadline to have expired first.");
        }

        if (state.GetNodeOccupancy(NodeId.SAW_QUEUE) != 0)
        {
            var followUp = state.Line.State == LineState.LINE_CLEAR
                ? DefaultIntakeAutoRouteFollowUp.IntakeAutoFeedJamDerivationRequired
                : DefaultIntakeAutoRouteFollowUp.ExistingLineConditionRetained;
            return new DefaultIntakeAutoRouteBlocked(state, requirement.LogId, currentTick, DefaultIntakeAutoRouteBlockReason.SawQueueOccupied, followUp);
        }

        var transition = _transitions.Apply(state, requirement.LogId, LogState.QUEUED_FOR_SAW);
        if (transition is not HostLogTransitionAccepted accepted ||
            accepted.Descriptor.LogId != requirement.LogId ||
            accepted.Descriptor.FromState != LogState.AT_INTAKE ||
            accepted.Descriptor.ToState != LogState.QUEUED_FOR_SAW ||
            accepted.Descriptor.PriorStateVersion != state.StateVersion ||
            accepted.Descriptor.CurrentStateVersion != state.StateVersion.Next() ||
            accepted.State.StateVersion != accepted.Descriptor.CurrentStateVersion ||
            accepted.State.ActiveIntakeDeadline is not null)
        {
            throw new InvalidOperationException("The host transition seam returned a contradictory default auto-route result.");
        }

        return new DefaultIntakeAutoRouteApplied(
            accepted.State,
            requirement.LogId,
            currentTick,
            accepted.Descriptor.FromState,
            accepted.Descriptor.ToState,
            accepted.Descriptor.PriorStateVersion,
            accepted.Descriptor.CurrentStateVersion);
    }
}
