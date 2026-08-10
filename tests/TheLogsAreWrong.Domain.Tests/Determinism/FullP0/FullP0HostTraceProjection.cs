using System.Collections.Immutable;
using System.Globalization;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Tests.Determinism;

/// <summary>One immutable canonical projection of a single journal event, including its complete semantic payload fields.</summary>
internal sealed record FullP0EventProjection(
    string ShiftId,
    string EventId,
    long EventSequence,
    string? CausedByIntentId,
    long ServerTick,
    long StateVersionAfter,
    string EventTypeId,
    string PayloadKind,
    ImmutableArray<string> PayloadFields)
{
    public bool StructurallyEquals(FullP0EventProjection other) =>
        ShiftId == other.ShiftId &&
        EventId == other.EventId &&
        EventSequence == other.EventSequence &&
        CausedByIntentId == other.CausedByIntentId &&
        ServerTick == other.ServerTick &&
        StateVersionAfter == other.StateVersionAfter &&
        EventTypeId == other.EventTypeId &&
        PayloadKind == other.PayloadKind &&
        PayloadFields.SequenceEqual(other.PayloadFields, StringComparer.Ordinal);

    public string Describe() =>
        $"{EventSequence.ToString(CultureInfo.InvariantCulture)}|{ServerTick.ToString(CultureInfo.InvariantCulture)}|{StateVersionAfter.ToString(CultureInfo.InvariantCulture)}|{EventTypeId}|{EventId}|{CausedByIntentId ?? "-"}|{PayloadKind}|{string.Join(";", PayloadFields)}";
}

/// <summary>One immutable canonical projection of a manifest log's terminal evidence.</summary>
internal sealed record FullP0LogProjection(string LogId, string TrueSpecies, string DeclaredSpecies, string? Anomaly, string State, ImmutableArray<string> Flags)
{
    public bool StructurallyEquals(FullP0LogProjection other) =>
        LogId == other.LogId &&
        TrueSpecies == other.TrueSpecies &&
        DeclaredSpecies == other.DeclaredSpecies &&
        Anomaly == other.Anomaly &&
        State == other.State &&
        Flags.SequenceEqual(other.Flags, StringComparer.Ordinal);
}

/// <summary>
/// The complete canonical value projection of the final <see cref="ShiftRuntimeState"/>. Every publicly readable
/// semantic component that participates in <c>ShiftRuntimeState.ValueEquals</c> is represented here with deterministic
/// ordering for every set and dictionary.
/// <para>
/// One component cannot be reached through the existing public API: the private per-node capacity map that
/// <c>ValueEquals</c> also compares is exposed only indirectly through <c>GetNodeOccupancy</c>. TLAW-042 does not add a
/// production accessor and does not use reflection for it; the repeatability test additionally asserts
/// <c>FinalShiftState.ValueEquals(...)</c> between independent runs, which covers that inaccessible portion.
/// </para>
/// </summary>
internal sealed record FullP0ShiftStateProjection(
    string ShiftId,
    int ShiftSeed,
    long StateVersion,
    ImmutableArray<FullP0LogProjection> Logs,
    ImmutableArray<string> ProcessedIntentIds,
    string PendingFeed,
    ImmutableArray<string> ConsumableInventory,
    ImmutableArray<string> ReusableInventory,
    ImmutableArray<string> ProcedureProgressByLog,
    string ActiveProcedureHold,
    string ActiveConfirmationTest,
    ImmutableArray<string> ConfirmationResultsByLog,
    string Containment,
    string ActiveContainmentRitual,
    string Line,
    string ActiveIntakeDeadline,
    string ActiveSawCycle,
    ImmutableArray<string> NodeOccupancy)
{
    public bool StructurallyEquals(FullP0ShiftStateProjection other) =>
        ShiftId == other.ShiftId &&
        ShiftSeed == other.ShiftSeed &&
        StateVersion == other.StateVersion &&
        Logs.Length == other.Logs.Length &&
        Logs.Select((log, index) => log.StructurallyEquals(other.Logs[index])).All(equal => equal) &&
        ProcessedIntentIds.SequenceEqual(other.ProcessedIntentIds, StringComparer.Ordinal) &&
        PendingFeed == other.PendingFeed &&
        ConsumableInventory.SequenceEqual(other.ConsumableInventory, StringComparer.Ordinal) &&
        ReusableInventory.SequenceEqual(other.ReusableInventory, StringComparer.Ordinal) &&
        ProcedureProgressByLog.SequenceEqual(other.ProcedureProgressByLog, StringComparer.Ordinal) &&
        ActiveProcedureHold == other.ActiveProcedureHold &&
        ActiveConfirmationTest == other.ActiveConfirmationTest &&
        ConfirmationResultsByLog.SequenceEqual(other.ConfirmationResultsByLog, StringComparer.Ordinal) &&
        Containment == other.Containment &&
        ActiveContainmentRitual == other.ActiveContainmentRitual &&
        Line == other.Line &&
        ActiveIntakeDeadline == other.ActiveIntakeDeadline &&
        ActiveSawCycle == other.ActiveSawCycle &&
        NodeOccupancy.SequenceEqual(other.NodeOccupancy, StringComparer.Ordinal);
}

