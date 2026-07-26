using System.Reflection;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Tests.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Scheduler;

[Trait("Scope", "TLAW-014")]
public sealed class FeedDueResolutionTests
{
    private static readonly ActorId BoundActor = ActorId.From("feed_due_host");

    [Fact]
    public void Null_or_default_inputs_fail_loudly_before_any_observable_result()
    {
        var service = new FeedDueResolutionService();

        Assert.Throws<ArgumentNullException>(() => service.Resolve(null!, ServerTick.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Resolve(RuntimeFixture.CreateInitialState(), default));
    }

    [Fact]
    public void No_pending_and_not_due_are_typed_exact_reference_no_ops()
    {
        var service = new FeedDueResolutionService();
        var initial = RuntimeFixture.CreateInitialState();
        Assert.Same(initial, Assert.IsType<FeedDueNoPendingFeed>(service.Resolve(initial, ServerTick.Zero)).State);

        var planned = PlanEarly(RuntimeFixture.CreateInitialState(), ServerTick.From(10));
        var notDue = Assert.IsType<FeedDueNotDueYet>(service.Resolve(planned.State, ServerTick.From(11)));
        Assert.Same(planned.State, notDue.State);
        Assert.Equal(planned.Schedule, notDue.State.PendingFeed);
        Assert.Equal(planned.State.StateVersion, notDue.State.StateVersion);
    }

    [Fact]
    public void Initial_plan_resolves_at_tick_zero_directly_to_intake_in_exactly_one_additional_version()
    {
        var before = RuntimeFixture.CreateInitialState();
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(before, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        var accepted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));

        Assert.Equal(planned.Schedule, accepted.ConsumedSchedule);
        Assert.Equal(ServerTick.Zero, accepted.ResolvedAt);
        Assert.Equal(FeedDueDisposition.AdmittedToIntake, accepted.Disposition);
        Assert.Equal(FeedDueFollowUpRequirement.IntakeDeadlineStartRequired, accepted.FollowUpRequirement);
        Assert.Equal(planned.State.StateVersion, accepted.PriorStateVersion);
        Assert.Equal(planned.State.StateVersion.Next(), accepted.CurrentStateVersion);
        Assert.Equal(accepted.CurrentStateVersion, accepted.State.StateVersion);
        Assert.Null(accepted.State.PendingFeed);
        Assert.Equal(LogState.AT_INTAKE, Log(accepted.State, "log_01").State);
        Assert.Equal(StateVersion.From(2), accepted.State.StateVersion);
        Assert.Empty(accepted.State.ProcessedIntentIds);
    }

