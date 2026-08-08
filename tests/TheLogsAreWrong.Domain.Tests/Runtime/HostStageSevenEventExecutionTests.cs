using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
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

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-036")]
public sealed class HostStageSevenEventExecutionTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static readonly ProfileId LearningId = ProfileId.From("learning");
    private static ShiftProfile Learning => Fx.Shift.Profiles[LearningId];

    [Fact]
    public void Initial_tick_plans_and_publishes_the_exact_stage_five_then_stage_six_causal_stream()
    {
        var execution = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var journal = new InMemoryEventJournal(execution.StageOne.InitialState.ShiftId);

        var published = Assert.IsType<HostStageSevenPublished>(new HostStageSevenEventExecutor().Execute(
            execution.StageOne,
            execution.StageTwo,
            execution.StageThree,
            execution.StageFour,
            execution.StageFive,
            execution.StageSix,
            journal,
            EventIds(4),
            ServerTick.Zero));

        Assert.Equal(
            [
                HostStageSevenEventTypes.FeedScheduled,
                HostStageSevenEventTypes.LogAdmittedToIntake,
                HostStageSevenEventTypes.IntakeDeadlineStarted,
                HostStageSevenEventTypes.LineNoiseChanged
            ],
            published.Publications.Select(publication => publication.Envelope.EventType));
        Assert.Equal([1L, 2L, 3L, 3L], published.Publications.Select(publication => publication.Envelope.StateVersionAfter.Value));
        Assert.Equal([1L, 2L, 3L, 4L], journal.Events.Select(envelope => envelope.Sequence.Value));
        Assert.Empty(published.Rejections);
        Assert.Same(execution.StageSix.FinalShiftState, published.FinalShiftState);
        Assert.Equal(execution.StageSix.FinalShiftState.StateVersion, journal.LastStateVersion);
    }

    [Fact]
    public void Accepted_early_feed_publishes_observation_before_schedule_with_the_exact_intent_causation()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var intent = new IntentEnvelope(
            initial.ShiftId,
            IntentId.From("early_feed"),
            ActorId.From("hint"),
            FeedPlanningTargets.FeedGate,
            FeedPlanningIntentActions.RequestEarlyFeed,
            initial.StateVersion,
            ServerTick.Zero,
            NoIntentParameters.Instance);
        var receipt = new AuthoritativeAcceptedIntent(intent, RuntimeFixture.BoundActor, ServerTick.Zero, ServerReceiveSequence.Zero);
        var execution = BuildExecution(ImmutableArray.Create(receipt));
        var journal = new InMemoryEventJournal(initial.ShiftId);

        var published = Assert.IsType<HostStageSevenPublished>(new HostStageSevenEventExecutor().Execute(
            execution.StageOne,
            execution.StageTwo,
            execution.StageThree,
            execution.StageFour,
            execution.StageFive,
            execution.StageSix,
            journal,
            EventIds(2),
            ServerTick.Zero));

        Assert.Equal(
            [HostStageSevenEventTypes.EarlyFeedRequested, HostStageSevenEventTypes.FeedScheduled],
            published.Publications.Select(publication => publication.Envelope.EventType));
        Assert.All(published.Publications, publication => Assert.Equal(intent.IntentId, publication.Envelope.CausedByIntentId));
        Assert.Equal(0L, published.Publications[0].Envelope.StateVersionAfter.Value);
        Assert.Equal(1L, published.Publications[1].Envelope.StateVersionAfter.Value);
    }

    [Fact]
    public void Planned_id_cardinality_failure_is_pre_append_and_preserves_the_journal()
    {
        var execution = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var journal = new InMemoryEventJournal(execution.StageOne.InitialState.ShiftId);

        Assert.Throws<ArgumentException>(() => new HostStageSevenEventExecutor().Execute(
            execution.StageOne,
            execution.StageTwo,
            execution.StageThree,
            execution.StageFour,
            execution.StageFive,
            execution.StageSix,
            journal,
            EventIds(3),
            ServerTick.Zero));

        Assert.Equal(0, journal.Count);
        Assert.Equal(EventSequence.None, journal.LastSequence);
        Assert.Equal(StateVersion.Zero, journal.LastStateVersion);
    }

    [Fact]
    public void A_valid_no_semantic_event_tick_accepts_empty_ids_and_preserves_the_exact_journal_cursor()
    {
        var first = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var journal = new InMemoryEventJournal(first.StageOne.InitialState.ShiftId);
        var executor = new HostStageSevenEventExecutor();
        _ = executor.Execute(first.StageOne, first.StageTwo, first.StageThree, first.StageFour, first.StageFive, first.StageSix, journal, EventIds(4), ServerTick.Zero);
        var checkpoint = Assert.IsType<HostTickCheckpointAdvanced>(first.StageSix.Checkpoint);
        var next = BuildExecutionFrom(
            first.StageSix.FinalShiftState,
            first.StageFour.FinalQuotaState,
            first.StageSix.FinalMovementNoise,
            first.StageSix.FinalLineNoise,
            checkpoint.Progression,
            checkpoint.Receipt.Lifecycle,
            ServerTick.From(1));
        var before = (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion);

        var published = Assert.IsType<HostStageSevenPublished>(executor.Execute(
            next.StageOne, next.StageTwo, next.StageThree, next.StageFour, next.StageFive, next.StageSix,
            journal, ImmutableArray<EventId>.Empty, ServerTick.From(1)));

        Assert.Empty(published.Publications);
        Assert.Equal(before, (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion));
    }

    [Fact]
    public void Manual_gameplay_rejection_is_materialized_without_a_journal_rejection_event()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var stale = new IntentEnvelope(
            initial.ShiftId,
            IntentId.From("stale"),
            ActorId.From("hint"),
            TargetId.From("log_01"),
            LogIntentActions.RouteToProcedure,
            initial.StateVersion.Next(),
            ServerTick.Zero,
            NoIntentParameters.Instance);
        var execution = BuildExecution(ImmutableArray.Create(new AuthoritativeAcceptedIntent(stale, RuntimeFixture.BoundActor, ServerTick.Zero, ServerReceiveSequence.Zero)));
        var journal = new InMemoryEventJournal(initial.ShiftId);

        var published = Assert.IsType<HostStageSevenPublished>(new HostStageSevenEventExecutor().Execute(
            execution.StageOne, execution.StageTwo, execution.StageThree, execution.StageFour, execution.StageFive, execution.StageSix,
            journal, EventIds(4), ServerTick.Zero));

        var rejection = Assert.Single(published.Rejections);
        Assert.Equal(stale.IntentId, rejection.IntentId);
        Assert.Equal(RejectionReason.STALE_STATE_VERSION, rejection.Reason);
        Assert.Equal(4, journal.Count);
        Assert.DoesNotContain(journal.Events, envelope => envelope.EventType.Value == "IntentRejected");
    }

    [Fact]
    public void Exact_already_published_advanced_checkpoint_does_not_append_duplicates()
    {
        var execution = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var journal = new InMemoryEventJournal(execution.StageOne.InitialState.ShiftId);
        var ids = EventIds(4);
        var executor = new HostStageSevenEventExecutor();
        _ = executor.Execute(execution.StageOne, execution.StageTwo, execution.StageThree, execution.StageFour, execution.StageFive, execution.StageSix, journal, ids, ServerTick.Zero);
        var before = (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion);

        _ = Assert.IsType<HostStageSevenAlreadyPublished>(executor.Execute(
            execution.StageOne, execution.StageTwo, execution.StageThree, execution.StageFour, execution.StageFive, execution.StageSix,
            journal, ids, ServerTick.Zero));

        Assert.Equal(before, (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion));
    }

    [Fact]
    public void Exact_checkpoint_replay_returns_no_publication_after_matching_the_existing_journal_tail()
    {
        var execution = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var journal = new InMemoryEventJournal(execution.StageOne.InitialState.ShiftId);
        var ids = EventIds(4);
        var executor = new HostStageSevenEventExecutor();
        _ = executor.Execute(execution.StageOne, execution.StageTwo, execution.StageThree, execution.StageFour, execution.StageFive, execution.StageSix, journal, ids, ServerTick.Zero);
        var advanced = Assert.IsType<HostTickCheckpointAdvanced>(execution.StageSix.Checkpoint);
        var replayStageSix = new HostStageSixDerivedExecutor().Execute(
            execution.StageOne,
            execution.StageTwo,
            execution.StageThree,
            execution.StageFour,
            execution.StageFive,
            MovementNoiseRuntimeState.Create(execution.StageOne.InitialState.ShiftId),
            LineNoiseRuntimeState.Create(execution.StageOne.InitialState.ShiftId),
            advanced.Progression,
            advanced.Receipt.Lifecycle,
            ImmutableHashSet<ItemId>.Empty,
            ServerTick.Zero,
            Fx.Shift.Scheduler,
            Fx.Shift,
            Fx.Anomalies);
        Assert.IsType<HostTickCheckpointReplayed>(replayStageSix.Checkpoint);
        var before = (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion);

        _ = Assert.IsType<HostStageSevenAlreadyPublished>(executor.Execute(
            execution.StageOne, execution.StageTwo, execution.StageThree, execution.StageFour, execution.StageFive, replayStageSix,
            journal, ids, ServerTick.Zero));

        Assert.Equal(before, (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion));
    }

    [Fact]
    public void Rejected_checkpoint_blocks_publication_and_leaves_the_journal_untouched()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var execution = BuildExecutionFrom(
            initial,
            QuotaRuntimeState.Create(Fx.Shift),
            MovementNoiseRuntimeState.Create(initial.ShiftId),
            LineNoiseRuntimeState.Create(initial.ShiftId),
            HostTickProgressionEvidence.Create(initial.ShiftId),
            ShiftLifecycleRuntimeState.Create(Fx.Shift, LearningId),
            ServerTick.From(1));
        Assert.IsType<HostTickCheckpointRejected>(execution.StageSix.Checkpoint);
        var journal = new InMemoryEventJournal(initial.ShiftId);

        var blocked = Assert.IsType<HostStageSevenBlocked>(new HostStageSevenEventExecutor().Execute(
            execution.StageOne, execution.StageTwo, execution.StageThree, execution.StageFour, execution.StageFive, execution.StageSix,
            journal, ImmutableArray<EventId>.Empty, ServerTick.From(1)));

        Assert.Same(execution.StageSix.Checkpoint, blocked.CheckpointRejection);
        Assert.Equal(0, journal.Count);
        Assert.Equal(EventSequence.None, journal.LastSequence);
        Assert.Equal(StateVersion.Zero, journal.LastStateVersion);
    }

    private static ImmutableArray<EventId> EventIds(int count) =>
        Enumerable.Range(1, count).Select(index => EventId.From($"event_{index}")).ToImmutableArray();

    private static StageExecution BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent> receipts)
    {
        var initial = RuntimeFixture.CreateInitialState();
        return BuildExecutionFrom(
            initial,
            QuotaRuntimeState.Create(Fx.Shift),
            MovementNoiseRuntimeState.Create(initial.ShiftId),
            LineNoiseRuntimeState.Create(initial.ShiftId),
            HostTickProgressionEvidence.Create(initial.ShiftId),
            ShiftLifecycleRuntimeState.Create(Fx.Shift, LearningId),
            ServerTick.Zero,
            receipts);
    }

    private static StageExecution BuildExecutionFrom(
        ShiftRuntimeState initial,
        QuotaRuntimeState quota,
        MovementNoiseRuntimeState movementNoise,
        LineNoiseRuntimeState lineNoise,
        HostTickProgressionEvidence progression,
        ShiftLifecycleRuntimeState lifecycle,
        ServerTick tick,
        ImmutableArray<AuthoritativeAcceptedIntent>? receipts = null)
    {
        var stageOne = new HostStageOneCompletionExecutor().Execute(initial, tick, Fx.Anomalies, Fx.Shift.Containment);
        var batch = AcceptedIntentTickBatchFactory.Create(initial.ShiftId, tick, receipts ?? ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var stageTwo = new AcceptedIntentStageExecutor().Execute(stageOne.FinalState, batch, Fx.Shift.Scheduler);
        var stageThree = new HostStageThreeDeadlineExecutor().Execute(stageTwo.FinalState, tick, Fx.Shift.Containment, Fx.Anomalies);
        var stageFour = new HostStageFourSawExecutor().Execute(stageThree.FinalState, quota, tick, Fx.Shift.Scheduler, Fx.Anomalies);
        var stageFive = new HostStageFiveFeedExecutor().Execute(stageOne, stageTwo, stageThree, stageFour, tick, Fx.Shift.Scheduler, Learning);
        var stageSix = new HostStageSixDerivedExecutor().Execute(
            stageOne,
            stageTwo,
            stageThree,
            stageFour,
            stageFive,
            movementNoise,
            lineNoise,
            progression,
            lifecycle,
            ImmutableHashSet<ItemId>.Empty,
            tick,
            Fx.Shift.Scheduler,
            Fx.Shift,
            Fx.Anomalies);
        return new StageExecution(stageOne, stageTwo, stageThree, stageFour, stageFive, stageSix);
    }

    private sealed record StageExecution(
        HostStageOneCompletionExecution StageOne,
        AcceptedIntentStageExecution StageTwo,
        HostStageThreeDeadlineExecution StageThree,
        HostStageFourSawExecution StageFour,
        HostStageFiveFeedExecution StageFive,
        HostStageSixDerivedExecution StageSix);
}