/// <summary>
/// The TLAW-042 canonical immutable full-host trace projection. It is a pure value view over the exact host-produced
/// evidence of one scenario run and is the unit of structural comparison for the repeatability and sensitivity proofs.
/// It introduces no production type, retains no mutable reference, and derives nothing from the environment.
/// </summary>
internal sealed class FullP0HostTraceProjection
{
    private FullP0HostTraceProjection(
        string scenarioId,
        int seed,
        string profile,
        long finalTick,
        int hostTickCount,
        bool lifecycleCompleted,
        string completionReason,
        long completedAt,
        long hardDeadlineAt,
        bool allLogsTerminal,
        bool hardDeadlineReached,
        bool objectivesSatisfied,
        int processedCount,
        int writtenOffCount,
        FullP0ShiftStateProjection finalShift,
        int quotaTargetTotal,
        int quotaTotalCreditedUnits,
        int quotaMinimumCorrectlyProcessedAnomalies,
        int quotaCorrectlyProcessedAnomalies,
        bool quotaObjectivesSatisfied,
        ImmutableArray<string> quotaTargetBySpecies,
        ImmutableArray<string> quotaCreditedBySpecies,
        ImmutableArray<string> quotaSettledLogIds,
        string finalMovementNoise,
        string finalLineNoise,
        string finalProgression,
        string finalCheckpoint,
        ImmutableArray<string> acceptedIntentOrder,
        ImmutableArray<FullP0EventProjection> events)
    {
        ScenarioId = scenarioId;
        Seed = seed;
        Profile = profile;
        FinalTick = finalTick;
        HostTickCount = hostTickCount;
        LifecycleCompleted = lifecycleCompleted;
        CompletionReason = completionReason;
        CompletedAt = completedAt;
        HardDeadlineAt = hardDeadlineAt;
        AllLogsTerminal = allLogsTerminal;
        HardDeadlineReached = hardDeadlineReached;
        ObjectivesSatisfied = objectivesSatisfied;
        ProcessedCount = processedCount;
        WrittenOffCount = writtenOffCount;
        FinalShift = finalShift;
        QuotaTargetTotal = quotaTargetTotal;
        QuotaTotalCreditedUnits = quotaTotalCreditedUnits;
        QuotaMinimumCorrectlyProcessedAnomalies = quotaMinimumCorrectlyProcessedAnomalies;
        QuotaCorrectlyProcessedAnomalies = quotaCorrectlyProcessedAnomalies;
        QuotaObjectivesSatisfied = quotaObjectivesSatisfied;
        QuotaTargetBySpecies = quotaTargetBySpecies;
        QuotaCreditedBySpecies = quotaCreditedBySpecies;
        QuotaSettledLogIds = quotaSettledLogIds;
        FinalMovementNoise = finalMovementNoise;
        FinalLineNoise = finalLineNoise;
        FinalProgression = finalProgression;
        FinalCheckpoint = finalCheckpoint;
        AcceptedIntentOrder = acceptedIntentOrder;
        Events = events;
    }

