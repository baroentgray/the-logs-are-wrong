using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Anomalies;

[Trait("Scope", "TLAW-005")]
public sealed class AnomalyResolutionTests
{
    [Fact]
    public void P0_procedure_plans_preserve_exact_steps_hold_consumption_grants_and_casing()
    {
        var plans = new ProcedurePlanResolver(Catalog());

        var penitent = plans.GetPlan(AnomalyId.From("PENITENT_TRUNK"));
        var resin = plans.GetPlan(AnomalyId.From("RESIN_BLASPHEMER"));
        var falseSpecies = plans.GetPlan(AnomalyId.From("FALSE_SPECIES"));

        var holyWater = Assert.Single(penitent.Steps);
        Assert.Equal("holy_water", holyWater.Item.Value);
        Assert.True(holyWater.Consumes);
        Assert.Equal(3, holyWater.HoldSeconds);
        Assert.Equal(new[] { "SANITIZED_PENITENT" }, penitent.GrantedFlags.Select(flag => flag.Value));

        Assert.Equal(new[] { "salt", "red_tape" }, resin.Steps.Select(step => step.Item.Value));
        Assert.All(resin.Steps, step => Assert.True(step.Consumes));
        Assert.All(resin.Steps, step => Assert.Null(step.HoldSeconds));
        Assert.Equal(new[] { "SEALED_RESIN" }, resin.GrantedFlags.Select(flag => flag.Value));

        var stamp = Assert.Single(falseSpecies.Steps);
        Assert.Equal("relabel_stamp", stamp.Item.Value);
        Assert.False(stamp.Consumes);
        Assert.Null(stamp.HoldSeconds);
        Assert.Equal(new[] { "CORRECTLY_RELABELED" }, falseSpecies.GrantedFlags.Select(flag => flag.Value));
    }

    [Fact]
    public void Procedure_plans_are_immutable_and_normal_logs_have_no_plan()
    {
        var plans = new ProcedurePlanResolver(Catalog());
        var penitent = plans.GetPlan(AnomalyId.From("PENITENT_TRUNK"));

        Assert.IsType<ImmutableArray<ProcedurePlanStep>>(penitent.Steps);
        Assert.IsType<ImmutableHashSet<FlagId>>(penitent.GrantedFlags);
        Assert.False(plans.TryGetPlan(Log("normal", "pine", "oak"), out var normalPlan));
        Assert.Null(normalPlan);
        Assert.Throws<AnomalyDefinitionNotFoundException>(() => plans.GetPlan(AnomalyId.From("PENITENT_TRUNK_LOWERCASE")));
    }

    [Theory]
    [InlineData("normal_pine", "pine", "oak")]
    [InlineData("normal_oak", "oak", "pine")]
    public void Normal_processing_credits_exact_true_species_and_ignores_declared_species(string logId, string trueSpecies, string declaredSpecies)
    {
        var log = Log(logId, trueSpecies, declaredSpecies);

        var result = AnomalyProcessingResolver.Resolve(log, Catalog());

        Assert.False(result.IsAnomalous);
        Assert.Null(result.AllRequiredFlagsPresent);
        Assert.Equal(LogState.PROCESSED, result.TerminalState);
        Assert.Equal(log.LogId, result.Settlement.LogId);
        Assert.Equal(SpeciesId.From(trueSpecies), result.Settlement.CreditedSpecies);
        Assert.Equal(1, result.Settlement.CreditedUnits);
        Assert.Equal(0, result.Settlement.CorrectAnomalyDelta);
        Assert.Empty(result.Effects);
    }

