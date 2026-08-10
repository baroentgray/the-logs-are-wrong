using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Journal;

/// <summary>One immutable value projection of a manifest log and its per-log inspection evidence.</summary>
public sealed record SnapshotLog
{
    public SnapshotLog(
        LogId logId,
        SpeciesId trueSpecies,
        SpeciesId declaredSpecies,
        AnomalyId? anomaly,
        LogState state,
        ImmutableArray<FlagId> flags,
        SnapshotProcedureProgress? procedureProgress,
        SnapshotConfirmationResult? confirmationResult)
    {
        if (logId.IsDefault || trueSpecies.IsDefault || declaredSpecies.IsDefault || (anomaly is { } value && value.IsDefault))
        {
            throw new ArgumentException("A snapshot log must retain initialized manifest identities.");
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Snapshot log state must be defined.");
        }

        if (flags.IsDefault || flags.Any(flag => flag.IsDefault))
        {
            throw new ArgumentException("Snapshot log flags must be an initialized array of initialized flags.", nameof(flags));
        }

        if (procedureProgress is not null && procedureProgress.LogId != logId)
        {
            throw new ArgumentException("Snapshot procedure progress must belong to its own log.", nameof(procedureProgress));
        }

        if (confirmationResult is not null && confirmationResult.LogId != logId)
        {
            throw new ArgumentException("Snapshot confirmation result must belong to its own log.", nameof(confirmationResult));
        }

        LogId = logId;
        TrueSpecies = trueSpecies;
        DeclaredSpecies = declaredSpecies;
        Anomaly = anomaly;
        State = state;
        Flags = SnapshotOrdering.SortFlags(flags);
        ProcedureProgress = procedureProgress;
        ConfirmationResult = confirmationResult;
    }

    public LogId LogId { get; }
    public SpeciesId TrueSpecies { get; }
    public SpeciesId DeclaredSpecies { get; }
    public AnomalyId? Anomaly { get; }
    public LogState State { get; }

    /// <summary>Granted flags in deterministic ordinal order.</summary>
    public ImmutableArray<FlagId> Flags { get; }

    public SnapshotProcedureProgress? ProcedureProgress { get; }
    public SnapshotConfirmationResult? ConfirmationResult { get; }

    public bool StructurallyEquals(SnapshotLog other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return LogId == other.LogId && TrueSpecies == other.TrueSpecies && DeclaredSpecies == other.DeclaredSpecies &&
            Anomaly == other.Anomaly && State == other.State && Flags.SequenceEqual(other.Flags) &&
            ProcedureProgress == other.ProcedureProgress &&
            (ConfirmationResult is null ? other.ConfirmationResult is null : other.ConfirmationResult is not null && ConfirmationResult.StructurallyEquals(other.ConfirmationResult));
    }

    /// <summary>A compact deterministic description used only for diagnostics.</summary>
    public string Describe() =>
        $"{State}|flags=[{string.Join('+', Flags)}]|progress={(ProcedureProgress is { } progress ? $"{progress.AnomalyId}/{progress.CompletedStepCount}/{progress.IsComplete}" : "-")}|confirmation={(ConfirmationResult is { } result ? $"{result.AnomalyId}/{result.Result}/{result.CompletedAt}/[{string.Join('+', result.RequiredTools)}]/{result.Duration}" : "-")}";
}

/// <summary>Immutable per-log procedure progress evidence.</summary>
public sealed record SnapshotProcedureProgress(LogId LogId, AnomalyId AnomalyId, int CompletedStepCount, bool IsComplete);

