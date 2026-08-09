using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>Frozen semantic event identifiers owned by host stage 7. Callers cannot select these values.</summary>
public static class HostStageSevenEventTypes
{
    public static readonly EventTypeId FeedScheduled = EventTypeId.From("FeedScheduled");
    public static readonly EventTypeId EarlyFeedRequested = EventTypeId.From("EarlyFeedRequested");
    public static readonly EventTypeId LogPlacedAtFeedGate = EventTypeId.From("LogPlacedAtFeedGate");
    public static readonly EventTypeId LogAdmittedToIntake = EventTypeId.From("LogAdmittedToIntake");
    public static readonly EventTypeId IntakeDeadlineStarted = EventTypeId.From("IntakeDeadlineStarted");
    public static readonly EventTypeId IntakeDeadlineExpired = EventTypeId.From("IntakeDeadlineExpired");
    public static readonly EventTypeId AutoRouteAttempted = EventTypeId.From("AutoRouteAttempted");
    public static readonly EventTypeId LineJammed = EventTypeId.From("LineJammed");
    public static readonly EventTypeId RepairCompleted = EventTypeId.From("RepairCompleted");
    public static readonly EventTypeId SawCycleStarted = EventTypeId.From("SawCycleStarted");
    public static readonly EventTypeId SawCycleCompleted = EventTypeId.From("SawCycleCompleted");
    public static readonly EventTypeId LineNoiseChanged = EventTypeId.From("LineNoiseChanged");
    public static readonly EventTypeId LogRouted = EventTypeId.From("LogRouted");
    public static readonly EventTypeId LogWrittenOff = EventTypeId.From("LogWrittenOff");
    public static readonly EventTypeId ProcedureActionStarted = EventTypeId.From("ProcedureActionStarted");
    public static readonly EventTypeId ProcedureActionCompleted = EventTypeId.From("ProcedureActionCompleted");
    public static readonly EventTypeId ConfirmationTestStarted = EventTypeId.From("ConfirmationTestStarted");
    public static readonly EventTypeId ConfirmationTestCompleted = EventTypeId.From("ConfirmationTestCompleted");
    public static readonly EventTypeId ContainmentRitualCompleted = EventTypeId.From("ContainmentRitualCompleted");
    public static readonly EventTypeId ContainmentStateChanged = EventTypeId.From("ContainmentStateChanged");
    public static readonly EventTypeId ConfirmationConditionUpdated = EventTypeId.From("ConfirmationConditionUpdated");
    public static readonly EventTypeId ShiftCompleted = EventTypeId.From("ShiftCompleted");
}

/// <summary>Closed base for stage-7 payloads; construction is restricted to this Domain assembly.</summary>
public abstract class HostStageSevenEventPayload : IDomainEventPayload
{
    private protected HostStageSevenEventPayload() { }
}

public abstract class HostStageSevenVersionedPayload : HostStageSevenEventPayload
{
    internal HostStageSevenVersionedPayload(StateVersion priorStateVersion, StateVersion currentStateVersion)
    {
        if (priorStateVersion.IsDefault || currentStateVersion.IsDefault ||
            (currentStateVersion != priorStateVersion && currentStateVersion != priorStateVersion.Next()))
        {
            throw new ArgumentException("Stage-seven payload versions must be exact same-state or one-step evidence.");
        }

        PriorStateVersion = priorStateVersion;
        CurrentStateVersion = currentStateVersion;
    }

    public StateVersion PriorStateVersion { get; }
    public StateVersion CurrentStateVersion { get; }
}

public sealed class HostStageSevenLogTransitionPayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenLogTransitionPayload(LogId logId, LogState fromState, LogState toState, StateVersion prior, StateVersion current)
        : base(prior, current)
    {
        if (logId.IsDefault || !Enum.IsDefined(fromState) || !Enum.IsDefined(toState)) throw new ArgumentException("Log transition evidence must be initialized.");
        LogId = logId; FromState = fromState; ToState = toState;
    }
    public LogId LogId { get; }
    public LogState FromState { get; }
    public LogState ToState { get; }
}

public sealed class HostStageSevenFeedSchedulePayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenFeedSchedulePayload(PendingFeedSchedule schedule, StateVersion prior, StateVersion current)
        : base(prior, current)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        LogId = schedule.LogId; Kind = schedule.Kind; ScheduledAt = schedule.ScheduledAt; DueAt = schedule.DueAt; Delay = schedule.Delay; CausedByIntentId = schedule.CausedByIntentId;
    }
    public LogId LogId { get; }
    public FeedScheduleKind Kind { get; }
    public ServerTick ScheduledAt { get; }
    public ServerTick DueAt { get; }
    public SimulationDuration Delay { get; }
    public IntentId? CausedByIntentId { get; }
}

public sealed class HostStageSevenIntakeDeadlinePayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenIntakeDeadlinePayload(ActiveIntakeDeadline deadline, ServerTick occurredAt, StateVersion prior, StateVersion current)
        : base(prior, current)
    {
        ArgumentNullException.ThrowIfNull(deadline);
        LogId = deadline.LogId; StartedAt = deadline.StartedAt; DueAt = deadline.DueAt; Duration = deadline.Duration; OccurredAt = occurredAt;
    }
    public LogId LogId { get; }
    public ServerTick StartedAt { get; }
    public ServerTick DueAt { get; }
    public SimulationDuration Duration { get; }
    public ServerTick OccurredAt { get; }
}

public enum HostStageSevenAutoRouteOutcome { Applied, Blocked, OwnerMissing, NoLongerApplicable }

public sealed class HostStageSevenAutoRoutePayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenAutoRoutePayload(DefaultIntakeAutoRouteResult result, ServerTick currentTick, StateVersion prior, StateVersion current)
        : base(prior, current)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (currentTick.IsDefault) throw new ArgumentException("The authoritative stage-seven tick must be initialized.", nameof(currentTick));
        switch (result)
        {
            case DefaultIntakeAutoRouteApplied applied:
                if (applied.AttemptedAt != currentTick) throw new InvalidOperationException("Applied auto-route evidence must agree with the authoritative stage-seven tick.");
                LogId = applied.LogId; AttemptedAt = applied.AttemptedAt; Outcome = HostStageSevenAutoRouteOutcome.Applied;
                Source = applied.Source; Destination = applied.Destination; BlockReason = null; FollowUp = null;
                break;
            case DefaultIntakeAutoRouteBlocked blocked:
                if (blocked.AttemptedAt != currentTick) throw new InvalidOperationException("Blocked auto-route evidence must agree with the authoritative stage-seven tick.");
                LogId = blocked.LogId; AttemptedAt = blocked.AttemptedAt; Outcome = HostStageSevenAutoRouteOutcome.Blocked;
                Source = null; Destination = null; BlockReason = blocked.Reason; FollowUp = blocked.FollowUp;
                break;
            case DefaultIntakeAutoRouteOwnerMissing missing:
                // A frozen stage-five trace normally cannot reach this after exact expiration evidence, but preserve the host tick if established evidence is supplied.
                LogId = missing.LogId; AttemptedAt = currentTick; Outcome = HostStageSevenAutoRouteOutcome.OwnerMissing;
                Source = null; Destination = null; BlockReason = null; FollowUp = null;
                break;
            case DefaultIntakeAutoRouteNoLongerApplicable inapplicable:
                // A frozen stage-five trace normally cannot reach this after exact expiration evidence, but preserve the host tick if established evidence is supplied.
                LogId = inapplicable.LogId; AttemptedAt = currentTick; Outcome = HostStageSevenAutoRouteOutcome.NoLongerApplicable;
                Source = null; Destination = null; BlockReason = null; FollowUp = null;
                break;
            default:
                throw new ArgumentException("Unsupported default auto-route evidence.", nameof(result));
        }
    }
    public LogId LogId { get; }
    public ServerTick AttemptedAt { get; }
    public HostStageSevenAutoRouteOutcome Outcome { get; }
    public LogState? Source { get; }
    public LogState? Destination { get; }
    public DefaultIntakeAutoRouteBlockReason? BlockReason { get; }
    public DefaultIntakeAutoRouteFollowUp? FollowUp { get; }
}

public sealed class HostStageSevenProcedurePayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenProcedurePayload(ItemActionCompletionDescriptor descriptor)
        : base(descriptor.PriorStateVersion, descriptor.CurrentStateVersion)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
    }
    public ItemActionCompletionDescriptor Descriptor { get; }
}

