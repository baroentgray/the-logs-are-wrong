using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Journal;

/// <summary>
/// The reducer's private working state. It exists only inside one reduction call, is never exposed, and is projected
/// back into an immutable <see cref="ShiftSnapshot"/> when reduction succeeds. Nothing here is published on failure.
/// </summary>
internal sealed class ReductionState
{
    private readonly SnapshotLog[] _logs;
    private readonly Dictionary<LogId, int> _logIndex;
    private readonly Dictionary<ItemId, int> _consumables;
    private readonly ImmutableArray<ItemId> _reusableItems;
    private readonly Dictionary<SpeciesId, int> _credited;
    private readonly HashSet<LogId> _settled;
    private readonly HashSet<IntentId> _processedIntents;
    private readonly SnapshotObjectives _objectiveBase;
    private readonly SimulationDuration _movementNoiseDuration;

    private StateVersion _stateVersion;
    private ServerTick _tick;
    private EventSequence _lastSequence;
    private bool _hasCompletedTick;
    private LineNoise _currentNoise;
    private ServerTick? _lastEvaluatedAt;
    private ServerTick? _lastChangedAt;
    private SnapshotMovementNoise? _movementNoise;
    private int _totalCreditedUnits;
    private int _correctlyProcessedAnomalies;
    private PendingLineTransitionDescriptor? _repairFollowUp;

    private ReductionState(ShiftSnapshot snapshot, ShiftConfiguration configuration)
    {
        ShiftId = snapshot.ShiftId;
        _logs = snapshot.Logs.ToArray();
        _logIndex = new Dictionary<LogId, int>(_logs.Length);
        for (var index = 0; index < _logs.Length; index++)
        {
            _logIndex[_logs[index].LogId] = index;
        }

        _consumables = snapshot.Inventory.Consumables.ToDictionary(entry => entry.Item, entry => entry.Quantity);
        _reusableItems = snapshot.Inventory.ReusableItems;
        _credited = snapshot.Quota.CreditedBySpecies.ToDictionary(entry => entry.Species, entry => entry.Units);
        _settled = [.. snapshot.Quota.SettledLogIds];
        _processedIntents = [.. snapshot.SchedulerState.ProcessedIntentIds];
        _objectiveBase = snapshot.Objectives;
        _movementNoiseDuration = SimulationDuration.FromTicks(configuration.Scheduler.MovementNoiseSeconds);

        _stateVersion = snapshot.StateVersion;
        _tick = snapshot.ServerTick;
        _lastSequence = snapshot.LastEventSequence;
        _hasCompletedTick = snapshot.SchedulerState.Progression.HasCompletedTick;
        _currentNoise = snapshot.LineState.LineNoise.Current;
        _lastEvaluatedAt = snapshot.LineState.LineNoise.LastEvaluatedAt;
        _lastChangedAt = snapshot.LineState.LineNoise.LastChangedAt;
        _movementNoise = snapshot.LineState.MovementNoise;
        _totalCreditedUnits = snapshot.Quota.TotalCreditedUnits;
        _correctlyProcessedAnomalies = snapshot.Quota.CorrectlyProcessedAnomalies;

        PendingFeed = snapshot.SchedulerState.PendingFeed;
        ActiveIntakeDeadline = snapshot.SchedulerState.ActiveIntakeDeadline;
        ActiveProcedureHold = snapshot.SchedulerState.ActiveProcedureHold;
        ActiveConfirmationTest = snapshot.SchedulerState.ActiveConfirmationTest;
        ActiveSawCycle = snapshot.SchedulerState.ActiveSawCycle;
        ActiveSawFailureWindow = snapshot.SchedulerState.ActiveSawFailureWindow;
        LineStateValue = snapshot.LineState.State;
        LineEnteredAt = snapshot.LineState.EnteredAt;
        LineCause = snapshot.LineState.Cause;
        LinePendingLogId = snapshot.LineState.PendingLogId;
        RepairHold = snapshot.LineState.ActiveRepairHold;
        ContainmentStateValue = snapshot.ContainmentState.State;
        ContainmentEnteredAt = snapshot.ContainmentState.EnteredAt;
        ContainmentDeadlineAt = snapshot.ContainmentState.DeadlineAt;
        ActiveContainmentRitual = snapshot.ContainmentState.ActiveRitual;
        Completion = snapshot.Objectives.Completion;
    }

    internal static ReductionState From(ShiftSnapshot snapshot, ShiftConfiguration configuration) => new(snapshot, configuration);

