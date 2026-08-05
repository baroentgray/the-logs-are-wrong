using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-031")]
public sealed class HostStageOneCompletionExecutionTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static readonly HostStageOneCompletionExecutor Executor = new();

    // ----- API and construction -----

    [Fact]
    public void Null_and_default_arguments_reject_before_execution()
    {
        var state = RuntimeFixture.CreateInitialState();

        Assert.Throws<ArgumentNullException>(() => Executor.Execute(null!, ServerTick.From(5), Fx.Anomalies, Fx.Shift.Containment));
        Assert.Throws<ArgumentException>(() => Executor.Execute(state, default, Fx.Anomalies, Fx.Shift.Containment));
        Assert.Throws<ArgumentNullException>(() => Executor.Execute(state, ServerTick.From(5), null!, Fx.Shift.Containment));
        Assert.Throws<ArgumentNullException>(() => Executor.Execute(state, ServerTick.From(5), Fx.Anomalies, null!));
    }

    [Fact]
    public void Executor_execute_accepts_only_state_tick_catalog_and_configuration()
    {
        var execute = Assert.Single(
            typeof(HostStageOneCompletionExecutor).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => method.Name == "Execute");

        Assert.Equal(typeof(HostStageOneCompletionExecution), execute.ReturnType);
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(ServerTick), typeof(AnomalyCatalog), typeof(ContainmentConfiguration) },
            execute.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(execute.GetParameters(), parameter =>
            parameter.ParameterType == typeof(object) ||
            parameter.ParameterType == typeof(bool) ||
            parameter.ParameterType == typeof(string) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
    }

    [Fact]
    public void Steps_and_result_are_sealed_immutable_and_non_publicly_constructible()
    {
        var publicInstance = BindingFlags.Public | BindingFlags.Instance;
        var types = new[]
        {
            typeof(HostStageOneCompletionExecution),
            typeof(ProcedureDueCompletionStageStep),
            typeof(ConfirmationDueCompletionStageStep),
            typeof(ContainmentRitualDueCompletionStageStep),
            typeof(LineRepairDueCompletionStageStep)
        };

        Assert.All(types, type =>
        {
            Assert.True(type.IsSealed);
            Assert.Empty(type.GetConstructors(publicInstance));
            Assert.Empty(type.GetFields(publicInstance));
            Assert.All(type.GetProperties(publicInstance), property => Assert.Null(property.SetMethod));
        });
    }

    // ----- Empty / no-op stage -----

    [Fact]
    public void All_no_active_evaluates_every_family_and_preserves_the_exact_initial_state()
    {
        var state = RuntimeFixture.CreateInitialState();

        var execution = Execute(state, ServerTick.From(5));

        Assert.IsType<ProcedureActionNoActiveHold>(execution.Procedure.Result);
        Assert.IsType<ConfirmationTestNoActive>(execution.Confirmation.Result);
        Assert.IsType<ContainmentRitualNoActive>(execution.ContainmentRitual.Result);
        Assert.IsType<LineRepairNoActive>(execution.LineRepair.Result);

        Assert.Same(state, execution.InitialState);
        Assert.Same(state, execution.Procedure.BeforeState);
        Assert.Same(state, execution.Procedure.AfterState);
        Assert.Same(state, execution.Confirmation.BeforeState);
        Assert.Same(state, execution.Confirmation.AfterState);
        Assert.Same(state, execution.ContainmentRitual.BeforeState);
        Assert.Same(state, execution.ContainmentRitual.AfterState);
        Assert.Same(state, execution.LineRepair.BeforeState);
        Assert.Same(state, execution.LineRepair.AfterState);
        Assert.Same(state, execution.FinalState);
        Assert.Equal(state.StateVersion, execution.FinalState.StateVersion);
    }

    // ----- Individual existing variants -----

    [Fact]
    public void Procedure_not_due_and_due_completed_variants_flow_through_the_stage()
    {
        var (held, due) = ProcedureHold(20);

        var notDue = Execute(held, ServerTick.From(20));
        Assert.IsType<ProcedureActionNotDue>(notDue.Procedure.Result);
        Assert.Same(held, notDue.FinalState);

        var completed = Execute(held, due);
        var done = Assert.IsType<ProcedureActionDueCompleted>(completed.Procedure.Result);
        Assert.Same(done.State, completed.Procedure.AfterState);
        Assert.Same(done.State, completed.FinalState);
        Assert.Equal(held.StateVersion.Next(), completed.FinalState.StateVersion);
        Assert.IsType<ConfirmationTestNoActive>(completed.Confirmation.Result);
        Assert.IsType<ContainmentRitualNoActive>(completed.ContainmentRitual.Result);
        Assert.IsType<LineRepairNoActive>(completed.LineRepair.Result);
    }

    [Fact]
    public void Confirmation_not_due_and_due_completed_variants_flow_through_the_stage()
    {
        var (running, due) = ConfirmationRunning("log_06", 10);

        var notDue = Execute(running, ServerTick.From(10));
        Assert.IsType<ConfirmationTestNotDue>(notDue.Confirmation.Result);
        Assert.Same(running, notDue.FinalState);

        var completed = Execute(running, due);
        Assert.IsType<ConfirmationTestDueCompleted>(completed.Confirmation.Result);
        Assert.Equal(running.StateVersion.Next(), completed.FinalState.StateVersion);
    }

    [Fact]
    public void Confirmation_paused_variant_flows_through_the_stage()
    {
        var intake = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_03");
        var started = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(
            intake,
            LogId.From("log_03"),
            ImmutableHashSet.Create(ItemId.From("sound_meter")),
            ServerTick.From(10),
            LineNoiseRuntimeState.Create(intake.ShiftId),
            Fx.Anomalies)).State;
        var loud = LoudEvaluation(ServerTick.From(12));
        var paused = Assert.IsType<ConfirmationTestConditionUpdated>(new ConfirmationTestConditionService().Update(
            started,
            ServerTick.From(12),
            loud,
            ImmutableHashSet.Create(ItemId.From("sound_meter")),
            Fx.Anomalies)).State;

        var execution = Execute(paused, ServerTick.From(13));

        Assert.IsType<ConfirmationTestPaused>(execution.Confirmation.Result);
        Assert.Same(paused, execution.FinalState);
    }

    [Fact]
    public void Containment_ritual_not_due_and_completed_variants_flow_through_the_stage()
    {
        var (started, due) = RitualStarted(110);

        var notDue = Execute(started, ServerTick.From(113));
        Assert.IsType<ContainmentRitualNotDue>(notDue.ContainmentRitual.Result);
        Assert.Same(started, notDue.FinalState);

        var completed = Execute(started, due);
        Assert.IsType<ContainmentRitualCompleted>(completed.ContainmentRitual.Result);
        Assert.Equal(started.StateVersion.Next(), completed.FinalState.StateVersion);
    }

    [Fact]
    public void Repair_variants_flow_through_the_stage_including_pending_and_no_pending_completions()
    {
        var repairing = FeedRepairing(10);
        Assert.IsType<LineRepairNotDue>(Execute(repairing, ServerTick.From(15)).LineRepair.Result);
        Assert.IsType<LineRepairBlockingConditionRemains>(Execute(repairing, ServerTick.From(16)).LineRepair.Result);

        var unblocked = RuntimeFixture.MoveHost(repairing, "log_01", LogState.AT_PROCEDURE);
        var completedWith = Execute(unblocked, ServerTick.From(16));
        var withPending = Assert.IsType<LineRepairCompleted>(completedWith.LineRepair.Result);
        Assert.NotNull(withPending.PendingTransition);
        Assert.Equal(LogId.From("log_02"), withPending.PendingTransition!.LogId);
        Assert.True(completedWith.FinalState.TryGetLog(LogId.From("log_02"), out var stillPending));
        Assert.Equal(LogState.AT_FEED_GATE, stillPending.State);

        var auto = AutoRepairing(10);
        var targetMoved = RuntimeFixture.MoveHost(auto, "log_01", LogState.AT_PROCEDURE);
        var autoUnblocked = RuntimeFixture.MoveHost(targetMoved, "log_02", LogState.IN_SAW);
        var completedWithout = Execute(autoUnblocked, ServerTick.From(16));
        var noPending = Assert.IsType<LineRepairCompleted>(completedWithout.LineRepair.Result);
        Assert.Null(noPending.PendingTransition);
    }

    // ----- Mixed simultaneous completions -----

    [Fact]
    public void Mixed_due_completions_run_in_fixed_order_and_no_ops_do_not_stop_later_families()
    {
        var (mixed, tick) = BuildMixedDueState();

        var execution = Execute(mixed, tick);

        // Fixed order and exact before/after chain.
        Assert.Same(mixed, execution.InitialState);
        Assert.Same(mixed, execution.Procedure.BeforeState);
        Assert.Same(execution.Procedure.AfterState, execution.Confirmation.BeforeState);
        Assert.Same(execution.Confirmation.AfterState, execution.ContainmentRitual.BeforeState);
        Assert.Same(execution.ContainmentRitual.AfterState, execution.LineRepair.BeforeState);
        Assert.Same(execution.LineRepair.AfterState, execution.FinalState);

        // Procedure completes first; confirmation and containment are no-ops between; repair still completes.
        Assert.IsType<ProcedureActionDueCompleted>(execution.Procedure.Result);
        Assert.IsType<ConfirmationTestNoActive>(execution.Confirmation.Result);
        Assert.IsType<ContainmentRitualNoActive>(execution.ContainmentRitual.Result);
        var repair = Assert.IsType<LineRepairCompleted>(execution.LineRepair.Result);

        // The no-op families pass the exact state through unchanged.
        Assert.Same(execution.Procedure.AfterState, execution.Confirmation.AfterState);
        Assert.Same(execution.Confirmation.AfterState, execution.ContainmentRitual.AfterState);

        // Version advances exactly twice, only through the two completing services.
        Assert.Equal(mixed.StateVersion.Next().Next(), execution.FinalState.StateVersion);

        // Repair pending transition is data-only: the pending log is not moved.
        Assert.NotNull(repair.PendingTransition);
        Assert.True(execution.FinalState.TryGetLog(LogId.From("log_02"), out var pendingLog));
        Assert.Equal(LogState.AT_FEED_GATE, pendingLog.State);
    }

    [Fact]
    public void Independent_equivalent_executions_produce_value_equivalent_projections_and_final_state()
    {
        var (firstState, firstTick) = BuildMixedDueState();
        var (secondState, secondTick) = BuildMixedDueState();

        var first = Execute(firstState, firstTick);
        var second = Execute(secondState, secondTick);

        Assert.Equal(
            new[] { first.Procedure.Result.GetType(), first.Confirmation.Result.GetType(), first.ContainmentRitual.Result.GetType(), first.LineRepair.Result.GetType() },
            new[] { second.Procedure.Result.GetType(), second.Confirmation.Result.GetType(), second.ContainmentRitual.Result.GetType(), second.LineRepair.Result.GetType() });
        Assert.True(first.FinalState.ValueEquals(second.FinalState));
        Assert.NotSame(first.FinalState, second.FinalState);
    }

    // ----- Stage separation -----

    [Fact]
    public void Stage_one_does_no_stage_two_through_seven_work()
    {
        var (mixed, tick) = BuildMixedDueState();

        var execution = Execute(mixed, tick);
        var final = execution.FinalState;

        // No accepted-intent processing: the processed-intent set is not extended.
        Assert.True(final.ProcessedIntentIds.SetEquals(mixed.ProcessedIntentIds));
        // No feed planning/admission (stage 5), no saw start (stage 4), no intake deadline expiry (stage 3).
        Assert.Null(final.PendingFeed);
        Assert.Null(final.ActiveSawCycle);
        Assert.Null(final.ActiveIntakeDeadline);
        // Repair pending-transition execution (stage 5) did not run: the pending log stays put.
        Assert.True(final.TryGetLog(LogId.From("log_02"), out var pendingLog));
        Assert.Equal(LogState.AT_FEED_GATE, pendingLog.State);
        // The line-repair completion cleared the line without a further derived transition.
        Assert.Equal(LineState.LINE_CLEAR, final.Line.State);
        // No direct version assignment beyond the two completing services.
        Assert.Equal(mixed.StateVersion.Next().Next(), final.StateVersion);
    }

    // ----- Typed-failure continuation -----

    [Fact]
    public void Procedure_typed_failure_continues_later_families_with_exact_failure_state()
    {
        var (held, due) = ProcedureHold(20);
        var emptyCatalog = new AnomalyCatalog(ImmutableDictionary<AnomalyId, AnomalyDefinition>.Empty);

        // The empty catalog makes the due procedure hold fail to resolve its plan, without mutating state.
        var execution = Executor.Execute(held, due, emptyCatalog, Fx.Shift.Containment);

        var failed = Assert.IsType<ProcedureActionDueCompletionFailed>(execution.Procedure.Result);
        Assert.Equal(ProcedureActionDueCompletionFailureReason.NoProcedurePlan, failed.Reason);
        Assert.Null(failed.CompletionRejection);
        Assert.Same(held, failed.State);
        Assert.Same(held, execution.Procedure.BeforeState);
        Assert.Same(held, execution.Procedure.AfterState);

        // The typed failure does not stop the stage: the later three families still execute in fixed order.
        Assert.IsType<ConfirmationTestNoActive>(execution.Confirmation.Result);
        Assert.IsType<ContainmentRitualNoActive>(execution.ContainmentRitual.Result);
        Assert.IsType<LineRepairNoActive>(execution.LineRepair.Result);

        // Every later no-op retains the exact original held-state reference.
        Assert.Same(held, execution.Confirmation.BeforeState);
        Assert.Same(held, execution.Confirmation.AfterState);
        Assert.Same(held, execution.ContainmentRitual.BeforeState);
        Assert.Same(held, execution.ContainmentRitual.AfterState);
        Assert.Same(held, execution.LineRepair.BeforeState);
        Assert.Same(held, execution.LineRepair.AfterState);

        Assert.Same(held, execution.FinalState);
        Assert.Equal(held.StateVersion, execution.FinalState.StateVersion);
    }

    // ----- Helpers -----

    private static HostStageOneCompletionExecution Execute(ShiftRuntimeState state, ServerTick tick) =>
        Executor.Execute(state, tick, Fx.Anomalies, Fx.Shift.Containment);

    private static (ShiftRuntimeState State, ServerTick Due) ProcedureHold(long startTick)
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_03");
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_PROCEDURE);
        var started = Assert.IsType<ProcedureActionHoldStarted>(new ProcedureActionStartService().Start(
            state, LogId.From("log_03"), ItemId.From("holy_water"), ServerTick.From(startTick), Fx.Anomalies));
        return (started.State, started.Hold.DueAt);
    }

    private static (ShiftRuntimeState State, ServerTick Due) ConfirmationRunning(string logId, long startTick)
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), logId);
        var started = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(
            state,
            LogId.From(logId),
            ImmutableHashSet.Create(ItemId.From("choir_cassette")),
            ServerTick.From(startTick),
            LineNoiseRuntimeState.Create(state.ShiftId),
            Fx.Anomalies)).State;
        var active = Assert.IsType<ActiveConfirmationTest>(started.ActiveConfirmationTest);
        return (started, active.DueAt!.Value);
    }

    private static (ShiftRuntimeState State, ServerTick Due) RitualStarted(long startTick)
    {
        var request = ContainmentRequest();
        var started = Assert.IsType<ContainmentRitualStarted>(new ContainmentRitualStartService().Start(
            request, ServerTick.From(startTick), Fx.Shift.Containment));
        return (started.State, started.Ritual.DueAt);
    }

    private static ShiftRuntimeState ContainmentRequest()
    {
        var written = WriteOff(RuntimeFixture.CreateInitialState(), "log_03");
        var armed = Assert.IsType<ContainmentStableIntervalArmed>(new ContainmentAdvanceService().Advance(
            written, ServerTick.From(10), Fx.Shift.Containment, Fx.Anomalies)).State;
        return Assert.IsType<ContainmentStateAdvanced>(new ContainmentAdvanceService().Advance(
            armed, ServerTick.From(100), Fx.Shift.Containment, Fx.Anomalies)).State;
    }

    private static ShiftRuntimeState WriteOff(ShiftRuntimeState state, string logId)
    {
        state = RuntimeFixture.MoveToIntake(state, logId);
        return RuntimeFixture.MoveHost(state, logId, LogState.HELD_WRITTEN_OFF);
    }

    private static ShiftRuntimeState FeedRepairing(long tick)
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
        var jammed = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.FEED_GATE_BLOCKED, ServerTick.From(tick))).State;
        return Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jammed, ServerTick.From(tick), Fx.Shift.Scheduler)).State;
    }

    private static ShiftRuntimeState AutoRepairing(long tick)
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_02");
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveToIntake(state, "log_01");
        var jammed = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.INTAKE_AUTOFEED_BLOCKED, ServerTick.From(tick))).State;
        return Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jammed, ServerTick.From(tick), Fx.Shift.Scheduler)).State;
    }

    private static (ShiftRuntimeState State, ServerTick Tick) BuildMixedDueState()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_03");
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_PROCEDURE);
        var procStart = Assert.IsType<ProcedureActionHoldStarted>(new ProcedureActionStartService().Start(
            state, LogId.From("log_03"), ItemId.From("holy_water"), ServerTick.From(20), Fx.Anomalies));
        var tick = procStart.Hold.DueAt;
        state = procStart.State;

        state = RuntimeFixture.MoveToIntake(state, "log_01");
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
        var repairStart = ServerTick.From(tick.Value - 6);
        var jammed = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.FEED_GATE_BLOCKED, repairStart)).State;
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jammed, repairStart, Fx.Shift.Scheduler)).State;
        var mixed = RuntimeFixture.MoveHost(repairing, "log_01", LogState.QUEUED_FOR_SAW);
        return (mixed, tick);
    }

    private static LineNoiseEvaluationResult LoudEvaluation(ServerTick tick)
    {
        var source = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        source = RuntimeFixture.MoveHost(source, "log_01", LogState.QUEUED_FOR_SAW);
        source = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(source, tick, Fx.Shift.Scheduler)).State;
        return new LineNoiseDerivationService().Evaluate(
            LineNoiseRuntimeState.Create(source.ShiftId),
            source,
            MovementNoiseRuntimeState.Create(source.ShiftId),
            tick);
    }
}
