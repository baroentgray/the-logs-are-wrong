using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Configuration;

/// <summary>Trusted deployment identity for a validated C1 configuration artifact.</summary>
public sealed record ValidatedConfigurationC1SourceBinding(string ShiftYamlSha256, string AnomaliesYamlSha256, string ValidatorSourceBlob);

/// <summary>
/// Versioned binary handoff for an already validated PortableAuthority configuration graph.
/// This transport deliberately has no YAML parsing or validation semantics.
/// </summary>
public static class ValidatedConfigurationC1Codec
{
    public const string Magic = "TLAW-CFG-U4-C1";
    public const int Version = 1;
    private const int HashLength = 32;
    private const int MaxCollectionCount = 100_000;
    private const int MaxStringBytes = 1_000_000;

    public static ImmutableArray<string> RequiredPortableRecordTypes { get; } = ImmutableArray.Create(
        "AfterSuccessfulRitualDefinition", "AnomalyCatalog", "AnomalyDefinition", "ConfirmTestDefinition",
        "ContainmentConfiguration", "EffectDefinition", "LineNoiseConfiguration", "ManifestLogDefinition", "ObjectivesDefinition",
        "PostFactoIntervalInferenceDefinition", "ProcessingDefinition", "ProcessingOutcome", "ProcedureDefinition",
        "ProcedureStepDefinition", "PrototypeIncidentDefinition", "QuotaCreditDefinition", "QuotaDefinition",
        "ResourcesConfiguration", "SchedulerConfiguration", "ShiftConfiguration", "ShiftProfile", "SupplyDefinition",
        "ValidatedConfiguration", "WrongActionDefinition").OrderBy(static name => name, StringComparer.Ordinal).ToImmutableArray();

    public static ImmutableArray<string> ObservedPortableRecordTypes() => ImmutableArray.Create(
        typeof(AfterSuccessfulRitualDefinition).Name, typeof(AnomalyCatalog).Name, typeof(AnomalyDefinition).Name,
        typeof(ConfirmTestDefinition).Name, typeof(ContainmentConfiguration).Name, typeof(EffectDefinition).Name,
        typeof(LineNoiseConfiguration).Name, typeof(ManifestLogDefinition).Name, typeof(ObjectivesDefinition).Name,
        typeof(PostFactoIntervalInferenceDefinition).Name, typeof(ProcessingDefinition).Name, typeof(ProcessingOutcome).Name,
        typeof(ProcedureDefinition).Name, typeof(ProcedureStepDefinition).Name, typeof(PrototypeIncidentDefinition).Name,
        typeof(QuotaCreditDefinition).Name, typeof(QuotaDefinition).Name, typeof(ResourcesConfiguration).Name,
        typeof(SchedulerConfiguration).Name, typeof(ShiftConfiguration).Name, typeof(ShiftProfile).Name,
        typeof(SupplyDefinition).Name, typeof(ValidatedConfiguration).Name, typeof(WrongActionDefinition).Name)
        .OrderBy(static name => name, StringComparer.Ordinal).ToImmutableArray();

    public static byte[] Encode(ValidatedConfiguration configuration, ValidatedConfigurationC1SourceBinding binding)
    {
        Require(configuration, nameof(configuration));
        Require(binding, nameof(binding));
        var payload = Projection.Bytes(configuration);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        WriteString(writer, Magic);
        writer.Write(Version);
        WriteBinding(writer, binding);
        writer.Write(payload.Length);
        writer.Write(payload);
        writer.Write(Hash(payload));
        writer.Flush();
        return stream.ToArray();
    }

