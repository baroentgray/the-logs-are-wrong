using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
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

[Trait("Scope", "TLAW-039")]
public sealed class HostTickConfirmationTestIntentTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static readonly ProfileId LearningId = ProfileId.From("learning");

    [Fact]
    public void Penitent_start_is_composed_and_published_then_existing_stage_one_completes_it_without_readding_the_intent()
    {
        var state = AtIntake("log_03");
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var beforeStart = AdvanceBefore(state, quota, ServerTick.From(10));
        var start = StartIntent(state, "penitent_start", "log_03", state.StateVersion);
        var initial = CreateInputs(
            state, quota, beforeStart.Progression, beforeStart.Lifecycle, ServerTick.From(10),
            Batch(state.ShiftId, ServerTick.From(10), start), Tools("sound_meter"), JournalAtState(state, ServerTick.From(9)), EventIds("start", 1));

        var started = Assert.IsType<HostStageSevenPublished>(Execute(initial));

        var stageStart = Assert.IsType<ConfirmationTestIntentStarted>(Assert.IsType<ConfirmationTestIntentStageOutcome>(started.StageTwo.Steps[0].Outcome).Result);
        Assert.Same(started.StageTwo.FinalState, started.StageThree.InitialState);
        Assert.Same(started.StageFive.FinalState, started.StageSix.InitialShiftState);
        Assert.Same(stageStart.State, started.StageSix.InitialShiftState);
        Assert.Equal(state.StateVersion.Next(), stageStart.State.StateVersion);
        Assert.Contains(start.IntentId, stageStart.State.ProcessedIntentIds);
        Assert.Equal(2, stageStart.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.True(stageStart.State.TryGetLog(LogId.From("log_03"), out var unchanged));
        Assert.Same(state.Logs.Single(log => log.LogId == unchanged.LogId), unchanged);
        Assert.Empty(unchanged.Flags);
        Assert.Null(stageStart.State.ActiveProcedureHold);
        Assert.False(stageStart.State.TryGetConfirmationResult(LogId.From("log_03"), out _));
        Assert.Equal(ServerTick.From(14), stageStart.State.ActiveConfirmationTest!.DueAt);

        var publication = Assert.Single(started.Publications);
        Assert.Equal(HostStageSevenEventTypes.ConfirmationTestStarted, publication.Envelope.EventType);
        Assert.Equal(start.IntentId, publication.Envelope.CausedByIntentId);
        var payload = Assert.IsType<HostStageSevenConfirmationTestStartedPayload>(publication.Envelope.Payload);
        Assert.Equal(LogId.From("log_03"), payload.LogId);
        Assert.Equal(AnomalyId.From("PENITENT_TRUNK"), payload.AnomalyId);
        Assert.True(payload.RequiredTools.SetEquals(Tools("sound_meter")));
        Assert.Equal(SimulationDuration.FromTicks(4), payload.Duration);
        Assert.True(payload.Continuous);
        Assert.Equal(LineNoise.QUIET, payload.RequiredLineNoise);
        Assert.True(payload.ResetWhenConditionLost);
        Assert.Equal("spoken_names_detected", payload.Result);
        Assert.Equal(ServerTick.From(10), payload.SegmentStartedAt);
        Assert.Equal(ServerTick.From(14), payload.DueAt);
        Assert.Equal(state.StateVersion, payload.PriorStateVersion);
        Assert.Equal(state.StateVersion.Next(), payload.CurrentStateVersion);
        Assert.DoesNotContain(started.Publications, item => item.Envelope.EventType == HostStageSevenEventTypes.ConfirmationTestCompleted);

        var replayed = Assert.IsType<HostStageSevenAlreadyPublished>(Execute(initial));
        Assert.Equal(Assert.Single(started.AssignedEventIds), Assert.Single(replayed.AssignedEventIds));

        var beforeDue = AdvanceBefore(
            started.FinalShiftState,
            started.FinalQuotaState,
            ServerTick.From(14),
            Assert.IsType<HostTickCheckpointAdvanced>(started.Checkpoint).Progression,
            Assert.IsType<HostTickCheckpointAdvanced>(started.Checkpoint).Receipt.Lifecycle);
        var dueInputs = CreateInputs(
            started.FinalShiftState, started.FinalQuotaState, beforeDue.Progression, beforeDue.Lifecycle, ServerTick.From(14),
            EmptyBatch(started.FinalShiftState.ShiftId, ServerTick.From(14)), Tools("sound_meter"), initial.Journal, EventIds("due", 1),
            started.StageSix.FinalMovementNoise, started.FinalLineNoise);

        var completed = Assert.IsType<HostStageSevenPublished>(Execute(dueInputs));

        Assert.IsType<ConfirmationTestDueCompleted>(completed.StageOne.Confirmation.Result);
        Assert.Null(completed.FinalShiftState.ActiveConfirmationTest);
        Assert.Contains(start.IntentId, completed.FinalShiftState.ProcessedIntentIds);
        var completion = Assert.Single(completed.Publications);
        Assert.Equal(HostStageSevenEventTypes.ConfirmationTestCompleted, completion.Envelope.EventType);
        Assert.Null(completion.Envelope.CausedByIntentId);
    }

    [Fact]
    public void Retained_quiet_stage_two_start_is_invalidated_by_real_same_tick_stage_six_loud_noise_in_frozen_publication_order()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveToIntake(state, "log_03");
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var before = AdvanceBefore(state, quota, ServerTick.From(10));
        var intent = StartIntent(state, "same_tick", "log_03", state.StateVersion);
        var inputs = CreateInputs(
            state, quota, before.Progression, before.Lifecycle, ServerTick.From(10),
            Batch(state.ShiftId, ServerTick.From(10), intent), Tools("sound_meter"), JournalAtState(state, ServerTick.From(9)), EventIds("loud", 4));

        var published = Assert.IsType<HostStageSevenPublished>(Execute(inputs));

        var started = Assert.IsType<ConfirmationTestIntentStarted>(Assert.IsType<ConfirmationTestIntentStageOutcome>(published.StageTwo.Steps[0].Outcome).Result);
        Assert.True(started.State.ActiveConfirmationTest!.IsRunning);
        Assert.Equal(LineNoise.QUIET, inputs.InitialLineNoise.Current);
        Assert.Equal(LineNoise.LOUD, published.StageSix.LineNoiseEvaluation.State.Current);
        var finalActive = Assert.IsType<ActiveConfirmationTest>(published.FinalShiftState.ActiveConfirmationTest);
        Assert.False(finalActive.IsRunning);
        Assert.Null(finalActive.SegmentStartedAt);
        Assert.Null(finalActive.DueAt);
        Assert.Equal(SimulationDuration.Zero, finalActive.AccumulatedValidDuration);

        var startIndex = IndexOf(published.Publications, HostStageSevenEventTypes.ConfirmationTestStarted);
        var conditionIndex = IndexOf(published.Publications, HostStageSevenEventTypes.ConfirmationConditionUpdated);
        Assert.True(startIndex >= 0 && conditionIndex > startIndex);
        Assert.Equal(intent.IntentId, published.Publications[startIndex].Envelope.CausedByIntentId);
        Assert.Null(published.Publications[conditionIndex].Envelope.CausedByIntentId);
        Assert.DoesNotContain(published.Publications, publication => publication.Envelope.EventType == HostStageSevenEventTypes.ConfirmationTestCompleted);
    }

    [Fact]
    public void Confirmation_rejection_publishes_only_exact_rejection_evidence_and_unsupported_action_appends_nothing()
    {
        var state = AtIntake("log_01");
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var before = AdvanceBefore(state, quota, ServerTick.From(10));
        var rejectedIntent = StartIntent(state, "no_plan", "log_01", state.StateVersion);
        var rejectedInputs = CreateInputs(
            state, quota, before.Progression, before.Lifecycle, ServerTick.From(10),
            Batch(state.ShiftId, ServerTick.From(10), rejectedIntent), ImmutableHashSet<ItemId>.Empty, JournalAtState(state, ServerTick.From(9)), ImmutableArray<EventId>.Empty);

        var rejected = Assert.IsType<HostStageSevenNoNewPublication>(Execute(rejectedInputs));

        var rejection = Assert.Single(rejected.Rejections);
        Assert.Equal(rejectedIntent.IntentId, rejection.IntentId);
        Assert.Equal(RejectionReason.CONFIRMATION_NO_PLAN, rejection.Reason);
        Assert.Equal(rejectedInputs.Journal.Count, rejected.BeforeCursor.Count);
        Assert.Equal(rejected.BeforeCursor.Count, rejected.AfterCursor.Count);

        var unsupportedIntent = new IntentEnvelope(
            state.ShiftId, IntentId.From("unsupported"), ActorId.From("hint"), TargetId.From("log_01"),
            IntentActionId.From("unowned_confirmation_action"), state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var unsupportedInputs = CreateInputs(
            state, quota, before.Progression, before.Lifecycle, ServerTick.From(10),
            Batch(state.ShiftId, ServerTick.From(10), unsupportedIntent), ImmutableHashSet<ItemId>.Empty, JournalAtState(state, ServerTick.From(9)), ImmutableArray<EventId>.Empty);

        var unsupported = Assert.IsType<HostStageSevenNoNewPublication>(Execute(unsupportedInputs));

        Assert.Empty(unsupported.Rejections);
        Assert.Equal(unsupportedInputs.Journal.Count, unsupported.BeforeCursor.Count);
        Assert.Equal(unsupported.BeforeCursor.Count, unsupported.AfterCursor.Count);
    }

    private static int IndexOf(ImmutableArray<HostStageSevenPublication> publications, EventTypeId type)
    {
        for (var index = 0; index < publications.Length; index++)
        {
            if (publications[index].Envelope.EventType == type)
            {
                return index;
            }
        }

        return -1;
    }

    private static HostStageSevenEventExecution Execute(ComposerInputs inputs) =>
        new HostTickExecutionService().Execute(
            inputs.InitialShiftState, inputs.InitialQuotaState, inputs.InitialMovementNoise, inputs.InitialLineNoise,
            inputs.Progression, inputs.Lifecycle, inputs.AcceptedIntents, inputs.ActiveTools, inputs.Journal,
            inputs.Tick, Fx.Shift.Scheduler, Fx.Shift, Fx.Shift.Containment, Fx.Anomalies);

    private static ComposerInputs CreateInputs(
        ShiftRuntimeState state,
        QuotaRuntimeState quota,
        HostTickProgressionEvidence progression,
        ShiftLifecycleRuntimeState lifecycle,
        ServerTick tick,
        AcceptedIntentTickBatch batch,
        ImmutableHashSet<ItemId> activeTools,
        InMemoryEventJournal journal,
        ImmutableArray<EventId> eventIds,
        MovementNoiseRuntimeState? movement = null,
        LineNoiseRuntimeState? line = null) =>
        new(state, quota, movement ?? MovementNoiseRuntimeState.Create(state.ShiftId), line ?? LineNoiseRuntimeState.Create(state.ShiftId), progression, lifecycle, batch, activeTools, journal, eventIds, tick);

    private static AcceptedIntentTickBatch Batch(ShiftId shiftId, ServerTick tick, params IntentEnvelope[] intents) =>
        AcceptedIntentTickBatchFactory.Create(
            shiftId,
            tick,
            intents.Select((intent, index) => new AuthoritativeAcceptedIntent(intent, RuntimeFixture.BoundActor, tick, ServerReceiveSequence.From(index))).ToImmutableArray());

    private static AcceptedIntentTickBatch EmptyBatch(ShiftId shiftId, ServerTick tick) =>
        AcceptedIntentTickBatchFactory.Create(shiftId, tick, ImmutableArray<AuthoritativeAcceptedIntent>.Empty);

    private static IntentEnvelope StartIntent(ShiftRuntimeState state, string intentId, string logId, StateVersion expected) => new(
        state.ShiftId, IntentId.From(intentId), ActorId.From("untrusted_hint"), TargetId.From(logId),
        ConfirmationIntentActions.StartConfirmationTest, expected, ServerTick.Zero, NoIntentParameters.Instance);

    private static ShiftRuntimeState AtIntake(string logId) => RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), logId);

    private static ImmutableHashSet<ItemId> Tools(params string[] items) => items.Select(ItemId.From).ToImmutableHashSet();

    private static (HostTickProgressionEvidence Progression, ShiftLifecycleRuntimeState Lifecycle) AdvanceBefore(
        ShiftRuntimeState state,
        QuotaRuntimeState quota,
        ServerTick targetTick,
        HostTickProgressionEvidence? progression = null,
        ShiftLifecycleRuntimeState? lifecycle = null)
    {
        var currentProgression = progression ?? HostTickProgressionEvidence.Create(state.ShiftId);
        var currentLifecycle = lifecycle ?? ShiftLifecycleRuntimeState.Create(Fx.Shift, LearningId);
        var next = currentProgression.LastCompletedTick is { } last ? last + SimulationDuration.FromTicks(1) : ServerTick.Zero;
        var service = new HostTickCompletionCheckpointService();
        while (next < targetTick)
        {
            var advanced = Assert.IsType<HostTickCheckpointAdvanced>(service.Complete(
                currentProgression, currentLifecycle, state, quota, next, Fx.Shift));
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

    private sealed record ComposerInputs(
        ShiftRuntimeState InitialShiftState,
        QuotaRuntimeState InitialQuotaState,
        MovementNoiseRuntimeState InitialMovementNoise,
        LineNoiseRuntimeState InitialLineNoise,
        HostTickProgressionEvidence Progression,
        ShiftLifecycleRuntimeState Lifecycle,
        AcceptedIntentTickBatch AcceptedIntents,
        ImmutableHashSet<ItemId> ActiveTools,
        InMemoryEventJournal Journal,
        ImmutableArray<EventId> EventIds,
        ServerTick Tick);

    private sealed class HistoryPayload : IDomainEventPayload
    {
        public static readonly HistoryPayload Instance = new();
    }
}
