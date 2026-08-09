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

[Trait("Scope", "TLAW-040")]
public sealed class HostTickLineRepairIntentTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static readonly ProfileId LearningId = ProfileId.From("learning");

    [Fact]
    public void Feed_gate_start_is_published_as_exact_repair_started_then_replays_with_full_payload_semantics()
    {
        var state = FeedGateJammed();
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var before = AdvanceBefore(state, quota, ServerTick.From(10));
        var intent = StartIntent(state, "feed_start", state.StateVersion);
        var inputs = CreateInputs(
            state, quota, before.Progression, before.Lifecycle, ServerTick.From(10), Batch(state.ShiftId, ServerTick.From(10), intent),
            JournalAtState(state, ServerTick.From(9)), EventIds("feed_start", 2));

        var published = Assert.IsType<HostStageSevenPublished>(Execute(inputs));

        var started = Assert.IsType<LineRepairIntentStarted>(Assert.IsType<LineRepairIntentStageOutcome>(published.StageTwo.Steps[0].Outcome).Result);
        Assert.Same(started.State, published.StageThree.InitialState);
        Assert.Same(started.State, published.StageFour.InitialShiftState);
        Assert.Same(started.State, published.StageFive.InitialState);
        Assert.Same(published.StageFive.FinalState, published.StageSix.InitialShiftState);
        Assert.Equal(LineState.REPAIRING, published.FinalShiftState.Line.State);
        Assert.Equal(LineNoise.LOUD, published.FinalLineNoise.Current);
        Assert.Equal(2, published.Publications.Length);
        Assert.Equal(new[] { HostStageSevenEventTypes.RepairStarted, HostStageSevenEventTypes.LineNoiseChanged }, published.Publications.Select(item => item.Envelope.EventType));
        Assert.DoesNotContain(published.Publications, item => item.Envelope.EventType == HostStageSevenEventTypes.RepairCompleted);

        var start = published.Publications[0];
        Assert.Equal(intent.IntentId, start.Envelope.CausedByIntentId);
        var payload = Assert.IsType<HostStageSevenRepairStartedPayload>(start.Envelope.Payload);
        Assert.Equal(JamCause.FEED_GATE_BLOCKED, payload.Cause);
        Assert.Equal(LogId.From("log_02"), payload.PendingLogId);
        Assert.Equal(ServerTick.From(10), payload.StartedAt);
        Assert.Equal(ServerTick.From(16), payload.DueAt);
        Assert.Equal(SimulationDuration.FromTicks(6), payload.Duration);
        Assert.Equal(state.StateVersion, payload.PriorStateVersion);
        Assert.Equal(state.StateVersion.Next(), payload.CurrentStateVersion);
        Assert.Contains(intent.IntentId, published.FinalShiftState.ProcessedIntentIds);

        var replayed = Assert.IsType<HostStageSevenAlreadyPublished>(Execute(inputs));
        Assert.Equal(new[] { EventId.From("feed_start_0"), EventId.From("feed_start_1") }, replayed.AssignedEventIds);
    }

    [Fact]
    public void Intake_auto_feed_start_preserves_its_existing_pending_identity_and_stage_six_does_not_layer_a_second_jam()
    {
        var state = IntakeAutoFeedJammed();
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var before = AdvanceBefore(state, quota, ServerTick.From(10));
        var intent = StartIntent(state, "auto_start", state.StateVersion);
        var inputs = CreateInputs(
            state, quota, before.Progression, before.Lifecycle, ServerTick.From(10), Batch(state.ShiftId, ServerTick.From(10), intent),
            JournalAtState(state, ServerTick.From(9)), EventIds("auto_start", 3));

        var published = Assert.IsType<HostStageSevenPublished>(Execute(inputs));

        var payload = Assert.IsType<HostStageSevenRepairStartedPayload>(published.Publications[0].Envelope.Payload);
        Assert.Equal(JamCause.INTAKE_AUTOFEED_BLOCKED, payload.Cause);
        Assert.Equal(LogId.From("log_02"), payload.PendingLogId);
        Assert.Equal(LineState.REPAIRING, published.FinalShiftState.Line.State);
        Assert.DoesNotContain(published.Publications, item => item.Envelope.EventType == HostStageSevenEventTypes.LineJammed);
        Assert.IsNotType<IntakeAutoFeedJamEntered>(published.StageSix.IntakeAutoFeedJam);
        Assert.IsNotType<FeedGateJamDerived>(published.StageSix.FeedGateJam);
    }

    [Fact]
    public void Due_completion_remains_stage_one_system_owned_and_retries_after_the_blocker_is_removed()
    {
        var state = FeedGateJammed();
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var beforeStart = AdvanceBefore(state, quota, ServerTick.From(10));
        var start = StartIntent(state, "repair_start", state.StateVersion);
        var startInputs = CreateInputs(
            state, quota, beforeStart.Progression, beforeStart.Lifecycle, ServerTick.From(10), Batch(state.ShiftId, ServerTick.From(10), start),
            JournalAtState(state, ServerTick.From(9)), EventIds("start", 2));
        var started = Assert.IsType<HostStageSevenPublished>(Execute(startInputs));

        var beforeDue = AdvanceBefore(
            started.FinalShiftState, started.FinalQuotaState, ServerTick.From(15),
            Assert.IsType<HostTickCheckpointAdvanced>(started.Checkpoint).Progression,
            Assert.IsType<HostTickCheckpointAdvanced>(started.Checkpoint).Receipt.Lifecycle);
        var notDue = Assert.IsType<HostStageSevenNoNewPublication>(Execute(CreateInputs(
            started.FinalShiftState, started.FinalQuotaState, beforeDue.Progression, beforeDue.Lifecycle, ServerTick.From(15),
            EmptyBatch(state.ShiftId, ServerTick.From(15)), startInputs.Journal, ImmutableArray<EventId>.Empty,
            started.StageSix.FinalMovementNoise, started.FinalLineNoise)));
        Assert.IsType<LineRepairNotDue>(notDue.StageOne.LineRepair.Result);

        var beforeBlocked = AdvanceBefore(
            started.FinalShiftState, started.FinalQuotaState, ServerTick.From(16),
            beforeDue.Progression,
            beforeDue.Lifecycle);
        var unblock = new IntentEnvelope(
            started.FinalShiftState.ShiftId, IntentId.From("unblock"), ActorId.From("hint"), TargetId.From("log_01"),
            LogIntentActions.RouteToProcedure, started.FinalShiftState.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var blocked = Assert.IsType<HostStageSevenPublished>(Execute(CreateInputs(
            started.FinalShiftState, started.FinalQuotaState, beforeBlocked.Progression, beforeBlocked.Lifecycle, ServerTick.From(16),
            Batch(state.ShiftId, ServerTick.From(16), unblock), startInputs.Journal, EventIds("unblock", 1),
            started.StageSix.FinalMovementNoise, started.FinalLineNoise)));
        Assert.IsType<LineRepairBlockingConditionRemains>(blocked.StageOne.LineRepair.Result);
        Assert.Equal(LineState.REPAIRING, blocked.FinalShiftState.Line.State);
        Assert.Contains(start.IntentId, blocked.FinalShiftState.ProcessedIntentIds);
        Assert.Contains(unblock.IntentId, blocked.FinalShiftState.ProcessedIntentIds);

        var beforeCompletion = AdvanceBefore(
            blocked.FinalShiftState, blocked.FinalQuotaState, ServerTick.From(17),
            Assert.IsType<HostTickCheckpointAdvanced>(blocked.Checkpoint).Progression,
            Assert.IsType<HostTickCheckpointAdvanced>(blocked.Checkpoint).Receipt.Lifecycle);
        var completionInputs = CreateInputs(
            blocked.FinalShiftState, blocked.FinalQuotaState, beforeCompletion.Progression, beforeCompletion.Lifecycle, ServerTick.From(17),
            EmptyBatch(state.ShiftId, ServerTick.From(17)), startInputs.Journal, EventIds("complete", 3),
            blocked.StageSix.FinalMovementNoise, blocked.FinalLineNoise);
        var completed = Assert.IsType<HostStageSevenPublished>(Execute(completionInputs));

        Assert.IsType<LineRepairCompleted>(completed.StageOne.LineRepair.Result);
        Assert.IsType<RepairPendingTransitionExecuted>(completed.StageFive.RepairExecution);
        var repairCompleted = Assert.Single(completed.Publications, item => item.Envelope.EventType == HostStageSevenEventTypes.RepairCompleted);
        Assert.Null(repairCompleted.Envelope.CausedByIntentId);
        Assert.Equal(HostStageSevenEventTypes.RepairCompleted, completed.Publications[0].Envelope.EventType);
        Assert.Equal(3, completed.Publications.Length);
        Assert.Equal(LineState.LINE_CLEAR, completed.FinalShiftState.Line.State);
        Assert.Contains(start.IntentId, completed.FinalShiftState.ProcessedIntentIds);
        Assert.DoesNotContain(completed.FinalShiftState.ProcessedIntentIds, item => item != start.IntentId && item != unblock.IntentId);
    }

    [Fact]
    public void Underlying_rejection_and_fixed_target_unsupported_result_append_no_state_changing_event()
    {
        var noJam = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var before = AdvanceBefore(noJam, quota, ServerTick.From(10));
        var rejection = Assert.IsType<HostStageSevenNoNewPublication>(Execute(CreateInputs(
            noJam, quota, before.Progression, before.Lifecycle, ServerTick.From(10),
            Batch(noJam.ShiftId, ServerTick.From(10), StartIntent(noJam, "no_jam", noJam.StateVersion)),
            JournalAtState(noJam, ServerTick.From(9)), ImmutableArray<EventId>.Empty)));
        Assert.Equal(RejectionReason.NO_ACTIVE_JAM, Assert.Single(rejection.Rejections).Reason);

        var jammed = FeedGateJammed();
        var target = new IntentEnvelope(
            jammed.ShiftId, IntentId.From("wrong_target"), ActorId.From("hint"), TargetId.From("OTHER"),
            LineRepairIntentActions.StartLineRepair, jammed.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var targetBefore = AdvanceBefore(jammed, quota, ServerTick.From(10));
        var unsupported = Assert.IsType<HostStageSevenNoNewPublication>(Execute(CreateInputs(
            jammed, quota, targetBefore.Progression, targetBefore.Lifecycle, ServerTick.From(10),
            Batch(jammed.ShiftId, ServerTick.From(10), target), JournalAtState(jammed, ServerTick.From(9)), ImmutableArray<EventId>.Empty)));
        Assert.Empty(unsupported.Rejections);
        Assert.Equal(unsupported.BeforeCursor.Count, unsupported.AfterCursor.Count);
    }

    private static HostStageSevenEventExecution Execute(ComposerInputs inputs) =>
        new HostTickExecutionService().Execute(
            inputs.InitialShiftState, inputs.InitialQuotaState, inputs.InitialMovementNoise, inputs.InitialLineNoise,
            inputs.Progression, inputs.Lifecycle, inputs.AcceptedIntents, ImmutableHashSet<ItemId>.Empty, inputs.Journal,
            inputs.EventIds, inputs.Tick, Fx.Shift.Scheduler, Fx.Shift, Fx.Shift.Containment, Fx.Anomalies);

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

    private static IntentEnvelope StartIntent(ShiftRuntimeState state, string intentId, StateVersion expected) => new(
        state.ShiftId, IntentId.From(intentId), ActorId.From("untrusted_hint"), LineRepairIntentTargets.Line,
        LineRepairIntentActions.StartLineRepair, expected, ServerTick.Zero, NoIntentParameters.Instance);

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

    private static ShiftRuntimeState FeedGateJammed()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
        return Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.FEED_GATE_BLOCKED, ServerTick.From(10))).State;
    }

    private static ShiftRuntimeState IntakeAutoFeedJammed()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveToIntake(state, "log_02");
        return Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.INTAKE_AUTOFEED_BLOCKED, ServerTick.From(10))).State;
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
        ServerTick Tick);

    private sealed class HistoryPayload : IDomainEventPayload
    {
        public static readonly HistoryPayload Instance = new();
    }
}