/// <summary>Immutable per-log completed confirmation evidence.</summary>
public sealed record SnapshotConfirmationResult
{
    public SnapshotConfirmationResult(LogId logId, AnomalyId anomalyId, string result, ImmutableArray<ItemId> requiredTools, SimulationDuration duration, ServerTick completedAt)
    {
        if (logId.IsDefault || anomalyId.IsDefault || string.IsNullOrWhiteSpace(result) || duration.IsDefault || completedAt.IsDefault)
        {
            throw new ArgumentException("A snapshot confirmation result must retain initialized evidence.");
        }

        if (requiredTools.IsDefault || requiredTools.Any(tool => tool.IsDefault))
        {
            throw new ArgumentException("Snapshot confirmation tools must be initialized.", nameof(requiredTools));
        }

        LogId = logId;
        AnomalyId = anomalyId;
        Result = result;
        RequiredTools = SnapshotOrdering.SortItems(requiredTools);
        Duration = duration;
        CompletedAt = completedAt;
    }

    public LogId LogId { get; }
    public AnomalyId AnomalyId { get; }
    public string Result { get; }
    public ImmutableArray<ItemId> RequiredTools { get; }
    public SimulationDuration Duration { get; }
    public ServerTick CompletedAt { get; }

    public bool StructurallyEquals(SnapshotConfirmationResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return LogId == other.LogId && AnomalyId == other.AnomalyId && string.Equals(Result, other.Result, StringComparison.Ordinal) &&
            RequiredTools.SequenceEqual(other.RequiredTools) && Duration == other.Duration && CompletedAt == other.CompletedAt;
    }
}

/// <summary>Immutable pending-feed evidence. <c>DueAt</c> stays derived from <c>ScheduledAt + Delay</c>.</summary>
public sealed record SnapshotPendingFeed(LogId LogId, FeedScheduleKind Kind, ServerTick ScheduledAt, SimulationDuration Delay, IntentId? CausedByIntentId);

/// <summary>Immutable active intake-deadline evidence.</summary>
public sealed record SnapshotIntakeDeadline(LogId LogId, ServerTick StartedAt, SimulationDuration Duration);

/// <summary>Immutable active saw-cycle evidence.</summary>
public sealed record SnapshotSawCycle(LogId LogId, ServerTick StartedAt, SimulationDuration Duration);

/// <summary>Immutable active procedure-hold evidence.</summary>
public sealed record SnapshotProcedureHold(LogId LogId, AnomalyId AnomalyId, ItemId AttemptedItem, int ProcedureStepIndex, ServerTick StartedAt, SimulationDuration Duration);

/// <summary>Immutable running/paused confirmation-test evidence, including the exact configured plan values.</summary>
public sealed record SnapshotConfirmationTest
{
    public SnapshotConfirmationTest(
        LogId logId,
        AnomalyId anomalyId,
        ImmutableArray<ItemId> requiredTools,
        SimulationDuration planDuration,
        bool continuous,
        LineNoise? requiredLineNoise,
        bool resetWhenConditionLost,
        string result,
        SimulationDuration accumulatedValidDuration,
        ServerTick? segmentStartedAt,
        bool isRunning,
        ServerTick lastConditionBoundaryAt)
    {
        if (logId.IsDefault || anomalyId.IsDefault || planDuration.IsDefault || accumulatedValidDuration.IsDefault || lastConditionBoundaryAt.IsDefault)
        {
            throw new ArgumentException("A snapshot confirmation test must retain initialized evidence.");
        }

        if (requiredTools.IsDefault || requiredTools.IsEmpty || requiredTools.Any(tool => tool.IsDefault))
        {
            throw new ArgumentException("Snapshot confirmation tools must be initialized and non-empty.", nameof(requiredTools));
        }

        if (string.IsNullOrWhiteSpace(result))
        {
            throw new ArgumentException("Snapshot confirmation result must be non-blank.", nameof(result));
        }

        if (isRunning != segmentStartedAt.HasValue)
        {
            throw new ArgumentException("A running snapshot confirmation requires its segment tick and a paused one requires none.", nameof(isRunning));
        }

        LogId = logId;
        AnomalyId = anomalyId;
        RequiredTools = SnapshotOrdering.SortItems(requiredTools);
        PlanDuration = planDuration;
        Continuous = continuous;
        RequiredLineNoise = requiredLineNoise;
        ResetWhenConditionLost = resetWhenConditionLost;
        Result = result;
        AccumulatedValidDuration = accumulatedValidDuration;
        SegmentStartedAt = segmentStartedAt;
        IsRunning = isRunning;
        LastConditionBoundaryAt = lastConditionBoundaryAt;
    }

