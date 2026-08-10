using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Tests.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Scheduler;

[Trait("Scope", "TLAW-047")]
public sealed class PenitentSawFailureWindowTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();

    [Fact]
    public void Window_is_immutable_uses_a_half_open_interval_and_fails_closed_on_invalid_timing()
    {
        var window = new SawFailureWindow(ServerTick.From(10), SimulationDuration.FromTicks(8));

        Assert.True(window.IsActiveAt(ServerTick.From(10)));
        Assert.True(window.IsActiveAt(ServerTick.From(17)));
        Assert.False(window.IsActiveAt(ServerTick.From(18)));
        Assert.DoesNotContain(typeof(SawFailureWindow).GetProperties(), property => property.SetMethod is not null);
        Assert.Throws<ArgumentException>(() => new SawFailureWindow(default, SimulationDuration.FromTicks(8)));
        Assert.Throws<ArgumentException>(() => new SawFailureWindow(ServerTick.Zero, SimulationDuration.Zero));
        Assert.Throws<OverflowException>(() => new SawFailureWindow(ServerTick.From(long.MaxValue), SimulationDuration.FromTicks(1)));
    }

    [Fact]
    public void Incorrect_penitent_completion_installs_the_exact_eight_second_window_without_a_second_mutation()
    {
        var started = Start(Queued("log_03"), ServerTick.From(10));

        var completion = Assert.IsType<SawCycleCompleted>(
            new SawCycleCompletionService().Complete(started.State, started.Cycle.DueAt, Fx.Anomalies));

        var window = Assert.IsType<SawFailureWindow>(completion.State.ActiveSawFailureWindow);
        Assert.Equal((ServerTick.From(16), 8L, ServerTick.From(24)), (window.StartedAt, window.Duration.Value, window.DueAt));
        Assert.Equal(LogState.PROCESSED, Log(completion.State, "log_03").State);
        Assert.False(completion.Resolution.AllRequiredFlagsPresent);
        Assert.Equal(0, completion.Resolution.Settlement.CreditedUnits);
        Assert.Equal(started.State.StateVersion.Next(), completion.State.StateVersion);
        Assert.Equal(completion.PriorStateVersion.Next(), completion.CurrentStateVersion);
    }

    [Fact]
    public void Active_window_blocks_only_saw_start_through_due_exclusive_and_then_allows_the_ordinary_start()
    {
        var started = Start(Queued("log_03"), ServerTick.From(10));
        var withSuccessor = Queue(started.State, "log_04");
        var completion = Assert.IsType<SawCycleCompleted>(
            new SawCycleCompletionService().Complete(withSuccessor, started.Cycle.DueAt, Fx.Anomalies));
        var window = Assert.IsType<SawFailureWindow>(completion.State.ActiveSawFailureWindow);
        var service = new SawCycleStartService();

        foreach (var tick in Enumerable.Range(0, 8).Select(offset => ServerTick.From(window.StartedAt.Value + offset)))
        {
            var blocked = Assert.IsType<SawCycleStartBlockedByFailureWindow>(service.Start(completion.State, tick, Fx.Shift.Scheduler));
            Assert.Same(completion.State, blocked.State);
            Assert.Same(window, blocked.Window);
            Assert.Equal(completion.State.StateVersion, blocked.State.StateVersion);
        }

        var resumed = Assert.IsType<SawCycleStarted>(service.Start(completion.State, window.DueAt, Fx.Shift.Scheduler));
        Assert.Equal(LogId.From("log_04"), resumed.Cycle.LogId);
        Assert.Equal(window.DueAt, resumed.Cycle.StartedAt);
        Assert.Equal(completion.State.StateVersion.Next(), resumed.State.StateVersion);

        var catchUp = Assert.IsType<SawCycleStarted>(service.Start(completion.State, ServerTick.From(window.DueAt.Value + 5), Fx.Shift.Scheduler));
        Assert.Equal(ServerTick.From(window.DueAt.Value + 5), catchUp.Cycle.StartedAt);
    }

    [Fact]
    public void Only_the_exact_incorrect_penitent_resolution_can_create_a_failure_window()
    {
        var completion = new SawCycleCompletionService();

        var normal = Assert.IsType<SawCycleCompleted>(completion.Complete(Start(Queued("log_01"), ServerTick.Zero).State, ServerTick.From(6), Fx.Anomalies));
        var resin = Assert.IsType<SawCycleCompleted>(completion.Complete(Start(Queued("log_06"), ServerTick.Zero).State, ServerTick.From(6), Fx.Anomalies));
        var falseSpecies = Assert.IsType<SawCycleCompleted>(completion.Complete(Start(Queued("log_05"), ServerTick.Zero).State, ServerTick.From(6), Fx.Anomalies));

        Assert.Null(normal.State.ActiveSawFailureWindow);
        Assert.Null(resin.State.ActiveSawFailureWindow);
        Assert.Null(falseSpecies.State.ActiveSawFailureWindow);
    }

    [Fact]
    public void Stage_four_keeps_completion_then_quota_then_typed_blocked_start_for_incorrect_penitent()
    {
        var started = Start(Queued("log_03"), ServerTick.From(10));
        var withSuccessor = Queue(started.State, "log_04");
        var beforeVersion = withSuccessor.StateVersion;

        var execution = new HostStageFourSawExecutor().Execute(
            withSuccessor,
            TheLogsAreWrong.Domain.Quota.QuotaRuntimeState.Create(Fx.Shift),
            started.Cycle.DueAt,
            Fx.Shift.Scheduler,
            Fx.Anomalies);

        var completed = Assert.IsType<SawCycleCompleted>(execution.Completion.Result);
        Assert.IsType<SawQuotaApplicationAccepted>(execution.Quota.Result);
        var blocked = Assert.IsType<SawCycleStartBlockedByFailureWindow>(execution.Start.Result);
        Assert.Same(completed.State, blocked.State);
        Assert.Same(completed.State, execution.FinalShiftState);
        Assert.Equal(beforeVersion.Next(), execution.FinalShiftState.StateVersion);
        Assert.Equal(0, execution.FinalQuotaState.TotalCreditedUnits);
    }

    private static SawCycleStarted Start(ShiftRuntimeState state, ServerTick tick) =>
        Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(state, tick, Fx.Shift.Scheduler));

    private static ShiftRuntimeState Queued(string logId)
    {
        var state = ShiftRuntimeState.Create(Fx.Shift);
        return Queue(state, logId);
    }

    private static ShiftRuntimeState Queue(ShiftRuntimeState state, string logId)
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
}
