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

[Trait("Scope", "TLAW-038")]
public sealed class HostTickProcedureActionIntentTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static readonly ProfileId LearningId = ProfileId.From("learning");

    [Fact]
    public void Penitent_hold_start_is_composed_then_due_completion_uses_existing_stage_one_semantics_without_reprocessing()
    {
        var state = AtProcedure("log_03");
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var beforeStart = AdvanceBefore(state, quota, ServerTick.From(10));
        var start = StartIntent(state, "penitent_start", "log_03", "holy_water", state.StateVersion);
        var initial = CreateInputs(
            state,
            quota,
            beforeStart.Progression,
            beforeStart.Lifecycle,
            ServerTick.From(10),
            Batch(state.ShiftId, ServerTick.From(10), start),
            JournalAtState(state, ServerTick.From(9)),
            EventIds("hold", 1));

        var started = Assert.IsType<HostStageSevenPublished>(Execute(initial));

        var stageStart = Assert.IsType<ProcedureActionIntentHoldStarted>(Assert.IsType<ProcedureActionIntentStageOutcome>(started.StageTwo.Steps[0].Outcome).Result);
        Assert.Same(started.StageTwo.FinalState, started.StageThree.InitialState);
        Assert.Same(started.StageFive.FinalState, started.StageSix.InitialShiftState);
        Assert.Same(stageStart.State, started.StageSix.InitialShiftState);
        Assert.Equal(state.StateVersion.Next(), stageStart.State.StateVersion);
        Assert.Contains(start.IntentId, stageStart.State.ProcessedIntentIds);
        Assert.Equal(2, stageStart.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.False(stageStart.State.TryGetProcedureProgress(LogId.From("log_03"), out _));
        Assert.Equal(ServerTick.From(13), stageStart.Result.Hold.DueAt);

        var startPublication = Assert.Single(started.Publications, publication => publication.Envelope.EventType == HostStageSevenEventTypes.ProcedureActionStarted);
        Assert.Equal([HostStageSevenEventTypes.ProcedureActionStarted], started.Publications.Select(publication => publication.Envelope.EventType));
        Assert.Equal(start.IntentId, startPublication.Envelope.CausedByIntentId);
        var startPayload = Assert.IsType<HostStageSevenProcedureActionStartedPayload>(startPublication.Envelope.Payload);
        Assert.Equal(LogId.From("log_03"), startPayload.LogId);
        Assert.Equal(AnomalyId.From("PENITENT_TRUNK"), startPayload.AnomalyId);
        Assert.Equal(ItemId.From("holy_water"), startPayload.AttemptedItem);
        Assert.Equal(0, startPayload.ProcedureStepIndex);
        Assert.Equal(ServerTick.From(10), startPayload.StartedAt);
        Assert.Equal(ServerTick.From(13), startPayload.DueAt);
        Assert.Equal(state.StateVersion, startPayload.PriorStateVersion);
        Assert.Equal(state.StateVersion.Next(), startPayload.CurrentStateVersion);
        Assert.DoesNotContain(started.Publications, publication => publication.Envelope.EventType == HostStageSevenEventTypes.ProcedureActionCompleted);

        var beforeDue = AdvanceBefore(
            started.FinalShiftState,
            started.FinalQuotaState,
            ServerTick.From(13),
            Assert.IsType<HostTickCheckpointAdvanced>(started.Checkpoint).Progression,
            Assert.IsType<HostTickCheckpointAdvanced>(started.Checkpoint).Receipt.Lifecycle);
        var dueInputs = CreateInputs(
            started.FinalShiftState,
            started.FinalQuotaState,
            beforeDue.Progression,
            beforeDue.Lifecycle,
            ServerTick.From(13),
            EmptyBatch(started.FinalShiftState.ShiftId, ServerTick.From(13)),
            initial.Journal,
            EventIds("due", 1),
            started.StageSix.FinalMovementNoise,
            started.FinalLineNoise);

        var completed = Assert.IsType<HostStageSevenPublished>(Execute(dueInputs));

        var due = Assert.IsType<ProcedureActionDueCompleted>(completed.StageOne.Procedure.Result);
        Assert.Null(completed.FinalShiftState.ActiveProcedureHold);
        Assert.Contains(start.IntentId, completed.FinalShiftState.ProcessedIntentIds);
        Assert.Equal(1, completed.FinalShiftState.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        var completionPublication = Assert.Single(completed.Publications);
        Assert.Equal(HostStageSevenEventTypes.ProcedureActionCompleted, completionPublication.Envelope.EventType);
        Assert.Null(completionPublication.Envelope.CausedByIntentId);
        Assert.Equal(due.Descriptor, Assert.IsType<HostStageSevenProcedurePayload>(completionPublication.Envelope.Payload).Descriptor);
    }

    [Fact]
    public void Resin_salt_immediate_completion_reuses_the_existing_descriptor_with_exact_intent_causation()
    {
        var state = AtProcedure("log_06");
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var before = AdvanceBefore(state, quota, ServerTick.From(10));
        var intent = StartIntent(state, "resin_salt", "log_06", "salt", state.StateVersion);
        var inputs = CreateInputs(
            state, quota, before.Progression, before.Lifecycle, ServerTick.From(10),
            Batch(state.ShiftId, ServerTick.From(10), intent), JournalAtState(state, ServerTick.From(9)), EventIds("salt", 1));

        var published = Assert.IsType<HostStageSevenPublished>(Execute(inputs));

        var completed = Assert.IsType<ProcedureActionIntentCompletedImmediately>(Assert.IsType<ProcedureActionIntentStageOutcome>(Assert.Single(published.StageTwo.Steps).Outcome).Result);
        Assert.Equal(state.StateVersion.Next(), completed.State.StateVersion);
        Assert.Contains(intent.IntentId, completed.State.ProcessedIntentIds);
        Assert.Equal(1, completed.State.Inventory.GetConsumableQuantity(ItemId.From("salt")));
        var publication = Assert.Single(published.Publications, item => item.Envelope.EventType == HostStageSevenEventTypes.ProcedureActionCompleted);
        Assert.Equal(intent.IntentId, publication.Envelope.CausedByIntentId);
        Assert.Equal(completed.Result.Descriptor, Assert.IsType<HostStageSevenProcedurePayload>(publication.Envelope.Payload).Descriptor);
        Assert.DoesNotContain(published.Publications, item => item.Envelope.EventType == HostStageSevenEventTypes.ProcedureActionStarted);
    }

    [Fact]
    public void Resin_holy_water_wrong_action_is_published_as_data_only_existing_completion_evidence()
    {
        var state = AtProcedure("log_06");
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var before = AdvanceBefore(state, quota, ServerTick.From(10));
        var intent = StartIntent(state, "resin_wrong", "log_06", "holy_water", state.StateVersion);
        var inputs = CreateInputs(
            state, quota, before.Progression, before.Lifecycle, ServerTick.From(10),
            Batch(state.ShiftId, ServerTick.From(10), intent), JournalAtState(state, ServerTick.From(9)), EventIds("wrong", 1));

        var published = Assert.IsType<HostStageSevenPublished>(Execute(inputs));

        var completed = Assert.IsType<ProcedureActionIntentCompletedImmediately>(Assert.IsType<ProcedureActionIntentStageOutcome>(Assert.Single(published.StageTwo.Steps).Outcome).Result);
        var descriptor = completed.Result.Descriptor;
        Assert.Equal(ItemActionCompletionKind.ConfiguredWrongAction, descriptor.Kind);
        Assert.True(descriptor.ItemConsumed);
        Assert.Equal(1, completed.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.Null(descriptor.CurrentProgress);
        Assert.Empty(descriptor.NewlyGrantedFlags);
        Assert.Equal(EffectEventId.From("RESIN_BUTTON_LOCK"), Assert.Single(descriptor.Effects).Event);
        var publication = Assert.Single(published.Publications, item => item.Envelope.EventType == HostStageSevenEventTypes.ProcedureActionCompleted);
        Assert.Equal(intent.IntentId, publication.Envelope.CausedByIntentId);
        Assert.Equal(descriptor, Assert.IsType<HostStageSevenProcedurePayload>(publication.Envelope.Payload).Descriptor);
    }

    [Fact]
    public void Stage_seven_handles_procedure_rejection_without_a_procedure_publication()
    {
        var state = AtProcedure("log_01");
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var before = AdvanceBefore(state, quota, ServerTick.From(10));
        var intent = StartIntent(state, "no_plan", "log_01", "holy_water", state.StateVersion);
        var inputs = CreateInputs(
            state, quota, before.Progression, before.Lifecycle, ServerTick.From(10),
            Batch(state.ShiftId, ServerTick.From(10), intent), JournalAtState(state, ServerTick.From(9)), ImmutableArray<EventId>.Empty);

        var noNew = Assert.IsType<HostStageSevenNoNewPublication>(Execute(inputs));

        var rejection = Assert.Single(noNew.Rejections);
        Assert.Equal(intent.IntentId, rejection.IntentId);
        Assert.Equal(RejectionReason.PROCEDURE_NO_PLAN, rejection.Reason);
        Assert.Same(state, noNew.StageTwo.FinalState);
        Assert.Equal(inputs.Journal.Count, noNew.BeforeCursor.Count);
        Assert.Equal(noNew.BeforeCursor.Count, noNew.AfterCursor.Count);
    }

    [Fact]
    public void Route_then_procedure_start_then_stage_five_feed_publication_preserves_frozen_event_order()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_03");
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var before = AdvanceBefore(state, quota, ServerTick.From(10));
        var route = new IntentEnvelope(
            state.ShiftId, IntentId.From("route"), ActorId.From("untrusted_hint"), TargetId.From("log_03"),
            LogIntentActions.RouteToProcedure, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var procedure = StartIntent(state, "after_route", "log_03", "holy_water", state.StateVersion.Next());
        var inputs = CreateInputs(
            state, quota, before.Progression, before.Lifecycle, ServerTick.From(10),
            Batch(state.ShiftId, ServerTick.From(10), route, procedure), JournalAtState(state, ServerTick.From(9)), EventIds("ordered", 4));

        var published = Assert.IsType<HostStageSevenPublished>(Execute(inputs));

        Assert.Equal(
            [HostStageSevenEventTypes.LogRouted, HostStageSevenEventTypes.ProcedureActionStarted, HostStageSevenEventTypes.FeedScheduled, HostStageSevenEventTypes.LineNoiseChanged],
            published.Publications.Select(publication => publication.Envelope.EventType));
        Assert.Equal(IntentId.From("route"), published.Publications[0].Envelope.CausedByIntentId);
        Assert.Equal(procedure.IntentId, published.Publications[1].Envelope.CausedByIntentId);
        Assert.Null(published.Publications[2].Envelope.CausedByIntentId);
        Assert.Null(published.Publications[3].Envelope.CausedByIntentId);
        Assert.IsType<HostStageSevenProcedureActionStartedPayload>(published.Publications[1].Envelope.Payload);
        Assert.IsType<HostStageSevenFeedSchedulePayload>(published.Publications[2].Envelope.Payload);
        Assert.IsType<HostStageSevenLineNoisePayload>(published.Publications[3].Envelope.Payload);
    }

    [Fact]
    public void Already_published_replay_of_a_procedure_start_preserves_the_existing_stage_seven_result_and_journal()
    {
        var state = AtProcedure("log_03");
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var before = AdvanceBefore(state, quota, ServerTick.From(10));
        var intent = StartIntent(state, "replay", "log_03", "holy_water", state.StateVersion);
        var inputs = CreateInputs(
            state, quota, before.Progression, before.Lifecycle, ServerTick.From(10),
            Batch(state.ShiftId, ServerTick.From(10), intent), JournalAtState(state, ServerTick.From(9)), EventIds("replay", 1));

        var published = Assert.IsType<HostStageSevenPublished>(Execute(inputs));
        var beforeReplay = inputs.Journal.Events.ToArray();

        var replayed = Assert.IsType<HostStageSevenAlreadyPublished>(Execute(inputs));

        Assert.Equal(beforeReplay, inputs.Journal.Events);
        Assert.Equal(Assert.Single(published.AssignedEventIds), Assert.Single(replayed.AssignedEventIds));
    }

    private static HostStageSevenEventExecution Execute(ComposerInputs inputs) =>
        new HostTickExecutionService().Execute(
            inputs.InitialShiftState, inputs.InitialQuotaState, inputs.InitialMovementNoise, inputs.InitialLineNoise,
            inputs.Progression, inputs.Lifecycle, inputs.AcceptedIntents, ImmutableHashSet<ItemId>.Empty, inputs.Journal,
            inputs.Tick, Fx.Shift.Scheduler, Fx.Shift, Fx.Shift.Containment, Fx.Anomalies);

    private static ComposerInputs CreateInputs(
        ShiftRuntimeState state,
        QuotaRuntimeState quota,
        HostTickProgressionEvidence progression,
        ShiftLifecycleRuntimeState lifecycle,
        ServerTick tick,
        AcceptedIntentTickBatch batch,
        InMemoryEventJournal journal,
        ImmutableArray<EventId> eventIds,
        MovementNoiseRuntimeState? movement = null,
        LineNoiseRuntimeState? line = null) =>
        new(state, quota, movement ?? MovementNoiseRuntimeState.Create(state.ShiftId), line ?? LineNoiseRuntimeState.Create(state.ShiftId), progression, lifecycle, batch, journal, eventIds, tick);

    private static AcceptedIntentTickBatch Batch(ShiftId shiftId, ServerTick tick, params IntentEnvelope[] intents) =>
        AcceptedIntentTickBatchFactory.Create(
            shiftId,
            tick,
            intents.Select((intent, index) => new AuthoritativeAcceptedIntent(intent, RuntimeFixture.BoundActor, tick, ServerReceiveSequence.From(index))).ToImmutableArray());

    private static AcceptedIntentTickBatch EmptyBatch(ShiftId shiftId, ServerTick tick) =>
        AcceptedIntentTickBatchFactory.Create(shiftId, tick, ImmutableArray<AuthoritativeAcceptedIntent>.Empty);

    private static IntentEnvelope StartIntent(ShiftRuntimeState state, string intentId, string logId, string itemId, StateVersion expected) => new(
        state.ShiftId, IntentId.From(intentId), ActorId.From("untrusted_hint"), TargetId.From(logId),
        ProcedureIntentActions.StartProcedureAction, expected, ServerTick.Zero,
        new ProcedureActionIntentParameters(ItemId.From(itemId)));

    private static ShiftRuntimeState AtProcedure(string logId)
    {
        var initial = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), logId);
        return RuntimeFixture.MoveHost(initial, logId, LogState.AT_PROCEDURE);
    }

    private static (HostTickProgressionEvidence Progression, ShiftLifecycleRuntimeState Lifecycle) AdvanceBefore(
        ShiftRuntimeState state,
        QuotaRuntimeState quota,
        ServerTick targetTick,
        HostTickProgressionEvidence? progression = null,
        ShiftLifecycleRuntimeState? lifecycle = null)
    {
        var currentProgression = progression ?? HostTickProgressionEvidence.Create(state.ShiftId);
        var currentLifecycle = lifecycle ?? ShiftLifecycleRuntimeState.Create(Fx.Shift, LearningId);
        var next = currentProgression.LastCompletedTick is { } last
            ? last + SimulationDuration.FromTicks(1)
            : ServerTick.Zero;
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
        InMemoryEventJournal Journal,
        ImmutableArray<EventId> EventIds,
        ServerTick Tick);

    private sealed class HistoryPayload : IDomainEventPayload
    {
        public static readonly HistoryPayload Instance = new();
    }
}