    public LogId LogId { get; }
    public AnomalyId AnomalyId { get; }
    public ImmutableArray<ItemId> RequiredTools { get; }
    public SimulationDuration PlanDuration { get; }
    public bool Continuous { get; }
    public LineNoise? RequiredLineNoise { get; }
    public bool ResetWhenConditionLost { get; }
    public string Result { get; }
    public SimulationDuration AccumulatedValidDuration { get; }
    public ServerTick? SegmentStartedAt { get; }
    public bool IsRunning { get; }
    public ServerTick LastConditionBoundaryAt { get; }

    public bool StructurallyEquals(SnapshotConfirmationTest other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return LogId == other.LogId && AnomalyId == other.AnomalyId && RequiredTools.SequenceEqual(other.RequiredTools) &&
            PlanDuration == other.PlanDuration && Continuous == other.Continuous && RequiredLineNoise == other.RequiredLineNoise &&
            ResetWhenConditionLost == other.ResetWhenConditionLost && string.Equals(Result, other.Result, StringComparison.Ordinal) &&
            AccumulatedValidDuration == other.AccumulatedValidDuration && SegmentStartedAt == other.SegmentStartedAt &&
            IsRunning == other.IsRunning && LastConditionBoundaryAt == other.LastConditionBoundaryAt;
    }
}

/// <summary>
/// Host-tick progression evidence. <c>docs/INTAKE_SCHEDULER.md</c> § "Порядок одного host tick" makes sequential host
/// ticks and intake/shift/containment deadline expiration scheduler-owned, so this evidence lives in the frozen
/// <c>scheduler_state</c> field. The last completed checkpoint tick is always the snapshot tick, so only its presence
/// is recorded.
/// </summary>
public sealed record SnapshotProgression(bool HasCompletedTick, bool LastReceiptCompletedShift);

/// <summary>
/// The frozen <c>scheduler_state</c> field. It carries exactly the node-owned active work and timers described by
/// <c>docs/INTAKE_SCHEDULER.md</c> — the pending feed, the intake timer, the procedure-position hold, the running
/// confirmation at intake, the saw cycle — plus host-tick progression and the accepted-intent identities the frozen
/// idempotency rule in <c>docs/LOG_STATE_MACHINE.md</c> requires.
/// </summary>
public sealed record SnapshotSchedulerState
{
    public SnapshotSchedulerState(
        SnapshotPendingFeed? pendingFeed,
        SnapshotIntakeDeadline? activeIntakeDeadline,
        SnapshotProcedureHold? activeProcedureHold,
        SnapshotConfirmationTest? activeConfirmationTest,
        SnapshotSawCycle? activeSawCycle,
        ImmutableArray<IntentId> processedIntentIds,
        SnapshotProgression progression)
    {
        ArgumentNullException.ThrowIfNull(progression);
        if (processedIntentIds.IsDefault || processedIntentIds.Any(intentId => intentId.IsDefault))
        {
            throw new ArgumentException("Snapshot processed-intent identities must be initialized.", nameof(processedIntentIds));
        }

        PendingFeed = pendingFeed;
        ActiveIntakeDeadline = activeIntakeDeadline;
        ActiveProcedureHold = activeProcedureHold;
        ActiveConfirmationTest = activeConfirmationTest;
        ActiveSawCycle = activeSawCycle;
        ProcessedIntentIds = SnapshotOrdering.SortIntents(processedIntentIds);
        Progression = progression;
    }

    public SnapshotPendingFeed? PendingFeed { get; }
    public SnapshotIntakeDeadline? ActiveIntakeDeadline { get; }
    public SnapshotProcedureHold? ActiveProcedureHold { get; }
    public SnapshotConfirmationTest? ActiveConfirmationTest { get; }
    public SnapshotSawCycle? ActiveSawCycle { get; }

