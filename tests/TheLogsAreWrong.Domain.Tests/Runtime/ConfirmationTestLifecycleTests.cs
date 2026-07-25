using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-008")]
public sealed class ConfirmationTestLifecycleTests
{
    private static readonly ConfirmationTestStartService StartService = new();
    private static readonly ConfirmationTestConditionService ConditionService = new();
    private static readonly ConfirmationTestDueCompletionService DueService = new();
    private static readonly ConfirmationTestCancellationService CancellationService = new();

    [Fact]
    public void Penitent_starts_at_tick_10_and_completes_only_at_exact_due_tick()
    {
        var before = AtIntake("log_03");
        var started = Assert.IsType<ConfirmationTestStarted>(Start(before, "log_03", 10, LineNoise.QUIET, "sound_meter"));

        var active = Assert.IsType<ActiveConfirmationTest>(started.State.ActiveConfirmationTest);
        Assert.Equal(ServerTick.From(14), active.DueAt);
        Assert.Equal(SimulationDuration.Zero, active.AccumulatedValidDuration);
        Assert.Equal(before.StateVersion.Next(), started.State.StateVersion);
        Assert.Equal(before.Inventory.ConsumableQuantities, started.State.Inventory.ConsumableQuantities);
        Assert.True(started.State.TryGetLog(LogId.From("log_03"), out var unchanged));
        Assert.Equal(LogState.AT_INTAKE, unchanged.State);
        Assert.Empty(unchanged.Flags);

        var notDue = Assert.IsType<ConfirmationTestNotDue>(Due(started.State, 13));
        Assert.Same(started.State, notDue.State);
        var completed = Assert.IsType<ConfirmationTestDueCompleted>(Due(started.State, 14));
        Assert.Equal(started.State.StateVersion.Next(), completed.State.StateVersion);
        Assert.Null(completed.State.ActiveConfirmationTest);
        Assert.True(completed.State.TryGetConfirmationResult(LogId.From("log_03"), out var result));
        Assert.Equal("spoken_names_detected", result.Result);
        Assert.Equal(ServerTick.From(14), result.CompletedAt);
        Assert.IsType<ConfirmationTestNoActive>(Due(completed.State, 15));
    }

    [Fact]
    public void Penitent_quiet_to_loud_resets_then_quiet_resumes_with_fresh_due_tick()
    {
        var started = Assert.IsType<ConfirmationTestStarted>(Start(AtIntake("log_03"), "log_03", 10, LineNoise.QUIET, "sound_meter")).State;
        var paused = Assert.IsType<ConfirmationTestConditionUpdated>(Condition(started, 12, LineNoise.LOUD, "sound_meter")).State;
        var loudNoOp = Assert.IsType<ConfirmationTestConditionNoChange>(Condition(paused, 12, LineNoise.LOUD, "sound_meter"));
        var resumed = Assert.IsType<ConfirmationTestConditionUpdated>(Condition(paused, 13, LineNoise.QUIET, "sound_meter")).State;

        var pausedActive = Assert.IsType<ActiveConfirmationTest>(paused.ActiveConfirmationTest);
        Assert.False(pausedActive.IsRunning);
        Assert.Equal(SimulationDuration.Zero, pausedActive.AccumulatedValidDuration);
        Assert.Same(paused, loudNoOp.State);
        var resumedActive = Assert.IsType<ActiveConfirmationTest>(resumed.ActiveConfirmationTest);
        Assert.True(resumedActive.IsRunning);
        Assert.Equal(ServerTick.From(17), resumedActive.DueAt);
        Assert.IsType<ConfirmationTestNotDue>(Due(resumed, 16));
        Assert.IsType<ConfirmationTestDueCompleted>(Due(resumed, 17));
    }

    [Fact]
    public void Due_completion_wins_over_a_same_tick_condition_update()
    {
        var started = Assert.IsType<ConfirmationTestStarted>(Start(AtIntake("log_03"), "log_03", 10, LineNoise.QUIET, "sound_meter")).State;

        var dueRequired = Assert.IsType<ConfirmationTestDueCompletionRequired>(Condition(started, 14, LineNoise.LOUD, "sound_meter"));
        var completed = Assert.IsType<ConfirmationTestDueCompleted>(Due(dueRequired.State, 14));

        Assert.Same(started, dueRequired.State);
        Assert.True(completed.State.TryGetConfirmationResult(LogId.From("log_03"), out var result));
        Assert.Equal("spoken_names_detected", result.Result);
    }

