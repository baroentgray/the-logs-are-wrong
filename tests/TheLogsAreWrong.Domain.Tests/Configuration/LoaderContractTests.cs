using TheLogsAreWrong.Config.Yaml;

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
        var result = loader.Load("schema_version: 3", "schema_version: 3");

        Assert.Collection(
            result.Diagnostics,
            diagnostic => Assert.Equal(("TLAW-CFG-004", "schema_version"), (diagnostic.Code, diagnostic.Path)),
            diagnostic => Assert.Equal(("TLAW-CFG-004", "schema_version"), (diagnostic.Code, diagnostic.Path)));
    }
}
