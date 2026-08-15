using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Scheduler;

public enum FeedDueDisposition
{
    AdmittedToIntake,
    PlacedAtFeedGate
}

public enum FeedDueFollowUpRequirement
{
    IntakeDeadlineStartRequired,
    FeedGateJamDerivationRequired
}

public abstract record FeedDueResolutionResult(ShiftRuntimeState State);

public sealed record FeedDueNoPendingFeed(ShiftRuntimeState State) : FeedDueResolutionResult(State);

public sealed record FeedDueNotDueYet(ShiftRuntimeState State) : FeedDueResolutionResult(State);

public sealed record FeedDueLineNotClear(ShiftRuntimeState State) : FeedDueResolutionResult(State);

public sealed record FeedDueFeedGateOccupied(ShiftRuntimeState State) : FeedDueResolutionResult(State);

public sealed record FeedDueResolved : FeedDueResolutionResult
{
    public FeedDueResolved(
        ShiftRuntimeState state,
        PendingFeedSchedule consumedSchedule,
        ServerTick resolvedAt,
        FeedDueDisposition disposition,
        FeedDueFollowUpRequirement followUpRequirement,
        StateVersion priorStateVersion,
        StateVersion currentStateVersion)
        : base(state)
    {
        if (state is null) { throw new ArgumentNullException("state"); }
        if (consumedSchedule is null) { throw new ArgumentNullException("consumedSchedule"); }
        if (resolvedAt.IsDefault || priorStateVersion.IsDefault || currentStateVersion.IsDefault ||
            !Enum.IsDefined(typeof(FeedDueDisposition), disposition) || !Enum.IsDefined(typeof(FeedDueFollowUpRequirement), followUpRequirement))
        {
            throw new ArgumentException("Feed-due acceptance requires initialized exact values.");
        }

        var expectedFollowUp = disposition == FeedDueDisposition.AdmittedToIntake
            ? FeedDueFollowUpRequirement.IntakeDeadlineStartRequired
            : FeedDueFollowUpRequirement.FeedGateJamDerivationRequired;
        var expectedDestination = disposition == FeedDueDisposition.AdmittedToIntake
            ? LogState.AT_INTAKE
            : LogState.AT_FEED_GATE;
        if (followUpRequirement != expectedFollowUp || currentStateVersion != priorStateVersion.Next() || state.StateVersion != currentStateVersion || state.PendingFeed is not null || !state.TryGetLog(consumedSchedule.LogId, out var log) || log.State != expectedDestination)
        {
            throw new ArgumentException("Feed-due acceptance cannot represent contradictory state, disposition, or follow-up data.");
        }

        ConsumedSchedule = consumedSchedule;
        ResolvedAt = resolvedAt;
        Disposition = disposition;
        FollowUpRequirement = followUpRequirement;
        PriorStateVersion = priorStateVersion;
        CurrentStateVersion = currentStateVersion;
    }

    public PendingFeedSchedule ConsumedSchedule { get; }
    public ServerTick ResolvedAt { get; }
    public FeedDueDisposition Disposition { get; }
    public FeedDueFollowUpRequirement FollowUpRequirement { get; }
    public StateVersion PriorStateVersion { get; }
    public StateVersion CurrentStateVersion { get; }
}

public sealed class FeedDueResolutionService
{
    public FeedDueResolutionResult Resolve(ShiftRuntimeState state, ServerTick currentTick)
    {
        if (state is null) { throw new ArgumentNullException("state"); }
        if (currentTick.IsDefault)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Current feed-due tick must be initialized.");
        }

        if (state.PendingFeed is not { } pendingFeed)
        {
            return new FeedDueNoPendingFeed(state);
        }

        if (currentTick < pendingFeed.DueAt)
        {
            return new FeedDueNotDueYet(state);
        }

        if (state.Line.State != LineState.LINE_CLEAR)
        {
            return new FeedDueLineNotClear(state);
        }

        var intakeOccupied = state.GetNodeOccupancy(NodeId.INTAKE) != 0;
        var feedGateOccupied = state.GetNodeOccupancy(NodeId.FEED_GATE) != 0;
        if (intakeOccupied && feedGateOccupied)
        {
            return new FeedDueFeedGateOccupied(state);
        }

        var disposition = intakeOccupied
            ? FeedDueDisposition.PlacedAtFeedGate
            : FeedDueDisposition.AdmittedToIntake;
        var destination = disposition == FeedDueDisposition.AdmittedToIntake
            ? LogState.AT_INTAKE
            : LogState.AT_FEED_GATE;
        var followUp = disposition == FeedDueDisposition.AdmittedToIntake
            ? FeedDueFollowUpRequirement.IntakeDeadlineStartRequired
            : FeedDueFollowUpRequirement.FeedGateJamDerivationRequired;
        var after = state.ConsumePendingFeed(destination);
        return new FeedDueResolved(after, pendingFeed, currentTick, disposition, followUp, state.StateVersion, after.StateVersion);
    }
}