    /// <summary>Accepted intent identities in deterministic ordinal order.</summary>
    public ImmutableArray<IntentId> ProcessedIntentIds { get; }

    public SnapshotProgression Progression { get; }

    public bool StructurallyEquals(SnapshotSchedulerState other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return PendingFeed == other.PendingFeed && ActiveIntakeDeadline == other.ActiveIntakeDeadline &&
            ActiveProcedureHold == other.ActiveProcedureHold &&
            (ActiveConfirmationTest is null ? other.ActiveConfirmationTest is null : other.ActiveConfirmationTest is not null && ActiveConfirmationTest.StructurallyEquals(other.ActiveConfirmationTest)) &&
            ActiveSawCycle == other.ActiveSawCycle && ProcessedIntentIds.SequenceEqual(other.ProcessedIntentIds) &&
            Progression == other.Progression;
    }
}

/// <summary>Immutable active repair-hold evidence.</summary>
public sealed record SnapshotRepairHold(ServerTick StartedAt, SimulationDuration Duration);

/// <summary>Immutable derived line-noise evidence, including the exact source snapshot that produced it.</summary>
public sealed record SnapshotLineNoise(LineNoise Current, ServerTick? LastEvaluatedAt, ServerTick? LastChangedAt, bool SawActive, bool MovementNoiseActive, bool RepairActive);

/// <summary>Immutable mechanical movement-noise evidence and its exact active window.</summary>
public sealed record SnapshotMovementNoise(
    MovementNoiseAcceptedSource Source,
    LogId LogId,
    LogState SourceState,
    LogState DestinationState,
    StateVersion PriorStateVersion,
    StateVersion CurrentStateVersion,
    ServerTick AcceptedAt,
    ServerTick StartedAt,
    ServerTick DueAt);

/// <summary>
/// The frozen <c>line_state</c> field. <c>docs/LOG_STATE_MACHINE.md</c> groups the line state machine and the derived
/// line-noise predicate together, and <c>docs/FIRST_SHIFT_SPEC.md</c> § "Line noise" names saw, mechanical movement
/// noise and repair as the three line-noise sources, so the retained movement-noise window belongs here too.
/// </summary>
public sealed record SnapshotLineState
{
    public SnapshotLineState(
        LineState state,
        ServerTick enteredAt,
        JamCause? cause,
        LogId? pendingLogId,
        SnapshotRepairHold? activeRepairHold,
        SnapshotLineNoise lineNoise,
        SnapshotMovementNoise? movementNoise)
    {
        ArgumentNullException.ThrowIfNull(lineNoise);
        if (!Enum.IsDefined(state) || enteredAt.IsDefault)
        {
            throw new ArgumentException("Snapshot line state and entry tick must be initialized.");
        }

        if (state == LineState.LINE_CLEAR)
        {
            if (cause is not null || pendingLogId is not null || activeRepairHold is not null)
            {
                throw new ArgumentException("A clear snapshot line cannot retain jam or repair evidence.");
            }
        }
        else
        {
            if (cause is not { } activeCause || !Enum.IsDefined(activeCause) || pendingLogId is not { } pending || pending.IsDefault)
            {
                throw new ArgumentException("A jammed snapshot line requires an exact active cause and pending log.");
            }

            if ((state == LineState.REPAIRING) != (activeRepairHold is not null))
            {
                throw new ArgumentException("Only a repairing snapshot line retains an active repair hold.");
            }

            if (activeRepairHold is not null && activeRepairHold.StartedAt != enteredAt)
            {
                throw new ArgumentException("A repairing snapshot line must enter when its repair hold starts.");
            }
        }

        State = state;
        EnteredAt = enteredAt;
        Cause = cause;
        PendingLogId = pendingLogId;
        ActiveRepairHold = activeRepairHold;
        LineNoise = lineNoise;
        MovementNoise = movementNoise;
    }

    public LineState State { get; }
    public ServerTick EnteredAt { get; }
    public JamCause? Cause { get; }
    public LogId? PendingLogId { get; }
    public SnapshotRepairHold? ActiveRepairHold { get; }
    public SnapshotLineNoise LineNoise { get; }
    public SnapshotMovementNoise? MovementNoise { get; }

