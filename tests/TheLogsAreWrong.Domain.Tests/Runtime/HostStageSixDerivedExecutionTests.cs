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
    private static readonly ProfileId LearningId = ProfileId.From("learning");

    private sealed record StageChain(
        HostStageOneCompletionExecution One,
        AcceptedIntentStageExecution Two,
        HostStageThreeDeadlineExecution Three,
        HostStageFourSawExecution Four,
        HostStageFiveFeedExecution Five);

    // ================= API and preflight =================

    [Fact]
    public void Null_default_tick_broken_chain_mismatch_and_invalid_tool_reject_before_execution()
    {
        var chain = PristineChain(ServerTick.Zero);
        var executor = new HostStageSixDerivedExecutor();
        var shiftId = chain.Five.FinalState.ShiftId;
        var movement = MovementNoiseRuntimeState.Create(shiftId);
        var lineNoise = LineNoiseRuntimeState.Create(shiftId);
        var progression = HostTickProgressionEvidence.Create(shiftId);
        var lifecycle = ShiftLifecycleRuntimeState.Create(Fx.Shift, LearningId);
        var tools = ImmutableHashSet<ItemId>.Empty;

        Assert.Throws<ArgumentNullException>(() => executor.Execute(null!, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, null!, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, null!, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, null!, chain.Five, movement, lineNoise, progression, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, null!, movement, lineNoise, progression, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, null!, lineNoise, progression, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, null!, progression, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, null!, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, null!, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, null!, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, tools, default, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, tools, ServerTick.Zero, null!, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, null!, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, null!));

        var badTools = ImmutableHashSet.Create(default(ItemId));
        Assert.Throws<ArgumentException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, badTools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));

        var other = PristineChain(ServerTick.Zero);
        Assert.Throws<ArgumentException>(() => executor.Execute(chain.One, chain.Two, other.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        Assert.Throws<ArgumentException>(() => executor.Execute(other.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));

        var mismatchTick = PristineChain(ServerTick.From(4));
        Assert.Throws<ArgumentException>(() => executor.Execute(mismatchTick.One, mismatchTick.Two, mismatchTick.Three, mismatchTick.Four, mismatchTick.Five, movement, lineNoise, progression, lifecycle, tools, ServerTick.From(5), Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
    }

    [Fact]
    public void Every_cross_shift_evidence_input_is_rejected_before_any_delegated_mutation()
    {
        var chain = PristineChain(ServerTick.Zero);
        var executor = new HostStageSixDerivedExecutor();
        var shiftId = chain.Five.FinalState.ShiftId;
        var initialVersion = chain.Five.FinalState.StateVersion;
        var otherConfiguration = Fx.Shift with { ShiftId = ShiftId.From("another_shift") };
        var otherId = otherConfiguration.ShiftId;

        var movement = MovementNoiseRuntimeState.Create(shiftId);
        var lineNoise = LineNoiseRuntimeState.Create(shiftId);
        var progression = HostTickProgressionEvidence.Create(shiftId);
        var lifecycle = ShiftLifecycleRuntimeState.Create(Fx.Shift, LearningId);
        var tools = ImmutableHashSet<ItemId>.Empty;

        // Cross-shift movement-noise runtime.
        Assert.Throws<ArgumentException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, MovementNoiseRuntimeState.Create(otherId), lineNoise, progression, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        // Cross-shift line-noise runtime.
        Assert.Throws<ArgumentException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, LineNoiseRuntimeState.Create(otherId), progression, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        // Cross-shift progression evidence.
        Assert.Throws<ArgumentException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, HostTickProgressionEvidence.Create(otherId), lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        // Cross-shift lifecycle evidence.
        Assert.Throws<ArgumentException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, ShiftLifecycleRuntimeState.Create(otherConfiguration, LearningId), tools, ServerTick.Zero, Fx.Shift.Scheduler, Fx.Shift, Fx.Anomalies));
        // Cross-shift shift configuration.
        Assert.Throws<ArgumentException>(() => executor.Execute(chain.One, chain.Two, chain.Three, chain.Four, chain.Five, movement, lineNoise, progression, lifecycle, tools, ServerTick.Zero, Fx.Shift.Scheduler, otherConfiguration, Fx.Anomalies));

        // Every rejection happened before any delegated service could mutate the immutable inputs.
        Assert.Equal(initialVersion, chain.Five.FinalState.StateVersion);
        Assert.False(movement.HasAcceptedMovement);
        Assert.Null(lineNoise.LastEvaluatedAt);
        Assert.False(progression.HasCompletedTick);
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

    // ================= Quiet baseline and exact chains =================

    [Fact]
    public void Quiet_tick_applies_no_movement_derives_no_jam_stays_quiet_and_advances_the_checkpoint()
    {
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
        // Feed-gate typed no-op family: the line is clear and nothing waits at the feed gate.
        Assert.IsType<FeedGateJamDerivationNoFeedGateLog>(e.FeedGateJam);
        Assert.IsType<LineNoiseEvaluatedWithoutChange>(e.LineNoiseEvaluation);
        Assert.Equal(LineNoise.QUIET, e.LineNoiseEvaluation.State.Current);
        Assert.IsType<ConfirmationTestConditionNoChange>(e.Confirmation);
        var advanced = Assert.IsType<HostTickCheckpointAdvanced>(e.Checkpoint);
        Assert.Same(e.FinalShiftState, advanced.Receipt.ShiftState);
        Assert.Same(chain.Four.FinalQuotaState, advanced.Receipt.QuotaState);
    }

    [Fact]
    public void Executor_trace_satisfies_the_exact_shift_line_noise_movement_and_quota_reference_chain()
    {
        var e = Stage6(PristineChain(ServerTick.Zero), ServerTick.Zero);

        Assert.Same(e.InitialShiftState, e.IntakeAutoFeedJamStep.BeforeShiftState);
        Assert.Same(e.IntakeAutoFeedJamStep.AfterShiftState, e.FeedGateJamStep.BeforeShiftState);
        Assert.Same(e.FeedGateJamStep.AfterShiftState, e.LineNoiseStep.ShiftState);
        Assert.Same(e.FeedGateJamStep.AfterShiftState, e.ConfirmationStep.BeforeShiftState);
        Assert.Same(e.ConfirmationStep.AfterShiftState, e.CheckpointStep.PostStageShift);
        Assert.Same(e.LineNoiseStep.Result, e.ConfirmationStep.ConsumedLineNoise);
        Assert.Same(e.FinalMovementNoise, e.LineNoiseStep.MovementNoiseState);
        // Explicit retained line-noise identity (Issue #90 §11).
        Assert.Same(e.InitialLineNoise, e.LineNoiseStep.BeforeState);
        Assert.Same(e.LineNoiseStep.Result.State, e.FinalLineNoise);
        // Explicit retained quota identity: stage 6 never settles quota.
        Assert.Same(e.InitialQuotaState, e.FinalQuotaState);
        Assert.Same(e.InitialQuotaState, e.CheckpointStep.PostStageQuota);
    }

    [Fact]
    public void Stage_six_retains_the_exact_stage_four_final_quota_reference()
    {
        var chain = PristineChain(ServerTick.Zero);

        var e = Stage6(chain, ServerTick.Zero);

        Assert.Same(chain.Four.FinalQuotaState, e.InitialQuotaState);
        Assert.Same(chain.Four.FinalQuotaState, e.FinalQuotaState);
        Assert.Same(chain.Four.FinalQuotaState, e.CheckpointStep.PostStageQuota);
    }

    // ================= Trace-constructor self-defense =================

    [Fact]
    public void Constructor_rejects_a_contradictory_shift_state_reference_chain()
    {
        var a = Stage6(PristineChain(ServerTick.Zero), ServerTick.Zero);
        var b = Stage6(PristineChain(ServerTick.Zero), ServerTick.Zero);
        var broken = ExecutionArguments(a);
        broken[6] = b.FeedGateJamStep;

        Assert.True(a.IntakeAutoFeedJamStep.AfterShiftState.ValueEquals(b.FeedGateJamStep.BeforeShiftState));
        Assert.False(ReferenceEquals(a.IntakeAutoFeedJamStep.AfterShiftState, b.FeedGateJamStep.BeforeShiftState));
        AssertExecutionConstructorRejects(broken);
    }

    [Fact]
    public void Constructor_rejects_a_line_noise_step_not_starting_from_the_exact_initial_line_noise()
    {
        var a = Stage6(PristineChain(ServerTick.Zero), ServerTick.Zero);
        var independentButEquivalent = LineNoiseRuntimeState.Create(a.InitialShiftState.ShiftId);
        var broken = ExecutionArguments(a);
        broken[2] = independentButEquivalent;

        Assert.True(independentButEquivalent.ValueEquals(a.InitialLineNoise));
        Assert.False(ReferenceEquals(independentButEquivalent, a.InitialLineNoise));
        AssertExecutionConstructorRejects(broken);
    }

    [Fact]
    public void Constructor_rejects_a_checkpoint_step_whose_quota_is_not_the_exact_retained_reference()
    {
        var a = Stage6(PristineChain(ServerTick.Zero), ServerTick.Zero);
        var independentButEquivalent = FreshQuota();
        var broken = ExecutionArguments(a);
        broken[3] = independentButEquivalent;

        Assert.True(independentButEquivalent.ValueEquals(a.InitialQuotaState));
        Assert.False(ReferenceEquals(independentButEquivalent, a.InitialQuotaState));
        AssertExecutionConstructorRejects(broken);
    }

    [Fact]
    public void Intake_auto_feed_step_rejects_an_existing_line_condition_source_presented_as_an_executed_derivation()
    {
        // A genuine ExistingLineConditionRetained blocked route plus a genuine derivation result from a
        // different, legitimately-derived tick: the pair is contradictory and must be rejected.
        var retainedChain = ExistingLineConditionChain(out var retainedTick);
        var retainedBlocked = Assert.IsType<DefaultIntakeAutoRouteBlocked>(retainedChain.Five.DefaultRoute);
        Assert.Equal(DefaultIntakeAutoRouteFollowUp.ExistingLineConditionRetained, retainedBlocked.FollowUp);

        var scheduler = Fx.Shift.Scheduler with { SawCycleSeconds = 200 };
        var derivedChain = BuildChain(ExpiringDeadlineWithBlockedSawQueue(scheduler, out var derivedTick), derivedTick, FreshQuota(), scheduler: scheduler);
        var derivation = Stage6(derivedChain, derivedTick, scheduler: scheduler).IntakeAutoFeedJam;
        Assert.NotNull(derivation);

        var constructor = typeof(IntakeAutoFeedJamStageStep).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
        var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(
            new object?[] { retainedChain.Five.FinalState, retainedBlocked, derivation }));
        Assert.IsType<ArgumentException>(exception.InnerException);

        // The valid conditional shapes still construct.
        Assert.NotNull(constructor.Invoke(new object?[] { retainedChain.Five.FinalState, null, null }));
        _ = retainedTick;
    }

    // ================= Movement aggregation =================

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
        Assert.Same(e.InitialMovementNoise, step.BeforeState);
        Assert.Same(step.AfterState, e.FinalMovementNoise);
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
        Assert.Equal(MovementNoiseAcceptedSource.SawCycleCompleted, Source(e.MovementSteps[0]));
        Assert.Equal(MovementNoiseAcceptedSource.SawCycleStarted, Source(e.MovementSteps[1]));
        // The exact ordered movement-runtime chain.
        Assert.Same(e.InitialMovementNoise, e.MovementSteps[0].BeforeState);
        Assert.Same(e.MovementSteps[0].AfterState, e.MovementSteps[1].BeforeState);
        Assert.Same(e.MovementSteps[1].AfterState, e.FinalMovementNoise);
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
        Assert.False(e.FinalLineNoise.LatestSources.SawActive);
        Assert.True(e.FinalLineNoise.LatestSources.MovementNoiseActive);
        Assert.Equal(LineNoise.LOUD, e.LineNoiseEvaluation.State.Current);
    }

    [Fact]
    public void Repair_pending_transition_movement_is_applied_when_present()
    {
        var s0 = RepairingFeedGate("log_01", "log_02", out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota());

        Assert.IsType<RepairPendingTransitionExecuted>(chain.Five.RepairExecution);

        var e = Stage6(chain, dueTick);

        Assert.Contains(e.MovementSteps, step => Source(step) == MovementNoiseAcceptedSource.RepairPendingTransition);
    }

    [Fact]
    public void Default_intake_auto_route_applied_produces_the_exact_default_route_movement()
    {
        var s0 = ActiveIntakeDeadline("log_01", out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota());

        var applied = Assert.IsType<DefaultIntakeAutoRouteApplied>(chain.Five.DefaultRoute);

        var e = Stage6(chain, dueTick);

        var step = Assert.Single(e.MovementSteps.Where(candidate => Source(candidate) == MovementNoiseAcceptedSource.DefaultIntakeAutoRoute));
        var movement = Assert.IsType<MovementNoiseApplied>(step.Result).State.LastAcceptedMovement!;
        Assert.Equal(applied.LogId, movement.LogId);
        Assert.Equal(LogState.AT_INTAKE, movement.SourceState);
        Assert.Equal(LogState.QUEUED_FOR_SAW, movement.DestinationState);
        Assert.Equal(applied.PriorStateVersion, movement.PriorStateVersion);
        Assert.Equal(applied.CurrentStateVersion, movement.CurrentStateVersion);
        Assert.Equal(applied.AttemptedAt, movement.AcceptedAt);
    }

    [Fact]
    public void Feed_due_admitted_to_intake_produces_the_exact_feed_due_movement()
    {
        var chain = PristineChain(ServerTick.Zero);
        var resolved = Assert.IsType<FeedDueResolved>(chain.Five.FeedDue);
        Assert.Equal(FeedDueDisposition.AdmittedToIntake, resolved.Disposition);

        var e = Stage6(chain, ServerTick.Zero);

        var step = Assert.Single(e.MovementSteps.Where(candidate => Source(candidate) == MovementNoiseAcceptedSource.FeedDueResolved));
        var movement = Assert.IsType<MovementNoiseApplied>(step.Result).State.LastAcceptedMovement!;
        Assert.Equal(resolved.ConsumedSchedule.LogId, movement.LogId);
        Assert.Equal(LogState.SCHEDULED, movement.SourceState);
        Assert.Equal(LogState.AT_INTAKE, movement.DestinationState);
        Assert.Equal(resolved.CurrentStateVersion, movement.CurrentStateVersion);
    }

    [Fact]
    public void Feed_due_placed_at_feed_gate_produces_the_exact_feed_gate_disposition_movement()
    {
        var s0 = PendingEarlyFeedWithOccupiedIntake(out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota());

        var resolved = Assert.IsType<FeedDueResolved>(chain.Five.FeedDue);
        Assert.Equal(FeedDueDisposition.PlacedAtFeedGate, resolved.Disposition);

        var e = Stage6(chain, dueTick);

        var step = Assert.Single(e.MovementSteps.Where(candidate => Source(candidate) == MovementNoiseAcceptedSource.FeedDueResolved));
        var movement = Assert.IsType<MovementNoiseApplied>(step.Result).State.LastAcceptedMovement!;
        Assert.Equal(LogState.AT_FEED_GATE, movement.DestinationState);
    }

    [Fact]
    public void Movement_from_more_than_one_stage_family_keeps_exact_host_order_with_increasing_versions()
    {
        // log_01 at intake is manually routed in stage 2; log_02 already waits in the saw queue, so stage 4
        // starts its cycle in the same tick. Stage-2 movement must precede stage-4 movement without sorting.
        var s0 = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_02");
        s0 = RuntimeFixture.MoveHost(s0, "log_02", LogState.QUEUED_FOR_SAW);
        s0 = RuntimeFixture.MoveToIntake(s0, "log_01");
        var chain = BuildChain(s0, ServerTick.From(10), FreshQuota(),
            one => RouteBatch(one.FinalState, ServerTick.From(10), "log_01", LogIntentActions.RouteToProcedure));

        Assert.IsType<SawCycleStarted>(chain.Four.Start.Result);

        var e = Stage6(chain, ServerTick.From(10));

        Assert.Equal(2, e.MovementSteps.Length);
        Assert.Equal(MovementNoiseAcceptedSource.ManualLogIntent, Source(e.MovementSteps[0]));
        Assert.Equal(MovementNoiseAcceptedSource.SawCycleStarted, Source(e.MovementSteps[1]));

        var first = Movement(e.MovementSteps[0]);
        var second = Movement(e.MovementSteps[1]);
        Assert.True(second.CurrentStateVersion > first.CurrentStateVersion, "accepted movement versions must strictly increase in host order");
        Assert.Same(e.MovementSteps[0].AfterState, e.MovementSteps[1].BeforeState);
    }

    [Fact]
    public void An_exactly_replayed_movement_is_retained_as_an_already_applied_step()
    {
        var s0 = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var chain = BuildChain(s0, ServerTick.From(10), FreshQuota(),
            one => RouteBatch(one.FinalState, ServerTick.From(10), "log_01", LogIntentActions.RouteToProcedure));

        var first = Stage6(chain, ServerTick.From(10));
        // Re-executing the exact same tick from the runtime the first pass produced replays identical evidence.
        var replay = Stage6(chain, ServerTick.From(10), movementNoise: first.FinalMovementNoise);

        var step = Assert.Single(replay.MovementSteps);
        Assert.IsType<MovementNoiseAlreadyApplied>(step.Result);
        Assert.Same(first.FinalMovementNoise, replay.FinalMovementNoise);
    }

    [Fact]
    public void An_overlapping_later_movement_extends_the_retained_window_without_restarting_it()
    {
        var scheduler = Fx.Shift.Scheduler with { MovementNoiseSeconds = 5 };
        var zero = TickZeroWithMovement(scheduler, out var seedChain);
        var window = zero.FinalMovementNoise;
        Assert.Equal(ServerTick.Zero, window.StartedAt);
        Assert.Equal(ServerTick.From(5), window.DueAt);

        var next = ManualRouteFollowUpTick(seedChain, zero, scheduler, ServerTick.From(1));

        var extended = next.FinalMovementNoise;
        Assert.Equal(ServerTick.Zero, extended.StartedAt);
        Assert.Equal(ServerTick.From(6), extended.DueAt);
    }

    [Fact]
    public void An_overlapping_shorter_movement_cannot_shorten_the_retained_window()
    {
        var wideScheduler = Fx.Shift.Scheduler with { MovementNoiseSeconds = 100 };
        var zero = TickZeroWithMovement(wideScheduler, out var seedChain);
        Assert.Equal(ServerTick.From(100), zero.FinalMovementNoise.DueAt);

        var narrowScheduler = Fx.Shift.Scheduler with { MovementNoiseSeconds = 2 };
        var next = ManualRouteFollowUpTick(seedChain, zero, narrowScheduler, ServerTick.From(1));

        var preserved = next.FinalMovementNoise;
        Assert.Equal(ServerTick.Zero, preserved.StartedAt);
        Assert.Equal(ServerTick.From(100), preserved.DueAt);
    }

    // ================= Jam composition =================

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
    public void Existing_line_condition_retained_never_invokes_the_intake_auto_feed_family()
    {
        var chain = ExistingLineConditionChain(out var dueTick);

        var blocked = Assert.IsType<DefaultIntakeAutoRouteBlocked>(chain.Five.DefaultRoute);
        Assert.Equal(DefaultIntakeAutoRouteFollowUp.ExistingLineConditionRetained, blocked.FollowUp);
        var conditionBefore = chain.Five.FinalState.Line;

        var e = Stage6(chain, dueTick, scheduler: Fx.Shift.Scheduler with { SawCycleSeconds = 200 });

        // The conditional derivation family did not execute at all.
        Assert.Null(e.IntakeAutoFeedJamSource);
        Assert.Null(e.IntakeAutoFeedJam);
        Assert.Same(e.InitialShiftState, e.IntakeAutoFeedJamStep.AfterShiftState);

        // Feed-gate state derivation still evaluates and preserves the existing condition unchanged.
        Assert.IsType<FeedGateJamDerivationLineNotClear>(e.FeedGateJam);
        Assert.Same(conditionBefore, e.FinalShiftState.Line);
        Assert.Equal(LineState.LINE_JAMMED, e.FinalShiftState.Line.State);
        Assert.Equal(JamCause.FEED_GATE_BLOCKED, e.FinalShiftState.Line.Cause);
    }

    [Fact]
    public void Dual_eligible_jam_precedence_derives_intake_auto_feed_first_and_no_second_jam_layers()
    {
        var scheduler = Fx.Shift.Scheduler with { SawCycleSeconds = 200 };
        var s0 = DualEligibleJamSeed(scheduler, out var dueTick);
        var chain = BuildChain(s0, dueTick, FreshQuota(), scheduler: scheduler);

        var blocked = Assert.IsType<DefaultIntakeAutoRouteBlocked>(chain.Five.DefaultRoute);
        Assert.Equal(DefaultIntakeAutoRouteFollowUp.IntakeAutoFeedJamDerivationRequired, blocked.FollowUp);
        var resolved = Assert.IsType<FeedDueResolved>(chain.Five.FeedDue);
        Assert.Equal(FeedDueDisposition.PlacedAtFeedGate, resolved.Disposition);
        Assert.NotEqual("log_01", resolved.ConsumedSchedule.LogId.ToString());
        Assert.Equal(LineState.LINE_CLEAR, chain.Five.FinalState.Line.State);

        var e = Stage6(chain, dueTick, scheduler: scheduler);

        var entered = Assert.IsType<IntakeAutoFeedJamEntered>(e.IntakeAutoFeedJam);
        Assert.Equal(JamCause.INTAKE_AUTOFEED_BLOCKED, entered.Cause);
        Assert.Equal("log_01", entered.LogId.ToString());

        Assert.IsType<FeedGateJamDerivationLineNotClear>(e.FeedGateJam);
        Assert.Equal(LineState.LINE_JAMMED, e.FinalShiftState.Line.State);
        Assert.Equal(JamCause.INTAKE_AUTOFEED_BLOCKED, e.FinalShiftState.Line.Cause);
        Assert.Equal("log_01", e.FinalShiftState.Line.PendingLogId!.Value.ToString());
    }

    // ================= Line noise =================

    [Fact]
    public void Manual_movement_alone_makes_the_line_loud()
    {
        var s0 = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var chain = BuildChain(s0, ServerTick.From(10), FreshQuota(),
            one => RouteBatch(one.FinalState, ServerTick.From(10), "log_01", LogIntentActions.RouteToProcedure));

        var e = Stage6(chain, ServerTick.From(10));

        Assert.False(e.FinalLineNoise.LatestSources.SawActive);
        Assert.True(e.FinalLineNoise.LatestSources.MovementNoiseActive);
        Assert.False(e.FinalLineNoise.LatestSources.RepairActive);
        Assert.Equal(LineNoise.LOUD, e.LineNoiseEvaluation.State.Current);
    }

    [Fact]
    public void An_active_saw_cycle_makes_the_line_loud()
    {
        var s0 = RuntimeFixture.MoveHost(RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01"), "log_01", LogState.QUEUED_FOR_SAW);
        var chain = BuildChain(s0, ServerTick.From(10), FreshQuota());

        Assert.IsType<SawCycleStarted>(chain.Four.Start.Result);

        var e = Stage6(chain, ServerTick.From(10));

        Assert.True(e.FinalLineNoise.LatestSources.SawActive);
        Assert.Equal(LineNoise.LOUD, e.LineNoiseEvaluation.State.Current);
    }

    [Fact]
    public void An_active_repair_is_an_authoritative_loud_source_without_movement_or_saw()
    {
        var s0 = RepairingLineWithoutOtherSources(out var repairTick);
        var observedTick = ServerTick.From(repairTick.Value + 1);
        var chain = BuildChain(s0, observedTick, FreshQuota());

        // Stage 1 must not complete the repair at this tick, so the line is still REPAIRING at stage 6.
        Assert.IsType<LineRepairNotDue>(chain.One.LineRepair.Result);
        Assert.Equal(LineState.REPAIRING, chain.Five.FinalState.Line.State);

        var e = Stage6(chain, observedTick);

        Assert.Empty(e.MovementSteps);
        Assert.True(e.FinalLineNoise.LatestSources.RepairActive);
        Assert.False(e.FinalLineNoise.LatestSources.SawActive);
        Assert.False(e.FinalLineNoise.LatestSources.MovementNoiseActive);
        Assert.Equal(LineNoise.LOUD, e.LineNoiseEvaluation.State.Current);
    }

    [Fact]
    public void A_jam_alone_is_not_a_noise_source_and_the_line_stays_quiet()
    {
        var s0 = JammedLineWithoutOtherSources(out var jamTick);
        var observedTick = ServerTick.From(jamTick.Value + 1);
        var chain = BuildChain(s0, observedTick, FreshQuota());

        Assert.Equal(LineState.LINE_JAMMED, chain.Five.FinalState.Line.State);

        var e = Stage6(chain, observedTick);

        Assert.Empty(e.MovementSteps);
        Assert.False(e.FinalLineNoise.LatestSources.SawActive);
        Assert.False(e.FinalLineNoise.LatestSources.MovementNoiseActive);
        Assert.False(e.FinalLineNoise.LatestSources.RepairActive);
        Assert.Equal(LineNoise.QUIET, e.LineNoiseEvaluation.State.Current);
    }

    // ================= Confirmation =================

    [Fact]
    public void An_active_confirmation_is_paused_by_same_tick_line_noise_from_a_new_saw_cycle()
    {
        var tools = ImmutableHashSet.Create(ItemId.From("sound_meter"));
        var queued = RuntimeFixture.MoveHost(RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01"), "log_01", LogState.QUEUED_FOR_SAW);
        var intake = RuntimeFixture.MoveToIntake(queued, "log_03");
        var started = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(
            intake, LogId.From("log_03"), tools, ServerTick.From(5), LineNoiseRuntimeState.Create(intake.ShiftId), Fx.Anomalies)).State;
        var activeBefore = Assert.IsType<ActiveConfirmationTest>(started.ActiveConfirmationTest);
        Assert.True(activeBefore.IsRunning);
        Assert.True(activeBefore.DueAt!.Value > ServerTick.From(6));

        var chain = BuildChain(started, ServerTick.From(6), FreshQuota());
        Assert.IsType<SawCycleStarted>(chain.Four.Start.Result);

        var e = Stage6(chain, ServerTick.From(6), activeTools: tools);

        Assert.Equal(LineNoise.LOUD, e.LineNoiseEvaluation.State.Current);
        var updated = Assert.IsType<ConfirmationTestConditionUpdated>(e.Confirmation);
        Assert.False(Assert.IsType<ActiveConfirmationTest>(updated.State.ActiveConfirmationTest).IsRunning);
        // The confirmation mutation is exactly what the checkpoint received.
        Assert.Same(updated.State, e.CheckpointStep.PostStageShift);
    }

    [Fact]
    public void Confirmation_due_completion_required_is_unreachable_because_leaving_intake_clears_the_confirmation()
    {
        // Reachability proof for Issue #90's "where reachable" due-completion case.
        // ConfirmationTestConditionService returns ConfirmationTestDueCompletionRequired only when stage 6
        // observes a RUNNING confirmation whose DueAt has passed. Stage 1 runs first and completes any such
        // confirmation; the only stage-1 branch that leaves a due running confirmation in place is
        // ConfirmationTestDueFailed, which requires the owner to not be AT_INTAKE. That state is
        // unconstructible: ShiftRuntimeState.ApplyTransition clears the active confirmation whenever its
        // owner leaves intake, as proven here through public APIs.
        var tools = ImmutableHashSet.Create(ItemId.From("sound_meter"));
        var intake = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_03");
        var started = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(
            intake, LogId.From("log_03"), tools, ServerTick.From(5), LineNoiseRuntimeState.Create(intake.ShiftId), Fx.Anomalies)).State;
        Assert.NotNull(started.ActiveConfirmationTest);

        var movedOut = RuntimeFixture.MoveHost(started, "log_03", LogState.AT_PROCEDURE);

        Assert.Null(movedOut.ActiveConfirmationTest);
        Assert.Equal(LogState.AT_PROCEDURE, Log(movedOut, "log_03").State);

        // Consequently a full valid chain past the due tick can only surface no-change/updated, never
        // ConfirmationTestDueCompletionRequired.
        var chain = BuildChain(started, ServerTick.From(20), FreshQuota());
        var e = Stage6(chain, ServerTick.From(20), activeTools: tools);

        Assert.IsNotType<ConfirmationTestDueCompletionRequired>(e.Confirmation);
        Assert.IsType<ConfirmationTestDueCompleted>(chain.One.Confirmation.Result);
    }

    // ================= Checkpoint =================

    [Fact]
    public void Checkpoint_newly_completes_when_all_manifest_logs_reach_terminal_state()
    {
        var lifecycle = ShiftLifecycleRuntimeState.Create(Fx.Shift, ProfileId.From("pressure"));
        var chain = BuildChain(AllLogsWrittenOff(), ServerTick.Zero, FreshQuota());

        var e = Stage6(chain, ServerTick.Zero, lifecycle: lifecycle);

        var advanced = Assert.IsType<HostTickCheckpointAdvanced>(e.Checkpoint);
        var completed = Assert.IsType<ShiftCompletionNewlyCompleted>(advanced.Receipt.Evaluation);
        Assert.True(advanced.Receipt.ShiftCompleted);
        Assert.Equal(ShiftCompletionReason.AllLogsTerminal, completed.Completion.Reason);
        Assert.Same(e.FinalShiftState, completed.Completion.FinalShiftState);
        Assert.Same(e.FinalQuotaState, completed.Completion.FinalQuotaState);
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
        // A rejected checkpoint still retains the exact post-stage inputs.
        Assert.Same(e.FinalShiftState, e.CheckpointStep.PostStageShift);
        Assert.Same(chain.Four.FinalQuotaState, e.CheckpointStep.PostStageQuota);
    }

    [Fact]
    public void Stage_six_never_settles_quota()
    {
        var quota = FreshQuota();
        var chain = BuildChain(RuntimeFixture.CreateInitialState(), ServerTick.Zero, quota);

        var e = Stage6(chain, ServerTick.Zero);

        Assert.Same(quota, e.InitialQuotaState);
        Assert.Same(quota, e.FinalQuotaState);
        Assert.Empty(e.FinalQuotaState.SettledLogIds);
        Assert.Equal(0, e.FinalQuotaState.TotalCreditedUnits);
    }

    // ================= Determinism and failure =================

    [Fact]
    public void Independent_equivalent_chains_produce_value_equivalent_projections_and_state()
    {
        var first = Stage6(PristineChain(ServerTick.Zero), ServerTick.Zero);
        var second = Stage6(PristineChain(ServerTick.Zero), ServerTick.Zero);

        Assert.Equal(first.LineNoiseEvaluation.GetType(), second.LineNoiseEvaluation.GetType());
        Assert.Equal(first.Confirmation.GetType(), second.Confirmation.GetType());
        Assert.Equal(first.Checkpoint.GetType(), second.Checkpoint.GetType());
        Assert.True(first.FinalShiftState.ValueEquals(second.FinalShiftState));
        Assert.True(first.FinalMovementNoise.ValueEquals(second.FinalMovementNoise));
        Assert.True(first.FinalLineNoise.ValueEquals(second.FinalLineNoise));
        Assert.NotSame(first.FinalShiftState, second.FinalShiftState);
    }

    [Fact]
    public void A_delegated_service_exception_propagates_without_a_partial_result()
    {
        var badConfiguration = Fx.Shift.Scheduler with { MovementNoiseSeconds = 0 };
        var s0 = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var chain = BuildChain(s0, ServerTick.From(10), FreshQuota(),
            one => RouteBatch(one.FinalState, ServerTick.From(10), "log_01", LogIntentActions.RouteToProcedure));
        var initial = chain.Five.FinalState;
        var initialVersion = initial.StateVersion;

        Assert.Throws<ArgumentOutOfRangeException>(() => Stage6(chain, ServerTick.From(10), scheduler: badConfiguration));

        Assert.Equal(initialVersion, initial.StateVersion);
        Assert.Equal(LogState.AT_PROCEDURE, Log(initial, "log_01").State);
    }

    [Fact]
    public void Stage_six_performs_no_stage_seven_or_containment_or_saw_mutation()
    {
        var chain = PristineChain(ServerTick.Zero);
        var before = chain.Five.FinalState;

        var e = Stage6(chain, ServerTick.Zero);

        Assert.Same(before.Containment, e.FinalShiftState.Containment);
        Assert.Same(before.ActiveContainmentRitual, e.FinalShiftState.ActiveContainmentRitual);
        Assert.Same(before.ActiveSawCycle, e.FinalShiftState.ActiveSawCycle);
        Assert.Same(before.PendingFeed, e.FinalShiftState.PendingFeed);
        Assert.Same(before.Inventory, e.FinalShiftState.Inventory);
        Assert.Same(before.ActiveIntakeDeadline, e.FinalShiftState.ActiveIntakeDeadline);
        Assert.Same(chain.Two.Batch, chain.Two.Batch);
    }

    // ================= Helpers =================

    private static QuotaRuntimeState FreshQuota() => QuotaRuntimeState.Create(Fx.Shift);

    private static MovementNoiseAcceptedSource Source(MovementNoiseApplicationStageStep step) =>
        Movement(step).Source;

    private static MovementNoiseAcceptedMovement Movement(MovementNoiseApplicationStageStep step) =>
        step.Result.State.LastAcceptedMovement ?? throw new InvalidOperationException("Movement step must retain accepted movement evidence.");

    private static object?[] ExecutionArguments(HostStageSixDerivedExecution execution) => new object?[]
    {
        execution.InitialShiftState, execution.InitialMovementNoise, execution.InitialLineNoise, execution.InitialQuotaState,
        execution.MovementSteps, execution.IntakeAutoFeedJamStep, execution.FeedGateJamStep, execution.LineNoiseStep,
        execution.ConfirmationStep, execution.CheckpointStep, execution.Progression, execution.Lifecycle,
        execution.ActiveTools, execution.CurrentTick
    };

    private static void AssertExecutionConstructorRejects(object?[] arguments)
    {
        var constructor = typeof(HostStageSixDerivedExecution).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
        var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(arguments));
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

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
            lifecycle ?? ShiftLifecycleRuntimeState.Create(Fx.Shift, LearningId),
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
        var two = new AcceptedIntentStageExecutor().Execute(one.FinalState, batch, sched, Fx.Anomalies);
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

    /// <summary>Tick 0 of a pristine shift: the initial feed is admitted to intake, producing one movement.</summary>
    private static HostStageSixDerivedExecution TickZeroWithMovement(SchedulerConfiguration scheduler, out StageChain chain)
    {
        chain = BuildChain(RuntimeFixture.CreateInitialState(), ServerTick.Zero, FreshQuota(), scheduler: scheduler);
        var execution = Stage6(chain, ServerTick.Zero, scheduler: scheduler);
        Assert.Single(execution.MovementSteps);
        return execution;
    }

    /// <summary>The next tick, manually routing the admitted log out of intake to produce a second movement.</summary>
    private static HostStageSixDerivedExecution ManualRouteFollowUpTick(
        StageChain seedChain,
        HostStageSixDerivedExecution seed,
        SchedulerConfiguration scheduler,
        ServerTick tick)
    {
        var advanced = Assert.IsType<HostTickCheckpointAdvanced>(seed.Checkpoint);
        var admitted = Assert.IsType<FeedDueResolved>(seedChain.Five.FeedDue).ConsumedSchedule.LogId;
        var chain = BuildChain(seed.FinalShiftState, tick, seedChain.Four.FinalQuotaState,
            one => RouteBatch(one.FinalState, tick, admitted.ToString(), LogIntentActions.RouteToProcedure),
            scheduler);

        var execution = Stage6(chain, tick,
            movementNoise: seed.FinalMovementNoise,
            lineNoise: seed.FinalLineNoise,
            scheduler: scheduler,
            progression: advanced.Progression,
            lifecycle: advanced.Receipt.Lifecycle);

        Assert.Single(execution.MovementSteps);
        Assert.Equal(MovementNoiseAcceptedSource.ManualLogIntent, Source(execution.MovementSteps[0]));
        return execution;
    }

    /// <summary>Every manifest log driven to a terminal written-off state through valid public host transitions.</summary>
    private static ShiftRuntimeState AllLogsWrittenOff()
    {
        var state = RuntimeFixture.CreateInitialState();
        foreach (var logId in state.Logs.Select(log => log.LogId.ToString()).ToArray())
        {
            state = RuntimeFixture.MoveToIntake(state, logId);
            state = RuntimeFixture.MoveHost(state, logId, LogState.HELD_WRITTEN_OFF);
        }

        Assert.All(state.Logs, log => Assert.Equal(LogState.HELD_WRITTEN_OFF, log.State));
        return state;
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

    /// <summary>
    /// A blocked default route evaluated while the line already carries a feed-gate jam, so stage 5 retains
    /// ExistingLineConditionRetained rather than requiring an intake-auto-feed derivation.
    /// </summary>
    private static StageChain ExistingLineConditionChain(out ServerTick dueTick)
    {
        var scheduler = Fx.Shift.Scheduler with { SawCycleSeconds = 200 };
        var state = ExpiringDeadlineWithBlockedSawQueue(scheduler, out var deadlineDueTick);
        // log_04 waits at the feed gate behind the occupied intake, so a feed-gate jam can be entered.
        state = RuntimeFixture.MoveHost(state, "log_04", LogState.AT_FEED_GATE);
        state = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.FEED_GATE_BLOCKED, ServerTick.From(30))).State;

        dueTick = deadlineDueTick;
        return BuildChain(state, deadlineDueTick, FreshQuota(), scheduler: scheduler);
    }

    private static ShiftRuntimeState DualEligibleJamSeed(SchedulerConfiguration scheduler, out ServerTick dueTick)
    {
        var state = ExpiringDeadlineWithBlockedSawQueue(scheduler, out var deadlineDueTick);
        dueTick = deadlineDueTick;

        var earlyRequestTick = ServerTick.From(dueTick.Value - Fx.Shift.Scheduler.EarlyFeedDelaySeconds);
        var intent = new IntentEnvelope(
            state.ShiftId, IntentId.From("distinct_due"), ActorId.From("hint"), FeedPlanningTargets.FeedGate,
            FeedPlanningIntentActions.RequestEarlyFeed, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var early = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(
            state, intent, RuntimeFixture.BoundActor, earlyRequestTick, Fx.Shift.Scheduler));
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

    /// <summary>A feed-gate jam with no saw, no repair and no movement evidence.</summary>
    private static ShiftRuntimeState JammedLineWithoutOtherSources(out ServerTick jamTick)
    {
        jamTick = ServerTick.From(5);
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
        return Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.FEED_GATE_BLOCKED, jamTick)).State;
    }

    /// <summary>The same jam, additionally repairing, so repair is the only active noise source.</summary>
    private static ShiftRuntimeState RepairingLineWithoutOtherSources(out ServerTick repairTick)
    {
        var jammed = JammedLineWithoutOtherSources(out var jamTick);
        repairTick = jamTick;
        return Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jammed, jamTick, Fx.Shift.Scheduler)).State;
    }

    private static LogRuntimeState Log(ShiftRuntimeState state, string logId)
    {
        Assert.True(state.TryGetLog(LogId.From(logId), out var log));
        return log;
    }
}