    [Fact]
    public void Resin_is_unaffected_by_noise_but_losing_cassette_resets_its_continuous_progress()
    {
        var started = Assert.IsType<ConfirmationTestStarted>(Start(AtIntake("log_06"), "log_06", 10, LineNoise.LOUD, "choir_cassette")).State;
        var noiseOnly = Assert.IsType<ConfirmationTestConditionNoChange>(Condition(started, 12, LineNoise.QUIET, "choir_cassette"));
        var paused = Assert.IsType<ConfirmationTestConditionUpdated>(Condition(started, 12, LineNoise.QUIET)).State;
        var resumed = Assert.IsType<ConfirmationTestConditionUpdated>(Condition(paused, 15, LineNoise.LOUD, "choir_cassette")).State;

        Assert.Same(started, noiseOnly.State);
        Assert.Equal(SimulationDuration.Zero, Assert.IsType<ActiveConfirmationTest>(paused.ActiveConfirmationTest).AccumulatedValidDuration);
        Assert.Equal(ServerTick.From(19), Assert.IsType<ActiveConfirmationTest>(resumed.ActiveConfirmationTest).DueAt);
        var completed = Assert.IsType<ConfirmationTestDueCompleted>(Due(resumed, 19));
        Assert.True(completed.State.TryGetConfirmationResult(LogId.From("log_06"), out var result));
        Assert.Equal("choir_resonance", result.Result);
    }

    [Fact]
    public void False_species_pauses_and_resumes_non_continuous_progress_without_double_counting()
    {
        var started = Assert.IsType<ConfirmationTestStarted>(Start(AtIntake("log_05"), "log_05", 10, LineNoise.QUIET, "scale", "caliper")).State;
        var paused = Assert.IsType<ConfirmationTestConditionUpdated>(Condition(started, 12, LineNoise.LOUD, "scale")).State;
        var resumed = Assert.IsType<ConfirmationTestConditionUpdated>(Condition(paused, 15, LineNoise.QUIET, "scale", "caliper")).State;

        Assert.Equal(SimulationDuration.FromTicks(2), Assert.IsType<ActiveConfirmationTest>(paused.ActiveConfirmationTest).AccumulatedValidDuration);
        Assert.Equal(ServerTick.From(19), Assert.IsType<ActiveConfirmationTest>(resumed.ActiveConfirmationTest).DueAt);
        Assert.IsType<ConfirmationTestNotDue>(Due(resumed, 18));
        var completed = Assert.IsType<ConfirmationTestDueCompleted>(Due(resumed, 19));
        Assert.True(completed.State.TryGetConfirmationResult(LogId.From("log_05"), out var result));
        Assert.Equal("true_species_identified", result.Result);
    }

    [Fact]
    public void Start_rejections_are_typed_unchanged_and_extra_tools_are_allowed()
    {
        var initial = ShiftRuntimeState.Create(Fixture.LoadP0().Shift);
        AssertStartRejected(Start(initial, "missing", 10, LineNoise.QUIET, "sound_meter"), ConfirmationTestStartRejectionReason.TargetNotFound, initial);
        AssertStartRejected(Start(initial, "log_03", 10, LineNoise.QUIET, "sound_meter"), ConfirmationTestStartRejectionReason.TargetNotAtIntake, initial);
        var intake = AtIntake("log_03");
        AssertStartRejected(Start(intake, "log_03", 10, LineNoise.QUIET), ConfirmationTestStartRejectionReason.MissingRequiredTool, intake);
        AssertStartRejected(Start(intake, "log_03", 10, LineNoise.LOUD, "sound_meter"), ConfirmationTestStartRejectionReason.RequiredLineNoiseNotMet, intake);
        var started = Assert.IsType<ConfirmationTestStarted>(Start(intake, "log_03", 10, LineNoise.QUIET, "sound_meter", "scale"));
        AssertStartRejected(Start(started.State, "log_03", 11, LineNoise.QUIET, "sound_meter"), ConfirmationTestStartRejectionReason.ActiveConfirmationAlreadyExists, started.State);
    }

    [Fact]
    public void Cancellation_and_transition_interruption_clear_only_active_progress_and_not_results()
    {
        var started = Assert.IsType<ConfirmationTestStarted>(Start(AtIntake("log_03"), "log_03", 10, LineNoise.QUIET, "sound_meter")).State;
        var cancelled = Assert.IsType<ConfirmationTestCancelled>(CancellationService.Cancel(started, LogId.From("log_03"))).State;
        var noActive = Assert.IsType<ConfirmationTestCancellationRejected>(CancellationService.Cancel(cancelled, LogId.From("log_03")));
        var held = Assert.IsType<ConfirmationTestStarted>(Start(AtIntake("log_03"), "log_03", 10, LineNoise.QUIET, "sound_meter")).State;
        var routed = RuntimeFixture.MoveHost(held, "log_03", LogState.AT_PROCEDURE);
        var returned = RuntimeFixture.MoveHost(routed, "log_03", LogState.AT_INTAKE);

        Assert.Null(cancelled.ActiveConfirmationTest);
        Assert.Equal(started.StateVersion.Next(), cancelled.StateVersion);
        Assert.Equal(ConfirmationTestCancellationRejectionReason.NoActiveConfirmation, noActive.Reason);
        Assert.Same(cancelled, noActive.State);
        Assert.Null(routed.ActiveConfirmationTest);
        Assert.Equal(held.StateVersion.Next(), routed.StateVersion);
        Assert.Null(returned.ActiveConfirmationTest);
    }

