using System;
using System.IO;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Gate2.Tests
{
    /// <summary>Unity consumes tracked C1 bytes through the PortableAuthority production materializer only.</summary>
    public sealed class Tlaw070C1ProductionHandoffTests
    {
        private const string ArtifactSha256 = "94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541";
        private const string ProjectionSha256 = "4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1";
        private const string ShiftYamlSha256 = "CD08DDFC6F354A1FDDEC7EE751007C95920CDBD26AFA6350A068C350D88277E7";
        private const string AnomaliesYamlSha256 = "6517C145AD41410131FF50BF691FE9C37FB33E1CB8E065E42ADB97364F4785D7";
        private const string ValidatorSourceBlob = "23651feb72bfa432685f8ef1850648d355baed57";

        [Test]
        public void Exact_tracked_production_artifact_is_trusted_materialized_and_accepted_by_HostSession_in_test_evidence()
        {
            var artifact = ReadArtifact();
            var manifest = ValidatedConfigurationC1DeploymentManifest.Parse(ReadManifest());

            Assert.AreEqual(2326, artifact.Length);
            Assert.AreEqual(ArtifactSha256, ValidatedConfigurationC1Codec.Sha256(artifact));
            Assert.AreEqual(ShiftYamlSha256, manifest.SourceBinding.ShiftYamlSha256);
            Assert.AreEqual(AnomaliesYamlSha256, manifest.SourceBinding.AnomaliesYamlSha256);
            Assert.AreEqual(ValidatorSourceBlob, manifest.SourceBinding.ValidatorSourceBlob);
            Assert.AreEqual(ArtifactSha256, manifest.ArtifactSha256);
            Assert.AreEqual(ProjectionSha256, manifest.CanonicalProjectionSha256);

            var configuration = manifest.VerifyAndMaterialize(artifact);
            Assert.AreEqual(ProjectionSha256, ValidatedConfigurationC1Codec.ProjectionSha256(configuration));
            var required = ValidatedConfigurationC1Codec.RequiredPortableRecordTypes;
            var observed = ValidatedConfigurationC1Codec.ObservedPortableRecordTypes();
            Assert.AreEqual(required.Length, observed.Length);
            for (var index = 0; index < required.Length; index++) Assert.AreEqual(required[index], observed[index]);

            using (var session = new HostSession(configuration.Shift, configuration.Anomalies, ProfileId.From("learning")))
            {
                Assert.AreEqual(configuration.Shift.ShiftId, session.ShiftState.ShiftId);
            }
        }

        [Test]
        public void Trusted_deployment_identity_rejects_a_modified_exact_artifact()
        {
            var artifact = ReadArtifact();
            artifact[artifact.Length - 1] ^= 0x01;
            var manifest = ValidatedConfigurationC1DeploymentManifest.Parse(ReadManifest());

            Assert.Throws<InvalidDataException>(() => manifest.VerifyAndMaterialize(artifact));
        }

        private static byte[] ReadArtifact()
        {
            return Convert.FromBase64String(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Gate2", "Configuration", "validated-configuration-c1-v1.base64")).Trim());
        }

        private static string ReadManifest()
        {
            return File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Gate2", "Configuration", "validated-configuration-c1-v1.manifest"));
        }
    }
}