    public static ValidatedConfiguration Decode(byte[] artifact, ValidatedConfigurationC1SourceBinding expectedBinding)
    {
        Require(artifact, nameof(artifact));
        Require(expectedBinding, nameof(expectedBinding));
        try
        {
            using var stream = new MemoryStream(artifact, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            if (!string.Equals(ReadString(reader), Magic, StringComparison.Ordinal)) throw new InvalidDataException("C1 artifact magic is invalid.");
            if (reader.ReadInt32() != Version) throw new InvalidDataException("C1 artifact version is unsupported.");
            if (ReadBinding(reader) != expectedBinding) throw new InvalidDataException("C1 artifact source binding is stale or unexpected.");
            var payloadLength = ReadLength(reader, "payload");
            var payload = ReadExact(reader, payloadLength, "payload");
            var expectedPayloadHash = ReadExact(reader, HashLength, "payload hash");
            if (stream.Position != stream.Length) throw new InvalidDataException("C1 artifact has trailing data.");
            if (!Hash(payload).AsSpan().SequenceEqual(expectedPayloadHash)) throw new InvalidDataException("C1 artifact payload integrity check failed.");
            var result = Projection.Read(payload);
            if (!Projection.Bytes(result).AsSpan().SequenceEqual(payload)) throw new InvalidDataException("C1 materialized projection does not match the artifact payload.");
            return result;
        }
        catch (InvalidDataException) { throw; }
        catch (Exception exception) when (exception is EndOfStreamException or IOException or ArgumentException or OverflowException or DecoderFallbackException)
        {
            throw new InvalidDataException("C1 artifact could not be materialized.", exception);
        }
    }

    public static byte[] ProjectionBytes(ValidatedConfiguration configuration) => Projection.Bytes(configuration);
    public static string ProjectionSha256(ValidatedConfiguration configuration) => Hex(Hash(Projection.Bytes(configuration)));
    public static string Sha256(byte[] bytes) { Require(bytes, nameof(bytes)); return Hex(Hash(bytes)); }

    private static void Require(object? value, string name)
    {
        if (value is null) throw new ArgumentNullException(name);
    }

    private static byte[] Hash(byte[] bytes)
    {
        using var algorithm = SHA256.Create();
        return algorithm.ComputeHash(bytes);
    }

    private static string Hex(byte[] bytes)
    {
        var text = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes) text.Append(value.ToString("X2", CultureInfo.InvariantCulture));
        return text.ToString();
    }

