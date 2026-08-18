using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>Closed authoritative outcomes for the one stage-2 line-repair start intent family.</summary>
public abstract class LineRepairIntentResult
{
    private protected LineRepairIntentResult(ShiftRuntimeState state)
    {
        if (state is null) { throw new ArgumentNullException("state"); }
        State = state;
    }

    public ShiftRuntimeState State { get; }
}

public sealed class LineRepairIntentStarted : LineRepairIntentResult
{
    internal LineRepairIntentStarted(LineRepairStarted result)
        : base(result?.State ?? throw new ArgumentNullException(nameof(result))) => Result = result;

    public LineRepairStarted Result { get; }
}

public sealed class LineRepairIntentRejected : LineRepairIntentResult
{
    internal LineRepairIntentRejected(ShiftRuntimeState state, IntentId intentId, RejectionReason reason)
        : base(state)
    {
        IntentId = intentId;
        Reason = reason;
    }

    public IntentId IntentId { get; }
    public RejectionReason Reason { get; }
}

public sealed class LineRepairIntentUnderlyingRejected : LineRepairIntentResult
{
    internal LineRepairIntentUnderlyingRejected(LineRepairStartRejected result, IntentId intentId, RejectionReason reason)
        : base(result?.State ?? throw new ArgumentNullException(nameof(result)))
    {
        Result = result;
        IntentId = intentId;
        Reason = reason;
    }

    public LineRepairStartRejected Result { get; }
    public IntentId IntentId { get; }
    public RejectionReason Reason { get; }
}

public sealed class LineRepairIntentDuplicateIgnored : LineRepairIntentResult
{
    internal LineRepairIntentDuplicateIgnored(ShiftRuntimeState state, IntentId intentId)
        : base(state) => IntentId = intentId;

    public IntentId IntentId { get; }
}

public sealed class LineRepairIntentUnsupportedAction : LineRepairIntentResult
{
    internal LineRepairIntentUnsupportedAction(ShiftRuntimeState state, IntentActionId action)
        : base(state) => Action = action;

    public IntentActionId Action { get; }
}

public sealed class LineRepairIntentUnsupportedTarget : LineRepairIntentResult
{
    internal LineRepairIntentUnsupportedTarget(ShiftRuntimeState state, TargetId target)
        : base(state) => Target = target;

    public TargetId Target { get; }
}

/// <summary>
/// Authoritative handler for exactly <c>start_line_repair</c>. Existing TLAW-010 repair gameplay remains the only
/// owner of jam cause, pending log and timing; this boundary validates receipt authority and records one accepted ID.
/// </summary>
public sealed class LineRepairIntentHandler
{
    private readonly LineRepairStartService _startService = new();

    public LineRepairIntentResult Handle(
        ShiftRuntimeState state,
        IntentEnvelope intent,
        ActorId? authoritativeActor,
        ServerTick currentTick,
        SchedulerConfiguration schedulerConfiguration)
    {
        if (state is null) { throw new ArgumentNullException("state"); }
        if (intent is null) { throw new ArgumentNullException("intent"); }
        if (schedulerConfiguration is null) { throw new ArgumentNullException("schedulerConfiguration"); }

        if (intent.ShiftId != state.ShiftId)
        {
            return Reject(state, intent.IntentId, RejectionReason.SHIFT_MISMATCH);
        }

        if (authoritativeActor is not { } boundActor || boundActor.IsDefault)
        {
            return Reject(state, intent.IntentId, RejectionReason.ACTOR_NOT_BOUND);
        }

        if (intent.ExpectedStateVersion != state.StateVersion)
        {
            return Reject(state, intent.IntentId, RejectionReason.STALE_STATE_VERSION);
        }

        if (state.ProcessedIntentIds.Contains(intent.IntentId))
        {
            return new LineRepairIntentDuplicateIgnored(state, intent.IntentId);
        }

        if (intent.Action != LineRepairIntentActions.StartLineRepair)
        {
            return new LineRepairIntentUnsupportedAction(state, intent.Action);
        }

        if (intent.TargetId != LineRepairIntentTargets.Line)
        {
            return new LineRepairIntentUnsupportedTarget(state, intent.TargetId);
        }

        if (intent.Parameters is not NoIntentParameters)
        {
            return Reject(state, intent.IntentId, RejectionReason.MALFORMED_LINE_REPAIR_PARAMETERS);
        }

        var result = _startService.StartForAuthoritativeIntent(
            state,
            currentTick,
            schedulerConfiguration,
            intent.IntentId);
        return result switch
        {
            LineRepairStarted started => new LineRepairIntentStarted(started),
            LineRepairStartRejected rejected => new LineRepairIntentUnderlyingRejected(rejected, intent.IntentId, Map(rejected.Reason)),
            LineRepairStartFailed failed => throw new InvalidOperationException($"Line-repair start invariant failure: {failed.Reason}."),
            _ => throw new InvalidOperationException("Unknown line-repair start result.")
        };
    }

    private static LineRepairIntentRejected Reject(ShiftRuntimeState state, IntentId intentId, RejectionReason reason) =>
        new(state, intentId, reason);

    private static RejectionReason Map(LineRepairStartRejectionReason reason) => reason switch
    {
        LineRepairStartRejectionReason.NO_ACTIVE_JAM => RejectionReason.NO_ACTIVE_JAM,
        LineRepairStartRejectionReason.REPAIR_ALREADY_ACTIVE => RejectionReason.REPAIR_ALREADY_ACTIVE,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown line-repair start rejection reason.")
    };
}