    public bool StructurallyEquals(SnapshotLineState other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return State == other.State && EnteredAt == other.EnteredAt && Cause == other.Cause && PendingLogId == other.PendingLogId &&
            ActiveRepairHold == other.ActiveRepairHold && LineNoise == other.LineNoise && MovementNoise == other.MovementNoise;
    }
}

/// <summary>Immutable active containment-ritual evidence.</summary>
public sealed record SnapshotContainmentRitual(ServerTick StartedAt, SimulationDuration Duration);

/// <summary>The frozen <c>containment_state</c> field, mirroring the containment state machine and its active ritual.</summary>
public sealed record SnapshotContainmentState
{
    public SnapshotContainmentState(ContainmentState state, ServerTick enteredAt, ServerTick? deadlineAt, SnapshotContainmentRitual? activeRitual)
    {
        if (!Enum.IsDefined(state) || enteredAt.IsDefault)
        {
            throw new ArgumentException("Snapshot containment state and entry tick must be initialized.");
        }

        if (state == ContainmentState.INCIDENT && deadlineAt is not null)
        {
            throw new ArgumentException("Incident containment cannot retain a deadline.", nameof(deadlineAt));
        }

        if (state is ContainmentState.SERVICE_REQUESTED or ContainmentState.OVERDUE && deadlineAt is null)
        {
            throw new ArgumentException("Active containment requests require a deadline.", nameof(deadlineAt));
        }

        if (deadlineAt is { } deadline && (deadline.IsDefault || deadline <= enteredAt))
        {
            throw new ArgumentException("Containment deadline must be initialized and later than entry.", nameof(deadlineAt));
        }

        State = state;
        EnteredAt = enteredAt;
        DeadlineAt = deadlineAt;
        ActiveRitual = activeRitual;
    }

    public ContainmentState State { get; }
    public ServerTick EnteredAt { get; }
    public ServerTick? DeadlineAt { get; }
    public SnapshotContainmentRitual? ActiveRitual { get; }

    public bool StructurallyEquals(SnapshotContainmentState other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return State == other.State && EnteredAt == other.EnteredAt && DeadlineAt == other.DeadlineAt && ActiveRitual == other.ActiveRitual;
    }
}

/// <summary>The frozen <c>inventory</c> field: consumable counts and reusable identities in deterministic order.</summary>
public sealed record SnapshotInventory
{
    public SnapshotInventory(ImmutableArray<SnapshotConsumable> consumables, ImmutableArray<ItemId> reusableItems)
    {
        if (consumables.IsDefault || consumables.Any(entry => entry is null || entry.Item.IsDefault || entry.Quantity < 0))
        {
            throw new ArgumentException("Snapshot consumables must be initialized with non-negative quantities.", nameof(consumables));
        }

        if (reusableItems.IsDefault || reusableItems.Any(item => item.IsDefault))
        {
            throw new ArgumentException("Snapshot reusable items must be initialized.", nameof(reusableItems));
        }

        Consumables = consumables.Sort((left, right) => string.CompareOrdinal(left.Item.ToString(), right.Item.ToString()));
        ReusableItems = SnapshotOrdering.SortItems(reusableItems);
    }

    public ImmutableArray<SnapshotConsumable> Consumables { get; }
    public ImmutableArray<ItemId> ReusableItems { get; }

    public bool StructurallyEquals(SnapshotInventory other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Consumables.SequenceEqual(other.Consumables) && ReusableItems.SequenceEqual(other.ReusableItems);
    }
}

/// <summary>One immutable consumable count.</summary>
public sealed record SnapshotConsumable(ItemId Item, int Quantity);

/// <summary>One immutable species credit or target entry.</summary>
public sealed record SnapshotSpeciesCount(SpeciesId Species, int Units);

