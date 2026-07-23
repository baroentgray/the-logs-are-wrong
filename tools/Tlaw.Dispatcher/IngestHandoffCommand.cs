using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tlaw.AgentProtocol;

namespace Tlaw.Dispatcher;

public static class IngestHandoffCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error) => Run(args, output, error, new SystemLeaseClock());
    internal static int RunForTesting(string[] args, TextWriter output, TextWriter error, ILeaseClock clock) => Run(args, output, error, clock);

    private static int Run(string[] args, TextWriter output, TextWriter error, ILeaseClock clock)
    {
        try
        {
            var options = HandoffIngestionOptions.Parse(args);
            IngestionPathGuard.ValidateOutput(options.OutputPath, options.StorePath, options.TaskPath, options.HandoffPath);
            var registry = PacketSchemaRegistry.Load(Path.Combine(TaskPacketCommand.FindRepositoryRoot(), "docs", "agent", "schemas"));
            var task = Task(options.TaskPath, registry);
            var handoff = Handoff(options.HandoffPath, registry);
            Correlate(task, handoff.Packet);
            var status = options.Reason == "timeout" ? LocalLeaseStatus.Expired : LocalLeaseStatus.Active;
            var decision = handoff.Packet.RequiredString("status") == "ready" ? "reassign" : "human";
            var bytes = HandoffIngestionJson.Write(task, handoff.Packet, Convert.ToHexStringLower(SHA256.HashData(handoff.Bytes)), status, options.Reason, decision);
            FileLeaseStore.WithMatchingLeaseStateGuard(options.StorePath, task.TaskId, task.ClaimedBy, task.ClaimId, status, clock, recheck =>
            {
                HandoffIngestionJson.Validate(bytes);
                recheck();
                TaskPacketCommand.WriteAtomically(options.OutputPath, bytes);
                return 0;
            });
            try
            {
                output.WriteLine($"HANDOFF: {decision}");
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException or NotSupportedException)
            {
                error.WriteLine("FAIL: handoff ingestion record was already published.");
                return 1;
            }

            return 0;
        }
        catch (Exception exception) when (exception is HandoffIngestionException or IngestResultCommandException or LeaseStoreException or IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or DecoderFallbackException or InvalidDataException or JsonException)
        {
            error.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
        catch (Exception)
        {
            error.WriteLine("FAIL: handoff ingestion failed unexpectedly.");
            return 1;
        }
    }

    private static TaskV2Packet Task(string path, PacketSchemaRegistry registry)
    {
        var validation = PacketValidator.Validate(TaskPacketCommand.ReadUtf8(path), registry);
        if (!validation.IsValid || validation.Packet?.Schema != "tlaw.agent-task/v2") throw new HandoffIngestionException("Input task must be valid tlaw.agent-task/v2.");
        var task = TaskV2Packet.From(validation.Packet);
        return task.IsClaimed ? task : throw new HandoffIngestionException("Input task must be fully claimed.");
    }

    private static (ProtocolPacket Packet, byte[] Bytes) Handoff(string path, PacketSchemaRegistry registry)
    {
        var bytes = File.ReadAllBytes(path);
        var validation = PacketValidator.Validate(new UTF8Encoding(false, true).GetString(bytes), registry);
        if (!validation.IsValid || validation.Packet?.Schema != "tlaw.agent-handoff/v2") throw new HandoffIngestionException("Input handoff must be valid tlaw.agent-handoff/v2.");
        return (validation.Packet, bytes);
    }

    private static void Correlate(TaskV2Packet task, ProtocolPacket handoff)
    {
        if (!string.Equals(task.TaskId, handoff.RequiredString("task_id"), StringComparison.Ordinal) ||
            !string.Equals(task.SourceId, handoff.RequiredString("source_id"), StringComparison.Ordinal) ||
            !string.Equals(task.ClaimedBy, handoff.RequiredString("claimed_by"), StringComparison.Ordinal) ||
            !string.Equals(task.ClaimId, handoff.RequiredString("claim_id"), StringComparison.Ordinal) ||
            !string.Equals(task.BaseSha, handoff.RequiredString("base_sha"), StringComparison.Ordinal) ||
            !string.Equals(task.Worktree, handoff.RequiredString("branch"), StringComparison.Ordinal))
        {
            throw new HandoffIngestionException("Handoff identity does not exactly match the claimed task.");
        }
    }
}

public sealed class HandoffIngestionException(string message) : Exception(message);

internal sealed record HandoffIngestionOptions(string TaskPath, string HandoffPath, string StorePath, string Reason, string OutputPath)
{
    internal static HandoffIngestionOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count != 11 || args[0] != "ingest-handoff") throw new HandoffIngestionException("Handoff ingestion requires exactly five named options.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Count; index += 2)
        {
            if (!values.TryAdd(args[index], args[index + 1]) || string.IsNullOrWhiteSpace(args[index + 1])) throw new HandoffIngestionException("Handoff ingestion received an unknown, duplicate, or empty option.");
        }

        if (!values.TryGetValue("--task", out var task) || !values.TryGetValue("--handoff", out var handoff) || !values.TryGetValue("--lease-store", out var store) || !values.TryGetValue("--reason", out var reason) || !values.TryGetValue("--output", out var output) || values.Count != 5 || !Path.IsPathFullyQualified(store) || reason is not ("timeout" or "quota_exhaustion" or "manual_cancel")) throw new HandoffIngestionException("Handoff ingestion options or reason are invalid.");
        return new HandoffIngestionOptions(Path.GetFullPath(task), Path.GetFullPath(handoff), Path.GetFullPath(store), reason, Path.GetFullPath(output));
    }
}

