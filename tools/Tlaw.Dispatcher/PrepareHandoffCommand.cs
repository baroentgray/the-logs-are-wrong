using System.Text;
using System.Text.Json;
using Tlaw.AgentProtocol;

namespace Tlaw.Dispatcher;

public static class PrepareHandoffCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            var options = PrepareHandoffOptions.Parse(args);
            IngestionPathGuard.ValidateOutputWithoutLease(options.OutputPath, options.TaskPath, options.SnapshotPath);
            var registry = PacketSchemaRegistry.Load(Path.Combine(TaskPacketCommand.FindRepositoryRoot(), "docs", "agent", "schemas"));
            var task = ReadClaimedTask(options.TaskPath, registry);
            var snapshot = HandoffSnapshot.Parse(TaskPacketCommand.ReadUtf8(options.SnapshotPath), task.BaseSha);
            var yaml = HandoffYaml.Render(task, snapshot);
            var validation = PacketValidator.Validate(yaml, registry);
            if (!validation.IsValid || validation.Packet?.Schema != "tlaw.agent-handoff/v2") throw new HandoffPreparationException("Generated handoff packet failed protocol validation.");
            TaskPacketCommand.WriteAtomically(options.OutputPath, yaml);
            try { output.WriteLine($"HANDOFF: {snapshot.Status}"); }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException or NotSupportedException)
            { error.WriteLine("FAIL: handoff packet was already published."); return 1; }
            return 0;
        }
        catch (Exception exception) when (exception is HandoffPreparationException or IngestResultCommandException or IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or DecoderFallbackException or InvalidDataException or JsonException)
        { error.WriteLine($"FAIL: {exception.Message}"); return 1; }
        catch (Exception) { error.WriteLine("FAIL: handoff preparation failed unexpectedly."); return 1; }
    }

    private static TaskV2Packet ReadClaimedTask(string path, PacketSchemaRegistry registry)
    {
        var validation = PacketValidator.Validate(TaskPacketCommand.ReadUtf8(path), registry);
        if (!validation.IsValid || validation.Packet?.Schema != "tlaw.agent-task/v2") throw new HandoffPreparationException("Input task must be valid tlaw.agent-task/v2.");
        var task = TaskV2Packet.From(validation.Packet);
        return task.IsClaimed ? task : throw new HandoffPreparationException("Input task must be fully claimed.");
    }
}

public sealed class HandoffPreparationException(string message) : Exception(message);

internal sealed record PrepareHandoffOptions(string TaskPath, string SnapshotPath, string OutputPath)
{
    internal static PrepareHandoffOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count != 7 || args[0] != "prepare-handoff") throw new HandoffPreparationException("Handoff preparation requires exactly three named options.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Count; index += 2) if (!values.TryAdd(args[index], args[index + 1]) || string.IsNullOrWhiteSpace(args[index + 1])) throw new HandoffPreparationException("Handoff preparation received an unknown, duplicate, or empty option.");
        if (!values.TryGetValue("--task", out var task) || !values.TryGetValue("--snapshot", out var snapshot) || !values.TryGetValue("--output", out var output) || values.Count != 3) throw new HandoffPreparationException("Handoff preparation requires --task, --snapshot, and --output exactly once.");
        return new(Path.GetFullPath(task), Path.GetFullPath(snapshot), Path.GetFullPath(output));
    }
}

internal sealed record HandoffCommand(string Command, int ExitCode);
internal sealed record HandoffEvidence(string Kind, string Reference);
internal sealed record HandoffSnapshot(string Status, string HeadSha, IReadOnlyList<string> Commits, IReadOnlyList<string> CompletedWork, IReadOnlyList<string> RemainingWork, IReadOnlyList<string> FilesChanged, IReadOnlyList<HandoffCommand> Commands, IReadOnlyList<HandoffEvidence> Evidence, IReadOnlyList<string> KnownFailures, IReadOnlyList<string> OpenQuestions, string HumanSummary, string NextAction)
{
    private static readonly HashSet<string> Required = new(StringComparer.Ordinal) { "schema", "status", "head_sha", "commits", "completed_work", "remaining_work", "files_changed", "commands_run", "evidence", "known_failures", "open_questions", "human_summary", "next_action" };

    internal static HandoffSnapshot Parse(string json, string baseSha)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new HandoffPreparationException("Handoff snapshot root must be an object.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (root.EnumerateObject().Any(property => !Required.Contains(property.Name) || !seen.Add(property.Name)) || seen.Count != Required.Count) throw new HandoffPreparationException("Handoff snapshot properties must be closed and complete.");
        if (Text(root, "schema") != "tlaw.dispatcher-handoff-input/v1") throw new HandoffPreparationException("Handoff snapshot schema is invalid.");
        var status = Text(root, "status"); if (status is not ("ready" or "blocked")) throw new HandoffPreparationException("Handoff snapshot status is invalid.");
        var head = Sha(Text(root, "head_sha"), "head_sha");
        var commits = Strings(root, "commits", allowEmpty: true); if (commits.Any(value => Sha(value, "commits") != value) || commits.Distinct(StringComparer.Ordinal).Count() != commits.Count || (commits.Count == 0 ? head != baseSha : commits[^1] != head)) throw new HandoffPreparationException("Handoff snapshot commit evidence is inconsistent.");
        var completed = Strings(root, "completed_work", true); var remaining = Strings(root, "remaining_work", false); var files = Strings(root, "files_changed", true); var known = Strings(root, "known_failures", true); var questions = Strings(root, "open_questions", true);
        if (new[] { completed, remaining, files, known, questions }.Any(values => values.Distinct(StringComparer.Ordinal).Count() != values.Count)) throw new HandoffPreparationException("Handoff snapshot arrays must not contain duplicates.");
        if (files.Any(path => !RepositoryPath(path))) throw new HandoffPreparationException("Handoff snapshot changed file path is invalid.");
        if (status == "blocked" && known.Count == 0 && questions.Count == 0) throw new HandoffPreparationException("Blocked handoff requires a known failure or open question.");
        var summary = Text(root, "human_summary"); if (summary.Contains('\0') || summary.Contains('\r') || summary.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length is < 1 or > 5) throw new HandoffPreparationException("Handoff snapshot human_summary is invalid.");
        return new(status, head, commits, completed, remaining, files, ParseCommands(root), ParseEvidence(root), known, questions, summary, Text(root, "next_action"));
    }

