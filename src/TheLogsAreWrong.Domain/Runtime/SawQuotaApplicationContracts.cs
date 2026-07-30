using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>Composes accepted saw completion evidence with the separate immutable quota state.</summary>
public abstract record SawQuotaApplicationResult(
    ShiftRuntimeState ShiftState,
    QuotaRuntimeState QuotaState,
    LogId CompletedLogId,
    ProcessingResolution Resolution,
    QuotaSettlementResult SettlementResult);

public sealed record SawQuotaApplicationAccepted(
    ShiftRuntimeState ShiftState,
    QuotaRuntimeState QuotaState,
    LogId CompletedLogId,
    ProcessingResolution Resolution,
    QuotaSettlementAccepted AcceptedSettlement)
    : SawQuotaApplicationResult(ShiftState, QuotaState, CompletedLogId, Resolution, AcceptedSettlement);

public sealed record SawQuotaApplicationAlreadyApplied(
    ShiftRuntimeState ShiftState,
    QuotaRuntimeState QuotaState,
    LogId CompletedLogId,
    ProcessingResolution Resolution,
    QuotaSettlementDuplicate DuplicateSettlement)
    : SawQuotaApplicationResult(ShiftState, QuotaState, CompletedLogId, Resolution, DuplicateSettlement);

/// <summary>Applies only the quota settlement retained by validated completed-saw evidence.</summary>
public sealed class SawQuotaApplicationService
{
    private readonly QuotaSettlementService _quotaSettlements = new();

    public SawQuotaApplicationResult Apply(SawCycleCompleted completion, QuotaRuntimeState quota)
    {
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(quota);
        ValidateCompletion(completion);

        return _quotaSettlements.Apply(quota, completion.Resolution.Settlement) switch
        {
            QuotaSettlementAccepted accepted => new SawQuotaApplicationAccepted(
                completion.State,
                accepted.State,
                completion.Cycle.LogId,
                completion.Resolution,
                accepted),
            QuotaSettlementDuplicate duplicate => new SawQuotaApplicationAlreadyApplied(
                completion.State,
                quota,
                completion.Cycle.LogId,
                completion.Resolution,
                duplicate),
            _ => throw new InvalidOperationException("Quota settlement returned an unsupported result.")
        };
    }

    private static void ValidateCompletion(SawCycleCompleted completion)
    {
        ArgumentNullException.ThrowIfNull(completion.State);
        ArgumentNullException.ThrowIfNull(completion.Cycle);
        ArgumentNullException.ThrowIfNull(completion.Resolution);
        ArgumentNullException.ThrowIfNull(completion.Resolution.Settlement);

        var cycle = completion.Cycle;
        var resolution = completion.Resolution;
        if (cycle.LogId.IsDefault || resolution.LogId.IsDefault || resolution.Settlement.LogId.IsDefault ||
            cycle.LogId != resolution.LogId || cycle.LogId != resolution.Settlement.LogId)
        {
            throw new InvalidOperationException("Completed saw evidence must retain one exact log identity.");
        }

        if (resolution.TerminalState != LogState.PROCESSED)
        {
            throw new InvalidOperationException("Completed saw evidence must retain the processed terminal state.");
        }

        if (completion.State.ActiveSawCycle is not null)
        {
            throw new InvalidOperationException("Completed saw evidence cannot retain an active saw cycle.");
        }

        if (!completion.State.TryGetLog(cycle.LogId, out var owner) || owner.State != LogState.PROCESSED)
        {
            throw new InvalidOperationException("Completed saw evidence must retain its processed owner.");
        }

        if (completion.CurrentStateVersion.IsDefault || completion.State.StateVersion.IsDefault ||
            completion.CurrentStateVersion != completion.State.StateVersion)
        {
            throw new InvalidOperationException("Completed saw evidence must retain the current shift-state version.");
        }

        if (!completion.PriorStateVersion.TryNext(out var expectedCurrentVersion) || completion.CurrentStateVersion != expectedCurrentVersion)
        {
            throw new InvalidOperationException("Completed saw evidence must advance the shift-state version exactly once.");
        }

        if (cycle.StartedAt.IsDefault || cycle.Duration.IsDefault || cycle.Duration <= SimulationDuration.Zero || cycle.DueAt.IsDefault ||
            cycle.DueAt != cycle.StartedAt + cycle.Duration)
        {
            throw new InvalidOperationException("Completed saw evidence must retain valid cycle timing.");
        }

        if (completion.CompletedAt.IsDefault || completion.CompletedAt < cycle.DueAt)
        {
            throw new InvalidOperationException("Completed saw evidence cannot complete before its due tick.");
        }
    }
}
