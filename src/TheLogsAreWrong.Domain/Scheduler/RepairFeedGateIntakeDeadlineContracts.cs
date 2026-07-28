using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Scheduler;

public sealed class RepairFeedGateIntakeDeadlineStartService
{
    public IntakeDeadlineStartResult Start(
        ShiftRuntimeState state,
        RepairPendingTransitionExecuted repairedAdmission,
        ShiftProfile selectedProfile)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(repairedAdmission);
        ArgumentNullException.ThrowIfNull(selectedProfile);
        ValidateRepairedFeedGateAdmission(repairedAdmission);
        return IntakeDeadlineStartService.StartFromAcceptedAdmission(
            state,
            repairedAdmission.State,
            repairedAdmission.LogId,
            repairedAdmission.AppliedAt,
            repairedAdmission.CurrentStateVersion,
            selectedProfile);
    }

    private static void ValidateRepairedFeedGateAdmission(RepairPendingTransitionExecuted repairedAdmission)
    {
        if (repairedAdmission.State is null ||
            repairedAdmission.PendingTransition is not { } pending ||
            repairedAdmission.AppliedAt.IsDefault ||
            pending.LogId.IsDefault ||
            pending.Cause != JamCause.FEED_GATE_BLOCKED ||
            pending.FromState != LogState.AT_FEED_GATE ||
            pending.ToState != LogState.AT_INTAKE ||
            repairedAdmission.LogId != pending.LogId ||
            repairedAdmission.Cause != pending.Cause ||
            repairedAdmission.Source != pending.FromState ||
            repairedAdmission.Destination != pending.ToState ||
            repairedAdmission.FollowUpRequirement != RepairPendingTransitionFollowUp.IntakeDeadlineStartRequired ||
            repairedAdmission.PriorStateVersion.IsDefault ||
            repairedAdmission.CurrentStateVersion.IsDefault ||
            !repairedAdmission.PriorStateVersion.TryNext(out var expectedCurrentVersion) ||
            repairedAdmission.CurrentStateVersion != expectedCurrentVersion ||
            repairedAdmission.State.StateVersion != repairedAdmission.CurrentStateVersion)
        {
            throw new ArgumentException("Only an exact accepted repaired feed-gate admission can start an intake deadline.", nameof(repairedAdmission));
        }

        if (!repairedAdmission.State.TryGetLog(repairedAdmission.LogId, out var owner))
        {
            throw new InvalidOperationException("A repaired admission deadline owner must resolve in the retained result state.");
        }

        if (owner.State != LogState.AT_INTAKE)
        {
            throw new InvalidOperationException("A repaired admission deadline owner must be at intake.");
        }

        var line = repairedAdmission.State.Line;
        if (line is null ||
            line.State != LineState.LINE_CLEAR ||
            line.Cause is not null ||
            line.PendingLogId is not null ||
            line.ActiveRepairHold is not null)
        {
            throw new InvalidOperationException("A repaired admission deadline requires an exactly clear retained line.");
        }

        if (repairedAdmission.State.ActiveIntakeDeadline is not null)
        {
            throw new InvalidOperationException("A repaired admission result cannot already contain an active intake deadline.");
        }
    }
}
