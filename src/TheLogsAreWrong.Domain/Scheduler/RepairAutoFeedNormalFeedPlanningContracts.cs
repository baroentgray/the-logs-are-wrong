using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Scheduler;

public sealed class RepairAutoFeedNormalFeedPlanningService
{
    private readonly NormalFeedPlanningService _normalPlanning = new();

    public NormalFeedPlanningResult Plan(
        ShiftRuntimeState state,
        RepairPendingTransitionExecuted repairedAutoRoute,
        SchedulerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(repairedAutoRoute);
        ArgumentNullException.ThrowIfNull(configuration);

        ValidateRepairedAutoRoute(repairedAutoRoute);

        var retainedState = repairedAutoRoute.State;
        var established = _normalPlanning.Plan(retainedState, repairedAutoRoute.AppliedAt, configuration);
        if (established is NormalFeedScheduled scheduled)
        {
            if (ReferenceEquals(state, retainedState) && state.StateVersion == repairedAutoRoute.CurrentStateVersion)
            {
                return scheduled;
            }

            if (IsExactScheduledRetry(state, scheduled))
            {
                return new NormalFeedPlanningNoOp(state, NormalFeedPlanningNoOpReason.FeedAlreadyPending);
            }

            throw new InvalidOperationException("The supplied state does not match the established normal-feed output.");
        }

        if (!ReferenceEquals(state, retainedState) || state.StateVersion != repairedAutoRoute.CurrentStateVersion)
        {
            throw new InvalidOperationException("The supplied state is not the retained repaired state.");
        }

        return established;
    }

    private static bool IsExactScheduledRetry(ShiftRuntimeState state, NormalFeedScheduled scheduled) =>
        state.StateVersion == scheduled.CurrentStateVersion &&
        state.PendingFeed == scheduled.Schedule &&
        state.ValueEquals(scheduled.State);

    private static void ValidateRepairedAutoRoute(RepairPendingTransitionExecuted repairedAutoRoute)
    {
        if (repairedAutoRoute.State is not { } retainedState ||
            repairedAutoRoute.PendingTransition is not { } pending ||
            repairedAutoRoute.AppliedAt.IsDefault ||
            pending.LogId.IsDefault ||
            pending.Cause != JamCause.INTAKE_AUTOFEED_BLOCKED ||
            pending.FromState != LogState.AT_INTAKE ||
            pending.ToState != LogState.QUEUED_FOR_SAW ||
            repairedAutoRoute.LogId != pending.LogId ||
            repairedAutoRoute.Cause != pending.Cause ||
            repairedAutoRoute.Source != pending.FromState ||
            repairedAutoRoute.Destination != pending.ToState ||
            repairedAutoRoute.FollowUpRequirement != RepairPendingTransitionFollowUp.NormalFeedPlanningEvaluationRequired ||
            repairedAutoRoute.PriorStateVersion.IsDefault ||
            repairedAutoRoute.CurrentStateVersion.IsDefault ||
            !repairedAutoRoute.PriorStateVersion.TryNext(out var expectedCurrentVersion) ||
            repairedAutoRoute.CurrentStateVersion != expectedCurrentVersion ||
            retainedState.StateVersion != repairedAutoRoute.CurrentStateVersion)
        {
            throw new ArgumentException("The repaired auto-route descriptor is not an accepted intake transition.", nameof(repairedAutoRoute));
        }

        if (!retainedState.TryGetLog(repairedAutoRoute.LogId, out var owner) ||
            owner.State != LogState.QUEUED_FOR_SAW)
        {
            throw new InvalidOperationException("The repaired auto-route owner is not retained at the saw queue.");
        }

        if (retainedState.Line is not { } line ||
            line.State != LineState.LINE_CLEAR ||
            line.Cause is not null ||
            line.PendingLogId is not null ||
            line.ActiveRepairHold is not null)
        {
            throw new InvalidOperationException("The retained line condition is not clear.");
        }

        if (retainedState.ActiveIntakeDeadline is not null ||
            retainedState.GetNodeOccupancy(NodeId.INTAKE) != 0)
        {
            throw new InvalidOperationException("The repaired auto-route has retained intake obligations.");
        }
    }
}
