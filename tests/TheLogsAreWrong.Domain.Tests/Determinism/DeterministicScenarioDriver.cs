using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Determinism;

// TLAW-012 test-only deterministic composition.
internal enum DeterministicScenarioOperationKind
{
    MovePenitentToGate,
    MovePenitentToIntake,
    MoveBlockerToGate,
    EnterJam,
    StartRepair,
    MovePenitentToProcedure,
    StartPenitentHold,
    CompletePenitentHoldEarly,
    CompletePenitentHoldDue,
    CompleteRepairEarly,
    CompleteRepairDue
}

internal sealed class DeterministicScenarioOperation
{
    public DeterministicScenarioOperation(
        DeterministicScenarioOperationKind kind,
        ServerTick tick,
        EventId? eventId,
        EventTypeId? eventType,
        IntentId? causedByIntentId,
        DeterministicScenarioPayload payload)
    {
        if (tick.IsDefault)
        {
            throw new ArgumentException("Scenario operation tick must be initialized.", nameof(tick));
        }

        if (eventId is { } id && id.IsDefault)
        {
            throw new ArgumentException("Scenario event identity must be initialized when present.", nameof(eventId));
        }

        if (eventType is { } type && type.IsDefault)
        {
            throw new ArgumentException("Scenario event type must be initialized when present.", nameof(eventType));
        }

        if ((eventId is null) != (eventType is null))
        {
            throw new ArgumentException("Scenario event identity and type must either both be present or both be absent.");
        }

        if (causedByIntentId is { } intent && intent.IsDefault)
        {
            throw new ArgumentException("Scenario causation intent must be initialized when present.", nameof(causedByIntentId));
        }

        Kind = kind;
        Tick = tick;
        EventId = eventId;
        EventType = eventType;
        CausedByIntentId = causedByIntentId;
        Payload = payload;
    }

    public DeterministicScenarioOperationKind Kind { get; }
    public ServerTick Tick { get; }
    public EventId? EventId { get; }
    public EventTypeId? EventType { get; }
    public IntentId? CausedByIntentId { get; }
    public DeterministicScenarioPayload Payload { get; }
    public bool ProducesEvent => EventId is not null;
}

internal sealed class DeterministicScenarioScript
{
    private DeterministicScenarioScript(ShiftSeed seed, ImmutableArray<DeterministicScenarioOperation> operations)
    {
        if (operations.IsDefaultOrEmpty)
        {
            throw new ArgumentException("Scenario script must contain operations.", nameof(operations));
        }

        Seed = seed;
        Operations = operations;
    }

    public ShiftSeed Seed { get; }
    public ImmutableArray<DeterministicScenarioOperation> Operations { get; }

    public static DeterministicScenarioScript Baseline { get; } = Create(ServerTick.From(9));

    public DeterministicScenarioScript WithRepairCompletionTick(ServerTick completionTick) => Create(completionTick);

