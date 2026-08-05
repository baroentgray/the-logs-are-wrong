using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-032")]
public sealed class HostStageThreeDeadlineExecutionTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static readonly HostStageThreeDeadlineExecutor Executor = new();

    // ----- API and construction -----

    [Fact]
    public void Null_and_default_arguments_reject_before_execution()
    {
        var state = RuntimeFixture.CreateInitialState();

        Assert.Throws<ArgumentNullException>(() => Executor.Execute(null!, ServerTick.From(5), Fx.Shift.Containment, Fx.Anomalies));
        Assert.Throws<ArgumentException>(() => Executor.Execute(state, default, Fx.Shift.Containment, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => Executor.Execute(state, ServerTick.From(5), null!, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => Executor.Execute(state, ServerTick.From(5), Fx.Shift.Containment, null!));
        Assert.Throws<ArgumentNullException>(() => Executor.Execute(state, ServerTick.From(5), Fx.Shift.Containment, new AnomalyCatalog(null!)));
    }

    [Fact]
    public void Executor_execute_accepts_only_state_tick_configuration_and_catalog()
    {
        var execute = Assert.Single(
            typeof(HostStageThreeDeadlineExecutor).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => method.Name == "Execute");

        Assert.Equal(typeof(HostStageThreeDeadlineExecution), execute.ReturnType);
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(ServerTick), typeof(ContainmentConfiguration), typeof(AnomalyCatalog) },
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
            typeof(HostStageThreeDeadlineExecution),
            typeof(IntakeDeadlineExpirationStageStep),
            typeof(ContainmentDeadlineAdvanceStageStep)
        };

        Assert.All(types, type =>
        {
            Assert.True(type.IsSealed);
            Assert.Empty(type.GetConstructors(publicInstance));
            Assert.Empty(type.GetFields(publicInstance));
            Assert.All(type.GetProperties(publicInstance), property => Assert.Null(property.SetMethod));
        });
    }

    // ----- All-no-op stage -----

    [Fact]
    public void All_no_op_evaluates_both_families_and_preserves_the_exact_initial_state()
    {
        var state = RuntimeFixture.CreateInitialState();

        var execution = Execute(state, ServerTick.From(5));

        Assert.IsType<IntakeDeadlineNoActiveDeadline>(execution.IntakeDeadline.Result);
        Assert.IsType<ContainmentAdvanceNoChange>(execution.Containment.Result);
        Assert.Same(state, execution.InitialState);
        Assert.Same(state, execution.IntakeDeadline.BeforeState);
        Assert.Same(state, execution.IntakeDeadline.AfterState);
        Assert.Same(state, execution.Containment.BeforeState);
        Assert.Same(state, execution.Containment.AfterState);
        Assert.Same(state, execution.FinalState);
        Assert.Equal(state.StateVersion, execution.FinalState.StateVersion);
    }

    // ----- Intake variants -----

    [Fact]
    public void Intake_not_due_exact_due_and_late_expiration_variants_flow_through_the_stage()
    {
        var started = StartInitialDeadline();

        var notDue = Execute(started.State, ServerTick.From(59));
        Assert.IsType<IntakeDeadlineNotDueYet>(notDue.IntakeDeadline.Result);
        Assert.Same(started.State, notDue.FinalState);

        var expiredExecution = Execute(started.State, ServerTick.From(60));
        var expired = Assert.IsType<IntakeDeadlineExpired>(expiredExecution.IntakeDeadline.Result);
        Assert.Equal(started.Deadline, expired.ExpiredDeadline);
        Assert.Equal(new DefaultAutoRouteRequired(LogId.From("log_01"), ServerTick.From(60)), expired.FollowUp);
        Assert.Null(expiredExecution.FinalState.ActiveIntakeDeadline);
        Assert.Equal(LogState.AT_INTAKE, Log(expiredExecution.FinalState, "log_01").State);
        Assert.Equal(started.State.StateVersion.Next(), expiredExecution.FinalState.StateVersion);
        Assert.IsType<ContainmentAdvanceNoChange>(expiredExecution.Containment.Result);
        // Route is not executed; no jam/feed/saw work occurs.
        Assert.Null(expiredExecution.FinalState.PendingFeed);
        Assert.Null(expiredExecution.FinalState.ActiveSawCycle);
        Assert.Equal(LineState.LINE_CLEAR, expiredExecution.FinalState.Line.State);

        var lateExecution = Execute(started.State, ServerTick.From(61));
        var late = Assert.IsType<IntakeDeadlineExpired>(lateExecution.IntakeDeadline.Result);
        Assert.Equal(ServerTick.From(61), late.ExpiredAt);
        Assert.Equal(ServerTick.From(60), late.ExpiredDeadline.DueAt);
    }

    // ----- Containment variants -----

    [Fact]
    public void Containment_no_change_armed_advance_incident_and_ritual_variants_flow_through_the_stage()
    {
        Assert.IsType<ContainmentAdvanceNoChange>(Execute(RuntimeFixture.CreateInitialState(), ServerTick.From(10)).Containment.Result);

        var written = WriteOff(RuntimeFixture.CreateInitialState(), "log_03");
        Assert.IsType<ContainmentStableIntervalArmed>(Execute(written, ServerTick.From(10)).Containment.Result);

        Assert.IsType<ContainmentAdvanceNoChange>(Execute(Armed(), ServerTick.From(99)).Containment.Result);
        Assert.IsType<ContainmentStateAdvanced>(Execute(Armed(), ServerTick.From(100)).Containment.Result);
        Assert.IsType<ContainmentStateAdvanced>(Execute(ToRequest(), ServerTick.From(120)).Containment.Result);

        var incidentExecution = Execute(ToOverdue(), ServerTick.From(130));
        var incident = Assert.IsType<ContainmentIncidentEntered>(incidentExecution.Containment.Result);
        Assert.NotNull(incident.Descriptor);
        // The incident descriptor is data only; stage 3 applies no forced line pause.
        Assert.Equal(LineState.LINE_CLEAR, incidentExecution.FinalState.Line.State);

        Assert.IsType<ContainmentRitualCompletionRequired>(Execute(RitualStarted(), ServerTick.From(114)).Containment.Result);
    }

    // ----- Simultaneous work and chaining -----

    [Fact]
    public void Simultaneous_deadline_work_runs_intake_then_containment_with_exact_chaining()
    {
        var (mixed, tick) = BuildSimultaneousState();

        var execution = Execute(mixed, tick);

        Assert.Same(mixed, execution.InitialState);
        Assert.Same(mixed, execution.IntakeDeadline.BeforeState);
        Assert.Same(execution.IntakeDeadline.AfterState, execution.Containment.BeforeState);
        Assert.Same(execution.Containment.AfterState, execution.FinalState);

        var expired = Assert.IsType<IntakeDeadlineExpired>(execution.IntakeDeadline.Result);
        Assert.IsType<ContainmentStateAdvanced>(execution.Containment.Result);

        // Both service-owned version increments occur sequentially; the executor assigns none itself.
        Assert.Equal(mixed.StateVersion.Next().Next(), execution.FinalState.StateVersion);
        // The default-route follow-up is retained as data only and the owner remains at intake.
        Assert.NotNull(expired.FollowUp);
        Assert.Equal(LogState.AT_INTAKE, Log(execution.FinalState, "log_01").State);
    }

    [Fact]
    public void A_no_op_from_either_family_does_not_suppress_the_other()
    {
        var intakeNoOp = Execute(Armed(), ServerTick.From(100));
        Assert.IsType<IntakeDeadlineNoActiveDeadline>(intakeNoOp.IntakeDeadline.Result);
        Assert.IsType<ContainmentStateAdvanced>(intakeNoOp.Containment.Result);

        var containmentNoOp = Execute(StartInitialDeadline().State, ServerTick.From(60));
        Assert.IsType<IntakeDeadlineExpired>(containmentNoOp.IntakeDeadline.Result);
        Assert.IsType<ContainmentAdvanceNoChange>(containmentNoOp.Containment.Result);
    }

    // ----- Exception behavior -----

    [Fact]
    public void Exception_from_a_delegated_service_propagates_without_a_partial_result()
    {
        var armed = Armed();

        // Containment advancement rejects a tick that precedes its entry; intake runs first as a no-op.
        Assert.Throws<ArgumentOutOfRangeException>(() => Executor.Execute(armed, ServerTick.From(5), Fx.Shift.Containment, Fx.Anomalies));

        // The immutable input is unchanged.
        Assert.Equal(ServerTick.From(10), armed.Containment.EnteredAt);
    }

    // ----- Determinism -----

    [Fact]
    public void Independent_equivalent_executions_produce_value_equivalent_projections_and_final_state()
    {
        var (firstState, firstTick) = BuildSimultaneousState();
        var (secondState, secondTick) = BuildSimultaneousState();

        var first = Execute(firstState, firstTick);
        var second = Execute(secondState, secondTick);

        Assert.Equal(
            new[] { first.IntakeDeadline.Result.GetType(), first.Containment.Result.GetType() },
            new[] { second.IntakeDeadline.Result.GetType(), second.Containment.Result.GetType() });
        Assert.True(first.FinalState.ValueEquals(second.FinalState));
        Assert.NotSame(first.FinalState, second.FinalState);
    }

    // ----- Stage separation -----

    [Fact]
    public void Stage_three_does_no_other_stage_work()
    {
        var (mixed, tick) = BuildSimultaneousState();

        var execution = Execute(mixed, tick);
        var final = execution.FinalState;

        Assert.True(final.ProcessedIntentIds.SetEquals(mixed.ProcessedIntentIds));
        Assert.Null(final.ActiveSawCycle);
        Assert.Equal(LogState.AT_INTAKE, Log(final, "log_01").State);
        Assert.Null(final.PendingFeed);
        Assert.Equal(LineState.LINE_CLEAR, final.Line.State);
        Assert.Equal(mixed.StateVersion.Next().Next(), final.StateVersion);
    }

    // ----- Helpers -----

    private static HostStageThreeDeadlineExecution Execute(ShiftRuntimeState state, ServerTick tick) =>
        Executor.Execute(state, tick, Fx.Shift.Containment, Fx.Anomalies);

    private static IntakeDeadlineStarted StartInitialDeadline()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, Fx.Shift.Scheduler));
        var admission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        return Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, Fx.Shift.Profiles[ProfileId.From("learning")]));
    }

    private static (ShiftRuntimeState State, ServerTick Tick) BuildSimultaneousState()
    {
        // Containment danger armed at tick 10 → deadline 100.
        var state = WriteOff(RuntimeFixture.CreateInitialState(), "log_03");
        state = Assert.IsType<ContainmentStableIntervalArmed>(new ContainmentAdvanceService().Advance(
            state, ServerTick.From(10), Fx.Shift.Containment, Fx.Anomalies)).State;

        // Admit log_01 to intake via a normal feed and start a learning deadline due at 100.
        var planned = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(state, ServerTick.From(35), Fx.Shift.Scheduler));
        var admission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.From(40)));
        var started = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(
            admission.State, admission, Fx.Shift.Profiles[ProfileId.From("learning")]));

        return (started.State, ServerTick.From(100));
    }

    private static ShiftRuntimeState WriteOff(ShiftRuntimeState state, string logId)
    {
        state = RuntimeFixture.MoveToIntake(state, logId);
        return RuntimeFixture.MoveHost(state, logId, LogState.HELD_WRITTEN_OFF);
    }

    private static ShiftRuntimeState Armed() =>
        Assert.IsType<ContainmentStableIntervalArmed>(new ContainmentAdvanceService().Advance(
            WriteOff(RuntimeFixture.CreateInitialState(), "log_03"), ServerTick.From(10), Fx.Shift.Containment, Fx.Anomalies)).State;

    private static ShiftRuntimeState ToRequest() =>
        Assert.IsType<ContainmentStateAdvanced>(new ContainmentAdvanceService().Advance(
            Armed(), ServerTick.From(100), Fx.Shift.Containment, Fx.Anomalies)).State;

    private static ShiftRuntimeState ToOverdue() =>
        Assert.IsType<ContainmentStateAdvanced>(new ContainmentAdvanceService().Advance(
            ToRequest(), ServerTick.From(120), Fx.Shift.Containment, Fx.Anomalies)).State;

    private static ShiftRuntimeState RitualStarted() =>
        Assert.IsType<ContainmentRitualStarted>(new ContainmentRitualStartService().Start(
            ToRequest(), ServerTick.From(110), Fx.Shift.Containment)).State;

    private static LogRuntimeState Log(ShiftRuntimeState state, string logId)
    {
        Assert.True(state.TryGetLog(LogId.From(logId), out var log));
        return log;
    }
}
