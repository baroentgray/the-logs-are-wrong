using TheLogsAreWrong.Config.Yaml;
using TheLogsAreWrong.Domain.Configuration.Diagnostics;

namespace TheLogsAreWrong.Domain.Tests.Parsing;

public sealed class ParserRuleTests
{
    [Theory]
    [InlineData("schema_version: [", "TLAW-CFG-001")]
    [InlineData("", "TLAW-CFG-011")]
    public void Shift_parser_failures_are_coded(string shiftYaml, string code)
    {
        var result = new YamlConfigurationLoader().Load(shiftYaml, Fixture.AnomaliesYaml);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Configuration);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code && diagnostic.Document == ConfigurationDocument.Shift);
    }

    [Fact]
    public void Duplicate_unknown_anchor_and_extra_document_are_not_silently_accepted()
    {
        var duplicate = Fixture.ShiftYaml.Replace("seed: 47001", "seed: 47001\nseed: 47002", StringComparison.Ordinal);
        var unknown = Fixture.ShiftYaml.Replace("seed: 47001", "seed: 47001\nbogus_key: 1", StringComparison.Ordinal);
        var anchors = Fixture.ShiftYaml.Replace("shift_id: P0_SHIFT_A", "shift_id: &shift P0_SHIFT_A", StringComparison.Ordinal);
        var multiple = Fixture.ShiftYaml + "\n---\nschema_version: 2\n";
        var loader = new YamlConfigurationLoader();

        Assert.Contains(loader.Load(duplicate, Fixture.AnomaliesYaml).Diagnostics, diagnostic => diagnostic.Code == "TLAW-CFG-005");
        Assert.Contains(loader.Load(unknown, Fixture.AnomaliesYaml).Diagnostics, diagnostic => diagnostic.Code == "TLAW-CFG-006");
        Assert.Contains(loader.Load(anchors, Fixture.AnomaliesYaml).Diagnostics, diagnostic => diagnostic.Code == "TLAW-CFG-009");
        Assert.Contains(loader.Load(multiple, Fixture.AnomaliesYaml).Diagnostics, diagnostic => diagnostic.Code == "TLAW-CFG-010");
    }

    [Fact]
    public void Unsupported_schema_stops_structural_spam_and_swapped_documents_are_identified()
    {
        var unsupported = Fixture.ShiftYaml.Replace("schema_version: 2", "schema_version: 3", StringComparison.Ordinal);
        var loader = new YamlConfigurationLoader();

        var unsupportedResult = loader.Load(unsupported, Fixture.AnomaliesYaml);
        var swappedResult = loader.Load(Fixture.AnomaliesYaml, Fixture.ShiftYaml);

        Assert.Contains(unsupportedResult.Diagnostics, diagnostic => diagnostic.Code == "TLAW-CFG-004" && diagnostic.Path == "schema_version");
        Assert.DoesNotContain(unsupportedResult.Diagnostics, diagnostic => diagnostic.Code == "TLAW-CFG-101");
        Assert.Equal(2, swappedResult.Diagnostics.Count(diagnostic => diagnostic.Code == "TLAW-CFG-014"));
    }
}