    private static DeterministicScenarioScript Create(ServerTick repairCompletionTick) => new(
        new ShiftSeed(47001),
        ImmutableArray.Create(
            Accepted(DeterministicScenarioOperationKind.MovePenitentToGate, ServerTick.From(1), EventId.From("scenario_event_01"), EventTypeId.From("HOST_LOG_TRANSITION"), IntentId.From("scenario_intent_01"), new DeterministicScenarioPayload(1, "host_transition", "log_03")),
            Accepted(DeterministicScenarioOperationKind.MovePenitentToIntake, ServerTick.From(1), EventId.From("scenario_event_02"), EventTypeId.From("HOST_LOG_TRANSITION"), IntentId.From("scenario_intent_02"), new DeterministicScenarioPayload(2, "host_transition", "log_03")),
            Accepted(DeterministicScenarioOperationKind.MoveBlockerToGate, ServerTick.From(2), EventId.From("scenario_event_03"), EventTypeId.From("HOST_LOG_TRANSITION"), null, new DeterministicScenarioPayload(3, "host_transition", "log_04")),
            Accepted(DeterministicScenarioOperationKind.EnterJam, ServerTick.From(2), EventId.From("scenario_event_04"), EventTypeId.From("LINE_JAM_ENTERED"), null, new DeterministicScenarioPayload(4, "jam_entered", "log_04")),
            Accepted(DeterministicScenarioOperationKind.StartRepair, ServerTick.From(3), EventId.From("scenario_event_05"), EventTypeId.From("REPAIR_STARTED"), null, new DeterministicScenarioPayload(5, "repair_started", "log_04")),
            Accepted(DeterministicScenarioOperationKind.MovePenitentToProcedure, ServerTick.From(3), EventId.From("scenario_event_06"), EventTypeId.From("HOST_LOG_TRANSITION"), IntentId.From("scenario_intent_06"), new DeterministicScenarioPayload(6, "host_transition", "log_03")),
            Accepted(DeterministicScenarioOperationKind.StartPenitentHold, ServerTick.From(4), EventId.From("scenario_event_07"), EventTypeId.From("PROCEDURE_HOLD_STARTED"), null, new DeterministicScenarioPayload(7, "procedure_hold_started", "log_03")),
            NoEvent(DeterministicScenarioOperationKind.CompletePenitentHoldEarly, ServerTick.From(6), new DeterministicScenarioPayload(8, "procedure_hold_early", "log_03")),
            Accepted(DeterministicScenarioOperationKind.CompletePenitentHoldDue, ServerTick.From(7), EventId.From("scenario_event_08"), EventTypeId.From("PROCEDURE_HOLD_COMPLETED"), null, new DeterministicScenarioPayload(9, "procedure_hold_completed", "log_03")),
            NoEvent(DeterministicScenarioOperationKind.CompleteRepairEarly, ServerTick.From(8), new DeterministicScenarioPayload(10, "repair_early", "log_04")),
            Accepted(DeterministicScenarioOperationKind.CompleteRepairDue, repairCompletionTick, EventId.From("scenario_event_09"), EventTypeId.From("REPAIR_COMPLETED"), null, new DeterministicScenarioPayload(11, "repair_completed", "log_04"))));

    private static DeterministicScenarioOperation Accepted(
        DeterministicScenarioOperationKind kind,
        ServerTick tick,
        EventId eventId,
        EventTypeId eventType,
        IntentId? causedByIntentId,
        DeterministicScenarioPayload payload) => new(kind, tick, eventId, eventType, causedByIntentId, payload);

    private static DeterministicScenarioOperation NoEvent(
        DeterministicScenarioOperationKind kind,
        ServerTick tick,
        DeterministicScenarioPayload payload) => new(kind, tick, null, null, null, payload);
}

internal readonly record struct DeterministicScenarioPayload(int ScriptOrder, string Operation, string Target) : IDomainEventPayload;

internal readonly record struct CanonicalScenarioEvent(
    ShiftId ShiftId,
    EventId EventId,
    EventSequence Sequence,
    IntentId? CausedByIntentId,
    ServerTick ServerTick,
    StateVersion StateVersionAfter,
    EventTypeId EventType,
    DeterministicScenarioPayload Payload);

internal sealed class DeterministicScenarioTrace
{
    public DeterministicScenarioTrace(
        ShiftRuntimeState finalState,
        IEventJournal journal,
        ImmutableArray<CanonicalScenarioEvent> events,
        SnapshotBoundary initialBoundary,
        SnapshotBoundary intermediateBoundary,
        SnapshotBoundary finalBoundary,
        int acceptedOperationCount,
        int rejectedNoOpOperationCount)
    {
        ArgumentNullException.ThrowIfNull(finalState);
        ArgumentNullException.ThrowIfNull(journal);
        if (events.IsDefault)
        {
            throw new ArgumentException("Canonical events must be initialized.", nameof(events));
        }

        FinalState = finalState;
        Journal = journal;
        Events = events;
        InitialBoundary = initialBoundary;
        IntermediateBoundary = intermediateBoundary;
        FinalBoundary = finalBoundary;
        AcceptedOperationCount = acceptedOperationCount;
        RejectedNoOpOperationCount = rejectedNoOpOperationCount;
    }

