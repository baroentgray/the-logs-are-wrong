using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Scheduler;

public enum FeedScheduleKind
{
    INITIAL,
    NORMAL,
    EARLY
}

public sealed record PendingFeedSchedule
{
    public PendingFeedSchedule(
        LogId logId,
        FeedScheduleKind kind,
        ServerTick scheduledAt,
        SimulationDuration delay,
        IntentId? causedByIntentId)
    {
        if (logId.IsDefault || !Enum.IsDefined(kind) || scheduledAt.IsDefault || delay.IsDefault)
        {
            throw new ArgumentException("A pending feed requires initialized identity, kind, timing, and delay.");
        }

        if (kind == FeedScheduleKind.INITIAL)
        {
            if (causedByIntentId is not null)
            {
                throw new ArgumentException("An initial feed cannot retain intent causation.", nameof(causedByIntentId));
            }
        }
        else if (kind == FeedScheduleKind.NORMAL)
        {
            if (delay <= SimulationDuration.Zero || causedByIntentId is not null)
            {
                throw new ArgumentException("A normal feed requires a positive delay and no intent causation.");
            }
        }
        else
        {
            if (delay <= SimulationDuration.Zero || causedByIntentId is not { } intentId || intentId.IsDefault)
            {
                throw new ArgumentException("An early feed requires a positive delay and initialized intent causation.");
            }
        }

        LogId = logId;
        Kind = kind;
        ScheduledAt = scheduledAt;
        Delay = delay;
        DueAt = scheduledAt + delay;
        CausedByIntentId = causedByIntentId;
    }

    public LogId LogId { get; }
    public FeedScheduleKind Kind { get; }
    public ServerTick ScheduledAt { get; }
    public ServerTick DueAt { get; }
    public SimulationDuration Delay { get; }
    public IntentId? CausedByIntentId { get; }
}

public static class FeedPlanningIntentActions
{
    public static readonly IntentActionId RequestEarlyFeed = IntentActionId.From("request_early_feed");
}

public static class FeedPlanningTargets
{
    public static readonly TargetId FeedGate = TargetId.From("FEED_GATE");
}

public abstract record InitialFeedPlanningResult(ShiftRuntimeState State);

public sealed record InitialFeedScheduled(
    ShiftRuntimeState State,
    PendingFeedSchedule Schedule,
    StateVersion PriorStateVersion,
    StateVersion CurrentStateVersion) : InitialFeedPlanningResult(State);

public sealed record InitialFeedPlanningNoOp(ShiftRuntimeState State, InitialFeedPlanningNoOpReason Reason) : InitialFeedPlanningResult(State);

public enum InitialFeedPlanningNoOpReason
{
    CurrentTickNotZero,
    PendingFeedExists,
    NoMoreLogs,
    StateNotPristine
}

public sealed class InitialFeedPlanningService
{
    public InitialFeedPlanningResult Plan(ShiftRuntimeState state, ServerTick currentTick, SchedulerConfiguration configuration)
    {
        FeedPlanningGuards.Validate(state, currentTick, configuration);
        if (currentTick != ServerTick.Zero)
        {
            return new InitialFeedPlanningNoOp(state, InitialFeedPlanningNoOpReason.CurrentTickNotZero);
        }

        if (state.PendingFeed is not null)
        {
            return new InitialFeedPlanningNoOp(state, InitialFeedPlanningNoOpReason.PendingFeedExists);
        }

        if (!state.TryGetNextScheduledLog(out var next))
        {
            return new InitialFeedPlanningNoOp(state, InitialFeedPlanningNoOpReason.NoMoreLogs);
        }

        if (!state.IsPristineForInitialFeedPlanning)
        {
            return new InitialFeedPlanningNoOp(state, InitialFeedPlanningNoOpReason.StateNotPristine);
        }

        var schedule = new PendingFeedSchedule(
            next.LogId,
            FeedScheduleKind.INITIAL,
            currentTick,
            FeedPlanningGuards.InitialDelay(configuration),
            null);
        var after = state.WithPendingFeed(schedule, null);
        return new InitialFeedScheduled(after, schedule, state.StateVersion, after.StateVersion);
    }
}

public abstract record NormalFeedPlanningResult(ShiftRuntimeState State);

public sealed record NormalFeedScheduled(
    ShiftRuntimeState State,
    PendingFeedSchedule Schedule,
    StateVersion PriorStateVersion,
    StateVersion CurrentStateVersion) : NormalFeedPlanningResult(State);

public sealed record NormalFeedPlanningNoOp(ShiftRuntimeState State, NormalFeedPlanningNoOpReason Reason) : NormalFeedPlanningResult(State);

public enum NormalFeedPlanningNoOpReason
{
    InitialPlanningRequired,
    IntakeOccupied,
    FeedGateOccupied,
    FeedAlreadyPending,
    LineNotClear,
    NoMoreLogs
}

