using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

internal sealed record Tlaw069SourceBinding(string ShiftYamlSha256, string AnomaliesYamlSha256, string ValidatorSourceBlob);

/// <summary>
/// Candidate C1 reference transport. It is deliberately test-only: a later owner-selected implementation
/// would move one equivalent codec into PortableAuthority rather than promote this test harness.
/// </summary>
internal static class Tlaw069C1ArtifactCodec
{
    private const string Magic = "TLAW-CFG-U4-C1";
    private const int Version = 1;
    private const int HashLength = 32;
    private const int MaxCollectionCount = 100_000;
    private const int MaxStringBytes = 1_000_000;

    internal static byte[] Encode(ValidatedConfiguration configuration, Tlaw069SourceBinding binding)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(binding);
        var payload = Tlaw069ConfigurationProjection.Bytes(configuration);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteString(writer, Magic);
        writer.Write(Version);
        WriteBinding(writer, binding);
        writer.Write(payload.Length);
        writer.Write(payload);
        writer.Write(SHA256.HashData(payload));
        writer.Flush();
        return stream.ToArray();
    }

    internal static ValidatedConfiguration Decode(byte[] artifact, Tlaw069SourceBinding expectedBinding)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(expectedBinding);
        try
        {
            using var stream = new MemoryStream(artifact, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            if (!string.Equals(ReadString(reader), Magic, StringComparison.Ordinal)) throw new InvalidDataException("C1 artifact magic is invalid.");
            if (reader.ReadInt32() != Version) throw new InvalidDataException("C1 artifact version is unsupported.");
            if (ReadBinding(reader) != expectedBinding) throw new InvalidDataException("C1 artifact source binding is stale or unexpected.");
            var payloadLength = ReadLength(reader, "payload");
            var payload = ReadExact(reader, payloadLength, "payload");
            var expectedPayloadHash = ReadExact(reader, HashLength, "payload hash");
            if (stream.Position != stream.Length) throw new InvalidDataException("C1 artifact has trailing data.");
            if (!SHA256.HashData(payload).AsSpan().SequenceEqual(expectedPayloadHash)) throw new InvalidDataException("C1 artifact payload integrity check failed.");

            var result = Tlaw069ConfigurationProjection.Read(payload);
            if (!Tlaw069ConfigurationProjection.Bytes(result).AsSpan().SequenceEqual(payload)) throw new InvalidDataException("C1 materialized projection does not match the artifact payload.");
            return result;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is EndOfStreamException or IOException or ArgumentException or OverflowException or DecoderFallbackException)
        {
            throw new InvalidDataException("C1 artifact could not be materialized.", exception);
        }
    }

    internal static void WriteVersionForTest(byte[] artifact, int version)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var offset = checked(1 + Encoding.UTF8.GetByteCount(Magic));
        if (artifact.Length < offset + sizeof(int)) throw new ArgumentException("Artifact is too short to contain a version.", nameof(artifact));
        BitConverter.GetBytes(version).CopyTo(artifact, offset);
    }

    private static void WriteBinding(BinaryWriter writer, Tlaw069SourceBinding binding)
    {
        WriteString(writer, binding.ShiftYamlSha256);
        WriteString(writer, binding.AnomaliesYamlSha256);
        WriteString(writer, binding.ValidatorSourceBlob);
    }

    private static Tlaw069SourceBinding ReadBinding(BinaryReader reader) => new(ReadString(reader), ReadString(reader), ReadString(reader));

    internal static void WriteString(BinaryWriter writer, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    internal static string ReadString(BinaryReader reader)
    {
        var bytes = ReadExact(reader, ReadLength(reader, "string"), "string");
        return Encoding.UTF8.GetString(bytes);
    }

    internal static int ReadCount(BinaryReader reader, string name)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > MaxCollectionCount) throw new InvalidDataException($"C1 {name} count is invalid.");
        return count;
    }

    private static int ReadLength(BinaryReader reader, string name)
    {
        var length = reader.ReadInt32();
        if (length < 0 || length > MaxStringBytes) throw new InvalidDataException($"C1 {name} length is invalid.");
        return length;
    }

    private static byte[] ReadExact(BinaryReader reader, int length, string name)
    {
        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length) throw new InvalidDataException($"C1 {name} is truncated.");
        return bytes;
    }
}

/// <summary>Canonical full-graph transport projection used only by the bounded C1/C2 proof.</summary>
internal static class Tlaw069ConfigurationProjection
{
    internal static readonly ImmutableArray<string> RequiredPortableRecordTypes = ImmutableArray.Create(
        "AfterSuccessfulRitualDefinition", "AnomalyCatalog", "AnomalyDefinition", "ConfirmTestDefinition",
        "ContainmentConfiguration", "EffectDefinition", "ManifestLogDefinition", "ObjectivesDefinition",
        "PostFactoIntervalInferenceDefinition", "ProcessingDefinition", "ProcessingOutcome", "ProcedureDefinition",
        "ProcedureStepDefinition", "PrototypeIncidentDefinition", "QuotaCreditDefinition", "QuotaDefinition",
        "ResourcesConfiguration", "SchedulerConfiguration", "ShiftConfiguration", "ShiftProfile",
        "SupplyDefinition", "ValidatedConfiguration", "WrongActionDefinition")
        .OrderBy(static name => name, StringComparer.Ordinal)
        .ToImmutableArray();

