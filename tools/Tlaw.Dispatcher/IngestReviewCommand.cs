using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tlaw.AgentProtocol;

namespace Tlaw.Dispatcher;

public static class IngestReviewCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            var options = ReviewOptions.Parse(args);
            IngestionPathGuard.ValidateOutputWithoutLease(options.OutputPath, options.TaskPath, options.FinalizationPath, options.ReviewPath);
            var registry = PacketSchemaRegistry.Load(Path.Combine(TaskPacketCommand.FindRepositoryRoot(), "docs", "agent", "schemas"));
            var task = ReadTask(options.TaskPath, registry);
            var finalization = ReviewFinalization.Parse(options.FinalizationPath);
            var reviewBytes = File.ReadAllBytes(options.ReviewPath);
            var review = ReadReview(reviewBytes, registry);
            if (!string.Equals(task.TaskId, finalization.TaskId, StringComparison.Ordinal) || !string.Equals(task.ClaimedBy, finalization.ClaimedBy, StringComparison.Ordinal) || !string.Equals(task.ClaimId, finalization.ClaimId, StringComparison.Ordinal) || !string.Equals(task.TaskId, review.RequiredString("task_id"), StringComparison.Ordinal) || !string.Equals(options.ExpectedHead, review.RequiredString("reviewed_head"), StringComparison.Ordinal)) throw new ReviewIngestionException("Task, finalization, and review evidence must agree exactly.");
            var decision = ReviewPolicy.Decide(review);
            var bytes = ReviewDecisionJson.Write(task.TaskId, options.ExpectedHead, Convert.ToHexStringLower(SHA256.HashData(reviewBytes)), review.RequiredString("verdict"), decision);
            TaskPacketCommand.WriteAtomically(options.OutputPath, bytes);
            try
            {
                output.WriteLine($"REVIEW: {decision.Decision}");
            }
            catch (IOException)
            {
                error.WriteLine("FAIL: review decision record was already published.");
                return 1;
            }
            return 0;
        }
        catch (Exception exception) when (exception is ReviewIngestionException or IngestResultCommandException or IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or DecoderFallbackException or InvalidDataException or JsonException)
        {
            error.WriteLine($"FAIL: {exception.Message}"); return 1;
        }
        catch (Exception) { error.WriteLine("FAIL: review ingestion failed unexpectedly."); return 1; }
    }
    private static TaskV2Packet ReadTask(string path, PacketSchemaRegistry registry) { var value = PacketValidator.Validate(TaskPacketCommand.ReadUtf8(path), registry); if (!value.IsValid || value.Packet is null || value.Packet.Schema != "tlaw.agent-task/v2") throw new ReviewIngestionException("Input task must be valid tlaw.agent-task/v2."); var task = TaskV2Packet.From(value.Packet); return task.IsClaimed ? task : throw new ReviewIngestionException("Input task must be fully claimed."); }
    private static ProtocolPacket ReadReview(byte[] bytes, PacketSchemaRegistry registry) { var value = PacketValidator.Validate(new UTF8Encoding(false, true).GetString(bytes), registry); return value.IsValid && value.Packet is not null && value.Packet.Schema == "tlaw.agent-review/v1" ? value.Packet : throw new ReviewIngestionException("Input review must be valid tlaw.agent-review/v1."); }
}

public sealed class ReviewIngestionException(string message) : Exception(message);
internal sealed record ReviewOptions(string TaskPath, string FinalizationPath, string ReviewPath, string ExpectedHead, string OutputPath)
{
    internal static ReviewOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count != 11 || args[0] != "ingest-review") throw new ReviewIngestionException("Review ingestion requires exactly five named options."); var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Count; i += 2) if (!values.TryAdd(args[i], args[i + 1]) || string.IsNullOrWhiteSpace(args[i + 1])) throw new ReviewIngestionException("Review ingestion received an unknown, duplicate, or empty option.");
        if (!values.TryGetValue("--task", out var task) || !values.TryGetValue("--finalization", out var finalization) || !values.TryGetValue("--review", out var review) || !values.TryGetValue("--expected-head", out var head) || !values.TryGetValue("--output", out var output) || values.Count != 5 || head.Length != 40 || head == new string('0', 40) || head.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f'))) throw new ReviewIngestionException("Review ingestion requires exact lowercase expected-head and all named options.");
        return new(Path.GetFullPath(task), Path.GetFullPath(finalization), Path.GetFullPath(review), head, Path.GetFullPath(output));
    }
}
internal sealed record ReviewFinalization(string TaskId, string ClaimedBy, string ClaimId)
{
    internal static ReviewFinalization Parse(string path)
    {
        var bytes = File.ReadAllBytes(path); _ = new UTF8Encoding(false, true).GetString(bytes); using var doc = JsonDocument.Parse(bytes); var root = doc.RootElement; var allowed = new HashSet<string>(["schema", "task_id", "claimed_by", "claim_id", "result_status", "result_sha256", "release_reason", "next_state"]); var seen = new HashSet<string>();
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Any(p => !allowed.Contains(p.Name) || !seen.Add(p.Name)) || seen.Count != 8 || Text(root, "schema") != "tlaw.dispatcher-finalization/v1" || Text(root, "result_status") != "success" || Text(root, "release_reason") != "completion" || Text(root, "next_state") != "in_review") throw new ReviewIngestionException("Finalization must be a closed successful completion record.");
        var hash = Text(root, "result_sha256"); if (hash.Length != 64 || hash.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f'))) throw new ReviewIngestionException("Finalization hash is invalid."); return new(Text(root, "task_id"), Text(root, "claimed_by"), Text(root, "claim_id"));
    }
    private static string Text(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new ReviewIngestionException($"Finalization field '{name}' is invalid.");
}
internal sealed record ReviewDecision(string Highest, int Blocking, string Decision, string NextState);
internal static class ReviewPolicy
{
    internal static ReviewDecision Decide(ProtocolPacket review)
    {
        var rank = new Dictionary<string, int> { ["blocker"] = 5, ["high"] = 4, ["medium"] = 3, ["low"] = 2, ["info"] = 1 }; var severities = review.RequiredObjectStrings("findings", "severity"); if (severities.Any(s => !rank.ContainsKey(s))) throw new ReviewIngestionException("Review contains unsupported severity."); var blocking = severities.Count(s => rank[s] >= 3); var highest = severities.Count == 0 ? "none" : severities.OrderByDescending(s => rank[s]).First();
        return review.RequiredString("verdict") switch { "approve" when blocking == 0 => new(highest, blocking, "merge", "in_review"), "request_changes" when blocking > 0 => new(highest, blocking, "correction", "todo"), "comment" => new(highest, blocking, "human", "in_review"), _ => throw new ReviewIngestionException("Review verdict contradicts severity policy.") };
    }
}
internal static class ReviewDecisionJson
{
    internal static string Write(string task, string head, string hash, string verdict, ReviewDecision decision)
    {
        var text = $"{{\n  \"schema\": \"tlaw.dispatcher-review-decision/v1\",\n  \"task_id\": {JsonSerializer.Serialize(task)},\n  \"reviewed_head\": \"{head}\",\n  \"review_sha256\": \"{hash}\",\n  \"verdict\": {JsonSerializer.Serialize(verdict)},\n  \"highest_severity\": \"{decision.Highest}\",\n  \"blocking_findings\": {decision.Blocking},\n  \"decision\": \"{decision.Decision}\",\n  \"next_state\": \"{decision.NextState}\"\n}}\n";
        using var document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().Count() != 9)
        {
            throw new ReviewIngestionException("Review decision record rendering failed validation.");
        }

        return text;
    }
}