    public string ScenarioId { get; }
    public int Seed { get; }
    public string Profile { get; }
    public long FinalTick { get; }
    public int HostTickCount { get; }
    public bool LifecycleCompleted { get; }
    public string CompletionReason { get; }
    public long CompletedAt { get; }
    public long HardDeadlineAt { get; }
    public bool AllLogsTerminal { get; }
    public bool HardDeadlineReached { get; }
    public bool ObjectivesSatisfied { get; }
    public int ProcessedCount { get; }
    public int WrittenOffCount { get; }
    /// <summary>The complete canonical value projection of the final shift runtime state.</summary>
    public FullP0ShiftStateProjection FinalShift { get; }

    public ImmutableArray<FullP0LogProjection> Logs => FinalShift.Logs;
    public int QuotaTargetTotal { get; }
    public int QuotaTotalCreditedUnits { get; }
    public int QuotaMinimumCorrectlyProcessedAnomalies { get; }
    public int QuotaCorrectlyProcessedAnomalies { get; }
    public bool QuotaObjectivesSatisfied { get; }
    public ImmutableArray<string> QuotaTargetBySpecies { get; }
    public ImmutableArray<string> QuotaCreditedBySpecies { get; }
    public ImmutableArray<string> QuotaSettledLogIds { get; }
    public string FinalMovementNoise { get; }
    public string FinalLineNoise { get; }
    public string FinalProgression { get; }
    public string FinalCheckpoint { get; }
    public ImmutableArray<string> AcceptedIntentOrder { get; }
    public ImmutableArray<FullP0EventProjection> Events { get; }

