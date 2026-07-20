using TheLogsAreWrong.Config.Yaml;

namespace TheLogsAreWrong.Domain.Tests.Validation;

public sealed class ValidationRuleTests
{
    [Theory]
    [InlineData("shift_id: P0_SHIFT_A", "shift_id: \" \"", "TLAW-CFG-101", "shift_id")]
    [InlineData("supply:\n  total: 12", "supply:\n  total: 0", "TLAW-CFG-112", "supply.total")]
    [InlineData("normal_feed_delay_seconds: 5", "normal_feed_delay_seconds: 0", "TLAW-CFG-125", "scheduler.normal_feed_delay_seconds")]
    [InlineData("pause_intake_timer_during_test: false", "pause_intake_timer_during_test: true", "TLAW-CFG-136", "line_noise.pause_intake_timer_during_test")]
    [InlineData("return_state: STABLE", "return_state: MELTDOWN", "TLAW-CFG-145", "containment.after_successful_ritual.return_state")]
    public void Shift_semantic_rules_return_coded_paths(string original, string replacement, string code, string path)
    {
        var result = new YamlConfigurationLoader().Load(Fixture.ShiftYaml.Replace(original, replacement, StringComparison.Ordinal), Fixture.AnomaliesYaml);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code && diagnostic.Path == path);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Configuration);
    }

    [Theory]
    [InlineData("danger_weight: 1", "danger_weight: -1", "TLAW-CFG-203")]
    [InlineData("required_line_noise: QUIET", "required_line_noise: MEDIUM", "TLAW-CFG-211")]
    [InlineData("route_without_flags: allowed", "route_without_flags: forbidden", "TLAW-CFG-215")]
    [InlineData("type: time_penalty", "type: explosion", "TLAW-CFG-224")]
    public void Anomaly_semantic_rules_return_coded_diagnostics(string original, string replacement, string code)
    {
        var result = new YamlConfigurationLoader().Load(Fixture.ShiftYaml, Fixture.AnomaliesYaml.Replace(original, replacement, StringComparison.Ordinal));

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Success_can_carry_warnings_but_errors_never_leak_partial_configuration()
    {
        var warningOnly = Fixture.ShiftYaml.Replace("true_species: pine", "true_species: birch", StringComparison.Ordinal);
        var error = Fixture.ShiftYaml.Replace("seed: 47001", "seed: forty", StringComparison.Ordinal);
        var loader = new YamlConfigurationLoader();

        var warningResult = loader.Load(warningOnly, Fixture.AnomaliesYaml);
        var errorResult = loader.Load(error, Fixture.AnomaliesYaml);

        Assert.True(warningResult.IsSuccess);
        Assert.NotNull(warningResult.Configuration);
        Assert.Contains(warningResult.Diagnostics, diagnostic => diagnostic.Code == "TLAW-CFG-310");
        Assert.False(errorResult.IsSuccess);
        Assert.Null(errorResult.Configuration);
        Assert.Contains(errorResult.Diagnostics, diagnostic => diagnostic.Code == "TLAW-CFG-008");
    }
}
