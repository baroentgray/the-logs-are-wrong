using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-007")]
public sealed class ProcedureActionLifecycleTests
{
    private static readonly ProcedureActionStartService StartService = new();
    private static readonly ProcedureActionCancellationService CancellationService = new();
    private static readonly ProcedureActionDueCompletionService DueCompletionService = new();

    [Fact]
    public void Penitent_holy_water_starts_an_immutable_hold_without_consumption_or_progress_before_due()
    {
        var before = AtProcedure("log_03");

        var started = Assert.IsType<ProcedureActionHoldStarted>(Start(before, "log_03", "holy_water", 10));

        Assert.Equal(before.StateVersion.Next(), started.State.StateVersion);
        var hold = Assert.IsType<ActiveProcedureHold>(started.State.ActiveProcedureHold);
        Assert.Equal(LogId.From("log_03"), hold.LogId);
        Assert.Equal(AnomalyId.From("PENITENT_TRUNK"), hold.AnomalyId);
        Assert.Equal(ItemId.From("holy_water"), hold.AttemptedItem);
        Assert.Equal(0, hold.ProcedureStepIndex);
        Assert.Equal(ServerTick.From(10), hold.StartedAt);
        Assert.Equal(ServerTick.From(13), hold.DueAt);
        Assert.Equal(SimulationDuration.FromTicks(3), hold.Duration);
        Assert.Equal(2, started.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.False(started.State.TryGetProcedureProgress(LogId.From("log_03"), out _));
        Assert.True(started.State.TryGetLog(LogId.From("log_03"), out var log));
        Assert.DoesNotContain(FlagId.From("SANITIZED_PENITENT"), log.Flags);
        Assert.Null(before.ActiveProcedureHold);
        Assert.Equal(2, before.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
    }

    [Fact]
    public void Hold_is_not_due_at_tick_12_and_completes_exactly_at_tick_13_with_one_completion_increment()
    {
        var held = Assert.IsType<ProcedureActionHoldStarted>(Start(AtProcedure("log_03"), "log_03", "holy_water", 10)).State;

        var notDue = Assert.IsType<ProcedureActionNotDue>(CompleteDue(held, 12));
        Assert.Same(held, notDue.State);
        Assert.Equal(ServerTick.From(13), notDue.DueAt);
        Assert.Equal(2, notDue.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));

        var completed = Assert.IsType<ProcedureActionDueCompleted>(CompleteDue(held, 13));
        Assert.Equal(held.StateVersion.Next(), completed.State.StateVersion);
        Assert.Null(completed.State.ActiveProcedureHold);
        Assert.Equal(1, completed.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.True(completed.State.TryGetProcedureProgress(LogId.From("log_03"), out var progress));
        Assert.True(progress.IsComplete);
        Assert.True(completed.State.TryGetLog(LogId.From("log_03"), out var log));
        Assert.Contains(FlagId.From("SANITIZED_PENITENT"), log.Flags);
        Assert.Equal(LogState.AT_PROCEDURE, log.State);
        Assert.Equal(held.StateVersion, completed.Descriptor.PriorStateVersion);
        Assert.Equal(completed.State.StateVersion, completed.Descriptor.CurrentStateVersion);
    }

    [Fact]
    public void Later_tick_catches_up_once_and_a_cleared_hold_cannot_complete_again()
    {
        var held = Assert.IsType<ProcedureActionHoldStarted>(Start(AtProcedure("log_03"), "log_03", "holy_water", 10)).State;

        var completed = Assert.IsType<ProcedureActionDueCompleted>(CompleteDue(held, 99));
        var noActive = Assert.IsType<ProcedureActionNoActiveHold>(CompleteDue(completed.State, 100));

        Assert.Same(completed.State, noActive.State);
        Assert.Equal(1, noActive.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.True(noActive.State.TryGetProcedureProgress(LogId.From("log_03"), out var progress));
        Assert.Equal(1, progress.CompletedStepCount);
    }

    [Fact]
    public void Due_completion_reports_a_typed_failure_for_a_hold_whose_plan_is_no_longer_resolvable()
    {
        var held = Assert.IsType<ProcedureActionHoldStarted>(Start(AtProcedure("log_03"), "log_03", "holy_water", 10)).State;
        var missingPlanCatalog = new AnomalyCatalog(ImmutableDictionary<AnomalyId, AnomalyDefinition>.Empty);

        var failed = Assert.IsType<ProcedureActionDueCompletionFailed>(DueCompletionService.CompleteDue(held, ServerTick.From(13), missingPlanCatalog));

        Assert.Equal(ProcedureActionDueCompletionFailureReason.NoProcedurePlan, failed.Reason);
        Assert.Null(failed.CompletionRejection);
        Assert.Same(held, failed.State);
        Assert.Equal(held.ActiveProcedureHold, failed.State.ActiveProcedureHold);
    }

    [Fact]
    public void Cancellation_clears_only_the_exact_held_log_without_consumption()
    {
        var held = Assert.IsType<ProcedureActionHoldStarted>(Start(AtProcedure("log_03"), "log_03", "holy_water", 10)).State;

        var mismatch = Assert.IsType<ProcedureActionCancellationRejected>(CancellationService.Cancel(held, LogId.From("log_06")));
        Assert.Equal(ProcedureActionCancellationRejectionReason.HeldLogMismatch, mismatch.Reason);
        Assert.Same(held, mismatch.State);

        var cancelled = Assert.IsType<ProcedureActionCancelled>(CancellationService.Cancel(held, LogId.From("log_03")));
        Assert.Equal(held.StateVersion.Next(), cancelled.State.StateVersion);
        Assert.Null(cancelled.State.ActiveProcedureHold);
        Assert.Equal(2, cancelled.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.False(cancelled.State.TryGetProcedureProgress(LogId.From("log_03"), out _));
        Assert.True(cancelled.State.TryGetLog(LogId.From("log_03"), out var log));
        Assert.Equal(LogState.AT_PROCEDURE, log.State);

        var noActive = Assert.IsType<ProcedureActionCancellationRejected>(CancellationService.Cancel(cancelled.State, LogId.From("log_03")));
        Assert.Equal(ProcedureActionCancellationRejectionReason.NoActiveHold, noActive.Reason);
        Assert.Same(cancelled.State, noActive.State);
    }

    [Fact]
    public void Leaving_procedure_interrupts_the_hold_and_reentry_does_not_resume_it()
    {
        var held = Assert.IsType<ProcedureActionHoldStarted>(Start(AtProcedure("log_03"), "log_03", "holy_water", 10)).State;

        var returned = RuntimeFixture.MoveHost(held, "log_03", LogState.AT_INTAKE);
        var reentered = RuntimeFixture.MoveHost(returned, "log_03", LogState.AT_PROCEDURE);

        Assert.Equal(held.StateVersion.Next(), returned.StateVersion);
        Assert.Null(returned.ActiveProcedureHold);
        Assert.Equal(2, returned.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.False(returned.TryGetProcedureProgress(LogId.From("log_03"), out _));
        Assert.Null(reentered.ActiveProcedureHold);
        Assert.IsType<ProcedureActionNoActiveHold>(CompleteDue(reentered, 99));
    }

    [Fact]
    public void Transition_for_another_log_preserves_the_active_hold()
    {
        var held = Assert.IsType<ProcedureActionHoldStarted>(Start(AtProcedure("log_03"), "log_03", "holy_water", 10)).State;

        var moved = RuntimeFixture.MoveHost(held, "log_01", LogState.AT_FEED_GATE);

        Assert.Equal(held.StateVersion.Next(), moved.StateVersion);
        Assert.Equal(held.ActiveProcedureHold, moved.ActiveProcedureHold);
        Assert.Equal(2, moved.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
    }

    [Fact]
    public void Immediate_p0_steps_complete_with_exactly_one_increment_and_never_create_a_hold()
    {
        var resin = AtProcedure("log_06");
        var salt = Assert.IsType<ProcedureActionCompletedImmediately>(Start(resin, "log_06", "salt", 10));
        var tape = Assert.IsType<ProcedureActionCompletedImmediately>(Start(salt.State, "log_06", "red_tape", 11));
        var falseSpecies = Assert.IsType<ProcedureActionCompletedImmediately>(Start(AtProcedure("log_05"), "log_05", "relabel_stamp", 10));

        Assert.Equal(resin.StateVersion.Next(), salt.State.StateVersion);
        Assert.Equal(salt.State.StateVersion.Next(), tape.State.StateVersion);
        Assert.Equal(1, salt.State.Inventory.GetConsumableQuantity(ItemId.From("salt")));
        Assert.Equal(1, tape.State.Inventory.GetConsumableQuantity(ItemId.From("red_tape")));
        Assert.Null(salt.State.ActiveProcedureHold);
        Assert.Null(tape.State.ActiveProcedureHold);
        Assert.True(salt.State.TryGetLog(LogId.From("log_06"), out var afterSalt));
        Assert.DoesNotContain(FlagId.From("SEALED_RESIN"), afterSalt.Flags);
        Assert.True(tape.State.TryGetLog(LogId.From("log_06"), out var afterTape));
        Assert.Contains(FlagId.From("SEALED_RESIN"), afterTape.Flags);
        Assert.False(falseSpecies.Descriptor.ItemConsumed);
        Assert.Equal(AtProcedure("log_05").Inventory.ConsumableQuantities, falseSpecies.State.Inventory.ConsumableQuantities);
        Assert.Null(falseSpecies.State.ActiveProcedureHold);
    }

    [Fact]
    public void Resin_holy_water_wrong_action_is_immediate_consumes_and_preserves_progress_without_executing_effects()
    {
        var before = AtProcedure("log_06");

        var completed = Assert.IsType<ProcedureActionCompletedImmediately>(Start(before, "log_06", "holy_water", 10));

        Assert.Equal(before.StateVersion.Next(), completed.State.StateVersion);
        Assert.Equal(ItemActionCompletionKind.ConfiguredWrongAction, completed.Descriptor.Kind);
        Assert.True(completed.Descriptor.ItemConsumed);
        Assert.Equal(1, completed.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.Null(completed.Descriptor.CurrentProgress);
        Assert.Null(completed.State.ActiveProcedureHold);
        var effect = Assert.Single(completed.Descriptor.Effects);
        Assert.Equal(EffectEventId.From("RESIN_BUTTON_LOCK"), effect.Event);
        Assert.True(completed.State.TryGetLog(LogId.From("log_06"), out var log));
        Assert.Empty(log.Flags);
    }

    [Fact]
    public void Null_and_zero_duration_steps_complete_immediately_without_a_fabricated_hold()
    {
        var nullDuration = Assert.IsType<ProcedureActionCompletedImmediately>(Start(AtProcedure("log_06"), "log_06", "salt", 10));
        var source = Fixture.LoadP0().Anomalies;
        var resinId = AnomalyId.From("RESIN_BLASPHEMER");
        var resin = source.Definitions[resinId];
        var zeroDurationCatalog = new AnomalyCatalog(source.Definitions.SetItem(resinId, resin with
        {
            Procedure = resin.Procedure with
            {
                Steps = resin.Procedure.Steps.SetItem(0, resin.Procedure.Steps[0] with { HoldSeconds = 0 })
            }
        }));

        var zeroDuration = Assert.IsType<ProcedureActionCompletedImmediately>(
            StartService.Start(AtProcedure("log_06"), LogId.From("log_06"), ItemId.From("salt"), ServerTick.From(10), zeroDurationCatalog));

        Assert.Null(nullDuration.State.ActiveProcedureHold);
        Assert.Null(zeroDuration.State.ActiveProcedureHold);
        Assert.Equal(1, nullDuration.State.Inventory.GetConsumableQuantity(ItemId.From("salt")));
        Assert.Equal(1, zeroDuration.State.Inventory.GetConsumableQuantity(ItemId.From("salt")));
    }

    [Fact]
    public void Rejections_are_unchanged_and_another_start_is_rejected_while_a_hold_is_active()
    {
        var initial = ShiftRuntimeState.Create(Fixture.LoadP0().Shift);
        var missing = Assert.IsType<ProcedureActionStartRejected>(Start(initial, "missing_log", "holy_water", 10));
        Assert.Equal(ProcedureActionStartRejectionReason.TargetNotFound, missing.Reason);
        Assert.Same(initial, missing.State);

        var held = Assert.IsType<ProcedureActionHoldStarted>(Start(AtProcedure("log_03"), "log_03", "holy_water", 10)).State;
        var active = Assert.IsType<ProcedureActionStartRejected>(Start(held, "log_03", "holy_water", 11));
        Assert.Equal(ProcedureActionStartRejectionReason.ActiveHoldAlreadyExists, active.Reason);
        Assert.Same(held, active.State);
        Assert.Equal(2, active.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
    }

    [Fact]
    public void All_start_rejection_categories_are_exact_unchanged_no_ops()
    {
        var initial = ShiftRuntimeState.Create(Fixture.LoadP0().Shift);
        AssertRejected(Start(initial, "log_03", "holy_water", 10), ProcedureActionStartRejectionReason.TargetNotAtProcedure, initial);

        var normal = AtProcedure("log_01");
        AssertRejected(Start(normal, "log_01", "holy_water", 10), ProcedureActionStartRejectionReason.NoProcedurePlan, normal);

        var resin = AtProcedure("log_06");
        AssertRejected(Start(resin, "log_06", "red_tape", 10), ProcedureActionStartRejectionReason.OutOfOrderItem, resin);
        AssertRejected(Start(resin, "log_06", "SALT", 10), ProcedureActionStartRejectionReason.MissingItem, resin);
        AssertRejected(Start(resin, "log_06", "hamster_statue", 10), ProcedureActionStartRejectionReason.UnconfiguredWrongAction, resin);

        var completed = Assert.IsType<ProcedureActionCompletedImmediately>(Start(AtProcedure("log_05"), "log_05", "relabel_stamp", 10)).State;
        AssertRejected(Start(completed, "log_05", "relabel_stamp", 11), ProcedureActionStartRejectionReason.RepeatedCorrectStep, completed);
        Assert.Throws<ArgumentException>(() => StartService.Start(initial, default, ItemId.From("holy_water"), ServerTick.From(10), Fixture.LoadP0().Anomalies));
        Assert.Throws<ArgumentException>(() => StartService.Start(initial, LogId.From("log_03"), default, ServerTick.From(10), Fixture.LoadP0().Anomalies));
        Assert.Throws<ArgumentException>(() => StartService.Start(initial, LogId.From("log_03"), ItemId.From("holy_water"), default, Fixture.LoadP0().Anomalies));
    }

    [Fact]
    public void Equivalent_starts_are_deterministic_and_active_hold_participates_in_value_equality()
    {
        var firstBefore = AtProcedure("log_03");
        var first = Assert.IsType<ProcedureActionHoldStarted>(Start(firstBefore, "log_03", "holy_water", 10)).State;
        var second = Assert.IsType<ProcedureActionHoldStarted>(Start(AtProcedure("log_03"), "log_03", "holy_water", 10)).State;

        Assert.False(firstBefore.ValueEquals(first));
        Assert.True(first.ValueEquals(second));
        var cancelled = Assert.IsType<ProcedureActionCancelled>(CancellationService.Cancel(first, LogId.From("log_03"))).State;
        Assert.False(first.ValueEquals(cancelled));
    }

    [Fact]
    public void Active_hold_identity_rejects_invalid_or_inconsistent_values()
    {
        var logId = LogId.From("log_03");
        var anomalyId = AnomalyId.From("PENITENT_TRUNK");
        var itemId = ItemId.From("holy_water");

        Assert.Null(ShiftRuntimeState.Create(Fixture.LoadP0().Shift).ActiveProcedureHold);
        Assert.Throws<ArgumentException>(() => new ActiveProcedureHold(default, anomalyId, itemId, 0, ServerTick.From(10), ServerTick.From(13), SimulationDuration.FromTicks(3)));
        Assert.Throws<ArgumentException>(() => new ActiveProcedureHold(logId, default, itemId, 0, ServerTick.From(10), ServerTick.From(13), SimulationDuration.FromTicks(3)));
        Assert.Throws<ArgumentException>(() => new ActiveProcedureHold(logId, anomalyId, default, 0, ServerTick.From(10), ServerTick.From(13), SimulationDuration.FromTicks(3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ActiveProcedureHold(logId, anomalyId, itemId, -1, ServerTick.From(10), ServerTick.From(13), SimulationDuration.FromTicks(3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ActiveProcedureHold(logId, anomalyId, itemId, 0, ServerTick.From(10), ServerTick.From(10), SimulationDuration.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ActiveProcedureHold(logId, anomalyId, itemId, 0, ServerTick.From(10), ServerTick.From(13), default));
        Assert.Throws<ArgumentException>(() => new ActiveProcedureHold(logId, anomalyId, itemId, 0, default, ServerTick.From(13), SimulationDuration.FromTicks(3)));
        Assert.Throws<ArgumentException>(() => new ActiveProcedureHold(logId, anomalyId, itemId, 0, ServerTick.From(10), default, SimulationDuration.FromTicks(3)));
        Assert.Throws<ArgumentException>(() => new ActiveProcedureHold(logId, anomalyId, itemId, 0, ServerTick.From(10), ServerTick.From(14), SimulationDuration.FromTicks(3)));
        Assert.Throws<OverflowException>(() => Start(AtProcedure("log_03"), "log_03", "holy_water", long.MaxValue - 1));
    }

    private static ProcedureActionStartResult Start(ShiftRuntimeState state, string logId, string itemId, long tick) =>
        StartService.Start(state, LogId.From(logId), ItemId.From(itemId), ServerTick.From(tick), Fixture.LoadP0().Anomalies);

    private static ProcedureActionDueCompletionResult CompleteDue(ShiftRuntimeState state, long tick) =>
        DueCompletionService.CompleteDue(state, ServerTick.From(tick), Fixture.LoadP0().Anomalies);

    private static ShiftRuntimeState AtProcedure(string logId)
    {
        var state = RuntimeFixture.MoveToIntake(ShiftRuntimeState.Create(Fixture.LoadP0().Shift), logId);
        return RuntimeFixture.MoveHost(state, logId, LogState.AT_PROCEDURE);
    }

    private static void AssertRejected(ProcedureActionStartResult result, ProcedureActionStartRejectionReason reason, ShiftRuntimeState expectedState)
    {
        var rejected = Assert.IsType<ProcedureActionStartRejected>(result);
        Assert.Equal(reason, rejected.Reason);
        Assert.Same(expectedState, rejected.State);
        Assert.Equal(expectedState.StateVersion, rejected.State.StateVersion);
        Assert.Null(rejected.State.ActiveProcedureHold);
    }
}