/// <summary>Semantic evidence for the start of an existing procedure hold; no completion is implied.</summary>
public sealed class HostStageSevenProcedureActionStartedPayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenProcedureActionStartedPayload(ActiveProcedureHold hold, StateVersion prior, StateVersion current)
        : base(prior, current)
    {
        ArgumentNullException.ThrowIfNull(hold);
        LogId = hold.LogId;
        AnomalyId = hold.AnomalyId;
        AttemptedItem = hold.AttemptedItem;
        ProcedureStepIndex = hold.ProcedureStepIndex;
        StartedAt = hold.StartedAt;
        DueAt = hold.DueAt;
        Duration = hold.Duration;
    }

    public LogId LogId { get; }
    public AnomalyId AnomalyId { get; }
    public ItemId AttemptedItem { get; }
    public int ProcedureStepIndex { get; }
    public ServerTick StartedAt { get; }
    public ServerTick DueAt { get; }
    public SimulationDuration Duration { get; }
}

public sealed class HostStageSevenConfirmationPayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenConfirmationPayload(ConfirmationTestResult result, StateVersion prior, StateVersion current)
        : base(prior, current) { ArgumentNullException.ThrowIfNull(result); Result = result; }
    public ConfirmationTestResult Result { get; }
}

/// <summary>Semantic evidence for a configured confirmation-test start; no completion is implied.</summary>
public sealed class HostStageSevenConfirmationTestStartedPayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenConfirmationTestStartedPayload(ActiveConfirmationTest active, StateVersion prior, StateVersion current)
        : base(prior, current)
    {
        ArgumentNullException.ThrowIfNull(active);
        if (!active.IsRunning || active.SegmentStartedAt is not { } segmentStartedAt || active.DueAt is not { } dueAt)
        {
            throw new ArgumentException("Confirmation start evidence must retain a running active confirmation.", nameof(active));
        }

        LogId = active.LogId;
        AnomalyId = active.AnomalyId;
        RequiredTools = active.Plan.RequiredTools;
        Duration = active.Plan.Duration;
        Continuous = active.Plan.Continuous;
        RequiredLineNoise = active.Plan.RequiredLineNoise;
        ResetWhenConditionLost = active.Plan.ResetWhenConditionLost;
        Result = active.Plan.Result;
        SegmentStartedAt = segmentStartedAt;
        DueAt = dueAt;
    }

    public LogId LogId { get; }
    public AnomalyId AnomalyId { get; }
    public ImmutableHashSet<ItemId> RequiredTools { get; }
    public SimulationDuration Duration { get; }
    public bool Continuous { get; }
    public LineNoise? RequiredLineNoise { get; }
    public bool ResetWhenConditionLost { get; }
    public string Result { get; }
    public ServerTick SegmentStartedAt { get; }
    public ServerTick DueAt { get; }
}

public sealed class HostStageSevenContainmentPayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenContainmentPayload(ContainmentRuntimeState priorContainment, ContainmentRuntimeState currentContainment, ActiveContainmentRitual? ritual, ContainmentIncidentDescriptor? incident, StateVersion prior, StateVersion current)
        : base(prior, current)
    {
        ArgumentNullException.ThrowIfNull(priorContainment); ArgumentNullException.ThrowIfNull(currentContainment);
        PriorContainment = priorContainment; CurrentContainment = currentContainment; Ritual = ritual; Incident = incident;
    }
    public ContainmentRuntimeState PriorContainment { get; }
    public ContainmentRuntimeState CurrentContainment { get; }
    public ActiveContainmentRitual? Ritual { get; }
    public ContainmentIncidentDescriptor? Incident { get; }
}

public sealed class HostStageSevenRepairPayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenRepairPayload(LineRuntimeState priorLine, LineRuntimeState currentLine, PendingLineTransitionDescriptor? pendingTransition, StateVersion prior, StateVersion current)
        : base(prior, current)
    {
        ArgumentNullException.ThrowIfNull(priorLine); ArgumentNullException.ThrowIfNull(currentLine);
        PriorLine = priorLine; CurrentLine = currentLine; PendingTransition = pendingTransition;
    }
    public LineRuntimeState PriorLine { get; }
    public LineRuntimeState CurrentLine { get; }
    public PendingLineTransitionDescriptor? PendingTransition { get; }
}

public sealed class HostStageSevenSawStartedPayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenSawStartedPayload(SawCycleStarted started)
        : base(started.PriorStateVersion, started.CurrentStateVersion) { ArgumentNullException.ThrowIfNull(started); Cycle = started.Cycle; }
    public ActiveSawCycle Cycle { get; }
}

public sealed class HostStageSevenSawCompletedPayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenSawCompletedPayload(SawCycleCompleted completed, SawQuotaApplicationResult quota)
        : base(completed.PriorStateVersion, completed.CurrentStateVersion)
    {
        ArgumentNullException.ThrowIfNull(completed); ArgumentNullException.ThrowIfNull(quota);
        if (quota.CompletedLogId != completed.Cycle.LogId || !ReferenceEquals(quota.Resolution, completed.Resolution))
        {
            throw new InvalidOperationException("Saw quota evidence must retain the exact completed cycle and processing resolution.");
        }

        Cycle = completed.Cycle;
        Resolution = completed.Resolution;
        CompletedAt = completed.CompletedAt;
        QuotaSettlement = completed.Resolution.Settlement;
        QuotaApplicationLogId = quota.CompletedLogId;
        switch (quota)
        {
            case SawQuotaApplicationAccepted accepted:
                QuotaApplicationOutcome = HostStageSevenSawQuotaOutcome.Accepted;
                AcceptedQuotaSettlement = accepted.AcceptedSettlement.Descriptor;
                DuplicateQuotaSettlementLogId = null;
                break;
            case SawQuotaApplicationAlreadyApplied duplicate:
                QuotaApplicationOutcome = HostStageSevenSawQuotaOutcome.AlreadyApplied;
                AcceptedQuotaSettlement = null;
                DuplicateQuotaSettlementLogId = duplicate.DuplicateSettlement.LogId;
                break;
            default:
                throw new ArgumentException("Unsupported saw quota application evidence.", nameof(quota));
        }
    }
    public ActiveSawCycle Cycle { get; }
    public ProcessingResolution Resolution { get; }
    public ServerTick CompletedAt { get; }
    public QuotaSettlement QuotaSettlement { get; }
    public LogId QuotaApplicationLogId { get; }
    public HostStageSevenSawQuotaOutcome QuotaApplicationOutcome { get; }
    public QuotaSettlementDescriptor? AcceptedQuotaSettlement { get; }
    public LogId? DuplicateQuotaSettlementLogId { get; }
    public bool QuotaWasApplied => QuotaApplicationOutcome == HostStageSevenSawQuotaOutcome.Accepted;
    public QuotaSettlementDescriptor? QuotaSettlementDescriptor => AcceptedQuotaSettlement;
}

public enum HostStageSevenSawQuotaOutcome { Accepted, AlreadyApplied }

public sealed class HostStageSevenLineJamPayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenLineJamPayload(LogId logId, JamCause cause, ServerTick enteredAt, StateVersion prior, StateVersion current)
        : base(prior, current) { LogId = logId; Cause = cause; EnteredAt = enteredAt; }
    public LogId LogId { get; }
    public JamCause Cause { get; }
    public ServerTick EnteredAt { get; }
}

public sealed class HostStageSevenLineNoisePayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenLineNoisePayload(LineNoiseChanged change, StateVersion version)
        : base(version, version) { ArgumentNullException.ThrowIfNull(change); Change = change; }
    public LineNoiseChanged Change { get; }
}

public sealed class HostStageSevenConfirmationConditionPayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenConfirmationConditionPayload(ActiveConfirmationTest? prior, ActiveConfirmationTest? current, StateVersion priorVersion, StateVersion currentVersion)
        : base(priorVersion, currentVersion) { Prior = prior; Current = current; }
    public ActiveConfirmationTest? Prior { get; }
    public ActiveConfirmationTest? Current { get; }
}