    private static string Text(JsonElement objectValue, string name) => objectValue.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new HandoffPreparationException($"Handoff snapshot '{name}' must be a non-empty string.");
    private static IReadOnlyList<string> Strings(JsonElement root, string name, bool allowEmpty)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) throw new HandoffPreparationException($"Handoff snapshot '{name}' must be an array.");
        var result = value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()) ? item.GetString()! : throw new HandoffPreparationException($"Handoff snapshot '{name}' has an invalid entry.")).ToArray();
        if (!allowEmpty && result.Length == 0) throw new HandoffPreparationException($"Handoff snapshot '{name}' cannot be empty."); return result;
    }
    private static IReadOnlyList<HandoffCommand> ParseCommands(JsonElement root)
    {
        if (!root.TryGetProperty("commands_run", out var value) || value.ValueKind != JsonValueKind.Array) throw new HandoffPreparationException("Handoff snapshot commands_run must be an array.");
        return value.EnumerateArray().Select(item => { Closed(item, ["command", "exit_code"], "commands_run"); return new HandoffCommand(Text(item, "command"), item.TryGetProperty("exit_code", out var code) && code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out var number) ? number : throw new HandoffPreparationException("Handoff snapshot exit_code must be an integer.")); }).ToArray();
    }
    private static IReadOnlyList<HandoffEvidence> ParseEvidence(JsonElement root)
    {
        if (!root.TryGetProperty("evidence", out var value) || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0) throw new HandoffPreparationException("Handoff snapshot evidence must be a non-empty array.");
        return value.EnumerateArray().Select(item => { Closed(item, ["kind", "reference"], "evidence"); var kind = Text(item, "kind"); if (kind is not ("command" or "source" or "file" or "ci")) throw new HandoffPreparationException("Handoff snapshot evidence kind is invalid."); return new HandoffEvidence(kind, Text(item, "reference")); }).ToArray();
    }
    private static void Closed(JsonElement item, string[] names, string path) { if (item.ValueKind != JsonValueKind.Object || item.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != names.Length || item.EnumerateObject().Any(property => !names.Contains(property.Name, StringComparer.Ordinal)) || names.Any(name => !item.TryGetProperty(name, out _))) throw new HandoffPreparationException($"Handoff snapshot {path} object is invalid."); }
    private static string Sha(string value, string name) { if (value.Length != 40 || value == new string('0', 40) || value.Any(character => !(character is >= '0' and <= '9' or >= 'a' and <= 'f'))) throw new HandoffPreparationException($"Handoff snapshot {name} must be a lowercase non-sentinel SHA."); return value; }
    private static bool RepositoryPath(string value) => !value.Contains('\\') && !value.Contains('\0') && !value.StartsWith("/", StringComparison.Ordinal) && !value.StartsWith("//", StringComparison.Ordinal) && !System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Za-z]:", System.Text.RegularExpressions.RegexOptions.CultureInvariant) && !value.EndsWith("/", StringComparison.Ordinal) && value.Split('/').All(segment => segment.Length > 0 && segment is not "." and not "..");
}

internal static class HandoffYaml
{
    internal static string Render(TaskV2Packet task, HandoffSnapshot snapshot)
    {
        var writer = new StringBuilder(); void Scalar(string name, string value) => writer.Append(name).Append(": ").Append(JsonSerializer.Serialize(value)).Append('\n'); void Strings(string name, IReadOnlyList<string> values) { writer.Append(name).Append(":"); if (values.Count == 0) writer.Append(" []\n"); else { writer.Append('\n'); foreach (var value in values) writer.Append("  - ").Append(JsonSerializer.Serialize(value)).Append('\n'); } }
        Scalar("schema", "tlaw.agent-handoff/v2"); Scalar("task_id", task.TaskId); Scalar("source_id", task.SourceId); Scalar("status", snapshot.Status); Scalar("claimed_by", task.ClaimedBy); Scalar("claim_id", task.ClaimId); Scalar("base_sha", task.BaseSha); Scalar("head_sha", snapshot.HeadSha); Scalar("branch", task.Worktree); Strings("commits", snapshot.Commits); Strings("completed_work", snapshot.CompletedWork); Strings("remaining_work", snapshot.RemainingWork); Strings("files_changed", snapshot.FilesChanged);
        writer.Append("commands_run:"); if (snapshot.Commands.Count == 0) writer.Append(" []\n"); else foreach (var command in snapshot.Commands) writer.Append("\n  - command: ").Append(JsonSerializer.Serialize(command.Command)).Append("\n    exit_code: ").Append(command.ExitCode).Append('\n');
        writer.Append("evidence:\n"); foreach (var evidence in snapshot.Evidence) writer.Append("  - kind: ").Append(JsonSerializer.Serialize(evidence.Kind)).Append("\n    reference: ").Append(JsonSerializer.Serialize(evidence.Reference)).Append('\n');
        Strings("known_failures", snapshot.KnownFailures); Strings("open_questions", snapshot.OpenQuestions); Scalar("human_summary", snapshot.HumanSummary); Scalar("next_action", snapshot.NextAction); return writer.ToString();
    }
}
