using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>Closed authoritative outcomes for the one stage-2 procedure-action start intent family.</summary>
public abstract record ProcedureActionIntentResult(ShiftRuntimeState State);

public sealed record ProcedureActionIntentHoldStarted(ProcedureActionHoldStarted Result)
    : ProcedureActionIntentResult(Result.State);

public sealed record ProcedureActionIntentCompletedImmediately(ProcedureActionCompletedImmediately Result)
    : ProcedureActionIntentResult(Result.State);

public sealed record ProcedureActionIntentRejected(ShiftRuntimeState State, IntentId IntentId, RejectionReason Reason)
    : ProcedureActionIntentResult(State);

public sealed record ProcedureActionIntentUnderlyingRejected(ProcedureActionStartRejected Result, IntentId IntentId, RejectionReason Reason)
    : ProcedureActionIntentResult(Result.State);

public sealed record ProcedureActionIntentDuplicateIgnored(ShiftRuntimeState State, IntentId IntentId)
    : ProcedureActionIntentResult(State);

public sealed record ProcedureActionIntentUnsupported(ShiftRuntimeState State, IntentActionId Action)
    : ProcedureActionIntentResult(State);

/// <summary>
/// Authoritative handler for exactly <c>start_procedure_action</c>. It validates the established intent guard order,
/// then delegates all gameplay semantics to the existing procedure-action lifecycle with an atomic processed-intent seam.
/// </summary>
public sealed class ProcedureActionIntentHandler
{
    private readonly ProcedureActionStartService _startService = new();

    public ProcedureActionIntentResult Handle(
        ShiftRuntimeState state,
        IntentEnvelope intent,
        ActorId? authoritativeActor,
        ServerTick currentTick,
        AnomalyCatalog anomalyCatalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(anomalyCatalog);

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
            return new ProcedureActionIntentDuplicateIgnored(state, intent.IntentId);
        }

        if (intent.Action != ProcedureIntentActions.StartProcedureAction)
        {
            return new ProcedureActionIntentUnsupported(state, intent.Action);
        }

        if (intent.Parameters is not ProcedureActionIntentParameters parameters)
        {
            return Reject(state, intent.IntentId, RejectionReason.MALFORMED_PROCEDURE_PARAMETERS);
        }

        if (!state.TryGetLog(intent.TargetId, out var target))
        {
            return Reject(state, intent.IntentId, RejectionReason.TARGET_NOT_FOUND);
        }

        var result = _startService.StartForAuthoritativeIntent(
            state,
            target.LogId,
            parameters.AttemptedItem,
            currentTick,
            anomalyCatalog,
            intent.IntentId);
        return result switch
        {
            ProcedureActionHoldStarted started => new ProcedureActionIntentHoldStarted(started),
            ProcedureActionCompletedImmediately completed => new ProcedureActionIntentCompletedImmediately(completed),
            ProcedureActionStartRejected rejected => new ProcedureActionIntentUnderlyingRejected(rejected, intent.IntentId, Map(rejected.Reason)),
            _ => throw new InvalidOperationException("Unknown procedure action start result.")
        };
    }

    private static ProcedureActionIntentRejected Reject(ShiftRuntimeState state, IntentId intentId, RejectionReason reason) =>
        new(state, intentId, reason);

    private static RejectionReason Map(ProcedureActionStartRejectionReason reason) => reason switch
    {
        ProcedureActionStartRejectionReason.TargetNotFound => RejectionReason.TARGET_NOT_FOUND,
        ProcedureActionStartRejectionReason.TargetNotAtProcedure => RejectionReason.TARGET_NOT_IN_STATE,
        ProcedureActionStartRejectionReason.ActiveHoldAlreadyExists => RejectionReason.PROCEDURE_HOLD_ACTIVE,
        ProcedureActionStartRejectionReason.NoProcedurePlan => RejectionReason.PROCEDURE_NO_PLAN,
        ProcedureActionStartRejectionReason.MissingItem => RejectionReason.MISSING_ITEM,
        ProcedureActionStartRejectionReason.OutOfOrderItem => RejectionReason.PROCEDURE_OUT_OF_ORDER_ITEM,
        ProcedureActionStartRejectionReason.RepeatedCorrectStep => RejectionReason.PROCEDURE_REPEATED_STEP,
        ProcedureActionStartRejectionReason.UnconfiguredWrongAction => RejectionReason.PROCEDURE_UNCONFIGURED_ITEM,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown procedure-action start rejection reason.")
    };
}
