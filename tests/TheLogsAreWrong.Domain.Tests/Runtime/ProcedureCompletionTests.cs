using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-006")]
public sealed class ProcedureCompletionTests
{
    private static readonly ItemActionCompletionService Service = new();

    [Fact]
    public void Initial_inventory_is_an_exact_immutable_copy_of_validated_resources()
    {
        var configuration = Fixture.LoadP0().Shift;
        var state = ShiftRuntimeState.Create(configuration);

        Assert.Equal(2, state.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.Equal(2, state.Inventory.GetConsumableQuantity(ItemId.From("salt")));
        Assert.Equal(2, state.Inventory.GetConsumableQuantity(ItemId.From("red_tape")));
        Assert.Equal(
            new[] { "caliper", "choir_cassette", "hamster_statue", "relabel_stamp", "scale", "sound_meter" },
            state.Inventory.ReusableItems.Select(item => item.Value).OrderBy(value => value));
        Assert.True(state.Inventory.IsAvailable(ItemId.From("relabel_stamp")));
        Assert.False(state.Inventory.IsAvailable(ItemId.From("missing_item")));
        Assert.IsType<ImmutableDictionary<ItemId, int>>(state.Inventory.ConsumableQuantities);
        Assert.IsType<ImmutableHashSet<ItemId>>(state.Inventory.ReusableItems);
        Assert.NotSame(configuration.Resources.Consumable, state.Inventory.ConsumableQuantities);
        Assert.NotSame(configuration.Resources.Reusable, state.Inventory.ReusableItems);
    }

    [Fact]
    public void Invalid_manual_inventory_definitions_fail_loudly()
    {
        var configuration = Fixture.LoadP0().Shift;

        Assert.Throws<ArgumentException>(() => ShiftRuntimeState.Create(configuration with
        {
            Resources = new ResourcesConfiguration(ImmutableDictionary<ItemId, int>.Empty.Add(default, 1), ImmutableHashSet<ItemId>.Empty)
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShiftRuntimeState.Create(configuration with
        {
            Resources = new ResourcesConfiguration(ImmutableDictionary<ItemId, int>.Empty.Add(ItemId.From("holy_water"), -1), ImmutableHashSet<ItemId>.Empty)
        }));
        Assert.Throws<ArgumentException>(() => ShiftRuntimeState.Create(configuration with
        {
            Resources = new ResourcesConfiguration(
                ImmutableDictionary<ItemId, int>.Empty.Add(ItemId.From("holy_water"), 1),
                ImmutableHashSet<ItemId>.Empty.Add(ItemId.From("holy_water")))
        }));
        Assert.Throws<ArgumentNullException>(() => ShiftRuntimeState.Create(configuration with
        {
            Resources = new ResourcesConfiguration(null!, ImmutableHashSet<ItemId>.Empty)
        }));
        Assert.Throws<ArgumentNullException>(() => ShiftRuntimeState.Create(configuration with
        {
            Resources = new ResourcesConfiguration(ImmutableDictionary<ItemId, int>.Empty, null!)
        }));
    }

    [Fact]
    public void Penitent_holy_water_completes_once_consumes_and_grants_only_on_final_step()
    {
        var before = AtProcedure("log_03");

        var result = Assert.IsType<ItemActionCompletionAccepted>(Complete(before, "log_03", "holy_water"));

        Assert.Equal(ItemActionCompletionKind.CorrectProcedureStep, result.Descriptor.Kind);
        Assert.True(result.Descriptor.ItemConsumed);
        Assert.Null(result.Descriptor.PriorProgress);
        Assert.True(result.Descriptor.CurrentProgress!.IsComplete);
        Assert.Equal(1, result.Descriptor.CurrentProgress.CompletedStepCount);
        Assert.Equal(before.StateVersion, result.Descriptor.PriorStateVersion);
        Assert.Equal(before.StateVersion.Next(), result.Descriptor.CurrentStateVersion);
        Assert.Equal(1, result.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.True(result.State.TryGetProcedureProgress(LogId.From("log_03"), out var progress));
        Assert.True(progress.IsComplete);
        Assert.True(result.State.TryGetLog(LogId.From("log_03"), out var log));
        Assert.Equal(LogState.AT_PROCEDURE, log.State);
        Assert.Contains(FlagId.From("SANITIZED_PENITENT"), log.Flags);
        Assert.Equal(ImmutableHashSet.Create(FlagId.From("SANITIZED_PENITENT")), result.Descriptor.NewlyGrantedFlags);
        Assert.Empty(result.Descriptor.Effects);

        var repeated = AssertRejected(Complete(result.State, "log_03", "holy_water"), ProcedureCompletionRejectionReason.RepeatedCorrectStep, result.State);
        Assert.Same(result.State, repeated.State);
        Assert.Equal(1, repeated.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
    }

    [Fact]
    public void Resin_requires_exact_order_and_case_then_grants_only_after_red_tape()
    {
        var before = AtProcedure("log_06");

        AssertRejected(Complete(before, "log_06", "red_tape"), ProcedureCompletionRejectionReason.OutOfOrderItem, before);
        AssertRejected(Complete(before, "log_06", "SALT"), ProcedureCompletionRejectionReason.MissingItem, before);

        var salt = Assert.IsType<ItemActionCompletionAccepted>(Complete(before, "log_06", "salt"));
        Assert.Equal(1, salt.State.Inventory.GetConsumableQuantity(ItemId.From("salt")));
        Assert.True(salt.State.TryGetProcedureProgress(LogId.From("log_06"), out var afterSalt));
        Assert.Equal(1, afterSalt.CompletedStepCount);
        Assert.False(afterSalt.IsComplete);
        Assert.True(salt.State.TryGetLog(LogId.From("log_06"), out var saltLog));
        Assert.DoesNotContain(FlagId.From("SEALED_RESIN"), saltLog.Flags);

        AssertRejected(Complete(salt.State, "log_06", "salt"), ProcedureCompletionRejectionReason.OutOfOrderItem, salt.State);
        var tape = Assert.IsType<ItemActionCompletionAccepted>(Complete(salt.State, "log_06", "red_tape"));
        Assert.Equal(1, tape.State.Inventory.GetConsumableQuantity(ItemId.From("red_tape")));
        Assert.True(tape.State.TryGetProcedureProgress(LogId.From("log_06"), out var complete));
        Assert.Equal(2, complete.CompletedStepCount);
        Assert.True(complete.IsComplete);
        Assert.True(tape.State.TryGetLog(LogId.From("log_06"), out var tapeLog));
        Assert.Contains(FlagId.From("SEALED_RESIN"), tapeLog.Flags);
        Assert.Equal(salt.State.StateVersion.Next(), tape.State.StateVersion);
    }

    [Fact]
    public void Exact_next_step_takes_precedence_over_an_overlapping_wrong_action_descriptor()
    {
        var source = Fixture.LoadP0().Anomalies;
        var resinId = AnomalyId.From("RESIN_BLASPHEMER");
        var resin = source.Definitions[resinId];
        var catalog = new AnomalyCatalog(source.Definitions.SetItem(resinId, resin with
        {
            WrongActions = resin.WrongActions.Add(
                ItemId.From("salt"),
                new WrongActionDefinition(true, null, true, ImmutableArray<EffectDefinition>.Empty))
        }));
        var before = AtProcedure("log_06");

        var result = Assert.IsType<ItemActionCompletionAccepted>(
            Service.Complete(before, LogId.From("log_06"), ItemId.From("salt"), catalog));

        Assert.Equal(ItemActionCompletionKind.CorrectProcedureStep, result.Descriptor.Kind);
        Assert.Empty(result.Descriptor.Effects);
        Assert.True(result.State.TryGetProcedureProgress(LogId.From("log_06"), out var progress));
        Assert.Equal(1, progress.CompletedStepCount);
    }

    [Fact]
    public void Progress_survives_return_from_and_reentry_to_the_procedure_node()
    {
        var before = AtProcedure("log_06");
        var afterSalt = Assert.IsType<ItemActionCompletionAccepted>(Complete(before, "log_06", "salt")).State;
        var returned = RuntimeFixture.MoveHost(afterSalt, "log_06", LogState.AT_INTAKE);
        var reentered = RuntimeFixture.MoveHost(returned, "log_06", LogState.AT_PROCEDURE);

        var afterTape = Assert.IsType<ItemActionCompletionAccepted>(Complete(reentered, "log_06", "red_tape")).State;

        Assert.True(afterTape.TryGetProcedureProgress(LogId.From("log_06"), out var progress));
        Assert.True(progress.IsComplete);
        Assert.True(afterTape.TryGetLog(LogId.From("log_06"), out var log));
        Assert.Contains(FlagId.From("SEALED_RESIN"), log.Flags);
    }

    [Fact]
    public void False_species_reusable_step_completes_without_consumable_decrement()
    {
        var before = AtProcedure("log_05");

        var result = Assert.IsType<ItemActionCompletionAccepted>(Complete(before, "log_05", "relabel_stamp"));

        Assert.False(result.Descriptor.ItemConsumed);
        Assert.Equal(before.Inventory.ConsumableQuantities, result.State.Inventory.ConsumableQuantities);
        Assert.True(result.State.TryGetProcedureProgress(LogId.From("log_05"), out var progress));
        Assert.True(progress.IsComplete);
        Assert.True(result.State.TryGetLog(LogId.From("log_05"), out var log));
        Assert.Contains(FlagId.From("CORRECTLY_RELABELED"), log.Flags);
        AssertRejected(Complete(result.State, "log_05", "relabel_stamp"), ProcedureCompletionRejectionReason.RepeatedCorrectStep, result.State);
    }

    [Fact]
    public void Two_consuming_completions_exhaust_item_and_later_exact_step_is_missing_without_mutation()
    {
        var configuration = Fixture.LoadP0().Shift with
        {
            Manifest = Fixture.LoadP0().Shift.Manifest.Add(new ManifestLogDefinition(
                LogId.From("log_13"), SpeciesId.From("pine"), SpeciesId.From("pine"), AnomalyId.From("PENITENT_TRUNK")))
        };
        var state = AtProcedure(ShiftRuntimeState.Create(configuration), "log_03");
        state = Assert.IsType<ItemActionCompletionAccepted>(Complete(state, "log_03", "holy_water")).State;
        state = RemoveFromProcedure(state, "log_03");
        state = AtProcedure(state, "log_08");
        state = Assert.IsType<ItemActionCompletionAccepted>(Complete(state, "log_08", "holy_water")).State;
        state = RemoveFromProcedure(state, "log_08");
        state = AtProcedure(state, "log_13");

        var rejected = AssertRejected(Complete(state, "log_13", "holy_water"), ProcedureCompletionRejectionReason.MissingItem, state);

        Assert.Equal(0, rejected.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.False(rejected.State.TryGetProcedureProgress(LogId.From("log_13"), out _));
        Assert.True(rejected.State.TryGetLog(LogId.From("log_13"), out var log));
        Assert.Empty(log.Flags);
        Assert.Equal(state.StateVersion, rejected.State.StateVersion);
    }

    [Fact]
    public void Configured_resin_holy_water_wrong_action_consumes_preserves_progress_and_effect_data_before_and_after_completion()
    {
        var before = AtProcedure("log_06");

        var wrongBefore = Assert.IsType<ItemActionCompletionAccepted>(Complete(before, "log_06", "holy_water"));
        Assert.Equal(ItemActionCompletionKind.ConfiguredWrongAction, wrongBefore.Descriptor.Kind);
        Assert.True(wrongBefore.Descriptor.ItemConsumed);
        Assert.Null(wrongBefore.Descriptor.PriorProgress);
        Assert.Null(wrongBefore.Descriptor.CurrentProgress);
        Assert.Empty(wrongBefore.Descriptor.NewlyGrantedFlags);
        Assert.Equal(1, wrongBefore.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.True(wrongBefore.State.TryGetLog(LogId.From("log_06"), out var unchanged));
        Assert.Equal(LogState.AT_PROCEDURE, unchanged.State);
        Assert.Empty(unchanged.Flags);
        var effect = Assert.Single(wrongBefore.Descriptor.Effects);
        Assert.Equal(EffectType.@lock, effect.Type);
        Assert.Equal(EffectEventId.From("RESIN_BUTTON_LOCK"), effect.Event);
        Assert.Equal("nearest_line_button", effect.Target);
        Assert.Equal(10, effect.DurationSeconds);
        Assert.Equal(before.StateVersion.Next(), wrongBefore.State.StateVersion);

        var salt = Assert.IsType<ItemActionCompletionAccepted>(Complete(wrongBefore.State, "log_06", "salt"));
        var completed = Assert.IsType<ItemActionCompletionAccepted>(Complete(salt.State, "log_06", "red_tape"));
        var wrongAfter = Assert.IsType<ItemActionCompletionAccepted>(Complete(completed.State, "log_06", "holy_water"));

        Assert.Equal(ItemActionCompletionKind.ConfiguredWrongAction, wrongAfter.Descriptor.Kind);
        Assert.True(wrongAfter.Descriptor.ItemConsumed);
        Assert.Equal(completed.State.StateVersion.Next(), wrongAfter.State.StateVersion);
        Assert.True(wrongAfter.State.TryGetProcedureProgress(LogId.From("log_06"), out var progress));
        Assert.True(progress.IsComplete);
        Assert.True(wrongAfter.State.TryGetLog(LogId.From("log_06"), out var completedLog));
        Assert.Contains(FlagId.From("SEALED_RESIN"), completedLog.Flags);
    }

    [Fact]
    public void Unconfigured_wrong_actions_and_all_precondition_rejections_are_exact_same_state_no_ops()
    {
        var initial = ShiftRuntimeState.Create(Fixture.LoadP0().Shift);
        AssertRejected(Complete(initial, "missing_log", "holy_water"), ProcedureCompletionRejectionReason.TargetNotFound, initial);
        AssertRejected(Complete(initial, "log_01", "holy_water"), ProcedureCompletionRejectionReason.TargetNotAtProcedure, initial);

        var normal = AtProcedure(initial, "log_01");
        AssertRejected(Complete(normal, "log_01", "holy_water"), ProcedureCompletionRejectionReason.NoProcedurePlan, normal);

        var resin = AtProcedure("log_06");
        var unknown = AssertRejected(Complete(resin, "log_06", "hamster_statue"), ProcedureCompletionRejectionReason.UnconfiguredWrongAction, resin);
        var wrongCase = AssertRejected(Complete(resin, "log_06", "HOLY_WATER"), ProcedureCompletionRejectionReason.MissingItem, resin);
        Assert.Same(resin, unknown.State);
        Assert.Same(resin, wrongCase.State);
        Assert.Throws<ArgumentException>(() => Service.Complete(resin, default, ItemId.From("holy_water"), Fixture.LoadP0().Anomalies));
        Assert.Throws<ArgumentException>(() => Service.Complete(resin, LogId.From("log_06"), default, Fixture.LoadP0().Anomalies));
        Assert.Throws<ArgumentNullException>(() => Service.Complete(resin, LogId.From("log_06"), ItemId.From("holy_water"), null!));
    }

    [Fact]
    public void Progress_and_inventory_participate_in_value_equality_and_equivalent_sequences_are_deterministic()
    {
        var initial = AtProcedure("log_03");
        var first = Assert.IsType<ItemActionCompletionAccepted>(Complete(initial, "log_03", "holy_water")).State;
        var secondStart = AtProcedure("log_03");
        var second = Assert.IsType<ItemActionCompletionAccepted>(Complete(secondStart, "log_03", "holy_water")).State;

        Assert.False(initial.ValueEquals(first));
        Assert.True(first.ValueEquals(second));
        Assert.Equal(initial.Inventory.GetConsumableQuantity(ItemId.From("holy_water")) - 1, first.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
    }

    [Fact]
    public void Processing_resolution_observes_only_final_procedure_flags_without_applying_quota()
    {
        var before = AtProcedure("log_06");
        var afterSalt = Assert.IsType<ItemActionCompletionAccepted>(Complete(before, "log_06", "salt")).State;
        Assert.True(afterSalt.TryGetLog(LogId.From("log_06"), out var saltLog));
        var incorrect = AnomalyProcessingResolver.Resolve(saltLog, Fixture.LoadP0().Anomalies);
        Assert.False(incorrect.AllRequiredFlagsPresent);
        Assert.Equal(0, incorrect.Settlement.CorrectAnomalyDelta);

        var afterTape = Assert.IsType<ItemActionCompletionAccepted>(Complete(afterSalt, "log_06", "red_tape")).State;
        Assert.True(afterTape.TryGetLog(LogId.From("log_06"), out var tapeLog));
        var correct = AnomalyProcessingResolver.Resolve(tapeLog, Fixture.LoadP0().Anomalies);
        Assert.True(correct.AllRequiredFlagsPresent);
        Assert.Equal(1, correct.Settlement.CorrectAnomalyDelta);
    }

    private static ItemActionCompletionResult Complete(ShiftRuntimeState state, string logId, string itemId) =>
        Service.Complete(state, LogId.From(logId), ItemId.From(itemId), Fixture.LoadP0().Anomalies);

    private static ShiftRuntimeState AtProcedure(string logId) => AtProcedure(ShiftRuntimeState.Create(Fixture.LoadP0().Shift), logId);

    private static ShiftRuntimeState AtProcedure(ShiftRuntimeState state, string logId)
    {
        state = RuntimeFixture.MoveToIntake(state, logId);
        return RuntimeFixture.MoveHost(state, logId, LogState.AT_PROCEDURE);
    }

    private static ShiftRuntimeState RemoveFromProcedure(ShiftRuntimeState state, string logId)
    {
        state = RuntimeFixture.MoveHost(state, logId, LogState.AT_INTAKE);
        state = RuntimeFixture.MoveHost(state, logId, LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveHost(state, logId, LogState.IN_SAW);
        return RuntimeFixture.MoveHost(state, logId, LogState.PROCESSED);
    }

    private static ItemActionCompletionRejected AssertRejected(
        ItemActionCompletionResult result,
        ProcedureCompletionRejectionReason reason,
        ShiftRuntimeState expectedState)
    {
        var rejected = Assert.IsType<ItemActionCompletionRejected>(result);
        Assert.Equal(reason, rejected.Reason);
        Assert.Same(expectedState, rejected.State);
        Assert.Equal(expectedState.StateVersion, rejected.State.StateVersion);
        Assert.True(expectedState.ValueEquals(rejected.State));
        return rejected;
    }
}
