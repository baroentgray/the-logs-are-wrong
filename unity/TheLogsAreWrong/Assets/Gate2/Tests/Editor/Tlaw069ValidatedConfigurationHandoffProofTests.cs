using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Gate2.Tests
{
    /// <summary>
    /// TLAW-069 Editor-only materialization proof. The code below is a candidate C1 reference codec,
    /// not a production ingestion path; it does not read YAML or impose configuration semantics.
    /// </summary>
    public sealed class Tlaw069ValidatedConfigurationHandoffProofTests
    {
        private const string ProjectionSha = "4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1";

        [Test]
        public void C2_generated_construction_materializes_the_complete_validated_graph_and_passes_it_directly_to_host_session()
        {
            var configuration = Tlaw069GeneratedValidatedConfiguration.Create();

            Assert.AreEqual("C2", Tlaw069GeneratedValidatedConfiguration.CandidateId);
            Assert.AreEqual(ProjectionSha, Tlaw069GeneratedValidatedConfiguration.CanonicalProjectionSha256);
            Assert.AreEqual(ProjectionSha, Tlaw069UnityConfigurationProjection.Sha256(configuration));

            using (var session = new HostSession(configuration.Shift, configuration.Anomalies, ProfileId.From("learning")))
            {
                Assert.AreEqual(configuration.Shift.ShiftId, session.ShiftState.ShiftId);
            }
        }

        [Test]
        public void C1_artifact_materializes_without_yaml_domain_or_fourth_plugin_and_matches_net10_projection()
        {
            var source = Tlaw069GeneratedValidatedConfiguration.Create();
            var binding = Tlaw069UnitySourceBinding.FromGenerated();
            var first = Tlaw069UnityC1ArtifactCodec.Encode(source, binding);
            var second = Tlaw069UnityC1ArtifactCodec.Encode(source, binding);
            var materialized = Tlaw069UnityC1ArtifactCodec.Decode(first, binding);

            CollectionAssert.AreEqual(first, second);
            Assert.AreEqual(ProjectionSha, Tlaw069UnityConfigurationProjection.Sha256(materialized));
            CollectionAssert.AreEqual(first, Tlaw069UnityC1ArtifactCodec.Encode(materialized, binding));

            using (var session = new HostSession(materialized.Shift, materialized.Anomalies, ProfileId.From("learning")))
            {
                Assert.AreEqual(materialized.Shift.ShiftId, session.ShiftState.ShiftId);
            }
        }

        [Test]
        public void C1_corrupt_truncated_wrong_version_and_stale_artifacts_fail_closed()
        {
            var binding = Tlaw069UnitySourceBinding.FromGenerated();
            var artifact = Tlaw069UnityC1ArtifactCodec.Encode(Tlaw069GeneratedValidatedConfiguration.Create(), binding);
            var corrupt = (byte[])artifact.Clone();
            corrupt[corrupt.Length - 1] ^= 0x01;
            var truncated = artifact.Take(artifact.Length - 1).ToArray();
            var wrongVersion = (byte[])artifact.Clone();
            Tlaw069UnityC1ArtifactCodec.WriteVersionForTest(wrongVersion, 2);
            var stale = new Tlaw069UnitySourceBinding(binding.ShiftYamlSha256, binding.AnomaliesYamlSha256, "0000000000000000000000000000000000000000");

            Assert.Throws<InvalidDataException>(() => Tlaw069UnityC1ArtifactCodec.Decode(corrupt, binding));
            Assert.Throws<InvalidDataException>(() => Tlaw069UnityC1ArtifactCodec.Decode(truncated, binding));
            Assert.Throws<InvalidDataException>(() => Tlaw069UnityC1ArtifactCodec.Decode(wrongVersion, binding));
            Assert.Throws<InvalidDataException>(() => Tlaw069UnityC1ArtifactCodec.Decode(artifact, stale));
        }

        [Test]
        public void Unity_test_assembly_references_only_the_existing_authorized_portable_plugin_set()
        {
            var names = typeof(Tlaw069ValidatedConfigurationHandoffProofTests).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            CollectionAssert.Contains(names, "TheLogsAreWrong.PortableAuthority");
            CollectionAssert.DoesNotContain(names, "TheLogsAreWrong.Domain");
            CollectionAssert.DoesNotContain(names, "TheLogsAreWrong.Config.Yaml");
            CollectionAssert.DoesNotContain(names, "YamlDotNet");
        }
    }

    internal sealed class Tlaw069UnitySourceBinding : IEquatable<Tlaw069UnitySourceBinding>
    {
        internal Tlaw069UnitySourceBinding(string shiftYamlSha256, string anomaliesYamlSha256, string validatorSourceBlob)
        {
            ShiftYamlSha256 = shiftYamlSha256;
            AnomaliesYamlSha256 = anomaliesYamlSha256;
            ValidatorSourceBlob = validatorSourceBlob;
        }

        internal string ShiftYamlSha256 { get; private set; }
        internal string AnomaliesYamlSha256 { get; private set; }
        internal string ValidatorSourceBlob { get; private set; }

        internal static Tlaw069UnitySourceBinding FromGenerated()
        {
            return new Tlaw069UnitySourceBinding(
                Tlaw069GeneratedValidatedConfiguration.ShiftYamlSha256,
                Tlaw069GeneratedValidatedConfiguration.AnomaliesYamlSha256,
                Tlaw069GeneratedValidatedConfiguration.ValidatorSourceBlob);
        }

        public bool Equals(Tlaw069UnitySourceBinding other)
        {
            return other != null && ShiftYamlSha256 == other.ShiftYamlSha256 && AnomaliesYamlSha256 == other.AnomaliesYamlSha256 && ValidatorSourceBlob == other.ValidatorSourceBlob;
        }

        public override bool Equals(object obj) { return Equals(obj as Tlaw069UnitySourceBinding); }
        public override int GetHashCode() { return (ShiftYamlSha256 + "|" + AnomaliesYamlSha256 + "|" + ValidatorSourceBlob).GetHashCode(); }
    }

    internal static class Tlaw069UnityC1ArtifactCodec
    {
        private const string Magic = "TLAW-CFG-U4-C1";
        private const int Version = 1;

        internal static byte[] Encode(ValidatedConfiguration configuration, Tlaw069UnitySourceBinding binding)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            if (binding == null) throw new ArgumentNullException("binding");
            var payload = Tlaw069UnityConfigurationProjection.Bytes(configuration);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                WriteString(writer, Magic);
                writer.Write(Version);
                WriteString(writer, binding.ShiftYamlSha256);
                WriteString(writer, binding.AnomaliesYamlSha256);
                WriteString(writer, binding.ValidatorSourceBlob);
                writer.Write(payload.Length);
                writer.Write(payload);
                writer.Write(Hash(payload));
                writer.Flush();
                return stream.ToArray();
            }
        }

        internal static ValidatedConfiguration Decode(byte[] artifact, Tlaw069UnitySourceBinding expectedBinding)
        {
            if (artifact == null) throw new ArgumentNullException("artifact");
            if (expectedBinding == null) throw new ArgumentNullException("expectedBinding");
            try
            {
                using (var stream = new MemoryStream(artifact, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (ReadString(reader) != Magic) throw new InvalidDataException("C1 artifact magic is invalid.");
                    if (reader.ReadInt32() != Version) throw new InvalidDataException("C1 artifact version is unsupported.");
                    var actual = new Tlaw069UnitySourceBinding(ReadString(reader), ReadString(reader), ReadString(reader));
                    if (!actual.Equals(expectedBinding)) throw new InvalidDataException("C1 artifact source binding is stale or unexpected.");
                    var length = ReadLength(reader, "payload");
                    var payload = ReadExact(reader, length, "payload");
                    var hash = ReadExact(reader, 32, "payload hash");
                    if (stream.Position != stream.Length) throw new InvalidDataException("C1 artifact has trailing data.");
                    if (!Hash(payload).SequenceEqual(hash)) throw new InvalidDataException("C1 artifact payload integrity check failed.");
                    var result = Tlaw069UnityConfigurationProjection.Read(payload);
                    if (!Tlaw069UnityConfigurationProjection.Bytes(result).SequenceEqual(payload)) throw new InvalidDataException("C1 materialized projection does not match the artifact payload.");
                    return result;
                }
            }
            catch (InvalidDataException) { throw; }
            catch (Exception exception) when (exception is EndOfStreamException || exception is IOException || exception is ArgumentException || exception is OverflowException || exception is DecoderFallbackException)
            {
                throw new InvalidDataException("C1 artifact could not be materialized.", exception);
            }
        }

        internal static void WriteVersionForTest(byte[] artifact, int version)
        {
            if (artifact == null) throw new ArgumentNullException("artifact");
            var offset = 1 + Encoding.UTF8.GetByteCount(Magic);
            if (artifact.Length < offset + 4) throw new ArgumentException("Artifact is too short to contain a version.", "artifact");
            BitConverter.GetBytes(version).CopyTo(artifact, offset);
        }

        internal static void WriteString(BinaryWriter writer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        internal static string ReadString(BinaryReader reader) { return Encoding.UTF8.GetString(ReadExact(reader, ReadLength(reader, "string"), "string")); }
        internal static int ReadCount(BinaryReader reader, string name)
        {
            var value = reader.ReadInt32();
            if (value < 0 || value > 100000) throw new InvalidDataException("C1 " + name + " count is invalid.");
            return value;
        }
        private static int ReadLength(BinaryReader reader, string name)
        {
            var value = reader.ReadInt32();
            if (value < 0 || value > 1000000) throw new InvalidDataException("C1 " + name + " length is invalid.");
            return value;
        }
        private static byte[] ReadExact(BinaryReader reader, int length, string name)
        {
            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new InvalidDataException("C1 " + name + " is truncated.");
            return bytes;
        }
        private static byte[] Hash(byte[] bytes) { using (var algorithm = SHA256.Create()) return algorithm.ComputeHash(bytes); }
    }

    internal static class Tlaw069UnityConfigurationProjection
    {
        internal static string Sha256(ValidatedConfiguration configuration)
        {
            using (var algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(Bytes(configuration))).Replace("-", string.Empty);
        }

        internal static byte[] Bytes(ValidatedConfiguration configuration)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                WriteConfiguration(writer, configuration);
                writer.Flush();
                return stream.ToArray();
            }
        }

        internal static ValidatedConfiguration Read(byte[] payload)
        {
            try
            {
                using (var stream = new MemoryStream(payload, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    var value = ReadConfiguration(reader);
                    if (stream.Position != stream.Length) throw new InvalidDataException("C1 configuration projection has trailing data.");
                    return value;
                }
            }
            catch (InvalidDataException) { throw; }
            catch (Exception exception) when (exception is EndOfStreamException || exception is IOException || exception is ArgumentException || exception is OverflowException || exception is DecoderFallbackException)
            {
                throw new InvalidDataException("C1 configuration projection is malformed.", exception);
            }
        }

        private static void WriteConfiguration(BinaryWriter writer, ValidatedConfiguration value) { WriteShift(writer, value.Shift); WriteAnomalyCatalog(writer, value.Anomalies); }
        private static ValidatedConfiguration ReadConfiguration(BinaryReader reader) { return new ValidatedConfiguration(ReadShift(reader), ReadAnomalyCatalog(reader)); }
        private static void WriteShift(BinaryWriter writer, ShiftConfiguration value)
        {
            W(writer, value.ShiftId); writer.Write(value.Seed.Value); Dict(writer, value.Profiles, delegate(BinaryWriter w, ProfileId k) { W(w, k); }, WriteShiftProfile);
            WriteObjectives(writer, value.Objectives); WriteSupply(writer, value.Supply); WriteScheduler(writer, value.Scheduler); WriteLineNoise(writer, value.LineNoise); WriteResources(writer, value.Resources); WriteContainment(writer, value.Containment);
            Array(writer, value.SuccessPredicate, delegate(BinaryWriter w, string v) { Tlaw069UnityC1ArtifactCodec.WriteString(w, v); }); Array(writer, value.Manifest, WriteManifest);
        }
        private static ShiftConfiguration ReadShift(BinaryReader r) { return new ShiftConfiguration(RShiftId(r), new ShiftSeed(r.ReadInt32()), Dict(r, RProfileId, ReadShiftProfile), ReadObjectives(r), ReadSupply(r), ReadScheduler(r), ReadLineNoise(r), ReadResources(r), ReadContainment(r), Array(r, Tlaw069UnityC1ArtifactCodec.ReadString), Array(r, ReadManifest)); }
        private static void WriteShiftProfile(BinaryWriter w, ShiftProfile v) { w.Write(v.IntakeTimeoutSeconds); w.Write(v.HardShiftDeadlineSeconds); }
        private static ShiftProfile ReadShiftProfile(BinaryReader r) { return new ShiftProfile(r.ReadInt32(), r.ReadInt32()); }
        private static void WriteObjectives(BinaryWriter w, ObjectivesDefinition v) { WriteQuota(w, v.Quota); w.Write(v.MinCorrectlyProcessedAnomalies); }
        private static ObjectivesDefinition ReadObjectives(BinaryReader r) { return new ObjectivesDefinition(ReadQuota(r), r.ReadInt32()); }
        private static void WriteQuota(BinaryWriter w, QuotaDefinition v) { w.Write(v.Total); Dict(w, v.BySpecies, delegate(BinaryWriter x, SpeciesId k) { W(x, k); }, delegate(BinaryWriter x, int n) { x.Write(n); }); }
        private static QuotaDefinition ReadQuota(BinaryReader r) { return new QuotaDefinition(r.ReadInt32(), Dict(r, RSpeciesId, delegate(BinaryReader x) { return x.ReadInt32(); })); }
        private static void WriteSupply(BinaryWriter w, SupplyDefinition v) { w.Write(v.Total); w.Write(v.FreeWriteoffBuffer); }
        private static SupplyDefinition ReadSupply(BinaryReader r) { return new SupplyDefinition(r.ReadInt32(), r.ReadInt32()); }
        private static void WriteManifest(BinaryWriter w, ManifestLogDefinition v) { W(w, v.Id); W(w, v.TrueSpecies); W(w, v.DeclaredSpecies); WNullable(w, v.Anomaly); }
        private static ManifestLogDefinition ReadManifest(BinaryReader r) { return new ManifestLogDefinition(RLogId(r), RSpeciesId(r), RSpeciesId(r), RNullableAnomalyId(r)); }
        private static void WriteScheduler(BinaryWriter w, SchedulerConfiguration v)
        {
            Dict(w, v.Capacities, delegate(BinaryWriter x, NodeId k) { E(x, k); }, WriteCapacity); w.Write(v.InitialAdmissionDelaySeconds); w.Write(v.NormalFeedDelaySeconds); w.Write(v.EarlyFeedDelaySeconds); w.Write(v.SawCycleSeconds); w.Write(v.RepairHoldSeconds); w.Write(v.MovementNoiseSeconds); Tlaw069UnityC1ArtifactCodec.WriteString(w, v.DefaultTimeoutRoute); Array(w, v.SameTickOrder, delegate(BinaryWriter x, HostTickStage item) { E(x, item); });
        }
        private static SchedulerConfiguration ReadScheduler(BinaryReader r) { return new SchedulerConfiguration(Dict(r, delegate(BinaryReader x) { return REnum<NodeId>(x); }, ReadCapacity), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), Tlaw069UnityC1ArtifactCodec.ReadString(r), Array(r, delegate(BinaryReader x) { return REnum<HostTickStage>(x); })); }
        private static void WriteCapacity(BinaryWriter w, NodeCapacity v) { w.Write(v.IsUnlimited); if (!v.IsUnlimited) w.Write(v.Limit.Value); }
        private static NodeCapacity ReadCapacity(BinaryReader r) { return r.ReadBoolean() ? NodeCapacity.Unlimited : NodeCapacity.Limited(r.ReadInt32()); }
        private static void WriteLineNoise(BinaryWriter w, LineNoiseConfiguration v) { Array(w, v.QuietWhenAllInactive, delegate(BinaryWriter x, string item) { Tlaw069UnityC1ArtifactCodec.WriteString(x, item); }); w.Write(v.PenitentConfirmRequiresContinuousQuietSeconds); w.Write(v.ResetTestProgressWhenLoud); w.Write(v.PauseIntakeTimerDuringTest); }
        private static LineNoiseConfiguration ReadLineNoise(BinaryReader r) { return new LineNoiseConfiguration(Array(r, Tlaw069UnityC1ArtifactCodec.ReadString), r.ReadInt32(), r.ReadBoolean(), r.ReadBoolean()); }
        private static void WriteResources(BinaryWriter w, ResourcesConfiguration v) { Dict(w, v.Consumable, delegate(BinaryWriter x, ItemId k) { W(x, k); }, delegate(BinaryWriter x, int n) { x.Write(n); }); Set(w, v.Reusable, delegate(BinaryWriter x, ItemId item) { W(x, item); }); }
        private static ResourcesConfiguration ReadResources(BinaryReader r) { return new ResourcesConfiguration(Dict(r, RItemId, delegate(BinaryReader x) { return x.ReadInt32(); }), Set(r, RItemId)); }
        private static void WriteContainment(BinaryWriter w, ContainmentConfiguration v) { w.Write(v.UnlimitedCapacity); w.Write(v.RitualHoldSeconds); w.Write(v.ServiceRequestedGraceSeconds); w.Write(v.OverdueSeconds); Dict(w, v.IntervalByDangerWeight, delegate(BinaryWriter x, string k) { Tlaw069UnityC1ArtifactCodec.WriteString(x, k); }, delegate(BinaryWriter x, int n) { x.Write(n); }); WriteAfterRitual(w, v.AfterSuccessfulRitual); WriteIncident(w, v.PrototypeIncident); WritePostFacto(w, v.PostFactoIntervalInference); }
        private static ContainmentConfiguration ReadContainment(BinaryReader r) { return new ContainmentConfiguration(r.ReadBoolean(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), Dict(r, Tlaw069UnityC1ArtifactCodec.ReadString, delegate(BinaryReader x) { return x.ReadInt32(); }), ReadAfterRitual(r), ReadIncident(r), ReadPostFacto(r)); }
        private static void WriteAfterRitual(BinaryWriter w, AfterSuccessfulRitualDefinition v) { E(w, v.ReturnState); w.Write(v.RetainLogs); w.Write(v.RetainDangerWeight); w.Write(v.RestartIntervalFromCurrentWeight); }
        private static AfterSuccessfulRitualDefinition ReadAfterRitual(BinaryReader r) { return new AfterSuccessfulRitualDefinition(REnum<ContainmentState>(r), r.ReadBoolean(), r.ReadBoolean(), r.ReadBoolean()); }
        private static void WriteIncident(BinaryWriter w, PrototypeIncidentDefinition v) { Tlaw069UnityC1ArtifactCodec.WriteString(w, v.Type); w.Write(v.DurationSeconds); w.Write(v.RemainsIncidentUntilRitual); w.Write(v.RepeatBeforeResolution); }
        private static PrototypeIncidentDefinition ReadIncident(BinaryReader r) { return new PrototypeIncidentDefinition(Tlaw069UnityC1ArtifactCodec.ReadString(r), r.ReadInt32(), r.ReadBoolean(), r.ReadBoolean()); }
        private static void WritePostFacto(BinaryWriter w, PostFactoIntervalInferenceDefinition v) { w.Write(v.Allowed); Tlaw069UnityC1ArtifactCodec.WriteString(w, v.Rationale); w.Write(v.SeededJitter); }
        private static PostFactoIntervalInferenceDefinition ReadPostFacto(BinaryReader r) { return new PostFactoIntervalInferenceDefinition(r.ReadBoolean(), Tlaw069UnityC1ArtifactCodec.ReadString(r), r.ReadBoolean()); }
        private static void WriteAnomalyCatalog(BinaryWriter w, AnomalyCatalog v) { Dict(w, v.Definitions, delegate(BinaryWriter x, AnomalyId k) { W(x, k); }, WriteAnomaly); }
        private static AnomalyCatalog ReadAnomalyCatalog(BinaryReader r) { return new AnomalyCatalog(Dict(r, RAnomalyId, ReadAnomaly)); }
        private static void WriteAnomaly(BinaryWriter w, AnomalyDefinition v)
        {
            W(w, v.Id); w.Write(v.DangerWeight); Array(w, v.InstantClues, delegate(BinaryWriter x, string item) { Tlaw069UnityC1ArtifactCodec.WriteString(x, item); }); Array(w, v.ObservedClues, delegate(BinaryWriter x, string item) { Tlaw069UnityC1ArtifactCodec.WriteString(x, item); }); WriteConfirm(w, v.ConfirmTest); WriteProcessing(w, v.Processing); WriteProcedure(w, v.Procedure); Dict(w, v.WrongActions, delegate(BinaryWriter x, ItemId k) { W(x, k); }, WriteWrongAction);
        }
        private static AnomalyDefinition ReadAnomaly(BinaryReader r) { return new AnomalyDefinition(RAnomalyId(r), r.ReadInt32(), Array(r, Tlaw069UnityC1ArtifactCodec.ReadString), Array(r, Tlaw069UnityC1ArtifactCodec.ReadString), ReadConfirm(r), ReadProcessing(r), ReadProcedure(r), Dict(r, RItemId, ReadWrongAction)); }
        private static void WriteConfirm(BinaryWriter w, ConfirmTestDefinition v) { WNullableEnum(w, v.RequiredLineNoise); w.Write(v.DurationSeconds); w.Write(v.Continuous); WNullableBool(w, v.ResetWhenConditionLost); Tlaw069UnityC1ArtifactCodec.WriteString(w, v.Result); Array(w, v.Tools, delegate(BinaryWriter x, ItemId item) { W(x, item); }); }
        private static ConfirmTestDefinition ReadConfirm(BinaryReader r) { return new ConfirmTestDefinition(RNullableEnum<LineNoise>(r), r.ReadInt32(), r.ReadBoolean(), RNullableBool(r), Tlaw069UnityC1ArtifactCodec.ReadString(r), Array(r, RItemId)); }
        private static void WriteProcessing(BinaryWriter w, ProcessingDefinition v) { Set(w, v.RequiredFlags, delegate(BinaryWriter x, FlagId item) { W(x, item); }); E(w, v.RouteWithoutFlags); WriteOutcome(w, v.OnCorrect); WriteOutcome(w, v.OnIncorrect); }
        private static ProcessingDefinition ReadProcessing(BinaryReader r) { return new ProcessingDefinition(Set(r, RFlagId), REnum<RouteWithoutFlagsPolicy>(r), ReadOutcome(r), ReadOutcome(r)); }
        private static void WriteOutcome(BinaryWriter w, ProcessingOutcome v) { E(w, v.TerminalState); WriteCredit(w, v.QuotaCredit); w.Write(v.CorrectAnomalyDelta); Array(w, v.Effects, WriteEffect); }
        private static ProcessingOutcome ReadOutcome(BinaryReader r) { return new ProcessingOutcome(REnum<LogState>(r), ReadCredit(r), r.ReadInt32(), Array(r, ReadEffect)); }
        private static void WriteCredit(BinaryWriter w, QuotaCreditDefinition v) { E(w, v.Species); w.Write(v.Units); }
        private static QuotaCreditDefinition ReadCredit(BinaryReader r) { return new QuotaCreditDefinition(REnum<SpeciesCreditRule>(r), r.ReadInt32()); }
        private static void WriteEffect(BinaryWriter w, EffectDefinition v) { E(w, v.Type); W(w, v.Event); WNullableInt(w, v.DurationSeconds); WNullableString(w, v.Target); }
        private static EffectDefinition ReadEffect(BinaryReader r) { return new EffectDefinition(REnum<EffectType>(r), REffectEventId(r), RNullableInt(r), RNullableString(r)); }
        private static void WriteProcedure(BinaryWriter w, ProcedureDefinition v) { Array(w, v.Steps, WriteStep); Set(w, v.GrantsFlags, delegate(BinaryWriter x, FlagId item) { W(x, item); }); }
        private static ProcedureDefinition ReadProcedure(BinaryReader r) { return new ProcedureDefinition(Array(r, ReadStep), Set(r, RFlagId)); }
        private static void WriteStep(BinaryWriter w, ProcedureStepDefinition v) { W(w, v.Item); w.Write(v.Consumes); WNullableInt(w, v.HoldSeconds); }
        private static ProcedureStepDefinition ReadStep(BinaryReader r) { return new ProcedureStepDefinition(RItemId(r), r.ReadBoolean(), RNullableInt(r)); }
        private static void WriteWrongAction(BinaryWriter w, WrongActionDefinition v) { w.Write(v.LeavesStateUnchanged); WNullableEnum(w, v.TerminalState); w.Write(v.Consumes); Array(w, v.Effects, WriteEffect); }
        private static WrongActionDefinition ReadWrongAction(BinaryReader r) { return new WrongActionDefinition(r.ReadBoolean(), RNullableEnum<LogState>(r), r.ReadBoolean(), Array(r, ReadEffect)); }
        private static void WNullableString(BinaryWriter w, string value) { w.Write(value != null); if (value != null) Tlaw069UnityC1ArtifactCodec.WriteString(w, value); }
        private static string RNullableString(BinaryReader r) { return r.ReadBoolean() ? Tlaw069UnityC1ArtifactCodec.ReadString(r) : null; }
        private static void WNullableInt(BinaryWriter w, int? value) { w.Write(value.HasValue); if (value.HasValue) w.Write(value.Value); }
        private static int? RNullableInt(BinaryReader r) { return r.ReadBoolean() ? r.ReadInt32() : (int?)null; }
        private static void WNullableBool(BinaryWriter w, bool? value) { w.Write(value.HasValue); if (value.HasValue) w.Write(value.Value); }
        private static bool? RNullableBool(BinaryReader r) { return r.ReadBoolean() ? r.ReadBoolean() : (bool?)null; }
        private static void WNullableEnum<T>(BinaryWriter w, T? value) where T : struct { w.Write(value.HasValue); if (value.HasValue) E(w, value.Value); }
        private static T? RNullableEnum<T>(BinaryReader r) where T : struct { return r.ReadBoolean() ? REnum<T>(r) : (T?)null; }
        private static void E<T>(BinaryWriter w, T value) where T : struct { w.Write(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture)); }
        private static T REnum<T>(BinaryReader r) where T : struct { var raw = r.ReadInt32(); if (!Enum.IsDefined(typeof(T), raw)) throw new InvalidDataException("C1 enum " + typeof(T).Name + " is invalid."); return (T)Enum.ToObject(typeof(T), raw); }
        private static void Array<T>(BinaryWriter w, ImmutableArray<T> values, Action<BinaryWriter, T> write) { w.Write(values.Length); foreach (var value in values) write(w, value); }
        private static ImmutableArray<T> Array<T>(BinaryReader r, Func<BinaryReader, T> read) { var count = Tlaw069UnityC1ArtifactCodec.ReadCount(r, "array"); var builder = ImmutableArray.CreateBuilder<T>(count); for (var index = 0; index < count; index++) builder.Add(read(r)); return builder.MoveToImmutable(); }
        private static void Set<T>(BinaryWriter w, ImmutableHashSet<T> values, Action<BinaryWriter, T> write) where T : notnull { var ordered = values.OrderBy(value => value.ToString(), StringComparer.Ordinal).ToArray(); w.Write(ordered.Length); foreach (var value in ordered) write(w, value); }
        private static ImmutableHashSet<T> Set<T>(BinaryReader r, Func<BinaryReader, T> read) where T : notnull { var count = Tlaw069UnityC1ArtifactCodec.ReadCount(r, "set"); var builder = ImmutableHashSet.CreateBuilder<T>(); for (var index = 0; index < count; index++) if (!builder.Add(read(r))) throw new InvalidDataException("C1 set contains a duplicate value."); return builder.ToImmutable(); }
        private static void Dict<TKey, TValue>(BinaryWriter w, ImmutableDictionary<TKey, TValue> values, Action<BinaryWriter, TKey> writeKey, Action<BinaryWriter, TValue> writeValue) where TKey : notnull { var ordered = values.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal).ToArray(); w.Write(ordered.Length); foreach (var pair in ordered) { writeKey(w, pair.Key); writeValue(w, pair.Value); } }
        private static ImmutableDictionary<TKey, TValue> Dict<TKey, TValue>(BinaryReader r, Func<BinaryReader, TKey> readKey, Func<BinaryReader, TValue> readValue) where TKey : notnull { var count = Tlaw069UnityC1ArtifactCodec.ReadCount(r, "dictionary"); var builder = ImmutableDictionary.CreateBuilder<TKey, TValue>(); for (var index = 0; index < count; index++) if (!builder.TryAdd(readKey(r), readValue(r))) throw new InvalidDataException("C1 dictionary contains a duplicate key."); return builder.ToImmutable(); }
        private static void W(BinaryWriter w, ShiftId v) { Tlaw069UnityC1ArtifactCodec.WriteString(w, v.ToString()); }
        private static void W(BinaryWriter w, LogId v) { Tlaw069UnityC1ArtifactCodec.WriteString(w, v.ToString()); }
        private static void W(BinaryWriter w, SpeciesId v) { Tlaw069UnityC1ArtifactCodec.WriteString(w, v.ToString()); }
        private static void W(BinaryWriter w, AnomalyId v) { Tlaw069UnityC1ArtifactCodec.WriteString(w, v.ToString()); }
        private static void W(BinaryWriter w, FlagId v) { Tlaw069UnityC1ArtifactCodec.WriteString(w, v.ToString()); }
        private static void W(BinaryWriter w, ItemId v) { Tlaw069UnityC1ArtifactCodec.WriteString(w, v.ToString()); }
        private static void W(BinaryWriter w, ProfileId v) { Tlaw069UnityC1ArtifactCodec.WriteString(w, v.ToString()); }
        private static void W(BinaryWriter w, EffectEventId v) { Tlaw069UnityC1ArtifactCodec.WriteString(w, v.ToString()); }
        private static ShiftId RShiftId(BinaryReader r) { return ShiftId.From(Tlaw069UnityC1ArtifactCodec.ReadString(r)); }
        private static LogId RLogId(BinaryReader r) { return LogId.From(Tlaw069UnityC1ArtifactCodec.ReadString(r)); }
        private static SpeciesId RSpeciesId(BinaryReader r) { return SpeciesId.From(Tlaw069UnityC1ArtifactCodec.ReadString(r)); }
        private static AnomalyId RAnomalyId(BinaryReader r) { return AnomalyId.From(Tlaw069UnityC1ArtifactCodec.ReadString(r)); }
        private static FlagId RFlagId(BinaryReader r) { return FlagId.From(Tlaw069UnityC1ArtifactCodec.ReadString(r)); }
        private static ItemId RItemId(BinaryReader r) { return ItemId.From(Tlaw069UnityC1ArtifactCodec.ReadString(r)); }
        private static ProfileId RProfileId(BinaryReader r) { return ProfileId.From(Tlaw069UnityC1ArtifactCodec.ReadString(r)); }
        private static EffectEventId REffectEventId(BinaryReader r) { return EffectEventId.From(Tlaw069UnityC1ArtifactCodec.ReadString(r)); }
        private static void WNullable(BinaryWriter w, AnomalyId? value) { w.Write(value.HasValue); if (value.HasValue) W(w, value.Value); }
        private static AnomalyId? RNullableAnomalyId(BinaryReader r) { return r.ReadBoolean() ? RAnomalyId(r) : (AnomalyId?)null; }
    }
}
