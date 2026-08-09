using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Line;

public sealed record ActiveRepairHold
{
    public ActiveRepairHold(ServerTick startedAt, ServerTick dueAt, SimulationDuration duration)
    {
        if (startedAt.IsDefault || dueAt.IsDefault || duration.IsDefault || duration <= SimulationDuration.Zero)
        {
            throw new ArgumentException("Repair hold timing must be initialized and positive.");
        }

        if (dueAt != startedAt + duration)
        {
            throw new ArgumentException("Repair hold due tick must equal start tick plus duration.", nameof(dueAt));
        }

        StartedAt = startedAt;
        DueAt = dueAt;
        Duration = duration;
    }

    public ServerTick StartedAt { get; }
    public ServerTick DueAt { get; }
    public SimulationDuration Duration { get; }
}

public sealed record LineRuntimeState
{
    public LineRuntimeState(LineState state, ServerTick enteredAt, JamCause? cause, LogId? pendingLogId, ActiveRepairHold? activeRepairHold)
    {
        if (!Enum.IsDefined(state) || enteredAt.IsDefault)
        {
            throw new ArgumentException("Line state and entry tick must be initialized.");
        }

        if (state == LineState.LINE_CLEAR)
        {
            if (cause is not null || pendingLogId is not null || activeRepairHold is not null)
            {
                throw new ArgumentException("A clear line cannot retain jam or repair data.");
            }
        }
        else
        {
            if (!TryGetActiveCause(cause, out _) || pendingLogId is not { } pending || pending.IsDefault)
            {
                throw new ArgumentException("A jammed line requires an exact active cause and pending log.");
            }

            if (state == LineState.LINE_JAMMED && activeRepairHold is not null)
            {
                throw new ArgumentException("A jammed line cannot retain an active repair hold.");
            }

            if (state == LineState.REPAIRING && activeRepairHold is null)
            {
                throw new ArgumentException("A repairing line requires an active repair hold.");
            }

            if (state == LineState.REPAIRING && activeRepairHold is not null && activeRepairHold.StartedAt != enteredAt)
            {
                throw new ArgumentException("A repairing line must enter when its repair hold starts.");
            }
        }

        State = state;
        EnteredAt = enteredAt;
        Cause = cause;
        PendingLogId = pendingLogId;
        ActiveRepairHold = activeRepairHold;
    }

    public LineState State { get; }
    public ServerTick EnteredAt { get; }
    public JamCause? Cause { get; }
    public LogId? PendingLogId { get; }
    public ActiveRepairHold? ActiveRepairHold { get; }

    internal static bool TryGetActiveCause(JamCause? cause, out JamCause activeCause)
    {
        if (cause is { } value && Enum.IsDefined(value))
        {
            activeCause = value;
            return true;
        }

        activeCause = default;
        return false;
    }
}

public sealed record PendingLineTransitionDescriptor
{
    public PendingLineTransitionDescriptor(LogId logId, LogState fromState, LogState toState, JamCause cause)
    {
        if (logId.IsDefault || !Enum.IsDefined(fromState) || !Enum.IsDefined(toState) || !LineRuntimeState.TryGetActiveCause(cause, out _))
        {
            throw new ArgumentException("Pending line transition must contain initialized exact values.");
        }

        var expected = cause switch
        {
            JamCause.FEED_GATE_BLOCKED => (LogState.AT_FEED_GATE, LogState.AT_INTAKE),
            JamCause.INTAKE_AUTOFEED_BLOCKED => (LogState.AT_INTAKE, LogState.QUEUED_FOR_SAW),
            _ => throw new ArgumentOutOfRangeException(nameof(cause))
        };
        if (fromState != expected.Item1 || toState != expected.Item2)
        {
            throw new ArgumentException("Pending line transition must match its jam cause.");
        }

        LogId = logId;
        FromState = fromState;
        ToState = toState;
        Cause = cause;
    }

    public LogId LogId { get; }
    public LogState FromState { get; }
    public LogState ToState { get; }
    public JamCause Cause { get; }
}