    internal static ImmutableArray<string> ObservedPortableRecordTypes() => ImmutableArray.Create(
        typeof(AfterSuccessfulRitualDefinition).Name, typeof(AnomalyCatalog).Name, typeof(AnomalyDefinition).Name,
        typeof(ConfirmTestDefinition).Name, typeof(ContainmentConfiguration).Name, typeof(EffectDefinition).Name,
        typeof(ManifestLogDefinition).Name, typeof(ObjectivesDefinition).Name, typeof(PostFactoIntervalInferenceDefinition).Name,
        typeof(ProcessingDefinition).Name, typeof(ProcessingOutcome).Name, typeof(ProcedureDefinition).Name,
        typeof(ProcedureStepDefinition).Name, typeof(PrototypeIncidentDefinition).Name, typeof(QuotaCreditDefinition).Name,
        typeof(QuotaDefinition).Name, typeof(ResourcesConfiguration).Name, typeof(SchedulerConfiguration).Name,
        typeof(ShiftConfiguration).Name, typeof(ShiftProfile).Name, typeof(SupplyDefinition).Name,
        typeof(ValidatedConfiguration).Name, typeof(WrongActionDefinition).Name).OrderBy(static name => name, StringComparer.Ordinal).ToImmutableArray();

    internal static string Sha256(ValidatedConfiguration configuration) => Convert.ToHexString(SHA256.HashData(Bytes(configuration)));

    internal static byte[] Bytes(ValidatedConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteConfiguration(writer, configuration);
        writer.Flush();
        return stream.ToArray();
    }

    internal static ValidatedConfiguration Read(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        try
        {
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var value = ReadConfiguration(reader);
            if (stream.Position != stream.Length) throw new InvalidDataException("C1 configuration projection has trailing data.");
            return value;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is EndOfStreamException or IOException or ArgumentException or OverflowException or DecoderFallbackException)
        {
            throw new InvalidDataException("C1 configuration projection is malformed.", exception);
        }
    }

    private static void WriteConfiguration(BinaryWriter writer, ValidatedConfiguration value)
    {
        WriteShift(writer, value.Shift);
        WriteAnomalyCatalog(writer, value.Anomalies);
    }

    private static ValidatedConfiguration ReadConfiguration(BinaryReader reader) => new(ReadShift(reader), ReadAnomalyCatalog(reader));

    private static void WriteShift(BinaryWriter writer, ShiftConfiguration value)
    {
        WriteId(writer, value.ShiftId);
        writer.Write(value.Seed.Value);
        WriteDictionary(writer, value.Profiles, static (w, key) => WriteId(w, key), WriteShiftProfile);
        WriteObjectives(writer, value.Objectives);
        WriteSupply(writer, value.Supply);
        WriteScheduler(writer, value.Scheduler);
        WriteLineNoiseConfiguration(writer, value.LineNoise);
        WriteResources(writer, value.Resources);
        WriteContainment(writer, value.Containment);
        WriteArray(writer, value.SuccessPredicate, static (w, item) => Tlaw069C1ArtifactCodec.WriteString(w, item));
        WriteArray(writer, value.Manifest, WriteManifestLog);
    }

    private static ShiftConfiguration ReadShift(BinaryReader reader) => new(
        ReadShiftId(reader), new ShiftSeed(reader.ReadInt32()),
        ReadDictionary(reader, ReadProfileId, ReadShiftProfile), ReadObjectives(reader), ReadSupply(reader), ReadScheduler(reader),
        ReadLineNoiseConfiguration(reader), ReadResources(reader), ReadContainment(reader),
        ReadArray(reader, Tlaw069C1ArtifactCodec.ReadString), ReadArray(reader, ReadManifestLog));

    private static void WriteShiftProfile(BinaryWriter writer, ShiftProfile value) { writer.Write(value.IntakeTimeoutSeconds); writer.Write(value.HardShiftDeadlineSeconds); }
    private static ShiftProfile ReadShiftProfile(BinaryReader reader) => new(reader.ReadInt32(), reader.ReadInt32());

    private static void WriteObjectives(BinaryWriter writer, ObjectivesDefinition value) { WriteQuota(writer, value.Quota); writer.Write(value.MinCorrectlyProcessedAnomalies); }
    private static ObjectivesDefinition ReadObjectives(BinaryReader reader) => new(ReadQuota(reader), reader.ReadInt32());

    private static void WriteQuota(BinaryWriter writer, QuotaDefinition value) { writer.Write(value.Total); WriteDictionary(writer, value.BySpecies, static (w, key) => WriteId(w, key), static (w, item) => w.Write(item)); }
    private static QuotaDefinition ReadQuota(BinaryReader reader) => new(reader.ReadInt32(), ReadDictionary(reader, ReadSpeciesId, static r => r.ReadInt32()));

    private static void WriteSupply(BinaryWriter writer, SupplyDefinition value) { writer.Write(value.Total); writer.Write(value.FreeWriteoffBuffer); }
    private static SupplyDefinition ReadSupply(BinaryReader reader) => new(reader.ReadInt32(), reader.ReadInt32());