public sealed class HostStageSevenShiftCompletedPayload : HostStageSevenVersionedPayload
{
    internal HostStageSevenShiftCompletedPayload(ShiftCompletionEvidence completion, StateVersion version)
        : base(version, version)
    {
        ArgumentNullException.ThrowIfNull(completion);
        CompletedAt = completion.CompletedAt; HardDeadlineAt = completion.HardDeadlineAt; Reason = completion.Reason;
        AllLogsTerminal = completion.AllLogsTerminal; HardDeadlineReached = completion.HardDeadlineReached; ObjectivesSatisfied = completion.ObjectivesSatisfied;
        ProcessedCount = completion.ProcessedCount; WrittenOffCount = completion.WrittenOffCount;
        TargetTotal = completion.Quota.TargetTotal; TotalCreditedUnits = completion.Quota.TotalCreditedUnits;
        MinimumCorrectlyProcessedAnomalies = completion.Quota.MinimumCorrectlyProcessedAnomalies; CorrectlyProcessedAnomalies = completion.Quota.CorrectlyProcessedAnomalies;
        TargetBySpecies = completion.Quota.TargetBySpecies;
        CreditedBySpecies = completion.Quota.CreditedBySpecies;
    }
    public ServerTick CompletedAt { get; }
    public ServerTick HardDeadlineAt { get; }
    public ShiftCompletionReason Reason { get; }
    public bool AllLogsTerminal { get; }
    public bool HardDeadlineReached { get; }
    public bool ObjectivesSatisfied { get; }
    public int ProcessedCount { get; }
    public int WrittenOffCount { get; }
    public int TargetTotal { get; }
    public int TotalCreditedUnits { get; }
    public int MinimumCorrectlyProcessedAnomalies { get; }
    public int CorrectlyProcessedAnomalies { get; }
    public ImmutableDictionary<SpeciesId, int> TargetBySpecies { get; }
    public ImmutableDictionary<SpeciesId, int> CreditedBySpecies { get; }
}

public enum HostStageSevenPublicationKind { StateChanging, Observational }

public sealed class HostStageSevenPublication
{
    internal HostStageSevenPublication(EventEnvelope envelope, HostStageSevenPublicationKind kind, ShiftRuntimeState beforeState, ShiftRuntimeState currentState)
    {
        ArgumentNullException.ThrowIfNull(envelope); ArgumentNullException.ThrowIfNull(beforeState); ArgumentNullException.ThrowIfNull(currentState);
        Envelope = envelope; Kind = kind; BeforeState = beforeState; CurrentState = currentState;
    }
    public EventEnvelope Envelope { get; }
    public HostStageSevenPublicationKind Kind { get; }
    public ShiftRuntimeState BeforeState { get; }
    public ShiftRuntimeState CurrentState { get; }
}

public sealed class HostStageSevenJournalCursor
{
    internal HostStageSevenJournalCursor(IEventJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ShiftId = journal.Shift; LastSequence = journal.LastSequence; LastTick = journal.LastTick; LastStateVersion = journal.LastStateVersion; Count = journal.Count;
    }
    public ShiftId ShiftId { get; }
    public EventSequence LastSequence { get; }
    public ServerTick LastTick { get; }
    public StateVersion LastStateVersion { get; }
    public int Count { get; }
}

public abstract class HostStageSevenEventExecution
{
    private protected HostStageSevenEventExecution(HostStageOneCompletionExecution stageOne, AcceptedIntentStageExecution stageTwo, HostStageThreeDeadlineExecution stageThree, HostStageFourSawExecution stageFour, HostStageFiveFeedExecution stageFive, HostStageSixDerivedExecution stageSix, ServerTick currentTick, HostStageSevenJournalCursor beforeCursor, HostStageSevenJournalCursor afterCursor)
    {
        StageOne = stageOne; StageTwo = stageTwo; StageThree = stageThree; StageFour = stageFour; StageFive = stageFive; StageSix = stageSix;
        CurrentTick = currentTick; BeforeCursor = beforeCursor; AfterCursor = afterCursor;
    }
    public HostStageOneCompletionExecution StageOne { get; }
    public AcceptedIntentStageExecution StageTwo { get; }
    public HostStageThreeDeadlineExecution StageThree { get; }
    public HostStageFourSawExecution StageFour { get; }
    public HostStageFiveFeedExecution StageFive { get; }
    public HostStageSixDerivedExecution StageSix { get; }
    public ServerTick CurrentTick { get; }
    public HostStageSevenJournalCursor BeforeCursor { get; }
    public HostStageSevenJournalCursor AfterCursor { get; }
    public ShiftRuntimeState FinalShiftState => StageSix.FinalShiftState;
    public QuotaRuntimeState FinalQuotaState => StageSix.FinalQuotaState;
    public LineNoiseRuntimeState FinalLineNoise => StageSix.FinalLineNoise;
    public HostTickCheckpointResult Checkpoint => StageSix.Checkpoint;
}

public sealed class HostStageSevenPublished : HostStageSevenEventExecution
{
    internal HostStageSevenPublished(HostStageOneCompletionExecution one, AcceptedIntentStageExecution two, HostStageThreeDeadlineExecution three, HostStageFourSawExecution four, HostStageFiveFeedExecution five, HostStageSixDerivedExecution six, ServerTick tick, HostStageSevenJournalCursor before, HostStageSevenJournalCursor after, ImmutableArray<HostStageSevenPublication> publications, ImmutableArray<RejectionEvent> rejections, ImmutableArray<EventId> assignedEventIds)
        : base(one, two, three, four, five, six, tick, before, after)
    { Publications = publications; Rejections = rejections; AssignedEventIds = assignedEventIds; }
    public ImmutableArray<HostStageSevenPublication> Publications { get; }
    public ImmutableArray<RejectionEvent> Rejections { get; }
    public ImmutableArray<EventId> AssignedEventIds { get; }
    public int JournalEventCount => Publications.Length;
    public int RejectionCount => Rejections.Length;
}

public sealed class HostStageSevenBlocked : HostStageSevenEventExecution
{
    internal HostStageSevenBlocked(HostStageOneCompletionExecution one, AcceptedIntentStageExecution two, HostStageThreeDeadlineExecution three, HostStageFourSawExecution four, HostStageFiveFeedExecution five, HostStageSixDerivedExecution six, ServerTick tick, HostStageSevenJournalCursor cursor, HostTickCheckpointRejected checkpoint)
        : base(one, two, three, four, five, six, tick, cursor, cursor) { CheckpointRejection = checkpoint; }
    public HostTickCheckpointRejected CheckpointRejection { get; }
}

public sealed class HostStageSevenAlreadyPublished : HostStageSevenEventExecution
{
    internal HostStageSevenAlreadyPublished(HostStageOneCompletionExecution one, AcceptedIntentStageExecution two, HostStageThreeDeadlineExecution three, HostStageFourSawExecution four, HostStageFiveFeedExecution five, HostStageSixDerivedExecution six, ServerTick tick, HostStageSevenJournalCursor cursor, ImmutableArray<EventId> eventIds)
        : base(one, two, three, four, five, six, tick, cursor, cursor) { AssignedEventIds = eventIds; }
    public ImmutableArray<EventId> AssignedEventIds { get; }
}

/// <summary>Stage seven accepted a valid trace that has no journal event to append; its cursor remains unchanged.</summary>
public sealed class HostStageSevenNoNewPublication : HostStageSevenEventExecution
{
    internal HostStageSevenNoNewPublication(HostStageOneCompletionExecution one, AcceptedIntentStageExecution two, HostStageThreeDeadlineExecution three, HostStageFourSawExecution four, HostStageFiveFeedExecution five, HostStageSixDerivedExecution six, ServerTick tick, HostStageSevenJournalCursor cursor, ImmutableArray<RejectionEvent> rejections, ImmutableArray<EventId> eventIds)
        : base(one, two, three, four, five, six, tick, cursor, cursor)
    {
        Rejections = rejections;
        AssignedEventIds = eventIds;
    }

    public ImmutableArray<RejectionEvent> Rejections { get; }
    public ImmutableArray<EventId> AssignedEventIds { get; }
}

public sealed class HostStageSevenEventExecutor
{
    private readonly JournaledMutationCommitService _mutations = new();