internal sealed record HandoffIngestionRecord(
    string TaskId,
    string SourceId,
    string ClaimedBy,
    string ClaimId,
    string HandoffSha256,
    string HandoffStatus,
    string HeadSha,
    string Branch,
    string LeaseStatus,
    string LeaseAction,
    string ReleaseReason,
    string Decision,
    string NextState,
    bool Blocked)
{
    internal static HandoffIngestionRecord Parse(string path) => Parse(File.ReadAllBytes(path));

    internal static HandoffIngestionRecord Parse(byte[] bytes)
    {
        _ = new UTF8Encoding(false, true).GetString(bytes);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new HandoffIngestionException("Handoff ingestion record must be one JSON object.");

        var names = new HashSet<string>(["schema", "task_id", "source_id", "claimed_by", "claim_id", "handoff_sha256", "handoff_status", "head_sha", "branch", "lease_status", "lease_action", "release_reason", "decision", "next_state", "blocked"], StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!names.Contains(property.Name) || !seen.Add(property.Name)) throw new HandoffIngestionException("Handoff ingestion record contains an unknown or duplicate property.");
        }

        if (seen.Count != names.Count) throw new HandoffIngestionException("Handoff ingestion record is missing a required property.");
        var schema = RequiredString(root, "schema");
        var taskId = RequiredString(root, "task_id");
        var sourceId = RequiredString(root, "source_id");
        var claimedBy = RequiredString(root, "claimed_by");
        var claimId = RequiredString(root, "claim_id");
        var handoffSha256 = RequiredString(root, "handoff_sha256");
        var handoffStatus = RequiredString(root, "handoff_status");
        var headSha = RequiredString(root, "head_sha");
        var branch = RequiredString(root, "branch");
        var leaseStatus = RequiredString(root, "lease_status");
        var leaseAction = RequiredString(root, "lease_action");
        var releaseReason = RequiredString(root, "release_reason");
        var decision = RequiredString(root, "decision");
        var nextState = RequiredString(root, "next_state");
        var blocked = RequiredBoolean(root, "blocked");

        if (!string.Equals(schema, "tlaw.dispatcher-handoff-ingestion/v1", StringComparison.Ordinal) ||
            !IsLowerHex(handoffSha256, 64) ||
            !IsLowerHex(headSha, 40) ||
            headSha == new string('0', 40) ||
            handoffStatus is not ("ready" or "blocked") ||
            leaseStatus is not ("active" or "expired") ||
            !string.Equals(leaseAction, "release_required", StringComparison.Ordinal) ||
            releaseReason is not ("timeout" or "quota_exhaustion" or "manual_cancel") ||
            decision is not ("reassign" or "human") ||
            !string.Equals(nextState, "todo", StringComparison.Ordinal) ||
            (handoffStatus == "ready" && (decision != "reassign" || blocked)) ||
            (handoffStatus == "blocked" && (decision != "human" || !blocked)) ||
            (releaseReason == "timeout" && leaseStatus != "expired") ||
            (releaseReason != "timeout" && leaseStatus != "active"))
        {
            throw new HandoffIngestionException("Handoff ingestion record has an invalid schema, hash, enum, or consistency mapping.");
        }

        return new HandoffIngestionRecord(taskId, sourceId, claimedBy, claimId, handoffSha256, handoffStatus, headSha, branch, leaseStatus, leaseAction, releaseReason, decision, nextState, blocked);
    }

    private static string RequiredString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new HandoffIngestionException($"Handoff ingestion record '{name}' must be a non-empty string.");
    }

    private static bool RequiredBoolean(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw new HandoffIngestionException($"Handoff ingestion record '{name}' must be a boolean.");
    }

    private static bool IsLowerHex(string value, int length) => value.Length == length && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

internal static class HandoffIngestionJson
{
    internal static string Write(TaskV2Packet task, ProtocolPacket handoff, string hash, LocalLeaseStatus status, string reason, string decision)
    {
        var blocked = decision == "human" ? "true" : "false";
        return $"{{\n  \"schema\": \"tlaw.dispatcher-handoff-ingestion/v1\",\n  \"task_id\": {JsonSerializer.Serialize(task.TaskId)},\n  \"source_id\": {JsonSerializer.Serialize(task.SourceId)},\n  \"claimed_by\": {JsonSerializer.Serialize(task.ClaimedBy)},\n  \"claim_id\": {JsonSerializer.Serialize(task.ClaimId)},\n  \"handoff_sha256\": \"{hash}\",\n  \"handoff_status\": {JsonSerializer.Serialize(handoff.RequiredString("status"))},\n  \"head_sha\": {JsonSerializer.Serialize(handoff.RequiredString("head_sha"))},\n  \"branch\": {JsonSerializer.Serialize(task.Worktree)},\n  \"lease_status\": \"{status.ToString().ToLowerInvariant()}\",\n  \"lease_action\": \"release_required\",\n  \"release_reason\": \"{reason}\",\n  \"decision\": \"{decision}\",\n  \"next_state\": \"todo\",\n  \"blocked\": {blocked}\n}}\n";
    }

    internal static void Validate(string text)
    {
        _ = HandoffIngestionRecord.Parse(new UTF8Encoding(false, true).GetBytes(text));
    }
}
