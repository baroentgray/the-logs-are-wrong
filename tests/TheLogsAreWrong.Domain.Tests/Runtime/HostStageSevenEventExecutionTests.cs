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
    public void Rejected_early_feed_is_a_non_journaled_rejection_and_never_fabricates_feed_events()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var stale = new IntentEnvelope(
            initial.ShiftId,
            IntentId.From("stale_early_feed"),
            ActorId.From("hint"),
            FeedPlanningTargets.FeedGate,
            FeedPlanningIntentActions.RequestEarlyFeed,
            initial.StateVersion.Next(),
            ServerTick.Zero,
            NoIntentParameters.Instance);
        var execution = BuildExecution(ImmutableArray.Create(new AuthoritativeAcceptedIntent(stale, RuntimeFixture.BoundActor, ServerTick.Zero, ServerReceiveSequence.Zero)));
        var journal = new InMemoryEventJournal(initial.ShiftId);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(execution, journal, EventIds(4), ServerTick.Zero));

        var rejection = Assert.Single(published.Rejections);
        Assert.Equal((stale.IntentId, RejectionReason.STALE_STATE_VERSION, initial.StateVersion), (rejection.IntentId, rejection.Reason, rejection.CurrentStateVersion));
        Assert.DoesNotContain(published.Publications, publication => publication.Envelope.EventType == HostStageSevenEventTypes.EarlyFeedRequested);
        Assert.DoesNotContain(published.Publications, publication => publication.Envelope.EventType == HostStageSevenEventTypes.FeedScheduled && publication.Envelope.CausedByIntentId == stale.IntentId);
    }

    [Fact]
    public void Unsupported_stage_two_intent_fabricates_neither_event_nor_rejection()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var unsupported = new IntentEnvelope(
            initial.ShiftId,
            IntentId.From("unsupported"),
            ActorId.From("hint"),
            TargetId.From("log_01"),
            IntentActionId.From("unknown_action"),
            initial.StateVersion,
            ServerTick.Zero,
            NoIntentParameters.Instance);
        var execution = BuildExecution(ImmutableArray.Create(new AuthoritativeAcceptedIntent(unsupported, RuntimeFixture.BoundActor, ServerTick.Zero, ServerReceiveSequence.Zero)));
        var journal = new InMemoryEventJournal(initial.ShiftId);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(execution, journal, EventIds(4), ServerTick.Zero));

        Assert.Empty(published.Rejections);
        Assert.DoesNotContain(published.Publications, publication => publication.Envelope.CausedByIntentId == unsupported.IntentId);
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
    public void Too_many_event_ids_are_rejected_before_any_journal_append()
    {
        var execution = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var journal = new InMemoryEventJournal(execution.StageOne.InitialState.ShiftId);
        var before = Snapshot(journal);

        Assert.Throws<ArgumentException>(() => Execute(execution, journal, EventIds(5), ServerTick.Zero));

        AssertJournalUnchanged(before, journal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Every_null_public_reference_input_is_rejected_before_publication(int nullInput)
    {
        var execution = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var journal = new InMemoryEventJournal(execution.StageOne.InitialState.ShiftId);
        var before = Snapshot(journal);

        Assert.Throws<ArgumentNullException>(() => new HostStageSevenEventExecutor().Execute(
            nullInput == 0 ? null! : execution.StageOne,
            nullInput == 1 ? null! : execution.StageTwo,
            nullInput == 2 ? null! : execution.StageThree,
            nullInput == 3 ? null! : execution.StageFour,
            nullInput == 4 ? null! : execution.StageFive,
            nullInput == 5 ? null! : execution.StageSix,
            nullInput == 6 ? null! : journal,
            EventIds(4),
            ServerTick.Zero));

        AssertJournalUnchanged(before, journal);
    }

    [Fact]
    public void Default_tick_default_event_id_and_uninitialized_id_array_are_rejected_without_append()
    {
        var execution = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var journal = new InMemoryEventJournal(execution.StageOne.InitialState.ShiftId);
        var before = Snapshot(journal);

        Assert.Throws<ArgumentException>(() => Execute(execution, journal, EventIds(4), default));
        AssertJournalUnchanged(before, journal);
        Assert.Throws<ArgumentException>(() => Execute(execution, journal, default, ServerTick.Zero));
        AssertJournalUnchanged(before, journal);
        Assert.Throws<ArgumentException>(() => Execute(execution, journal, ImmutableArray.Create(default(EventId), EventId.From("two"), EventId.From("three"), EventId.From("four")), ServerTick.Zero));
        AssertJournalUnchanged(before, journal);
    }

    [Fact]
    public void Broken_stage_reference_chain_and_stage_tick_mismatch_are_rejected_without_append()
    {
        var execution = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var unrelated = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var journal = new InMemoryEventJournal(execution.StageOne.InitialState.ShiftId);
        var before = Snapshot(journal);

        Assert.Throws<ArgumentException>(() => new HostStageSevenEventExecutor().Execute(
            execution.StageOne, unrelated.StageTwo, execution.StageThree, execution.StageFour, execution.StageFive, execution.StageSix,
            journal, EventIds(4), ServerTick.Zero));
        AssertJournalUnchanged(before, journal);
        Assert.Throws<ArgumentException>(() => Execute(execution, journal, EventIds(4), ServerTick.From(1)));
        AssertJournalUnchanged(before, journal);
    }

    [Fact]
    public void Cross_shift_future_tick_and_behind_or_ahead_journal_cursors_fail_closed_pre_append()
    {
        var (_, next, priorJournal, _) = PublishPrecedingEventfulTickThenBuildZeroEventTick();
        var before = Snapshot(priorJournal);
        var wrongShift = new InMemoryEventJournal(ShiftId.From("other_shift"));

        Assert.Throws<ArgumentException>(() => Execute(next, wrongShift, ImmutableArray<EventId>.Empty, ServerTick.From(1)));
        AssertJournalUnchanged(before, priorJournal);

        var shift = next.StageOne.InitialState.ShiftId;
        foreach (var journal in new[]
                 {
                     new CursorJournal(shift, EventSequence.From(4), ServerTick.Zero, StateVersion.Zero),
                     new CursorJournal(shift, EventSequence.From(4), ServerTick.Zero, StateVersion.From(4)),
                     new CursorJournal(shift, EventSequence.From(4), ServerTick.From(2), next.StageOne.InitialState.StateVersion)
                 })
        {
            var journalBefore = Snapshot(journal);
            Assert.ThrowsAny<Exception>(() => Execute(next, journal, ImmutableArray<EventId>.Empty, ServerTick.From(1)));
            AssertJournalUnchanged(journalBefore, journal);
            Assert.Equal(0, journal.AppendAttempts);
        }
    }

    [Fact]
    public void Sequence_overflow_is_detected_before_first_append()
    {
        var execution = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var journal = new CursorJournal(execution.StageOne.InitialState.ShiftId, EventSequence.From(long.MaxValue), ServerTick.Zero, execution.StageOne.InitialState.StateVersion);
        var before = Snapshot(journal);

        Assert.Throws<OverflowException>(() => Execute(execution, journal, EventIds(4), ServerTick.Zero));

        AssertJournalUnchanged(before, journal);
        Assert.Equal(0, journal.AppendAttempts);
    }

    [Fact]
    public void First_valid_zero_event_tick_returns_no_new_publication_and_preserves_the_exact_journal_cursor()
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

        var noPublication = Assert.IsType<HostStageSevenNoNewPublication>(executor.Execute(
            next.StageOne, next.StageTwo, next.StageThree, next.StageFour, next.StageFive, next.StageSix,
            journal, ImmutableArray<EventId>.Empty, ServerTick.From(1)));

        Assert.Empty(noPublication.Rejections);
        Assert.Empty(noPublication.AssignedEventIds);
        Assert.Equal(before, (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion));
    }

    [Fact]
    public void Reinvoking_the_same_advanced_zero_event_tick_returns_no_new_publication_without_appending()
    {
        var (_, next, journal, executor) = PublishPrecedingEventfulTickThenBuildZeroEventTick();
        var before = (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion);
        var beforeEvents = journal.Events.ToArray();

        _ = Assert.IsType<HostStageSevenNoNewPublication>(executor.Execute(
            next.StageOne, next.StageTwo, next.StageThree, next.StageFour, next.StageFive, next.StageSix,
            journal, ImmutableArray<EventId>.Empty, ServerTick.From(1)));
        _ = Assert.IsType<HostStageSevenNoNewPublication>(executor.Execute(
            next.StageOne, next.StageTwo, next.StageThree, next.StageFour, next.StageFive, next.StageSix,
            journal, ImmutableArray<EventId>.Empty, ServerTick.From(1)));

        Assert.Equal(before, (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion));
        Assert.Equal(beforeEvents, journal.Events);
    }

    [Fact]
    public void Replaying_a_completed_zero_event_tick_accepts_the_preserved_prior_journal_cursor()
    {
        var (_, next, journal, executor) = PublishPrecedingEventfulTickThenBuildZeroEventTick();
        _ = Assert.IsType<HostStageSevenNoNewPublication>(executor.Execute(
            next.StageOne, next.StageTwo, next.StageThree, next.StageFour, next.StageFive, next.StageSix,
            journal, ImmutableArray<EventId>.Empty, ServerTick.From(1)));
        var advanced = Assert.IsType<HostTickCheckpointAdvanced>(next.StageSix.Checkpoint);
        var replay = new HostStageSixDerivedExecutor().Execute(
            next.StageOne, next.StageTwo, next.StageThree, next.StageFour, next.StageFive,
            next.StageSix.InitialMovementNoise, next.StageSix.InitialLineNoise,
            advanced.Progression, advanced.Receipt.Lifecycle, ImmutableHashSet<ItemId>.Empty,
            ServerTick.From(1), Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies);
        Assert.IsType<HostTickCheckpointReplayed>(replay.Checkpoint);
        var before = (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion);
        var beforeEvents = journal.Events.ToArray();

        _ = Assert.IsType<HostStageSevenNoNewPublication>(executor.Execute(
            next.StageOne, next.StageTwo, next.StageThree, next.StageFour, next.StageFive, replay,
            journal, ImmutableArray<EventId>.Empty, ServerTick.From(1)));

        Assert.Equal(before, (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion));
        Assert.Equal(beforeEvents, journal.Events);
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

    [Theory]
    [InlineData("route_to_procedure", "LogRouted")]
    [InlineData("write_off", "LogWrittenOff")]
    public void Accepted_manual_intent_publishes_the_exact_transition_with_receipt_causation(string action, string expectedEventType)
    {
        var initial = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var intent = new IntentEnvelope(
            initial.ShiftId,
            IntentId.From($"accepted_{action}"),
            ActorId.From("hint"),
            TargetId.From("log_01"),
            IntentActionId.From(action),
            initial.StateVersion,
            ServerTick.Zero,
            NoIntentParameters.Instance);
        var execution = BuildExecutionFrom(
            initial,
            QuotaRuntimeState.Create(Fx.Shift),
            MovementNoiseRuntimeState.Create(initial.ShiftId),
            LineNoiseRuntimeState.Create(initial.ShiftId),
            HostTickProgressionEvidence.Create(initial.ShiftId),
            ShiftLifecycleRuntimeState.Create(Fx.Shift, LearningId),
            ServerTick.Zero,
            ImmutableArray.Create(new AuthoritativeAcceptedIntent(intent, RuntimeFixture.BoundActor, ServerTick.Zero, ServerReceiveSequence.Zero)));
        var journal = JournalAtState(initial, ServerTick.Zero);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(execution, journal, EventIds(3), ServerTick.Zero));

        var envelope = Assert.Single(published.Publications, publication => publication.Envelope.EventType == EventTypeId.From(expectedEventType)).Envelope;
        var payload = Assert.IsType<HostStageSevenLogTransitionPayload>(envelope.Payload);
        Assert.Equal(intent.IntentId, envelope.CausedByIntentId);
        Assert.Equal(LogId.From("log_01"), payload.LogId);
        Assert.Equal(initial.StateVersion, payload.PriorStateVersion);
        Assert.Equal(initial.StateVersion.Next(), payload.CurrentStateVersion);
        Assert.Equal(expectedEventType, envelope.EventType.Value);
    }

    [Fact]
    public void Intake_deadline_expiration_precedes_its_default_auto_route_with_exact_deadline_evidence()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var scheduled = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, Fx.Shift.Scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(scheduled.State, ServerTick.Zero));
        var started = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admitted.State, admitted, Learning));
        var (progression, lifecycle) = AdvanceActiveCheckpointTo(started.State, QuotaRuntimeState.Create(Fx.Shift), started.Deadline.DueAt);
        var execution = BuildExecutionFrom(
            started.State,
            QuotaRuntimeState.Create(Fx.Shift),
            MovementNoiseRuntimeState.Create(initial.ShiftId),
            LineNoiseRuntimeState.Create(initial.ShiftId),
            progression,
            lifecycle,
            started.Deadline.DueAt);
        var journal = JournalAtState(started.State, ServerTick.Zero);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(execution, journal, EventIds(4), started.Deadline.DueAt));

        Assert.Equal(
            [HostStageSevenEventTypes.IntakeDeadlineExpired, HostStageSevenEventTypes.AutoRouteAttempted, HostStageSevenEventTypes.FeedScheduled, HostStageSevenEventTypes.LineNoiseChanged],
            published.Publications.Select(publication => publication.Envelope.EventType));
        var deadline = Assert.IsType<HostStageSevenIntakeDeadlinePayload>(published.Publications[0].Envelope.Payload);
        Assert.Equal((started.Deadline.LogId, started.Deadline.StartedAt, started.Deadline.DueAt, started.Deadline.DueAt),
            (deadline.LogId, deadline.StartedAt, deadline.DueAt, deadline.OccurredAt));
        Assert.Equal(execution.StageSix.FinalShiftState.StateVersion, journal.LastStateVersion);
    }

    [Fact]
    public void Due_procedure_completion_is_the_first_stage_one_publication_with_exact_descriptor_evidence()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_03");
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_PROCEDURE);
        var started = Assert.IsType<ProcedureActionHoldStarted>(new ProcedureActionStartService().Start(
            state, LogId.From("log_03"), ItemId.From("holy_water"), ServerTick.Zero, Fx.Anomalies));
        var due = started.Hold.DueAt;
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var (progression, lifecycle) = AdvanceActiveCheckpointTo(started.State, quota, due);
        var execution = BuildExecutionFrom(
            started.State, quota, MovementNoiseRuntimeState.Create(state.ShiftId), LineNoiseRuntimeState.Create(state.ShiftId),
            progression, lifecycle, due);
        var journal = JournalAtState(started.State, ServerTick.Zero);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(execution, journal, EventIds(1), due));

        Assert.Equal(HostStageSevenEventTypes.ProcedureActionCompleted, published.Publications[0].Envelope.EventType);
        var payload = Assert.IsType<HostStageSevenProcedurePayload>(published.Publications[0].Envelope.Payload);
        var completed = Assert.IsType<ProcedureActionDueCompleted>(execution.StageOne.Procedure.Result);
        Assert.Equal(completed.Descriptor.LogId, payload.Descriptor.LogId);
        Assert.Equal(completed.Descriptor.PriorStateVersion, payload.PriorStateVersion);
        Assert.Equal(completed.Descriptor.CurrentStateVersion, payload.CurrentStateVersion);
    }

    [Fact]
    public void Due_confirmation_completion_publishes_the_exact_result_and_state_versions()
    {
        var intake = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_10");
        var started = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(
            intake, LogId.From("log_10"), ImmutableHashSet.Create(ItemId.From("choir_cassette")), ServerTick.Zero,
            LineNoiseRuntimeState.Create(intake.ShiftId), Fx.Anomalies));
        var due = started.State.ActiveConfirmationTest!.DueAt!.Value;
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var (progression, lifecycle) = AdvanceActiveCheckpointTo(started.State, quota, due);
        var execution = BuildExecutionFrom(
            started.State, quota, MovementNoiseRuntimeState.Create(intake.ShiftId), LineNoiseRuntimeState.Create(intake.ShiftId),
            progression, lifecycle, due);
        var journal = JournalAtState(started.State, ServerTick.Zero);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(execution, journal, EventIds(1), due));

        Assert.Equal(HostStageSevenEventTypes.ConfirmationTestCompleted, published.Publications[0].Envelope.EventType);
        var payload = Assert.IsType<HostStageSevenConfirmationPayload>(published.Publications[0].Envelope.Payload);
        Assert.Equal(LogId.From("log_10"), payload.Result.LogId);
        Assert.Equal(execution.StageOne.Confirmation.BeforeState.StateVersion, payload.PriorStateVersion);
        Assert.Equal(execution.StageOne.Confirmation.AfterState.StateVersion, payload.CurrentStateVersion);
    }

    [Fact]
    public void Line_noise_observation_precedes_confirmation_condition_mutation_at_the_exact_post_noise_version()
    {
        var tools = ImmutableHashSet.Create(ItemId.From("sound_meter"));
        var queued = RuntimeFixture.MoveHost(RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01"), "log_01", LogState.QUEUED_FOR_SAW);
        var intake = RuntimeFixture.MoveToIntake(queued, "log_03");
        var started = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(
            intake, LogId.From("log_03"), tools, ServerTick.From(5),
            LineNoiseRuntimeState.Create(intake.ShiftId), Fx.Anomalies));
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var (progression, lifecycle) = AdvanceActiveCheckpointTo(started.State, quota, ServerTick.From(6));
        var execution = BuildExecutionFrom(
            started.State, quota, MovementNoiseRuntimeState.Create(intake.ShiftId), LineNoiseRuntimeState.Create(intake.ShiftId),
            progression, lifecycle, ServerTick.From(6), activeTools: tools);
        var journal = JournalAtState(started.State, ServerTick.Zero);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(execution, journal, EventIds(3), ServerTick.From(6)));

        var noiseIndex = Array.IndexOf(published.Publications.Select(publication => publication.Envelope.EventType).ToArray(), HostStageSevenEventTypes.LineNoiseChanged);
        var confirmationIndex = Array.IndexOf(published.Publications.Select(publication => publication.Envelope.EventType).ToArray(), HostStageSevenEventTypes.ConfirmationConditionUpdated);
        Assert.True(noiseIndex >= 0 && confirmationIndex > noiseIndex);
        var noise = Assert.IsType<HostStageSevenLineNoisePayload>(published.Publications[noiseIndex].Envelope.Payload);
        var confirmation = Assert.IsType<HostStageSevenConfirmationConditionPayload>(published.Publications[confirmationIndex].Envelope.Payload);
        Assert.Equal(noise.CurrentStateVersion, confirmation.PriorStateVersion);
        Assert.Equal(confirmation.CurrentStateVersion, journal.LastStateVersion);
        var saw = Assert.Single(published.Publications, publication => publication.Envelope.EventType == HostStageSevenEventTypes.SawCycleStarted);
        Assert.Equal(Assert.IsType<SawCycleStarted>(execution.StageFour.Start.Result).Cycle, Assert.IsType<HostStageSevenSawStartedPayload>(saw.Envelope.Payload).Cycle);
    }

    [Fact]
    public void Newly_completed_checkpoint_publishes_shift_completed_as_the_observational_tail_with_exact_evidence()
    {
        var state = RuntimeFixture.CreateInitialState();
        foreach (var logId in state.Logs.Select(log => log.LogId.ToString()).ToArray())
        {
            state = RuntimeFixture.MoveToIntake(state, logId);
            state = RuntimeFixture.MoveHost(state, logId, LogState.HELD_WRITTEN_OFF);
        }

        var execution = BuildExecutionFrom(
            state, QuotaRuntimeState.Create(Fx.Shift), MovementNoiseRuntimeState.Create(state.ShiftId), LineNoiseRuntimeState.Create(state.ShiftId),
            HostTickProgressionEvidence.Create(state.ShiftId), ShiftLifecycleRuntimeState.Create(Fx.Shift, LearningId), ServerTick.Zero);
        var journal = JournalAtState(state, ServerTick.Zero);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(execution, journal, EventIds(2), ServerTick.Zero));

        var completion = Assert.IsType<HostTickCheckpointAdvanced>(execution.StageSix.Checkpoint).Receipt.Evaluation;
        var completed = Assert.IsType<ShiftCompletionNewlyCompleted>(completion).Completion;
        var envelope = published.Publications[^1].Envelope;
        Assert.Equal(HostStageSevenEventTypes.ShiftCompleted, envelope.EventType);
        Assert.Equal(execution.StageSix.FinalShiftState.StateVersion, envelope.StateVersionAfter);
        var payload = Assert.IsType<HostStageSevenShiftCompletedPayload>(envelope.Payload);
        Assert.Equal((completed.CompletedAt, completed.Reason, completed.ObjectivesSatisfied, completed.ProcessedCount, completed.WrittenOffCount),
            (payload.CompletedAt, payload.Reason, payload.ObjectivesSatisfied, payload.ProcessedCount, payload.WrittenOffCount));
        AssertSpeciesValues(completed.Quota.TargetBySpecies, payload.TargetBySpecies);
        AssertSpeciesValues(completed.Quota.CreditedBySpecies, payload.CreditedBySpecies);
    }

    [Fact]
    public void Saw_completion_publishes_exact_accepted_quota_evidence_before_its_successor_without_a_quota_event()
    {
        var queued = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        queued = RuntimeFixture.MoveHost(queued, "log_01", LogState.QUEUED_FOR_SAW);
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var startedProgress = AdvanceActiveCheckpointTo(queued, quota, ServerTick.Zero);
        var started = BuildExecutionFrom(
            queued, quota, MovementNoiseRuntimeState.Create(queued.ShiftId), LineNoiseRuntimeState.Create(queued.ShiftId),
            startedProgress.Progression, startedProgress.Lifecycle, ServerTick.Zero);
        var startedCycle = Assert.IsType<SawCycleStarted>(started.StageFour.Start.Result).Cycle;
        var successorState = RuntimeFixture.MoveToIntake(started.StageSix.FinalShiftState, "log_02");
        successorState = RuntimeFixture.MoveHost(successorState, "log_02", LogState.QUEUED_FOR_SAW);
        var dueProgress = AdvanceActiveCheckpointTo(successorState, started.StageFour.FinalQuotaState, startedCycle.DueAt);
        var execution = BuildExecutionFrom(
            successorState, started.StageFour.FinalQuotaState, started.StageSix.FinalMovementNoise, started.StageSix.FinalLineNoise,
            dueProgress.Progression, dueProgress.Lifecycle, startedCycle.DueAt);
        var journal = JournalAtState(execution.StageOne.InitialState, ServerTick.Zero);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(execution, journal, EventIds(2), startedCycle.DueAt));

        Assert.Equal([HostStageSevenEventTypes.SawCycleCompleted, HostStageSevenEventTypes.SawCycleStarted], published.Publications.Select(publication => publication.Envelope.EventType));
        Assert.All(published.Publications, publication => Assert.Equal(HostStageSevenPublicationKind.StateChanging, publication.Kind));
        var completed = Assert.IsType<SawCycleCompleted>(execution.StageFour.Completion.Result);
        var quotaResult = Assert.IsType<SawQuotaApplicationAccepted>(execution.StageFour.Quota.Result);
        var payload = Assert.IsType<HostStageSevenSawCompletedPayload>(published.Publications[0].Envelope.Payload);
        Assert.Equal((completed.Cycle, completed.Cycle.StartedAt, completed.Cycle.DueAt, completed.CompletedAt, completed.Resolution, completed.Resolution.Settlement),
            (payload.Cycle, payload.Cycle.StartedAt, payload.Cycle.DueAt, payload.CompletedAt, payload.Resolution, payload.QuotaSettlement));
        Assert.Equal(HostStageSevenSawQuotaOutcome.Accepted, payload.QuotaApplicationOutcome);
        Assert.Equal(quotaResult.AcceptedSettlement.Descriptor, payload.AcceptedQuotaSettlement);
        Assert.Null(payload.DuplicateQuotaSettlementLogId);
        Assert.DoesNotContain(published.Publications, publication => publication.Envelope.EventType is { Value: { } type } && type.Contains("Quota", StringComparison.Ordinal));
    }

    [Fact]
    public void Saw_quota_already_applied_evidence_is_publicly_reachable_without_a_second_credit_but_is_rejected_before_a_closed_stage_six_trace()
    {
        var queued = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        queued = RuntimeFixture.MoveHost(queued, "log_01", LogState.QUEUED_FOR_SAW);
        var initialQuota = QuotaRuntimeState.Create(Fx.Shift);
        var started = new HostStageFourSawExecutor().Execute(queued, initialQuota, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Anomalies);
        var cycle = Assert.IsType<SawCycleStarted>(started.Start.Result).Cycle;
        var completed = Assert.IsType<SawCycleCompleted>(new SawCycleCompletionService().Complete(started.FinalShiftState, cycle.DueAt, Fx.Anomalies));
        var settledQuota = Assert.IsType<SawQuotaApplicationAccepted>(new SawQuotaApplicationService().Apply(completed, initialQuota)).QuotaState;
        var duplicate = Assert.IsType<SawQuotaApplicationAlreadyApplied>(new SawQuotaApplicationService().Apply(completed, settledQuota));

        Assert.Equal((completed.Cycle.LogId, completed.Cycle.LogId), (duplicate.CompletedLogId, duplicate.DuplicateSettlement.LogId));
        Assert.Same(settledQuota, duplicate.QuotaState);
        Assert.Equal(settledQuota.TotalCreditedUnits, duplicate.QuotaState.TotalCreditedUnits);
        Assert.Throws<InvalidOperationException>(() => AdvanceActiveCheckpointTo(started.FinalShiftState, settledQuota, cycle.DueAt));
    }

    [Fact]
    public void Containment_ritual_and_repair_completions_publish_from_stage_one_before_their_later_causal_consequences()
    {
        var ritualState = WriteOff(RuntimeFixture.CreateInitialState(), "log_03");
        ritualState = Assert.IsType<ContainmentStableIntervalArmed>(new ContainmentAdvanceService().Advance(ritualState, ServerTick.From(10), Fx.Shift.Containment, Fx.Anomalies)).State;
        ritualState = Assert.IsType<ContainmentStateAdvanced>(new ContainmentAdvanceService().Advance(ritualState, ServerTick.From(100), Fx.Shift.Containment, Fx.Anomalies)).State;
        var ritual = Assert.IsType<ContainmentRitualStarted>(new ContainmentRitualStartService().Start(ritualState, ServerTick.From(100), Fx.Shift.Containment));
        var ritualProgress = AdvanceActiveCheckpointTo(ritual.State, QuotaRuntimeState.Create(Fx.Shift), ritual.Ritual.DueAt);
        var ritualExecution = BuildExecutionFrom(
            ritual.State, QuotaRuntimeState.Create(Fx.Shift), MovementNoiseRuntimeState.Create(ritual.State.ShiftId), LineNoiseRuntimeState.Create(ritual.State.ShiftId),
            ritualProgress.Progression, ritualProgress.Lifecycle, ritual.Ritual.DueAt);
        var ritualPublished = Assert.IsType<HostStageSevenPublished>(Execute(ritualExecution, JournalAtState(ritual.State, ServerTick.Zero), EventIds(1), ritual.Ritual.DueAt));

        var ritualPayload = Assert.IsType<HostStageSevenContainmentPayload>(Assert.Single(ritualPublished.Publications).Envelope.Payload);
        Assert.Equal(HostStageSevenEventTypes.ContainmentRitualCompleted, ritualPublished.Publications[0].Envelope.EventType);
        Assert.Equal(ritualExecution.StageOne.ContainmentRitual.BeforeState.Containment, ritualPayload.PriorContainment);
        Assert.Equal(ritualExecution.StageOne.ContainmentRitual.AfterState.Containment, ritualPayload.CurrentContainment);

        var repairState = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        repairState = RuntimeFixture.MoveHost(repairState, "log_02", LogState.AT_FEED_GATE);
        repairState = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(repairState, JamCause.FEED_GATE_BLOCKED, ServerTick.From(10))).State;
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(repairState, ServerTick.From(10), Fx.Shift.Scheduler));
        repairState = RuntimeFixture.MoveHost(repairing.State, "log_01", LogState.QUEUED_FOR_SAW);
        var repairProgress = AdvanceActiveCheckpointTo(repairState, QuotaRuntimeState.Create(Fx.Shift), repairing.Hold.DueAt);
        var repairExecution = BuildExecutionFrom(
            repairState, QuotaRuntimeState.Create(Fx.Shift), MovementNoiseRuntimeState.Create(repairState.ShiftId), LineNoiseRuntimeState.Create(repairState.ShiftId),
            repairProgress.Progression, repairProgress.Lifecycle, repairing.Hold.DueAt);
        var repairPublished = Assert.IsType<HostStageSevenPublished>(Execute(repairExecution, JournalAtState(repairState, ServerTick.Zero), EventIds(5), repairing.Hold.DueAt));

        var repairIndex = Array.IndexOf(repairPublished.Publications.Select(publication => publication.Envelope.EventType).ToArray(), HostStageSevenEventTypes.RepairCompleted);
        var followUpIndex = Array.IndexOf(repairPublished.Publications.Select(publication => publication.Envelope.EventType).ToArray(), HostStageSevenEventTypes.LogAdmittedToIntake);
        Assert.True(repairIndex >= 0 && followUpIndex > repairIndex);
        var repairPayload = Assert.IsType<HostStageSevenRepairPayload>(repairPublished.Publications[repairIndex].Envelope.Payload);
        Assert.Equal(repairExecution.StageOne.LineRepair.BeforeState.Line, repairPayload.PriorLine);
        Assert.Equal(repairExecution.StageOne.LineRepair.AfterState.Line, repairPayload.CurrentLine);
    }

    [Fact]
    public void Intake_expiration_precedes_containment_change_with_exact_prior_and_current_containment_evidence()
    {
        var state = WriteOff(RuntimeFixture.CreateInitialState(), "log_03");
        state = Assert.IsType<ContainmentStableIntervalArmed>(new ContainmentAdvanceService().Advance(state, ServerTick.From(10), Fx.Shift.Containment, Fx.Anomalies)).State;
        var planned = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(state, ServerTick.From(35), Fx.Shift.Scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.From(40)));
        var deadline = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admitted.State, admitted, Learning));
        var progress = AdvanceActiveCheckpointTo(deadline.State, QuotaRuntimeState.Create(Fx.Shift), ServerTick.From(100));
        var execution = BuildExecutionFrom(
            deadline.State, QuotaRuntimeState.Create(Fx.Shift), MovementNoiseRuntimeState.Create(state.ShiftId), LineNoiseRuntimeState.Create(state.ShiftId),
            progress.Progression, progress.Lifecycle, ServerTick.From(100));
        var journal = JournalAtState(deadline.State, ServerTick.Zero);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(execution, journal, EventIds(5), ServerTick.From(100)));

        var types = published.Publications.Select(publication => publication.Envelope.EventType).ToArray();
        var expiryIndex = Array.IndexOf(types, HostStageSevenEventTypes.IntakeDeadlineExpired);
        var containmentIndex = Array.IndexOf(types, HostStageSevenEventTypes.ContainmentStateChanged);
        Assert.True(expiryIndex >= 0 && containmentIndex > expiryIndex);
        var payload = Assert.IsType<HostStageSevenContainmentPayload>(published.Publications[containmentIndex].Envelope.Payload);
        Assert.Equal(execution.StageThree.Containment.BeforeState.Containment, payload.PriorContainment);
        Assert.Equal(execution.StageThree.Containment.AfterState.Containment, payload.CurrentContainment);
        Assert.Equal(execution.StageThree.Containment.BeforeState.StateVersion, payload.PriorStateVersion);
        Assert.Equal(execution.StageThree.Containment.AfterState.StateVersion, payload.CurrentStateVersion);
    }

    [Fact]
    public void Blocked_default_route_then_intake_auto_feed_jam_retains_exact_tick_owner_cause_and_versions_without_movement_noise_events()
    {
        var scheduler = Fx.Shift.Scheduler with { Capacities = Fx.Shift.Scheduler.Capacities.SetItem(NodeId.INTAKE, NodeCapacity.Limited(2)) };
        var state = ShiftRuntimeState.Create(Fx.Shift with { Scheduler = scheduler });
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(state, ServerTick.Zero, scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        var deadline = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admitted.State, admitted, Learning));
        state = RuntimeFixture.MoveHost(deadline.State, "log_03", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_INTAKE);
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.QUEUED_FOR_SAW);
        state = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(state, ServerTick.From(59), Fx.Shift.Scheduler)).State;
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_INTAKE);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.QUEUED_FOR_SAW);
        var due = deadline.Deadline.DueAt;
        var progress = AdvanceActiveCheckpointTo(state, QuotaRuntimeState.Create(Fx.Shift), due);
        var execution = BuildExecutionFrom(
            state, QuotaRuntimeState.Create(Fx.Shift), MovementNoiseRuntimeState.Create(state.ShiftId), LineNoiseRuntimeState.Create(state.ShiftId),
            progress.Progression, progress.Lifecycle, due);
        var journal = JournalAtState(state, ServerTick.Zero);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(execution, journal, EventIds(4), due));

        var types = published.Publications.Select(publication => publication.Envelope.EventType).ToArray();
        var routeIndex = Array.IndexOf(types, HostStageSevenEventTypes.AutoRouteAttempted);
        var jamIndex = Array.IndexOf(types, HostStageSevenEventTypes.LineJammed);
        Assert.True(routeIndex >= 0 && jamIndex == routeIndex + 1);
        Assert.Equal(HostStageSevenPublicationKind.Observational, published.Publications[routeIndex].Kind);
        var route = Assert.IsType<HostStageSevenAutoRoutePayload>(published.Publications[routeIndex].Envelope.Payload);
        var jam = Assert.IsType<HostStageSevenLineJamPayload>(published.Publications[jamIndex].Envelope.Payload);
        Assert.Equal((LogId.From("log_01"), due, HostStageSevenAutoRouteOutcome.Blocked), (route.LogId, route.AttemptedAt, route.Outcome));
        Assert.Equal((LogId.From("log_01"), JamCause.INTAKE_AUTOFEED_BLOCKED, due), (jam.LogId, jam.Cause, jam.EnteredAt));
        Assert.Equal((execution.StageSix.IntakeAutoFeedJamStep.BeforeShiftState.StateVersion, execution.StageSix.IntakeAutoFeedJamStep.AfterShiftState.StateVersion), (jam.PriorStateVersion, jam.CurrentStateVersion));
        Assert.DoesNotContain(published.Publications, publication => publication.Envelope.EventType is { Value: { } type } && type.Contains("Movement", StringComparison.Ordinal));
    }

    [Fact]
    public void Public_auto_route_noop_results_are_typed_but_cannot_arise_after_the_closed_stage_three_to_five_owner_chain()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var scheduled = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, Fx.Shift.Scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(scheduled.State, ServerTick.Zero));
        var started = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admitted.State, admitted, Learning));
        var expired = Assert.IsType<IntakeDeadlineExpired>(new IntakeDeadlineExpirationService().Expire(started.State, started.Deadline.DueAt));
        var route = new DefaultIntakeAutoRouteService();

        var missing = Assert.IsType<DefaultIntakeAutoRouteOwnerMissing>(route.Attempt(
            expired.State, new DefaultAutoRouteRequired(LogId.From("missing_owner"), started.Deadline.DueAt), started.Deadline.DueAt));
        var moved = RuntimeFixture.MoveHost(expired.State, "log_01", LogState.AT_PROCEDURE);
        var inapplicable = Assert.IsType<DefaultIntakeAutoRouteNoLongerApplicable>(route.Attempt(moved, expired.FollowUp, started.Deadline.DueAt));

        Assert.Equal(LogId.From("missing_owner"), missing.LogId);
        Assert.Equal(LogId.From("log_01"), inapplicable.LogId);
        Assert.Equal(LogState.AT_INTAKE, expired.State.TryGetLog(LogId.From("log_01"), out var owner) ? owner.State : default);
        // Stage five invokes its route immediately from this exact expiry state, before a mutation could yield either no-op source state.
        _ = Assert.IsType<DefaultIntakeAutoRouteApplied>(route.Attempt(expired.State, expired.FollowUp, started.Deadline.DueAt));
    }

    [Fact]
    public void Feed_gate_placement_precedes_feed_gate_jam_with_exact_payload_evidence_and_no_movement_noise_event()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var intent = new IntentEnvelope(
            state.ShiftId, IntentId.From("gate_jam_early"), ActorId.From("hint"), FeedPlanningTargets.FeedGate,
            FeedPlanningIntentActions.RequestEarlyFeed, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var early = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(state, intent, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Shift.Scheduler));
        var due = early.Schedule.DueAt;
        var progress = AdvanceActiveCheckpointTo(early.State, QuotaRuntimeState.Create(Fx.Shift), due);
        var execution = BuildExecutionFrom(
            early.State, QuotaRuntimeState.Create(Fx.Shift), MovementNoiseRuntimeState.Create(state.ShiftId), LineNoiseRuntimeState.Create(state.ShiftId),
            progress.Progression, progress.Lifecycle, due);
        var journal = JournalAtState(early.State, ServerTick.Zero);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(execution, journal, EventIds(3), due));

        Assert.Equal([HostStageSevenEventTypes.LogPlacedAtFeedGate, HostStageSevenEventTypes.LineJammed], published.Publications.Take(2).Select(publication => publication.Envelope.EventType));
        var placement = Assert.IsType<HostStageSevenLogTransitionPayload>(published.Publications[0].Envelope.Payload);
        var jam = Assert.IsType<HostStageSevenLineJamPayload>(published.Publications[1].Envelope.Payload);
        Assert.Equal((early.Schedule.LogId, LogState.SCHEDULED, LogState.AT_FEED_GATE), (placement.LogId, placement.FromState, placement.ToState));
        Assert.Equal((early.Schedule.LogId, JamCause.FEED_GATE_BLOCKED, due), (jam.LogId, jam.Cause, jam.EnteredAt));
        Assert.DoesNotContain(published.Publications, publication => publication.Envelope.EventType is { Value: { } type } && type.Contains("Movement", StringComparison.Ordinal));
    }

    [Fact]
    public void Mixed_stage_two_batch_preserves_receipt_order_and_exact_later_versions()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var manual = new IntentEnvelope(
            state.ShiftId, IntentId.From("mixed_manual"), ActorId.From("hint"), TargetId.From("log_01"), LogIntentActions.RouteToProcedure,
            state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var early = new IntentEnvelope(
            state.ShiftId, IntentId.From("mixed_early"), ActorId.From("hint"), FeedPlanningTargets.FeedGate, FeedPlanningIntentActions.RequestEarlyFeed,
            state.StateVersion.Next(), ServerTick.Zero, NoIntentParameters.Instance);
        var receipts = ImmutableArray.Create(
            new AuthoritativeAcceptedIntent(manual, RuntimeFixture.BoundActor, ServerTick.Zero, ServerReceiveSequence.Zero),
            new AuthoritativeAcceptedIntent(early, RuntimeFixture.BoundActor, ServerTick.Zero, ServerReceiveSequence.From(1)));
        var execution = BuildExecutionFrom(
            state, QuotaRuntimeState.Create(Fx.Shift), MovementNoiseRuntimeState.Create(state.ShiftId), LineNoiseRuntimeState.Create(state.ShiftId),
            HostTickProgressionEvidence.Create(state.ShiftId), ShiftLifecycleRuntimeState.Create(Fx.Shift, LearningId), ServerTick.Zero, receipts);
        var journal = JournalAtState(state, ServerTick.Zero);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(execution, journal, EventIds(4), ServerTick.Zero));

        var stageTwo = published.Publications.Where(publication => publication.Envelope.CausedByIntentId is not null).Take(3).ToArray();
        Assert.Equal([HostStageSevenEventTypes.LogRouted, HostStageSevenEventTypes.EarlyFeedRequested, HostStageSevenEventTypes.FeedScheduled], stageTwo.Select(publication => publication.Envelope.EventType));
        Assert.Equal([manual.IntentId, early.IntentId, early.IntentId], stageTwo.Select(publication => publication.Envelope.CausedByIntentId));
        Assert.Equal([state.StateVersion.Next(), state.StateVersion.Next(), state.StateVersion.Next().Next()], stageTwo.Select(publication => publication.Envelope.StateVersionAfter));
    }

    [Fact]
    public void Line_noise_no_change_does_not_fabricate_an_observational_event()
    {
        var (_, zero, journal, executor) = PublishPrecedingEventfulTickThenBuildZeroEventTick();

        var result = Assert.IsType<HostStageSevenNoNewPublication>(executor.Execute(
            zero.StageOne, zero.StageTwo, zero.StageThree, zero.StageFour, zero.StageFive, zero.StageSix,
            journal, ImmutableArray<EventId>.Empty, ServerTick.From(1)));

        Assert.Empty(result.AssignedEventIds);
        Assert.DoesNotContain(journal.Events, envelope => envelope.EventType == HostStageSevenEventTypes.LineNoiseChanged && envelope.ServerTick == ServerTick.From(1));
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
    public void Already_published_eventful_tail_at_sequence_capacity_replays_without_attempting_a_new_append()
    {
        var execution = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var ids = EventIds(4);
        var reference = new InMemoryEventJournal(execution.StageOne.InitialState.ShiftId);
        _ = Assert.IsType<HostStageSevenPublished>(Execute(execution, reference, ids, ServerTick.Zero));
        var tail = reference.Events.Select((envelope, index) => envelope with { Sequence = EventSequence.From(long.MaxValue - reference.Events.Count + index + 1) }).ToImmutableArray();
        var exhausted = new ReplayTailJournal(execution.StageOne.InitialState.ShiftId, tail);

        var replayed = Assert.IsType<HostStageSevenAlreadyPublished>(Execute(execution, exhausted, ids, ServerTick.Zero));

        Assert.Equal(EventSequence.From(long.MaxValue), exhausted.LastSequence);
        Assert.Equal(0, exhausted.AppendAttempts);
        Assert.Equal(tail, exhausted.Events);
        Assert.Equal(replayed.BeforeCursor.Count, replayed.AfterCursor.Count);
    }

    [Fact]
    public void Contradictory_replay_event_identity_fails_closed_without_changing_the_journal()
    {
        var execution = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var journal = new InMemoryEventJournal(execution.StageOne.InitialState.ShiftId);
        var ids = EventIds(4);
        var executor = new HostStageSevenEventExecutor();
        _ = executor.Execute(execution.StageOne, execution.StageTwo, execution.StageThree, execution.StageFour, execution.StageFive, execution.StageSix, journal, ids, ServerTick.Zero);
        var advanced = Assert.IsType<HostTickCheckpointAdvanced>(execution.StageSix.Checkpoint);
        var replay = new HostStageSixDerivedExecutor().Execute(
            execution.StageOne, execution.StageTwo, execution.StageThree, execution.StageFour, execution.StageFive,
            execution.StageSix.InitialMovementNoise, execution.StageSix.InitialLineNoise,
            advanced.Progression, advanced.Receipt.Lifecycle, ImmutableHashSet<ItemId>.Empty,
            ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies);
        var before = Snapshot(journal);

        Assert.Throws<InvalidOperationException>(() => new HostStageSevenEventExecutor().Execute(
            execution.StageOne, execution.StageTwo, execution.StageThree, execution.StageFour, execution.StageFive, replay,
            journal, ImmutableArray.Create(EventId.From("wrong_1"), EventId.From("wrong_2"), EventId.From("wrong_3"), EventId.From("wrong_4")), ServerTick.Zero));

        AssertJournalUnchanged(before, journal);
    }

    [Fact]
    public void Replay_rejects_a_tail_with_matching_envelope_metadata_but_contradictory_payload_evidence()
    {
        var execution = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var ids = EventIds(4);
        var referenceJournal = new InMemoryEventJournal(execution.StageOne.InitialState.ShiftId);
        _ = new HostStageSevenEventExecutor().Execute(
            execution.StageOne, execution.StageTwo, execution.StageThree, execution.StageFour, execution.StageFive, execution.StageSix,
            referenceJournal, ids, ServerTick.Zero);
        var adversarialJournal = new InMemoryEventJournal(execution.StageOne.InitialState.ShiftId);
        for (var index = 0; index < referenceJournal.Events.Count; index++)
        {
            var envelope = referenceJournal.Events[index];
            adversarialJournal.Append(index == 0 ? envelope with { Payload = TestPayload.Instance } : envelope);
        }

        var advanced = Assert.IsType<HostTickCheckpointAdvanced>(execution.StageSix.Checkpoint);
        var replay = new HostStageSixDerivedExecutor().Execute(
            execution.StageOne, execution.StageTwo, execution.StageThree, execution.StageFour, execution.StageFive,
            execution.StageSix.InitialMovementNoise, execution.StageSix.InitialLineNoise,
            advanced.Progression, advanced.Receipt.Lifecycle, ImmutableHashSet<ItemId>.Empty,
            ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies);
        var before = Snapshot(adversarialJournal);

        Assert.Throws<InvalidOperationException>(() => new HostStageSevenEventExecutor().Execute(
            execution.StageOne, execution.StageTwo, execution.StageThree, execution.StageFour, execution.StageFive, replay,
            adversarialJournal, ids, ServerTick.Zero));

        AssertJournalUnchanged(before, adversarialJournal);
    }

    [Fact]
    public void Equivalent_traces_have_equal_semantic_projections_and_changing_only_event_ids_changes_only_identity()
    {
        var left = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var right = BuildExecution(ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var leftJournal = new InMemoryEventJournal(left.StageOne.InitialState.ShiftId);
        var rightJournal = new InMemoryEventJournal(right.StageOne.InitialState.ShiftId);

        var leftPublished = Assert.IsType<HostStageSevenPublished>(Execute(left, leftJournal, EventIds(4), ServerTick.Zero));
        var rightPublished = Assert.IsType<HostStageSevenPublished>(Execute(right, rightJournal,
            ImmutableArray.Create(EventId.From("alternate_1"), EventId.From("alternate_2"), EventId.From("alternate_3"), EventId.From("alternate_4")), ServerTick.Zero));

        Assert.Equal(leftPublished.Publications.Length, rightPublished.Publications.Length);
        foreach (var pair in leftPublished.Publications.Zip(rightPublished.Publications))
        {
            Assert.NotEqual(pair.First.Envelope.EventId, pair.Second.Envelope.EventId);
            Assert.True(SamePublicationSemantics(pair.First, pair.Second));
        }

        var initialSchedule = leftPublished.Publications.Single(publication => publication.Envelope.EventType == HostStageSevenEventTypes.FeedScheduled);
        var initial = RuntimeFixture.CreateInitialState();
        var earlyIntent = new IntentEnvelope(
            initial.ShiftId, IntentId.From("different_schedule"), ActorId.From("hint"), FeedPlanningTargets.FeedGate,
            FeedPlanningIntentActions.RequestEarlyFeed, initial.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var changedTrace = BuildExecution(ImmutableArray.Create(new AuthoritativeAcceptedIntent(earlyIntent, RuntimeFixture.BoundActor, ServerTick.Zero, ServerReceiveSequence.Zero)));
        var changedPublished = Assert.IsType<HostStageSevenPublished>(Execute(changedTrace, new InMemoryEventJournal(initial.ShiftId), EventIds(2), ServerTick.Zero));
        var changedSchedule = changedPublished.Publications.Single(publication => publication.Envelope.EventType == HostStageSevenEventTypes.FeedScheduled);
        Assert.False(SamePayloadSemantics(initialSchedule.Envelope.Payload, changedSchedule.Envelope.Payload));
    }

    [Fact]
    public void Eventful_tick_after_a_zero_event_tick_continues_exact_sequence_without_a_marker()
    {
        var (_, zero, journal, executor) = PublishPrecedingEventfulTickThenBuildZeroEventTick();
        _ = Assert.IsType<HostStageSevenNoNewPublication>(Execute(zero, journal, ImmutableArray<EventId>.Empty, ServerTick.From(1)));
        var checkpoint = Assert.IsType<HostTickCheckpointAdvanced>(zero.StageSix.Checkpoint);
        var state = zero.StageSix.FinalShiftState;
        var early = new IntentEnvelope(
            state.ShiftId, IntentId.From("later_early"), ActorId.From("hint"), FeedPlanningTargets.FeedGate,
            FeedPlanningIntentActions.RequestEarlyFeed, state.StateVersion, ServerTick.From(2), NoIntentParameters.Instance);
        var later = BuildExecutionFrom(
            state, zero.StageFour.FinalQuotaState, zero.StageSix.FinalMovementNoise, zero.StageSix.FinalLineNoise,
            checkpoint.Progression, checkpoint.Receipt.Lifecycle, ServerTick.From(2),
            ImmutableArray.Create(new AuthoritativeAcceptedIntent(early, RuntimeFixture.BoundActor, ServerTick.From(2), ServerReceiveSequence.Zero)));

        var published = Assert.IsType<HostStageSevenPublished>(Execute(later, journal, EventIds(3), ServerTick.From(2)));

        Assert.Equal([HostStageSevenEventTypes.EarlyFeedRequested, HostStageSevenEventTypes.FeedScheduled, HostStageSevenEventTypes.LineNoiseChanged], published.Publications.Select(publication => publication.Envelope.EventType));
        Assert.Equal([5L, 6L, 7L], published.Publications.Select(publication => publication.Envelope.Sequence.Value));
        Assert.Equal(ServerTick.From(2), journal.LastTick);
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

    private static ShiftRuntimeState WriteOff(ShiftRuntimeState state, string logId)
    {
        state = RuntimeFixture.MoveToIntake(state, logId);
        return RuntimeFixture.MoveHost(state, logId, LogState.HELD_WRITTEN_OFF);
    }

    private static void AssertSpeciesValues(ImmutableDictionary<SpeciesId, int> expected, ImmutableDictionary<SpeciesId, int> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var entry in expected)
        {
            Assert.True(actual.TryGetValue(entry.Key, out var value));
            Assert.Equal(entry.Value, value);
        }
    }

    private static bool SamePublicationSemantics(HostStageSevenPublication left, HostStageSevenPublication right) =>
        left.Envelope.EventType == right.Envelope.EventType &&
        left.Envelope.CausedByIntentId == right.Envelope.CausedByIntentId &&
        left.Envelope.ServerTick == right.Envelope.ServerTick &&
        left.Envelope.StateVersionAfter == right.Envelope.StateVersionAfter &&
        left.Kind == right.Kind &&
        SamePayloadSemantics(left.Envelope.Payload, right.Envelope.Payload);

    private static bool SamePayloadSemantics(IDomainEventPayload left, IDomainEventPayload right) =>
        (left, right) switch
        {
            (HostStageSevenLogTransitionPayload a, HostStageSevenLogTransitionPayload b) =>
                SameVersions(a, b) && a.LogId == b.LogId && a.FromState == b.FromState && a.ToState == b.ToState,
            (HostStageSevenFeedSchedulePayload a, HostStageSevenFeedSchedulePayload b) =>
                SameVersions(a, b) && a.LogId == b.LogId && a.Kind == b.Kind && a.ScheduledAt == b.ScheduledAt && a.DueAt == b.DueAt && a.Delay == b.Delay && a.CausedByIntentId == b.CausedByIntentId,
            (HostStageSevenIntakeDeadlinePayload a, HostStageSevenIntakeDeadlinePayload b) =>
                SameVersions(a, b) && a.LogId == b.LogId && a.StartedAt == b.StartedAt && a.DueAt == b.DueAt && a.Duration == b.Duration && a.OccurredAt == b.OccurredAt,
            (HostStageSevenAutoRoutePayload a, HostStageSevenAutoRoutePayload b) =>
                SameVersions(a, b) && a.LogId == b.LogId && a.AttemptedAt == b.AttemptedAt && a.Outcome == b.Outcome && a.Source == b.Source && a.Destination == b.Destination && a.BlockReason == b.BlockReason && a.FollowUp == b.FollowUp,
            (HostStageSevenProcedurePayload a, HostStageSevenProcedurePayload b) => SameVersions(a, b) && a.Descriptor == b.Descriptor,
            (HostStageSevenConfirmationPayload a, HostStageSevenConfirmationPayload b) => SameVersions(a, b) && a.Result == b.Result,
            (HostStageSevenContainmentPayload a, HostStageSevenContainmentPayload b) => SameVersions(a, b) && a.PriorContainment == b.PriorContainment && a.CurrentContainment == b.CurrentContainment && a.Ritual == b.Ritual && a.Incident == b.Incident,
            (HostStageSevenRepairPayload a, HostStageSevenRepairPayload b) => SameVersions(a, b) && a.PriorLine == b.PriorLine && a.CurrentLine == b.CurrentLine && a.PendingTransition == b.PendingTransition,
            (HostStageSevenSawStartedPayload a, HostStageSevenSawStartedPayload b) => SameVersions(a, b) && a.Cycle == b.Cycle,
            (HostStageSevenSawCompletedPayload a, HostStageSevenSawCompletedPayload b) =>
                SameVersions(a, b) && a.Cycle == b.Cycle && a.Resolution == b.Resolution && a.CompletedAt == b.CompletedAt && a.QuotaSettlement == b.QuotaSettlement && a.QuotaApplicationLogId == b.QuotaApplicationLogId && a.QuotaApplicationOutcome == b.QuotaApplicationOutcome && a.AcceptedQuotaSettlement == b.AcceptedQuotaSettlement && a.DuplicateQuotaSettlementLogId == b.DuplicateQuotaSettlementLogId,
            (HostStageSevenLineJamPayload a, HostStageSevenLineJamPayload b) => SameVersions(a, b) && a.LogId == b.LogId && a.Cause == b.Cause && a.EnteredAt == b.EnteredAt,
            (HostStageSevenLineNoisePayload a, HostStageSevenLineNoisePayload b) => SameVersions(a, b) && a.Change == b.Change,
            (HostStageSevenConfirmationConditionPayload a, HostStageSevenConfirmationConditionPayload b) => SameVersions(a, b) && a.Prior == b.Prior && a.Current == b.Current,
            (HostStageSevenShiftCompletedPayload a, HostStageSevenShiftCompletedPayload b) =>
                SameVersions(a, b) && a.CompletedAt == b.CompletedAt && a.HardDeadlineAt == b.HardDeadlineAt && a.Reason == b.Reason && a.AllLogsTerminal == b.AllLogsTerminal && a.HardDeadlineReached == b.HardDeadlineReached && a.ObjectivesSatisfied == b.ObjectivesSatisfied && a.ProcessedCount == b.ProcessedCount && a.WrittenOffCount == b.WrittenOffCount && a.TargetTotal == b.TargetTotal && a.TotalCreditedUnits == b.TotalCreditedUnits && a.MinimumCorrectlyProcessedAnomalies == b.MinimumCorrectlyProcessedAnomalies && a.CorrectlyProcessedAnomalies == b.CorrectlyProcessedAnomalies && SameSpeciesValues(a.TargetBySpecies, b.TargetBySpecies) && SameSpeciesValues(a.CreditedBySpecies, b.CreditedBySpecies),
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

    private static HostStageSevenEventExecution Execute(StageExecution execution, IEventJournal journal, ImmutableArray<EventId> eventIds, ServerTick tick) =>
        new HostStageSevenEventExecutor().Execute(
            execution.StageOne, execution.StageTwo, execution.StageThree, execution.StageFour, execution.StageFive, execution.StageSix,
            journal, eventIds, tick);

    private static InMemoryEventJournal JournalAtState(ShiftRuntimeState state, ServerTick tick)
    {
        var journal = new InMemoryEventJournal(state.ShiftId);
        for (var version = 1L; version <= state.StateVersion.Value; version++)
        {
            journal.Append(new EventEnvelope
            {
                ShiftId = state.ShiftId,
                EventId = EventId.From($"history_{version}"),
                Sequence = EventSequence.From(version),
                ServerTick = tick,
                StateVersionAfter = StateVersion.From(version),
                EventType = EventTypeId.From("History"),
                Payload = TestPayload.Instance
            });
        }

        return journal;
    }

    private static (HostTickProgressionEvidence Progression, ShiftLifecycleRuntimeState Lifecycle) AdvanceActiveCheckpointTo(ShiftRuntimeState state, QuotaRuntimeState quota, ServerTick targetTick)
    {
        var service = new HostTickCompletionCheckpointService();
        var progression = HostTickProgressionEvidence.Create(state.ShiftId);
        var lifecycle = ShiftLifecycleRuntimeState.Create(Fx.Shift, LearningId);
        for (var value = 0L; value < targetTick.Value; value++)
        {
            var tick = ServerTick.From(value);
            var advanced = Assert.IsType<HostTickCheckpointAdvanced>(service.Complete(progression, lifecycle, state, quota, tick, Fx.Shift));
            progression = advanced.Progression;
            lifecycle = advanced.Receipt.Lifecycle;
        }

        return (progression, lifecycle);
    }

    private static JournalSnapshot Snapshot(IEventJournal journal) =>
        new(journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion, journal.Events.ToArray());

    private static void AssertJournalUnchanged(JournalSnapshot expected, IEventJournal actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(expected.LastSequence, actual.LastSequence);
        Assert.Equal(expected.LastTick, actual.LastTick);
        Assert.Equal(expected.LastStateVersion, actual.LastStateVersion);
        Assert.Equal(expected.Events, actual.Events);
    }

    private static (StageExecution First, StageExecution Next, InMemoryEventJournal Journal, HostStageSevenEventExecutor Executor) PublishPrecedingEventfulTickThenBuildZeroEventTick()
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
        return (first, next, journal, executor);
    }

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
        ImmutableArray<AuthoritativeAcceptedIntent>? receipts = null,
        ImmutableHashSet<ItemId>? activeTools = null)
    {
        var stageOne = new HostStageOneCompletionExecutor().Execute(initial, tick, Fx.Anomalies, Fx.Shift.Containment);
        var batch = AcceptedIntentTickBatchFactory.Create(initial.ShiftId, tick, receipts ?? ImmutableArray<AuthoritativeAcceptedIntent>.Empty);
        var stageTwo = new AcceptedIntentStageExecutor().Execute(stageOne.FinalState, batch, Fx.Shift.Scheduler, ImmutableHashSet<ItemId>.Empty, LineNoiseRuntimeState.Create(stageOne.FinalState.ShiftId), Fx.Anomalies);
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
            activeTools ?? ImmutableHashSet<ItemId>.Empty,
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

    private sealed record JournalSnapshot(int Count, EventSequence LastSequence, ServerTick LastTick, StateVersion LastStateVersion, EventEnvelope[] Events);

    private sealed class CursorJournal : IEventJournal
    {
        public CursorJournal(ShiftId shift, EventSequence lastSequence, ServerTick lastTick, StateVersion lastStateVersion)
        {
            Shift = shift;
            LastSequence = lastSequence;
            LastTick = lastTick;
            LastStateVersion = lastStateVersion;
        }

        public ShiftId Shift { get; }
        public EventSequence LastSequence { get; }
        public ServerTick LastTick { get; }
        public StateVersion LastStateVersion { get; }
        public int Count => 1;
        public IReadOnlyList<EventEnvelope> Events { get; } = Array.Empty<EventEnvelope>();
        public int AppendAttempts { get; private set; }

        public void Append(EventEnvelope envelope) => throw new InvalidOperationException("Preflight test journal must not append.");

        public JournalAppendOutcome TryAppend(EventEnvelope envelope)
        {
            AppendAttempts++;
            throw new InvalidOperationException("Preflight test journal must not append.");
        }
    }

    private sealed class ReplayTailJournal : IEventJournal
    {
        public ReplayTailJournal(ShiftId shift, ImmutableArray<EventEnvelope> events)
        {
            if (events.IsDefaultOrEmpty) throw new ArgumentException("Replay tail evidence must be populated.", nameof(events));
            Shift = shift;
            Events = events;
            LastSequence = events[^1].Sequence;
            LastTick = events[^1].ServerTick;
            LastStateVersion = events[^1].StateVersionAfter;
        }

        public ShiftId Shift { get; }
        public EventSequence LastSequence { get; }
        public ServerTick LastTick { get; }
        public StateVersion LastStateVersion { get; }
        public int Count => Events.Count;
        public IReadOnlyList<EventEnvelope> Events { get; }
        public int AppendAttempts { get; private set; }

        public void Append(EventEnvelope envelope) => throw new InvalidOperationException("An already-published replay must not append.");

        public JournalAppendOutcome TryAppend(EventEnvelope envelope)
        {
            AppendAttempts++;
            throw new InvalidOperationException("An already-published replay must not append.");
        }
    }

    private sealed class TestPayload : IDomainEventPayload
    {
        public static readonly TestPayload Instance = new();
        private TestPayload() { }
    }
}