    public HostStageSevenEventExecution Execute(
        HostStageOneCompletionExecution stageOne,
        AcceptedIntentStageExecution stageTwo,
        HostStageThreeDeadlineExecution stageThree,
        HostStageFourSawExecution stageFour,
        HostStageFiveFeedExecution stageFive,
        HostStageSixDerivedExecution stageSix,
        IEventJournal journal,
        ImmutableArray<EventId> eventIds,
        ServerTick currentTick)
    {
        ValidateInputs(stageOne, stageTwo, stageThree, stageFour, stageFive, stageSix, journal, eventIds, currentTick);
        var beforeCursor = new HostStageSevenJournalCursor(journal);
        if (stageSix.Checkpoint is HostTickCheckpointRejected rejected)
        {
            return new HostStageSevenBlocked(stageOne, stageTwo, stageThree, stageFour, stageFive, stageSix, currentTick, beforeCursor, rejected);
        }

        var plan = BuildPlan(stageOne, stageTwo, stageThree, stageFour, stageFive, stageSix, currentTick, out var rejections);
        ValidatePlan(plan, eventIds, stageOne.InitialState, stageSix.FinalShiftState, journal, currentTick);

        if (plan.Count == 0)
        {
            RequireNoNewPublicationCursor(journal, stageSix.FinalShiftState, currentTick, "Zero-event publication");
            return new HostStageSevenNoNewPublication(stageOne, stageTwo, stageThree, stageFour, stageFive, stageSix, currentTick, beforeCursor, rejections, eventIds);
        }

        if (stageSix.Checkpoint is HostTickCheckpointReplayed)
        {
            RequirePublishedCursor(journal, stageSix.FinalShiftState, currentTick, "Replayed checkpoint");
            ValidatePublishedPlan(journal, plan, eventIds, currentTick);
            return new HostStageSevenAlreadyPublished(stageOne, stageTwo, stageThree, stageFour, stageFive, stageSix, currentTick, beforeCursor, eventIds);
        }

        if (journal.LastStateVersion == stageSix.FinalShiftState.StateVersion && journal.LastTick == currentTick)
        {
            ValidatePublishedPlan(journal, plan, eventIds, currentTick);
            return new HostStageSevenAlreadyPublished(stageOne, stageTwo, stageThree, stageFour, stageFive, stageSix, currentTick, beforeCursor, eventIds);
        }

        if (journal.LastStateVersion != stageOne.InitialState.StateVersion || currentTick < journal.LastTick)
        {
            throw new ArgumentException("Journal cursor must align with the exact stage-one initial state before new publication.", nameof(journal));
        }

        ValidateSequenceCapacity(journal.LastSequence, plan.Count);

        var publications = ImmutableArray.CreateBuilder<HostStageSevenPublication>(plan.Count);
        for (var index = 0; index < plan.Count; index++)
        {
            var planned = plan[index];
            var draft = new DomainEventDraft(eventIds[index], planned.EventType, planned.Payload, planned.CausedByIntentId);
            EventEnvelope envelope;
            if (planned.Kind == HostStageSevenPublicationKind.StateChanging)
            {
                var result = _mutations.Commit(journal, planned.BeforeState, planned.CurrentState, currentTick, draft);
                envelope = result is JournaledMutationCommitted committed
                    ? committed.Envelope
                    : throw new InvalidOperationException("A fully preflighted stage-seven mutation commit was rejected by the journal boundary.");
            }
            else
            {
                envelope = CommitObservation(journal, planned.CurrentState, currentTick, draft);
            }

            publications.Add(new HostStageSevenPublication(envelope, planned.Kind, planned.BeforeState, planned.CurrentState));
        }

        return new HostStageSevenPublished(stageOne, stageTwo, stageThree, stageFour, stageFive, stageSix, currentTick, beforeCursor, new HostStageSevenJournalCursor(journal), publications.MoveToImmutable(), rejections, eventIds);
    }

    private static EventEnvelope CommitObservation(IEventJournal journal, ShiftRuntimeState state, ServerTick tick, DomainEventDraft draft)
    {
        if (journal.Shift != state.ShiftId || journal.LastStateVersion != state.StateVersion || tick < journal.LastTick || !journal.LastSequence.TryNext(out var sequence))
        {
            throw new InvalidOperationException("Observation commit cursor invariant failed after stage-seven preflight.");
        }
        var envelope = new EventEnvelope { ShiftId = state.ShiftId, EventId = draft.EventId, Sequence = sequence, CausedByIntentId = draft.CausedByIntentId, ServerTick = tick, StateVersionAfter = state.StateVersion, EventType = draft.EventType, Payload = draft.Payload };
        var outcome = journal.TryAppend(envelope);
        if (outcome != JournalAppendOutcome.Accepted) throw new JournalInvariantViolationException(outcome);
        return envelope;
    }

    private static void ValidateInputs(HostStageOneCompletionExecution one, AcceptedIntentStageExecution two, HostStageThreeDeadlineExecution three, HostStageFourSawExecution four, HostStageFiveFeedExecution five, HostStageSixDerivedExecution six, IEventJournal journal, ImmutableArray<EventId> eventIds, ServerTick tick)
    {
        ArgumentNullException.ThrowIfNull(one); ArgumentNullException.ThrowIfNull(two); ArgumentNullException.ThrowIfNull(three); ArgumentNullException.ThrowIfNull(four); ArgumentNullException.ThrowIfNull(five); ArgumentNullException.ThrowIfNull(six); ArgumentNullException.ThrowIfNull(journal);
        if (eventIds.IsDefault) throw new ArgumentException("Event identities must be an initialized immutable array.", nameof(eventIds));
        if (eventIds.Any(id => id.IsDefault)) throw new ArgumentException("Every event identity must be initialized.", nameof(eventIds));
        if (tick.IsDefault) throw new ArgumentException("Current tick must be initialized.", nameof(tick));
        if (!ReferenceEquals(two.InitialState, one.FinalState) || !ReferenceEquals(three.InitialState, two.FinalState) || !ReferenceEquals(four.InitialShiftState, three.FinalState) || !ReferenceEquals(five.InitialState, four.FinalShiftState) || !ReferenceEquals(six.InitialShiftState, five.FinalState))
            throw new ArgumentException("Stages one through six must form the exact host-tick reference chain.");
        if (!ReferenceEquals(five.LineRepairSource, one.LineRepair.Result) || !ReferenceEquals(five.IntakeExpirationSource, three.IntakeDeadline.Result) || !ReferenceEquals(six.InitialQuotaState, four.FinalQuotaState) || six.CurrentTick != tick || two.Batch.CurrentTick != tick)
            throw new ArgumentException("Retained stage evidence must agree exactly with its frozen source.");
        var shift = one.InitialState.ShiftId; var seed = one.InitialState.ShiftSeed;
        foreach (var state in States(one, two, three, four, five, six)) RequireStateIdentity(state, shift, seed);
        if (six.InitialMovementNoise.ShiftId != shift || six.FinalMovementNoise.ShiftId != shift ||
            six.InitialLineNoise.ShiftId != shift || six.FinalLineNoise.ShiftId != shift ||
            six.Progression.ShiftId != shift || six.Lifecycle.ShiftId != shift ||
            six.ActiveTools.Any(tool => tool.IsDefault))
        {
            throw new ArgumentException("Every retained stage-six runtime and checkpoint input must belong to the exact shift.");
        }
        if (two.Batch.ShiftId != shift || two.Steps.IsDefault || two.Steps.Length != two.Batch.Intents.Length || journal.Shift != shift || journal.Shift.IsDefault || journal.LastStateVersion.IsDefault || journal.LastTick.IsDefault)
            throw new ArgumentException("Stage, batch, and journal identities must belong to one initialized shift.");
        for (var index = 0; index < two.Steps.Length; index++)
        {
            if (!ReferenceEquals(two.Steps[index].Receipt, two.Batch.Intents[index]) || two.Steps[index].Receipt.Envelope.ShiftId != shift)
                throw new ArgumentException("Stage-two receipt identity must remain exact.");
        }
    }

