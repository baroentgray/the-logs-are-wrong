using TheLogsAreWrong.Config.Yaml;
using TheLogsAreWrong.Domain.Configuration.Diagnostics;

namespace TheLogsAreWrong.Domain.Tests.Configuration;

public sealed class LoaderContractTests
{
    [Fact]
    public void Frozen_fixtures_load_as_a_complete_configuration_with_no_diagnostics()
    {
        var loader = new YamlConfigurationLoader();
        var shift = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "shift_p0.yaml"));
        var anomalies = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "anomalies.prototype.yaml"));

        var result = loader.Load(shift, anomalies);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}")));
        Assert.NotNull(result.Configuration);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Malformed_yaml_becomes_a_coded_diagnostic_instead_of_an_exception()
    {
        var loader = new YamlConfigurationLoader();
        var result = loader.Load("schema_version: [", "schema_version: 2\nanomalies: {}");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Configuration);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "TLAW-CFG-001");
    }

    [Fact]
    public void Diagnostics_are_sorted_by_the_document_code_path_and_message_contract()
    {
        var loader = new YamlConfigurationLoader();
        var shift = Fixture.ShiftYaml
            .Replace("  total: 12", "  total: 11", StringComparison.Ordinal);
        var anomalies = Fixture.AnomaliesYaml
            .Replace("    danger_weight: 1", "    danger_weight: -1", StringComparison.Ordinal);

        var expected = new[]
        {
            (ConfigurationDocument.Shift, "TLAW-CFG-113", "supply.total", "Supply total must equal manifest count."),
            (ConfigurationDocument.Shift, "TLAW-CFG-114", "supply.free_writeoff_buffer", "Writeoff buffer must equal supply minus quota."),
            (ConfigurationDocument.Anomalies, "TLAW-CFG-203", "anomalies.PENITENT_TRUNK.danger_weight", "Danger weight cannot be negative."),
            (ConfigurationDocument.Anomalies, "TLAW-CFG-203", "anomalies.RESIN_BLASPHEMER.danger_weight", "Danger weight cannot be negative.")
        };

        var actualRuns = Enumerable.Range(0, 5)
            .Select(_ => loader.Load(shift, anomalies).Diagnostics
                .Select(static diagnostic => (diagnostic.Document, diagnostic.Code, diagnostic.Path, diagnostic.Message))
                .ToArray())
            .ToArray();

        Assert.All(actualRuns, actual => Assert.Equal(expected, actual));
        Assert.All(actualRuns.Skip(1), actual => Assert.Equal(actualRuns[0], actual));
    }
}
