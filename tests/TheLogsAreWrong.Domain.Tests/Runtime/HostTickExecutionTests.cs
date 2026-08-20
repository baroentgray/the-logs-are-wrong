using System.Collections.Immutable;
using System.Reflection;
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

[Trait("Scope", "TLAW-037")]
public sealed class HostTickExecutionTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static readonly ProfileId LearningId = ProfileId.From("learning");

    [Fact]
    public void Public_boundary_accepts_only_separate_source_derived_host_evidence()
    {
        var execute = Assert.Single(
            typeof(HostTickExecutionService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => method.Name == "Execute");

        Assert.Equal(typeof(HostStageSevenEventExecution), execute.ReturnType);
        Assert.Equal(
            new[]
            {
                typeof(ShiftRuntimeState), typeof(QuotaRuntimeState), typeof(MovementNoiseRuntimeState),
                typeof(LineNoiseRuntimeState), typeof(HostTickProgressionEvidence), typeof(ShiftLifecycleRuntimeState),
                typeof(AcceptedIntentTickBatch), typeof(ImmutableHashSet<ItemId>), typeof(IEventJournal),
                typeof(ServerTick), typeof(SchedulerConfiguration),
                typeof(ShiftConfiguration), typeof(ContainmentConfiguration), typeof(AnomalyCatalog)
            },
            execute.GetParameters().Select(parameter => parameter.ParameterType));

        Assert.DoesNotContain(execute.GetParameters(), parameter =>
            parameter.ParameterType == typeof(ShiftProfile) ||
            parameter.ParameterType == typeof(IntentEnvelope) ||
            parameter.ParameterType == typeof(AuthoritativeAcceptedIntent) ||
            parameter.ParameterType == typeof(EventTypeId) ||
            parameter.ParameterType == typeof(EventSequence) ||
            parameter.ParameterType == typeof(HostStageOneCompletionExecution) ||
            parameter.ParameterType == typeof(AcceptedIntentStageExecution) ||
            parameter.ParameterType == typeof(HostStageThreeDeadlineExecution) ||
            parameter.ParameterType == typeof(HostStageFourSawExecution) ||
            parameter.ParameterType == typeof(HostStageFiveFeedExecution) ||
            parameter.ParameterType == typeof(HostStageSixDerivedExecution) ||
            parameter.ParameterType == typeof(object) ||
            parameter.ParameterType == typeof(bool) ||
            parameter.ParameterType == typeof(string) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
    }

    [Fact]
    public void Null_default_identity_and_selected_profile_preflight_failures_preserve_the_journal()
    {
        var inputs = CreateInputs(ServerTick.Zero);

        AssertPreStageFailure(inputs with { InitialShiftState = null! });
        AssertPreStageFailure(inputs with { InitialQuotaState = null! });
        AssertPreStageFailure(inputs with { InitialMovementNoise = null! });
        AssertPreStageFailure(inputs with { InitialLineNoise = null! });
        AssertPreStageFailure(inputs with { Progression = null! });
        AssertPreStageFailure(inputs with { Lifecycle = null! });
        AssertPreStageFailure(inputs with { AcceptedIntents = null! });
        AssertPreStageFailure(inputs with { ActiveTools = null! });
        AssertPreStageFailure(inputs with { Journal = null! });
        AssertPreStageFailure(inputs with { SchedulerConfiguration = null! });
        AssertPreStageFailure(inputs with { ShiftConfiguration = null! });
        AssertPreStageFailure(inputs with { ContainmentConfiguration = null! });
        AssertPreStageFailure(inputs with { AnomalyCatalog = null! });
        AssertPreStageFailure(inputs with { CurrentTick = default });
        AssertPreStageFailure(inputs with { ActiveTools = ImmutableHashSet.Create(default(ItemId)) });

        var configurationWithoutSelectedProfile = Fx.Shift with { Profiles = Fx.Shift.Profiles.Remove(LearningId) };
        AssertPreStageFailure(inputs with { ShiftConfiguration = configurationWithoutSelectedProfile });
    }

    [Fact]
    public void Cross_shift_and_batch_tick_evidence_fail_closed_before_stage_one()
    {
        var inputs = CreateInputs(ServerTick.Zero);
        var otherConfiguration = Fx.Shift with { ShiftId = ShiftId.From("TLAW_037_OTHER_SHIFT") };
        var otherShift = otherConfiguration.ShiftId;

        AssertPreStageFailure(inputs with { InitialShiftState = ShiftRuntimeState.Create(otherConfiguration) });
        AssertPreStageFailure(inputs with { InitialMovementNoise = MovementNoiseRuntimeState.Create(otherShift) });
        AssertPreStageFailure(inputs with { InitialLineNoise = LineNoiseRuntimeState.Create(otherShift) });
        AssertPreStageFailure(inputs with { Progression = HostTickProgressionEvidence.Create(otherShift) });
        AssertPreStageFailure(inputs with { Lifecycle = ShiftLifecycleRuntimeState.Create(otherConfiguration, LearningId) });
        AssertPreStageFailure(inputs with
        {
            AcceptedIntents = AcceptedIntentTickBatchFactory.Create(otherShift, ServerTick.Zero, ImmutableArray<AuthoritativeAcceptedIntent>.Empty)
        });
        AssertPreStageFailure(inputs with { ShiftConfiguration = otherConfiguration });
        AssertPreStageFailure(inputs with { Journal = new InMemoryEventJournal(otherShift) });
        AssertPreStageFailure(inputs with
        {
            AcceptedIntents = AcceptedIntentTickBatchFactory.Create(inputs.InitialShiftState.ShiftId, ServerTick.From(1), ImmutableArray<AuthoritativeAcceptedIntent>.Empty)
        });
    }

    [Fact]
    public void Composer_returns_stage_seven_result_with_the_exact_frozen_reference_chain_and_selected_profile()
    {
        var inputs = CreateInputs(ServerTick.Zero);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(inputs));

        Assert.Same(inputs.InitialShiftState, published.StageOne.InitialState);
        Assert.Same(published.StageOne.FinalState, published.StageTwo.InitialState);
        Assert.Same(published.StageTwo.FinalState, published.StageThree.InitialState);
        Assert.Same(published.StageThree.FinalState, published.StageFour.InitialShiftState);
        Assert.Same(inputs.InitialQuotaState, published.StageFour.InitialQuotaState);
        Assert.Same(published.StageFour.FinalShiftState, published.StageFive.InitialState);
        Assert.Same(published.StageOne.LineRepair.Result, published.StageFive.LineRepairSource);
        Assert.Same(published.StageThree.IntakeDeadline.Result, published.StageFive.IntakeExpirationSource);
        Assert.Same(published.StageFive.FinalState, published.StageSix.InitialShiftState);
        Assert.Same(published.StageFour.FinalQuotaState, published.StageSix.InitialQuotaState);
        Assert.Same(inputs.InitialMovementNoise, published.StageSix.InitialMovementNoise);
        Assert.Same(inputs.InitialLineNoise, published.StageSix.InitialLineNoise);
        Assert.Same(inputs.Progression, published.StageSix.Progression);
        Assert.Same(inputs.Lifecycle, published.StageSix.Lifecycle);
        Assert.Same(inputs.ActiveTools, published.StageSix.ActiveTools);
        Assert.Same(published.StageSix.FinalShiftState, published.FinalShiftState);
        Assert.Same(published.StageSix.FinalQuotaState, published.FinalQuotaState);
        Assert.Equal(inputs.CurrentTick, published.CurrentTick);

        var deadline = Assert.IsType<IntakeDeadlineStarted>(published.StageFive.OrdinaryDeadlineStart);
        Assert.Equal(Fx.Shift.Profiles[LearningId].IntakeTimeoutSeconds, deadline.Deadline.Duration.Value);
    }

    [Fact]
    public void Accepted_stage_two_route_precedes_same_tick_stage_three_deadline_work()
    {
        var (state, dueTick) = ActiveIntakeDeadline();
        var route = AcceptedRoute(state, dueTick, "log_01", LogIntentActions.RouteToProcedure);
        var inputs = CreateInputs(dueTick, initialShiftState: state, acceptedIntents: route, eventIds: ImmutableArray<EventId>.Empty);

        var blocked = Assert.IsType<HostStageSevenBlocked>(Execute(inputs));

        Assert.IsType<ManualLogIntentAccepted>(Assert.IsType<ManualRoutingIntentStageOutcome>(blocked.StageTwo.Steps[0].Outcome).Result);
        Assert.IsType<IntakeDeadlineNoActiveDeadline>(blocked.StageThree.IntakeDeadline.Result);
        Assert.Equal(LogState.AT_PROCEDURE, Log(blocked.StageThree.FinalState, "log_01").State);
        Assert.Equal(0, inputs.Journal.Count);
    }

    [Fact]
    public void Due_stage_one_repair_is_visible_to_stage_five_follow_up_and_stage_six_movement_in_the_same_tick()
    {
        var (state, dueTick) = RepairingFeedGateWithUnblockedIntake();
        var inputs = CreateInputs(dueTick, initialShiftState: state, eventIds: ImmutableArray<EventId>.Empty);

        var blocked = Assert.IsType<HostStageSevenBlocked>(Execute(inputs));

        var repair = Assert.IsType<LineRepairCompleted>(blocked.StageOne.LineRepair.Result);
        Assert.Same(repair, blocked.StageFive.LineRepairSource);
        Assert.IsType<RepairPendingTransitionExecuted>(blocked.StageFive.RepairExecution);
        Assert.Same(blocked.StageFive.FinalState, blocked.StageSix.InitialShiftState);
        Assert.NotEmpty(blocked.StageSix.MovementSteps);
        Assert.Equal(0, inputs.Journal.Count);
    }

    [Fact]
    public void Saw_quota_settlement_and_stage_five_jam_consequences_flow_to_stage_six_before_publication()
    {
        var (sawState, sawTick) = DueSawCycleWithSuccessor();
        var sawInputs = CreateInputs(sawTick, initialShiftState: sawState, eventIds: ImmutableArray<EventId>.Empty);
        var sawBlocked = Assert.IsType<HostStageSevenBlocked>(Execute(sawInputs));

        Assert.IsType<SawCycleCompleted>(sawBlocked.StageFour.Completion.Result);
        Assert.NotNull(sawBlocked.StageFour.Quota.Result);
        Assert.Same(sawBlocked.StageFour.FinalQuotaState, sawBlocked.StageSix.InitialQuotaState);
        Assert.Same(sawBlocked.StageFour.FinalQuotaState, sawBlocked.StageSix.CheckpointStep.PostStageQuota);

        var slowSawScheduler = Fx.Shift.Scheduler with { SawCycleSeconds = 200 };
        var (jammedState, jamTick) = ExpiringDeadlineWithBlockedSawQueue(slowSawScheduler);
        var jamInputs = CreateInputs(
            jamTick,
            initialShiftState: jammedState,
            eventIds: ImmutableArray<EventId>.Empty,
            schedulerConfiguration: slowSawScheduler);
        var jamBlocked = Assert.IsType<HostStageSevenBlocked>(Execute(jamInputs));

        Assert.IsType<IntakeDeadlineExpired>(jamBlocked.StageThree.IntakeDeadline.Result);
        Assert.IsType<DefaultIntakeAutoRouteBlocked>(jamBlocked.StageFive.DefaultRoute);
        Assert.IsType<IntakeAutoFeedJamEntered>(jamBlocked.StageSix.IntakeAutoFeedJam);
        Assert.Equal(0, jamInputs.Journal.Count);
    }

    [Fact]
    public void Checkpoint_is_completed_before_stage_seven_publishes_and_advances_the_journal_cursor()
    {
        var inputs = CreateInputs(ServerTick.Zero);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(inputs));

        Assert.IsType<HostTickCheckpointAdvanced>(published.Checkpoint);
        Assert.Equal(0, published.BeforeCursor.Count);
        Assert.Equal(published.Publications.Length, published.AfterCursor.Count);
        Assert.Equal(
            [
                HostStageSevenEventTypes.FeedScheduled,
                HostStageSevenEventTypes.LogAdmittedToIntake,
                HostStageSevenEventTypes.IntakeDeadlineStarted,
                HostStageSevenEventTypes.LineNoiseChanged
            ],
            published.Publications.Select(publication => publication.Envelope.EventType));
    }

    [Fact]
    public void Backward_checkpoint_rejection_returns_the_exact_blocked_result_without_journal_append()
    {
        var state = RuntimeFixture.CreateInitialState();
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var progress = AdvanceActiveCheckpointTo(state, quota, ServerTick.From(2));
        var inputs = CreateInputs(
            ServerTick.Zero,
            initialShiftState: state,
            initialQuotaState: quota,
            progression: progress.Progression,
            lifecycle: progress.Lifecycle,
            eventIds: ImmutableArray<EventId>.Empty);
        var before = Snapshot(inputs.Journal);

        var blocked = Assert.IsType<HostStageSevenBlocked>(Execute(inputs));

        var rejection = Assert.IsType<HostTickCheckpointRejected>(blocked.CheckpointRejection);
        Assert.Equal(HostTickCheckpointRejectionReason.BackwardTick, rejection.Reason);
        Assert.Equal(inputs.CurrentTick, rejection.RequestedTick);
        AssertJournalUnchanged(before, inputs.Journal);
    }

    [Fact]
    public void Later_tick_after_shift_completed_checkpoint_returns_the_exact_blocked_result_without_journal_append()
    {
        var terminal = AllLogsWrittenOff();
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var initial = CreateInputs(
            ServerTick.Zero,
            initialShiftState: terminal,
            initialQuotaState: quota,
            journal: JournalAtState(terminal, ServerTick.Zero),
            eventIds: EventIds("complete", 2));

        var completed = Assert.IsType<HostStageSevenPublished>(Execute(initial));
        var checkpoint = Assert.IsType<HostTickCheckpointAdvanced>(completed.Checkpoint);
        Assert.True(checkpoint.Receipt.ShiftCompleted);
        Assert.Contains(completed.Publications, publication => publication.Envelope.EventType == HostStageSevenEventTypes.ShiftCompleted);

        var later = CreateInputs(
            ServerTick.From(1),
            initialShiftState: completed.FinalShiftState,
            initialQuotaState: completed.FinalQuotaState,
            initialMovementNoise: completed.StageSix.FinalMovementNoise,
            initialLineNoise: completed.FinalLineNoise,
            progression: checkpoint.Progression,
            lifecycle: checkpoint.Receipt.Lifecycle,
            journal: initial.Journal,
            eventIds: ImmutableArray<EventId>.Empty);
        var before = Snapshot(later.Journal);

        var blocked = Assert.IsType<HostStageSevenBlocked>(Execute(later));

        var rejection = Assert.IsType<HostTickCheckpointRejected>(blocked.CheckpointRejection);
        Assert.Equal(HostTickCheckpointRejectionReason.ShiftCompleted, rejection.Reason);
        AssertJournalUnchanged(before, later.Journal);
    }

    [Fact]
    public void Blocked_no_new_and_already_published_results_retain_existing_stage_seven_taxonomy_without_extra_append()
    {
        var blockedInputs = CreateInputs(ServerTick.From(1), eventIds: ImmutableArray<EventId>.Empty);
        var blocked = Assert.IsType<HostStageSevenBlocked>(Execute(blockedInputs));
        Assert.Equal(0, blockedInputs.Journal.Count);

        var initial = CreateInputs(ServerTick.Zero);
        var first = Assert.IsType<HostStageSevenPublished>(Execute(initial));
        var advanced = Assert.IsType<HostTickCheckpointAdvanced>(first.Checkpoint);
        var next = CreateInputs(
            ServerTick.From(1),
            initialShiftState: first.FinalShiftState,
            initialQuotaState: first.FinalQuotaState,
            initialMovementNoise: first.StageSix.FinalMovementNoise,
            initialLineNoise: first.FinalLineNoise,
            progression: advanced.Progression,
            lifecycle: advanced.Receipt.Lifecycle,
            journal: initial.Journal,
            eventIds: ImmutableArray<EventId>.Empty);
        var beforeNoNew = Snapshot(next.Journal);
        var noNew = Assert.IsType<HostStageSevenNoNewPublication>(Execute(next));
        AssertJournalUnchanged(beforeNoNew, next.Journal);
        Assert.Equal(noNew.BeforeCursor.Count, noNew.AfterCursor.Count);

        var replay = CreateInputs(ServerTick.Zero, journal: initial.Journal, eventIds: initial.EventIds);
        var beforeReplay = Snapshot(replay.Journal);
        _ = Assert.IsType<HostStageSevenAlreadyPublished>(Execute(replay));
        AssertJournalUnchanged(beforeReplay, replay.Journal);
    }

    [Fact]
    public void Mixed_reachable_stage_publications_remain_in_frozen_host_order_with_exact_causation_and_payload_sources()
    {
        var (state, tick) = MixedStagePublicationState();
        var quota = QuotaRuntimeState.Create(Fx.Shift);
        var progress = AdvanceActiveCheckpointTo(state, quota, tick);
        var accepted = AcceptedRoute(state, tick, "log_01", LogIntentActions.WriteOff);
        var inputs = CreateInputs(
            tick,
            initialShiftState: state,
            initialQuotaState: quota,
            progression: progress.Progression,
            lifecycle: progress.Lifecycle,
            acceptedIntents: accepted,
            journal: JournalAtState(state, ServerTick.Zero),
            eventIds: EventIds("mixed", 5));

        var published = Assert.IsType<HostStageSevenPublished>(Execute(inputs));

        Assert.Equal(
            [
                HostStageSevenEventTypes.LogWrittenOff,
                HostStageSevenEventTypes.ContainmentStateChanged,
                HostStageSevenEventTypes.SawCycleCompleted,
                HostStageSevenEventTypes.FeedScheduled,
                HostStageSevenEventTypes.LineNoiseChanged
            ],
            published.Publications.Select(publication => publication.Envelope.EventType));

        var writtenOff = Assert.IsType<ManualLogIntentAccepted>(Assert.IsType<ManualRoutingIntentStageOutcome>(published.StageTwo.Steps[0].Outcome).Result);
        var writtenOffPublication = published.Publications[0].Envelope;
        var writtenOffPayload = Assert.IsType<HostStageSevenLogTransitionPayload>(writtenOffPublication.Payload);
        Assert.Equal(published.StageTwo.Steps[0].Receipt.Envelope.IntentId, writtenOffPublication.CausedByIntentId);
        Assert.Equal((writtenOff.Transition.LogId, writtenOff.Transition.FromState, writtenOff.Transition.ToState, writtenOff.Transition.PriorStateVersion, writtenOff.Transition.CurrentStateVersion),
            (writtenOffPayload.LogId, writtenOffPayload.FromState, writtenOffPayload.ToState, writtenOffPayload.PriorStateVersion, writtenOffPayload.CurrentStateVersion));

        var containment = Assert.IsType<HostStageSevenContainmentPayload>(published.Publications[1].Envelope.Payload);
        Assert.Null(published.Publications[1].Envelope.CausedByIntentId);
        Assert.Equal((published.StageThree.Containment.BeforeState.Containment, published.StageThree.Containment.AfterState.Containment),
            (containment.PriorContainment, containment.CurrentContainment));

        var saw = Assert.IsType<SawCycleCompleted>(published.StageFour.Completion.Result);
        var sawPayload = Assert.IsType<HostStageSevenSawCompletedPayload>(published.Publications[2].Envelope.Payload);
        Assert.Equal((saw.Cycle, saw.CompletedAt, saw.Resolution), (sawPayload.Cycle, sawPayload.CompletedAt, sawPayload.Resolution));

        var scheduled = Assert.IsType<NormalFeedScheduled>(published.StageFive.GenericNormalPlanning);
        var schedulePayload = Assert.IsType<HostStageSevenFeedSchedulePayload>(published.Publications[3].Envelope.Payload);
        Assert.Null(published.Publications[3].Envelope.CausedByIntentId);
        Assert.Equal((scheduled.Schedule.LogId, scheduled.Schedule.Kind, scheduled.Schedule.ScheduledAt, scheduled.Schedule.DueAt, scheduled.Schedule.Delay),
            (schedulePayload.LogId, schedulePayload.Kind, schedulePayload.ScheduledAt, schedulePayload.DueAt, schedulePayload.Delay));

        var noise = Assert.IsType<LineNoiseEvaluatedWithChange>(published.StageSix.LineNoiseEvaluation);
        var noisePayload = Assert.IsType<HostStageSevenLineNoisePayload>(published.Publications[4].Envelope.Payload);
        Assert.Equal(noise.Change, noisePayload.Change);
    }

    [Fact]
    public void Stage_five_admitted_feed_evidence_is_applied_to_stage_six_movement_noise_before_line_noise()
    {
        var published = Assert.IsType<HostStageSevenPublished>(Execute(CreateInputs(ServerTick.Zero)));

        var feed = Assert.IsType<FeedDueResolved>(published.StageFive.FeedDue);
        Assert.Equal(FeedDueDisposition.AdmittedToIntake, feed.Disposition);
        var movementStep = Assert.Single(published.StageSix.MovementSteps);
        var movement = Assert.IsType<MovementNoiseApplied>(movementStep.Result).State.LastAcceptedMovement!;
        Assert.Equal(MovementNoiseAcceptedSource.FeedDueResolved, movement.Source);
        Assert.Equal((feed.ConsumedSchedule.LogId, LogState.SCHEDULED, LogState.AT_INTAKE, feed.PriorStateVersion, feed.CurrentStateVersion),
            (movement.LogId, movement.SourceState, movement.DestinationState, movement.PriorStateVersion, movement.CurrentStateVersion));
        Assert.Same(published.StageSix.FinalMovementNoise, published.StageSix.LineNoiseStep.MovementNoiseState);
    }

    [Fact]
    public void Composer_preserves_all_immutable_caller_inputs_and_mutates_only_the_supplied_journal_on_publication()
    {
        var inputs = CreateInputs(ServerTick.Zero);
        var initialState = inputs.InitialShiftState;
        var initialQuota = inputs.InitialQuotaState;
        var initialMovement = inputs.InitialMovementNoise;
        var initialLineNoise = inputs.InitialLineNoise;
        var progression = inputs.Progression;
        var lifecycle = inputs.Lifecycle;
        var batch = inputs.AcceptedIntents;
        var activeTools = inputs.ActiveTools;
        var scheduler = inputs.SchedulerConfiguration;
        var shift = inputs.ShiftConfiguration;
        var containment = inputs.ContainmentConfiguration;
        var anomalies = inputs.AnomalyCatalog;
        var journalBefore = Snapshot(inputs.Journal);

        var published = Assert.IsType<HostStageSevenPublished>(Execute(inputs));

        Assert.Same(initialState, inputs.InitialShiftState);
        Assert.Same(initialQuota, inputs.InitialQuotaState);
        Assert.Same(initialMovement, inputs.InitialMovementNoise);
        Assert.Same(initialLineNoise, inputs.InitialLineNoise);
        Assert.Same(progression, inputs.Progression);
        Assert.Same(lifecycle, inputs.Lifecycle);
        Assert.Same(batch, inputs.AcceptedIntents);
        Assert.Same(activeTools, inputs.ActiveTools);
        Assert.Same(scheduler, inputs.SchedulerConfiguration);
        Assert.Same(shift, inputs.ShiftConfiguration);
        Assert.Same(containment, inputs.ContainmentConfiguration);
        Assert.Same(anomalies, inputs.AnomalyCatalog);
        Assert.Same(initialState, published.StageOne.InitialState);
        Assert.Same(initialQuota, published.StageFour.InitialQuotaState);
        Assert.Same(initialMovement, published.StageSix.InitialMovementNoise);
        Assert.Same(initialLineNoise, published.StageSix.InitialLineNoise);
        Assert.Same(progression, published.StageSix.Progression);
        Assert.Same(lifecycle, published.StageSix.Lifecycle);
        Assert.Same(batch, published.StageTwo.Batch);
        Assert.Same(activeTools, published.StageSix.ActiveTools);
        Assert.Equal(0, journalBefore.Count);
        Assert.Equal(published.Publications.Length, inputs.Journal.Count);
    }

    [Fact]
    public void Equivalent_inputs_are_independently_deterministic_with_host_owned_event_identity()
    {
        var first = CreateInputs(ServerTick.Zero, eventIds: EventIds("first", 4));
        var second = CreateInputs(ServerTick.Zero, eventIds: EventIds("first", 4));
        var changedIds = CreateInputs(ServerTick.Zero, eventIds: EventIds("second", 4));

        var firstResult = Assert.IsType<HostStageSevenPublished>(Execute(first));
        var secondResult = Assert.IsType<HostStageSevenPublished>(Execute(second));
        var changedResult = Assert.IsType<HostStageSevenPublished>(Execute(changedIds));

        Assert.True(firstResult.FinalShiftState.ValueEquals(secondResult.FinalShiftState));
        Assert.True(firstResult.FinalQuotaState.ValueEquals(secondResult.FinalQuotaState));
        AssertEquivalentExecution(firstResult, secondResult);
        AssertJournalSemanticsEqual(first.Journal.Events, second.Journal.Events);
        AssertJournalSemanticsEqual(first.Journal.Events, changedIds.Journal.Events);
        Assert.All(first.Journal.Events.Zip(changedIds.Journal.Events), pair => Assert.Equal(pair.First.EventId, pair.Second.EventId));
        Assert.True(firstResult.FinalShiftState.ValueEquals(changedResult.FinalShiftState));
        Assert.True(firstResult.FinalQuotaState.ValueEquals(changedResult.FinalQuotaState));
        AssertEquivalentExecution(firstResult, changedResult);
        Assert.Same(first.InitialShiftState, firstResult.StageOne.InitialState);
        Assert.Same(first.AcceptedIntents, firstResult.StageTwo.Batch);
        Assert.Same(first.ActiveTools, firstResult.StageSix.ActiveTools);
    }

    private static HostStageSevenEventExecution Execute(ComposerInputs inputs) =>
        new HostTickExecutionService().Execute(
            inputs.InitialShiftState,
            inputs.InitialQuotaState,
            inputs.InitialMovementNoise,
            inputs.InitialLineNoise,
            inputs.Progression,
            inputs.Lifecycle,
            inputs.AcceptedIntents,
            inputs.ActiveTools,
            inputs.Journal,
            inputs.CurrentTick,
            inputs.SchedulerConfiguration,
            inputs.ShiftConfiguration,
            inputs.ContainmentConfiguration,
            inputs.AnomalyCatalog);

    private static ComposerInputs CreateInputs(
        ServerTick tick,
        ShiftRuntimeState? initialShiftState = null,
        QuotaRuntimeState? initialQuotaState = null,
        MovementNoiseRuntimeState? initialMovementNoise = null,
        LineNoiseRuntimeState? initialLineNoise = null,
        HostTickProgressionEvidence? progression = null,
        ShiftLifecycleRuntimeState? lifecycle = null,
        AcceptedIntentTickBatch? acceptedIntents = null,
        ImmutableHashSet<ItemId>? activeTools = null,
        InMemoryEventJournal? journal = null,
        ImmutableArray<EventId>? eventIds = null,
        SchedulerConfiguration? schedulerConfiguration = null)
    {
        var state = initialShiftState ?? RuntimeFixture.CreateInitialState();
        return new ComposerInputs(
            state,
            initialQuotaState ?? QuotaRuntimeState.Create(Fx.Shift),
            initialMovementNoise ?? MovementNoiseRuntimeState.Create(state.ShiftId),
            initialLineNoise ?? LineNoiseRuntimeState.Create(state.ShiftId),
            progression ?? HostTickProgressionEvidence.Create(state.ShiftId),
            lifecycle ?? ShiftLifecycleRuntimeState.Create(Fx.Shift, LearningId),
            acceptedIntents ?? AcceptedIntentTickBatchFactory.Create(state.ShiftId, tick, ImmutableArray<AuthoritativeAcceptedIntent>.Empty),
            activeTools ?? ImmutableHashSet<ItemId>.Empty,
            journal ?? new InMemoryEventJournal(state.ShiftId),
            eventIds ?? EventIds("event", 4),
            tick,
            schedulerConfiguration ?? Fx.Shift.Scheduler,
            Fx.Shift,
            Fx.Shift.Containment,
            Fx.Anomalies);
    }

    private static void AssertPreStageFailure(ComposerInputs inputs)
    {
        var journal = inputs.Journal;
        var before = journal is null ? default : Snapshot(journal);

        Assert.ThrowsAny<ArgumentException>(() => Execute(inputs));

        if (journal is not null)
        {
            AssertJournalUnchanged(before!, journal);
        }
    }

    private static ImmutableArray<EventId> EventIds(string prefix, int count) =>
        ImmutableArray.CreateRange(Enumerable.Range(0, count).Select(index => EventId.From($"{prefix}_{index}")));

    private static (ShiftRuntimeState State, ServerTick DueTick) ActiveIntakeDeadline()
    {
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(
            RuntimeFixture.CreateInitialState(), ServerTick.Zero, Fx.Shift.Scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        var started = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(
            admitted.State, admitted, Fx.Shift.Profiles[LearningId]));
        return (started.State, started.Deadline.DueAt);
    }

    private static AcceptedIntentTickBatch AcceptedRoute(ShiftRuntimeState state, ServerTick tick, string logId, IntentActionId action)
    {
        var envelope = new IntentEnvelope(
            state.ShiftId,
            IntentId.From("TLAW_037_route"),
            ActorId.From("hint"),
            TargetId.From(logId),
            action,
            state.StateVersion,
            ServerTick.Zero,
            NoIntentParameters.Instance);
        var receipt = new AuthoritativeAcceptedIntent(envelope, RuntimeFixture.BoundActor, tick, ServerReceiveSequence.Zero);
        return AcceptedIntentTickBatchFactory.Create(state.ShiftId, tick, ImmutableArray.Create(receipt));
    }

    private static (ShiftRuntimeState State, ServerTick DueTick) DueSawCycleWithSuccessor()
    {
        var state = QueueForSaw(RuntimeFixture.CreateInitialState(), "log_01");
        var started = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(state, ServerTick.From(10), Fx.Shift.Scheduler));
        return (QueueForSaw(started.State, "log_02"), started.Cycle.DueAt);
    }

    private static ShiftRuntimeState AllLogsWrittenOff()
    {
        var state = RuntimeFixture.CreateInitialState();
        foreach (var log in state.Logs)
        {
            state = RuntimeFixture.MoveToIntake(state, log.LogId.ToString());
            state = RuntimeFixture.MoveHost(state, log.LogId.ToString(), LogState.HELD_WRITTEN_OFF);
        }

        return state;
    }

    private static (ShiftRuntimeState State, ServerTick Tick) MixedStagePublicationState()
    {
        // The setup uses only established services: an armed containment deadline, one active saw cycle,
        // and a learning intake deadline. At tick 100 stage 2 vacates intake, stage 3 advances containment,
        // stage 4 completes saw work, stage 5 schedules the next normal feed, and stage 6 observes the pulse.
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_03");
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.HELD_WRITTEN_OFF);
        state = Assert.IsType<ContainmentStableIntervalArmed>(new ContainmentAdvanceService().Advance(
            state, ServerTick.From(10), Fx.Shift.Containment, Fx.Anomalies)).State;
        state = QueueForSaw(state, "log_02");
        var planned = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(
            state, ServerTick.From(35), Fx.Shift.Scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.From(40)));
        state = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(
            admitted.State, admitted, Fx.Shift.Profiles[LearningId])).State;
        state = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(state, ServerTick.From(94), Fx.Shift.Scheduler)).State;
        return (state, ServerTick.From(100));
    }

    private static (ShiftRuntimeState State, ServerTick DueTick) RepairingFeedGateWithUnblockedIntake()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
        var jammed = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(
            state, JamCause.FEED_GATE_BLOCKED, ServerTick.From(10)));
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(
            jammed.State, ServerTick.From(10), Fx.Shift.Scheduler));
        var unblocked = RuntimeFixture.MoveHost(repairing.State, "log_01", LogState.QUEUED_FOR_SAW);
        return (unblocked, repairing.Hold.DueAt);
    }

    private static (ShiftRuntimeState State, ServerTick DueTick) ExpiringDeadlineWithBlockedSawQueue(SchedulerConfiguration scheduler)
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_03");
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.QUEUED_FOR_SAW);
        state = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(state, ServerTick.From(10), scheduler)).State;
        state = RuntimeFixture.MoveToIntake(state, "log_02");
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.QUEUED_FOR_SAW);
        var planned = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(state, ServerTick.From(20), scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, planned.Schedule.DueAt));
        var started = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(
            admitted.State, admitted, Fx.Shift.Profiles[LearningId]));
        return (started.State, started.Deadline.DueAt);
    }

    private static ShiftRuntimeState QueueForSaw(ShiftRuntimeState state, string logId)
    {
        state = RuntimeFixture.MoveHost(state, logId, LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, logId, LogState.AT_INTAKE);
        return RuntimeFixture.MoveHost(state, logId, LogState.QUEUED_FOR_SAW);
    }

    private static LogRuntimeState Log(ShiftRuntimeState state, string logId)
    {
        Assert.True(state.TryGetLog(LogId.From(logId), out var log));
        return log;
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
                Payload = HistoryPayload.Instance
            });
        }

        return journal;
    }

    private static (HostTickProgressionEvidence Progression, ShiftLifecycleRuntimeState Lifecycle) AdvanceActiveCheckpointTo(
        ShiftRuntimeState state,
        QuotaRuntimeState quota,
        ServerTick targetTick)
    {
        var service = new HostTickCompletionCheckpointService();
        var progression = HostTickProgressionEvidence.Create(state.ShiftId);
        var lifecycle = ShiftLifecycleRuntimeState.Create(Fx.Shift, LearningId);
        for (var value = 0L; value < targetTick.Value; value++)
        {
            var advanced = Assert.IsType<HostTickCheckpointAdvanced>(service.Complete(
                progression, lifecycle, state, quota, ServerTick.From(value), Fx.Shift));
            progression = advanced.Progression;
            lifecycle = advanced.Receipt.Lifecycle;
        }

        return (progression, lifecycle);
    }

    private static void AssertEquivalentExecution(HostStageSevenPublished left, HostStageSevenPublished right)
    {
        Assert.True(left.StageOne.FinalState.ValueEquals(right.StageOne.FinalState));
        Assert.True(left.StageTwo.FinalState.ValueEquals(right.StageTwo.FinalState));
        Assert.True(left.StageThree.FinalState.ValueEquals(right.StageThree.FinalState));
        Assert.True(left.StageFour.FinalShiftState.ValueEquals(right.StageFour.FinalShiftState));
        Assert.True(left.StageFour.FinalQuotaState.ValueEquals(right.StageFour.FinalQuotaState));
        Assert.True(left.StageFive.FinalState.ValueEquals(right.StageFive.FinalState));
        Assert.True(left.StageSix.FinalShiftState.ValueEquals(right.StageSix.FinalShiftState));
        Assert.True(left.StageSix.FinalQuotaState.ValueEquals(right.StageSix.FinalQuotaState));
        Assert.True(left.StageSix.FinalMovementNoise.ValueEquals(right.StageSix.FinalMovementNoise));
        Assert.True(left.StageSix.FinalLineNoise.ValueEquals(right.StageSix.FinalLineNoise));
        Assert.Equal(left.StageSix.Checkpoint.GetType(), right.StageSix.Checkpoint.GetType());

        var leftCheckpoint = Assert.IsType<HostTickCheckpointAdvanced>(left.StageSix.Checkpoint);
        var rightCheckpoint = Assert.IsType<HostTickCheckpointAdvanced>(right.StageSix.Checkpoint);
        Assert.Equal(leftCheckpoint.Receipt.CompletedTick, rightCheckpoint.Receipt.CompletedTick);
        Assert.Equal(leftCheckpoint.Receipt.Evaluation.GetType(), rightCheckpoint.Receipt.Evaluation.GetType());
        Assert.True(leftCheckpoint.Receipt.Lifecycle.ValueEquals(rightCheckpoint.Receipt.Lifecycle));
        Assert.True(leftCheckpoint.Receipt.ShiftState.ValueEquals(rightCheckpoint.Receipt.ShiftState));
        Assert.True(leftCheckpoint.Receipt.QuotaState.ValueEquals(rightCheckpoint.Receipt.QuotaState));
        Assert.Equal(left.GetType(), right.GetType());
        Assert.Equal(left.Publications.Length, right.Publications.Length);
        Assert.Equal(left.Rejections.Length, right.Rejections.Length);
    }

    private static void AssertJournalSemanticsEqual(IEnumerable<EventEnvelope> left, IEnumerable<EventEnvelope> right)
    {
        var leftEvents = left.ToArray();
        var rightEvents = right.ToArray();
        Assert.Equal(leftEvents.Length, rightEvents.Length);
        for (var index = 0; index < leftEvents.Length; index++)
        {
            Assert.Equal(leftEvents[index].Sequence, rightEvents[index].Sequence);
            Assert.Equal(leftEvents[index].ServerTick, rightEvents[index].ServerTick);
            Assert.Equal(leftEvents[index].StateVersionAfter, rightEvents[index].StateVersionAfter);
            Assert.Equal(leftEvents[index].EventType, rightEvents[index].EventType);
            Assert.Equal(leftEvents[index].CausedByIntentId, rightEvents[index].CausedByIntentId);
            Assert.True(SamePayloadSemantics(leftEvents[index].Payload, rightEvents[index].Payload));
        }
    }

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

    private static bool SameSpeciesValues(ImmutableDictionary<SpeciesId, int> left, ImmutableDictionary<SpeciesId, int> right) =>
        left.Count == right.Count && left.All(entry => right.TryGetValue(entry.Key, out var value) && value == entry.Value);

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
        ServerTick CurrentTick,
        SchedulerConfiguration SchedulerConfiguration,
        ShiftConfiguration ShiftConfiguration,
        ContainmentConfiguration ContainmentConfiguration,
        AnomalyCatalog AnomalyCatalog);

    private sealed record JournalSnapshot(
        int Count,
        EventSequence LastSequence,
        ServerTick LastTick,
        StateVersion LastStateVersion,
        EventEnvelope[] Events);

    private sealed class HistoryPayload : IDomainEventPayload
    {
        public static readonly HistoryPayload Instance = new();
        private HistoryPayload() { }
    }
}