    private static IEnumerable<ShiftRuntimeState> States(HostStageOneCompletionExecution one, AcceptedIntentStageExecution two, HostStageThreeDeadlineExecution three, HostStageFourSawExecution four, HostStageFiveFeedExecution five, HostStageSixDerivedExecution six)
    {
        yield return one.InitialState; yield return one.Procedure.BeforeState; yield return one.Procedure.AfterState; yield return one.Confirmation.AfterState; yield return one.ContainmentRitual.AfterState; yield return one.LineRepair.AfterState;
        yield return two.InitialState; foreach (var step in two.Steps) { yield return step.BeforeState; yield return step.AfterState; }
        yield return three.InitialState; yield return three.IntakeDeadline.AfterState; yield return three.Containment.AfterState;
        yield return four.InitialShiftState; yield return four.Completion.AfterShiftState; yield return four.Start.AfterShiftState;
        yield return five.InitialState; yield return five.InitialFeedPlanningStep.AfterState; yield return five.RepairStep.AfterState; yield return five.RepairFollowUpStep.AfterState; yield return five.DefaultRouteStep.AfterState; yield return five.GenericNormalPlanningStep.AfterState; yield return five.FeedDueStep.AfterState; yield return five.OrdinaryDeadlineStartStep.AfterState;
        yield return six.InitialShiftState; yield return six.IntakeAutoFeedJamStep.AfterShiftState; yield return six.FeedGateJamStep.AfterShiftState; yield return six.ConfirmationStep.AfterShiftState;
    }