    [Theory]
    [InlineData("PENITENT_TRUNK", "SANITIZED_PENITENT")]
    [InlineData("RESIN_BLASPHEMER", "SEALED_RESIN")]
    [InlineData("FALSE_SPECIES", "CORRECTLY_RELABELED")]
    public void Correct_anomaly_processing_requires_flags_and_returns_true_species_credit(string anomaly, string requiredFlag)
    {
        var log = Log("correct_" + anomaly, "pine", "oak", anomaly, requiredFlag);

        var result = AnomalyProcessingResolver.Resolve(log, Catalog());

        Assert.True(result.IsAnomalous);
        Assert.True(result.AllRequiredFlagsPresent);
        Assert.Equal(LogState.PROCESSED, result.TerminalState);
        Assert.Equal(SpeciesId.From("pine"), result.Settlement.CreditedSpecies);
        Assert.Equal(1, result.Settlement.CreditedUnits);
        Assert.Equal(1, result.Settlement.CorrectAnomalyDelta);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void Extra_flags_are_allowed_but_flag_identity_is_exact_and_case_sensitive()
    {
        var withExtra = Log("extra", "pine", "oak", "PENITENT_TRUNK", "SANITIZED_PENITENT", "UNRELATED_FLAG");
        var wrongCase = Log("case", "pine", "oak", "PENITENT_TRUNK", "sanitized_penitent");

        var correct = AnomalyProcessingResolver.Resolve(withExtra, Catalog());
        var incorrect = AnomalyProcessingResolver.Resolve(wrongCase, Catalog());

        Assert.True(correct.AllRequiredFlagsPresent);
        Assert.Equal(1, correct.Settlement.CorrectAnomalyDelta);
        Assert.False(incorrect.AllRequiredFlagsPresent);
        Assert.Null(incorrect.Settlement.CreditedSpecies);
        Assert.Equal(0, incorrect.Settlement.CreditedUnits);
    }

    [Fact]
    public void All_required_flags_are_required_not_merely_any_flag()
    {
        var catalog = CatalogWithMultiFlagAnomaly();
        var onlyFirst = Log("only_first", "pine", "oak", "MULTI_FLAG", "FIRST");
        var both = Log("both", "pine", "oak", "MULTI_FLAG", "FIRST", "SECOND");

        var incomplete = AnomalyProcessingResolver.Resolve(onlyFirst, catalog);
        var complete = AnomalyProcessingResolver.Resolve(both, catalog);

        Assert.False(incomplete.AllRequiredFlagsPresent);
        Assert.Equal(0, incomplete.Settlement.CorrectAnomalyDelta);
        Assert.True(complete.AllRequiredFlagsPresent);
        Assert.Equal(1, complete.Settlement.CorrectAnomalyDelta);
    }

    [Fact]
    public void Incorrect_penitent_returns_exact_no_credit_time_penalty_without_mutation()
    {
        var log = Log("penitent_incorrect", "pine", "oak", "PENITENT_TRUNK");
        var beforeFlags = log.Flags;
        var quota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);

        var result = AnomalyProcessingResolver.Resolve(log, Catalog());

        Assert.False(result.AllRequiredFlagsPresent);
        Assert.Null(result.Settlement.CreditedSpecies);
        Assert.Equal(0, result.Settlement.CreditedUnits);
        Assert.Equal(0, result.Settlement.CorrectAnomalyDelta);
        var effect = Assert.Single(result.Effects);
        Assert.Equal(EffectType.time_penalty, effect.Type);
        Assert.Equal(EffectEventId.From("FALSE_PA_ANNOUNCEMENT"), effect.Event);
        Assert.Equal(8, effect.DurationSeconds);
        Assert.Equal(LogState.AT_INTAKE, log.State);
        Assert.Same(beforeFlags, log.Flags);
        Assert.Equal(0, quota.TotalCreditedUnits);
        Assert.Empty(quota.SettledLogIds);
    }

    [Fact]
    public void Incorrect_resin_returns_exact_no_credit_lock_without_mutation()
    {
        var log = Log("resin_incorrect", "pine", "oak", "RESIN_BLASPHEMER");

        var result = AnomalyProcessingResolver.Resolve(log, Catalog());

        Assert.False(result.AllRequiredFlagsPresent);
        Assert.Null(result.Settlement.CreditedSpecies);
        Assert.Equal(0, result.Settlement.CreditedUnits);
        Assert.Equal(0, result.Settlement.CorrectAnomalyDelta);
        var effect = Assert.Single(result.Effects);
        Assert.Equal(EffectType.@lock, effect.Type);
        Assert.Equal(EffectEventId.From("RESIN_BUTTON_LOCK"), effect.Event);
        Assert.Equal("nearest_line_button", effect.Target);
        Assert.Equal(10, effect.DurationSeconds);
        Assert.Equal(LogState.AT_INTAKE, log.State);
    }

