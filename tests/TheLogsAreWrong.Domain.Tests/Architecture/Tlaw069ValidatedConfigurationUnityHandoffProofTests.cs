using System.Security.Cryptography;
using TheLogsAreWrong.Config.Yaml;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>
/// TLAW-069 proof contracts. These exercise candidate transport material only; neither the
/// artifact codec nor generated construction source is production configuration ingestion.
/// </summary>
public sealed class Tlaw069ValidatedConfigurationUnityHandoffProofTests
{
    private const string ShiftYamlSha256 = "CD08DDFC6F354A1FDDEC7EE751007C95920CDBD26AFA6350A068C350D88277E7";
    private const string AnomaliesYamlSha256 = "6517C145AD41410131FF50BF691FE9C37FB33E1CB8E065E42ADB97364F4785D7";
    private const string ValidatorSourceBlob = "23651feb72bfa432685f8ef1850648d355baed57";
    private const string CanonicalProjectionSha256 = "4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1";
    private const string C1ArtifactSha256 = "94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541";

    [Fact]
    public void C1_real_yaml_loader_regenerates_the_exact_boundary_artifact_and_materializes_the_complete_graph()
    {
        var source = LoadCanonicalYaml();
        var binding = CanonicalBinding();
        var boundaryArtifact = Tlaw069C1BoundaryArtifact.ReadExactArtifact();
        var regenerated = Tlaw069C1ArtifactCodec.Encode(source, binding);
        var materialized = Tlaw069C1ArtifactCodec.Decode(boundaryArtifact, binding);

        Assert.Equal(C1ArtifactSha256, Tlaw069C1BoundaryArtifact.Sha256(boundaryArtifact));
        Assert.Equal(regenerated, boundaryArtifact);
        Assert.Equal(CanonicalProjectionSha256, Tlaw069ConfigurationProjection.Sha256(source));
        Assert.Equal(CanonicalProjectionSha256, Tlaw069ConfigurationProjection.Sha256(materialized));
        Tlaw069C1BoundaryArtifact.VerifyTrustedIdentity(boundaryArtifact);
        Assert.Equal(boundaryArtifact, Tlaw069C1ArtifactCodec.Encode(materialized, binding));
        Assert.Equal(Tlaw069ConfigurationProjection.RequiredPortableRecordTypes, Tlaw069ConfigurationProjection.ObservedPortableRecordTypes());

        using var session = new HostSession(materialized.Shift, materialized.Anomalies, ProfileId.From("learning"));
        Assert.Equal(materialized.Shift.ShiftId, session.ShiftState.ShiftId);
    }

    [Fact]
    public void C1_corruption_truncation_version_mismatch_and_stale_binding_fail_closed()
    {
        var artifact = Tlaw069C1BoundaryArtifact.ReadExactArtifact();
        var corrupted = artifact.ToArray();
        corrupted[^1] ^= 0x01;
        var truncated = artifact[..^1];
        var wrongVersion = artifact.ToArray();
        Tlaw069C1ArtifactCodec.WriteVersionForTest(wrongVersion, 2);
        var stale = CanonicalBinding() with { ValidatorSourceBlob = "0000000000000000000000000000000000000000" };

        Assert.Throws<InvalidDataException>(() => { Tlaw069C1ArtifactCodec.Decode(corrupted, CanonicalBinding()); });
        Assert.Throws<InvalidDataException>(() => { Tlaw069C1ArtifactCodec.Decode(truncated, CanonicalBinding()); });
        Assert.Throws<InvalidDataException>(() => { Tlaw069C1ArtifactCodec.Decode(wrongVersion, CanonicalBinding()); });
        Assert.Throws<InvalidDataException>(() => { Tlaw069C1ArtifactCodec.Decode(artifact, stale); });
    }

    [Fact]
    public void C1_payload_tampering_with_a_recomputed_internal_hash_still_fails_the_trusted_source_result_identity()
    {
        var selfConsistentTampered = Tlaw069C1BoundaryArtifact.ReadExactArtifact();
        Tlaw069C1ArtifactCodec.ModifyPayloadSeedAndRecomputeHashForTest(selfConsistentTampered);

        var materialized = Tlaw069C1ArtifactCodec.Decode(selfConsistentTampered, CanonicalBinding());

        Assert.NotEqual(CanonicalProjectionSha256, Tlaw069ConfigurationProjection.Sha256(materialized));
        Assert.Throws<InvalidDataException>(() => { Tlaw069C1BoundaryArtifact.VerifyTrustedIdentity(selfConsistentTampered); });
    }

    [Fact]
    public void C2_generated_construction_source_is_a_deterministic_exact_binding_of_the_real_validated_result()
    {
        var source = LoadCanonicalYaml();
        var binding = CanonicalBinding();
        var emitted = Tlaw069C2GeneratedSourceEmitter.Emit(source, binding);
        var committed = File.ReadAllText(Tlaw069ProofPaths.GeneratedUnityFactoryPath());

        Assert.Equal(emitted, committed);
        Assert.Contains(binding.ShiftYamlSha256, committed, StringComparison.Ordinal);
        Assert.Contains(binding.AnomaliesYamlSha256, committed, StringComparison.Ordinal);
        Assert.Contains(binding.ValidatorSourceBlob, committed, StringComparison.Ordinal);
        Assert.Contains(Tlaw069ConfigurationProjection.Sha256(source), committed, StringComparison.Ordinal);
    }

    [Fact]
    public void Canonical_source_binding_is_the_real_yaml_and_real_loader_before_any_candidate_transport()
    {
        var shift = Fixture.ShiftYaml;
        var anomalies = Fixture.AnomaliesYaml;
        var result = new YamlConfigurationLoader().Load(shift, anomalies);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(ShiftYamlSha256, Sha256(shift));
        Assert.Equal(AnomaliesYamlSha256, Sha256(anomalies));
        Assert.Equal(ValidatorSourceBlob, CanonicalBinding().ValidatorSourceBlob);
        Assert.NotNull(result.Configuration);
    }

    private static ValidatedConfiguration LoadCanonicalYaml()
    {
        var result = new YamlConfigurationLoader().Load(Fixture.ShiftYaml, Fixture.AnomaliesYaml);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        return Assert.IsType<ValidatedConfiguration>(result.Configuration);
    }

    private static Tlaw069SourceBinding CanonicalBinding() => new(ShiftYamlSha256, AnomaliesYamlSha256, ValidatorSourceBlob);

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
