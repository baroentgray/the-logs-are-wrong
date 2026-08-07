using System.Collections.Immutable;
using System.Reflection;
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

[Trait("Scope", "TLAW-035")]
public sealed class HostStageSixDerivedExecutionTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static ShiftProfile Learning => Fx.Shift.Profiles[ProfileId.From("learning")];

    private sealed record StageChain(
        HostStageOneCompletionExecution One,
        AcceptedIntentStageExecution Two,
        HostStageThreeDeadlineExecution Three,
        HostStageFourSawExecution Four,
        HostStageFiveFeedExecution Five);

    // ----- API and preflight -----

    [Fact]
    public void Null_default_tick_broken_chain_mismatch_and_invalid_tool_reject_before_execution()
    {
        var chain = PristineChain(ServerTick.Zero);
        var executor = new HostStageSixDerivedExecutor();
        var movement = MovementNoiseRuntimeState.Create(chain.Five.FinalState.ShiftId);
        var lineNoise = LineNoiseRuntimeState.Create(chain.Five.FinalState.ShiftId);
        var progression = HostTickProgressionEvidence.Create(chain.Five.FinalState.ShiftId);
        var lifecycle = ShiftLifecycleRuntimeState.Create(Fx.Shift, ProfileId.From("learning"));

        Assert.Throws<ArgumentNullException>(() => executor.Execute(null!, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, ImmutableHashSet<ItemId>.Empty, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, null!, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, ImmutableHashSet<ItemId>.Empty, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, null!, lineNoise, progression, lifecycle, ImmutableHashSet<ItemId>.Empty, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, null!, progression, lifecycle, ImmutableHashSet<ItemId>.Empty, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, null!, lifecycle, ImmutableHashSet<ItemId>.Empty, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, null!, ImmutableHashSet<ItemId>.Empty, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, null!, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, ImmutableHashSet<ItemId>.Empty, default, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, ImmutableHashSet<ItemId>.Empty, ServerTick.Zero, null!, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, ImmutableHashSet<ItemId>.Empty, ServerTick.Zero, Fx.Shift.Scheduler, null!, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, ImmutableHashSet<ItemId>.Empty, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, null!));

        // Invalid active-tool entry.
        var badTools = ImmutableHashSet.Create(default(ItemId));
        Assert.Throws<ArgumentException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, badTools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));

        // Broken state chain: substitute a stage three from an independent chain.
        var other = PristineChain(ServerTick.Zero);
        Assert.Throws<ArgumentException>(() => executor.Execute(chain.One, chain.Two, other.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, ImmutableHashSet<ItemId>.Empty, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));

        // Stage-5 source evidence not exact stage-1/stage-3 origin.
        Assert.Throws<ArgumentException>(() => executor.Execute(other.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, ImmutableHashSet<ItemId>.Empty, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));

        // Batch tick mismatch.
        var mismatchTick = PristineChain(ServerTick.From(4));
        Assert.Throws<ArgumentException>(() => executor.Execute(mismatchTick.One, mismatchTick.Two, mismatchTick.Three, mismatchTick.Four, mismatchTick.Five, movement, lineNoise, progression, lifecycle, ImmutableHashSet<ItemId>.Empty, ServerTick.From(5), Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
    }

    [Fact]
    public void Executor_execute_signature_is_exact_and_takes_no_shift_or_quota_runtime_state()
    {
        var execute = Assert.Single(
            typeof(HostStageSixDerivedExecutor).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => method.Name == "Execute");

        Assert.Equal(typeof(HostStageSixDerivedExecution), execute.ReturnType);
        Assert.Equal(
            new[]
            {
                typeof(HostStageOneCompletionExecution), typeof(AcceptedIntentStageExecution),
                typeof(HostStageThreeDeadlineExecution), typeof(HostStageFourSawExecution), typeof(HostStageFiveFeedExecution),
                typeof(MovementNoiseRuntimeState), typeof(LineNoiseRuntimeState),
                typeof(HostTickProgressionEvidence), typeof(ShiftLifecycleRuntimeState),
                typeof(ImmutableHashSet<ItemId>), typeof(ServerTick),
                typeof(SchedulerConfiguration), typeof(ShiftConfiguration), typeof(AnomalyCatalog)
            },
            execute.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(execute.GetParameters(), parameter =>
            parameter.ParameterType == typeof(ShiftRuntimeState) ||
            parameter.ParameterType == typeof(QuotaRuntimeState) ||
            parameter.ParameterType == typeof(object) ||
            parameter.ParameterType == typeof(bool) ||
            parameter.ParameterType == typeof(string) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
    }

    // ----- Quiet baseline -----

    [Fact]
    public void Quiet_tick_applies_no_movement_derives_no_jam_stays_quiet_and_advances_the_checkpoint()
    {
        // Tick 0 always performs initial feed admission in this fixture; the genuinely quiet tick is the
        // next one, once nothing remains pending and nothing is due.
        var seed = PristineChain(ServerTick.Zero);
        var zero = Assert.IsType<HostTickCheckpointAdvanced>(Stage6(seed, ServerTick.Zero).Checkpoint);
        var chain = BuildChain(seed.Five.FinalState, ServerTick.From(1), FreshQuota());

        var e = Stage6(chain, ServerTick.From(1), progression: zero.Progression, lifecycle: zero.Receipt.Lifecycle);

        Assert.Same(chain.Five.FinalState, e.InitialShiftState);
        Assert.Empty(e.MovementSteps);
        Assert.Same(e.InitialMovementNoise, e.FinalMovementNoise);
        Assert.Null(e.IntakeAutoFeedJamSource);
        Assert.Null(e.IntakeAutoFeedJam);
        Assert.Same(e.InitialShiftState, e.IntakeAutoFeedJamStep.AfterShiftState);
        Assert.IsType<LineNoiseEvaluatedWithoutChange>(e.LineNoiseEvaluation);
        Assert.Equal(LineNoise.QUIET, e.LineNoiseEvaluation.State.Current);
        Assert.IsType<ConfirmationTestConditionNoChange>(e.Confirmation);
        var advanced = Assert.IsType<HostTickCheckpointAdvanced>(e.Checkpoint);
        Assert.Same(e.FinalShiftState, advanced.Receipt.ShiftState);
        Assert.Same(chain.Four.FinalQuotaState, e.QuotaState);
        Assert.Same(chain.Four.FinalQuotaState, advanced.Receipt.QuotaState);
    }

    [Fact]
    public void Executor_trace_satisfies_the_exact_shift_state_and_line_noise_reference_chain()
    {
        var e = Stage6(PristineChain(ServerTick.Zero), ServerTick.Zero);

        Assert.Same(e.InitialShiftState, e.IntakeAutoFeedJamStep.BeforeShiftState);
        Assert.Same(e.IntakeAutoFeedJamStep.AfterShiftState, e.FeedGateJamStep.BeforeShiftState);
        Assert.Same(e.FeedGateJamStep.AfterShiftState, e.LineNoiseStep.ShiftState);
        Assert.Same(e.FeedGateJamStep.AfterShiftState, e.ConfirmationStep.BeforeShiftState);
        Assert.Same(e.ConfirmationStep.AfterShiftState, e.CheckpointStep.PostStageShift);
        Assert.Same(e.LineNoiseStep.Result, e.ConfirmationStep.ConsumedLineNoise);
        Assert.Same(e.FinalMovementNoise, e.LineNoiseStep.MovementNoiseState);
    }

    [Fact]
    public void Constructor_rejects_a_contradictory_reference_chain_from_valid_public_lower_level_results()
    {
        var a = Stage6(PristineChain(ServerTick.Zero), ServerTick.Zero);
        var b = Stage6(PristineChain(ServerTick.Zero), ServerTick.Zero);
        var constructor = typeof(HostStageSixDerivedExecution).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
        var valid = new object?[]
        {
            a.InitialShiftState, a.InitialMovementNoise, a.MovementSteps,
            a.IntakeAutoFeedJamStep, a.FeedGateJamStep, a.LineNoiseStep, a.ConfirmationStep, a.CheckpointStep,
            a.Progression, a.Lifecycle, a.ActiveTools, a.CurrentTick
        };

        Assert.NotNull(constructor.Invoke(valid));

        var broken = (object?[])valid.Clone();
        broken[4] = b.FeedGateJamStep;
        Assert.True(a.IntakeAutoFeedJamStep.AfterShiftState.ValueEquals(b.FeedGateJamStep.BeforeShiftState));
        Assert.False(ReferenceEquals(a.IntakeAutoFeedJamStep.AfterShiftState, b.FeedGateJamStep.BeforeShiftState));
        var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(broken));
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    // ----- Movement aggregation -----

    [Fact]
    public void Stage_two_manual_movement_is_applied_with_currentTick_as_the_accepted_at_tick()
    {
        var s0 = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var chain = BuildChain(s0, ServerTick.From(10), FreshQuota(),
            one => RouteBatch(one.FinalState, ServerTick.From(10), "log_01", LogIntentActions.RouteToProcedure));

        var e = Stage6(chain, ServerTick.From(10));

        var step = Assert.Single(e.MovementSteps);
        var applied = Assert.IsType<MovementNoiseApplied>(step.Result);
        Assert.Equal(ServerTick.From(10), applied.State.LastAcceptedMovement!.AcceptedAt);
        Assert.Equal(MovementNoiseAcceptedSource.ManualLogIntent, applied.State.LastAcceptedMovement!.Source);
        Assert.True(applied.State.IsActiveAt(ServerTick.From(10)));
    }

    [Fact]
    public void Saw_completion_and_start_apply_movement_in_that_order_within_the_same_tick()
    {
        var scheduler = Fx.Shift.Scheduler with { SawCycleSeconds = 5 };
        var s0 = ActiveSawWithQueuedSuccessor(scheduler, out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota(), scheduler: scheduler);

        Assert.IsType<SawCycleCompleted>(chain.Four.Completion.Result);
        Assert.IsType<SawCycleStarted>(chain.Four.Start.Result);

        var e = Stage6(chain, dueTick, scheduler: scheduler);

        Assert.Equal(2, e.MovementSteps.Length);
        Assert.Equal(MovementNoiseAcceptedSource.SawCycleCompleted, Assert.IsType<MovementNoiseApplied>(e.MovementSteps[0].Result).State.LastAcceptedMovement!.Source);
        Assert.Equal(MovementNoiseAcceptedSource.SawCycleStarted, Assert.IsType<MovementNoiseApplied>(e.MovementSteps[1].Result).State.LastAcceptedMovement!.Source);
    }

    [Fact]
    public void Saw_completion_movement_pulse_keeps_the_line_loud_after_the_active_cycle_disappears()
    {
        var scheduler = Fx.Shift.Scheduler with { SawCycleSeconds = 5 };
        var s0 = ActiveSawAlone(scheduler, out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota(), scheduler: scheduler);

        Assert.IsType<SawCycleCompleted>(chain.Four.Completion.Result);
        Assert.IsType<SawCycleStartNoQueuedOwner>(chain.Four.Start.Result);

        var e = Stage6(chain, dueTick, scheduler: scheduler);

        Assert.Null(e.FinalShiftState.ActiveSawCycle);
        Assert.False(e.LineNoiseStep.Result.State.LatestSources.SawActive);
        Assert.True(e.LineNoiseStep.Result.State.LatestSources.MovementNoiseActive);
        Assert.Equal(LineNoise.LOUD, e.LineNoiseEvaluation.State.Current);
    }

    [Fact]
    public void Repair_pending_transition_and_default_auto_route_movements_are_applied_when_present()
    {
        var s0 = RepairingFeedGate("log_01", "log_02", out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota());

        Assert.IsType<RepairPendingTransitionExecuted>(chain.Five.RepairExecution);

        var e = Stage6(chain, dueTick);

        var repairStep = Assert.Single(e.MovementSteps.Where(step => Assert.IsType<MovementNoiseApplied>(step.Result).State.LastAcceptedMovement!.Source == MovementNoiseAcceptedSource.RepairPendingTransition));
        Assert.NotNull(repairStep);
    }

    // ----- Jam composition -----

    [Fact]
    public void Feed_due_placed_at_feed_gate_derives_the_feed_gate_jam_with_no_intake_auto_feed_source()
    {
        var s0 = PendingEarlyFeedWithOccupiedIntake(out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota());

        var e = Stage6(chain, dueTick);

        Assert.Null(e.IntakeAutoFeedJamSource);
        Assert.Null(e.IntakeAutoFeedJam);
        var derived = Assert.IsType<FeedGateJamDerived>(e.FeedGateJam);
        Assert.Equal(JamCause.FEED_GATE_BLOCKED, derived.Cause);
        Assert.Equal(LineState.LINE_JAMMED, e.FinalShiftState.Line.State);
        Assert.Equal(JamCause.FEED_GATE_BLOCKED, e.FinalShiftState.Line.Cause);
        // The placing feed-due movement was applied before jam derivation.
        Assert.Contains(e.MovementSteps, step => Assert.IsType<MovementNoiseApplied>(step.Result).State.LastAcceptedMovement!.Source == MovementNoiseAcceptedSource.FeedDueResolved);
    }

    [Fact]
    public void Blocked_default_route_alone_derives_the_intake_auto_feed_jam()
    {
        var scheduler = Fx.Shift.Scheduler with { SawCycleSeconds = 200 };
        var s0 = ExpiringDeadlineWithBlockedSawQueue(scheduler, out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota(), scheduler: scheduler);

        var blocked = Assert.IsType<DefaultIntakeAutoRouteBlocked>(chain.Five.DefaultRoute);
        Assert.Equal(DefaultIntakeAutoRouteFollowUp.IntakeAutoFeedJamDerivationRequired, blocked.FollowUp);

        var e = Stage6(chain, dueTick, scheduler: scheduler);

        Assert.Same(blocked, e.IntakeAutoFeedJamSource);
        var entered = Assert.IsType<IntakeAutoFeedJamEntered>(e.IntakeAutoFeedJam);
        Assert.Equal(JamCause.INTAKE_AUTOFEED_BLOCKED, entered.Cause);
        Assert.Equal("log_01", entered.LogId.ToString());
        Assert.IsType<FeedGateJamDerivationLineNotClear>(e.FeedGateJam);
        Assert.Equal(LineState.LINE_JAMMED, e.FinalShiftState.Line.State);
        Assert.Equal(JamCause.INTAKE_AUTOFEED_BLOCKED, e.FinalShiftState.Line.Cause);
    }

    [Fact]
    public void Dual_eligible_jam_precedence_derives_intake_auto_feed_first_and_no_second_jam_layers()
    {
        var scheduler = Fx.Shift.Scheduler with { SawCycleSeconds = 200 };
        var s0 = DualEligibleJamSeed(scheduler, out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota(), scheduler: scheduler);

        // Confirm the stage-5 shape: blocked default route (line clear when evaluated) plus a distinct
        // due feed subsequently placed at FEED_GATE because intake remains occupied.
        var blocked = Assert.IsType<DefaultIntakeAutoRouteBlocked>(chain.Five.DefaultRoute);
        Assert.Equal(DefaultIntakeAutoRouteFollowUp.IntakeAutoFeedJamDerivationRequired, blocked.FollowUp);
        var resolved = Assert.IsType<FeedDueResolved>(chain.Five.FeedDue);
        Assert.Equal(FeedDueDisposition.PlacedAtFeedGate, resolved.Disposition);
        Assert.NotEqual("log_01", resolved.ConsumedSchedule.LogId.ToString());
        Assert.Equal(LineState.LINE_CLEAR, chain.Five.FinalState.Line.State);

        var e = Stage6(chain, dueTick, scheduler: scheduler);

        // Intake-auto-feed derives first and enters the jam for the blocked intake owner.
        var entered = Assert.IsType<IntakeAutoFeedJamEntered>(e.IntakeAutoFeedJam);
        Assert.Equal(JamCause.INTAKE_AUTOFEED_BLOCKED, entered.Cause);
        Assert.Equal("log_01", entered.LogId.ToString());

        // Feed-gate derivation observes the now non-clear line and retains it — no second jam is layered.
        Assert.IsType<FeedGateJamDerivationLineNotClear>(e.FeedGateJam);
        Assert.Equal(LineState.LINE_JAMMED, e.FinalShiftState.Line.State);
        Assert.Equal(JamCause.INTAKE_AUTOFEED_BLOCKED, e.FinalShiftState.Line.Cause);
        Assert.Equal("log_01", e.FinalShiftState.Line.PendingLogId!.Value.ToString());
    }

    // ----- Line noise -----

    [Fact]
    public void Manual_movement_alone_makes_the_line_loud()
    {
        var s0 = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var chain = BuildChain(s0, ServerTick.From(10), FreshQuota(),
            one => RouteBatch(one.FinalState, ServerTick.From(10), "log_01", LogIntentActions.RouteToProcedure));

        var e = Stage6(chain, ServerTick.From(10));

        Assert.False(e.LineNoiseStep.Result.State.LatestSources.SawActive);
        Assert.True(e.LineNoiseStep.Result.State.LatestSources.MovementNoiseActive);
        Assert.Equal(LineNoise.LOUD, e.LineNoiseEvaluation.State.Current);
    }

    [Fact]
    public void An_active_saw_cycle_makes_the_line_loud()
    {
        var s0 = RuntimeFixture.MoveHost(RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01"), "log_01", LogState.QUEUED_FOR_SAW);
        var chain = BuildChain(s0, ServerTick.From(10), FreshQuota());

        Assert.IsType<SawCycleStarted>(chain.Four.Start.Result);

        var e = Stage6(chain, ServerTick.From(10));

        Assert.True(e.LineNoiseStep.Result.State.LatestSources.SawActive);
        Assert.Equal(LineNoise.LOUD, e.LineNoiseEvaluation.State.Current);
    }

    // ----- Confirmation -----

    [Fact]
    public void An_active_confirmation_is_paused_by_same_tick_line_noise_from_a_new_saw_cycle()
    {
        var tools = ImmutableHashSet.Create(ItemId.From("sound_meter"));
        // Queue log_01 for the saw before log_03 occupies INTAKE for its confirmation, since INTAKE has
        // capacity for only one owner at a time.
        var queued = RuntimeFixture.MoveHost(RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01"), "log_01", LogState.QUEUED_FOR_SAW);
        var intake = RuntimeFixture.MoveToIntake(queued, "log_03");
        var quietRuntime = LineNoiseRuntimeState.Create(intake.ShiftId);
        var started = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(
            intake, LogId.From("log_03"), tools, ServerTick.From(5), quietRuntime, Fx.Anomalies)).State;
        var activeBeforeTick = Assert.IsType<ActiveConfirmationTest>(started.ActiveConfirmationTest);
        Assert.True(activeBeforeTick.IsRunning);
        Assert.True(activeBeforeTick.DueAt!.Value > ServerTick.From(6), "confirmation must still be running (not yet due) at the tested tick");

        var chain = BuildChain(started, ServerTick.From(6), FreshQuota());
        Assert.IsType<SawCycleStarted>(chain.Four.Start.Result);

        var e = Stage6(chain, ServerTick.From(6), activeTools: tools);

        Assert.Equal(LineNoise.LOUD, e.LineNoiseEvaluation.State.Current);
        var updated = Assert.IsType<ConfirmationTestConditionUpdated>(e.Confirmation);
        var active = Assert.IsType<ActiveConfirmationTest>(updated.State.ActiveConfirmationTest);
        Assert.False(active.IsRunning);
    }

    // ----- Checkpoint -----

    [Fact]
    public void Checkpoint_newly_completes_when_all_manifest_logs_reach_terminal_state()
    {
        var lifecycle = ShiftLifecycleRuntimeState.Create(Fx.Shift, ProfileId.From("pressure"));
        var chain = BuildChain(AllLogsWrittenOff(), ServerTick.Zero, FreshQuota());

        var e = Stage6(chain, ServerTick.Zero, lifecycle: lifecycle);

        var completed = Assert.IsType<HostTickCheckpointAdvanced>(e.Checkpoint);
        var newlyCompleted = Assert.IsType<ShiftCompletionNewlyCompleted>(completed.Receipt.Evaluation);
        Assert.True(completed.Receipt.ShiftCompleted);
        Assert.Equal(ShiftCompletionReason.AllLogsTerminal, newlyCompleted.Completion.Reason);
    }

    [Fact]
    public void Checkpoint_typed_rejection_is_retained_without_translation()
    {
        var chain = PristineChain(ServerTick.From(1));
        var progression = HostTickProgressionEvidence.Create(chain.Five.FinalState.ShiftId);

        var e = Stage6(chain, ServerTick.From(1), progression: progression);

        var rejected = Assert.IsType<HostTickCheckpointRejected>(e.Checkpoint);
        Assert.Equal(HostTickCheckpointRejectionReason.SkippedTick, rejected.Reason);
        Assert.Same(progression, e.Progression);
    }

    // ----- Determinism -----

    [Fact]
    public void Independent_equivalent_chains_produce_value_equivalent_projections_and_state()
    {
        var first = Stage6(PristineChain(ServerTick.Zero), ServerTick.Zero);
        var second = Stage6(PristineChain(ServerTick.Zero), ServerTick.Zero);

        Assert.Equal(first.LineNoiseEvaluation.GetType(), second.LineNoiseEvaluation.GetType());
        Assert.Equal(first.Confirmation.GetType(), second.Confirmation.GetType());
        Assert.Equal(first.Checkpoint.GetType(), second.Checkpoint.GetType());
        Assert.True(first.FinalShiftState.ValueEquals(second.FinalShiftState));
        Assert.NotSame(first.FinalShiftState, second.FinalShiftState);
    }

    // ----- Exception propagation -----

    [Fact]
    public void A_delegated_service_exception_propagates_without_a_partial_result()
    {
        var chain = PristineChain(ServerTick.Zero);
        var badConfiguration = Fx.Shift.Scheduler with { MovementNoiseSeconds = 0 };
        var s0 = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var brokenChain = BuildChain(s0, ServerTick.From(10), FreshQuota(),
            one => RouteBatch(one.FinalState, ServerTick.From(10), "log_01", LogIntentActions.RouteToProcedure));
        var initial = brokenChain.Five.FinalState;

        Assert.Throws<ArgumentOutOfRangeException>(() => new HostStageSixDerivedExecutor().Execute(
            brokenChain.One, brokenChain.Two, brokenChain.Three, brokenChain.Four, brokenChain.Five,
            MovementNoiseRuntimeState.Create(initial.ShiftId), LineNoiseRuntimeState.Create(initial.ShiftId),
            HostTickProgressionEvidence.Create(initial.ShiftId), ShiftLifecycleRuntimeState.Create(Fx.Shift, ProfileId.From("learning")),
            ImmutableHashSet<ItemId>.Empty, ServerTick.From(10), badConfiguration, Fx.Shift, Fx.Anomalies));

        Assert.Equal(LogState.AT_PROCEDURE, Log(initial, "log_01").State);
    }

    // ----- Helpers -----

    private static QuotaRuntimeState FreshQuota() => QuotaRuntimeState.Create(Fx.Shift);

    private static HostStageSixDerivedExecution Stage6(
        StageChain chain,
        ServerTick tick,
        MovementNoiseRuntimeState? movementNoise = null,
        LineNoiseRuntimeState? lineNoise = null,
        ImmutableHashSet<ItemId>? activeTools = null,
        SchedulerConfiguration? scheduler = null,
        HostTickProgressionEvidence? progression = null,
        ShiftLifecycleRuntimeState? lifecycle = null)
    {
        var shiftId = chain.Five.FinalState.ShiftId;
        return new HostStageSixDerivedExecutor().Execute(
            chain.One, chain.Two, chain.Three, chain.Four, chain.Five,
            movementNoise ?? MovementNoiseRuntimeState.Create(shiftId),
            lineNoise ?? LineNoiseRuntimeState.Create(shiftId),
            progression ?? HostTickProgressionEvidence.Create(shiftId),
            lifecycle ?? ShiftLifecycleRuntimeState.Create(Fx.Shift, ProfileId.From("learning")),
            activeTools ?? ImmutableHashSet<ItemId>.Empty,
            tick,
            scheduler ?? Fx.Shift.Scheduler,
            Fx.Shift,
            Fx.Anomalies);
    }

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
        var two = new AcceptedIntentStageExecutor().Execute(one.FinalState, batch, sched);
        var three = new HostStageThreeDeadlineExecutor().Execute(two.FinalState, tick, Fx.Shift.Containment, Fx.Anomalies);
        var four = new HostStageFourSawExecutor().Execute(three.FinalState, quota, tick, sched, Fx.Anomalies);
        var five = new HostStageFiveFeedExecutor().Execute(one, two, three, four, tick, sched, Learning);
        return new StageChain(one, two, three, four, five);
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

    private static ShiftRuntimeState ExpiringDeadlineWithBlockedSawQueue(SchedulerConfiguration scheduler, out ServerTick dueTick)
    {
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

    private static ShiftRuntimeState AllLogsWrittenOff()
    {
        var state = RuntimeFixture.CreateInitialState();
        var logs = state.Logs;
        for (var index = 0; index < logs.Length; index++)
        {
            var log = logs[index];
            logs = logs.SetItem(index, new LogRuntimeState(log.LogId, log.TrueSpecies, log.DeclaredSpecies, log.Anomaly, LogState.HELD_WRITTEN_OFF, log.Flags));
        }

        return CloneWith(state, nameof(ShiftRuntimeState.Logs), logs);
    }

    private static ShiftRuntimeState DualEligibleJamSeed(SchedulerConfiguration scheduler, out ServerTick dueTick)
    {
        // log_03 active in saw (not due), log_02 queued behind it (occupies SAW_QUEUE), log_01 admitted to
        // intake with an expiring deadline, and a distinct early feed (the next scheduled log) due at the
        // exact same tick, still unresolved when stage 5 runs.
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

        var earlyRequestTick = ServerTick.From(dueTick.Value - Fx.Shift.Scheduler.EarlyFeedDelaySeconds);
        var intent = new IntentEnvelope(
            started.State.ShiftId, IntentId.From("distinct_due"), ActorId.From("hint"), FeedPlanningTargets.FeedGate,
            FeedPlanningIntentActions.RequestEarlyFeed, started.State.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var early = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(
            started.State, intent, RuntimeFixture.BoundActor, earlyRequestTick, Fx.Shift.Scheduler));
        Assert.Equal(dueTick, early.Schedule.DueAt);
        Assert.NotEqual("log_01", early.Schedule.LogId.ToString());
        return early.State;
    }

    private static ShiftRuntimeState ActiveSawWithQueuedSuccessor(SchedulerConfiguration scheduler, out ServerTick dueTick)
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.QUEUED_FOR_SAW);
        var started = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(state, ServerTick.From(10), scheduler));
        dueTick = started.Cycle.DueAt;
        var withSuccessor = RuntimeFixture.MoveToIntake(started.State, "log_02");
        return RuntimeFixture.MoveHost(withSuccessor, "log_02", LogState.QUEUED_FOR_SAW);
    }

    private static ShiftRuntimeState ActiveSawAlone(SchedulerConfiguration scheduler, out ServerTick dueTick)
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.QUEUED_FOR_SAW);
        var started = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(state, ServerTick.From(10), scheduler));
        dueTick = started.Cycle.DueAt;
        return started.State;
    }

    private static ShiftRuntimeState RepairingFeedGate(string intakeLogId, string gateLogId, out ServerTick dueTick)
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), intakeLogId);
        state = RuntimeFixture.MoveHost(state, gateLogId, LogState.AT_FEED_GATE);
        var jammed = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.FEED_GATE_BLOCKED, ServerTick.From(10))).State;
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jammed, ServerTick.From(10), Fx.Shift.Scheduler));
        var unblocked = RuntimeFixture.MoveHost(repairing.State, intakeLogId, LogState.QUEUED_FOR_SAW);
        dueTick = repairing.Hold.DueAt;
        return unblocked;
    }

    private static T CloneWith<T>(T source, string name, object? value) where T : class
    {
        var clone = Assert.IsType<T>(typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(source, null));
        FindField(typeof(T), name)!.SetValue(clone, value);
        return clone;
    }

    private static FieldInfo? FindField(Type type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? current.GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null)
            {
                return field;
            }
        }

        return null;
    }

    private static LogRuntimeState Log(ShiftRuntimeState state, string logId)
    {
        Assert.True(state.TryGetLog(LogId.From(logId), out var log));
        return log;
    }
}
