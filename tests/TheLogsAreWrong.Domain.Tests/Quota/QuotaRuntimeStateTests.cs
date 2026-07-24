using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Quota;

namespace TheLogsAreWrong.Domain.Tests.Quota;

internal static class QuotaFixture
{
    internal static readonly QuotaSettlementService Service = new();

    internal static QuotaRuntimeState CreateState() => QuotaRuntimeState.Create(Fixture.LoadP0().Shift);

    internal static QuotaSettlement Settlement(string logId, string? species = null, int units = 0, int correctAnomalyDelta = 0) => new(
        LogId.From(logId),
        species is null ? null : SpeciesId.From(species),
        units,
        correctAnomalyDelta);

    internal static QuotaRuntimeState Apply(QuotaRuntimeState state, QuotaSettlement settlement) =>
        Assert.IsType<QuotaSettlementAccepted>(Service.Apply(state, settlement)).State;

    internal static QuotaRuntimeState Apply(QuotaRuntimeState state, string logId, string? species = null, int units = 0, int correctAnomalyDelta = 0) =>
        Apply(state, Settlement(logId, species, units, correctAnomalyDelta));
}

[Trait("Scope", "TLAW-004")]
public sealed class QuotaRuntimeStateTests
{
    [Fact]
    public void P0_initialization_retains_targets_and_starts_with_zero_progress()
    {
        var state = QuotaFixture.CreateState();

        Assert.Equal(9, state.TargetTotal);
        Assert.Equal(5, state.TargetBySpecies[SpeciesId.From("pine")]);
        Assert.Equal(4, state.TargetBySpecies[SpeciesId.From("oak")]);
        Assert.Equal(2, state.MinimumCorrectlyProcessedAnomalies);
        Assert.Equal(0, state.TotalCreditedUnits);
        Assert.Equal(0, state.CorrectlyProcessedAnomalies);
        Assert.Equal(0, state.GetCreditedUnits(SpeciesId.From("pine")));
        Assert.Empty(state.CreditedBySpecies);
        Assert.Empty(state.SettledLogIds);
        Assert.False(state.ObjectivesSatisfied);
    }

    [Fact]
    public void Same_validated_configuration_creates_value_equivalent_quota_state()
    {
        var configuration = Fixture.LoadP0().Shift;

        var first = QuotaRuntimeState.Create(configuration);
        var second = QuotaRuntimeState.Create(configuration);

        Assert.NotSame(first, second);
        Assert.True(first.ValueEquals(second));
    }

    [Fact]
    public void Extra_species_credit_is_queryable_without_changing_objective_targets()
    {
        var initial = QuotaFixture.CreateState();
        var state = QuotaFixture.Apply(initial, "extra_species", "birch", 3);

        Assert.Equal(3, state.GetCreditedUnits(SpeciesId.From("birch")));
        Assert.False(state.TargetBySpecies.ContainsKey(SpeciesId.From("birch")));
        Assert.Equal(5, state.TargetBySpecies[SpeciesId.From("pine")]);
        Assert.Equal(4, state.TargetBySpecies[SpeciesId.From("oak")]);
        Assert.Equal(0, initial.GetCreditedUnits(SpeciesId.From("birch")));
    }

    [Fact]
    public void Settlement_contract_rejects_invalid_identifiers_and_negative_values()
    {
        Assert.Throws<ArgumentException>(() => new QuotaSettlement(default, SpeciesId.From("pine"), 1, 0));
        Assert.Throws<ArgumentException>(() => new QuotaSettlement(LogId.From("invalid_species"), default(SpeciesId), 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuotaSettlement(LogId.From("negative_units"), SpeciesId.From("pine"), -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuotaSettlement(LogId.From("negative_anomaly"), null, 0, -1));
        Assert.Throws<ArgumentException>(() => new QuotaSettlement(LogId.From("null_credit"), null, 1, 0));
    }

    [Fact]
    public void Checked_arithmetic_fails_loudly_on_credit_overflow()
    {
        var pine = SpeciesId.From("pine");
        var objectives = new ObjectivesDefinition(
            new QuotaDefinition(int.MaxValue, ImmutableDictionary<SpeciesId, int>.Empty.Add(pine, int.MaxValue)),
            0);
        var state = QuotaRuntimeState.Create(objectives);
        state = QuotaFixture.Apply(state, "max_credit", "pine", int.MaxValue);

        Assert.Throws<OverflowException>(() => QuotaFixture.Service.Apply(state, QuotaFixture.Settlement("overflow_credit", "pine", 1)));
    }

    [Fact]
    public void Checked_arithmetic_fails_loudly_on_correct_anomaly_overflow()
    {
        var state = QuotaFixture.Apply(QuotaFixture.CreateState(), "max_anomaly", null, 0, int.MaxValue);

        Assert.Throws<OverflowException>(() => QuotaFixture.Service.Apply(state, QuotaFixture.Settlement("overflow_anomaly", null, 0, 1)));
    }
}