    private static void WriteManifestLog(BinaryWriter writer, ManifestLogDefinition value)
    {
        WriteId(writer, value.Id); WriteId(writer, value.TrueSpecies); WriteId(writer, value.DeclaredSpecies); WriteNullableId(writer, value.Anomaly);
    }
    private static ManifestLogDefinition ReadManifestLog(BinaryReader reader) => new(ReadLogId(reader), ReadSpeciesId(reader), ReadSpeciesId(reader), ReadNullableAnomalyId(reader));

    private static void WriteScheduler(BinaryWriter writer, SchedulerConfiguration value)
    {
        WriteDictionary(writer, value.Capacities, static (w, key) => WriteEnum(w, key), WriteNodeCapacity);
        writer.Write(value.InitialAdmissionDelaySeconds); writer.Write(value.NormalFeedDelaySeconds); writer.Write(value.EarlyFeedDelaySeconds);
        writer.Write(value.SawCycleSeconds); writer.Write(value.RepairHoldSeconds); writer.Write(value.MovementNoiseSeconds);
        Tlaw069C1ArtifactCodec.WriteString(writer, value.DefaultTimeoutRoute);
        WriteArray(writer, value.SameTickOrder, static (w, item) => WriteEnum(w, item));
    }
    private static SchedulerConfiguration ReadScheduler(BinaryReader reader) => new(
        ReadDictionary(reader, static r => ReadEnum<NodeId>(r), ReadNodeCapacity), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
        reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), Tlaw069C1ArtifactCodec.ReadString(reader),
        ReadArray(reader, static r => ReadEnum<HostTickStage>(r)));

    private static void WriteNodeCapacity(BinaryWriter writer, NodeCapacity value) { writer.Write(value.IsUnlimited); if (!value.IsUnlimited) writer.Write(value.Limit!.Value); }
    private static NodeCapacity ReadNodeCapacity(BinaryReader reader) => reader.ReadBoolean() ? NodeCapacity.Unlimited : NodeCapacity.Limited(reader.ReadInt32());

    private static void WriteLineNoiseConfiguration(BinaryWriter writer, LineNoiseConfiguration value)
    {
        WriteArray(writer, value.QuietWhenAllInactive, static (w, item) => Tlaw069C1ArtifactCodec.WriteString(w, item));
        writer.Write(value.PenitentConfirmRequiresContinuousQuietSeconds); writer.Write(value.ResetTestProgressWhenLoud); writer.Write(value.PauseIntakeTimerDuringTest);
    }
    private static LineNoiseConfiguration ReadLineNoiseConfiguration(BinaryReader reader) => new(ReadArray(reader, Tlaw069C1ArtifactCodec.ReadString), reader.ReadInt32(), reader.ReadBoolean(), reader.ReadBoolean());

    private static void WriteResources(BinaryWriter writer, ResourcesConfiguration value)
    {
        WriteDictionary(writer, value.Consumable, static (w, key) => WriteId(w, key), static (w, item) => w.Write(item));
        WriteSet(writer, value.Reusable, static (w, item) => WriteId(w, item));
    }
    private static ResourcesConfiguration ReadResources(BinaryReader reader) => new(ReadDictionary(reader, ReadItemId, static r => r.ReadInt32()), ReadSet(reader, ReadItemId));

    private static void WriteContainment(BinaryWriter writer, ContainmentConfiguration value)
    {
        writer.Write(value.UnlimitedCapacity); writer.Write(value.RitualHoldSeconds); writer.Write(value.ServiceRequestedGraceSeconds); writer.Write(value.OverdueSeconds);
        WriteDictionary(writer, value.IntervalByDangerWeight, static (w, key) => Tlaw069C1ArtifactCodec.WriteString(w, key), static (w, item) => w.Write(item));
        WriteAfterRitual(writer, value.AfterSuccessfulRitual); WriteIncident(writer, value.PrototypeIncident); WritePostFacto(writer, value.PostFactoIntervalInference);
    }
    private static ContainmentConfiguration ReadContainment(BinaryReader reader) => new(reader.ReadBoolean(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
        ReadDictionary(reader, Tlaw069C1ArtifactCodec.ReadString, static r => r.ReadInt32()), ReadAfterRitual(reader), ReadIncident(reader), ReadPostFacto(reader));

    private static void WriteAfterRitual(BinaryWriter writer, AfterSuccessfulRitualDefinition value)
    {
        WriteEnum(writer, value.ReturnState); writer.Write(value.RetainLogs); writer.Write(value.RetainDangerWeight); writer.Write(value.RestartIntervalFromCurrentWeight);
    }
    private static AfterSuccessfulRitualDefinition ReadAfterRitual(BinaryReader reader) => new(ReadEnum<ContainmentState>(reader), reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadBoolean());

    private static void WriteIncident(BinaryWriter writer, PrototypeIncidentDefinition value)
    {
        Tlaw069C1ArtifactCodec.WriteString(writer, value.Type); writer.Write(value.DurationSeconds); writer.Write(value.RemainsIncidentUntilRitual); writer.Write(value.RepeatBeforeResolution);
    }
    private static PrototypeIncidentDefinition ReadIncident(BinaryReader reader) => new(Tlaw069C1ArtifactCodec.ReadString(reader), reader.ReadInt32(), reader.ReadBoolean(), reader.ReadBoolean());

    private static void WritePostFacto(BinaryWriter writer, PostFactoIntervalInferenceDefinition value)
    {
        writer.Write(value.Allowed); Tlaw069C1ArtifactCodec.WriteString(writer, value.Rationale); writer.Write(value.SeededJitter);
    }
    private static PostFactoIntervalInferenceDefinition ReadPostFacto(BinaryReader reader) => new(reader.ReadBoolean(), Tlaw069C1ArtifactCodec.ReadString(reader), reader.ReadBoolean());

    private static void WriteAnomalyCatalog(BinaryWriter writer, AnomalyCatalog value) => WriteDictionary(writer, value.Definitions, static (w, key) => WriteId(w, key), WriteAnomalyDefinition);
    private static AnomalyCatalog ReadAnomalyCatalog(BinaryReader reader) => new(ReadDictionary(reader, ReadAnomalyId, ReadAnomalyDefinition));

    private static void WriteAnomalyDefinition(BinaryWriter writer, AnomalyDefinition value)
    {
        WriteId(writer, value.Id); writer.Write(value.DangerWeight);
        WriteArray(writer, value.InstantClues, static (w, item) => Tlaw069C1ArtifactCodec.WriteString(w, item));
        WriteArray(writer, value.ObservedClues, static (w, item) => Tlaw069C1ArtifactCodec.WriteString(w, item));
        WriteConfirmTest(writer, value.ConfirmTest); WriteProcessing(writer, value.Processing); WriteProcedure(writer, value.Procedure);
        WriteDictionary(writer, value.WrongActions, static (w, key) => WriteId(w, key), WriteWrongAction);
    }
    private static AnomalyDefinition ReadAnomalyDefinition(BinaryReader reader) => new(ReadAnomalyId(reader), reader.ReadInt32(),
        ReadArray(reader, Tlaw069C1ArtifactCodec.ReadString), ReadArray(reader, Tlaw069C1ArtifactCodec.ReadString), ReadConfirmTest(reader), ReadProcessing(reader),
        ReadProcedure(reader), ReadDictionary(reader, ReadItemId, ReadWrongAction));

    private static void WriteConfirmTest(BinaryWriter writer, ConfirmTestDefinition value)
    {
        WriteNullableEnum(writer, value.RequiredLineNoise); writer.Write(value.DurationSeconds); writer.Write(value.Continuous); WriteNullableBool(writer, value.ResetWhenConditionLost);
        Tlaw069C1ArtifactCodec.WriteString(writer, value.Result); WriteArray(writer, value.Tools, static (w, item) => WriteId(w, item));
    }
    private static ConfirmTestDefinition ReadConfirmTest(BinaryReader reader) => new(ReadNullableEnum<LineNoise>(reader), reader.ReadInt32(), reader.ReadBoolean(),
        ReadNullableBool(reader), Tlaw069C1ArtifactCodec.ReadString(reader), ReadArray(reader, ReadItemId));

    private static void WriteProcessing(BinaryWriter writer, ProcessingDefinition value)
    {
        WriteSet(writer, value.RequiredFlags, static (w, item) => WriteId(w, item)); WriteEnum(writer, value.RouteWithoutFlags); WriteProcessingOutcome(writer, value.OnCorrect); WriteProcessingOutcome(writer, value.OnIncorrect);
    }
    private static ProcessingDefinition ReadProcessing(BinaryReader reader) => new(ReadSet(reader, ReadFlagId), ReadEnum<RouteWithoutFlagsPolicy>(reader), ReadProcessingOutcome(reader), ReadProcessingOutcome(reader));

    private static void WriteProcessingOutcome(BinaryWriter writer, ProcessingOutcome value)
    {
        WriteEnum(writer, value.TerminalState); WriteQuotaCredit(writer, value.QuotaCredit); writer.Write(value.CorrectAnomalyDelta); WriteArray(writer, value.Effects, WriteEffect);
    }
    private static ProcessingOutcome ReadProcessingOutcome(BinaryReader reader) => new(ReadEnum<LogState>(reader), ReadQuotaCredit(reader), reader.ReadInt32(), ReadArray(reader, ReadEffect));

    private static void WriteQuotaCredit(BinaryWriter writer, QuotaCreditDefinition value) { WriteEnum(writer, value.Species); writer.Write(value.Units); }
    private static QuotaCreditDefinition ReadQuotaCredit(BinaryReader reader) => new(ReadEnum<SpeciesCreditRule>(reader), reader.ReadInt32());

    private static void WriteEffect(BinaryWriter writer, EffectDefinition value)
    {
        WriteEnum(writer, value.Type); WriteId(writer, value.Event); WriteNullableInt(writer, value.DurationSeconds); WriteNullableString(writer, value.Target);
    }
    private static EffectDefinition ReadEffect(BinaryReader reader) => new(ReadEnum<EffectType>(reader), ReadEffectEventId(reader), ReadNullableInt(reader), ReadNullableString(reader));

    private static void WriteProcedure(BinaryWriter writer, ProcedureDefinition value)
    {
        WriteArray(writer, value.Steps, WriteProcedureStep); WriteSet(writer, value.GrantsFlags, static (w, item) => WriteId(w, item));
    }
    private static ProcedureDefinition ReadProcedure(BinaryReader reader) => new(ReadArray(reader, ReadProcedureStep), ReadSet(reader, ReadFlagId));

    private static void WriteProcedureStep(BinaryWriter writer, ProcedureStepDefinition value) { WriteId(writer, value.Item); writer.Write(value.Consumes); WriteNullableInt(writer, value.HoldSeconds); }
    private static ProcedureStepDefinition ReadProcedureStep(BinaryReader reader) => new(ReadItemId(reader), reader.ReadBoolean(), ReadNullableInt(reader));

    private static void WriteWrongAction(BinaryWriter writer, WrongActionDefinition value)
    {
        writer.Write(value.LeavesStateUnchanged); WriteNullableEnum(writer, value.TerminalState); writer.Write(value.Consumes); WriteArray(writer, value.Effects, WriteEffect);
    }
    private static WrongActionDefinition ReadWrongAction(BinaryReader reader) => new(reader.ReadBoolean(), ReadNullableEnum<LogState>(reader), reader.ReadBoolean(), ReadArray(reader, ReadEffect));

    private static void WriteNullableString(BinaryWriter writer, string? value) { writer.Write(value is not null); if (value is not null) Tlaw069C1ArtifactCodec.WriteString(writer, value); }
    private static string? ReadNullableString(BinaryReader reader) => reader.ReadBoolean() ? Tlaw069C1ArtifactCodec.ReadString(reader) : null;
    private static void WriteNullableInt(BinaryWriter writer, int? value) { writer.Write(value.HasValue); if (value.HasValue) writer.Write(value.Value); }
    private static int? ReadNullableInt(BinaryReader reader) => reader.ReadBoolean() ? reader.ReadInt32() : null;
    private static void WriteNullableBool(BinaryWriter writer, bool? value) { writer.Write(value.HasValue); if (value.HasValue) writer.Write(value.Value); }
    private static bool? ReadNullableBool(BinaryReader reader) => reader.ReadBoolean() ? reader.ReadBoolean() : null;

    private static void WriteNullableEnum<T>(BinaryWriter writer, T? value) where T : struct, Enum { writer.Write(value.HasValue); if (value.HasValue) WriteEnum(writer, value.Value); }
    private static T? ReadNullableEnum<T>(BinaryReader reader) where T : struct, Enum => reader.ReadBoolean() ? ReadEnum<T>(reader) : null;
    private static void WriteEnum<T>(BinaryWriter writer, T value) where T : struct, Enum => writer.Write(Convert.ToInt32(value, CultureInfo.InvariantCulture));
    private static T ReadEnum<T>(BinaryReader reader) where T : struct, Enum
    {
        var raw = reader.ReadInt32();
        if (!Enum.IsDefined(typeof(T), raw)) throw new InvalidDataException($"C1 enum {typeof(T).Name} is invalid.");
        return (T)Enum.ToObject(typeof(T), raw);
    }

    private static void WriteArray<T>(BinaryWriter writer, ImmutableArray<T> values, Action<BinaryWriter, T> write)
    {
        writer.Write(values.Length);
        foreach (var value in values) write(writer, value);
    }
    private static ImmutableArray<T> ReadArray<T>(BinaryReader reader, Func<BinaryReader, T> read)
    {
        var builder = ImmutableArray.CreateBuilder<T>(Tlaw069C1ArtifactCodec.ReadCount(reader, "array"));
        for (var index = 0; index < builder.Capacity; index++) builder.Add(read(reader));
        return builder.MoveToImmutable();
    }

    private static void WriteSet<T>(BinaryWriter writer, ImmutableHashSet<T> values, Action<BinaryWriter, T> write) where T : notnull
    {
        var ordered = values.OrderBy(static value => value!.ToString(), StringComparer.Ordinal).ToArray();
        writer.Write(ordered.Length);
        foreach (var value in ordered) write(writer, value);
    }
    private static ImmutableHashSet<T> ReadSet<T>(BinaryReader reader, Func<BinaryReader, T> read) where T : notnull
    {
        var builder = ImmutableHashSet.CreateBuilder<T>();
        var count = Tlaw069C1ArtifactCodec.ReadCount(reader, "set");
        for (var index = 0; index < count; index++) if (!builder.Add(read(reader))) throw new InvalidDataException("C1 set contains a duplicate value.");
        return builder.ToImmutable();
    }

    private static void WriteDictionary<TKey, TValue>(BinaryWriter writer, ImmutableDictionary<TKey, TValue> values, Action<BinaryWriter, TKey> writeKey, Action<BinaryWriter, TValue> writeValue) where TKey : notnull
    {
        var ordered = values.OrderBy(static pair => pair.Key!.ToString(), StringComparer.Ordinal).ToArray();
        writer.Write(ordered.Length);
        foreach (var pair in ordered) { writeKey(writer, pair.Key); writeValue(writer, pair.Value); }
    }
    private static ImmutableDictionary<TKey, TValue> ReadDictionary<TKey, TValue>(BinaryReader reader, Func<BinaryReader, TKey> readKey, Func<BinaryReader, TValue> readValue) where TKey : notnull
    {
        var builder = ImmutableDictionary.CreateBuilder<TKey, TValue>();
        var count = Tlaw069C1ArtifactCodec.ReadCount(reader, "dictionary");
        for (var index = 0; index < count; index++) if (!builder.TryAdd(readKey(reader), readValue(reader))) throw new InvalidDataException("C1 dictionary contains a duplicate key.");
        return builder.ToImmutable();
    }

    private static void WriteId(BinaryWriter writer, ShiftId value) => Tlaw069C1ArtifactCodec.WriteString(writer, value.ToString());
    private static void WriteId(BinaryWriter writer, LogId value) => Tlaw069C1ArtifactCodec.WriteString(writer, value.ToString());
    private static void WriteId(BinaryWriter writer, SpeciesId value) => Tlaw069C1ArtifactCodec.WriteString(writer, value.ToString());
    private static void WriteId(BinaryWriter writer, AnomalyId value) => Tlaw069C1ArtifactCodec.WriteString(writer, value.ToString());
    private static void WriteId(BinaryWriter writer, FlagId value) => Tlaw069C1ArtifactCodec.WriteString(writer, value.ToString());
    private static void WriteId(BinaryWriter writer, ItemId value) => Tlaw069C1ArtifactCodec.WriteString(writer, value.ToString());
    private static void WriteId(BinaryWriter writer, ProfileId value) => Tlaw069C1ArtifactCodec.WriteString(writer, value.ToString());
    private static void WriteId(BinaryWriter writer, EffectEventId value) => Tlaw069C1ArtifactCodec.WriteString(writer, value.ToString());
    private static ShiftId ReadShiftId(BinaryReader reader) => ShiftId.From(Tlaw069C1ArtifactCodec.ReadString(reader));
    private static LogId ReadLogId(BinaryReader reader) => LogId.From(Tlaw069C1ArtifactCodec.ReadString(reader));
    private static SpeciesId ReadSpeciesId(BinaryReader reader) => SpeciesId.From(Tlaw069C1ArtifactCodec.ReadString(reader));
    private static AnomalyId ReadAnomalyId(BinaryReader reader) => AnomalyId.From(Tlaw069C1ArtifactCodec.ReadString(reader));
    private static FlagId ReadFlagId(BinaryReader reader) => FlagId.From(Tlaw069C1ArtifactCodec.ReadString(reader));
    private static ItemId ReadItemId(BinaryReader reader) => ItemId.From(Tlaw069C1ArtifactCodec.ReadString(reader));
    private static ProfileId ReadProfileId(BinaryReader reader) => ProfileId.From(Tlaw069C1ArtifactCodec.ReadString(reader));
    private static EffectEventId ReadEffectEventId(BinaryReader reader) => EffectEventId.From(Tlaw069C1ArtifactCodec.ReadString(reader));
    private static void WriteNullableId(BinaryWriter writer, AnomalyId? value) { writer.Write(value.HasValue); if (value.HasValue) WriteId(writer, value.Value); }
    private static AnomalyId? ReadNullableAnomalyId(BinaryReader reader) => reader.ReadBoolean() ? ReadAnomalyId(reader) : null;
}