/// <summary>The frozen <c>quota</c> field: monotonic credited progress and settled-log evidence.</summary>
public sealed record SnapshotQuota
{
    public SnapshotQuota(ImmutableArray<SnapshotSpeciesCount> creditedBySpecies, int totalCreditedUnits, int correctlyProcessedAnomalies, ImmutableArray<LogId> settledLogIds)
    {
        if (creditedBySpecies.IsDefault || creditedBySpecies.Any(entry => entry is null || entry.Species.IsDefault || entry.Units < 0))
        {
            throw new ArgumentException("Snapshot species credits must be initialized and non-negative.", nameof(creditedBySpecies));
        }

        if (totalCreditedUnits < 0 || correctlyProcessedAnomalies < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCreditedUnits), "Snapshot quota progress cannot be negative.");
        }

        if (settledLogIds.IsDefault || settledLogIds.Any(logId => logId.IsDefault))
        {
            throw new ArgumentException("Snapshot settled logs must be initialized.", nameof(settledLogIds));
        }

        CreditedBySpecies = SnapshotOrdering.SortSpecies(creditedBySpecies);
        TotalCreditedUnits = totalCreditedUnits;
        CorrectlyProcessedAnomalies = correctlyProcessedAnomalies;
        SettledLogIds = SnapshotOrdering.SortLogs(settledLogIds);
    }

    public ImmutableArray<SnapshotSpeciesCount> CreditedBySpecies { get; }
    public int TotalCreditedUnits { get; }
    public int CorrectlyProcessedAnomalies { get; }
    public ImmutableArray<LogId> SettledLogIds { get; }

    public bool StructurallyEquals(SnapshotQuota other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return CreditedBySpecies.SequenceEqual(other.CreditedBySpecies) && TotalCreditedUnits == other.TotalCreditedUnits &&
            CorrectlyProcessedAnomalies == other.CorrectlyProcessedAnomalies && SettledLogIds.SequenceEqual(other.SettledLogIds);
    }
}

/// <summary>Immutable completion evidence retained only after the frozen completion decision.</summary>
public sealed record SnapshotCompletion(
    ServerTick CompletedAt,
    ShiftCompletionReason Reason,
    bool AllLogsTerminal,
    bool HardDeadlineReached,
    bool ObjectivesSatisfied,
    int ProcessedCount,
    int WrittenOffCount);

/// <summary>
/// The frozen <c>objectives</c> field. <c>docs/FIRST_SHIFT_SPEC.md</c> § "Success predicate" defines the shift
/// objective and its completion condition together — all logs terminal or the hard deadline — so the selected profile,
/// the shift lifecycle window and any completion outcome are recorded here rather than duplicated elsewhere.
/// </summary>
public sealed record SnapshotObjectives
{
    public SnapshotObjectives(
        ProfileId selectedProfileId,
        int targetTotal,
        ImmutableArray<SnapshotSpeciesCount> targetBySpecies,
        int minimumCorrectlyProcessedAnomalies,
        ServerTick startedAt,
        SimulationDuration hardDeadlineDuration,
        SnapshotCompletion? completion)
    {
        if (selectedProfileId.IsDefault || startedAt.IsDefault || hardDeadlineDuration.IsDefault || hardDeadlineDuration <= SimulationDuration.Zero)
        {
            throw new ArgumentException("Snapshot objectives require an initialized profile and a positive deadline window.");
        }

        if (targetBySpecies.IsDefault || targetBySpecies.Any(entry => entry is null || entry.Species.IsDefault || entry.Units < 0))
        {
            throw new ArgumentException("Snapshot species targets must be initialized and non-negative.", nameof(targetBySpecies));
        }

        if (targetTotal < 0 || minimumCorrectlyProcessedAnomalies < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetTotal), "Snapshot objective targets cannot be negative.");
        }

        SelectedProfileId = selectedProfileId;
        TargetTotal = targetTotal;
        TargetBySpecies = SnapshotOrdering.SortSpecies(targetBySpecies);
        MinimumCorrectlyProcessedAnomalies = minimumCorrectlyProcessedAnomalies;
        StartedAt = startedAt;
        HardDeadlineDuration = hardDeadlineDuration;
        HardDeadlineAt = checked(startedAt + hardDeadlineDuration);
        Completion = completion;
    }

    public ProfileId SelectedProfileId { get; }
    public int TargetTotal { get; }
    public ImmutableArray<SnapshotSpeciesCount> TargetBySpecies { get; }
    public int MinimumCorrectlyProcessedAnomalies { get; }
    public ServerTick StartedAt { get; }
    public SimulationDuration HardDeadlineDuration { get; }
    public ServerTick HardDeadlineAt { get; }
    public SnapshotCompletion? Completion { get; }
    public bool IsCompleted => Completion is not null;

    public bool StructurallyEquals(SnapshotObjectives other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return SelectedProfileId == other.SelectedProfileId && TargetTotal == other.TargetTotal &&
            TargetBySpecies.SequenceEqual(other.TargetBySpecies) && MinimumCorrectlyProcessedAnomalies == other.MinimumCorrectlyProcessedAnomalies &&
            StartedAt == other.StartedAt && HardDeadlineDuration == other.HardDeadlineDuration && HardDeadlineAt == other.HardDeadlineAt &&
            Completion == other.Completion;
    }
}

