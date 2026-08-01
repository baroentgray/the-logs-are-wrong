using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Tests.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-026")]
public sealed class ConfirmationTestLineNoiseIntegrationTests
{
    private static readonly ConfirmationTestStartService StartService = new();
    private static readonly ConfirmationTestConditionService ConditionService = new();
    private static readonly ConfirmationTestDueCompletionService DueService = new();
    private static readonly LineNoiseDerivationService LineNoiseService = new();

    [Fact]
    public void Start_consumes_retained_same_shift_noise_and_rejects_foreign_or_future_evidence_before_mutation()
    {
        var state = AtIntake("log_03");
        var quietAtTen = EvaluateQuiet(state, 10).State;

        var started = Assert.IsType<ConfirmationTestStarted>(Start(state, "log_03", 10, quietAtTen, "sound_meter"));
        var future = EvaluateQuiet(state, 11).State;
        var foreign = LineNoiseRuntimeState.Create(ShiftId.From("another_shift"));

        Assert.Equal(state.StateVersion.Next(), started.State.StateVersion);
        Assert.Equal(LineNoise.QUIET, quietAtTen.Current);
        Assert.Throws<ArgumentOutOfRangeException>(() => Start(state, "log_03", 10, future, "sound_meter"));
        Assert.Throws<ArgumentException>(() => Start(state, "log_03", 10, foreign, "sound_meter"));
        Assert.Null(state.ActiveConfirmationTest);
        Assert.Equal(LineNoise.QUIET, quietAtTen.Current);
    }

    [Fact]
    public void Penitent_reset_resumption_and_due_priority_follow_derived_saw_noise()
    {
        var intake = AtIntake("log_03");
        var started = Assert.IsType<ConfirmationTestStarted>(Start(intake, "log_03", 10, EvaluateQuiet(intake, 10).State, "sound_meter")).State;

        var paused = Assert.IsType<ConfirmationTestConditionUpdated>(Update(started, 12, EvaluateSawNoise(12), "sound_meter")).State;
        var resumed = Assert.IsType<ConfirmationTestConditionUpdated>(Update(paused, 13, EvaluateQuiet(paused, 13), "sound_meter")).State;
        var dueRequired = Assert.IsType<ConfirmationTestDueCompletionRequired>(Update(resumed, 17, EvaluateSawNoise(17), "sound_meter"));
        var completed = Assert.IsType<ConfirmationTestDueCompleted>(DueService.CompleteDue(dueRequired.State, ServerTick.From(17), Fixture.LoadP0().Anomalies));

        Assert.False(Assert.IsType<ActiveConfirmationTest>(paused.ActiveConfirmationTest).IsRunning);
        Assert.Equal(SimulationDuration.Zero, Assert.IsType<ActiveConfirmationTest>(paused.ActiveConfirmationTest).AccumulatedValidDuration);
        Assert.Equal(ServerTick.From(17), Assert.IsType<ActiveConfirmationTest>(resumed.ActiveConfirmationTest).DueAt);
        Assert.Same(resumed, dueRequired.State);
        Assert.True(completed.State.TryGetConfirmationResult(LogId.From("log_03"), out var result));
        Assert.Equal("spoken_names_detected", result.Result);
    }

    [Fact]
    public void Resin_and_false_species_ignore_derived_line_noise_when_their_plans_do_not_require_it()
    {
        var resin = AtIntake("log_06");
        var resinStarted = Assert.IsType<ConfirmationTestStarted>(Start(resin, "log_06", 10, EvaluateSawNoise(10).State, "choir_cassette")).State;
        var resinNoChange = Assert.IsType<ConfirmationTestConditionNoChange>(Update(resinStarted, 12, EvaluateQuiet(resinStarted, 12), "choir_cassette"));

        var falseSpecies = AtIntake("log_05");
        var falseStarted = Assert.IsType<ConfirmationTestStarted>(Start(falseSpecies, "log_05", 10, EvaluateQuiet(falseSpecies, 10).State, "scale", "caliper")).State;
        var falseNoChange = Assert.IsType<ConfirmationTestConditionNoChange>(Update(falseStarted, 12, EvaluateSawNoise(12), "scale", "caliper"));

        Assert.Same(resinStarted, resinNoChange.State);
        Assert.Same(falseStarted, falseNoChange.State);
    }