    internal ShiftId ShiftId { get; }
    internal StateVersion StateVersion => _stateVersion;
    internal ServerTick HardDeadlineAt => _objectiveBase.HardDeadlineAt;
    internal int TotalCreditedUnits => _totalCreditedUnits;
    internal int CorrectlyProcessedAnomalies => _correctlyProcessedAnomalies;

    internal SnapshotPendingFeed? PendingFeed { get; set; }
    internal SnapshotIntakeDeadline? ActiveIntakeDeadline { get; set; }
    internal SnapshotProcedureHold? ActiveProcedureHold { get; set; }
    internal SnapshotConfirmationTest? ActiveConfirmationTest { get; set; }
    internal SnapshotSawCycle? ActiveSawCycle { get; set; }
    internal SnapshotSawFailureWindow? ActiveSawFailureWindow { get; set; }
    internal LineState LineStateValue { get; set; }
    internal ServerTick LineEnteredAt { get; set; }
    internal JamCause? LineCause { get; set; }
    internal LogId? LinePendingLogId { get; set; }
    internal SnapshotRepairHold? RepairHold { get; set; }
    internal ContainmentState ContainmentStateValue { get; private set; }
    internal ServerTick ContainmentEnteredAt { get; private set; }
    internal ServerTick? ContainmentDeadlineAt { get; private set; }
    internal SnapshotContainmentRitual? ActiveContainmentRitual { get; set; }
    internal SnapshotCompletion? Completion { get; set; }

    // ----- manifest -----

    internal bool HasLog(LogId logId) => !logId.IsDefault && _logIndex.ContainsKey(logId);

    internal bool TryGetLog(LogId logId, out SnapshotLog log)
    {
        if (!logId.IsDefault && _logIndex.TryGetValue(logId, out var index))
        {
            log = _logs[index];
            return true;
        }

        log = default!;
        return false;
    }

    /// <summary>
    /// Applies a log transition with exactly the frozen conditional clearing the live runtime performs: a hold, a
    /// running confirmation and an intake deadline are released when their owner leaves the node that carried them.
    /// </summary>
    internal void SetLogState(LogId logId, LogState toState)
    {
        var index = _logIndex[logId];
        var existing = _logs[index];
        _logs[index] = new SnapshotLog(
            existing.LogId, existing.TrueSpecies, existing.DeclaredSpecies, existing.Anomaly, toState,
            existing.Flags, existing.ProcedureProgress, existing.ConfirmationResult);

        if (ActiveProcedureHold is { } hold && hold.LogId == logId && toState != LogState.AT_PROCEDURE)
        {
            ActiveProcedureHold = null;
        }

        if (ActiveConfirmationTest is { } confirmation && confirmation.LogId == logId && toState != LogState.AT_INTAKE)
        {
            ActiveConfirmationTest = null;
        }

        if (ActiveIntakeDeadline is { } deadline && deadline.LogId == logId && existing.State == LogState.AT_INTAKE && toState != LogState.AT_INTAKE)
        {
            ActiveIntakeDeadline = null;
        }
    }

    internal void AddFlags(LogId logId, ImmutableHashSet<FlagId> flags)
    {
        if (flags.IsEmpty)
        {
            return;
        }

        var index = _logIndex[logId];
        var existing = _logs[index];
        var merged = existing.Flags.ToImmutableHashSet().Union(flags).ToImmutableArray();
        _logs[index] = new SnapshotLog(
            existing.LogId, existing.TrueSpecies, existing.DeclaredSpecies, existing.Anomaly, existing.State,
            merged, existing.ProcedureProgress, existing.ConfirmationResult);
    }

    internal void SetProcedureProgress(LogId logId, ProcedureProgress? progress)
    {
        if (progress is null)
        {
            return;
        }

        var index = _logIndex[logId];
        var existing = _logs[index];
        _logs[index] = new SnapshotLog(
            existing.LogId, existing.TrueSpecies, existing.DeclaredSpecies, existing.Anomaly, existing.State,
            existing.Flags,
            new SnapshotProcedureProgress(progress.LogId, progress.AnomalyId, progress.CompletedStepCount, progress.IsComplete),
            existing.ConfirmationResult);
    }

    internal void SetConfirmationResult(ConfirmationTestResult result)
    {
        var index = _logIndex[result.LogId];
        var existing = _logs[index];
        _logs[index] = new SnapshotLog(
            existing.LogId, existing.TrueSpecies, existing.DeclaredSpecies, existing.Anomaly, existing.State,
            existing.Flags, existing.ProcedureProgress,
            new SnapshotConfirmationResult(
                result.LogId, result.AnomalyId, result.Result, result.RequiredTools.ToImmutableArray(), result.Duration, result.CompletedAt));
    }