    public ShiftRuntimeState FinalState { get; }
    public IEventJournal Journal { get; }
    public ImmutableArray<CanonicalScenarioEvent> Events { get; }
    public SnapshotBoundary InitialBoundary { get; }
    public SnapshotBoundary IntermediateBoundary { get; }
    public SnapshotBoundary FinalBoundary { get; }
    public int AcceptedOperationCount { get; }
    public int RejectedNoOpOperationCount { get; }
}

internal sealed class DeterministicScenarioDriver
{
    private readonly HostLogTransitionService _hostTransitions = new();
    private readonly LineJamEntryService _jamEntry = new();
    private readonly LineRepairStartService _repairStart = new();
    private readonly LineRepairDueCompletionService _repairCompletion = new();
    private readonly ProcedureActionStartService _procedureStart = new();
    private readonly ProcedureActionDueCompletionService _procedureCompletion = new();
    private readonly JournaledMutationCommitService _commits = new();
    private readonly RuntimeCheckpointFactory _checkpoints = new();

    public DeterministicScenarioTrace Run(ValidatedConfiguration configuration, DeterministicScenarioScript script)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(script);
        if (configuration.Shift.Seed != script.Seed)
        {
            throw new ArgumentException("Scenario script seed must match the supplied configuration.", nameof(script));
        }

        var state = ShiftRuntimeState.Create(configuration.Shift);
        var journal = new InMemoryEventJournal(state.ShiftId);
        var events = ImmutableArray.CreateBuilder<CanonicalScenarioEvent>();
        var initialBoundary = _checkpoints.Capture(state, journal, ServerTick.Zero);
        SnapshotBoundary? intermediateBoundary = null;
        var acceptedOperationCount = 0;
        var rejectedNoOpOperationCount = 0;

        foreach (var operation in script.Operations)
        {
            var before = state;
            switch (operation.Kind)
            {
                case DeterministicScenarioOperationKind.MovePenitentToGate:
                    Commit(operation, before, Accepted(_hostTransitions.Apply(state, LogId.From("log_03"), LogState.AT_FEED_GATE)));
                    break;
                case DeterministicScenarioOperationKind.MovePenitentToIntake:
                    Commit(operation, before, Accepted(_hostTransitions.Apply(state, LogId.From("log_03"), LogState.AT_INTAKE)));
                    break;
                case DeterministicScenarioOperationKind.MoveBlockerToGate:
                    Commit(operation, before, Accepted(_hostTransitions.Apply(state, LogId.From("log_04"), LogState.AT_FEED_GATE)));
                    break;
                case DeterministicScenarioOperationKind.EnterJam:
                    Commit(operation, before, Accepted(_jamEntry.Enter(state, JamCause.FEED_GATE_BLOCKED, operation.Tick)));
                    break;
                case DeterministicScenarioOperationKind.StartRepair:
                    Commit(operation, before, Accepted(_repairStart.Start(state, operation.Tick, configuration.Shift.Scheduler)));
                    break;
                case DeterministicScenarioOperationKind.MovePenitentToProcedure:
                    Commit(operation, before, Accepted(_hostTransitions.Apply(state, LogId.From("log_03"), LogState.AT_PROCEDURE)));
                    break;
                case DeterministicScenarioOperationKind.StartPenitentHold:
                    Commit(operation, before, Accepted(_procedureStart.Start(state, LogId.From("log_03"), ItemId.From("holy_water"), operation.Tick, configuration.Anomalies)));
                    break;
                case DeterministicScenarioOperationKind.CompletePenitentHoldEarly:
                    RequireNoOp(_procedureCompletion.CompleteDue(state, operation.Tick, configuration.Anomalies), before);
                    rejectedNoOpOperationCount++;
                    break;
                case DeterministicScenarioOperationKind.CompletePenitentHoldDue:
                    Commit(operation, before, Accepted(_procedureCompletion.CompleteDue(state, operation.Tick, configuration.Anomalies)));
                    break;
                case DeterministicScenarioOperationKind.CompleteRepairEarly:
                    RequireNoOp(_repairCompletion.CompleteDue(state, operation.Tick), before);
                    rejectedNoOpOperationCount++;
                    break;
                case DeterministicScenarioOperationKind.CompleteRepairDue:
                    Commit(operation, before, Accepted(_repairCompletion.CompleteDue(state, operation.Tick)));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }

        if (intermediateBoundary is null)
        {
            throw new InvalidOperationException("Scenario must capture an intermediate boundary.");
        }

        var finalBoundary = _checkpoints.Capture(state, journal, script.Operations[^1].Tick);
        return new DeterministicScenarioTrace(
            state,
            journal,
            events.ToImmutable(),
            initialBoundary,
            intermediateBoundary,
            finalBoundary,
            acceptedOperationCount,
            rejectedNoOpOperationCount);

        void Commit(DeterministicScenarioOperation operation, ShiftRuntimeState before, ShiftRuntimeState after)
        {
            if (!operation.ProducesEvent || operation.EventId is not { } eventId || operation.EventType is not { } eventType)
            {
                throw new InvalidOperationException("Accepted scenario operations must declare deterministic event data.");
            }

            var committed = _commits.Commit(
                journal,
                before,
                after,
                operation.Tick,
                new DomainEventDraft(eventId, eventType, operation.Payload, operation.CausedByIntentId));
            if (committed is not JournaledMutationCommitted success)
            {
                throw new InvalidOperationException("Accepted scenario mutation must commit exactly once.");
            }

            state = success.After;
            events.Add(Project(success.Envelope));
            acceptedOperationCount++;
            if (acceptedOperationCount == 2)
            {
                intermediateBoundary = _checkpoints.Capture(state, journal, operation.Tick);
            }
        }
    }

