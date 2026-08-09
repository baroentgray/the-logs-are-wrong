using System.Reflection;
using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-034")]
public sealed class HostStageFiveFeedExecutionTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static ShiftProfile Learning => Fx.Shift.Profiles[ProfileId.From("learning")];

    private sealed record StageChain(
        HostStageOneCompletionExecution One,
        AcceptedIntentStageExecution Two,
        HostStageThreeDeadlineExecution Three,
        HostStageFourSawExecution Four);

    // ----- API and construction -----

    [Fact]
    public void Null_default_tick_broken_chain_and_mismatch_reject_before_execution()
    {
        var chain = PristineChain(ServerTick.Zero);
        var executor = new HostStageFiveFeedExecutor();

        Assert.Throws<ArgumentNullException>(() => executor.Execute(null!, chain.Two, chain.Three, chain.Four, ServerTick.Zero, Fx.Shift.Scheduler, Learning));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, null!, chain.Three, chain.Four, ServerTick.Zero, Fx.Shift.Scheduler, Learning));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, ServerTick.Zero, null!, Learning));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, ServerTick.Zero, Fx.Shift.Scheduler, null!));
        Assert.Throws<ArgumentException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, default, Fx.Shift.Scheduler, Learning));

        // Broken state chain: substitute a stage three from an independent chain.
        var other = PristineChain(ServerTick.Zero);
        Assert.Throws<ArgumentException>(() => executor.Execute(chain.One, chain.Two, other.Three, chain.Four, ServerTick.Zero, Fx.Shift.Scheduler, Learning));

        // Batch tick mismatch.
        var mismatchTick = PristineChain(ServerTick.From(4));
        Assert.Throws<ArgumentException>(() => executor.Execute(mismatchTick.One, mismatchTick.Two, mismatchTick.Three, mismatchTick.Four, ServerTick.From(5), Fx.Shift.Scheduler, Learning));
    }

    [Fact]
    public void Executor_execute_signature_is_exact_and_takes_no_shift_runtime_state()
    {
        var execute = Assert.Single(
            typeof(HostStageFiveFeedExecutor).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => method.Name == "Execute");

        Assert.Equal(typeof(HostStageFiveFeedExecution), execute.ReturnType);
        Assert.Equal(
            new[]
            {
                typeof(HostStageOneCompletionExecution), typeof(AcceptedIntentStageExecution),
                typeof(HostStageThreeDeadlineExecution), typeof(HostStageFourSawExecution),
                typeof(ServerTick), typeof(SchedulerConfiguration), typeof(ShiftProfile)
            },
            execute.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(execute.GetParameters(), parameter =>
            parameter.ParameterType == typeof(ShiftRuntimeState) ||
            parameter.ParameterType == typeof(object) ||
            parameter.ParameterType == typeof(bool) ||
            parameter.ParameterType == typeof(string) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
    }

    [Fact]
    public void Result_is_sealed_immutable_and_non_publicly_constructible()
    {
        var publicInstance = BindingFlags.Public | BindingFlags.Instance;
        var type = typeof(HostStageFiveFeedExecution);

        Assert.True(type.IsSealed);
        Assert.Empty(type.GetConstructors(publicInstance));
        Assert.Empty(type.GetFields(publicInstance));
        Assert.All(type.GetProperties(publicInstance), property => Assert.Null(property.SetMethod));
    }

    // ----- Initial tick -----

    [Fact]
    public void Initial_tick_schedules_resolves_and_starts_deadline_in_one_tick()
    {
        var chain = PristineChain(ServerTick.Zero);
        var initial = chain.Four.FinalShiftState;

        var execution = Stage5(chain, ServerTick.Zero);

        Assert.Same(initial, execution.InitialState);
        Assert.IsType<InitialFeedScheduled>(execution.InitialFeedPlanning);
        Assert.Null(execution.RepairExecution);
        Assert.Null(execution.DefaultRoute);
        Assert.False(execution.GenericNormalPlanningRequired);
        var resolved = Assert.IsType<FeedDueResolved>(execution.FeedDue);
        Assert.Equal(FeedDueDisposition.AdmittedToIntake, resolved.Disposition);
        Assert.IsType<IntakeDeadlineStarted>(execution.OrdinaryDeadlineStart);
        Assert.Same(execution.OrdinaryDeadlineStart!.State, execution.FinalState);
        Assert.Equal(LogState.AT_INTAKE, Log(execution.FinalState, "log_01").State);
        Assert.NotNull(execution.FinalState.ActiveIntakeDeadline);
        // Exact state-version progression: initial planning, feed-due resolution, deadline start (three existing services).
        Assert.Equal(StateVersion.Zero, execution.InitialState.StateVersion);
        Assert.Equal(execution.InitialState.StateVersion.Next().Next().Next(), execution.FinalState.StateVersion);
    }

    // ----- MEDIUM 1: self-defending closed trace -----

    [Fact]
    public void Executor_trace_satisfies_the_exact_before_after_reference_chain()
    {
        var e = Stage5(PristineChain(ServerTick.Zero), ServerTick.Zero);

        Assert.Same(e.InitialState, e.InitialFeedPlanningStep.BeforeState);
        Assert.Same(e.InitialFeedPlanningStep.AfterState, e.RepairStep.BeforeState);
        Assert.Same(e.RepairStep.AfterState, e.RepairFollowUpStep.BeforeState);
        Assert.Same(e.RepairFollowUpStep.AfterState, e.DefaultRouteStep.BeforeState);
        Assert.Same(e.DefaultRouteStep.AfterState, e.GenericNormalPlanningStep.BeforeState);
        Assert.Same(e.GenericNormalPlanningStep.AfterState, e.FeedDueStep.BeforeState);
        Assert.Same(e.FeedDueStep.AfterState, e.OrdinaryDeadlineStartStep.BeforeState);
        Assert.Same(e.OrdinaryDeadlineStartStep.AfterState, e.FinalState);
        // Conditional steps that did not execute pass their exact before-state through.
        Assert.Same(e.RepairStep.BeforeState, e.RepairStep.AfterState);
        Assert.Same(e.RepairFollowUpStep.BeforeState, e.RepairFollowUpStep.AfterState);
        Assert.Same(e.DefaultRouteStep.BeforeState, e.DefaultRouteStep.AfterState);
        // Executed steps derive their after-state only from the result state.
        Assert.Same(e.InitialFeedPlanningStep.Result.State, e.InitialFeedPlanningStep.AfterState);
        Assert.Same(e.FeedDueStep.Result.State, e.FeedDueStep.AfterState);
        Assert.Same(e.OrdinaryDeadlineStartStep.Result!.State, e.OrdinaryDeadlineStartStep.AfterState);
    }

    [Fact]
    public void Stage_execution_rejects_a_value_equivalent_but_non_reference_equal_step_chain()
    {
        var a = Stage5(PristineChain(ServerTick.Zero), ServerTick.Zero);
        var b = Stage5(PristineChain(ServerTick.Zero), ServerTick.Zero);
        var constructor = typeof(HostStageFiveFeedExecution).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
        var valid = new object?[]
        {
            a.InitialState, a.LineRepairSource, a.IntakeExpirationSource,
            a.InitialFeedPlanningStep, a.RepairStep, a.RepairFollowUpStep, a.DefaultRouteStep,
            a.GenericNormalPlanningStep, a.FeedDueStep, a.OrdinaryDeadlineStartStep
        };

        // The exact valid trace reconstructs.
        Assert.NotNull(constructor.Invoke(valid));

        // Substituting B's value-equivalent-but-non-reference-equal feed-due step breaks the exact chain.
        var broken = (object?[])valid.Clone();
        broken[8] = b.FeedDueStep;
        Assert.True(a.GenericNormalPlanningStep.AfterState.ValueEquals(b.FeedDueStep.BeforeState));
        Assert.False(ReferenceEquals(a.GenericNormalPlanningStep.AfterState, b.FeedDueStep.BeforeState));
        var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(broken));
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void Stage_steps_reject_internally_contradictory_shapes()
    {
        var before = RuntimeFixture.CreateInitialState();
        var deadlineResult = Stage5(PristineChain(ServerTick.Zero), ServerTick.Zero).OrdinaryDeadlineStart!;
        var vacancyChain = BuildChain(
            RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01"), ServerTick.From(10), FreshQuota(),
            one => RouteBatch(one.FinalState, ServerTick.From(10), "log_01", LogIntentActions.RouteToProcedure));
        var normalResult = Stage5(vacancyChain, ServerTick.From(10)).GenericNormalPlanning!;

        var followUpConstructor = typeof(RepairFollowUpStageStep).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
        AssertConstructorThrows(followUpConstructor, new object?[] { before, deadlineResult, normalResult });

        var genericConstructor = typeof(GenericNormalFeedPlanningStageStep).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
        AssertConstructorThrows(genericConstructor, new object?[] { before, true, null });
        AssertConstructorThrows(genericConstructor, new object?[] { before, false, normalResult });
    }

    // ----- MEDIUM 2: same-tick expiration routes owner before an independently due feed -----

    [Fact]
    public void Same_tick_deadline_expiration_routes_owner_before_due_feed_admission()
    {
        var s0 = ExpiringDeadlineWithIndependentDueFeed(out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota());

        Assert.IsType<IntakeDeadlineExpired>(chain.Three.IntakeDeadline.Result);

        var e = Stage5(chain, dueTick);

        Assert.Same(chain.Four.FinalShiftState, e.InitialState);
        // The route runs before feed-due resolution and vacates intake.
        Assert.IsType<DefaultIntakeAutoRouteApplied>(e.DefaultRoute);
        Assert.NotEqual(LogState.AT_INTAKE, Log(e.DefaultRouteStep.AfterState, "log_01").State);
        Assert.Same(e.DefaultRouteStep.AfterState, e.GenericNormalPlanningStep.BeforeState);
        // The pre-existing pending feed remains owned by the existing planner (a no-op that does not consume it).
        Assert.True(e.GenericNormalPlanningRequired);
        Assert.IsType<NormalFeedPlanningNoOp>(e.GenericNormalPlanning);
        // Feed-due sees the post-route state and admits the distinct pending owner directly to intake.
        var resolved = Assert.IsType<FeedDueResolved>(e.FeedDue);
        Assert.Equal(FeedDueDisposition.AdmittedToIntake, resolved.Disposition);
        Assert.Equal("log_02", resolved.ConsumedSchedule.LogId.ToString());
        Assert.Equal(LogState.AT_INTAKE, Log(e.FinalState, "log_02").State);
        Assert.IsType<IntakeDeadlineStarted>(e.OrdinaryDeadlineStart);
        // No stale feed-gate placement.
        Assert.NotEqual(FeedDueDisposition.PlacedAtFeedGate, resolved.Disposition);
    }

    // ----- MEDIUM 3: blocked default route -----

    [Fact]
    public void Blocked_default_route_is_retained_without_jam_or_generic_vacancy_trigger()
    {
        var scheduler = Fx.Shift.Scheduler with { SawCycleSeconds = 200 };
        var s0 = ExpiringDeadlineWithBlockedSawQueue(scheduler, out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota(), scheduler: scheduler);

        Assert.IsType<IntakeDeadlineExpired>(chain.Three.IntakeDeadline.Result);
        Assert.IsType<SawCycleNotDue>(chain.Four.Completion.Result);
        Assert.IsType<SawCycleStartAlreadyActive>(chain.Four.Start.Result);

        var e = Stage5(chain, dueTick, scheduler);

        var blocked = Assert.IsType<DefaultIntakeAutoRouteBlocked>(e.DefaultRoute);
        Assert.Equal(DefaultIntakeAutoRouteBlockReason.SawQueueOccupied, blocked.Reason);
        Assert.Equal(LogState.AT_INTAKE, Log(e.FinalState, "log_01").State);
        // A blocked route is not a vacancy trigger.
        Assert.False(e.GenericNormalPlanningRequired);
        Assert.Null(e.GenericNormalPlanning);
        // Stage 5 derives no jam; line remains unchanged and clear.
        Assert.Equal(LineState.LINE_CLEAR, e.FinalState.Line.State);
        Assert.Null(e.FinalState.Line.Cause);
        // Feed-due still runs per existing rules.
        Assert.IsType<FeedDueNoPendingFeed>(e.FeedDue);
    }

    // ----- Feed due paths -----

    [Fact]
    public void Existing_pending_feed_not_due_is_preserved()
    {
        var s0 = PendingNormalFeed(dueAt: 15);
        var chain = BuildChain(s0, ServerTick.From(12), FreshQuota());

        var execution = Stage5(chain, ServerTick.From(12));

        Assert.IsType<InitialFeedPlanningNoOp>(execution.InitialFeedPlanning);
        Assert.IsType<FeedDueNotDueYet>(execution.FeedDue);
        Assert.Null(execution.OrdinaryDeadlineStart);
        Assert.NotNull(execution.FinalState.PendingFeed);
    }

    [Fact]
    public void Due_feed_with_empty_intake_admits_and_starts_ordinary_deadline()
    {
        var s0 = PendingNormalFeed(dueAt: 15);
        var chain = BuildChain(s0, ServerTick.From(15), FreshQuota());

        var execution = Stage5(chain, ServerTick.From(15));

        var resolved = Assert.IsType<FeedDueResolved>(execution.FeedDue);
        Assert.Equal(FeedDueDisposition.AdmittedToIntake, resolved.Disposition);
        Assert.IsType<IntakeDeadlineStarted>(execution.OrdinaryDeadlineStart);
        Assert.Equal(LogState.AT_INTAKE, Log(execution.FinalState, "log_02").State);
    }

    [Fact]
    public void Due_feed_with_occupied_intake_places_at_feed_gate_and_retains_jam_followup_as_data()
    {
        var s0 = PendingEarlyFeedWithOccupiedIntake(out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota());

        var execution = Stage5(chain, dueTick);

        var resolved = Assert.IsType<FeedDueResolved>(execution.FeedDue);
        Assert.Equal(FeedDueDisposition.PlacedAtFeedGate, resolved.Disposition);
        Assert.Equal(FeedDueFollowUpRequirement.FeedGateJamDerivationRequired, resolved.FollowUpRequirement);
        Assert.Null(execution.OrdinaryDeadlineStart);
        Assert.Equal(LogState.AT_FEED_GATE, Log(execution.FinalState, "log_02").State);
        // No jam is derived in stage 5; the follow-up is data only.
        Assert.Equal(LineState.LINE_CLEAR, execution.FinalState.Line.State);
    }

    // ----- Generic normal planning triggers -----

    [Fact]
    public void Stage_two_manual_intake_vacancy_triggers_generic_normal_planning()
    {
        var s0 = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var chain = BuildChain(s0, ServerTick.From(10), FreshQuota(),
            one => RouteBatch(one.FinalState, ServerTick.From(10), "log_01", LogIntentActions.RouteToProcedure));

        var execution = Stage5(chain, ServerTick.From(10));

        Assert.Equal(LogState.AT_PROCEDURE, Log(chain.Two.FinalState, "log_01").State);
        Assert.True(execution.GenericNormalPlanningRequired);
        Assert.IsType<NormalFeedScheduled>(execution.GenericNormalPlanning);
    }

    [Fact]
    public void Stage_two_rejected_transition_does_not_trigger_generic_normal_planning()
    {
        var s0 = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        // log_02 is SCHEDULED, so route_to_saw_queue cannot apply and is rejected.
        var chain = BuildChain(s0, ServerTick.From(10), FreshQuota(),
            one => RouteBatch(one.FinalState, ServerTick.From(10), "log_02", LogIntentActions.RouteToSawQueue));

        var execution = Stage5(chain, ServerTick.From(10));

        Assert.False(execution.GenericNormalPlanningRequired);
        Assert.Null(execution.GenericNormalPlanning);
    }

    // ----- Default intake auto-route -----

    [Fact]
    public void Stage_three_expiration_applies_default_route_before_feed_due()
    {
        var s0 = ActiveIntakeDeadline("log_01", out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota());

        var execution = Stage5(chain, dueTick);

        Assert.IsType<IntakeDeadlineExpired>(chain.Three.IntakeDeadline.Result);
        Assert.IsType<DefaultIntakeAutoRouteApplied>(execution.DefaultRoute);
        Assert.Equal(LogState.QUEUED_FOR_SAW, Log(execution.DefaultRoute!.State, "log_01").State);
        // Vacating intake through the route triggers generic normal planning.
        Assert.True(execution.GenericNormalPlanningRequired);
    }

    [Fact]
    public void Stage_four_queue_release_is_visible_to_the_default_route()
    {
        var s0 = QueuedOwnerPlusIntakeDeadline("log_02", "log_01", out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota());

        // Stage 4 starts the queued owner, freeing SAW_QUEUE for the stage-5 default route.
        Assert.IsType<SawCycleStarted>(chain.Four.Start.Result);
        Assert.Equal(LogState.IN_SAW, Log(chain.Four.FinalShiftState, "log_02").State);

        var execution = Stage5(chain, dueTick);

        Assert.IsType<IntakeDeadlineExpired>(chain.Three.IntakeDeadline.Result);
        Assert.IsType<DefaultIntakeAutoRouteApplied>(execution.DefaultRoute);
        Assert.Equal(LogState.QUEUED_FOR_SAW, Log(execution.FinalState, "log_01").State);
    }

    // ----- Repair continuations -----

    [Fact]
    public void Repair_feed_gate_continuation_starts_repaired_deadline_only()
    {
        var s0 = RepairingFeedGate("log_01", "log_02", out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota());

        var execution = Stage5(chain, dueTick);

        Assert.IsType<LineRepairCompleted>(chain.One.LineRepair.Result);
        var executed = Assert.IsType<RepairPendingTransitionExecuted>(execution.RepairExecution);
        Assert.Equal(RepairPendingTransitionFollowUp.IntakeDeadlineStartRequired, executed.FollowUpRequirement);
        Assert.IsType<IntakeDeadlineStarted>(execution.RepairedDeadlineStart);
        Assert.Null(execution.RepairedNormalPlanning);
        Assert.Equal(LogState.AT_INTAKE, Log(execution.FinalState, "log_02").State);
    }

    [Fact]
    public void Repair_intake_autofeed_continuation_uses_specialized_planner_and_suppresses_generic()
    {
        var s0 = RepairingAutoFeed("log_01", "log_02", out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota());

        var execution = Stage5(chain, dueTick);

        Assert.IsType<LineRepairCompleted>(chain.One.LineRepair.Result);
        var executed = Assert.IsType<RepairPendingTransitionExecuted>(execution.RepairExecution);
        Assert.Equal(RepairPendingTransitionFollowUp.NormalFeedPlanningEvaluationRequired, executed.FollowUpRequirement);
        Assert.NotNull(execution.RepairedNormalPlanning);
        Assert.Null(execution.RepairedDeadlineStart);
        // Generic planner must not run again for the same repaired-auto-feed consequence.
        Assert.False(execution.GenericNormalPlanningRequired);
        Assert.Null(execution.GenericNormalPlanning);
    }

    [Fact]
    public void Repair_no_op_is_retained_and_stage_continues()
    {
        // A LineRepairCompleted with no pending transition executes as a retained no-op and stage 5 continues.
        var s0 = RepairingAutoFeedTargetMoved(out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota());

        var execution = Stage5(chain, dueTick);

        Assert.IsType<LineRepairCompleted>(chain.One.LineRepair.Result);
        Assert.IsType<RepairPendingTransitionNoPendingTransition>(execution.RepairExecution);
        Assert.Null(execution.RepairedDeadlineStart);
        Assert.Null(execution.RepairedNormalPlanning);
        // Line-repair completion still makes generic planning relevant.
        Assert.True(execution.GenericNormalPlanningRequired);
        Assert.NotNull(execution.FeedDue);
    }

    // ----- Determinism -----

    [Fact]
    public void Independent_equivalent_chains_produce_value_equivalent_projections_and_state()
    {
        var first = Stage5(PristineChain(ServerTick.Zero), ServerTick.Zero);
        var second = Stage5(PristineChain(ServerTick.Zero), ServerTick.Zero);

        Assert.Equal(first.InitialFeedPlanning.GetType(), second.InitialFeedPlanning.GetType());
        Assert.Equal(first.FeedDue.GetType(), second.FeedDue.GetType());
        Assert.Equal(first.OrdinaryDeadlineStart!.GetType(), second.OrdinaryDeadlineStart!.GetType());
        Assert.True(first.FinalState.ValueEquals(second.FinalState));
        Assert.NotSame(first.FinalState, second.FinalState);
    }

    // ----- Exception propagation -----

    [Fact]
    public void A_delegated_service_exception_propagates_without_a_partial_result()
    {
        var chain = PristineChain(ServerTick.Zero);
        var invalidProfile = new ShiftProfile(0, 600);
        var initial = chain.Four.FinalShiftState;

        // The ordinary deadline start delegates profile validation; a non-positive intake timeout throws.
        Assert.Throws<ArgumentOutOfRangeException>(() => new HostStageFiveFeedExecutor().Execute(
            chain.One, chain.Two, chain.Three, chain.Four, ServerTick.Zero, Fx.Shift.Scheduler, invalidProfile));

        // Immutable inputs are unchanged.
        Assert.Equal(StateVersion.Zero, initial.StateVersion);
    }

    // ----- Stage separation -----

    [Fact]
    public void Stage_five_derives_no_jam_and_does_no_stage_six_or_seven_work()
    {
        var s0 = ActiveIntakeDeadline("log_01", out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota());

        var execution = Stage5(chain, dueTick);

        // Even after a default route and a placed/normal feed, stage 5 never derives a jam or line-noise change.
        Assert.Equal(LineState.LINE_CLEAR, execution.FinalState.Line.State);
        Assert.Null(execution.FinalState.Line.Cause);
    }

    // ----- Helpers -----

    private static QuotaRuntimeState FreshQuota() => QuotaRuntimeState.Create(Fx.Shift);

    private static HostStageFiveFeedExecution Stage5(StageChain chain, ServerTick tick, SchedulerConfiguration? scheduler = null) =>
        new HostStageFiveFeedExecutor().Execute(chain.One, chain.Two, chain.Three, chain.Four, tick, scheduler ?? Fx.Shift.Scheduler, Learning);

    private static StageChain BuildChain(
        ShiftRuntimeState initialShiftState,
        ServerTick tick,
        QuotaRuntimeState quota,
        Func<HostStageOneCompletionExecution, AcceptedIntentTickBatch>? batchFactory = null,
        SchedulerConfiguration? scheduler = null)
    {
        var sched = scheduler ?? Fx.Shift.Scheduler;
        var one = new HostStageOneCompletionExecutor().Execute(initialShiftState, tick, Fx.Anomalies, Fx.Shift.Containment);
        var batch = batchFactory is null ? EmptyBatch(one.FinalState.ShiftId, tick) : batchFactory(one);
        var two = new AcceptedIntentStageExecutor().Execute(one.FinalState, batch, sched, ImmutableHashSet<ItemId>.Empty, LineNoiseRuntimeState.Create(one.FinalState.ShiftId), Fx.Anomalies, Fx.Shift.Containment);
        var three = new HostStageThreeDeadlineExecutor().Execute(two.FinalState, tick, Fx.Shift.Containment, Fx.Anomalies);
        var four = new HostStageFourSawExecutor().Execute(three.FinalState, quota, tick, sched, Fx.Anomalies);
        return new StageChain(one, two, three, four);
    }

    private static StageChain PristineChain(ServerTick tick) =>
        BuildChain(RuntimeFixture.CreateInitialState(), tick, FreshQuota());

    private static AcceptedIntentTickBatch EmptyBatch(ShiftId shift, ServerTick tick) =>
        AcceptedIntentTickBatchFactory.Create(shift, tick, Array.Empty<AuthoritativeAcceptedIntent>());

    private static AcceptedIntentTickBatch RouteBatch(ShiftRuntimeState state, ServerTick tick, string logId, IntentActionId action)
    {
        var envelope = new IntentEnvelope(
            state.ShiftId, IntentId.From("route"), ActorId.From("hint"), TargetId.From(logId),
            action, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var receipt = new AuthoritativeAcceptedIntent(envelope, RuntimeFixture.BoundActor, tick, ServerReceiveSequence.Zero);
        return AcceptedIntentTickBatchFactory.Create(state.ShiftId, tick, new[] { receipt });
    }

    private static ShiftRuntimeState PendingNormalFeed(long dueAt)
    {
        // Non-pristine, intake empty (log_01 written off), then a normal feed scheduled for the next log.
        var planTick = dueAt - Fx.Shift.Scheduler.NormalFeedDelaySeconds;
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.HELD_WRITTEN_OFF);
        return Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(state, ServerTick.From(planTick), Fx.Shift.Scheduler)).State;
    }

    private static ShiftRuntimeState PendingEarlyFeedWithOccupiedIntake(out ServerTick dueTick)
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var intent = new IntentEnvelope(
            state.ShiftId, IntentId.From("early"), ActorId.From("hint"), FeedPlanningTargets.FeedGate,
            FeedPlanningIntentActions.RequestEarlyFeed, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var scheduled = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(
            state, intent, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Shift.Scheduler));
        dueTick = scheduled.Schedule.DueAt;
        return scheduled.State;
    }

    private static ShiftRuntimeState ActiveIntakeDeadline(string logId, out ServerTick dueTick)
    {
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(
            RuntimeFixture.CreateInitialState(), ServerTick.Zero, Fx.Shift.Scheduler));
        var admission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        var started = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, Learning));
        Assert.Equal(logId, started.Deadline.LogId.ToString());
        dueTick = started.Deadline.DueAt;
        return started.State;
    }

    private static ShiftRuntimeState QueuedOwnerPlusIntakeDeadline(string queuedLogId, string deadlineLogId, out ServerTick dueTick)
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), queuedLogId);
        state = RuntimeFixture.MoveHost(state, queuedLogId, LogState.QUEUED_FOR_SAW);
        var planned = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(state, ServerTick.From(10), Fx.Shift.Scheduler));
        var admission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, planned.Schedule.DueAt));
        Assert.Equal(deadlineLogId, admission.ConsumedSchedule.LogId.ToString());
        var started = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, Learning));
        dueTick = started.Deadline.DueAt;
        return started.State;
    }

    private static ShiftRuntimeState RepairingFeedGate(string intakeLogId, string gateLogId, out ServerTick dueTick)
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), intakeLogId);
        state = RuntimeFixture.MoveHost(state, gateLogId, LogState.AT_FEED_GATE);
        var jammed = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.FEED_GATE_BLOCKED, ServerTick.From(10))).State;
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jammed, ServerTick.From(10), Fx.Shift.Scheduler));
        // Free INTAKE so the repair can complete: move the intake log to the saw queue.
        var unblocked = RuntimeFixture.MoveHost(repairing.State, intakeLogId, LogState.QUEUED_FOR_SAW);
        dueTick = repairing.Hold.DueAt;
        return unblocked;
    }

    private static ShiftRuntimeState RepairingAutoFeed(string intakeLogId, string queuedLogId, out ServerTick dueTick)
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), queuedLogId);
        state = RuntimeFixture.MoveHost(state, queuedLogId, LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveToIntake(state, intakeLogId);
        var jammed = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.INTAKE_AUTOFEED_BLOCKED, ServerTick.From(10))).State;
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jammed, ServerTick.From(10), Fx.Shift.Scheduler));
        // Free SAW_QUEUE so the repair can complete: process the queued log out of the queue.
        var unblocked = RuntimeFixture.MoveHost(repairing.State, queuedLogId, LogState.IN_SAW);
        unblocked = RuntimeFixture.MoveHost(unblocked, queuedLogId, LogState.PROCESSED);
        dueTick = repairing.Hold.DueAt;
        return unblocked;
    }

    private static ShiftRuntimeState RepairingAutoFeedTargetMoved(out ServerTick dueTick)
    {
        // Repairing auto-feed jam whose pending owner is moved before stage 5, so the executed result is a retained no-op.
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_02");
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveToIntake(state, "log_01");
        var jammed = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.INTAKE_AUTOFEED_BLOCKED, ServerTick.From(10))).State;
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jammed, ServerTick.From(10), Fx.Shift.Scheduler));
        // Free saw queue and move the pending owner out of intake so the pending transition no longer applies.
        var unblocked = RuntimeFixture.MoveHost(repairing.State, "log_02", LogState.IN_SAW);
        unblocked = RuntimeFixture.MoveHost(unblocked, "log_02", LogState.PROCESSED);
        unblocked = RuntimeFixture.MoveHost(unblocked, "log_01", LogState.HELD_WRITTEN_OFF);
        dueTick = repairing.Hold.DueAt;
        return unblocked;
    }

    private static ShiftRuntimeState ExpiringDeadlineWithIndependentDueFeed(out ServerTick dueTick)
    {
        // log_01 admitted to intake with a deadline due at 60, plus an independent early feed for log_02 due at 60.
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(
            RuntimeFixture.CreateInitialState(), ServerTick.Zero, Fx.Shift.Scheduler));
        var admission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        var started = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, Learning));
        var intent = new IntentEnvelope(
            started.State.ShiftId, IntentId.From("independent_early"), ActorId.From("hint"), FeedPlanningTargets.FeedGate,
            FeedPlanningIntentActions.RequestEarlyFeed, started.State.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var early = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(
            started.State, intent, RuntimeFixture.BoundActor, ServerTick.From(58), Fx.Shift.Scheduler));
        Assert.Equal(started.Deadline.DueAt, early.Schedule.DueAt);
        dueTick = started.Deadline.DueAt;
        return early.State;
    }

    private static ShiftRuntimeState ExpiringDeadlineWithBlockedSawQueue(SchedulerConfiguration scheduler, out ServerTick dueTick)
    {
        // Active saw cycle (log_03) not due at T, a queued owner (log_02) behind it, and log_01 at intake with an expiring deadline.
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_03");
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.QUEUED_FOR_SAW);
        state = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(state, ServerTick.From(10), scheduler)).State;
        state = RuntimeFixture.MoveToIntake(state, "log_02");
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.QUEUED_FOR_SAW);
        var planned = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(state, ServerTick.From(20), scheduler));
        var admission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, planned.Schedule.DueAt));
        Assert.Equal("log_01", admission.ConsumedSchedule.LogId.ToString());
        var started = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, Learning));
        dueTick = started.Deadline.DueAt;
        return started.State;
    }

    private static void AssertConstructorThrows(ConstructorInfo constructor, object?[] arguments)
    {
        var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(arguments));
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    private static LogRuntimeState Log(ShiftRuntimeState state, string logId)
    {
        Assert.True(state.TryGetLog(LogId.From(logId), out var log));
        return log;
    }
}