    internal (int Processed, int WrittenOff) CountTerminal()
    {
        var processed = 0;
        var writtenOff = 0;
        foreach (var log in _logs)
        {
            if (log.State == LogState.PROCESSED)
            {
                processed++;
            }
            else if (log.State == LogState.HELD_WRITTEN_OFF)
            {
                writtenOff++;
            }
        }

        return (processed, writtenOff);
    }

    // ----- inventory -----

    internal string? ConsumeItem(ItemId item)
    {
        if (!_consumables.TryGetValue(item, out var quantity))
        {
            return $"item {item} is not a configured consumable";
        }

        if (quantity <= 0)
        {
            return $"consumable {item} is exhausted";
        }

        _consumables[item] = quantity - 1;
        return null;
    }

    // ----- quota -----

    internal bool IsSettled(LogId logId) => _settled.Contains(logId);

    internal string? ApplySettlement(QuotaSettlementDescriptor descriptor)
    {
        if (descriptor.CreditedSpecies is { } species)
        {
            var prior = _credited.TryGetValue(species, out var existing) ? existing : 0;
            if (prior != descriptor.PriorSpeciesCredit)
            {
                return $"species {species} prior credit {prior} contradicts settlement {descriptor.PriorSpeciesCredit}";
            }

            var current = checked(prior + descriptor.CreditedUnits);
            if (current != descriptor.CurrentSpeciesCredit)
            {
                return $"species {species} current credit {current} contradicts settlement {descriptor.CurrentSpeciesCredit}";
            }

            _credited[species] = current;
        }
        else if (descriptor.CreditedUnits != 0)
        {
            return "a settlement without a credited species must credit zero units";
        }

        if (checked(_totalCreditedUnits + descriptor.CreditedUnits) != descriptor.CurrentTotalCreditedUnits ||
            checked(_correctlyProcessedAnomalies + descriptor.CorrectAnomalyDelta) != descriptor.CurrentCorrectAnomalyCount)
        {
            return "settlement totals do not follow from the reconstructed quota";
        }

        _totalCreditedUnits = descriptor.CurrentTotalCreditedUnits;
        _correctlyProcessedAnomalies = descriptor.CurrentCorrectAnomalyCount;
        _settled.Add(descriptor.LogId);
        return null;
    }

    // ----- containment -----

    internal void SetContainment(ContainmentRuntimeState containment)
    {
        ContainmentStateValue = containment.State;
        ContainmentEnteredAt = containment.EnteredAt;
        ContainmentDeadlineAt = containment.DeadlineAt;
    }

    // ----- line noise and movement noise -----

    /// <summary>The frozen derived predicate: loud when the saw, mechanical movement noise or a repair is active.</summary>
    private LineNoise DeriveNoise(ServerTick tick) =>
        ActiveSawCycle is not null || IsMovementActive(tick) || LineStateValue == Enums.LineState.REPAIRING
            ? LineNoise.LOUD
            : LineNoise.QUIET;

    private bool IsMovementActive(ServerTick tick) =>
        _movementNoise is { } movement && tick >= movement.StartedAt && tick < movement.DueAt;

    internal string? RecordLineNoiseChange(LineNoise published, ServerTick changedAt)
    {
        var derived = DeriveNoise(changedAt);
        if (derived != published)
        {
            return $"derived line noise {derived} contradicts the published change {published}";
        }

        _lastChangedAt = changedAt;
        return null;
    }

    internal void ApplyMovement(MovementNoiseAcceptedSource source, HostStageSevenLogTransitionPayload payload, ServerTick acceptedAt) =>
        ApplyMovement(source, payload.LogId, payload.FromState, payload.ToState, payload.PriorStateVersion, payload.CurrentStateVersion, acceptedAt);

    /// <summary>Reproduces the frozen movement-noise window rule: a fresh window, or an extended one while overlapping.</summary>
    internal void ApplyMovement(
        MovementNoiseAcceptedSource source,
        LogId logId,
        LogState sourceState,
        LogState destinationState,
        StateVersion priorStateVersion,
        StateVersion currentStateVersion,
        ServerTick acceptedAt)
    {
        var candidateDueAt = acceptedAt + _movementNoiseDuration;
        ServerTick startedAt;
        ServerTick dueAt;
        if (_movementNoise is null || acceptedAt >= _movementNoise.DueAt)
        {
            startedAt = acceptedAt;
            dueAt = candidateDueAt;
        }
        else
        {
            startedAt = _movementNoise.StartedAt;
            dueAt = candidateDueAt > _movementNoise.DueAt ? candidateDueAt : _movementNoise.DueAt;
        }

        _movementNoise = new SnapshotMovementNoise(
            source, logId, sourceState, destinationState, priorStateVersion, currentStateVersion, acceptedAt, startedAt, dueAt);
    }

