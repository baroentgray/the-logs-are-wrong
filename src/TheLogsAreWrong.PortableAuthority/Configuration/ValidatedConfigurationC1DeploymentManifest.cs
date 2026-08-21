using System.Text;

namespace TheLogsAreWrong.Domain.Configuration;

/// <summary>
/// Deterministic deployment identity for a C1 artifact. It is transport metadata, not a configuration schema.
/// </summary>
public sealed record ValidatedConfigurationC1DeploymentManifest(
    ValidatedConfigurationC1SourceBinding SourceBinding,
    int ArtifactByteLength,
    string ArtifactSha256,
    string CanonicalProjectionSha256)
{
    public const string Schema = "tlaw.validated-config-c1-deployment/v1";

    public static ValidatedConfigurationC1DeploymentManifest Create(
        ValidatedConfigurationC1SourceBinding sourceBinding,
        ValidatedConfiguration configuration,
        byte[] artifact)
    {
        if (sourceBinding is null) throw new ArgumentNullException(nameof(sourceBinding));
        if (configuration is null) throw new ArgumentNullException(nameof(configuration));
        if (artifact is null) throw new ArgumentNullException(nameof(artifact));
        return new ValidatedConfigurationC1DeploymentManifest(
            sourceBinding,
            artifact.Length,
            ValidatedConfigurationC1Codec.Sha256(artifact),
            ValidatedConfigurationC1Codec.ProjectionSha256(configuration));
    }

    public string Serialize() => string.Concat(
        "schema=", Schema, "\n",
        "format=", ValidatedConfigurationC1Codec.Magic, "\n",
        "version=", ValidatedConfigurationC1Codec.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), "\n",
        "shift_yaml_sha256=", SourceBinding.ShiftYamlSha256, "\n",
        "anomalies_yaml_sha256=", SourceBinding.AnomaliesYamlSha256, "\n",
        "validator_source_blob=", SourceBinding.ValidatorSourceBlob, "\n",
        "artifact_byte_length=", ArtifactByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture), "\n",
        "artifact_sha256=", ArtifactSha256, "\n",
        "canonical_projection_sha256=", CanonicalProjectionSha256, "\n");

    public static ValidatedConfigurationC1DeploymentManifest Parse(string text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        var lines = text.Split('\n');
        if (lines.Length != 10 || lines[^1].Length != 0) throw new InvalidDataException("C1 deployment manifest framing is invalid.");
        var expected = new[]
        {
            "schema", "format", "version", "shift_yaml_sha256", "anomalies_yaml_sha256", "validator_source_blob",
            "artifact_byte_length", "artifact_sha256", "canonical_projection_sha256"
        };
        var values = new string[expected.Length];
        for (var index = 0; index < expected.Length; index++)
        {
            var prefix = expected[index] + "=";
            if (!lines[index].StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("C1 deployment manifest field order is invalid.");
            values[index] = lines[index].Substring(prefix.Length);
        }
        if (!string.Equals(values[0], Schema, StringComparison.Ordinal) || !string.Equals(values[1], ValidatedConfigurationC1Codec.Magic, StringComparison.Ordinal) || values[2] != "1")
            throw new InvalidDataException("C1 deployment manifest format is unsupported.");
        if (!int.TryParse(values[6], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var artifactLength) || artifactLength < 0)
            throw new InvalidDataException("C1 deployment manifest artifact length is invalid.");
        RequireHex(values[3], 64, "shift YAML SHA-256");
        RequireHex(values[4], 64, "anomalies YAML SHA-256");
        RequireHex(values[5], 40, "validator source blob");
        RequireHex(values[7], 64, "artifact SHA-256");
        RequireHex(values[8], 64, "canonical projection SHA-256");
        return new ValidatedConfigurationC1DeploymentManifest(new ValidatedConfigurationC1SourceBinding(values[3], values[4], values[5]), artifactLength, values[7], values[8]);
    }

    public ValidatedConfiguration VerifyAndMaterialize(byte[] artifact)
    {
        if (artifact is null) throw new ArgumentNullException(nameof(artifact));
        if (artifact.Length != ArtifactByteLength || !string.Equals(ValidatedConfigurationC1Codec.Sha256(artifact), ArtifactSha256, StringComparison.Ordinal))
            throw new InvalidDataException("C1 artifact differs from the trusted deployment identity.");
        var configuration = ValidatedConfigurationC1Codec.Decode(artifact, SourceBinding);
        if (!string.Equals(ValidatedConfigurationC1Codec.ProjectionSha256(configuration), CanonicalProjectionSha256, StringComparison.Ordinal))
            throw new InvalidDataException("C1 artifact differs from the trusted canonical projection.");
        return configuration;
    }

    private static void RequireHex(string value, int length, string name)
    {
        if (value.Length != length || value.Any(static character => !((character >= '0' && character <= '9') || (character >= 'A' && character <= 'F') || (character >= 'a' && character <= 'f'))))
            throw new InvalidDataException($"C1 deployment manifest {name} is invalid.");
    }
}
