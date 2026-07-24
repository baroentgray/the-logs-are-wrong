using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-003")]
public sealed class ShiftRuntimeInitializationTests
{
    [Fact]
    public void P0_initialization_preserves_manifest_order_seed_identity_species_and_anomaly()
    {
        var configuration = Fixture.LoadP0().Shift;
        var state = ShiftRuntimeState.Create(configuration);

        Assert.Equal(configuration.ShiftId, state.ShiftId);
        Assert.Equal(configuration.Seed, state.ShiftSeed);
        Assert.Equal(TheLogsAreWrong.Domain.Primitives.StateVersion.Zero, state.StateVersion);
        Assert.Equal(12, state.Logs.Length);
        Assert.Equal(configuration.Manifest.Select(log => log.Id), state.Logs.Select(log => log.LogId));
        Assert.Equal(configuration.Manifest.Select(log => log.TrueSpecies), state.Logs.Select(log => log.TrueSpecies));
        Assert.Equal(configuration.Manifest.Select(log => log.DeclaredSpecies), state.Logs.Select(log => log.DeclaredSpecies));
        Assert.Equal(configuration.Manifest.Select(log => log.Anomaly), state.Logs.Select(log => log.Anomaly));
        Assert.All(state.Logs, log =>
        {
            Assert.Equal(LogState.SCHEDULED, log.State);
            Assert.Empty(log.Flags);
        });
        Assert.Equal(12, state.GetNodeOccupancy(NodeId.SUPPLY_QUEUE));
        Assert.Empty(state.ProcessedIntentIds);
    }

    [Fact]
    public void P0_initialization_is_deterministic_and_uses_exact_log_lookup()
    {
        var configuration = Fixture.LoadP0().Shift;
        var first = ShiftRuntimeState.Create(configuration);
        var second = ShiftRuntimeState.Create(configuration);

        Assert.True(first.ValueEquals(second));
        Assert.True(first.TryGetLog(LogId.From("log_05"), out var log));
        Assert.Equal(SpeciesId.From("oak"), log.TrueSpecies);
        Assert.Equal(SpeciesId.From("pine"), log.DeclaredSpecies);
        Assert.Equal(AnomalyId.From("FALSE_SPECIES"), log.Anomaly);
        Assert.False(first.TryGetLog(LogId.From("LOG_05"), out _));
    }

    [Fact]
    public void Initialization_rejects_programmer_invalid_manifest_inputs_loudly()
    {
        var configuration = Fixture.LoadP0().Shift;
        var invalid = configuration with
        {
            Manifest = [new TheLogsAreWrong.Domain.Configuration.ManifestLogDefinition(default, SpeciesId.From("pine"), SpeciesId.From("pine"), null)]
        };

        Assert.Throws<ArgumentException>(() => ShiftRuntimeState.Create(invalid));
    }
}