/// <summary>
/// The frozen Gate-1 <c>ShiftSnapshot</c> from <c>docs/LOG_STATE_MACHINE.md</c> § "Snapshot/replay", implemented as an
/// immutable value projection. It retains no live runtime, stage-execution or journal reference, exposes only immutable
/// collections in deterministic order, and is compared by <see cref="StructurallyEquals"/> rather than by identity.
/// <para>
/// The shift seed, node capacities and manifest order are configuration-derived and are validated against the exact
/// <c>ShiftConfiguration</c> during restore instead of being duplicated into the frozen shape.
/// </para>
/// </summary>
public sealed class ShiftSnapshot
{
    public ShiftSnapshot(
        ShiftId shiftId,
        ServerTick serverTick,
        StateVersion stateVersion,
        EventSequence lastEventSequence,
        SnapshotSchedulerState schedulerState,
        ImmutableArray<SnapshotLog> logs,
        SnapshotLineState lineState,
        SnapshotContainmentState containmentState,
        SnapshotInventory inventory,
        SnapshotQuota quota,
        SnapshotObjectives objectives)
    {
        ArgumentNullException.ThrowIfNull(schedulerState);
        ArgumentNullException.ThrowIfNull(lineState);
        ArgumentNullException.ThrowIfNull(containmentState);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(quota);
        ArgumentNullException.ThrowIfNull(objectives);

        if (shiftId.IsDefault || serverTick.IsDefault || stateVersion.IsDefault)
        {
            throw new ArgumentException("A shift snapshot requires an initialized shift, tick and state version.");
        }

        if (logs.IsDefaultOrEmpty || logs.Any(log => log is null))
        {
            throw new ArgumentException("A shift snapshot requires an initialized non-empty manifest projection.", nameof(logs));
        }

        var identities = new HashSet<LogId>();
        foreach (var log in logs)
        {
            if (!identities.Add(log.LogId))
            {
                throw new ArgumentException("Snapshot manifest identities must be unique.", nameof(logs));
            }
        }

        ShiftId = shiftId;
        ServerTick = serverTick;
        StateVersion = stateVersion;
        LastEventSequence = lastEventSequence;
        SchedulerState = schedulerState;
        Logs = logs;
        LineState = lineState;
        ContainmentState = containmentState;
        Inventory = inventory;
        Quota = quota;
        Objectives = objectives;
    }

    public ShiftId ShiftId { get; }
    public ServerTick ServerTick { get; }
    public StateVersion StateVersion { get; }

    /// <summary>The exact journal boundary; <see cref="EventSequence.None"/> when no event has been published.</summary>
    public EventSequence LastEventSequence { get; }

    public SnapshotSchedulerState SchedulerState { get; }

    /// <summary>The manifest projection in exact manifest order.</summary>
    public ImmutableArray<SnapshotLog> Logs { get; }

