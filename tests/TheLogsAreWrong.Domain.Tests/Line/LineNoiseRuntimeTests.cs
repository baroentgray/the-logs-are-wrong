using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Tests.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Line;

[Trait("Scope", "TLAW-025")]
public sealed class LineNoiseRuntimeTests
{
    private static readonly LineNoiseDerivationService Service = new();

    [Fact]
    public void Create_establishes_an_exact_shift_quiet_baseline_without_a_change()
    {
        var state = RuntimeFixture.CreateInitialState();

        var runtime = LineNoiseRuntimeState.Create(state.ShiftId);

        Assert.Equal(state.ShiftId, runtime.ShiftId);
        Assert.Equal(LineNoise.QUIET, runtime.Current);
        Assert.Null(runtime.LastEvaluatedAt);
        Assert.Null(runtime.LastChangedAt);
        Assert.Equal(LineNoiseSourceSnapshot.Quiet, runtime.LatestSources);
        Assert.Throws<ArgumentException>(() => LineNoiseRuntimeState.Create(default));
    }

    [Theory]
    [InlineData(0, LineNoise.QUIET, false, false, false)]
    [InlineData(1, LineNoise.LOUD, true, false, false)]
    [InlineData(2, LineNoise.LOUD, false, true, false)]
    [InlineData(3, LineNoise.LOUD, false, false, true)]
    [InlineData(4, LineNoise.LOUD, true, true, false)]
    [InlineData(5, LineNoise.LOUD, true, false, true)]
    [InlineData(6, LineNoise.LOUD, false, true, true)]
    [InlineData(7, LineNoise.LOUD, true, true, true)]
    public void Evaluate_derives_the_complete_three_source_truth_table(
        int combination,
        LineNoise expected,
        bool saw,
        bool movement,
        bool repair)
    {
        var evidence = Sources(combination);
        var runtime = LineNoiseRuntimeState.Create(evidence.State.ShiftId);

        var result = Service.Evaluate(runtime, evidence.State, evidence.Movement, evidence.Tick);

        Assert.Equal(expected, result.State.Current);
        AssertSnapshot(result.State.LatestSources, saw, movement, repair);
        if (expected == LineNoise.QUIET)
        {
            Assert.IsType<LineNoiseEvaluatedWithoutChange>(result);
        }
        else
        {
            var changed = Assert.IsType<LineNoiseEvaluatedWithChange>(result);
            Assert.Equal((evidence.State.ShiftId, LineNoise.QUIET, LineNoise.LOUD, evidence.Tick, result.State.LatestSources),
                (changed.Change.ShiftId, changed.Change.Previous, changed.Change.Current, changed.Change.ChangedAt, changed.Change.Sources));
        }
    }

    [Fact]
    public void Exact_movement_window_bounds_and_saw_completion_order_are_derived_without_lifecycle_mutation()
    {
        var evidence = MovementOnly();
        var runtime = LineNoiseRuntimeState.Create(evidence.State.ShiftId);

        var started = Assert.IsType<LineNoiseEvaluatedWithChange>(Service.Evaluate(runtime, evidence.State, evidence.Movement, evidence.Tick));
        var due = Assert.IsType<LineNoiseEvaluatedWithChange>(Service.Evaluate(started.State, evidence.State, evidence.Movement, evidence.Movement.DueAt));

        Assert.True(evidence.Movement.IsActiveAt(evidence.Tick));
        Assert.False(evidence.Movement.IsActiveAt(evidence.Movement.DueAt));
        Assert.Null(evidence.State.ActiveSawCycle);
        AssertSnapshot(started.State.LatestSources, saw: false, movement: true, repair: false);
        Assert.Equal(LineNoise.LOUD, started.State.Current);
        Assert.Equal(LineNoise.QUIET, due.State.Current);
        Assert.Equal(evidence.Movement.DueAt, due.Change.ChangedAt);
    }