    private static void WriteBinding(BinaryWriter writer, ValidatedConfigurationC1SourceBinding binding)
    {
        WriteString(writer, binding.ShiftYamlSha256); WriteString(writer, binding.AnomaliesYamlSha256); WriteString(writer, binding.ValidatorSourceBlob);
    }
    private static ValidatedConfigurationC1SourceBinding ReadBinding(BinaryReader reader) => new(ReadString(reader), ReadString(reader), ReadString(reader));
    private static void WriteString(BinaryWriter writer, string value) { Require(value, nameof(value)); var bytes = Encoding.UTF8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes); }
    private static string ReadString(BinaryReader reader) => Encoding.UTF8.GetString(ReadExact(reader, ReadLength(reader, "string"), "string"));
    private static int ReadCount(BinaryReader reader, string name) { var count = reader.ReadInt32(); if (count < 0 || count > MaxCollectionCount) throw new InvalidDataException($"C1 {name} count is invalid."); return count; }
    private static int ReadLength(BinaryReader reader, string name) { var length = reader.ReadInt32(); if (length < 0 || length > MaxStringBytes) throw new InvalidDataException($"C1 {name} length is invalid."); return length; }
    private static byte[] ReadExact(BinaryReader reader, int length, string name) { var bytes = reader.ReadBytes(length); if (bytes.Length != length) throw new InvalidDataException($"C1 {name} is truncated."); return bytes; }

    private static class Projection
    {
        internal static byte[] Bytes(ValidatedConfiguration configuration)
        {
            Require(configuration, nameof(configuration));
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            WriteConfiguration(writer, configuration); writer.Flush(); return stream.ToArray();
        }
        internal static ValidatedConfiguration Read(byte[] payload)
        {
            Require(payload, nameof(payload));
            try
            {
                using var stream = new MemoryStream(payload, false); using var reader = new BinaryReader(stream, Encoding.UTF8, true);
                var value = ReadConfiguration(reader);
                if (stream.Position != stream.Length) throw new InvalidDataException("C1 configuration projection has trailing data.");
                return value;
            }
            catch (InvalidDataException) { throw; }
            catch (Exception exception) when (exception is EndOfStreamException or IOException or ArgumentException or OverflowException or DecoderFallbackException)
            { throw new InvalidDataException("C1 configuration projection is malformed.", exception); }
        }

        private static void WriteConfiguration(BinaryWriter w, ValidatedConfiguration v) { WriteShift(w, v.Shift); WriteAnomalyCatalog(w, v.Anomalies); }
        private static ValidatedConfiguration ReadConfiguration(BinaryReader r) => new(ReadShift(r), ReadAnomalyCatalog(r));
        private static void WriteShift(BinaryWriter w, ShiftConfiguration v)
        {
            W(w, v.ShiftId); w.Write(v.Seed.Value); Dict(w, v.Profiles, static (x, k) => W(x, k), WriteProfile); WriteObjectives(w, v.Objectives); WriteSupply(w, v.Supply); WriteScheduler(w, v.Scheduler); WriteLineNoise(w, v.LineNoise); WriteResources(w, v.Resources); WriteContainment(w, v.Containment); Array(w, v.SuccessPredicate, static (x, i) => WriteString(x, i)); Array(w, v.Manifest, WriteManifest);
        }
        private static ShiftConfiguration ReadShift(BinaryReader r) => new(RShiftId(r), new ShiftSeed(r.ReadInt32()), Dict(r, RProfileId, ReadProfile), ReadObjectives(r), ReadSupply(r), ReadScheduler(r), ReadLineNoise(r), ReadResources(r), ReadContainment(r), Array(r, ReadString), Array(r, ReadManifest));
        private static void WriteProfile(BinaryWriter w, ShiftProfile v) { w.Write(v.IntakeTimeoutSeconds); w.Write(v.HardShiftDeadlineSeconds); }
        private static ShiftProfile ReadProfile(BinaryReader r) => new(r.ReadInt32(), r.ReadInt32());
        private static void WriteObjectives(BinaryWriter w, ObjectivesDefinition v) { WriteQuota(w, v.Quota); w.Write(v.MinCorrectlyProcessedAnomalies); }
        private static ObjectivesDefinition ReadObjectives(BinaryReader r) => new(ReadQuota(r), r.ReadInt32());
        private static void WriteQuota(BinaryWriter w, QuotaDefinition v) { w.Write(v.Total); Dict(w, v.BySpecies, static (x, k) => W(x, k), static (x, i) => x.Write(i)); }
        private static QuotaDefinition ReadQuota(BinaryReader r) => new(r.ReadInt32(), Dict(r, RSpeciesId, static x => x.ReadInt32()));
        private static void WriteSupply(BinaryWriter w, SupplyDefinition v) { w.Write(v.Total); w.Write(v.FreeWriteoffBuffer); }
        private static SupplyDefinition ReadSupply(BinaryReader r) => new(r.ReadInt32(), r.ReadInt32());
        private static void WriteManifest(BinaryWriter w, ManifestLogDefinition v) { W(w, v.Id); W(w, v.TrueSpecies); W(w, v.DeclaredSpecies); WNullable(w, v.Anomaly); }
        private static ManifestLogDefinition ReadManifest(BinaryReader r) => new(RLogId(r), RSpeciesId(r), RSpeciesId(r), RNullableAnomalyId(r));
        private static void WriteScheduler(BinaryWriter w, SchedulerConfiguration v)
        {
            Dict(w, v.Capacities, static (x, k) => WEnum(x, k), WriteCapacity); w.Write(v.InitialAdmissionDelaySeconds); w.Write(v.NormalFeedDelaySeconds); w.Write(v.EarlyFeedDelaySeconds); w.Write(v.SawCycleSeconds); w.Write(v.RepairHoldSeconds); w.Write(v.MovementNoiseSeconds); WriteString(w, v.DefaultTimeoutRoute); Array(w, v.SameTickOrder, static (x, i) => WEnum(x, i));
        }
        private static SchedulerConfiguration ReadScheduler(BinaryReader r) => new(Dict(r, static x => REnum<NodeId>(x), ReadCapacity), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), ReadString(r), Array(r, static x => REnum<HostTickStage>(x)));
        private static void WriteCapacity(BinaryWriter w, NodeCapacity v) { w.Write(v.IsUnlimited); if (!v.IsUnlimited) w.Write(v.Limit!.Value); }
        private static NodeCapacity ReadCapacity(BinaryReader r) => r.ReadBoolean() ? NodeCapacity.Unlimited : NodeCapacity.Limited(r.ReadInt32());
        private static void WriteLineNoise(BinaryWriter w, LineNoiseConfiguration v) { Array(w, v.QuietWhenAllInactive, static (x, i) => WriteString(x, i)); w.Write(v.PenitentConfirmRequiresContinuousQuietSeconds); w.Write(v.ResetTestProgressWhenLoud); w.Write(v.PauseIntakeTimerDuringTest); }
        private static LineNoiseConfiguration ReadLineNoise(BinaryReader r) => new(Array(r, ReadString), r.ReadInt32(), r.ReadBoolean(), r.ReadBoolean());
        private static void WriteResources(BinaryWriter w, ResourcesConfiguration v) { Dict(w, v.Consumable, static (x, k) => W(x, k), static (x, i) => x.Write(i)); Set(w, v.Reusable, static (x, i) => W(x, i)); }
        private static ResourcesConfiguration ReadResources(BinaryReader r) => new(Dict(r, RItemId, static x => x.ReadInt32()), Set(r, RItemId));
        private static void WriteContainment(BinaryWriter w, ContainmentConfiguration v)
        {
            w.Write(v.UnlimitedCapacity); w.Write(v.RitualHoldSeconds); w.Write(v.ServiceRequestedGraceSeconds); w.Write(v.OverdueSeconds); Dict(w, v.IntervalByDangerWeight, static (x, k) => WriteString(x, k), static (x, i) => x.Write(i)); WriteAfterRitual(w, v.AfterSuccessfulRitual); WriteIncident(w, v.PrototypeIncident); WritePostFacto(w, v.PostFactoIntervalInference);
        }
        private static ContainmentConfiguration ReadContainment(BinaryReader r) => new(r.ReadBoolean(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), Dict(r, ReadString, static x => x.ReadInt32()), ReadAfterRitual(r), ReadIncident(r), ReadPostFacto(r));
        private static void WriteAfterRitual(BinaryWriter w, AfterSuccessfulRitualDefinition v) { WEnum(w, v.ReturnState); w.Write(v.RetainLogs); w.Write(v.RetainDangerWeight); w.Write(v.RestartIntervalFromCurrentWeight); }
        private static AfterSuccessfulRitualDefinition ReadAfterRitual(BinaryReader r) => new(REnum<ContainmentState>(r), r.ReadBoolean(), r.ReadBoolean(), r.ReadBoolean());
        private static void WriteIncident(BinaryWriter w, PrototypeIncidentDefinition v) { WriteString(w, v.Type); w.Write(v.DurationSeconds); w.Write(v.RemainsIncidentUntilRitual); w.Write(v.RepeatBeforeResolution); }
        private static PrototypeIncidentDefinition ReadIncident(BinaryReader r) => new(ReadString(r), r.ReadInt32(), r.ReadBoolean(), r.ReadBoolean());
        private static void WritePostFacto(BinaryWriter w, PostFactoIntervalInferenceDefinition v) { w.Write(v.Allowed); WriteString(w, v.Rationale); w.Write(v.SeededJitter); }
        private static PostFactoIntervalInferenceDefinition ReadPostFacto(BinaryReader r) => new(r.ReadBoolean(), ReadString(r), r.ReadBoolean());
        private static void WriteAnomalyCatalog(BinaryWriter w, AnomalyCatalog v) => Dict(w, v.Definitions, static (x, k) => W(x, k), WriteAnomaly);
        private static AnomalyCatalog ReadAnomalyCatalog(BinaryReader r) => new(Dict(r, RAnomalyId, ReadAnomaly));
        private static void WriteAnomaly(BinaryWriter w, AnomalyDefinition v)
        {
            W(w, v.Id); w.Write(v.DangerWeight); Array(w, v.InstantClues, static (x, i) => WriteString(x, i)); Array(w, v.ObservedClues, static (x, i) => WriteString(x, i)); WriteConfirm(w, v.ConfirmTest); WriteProcessing(w, v.Processing); WriteProcedure(w, v.Procedure); Dict(w, v.WrongActions, static (x, k) => W(x, k), WriteWrongAction);
        }
        private static AnomalyDefinition ReadAnomaly(BinaryReader r) => new(RAnomalyId(r), r.ReadInt32(), Array(r, ReadString), Array(r, ReadString), ReadConfirm(r), ReadProcessing(r), ReadProcedure(r), Dict(r, RItemId, ReadWrongAction));
        private static void WriteConfirm(BinaryWriter w, ConfirmTestDefinition v) { WNullableEnum(w, v.RequiredLineNoise); w.Write(v.DurationSeconds); w.Write(v.Continuous); WNullableBool(w, v.ResetWhenConditionLost); WriteString(w, v.Result); Array(w, v.Tools, static (x, i) => W(x, i)); }
        private static ConfirmTestDefinition ReadConfirm(BinaryReader r) => new(RNullableEnum<LineNoise>(r), r.ReadInt32(), r.ReadBoolean(), RNullableBool(r), ReadString(r), Array(r, RItemId));
        private static void WriteProcessing(BinaryWriter w, ProcessingDefinition v) { Set(w, v.RequiredFlags, static (x, i) => W(x, i)); WEnum(w, v.RouteWithoutFlags); WriteProcessingOutcome(w, v.OnCorrect); WriteProcessingOutcome(w, v.OnIncorrect); }
        private static ProcessingDefinition ReadProcessing(BinaryReader r) => new(Set(r, RFlagId), REnum<RouteWithoutFlagsPolicy>(r), ReadProcessingOutcome(r), ReadProcessingOutcome(r));
        private static void WriteProcessingOutcome(BinaryWriter w, ProcessingOutcome v) { WEnum(w, v.TerminalState); WriteQuotaCredit(w, v.QuotaCredit); w.Write(v.CorrectAnomalyDelta); Array(w, v.Effects, WriteEffect); }
        private static ProcessingOutcome ReadProcessingOutcome(BinaryReader r) => new(REnum<LogState>(r), ReadQuotaCredit(r), r.ReadInt32(), Array(r, ReadEffect));
        private static void WriteQuotaCredit(BinaryWriter w, QuotaCreditDefinition v) { WEnum(w, v.Species); w.Write(v.Units); }
        private static QuotaCreditDefinition ReadQuotaCredit(BinaryReader r) => new(REnum<SpeciesCreditRule>(r), r.ReadInt32());
        private static void WriteEffect(BinaryWriter w, EffectDefinition v) { WEnum(w, v.Type); W(w, v.Event); WNullableInt(w, v.DurationSeconds); WNullableString(w, v.Target); }
        private static EffectDefinition ReadEffect(BinaryReader r) => new(REnum<EffectType>(r), REffectEventId(r), RNullableInt(r), RNullableString(r));
        private static void WriteProcedure(BinaryWriter w, ProcedureDefinition v) { Array(w, v.Steps, WriteStep); Set(w, v.GrantsFlags, static (x, i) => W(x, i)); }
        private static ProcedureDefinition ReadProcedure(BinaryReader r) => new(Array(r, ReadStep), Set(r, RFlagId));
        private static void WriteStep(BinaryWriter w, ProcedureStepDefinition v) { W(w, v.Item); w.Write(v.Consumes); WNullableInt(w, v.HoldSeconds); }
        private static ProcedureStepDefinition ReadStep(BinaryReader r) => new(RItemId(r), r.ReadBoolean(), RNullableInt(r));
        private static void WriteWrongAction(BinaryWriter w, WrongActionDefinition v) { w.Write(v.LeavesStateUnchanged); WNullableEnum(w, v.TerminalState); w.Write(v.Consumes); Array(w, v.Effects, WriteEffect); }
        private static WrongActionDefinition ReadWrongAction(BinaryReader r) => new(r.ReadBoolean(), RNullableEnum<LogState>(r), r.ReadBoolean(), Array(r, ReadEffect));
        private static void WNullableString(BinaryWriter w, string? v) { w.Write(v is not null); if (v is not null) WriteString(w, v); }
        private static string? RNullableString(BinaryReader r) => r.ReadBoolean() ? ReadString(r) : null;
        private static void WNullableInt(BinaryWriter w, int? v) { w.Write(v.HasValue); if (v.HasValue) w.Write(v.Value); }
        private static int? RNullableInt(BinaryReader r) => r.ReadBoolean() ? r.ReadInt32() : null;
        private static void WNullableBool(BinaryWriter w, bool? v) { w.Write(v.HasValue); if (v.HasValue) w.Write(v.Value); }
        private static bool? RNullableBool(BinaryReader r) => r.ReadBoolean() ? r.ReadBoolean() : null;
        private static void WNullableEnum<T>(BinaryWriter w, T? v) where T : struct, Enum { w.Write(v.HasValue); if (v.HasValue) WEnum(w, v.Value); }
        private static T? RNullableEnum<T>(BinaryReader r) where T : struct, Enum => r.ReadBoolean() ? REnum<T>(r) : null;
        private static void WEnum<T>(BinaryWriter w, T v) where T : struct, Enum => w.Write(Convert.ToInt32(v, CultureInfo.InvariantCulture));
        private static T REnum<T>(BinaryReader r) where T : struct, Enum { var raw = r.ReadInt32(); if (!Enum.IsDefined(typeof(T), raw)) throw new InvalidDataException($"C1 enum {typeof(T).Name} is invalid."); return (T)Enum.ToObject(typeof(T), raw); }
        private static void Array<T>(BinaryWriter w, ImmutableArray<T> values, Action<BinaryWriter, T> write) { w.Write(values.Length); foreach (var value in values) write(w, value); }
        private static ImmutableArray<T> Array<T>(BinaryReader r, Func<BinaryReader, T> read) { var builder = ImmutableArray.CreateBuilder<T>(ReadCount(r, "array")); for (var i = 0; i < builder.Capacity; i++) builder.Add(read(r)); return builder.MoveToImmutable(); }
        private static void Set<T>(BinaryWriter w, ImmutableHashSet<T> values, Action<BinaryWriter, T> write) where T : notnull { var ordered = values.OrderBy(static value => value!.ToString(), StringComparer.Ordinal).ToArray(); w.Write(ordered.Length); foreach (var value in ordered) write(w, value); }
        private static ImmutableHashSet<T> Set<T>(BinaryReader r, Func<BinaryReader, T> read) where T : notnull { var builder = ImmutableHashSet.CreateBuilder<T>(); var count = ReadCount(r, "set"); for (var i = 0; i < count; i++) if (!builder.Add(read(r))) throw new InvalidDataException("C1 set contains a duplicate value."); return builder.ToImmutable(); }
        private static void Dict<TKey, TValue>(BinaryWriter w, ImmutableDictionary<TKey, TValue> values, Action<BinaryWriter, TKey> writeKey, Action<BinaryWriter, TValue> writeValue) where TKey : notnull { var ordered = values.OrderBy(static pair => pair.Key!.ToString(), StringComparer.Ordinal).ToArray(); w.Write(ordered.Length); foreach (var pair in ordered) { writeKey(w, pair.Key); writeValue(w, pair.Value); } }
        private static ImmutableDictionary<TKey, TValue> Dict<TKey, TValue>(BinaryReader r, Func<BinaryReader, TKey> readKey, Func<BinaryReader, TValue> readValue) where TKey : notnull { var builder = ImmutableDictionary.CreateBuilder<TKey, TValue>(); var count = ReadCount(r, "dictionary"); for (var i = 0; i < count; i++) if (!builder.TryAdd(readKey(r), readValue(r))) throw new InvalidDataException("C1 dictionary contains a duplicate key."); return builder.ToImmutable(); }
        private static void W(BinaryWriter w, ShiftId v) => WriteString(w, v.ToString()); private static void W(BinaryWriter w, LogId v) => WriteString(w, v.ToString()); private static void W(BinaryWriter w, SpeciesId v) => WriteString(w, v.ToString()); private static void W(BinaryWriter w, AnomalyId v) => WriteString(w, v.ToString()); private static void W(BinaryWriter w, FlagId v) => WriteString(w, v.ToString()); private static void W(BinaryWriter w, ItemId v) => WriteString(w, v.ToString()); private static void W(BinaryWriter w, ProfileId v) => WriteString(w, v.ToString()); private static void W(BinaryWriter w, EffectEventId v) => WriteString(w, v.ToString());
        private static ShiftId RShiftId(BinaryReader r) => ShiftId.From(ReadString(r)); private static LogId RLogId(BinaryReader r) => LogId.From(ReadString(r)); private static SpeciesId RSpeciesId(BinaryReader r) => SpeciesId.From(ReadString(r)); private static AnomalyId RAnomalyId(BinaryReader r) => AnomalyId.From(ReadString(r)); private static FlagId RFlagId(BinaryReader r) => FlagId.From(ReadString(r)); private static ItemId RItemId(BinaryReader r) => ItemId.From(ReadString(r)); private static ProfileId RProfileId(BinaryReader r) => ProfileId.From(ReadString(r)); private static EffectEventId REffectEventId(BinaryReader r) => EffectEventId.From(ReadString(r));
        private static void WNullable(BinaryWriter w, AnomalyId? v) { w.Write(v.HasValue); if (v.HasValue) W(w, v.Value); }
        private static AnomalyId? RNullableAnomalyId(BinaryReader r) => r.ReadBoolean() ? RAnomalyId(r) : null;
    }
}
