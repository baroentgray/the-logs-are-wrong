using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Quota;

namespace TheLogsAreWrong.Domain.Tests.Quota;

[Trait("Scope", "TLAW-004")]
public sealed class QuotaSettlementServiceTests
{
    [Fact]
    public void Accepted_settlement_accumulates_exact_case_sensitive_species_and_leaves_original_unchanged()
    {
        var initial = QuotaFixture.CreateState();
        var first = QuotaFixture.Service.Apply(initial, QuotaFixture.Settlement("pine_log", "pine", 2, 1));
        var accepted = Assert.IsType<QuotaSettlementAccepted>(first);
        var state = QuotaFixture.Apply(accepted.State, "oak_log", "oak", 3, 0);
        state = QuotaFixture.Apply(state, "upper_pine", "Pine", 1, 0);

        Assert.Equal(0, initial.TotalCreditedUnits);
        Assert.Equal(0, initial.CorrectlyProcessedAnomalies);
        Assert.Equal(0, accepted.Descriptor.PriorSpeciesCredit);
        Assert.Equal(2, accepted.Descriptor.CurrentSpeciesCredit);
        Assert.Equal(0, accepted.Descriptor.PriorCorrectAnomalyCount);
        Assert.Equal(1, accepted.Descriptor.CurrentCorrectAnomalyCount);
        Assert.Equal(2, state.GetCreditedUnits(SpeciesId.From("pine")));
        Assert.Equal(3, state.GetCreditedUnits(SpeciesId.From("oak")));
        Assert.Equal(1, state.GetCreditedUnits(SpeciesId.From("Pine")));
        Assert.Equal(6, state.TotalCreditedUnits);
        Assert.Equal(1, state.CorrectlyProcessedAnomalies);
    }

    [Fact]
    public void Null_species_zero_credit_supports_anomaly_only_settlement()
    {
        var initial = QuotaFixture.CreateState();
        var result = QuotaFixture.Service.Apply(initial, QuotaFixture.Settlement("anomaly_only", null, 0, 1));
        var accepted = Assert.IsType<QuotaSettlementAccepted>(result);

        Assert.Null(accepted.Descriptor.CreditedSpecies);
        Assert.Equal(0, accepted.Descriptor.PriorSpeciesCredit);
        Assert.Equal(0, accepted.Descriptor.CurrentSpeciesCredit);
        Assert.Equal(1, accepted.State.CorrectlyProcessedAnomalies);
        Assert.Equal(0, accepted.State.TotalCreditedUnits);
        Assert.True(accepted.State.IsSettled(LogId.From("anomaly_only")));
    }

    [Fact]
    public void No_credit_settlement_still_records_log_and_blocks_later_credit()
    {
        var initial = QuotaFixture.CreateState();
        var first = QuotaFixture.Service.Apply(initial, QuotaFixture.Settlement("written_off", null, 0, 0));
        var accepted = Assert.IsType<QuotaSettlementAccepted>(first);
        var duplicate = Assert.IsType<QuotaSettlementDuplicate>(QuotaFixture.Service.Apply(accepted.State, QuotaFixture.Settlement("written_off", "pine", 1, 1)));

        Assert.Same(accepted.State, duplicate.State);
        Assert.Equal(0, duplicate.State.TotalCreditedUnits);
        Assert.Equal(0, duplicate.State.CorrectlyProcessedAnomalies);
        Assert.True(duplicate.State.IsSettled(LogId.From("written_off")));
    }

    [Fact]
    public void Duplicate_log_is_an_exact_same_state_no_op_even_when_contradictory()
    {
        var firstState = QuotaFixture.Apply(QuotaFixture.CreateState(), "duplicate_log", "pine", 1, 0);
        var duplicate = Assert.IsType<QuotaSettlementDuplicate>(QuotaFixture.Service.Apply(firstState, QuotaFixture.Settlement("duplicate_log", "oak", 99, 99)));

        Assert.Same(firstState, duplicate.State);
        Assert.Equal(1, duplicate.State.GetCreditedUnits(SpeciesId.From("pine")));
        Assert.Equal(0, duplicate.State.GetCreditedUnits(SpeciesId.From("oak")));
        Assert.Equal(1, duplicate.State.TotalCreditedUnits);
        Assert.Equal(0, duplicate.State.CorrectlyProcessedAnomalies);
    }

