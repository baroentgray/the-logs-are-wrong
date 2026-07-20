using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;

namespace TheLogsAreWrong.Domain.Tests.ScenarioP0;

public sealed class ScenarioP0ContractTests
{
    [Fact]
    public void P0_001_to_015_shift_identity_manifest_objectives_and_profiles_are_frozen()
    {
        var configuration = Fixture.LoadP0();
        var shift = configuration.Shift;

        Assert.Equal("P0_SHIFT_A", shift.ShiftId.Value);
        Assert.Equal(47001, shift.Seed.Value);
        Assert.Equal(12, shift.Manifest.Length);
        Assert.Equal(6, shift.Manifest.Count(log => log.TrueSpecies == SpeciesId.From("pine")));
        Assert.Equal(6, shift.Manifest.Count(log => log.TrueSpecies == SpeciesId.From("oak")));
        Assert.Equal(7, shift.Manifest.Count(log => log.Anomaly is null));
        Assert.Equal(2, shift.Manifest.Count(log => log.Anomaly == AnomalyId.From("PENITENT_TRUNK")));
        Assert.Equal(2, shift.Manifest.Count(log => log.Anomaly == AnomalyId.From("RESIN_BLASPHEMER")));
        Assert.Equal(1, shift.Manifest.Count(log => log.Anomaly == AnomalyId.From("FALSE_SPECIES")));
        Assert.Equal(4, shift.Manifest.Count(log => log.Anomaly is null && log.TrueSpecies == SpeciesId.From("pine")));
        Assert.Equal(3, shift.Manifest.Count(log => log.Anomaly is null && log.TrueSpecies == SpeciesId.From("oak")));
        Assert.Equal(9, shift.Objectives.Quota.Total);
        Assert.Equal(5, shift.Objectives.Quota.BySpecies[SpeciesId.From("pine")]);
        Assert.Equal(4, shift.Objectives.Quota.BySpecies[SpeciesId.From("oak")]);
        Assert.Equal(3, shift.Supply.FreeWriteoffBuffer);
        Assert.Equal(2, shift.Objectives.MinCorrectlyProcessedAnomalies);
        Assert.Equal(new[] { 60, 840 }, new[] { shift.Profiles[ProfileId.From("learning")].IntakeTimeoutSeconds, shift.Profiles[ProfileId.From("learning")].HardShiftDeadlineSeconds });
        Assert.Equal(new[] { 45, 600 }, new[] { shift.Profiles[ProfileId.From("pressure")].IntakeTimeoutSeconds, shift.Profiles[ProfileId.From("pressure")].HardShiftDeadlineSeconds });
    }

    [Fact]
    public void P0_016_to_024_resources_scheduler_noise_and_containment_are_frozen()
    {
        var shift = Fixture.LoadP0().Shift;

        Assert.Equal(2, shift.Resources.Consumable[ItemId.From("holy_water")]);
        Assert.Equal(2, shift.Resources.Consumable[ItemId.From("salt")]);
        Assert.Equal(2, shift.Resources.Consumable[ItemId.From("red_tape")]);
        Assert.Equal(new[] { "caliper", "choir_cassette", "hamster_statue", "relabel_stamp", "scale", "sound_meter" }, shift.Resources.Reusable.Select(item => item.Value).OrderBy(static item => item));
        Assert.True(shift.Scheduler.Capacities[NodeId.CONTAINMENT].IsUnlimited);
        Assert.All(new[] { NodeId.FEED_GATE, NodeId.INTAKE, NodeId.PROCEDURE, NodeId.SAW_QUEUE, NodeId.SAW }, node => Assert.Equal(1, shift.Scheduler.Capacities[node].Limit));
        Assert.Equal(new[] { 0, 5, 2, 6, 6, 2 }, new[] { shift.Scheduler.InitialAdmissionDelaySeconds, shift.Scheduler.NormalFeedDelaySeconds, shift.Scheduler.EarlyFeedDelaySeconds, shift.Scheduler.SawCycleSeconds, shift.Scheduler.RepairHoldSeconds, shift.Scheduler.MovementNoiseSeconds });
        Assert.Equal("saw_queue", shift.Scheduler.DefaultTimeoutRoute);
        Assert.Equal(new[] { HostTickStage.hold_and_procedure_completions, HostTickStage.accepted_intents_by_server_receive_sequence, HostTickStage.deadline_expirations, HostTickStage.saw_transitions, HostTickStage.feed_and_auto_routes, HostTickStage.derived_states, HostTickStage.event_emission }, shift.Scheduler.SameTickOrder);
        Assert.Equal(4, shift.LineNoise.PenitentConfirmRequiresContinuousQuietSeconds);
        Assert.False(shift.LineNoise.PauseIntakeTimerDuringTest);
        Assert.Equal(new[] { 4, 20, 10 }, new[] { shift.Containment.RitualHoldSeconds, shift.Containment.ServiceRequestedGraceSeconds, shift.Containment.OverdueSeconds });
        Assert.Equal(90, shift.Containment.IntervalByDangerWeight["1"]);
        Assert.Equal(75, shift.Containment.IntervalByDangerWeight["2"]);
        Assert.Equal(60, shift.Containment.IntervalByDangerWeight["3_or_more"]);
        Assert.Equal("forced_line_pause", shift.Containment.PrototypeIncident.Type);
        Assert.Equal(8, shift.Containment.PrototypeIncident.DurationSeconds);
    }

