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
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Scheduler;

[Trait("Scope", "TLAW-013")]
public sealed class FeedPlanningTests
{
    private static readonly ActorId BoundActor = ActorId.From("scheduler_host");

    [Fact]
    public void Initial_runtime_has_no_pending_feed()
    {
        Assert.Null(RuntimeFixture.CreateInitialState().PendingFeed);
    }

    [Fact]
    public void Pending_feed_requires_exact_identity_timing_kind_and_causation_combinations()
    {
        var log = LogId.From("log_01");
        var scheduled = ServerTick.From(10);

        var initial = new PendingFeedSchedule(log, FeedScheduleKind.INITIAL, scheduled, SimulationDuration.Zero, null);
        var normal = new PendingFeedSchedule(log, FeedScheduleKind.NORMAL, scheduled, SimulationDuration.FromTicks(5), null);
        var early = new PendingFeedSchedule(log, FeedScheduleKind.EARLY, scheduled, SimulationDuration.FromTicks(2), IntentId.From("early_01"));

        Assert.Equal(ServerTick.From(10), initial.DueAt);
        Assert.Equal(ServerTick.From(15), normal.DueAt);
        Assert.Equal(ServerTick.From(12), early.DueAt);
        Assert.Null(initial.CausedByIntentId);
        Assert.Equal(IntentId.From("early_01"), early.CausedByIntentId);

        Assert.Throws<ArgumentException>(() => new PendingFeedSchedule(default, FeedScheduleKind.INITIAL, scheduled, SimulationDuration.Zero, null));
        Assert.Throws<ArgumentException>(() => new PendingFeedSchedule(log, (FeedScheduleKind)99, scheduled, SimulationDuration.Zero, null));
        Assert.Throws<ArgumentException>(() => new PendingFeedSchedule(log, FeedScheduleKind.INITIAL, default, SimulationDuration.Zero, null));
        Assert.Throws<ArgumentException>(() => new PendingFeedSchedule(log, FeedScheduleKind.INITIAL, scheduled, default, null));
        Assert.Throws<ArgumentException>(() => new PendingFeedSchedule(log, FeedScheduleKind.NORMAL, scheduled, SimulationDuration.Zero, null));
        Assert.Throws<ArgumentException>(() => new PendingFeedSchedule(log, FeedScheduleKind.EARLY, scheduled, SimulationDuration.FromTicks(2), null));
        Assert.Throws<ArgumentException>(() => new PendingFeedSchedule(log, FeedScheduleKind.NORMAL, scheduled, SimulationDuration.FromTicks(2), IntentId.From("not_allowed")));
        Assert.Throws<OverflowException>(() => new PendingFeedSchedule(log, FeedScheduleKind.NORMAL, ServerTick.From(long.MaxValue), SimulationDuration.FromTicks(1), null));
    }

