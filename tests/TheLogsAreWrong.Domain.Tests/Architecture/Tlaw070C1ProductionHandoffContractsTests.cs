using TheLogsAreWrong.Config.Yaml;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Runtime;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>Contracts for the owner-selected C1 production handoff; the canonical YAML loader remains outside Unity.</summary>
public sealed class Tlaw070C1ProductionHandoffContractsTests
{
    [Fact]
    public void Real_loader_export_and_the_tracked_deployment_resource_are_byte_identical()
    {
        var export = ValidatedConfigurationC1Exporter.Export(Fixture.ShiftYaml, Fixture.AnomaliesYaml, Tlaw070TrustedDeployment.Binding);
        var manifest = ValidatedConfigurationC1DeploymentManifest.Parse(Tlaw070TrustedDeployment.ReadManifest());

        Assert.Equal(2326, export.Artifact.Length);
        Assert.Equal(Tlaw070TrustedDeployment.ArtifactSha256, ValidatedConfigurationC1Codec.Sha256(export.Artifact));
        Assert.Equal(Tlaw070TrustedDeployment.ProjectionSha256, ValidatedConfigurationC1Codec.ProjectionSha256(export.Configuration));
        Assert.Equal(Tlaw070TrustedDeployment.ReadArtifact(), export.Artifact);
        Assert.Equal(Tlaw070TrustedDeployment.ReadManifest(), export.Manifest.Serialize());
        Assert.Equal(Tlaw070TrustedDeployment.Binding, manifest.SourceBinding);
        Assert.Equal(Tlaw070TrustedDeployment.ProjectionSha256, ValidatedConfigurationC1Codec.ProjectionSha256(manifest.VerifyAndMaterialize(Tlaw070TrustedDeployment.ReadArtifact())));
        Assert.Equal(Tlaw070TrustedDeployment.Binding.ValidatorSourceBlob, GitBlob("src/TheLogsAreWrong.Config.Yaml/YamlConfigurationLoader.cs"));
        Assert.Equal(Tlaw070TrustedDeployment.Binding.ShiftYamlSha256, Sha256(Fixture.ShiftYaml));
        Assert.Equal(Tlaw070TrustedDeployment.Binding.AnomaliesYamlSha256, Sha256(Fixture.AnomaliesYaml));
    }

    [Fact]
    public void Production_codec_is_deterministic_and_materializes_the_complete_portable_graph()
    {
        var first = ValidatedConfigurationC1Exporter.Export(Fixture.ShiftYaml, Fixture.AnomaliesYaml, Tlaw070TrustedDeployment.Binding);
        var second = ValidatedConfigurationC1Exporter.Export(Fixture.ShiftYaml, Fixture.AnomaliesYaml, Tlaw070TrustedDeployment.Binding);
        var materialized = ValidatedConfigurationC1Codec.Decode(first.Artifact, Tlaw070TrustedDeployment.Binding);

        Assert.Equal(first.Artifact, second.Artifact);
        Assert.Equal(24, ValidatedConfigurationC1Codec.RequiredPortableRecordTypes.Length);
        Assert.Equal(ValidatedConfigurationC1Codec.RequiredPortableRecordTypes, ValidatedConfigurationC1Codec.ObservedPortableRecordTypes());
        Assert.Equal(Tlaw070TrustedDeployment.ProjectionSha256, ValidatedConfigurationC1Codec.ProjectionSha256(materialized));
        Assert.Equal(first.Artifact, ValidatedConfigurationC1Codec.Encode(materialized, Tlaw070TrustedDeployment.Binding));

        using var session = new HostSession(materialized.Shift, materialized.Anomalies, ProfileId.From("learning"));
        Assert.Equal(materialized.Shift.ShiftId, session.ShiftState.ShiftId);
    }