    [Fact]
    public void Jammed_but_not_repairing_is_quiet_while_an_unresolved_repair_remains_loud()
    {
        var jammed = FeedJammed();
        var quietRuntime = LineNoiseRuntimeState.Create(jammed.ShiftId);
        var quiet = Assert.IsType<LineNoiseEvaluatedWithoutChange>(Service.Evaluate(quietRuntime, jammed, MovementNoiseRuntimeState.Create(jammed.ShiftId), ServerTick.From(10)));
        var repairing = FeedRepairing();
        var lateTick = repairing.Line.ActiveRepairHold!.DueAt + TheLogsAreWrong.Domain.Time.SimulationDuration.FromTicks(2);

        var loud = Assert.IsType<LineNoiseEvaluatedWithChange>(Service.Evaluate(LineNoiseRuntimeState.Create(repairing.ShiftId), repairing, MovementNoiseRuntimeState.Create(repairing.ShiftId), lateTick));

        Assert.Equal(LineNoise.QUIET, quiet.State.Current);
        AssertSnapshot(quiet.State.LatestSources, saw: false, movement: false, repair: false);
        Assert.Equal(LineNoise.LOUD, loud.State.Current);
        AssertSnapshot(loud.State.LatestSources, saw: false, movement: false, repair: true);
        Assert.Equal(LineState.REPAIRING, repairing.Line.State);
    }

    [Fact]
    public void Composition_changes_without_noise_changes_update_sources_but_emit_no_descriptor()
    {
        var saw = SawOnly();
        var runtime = LineNoiseRuntimeState.Create(saw.State.ShiftId);
        var first = Assert.IsType<LineNoiseEvaluatedWithChange>(Service.Evaluate(runtime, saw.State, saw.Movement, saw.Tick));
        var movement = MovementOnly();

        var second = Assert.IsType<LineNoiseEvaluatedWithoutChange>(Service.Evaluate(first.State, movement.State, movement.Movement, movement.Tick));

        Assert.Equal(LineNoise.LOUD, second.State.Current);
        AssertSnapshot(second.State.LatestSources, saw: false, movement: true, repair: false);
        Assert.Equal(first.Change.ChangedAt, second.State.LastChangedAt);
    }

    [Fact]
    public void Exact_same_tick_evidence_is_an_identity_preserving_no_op_but_non_equivalent_evidence_fails_closed()
    {
        var quiet = NoSources(ServerTick.From(7));
        var runtime = Assert.IsType<LineNoiseEvaluatedWithoutChange>(Service.Evaluate(LineNoiseRuntimeState.Create(quiet.State.ShiftId), quiet.State, quiet.Movement, quiet.Tick)).State;

        var duplicate = Assert.IsType<LineNoiseAlreadyEvaluated>(Service.Evaluate(runtime, quiet.State, quiet.Movement, quiet.Tick));

        Assert.Same(runtime, duplicate.State);
        AssertRejectsWithoutMutation(runtime, candidate => Service.Evaluate(candidate, SawOnly(ServerTick.From(7)).State, MovementNoiseRuntimeState.Create(quiet.State.ShiftId), quiet.Tick));

        var movement = MovementOnly();
        var sameTickQuiet = Assert.IsType<LineNoiseEvaluatedWithoutChange>(Service.Evaluate(LineNoiseRuntimeState.Create(movement.State.ShiftId), movement.State, MovementNoiseRuntimeState.Create(movement.State.ShiftId), movement.Tick)).State;
        AssertRejectsWithoutMutation(sameTickQuiet, candidate => Service.Evaluate(candidate, movement.State, movement.Movement, movement.Tick));

        var jammed = FeedJammed();
        var repairTick = ServerTick.From(10);
        var sameTickJammed = Assert.IsType<LineNoiseEvaluatedWithoutChange>(Service.Evaluate(LineNoiseRuntimeState.Create(jammed.ShiftId), jammed, MovementNoiseRuntimeState.Create(jammed.ShiftId), repairTick)).State;
        AssertRejectsWithoutMutation(sameTickJammed, candidate => Service.Evaluate(candidate, FeedRepairing(), MovementNoiseRuntimeState.Create(jammed.ShiftId), repairTick));
    }