internal static class Tlaw069C2GeneratedSourceEmitter
{
    internal static string Emit(ValidatedConfiguration configuration, Tlaw069SourceBinding binding)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(binding);
        var text = new StringBuilder();
        Line(text, "// <auto-generated />");
        Line(text, "// TLAW-069 C2 proof transport only. Regenerate exclusively from the validated canonical YAML result.");
        Line(text, "using System.Collections.Immutable;");
        Line(text, "using TheLogsAreWrong.Domain.Configuration;");
        Line(text, "using TheLogsAreWrong.Domain.Enums;");
        Line(text, "using TheLogsAreWrong.Domain.Identifiers;");
        Line(text, "using TheLogsAreWrong.Domain.Primitives;");
        Line(text, string.Empty);
        Line(text, "namespace TheLogsAreWrong.Gate2.Tests");
        Line(text, "{");
        Line(text, "    public static class Tlaw069GeneratedValidatedConfiguration");
        Line(text, "    {");
        Line(text, "        public const string CandidateId = \"C2\";");
        Line(text, "        public const string ShiftYamlSha256 = \"" + binding.ShiftYamlSha256 + "\";");
        Line(text, "        public const string AnomaliesYamlSha256 = \"" + binding.AnomaliesYamlSha256 + "\";");
        Line(text, "        public const string ValidatorSourceBlob = \"" + binding.ValidatorSourceBlob + "\";");
        Line(text, "        public const string CanonicalProjectionSha256 = \"" + Tlaw069ConfigurationProjection.Sha256(configuration) + "\";");
        Line(text, string.Empty);
        Line(text, "        public static ValidatedConfiguration Create() => " + E(configuration) + ";");
        Line(text, "    }");
        Line(text, "}");
        return text.ToString();
    }

    private static void Line(StringBuilder text, string value) => text.Append(value).Append('\n');
    private static string E(ValidatedConfiguration value) => "new ValidatedConfiguration(" + E(value.Shift) + ", " + E(value.Anomalies) + ")";
    private static string E(ShiftConfiguration value) => "new ShiftConfiguration(" + Id("ShiftId", value.ShiftId) + ", new ShiftSeed(" + value.Seed.Value.ToString(CultureInfo.InvariantCulture) + "), " + Dict(value.Profiles, "ImmutableDictionary<ProfileId, ShiftProfile>", key => Id("ProfileId", key), E) + ", " + E(value.Objectives) + ", " + E(value.Supply) + ", " + E(value.Scheduler) + ", " + E(value.LineNoise) + ", " + E(value.Resources) + ", " + E(value.Containment) + ", " + Array(value.SuccessPredicate, "string", S) + ", " + Array(value.Manifest, "ManifestLogDefinition", E) + ")";
    private static string E(ShiftProfile value) => "new ShiftProfile(" + I(value.IntakeTimeoutSeconds) + ", " + I(value.HardShiftDeadlineSeconds) + ")";
    private static string E(ObjectivesDefinition value) => "new ObjectivesDefinition(" + E(value.Quota) + ", " + I(value.MinCorrectlyProcessedAnomalies) + ")";
    private static string E(QuotaDefinition value) => "new QuotaDefinition(" + I(value.Total) + ", " + Dict(value.BySpecies, "ImmutableDictionary<SpeciesId, int>", key => Id("SpeciesId", key), I) + ")";
    private static string E(SupplyDefinition value) => "new SupplyDefinition(" + I(value.Total) + ", " + I(value.FreeWriteoffBuffer) + ")";
    private static string E(ManifestLogDefinition value) => "new ManifestLogDefinition(" + Id("LogId", value.Id) + ", " + Id("SpeciesId", value.TrueSpecies) + ", " + Id("SpeciesId", value.DeclaredSpecies) + ", " + NullableId("AnomalyId", value.Anomaly) + ")";
    private static string E(SchedulerConfiguration value) => "new SchedulerConfiguration(" + Dict(value.Capacities, "ImmutableDictionary<NodeId, NodeCapacity>", key => Enum(key), E) + ", " + I(value.InitialAdmissionDelaySeconds) + ", " + I(value.NormalFeedDelaySeconds) + ", " + I(value.EarlyFeedDelaySeconds) + ", " + I(value.SawCycleSeconds) + ", " + I(value.RepairHoldSeconds) + ", " + I(value.MovementNoiseSeconds) + ", " + S(value.DefaultTimeoutRoute) + ", " + Array(value.SameTickOrder, "HostTickStage", Enum) + ")";
    private static string E(NodeCapacity value) => value.IsUnlimited ? "NodeCapacity.Unlimited" : "NodeCapacity.Limited(" + I(value.Limit!.Value) + ")";
    private static string E(LineNoiseConfiguration value) => "new LineNoiseConfiguration(" + Array(value.QuietWhenAllInactive, "string", S) + ", " + I(value.PenitentConfirmRequiresContinuousQuietSeconds) + ", " + B(value.ResetTestProgressWhenLoud) + ", " + B(value.PauseIntakeTimerDuringTest) + ")";
    private static string E(ResourcesConfiguration value) => "new ResourcesConfiguration(" + Dict(value.Consumable, "ImmutableDictionary<ItemId, int>", key => Id("ItemId", key), I) + ", " + Set(value.Reusable, "ItemId", item => Id("ItemId", item)) + ")";
    private static string E(ContainmentConfiguration value) => "new ContainmentConfiguration(" + B(value.UnlimitedCapacity) + ", " + I(value.RitualHoldSeconds) + ", " + I(value.ServiceRequestedGraceSeconds) + ", " + I(value.OverdueSeconds) + ", " + Dict(value.IntervalByDangerWeight, "ImmutableDictionary<string, int>", S, I) + ", " + E(value.AfterSuccessfulRitual) + ", " + E(value.PrototypeIncident) + ", " + E(value.PostFactoIntervalInference) + ")";
    private static string E(AfterSuccessfulRitualDefinition value) => "new AfterSuccessfulRitualDefinition(" + Enum(value.ReturnState) + ", " + B(value.RetainLogs) + ", " + B(value.RetainDangerWeight) + ", " + B(value.RestartIntervalFromCurrentWeight) + ")";
    private static string E(PrototypeIncidentDefinition value) => "new PrototypeIncidentDefinition(" + S(value.Type) + ", " + I(value.DurationSeconds) + ", " + B(value.RemainsIncidentUntilRitual) + ", " + B(value.RepeatBeforeResolution) + ")";
    private static string E(PostFactoIntervalInferenceDefinition value) => "new PostFactoIntervalInferenceDefinition(" + B(value.Allowed) + ", " + S(value.Rationale) + ", " + B(value.SeededJitter) + ")";
    private static string E(AnomalyCatalog value) => "new AnomalyCatalog(" + Dict(value.Definitions, "ImmutableDictionary<AnomalyId, AnomalyDefinition>", key => Id("AnomalyId", key), E) + ")";
    private static string E(AnomalyDefinition value) => "new AnomalyDefinition(" + Id("AnomalyId", value.Id) + ", " + I(value.DangerWeight) + ", " + Array(value.InstantClues, "string", S) + ", " + Array(value.ObservedClues, "string", S) + ", " + E(value.ConfirmTest) + ", " + E(value.Processing) + ", " + E(value.Procedure) + ", " + Dict(value.WrongActions, "ImmutableDictionary<ItemId, WrongActionDefinition>", key => Id("ItemId", key), E) + ")";
    private static string E(ConfirmTestDefinition value) => "new ConfirmTestDefinition(" + NullableEnum(value.RequiredLineNoise) + ", " + I(value.DurationSeconds) + ", " + B(value.Continuous) + ", " + NullableBool(value.ResetWhenConditionLost) + ", " + S(value.Result) + ", " + Array(value.Tools, "ItemId", item => Id("ItemId", item)) + ")";
    private static string E(ProcessingDefinition value) => "new ProcessingDefinition(" + Set(value.RequiredFlags, "FlagId", item => Id("FlagId", item)) + ", " + Enum(value.RouteWithoutFlags) + ", " + E(value.OnCorrect) + ", " + E(value.OnIncorrect) + ")";
    private static string E(ProcessingOutcome value) => "new ProcessingOutcome(" + Enum(value.TerminalState) + ", " + E(value.QuotaCredit) + ", " + I(value.CorrectAnomalyDelta) + ", " + Array(value.Effects, "EffectDefinition", E) + ")";
    private static string E(QuotaCreditDefinition value) => "new QuotaCreditDefinition(" + Enum(value.Species) + ", " + I(value.Units) + ")";
    private static string E(EffectDefinition value) => "new EffectDefinition(" + Enum(value.Type) + ", " + Id("EffectEventId", value.Event) + ", " + NullableInt(value.DurationSeconds) + ", " + NullableString(value.Target) + ")";
    private static string E(ProcedureDefinition value) => "new ProcedureDefinition(" + Array(value.Steps, "ProcedureStepDefinition", E) + ", " + Set(value.GrantsFlags, "FlagId", item => Id("FlagId", item)) + ")";
    private static string E(ProcedureStepDefinition value) => "new ProcedureStepDefinition(" + Id("ItemId", value.Item) + ", " + B(value.Consumes) + ", " + NullableInt(value.HoldSeconds) + ")";
    private static string E(WrongActionDefinition value) => "new WrongActionDefinition(" + B(value.LeavesStateUnchanged) + ", " + NullableEnum(value.TerminalState) + ", " + B(value.Consumes) + ", " + Array(value.Effects, "EffectDefinition", E) + ")";

    private static string Dict<TKey, TValue>(ImmutableDictionary<TKey, TValue> values, string type, Func<TKey, string> key, Func<TValue, string> value) where TKey : notnull => values.OrderBy(static pair => pair.Key!.ToString(), StringComparer.Ordinal).Aggregate(type + ".Empty", (text, pair) => text + ".Add(" + key(pair.Key) + ", " + value(pair.Value) + ")");
    private static string Array<T>(ImmutableArray<T> values, string type, Func<T, string> value) => values.IsEmpty ? "ImmutableArray<" + type + ">.Empty" : "ImmutableArray.Create<" + type + ">(" + string.Join(", ", values.Select(value)) + ")";
    private static string Set<T>(ImmutableHashSet<T> values, string type, Func<T, string> value) where T : notnull => values.IsEmpty ? "ImmutableHashSet<" + type + ">.Empty" : values.OrderBy(static item => item!.ToString(), StringComparer.Ordinal).Aggregate("ImmutableHashSet<" + type + ">.Empty", (text, item) => text + ".Add(" + value(item) + ")");
    private static string Id<T>(string type, T value) => type + ".From(" + S(value!.ToString()!) + ")";
    private static string NullableId<T>(string type, T? value) where T : struct => value.HasValue ? Id(type, value.Value) : "null";
    private static string Enum<T>(T value) where T : struct, Enum => typeof(T).Name + "." + (string.Equals(value.ToString(), "lock", StringComparison.Ordinal) ? "@lock" : value.ToString());
    private static string NullableEnum<T>(T? value) where T : struct, Enum => value.HasValue ? Enum(value.Value) : "null";
    private static string NullableInt(int? value) => value.HasValue ? I(value.Value) : "null";
    private static string NullableBool(bool? value) => value.HasValue ? B(value.Value) : "null";
    private static string NullableString(string? value) => value is null ? "null" : S(value);
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string B(bool value) => value ? "true" : "false";
    private static string S(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
}

internal static class Tlaw069ProofPaths
{
    internal static string GeneratedUnityFactoryPath() => Path.Combine(RepositoryRoot(), "unity", "TheLogsAreWrong", "Assets", "Gate2", "Tests", "Editor", "Tlaw069GeneratedValidatedConfiguration.cs");

    private static string RepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "TheLogsAreWrong.sln"))) return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root could not be located for TLAW-069 proof source binding.");
    }
}
