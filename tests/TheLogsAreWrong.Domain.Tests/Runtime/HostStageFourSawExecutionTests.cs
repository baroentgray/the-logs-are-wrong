using System.Reflection;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-033")]
public sealed class HostStageFourSawExecutionTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static readonly HostStageFourSawExecutor Executor = new();

    // ----- API and construction -----

    [Fact]
    public void Null_and_default_arguments_reject_before_execution()
    {
        var shift = Create();
        var quota = FreshQuota();

        Assert.Throws<ArgumentNullException>(() => Executor.Execute(null!, quota, ServerTick.From(5), Fx.Shift.Scheduler, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => Executor.Execute(shift, null!, ServerTick.From(5), Fx.Shift.Scheduler, Fx.Anomalies));
        Assert.Throws<ArgumentException>(() => Executor.Execute(shift, quota, default, Fx.Shift.Scheduler, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => Executor.Execute(shift, quota, ServerTick.From(5), null!, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => Executor.Execute(shift, quota, ServerTick.From(5), Fx.Shift.Scheduler, null!));
        Assert.Throws<ArgumentNullException>(() => Executor.Execute(shift, quota, ServerTick.From(5), Fx.Shift.Scheduler, new AnomalyCatalog(null!)));
    }

    [Fact]
    public void Executor_execute_accepts_only_typed_source_derived_inputs()
    {
        var execute = Assert.Single(
            typeof(HostStageFourSawExecutor).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => method.Name == "Execute");

        Assert.Equal(typeof(HostStageFourSawExecution), execute.ReturnType);
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(QuotaRuntimeState), typeof(ServerTick), typeof(SchedulerConfiguration), typeof(AnomalyCatalog) },
            execute.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(execute.GetParameters(), parameter =>
            parameter.ParameterType == typeof(object) ||
            parameter.ParameterType == typeof(bool) ||
            parameter.ParameterType == typeof(string) ||
            parameter.ParameterType == typeof(LogId) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
    }

    [Fact]
    public void Steps_and_result_are_sealed_immutable_and_non_publicly_constructible()
    {
        var publicInstance = BindingFlags.Public | BindingFlags.Instance;
        var types = new[]
        {
            typeof(HostStageFourSawExecution),
            typeof(SawCycleCompletionStageStep),
            typeof(SawQuotaApplicationStageStep),
            typeof(SawCycleStartStageStep)
        };

        Assert.All(types, type =>
        {
            Assert.True(type.IsSealed);
            Assert.Empty(type.GetConstructors(publicInstance));
            Assert.Empty(type.GetFields(publicInstance));
            Assert.All(type.GetProperties(publicInstance), property => Assert.Null(property.SetMethod));
        });
    }

    [Fact]
    public void Quota_step_non_public_constructor_rejects_contradictory_shapes()
    {
        var completed = Complete("log_01");
        var quotaResult = new SawQuotaApplicationService().Apply(completed, FreshQuota());
        var noActive = new SawCycleCompletionService().Complete(Create(), ServerTick.From(5), Fx.Anomalies);
        var constructor = Assert.Single(typeof(SawQuotaApplicationStageStep).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));

        // Completed saw cycle with no quota result.
        AssertConstructorThrows(constructor, new object?[] { completed, FreshQuota(), null });
        // Non-completed result carrying a quota result.
        AssertConstructorThrows(constructor, new object?[] { noActive, FreshQuota(), quotaResult });
    }

    // ----- No-op and start-only paths -----

    [Fact]
    public void Idle_saw_empty_queue_completes_no_active_applies_no_quota_and_finds_no_owner()
    {
        var shift = Create();
        var quota = FreshQuota();

        var execution = Execute(shift, quota, ServerTick.From(10));

        Assert.IsType<SawCycleNoActive>(execution.Completion.Result);
        Assert.False(execution.Quota.WasRequired);
        Assert.Null(execution.Quota.Result);
        Assert.IsType<SawCycleStartNoQueuedOwner>(execution.Start.Result);
        // Exact original references throughout.
        Assert.Same(shift, execution.InitialShiftState);
        Assert.Same(quota, execution.InitialQuotaState);
        Assert.Same(shift, execution.Completion.AfterShiftState);
        Assert.Same(shift, execution.Start.BeforeShiftState);
        Assert.Same(shift, execution.FinalShiftState);
        Assert.Same(quota, execution.Quota.AfterQuotaState);
        Assert.Same(quota, execution.FinalQuotaState);
        Assert.Equal(shift.StateVersion, execution.FinalShiftState.StateVersion);
    }

    [Fact]
    public void Idle_saw_with_queued_owner_starts_the_cycle_and_leaves_quota_untouched()
    {
        var shift = Queued(Create(), "log_01");
        var quota = FreshQuota();

        var execution = Execute(shift, quota, ServerTick.From(10));

        Assert.IsType<SawCycleNoActive>(execution.Completion.Result);
        Assert.False(execution.Quota.WasRequired);
        var started = Assert.IsType<SawCycleStarted>(execution.Start.Result);
        Assert.Equal(LogId.From("log_01"), started.Cycle.LogId);
        Assert.Equal(ServerTick.From(10), started.Cycle.StartedAt);
        Assert.Equal(LogState.IN_SAW, Log(execution.FinalShiftState, "log_01").State);
        Assert.Equal(shift.StateVersion.Next(), execution.FinalShiftState.StateVersion);
        Assert.Same(quota, execution.FinalQuotaState);
    }

    [Fact]
    public void Active_cycle_before_due_is_not_due_applies_no_quota_and_reports_already_active()
    {
        var started = StartCycle(Queued(Create(), "log_01"), 10);
        var shift = started.State;
        var quota = FreshQuota();

        var execution = Execute(shift, quota, ServerTick.From(15));

        Assert.IsType<SawCycleNotDue>(execution.Completion.Result);
        Assert.False(execution.Quota.WasRequired);
        Assert.Null(execution.Quota.Result);
        Assert.IsType<SawCycleStartAlreadyActive>(execution.Start.Result);
        Assert.Same(shift, execution.FinalShiftState);
        Assert.Same(quota, execution.FinalQuotaState);
        Assert.Equal(shift.StateVersion, execution.FinalShiftState.StateVersion);
    }

    // ----- Completion and quota -----

    [Fact]
    public void Due_cycle_without_successor_completes_applies_quota_and_finds_no_owner()
    {
        var started = StartCycle(Queued(Create(), "log_01"), 10);
        var shift = started.State;
        var quota = FreshQuota();

        var execution = Execute(shift, quota, started.Cycle.DueAt);

        var completed = Assert.IsType<SawCycleCompleted>(execution.Completion.Result);
        Assert.True(execution.Quota.WasRequired);
        var quotaResult = Assert.IsType<SawQuotaApplicationAccepted>(execution.Quota.Result);
        Assert.Same(completed.State, quotaResult.ShiftState);
        Assert.IsType<SawCycleStartNoQueuedOwner>(execution.Start.Result);
        Assert.Equal(LogState.PROCESSED, Log(execution.FinalShiftState, "log_01").State);
        Assert.Same(completed.State, execution.FinalShiftState);
        Assert.Same(quotaResult.QuotaState, execution.FinalQuotaState);
        Assert.Equal(shift.StateVersion.Next(), execution.FinalShiftState.StateVersion);
    }

    [Fact]
    public void Late_cycle_without_successor_catches_up_and_applies_quota()
    {
        var started = StartCycle(Queued(Create(), "log_01"), 10);
        var lateTick = ServerTick.From(started.Cycle.DueAt.Value + 3);

        var execution = Execute(started.State, FreshQuota(), lateTick);

        var completed = Assert.IsType<SawCycleCompleted>(execution.Completion.Result);
        Assert.Equal(lateTick, completed.CompletedAt);
        Assert.IsType<SawQuotaApplicationAccepted>(execution.Quota.Result);
        Assert.IsType<SawCycleStartNoQueuedOwner>(execution.Start.Result);
    }

    [Fact]
    public void Already_applied_quota_continues_and_preserves_the_settled_reference()
    {
        var settledQuota = Assert.IsType<SawQuotaApplicationAccepted>(
            new SawQuotaApplicationService().Apply(Complete("log_01"), FreshQuota())).QuotaState;
        var started = StartCycle(Queued(Create(), "log_01"), 10);

        var execution = Execute(started.State, settledQuota, started.Cycle.DueAt);

        Assert.IsType<SawCycleCompleted>(execution.Completion.Result);
        Assert.True(execution.Quota.WasRequired);
        Assert.IsType<SawQuotaApplicationAlreadyApplied>(execution.Quota.Result);
        Assert.Same(settledQuota, execution.FinalQuotaState);
    }

    [Fact]
    public void Normal_log_quota_is_credited_and_incorrect_anomaly_effects_are_retained_as_data()
    {
        var normal = Execute(StartCycle(Queued(Create(), "log_01"), 10).State, FreshQuota(), ServerTick.From(16));
        var normalQuota = Assert.IsType<SawQuotaApplicationAccepted>(normal.Quota.Result);
        Assert.Equal(1, normalQuota.QuotaState.GetCreditedUnits(SpeciesId.From("pine")));
        Assert.Equal(0, normalQuota.QuotaState.CorrectlyProcessedAnomalies);

        // log_06 is a RESIN_BLASPHEMER processed without its required flag: effects retained, no credit.
        var incorrect = Execute(StartCycle(Queued(Create(), "log_06"), 10).State, FreshQuota(), ServerTick.From(16));
        var incorrectQuota = Assert.IsType<SawQuotaApplicationAccepted>(incorrect.Quota.Result);
        Assert.NotEmpty(incorrectQuota.Resolution.Effects);
        Assert.Equal(0, incorrectQuota.QuotaState.TotalCreditedUnits);
        Assert.Equal(0, incorrectQuota.QuotaState.CorrectlyProcessedAnomalies);
    }

    [Fact]
    public void Correctly_prepared_anomaly_settles_its_resolved_credit_and_anomaly_delta()
    {
        var started = StartCycle(PreparedPenitentQueued(), 10);

        var execution = Execute(started.State, FreshQuota(), started.Cycle.DueAt);

        var quotaResult = Assert.IsType<SawQuotaApplicationAccepted>(execution.Quota.Result);
        Assert.Equal(1, quotaResult.AcceptedSettlement.Descriptor.CorrectAnomalyDelta);
        Assert.Equal(1, execution.FinalQuotaState.CorrectlyProcessedAnomalies);
        Assert.True(execution.FinalQuotaState.IsSettled(LogId.From("log_03")));
    }

    // ----- Same-tick completion and restart -----

    [Fact]
    public void Same_tick_completion_applies_quota_then_starts_the_queued_successor_in_fixed_order()
    {
        var (shift, tick) = DueCycleWithSuccessor();
        var quota = FreshQuota();

        var execution = Execute(shift, quota, tick);

        // Fixed order and exact chain.
        Assert.Same(shift, execution.InitialShiftState);
        Assert.Same(quota, execution.InitialQuotaState);
        Assert.Same(shift, execution.Completion.BeforeShiftState);
        Assert.Same(execution.Completion.Result.State, execution.Completion.AfterShiftState);
        Assert.Same(quota, execution.Quota.BeforeQuotaState);
        Assert.Same(execution.Completion.AfterShiftState, execution.Start.BeforeShiftState);
        Assert.Same(execution.Start.AfterShiftState, execution.FinalShiftState);
        Assert.Same(execution.Quota.AfterQuotaState, execution.FinalQuotaState);

        var completed = Assert.IsType<SawCycleCompleted>(execution.Completion.Result);
        var quotaResult = Assert.IsType<SawQuotaApplicationAccepted>(execution.Quota.Result);
        var started = Assert.IsType<SawCycleStarted>(execution.Start.Result);
        Assert.Same(execution.Completion.AfterShiftState, quotaResult.ShiftState);

        // Completed owner processed; successor now in the saw at this exact tick.
        Assert.Equal(LogId.From("log_01"), completed.Cycle.LogId);
        Assert.Equal(LogState.PROCESSED, Log(execution.FinalShiftState, "log_01").State);
        Assert.Equal(LogId.From("log_02"), started.Cycle.LogId);
        Assert.Equal(LogState.IN_SAW, Log(execution.FinalShiftState, "log_02").State);
        Assert.Equal(tick, started.Cycle.StartedAt);

        // Two separate shift-version increments (completion + start); quota adds none.
        Assert.Equal(shift.StateVersion.Next().Next(), execution.FinalShiftState.StateVersion);
    }

    // ----- Automatic start across line states (start ownership remains with the existing service) -----

    [Fact]
    public void Queued_owner_starts_while_line_is_jammed_and_jam_state_is_preserved()
    {
        var shift = AutoFeedJammed();
        var quota = FreshQuota();
        Assert.Equal(LineState.LINE_JAMMED, shift.Line.State);
        var tick = ServerTick.From(12);

        var execution = Execute(shift, quota, tick);

        Assert.IsType<SawCycleNoActive>(execution.Completion.Result);
        Assert.False(execution.Quota.WasRequired);
        Assert.Null(execution.Quota.Result);
        Assert.Same(quota, execution.FinalQuotaState);

        var started = Assert.IsType<SawCycleStarted>(execution.Start.Result);
        Assert.Equal(LogId.From("log_02"), started.Cycle.LogId);
        Assert.Equal(tick, started.Cycle.StartedAt);
        Assert.Equal(LogState.IN_SAW, Log(execution.FinalShiftState, "log_02").State);
        Assert.Same(started.Cycle, execution.FinalShiftState.ActiveSawCycle);

        // Stage 4 added no line-clear precondition and altered no jam state: the exact line runtime is preserved.
        Assert.Equal(LineState.LINE_JAMMED, execution.FinalShiftState.Line.State);
        Assert.Same(shift.Line, execution.FinalShiftState.Line);
        Assert.Equal(JamCause.INTAKE_AUTOFEED_BLOCKED, execution.FinalShiftState.Line.Cause);
        Assert.Equal(LogId.From("log_01"), execution.FinalShiftState.Line.PendingLogId);
        Assert.Null(execution.FinalShiftState.Line.ActiveRepairHold);

        Assert.Equal(shift.StateVersion.Next(), execution.FinalShiftState.StateVersion);

        // Exact completion → start reference chain.
        Assert.Same(shift, execution.Completion.BeforeShiftState);
        Assert.Same(execution.Completion.Result.State, execution.Completion.AfterShiftState);
        Assert.Same(execution.Completion.AfterShiftState, execution.Start.BeforeShiftState);
        Assert.Same(execution.Start.Result.State, execution.FinalShiftState);
    }

    [Fact]
    public void Queued_owner_starts_while_line_is_repairing_and_repair_state_is_preserved()
    {
        var repairing = Assert.IsType<LineRepairStarted>(
            new LineRepairStartService().Start(AutoFeedJammed(), ServerTick.From(10), Fx.Shift.Scheduler)).State;
        var quota = FreshQuota();
        Assert.Equal(LineState.REPAIRING, repairing.Line.State);
        Assert.NotNull(repairing.Line.ActiveRepairHold);
        var tick = ServerTick.From(15);

        var execution = Execute(repairing, quota, tick);

        Assert.IsType<SawCycleNoActive>(execution.Completion.Result);
        Assert.False(execution.Quota.WasRequired);
        Assert.Null(execution.Quota.Result);
        Assert.Same(quota, execution.FinalQuotaState);

        var started = Assert.IsType<SawCycleStarted>(execution.Start.Result);
        Assert.Equal(LogId.From("log_02"), started.Cycle.LogId);
        Assert.Equal(tick, started.Cycle.StartedAt);
        Assert.Equal(LogState.IN_SAW, Log(execution.FinalShiftState, "log_02").State);
        Assert.Same(started.Cycle, execution.FinalShiftState.ActiveSawCycle);

        // Repair evidence remains: stage 4 does not complete, clear, or advance the repair.
        Assert.Equal(LineState.REPAIRING, execution.FinalShiftState.Line.State);
        Assert.Same(repairing.Line, execution.FinalShiftState.Line);
        Assert.Same(repairing.Line.ActiveRepairHold, execution.FinalShiftState.Line.ActiveRepairHold);

        Assert.Equal(repairing.StateVersion.Next(), execution.FinalShiftState.StateVersion);

        Assert.Same(repairing, execution.Completion.BeforeShiftState);
        Assert.Same(execution.Completion.Result.State, execution.Completion.AfterShiftState);
        Assert.Same(execution.Completion.AfterShiftState, execution.Start.BeforeShiftState);
        Assert.Same(execution.Start.Result.State, execution.FinalShiftState);
    }

    // ----- Continuation and failures -----

    [Fact]
    public void A_delegated_service_exception_propagates_without_a_partial_result()
    {
        var started = StartCycle(Queued(Create(), "log_01"), 10);
        var shift = started.State;

        // Completing before the cycle's own start tick is an invariant failure raised by the completion service.
        Assert.Throws<ArgumentOutOfRangeException>(() => Executor.Execute(shift, FreshQuota(), ServerTick.From(5), Fx.Shift.Scheduler, Fx.Anomalies));

        // The immutable input is unchanged.
        Assert.Equal(ServerTick.From(10), shift.ActiveSawCycle!.StartedAt);
    }

    // ----- Determinism -----

    [Fact]
    public void Independent_equivalent_executions_produce_value_equivalent_projections_and_states()
    {
        var (firstShift, firstTick) = DueCycleWithSuccessor();
        var (secondShift, secondTick) = DueCycleWithSuccessor();

        var first = Execute(firstShift, FreshQuota(), firstTick);
        var second = Execute(secondShift, FreshQuota(), secondTick);

        Assert.Equal(
            new[] { first.Completion.Result.GetType(), first.Start.Result.GetType() },
            new[] { second.Completion.Result.GetType(), second.Start.Result.GetType() });
        Assert.Equal(first.Quota.WasRequired, second.Quota.WasRequired);
        Assert.Equal(first.Quota.Result!.GetType(), second.Quota.Result!.GetType());
        Assert.True(first.FinalShiftState.ValueEquals(second.FinalShiftState));
        Assert.True(first.FinalQuotaState.ValueEquals(second.FinalQuotaState));
        Assert.NotSame(first.FinalShiftState, second.FinalShiftState);
        Assert.NotSame(first.FinalQuotaState, second.FinalQuotaState);
    }

    // ----- Stage separation -----

    [Fact]
    public void Stage_four_does_no_other_stage_work()
    {
        var (shift, tick) = DueCycleWithSuccessor();

        var execution = Execute(shift, FreshQuota(), tick);
        var final = execution.FinalShiftState;

        // No stage-2 accepted intents, no stage-5 feed, no stage-3 deadline, no jam/line derivation.
        Assert.True(final.ProcessedIntentIds.SetEquals(shift.ProcessedIntentIds));
        Assert.Equal(shift.PendingFeed, final.PendingFeed);
        Assert.Null(final.ActiveIntakeDeadline);
        Assert.Equal(LineState.LINE_CLEAR, final.Line.State);
        Assert.Equal(ContainmentState.STABLE, final.Containment.State);
        // Only the two saw-owned shift-version increments.
        Assert.Equal(shift.StateVersion.Next().Next(), final.StateVersion);
    }

    // ----- Helpers -----

    private static ShiftRuntimeState Create() => ShiftRuntimeState.Create(Fx.Shift);

    private static QuotaRuntimeState FreshQuota() => QuotaRuntimeState.Create(Fx.Shift);

    private static HostStageFourSawExecution Execute(ShiftRuntimeState shift, QuotaRuntimeState quota, ServerTick tick) =>
        Executor.Execute(shift, quota, tick, Fx.Shift.Scheduler, Fx.Anomalies);

    private static ShiftRuntimeState Queued(ShiftRuntimeState state, string logId)
    {
        state = RuntimeFixture.MoveHost(state, logId, LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, logId, LogState.AT_INTAKE);
        return RuntimeFixture.MoveHost(state, logId, LogState.QUEUED_FOR_SAW);
    }

    private static SawCycleStarted StartCycle(ShiftRuntimeState queuedState, long tick) =>
        Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(queuedState, ServerTick.From(tick), Fx.Shift.Scheduler));

    private static SawCycleCompleted Complete(string logId)
    {
        var started = StartCycle(Queued(Create(), logId), 10);
        return Assert.IsType<SawCycleCompleted>(new SawCycleCompletionService().Complete(started.State, started.Cycle.DueAt, Fx.Anomalies));
    }

    private static (ShiftRuntimeState Shift, ServerTick Tick) DueCycleWithSuccessor()
    {
        var started = StartCycle(Queued(Create(), "log_01"), 10);
        var withSuccessor = Queued(started.State, "log_02");
        return (withSuccessor, started.Cycle.DueAt);
    }

    private static ShiftRuntimeState AutoFeedJammed()
    {
        // Auto-feed-blocked jam via the existing public line service: one queued saw owner, one intake log, no cycle.
        var state = RuntimeFixture.MoveToIntake(Create(), "log_02");
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveToIntake(state, "log_01");
        return Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.INTAKE_AUTOFEED_BLOCKED, ServerTick.From(10))).State;
    }

    private static ShiftRuntimeState PreparedPenitentQueued()
    {
        var state = RuntimeFixture.MoveToIntake(Create(), "log_03");
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_PROCEDURE);
        var started = Assert.IsType<ProcedureActionHoldStarted>(new ProcedureActionStartService().Start(
            state, LogId.From("log_03"), ItemId.From("holy_water"), ServerTick.From(1), Fx.Anomalies));
        var granted = Assert.IsType<ProcedureActionDueCompleted>(new ProcedureActionDueCompletionService().CompleteDue(
            started.State, started.Hold.DueAt, Fx.Anomalies)).State;
        granted = RuntimeFixture.MoveHost(granted, "log_03", LogState.AT_INTAKE);
        return RuntimeFixture.MoveHost(granted, "log_03", LogState.QUEUED_FOR_SAW);
    }

    private static void AssertConstructorThrows(ConstructorInfo constructor, object?[] arguments)
    {
        var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(arguments));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    private static LogRuntimeState Log(ShiftRuntimeState state, string logId)
    {
        Assert.True(state.TryGetLog(LogId.From(logId), out var log));
        return log;
    }
}