    [Fact]
    public void Incorrect_false_species_returns_exact_declared_species_miscredit()
    {
        var log = Log("false_species_incorrect", "oak", "pine", "FALSE_SPECIES");

        var result = AnomalyProcessingResolver.Resolve(log, Catalog());

        Assert.False(result.AllRequiredFlagsPresent);
        Assert.Equal(SpeciesId.From("pine"), result.Settlement.CreditedSpecies);
        Assert.Equal(1, result.Settlement.CreditedUnits);
        Assert.Equal(0, result.Settlement.CorrectAnomalyDelta);
        var effect = Assert.Single(result.Effects);
        Assert.Equal(EffectType.miscredit, effect.Type);
        Assert.Equal(EffectEventId.From("CREDIT_TO_DECLARED_SPECIES"), effect.Event);
        Assert.Null(effect.DurationSeconds);
        Assert.Null(effect.Target);
    }

    [Fact]
    public void Resolution_is_value_equivalent_repeatedly_and_copies_immutable_effect_data()
    {
        var log = Log("repeatable", "pine", "oak", "RESIN_BLASPHEMER");

        var first = AnomalyProcessingResolver.Resolve(log, Catalog());
        var second = AnomalyProcessingResolver.Resolve(log, Catalog());

        Assert.True(first.ValueEquals(second));
        Assert.IsType<ImmutableArray<EffectDefinition>>(first.Effects);
        Assert.Equal(log.Flags, log.Flags);
        Assert.Equal(LogState.AT_INTAKE, log.State);
    }

    [Fact]
    public void Resin_holy_water_wrong_action_is_exact_data_only_and_leaves_log_processable()
    {
        var log = Log("resin_wrong_action", "pine", "oak", "RESIN_BLASPHEMER");

        var result = WrongActionResolver.Resolve(log, ItemId.From("holy_water"), Catalog());

        var configured = Assert.IsType<ConfiguredWrongActionResolution>(result);
        Assert.Equal(log.LogId, configured.LogId);
        Assert.Equal(AnomalyId.From("RESIN_BLASPHEMER"), configured.AnomalyId);
        Assert.Equal(ItemId.From("holy_water"), configured.AttemptedItem);
        Assert.True(configured.Consumes);
        Assert.True(configured.LeavesStateUnchanged);
        Assert.Null(configured.TerminalState);
        var effect = Assert.Single(configured.Effects);
        Assert.Equal(EffectType.@lock, effect.Type);
        Assert.Equal(EffectEventId.From("RESIN_BUTTON_LOCK"), effect.Event);
        Assert.Equal("nearest_line_button", effect.Target);
        Assert.Equal(10, effect.DurationSeconds);
        Assert.Equal(LogState.AT_INTAKE, log.State);
        Assert.Empty(log.Flags);
    }

    [Fact]
    public void Unconfigured_wrong_action_is_explicit_and_exact_item_identity_is_used()
    {
        var log = Log("resin_unconfigured", "pine", "oak", "RESIN_BLASPHEMER");

        var missing = WrongActionResolver.Resolve(log, ItemId.From("salt"), Catalog());
        var wrongCase = WrongActionResolver.Resolve(log, ItemId.From("HOLY_WATER"), Catalog());

        Assert.IsType<WrongActionNotConfigured>(missing);
        Assert.IsType<WrongActionNotConfigured>(wrongCase);
    }

    [Fact]
    public void Normal_log_wrong_action_is_explicitly_not_configured()
    {
        var result = WrongActionResolver.Resolve(Log("normal_wrong_action", "pine", "oak"), ItemId.From("holy_water"), Catalog());

        var missing = Assert.IsType<WrongActionNotConfigured>(result);
        Assert.Null(missing.AnomalyId);
        Assert.Equal(ItemId.From("holy_water"), missing.AttemptedItem);
    }

    [Fact]
    public void Missing_anomaly_and_default_identifiers_fail_loudly_and_deterministically()
    {
        var missing = Log("missing_anomaly", "pine", "oak", "MISSING");

        Assert.Throws<AnomalyDefinitionNotFoundException>(() => AnomalyProcessingResolver.Resolve(missing, Catalog()));
        Assert.Throws<ArgumentException>(() => WrongActionResolver.Resolve(missing, default, Catalog()));
        Assert.Throws<ArgumentException>(() => new ProcedurePlanResolver(new AnomalyCatalog(ImmutableDictionary<AnomalyId, AnomalyDefinition>.Empty.Add(default, MultiFlagDefinition()))));
    }

