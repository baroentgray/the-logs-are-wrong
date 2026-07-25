using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Anomalies;

[Trait("Scope", "TLAW-008")]
public sealed class ConfirmationTestPlanTests
{
    [Fact]
    public void P0_confirmation_plans_preserve_exact_data_and_normal_logs_have_no_plan()
    {
        var resolver = new ConfirmationTestPlanResolver(Fixture.LoadP0().Anomalies);

        var penitent = resolver.GetPlan(AnomalyId.From("PENITENT_TRUNK"));
        var resin = resolver.GetPlan(AnomalyId.From("RESIN_BLASPHEMER"));
        var falseSpecies = resolver.GetPlan(AnomalyId.From("FALSE_SPECIES"));
        var normal = new LogRuntimeState(LogId.From("normal"), SpeciesId.From("pine"), SpeciesId.From("pine"), null, LogState.AT_INTAKE, ImmutableHashSet<FlagId>.Empty);

        Assert.Equal(ImmutableHashSet.Create(ItemId.From("sound_meter")), penitent.RequiredTools);
        Assert.Equal(SimulationDuration.FromTicks(4), penitent.Duration);
        Assert.True(penitent.Continuous);
        Assert.True(penitent.ResetWhenConditionLost);
        Assert.Equal(LineNoise.QUIET, penitent.RequiredLineNoise);
        Assert.Equal("spoken_names_detected", penitent.Result);
        Assert.Equal(ImmutableHashSet.Create(ItemId.From("choir_cassette")), resin.RequiredTools);
        Assert.True(resin.Continuous);
        Assert.True(resin.ResetWhenConditionLost);
        Assert.Null(resin.RequiredLineNoise);
        Assert.Equal("choir_resonance", resin.Result);
        Assert.Equal(ImmutableHashSet.Create(ItemId.From("scale"), ItemId.From("caliper")), falseSpecies.RequiredTools);
        Assert.Equal(SimulationDuration.FromTicks(6), falseSpecies.Duration);
        Assert.False(falseSpecies.Continuous);
        Assert.False(falseSpecies.ResetWhenConditionLost);
        Assert.Equal("true_species_identified", falseSpecies.Result);
        Assert.False(resolver.TryGetPlan(normal, out _));
    }

    [Fact]
    public void Confirmation_plan_validation_is_strict_and_tool_collections_are_immutable()
    {
        Assert.Throws<ArgumentException>(() => new ConfirmationTestPlan(default, ImmutableHashSet.Create(ItemId.From("sound_meter")), SimulationDuration.FromTicks(1), true, null, true, "result"));
        Assert.Throws<ArgumentException>(() => new ConfirmationTestPlan(AnomalyId.From("PENITENT_TRUNK"), ImmutableHashSet<ItemId>.Empty, SimulationDuration.FromTicks(1), true, null, true, "result"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConfirmationTestPlan(AnomalyId.From("PENITENT_TRUNK"), ImmutableHashSet.Create(ItemId.From("sound_meter")), SimulationDuration.Zero, true, null, true, "result"));
        Assert.Throws<ArgumentException>(() => new ConfirmationTestPlan(AnomalyId.From("PENITENT_TRUNK"), ImmutableHashSet.Create(ItemId.From("sound_meter")), SimulationDuration.FromTicks(1), true, null, true, " "));

        var source = Fixture.LoadP0().Anomalies;
        var penitentId = AnomalyId.From("PENITENT_TRUNK");
        var penitent = source.Definitions[penitentId];
        var duplicateTools = new AnomalyCatalog(source.Definitions.SetItem(penitentId, penitent with
        {
            ConfirmTest = penitent.ConfirmTest with { Tools = ImmutableArray.Create(ItemId.From("sound_meter"), ItemId.From("sound_meter")) }
        }));

        Assert.Throws<ArgumentException>(() => new ConfirmationTestPlanResolver(duplicateTools));
    }
}
