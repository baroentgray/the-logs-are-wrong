using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>Closed authoritative outcomes for the one stage-2 confirmation-test start intent family.</summary>
public abstract class ConfirmationTestIntentResult
{
    private protected ConfirmationTestIntentResult(ShiftRuntimeState state)
    {
        if (state is null) { throw new ArgumentNullException("state"); }
        State = state;
    }

    public ShiftRuntimeState State { get; }
}

public sealed class ConfirmationTestIntentStarted : ConfirmationTestIntentResult
{
    internal ConfirmationTestIntentStarted(ConfirmationTestStarted result)
        : base(result?.State ?? throw new ArgumentNullException(nameof(result))) => Result = result;

    public ConfirmationTestStarted Result { get; }
}

public sealed class ConfirmationTestIntentRejected : ConfirmationTestIntentResult
{
    internal ConfirmationTestIntentRejected(ShiftRuntimeState state, IntentId intentId, RejectionReason reason)
        : base(state)
    {
        IntentId = intentId;
        Reason = reason;
    }

    public IntentId IntentId { get; }
    public RejectionReason Reason { get; }
}

public sealed class ConfirmationTestIntentUnderlyingRejected : ConfirmationTestIntentResult
{
    internal ConfirmationTestIntentUnderlyingRejected(ConfirmationTestStartRejected result, IntentId intentId, RejectionReason reason)
        : base(result?.State ?? throw new ArgumentNullException(nameof(result)))
    {
        Result = result;
        IntentId = intentId;
        Reason = reason;
    }

    public ConfirmationTestStartRejected Result { get; }
    public IntentId IntentId { get; }
    public RejectionReason Reason { get; }
}

public sealed class ConfirmationTestIntentDuplicateIgnored : ConfirmationTestIntentResult
{
    internal ConfirmationTestIntentDuplicateIgnored(ShiftRuntimeState state, IntentId intentId)
        : base(state) => IntentId = intentId;

    public IntentId IntentId { get; }
}

public sealed class ConfirmationTestIntentUnsupported : ConfirmationTestIntentResult
{
    internal ConfirmationTestIntentUnsupported(ShiftRuntimeState state, IntentActionId action)
        : base(state) => Action = action;

    public IntentActionId Action { get; }
}

/// <summary>
/// Authoritative handler for exactly <c>start_confirmation_test</c>. Gameplay remains owned by the TLAW-008
/// confirmation lifecycle; this boundary only validates authoritative receipt guards and records accepted intent identity.
/// </summary>
public sealed class ConfirmationTestIntentHandler
{
    private readonly ConfirmationTestStartService _startService = new();

    public ConfirmationTestIntentResult Handle(
        ShiftRuntimeState state,
        IntentEnvelope intent,
        ActorId? authoritativeActor,
        ServerTick currentTick,
        ImmutableHashSet<ItemId> activeTools,
        LineNoiseRuntimeState lineNoiseRuntime,
        AnomalyCatalog anomalyCatalog)
    {
        if (state is null) { throw new ArgumentNullException("state"); }
        if (intent is null) { throw new ArgumentNullException("intent"); }
        if (activeTools is null) { throw new ArgumentNullException("activeTools"); }
        if (lineNoiseRuntime is null) { throw new ArgumentNullException("lineNoiseRuntime"); }
        if (anomalyCatalog is null) { throw new ArgumentNullException("anomalyCatalog"); }

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
            return new ConfirmationTestIntentDuplicateIgnored(state, intent.IntentId);
        }

        if (intent.Action != ConfirmationIntentActions.StartConfirmationTest)
        {
            return new ConfirmationTestIntentUnsupported(state, intent.Action);
        }

        if (intent.Parameters is not NoIntentParameters)
        {
            return Reject(state, intent.IntentId, RejectionReason.MALFORMED_CONFIRMATION_PARAMETERS);
        }

        if (!state.TryGetLog(intent.TargetId, out var target))
        {
            return Reject(state, intent.IntentId, RejectionReason.TARGET_NOT_FOUND);
        }

        var result = _startService.StartForAuthoritativeIntent(
            state,
            target.LogId,
            activeTools,
            currentTick,
            lineNoiseRuntime,
            anomalyCatalog,
            intent.IntentId);
        return result switch
        {
            ConfirmationTestStarted started => new ConfirmationTestIntentStarted(started),
            ConfirmationTestStartRejected rejected => new ConfirmationTestIntentUnderlyingRejected(rejected, intent.IntentId, Map(rejected.Reason)),
            _ => throw new InvalidOperationException("Unknown confirmation test start result.")
        };
    }

    private static ConfirmationTestIntentRejected Reject(ShiftRuntimeState state, IntentId intentId, RejectionReason reason) =>
        new(state, intentId, reason);

    private static RejectionReason Map(ConfirmationTestStartRejectionReason reason) => reason switch
    {
        ConfirmationTestStartRejectionReason.TargetNotFound => RejectionReason.TARGET_NOT_FOUND,
        ConfirmationTestStartRejectionReason.TargetNotAtIntake => RejectionReason.TARGET_NOT_IN_STATE,
        ConfirmationTestStartRejectionReason.ActiveConfirmationAlreadyExists => RejectionReason.CONFIRMATION_ACTIVE,
        ConfirmationTestStartRejectionReason.AlreadyConfirmed => RejectionReason.CONFIRMATION_ALREADY_COMPLETED,
        ConfirmationTestStartRejectionReason.NoConfirmationPlan => RejectionReason.CONFIRMATION_NO_PLAN,
        ConfirmationTestStartRejectionReason.MissingRequiredTool => RejectionReason.CONFIRMATION_REQUIRED_TOOL_UNAVAILABLE,
        ConfirmationTestStartRejectionReason.RequiredLineNoiseNotMet => RejectionReason.CONFIRMATION_REQUIRED_LINE_NOISE_NOT_MET,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown confirmation-test start rejection reason.")
    };
}
