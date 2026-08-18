using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Scheduler;

public abstract record IntakeAutoFeedJamDerivationResult(ShiftRuntimeState State);
public sealed record IntakeAutoFeedJamExistingLineConditionRetained(ShiftRuntimeState State) : IntakeAutoFeedJamDerivationResult(State);
public sealed record IntakeAutoFeedJamOwnerMissing(ShiftRuntimeState State, LogId LogId) : IntakeAutoFeedJamDerivationResult(State);
public sealed record IntakeAutoFeedJamNoLongerApplicable(ShiftRuntimeState State, LogId LogId) : IntakeAutoFeedJamDerivationResult(State);
public sealed record IntakeAutoFeedJamBlockerCleared(ShiftRuntimeState State, LogId LogId) : IntakeAutoFeedJamDerivationResult(State);
public enum IntakeAutoFeedJamDefensiveFailureReason { InvalidBlockedDescriptor, CurrentStatePrecedesBlocked, DivergentSameVersion, AttemptTickPrecedesLine, CurrentShapeInvalid, JamEntryRejected }
public sealed record IntakeAutoFeedJamDefensiveFailure(ShiftRuntimeState State, IntakeAutoFeedJamDefensiveFailureReason Reason) : IntakeAutoFeedJamDerivationResult(State);
public sealed record IntakeAutoFeedJamEntered(ShiftRuntimeState State, LogId LogId, ServerTick EnteredAt, JamCause Cause, StateVersion PriorStateVersion, StateVersion CurrentStateVersion) : IntakeAutoFeedJamDerivationResult(State);

public sealed class IntakeAutoFeedJamDerivationService
{
    private readonly LineJamEntryService _entry = new();

    public IntakeAutoFeedJamDerivationResult Derive(ShiftRuntimeState state, DefaultIntakeAutoRouteBlocked blocked)
    {
        if (state is null) { throw new ArgumentNullException("state"); }
        if (blocked is null) { throw new ArgumentNullException("blocked"); }
        if (!ValidBlockedShape(blocked)) return new IntakeAutoFeedJamDefensiveFailure(state, IntakeAutoFeedJamDefensiveFailureReason.InvalidBlockedDescriptor);
        if (state.StateVersion < blocked.State.StateVersion) return new IntakeAutoFeedJamDefensiveFailure(state, IntakeAutoFeedJamDefensiveFailureReason.CurrentStatePrecedesBlocked);
        if (state.StateVersion == blocked.State.StateVersion && !ReferenceEquals(state, blocked.State)) return new IntakeAutoFeedJamDefensiveFailure(state, IntakeAutoFeedJamDefensiveFailureReason.DivergentSameVersion);
        if (blocked.AttemptedAt < state.Line.EnteredAt) return new IntakeAutoFeedJamDefensiveFailure(state, IntakeAutoFeedJamDefensiveFailureReason.AttemptTickPrecedesLine);
        if (state.Line.State != LineState.LINE_CLEAR) return new IntakeAutoFeedJamExistingLineConditionRetained(state);
        if (!state.TryGetLog(blocked.LogId, out var owner)) return new IntakeAutoFeedJamOwnerMissing(state, blocked.LogId);
        if (owner.State != LogState.AT_INTAKE) return new IntakeAutoFeedJamNoLongerApplicable(state, blocked.LogId);
        if (state.ActiveIntakeDeadline is not null) throw new InvalidOperationException("An intake auto-feed jam cannot follow an active deadline.");
        if (state.GetNodeOccupancy(NodeId.SAW_QUEUE) == 0) return new IntakeAutoFeedJamBlockerCleared(state, blocked.LogId);
        var intake = state.Logs.Where(log => log.State == LogState.AT_INTAKE).Select(log => log.LogId).ToArray();
        if (intake.Length != 1 || intake[0] != blocked.LogId) return new IntakeAutoFeedJamDefensiveFailure(state, IntakeAutoFeedJamDefensiveFailureReason.CurrentShapeInvalid);
        var entry = _entry.Enter(state, JamCause.INTAKE_AUTOFEED_BLOCKED, blocked.AttemptedAt);
        if (entry is not LineJamEntered entered || entered.State.Line.PendingLogId != blocked.LogId || entered.State.Line.Cause != JamCause.INTAKE_AUTOFEED_BLOCKED || entered.State.StateVersion != state.StateVersion.Next()) return new IntakeAutoFeedJamDefensiveFailure(state, IntakeAutoFeedJamDefensiveFailureReason.JamEntryRejected);
        return new IntakeAutoFeedJamEntered(entered.State, blocked.LogId, blocked.AttemptedAt, JamCause.INTAKE_AUTOFEED_BLOCKED, state.StateVersion, entered.State.StateVersion);
    }

    private static bool ValidBlockedShape(DefaultIntakeAutoRouteBlocked blocked)
    {
        if (blocked.State is null || blocked.LogId.IsDefault || blocked.AttemptedAt.IsDefault || blocked.Reason != DefaultIntakeAutoRouteBlockReason.SawQueueOccupied || blocked.FollowUp != DefaultIntakeAutoRouteFollowUp.IntakeAutoFeedJamDerivationRequired) return false;
        return blocked.State.Line.State == LineState.LINE_CLEAR && blocked.State.ActiveIntakeDeadline is null && blocked.State.GetNodeOccupancy(NodeId.SAW_QUEUE) != 0 && blocked.State.TryGetLog(blocked.LogId, out var owner) && owner.State == LogState.AT_INTAKE;
    }
}