    [Fact]
    public void Condition_requires_exact_current_tick_derivation_before_it_can_change_confirmation_state()
    {
        var intake = AtIntake("log_03");
        var started = Assert.IsType<ConfirmationTestStarted>(Start(intake, "log_03", 10, EvaluateQuiet(intake, 10).State, "sound_meter")).State;
        var stale = EvaluateQuiet(started, 11);

        Assert.Throws<ArgumentException>(() => Update(started, 12, stale, "sound_meter"));

        var exact = Assert.IsType<LineNoiseEvaluatedWithoutChange>(EvaluateQuiet(started, 12));
        var noChange = Assert.IsType<ConfirmationTestConditionNoChange>(Update(started, 12, exact, "sound_meter"));

        Assert.Same(started, noChange.State);
        Assert.True(Assert.IsType<ActiveConfirmationTest>(started.ActiveConfirmationTest).IsRunning);
    }

    [Fact]
    public void Condition_rejects_a_corrupted_change_descriptor_before_confirmation_mutation()
    {
        var intake = AtIntake("log_03");
        var started = Assert.IsType<ConfirmationTestStarted>(Start(intake, "log_03", 10, EvaluateQuiet(intake, 10).State, "sound_meter")).State;
        var loud = Assert.IsType<LineNoiseEvaluatedWithChange>(EvaluateSawNoise(12));
        SetAutoProperty(loud.Change, nameof(LineNoiseChanged.Current), LineNoise.QUIET);

        Assert.Throws<ArgumentException>(() => Update(started, 12, loud, "sound_meter"));

        Assert.True(Assert.IsType<ActiveConfirmationTest>(started.ActiveConfirmationTest).IsRunning);
        Assert.Equal(ServerTick.From(14), started.ActiveConfirmationTest!.DueAt);
    }

    [Fact]
    public void Same_tick_service_produced_loud_noise_pauses_a_new_penitent_without_counting_an_interval()
    {
        var intake = AtIntake("log_03");
        var started = Assert.IsType<ConfirmationTestStarted>(Start(intake, "log_03", 10, LineNoiseRuntimeState.Create(intake.ShiftId), "sound_meter")).State;
        var loud = Assert.IsType<LineNoiseEvaluatedWithChange>(EvaluateSawNoise(10));

        var paused = Assert.IsType<ConfirmationTestConditionUpdated>(Update(started, 10, loud, "sound_meter")).State;
        var active = Assert.IsType<ActiveConfirmationTest>(paused.ActiveConfirmationTest);

        Assert.Equal(LineNoise.LOUD, loud.State.Current);
        Assert.False(active.IsRunning);
        Assert.Equal(SimulationDuration.Zero, active.AccumulatedValidDuration);
        Assert.Null(active.SegmentStartedAt);
        Assert.Null(active.DueAt);
    }

    [Fact]
    public void Remaining_loud_after_a_service_produced_source_composition_change_does_not_reset_penitent_twice()
    {
        var intake = AtIntake("log_03");
        var started = Assert.IsType<ConfirmationTestStarted>(Start(intake, "log_03", 10, EvaluateQuiet(intake, 10).State, "sound_meter")).State;
        var firstLoud = Assert.IsType<LineNoiseEvaluatedWithChange>(EvaluateSawNoise(12));
        var paused = Assert.IsType<ConfirmationTestConditionUpdated>(Update(started, 12, firstLoud, "sound_meter")).State;
        var compositionChanged = Assert.IsType<LineNoiseEvaluatedWithoutChange>(EvaluateSawWithMovement(firstLoud.State, 13));

        var noChange = Assert.IsType<ConfirmationTestConditionNoChange>(Update(paused, 13, compositionChanged, "sound_meter"));

        Assert.True(firstLoud.State.LatestSources.SawActive);
        Assert.False(firstLoud.State.LatestSources.MovementNoiseActive);
        Assert.True(compositionChanged.State.LatestSources.SawActive);
        Assert.True(compositionChanged.State.LatestSources.MovementNoiseActive);
        Assert.Equal(LineNoise.LOUD, compositionChanged.State.Current);
        Assert.Same(paused, noChange.State);
        Assert.Equal(SimulationDuration.Zero, Assert.IsType<ActiveConfirmationTest>(paused.ActiveConfirmationTest).AccumulatedValidDuration);
    }

