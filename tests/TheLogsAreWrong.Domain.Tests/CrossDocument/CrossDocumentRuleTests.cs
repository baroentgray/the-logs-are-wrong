using TheLogsAreWrong.Config.Yaml;

namespace TheLogsAreWrong.Domain.Tests.CrossDocument;

public sealed class CrossDocumentRuleTests
{
    [Theory]
    [InlineData("anomaly: PENITENT_TRUNK", "anomaly: GHOST_LOG", "TLAW-CFG-302")]
    [InlineData("item: holy_water", "item: crowbar", "TLAW-CFG-303")]
    [InlineData("item: holy_water\n        hold_seconds: 3\n        consumes: true", "item: sound_meter\n        hold_seconds: 3\n        consumes: true", "TLAW-CFG-304")]
    [InlineData("duration_seconds: 4", "duration_seconds: 5", "TLAW-CFG-308")]
    public void Cross_document_contracts_are_validated(string original, string replacement, string code)
    {
        var anomalies = Fixture.AnomaliesYaml.Replace(original, replacement, StringComparison.Ordinal);
        var shift = code == "TLAW-CFG-302" ? Fixture.ShiftYaml.Replace(original, replacement, StringComparison.Ordinal) : Fixture.ShiftYaml;
        var result = new YamlConfigurationLoader().Load(shift, anomalies);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
        Assert.False(result.IsSuccess);
    }
}