    [Fact]
    public void Older_cross_shift_and_malformed_source_evidence_fail_before_replacing_the_input_runtime()
    {
        var quiet = NoSources(ServerTick.From(5));
        var runtime = Assert.IsType<LineNoiseEvaluatedWithoutChange>(Service.Evaluate(LineNoiseRuntimeState.Create(quiet.State.ShiftId), quiet.State, quiet.Movement, quiet.Tick)).State;

        AssertRejectsWithoutMutation(runtime, candidate => Service.Evaluate(candidate, quiet.State, quiet.Movement, ServerTick.From(4)));
        AssertRejectsWithoutMutation(runtime, candidate => Service.Evaluate(candidate, OtherShift(), quiet.Movement, ServerTick.From(6)));
        AssertRejectsWithoutMutation(runtime, candidate => Service.Evaluate(candidate, quiet.State, MovementNoiseRuntimeState.Create(ShiftId.From("other_shift")), ServerTick.From(6)));

        var saw = SawOnly();
        SetAutoProperty(saw.State.ActiveSawCycle!, nameof(ActiveSawCycle.DueAt), default(ServerTick));
        AssertRejectsWithoutMutation(LineNoiseRuntimeState.Create(saw.State.ShiftId), candidate => Service.Evaluate(candidate, saw.State, saw.Movement, saw.Tick));

        var repairing = FeedRepairing();
        SetAutoProperty(repairing.Line.ActiveRepairHold!, nameof(ActiveRepairHold.DueAt), default(ServerTick));
        AssertRejectsWithoutMutation(LineNoiseRuntimeState.Create(repairing.ShiftId), candidate => Service.Evaluate(candidate, repairing, MovementNoiseRuntimeState.Create(repairing.ShiftId), ServerTick.From(10)));

        var movement = MovementOnly();
        SetAutoProperty(movement.Movement, nameof(MovementNoiseRuntimeState.DueAt), default(ServerTick));
        AssertRejectsWithoutMutation(LineNoiseRuntimeState.Create(movement.State.ShiftId), candidate => Service.Evaluate(candidate, movement.State, movement.Movement, movement.Tick));
    }

    [Fact]
    public void Descriptor_rejects_equal_unknown_or_inconsistent_values_and_retains_typed_evidence()
    {
        var saw = SawOnly();
        var sources = Assert.IsType<LineNoiseEvaluatedWithChange>(
            Service.Evaluate(LineNoiseRuntimeState.Create(saw.State.ShiftId), saw.State, saw.Movement, saw.Tick)).State.LatestSources;
        var shiftId = saw.State.ShiftId;

        Assert.Throws<ArgumentException>(() => new LineNoiseChanged(shiftId, LineNoise.LOUD, LineNoise.LOUD, ServerTick.From(1), sources));
        Assert.Throws<ArgumentException>(() => new LineNoiseChanged(default, LineNoise.QUIET, LineNoise.LOUD, ServerTick.From(1), sources));
        Assert.Throws<ArgumentException>(() => new LineNoiseChanged(shiftId, LineNoise.QUIET, (LineNoise)42, ServerTick.From(1), sources));
        Assert.Throws<ArgumentException>(() => new LineNoiseChanged(shiftId, LineNoise.QUIET, LineNoise.LOUD, default, sources));
        Assert.Throws<ArgumentException>(() => new LineNoiseChanged(shiftId, LineNoise.QUIET, LineNoise.LOUD, ServerTick.From(1), LineNoiseSourceSnapshot.Quiet));
    }