public abstract record LineJamEntryResult(ShiftRuntimeState State);
public sealed record LineJamEntered(ShiftRuntimeState State) : LineJamEntryResult(State);
public sealed record LineJamEntryRejected(ShiftRuntimeState State, LineJamEntryRejectionReason Reason) : LineJamEntryResult(State);
public sealed record LineJamEntryFailed(ShiftRuntimeState State, LineJamEntryFailureReason Reason) : LineJamEntryResult(State);

public enum LineJamEntryRejectionReason
{
    LINE_NOT_CLEAR,
    CAUSE_RUNTIME_SHAPE_INVALID
}

public enum LineJamEntryFailureReason
{
    LineStateInvalid
}

public sealed class LineJamEntryService
{
    public LineJamEntryResult Enter(ShiftRuntimeState state, JamCause cause, ServerTick currentTick)
    {
        LineGuards.ValidateCurrent(state, currentTick);
        if (state.Line.State != LineState.LINE_CLEAR)
        {
            return new LineJamEntryRejected(state, LineJamEntryRejectionReason.LINE_NOT_CLEAR);
        }

        if (!LineRuntimeState.TryGetActiveCause(cause, out var activeCause) || !TryDerivePendingLog(state, activeCause, out var pendingLogId))
        {
            return new LineJamEntryRejected(state, LineJamEntryRejectionReason.CAUSE_RUNTIME_SHAPE_INVALID);
        }

        var jammed = new LineRuntimeState(LineState.LINE_JAMMED, currentTick, activeCause, pendingLogId, null);
        return new LineJamEntered(state.WithLine(jammed));
    }

    private static bool TryDerivePendingLog(ShiftRuntimeState state, JamCause cause, out LogId pendingLogId)
    {
        var sourceState = cause == JamCause.FEED_GATE_BLOCKED ? LogState.AT_FEED_GATE : LogState.AT_INTAKE;
        var blocker = cause == JamCause.FEED_GATE_BLOCKED ? NodeId.INTAKE : NodeId.SAW_QUEUE;
        var candidates = state.Logs.Where(log => log.State == sourceState).Select(log => log.LogId).ToArray();
        if (candidates.Length != 1 || state.GetNodeOccupancy(blocker) == 0)
        {
            pendingLogId = default;
            return false;
        }

        pendingLogId = candidates[0];
        return true;
    }
}

public abstract record LineRepairStartResult(ShiftRuntimeState State);
public sealed record LineRepairStarted(ShiftRuntimeState State, ActiveRepairHold Hold) : LineRepairStartResult(State);
public sealed record LineRepairStartRejected(ShiftRuntimeState State, LineRepairStartRejectionReason Reason) : LineRepairStartResult(State);
public sealed record LineRepairStartFailed(ShiftRuntimeState State, LineRepairStartFailureReason Reason) : LineRepairStartResult(State);

public enum LineRepairStartRejectionReason
{
    NO_ACTIVE_JAM,
    REPAIR_ALREADY_ACTIVE
}

public enum LineRepairStartFailureReason
{
    LineStateInvalid
}

public sealed class LineRepairStartService
{
    public LineRepairStartResult Start(ShiftRuntimeState state, ServerTick currentTick, SchedulerConfiguration configuration)
        => StartCore(state, currentTick, configuration, null);

    /// <summary>Starts repair for one already-authoritative accepted intent, atomically retaining its exact identity.</summary>
    internal LineRepairStartResult StartForAuthoritativeIntent(
        ShiftRuntimeState state,
        ServerTick currentTick,
        SchedulerConfiguration configuration,
        IntentId processedIntentId)
    {
        if (processedIntentId.IsDefault)
        {
            throw new ArgumentException("Processed intent identifier must be initialized.", nameof(processedIntentId));
        }

        return StartCore(state, currentTick, configuration, processedIntentId);
    }