    /// <summary>Remembers the pending transition a completed repair retained, so stage five's follow-up is attributed to it.</summary>
    internal void RememberRepairFollowUp(PendingLineTransitionDescriptor? pendingTransition) => _repairFollowUp = pendingTransition;

    internal bool ConsumeRepairFollowUp(LogId logId, JamCause cause)
    {
        if (_repairFollowUp is not { } pending || pending.LogId != logId || pending.Cause != cause)
        {
            return false;
        }

        _repairFollowUp = null;
        return true;
    }

    // ----- boundary bookkeeping -----

    internal void AdvanceVersion() => _stateVersion = _stateVersion.Next();

    internal void RecordCausation(IntentId? causedByIntentId)
    {
        if (causedByIntentId is { } intentId && !intentId.IsDefault)
        {
            _processedIntents.Add(intentId);
        }
    }

    /// <summary>
    /// Advances the journal and tick boundary. Every executed tick evaluates line noise, so an event at a tick proves
    /// that tick was evaluated; the derived value is recomputed from the reconstructed sources rather than guessed.
    /// </summary>
    internal void AdvanceBoundary(EventEnvelope envelope)
    {
        var previousTick = _tick;
        _tick = envelope.ServerTick;
        _lastSequence = envelope.Sequence;
        _hasCompletedTick = true;
        _lastEvaluatedAt = envelope.ServerTick;
        _currentNoise = DeriveNoise(envelope.ServerTick);

        // A retained repair follow-up only applies inside the tick whose stage one completed the repair.
        if (_repairFollowUp is not null && envelope.ServerTick != previousTick)
        {
            _repairFollowUp = null;
        }
    }

    internal static SnapshotConfirmationTest ProjectConfirmation(ActiveConfirmationTest active) => new(
        active.LogId,
        active.AnomalyId,
        active.Plan.RequiredTools.ToImmutableArray(),
        active.Plan.Duration,
        active.Plan.Continuous,
        active.Plan.RequiredLineNoise,
        active.Plan.ResetWhenConditionLost,
        active.Plan.Result,
        active.AccumulatedValidDuration,
        active.SegmentStartedAt,
        active.IsRunning,
        active.LastConditionBoundaryAt);

    internal ShiftSnapshot ToSnapshot() => new(
        ShiftId,
        _tick,
        _stateVersion,
        _lastSequence,
        new SnapshotSchedulerState(
            PendingFeed,
            ActiveIntakeDeadline,
            ActiveProcedureHold,
            ActiveConfirmationTest,
            ActiveSawCycle,
            ActiveSawFailureWindow,
            _processedIntents.ToImmutableArray(),
            new SnapshotProgression(_hasCompletedTick, Completion is not null)),
        [.. _logs],
        new SnapshotLineState(
            LineStateValue,
            LineEnteredAt,
            LineCause,
            LinePendingLogId,
            RepairHold,
            new SnapshotLineNoise(
                _currentNoise,
                _lastEvaluatedAt,
                _lastChangedAt,
                ActiveSawCycle is not null,
                _lastEvaluatedAt is { } evaluatedAt && IsMovementActive(evaluatedAt),
                LineStateValue == Enums.LineState.REPAIRING),
            _movementNoise),
        new SnapshotContainmentState(ContainmentStateValue, ContainmentEnteredAt, ContainmentDeadlineAt, ActiveContainmentRitual),
        new SnapshotInventory(
            _consumables.Select(pair => new SnapshotConsumable(pair.Key, pair.Value)).ToImmutableArray(),
            _reusableItems),
        new SnapshotQuota(
            _credited.Select(pair => new SnapshotSpeciesCount(pair.Key, pair.Value)).ToImmutableArray(),
            _totalCreditedUnits,
            _correctlyProcessedAnomalies,
            _settled.ToImmutableArray()),
        new SnapshotObjectives(
            _objectiveBase.SelectedProfileId,
            _objectiveBase.TargetTotal,
            _objectiveBase.TargetBySpecies,
            _objectiveBase.MinimumCorrectlyProcessedAnomalies,
            _objectiveBase.StartedAt,
            _objectiveBase.HardDeadlineDuration,
            Completion));
}