    [Fact]
    public void Over_credit_is_retained_monotonically()
    {
        var state = QuotaFixture.Apply(QuotaFixture.CreateState(), "over_credit", "pine", 8, 0);

        Assert.Equal(8, state.GetCreditedUnits(SpeciesId.From("pine")));
        Assert.Equal(8, state.TotalCreditedUnits);
    }

    [Fact]
    public void Objective_predicate_requires_each_target_species_and_correct_anomalies()
    {
        var state = QuotaFixture.CreateState();
        state = QuotaFixture.Apply(state, "pine_credit", "pine", 5, 1);
        state = QuotaFixture.Apply(state, "oak_credit", "oak", 4, 1);

        Assert.True(state.ObjectivesSatisfied);

        var oneAnomaly = QuotaFixture.CreateState();
        oneAnomaly = QuotaFixture.Apply(oneAnomaly, "pine_one", "pine", 5, 1);
        oneAnomaly = QuotaFixture.Apply(oneAnomaly, "oak_one", "oak", 4, 0);
        Assert.False(oneAnomaly.ObjectivesSatisfied);

        var wrongDistribution = QuotaFixture.CreateState();
        wrongDistribution = QuotaFixture.Apply(wrongDistribution, "wrong_distribution", "pine", 9, 2);
        Assert.Equal(9, wrongDistribution.TotalCreditedUnits);
        Assert.False(wrongDistribution.ObjectivesSatisfied);
    }

    [Fact]
    public void Over_target_credits_succeed_but_extra_species_cannot_substitute_for_targets()
    {
        var state = QuotaFixture.CreateState();
        state = QuotaFixture.Apply(state, "pine_over", "pine", 7, 1);
        state = QuotaFixture.Apply(state, "oak_over", "oak", 6, 1);
        Assert.True(state.ObjectivesSatisfied);

        var extraSpecies = QuotaFixture.CreateState();
        extraSpecies = QuotaFixture.Apply(extraSpecies, "pine_four", "pine", 4, 1);
        extraSpecies = QuotaFixture.Apply(extraSpecies, "oak_four", "oak", 4, 1);
        extraSpecies = QuotaFixture.Apply(extraSpecies, "birch_extra", "birch", 10, 0);
        Assert.False(extraSpecies.ObjectivesSatisfied);
    }

    [Fact]
    public void Seven_normal_credits_and_five_no_credit_settlements_cannot_satisfy_p0()
    {
        var state = QuotaFixture.CreateState();
        state = QuotaFixture.Apply(state, "normal_pine", "pine", 4);
        state = QuotaFixture.Apply(state, "normal_oak", "oak", 3);
        for (var index = 0; index < 5; index++)
        {
            state = QuotaFixture.Apply(state, $"no_credit_{index}");
        }

        Assert.Equal(7, state.TotalCreditedUnits);
        Assert.Equal(7, state.SettledLogIds.Count);
        Assert.False(state.ObjectivesSatisfied);
    }

    [Fact]
    public void Resolved_wrong_anomaly_outcomes_are_representable_without_selector_resolution()
    {
        var state = QuotaFixture.CreateState();
        state = QuotaFixture.Apply(state, "wrong_penitent");
        state = QuotaFixture.Apply(state, "wrong_resin");
        state = QuotaFixture.Apply(state, "wrong_false_species", "pine", 1);

        Assert.Equal(1, state.GetCreditedUnits(SpeciesId.From("pine")));
        Assert.Equal(0, state.CorrectlyProcessedAnomalies);
        Assert.Equal(3, state.SettledLogIds.Count);
        Assert.False(state.ObjectivesSatisfied);
    }
}