    private static LineRepairStartResult StartCore(
        ShiftRuntimeState state,
        ServerTick currentTick,
        SchedulerConfiguration configuration,
        IntentId? processedIntentId)
    {
        LineGuards.ValidateCurrent(state, currentTick);
        ArgumentNullException.ThrowIfNull(configuration);
        if (state.Line.State == LineState.LINE_CLEAR)
        {
            return new LineRepairStartRejected(state, LineRepairStartRejectionReason.NO_ACTIVE_JAM);
        }

        if (state.Line.State == LineState.REPAIRING)
        {
            return new LineRepairStartRejected(state, LineRepairStartRejectionReason.REPAIR_ALREADY_ACTIVE);
        }

        if (state.Line.State != LineState.LINE_JAMMED || !LineRuntimeState.TryGetActiveCause(state.Line.Cause, out var cause) || state.Line.PendingLogId is not { } pendingLogId)
        {
            return new LineRepairStartFailed(state, LineRepairStartFailureReason.LineStateInvalid);
        }

        var duration = LineGuards.PositiveDuration(configuration.RepairHoldSeconds, nameof(configuration.RepairHoldSeconds));
        var hold = new ActiveRepairHold(currentTick, currentTick + duration, duration);
        var repairing = new LineRuntimeState(LineState.REPAIRING, currentTick, cause, pendingLogId, hold);
        return new LineRepairStarted(state.WithLineAndProcessedIntent(repairing, processedIntentId), hold);
    }
}

public abstract record LineRepairDueCompletionResult(ShiftRuntimeState State);
public sealed record LineRepairNoActive(ShiftRuntimeState State) : LineRepairDueCompletionResult(State);
public sealed record LineRepairNotDue(ShiftRuntimeState State) : LineRepairDueCompletionResult(State);
public sealed record LineRepairBlockingConditionRemains(ShiftRuntimeState State) : LineRepairDueCompletionResult(State);
public sealed record LineRepairCompleted(ShiftRuntimeState State, PendingLineTransitionDescriptor? PendingTransition) : LineRepairDueCompletionResult(State);
public sealed record LineRepairDueCompletionFailed(ShiftRuntimeState State, LineRepairDueCompletionFailureReason Reason) : LineRepairDueCompletionResult(State);

public enum LineRepairDueCompletionFailureReason
{
    LineStateInvalid
}

public sealed class LineRepairDueCompletionService
{
    public LineRepairDueCompletionResult CompleteDue(ShiftRuntimeState state, ServerTick currentTick)
    {
        LineGuards.ValidateCurrent(state, currentTick);
        if (state.Line.ActiveRepairHold is not { } hold)
        {
            return new LineRepairNoActive(state);
        }

        if (currentTick < hold.StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Current tick cannot precede an active repair hold.");
        }

        if (currentTick < hold.DueAt)
        {
            return new LineRepairNotDue(state);
        }

        if (state.Line.State != LineState.REPAIRING || !LineRuntimeState.TryGetActiveCause(state.Line.Cause, out var cause) || state.Line.PendingLogId is not { } pendingLogId)
        {
            return new LineRepairDueCompletionFailed(state, LineRepairDueCompletionFailureReason.LineStateInvalid);
        }

        if (state.GetNodeOccupancy(cause == JamCause.FEED_GATE_BLOCKED ? NodeId.INTAKE : NodeId.SAW_QUEUE) != 0)
        {
            return new LineRepairBlockingConditionRemains(state);
        }

        PendingLineTransitionDescriptor? descriptor = null;
        if (state.TryGetLog(pendingLogId, out var pendingLog) && pendingLog.State == ExpectedSource(cause))
        {
            descriptor = new PendingLineTransitionDescriptor(pendingLogId, pendingLog.State, ExpectedTarget(cause), cause);
        }

        var clear = new LineRuntimeState(LineState.LINE_CLEAR, currentTick, null, null, null);
        return new LineRepairCompleted(state.WithLine(clear), descriptor);
    }

    private static LogState ExpectedSource(JamCause cause) => cause == JamCause.FEED_GATE_BLOCKED ? LogState.AT_FEED_GATE : LogState.AT_INTAKE;
    private static LogState ExpectedTarget(JamCause cause) => cause == JamCause.FEED_GATE_BLOCKED ? LogState.AT_INTAKE : LogState.QUEUED_FOR_SAW;
}

internal static class LineGuards
{
    internal static void ValidateCurrent(ShiftRuntimeState state, ServerTick currentTick)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (currentTick.IsDefault || currentTick < state.Line.EnteredAt)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Current tick cannot precede line state entry.");
        }
    }

    internal static SimulationDuration PositiveDuration(int ticks, string parameterName) => ticks > 0
        ? SimulationDuration.FromTicks(ticks)
        : throw new ArgumentOutOfRangeException(parameterName, "Repair timing must be positive.");
}