    [Fact]
    public void Pending_feed_has_an_immutable_public_surface_without_a_caller_supplied_due_tick()
    {
        Assert.DoesNotContain(typeof(PendingFeedSchedule).GetProperties(BindingFlags.Instance | BindingFlags.Public), property => property.SetMethod is not null);
        Assert.DoesNotContain(typeof(PendingFeedSchedule).GetConstructors(), constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(ServerTick) && parameter.Name!.Contains("due", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Initial_planning_selects_first_manifest_log_and_creates_due_at_zero_schedule()
    {
        var before = RuntimeFixture.CreateInitialState();
        var result = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(before, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));

        Assert.Equal(before.StateVersion, result.PriorStateVersion);
        Assert.Equal(before.StateVersion.Next(), result.CurrentStateVersion);
        Assert.Equal(result.CurrentStateVersion, result.State.StateVersion);
        Assert.Equal(FeedScheduleKind.INITIAL, result.Schedule.Kind);
        Assert.Equal(LogId.From("log_01"), result.Schedule.LogId);
        Assert.Equal(ServerTick.Zero, result.Schedule.ScheduledAt);
        Assert.Equal(SimulationDuration.Zero, result.Schedule.Delay);
        Assert.Equal(ServerTick.Zero, result.Schedule.DueAt);
        Assert.Same(result.Schedule, result.State.PendingFeed);
        Assert.Empty(result.State.ProcessedIntentIds);
    }

    [Fact]
    public void Initial_planning_rejects_non_zero_tick_without_mutating()
    {
        var state = RuntimeFixture.CreateInitialState();
        var result = Assert.IsType<InitialFeedPlanningNoOp>(new InitialFeedPlanningService().Plan(state, ServerTick.From(1), Fixture.LoadP0().Shift.Scheduler));

        Assert.Equal(InitialFeedPlanningNoOpReason.CurrentTickNotZero, result.Reason);
        Assert.Same(state, result.State);
    }

    [Fact]
    public void Initial_planning_rejects_non_pristine_and_second_attempt_with_the_exact_original_state()
    {
        var configuration = Fixture.LoadP0().Shift.Scheduler;
        var pristine = RuntimeFixture.CreateInitialState();
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(pristine, ServerTick.Zero, configuration));
        var second = Assert.IsType<InitialFeedPlanningNoOp>(new InitialFeedPlanningService().Plan(planned.State, ServerTick.Zero, configuration));
        Assert.Equal(InitialFeedPlanningNoOpReason.PendingFeedExists, second.Reason);
        Assert.Same(planned.State, second.State);

        var changed = RuntimeFixture.MoveHost(RuntimeFixture.CreateInitialState(), "log_01", LogState.AT_FEED_GATE);
        var nonPristine = Assert.IsType<InitialFeedPlanningNoOp>(new InitialFeedPlanningService().Plan(changed, ServerTick.Zero, configuration));
        Assert.Equal(InitialFeedPlanningNoOpReason.StateNotPristine, nonPristine.Reason);
        Assert.Same(changed, nonPristine.State);
    }

    [Fact]
    public void Initial_planning_reports_no_more_logs_without_creating_or_consuming_a_pending_feed()
    {
        var state = MoveAllLogsOutOfSupply(RuntimeFixture.CreateInitialState());
        var result = Assert.IsType<InitialFeedPlanningNoOp>(new InitialFeedPlanningService().Plan(state, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));

        Assert.Equal(InitialFeedPlanningNoOpReason.NoMoreLogs, result.Reason);
        Assert.Same(state, result.State);
        Assert.Null(state.PendingFeed);
    }

    [Fact]
    public void Normal_planning_uses_first_remaining_scheduled_log_and_configured_delay()
    {
        var state = MoveFirstLogOutOfSupply(RuntimeFixture.CreateInitialState());
        var result = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(state, ServerTick.From(20), Fixture.LoadP0().Shift.Scheduler));

        Assert.Equal(LogId.From("log_02"), result.Schedule.LogId);
        Assert.Equal(FeedScheduleKind.NORMAL, result.Schedule.Kind);
        Assert.Equal(SimulationDuration.FromTicks(5), result.Schedule.Delay);
        Assert.Equal(ServerTick.From(25), result.Schedule.DueAt);
        Assert.Empty(result.State.ProcessedIntentIds);
        Assert.Equal(state.StateVersion.Next(), result.State.StateVersion);
    }

    [Fact]
    public void Normal_planning_exposes_each_eligibility_no_op_without_mutation()
    {
        var service = new NormalFeedPlanningService();
        var configuration = Fixture.LoadP0().Shift.Scheduler;
        var pristine = RuntimeFixture.CreateInitialState();
        AssertNormalNoOp(service.Plan(pristine, ServerTick.Zero, configuration), NormalFeedPlanningNoOpReason.InitialPlanningRequired, pristine);

        var intakeOccupied = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        AssertNormalNoOp(service.Plan(intakeOccupied, ServerTick.From(5), configuration), NormalFeedPlanningNoOpReason.IntakeOccupied, intakeOccupied);

        var feedGateOccupied = RuntimeFixture.MoveHost(RuntimeFixture.CreateInitialState(), "log_01", LogState.AT_FEED_GATE);
        AssertNormalNoOp(service.Plan(feedGateOccupied, ServerTick.From(5), configuration), NormalFeedPlanningNoOpReason.FeedGateOccupied, feedGateOccupied);

        var pending = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(RuntimeFixture.CreateInitialState(), ServerTick.Zero, configuration)).State;
        AssertNormalNoOp(service.Plan(pending, ServerTick.From(5), configuration), NormalFeedPlanningNoOpReason.FeedAlreadyPending, pending);

