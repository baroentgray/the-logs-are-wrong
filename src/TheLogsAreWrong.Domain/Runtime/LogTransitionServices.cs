using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Logs;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Runtime;

public sealed record LogTransitionDescriptor(
    LogId LogId,
    LogState FromState,
    LogState ToState,
    StateVersion PriorStateVersion,
    StateVersion CurrentStateVersion,
    ActorId AuthoritativeActor);

public sealed record HostLogTransitionDescriptor(
    LogId LogId,
    LogState FromState,
    LogState ToState,
    StateVersion PriorStateVersion,
    StateVersion CurrentStateVersion);

public abstract record HostLogTransitionResult(ShiftRuntimeState State);

public sealed record HostLogTransitionAccepted(ShiftRuntimeState State, HostLogTransitionDescriptor Descriptor)
    : HostLogTransitionResult(State);

public sealed record HostLogTransitionRejected(ShiftRuntimeState State, HostLogTransitionFailure Failure)
    : HostLogTransitionResult(State);

public enum HostLogTransitionFailure
{
    TargetNotFound,
    IllegalTransition,
    TargetOccupied
}

/// <summary>Host-only seam for future scheduler and saw orchestration. It performs no timing, event emission, or authority binding.</summary>
public sealed class HostLogTransitionService
{
    public HostLogTransitionResult Apply(ShiftRuntimeState state, LogId logId, LogState toState)
    {
        ArgumentNullException.ThrowIfNull(state);
        var attempt = LogTransitionExecutor.Apply(state, logId, toState, null);
        return attempt switch
        {
            SharedTransitionAccepted accepted => new HostLogTransitionAccepted(
                accepted.State,
                new HostLogTransitionDescriptor(logId, accepted.FromState, toState, state.StateVersion, accepted.State.StateVersion)),
            SharedTransitionRejected rejected => new HostLogTransitionRejected(state, rejected.Failure switch
            {
                SharedTransitionFailure.TargetNotFound => HostLogTransitionFailure.TargetNotFound,
                SharedTransitionFailure.IllegalTransition => HostLogTransitionFailure.IllegalTransition,
                SharedTransitionFailure.TargetOccupied => HostLogTransitionFailure.TargetOccupied,
                _ => throw new ArgumentOutOfRangeException()
            }),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

public abstract record ManualLogIntentResult(ShiftRuntimeState State);

public sealed record ManualLogIntentAccepted(ShiftRuntimeState State, LogTransitionDescriptor Transition)
    : ManualLogIntentResult(State);

public sealed record ManualLogIntentRejected(ShiftRuntimeState State, RejectionReason Reason)
    : ManualLogIntentResult(State);

public sealed record DuplicateIntentIgnored(ShiftRuntimeState State, IntentId IntentId)
    : ManualLogIntentResult(State);

public sealed record UnsupportedIntentAction(ShiftRuntimeState State, IntentActionId Action)
    : ManualLogIntentResult(State);

public sealed class ManualLogIntentHandler
{
    public ManualLogIntentResult Handle(ShiftRuntimeState state, IntentEnvelope intent, ActorId? authoritativeActor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(intent);

        if (intent.ShiftId != state.ShiftId)
        {
            return new ManualLogIntentRejected(state, RejectionReason.SHIFT_MISMATCH);
        }

        if (authoritativeActor is not { } boundActor || boundActor.IsDefault)
        {
            return new ManualLogIntentRejected(state, RejectionReason.ACTOR_NOT_BOUND);
        }

        if (intent.ExpectedStateVersion != state.StateVersion)
        {
            return new ManualLogIntentRejected(state, RejectionReason.STALE_STATE_VERSION);
        }

        if (state.ProcessedIntentIds.Contains(intent.IntentId))
        {
            return new DuplicateIntentIgnored(state, intent.IntentId);
        }

        if (!state.TryGetLog(intent.TargetId, out var target))
        {
            return new ManualLogIntentRejected(state, RejectionReason.TARGET_NOT_FOUND);
        }

        if (!ManualLogRouting.TryResolve(intent.Action, out var route))
        {
            return new UnsupportedIntentAction(state, intent.Action);
        }

        if (!route.Allows(target.State))
        {
            return new ManualLogIntentRejected(state, RejectionReason.TARGET_NOT_IN_STATE);
        }

        var attempt = LogTransitionExecutor.Apply(state, target.LogId, route.TargetState, intent.IntentId);
        return attempt switch
        {
            SharedTransitionAccepted accepted => new ManualLogIntentAccepted(
                accepted.State,
                new LogTransitionDescriptor(target.LogId, accepted.FromState, route.TargetState, state.StateVersion, accepted.State.StateVersion, boundActor)),
            SharedTransitionRejected { Failure: SharedTransitionFailure.TargetOccupied } => new ManualLogIntentRejected(state, RejectionReason.TARGET_OCCUPIED),
            SharedTransitionRejected { Failure: SharedTransitionFailure.TargetNotFound or SharedTransitionFailure.IllegalTransition } => new ManualLogIntentRejected(state, RejectionReason.TARGET_NOT_IN_STATE),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

internal readonly record struct ManualLogRoute(LogState RequiredSourceState, LogState TargetState, LogState? AlternateSourceState = null)
{
    internal bool Allows(LogState sourceState) => sourceState == RequiredSourceState || sourceState == AlternateSourceState;
}

internal static class ManualLogRouting
{
    internal static bool TryResolve(IntentActionId action, out ManualLogRoute route)
    {
        if (action == LogIntentActions.RouteToProcedure)
        {
            route = new ManualLogRoute(LogState.AT_INTAKE, LogState.AT_PROCEDURE);
            return true;
        }

        if (action == LogIntentActions.ReturnFromProcedure)
        {
            route = new ManualLogRoute(LogState.AT_PROCEDURE, LogState.AT_INTAKE);
            return true;
        }

        if (action == LogIntentActions.RouteToSawQueue)
        {
            route = new ManualLogRoute(LogState.AT_INTAKE, LogState.QUEUED_FOR_SAW);
            return true;
        }

        if (action == LogIntentActions.WriteOff)
        {
            route = new ManualLogRoute(LogState.AT_INTAKE, LogState.HELD_WRITTEN_OFF, LogState.AT_PROCEDURE);
            return true;
        }

        route = default;
        return false;
    }
}

internal enum SharedTransitionFailure
{
    TargetNotFound,
    IllegalTransition,
    TargetOccupied
}

internal abstract record SharedTransitionResult;

internal sealed record SharedTransitionAccepted(ShiftRuntimeState State, LogState FromState) : SharedTransitionResult;

internal sealed record SharedTransitionRejected(SharedTransitionFailure Failure) : SharedTransitionResult;

internal static class LogTransitionExecutor
{
    internal static SharedTransitionResult Apply(ShiftRuntimeState state, LogId logId, LogState toState, IntentId? processedIntentId)
    {
        if (!state.TryGetLog(logId, out var target))
        {
            return new SharedTransitionRejected(SharedTransitionFailure.TargetNotFound);
        }

        if (!LogTransitionPolicy.IsAllowed(target.State, toState))
        {
            return new SharedTransitionRejected(SharedTransitionFailure.IllegalTransition);
        }

        if (!state.CanEnter(toState))
        {
            return new SharedTransitionRejected(SharedTransitionFailure.TargetOccupied);
        }

        return new SharedTransitionAccepted(state.ApplyTransition(logId, toState, processedIntentId), target.State);
    }
}