    private static void RequireStateIdentity(ShiftRuntimeState state, ShiftId shift, ShiftSeed seed)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ShiftId.IsDefault || state.StateVersion.IsDefault || state.ShiftId != shift || state.ShiftSeed != seed) throw new ArgumentException("Every retained runtime state must share the exact shift identity and seed.");
    }

    private static List<PlannedPublication> BuildPlan(HostStageOneCompletionExecution one, AcceptedIntentStageExecution two, HostStageThreeDeadlineExecution three, HostStageFourSawExecution four, HostStageFiveFeedExecution five, HostStageSixDerivedExecution six, ServerTick tick, out ImmutableArray<RejectionEvent> rejections)
    {
        var plan = new List<PlannedPublication>(); var rejectionBuilder = ImmutableArray.CreateBuilder<RejectionEvent>();
        PlanStageOne(plan, one); PlanStageTwo(plan, two, tick, rejectionBuilder); PlanStageThree(plan, three); PlanStageFour(plan, four); PlanStageFive(plan, five, tick); PlanStageSix(plan, six);
        rejections = rejectionBuilder.ToImmutable(); return plan;
    }

    private static void PlanStageOne(List<PlannedPublication> plan, HostStageOneCompletionExecution stage)
    {
        if (stage.Procedure.Result is ProcedureActionDueCompleted procedure) AddMutation(plan, HostStageSevenEventTypes.ProcedureActionCompleted, new HostStageSevenProcedurePayload(procedure.Descriptor), null, stage.Procedure.BeforeState, procedure.State); else RequireNoMutation(stage.Procedure.BeforeState, stage.Procedure.AfterState);
        if (stage.Confirmation.Result is ConfirmationTestDueCompleted)
        {
            var active = stage.Confirmation.BeforeState.ActiveConfirmationTest ?? throw new InvalidOperationException("Confirmation completion requires prior active evidence.");
            if (!stage.Confirmation.AfterState.TryGetConfirmationResult(active.LogId, out var result)) throw new InvalidOperationException("Confirmation completion requires exact retained result evidence.");
            AddMutation(plan, HostStageSevenEventTypes.ConfirmationTestCompleted, new HostStageSevenConfirmationPayload(result, stage.Confirmation.BeforeState.StateVersion, stage.Confirmation.AfterState.StateVersion), null, stage.Confirmation.BeforeState, stage.Confirmation.AfterState);
        }
        else RequireNoMutation(stage.Confirmation.BeforeState, stage.Confirmation.AfterState);
        if (stage.ContainmentRitual.Result is ContainmentRitualCompleted)
            AddMutation(plan, HostStageSevenEventTypes.ContainmentRitualCompleted, new HostStageSevenContainmentPayload(stage.ContainmentRitual.BeforeState.Containment, stage.ContainmentRitual.AfterState.Containment, stage.ContainmentRitual.BeforeState.ActiveContainmentRitual, null, stage.ContainmentRitual.BeforeState.StateVersion, stage.ContainmentRitual.AfterState.StateVersion), null, stage.ContainmentRitual.BeforeState, stage.ContainmentRitual.AfterState);
        else RequireNoMutation(stage.ContainmentRitual.BeforeState, stage.ContainmentRitual.AfterState);
        if (stage.LineRepair.Result is LineRepairCompleted repair)
            AddMutation(plan, HostStageSevenEventTypes.RepairCompleted, new HostStageSevenRepairPayload(stage.LineRepair.BeforeState.Line, stage.LineRepair.AfterState.Line, repair.PendingTransition, stage.LineRepair.BeforeState.StateVersion, stage.LineRepair.AfterState.StateVersion), null, stage.LineRepair.BeforeState, stage.LineRepair.AfterState);
        else RequireNoMutation(stage.LineRepair.BeforeState, stage.LineRepair.AfterState);
    }

    private static void PlanStageTwo(List<PlannedPublication> plan, AcceptedIntentStageExecution stage, ServerTick tick, ImmutableArray<RejectionEvent>.Builder rejections)
    {
        foreach (var step in stage.Steps)
        {
            var intentId = step.Receipt.Envelope.IntentId;
            switch (step.Outcome)
            {
                case ManualRoutingIntentStageOutcome { Result: ManualLogIntentAccepted accepted }:
                    AddMutation(plan, accepted.Transition.ToState == LogState.HELD_WRITTEN_OFF ? HostStageSevenEventTypes.LogWrittenOff : HostStageSevenEventTypes.LogRouted,
                        new HostStageSevenLogTransitionPayload(accepted.Transition.LogId, accepted.Transition.FromState, accepted.Transition.ToState, accepted.Transition.PriorStateVersion, accepted.Transition.CurrentStateVersion), intentId, step.BeforeState, accepted.State);
                    break;
                case ManualRoutingIntentStageOutcome { Result: ManualLogIntentRejected rejected }:
                    RequireNoMutation(step.BeforeState, rejected.State); rejections.Add(new RejectionEvent { ShiftId = step.Receipt.Envelope.ShiftId, IntentId = intentId, ServerTick = tick, CurrentStateVersion = step.BeforeState.StateVersion, Reason = rejected.Reason }); break;
                case ManualRoutingIntentStageOutcome:
                    RequireNoMutation(step.BeforeState, step.AfterState); break;
                case EarlyFeedIntentStageOutcome { Result: EarlyFeedScheduled scheduled }:
                    AddObservation(plan, HostStageSevenEventTypes.EarlyFeedRequested, new HostStageSevenFeedSchedulePayload(scheduled.Schedule, step.BeforeState.StateVersion, step.BeforeState.StateVersion), intentId, step.BeforeState);
                    AddMutation(plan, HostStageSevenEventTypes.FeedScheduled, new HostStageSevenFeedSchedulePayload(scheduled.Schedule, scheduled.PriorStateVersion, scheduled.CurrentStateVersion), intentId, step.BeforeState, scheduled.State);
                    break;
                case EarlyFeedIntentStageOutcome { Result: EarlyFeedIntentRejected rejected }:
                    RequireNoMutation(step.BeforeState, rejected.State); rejections.Add(new RejectionEvent { ShiftId = step.Receipt.Envelope.ShiftId, IntentId = intentId, ServerTick = tick, CurrentStateVersion = step.BeforeState.StateVersion, Reason = rejected.Reason }); break;
                case ProcedureActionIntentStageOutcome { Result: ProcedureActionIntentHoldStarted started }:
                    AddMutation(plan, HostStageSevenEventTypes.ProcedureActionStarted,
                        new HostStageSevenProcedureActionStartedPayload(started.Result.Hold, step.BeforeState.StateVersion, started.State.StateVersion),
                        intentId, step.BeforeState, started.State);
                    break;
                case ProcedureActionIntentStageOutcome { Result: ProcedureActionIntentCompletedImmediately completed }:
                    AddMutation(plan, HostStageSevenEventTypes.ProcedureActionCompleted,
                        new HostStageSevenProcedurePayload(completed.Result.Descriptor),
                        intentId, step.BeforeState, completed.State);
                    break;
                case ProcedureActionIntentStageOutcome { Result: ProcedureActionIntentRejected rejected }:
                    RequireNoMutation(step.BeforeState, rejected.State); rejections.Add(new RejectionEvent { ShiftId = step.Receipt.Envelope.ShiftId, IntentId = intentId, ServerTick = tick, CurrentStateVersion = step.BeforeState.StateVersion, Reason = rejected.Reason }); break;
                case ProcedureActionIntentStageOutcome { Result: ProcedureActionIntentUnderlyingRejected rejected }:
                    RequireNoMutation(step.BeforeState, rejected.State); rejections.Add(new RejectionEvent { ShiftId = step.Receipt.Envelope.ShiftId, IntentId = intentId, ServerTick = tick, CurrentStateVersion = step.BeforeState.StateVersion, Reason = rejected.Reason }); break;
                case ProcedureActionIntentStageOutcome:
                    RequireNoMutation(step.BeforeState, step.AfterState); break;
                case ConfirmationTestIntentStageOutcome { Result: ConfirmationTestIntentStarted started }:
                    var active = started.State.ActiveConfirmationTest ?? throw new InvalidOperationException("Confirmation start requires exact active confirmation evidence.");
                    AddMutation(plan, HostStageSevenEventTypes.ConfirmationTestStarted,
                        new HostStageSevenConfirmationTestStartedPayload(active, step.BeforeState.StateVersion, started.State.StateVersion),
                        intentId, step.BeforeState, started.State);
                    break;
                case ConfirmationTestIntentStageOutcome { Result: ConfirmationTestIntentRejected rejected }:
                    RequireNoMutation(step.BeforeState, rejected.State); rejections.Add(new RejectionEvent { ShiftId = step.Receipt.Envelope.ShiftId, IntentId = intentId, ServerTick = tick, CurrentStateVersion = step.BeforeState.StateVersion, Reason = rejected.Reason }); break;
                case ConfirmationTestIntentStageOutcome { Result: ConfirmationTestIntentUnderlyingRejected rejected }:
                    RequireNoMutation(step.BeforeState, rejected.State); rejections.Add(new RejectionEvent { ShiftId = step.Receipt.Envelope.ShiftId, IntentId = intentId, ServerTick = tick, CurrentStateVersion = step.BeforeState.StateVersion, Reason = rejected.Reason }); break;
                case ConfirmationTestIntentStageOutcome:
                    RequireNoMutation(step.BeforeState, step.AfterState); break;
                case EarlyFeedIntentStageOutcome:
                case UnsupportedIntentStageOutcome:
                    RequireNoMutation(step.BeforeState, step.AfterState); break;
                default: throw new InvalidOperationException("Unknown closed stage-two outcome.");
            }
        }
    }

    private static void PlanStageThree(List<PlannedPublication> plan, HostStageThreeDeadlineExecution stage)
    {
        if (stage.IntakeDeadline.Result is IntakeDeadlineExpired expired)
            AddMutation(plan, HostStageSevenEventTypes.IntakeDeadlineExpired, new HostStageSevenIntakeDeadlinePayload(expired.ExpiredDeadline, expired.ExpiredAt, expired.PriorStateVersion, expired.CurrentStateVersion), null, stage.IntakeDeadline.BeforeState, expired.State);
        else RequireNoMutation(stage.IntakeDeadline.BeforeState, stage.IntakeDeadline.AfterState);
        switch (stage.Containment.Result)
        {
            case ContainmentStableIntervalArmed or ContainmentStateAdvanced:
                AddMutation(plan, HostStageSevenEventTypes.ContainmentStateChanged, new HostStageSevenContainmentPayload(stage.Containment.BeforeState.Containment, stage.Containment.AfterState.Containment, stage.Containment.AfterState.ActiveContainmentRitual, null, stage.Containment.BeforeState.StateVersion, stage.Containment.AfterState.StateVersion), null, stage.Containment.BeforeState, stage.Containment.AfterState); break;
            case ContainmentIncidentEntered incident:
                AddMutation(plan, HostStageSevenEventTypes.ContainmentStateChanged, new HostStageSevenContainmentPayload(stage.Containment.BeforeState.Containment, stage.Containment.AfterState.Containment, stage.Containment.AfterState.ActiveContainmentRitual, incident.Descriptor, stage.Containment.BeforeState.StateVersion, stage.Containment.AfterState.StateVersion), null, stage.Containment.BeforeState, stage.Containment.AfterState); break;
            default: RequireNoMutation(stage.Containment.BeforeState, stage.Containment.AfterState); break;
        }
    }

    private static void PlanStageFour(List<PlannedPublication> plan, HostStageFourSawExecution stage)
    {
        if (stage.Completion.Result is SawCycleCompleted completed)
        {
            var quota = stage.Quota.Result ?? throw new InvalidOperationException("Completed saw evidence requires exact quota evidence.");
            AddMutation(plan, HostStageSevenEventTypes.SawCycleCompleted, new HostStageSevenSawCompletedPayload(completed, quota), null, stage.Completion.BeforeShiftState, completed.State);
        }
        else RequireNoMutation(stage.Completion.BeforeShiftState, stage.Completion.AfterShiftState);
        if (stage.Start.Result is SawCycleStarted started) AddMutation(plan, HostStageSevenEventTypes.SawCycleStarted, new HostStageSevenSawStartedPayload(started), null, stage.Start.BeforeShiftState, started.State); else RequireNoMutation(stage.Start.BeforeShiftState, stage.Start.AfterShiftState);
    }

    private static void PlanStageFive(List<PlannedPublication> plan, HostStageFiveFeedExecution stage, ServerTick currentTick)
    {
        PlanFeedSchedule(plan, stage.InitialFeedPlanning, stage.InitialFeedPlanningStep.BeforeState);
        if (stage.RepairExecution is RepairPendingTransitionExecuted repair)
        {
            var type = repair.Cause == JamCause.FEED_GATE_BLOCKED ? HostStageSevenEventTypes.LogAdmittedToIntake : HostStageSevenEventTypes.AutoRouteAttempted;
            if (type == HostStageSevenEventTypes.AutoRouteAttempted)
                AddMutation(plan, type, new HostStageSevenAutoRoutePayload(new DefaultIntakeAutoRouteApplied(repair.State, repair.LogId, repair.AppliedAt, repair.Source, repair.Destination, repair.PriorStateVersion, repair.CurrentStateVersion), currentTick, repair.PriorStateVersion, repair.CurrentStateVersion), null, stage.RepairStep.BeforeState, repair.State);
            else AddMutation(plan, type, new HostStageSevenLogTransitionPayload(repair.LogId, repair.Source, repair.Destination, repair.PriorStateVersion, repair.CurrentStateVersion), null, stage.RepairStep.BeforeState, repair.State);
        }
        else RequireNoMutation(stage.RepairStep.BeforeState, stage.RepairStep.AfterState);
        if (stage.RepairedDeadlineStart is IntakeDeadlineStarted repairedDeadline) AddMutation(plan, HostStageSevenEventTypes.IntakeDeadlineStarted, new HostStageSevenIntakeDeadlinePayload(repairedDeadline.Deadline, repairedDeadline.Deadline.StartedAt, repairedDeadline.PriorStateVersion, repairedDeadline.CurrentStateVersion), null, stage.RepairFollowUpStep.BeforeState, repairedDeadline.State);
        else if (stage.RepairedNormalPlanning is { } repairedNormal) PlanFeedSchedule(plan, repairedNormal, stage.RepairFollowUpStep.BeforeState);
        else RequireNoMutation(stage.RepairFollowUpStep.BeforeState, stage.RepairFollowUpStep.AfterState);
        if (stage.DefaultRoute is { } route) PlanAutoRoute(plan, route, currentTick, stage.DefaultRouteStep.BeforeState); else RequireNoMutation(stage.DefaultRouteStep.BeforeState, stage.DefaultRouteStep.AfterState);
        if (stage.GenericNormalPlanning is { } normal) PlanFeedSchedule(plan, normal, stage.GenericNormalPlanningStep.BeforeState); else RequireNoMutation(stage.GenericNormalPlanningStep.BeforeState, stage.GenericNormalPlanningStep.AfterState);
        if (stage.FeedDue is FeedDueResolved feed)
        {
            var type = feed.Disposition == FeedDueDisposition.AdmittedToIntake ? HostStageSevenEventTypes.LogAdmittedToIntake : HostStageSevenEventTypes.LogPlacedAtFeedGate;
            var destination = feed.Disposition == FeedDueDisposition.AdmittedToIntake ? LogState.AT_INTAKE : LogState.AT_FEED_GATE;
            AddMutation(plan, type, new HostStageSevenLogTransitionPayload(feed.ConsumedSchedule.LogId, LogState.SCHEDULED, destination, feed.PriorStateVersion, feed.CurrentStateVersion), feed.ConsumedSchedule.CausedByIntentId, stage.FeedDueStep.BeforeState, feed.State);
        }
        else RequireNoMutation(stage.FeedDueStep.BeforeState, stage.FeedDueStep.AfterState);
        if (stage.OrdinaryDeadlineStart is IntakeDeadlineStarted deadline)
        {
            var cause = stage.FeedDue is FeedDueResolved resolved ? resolved.ConsumedSchedule.CausedByIntentId : null;
            AddMutation(plan, HostStageSevenEventTypes.IntakeDeadlineStarted, new HostStageSevenIntakeDeadlinePayload(deadline.Deadline, deadline.Deadline.StartedAt, deadline.PriorStateVersion, deadline.CurrentStateVersion), cause, stage.OrdinaryDeadlineStartStep.BeforeState, deadline.State);
        }
        else RequireNoMutation(stage.OrdinaryDeadlineStartStep.BeforeState, stage.OrdinaryDeadlineStartStep.AfterState);
    }

    private static void PlanFeedSchedule(List<PlannedPublication> plan, InitialFeedPlanningResult result, ShiftRuntimeState before)
    {
        if (result is InitialFeedScheduled scheduled) AddMutation(plan, HostStageSevenEventTypes.FeedScheduled, new HostStageSevenFeedSchedulePayload(scheduled.Schedule, scheduled.PriorStateVersion, scheduled.CurrentStateVersion), null, before, scheduled.State); else RequireNoMutation(before, result.State);
    }
    private static void PlanFeedSchedule(List<PlannedPublication> plan, NormalFeedPlanningResult result, ShiftRuntimeState before)
    {
        if (result is NormalFeedScheduled scheduled) AddMutation(plan, HostStageSevenEventTypes.FeedScheduled, new HostStageSevenFeedSchedulePayload(scheduled.Schedule, scheduled.PriorStateVersion, scheduled.CurrentStateVersion), null, before, scheduled.State); else RequireNoMutation(before, result.State);
    }
    private static void PlanAutoRoute(List<PlannedPublication> plan, DefaultIntakeAutoRouteResult result, ServerTick currentTick, ShiftRuntimeState before)
    {
        var changing = result is DefaultIntakeAutoRouteApplied;
        var payload = new HostStageSevenAutoRoutePayload(result, currentTick, before.StateVersion, result.State.StateVersion);
        if (changing) AddMutation(plan, HostStageSevenEventTypes.AutoRouteAttempted, payload, null, before, result.State); else AddObservation(plan, HostStageSevenEventTypes.AutoRouteAttempted, payload, null, before);
    }

    private static void PlanStageSix(List<PlannedPublication> plan, HostStageSixDerivedExecution stage)
    {
        if (stage.IntakeAutoFeedJam is IntakeAutoFeedJamEntered intakeJam) AddMutation(plan, HostStageSevenEventTypes.LineJammed, new HostStageSevenLineJamPayload(intakeJam.LogId, intakeJam.Cause, intakeJam.EnteredAt, intakeJam.PriorStateVersion, intakeJam.CurrentStateVersion), null, stage.IntakeAutoFeedJamStep.BeforeShiftState, intakeJam.State); else RequireNoMutation(stage.IntakeAutoFeedJamStep.BeforeShiftState, stage.IntakeAutoFeedJamStep.AfterShiftState);
        if (stage.FeedGateJam is FeedGateJamDerived gateJam) AddMutation(plan, HostStageSevenEventTypes.LineJammed, new HostStageSevenLineJamPayload(gateJam.BlockedLogId, gateJam.Cause, gateJam.EnteredAt, gateJam.PriorStateVersion, gateJam.CurrentStateVersion), null, stage.FeedGateJamStep.BeforeShiftState, gateJam.State); else RequireNoMutation(stage.FeedGateJamStep.BeforeShiftState, stage.FeedGateJamStep.AfterShiftState);
        if (stage.LineNoiseEvaluation is LineNoiseEvaluatedWithChange changed) AddObservation(plan, HostStageSevenEventTypes.LineNoiseChanged, new HostStageSevenLineNoisePayload(changed.Change, stage.LineNoiseStep.ShiftState.StateVersion), null, stage.LineNoiseStep.ShiftState);
        if (stage.Confirmation is ConfirmationTestConditionUpdated) AddMutation(plan, HostStageSevenEventTypes.ConfirmationConditionUpdated, new HostStageSevenConfirmationConditionPayload(stage.ConfirmationStep.BeforeShiftState.ActiveConfirmationTest, stage.ConfirmationStep.AfterShiftState.ActiveConfirmationTest, stage.ConfirmationStep.BeforeShiftState.StateVersion, stage.ConfirmationStep.AfterShiftState.StateVersion), null, stage.ConfirmationStep.BeforeShiftState, stage.ConfirmationStep.AfterShiftState); else RequireNoMutation(stage.ConfirmationStep.BeforeShiftState, stage.ConfirmationStep.AfterShiftState);
        if (stage.Checkpoint is HostTickCheckpointAdvanced { Receipt.Evaluation: ShiftCompletionNewlyCompleted completed }) AddObservation(plan, HostStageSevenEventTypes.ShiftCompleted, new HostStageSevenShiftCompletedPayload(completed.Completion, stage.FinalShiftState.StateVersion), null, stage.FinalShiftState);
    }

    private static void AddMutation(List<PlannedPublication> plan, EventTypeId type, HostStageSevenEventPayload payload, IntentId? cause, ShiftRuntimeState before, ShiftRuntimeState after)
    {
        RequireMutation(before, after); plan.Add(new PlannedPublication(type, payload, cause, HostStageSevenPublicationKind.StateChanging, before, after));
    }
    private static void AddObservation(List<PlannedPublication> plan, EventTypeId type, HostStageSevenEventPayload payload, IntentId? cause, ShiftRuntimeState state)
    {
        plan.Add(new PlannedPublication(type, payload, cause, HostStageSevenPublicationKind.Observational, state, state));
    }
    private static void RequireMutation(ShiftRuntimeState before, ShiftRuntimeState after)
    {
        if (!before.StateVersion.TryNext(out var next) || after.StateVersion != next) throw new InvalidOperationException("Every state-changing stage trace must advance exactly one state version.");
    }
    private static void RequireNoMutation(ShiftRuntimeState before, ShiftRuntimeState after)
    {
        if (before.StateVersion != after.StateVersion) throw new InvalidOperationException("An unmapped stage result changed state and cannot be published safely.");
    }

    private static void ValidatePlan(List<PlannedPublication> plan, ImmutableArray<EventId> eventIds, ShiftRuntimeState initial, ShiftRuntimeState final, IEventJournal journal, ServerTick tick)
    {
        if (eventIds.Length != plan.Count) throw new ArgumentException("The supplied event identity count must exactly match the complete publication plan.", nameof(eventIds));
        var reached = initial.StateVersion;
        foreach (var entry in plan)
        {
            if (entry.Kind == HostStageSevenPublicationKind.StateChanging)
            {
                if (entry.BeforeState.StateVersion != reached || !reached.TryNext(out var expected) || entry.CurrentState.StateVersion != expected) throw new InvalidOperationException("Planned mutation versions must be contiguous in causal order.");
                reached = expected;
            }
            else if (entry.BeforeState.StateVersion != reached || entry.CurrentState.StateVersion != reached)
                throw new InvalidOperationException("Planned observation must retain the exact currently reached state version.");
        }
        if (reached != final.StateVersion) throw new InvalidOperationException("The planned journal state version must equal the exact final stage-six state.");
        if (tick < journal.LastTick) throw new ArgumentException("Journal tick cannot be later than the current host tick.", nameof(journal));
    }

    private static void ValidateSequenceCapacity(EventSequence lastSequence, int count)
    {
        var sequence = lastSequence;
        for (var index = 0; index < count; index++)
        {
            if (!sequence.TryNext(out sequence)) throw new OverflowException("The journal sequence cannot advance through the complete publication plan.");
        }
    }

    private static void RequirePublishedCursor(IEventJournal journal, ShiftRuntimeState finalState, ServerTick tick, string source)
    {
        if (journal.LastStateVersion != finalState.StateVersion || journal.LastTick != tick) throw new InvalidOperationException($"{source} contradicts the current journal cursor.");
    }

    private static void RequireNoNewPublicationCursor(IEventJournal journal, ShiftRuntimeState finalState, ServerTick tick, string source)
    {
        if (journal.LastStateVersion != finalState.StateVersion ||
            (journal.Count != 0 && journal.LastTick == tick))
        {
            throw new InvalidOperationException($"{source} contradicts the unchanged journal cursor.");
        }
    }

    private static void ValidatePublishedPlan(IEventJournal journal, List<PlannedPublication> plan, ImmutableArray<EventId> eventIds, ServerTick tick)
    {
        if (plan.Count == 0)
        {
            return;
        }

        if (journal.Count < plan.Count)
        {
            throw new InvalidOperationException("The aligned journal has too few events to prove this stage-seven publication was already completed.");
        }

        var first = journal.Count - plan.Count;
        for (var index = 0; index < plan.Count; index++)
        {
            var envelope = journal.Events[first + index];
            var planned = plan[index];
            if (envelope.EventId != eventIds[index] || envelope.EventType != planned.EventType ||
                envelope.CausedByIntentId != planned.CausedByIntentId || envelope.ServerTick != tick ||
                envelope.StateVersionAfter != planned.CurrentState.StateVersion ||
                !HasExactPayloadSemantics(envelope.Payload, planned.Payload))
            {
                throw new InvalidOperationException("The aligned journal cursor contradicts the exact stage-seven publication plan.");
            }
        }
    }

    private static bool HasExactPayloadSemantics(IDomainEventPayload actual, HostStageSevenEventPayload expected) =>
        (actual, expected) switch
        {
            (HostStageSevenLogTransitionPayload left, HostStageSevenLogTransitionPayload right) =>
                SameVersions(left, right) && left.LogId == right.LogId && left.FromState == right.FromState && left.ToState == right.ToState,
            (HostStageSevenFeedSchedulePayload left, HostStageSevenFeedSchedulePayload right) =>
                SameVersions(left, right) && left.LogId == right.LogId && left.Kind == right.Kind && left.ScheduledAt == right.ScheduledAt && left.DueAt == right.DueAt && left.Delay == right.Delay && left.CausedByIntentId == right.CausedByIntentId,
            (HostStageSevenIntakeDeadlinePayload left, HostStageSevenIntakeDeadlinePayload right) =>
                SameVersions(left, right) && left.LogId == right.LogId && left.StartedAt == right.StartedAt && left.DueAt == right.DueAt && left.Duration == right.Duration && left.OccurredAt == right.OccurredAt,
            (HostStageSevenAutoRoutePayload left, HostStageSevenAutoRoutePayload right) =>
                SameVersions(left, right) && left.LogId == right.LogId && left.AttemptedAt == right.AttemptedAt && left.Outcome == right.Outcome && left.Source == right.Source && left.Destination == right.Destination && left.BlockReason == right.BlockReason && left.FollowUp == right.FollowUp,
            (HostStageSevenProcedurePayload left, HostStageSevenProcedurePayload right) =>
                SameVersions(left, right) && left.Descriptor == right.Descriptor,
            (HostStageSevenProcedureActionStartedPayload left, HostStageSevenProcedureActionStartedPayload right) =>
                SameVersions(left, right) && left.LogId == right.LogId && left.AnomalyId == right.AnomalyId && left.AttemptedItem == right.AttemptedItem && left.ProcedureStepIndex == right.ProcedureStepIndex && left.StartedAt == right.StartedAt && left.DueAt == right.DueAt && left.Duration == right.Duration,
            (HostStageSevenConfirmationTestStartedPayload left, HostStageSevenConfirmationTestStartedPayload right) =>
                SameVersions(left, right) && left.LogId == right.LogId && left.AnomalyId == right.AnomalyId && left.RequiredTools.SetEquals(right.RequiredTools) && left.Duration == right.Duration && left.Continuous == right.Continuous && left.RequiredLineNoise == right.RequiredLineNoise && left.ResetWhenConditionLost == right.ResetWhenConditionLost && left.Result == right.Result && left.SegmentStartedAt == right.SegmentStartedAt && left.DueAt == right.DueAt,
            (HostStageSevenConfirmationPayload left, HostStageSevenConfirmationPayload right) =>
                SameVersions(left, right) && left.Result == right.Result,
            (HostStageSevenContainmentPayload left, HostStageSevenContainmentPayload right) =>
                SameVersions(left, right) && left.PriorContainment == right.PriorContainment && left.CurrentContainment == right.CurrentContainment && left.Ritual == right.Ritual && left.Incident == right.Incident,
            (HostStageSevenRepairPayload left, HostStageSevenRepairPayload right) =>
                SameVersions(left, right) && left.PriorLine == right.PriorLine && left.CurrentLine == right.CurrentLine && left.PendingTransition == right.PendingTransition,
            (HostStageSevenSawStartedPayload left, HostStageSevenSawStartedPayload right) =>
                SameVersions(left, right) && left.Cycle == right.Cycle,
            (HostStageSevenSawCompletedPayload left, HostStageSevenSawCompletedPayload right) =>
                SameVersions(left, right) && left.Cycle == right.Cycle && left.Resolution == right.Resolution && left.CompletedAt == right.CompletedAt && left.QuotaSettlement == right.QuotaSettlement && left.QuotaApplicationLogId == right.QuotaApplicationLogId && left.QuotaApplicationOutcome == right.QuotaApplicationOutcome && left.AcceptedQuotaSettlement == right.AcceptedQuotaSettlement && left.DuplicateQuotaSettlementLogId == right.DuplicateQuotaSettlementLogId,
            (HostStageSevenLineJamPayload left, HostStageSevenLineJamPayload right) =>
                SameVersions(left, right) && left.LogId == right.LogId && left.Cause == right.Cause && left.EnteredAt == right.EnteredAt,
            (HostStageSevenLineNoisePayload left, HostStageSevenLineNoisePayload right) =>
                SameVersions(left, right) && left.Change == right.Change,
            (HostStageSevenConfirmationConditionPayload left, HostStageSevenConfirmationConditionPayload right) =>
                SameVersions(left, right) && left.Prior == right.Prior && left.Current == right.Current,
            (HostStageSevenShiftCompletedPayload left, HostStageSevenShiftCompletedPayload right) =>
                SameVersions(left, right) && left.CompletedAt == right.CompletedAt && left.HardDeadlineAt == right.HardDeadlineAt && left.Reason == right.Reason && left.AllLogsTerminal == right.AllLogsTerminal && left.HardDeadlineReached == right.HardDeadlineReached && left.ObjectivesSatisfied == right.ObjectivesSatisfied && left.ProcessedCount == right.ProcessedCount && left.WrittenOffCount == right.WrittenOffCount && left.TargetTotal == right.TargetTotal && left.TotalCreditedUnits == right.TotalCreditedUnits && left.MinimumCorrectlyProcessedAnomalies == right.MinimumCorrectlyProcessedAnomalies && left.CorrectlyProcessedAnomalies == right.CorrectlyProcessedAnomalies && SameSpeciesValues(left.TargetBySpecies, right.TargetBySpecies) && SameSpeciesValues(left.CreditedBySpecies, right.CreditedBySpecies),
            _ => false
        };

    private static bool SameVersions(HostStageSevenVersionedPayload left, HostStageSevenVersionedPayload right) =>
        left.PriorStateVersion == right.PriorStateVersion && left.CurrentStateVersion == right.CurrentStateVersion;

    private static bool SameSpeciesValues(ImmutableDictionary<SpeciesId, int> left, ImmutableDictionary<SpeciesId, int> right)
    {
        if (left.Count != right.Count) return false;
        foreach (var entry in left)
        {
            if (!right.TryGetValue(entry.Key, out var value) || value != entry.Value) return false;
        }

        return true;
    }

    private sealed record PlannedPublication(EventTypeId EventType, HostStageSevenEventPayload Payload, IntentId? CausedByIntentId, HostStageSevenPublicationKind Kind, ShiftRuntimeState BeforeState, ShiftRuntimeState CurrentState);
}