        var noMoreLogs = MoveAllLogsOutOfSupply(RuntimeFixture.CreateInitialState());
        AssertNormalNoOp(service.Plan(noMoreLogs, ServerTick.From(100), configuration), NormalFeedPlanningNoOpReason.NoMoreLogs, noMoreLogs);
    }

    [Fact]
    public void Normal_and_early_planning_reject_a_non_clear_line_after_other_node_conditions_are_clear()
    {
        var configuration = Fixture.LoadP0().Shift.Scheduler;
        var lineNotClear = CreateLineOnlyBlockedState();

        AssertNormalNoOp(new NormalFeedPlanningService().Plan(lineNotClear, ServerTick.From(4), configuration), NormalFeedPlanningNoOpReason.LineNotClear, lineNotClear);
        AssertEarlyRejected(new EarlyFeedIntentHandler().Handle(lineNotClear, EarlyIntent(lineNotClear, "jammed", lineNotClear.StateVersion), BoundActor, ServerTick.From(4), configuration), RejectionReason.LINE_NOT_CLEAR, lineNotClear);

        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(lineNotClear, ServerTick.From(4), configuration)).State;
        AssertNormalNoOp(new NormalFeedPlanningService().Plan(repairing, ServerTick.From(5), configuration), NormalFeedPlanningNoOpReason.LineNotClear, repairing);
        AssertEarlyRejected(new EarlyFeedIntentHandler().Handle(repairing, EarlyIntent(repairing, "repairing", repairing.StateVersion), BoundActor, ServerTick.From(5), configuration), RejectionReason.LINE_NOT_CLEAR, repairing);
    }

    [Fact]
    public void Scheduler_defensively_rejects_uninitialized_tick_and_invalid_configured_delays_before_mutation()
    {
        var state = RuntimeFixture.CreateInitialState();
        var configuration = Fixture.LoadP0().Shift.Scheduler;

        Assert.Throws<ArgumentOutOfRangeException>(() => new InitialFeedPlanningService().Plan(state, default, configuration));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InitialFeedPlanningService().Plan(state, ServerTick.Zero, configuration with { InitialAdmissionDelaySeconds = -1 }));

        var nonPristine = MoveFirstLogOutOfSupply(state);
        Assert.Throws<ArgumentOutOfRangeException>(() => new NormalFeedPlanningService().Plan(nonPristine, ServerTick.From(10), configuration with { NormalFeedDelaySeconds = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EarlyFeedIntentHandler().Handle(nonPristine, EarlyIntent(nonPristine, "invalid_delay", nonPristine.StateVersion), BoundActor, ServerTick.From(10), configuration with { EarlyFeedDelaySeconds = 0 }));
    }

    [Fact]
    public void Early_feed_accepts_the_exact_contract_and_records_intent_once_in_the_same_version_step()
    {
        var state = RuntimeFixture.CreateInitialState();
        var intent = EarlyIntent(state, "early_accepted", state.StateVersion, "attacker_hint");
        var result = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(state, intent, BoundActor, ServerTick.From(7), Fixture.LoadP0().Shift.Scheduler));

        Assert.Equal(BoundActor, result.AuthoritativeActor);
        Assert.NotEqual(intent.ActorIdHint, result.AuthoritativeActor);
        Assert.Equal(FeedScheduleKind.EARLY, result.Schedule.Kind);
        Assert.Equal(LogId.From("log_01"), result.Schedule.LogId);
        Assert.Equal(ServerTick.From(7), result.Schedule.ScheduledAt);
        Assert.Equal(SimulationDuration.FromTicks(2), result.Schedule.Delay);
        Assert.Equal(ServerTick.From(9), result.Schedule.DueAt);
        Assert.Equal(intent.IntentId, result.Schedule.CausedByIntentId);
        Assert.Contains(intent.IntentId, result.State.ProcessedIntentIds);
        Assert.Equal(state.StateVersion.Next(), result.State.StateVersion);
        Assert.Equal(result.PriorStateVersion, state.StateVersion);
        Assert.Equal(result.CurrentStateVersion, result.State.StateVersion);
    }

    [Fact]
    public void Early_feed_accepts_while_intake_is_occupied_and_keeps_manifest_order()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var result = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(state, EarlyIntent(state, "intake_occupied", state.StateVersion), BoundActor, ServerTick.From(8), Fixture.LoadP0().Shift.Scheduler));

        Assert.Equal(LogId.From("log_02"), result.Schedule.LogId);
        Assert.Equal(FeedScheduleKind.EARLY, result.Schedule.Kind);
    }

    [Fact]
    public void Early_feed_uses_frozen_common_guard_order_and_duplicate_idempotency()
    {
        var state = RuntimeFixture.CreateInitialState();
        var handler = new EarlyFeedIntentHandler();
        var wrongShift = new IntentEnvelope(ShiftId.From("other"), IntentId.From("one"), ActorId.From("hint"), FeedPlanningTargets.FeedGate, FeedPlanningIntentActions.RequestEarlyFeed, StateVersion.From(99), ServerTick.Zero, NoIntentParameters.Instance);
        AssertEarlyRejected(handler.Handle(state, wrongShift, null, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler), RejectionReason.SHIFT_MISMATCH, state);

        var missingActor = EarlyIntent(state, "two", StateVersion.From(99));
        AssertEarlyRejected(handler.Handle(state, missingActor, null, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler), RejectionReason.ACTOR_NOT_BOUND, state);

        var stale = EarlyIntent(state, "three", StateVersion.From(99));
        AssertEarlyRejected(handler.Handle(state, stale, BoundActor, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler), RejectionReason.STALE_STATE_VERSION, state);

        var accepted = Assert.IsType<EarlyFeedScheduled>(handler.Handle(state, EarlyIntent(state, "once", state.StateVersion), BoundActor, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        var duplicate = EarlyIntent(accepted.State, "once", accepted.State.StateVersion);
        var ignored = Assert.IsType<DuplicateEarlyFeedIntentIgnored>(handler.Handle(accepted.State, duplicate, BoundActor, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        Assert.Same(accepted.State, ignored.State);
        Assert.Equal(IntentId.From("once"), ignored.IntentId);
    }

    [Fact]
    public void Early_feed_distinguishes_unsupported_action_target_and_parameters_without_recording_intent()
    {
        var state = RuntimeFixture.CreateInitialState();
        var handler = new EarlyFeedIntentHandler();
        var action = new IntentEnvelope(state.ShiftId, IntentId.From("bad_action"), ActorId.From("hint"), FeedPlanningTargets.FeedGate, IntentActionId.From("other"), state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        AssertUnsupported(handler.Handle(state, action, BoundActor, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler), EarlyFeedIntentUnsupportedReason.Action, state);

        var target = new IntentEnvelope(state.ShiftId, IntentId.From("bad_target"), ActorId.From("hint"), TargetId.From("INTAKE"), FeedPlanningIntentActions.RequestEarlyFeed, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        AssertUnsupported(handler.Handle(state, target, BoundActor, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler), EarlyFeedIntentUnsupportedReason.Target, state);

        var parameters = new IntentEnvelope(state.ShiftId, IntentId.From("bad_parameters"), ActorId.From("hint"), FeedPlanningTargets.FeedGate, FeedPlanningIntentActions.RequestEarlyFeed, state.StateVersion, ServerTick.Zero, new OtherParameters());
        AssertUnsupported(handler.Handle(state, parameters, BoundActor, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler), EarlyFeedIntentUnsupportedReason.Parameters, state);
    }

    [Fact]
    public void Early_feed_uses_the_frozen_scheduler_rejection_order_and_preserves_state()
    {
        var configuration = Fixture.LoadP0().Shift.Scheduler;
        var handler = new EarlyFeedIntentHandler();
        var noMore = MoveAllLogsOutOfSupply(RuntimeFixture.CreateInitialState());
        AssertEarlyRejected(handler.Handle(noMore, EarlyIntent(noMore, "none", noMore.StateVersion), BoundActor, ServerTick.From(100), configuration), RejectionReason.NO_MORE_LOGS, noMore);

        var pending = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(RuntimeFixture.CreateInitialState(), ServerTick.Zero, configuration)).State;
        AssertEarlyRejected(handler.Handle(pending, EarlyIntent(pending, "pending", pending.StateVersion), BoundActor, ServerTick.From(1), configuration), RejectionReason.FEED_ALREADY_PENDING, pending);

        var feedGate = RuntimeFixture.MoveHost(RuntimeFixture.CreateInitialState(), "log_01", LogState.AT_FEED_GATE);
        AssertEarlyRejected(handler.Handle(feedGate, EarlyIntent(feedGate, "gate", feedGate.StateVersion), BoundActor, ServerTick.From(1), configuration), RejectionReason.FEED_GATE_OCCUPIED, feedGate);
    }

    [Fact]
    public void Pending_log_is_reserved_from_generic_host_transitions_while_unrelated_transitions_preserve_the_pending_value()
    {
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(RuntimeFixture.CreateInitialState(), ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        var reserved = Assert.IsType<HostLogTransitionRejected>(new HostLogTransitionService().Apply(planned.State, LogId.From("log_01"), LogState.AT_FEED_GATE));
        Assert.Equal(HostLogTransitionFailure.PendingFeedReserved, reserved.Failure);
        Assert.Same(planned.State, reserved.State);

        var unrelated = Assert.IsType<HostLogTransitionAccepted>(new HostLogTransitionService().Apply(planned.State, LogId.From("log_02"), LogState.AT_FEED_GATE));
        Assert.Equal(planned.Schedule, unrelated.State.PendingFeed);
        Assert.Equal(planned.State.StateVersion.Next(), unrelated.State.StateVersion);
    }

    [Fact]
    public void Pending_feed_is_preserved_by_existing_mutations_and_schedule_state_is_value_equivalent_across_independent_runs()
    {
        var configuration = Fixture.LoadP0().Shift.Scheduler;
        var first = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(RuntimeFixture.CreateInitialState(), ServerTick.Zero, configuration)).State;
        var second = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(RuntimeFixture.CreateInitialState(), ServerTick.Zero, configuration)).State;
        Assert.True(first.ValueEquals(second));

        var moved = Assert.IsType<HostLogTransitionAccepted>(new HostLogTransitionService().Apply(first, LogId.From("log_02"), LogState.AT_FEED_GATE)).State;
        Assert.Equal(first.PendingFeed, moved.PendingFeed);
        Assert.DoesNotContain(typeof(ShiftRuntimeState).GetMethods(BindingFlags.Instance | BindingFlags.Public), method => method.Name.Contains("PendingFeed", StringComparison.OrdinalIgnoreCase) && method.Name.Contains("Clear", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Accepted_initial_normal_and_early_plans_commit_once_through_the_journal_boundary_while_no_ops_commit_nothing()
    {
        var configuration = Fixture.LoadP0().Shift.Scheduler;
        var commit = new JournaledMutationCommitService();
        var initialBefore = RuntimeFixture.CreateInitialState();
        var initial = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initialBefore, ServerTick.Zero, configuration));
        var initialJournal = AlignedJournal(initialBefore, ServerTick.Zero);
        AssertCommitted(commit.Commit(initialJournal, initialBefore, initial.State, ServerTick.Zero, Draft("initial")), 1, 1, null);

        var normalBefore = MoveFirstLogOutOfSupply(RuntimeFixture.CreateInitialState());
        var normal = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(normalBefore, ServerTick.From(10), configuration));
        var normalJournal = AlignedJournal(normalBefore, ServerTick.Zero);
        AssertCommitted(commit.Commit(normalJournal, normalBefore, normal.State, ServerTick.From(10), Draft("normal")), normal.State.StateVersion.Value, normal.State.StateVersion.Value, null);

        var earlyBefore = RuntimeFixture.CreateInitialState();
        var intent = EarlyIntent(earlyBefore, "journal_early", earlyBefore.StateVersion);
        var early = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(earlyBefore, intent, BoundActor, ServerTick.From(3), configuration));
        var earlyJournal = AlignedJournal(earlyBefore, ServerTick.Zero);
        AssertCommitted(commit.Commit(earlyJournal, earlyBefore, early.State, ServerTick.From(3), Draft("early", intent.IntentId)), 1, 1, intent.IntentId);

        var noOp = new NormalFeedPlanningService().Plan(RuntimeFixture.CreateInitialState(), ServerTick.Zero, configuration);
        Assert.IsType<NormalFeedPlanningNoOp>(noOp);
        Assert.Empty(new InMemoryEventJournal(earlyBefore.ShiftId).Events);
    }

    private static ShiftRuntimeState MoveFirstLogOutOfSupply(ShiftRuntimeState state)
    {
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.AT_INTAKE);
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.IN_SAW);
        return RuntimeFixture.MoveHost(state, "log_01", LogState.PROCESSED);
    }

    private static ShiftRuntimeState MoveAllLogsOutOfSupply(ShiftRuntimeState state)
    {
        foreach (var log in state.Logs.Select(log => log.LogId.Value!))
        {
            state = RuntimeFixture.MoveHost(state, log, LogState.AT_FEED_GATE);
            state = RuntimeFixture.MoveHost(state, log, LogState.AT_INTAKE);
            state = RuntimeFixture.MoveHost(state, log, LogState.QUEUED_FOR_SAW);
            state = RuntimeFixture.MoveHost(state, log, LogState.IN_SAW);
            state = RuntimeFixture.MoveHost(state, log, LogState.PROCESSED);
        }

        return state;
    }

    private static ShiftRuntimeState CreateLineOnlyBlockedState()
    {
        var state = RuntimeFixture.MoveHost(RuntimeFixture.CreateInitialState(), "log_01", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.AT_INTAKE);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
        state = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.FEED_GATE_BLOCKED, ServerTick.From(3))).State;
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_INTAKE);
        return RuntimeFixture.MoveHost(state, "log_02", LogState.AT_PROCEDURE);
    }

    private static IntentEnvelope EarlyIntent(ShiftRuntimeState state, string intentId, StateVersion version, string actorHint = "untrusted") => new(
        state.ShiftId,
        IntentId.From(intentId),
        ActorId.From(actorHint),
        FeedPlanningTargets.FeedGate,
        FeedPlanningIntentActions.RequestEarlyFeed,
        version,
        ServerTick.Zero,
        NoIntentParameters.Instance);

    private static void AssertNormalNoOp(NormalFeedPlanningResult result, NormalFeedPlanningNoOpReason reason, ShiftRuntimeState state)
    {
        var noOp = Assert.IsType<NormalFeedPlanningNoOp>(result);
        Assert.Equal(reason, noOp.Reason);
        Assert.Same(state, noOp.State);
    }

    private static void AssertEarlyRejected(EarlyFeedIntentResult result, RejectionReason reason, ShiftRuntimeState state)
    {
        var rejected = Assert.IsType<EarlyFeedIntentRejected>(result);
        Assert.Equal(reason, rejected.Reason);
        Assert.Same(state, rejected.State);
        Assert.Empty(state.ProcessedIntentIds);
    }

    private static void AssertUnsupported(EarlyFeedIntentResult result, EarlyFeedIntentUnsupportedReason reason, ShiftRuntimeState state)
    {
        var unsupported = Assert.IsType<UnsupportedEarlyFeedIntent>(result);
        Assert.Equal(reason, unsupported.Reason);
        Assert.Same(state, unsupported.State);
        Assert.Empty(state.ProcessedIntentIds);
    }

    private static DomainEventDraft Draft(string suffix, IntentId? cause = null) => new(EventId.From($"scheduler_{suffix}"), EventTypeId.From("test.scheduler.feed"), new SchedulerPayload(suffix), cause);

    private static void AssertCommitted(JournaledMutationCommitResult result, long sequence, long version, IntentId? cause)
    {
        var committed = Assert.IsType<JournaledMutationCommitted>(result);
        Assert.Equal(sequence, committed.Envelope.Sequence.Value);
        Assert.Equal(version, committed.Envelope.StateVersionAfter.Value);
        Assert.Equal(cause, committed.Envelope.CausedByIntentId);
    }

    private static InMemoryEventJournal AlignedJournal(ShiftRuntimeState state, ServerTick tick)
    {
        var journal = new InMemoryEventJournal(state.ShiftId);
        for (var version = 1L; version <= state.StateVersion.Value; version++)
        {
            journal.Append(new EventEnvelope
            {
                ShiftId = state.ShiftId,
                EventId = EventId.From($"seed_{version}"),
                Sequence = EventSequence.From(version),
                ServerTick = tick,
                StateVersionAfter = StateVersion.From(version),
                EventType = EventTypeId.From("test.seed"),
                Payload = new SchedulerPayload($"seed_{version}")
            });
        }

        return journal;
    }

    private sealed record SchedulerPayload(string Value) : IDomainEventPayload;
    private sealed record OtherParameters : IIntentParameters;
}