    [Fact]
    public void Independent_equivalent_sequences_produce_value_equivalent_runtime_and_descriptor_evidence()
    {
        var left = EvaluateSequence();
        var right = EvaluateSequence();

        Assert.True(left.Runtime.ValueEquals(right.Runtime));
        Assert.Equal(left.Changes, right.Changes);
    }

    private static (LineNoiseRuntimeState Runtime, IReadOnlyList<LineNoiseChanged> Changes) EvaluateSequence()
    {
        var quiet = NoSources(ServerTick.From(1));
        var first = Assert.IsType<LineNoiseEvaluatedWithoutChange>(Service.Evaluate(LineNoiseRuntimeState.Create(quiet.State.ShiftId), quiet.State, quiet.Movement, quiet.Tick));
        var saw = SawOnly(ServerTick.From(2));
        var second = Assert.IsType<LineNoiseEvaluatedWithChange>(Service.Evaluate(first.State, saw.State, saw.Movement, saw.Tick));
        var movement = MovementOnly();
        var third = Assert.IsType<LineNoiseEvaluatedWithoutChange>(Service.Evaluate(second.State, movement.State, movement.Movement, movement.Tick));
        var fourth = Assert.IsType<LineNoiseEvaluatedWithChange>(Service.Evaluate(third.State, movement.State, movement.Movement, movement.Movement.DueAt));
        return (fourth.State, new[] { second.Change, fourth.Change });
    }

    private static SourceEvidence Sources(int combination) => combination switch
    {
        0 => NoSources(ServerTick.From(1)),
        1 => SawOnly(),
        2 => MovementOnly(),
        3 => RepairOnly(),
        4 => SawWithMovement(),
        5 => SawWithRepair(),
        6 => RepairWithMovement(),
        7 => SawRepairWithMovement(),
        _ => throw new ArgumentOutOfRangeException(nameof(combination))
    };

    private static SourceEvidence NoSources(ServerTick tick) => new(RuntimeFixture.CreateInitialState(), MovementNoiseRuntimeState.Create(RuntimeFixture.CreateInitialState().ShiftId), tick);

    private static SourceEvidence SawOnly(ServerTick? tick = null)
    {
        var started = StartSaw(tick ?? ServerTick.From(10));
        return new(started.State, MovementNoiseRuntimeState.Create(started.State.ShiftId), started.Cycle.StartedAt);
    }

    private static SourceEvidence MovementOnly()
    {
        var started = StartSaw(ServerTick.From(10));
        var completed = Assert.IsType<SawCycleCompleted>(new SawCycleCompletionService().Complete(started.State, started.Cycle.DueAt, Fixture.LoadP0().Anomalies));
        var movement = Assert.IsType<MovementNoiseApplied>(new MovementNoiseApplicationService().Apply(MovementNoiseRuntimeState.Create(completed.State.ShiftId), completed, Scheduler())).State;
        return new(completed.State, movement, completed.CompletedAt);
    }

    private static SourceEvidence RepairOnly()
    {
        var state = FeedRepairing();
        return new(state, MovementNoiseRuntimeState.Create(state.ShiftId), ServerTick.From(10));
    }

    private static SourceEvidence SawWithMovement()
    {
        var started = StartSaw(ServerTick.From(10));
        var movement = Assert.IsType<MovementNoiseApplied>(new MovementNoiseApplicationService().Apply(MovementNoiseRuntimeState.Create(started.State.ShiftId), started, Scheduler())).State;
        return new(started.State, movement, started.Cycle.StartedAt);
    }

    private static SourceEvidence SawWithRepair()
    {
        var state = SawRepairing();
        return new(state, MovementNoiseRuntimeState.Create(state.ShiftId), ServerTick.From(10));
    }