    private static ShiftRuntimeState Accepted(HostLogTransitionResult result) => result is HostLogTransitionAccepted accepted
        ? accepted.State
        : throw new InvalidOperationException("Host transition must be accepted by the fixed scenario.");

    private static ShiftRuntimeState Accepted(LineJamEntryResult result) => result is LineJamEntered entered
        ? entered.State
        : throw new InvalidOperationException("Jam entry must be accepted by the fixed scenario.");

    private static ShiftRuntimeState Accepted(LineRepairStartResult result) => result is LineRepairStarted started
        ? started.State
        : throw new InvalidOperationException("Repair start must be accepted by the fixed scenario.");

    private static ShiftRuntimeState Accepted(LineRepairDueCompletionResult result) => result is LineRepairCompleted completed
        ? completed.State
        : throw new InvalidOperationException("Repair completion must be accepted by the fixed scenario.");

    private static ShiftRuntimeState Accepted(ProcedureActionStartResult result) => result is ProcedureActionHoldStarted started
        ? started.State
        : throw new InvalidOperationException("Procedure hold start must be accepted by the fixed scenario.");

    private static ShiftRuntimeState Accepted(ProcedureActionDueCompletionResult result) => result is ProcedureActionDueCompleted completed
        ? completed.State
        : throw new InvalidOperationException("Procedure hold completion must be accepted by the fixed scenario.");

    private static void RequireNoOp(ProcedureActionDueCompletionResult result, ShiftRuntimeState expectedState)
    {
        if (result is not ProcedureActionNotDue || !ReferenceEquals(expectedState, result.State))
        {
            throw new InvalidOperationException("Early procedure completion must be an unchanged no-op.");
        }
    }

    private static void RequireNoOp(LineRepairDueCompletionResult result, ShiftRuntimeState expectedState)
    {
        if (result is not LineRepairNotDue || !ReferenceEquals(expectedState, result.State))
        {
            throw new InvalidOperationException("Early repair completion must be an unchanged no-op.");
        }
    }

    private static CanonicalScenarioEvent Project(EventEnvelope envelope)
    {
        if (envelope.Payload is not DeterministicScenarioPayload payload)
        {
            throw new InvalidOperationException("Scenario payload must retain its deterministic value fields.");
        }

        return new CanonicalScenarioEvent(
            envelope.ShiftId,
            envelope.EventId,
            envelope.Sequence,
            envelope.CausedByIntentId,
            envelope.ServerTick,
            envelope.StateVersionAfter,
            envelope.EventType,
            payload);
    }
}
