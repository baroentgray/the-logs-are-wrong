using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Scheduler;

public abstract record FeedGateJamDerivationResult(ShiftRuntimeState State);

public sealed record FeedGateJamDerivationLineNotClear(ShiftRuntimeState State) : FeedGateJamDerivationResult(State);

public sealed record FeedGateJamDerivationNoFeedGateLog(ShiftRuntimeState State) : FeedGateJamDerivationResult(State);

public sealed record FeedGateJamDerivationIntakeAvailable(ShiftRuntimeState State) : FeedGateJamDerivationResult(State);

public sealed record FeedGateJamDerivationDefensiveFailure(
    ShiftRuntimeState State,
    FeedGateJamDerivationFailureReason Reason) : FeedGateJamDerivationResult(State);

public enum FeedGateJamDerivationFailureReason
{
    FeedGateRuntimeShapeInvalid,
    JamEntryRejected
}

public sealed record FeedGateJamDerived : FeedGateJamDerivationResult
{
    public FeedGateJamDerived(
        ShiftRuntimeState state,
        LogId blockedLogId,
        ServerTick enteredAt,
        JamCause cause,
        StateVersion priorStateVersion,
        StateVersion currentStateVersion)
        : base(state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (blockedLogId.IsDefault || enteredAt.IsDefault || priorStateVersion.IsDefault || currentStateVersion.IsDefault ||
            cause != JamCause.FEED_GATE_BLOCKED || currentStateVersion != priorStateVersion.Next() || state.StateVersion != currentStateVersion ||
            state.Line.State != LineState.LINE_JAMMED || state.Line.Cause != cause || state.Line.PendingLogId != blockedLogId ||
            state.Line.EnteredAt != enteredAt || state.Line.ActiveRepairHold is not null || !state.TryGetLog(blockedLogId, out var blockedLog) ||
            blockedLog.State != LogState.AT_FEED_GATE || state.GetNodeOccupancy(NodeId.INTAKE) == 0)
        {
            throw new ArgumentException("A feed-gate jam derivation must retain the exact accepted runtime state.");
        }

        BlockedLogId = blockedLogId;
        EnteredAt = enteredAt;
        Cause = cause;
        PriorStateVersion = priorStateVersion;
        CurrentStateVersion = currentStateVersion;
    }

    public LogId BlockedLogId { get; }
    public ServerTick EnteredAt { get; }
    public JamCause Cause { get; }
    public StateVersion PriorStateVersion { get; }
    public StateVersion CurrentStateVersion { get; }
}

public sealed class FeedGateJamDerivationService
{
    private readonly LineJamEntryService _lineJamEntry = new();

    public FeedGateJamDerivationResult Derive(ShiftRuntimeState state, ServerTick currentTick)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (currentTick.IsDefault || currentTick < state.Line.EnteredAt)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Current tick cannot precede line state entry.");
        }

        if (state.Line.State != LineState.LINE_CLEAR)
        {
            return new FeedGateJamDerivationLineNotClear(state);
        }

        var feedGateCandidates = state.Logs.Where(log => log.State == LogState.AT_FEED_GATE).Select(log => log.LogId).ToArray();
        if (feedGateCandidates.Length == 0)
        {
            return new FeedGateJamDerivationNoFeedGateLog(state);
        }

        if (state.GetNodeOccupancy(NodeId.INTAKE) == 0)
        {
            return new FeedGateJamDerivationIntakeAvailable(state);
        }

        if (feedGateCandidates.Length != 1 || state.GetNodeOccupancy(NodeId.FEED_GATE) != 1)
        {
            return new FeedGateJamDerivationDefensiveFailure(state, FeedGateJamDerivationFailureReason.FeedGateRuntimeShapeInvalid);
        }

        var entry = _lineJamEntry.Enter(state, JamCause.FEED_GATE_BLOCKED, currentTick);
        if (entry is not LineJamEntered entered)
        {
            return new FeedGateJamDerivationDefensiveFailure(state, FeedGateJamDerivationFailureReason.JamEntryRejected);
        }

        return new FeedGateJamDerived(
            entered.State,
            feedGateCandidates[0],
            currentTick,
            JamCause.FEED_GATE_BLOCKED,
            state.StateVersion,
            entered.State.StateVersion);
    }
}
