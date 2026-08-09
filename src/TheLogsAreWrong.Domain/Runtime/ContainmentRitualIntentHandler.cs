using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>Closed authoritative outcomes for the one stage-2 containment-ritual start intent family.</summary>
public abstract class ContainmentRitualIntentResult
{
    private protected ContainmentRitualIntentResult(ShiftRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
    }

    public ShiftRuntimeState State { get; }
}

public sealed class ContainmentRitualIntentStarted : ContainmentRitualIntentResult
{
    internal ContainmentRitualIntentStarted(ContainmentRitualStarted result)
        : base(result?.State ?? throw new ArgumentNullException(nameof(result))) => Result = result;

    public ContainmentRitualStarted Result { get; }
}

public sealed class ContainmentRitualIntentRejected : ContainmentRitualIntentResult
{
    internal ContainmentRitualIntentRejected(ShiftRuntimeState state, IntentId intentId, RejectionReason reason)
        : base(state)
    {
        IntentId = intentId;
        Reason = reason;
    }

    public IntentId IntentId { get; }
    public RejectionReason Reason { get; }
}

public sealed class ContainmentRitualIntentUnderlyingRejected : ContainmentRitualIntentResult
{
    internal ContainmentRitualIntentUnderlyingRejected(ContainmentRitualStartRejected result, IntentId intentId, RejectionReason reason)
        : base(result?.State ?? throw new ArgumentNullException(nameof(result)))
    {
        Result = result;
        IntentId = intentId;
        Reason = reason;
    }

    public ContainmentRitualStartRejected Result { get; }
    public IntentId IntentId { get; }
    public RejectionReason Reason { get; }
}

public sealed class ContainmentRitualIntentDuplicateIgnored : ContainmentRitualIntentResult
{
    internal ContainmentRitualIntentDuplicateIgnored(ShiftRuntimeState state, IntentId intentId)
        : base(state) => IntentId = intentId;

    public IntentId IntentId { get; }
}

public sealed class ContainmentRitualIntentUnsupportedAction : ContainmentRitualIntentResult
{
    internal ContainmentRitualIntentUnsupportedAction(ShiftRuntimeState state, IntentActionId action)
        : base(state) => Action = action;

    public IntentActionId Action { get; }
}

public sealed class ContainmentRitualIntentUnsupportedTarget : ContainmentRitualIntentResult
{
    internal ContainmentRitualIntentUnsupportedTarget(ShiftRuntimeState state, TargetId target)
        : base(state) => Target = target;

    public TargetId Target { get; }
}

/// <summary>
/// Authoritative handler for exactly <c>start_containment_ritual</c>. Existing TLAW-009 containment gameplay remains
/// the only owner of containment state, timing, and ritual lifecycle; this boundary validates receipt authority and
/// records one accepted intent identity atomically with the existing start mutation.
/// </summary>
public sealed class ContainmentRitualIntentHandler
{
    private readonly ContainmentRitualStartService _startService = new();

    public ContainmentRitualIntentResult Handle(
        ShiftRuntimeState state,
        IntentEnvelope intent,
        ActorId? authoritativeActor,
        ServerTick currentTick,
        ContainmentConfiguration containmentConfiguration)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(containmentConfiguration);

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
            return new ContainmentRitualIntentDuplicateIgnored(state, intent.IntentId);
        }

        if (intent.Action != ContainmentRitualIntentActions.StartContainmentRitual)
        {
            return new ContainmentRitualIntentUnsupportedAction(state, intent.Action);
        }

        if (intent.TargetId != ContainmentRitualIntentTargets.Containment)
        {
            return new ContainmentRitualIntentUnsupportedTarget(state, intent.TargetId);
        }

        if (intent.Parameters is not NoIntentParameters)
        {
            return Reject(state, intent.IntentId, RejectionReason.MALFORMED_CONTAINMENT_RITUAL_PARAMETERS);
        }

        var result = _startService.StartForAuthoritativeIntent(
            state,
            currentTick,
            containmentConfiguration,
            intent.IntentId);
        return result switch
        {
            ContainmentRitualStarted started => new ContainmentRitualIntentStarted(started),
            ContainmentRitualStartRejected rejected => new ContainmentRitualIntentUnderlyingRejected(rejected, intent.IntentId, Map(rejected.Reason)),
            _ => throw new InvalidOperationException("Unknown containment-ritual start result.")
        };
    }

    private static ContainmentRitualIntentRejected Reject(ShiftRuntimeState state, IntentId intentId, RejectionReason reason) =>
        new(state, intentId, reason);

    private static RejectionReason Map(ContainmentRitualStartRejectionReason reason) => reason switch
    {
        ContainmentRitualStartRejectionReason.NO_ACTIVE_REQUEST => RejectionReason.NO_ACTIVE_REQUEST,
        ContainmentRitualStartRejectionReason.RITUAL_ALREADY_ACTIVE => RejectionReason.RITUAL_ALREADY_ACTIVE,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown containment-ritual start rejection reason.")
    };
}