    [Fact]
    public void Normal_schedule_uses_stored_due_tick_and_late_execution_preserves_null_causation()
    {
        var before = MoveFirstLogOutOfSupply(RuntimeFixture.CreateInitialState());
        var planned = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(before, ServerTick.From(20), Fixture.LoadP0().Shift.Scheduler));
        var exact = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.From(25)));

        Assert.Equal(ServerTick.From(25), exact.ResolvedAt);
        Assert.Null(exact.ConsumedSchedule.CausedByIntentId);
        Assert.Equal(LogState.AT_INTAKE, Log(exact.State, "log_02").State);

        var laterPlan = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(MoveFirstLogOutOfSupply(RuntimeFixture.CreateInitialState()), ServerTick.From(20), Fixture.LoadP0().Shift.Scheduler));
        var later = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(laterPlan.State, ServerTick.From(29)));
        Assert.Equal(ServerTick.From(29), later.ResolvedAt);
        Assert.Equal(laterPlan.Schedule, later.ConsumedSchedule);
    }

    [Fact]
    public void Due_feed_directly_admits_to_intake_even_if_feed_gate_is_occupied()
    {
        var before = MoveFirstLogOutOfSupply(RuntimeFixture.CreateInitialState());
        var planned = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(before, ServerTick.From(10), Fixture.LoadP0().Shift.Scheduler));
        var withUnrelatedGateLog = RuntimeFixture.MoveHost(planned.State, "log_03", LogState.AT_FEED_GATE);

        var resolved = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(withUnrelatedGateLog, ServerTick.From(15)));

        Assert.Equal(FeedDueDisposition.AdmittedToIntake, resolved.Disposition);
        Assert.Equal(LogState.AT_INTAKE, Log(resolved.State, "log_02").State);
        Assert.Equal(LogState.AT_FEED_GATE, Log(resolved.State, "log_03").State);
    }

    [Fact]
    public void Early_due_places_the_reserved_log_at_feed_gate_when_intake_remains_occupied_and_retains_causation()
    {
        var withIntake = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var planned = PlanEarly(withIntake, ServerTick.From(10));
        var beforeLine = planned.State.Line;
        var beforeIntents = planned.State.ProcessedIntentIds;

        var resolved = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.From(12)));

        Assert.Equal(FeedDueDisposition.PlacedAtFeedGate, resolved.Disposition);
        Assert.Equal(FeedDueFollowUpRequirement.FeedGateJamDerivationRequired, resolved.FollowUpRequirement);
        Assert.Equal(LogState.AT_FEED_GATE, Log(resolved.State, "log_02").State);
        Assert.Equal(planned.Schedule.CausedByIntentId, resolved.ConsumedSchedule.CausedByIntentId);
        Assert.True(beforeIntents.SetEquals(resolved.State.ProcessedIntentIds));
        Assert.Equal(beforeLine, resolved.State.Line);
    }

    [Fact]
    public void Initial_and_normal_schedules_use_the_same_current_state_feed_gate_rule_when_intake_is_occupied()
    {
        var initialPlan = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(RuntimeFixture.CreateInitialState(), ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        var initialWithIntake = RuntimeFixture.MoveToIntake(initialPlan.State, "log_02");
        var initialResolved = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(initialWithIntake, ServerTick.Zero));
        Assert.Equal(FeedDueDisposition.PlacedAtFeedGate, initialResolved.Disposition);
        Assert.Equal(LogState.AT_FEED_GATE, Log(initialResolved.State, "log_01").State);

        var normalBefore = MoveFirstLogOutOfSupply(RuntimeFixture.CreateInitialState());
        var normalPlan = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(normalBefore, ServerTick.From(10), Fixture.LoadP0().Shift.Scheduler));
        var normalWithIntake = RuntimeFixture.MoveToIntake(normalPlan.State, "log_03");
        var normalResolved = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(normalWithIntake, ServerTick.From(15)));
        Assert.Equal(FeedDueDisposition.PlacedAtFeedGate, normalResolved.Disposition);
        Assert.Equal(LogState.AT_FEED_GATE, Log(normalResolved.State, "log_02").State);
    }

    [Fact]
    public void Early_schedule_admits_to_intake_when_intake_becomes_free_before_due()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var planned = PlanEarly(state, ServerTick.From(10));
        var freed = RuntimeFixture.MoveHost(planned.State, "log_01", LogState.QUEUED_FOR_SAW);

        var resolved = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(freed, ServerTick.From(12)));

        Assert.Equal(FeedDueDisposition.AdmittedToIntake, resolved.Disposition);
        Assert.Equal(LogState.AT_INTAKE, Log(resolved.State, "log_02").State);
    }

    [Fact]
    public void Line_and_full_destination_no_ops_retain_the_due_schedule_and_exact_original_state()
    {
        var blocked = PlannedEarlyWithIntakeAndGate();
        var full = Assert.IsType<FeedDueFeedGateOccupied>(new FeedDueResolutionService().Resolve(blocked, ServerTick.From(12)));
        Assert.Same(blocked, full.State);
        Assert.NotNull(full.State.PendingFeed);

        var jammed = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(blocked, JamCause.FEED_GATE_BLOCKED, ServerTick.From(11))).State;
        var lineNoOp = Assert.IsType<FeedDueLineNotClear>(new FeedDueResolutionService().Resolve(jammed, ServerTick.From(12)));
        Assert.Same(jammed, lineNoOp.State);
        Assert.NotNull(lineNoOp.State.PendingFeed);

        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jammed, ServerTick.From(11), Fixture.LoadP0().Shift.Scheduler)).State;
        var repairNoOp = Assert.IsType<FeedDueLineNotClear>(new FeedDueResolutionService().Resolve(repairing, ServerTick.From(12)));
        Assert.Same(repairing, repairNoOp.State);
    }

    [Fact]
    public void Repeated_resolution_is_idempotent_and_ordinary_transitions_work_after_pending_consumption()
    {
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(RuntimeFixture.CreateInitialState(), ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        var accepted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        var repeated = Assert.IsType<FeedDueNoPendingFeed>(new FeedDueResolutionService().Resolve(accepted.State, ServerTick.Zero));
        Assert.Same(accepted.State, repeated.State);

        var moved = Assert.IsType<HostLogTransitionAccepted>(new HostLogTransitionService().Apply(accepted.State, LogId.From("log_01"), LogState.AT_PROCEDURE));
        Assert.Equal(LogState.AT_PROCEDURE, Log(moved.State, "log_01").State);
    }

    [Fact]
    public void Reservation_holds_before_resolution_and_service_exposes_no_caller_selected_target_or_destination()
    {
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(RuntimeFixture.CreateInitialState(), ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        var reserved = Assert.IsType<HostLogTransitionRejected>(new HostLogTransitionService().Apply(planned.State, LogId.From("log_01"), LogState.AT_FEED_GATE));
        Assert.Equal(HostLogTransitionFailure.PendingFeedReserved, reserved.Failure);

        var parameters = typeof(FeedDueResolutionService).GetMethod(nameof(FeedDueResolutionService.Resolve))!.GetParameters();
        Assert.Equal(new[] { typeof(ShiftRuntimeState), typeof(ServerTick) }, parameters.Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void Equivalent_independent_runs_are_deterministic_and_controlled_tick_or_occupancy_changes_select_expected_branches()
    {
        var first = PlanEarly(RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01"), ServerTick.From(10));
        var second = PlanEarly(RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01"), ServerTick.From(10));
        var firstResult = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(first.State, ServerTick.From(12)));
        var secondResult = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(second.State, ServerTick.From(12)));

        Assert.Equal(firstResult.Disposition, secondResult.Disposition);
        Assert.Equal(firstResult.FollowUpRequirement, secondResult.FollowUpRequirement);
        Assert.Equal(firstResult.ConsumedSchedule, secondResult.ConsumedSchedule);
        Assert.True(firstResult.State.ValueEquals(secondResult.State));

        Assert.IsType<FeedDueNotDueYet>(new FeedDueResolutionService().Resolve(first.State, ServerTick.From(11)));
        var freed = RuntimeFixture.MoveHost(second.State, "log_01", LogState.QUEUED_FOR_SAW);
        Assert.Equal(FeedDueDisposition.AdmittedToIntake, Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(freed, ServerTick.From(12))).Disposition);
    }

    [Fact]
    public void Initial_normal_and_early_resolutions_commit_once_through_tlaw011_with_the_consumed_causation()
    {
        var commit = new JournaledMutationCommitService();
        var initialBefore = RuntimeFixture.CreateInitialState();
        var initialPlan = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initialBefore, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        var initialJournal = new InMemoryEventJournal(initialBefore.ShiftId);
        Commit(commit, initialJournal, initialBefore, initialPlan.State, ServerTick.Zero, "initial_plan", null);
        var initialResolution = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(initialPlan.State, ServerTick.Zero));
        Commit(commit, initialJournal, initialPlan.State, initialResolution.State, ServerTick.Zero, "initial_due", initialResolution.ConsumedSchedule.CausedByIntentId);
        Assert.Equal(EventSequence.From(2), initialJournal.LastSequence);
        Assert.Equal(StateVersion.From(2), initialJournal.LastStateVersion);

        var normalBefore = MoveFirstLogOutOfSupply(RuntimeFixture.CreateInitialState());
        var normalPlan = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(normalBefore, ServerTick.From(10), Fixture.LoadP0().Shift.Scheduler));
        var normalJournal = AlignedJournal(normalBefore, ServerTick.Zero);
        Commit(commit, normalJournal, normalBefore, normalPlan.State, ServerTick.From(10), "normal_plan", null);
        var normalResolution = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(normalPlan.State, ServerTick.From(15)));
        Commit(commit, normalJournal, normalPlan.State, normalResolution.State, ServerTick.From(15), "normal_due", normalResolution.ConsumedSchedule.CausedByIntentId);

        var earlyBefore = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var earlyPlan = PlanEarly(earlyBefore, ServerTick.From(10));
        var earlyJournal = AlignedJournal(earlyBefore, ServerTick.Zero);
        Commit(commit, earlyJournal, earlyBefore, earlyPlan.State, ServerTick.From(10), "early_plan", earlyPlan.Schedule.CausedByIntentId);
        var earlyResolution = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(earlyPlan.State, ServerTick.From(12)));
        Commit(commit, earlyJournal, earlyPlan.State, earlyResolution.State, ServerTick.From(12), "early_due", earlyResolution.ConsumedSchedule.CausedByIntentId);
        Assert.Equal(earlyPlan.Schedule.CausedByIntentId, earlyJournal.Events[^1].CausedByIntentId);
    }

    private static EarlyFeedScheduled PlanEarly(ShiftRuntimeState state, ServerTick tick)
    {
        var intent = new IntentEnvelope(state.ShiftId, IntentId.From($"early_{tick.Value}"), ActorId.From("hint"), FeedPlanningTargets.FeedGate, FeedPlanningIntentActions.RequestEarlyFeed, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        return Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(state, intent, BoundActor, tick, Fixture.LoadP0().Shift.Scheduler));
    }

    private static ShiftRuntimeState PlannedEarlyWithIntakeAndGate()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var planned = PlanEarly(state, ServerTick.From(10));
        return RuntimeFixture.MoveHost(planned.State, "log_03", LogState.AT_FEED_GATE);
    }

    private static ShiftRuntimeState MoveFirstLogOutOfSupply(ShiftRuntimeState state)
    {
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.AT_INTAKE);
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.IN_SAW);
        return RuntimeFixture.MoveHost(state, "log_01", LogState.PROCESSED);
    }

    private static LogRuntimeState Log(ShiftRuntimeState state, string id)
    {
        Assert.True(state.TryGetLog(LogId.From(id), out var log));
        return log;
    }

    private static void Commit(JournaledMutationCommitService service, IEventJournal journal, ShiftRuntimeState before, ShiftRuntimeState after, ServerTick tick, string eventId, IntentId? cause)
    {
        Assert.IsType<JournaledMutationCommitted>(service.Commit(journal, before, after, tick, new DomainEventDraft(EventId.From(eventId), EventTypeId.From("test.scheduler.feed_due"), new FeedDuePayload(eventId), cause)));
    }

    private static InMemoryEventJournal AlignedJournal(ShiftRuntimeState state, ServerTick tick)
    {
        var journal = new InMemoryEventJournal(state.ShiftId);
        for (var version = 1L; version <= state.StateVersion.Value; version++)
        {
            journal.Append(new EventEnvelope { ShiftId = state.ShiftId, EventId = EventId.From($"seed_{version}"), Sequence = EventSequence.From(version), ServerTick = tick, StateVersionAfter = StateVersion.From(version), EventType = EventTypeId.From("test.seed"), Payload = new FeedDuePayload($"seed_{version}") });
        }

        return journal;
    }

    private sealed record FeedDuePayload(string Value) : IDomainEventPayload;
}