    public static FullP0HostTraceProjection Create(FullP0HostScenarioRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var shift = run.FinalShiftState;
        var quota = run.FinalQuotaState;
        var completion = run.FinalLifecycle.Completion;

        return new FullP0HostTraceProjection(
            run.Script.ScenarioId,
            run.Script.Seed.Value,
            run.Script.Profile.ToString(),
            run.Ticks.IsEmpty ? -1 : run.Ticks[^1].Tick.Value,
            run.HostTickCount,
            run.FinalLifecycle.IsCompleted,
            completion is null ? "<active>" : completion.Reason.ToString(),
            completion?.CompletedAt.Value ?? -1,
            run.FinalLifecycle.HardDeadlineAt.Value,
            completion?.AllLogsTerminal ?? false,
            completion?.HardDeadlineReached ?? false,
            completion?.ObjectivesSatisfied ?? false,
            completion?.ProcessedCount ?? shift.Logs.Count(log => log.State == LogState.PROCESSED),
            completion?.WrittenOffCount ?? shift.Logs.Count(log => log.State == LogState.HELD_WRITTEN_OFF),
            ProjectShiftState(shift),
            quota.TargetTotal,
            quota.TotalCreditedUnits,
            quota.MinimumCorrectlyProcessedAnomalies,
            quota.CorrectlyProcessedAnomalies,
            quota.ObjectivesSatisfied,
            ProjectSpecies(quota.TargetBySpecies),
            ProjectSpecies(quota.CreditedBySpecies),
            quota.SettledLogIds.Select(logId => logId.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToImmutableArray(),
            ProjectMovementNoise(run.FinalMovementNoise),
            ProjectLineNoise(run.FinalLineNoise),
            ProjectProgression(run.FinalProgression),
            ProjectCheckpoint(run),
            run.Ticks.SelectMany(record => record.AcceptedIntentIds.Select(intentId => $"{record.Tick.Value.ToString(CultureInfo.InvariantCulture)}:{intentId}")).ToImmutableArray(),
            run.Journal.Events.Select(ProjectEvent).ToImmutableArray());
    }

    public bool StructurallyEquals(FullP0HostTraceProjection other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return ScenarioId == other.ScenarioId &&
            Seed == other.Seed &&
            Profile == other.Profile &&
            FinalTick == other.FinalTick &&
            HostTickCount == other.HostTickCount &&
            LifecycleCompleted == other.LifecycleCompleted &&
            CompletionReason == other.CompletionReason &&
            CompletedAt == other.CompletedAt &&
            HardDeadlineAt == other.HardDeadlineAt &&
            AllLogsTerminal == other.AllLogsTerminal &&
            HardDeadlineReached == other.HardDeadlineReached &&
            ObjectivesSatisfied == other.ObjectivesSatisfied &&
            ProcessedCount == other.ProcessedCount &&
            WrittenOffCount == other.WrittenOffCount &&
            FinalShift.StructurallyEquals(other.FinalShift) &&
            QuotaTargetTotal == other.QuotaTargetTotal &&
            QuotaTotalCreditedUnits == other.QuotaTotalCreditedUnits &&
            QuotaMinimumCorrectlyProcessedAnomalies == other.QuotaMinimumCorrectlyProcessedAnomalies &&
            QuotaCorrectlyProcessedAnomalies == other.QuotaCorrectlyProcessedAnomalies &&
            QuotaObjectivesSatisfied == other.QuotaObjectivesSatisfied &&
            QuotaTargetBySpecies.SequenceEqual(other.QuotaTargetBySpecies, StringComparer.Ordinal) &&
            QuotaCreditedBySpecies.SequenceEqual(other.QuotaCreditedBySpecies, StringComparer.Ordinal) &&
            QuotaSettledLogIds.SequenceEqual(other.QuotaSettledLogIds, StringComparer.Ordinal) &&
            FinalMovementNoise == other.FinalMovementNoise &&
            FinalLineNoise == other.FinalLineNoise &&
            FinalProgression == other.FinalProgression &&
            FinalCheckpoint == other.FinalCheckpoint &&
            AcceptedIntentOrder.SequenceEqual(other.AcceptedIntentOrder, StringComparer.Ordinal) &&
            Events.Length == other.Events.Length &&
            Events.Select((projection, index) => projection.StructurallyEquals(other.Events[index])).All(equal => equal);
    }

    /// <summary>The first structural difference against <paramref name="other"/>, or null when the projections are equal.</summary>
    public string? FirstDifference(FullP0HostTraceProjection other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (StructurallyEquals(other))
        {
            return null;
        }

        for (var index = 0; index < Math.Min(Events.Length, other.Events.Length); index++)
        {
            if (!Events[index].StructurallyEquals(other.Events[index]))
            {
                return $"event[{index.ToString(CultureInfo.InvariantCulture)}] {Events[index].Describe()} != {other.Events[index].Describe()}";
            }
        }

        return Events.Length != other.Events.Length
            ? $"event count {Events.Length.ToString(CultureInfo.InvariantCulture)} != {other.Events.Length.ToString(CultureInfo.InvariantCulture)}"
            : "non-journal canonical field difference";
    }

    /// <summary>Projects every publicly readable semantic component of the final shift runtime state.</summary>
    private static FullP0ShiftStateProjection ProjectShiftState(ShiftRuntimeState shift) => new(
        shift.ShiftId.ToString(),
        shift.ShiftSeed.Value,
        shift.StateVersion.Value,
        shift.Logs.Select(ProjectLog).ToImmutableArray(),
        shift.ProcessedIntentIds.Select(intentId => intentId.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToImmutableArray(),
        ProjectPendingFeed(shift.PendingFeed),
        shift.Inventory.ConsumableQuantities
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value.ToString(CultureInfo.InvariantCulture)}")
            .ToImmutableArray(),
        shift.Inventory.ReusableItems.Select(item => item.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToImmutableArray(),
        shift.ProcedureProgressByLog
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}=>{ProjectProgress(pair.Value)}")
            .ToImmutableArray(),
        shift.ActiveProcedureHold is { } hold
            ? $"{hold.LogId}/{hold.AnomalyId}/{hold.AttemptedItem}/step={hold.ProcedureStepIndex.ToString(CultureInfo.InvariantCulture)}/{hold.StartedAt}->{hold.DueAt}/{hold.Duration.Value.ToString(CultureInfo.InvariantCulture)}"
            : "-",
        ProjectActiveConfirmation(shift.ActiveConfirmationTest),
        shift.ConfirmationResultsByLog
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}=>{string.Join('/', ProjectConfirmationResult(pair.Value))}")
            .ToImmutableArray(),
        ProjectContainment(shift.Containment),
        ProjectRitual(shift.ActiveContainmentRitual),
        ProjectLine(shift.Line),
        shift.ActiveIntakeDeadline is { } deadline
            ? $"{deadline.LogId}/{deadline.StartedAt}->{deadline.DueAt}/{deadline.Duration.Value.ToString(CultureInfo.InvariantCulture)}"
            : "-",
        shift.ActiveSawCycle is { } cycle ? ProjectSawCycle(cycle) : "-",
        Enum.GetValues<NodeId>()
            .OrderBy(node => node.ToString(), StringComparer.Ordinal)
            .Select(node => $"{node}={shift.GetNodeOccupancy(node).ToString(CultureInfo.InvariantCulture)}")
            .ToImmutableArray());

