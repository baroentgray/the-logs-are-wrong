using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tlaw.AgentProtocol;

namespace Tlaw.Dispatcher;

public static class FinalizeResultCommand
{
    public static int Run(string[] args, TextWriter standardOutput, TextWriter standardError) => Run(args, standardOutput, standardError, new SystemLeaseClock());
    internal static int RunForTesting(string[] args, TextWriter standardOutput, TextWriter standardError, ILeaseClock clock) => Run(args, standardOutput, standardError, clock);

    private static int Run(string[] args, TextWriter standardOutput, TextWriter standardError, ILeaseClock clock)
    {
        try
        {
            var options = FinalizationOptions.Parse(args);
            IngestionPathGuard.ValidateOutput(options.OutputPath, options.LeaseStorePath, options.TaskPath, options.ResultPath, options.IngestionPath);
            var registry = PacketSchemaRegistry.Load(Path.Combine(TaskPacketCommand.FindRepositoryRoot(), "docs", "agent", "schemas"));
            var task = ReadTask(options.TaskPath, registry);
            var resultBytes = File.ReadAllBytes(options.ResultPath);
            var result = ReadResult(resultBytes, registry);
            var ingestion = IngestionRecord.Parse(options.IngestionPath);
            var hash = Convert.ToHexStringLower(SHA256.HashData(resultBytes));
            var mapping = FinalizationMapping.Create(result);
            Verify(task, result, ingestion, hash);
            var output = FinalizationJson.Write(task, result.RequiredString("status"), hash, mapping);
            FileLeaseStore.WithActiveLeaseGuard(options.LeaseStorePath, task.TaskId, task.ClaimedBy, task.ClaimId, clock, (recheck, release) =>
            {
                recheck();
                release(mapping.Reason);
                try
                {
                    TaskPacketCommand.WriteAtomically(options.OutputPath, output);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
                {
                    throw new FinalizationPublicationException("The exact lease was already released; the finalization record was not published.", exception);
                }

                return 0;
            });
            standardOutput.WriteLine($"FINALIZED: {mapping.ReasonText} -> {mapping.NextState}");
            return 0;
        }
        catch (FinalizationPublicationException exception)
        {
            standardError.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
        catch (Exception exception) when (exception is FinalizationCommandException or IngestResultCommandException or LeaseStoreException or IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or DecoderFallbackException or InvalidDataException or JsonException)
        {
            standardError.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
        catch (Exception)
        {
            standardError.WriteLine("FAIL: result finalization failed unexpectedly.");
            return 1;
        }
    }

    private static TaskV2Packet ReadTask(string path, PacketSchemaRegistry registry)
    {
        var validation = PacketValidator.Validate(TaskPacketCommand.ReadUtf8(path), registry);
        if (!validation.IsValid || validation.Packet is null || !string.Equals(validation.Packet.Schema, "tlaw.agent-task/v2", StringComparison.Ordinal)) throw new FinalizationCommandException("Input task packet must be a valid tlaw.agent-task/v2 packet.");
        var task = TaskV2Packet.From(validation.Packet);
        if (!task.IsClaimed) throw new FinalizationCommandException("Input task packet must be fully claimed before finalization.");
        return task;
    }

    private static ProtocolPacket ReadResult(byte[] bytes, PacketSchemaRegistry registry)
    {
        var yaml = new UTF8Encoding(false, true).GetString(bytes);
        var validation = PacketValidator.Validate(yaml, registry);
        if (!validation.IsValid || validation.Packet is null || !string.Equals(validation.Packet.Schema, "tlaw.agent-result/v1", StringComparison.Ordinal)) throw new FinalizationCommandException("Input result packet must be a valid tlaw.agent-result/v1 packet.");
        return validation.Packet;
    }

    private static void Verify(TaskV2Packet task, ProtocolPacket result, IngestionRecord ingestion, string hash)
    {
        if (!string.Equals(task.TaskId, result.RequiredString("task_id"), StringComparison.Ordinal) || !string.Equals(task.TaskId, ingestion.TaskId, StringComparison.Ordinal) || !string.Equals(task.ClaimedBy, ingestion.ClaimedBy, StringComparison.Ordinal) || !string.Equals(task.ClaimId, ingestion.ClaimId, StringComparison.Ordinal) || !string.Equals(result.RequiredString("status"), ingestion.ResultStatus, StringComparison.Ordinal) || result.RequiredBoolean("human", "required") != ingestion.HumanRequired || !string.Equals(hash, ingestion.ResultSha256, StringComparison.Ordinal))
        {
            throw new FinalizationCommandException("Task, result, and ingestion evidence must agree exactly.");
        }
    }
}

public sealed class FinalizationCommandException(string message) : Exception(message);
public sealed class FinalizationPublicationException(string message, Exception inner) : Exception(message, inner);

internal sealed record FinalizationOptions(string TaskPath, string ResultPath, string IngestionPath, string LeaseStorePath, string OutputPath)
{
    internal static FinalizationOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count != 11 || !string.Equals(args[0], "finalize-result", StringComparison.Ordinal)) throw new FinalizationCommandException("Result finalization command requires exactly five named options.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Count; index += 2)
        {
            if (!values.TryAdd(args[index], args[index + 1]) || string.IsNullOrWhiteSpace(args[index + 1])) throw new FinalizationCommandException("Result finalization command received an unknown, duplicate, or empty option.");
        }
        if (!values.TryGetValue("--task", out var task) || !values.TryGetValue("--result", out var result) || !values.TryGetValue("--ingestion", out var ingestion) || !values.TryGetValue("--lease-store", out var store) || !values.TryGetValue("--output", out var output) || values.Count != 5 || !Path.IsPathFullyQualified(store)) throw new FinalizationCommandException("Result finalization command requires --task, --result, --ingestion, --lease-store, and --output exactly once; lease-store must be absolute.");
        return new(Path.GetFullPath(task), Path.GetFullPath(result), Path.GetFullPath(ingestion), Path.GetFullPath(store), Path.GetFullPath(output));
    }
}

internal sealed record IngestionRecord(string TaskId, string ClaimedBy, string ClaimId, string ResultStatus, string ResultSha256, bool HumanRequired)
{
    internal static IngestionRecord Parse(string path)
    {
        var bytes = File.ReadAllBytes(path);
        _ = new UTF8Encoding(false, true).GetString(bytes);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new FinalizationCommandException("Ingestion record must be a JSON object.");
        var allowed = new HashSet<string>(["schema", "task_id", "claimed_by", "claim_id", "result_status", "result_sha256", "human_required", "projection"], StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject()) if (!allowed.Contains(property.Name) || !seen.Add(property.Name)) throw new FinalizationCommandException("Ingestion record contains an unknown or duplicate property.");
        if (seen.Count != allowed.Count || !string.Equals(String(root, "schema"), "tlaw.dispatcher-ingestion/v1", StringComparison.Ordinal)) throw new FinalizationCommandException("Ingestion record has an invalid schema or missing required field.");
        var hash = String(root, "result_sha256");
        if (hash.Length != 64 || hash.Any(character => !(character is >= '0' and <= '9' or >= 'a' and <= 'f'))) throw new FinalizationCommandException("Ingestion record result_sha256 must be lowercase SHA-256.");
        if (!root.TryGetProperty("human_required", out var human) || human.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new FinalizationCommandException("Ingestion record human_required must be boolean.");
        _ = String(root, "projection");
        return new(String(root, "task_id"), String(root, "claimed_by"), String(root, "claim_id"), String(root, "result_status"), hash, human.GetBoolean());
    }

    private static string String(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new FinalizationCommandException($"Ingestion record '{name}' must be a non-empty string.");
}

internal sealed record FinalizationMapping(LeaseReleaseReason Reason, string ReasonText, string NextState)
{
    internal static FinalizationMapping Create(ProtocolPacket result)
    {
        if (result.RequiredBoolean("human", "required")) throw new FinalizationCommandException("Human-required result finalization must pause without releasing the lease.");
        return result.RequiredString("status") switch
        {
            "success" => new(LeaseReleaseReason.Completion, "completion", "in_review"),
            "failed" => new(LeaseReleaseReason.Error, "error", "todo"),
            _ => throw new FinalizationCommandException("Blocked results must not be finalized automatically.")
        };
    }
}

internal static class FinalizationJson
{
    internal static string Write(TaskV2Packet task, string status, string hash, FinalizationMapping mapping)
    {
        var output = $"{{\n  \"schema\": \"tlaw.dispatcher-finalization/v1\",\n  \"task_id\": {JsonSerializer.Serialize(task.TaskId)},\n  \"claimed_by\": {JsonSerializer.Serialize(task.ClaimedBy)},\n  \"claim_id\": {JsonSerializer.Serialize(task.ClaimId)},\n  \"result_status\": {JsonSerializer.Serialize(status)},\n  \"result_sha256\": {JsonSerializer.Serialize(hash)},\n  \"release_reason\": \"{mapping.ReasonText}\",\n  \"next_state\": \"{mapping.NextState}\"\n}}\n";
        using var document = JsonDocument.Parse(Encoding.UTF8.GetBytes(output));
        if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().Count() != 8)
        {
            throw new InvalidDataException("Finalization record could not be rendered as a closed JSON object.");
        }

        return output;
    }
}