public sealed class NormalFeedPlanningService
{
    public NormalFeedPlanningResult Plan(ShiftRuntimeState state, ServerTick currentTick, SchedulerConfiguration configuration)
    {
        FeedPlanningGuards.Validate(state, currentTick, configuration);
        if (state.StateVersion == StateVersion.Zero)
        {
            return new NormalFeedPlanningNoOp(state, NormalFeedPlanningNoOpReason.InitialPlanningRequired);
        }

        if (state.GetNodeOccupancy(NodeId.INTAKE) != 0)
        {
            return new NormalFeedPlanningNoOp(state, NormalFeedPlanningNoOpReason.IntakeOccupied);
        }

        if (state.GetNodeOccupancy(NodeId.FEED_GATE) != 0)
        {
            return new NormalFeedPlanningNoOp(state, NormalFeedPlanningNoOpReason.FeedGateOccupied);
        }

        if (state.PendingFeed is not null)
        {
            return new NormalFeedPlanningNoOp(state, NormalFeedPlanningNoOpReason.FeedAlreadyPending);
        }

        if (state.Line.State != LineState.LINE_CLEAR)
        {
            return new NormalFeedPlanningNoOp(state, NormalFeedPlanningNoOpReason.LineNotClear);
        }

        if (!state.TryGetNextScheduledLog(out var next))
        {
            return new NormalFeedPlanningNoOp(state, NormalFeedPlanningNoOpReason.NoMoreLogs);
        }

        var schedule = new PendingFeedSchedule(
            next.LogId,
            FeedScheduleKind.NORMAL,
            currentTick,
            FeedPlanningGuards.PositiveDelay(configuration.NormalFeedDelaySeconds, nameof(configuration.NormalFeedDelaySeconds)),
            null);
        var after = state.WithPendingFeed(schedule, null);
        return new NormalFeedScheduled(after, schedule, state.StateVersion, after.StateVersion);
    }
}

public abstract record EarlyFeedIntentResult(ShiftRuntimeState State);

public sealed record EarlyFeedScheduled(
    ShiftRuntimeState State,
    PendingFeedSchedule Schedule,
    StateVersion PriorStateVersion,
    StateVersion CurrentStateVersion,
    ActorId AuthoritativeActor) : EarlyFeedIntentResult(State);

public sealed record EarlyFeedIntentRejected(ShiftRuntimeState State, RejectionReason Reason) : EarlyFeedIntentResult(State);

public sealed record DuplicateEarlyFeedIntentIgnored(ShiftRuntimeState State, IntentId IntentId) : EarlyFeedIntentResult(State);

public sealed record UnsupportedEarlyFeedIntent(ShiftRuntimeState State, EarlyFeedIntentUnsupportedReason Reason) : EarlyFeedIntentResult(State);

public enum EarlyFeedIntentUnsupportedReason
{
    Action,
    Target,
    Parameters
}

public sealed class EarlyFeedIntentHandler
{
    public EarlyFeedIntentResult Handle(
        ShiftRuntimeState state,
        IntentEnvelope intent,
        ActorId? authoritativeActor,
        ServerTick currentTick,
        SchedulerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(intent);
        FeedPlanningGuards.Validate(state, currentTick, configuration);

        if (intent.ShiftId != state.ShiftId)
        {
            return new EarlyFeedIntentRejected(state, RejectionReason.SHIFT_MISMATCH);
        }

        if (authoritativeActor is not { } actor || actor.IsDefault)
        {
            return new EarlyFeedIntentRejected(state, RejectionReason.ACTOR_NOT_BOUND);
        }

        if (intent.ExpectedStateVersion != state.StateVersion)
        {
            return new EarlyFeedIntentRejected(state, RejectionReason.STALE_STATE_VERSION);
        }

        if (state.ProcessedIntentIds.Contains(intent.IntentId))
        {
            return new DuplicateEarlyFeedIntentIgnored(state, intent.IntentId);
        }

        if (intent.Action != FeedPlanningIntentActions.RequestEarlyFeed)
        {
            return new UnsupportedEarlyFeedIntent(state, EarlyFeedIntentUnsupportedReason.Action);
        }

        if (intent.TargetId != FeedPlanningTargets.FeedGate)
        {
            return new UnsupportedEarlyFeedIntent(state, EarlyFeedIntentUnsupportedReason.Target);
        }

        if (intent.Parameters is not NoIntentParameters)
        {
            return new UnsupportedEarlyFeedIntent(state, EarlyFeedIntentUnsupportedReason.Parameters);
        }

        if (!state.TryGetNextScheduledLog(out var next))
        {
            return new EarlyFeedIntentRejected(state, RejectionReason.NO_MORE_LOGS);
        }

        if (state.PendingFeed is not null)
        {
            return new EarlyFeedIntentRejected(state, RejectionReason.FEED_ALREADY_PENDING);
        }

        if (state.GetNodeOccupancy(NodeId.FEED_GATE) != 0)
        {
            return new EarlyFeedIntentRejected(state, RejectionReason.FEED_GATE_OCCUPIED);
        }

        if (state.Line.State != LineState.LINE_CLEAR)
        {
            return new EarlyFeedIntentRejected(state, RejectionReason.LINE_NOT_CLEAR);
        }

        var schedule = new PendingFeedSchedule(
            next.LogId,
            FeedScheduleKind.EARLY,
            currentTick,
            FeedPlanningGuards.PositiveDelay(configuration.EarlyFeedDelaySeconds, nameof(configuration.EarlyFeedDelaySeconds)),
            intent.IntentId);
        var after = state.WithPendingFeed(schedule, intent.IntentId);
        return new EarlyFeedScheduled(after, schedule, state.StateVersion, after.StateVersion, actor);
    }
}

internal static class FeedPlanningGuards
{
    internal static void Validate(ShiftRuntimeState state, ServerTick currentTick, SchedulerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(configuration);
        if (currentTick.IsDefault)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Current scheduler tick must be initialized.");
        }
    }

    internal static SimulationDuration InitialDelay(SchedulerConfiguration configuration)
    {
        if (configuration.InitialAdmissionDelaySeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration.InitialAdmissionDelaySeconds), "Initial feed delay cannot be negative.");
        }

        return SimulationDuration.FromTicks(configuration.InitialAdmissionDelaySeconds);
    }

    internal static SimulationDuration PositiveDelay(int ticks, string parameterName) => ticks > 0
        ? SimulationDuration.FromTicks(ticks)
        : throw new ArgumentOutOfRangeException(parameterName, "Feed delay must be positive.");
}