    private static string ProjectPendingFeed(PendingFeedSchedule? pendingFeed) => pendingFeed is null
        ? "-"
        : $"{pendingFeed.LogId}/{pendingFeed.Kind}/{pendingFeed.ScheduledAt}->{pendingFeed.DueAt}/{pendingFeed.Delay.Value.ToString(CultureInfo.InvariantCulture)}/{pendingFeed.CausedByIntentId?.ToString() ?? "-"}";

    private static FullP0LogProjection ProjectLog(LogRuntimeState log) => new(
        log.LogId.ToString(),
        log.TrueSpecies.ToString(),
        log.DeclaredSpecies.ToString(),
        log.Anomaly?.ToString(),
        log.State.ToString(),
        log.Flags.Select(flag => flag.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToImmutableArray());

    private static ImmutableArray<string> ProjectSpecies(ImmutableDictionary<SpeciesId, int> values) => values
        .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
        .Select(pair => $"{pair.Key}={pair.Value.ToString(CultureInfo.InvariantCulture)}")
        .ToImmutableArray();

    private static string ProjectMovementNoise(MovementNoiseRuntimeState runtime) => runtime.LastAcceptedMovement is not { } movement
        ? "idle"
        : $"{movement.Source}|{movement.LogId}|{movement.SourceState}->{movement.DestinationState}|{movement.PriorStateVersion}->{movement.CurrentStateVersion}|accepted={movement.AcceptedAt}|window={runtime.StartedAt}..{runtime.DueAt}";

    private static string ProjectLineNoise(LineNoiseRuntimeState runtime) =>
        $"{runtime.Current}|evaluated={Optional(runtime.LastEvaluatedAt)}|changed={Optional(runtime.LastChangedAt)}|saw={runtime.LatestSources.SawActive}|movement={runtime.LatestSources.MovementNoiseActive}|repair={runtime.LatestSources.RepairActive}";

    private static string ProjectProgression(HostTickProgressionEvidence progression) =>
        $"initial={progression.InitialTick}|last={Optional(progression.LastCompletedTick)}|hasReceipt={progression.LastReceipt is not null}";

    private static string ProjectCheckpoint(FullP0HostScenarioRun run)
    {
        if (run.FinalProgression.LastReceipt is not { } receipt)
        {
            return "none";
        }

        return $"tick={receipt.CompletedTick}|completed={receipt.ShiftCompleted}|evaluation={receipt.Evaluation.GetType().Name}|shiftVersion={receipt.ShiftState.StateVersion}|quotaTotal={receipt.QuotaState.TotalCreditedUnits.ToString(CultureInfo.InvariantCulture)}|quotaCorrect={receipt.QuotaState.CorrectlyProcessedAnomalies.ToString(CultureInfo.InvariantCulture)}";
    }

    private static FullP0EventProjection ProjectEvent(EventEnvelope envelope) => new(
        envelope.ShiftId.ToString(),
        envelope.EventId.ToString(),
        envelope.Sequence.Value,
        envelope.CausedByIntentId?.ToString(),
        envelope.ServerTick.Value,
        envelope.StateVersionAfter.Value,
        envelope.EventType.ToString(),
        envelope.Payload.GetType().Name,
        ProjectPayload(envelope.Payload));

    /// <summary>
    /// The exhaustive canonical field projection for every frozen stage-7 payload kind. Every semantic property of the
    /// payload contributes; nothing is compared by object reference or by <c>ToString</c> of a composite object.
    /// </summary>
    internal static ImmutableArray<string> ProjectPayload(IDomainEventPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        string[] versions = payload is HostStageSevenVersionedPayload versioned
            ? [$"priorVersion={versioned.PriorStateVersion}", $"currentVersion={versioned.CurrentStateVersion}"]
            : [];

        string[] fields = payload switch
        {
            HostStageSevenLogTransitionPayload value =>
            [
                $"logId={value.LogId}", $"from={value.FromState}", $"to={value.ToState}"
            ],
            HostStageSevenFeedSchedulePayload value =>
            [
                $"logId={value.LogId}", $"kind={value.Kind}", $"scheduledAt={value.ScheduledAt}",
                $"dueAt={value.DueAt}", $"delay={value.Delay.Value.ToString(CultureInfo.InvariantCulture)}",
                $"causedBy={value.CausedByIntentId?.ToString() ?? "-"}"
            ],
            HostStageSevenIntakeDeadlinePayload value =>
            [
                $"logId={value.LogId}", $"startedAt={value.StartedAt}", $"dueAt={value.DueAt}",
                $"duration={value.Duration.Value.ToString(CultureInfo.InvariantCulture)}", $"occurredAt={value.OccurredAt}"
            ],
            HostStageSevenAutoRoutePayload value =>
            [
                $"logId={value.LogId}", $"attemptedAt={value.AttemptedAt}", $"outcome={value.Outcome}",
                $"source={Optional(value.Source)}", $"destination={Optional(value.Destination)}",
                $"blockReason={Optional(value.BlockReason)}", $"followUp={Optional(value.FollowUp)}"
            ],
            HostStageSevenProcedurePayload value => ProjectItemAction(value.Descriptor),
            HostStageSevenProcedureActionStartedPayload value =>
            [
                $"logId={value.LogId}", $"anomaly={value.AnomalyId}", $"item={value.AttemptedItem}",
                $"stepIndex={value.ProcedureStepIndex.ToString(CultureInfo.InvariantCulture)}",
                $"startedAt={value.StartedAt}", $"dueAt={value.DueAt}", $"duration={value.Duration.Value.ToString(CultureInfo.InvariantCulture)}"
            ],
            HostStageSevenConfirmationPayload value => ProjectConfirmationResult(value.Result),
            HostStageSevenConfirmationTestStartedPayload value =>
            [
                $"logId={value.LogId}", $"anomaly={value.AnomalyId}", $"tools={Join(value.RequiredTools.Select(tool => tool.ToString()))}",
                $"duration={value.Duration.Value.ToString(CultureInfo.InvariantCulture)}", $"continuous={value.Continuous}",
                $"requiredNoise={Optional(value.RequiredLineNoise)}", $"resetWhenLost={value.ResetWhenConditionLost}",
                $"result={value.Result}", $"segmentStartedAt={value.SegmentStartedAt}", $"dueAt={value.DueAt}"
            ],
            HostStageSevenContainmentPayload value =>
            [
                $"prior={ProjectContainment(value.PriorContainment)}", $"current={ProjectContainment(value.CurrentContainment)}",
                $"ritual={ProjectRitual(value.Ritual)}", $"incident={ProjectIncident(value.Incident)}"
            ],
            HostStageSevenContainmentRitualStartedPayload value =>
            [
                $"containmentState={value.ContainmentState}", $"enteredAt={value.ContainmentEnteredAt}",
                $"deadlineAt={Optional(value.ContainmentDeadlineAt)}", $"ritualStartedAt={value.RitualStartedAt}",
                $"ritualDueAt={value.RitualDueAt}", $"ritualDuration={value.RitualDuration.Value.ToString(CultureInfo.InvariantCulture)}"
            ],
            HostStageSevenRepairPayload value =>
            [
                $"priorLine={ProjectLine(value.PriorLine)}", $"currentLine={ProjectLine(value.CurrentLine)}",
                $"pendingTransition={ProjectPendingTransition(value.PendingTransition)}"
            ],
            HostStageSevenRepairStartedPayload value =>
            [
                $"cause={value.Cause}", $"pendingLogId={value.PendingLogId}", $"startedAt={value.StartedAt}",
                $"dueAt={value.DueAt}", $"duration={value.Duration.Value.ToString(CultureInfo.InvariantCulture)}"
            ],
            HostStageSevenSawStartedPayload value => [$"cycle={ProjectSawCycle(value.Cycle)}"],
            HostStageSevenSawCompletedPayload value =>
            [
                $"cycle={ProjectSawCycle(value.Cycle)}", $"completedAt={value.CompletedAt}",
                $"resolution={ProjectResolution(value.Resolution)}", $"settlement={ProjectSettlement(value.QuotaSettlement)}",
                $"applicationLogId={value.QuotaApplicationLogId}", $"applicationOutcome={value.QuotaApplicationOutcome}",
                $"acceptedSettlement={ProjectSettlementDescriptor(value.AcceptedQuotaSettlement)}",
                $"duplicateLogId={Optional(value.DuplicateQuotaSettlementLogId)}", $"quotaWasApplied={value.QuotaWasApplied}"
            ],
            HostStageSevenLineJamPayload value => [$"logId={value.LogId}", $"cause={value.Cause}", $"enteredAt={value.EnteredAt}"],
            HostStageSevenLineNoisePayload value =>
            [
                $"previous={value.Change.Previous}", $"current={value.Change.Current}", $"changedAt={value.Change.ChangedAt}",
                $"saw={value.Change.Sources.SawActive}", $"movement={value.Change.Sources.MovementNoiseActive}", $"repair={value.Change.Sources.RepairActive}"
            ],
            HostStageSevenConfirmationConditionPayload value =>
            [
                $"prior={ProjectActiveConfirmation(value.Prior)}", $"current={ProjectActiveConfirmation(value.Current)}"
            ],
            HostStageSevenShiftCompletedPayload value =>
            [
                $"completedAt={value.CompletedAt}", $"hardDeadlineAt={value.HardDeadlineAt}", $"reason={value.Reason}",
                $"allLogsTerminal={value.AllLogsTerminal}", $"hardDeadlineReached={value.HardDeadlineReached}",
                $"objectivesSatisfied={value.ObjectivesSatisfied}",
                $"processed={value.ProcessedCount.ToString(CultureInfo.InvariantCulture)}",
                $"writtenOff={value.WrittenOffCount.ToString(CultureInfo.InvariantCulture)}",
                $"targetTotal={value.TargetTotal.ToString(CultureInfo.InvariantCulture)}",
                $"creditedTotal={value.TotalCreditedUnits.ToString(CultureInfo.InvariantCulture)}",
                $"minimumCorrect={value.MinimumCorrectlyProcessedAnomalies.ToString(CultureInfo.InvariantCulture)}",
                $"correct={value.CorrectlyProcessedAnomalies.ToString(CultureInfo.InvariantCulture)}",
                $"targetBySpecies={Join(ProjectSpecies(value.TargetBySpecies))}",
                $"creditedBySpecies={Join(ProjectSpecies(value.CreditedBySpecies))}"
            ],
            _ => throw new InvalidOperationException($"TLAW-042 canonical projection does not cover payload {payload.GetType().Name}.")
        };

        return versions.Concat(fields).ToImmutableArray();
    }

    private static string[] ProjectItemAction(ItemActionCompletionDescriptor descriptor) =>
    [
        $"logId={descriptor.LogId}", $"item={descriptor.AttemptedItem}", $"kind={descriptor.Kind}",
        $"consumed={descriptor.ItemConsumed}", $"priorProgress={ProjectProgress(descriptor.PriorProgress)}",
        $"currentProgress={ProjectProgress(descriptor.CurrentProgress)}",
        $"grantedFlags={Join(descriptor.NewlyGrantedFlags.Select(flag => flag.ToString()))}",
        $"effects={Join(descriptor.Effects.Select(ProjectEffect))}"
    ];

    private static string[] ProjectConfirmationResult(ConfirmationTestResult result) =>
    [
        $"logId={result.LogId}", $"anomaly={result.AnomalyId}", $"result={result.Result}",
        $"tools={Join(result.RequiredTools.Select(tool => tool.ToString()))}",
        $"duration={result.Duration.Value.ToString(CultureInfo.InvariantCulture)}", $"completedAt={result.CompletedAt}"
    ];

    private static string ProjectProgress(ProcedureProgress? progress) => progress is null
        ? "-"
        : $"{progress.LogId}/{progress.AnomalyId}/{progress.CompletedStepCount.ToString(CultureInfo.InvariantCulture)}/{progress.IsComplete}";

    internal static string ProjectEffect(EffectDefinition effect) =>
        $"{effect.Type}:{effect.Event}:{effect.DurationSeconds?.ToString(CultureInfo.InvariantCulture) ?? "-"}:{effect.Target ?? "-"}";

    private static string ProjectContainment(ContainmentRuntimeState containment) =>
        $"{containment.State}@{containment.EnteredAt}/{Optional(containment.DeadlineAt)}";

    private static string ProjectRitual(ActiveContainmentRitual? ritual) => ritual is null
        ? "-"
        : $"{ritual.StartedAt}->{ritual.DueAt}/{ritual.Duration.Value.ToString(CultureInfo.InvariantCulture)}";

    private static string ProjectIncident(ContainmentIncidentDescriptor? incident) => incident is null
        ? "-"
        : $"{incident.Type}/{incident.Duration.Value.ToString(CultureInfo.InvariantCulture)}/{incident.TriggeredAt}";

    private static string ProjectLine(LineRuntimeState line) =>
        $"{line.State}@{line.EnteredAt}/{Optional(line.Cause)}/{Optional(line.PendingLogId)}/{(line.ActiveRepairHold is { } hold ? $"{hold.StartedAt}->{hold.DueAt}" : "-")}";

    private static string ProjectPendingTransition(PendingLineTransitionDescriptor? transition) => transition is null
        ? "-"
        : $"{transition.LogId}/{transition.FromState}->{transition.ToState}/{transition.Cause}";

    private static string ProjectSawCycle(ActiveSawCycle cycle) =>
        $"{cycle.LogId}@{cycle.StartedAt}->{cycle.DueAt}/{cycle.Duration.Value.ToString(CultureInfo.InvariantCulture)}";

    private static string ProjectResolution(ProcessingResolution resolution) =>
        $"{resolution.LogId}/anomalous={resolution.IsAnomalous}/flags={Optional(resolution.AllRequiredFlagsPresent)}/terminal={resolution.TerminalState}/effects={Join(resolution.Effects.Select(ProjectEffect))}";

    private static string ProjectSettlement(QuotaSettlement settlement) =>
        $"{settlement.LogId}/{Optional(settlement.CreditedSpecies)}/{settlement.CreditedUnits.ToString(CultureInfo.InvariantCulture)}/{settlement.CorrectAnomalyDelta.ToString(CultureInfo.InvariantCulture)}";

    private static string ProjectSettlementDescriptor(QuotaSettlementDescriptor? descriptor) => descriptor is null
        ? "-"
        : string.Join('/', new[]
        {
            descriptor.LogId.ToString(),
            Optional(descriptor.CreditedSpecies),
            descriptor.CreditedUnits.ToString(CultureInfo.InvariantCulture),
            descriptor.CorrectAnomalyDelta.ToString(CultureInfo.InvariantCulture),
            descriptor.PriorSpeciesCredit.ToString(CultureInfo.InvariantCulture),
            descriptor.CurrentSpeciesCredit.ToString(CultureInfo.InvariantCulture),
            descriptor.PriorTotalCreditedUnits.ToString(CultureInfo.InvariantCulture),
            descriptor.CurrentTotalCreditedUnits.ToString(CultureInfo.InvariantCulture),
            descriptor.PriorCorrectAnomalyCount.ToString(CultureInfo.InvariantCulture),
            descriptor.CurrentCorrectAnomalyCount.ToString(CultureInfo.InvariantCulture)
        });

    private static string ProjectActiveConfirmation(ActiveConfirmationTest? active) => active is null
        ? "-"
        : $"{active.LogId}/{active.AnomalyId}/accumulated={active.AccumulatedValidDuration.Value.ToString(CultureInfo.InvariantCulture)}/segment={Optional(active.SegmentStartedAt)}/due={Optional(active.DueAt)}/running={active.IsRunning}/boundary={active.LastConditionBoundaryAt}";

    private static string Join(IEnumerable<string> values) =>
        string.Join('+', values.OrderBy(value => value, StringComparer.Ordinal));

    private static string Optional<T>(T? value) where T : struct => value is { } present ? present.ToString() ?? "-" : "-";
}