    public SnapshotLineState LineState { get; }
    public SnapshotContainmentState ContainmentState { get; }
    public SnapshotInventory Inventory { get; }
    public SnapshotQuota Quota { get; }
    public SnapshotObjectives Objectives { get; }

    /// <summary>The exact replay boundary this snapshot represents, reusing the established boundary contract.</summary>
    public SnapshotBoundary Boundary => new()
    {
        ShiftId = ShiftId,
        ServerTick = ServerTick,
        StateVersion = StateVersion,
        LastEventSequence = LastEventSequence
    };

    /// <summary>Deterministic structural value equality, independent of object identity and collection enumeration order.</summary>
    public bool StructurallyEquals(ShiftSnapshot? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ShiftId != other.ShiftId || ServerTick != other.ServerTick || StateVersion != other.StateVersion ||
            LastEventSequence != other.LastEventSequence || Logs.Length != other.Logs.Length)
        {
            return false;
        }

        for (var index = 0; index < Logs.Length; index++)
        {
            if (!Logs[index].StructurallyEquals(other.Logs[index]))
            {
                return false;
            }
        }

        return SchedulerState.StructurallyEquals(other.SchedulerState) &&
            LineState.StructurallyEquals(other.LineState) &&
            ContainmentState.StructurallyEquals(other.ContainmentState) &&
            Inventory.StructurallyEquals(other.Inventory) &&
            Quota.StructurallyEquals(other.Quota) &&
            Objectives.StructurallyEquals(other.Objectives);
    }

    /// <summary>The first structural difference against <paramref name="other"/>, or null when the snapshots are equal.</summary>
    public string? FirstDifference(ShiftSnapshot? other)
    {
        if (other is null)
        {
            return "other snapshot is null";
        }

        if (ShiftId != other.ShiftId) return $"shiftId {ShiftId} != {other.ShiftId}";
        if (ServerTick != other.ServerTick) return $"serverTick {ServerTick} != {other.ServerTick}";
        if (StateVersion != other.StateVersion) return $"stateVersion {StateVersion} != {other.StateVersion}";
        if (LastEventSequence != other.LastEventSequence) return $"lastEventSequence {LastEventSequence} != {other.LastEventSequence}";
        if (Logs.Length != other.Logs.Length) return $"log count {Logs.Length} != {other.Logs.Length}";

        for (var index = 0; index < Logs.Length; index++)
        {
            if (!Logs[index].StructurallyEquals(other.Logs[index]))
            {
                return $"log {Logs[index].LogId}: {Logs[index].Describe()} != {other.Logs[index].Describe()}";
            }
        }

        if (!SchedulerState.StructurallyEquals(other.SchedulerState)) return "schedulerState";
        if (!LineState.StructurallyEquals(other.LineState)) return "lineState";
        if (!ContainmentState.StructurallyEquals(other.ContainmentState)) return "containmentState";
        if (!Inventory.StructurallyEquals(other.Inventory)) return "inventory";
        if (!Quota.StructurallyEquals(other.Quota)) return "quota";
        return Objectives.StructurallyEquals(other.Objectives) ? null : "objectives";
    }
}

/// <summary>Deterministic ordinal ordering helpers so every snapshot collection has one canonical order.</summary>
internal static class SnapshotOrdering
{
    internal static ImmutableArray<FlagId> SortFlags(ImmutableArray<FlagId> values) =>
        values.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));

    internal static ImmutableArray<ItemId> SortItems(ImmutableArray<ItemId> values) =>
        values.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));

    internal static ImmutableArray<IntentId> SortIntents(ImmutableArray<IntentId> values) =>
        values.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));

    internal static ImmutableArray<LogId> SortLogs(ImmutableArray<LogId> values) =>
        values.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));

    internal static ImmutableArray<SnapshotSpeciesCount> SortSpecies(ImmutableArray<SnapshotSpeciesCount> values) =>
        values.Sort((left, right) => string.CompareOrdinal(left.Species.ToString(), right.Species.ToString()));
}
