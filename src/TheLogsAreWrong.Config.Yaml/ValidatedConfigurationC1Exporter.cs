using System.Security.Cryptography;
using System.Text;
using TheLogsAreWrong.Domain.Configuration;

namespace TheLogsAreWrong.Config.Yaml;

/// <summary>
/// Outside-Unity export boundary: canonical YAML is validated by <see cref="YamlConfigurationLoader"/>
/// before its already validated PortableAuthority graph is handed to C1 transport.
/// </summary>
public static class ValidatedConfigurationC1Exporter
{
    public static ValidatedConfigurationC1Export Export(
        string shiftYaml,
        string anomaliesYaml,
        ValidatedConfigurationC1SourceBinding sourceBinding)
    {
        ArgumentNullException.ThrowIfNull(shiftYaml);
        ArgumentNullException.ThrowIfNull(anomaliesYaml);
        ArgumentNullException.ThrowIfNull(sourceBinding);
        if (!string.Equals(Sha256(shiftYaml), sourceBinding.ShiftYamlSha256, StringComparison.Ordinal) ||
            !string.Equals(Sha256(anomaliesYaml), sourceBinding.AnomaliesYamlSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("C1 source binding does not identify the supplied canonical YAML inputs.");
        }

        var load = new YamlConfigurationLoader().Load(shiftYaml, anomaliesYaml);
        if (!load.IsSuccess || load.Configuration is null)
        {
            throw new InvalidDataException("C1 export is forbidden when canonical YAML validation fails.");
        }

        var artifact = ValidatedConfigurationC1Codec.Encode(load.Configuration, sourceBinding);
        return new ValidatedConfigurationC1Export(load.Configuration, artifact, ValidatedConfigurationC1DeploymentManifest.Create(sourceBinding, load.Configuration, artifact));
    }

    private static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

public sealed record ValidatedConfigurationC1Export(
    ValidatedConfiguration Configuration,
    byte[] Artifact,
    ValidatedConfigurationC1DeploymentManifest Manifest);