    [Fact]
    public void Malformed_manual_definitions_cannot_produce_contradictory_concrete_results()
    {
        var noneWithUnits = MultiFlagDefinition() with
        {
            Processing = new ProcessingDefinition(
                ImmutableHashSet<FlagId>.Empty,
                RouteWithoutFlagsPolicy.allowed,
                new ProcessingOutcome(LogState.PROCESSED, new QuotaCreditDefinition(SpeciesCreditRule.none, 1), 0, ImmutableArray<EffectDefinition>.Empty),
                new ProcessingOutcome(LogState.PROCESSED, new QuotaCreditDefinition(SpeciesCreditRule.none, 1), 0, ImmutableArray<EffectDefinition>.Empty))
        };
        var missingEffectId = MultiFlagDefinition() with
        {
            Processing = new ProcessingDefinition(
                ImmutableHashSet<FlagId>.Empty,
                RouteWithoutFlagsPolicy.allowed,
                new ProcessingOutcome(LogState.PROCESSED, new QuotaCreditDefinition(SpeciesCreditRule.true_species, 1), 0, ImmutableArray.Create(new EffectDefinition(EffectType.@lock, default, 1, "target"))),
                new ProcessingOutcome(LogState.PROCESSED, new QuotaCreditDefinition(SpeciesCreditRule.true_species, 1), 0, ImmutableArray<EffectDefinition>.Empty))
        };

        Assert.Throws<ArgumentException>(() => AnomalyProcessingResolver.Resolve(Log("none_units", "pine", "oak", "MULTI_FLAG"), CatalogWith("MULTI_FLAG", noneWithUnits)));
        Assert.Throws<ArgumentException>(() => AnomalyProcessingResolver.Resolve(Log("default_effect", "pine", "oak", "MULTI_FLAG"), CatalogWith("MULTI_FLAG", missingEffectId)));
    }

    [Fact]
    public void Public_concrete_outputs_reject_default_effect_identity()
    {
        var settlement = new QuotaSettlement(LogId.From("output_contract"), null, 0, 0);
        var effects = ImmutableArray.Create(new EffectDefinition(EffectType.@lock, default, 10, "target"));

        Assert.Throws<ArgumentException>(() => new ProcessingResolution(LogId.From("output_contract"), false, null, LogState.PROCESSED, settlement, effects));
        Assert.Throws<ArgumentException>(() => new ConfiguredWrongActionResolution(LogId.From("wrong_output"), AnomalyId.From("RESIN_BLASPHEMER"), ItemId.From("holy_water"), true, true, null, effects));
    }

    private static AnomalyCatalog Catalog() => Fixture.LoadP0().Anomalies;

    private static LogRuntimeState Log(string logId, string trueSpecies, string declaredSpecies, string? anomaly = null, params string[] flags) =>
        new(
            LogId.From(logId),
            SpeciesId.From(trueSpecies),
            SpeciesId.From(declaredSpecies),
            anomaly is null ? null : AnomalyId.From(anomaly),
            LogState.AT_INTAKE,
            flags.Select(FlagId.From).ToImmutableHashSet());

    private static AnomalyCatalog CatalogWithMultiFlagAnomaly() => CatalogWith("MULTI_FLAG", MultiFlagDefinition());

    private static AnomalyCatalog CatalogWith(string id, AnomalyDefinition definition) =>
        new(Catalog().Definitions.SetItem(AnomalyId.From(id), definition));

    private static AnomalyDefinition MultiFlagDefinition() =>
        new(
            AnomalyId.From("MULTI_FLAG"),
            0,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            new ConfirmTestDefinition(null, 0, false, null, "none", ImmutableArray<ItemId>.Empty),
            new ProcessingDefinition(
                ImmutableHashSet.Create(FlagId.From("FIRST"), FlagId.From("SECOND")),
                RouteWithoutFlagsPolicy.allowed,
                new ProcessingOutcome(LogState.PROCESSED, new QuotaCreditDefinition(SpeciesCreditRule.true_species, 1), 1, ImmutableArray<EffectDefinition>.Empty),
                new ProcessingOutcome(LogState.PROCESSED, new QuotaCreditDefinition(SpeciesCreditRule.none, 0), 0, ImmutableArray<EffectDefinition>.Empty)),
            new ProcedureDefinition(ImmutableArray<ProcedureStepDefinition>.Empty, ImmutableHashSet<FlagId>.Empty),
            ImmutableDictionary<ItemId, WrongActionDefinition>.Empty);
}
