using TheLogsAreWrong.Config.Yaml;
using TheLogsAreWrong.Domain.Configuration;

namespace TheLogsAreWrong.Domain.Tests;

internal static class Fixture
{
    internal static string ShiftYaml => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "shift_p0.yaml"));
    internal static string AnomaliesYaml => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "anomalies.prototype.yaml"));

    internal static ValidatedConfiguration LoadP0()
    {
        var result = new YamlConfigurationLoader().Load(ShiftYaml, AnomaliesYaml);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        return Assert.IsType<ValidatedConfiguration>(result.Configuration);
    }
}