    [Fact]
    public void Results_survive_later_transitions_and_active_confirmation_coexists_with_a_procedure_hold()
    {
        var confirmation = Assert.IsType<ConfirmationTestDueCompleted>(Due(Assert.IsType<ConfirmationTestStarted>(Start(AtIntake("log_06"), "log_06", 10, LineNoise.QUIET, "choir_cassette")).State, 14)).State;
        var moved = RuntimeFixture.MoveHost(confirmation, "log_06", LogState.AT_PROCEDURE);
        Assert.True(moved.TryGetConfirmationResult(LogId.From("log_06"), out var result));
        Assert.Equal("choir_resonance", result.Result);

        var procedure = AtProcedure(ShiftRuntimeState.Create(Fixture.LoadP0().Shift), "log_03");
        procedure = Assert.IsType<ProcedureActionHoldStarted>(new ProcedureActionStartService().Start(procedure, LogId.From("log_03"), ItemId.From("holy_water"), ServerTick.From(10), Fixture.LoadP0().Anomalies)).State;
        procedure = RuntimeFixture.MoveHost(procedure, "log_06", LogState.AT_FEED_GATE);
        procedure = RuntimeFixture.MoveHost(procedure, "log_06", LogState.AT_INTAKE);
        var started = Assert.IsType<ConfirmationTestStarted>(Start(procedure, "log_06", 10, LineNoise.QUIET, "choir_cassette"));

        Assert.NotNull(started.State.ActiveProcedureHold);
        Assert.NotNull(started.State.ActiveConfirmationTest);
    }

    [Fact]
    public void Confirmation_data_participates_in_value_equality_and_overflow_is_checked()
    {
        var firstBefore = AtIntake("log_03");
        var first = Assert.IsType<ConfirmationTestStarted>(Start(firstBefore, "log_03", 10, LineNoise.QUIET, "sound_meter")).State;
        var second = Assert.IsType<ConfirmationTestStarted>(Start(AtIntake("log_03"), "log_03", 10, LineNoise.QUIET, "sound_meter")).State;

        Assert.False(firstBefore.ValueEquals(first));
        Assert.True(first.ValueEquals(second));
        Assert.Throws<OverflowException>(() => Start(AtIntake("log_03"), "log_03", long.MaxValue - 1, LineNoise.QUIET, "sound_meter"));
    }

    private static ConfirmationTestStartResult Start(ShiftRuntimeState state, string logId, long tick, LineNoise noise, params string[] tools) =>
        StartService.Start(state, LogId.From(logId), tools.Select(ItemId.From).ToImmutableHashSet(), ServerTick.From(tick), noise, Fixture.LoadP0().Anomalies);

    private static ConfirmationTestConditionResult Condition(ShiftRuntimeState state, long tick, LineNoise noise, params string[] tools) =>
        ConditionService.Update(state, ServerTick.From(tick), noise, tools.Select(ItemId.From).ToImmutableHashSet(), Fixture.LoadP0().Anomalies);

    private static ConfirmationTestDueCompletionResult Due(ShiftRuntimeState state, long tick) =>
        DueService.CompleteDue(state, ServerTick.From(tick), Fixture.LoadP0().Anomalies);

    private static ShiftRuntimeState AtIntake(string logId)
    {
        var state = ShiftRuntimeState.Create(Fixture.LoadP0().Shift);
        return RuntimeFixture.MoveToIntake(state, logId);
    }

    private static ShiftRuntimeState AtProcedure(ShiftRuntimeState state, string logId)
    {
        state = RuntimeFixture.MoveToIntake(state, logId);
        return RuntimeFixture.MoveHost(state, logId, LogState.AT_PROCEDURE);
    }

    private static void AssertStartRejected(ConfirmationTestStartResult result, ConfirmationTestStartRejectionReason reason, ShiftRuntimeState state)
    {
        var rejected = Assert.IsType<ConfirmationTestStartRejected>(result);
        Assert.Equal(reason, rejected.Reason);
        Assert.Same(state, rejected.State);
        Assert.Equal(state.ActiveConfirmationTest, rejected.State.ActiveConfirmationTest);
    }
}