    private static SourceEvidence RepairWithMovement()
    {
        var repairing = FeedRepairing();
        var accepted = Assert.IsType<HostLogTransitionAccepted>(new HostLogTransitionService().Apply(repairing, LogId.From("log_01"), LogState.AT_PROCEDURE));
        var movement = Assert.IsType<MovementNoiseApplied>(new MovementNoiseApplicationService().Apply(MovementNoiseRuntimeState.Create(accepted.State.ShiftId), accepted, ServerTick.From(10), Scheduler())).State;
        return new(accepted.State, movement, ServerTick.From(10));
    }

    private static SourceEvidence SawRepairWithMovement()
    {
        var repairing = SawRepairing();
        var accepted = Assert.IsType<HostLogTransitionAccepted>(new HostLogTransitionService().Apply(repairing, LogId.From("log_02"), LogState.AT_PROCEDURE));
        var movement = Assert.IsType<MovementNoiseApplied>(new MovementNoiseApplicationService().Apply(MovementNoiseRuntimeState.Create(accepted.State.ShiftId), accepted, ServerTick.From(10), Scheduler())).State;
        return new(accepted.State, movement, ServerTick.From(10));
    }

    private static SawCycleStarted StartSaw(ServerTick tick)
    {
        var state = RuntimeFixture.MoveHost(RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01"), "log_01", LogState.QUEUED_FOR_SAW);
        return Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(state, tick, Scheduler()));
    }

    private static ShiftRuntimeState FeedJammed()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
        return Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.FEED_GATE_BLOCKED, ServerTick.From(10))).State;
    }

    private static ShiftRuntimeState FeedRepairing() => Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(FeedJammed(), ServerTick.From(10), Scheduler())).State;

    private static ShiftRuntimeState SawRepairing()
    {
        var saw = StartSaw(ServerTick.From(10)).State;
        saw = RuntimeFixture.MoveHost(saw, "log_02", LogState.AT_FEED_GATE);
        saw = RuntimeFixture.MoveHost(saw, "log_02", LogState.AT_INTAKE);
        saw = RuntimeFixture.MoveHost(saw, "log_03", LogState.AT_FEED_GATE);
        var jammed = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(saw, JamCause.FEED_GATE_BLOCKED, ServerTick.From(10))).State;
        return Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jammed, ServerTick.From(10), Scheduler())).State;
    }

    private static ShiftRuntimeState OtherShift()
    {
        var fixture = Fixture.LoadP0();
        return ShiftRuntimeState.Create(fixture.Shift with { ShiftId = ShiftId.From("other_shift") });
    }

    private static SchedulerConfiguration Scheduler() => Fixture.LoadP0().Shift.Scheduler;

    private static void AssertRejectsWithoutMutation(LineNoiseRuntimeState runtime, Func<LineNoiseRuntimeState, LineNoiseEvaluationResult> evaluate)
    {
        var shiftId = runtime.ShiftId;
        var current = runtime.Current;
        var evaluatedAt = runtime.LastEvaluatedAt;
        var changedAt = runtime.LastChangedAt;
        var sources = runtime.LatestSources;

        Assert.ThrowsAny<ArgumentException>(() => evaluate(runtime));

        Assert.Equal(shiftId, runtime.ShiftId);
        Assert.Equal(current, runtime.Current);
        Assert.Equal(evaluatedAt, runtime.LastEvaluatedAt);
        Assert.Equal(changedAt, runtime.LastChangedAt);
        Assert.Equal(sources, runtime.LatestSources);
    }

    private static void AssertSnapshot(LineNoiseSourceSnapshot snapshot, bool saw, bool movement, bool repair)
    {
        Assert.Equal(saw, snapshot.SawActive);
        Assert.Equal(movement, snapshot.MovementNoiseActive);
        Assert.Equal(repair, snapshot.RepairActive);
        Assert.Equal(saw || movement || repair ? LineNoise.LOUD : LineNoise.QUIET, snapshot.DerivedValue);
    }

    private static void SetAutoProperty<T>(object instance, string propertyName, T value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private sealed record SourceEvidence(ShiftRuntimeState State, MovementNoiseRuntimeState Movement, ServerTick Tick);
}
