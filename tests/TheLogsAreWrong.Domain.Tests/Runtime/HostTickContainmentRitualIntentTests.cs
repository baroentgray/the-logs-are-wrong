using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Sequencing;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-041")]
public sealed class HostTickContainmentRitualIntentTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static readonly ProfileId LearningId = ProfileId.From("learning");

    [Fact]
    public void Service_requested_start_at_tick_ten_publishes_exact_semantic_evidence_and_replays()
    {
        var scenario = ServiceRequestedAtTickTen();
        var quota = QuotaRuntimeState.Create(scenario.ShiftConfiguration);
        var before = AdvanceBefore(scenario.State, quota, ServerTick.From(10), scenario.ShiftConfiguration);
        var intent = StartIntent(scenario.State, "start", scenario.State.StateVersion);
        var inputs = CreateInputs(
            scenario.State, quota, before.Progression, before.Lifecycle, ServerTick.From(10), Batch(scenario.State.ShiftId, ServerTick.From(10), intent),
            JournalAtState(scenario.State, ServerTick.From(9)), EventIds("start", 1), scenario.ShiftConfiguration);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(inputs));

        var started = Assert.IsType<ContainmentRitualIntentStarted>(Assert.IsType<ContainmentRitualIntentStageOutcome>(published.StageTwo.Steps[0].Outcome).Result);
        Assert.Same(started.State, published.StageThree.InitialState);
        Assert.Same(started.State, published.StageFour.InitialShiftState);
        Assert.Same(published.StageFive.FinalState, published.StageSix.InitialShiftState);
        Assert.Equal(ContainmentState.SERVICE_REQUESTED, published.FinalShiftState.Containment.State);
        Assert.Same(scenario.State.Containment, started.State.Containment);
        Assert.Equal(ServerTick.From(10), started.Result.Ritual.StartedAt);
        Assert.Equal(ServerTick.From(14), started.Result.Ritual.DueAt);
        Assert.Equal(SimulationDuration.FromTicks(4), started.Result.Ritual.Duration);
        Assert.Equal(intent.IntentId, Assert.Single(published.FinalShiftState.ProcessedIntentIds));

        var ritualPublication = Assert.Single(published.Publications, item => item.Envelope.EventType == HostStageSevenEventTypes.ContainmentRitualStarted);
        Assert.Equal(intent.IntentId, ritualPublication.Envelope.CausedByIntentId);
        var payload = Assert.IsType<HostStageSevenContainmentRitualStartedPayload>(ritualPublication.Envelope.Payload);
        Assert.Equal(ContainmentState.SERVICE_REQUESTED, payload.ContainmentState);
        Assert.Equal(scenario.State.Containment.EnteredAt, payload.ContainmentEnteredAt);
        Assert.Equal(scenario.State.Containment.DeadlineAt, payload.ContainmentDeadlineAt);
        Assert.Equal(ServerTick.From(10), payload.RitualStartedAt);
        Assert.Equal(ServerTick.From(14), payload.RitualDueAt);
        Assert.Equal(SimulationDuration.FromTicks(4), payload.RitualDuration);
        Assert.Equal(scenario.State.StateVersion, payload.PriorStateVersion);
        Assert.Equal(scenario.State.StateVersion.Next(), payload.CurrentStateVersion);
        Assert.DoesNotContain(published.Publications, item => item.Envelope.EventType == HostStageSevenEventTypes.ContainmentRitualCompleted);

        var replayed = Assert.IsType<HostStageSevenAlreadyPublished>(Execute(inputs));
        Assert.Equal(published.AssignedEventIds, replayed.AssignedEventIds);
    }

    [Fact]
    public void Same_tick_service_and_overdue_escalations_retain_the_exact_ritual_and_publish_stage_two_before_stage_three()
    {
        AssertSameTickEscalation(ServiceRequestedDueAtTickTen(), ContainmentState.OVERDUE, "service_escalation");
        AssertSameTickEscalation(OverdueDueAtTickTen(), ContainmentState.INCIDENT, "overdue_escalation");
    }

    [Fact]
    public void Due_completion_is_stage_one_system_owned_and_resolves_an_escalated_incident_without_a_second_processed_id()
    {
        var scenario = OverdueDueAtTickTen();
        var quota = QuotaRuntimeState.Create(scenario.ShiftConfiguration);
        var beforeStart = AdvanceBefore(scenario.State, quota, ServerTick.From(10), scenario.ShiftConfiguration);
        var startIntent = StartIntent(scenario.State, "start", scenario.State.StateVersion);
        var startInputs = CreateInputs(
            scenario.State, quota, beforeStart.Progression, beforeStart.Lifecycle, ServerTick.From(10), Batch(scenario.State.ShiftId, ServerTick.From(10), startIntent),
            JournalAtState(scenario.State, ServerTick.From(9)), EventIds("start", 2), scenario.ShiftConfiguration);
        var started = Assert.IsType<HostStageSevenPublished>(Execute(startInputs));
        Assert.Equal(ContainmentState.INCIDENT, started.FinalShiftState.Containment.State);
        Assert.NotNull(started.FinalShiftState.ActiveContainmentRitual);

        var beforeDue = AdvanceBefore(
            started.FinalShiftState,
            started.FinalQuotaState,
            ServerTick.From(14),
            scenario.ShiftConfiguration,
            Assert.IsType<HostTickCheckpointAdvanced>(started.Checkpoint).Progression,
            Assert.IsType<HostTickCheckpointAdvanced>(started.Checkpoint).Receipt.Lifecycle);
        var due = Assert.IsType<HostStageSevenPublished>(Execute(CreateInputs(
            started.FinalShiftState,
            started.FinalQuotaState,
            beforeDue.Progression,
            beforeDue.Lifecycle,
            ServerTick.From(14),
            EmptyBatch(scenario.State.ShiftId, ServerTick.From(14)),
            startInputs.Journal,
            EventIds("due", 1),
            scenario.ShiftConfiguration,
            started.StageSix.FinalMovementNoise,
            started.FinalLineNoise)));

        Assert.IsType<ContainmentRitualCompleted>(due.StageOne.ContainmentRitual.Result);
        Assert.Equal(ContainmentState.STABLE, due.FinalShiftState.Containment.State);
        Assert.Null(due.FinalShiftState.ActiveContainmentRitual);
        Assert.Equal(startIntent.IntentId, Assert.Single(due.FinalShiftState.ProcessedIntentIds));
        var completion = Assert.Single(due.Publications, item => item.Envelope.EventType == HostStageSevenEventTypes.ContainmentRitualCompleted);
        Assert.Null(completion.Envelope.CausedByIntentId);
        Assert.Equal(ServerTick.From(14), due.FinalShiftState.Containment.EnteredAt);
        Assert.Equal(ServerTick.From(15), due.FinalShiftState.Containment.DeadlineAt);
    }

    [Fact]
    public void Duplicate_accepted_intent_on_a_later_host_tick_is_ignored_without_mutation_or_publication()
    {
        var scenario = ServiceRequestedAtTickTen();
        var quota = QuotaRuntimeState.Create(scenario.ShiftConfiguration);
        var beforeStart = AdvanceBefore(scenario.State, quota, ServerTick.From(10), scenario.ShiftConfiguration);
        var intent = StartIntent(scenario.State, "duplicate", scenario.State.StateVersion);
        var startInputs = CreateInputs(
            scenario.State, quota, beforeStart.Progression, beforeStart.Lifecycle, ServerTick.From(10), Batch(scenario.State.ShiftId, ServerTick.From(10), intent),
            JournalAtState(scenario.State, ServerTick.From(9)), EventIds("duplicate_start", 1), scenario.ShiftConfiguration);
        var started = Assert.IsType<HostStageSevenPublished>(Execute(startInputs));

        var beforeDuplicate = AdvanceBefore(
            started.FinalShiftState,
            started.FinalQuotaState,
            ServerTick.From(11),
            scenario.ShiftConfiguration,
            Assert.IsType<HostTickCheckpointAdvanced>(started.Checkpoint).Progression,
            Assert.IsType<HostTickCheckpointAdvanced>(started.Checkpoint).Receipt.Lifecycle);
        var duplicate = Assert.IsType<HostStageSevenNoNewPublication>(Execute(CreateInputs(
            started.FinalShiftState,
            started.FinalQuotaState,
            beforeDuplicate.Progression,
            beforeDuplicate.Lifecycle,
            ServerTick.From(11),
            Batch(started.FinalShiftState.ShiftId, ServerTick.From(11), StartIntent(started.FinalShiftState, "duplicate", started.FinalShiftState.StateVersion)),
            startInputs.Journal,
            ImmutableArray<EventId>.Empty,
            scenario.ShiftConfiguration,
            started.StageSix.FinalMovementNoise,
            started.FinalLineNoise)));

        Assert.IsType<ContainmentRitualIntentDuplicateIgnored>(Assert.IsType<ContainmentRitualIntentStageOutcome>(duplicate.StageTwo.Steps[0].Outcome).Result);
        Assert.Same(started.FinalShiftState, duplicate.FinalShiftState);
        Assert.Equal(intent.IntentId, Assert.Single(duplicate.FinalShiftState.ProcessedIntentIds));
        Assert.Empty(duplicate.Rejections);
        Assert.Equal(duplicate.BeforeCursor.Count, duplicate.AfterCursor.Count);
    }

    [Fact]
    public void Underlying_rejection_and_fixed_target_unsupported_result_do_not_publish_containment_ritual_start()
    {
        var state = RuntimeFixture.CreateInitialState();
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var before = AdvanceBefore(state, quota, ServerTick.From(10), Fx.Shift);
        var rejected = Assert.IsType<HostStageSevenNoNewPublication>(Execute(CreateInputs(
            state, quota, before.Progression, before.Lifecycle, ServerTick.From(10),
            Batch(state.ShiftId, ServerTick.From(10), StartIntent(state, "stable", state.StateVersion)),
            JournalAtState(state, ServerTick.From(9)), ImmutableArray<EventId>.Empty, Fx.Shift)));
        Assert.Equal(RejectionReason.NO_ACTIVE_REQUEST, Assert.Single(rejected.Rejections).Reason);
        Assert.Equal(rejected.BeforeCursor.Count, rejected.AfterCursor.Count);

        var target = new IntentEnvelope(
            state.ShiftId, IntentId.From("wrong_target"), ActorId.From("hint"), TargetId.From("OTHER"),
            ContainmentRitualIntentActions.StartContainmentRitual, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var unsupported = Assert.IsType<HostStageSevenNoNewPublication>(Execute(CreateInputs(
            state, quota, before.Progression, before.Lifecycle, ServerTick.From(10),
            Batch(state.ShiftId, ServerTick.From(10), target), JournalAtState(state, ServerTick.From(9)), ImmutableArray<EventId>.Empty, Fx.Shift)));
        Assert.Empty(unsupported.Rejections);
        Assert.Equal(unsupported.BeforeCursor.Count, unsupported.AfterCursor.Count);
    }

    private static void AssertSameTickEscalation(ContainmentScenario scenario, ContainmentState expectedState, string prefix)
    {
        var quota = QuotaRuntimeState.Create(scenario.ShiftConfiguration);
        var before = AdvanceBefore(scenario.State, quota, ServerTick.From(10), scenario.ShiftConfiguration);
        var intent = StartIntent(scenario.State, prefix, scenario.State.StateVersion);
        var published = Assert.IsType<HostStageSevenPublished>(Execute(CreateInputs(
            scenario.State, quota, before.Progression, before.Lifecycle, ServerTick.From(10), Batch(scenario.State.ShiftId, ServerTick.From(10), intent),
            JournalAtState(scenario.State, ServerTick.From(9)), EventIds(prefix, 2), scenario.ShiftConfiguration)));

        var started = Assert.IsType<ContainmentRitualIntentStarted>(Assert.IsType<ContainmentRitualIntentStageOutcome>(published.StageTwo.Steps[0].Outcome).Result);
        Assert.Equal(expectedState, published.FinalShiftState.Containment.State);
        Assert.Same(started.Result.Ritual, published.FinalShiftState.ActiveContainmentRitual);
        if (expectedState == ContainmentState.INCIDENT)
        {
            Assert.IsType<ContainmentIncidentEntered>(published.StageThree.Containment.Result);
        }

        var types = published.Publications.Select(item => item.Envelope.EventType).ToArray();
        var startIndex = Array.IndexOf(types, HostStageSevenEventTypes.ContainmentRitualStarted);
        var stateChangeIndex = Array.IndexOf(types, HostStageSevenEventTypes.ContainmentStateChanged);
        Assert.True(startIndex >= 0 && stateChangeIndex > startIndex);
        Assert.DoesNotContain(types, type => type == HostStageSevenEventTypes.ContainmentRitualCompleted);
    }

    private static HostStageSevenEventExecution Execute(ComposerInputs inputs) =>
        new HostTickExecutionService().Execute(
            inputs.InitialShiftState, inputs.InitialQuotaState, inputs.InitialMovementNoise, inputs.InitialLineNoise,
            inputs.Progression, inputs.Lifecycle, inputs.AcceptedIntents, ImmutableHashSet<ItemId>.Empty, inputs.Journal,
            inputs.Tick, inputs.ShiftConfiguration.Scheduler, inputs.ShiftConfiguration, inputs.ShiftConfiguration.Containment, Fx.Anomalies);

    private static ComposerInputs CreateInputs(
        ShiftRuntimeState state,
        QuotaRuntimeState quota,
        HostTickProgressionEvidence progression,
        ShiftLifecycleRuntimeState lifecycle,
        ServerTick tick,
        AcceptedIntentTickBatch batch,
        InMemoryEventJournal journal,
        ImmutableArray<EventId> eventIds,
        ShiftConfiguration shiftConfiguration,
        MovementNoiseRuntimeState? movement = null,
        LineNoiseRuntimeState? line = null) =>
        new(state, quota, movement ?? MovementNoiseRuntimeState.Create(state.ShiftId), line ?? LineNoiseRuntimeState.Create(state.ShiftId), progression, lifecycle, batch, journal, eventIds, tick, shiftConfiguration);

    private static AcceptedIntentTickBatch Batch(ShiftId shiftId, ServerTick tick, params IntentEnvelope[] intents) =>
        AcceptedIntentTickBatchFactory.Create(
            shiftId,
            tick,
            intents.Select((intent, index) => new AuthoritativeAcceptedIntent(intent, RuntimeFixture.BoundActor, tick, ServerReceiveSequence.From(index))).ToImmutableArray());

    private static AcceptedIntentTickBatch EmptyBatch(ShiftId shiftId, ServerTick tick) =>
        AcceptedIntentTickBatchFactory.Create(shiftId, tick, ImmutableArray<AuthoritativeAcceptedIntent>.Empty);

    private static IntentEnvelope StartIntent(ShiftRuntimeState state, string intentId, StateVersion expected) => new(
        state.ShiftId, IntentId.From(intentId), ActorId.From("untrusted_hint"), ContainmentRitualIntentTargets.Containment,
        ContainmentRitualIntentActions.StartContainmentRitual, expected, ServerTick.Zero, NoIntentParameters.Instance);

    private static (HostTickProgressionEvidence Progression, ShiftLifecycleRuntimeState Lifecycle) AdvanceBefore(
        ShiftRuntimeState state,
        QuotaRuntimeState quota,
        ServerTick targetTick,
        ShiftConfiguration shiftConfiguration,
        HostTickProgressionEvidence? progression = null,
        ShiftLifecycleRuntimeState? lifecycle = null)
    {
        var currentProgression = progression ?? HostTickProgressionEvidence.Create(state.ShiftId);
        var currentLifecycle = lifecycle ?? ShiftLifecycleRuntimeState.Create(shiftConfiguration, LearningId);
        var next = currentProgression.LastCompletedTick is { } last ? last + SimulationDuration.FromTicks(1) : ServerTick.Zero;
        var service = new HostTickCompletionCheckpointService();
        while (next < targetTick)
        {
            var advanced = Assert.IsType<HostTickCheckpointAdvanced>(service.Complete(
                currentProgression, currentLifecycle, state, quota, next, shiftConfiguration));
            currentProgression = advanced.Progression;
            currentLifecycle = advanced.Receipt.Lifecycle;
            next += SimulationDuration.FromTicks(1);
        }

        return (currentProgression, currentLifecycle);
    }

    private static InMemoryEventJournal JournalAtState(ShiftRuntimeState state, ServerTick tick)
    {
        var journal = new InMemoryEventJournal(state.ShiftId);
        for (var version = 1L; version <= state.StateVersion.Value; version++)
        {
            Assert.Equal(JournalAppendOutcome.Accepted, journal.TryAppend(new EventEnvelope
            {
                ShiftId = state.ShiftId,
                EventId = EventId.From($"history_{version}"),
                Sequence = EventSequence.From(version),
                ServerTick = tick,
                StateVersionAfter = StateVersion.From(version),
                EventType = EventTypeId.From("History"),
                Payload = HistoryPayload.Instance
            }));
        }

        return journal;
    }

    private static ImmutableArray<EventId> EventIds(string prefix, int count) =>
        ImmutableArray.CreateRange(Enumerable.Range(0, count).Select(index => EventId.From($"{prefix}_{index}")));

    private static ContainmentScenario ServiceRequestedAtTickTen()
    {
        var containment = Fx.Shift.Containment with
        {
            IntervalByDangerWeight = Fx.Shift.Containment.IntervalByDangerWeight.SetItem("1", 1)
        };
        return RequestAtTickTwo(containment);
    }

    private static ContainmentScenario ServiceRequestedDueAtTickTen()
    {
        var containment = Fx.Shift.Containment with
        {
            IntervalByDangerWeight = Fx.Shift.Containment.IntervalByDangerWeight.SetItem("1", 1),
            ServiceRequestedGraceSeconds = 8
        };
        return RequestAtTickTwo(containment);
    }

    private static ContainmentScenario OverdueDueAtTickTen()
    {
        var containment = Fx.Shift.Containment with
        {
            IntervalByDangerWeight = Fx.Shift.Containment.IntervalByDangerWeight.SetItem("1", 1),
            ServiceRequestedGraceSeconds = 1,
            OverdueSeconds = 7
        };
        var request = RequestAtTickTwo(containment);
        var overdue = Assert.IsType<ContainmentStateAdvanced>(new ContainmentAdvanceService().Advance(
            request.State, ServerTick.From(3), containment, Fx.Anomalies)).State;
        return request with { State = overdue };
    }

    private static ContainmentScenario RequestAtTickTwo(ContainmentConfiguration containment)
    {
        var shiftConfiguration = Fx.Shift with { Containment = containment };
        var writtenOff = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_03");
        writtenOff = RuntimeFixture.MoveHost(writtenOff, "log_03", LogState.HELD_WRITTEN_OFF);
        var armed = Assert.IsType<ContainmentStableIntervalArmed>(new ContainmentAdvanceService().Advance(
            writtenOff, ServerTick.From(1), containment, Fx.Anomalies)).State;
        var request = Assert.IsType<ContainmentStateAdvanced>(new ContainmentAdvanceService().Advance(
            armed, ServerTick.From(2), containment, Fx.Anomalies)).State;
        return new ContainmentScenario(request, shiftConfiguration);
    }

    private sealed record ComposerInputs(
        ShiftRuntimeState InitialShiftState,
        QuotaRuntimeState InitialQuotaState,
        MovementNoiseRuntimeState InitialMovementNoise,
        LineNoiseRuntimeState InitialLineNoise,
        HostTickProgressionEvidence Progression,
        ShiftLifecycleRuntimeState Lifecycle,
        AcceptedIntentTickBatch AcceptedIntents,
        InMemoryEventJournal Journal,
        ImmutableArray<EventId> EventIds,
        ServerTick Tick,
        ShiftConfiguration ShiftConfiguration);

    private sealed record ContainmentScenario(ShiftRuntimeState State, ShiftConfiguration ShiftConfiguration);

    private sealed class HistoryPayload : IDomainEventPayload
    {
        public static readonly HistoryPayload Instance = new();
    }
}