    [Fact]
    public void Quiet_to_quiet_service_history_preserves_running_penitent_timing_without_a_duplicate_mutation()
    {
        var intake = AtIntake("log_03");
        var quietAtTen = Assert.IsType<LineNoiseEvaluatedWithoutChange>(EvaluateQuiet(intake, 10));
        var started = Assert.IsType<ConfirmationTestStarted>(Start(intake, "log_03", 10, quietAtTen.State, "sound_meter")).State;
        var quietAtEleven = Assert.IsType<LineNoiseEvaluatedWithoutChange>(EvaluateQuiet(quietAtTen.State, started, 11));

        var noChange = Assert.IsType<ConfirmationTestConditionNoChange>(Update(started, 11, quietAtEleven, "sound_meter"));

        Assert.Same(started, noChange.State);
        Assert.True(Assert.IsType<ActiveConfirmationTest>(started.ActiveConfirmationTest).IsRunning);
        Assert.Equal(ServerTick.From(14), started.ActiveConfirmationTest!.DueAt);
    }

    [Fact]
    public void Condition_rejects_distinct_service_produced_cross_shift_and_future_evidence_before_mutation()
    {
        var intake = AtIntake("log_03");
        var started = Assert.IsType<ConfirmationTestStarted>(Start(intake, "log_03", 10, EvaluateQuiet(intake, 10).State, "sound_meter")).State;
        var otherShift = ShiftRuntimeState.Create(Fixture.LoadP0().Shift with { ShiftId = ShiftId.From("another_shift") });
        var crossShift = EvaluateQuiet(otherShift, 12);
        var future = EvaluateQuiet(started, 13);
        var active = started.ActiveConfirmationTest;
        var version = started.StateVersion;

        Assert.Throws<ArgumentException>(() => Update(started, 12, crossShift, "sound_meter"));
        Assert.Throws<ArgumentException>(() => Update(started, 12, future, "sound_meter"));

        Assert.Same(active, started.ActiveConfirmationTest);
        Assert.Equal(version, started.StateVersion);
    }

    private static ConfirmationTestStartResult Start(ShiftRuntimeState state, string logId, long tick, LineNoiseRuntimeState runtime, params string[] tools) =>
        StartService.Start(state, LogId.From(logId), tools.Select(ItemId.From).ToImmutableHashSet(), ServerTick.From(tick), runtime, Fixture.LoadP0().Anomalies);

    private static ConfirmationTestConditionResult Update(ShiftRuntimeState state, long tick, LineNoiseEvaluationResult evaluation, params string[] tools) =>
        ConditionService.Update(state, ServerTick.From(tick), evaluation, tools.Select(ItemId.From).ToImmutableHashSet(), Fixture.LoadP0().Anomalies);

    private static ShiftRuntimeState AtIntake(string logId) => RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), logId);

    private static LineNoiseEvaluationResult EvaluateQuiet(ShiftRuntimeState state, long tick) =>
        EvaluateQuiet(LineNoiseRuntimeState.Create(state.ShiftId), state, tick);

    private static LineNoiseEvaluationResult EvaluateQuiet(LineNoiseRuntimeState runtime, ShiftRuntimeState state, long tick) =>
        LineNoiseService.Evaluate(runtime, state, MovementNoiseRuntimeState.Create(state.ShiftId), ServerTick.From(tick));

    private static LineNoiseEvaluationResult EvaluateSawNoise(long tick)
    {
        var saw = StartSaw(tick);
        return LineNoiseService.Evaluate(LineNoiseRuntimeState.Create(saw.State.ShiftId), saw.State, MovementNoiseRuntimeState.Create(saw.State.ShiftId), ServerTick.From(tick));
    }

    private static LineNoiseEvaluationResult EvaluateSawWithMovement(LineNoiseRuntimeState runtime, long tick)
    {
        var saw = StartSaw(tick);
        var movement = Assert.IsType<MovementNoiseApplied>(new MovementNoiseApplicationService().Apply(MovementNoiseRuntimeState.Create(saw.State.ShiftId), saw, Fixture.LoadP0().Shift.Scheduler)).State;
        return LineNoiseService.Evaluate(runtime, saw.State, movement, ServerTick.From(tick));
    }

    private static SawCycleStarted StartSaw(long tick)
    {
        var source = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        source = RuntimeFixture.MoveHost(source, "log_01", LogState.QUEUED_FOR_SAW);
        return Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(source, ServerTick.From(tick), Fixture.LoadP0().Shift.Scheduler));
    }

    private static void SetAutoProperty<T>(object instance, string propertyName, T value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }
}