    [Fact]
    public void Production_decoder_and_deployment_identity_fail_closed_for_untrusted_or_malformed_inputs()
    {
        var artifact = Tlaw070TrustedDeployment.ReadArtifact();
        var corrupt = artifact.ToArray();
        corrupt[^1] ^= 0x01;
        var truncated = artifact[..^1];
        var version = artifact.ToArray();
        BitConverter.GetBytes(2).CopyTo(version, 4 + Encoding.UTF8.GetByteCount(ValidatedConfigurationC1Codec.Magic));
        var trailing = artifact.Concat(new byte[] { 0x01 }).ToArray();
        var invalidLength = artifact.ToArray();
        BitConverter.GetBytes(1_000_001).CopyTo(invalidLength, PayloadLengthOffset(invalidLength));
        var invalidCount = artifact.ToArray();
        BitConverter.GetBytes(-1).CopyTo(invalidCount, ProfilesCountOffset(invalidCount));
        var stale = Tlaw070TrustedDeployment.Binding with { ValidatorSourceBlob = new string('0', 40) };

        Assert.Throws<InvalidDataException>(() => ValidatedConfigurationC1Codec.Decode(corrupt, Tlaw070TrustedDeployment.Binding));
        Assert.Throws<InvalidDataException>(() => ValidatedConfigurationC1Codec.Decode(truncated, Tlaw070TrustedDeployment.Binding));
        Assert.Throws<InvalidDataException>(() => ValidatedConfigurationC1Codec.Decode(version, Tlaw070TrustedDeployment.Binding));
        Assert.Throws<InvalidDataException>(() => ValidatedConfigurationC1Codec.Decode(trailing, Tlaw070TrustedDeployment.Binding));
        Assert.Throws<InvalidDataException>(() => ValidatedConfigurationC1Codec.Decode(invalidLength, Tlaw070TrustedDeployment.Binding));
        Assert.Throws<InvalidDataException>(() => ValidatedConfigurationC1Codec.Decode(invalidCount, Tlaw070TrustedDeployment.Binding));
        Assert.Throws<InvalidDataException>(() => ValidatedConfigurationC1Codec.Decode(artifact, stale));
        Assert.Throws<InvalidDataException>(() => ValidatedConfigurationC1DeploymentManifest.Parse("schema=not-c1\n"));
    }

    [Fact]
    public void Recomputed_internal_payload_hash_does_not_supersede_the_trusted_deployment_identity()
    {
        var tampered = Tlaw070TrustedDeployment.ReadArtifact();
        var payloadLengthOffset = PayloadLengthOffset(tampered);
        var payloadOffset = payloadLengthOffset + sizeof(int);
        var payloadLength = BitConverter.ToInt32(tampered, payloadLengthOffset);
        var shiftIdLength = BitConverter.ToInt32(tampered, payloadOffset);
        var seedOffset = payloadOffset + sizeof(int) + shiftIdLength;
        BitConverter.GetBytes(BitConverter.ToInt32(tampered, seedOffset) ^ 0x01).CopyTo(tampered, seedOffset);
        SHA256.HashData(tampered.AsSpan(payloadOffset, payloadLength)).CopyTo(tampered.AsSpan(payloadOffset + payloadLength, 32));

        var materialized = ValidatedConfigurationC1Codec.Decode(tampered, Tlaw070TrustedDeployment.Binding);
        Assert.NotEqual(Tlaw070TrustedDeployment.ProjectionSha256, ValidatedConfigurationC1Codec.ProjectionSha256(materialized));
        Assert.Throws<InvalidDataException>(() => ValidatedConfigurationC1DeploymentManifest.Parse(Tlaw070TrustedDeployment.ReadManifest()).VerifyAndMaterialize(tampered));
    }

    [Fact]
    public void Export_is_refused_when_the_real_yaml_loader_reports_validation_errors()
    {
        var invalidShift = Fixture.ShiftYaml.Replace("seed: 47001", "seed: forty", StringComparison.Ordinal);
        var invalidBinding = Tlaw070TrustedDeployment.Binding with { ShiftYamlSha256 = Sha256(invalidShift) };
        var load = new YamlConfigurationLoader().Load(invalidShift, Fixture.AnomaliesYaml);

        Assert.Equal(Sha256(invalidShift), invalidBinding.ShiftYamlSha256);
        Assert.Equal(Tlaw070TrustedDeployment.Binding.AnomaliesYamlSha256, invalidBinding.AnomaliesYamlSha256);
        Assert.Equal(GitBlob("src/TheLogsAreWrong.Config.Yaml/YamlConfigurationLoader.cs"), invalidBinding.ValidatorSourceBlob);
        Assert.False(load.IsSuccess, string.Join(Environment.NewLine, load.Diagnostics));
        Assert.Null(load.Configuration);
        Assert.Contains(load.Diagnostics, diagnostic => diagnostic.Code == "TLAW-CFG-102");
        Assert.Throws<InvalidDataException>(() => ValidatedConfigurationC1Exporter.Export(invalidShift, Fixture.AnomaliesYaml, invalidBinding));
    }

    private static int PayloadLengthOffset(byte[] artifact)
    {
        var offset = sizeof(int) + Encoding.UTF8.GetByteCount(ValidatedConfigurationC1Codec.Magic) + sizeof(int);
        for (var field = 0; field < 3; field++) offset += sizeof(int) + BitConverter.ToInt32(artifact, offset);
        return offset;
    }

    private static int ProfilesCountOffset(byte[] artifact)
    {
        var payloadOffset = PayloadLengthOffset(artifact) + sizeof(int);
        return payloadOffset + sizeof(int) + BitConverter.ToInt32(artifact, payloadOffset) + sizeof(int);
    }

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string GitBlob(string path)
    {
        var start = new ProcessStartInfo("git", "rev-parse HEAD:" + path) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Git could not be started.");
        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output;
    }
}
