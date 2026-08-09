using System.Collections.Immutable;
using System.Reflection;
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
                typeof(ImmutableArray<EventId>), typeof(ServerTick), typeof(SchedulerConfiguration),
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
        AssertPreStageFailure(inputs with { EventIds = default });
        AssertPreStageFailure(inputs with { EventIds = ImmutableArray.Create(default(EventId)) });
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
    public void Equivalent_inputs_are_deterministic_and_event_id_changes_affect_only_existing_identity_fields()
    {
        var first = CreateInputs(ServerTick.Zero, eventIds: EventIds("first", 4));
        var second = CreateInputs(ServerTick.Zero, eventIds: EventIds("first", 4));
        var changedIds = CreateInputs(ServerTick.Zero, eventIds: EventIds("second", 4));

        var firstResult = Assert.IsType<HostStageSevenPublished>(Execute(first));
        var secondResult = Assert.IsType<HostStageSevenPublished>(Execute(second));
        var changedResult = Assert.IsType<HostStageSevenPublished>(Execute(changedIds));

        Assert.True(firstResult.FinalShiftState.ValueEquals(secondResult.FinalShiftState));
        Assert.True(firstResult.FinalQuotaState.ValueEquals(secondResult.FinalQuotaState));
        Assert.Equal(first.Journal.Events.Select(EventSemantics), second.Journal.Events.Select(EventSemantics));
        Assert.Equal(first.Journal.Events.Select(envelope => envelope.EventType), changedIds.Journal.Events.Select(envelope => envelope.EventType));
        Assert.NotEqual(first.Journal.Events.Select(envelope => envelope.EventId), changedIds.Journal.Events.Select(envelope => envelope.EventId));
        Assert.True(firstResult.FinalShiftState.ValueEquals(changedResult.FinalShiftState));
        Assert.True(firstResult.FinalQuotaState.ValueEquals(changedResult.FinalQuotaState));
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
            inputs.EventIds,
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

    private static (EventTypeId Type, EventSequence Sequence, ServerTick Tick, StateVersion Version) EventSemantics(EventEnvelope envelope) =>
        (envelope.EventType, envelope.Sequence, envelope.ServerTick, envelope.StateVersionAfter);

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
}