    [Fact]
    public void P0_023_and_025_to_030_anomaly_contract_is_frozen()
    {
        var anomalies = Fixture.LoadP0().Anomalies.Definitions;
        var penitent = anomalies[AnomalyId.From("PENITENT_TRUNK")];
        var resin = anomalies[AnomalyId.From("RESIN_BLASPHEMER")];
        var falseSpecies = anomalies[AnomalyId.From("FALSE_SPECIES")];

        Assert.True(resin.WrongActions[ItemId.From("holy_water")].Consumes);
        Assert.Equal(LogState.PROCESSED, penitent.Processing.OnIncorrect.TerminalState);
        Assert.Equal(SpeciesCreditRule.none, penitent.Processing.OnIncorrect.QuotaCredit.Species);
        Assert.Equal(0, penitent.Processing.OnIncorrect.QuotaCredit.Units);
        Assert.Equal(0, penitent.Processing.OnIncorrect.CorrectAnomalyDelta);
        Assert.Equal(EffectType.time_penalty, Assert.Single(penitent.Processing.OnIncorrect.Effects).Type);
        Assert.Equal(8, Assert.Single(penitent.Processing.OnIncorrect.Effects).DurationSeconds);
        Assert.Equal(EffectType.@lock, Assert.Single(resin.Processing.OnIncorrect.Effects).Type);
        Assert.Equal("nearest_line_button", Assert.Single(resin.Processing.OnIncorrect.Effects).Target);
        Assert.Equal(10, Assert.Single(resin.Processing.OnIncorrect.Effects).DurationSeconds);
        Assert.Equal(SpeciesCreditRule.declared_species, falseSpecies.Processing.OnIncorrect.QuotaCredit.Species);
        Assert.Equal(EffectType.miscredit, Assert.Single(falseSpecies.Processing.OnIncorrect.Effects).Type);
        Assert.Equal("CREDIT_TO_DECLARED_SPECIES", Assert.Single(falseSpecies.Processing.OnIncorrect.Effects).Event.Value);
        Assert.All(anomalies.Values, anomaly =>
        {
            Assert.Equal(SpeciesCreditRule.true_species, anomaly.Processing.OnCorrect.QuotaCredit.Species);
            Assert.Equal(1, anomaly.Processing.OnCorrect.QuotaCredit.Units);
            Assert.Equal(1, anomaly.Processing.OnCorrect.CorrectAnomalyDelta);
            Assert.Empty(anomaly.Processing.OnCorrect.Effects);
        });
        Assert.Equal(new[] { 1, 1, 0 }, anomalies.Values.OrderBy(anomaly => anomaly.Id.Value).Select(anomaly => anomaly.DangerWeight).OrderByDescending(static weight => weight));
        Assert.Equal("log_05", Assert.Single(Fixture.LoadP0().Shift.Manifest.Where(log => log.TrueSpecies != log.DeclaredSpecies)).Id.Value);
    }
}
