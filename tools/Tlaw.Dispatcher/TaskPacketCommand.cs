using System.Text;
using System.Text.Json;
using Tlaw.AgentProtocol;

namespace Tlaw.Dispatcher;

public static class TaskPacketCommand
{
    public static int Run(string[] args, TextWriter standardOutput, TextWriter standardError)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        if (!TryParseArguments(args, out var inputPath, out var outputPath))
        {
            standardError.WriteLine("Usage: tlaw-dispatcher packet --input <normalized-input.json> --output <task.yaml>");
            return 2;
        }

        try
        {
            var input = NormalizedTaskInput.Parse(ReadUtf8(inputPath!));
            var schemaRoot = Path.Combine(FindRepositoryRoot(), "docs", "agent", "schemas");
            var yaml = TaskPacketGenerator.Generate(input, PacketSchemaRegistry.Load(schemaRoot));
            WriteAtomically(outputPath!, yaml);
            standardOutput.WriteLine("PASS");
            return 0;
        }
        catch (TaskPacketInputException exception)
        {
            standardError.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or DecoderFallbackException or JsonException or InvalidDataException)
        {
            standardError.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
        catch (Exception)
        {
            standardError.WriteLine("FAIL: packet generation failed unexpectedly.");
            return 1;
        }
    }

    private static bool TryParseArguments(IReadOnlyList<string> args, out string? inputPath, out string? outputPath)
    {
        inputPath = null;
        outputPath = null;
        if (args.Count != 5 || !string.Equals(args[0], "packet", StringComparison.Ordinal) || !string.Equals(args[1], "--input", StringComparison.Ordinal) || !string.Equals(args[3], "--output", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(args[2]) || string.IsNullOrWhiteSpace(args[4]))
        {
            return false;
        }

        inputPath = Path.GetFullPath(args[2]);
        outputPath = Path.GetFullPath(args[4]);
        return true;
    }

    internal static string ReadUtf8(string path) => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(File.ReadAllBytes(path));

    internal static void WriteAtomically(string outputPath, string yaml)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("The output directory does not exist.");
        }

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, yaml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    internal static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found from the current directory.");
    }
}

internal sealed record NormalizedTaskInput(
    string TaskId,
    string SourceId,
    IReadOnlyList<string> Sources,
    string Objective,
    string WorkType,
    string PreferredAgent,
    IReadOnlyList<string> EligibleAgents,
    IReadOnlyList<string> RequiredCapabilities,
    string AutonomyLevel,
    IReadOnlyList<string> ForbiddenOperations,
    string ClaimedBy,
    string ClaimId,
    string ClaimStartedAt,
    string ClaimExpiresAt,
    string BaseSha,
    bool HandoffRequired,
    string Worktree,
    VerificationRequirement Verification,
    DeliveryContract Delivery)
{
    private static readonly IReadOnlySet<string> RootProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "schema", "task_id", "source_id", "sources", "objective", "work_type", "preferred_agent", "eligible_agents", "required_capabilities", "autonomy_level", "forbidden_operations", "claimed_by", "claim_id", "claim_started_at", "claim_expires_at", "base_sha", "handoff_required", "worktree", "verification", "delivery"
    };

    internal static NormalizedTaskInput Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        RequireObject(root, "(root)");
        RejectUnknownProperties(root, RootProperties, "(root)");
        RequireExactValue(root, "schema", "tlaw.dispatcher-input/v1");

        return new NormalizedTaskInput(
            RequiredString(root, "task_id"),
            RequiredString(root, "source_id"),
            RequiredStringArray(root, "sources"),
            RequiredString(root, "objective"),
            RequiredString(root, "work_type"),
            RequiredString(root, "preferred_agent"),
            RequiredStringArray(root, "eligible_agents"),
            RequiredStringArray(root, "required_capabilities"),
            RequiredString(root, "autonomy_level"),
            RequiredStringArray(root, "forbidden_operations"),
            RequiredString(root, "claimed_by"),
            RequiredString(root, "claim_id"),
            RequiredString(root, "claim_started_at"),
            RequiredString(root, "claim_expires_at"),
            RequiredString(root, "base_sha"),
            RequiredBoolean(root, "handoff_required"),
            RequiredString(root, "worktree"),
            ParseVerification(RequiredProperty(root, "verification")),
            ParseDelivery(RequiredProperty(root, "delivery")));
    }

    private static VerificationRequirement ParseVerification(JsonElement element)
    {
        RequireObject(element, "verification");
        RejectUnknownProperties(element, new HashSet<string>(["required", "commands"], StringComparer.Ordinal), "verification");
        return new VerificationRequirement(RequiredBoolean(element, "required"), RequiredStringArray(element, "commands"));
    }

    private static DeliveryContract ParseDelivery(JsonElement element)
    {
        RequireObject(element, "delivery");
        RejectUnknownProperties(element, new HashSet<string>(["branch_required", "draft_pr_required", "merge_forbidden"], StringComparer.Ordinal), "delivery");
        return new DeliveryContract(
            RequiredBoolean(element, "branch_required"),
            RequiredBoolean(element, "draft_pr_required"),
            RequiredBoolean(element, "merge_forbidden"));
    }

    private static JsonElement RequiredProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            throw new TaskPacketInputException($"Required input property '{name}' is missing.");
        }

        return value;
    }

    private static string RequiredString(JsonElement element, string name)
    {
        var value = RequiredProperty(element, name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new TaskPacketInputException($"Input property '{name}' must be a non-empty string.");
        }

        return value.GetString()!;
    }

    private static IReadOnlyList<string> RequiredStringArray(JsonElement element, string name)
    {
        var value = RequiredProperty(element, name);
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
        {
            throw new TaskPacketInputException($"Input property '{name}' must be a non-empty string array.");
        }

        var values = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new TaskPacketInputException($"Input property '{name}' must contain only non-empty strings.");
            }

            values.Add(item.GetString()!);
        }

        return values;
    }

    private static bool RequiredBoolean(JsonElement element, string name)
    {
        var value = RequiredProperty(element, name);
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new TaskPacketInputException($"Input property '{name}' must be a boolean.");
        }

        return value.GetBoolean();
    }

    private static void RequireObject(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new TaskPacketInputException($"Input '{path}' must be an object.");
        }
    }

    private static void RejectUnknownProperties(JsonElement element, IReadOnlySet<string> allowed, string path)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw new TaskPacketInputException($"Input property '{path}.{property.Name}' is not allowed.");
            }
        }
    }

    private static void RequireExactValue(JsonElement element, string name, string expected)
    {
        if (!string.Equals(RequiredString(element, name), expected, StringComparison.Ordinal))
        {
            throw new TaskPacketInputException($"Input property '{name}' must be '{expected}'.");
        }
    }
}

internal sealed record VerificationRequirement(bool Required, IReadOnlyList<string> Commands);

internal sealed record DeliveryContract(bool BranchRequired, bool DraftPrRequired, bool MergeForbidden);

internal sealed record TaskV2Packet(
    string TaskId,
    string SourceId,
    IReadOnlyList<string> Sources,
    string Objective,
    string WorkType,
    string PreferredAgent,
    IReadOnlyList<string> EligibleAgents,
    IReadOnlyList<string> RequiredCapabilities,
    string AutonomyLevel,
    IReadOnlyList<string> ForbiddenOperations,
    string ClaimedBy,
    string ClaimId,
    string ClaimStartedAt,
    string ClaimExpiresAt,
    string BaseSha,
    bool HandoffRequired,
    string Worktree,
    VerificationRequirement Verification,
    DeliveryContract Delivery)
{
    internal static TaskV2Packet From(NormalizedTaskInput input) => new(
        input.TaskId,
        input.SourceId,
        input.Sources,
        input.Objective,
        input.WorkType,
        input.PreferredAgent,
        input.EligibleAgents,
        input.RequiredCapabilities,
        input.AutonomyLevel,
        input.ForbiddenOperations,
        input.ClaimedBy,
        input.ClaimId,
        input.ClaimStartedAt,
        input.ClaimExpiresAt,
        input.BaseSha,
        input.HandoffRequired,
        input.Worktree,
        input.Verification,
        input.Delivery);

    internal static TaskV2Packet From(ProtocolPacket packet) => new(
        packet.RequiredString("task_id"),
        packet.RequiredString("source_id"),
        packet.RequiredStrings("sources"),
        packet.RequiredString("objective"),
        packet.RequiredString("work_type"),
        packet.RequiredString("preferred_agent"),
        packet.RequiredStrings("eligible_agents"),
        packet.RequiredStrings("required_capabilities"),
        packet.RequiredString("autonomy_level"),
        packet.RequiredStrings("forbidden_operations"),
        packet.RequiredString("claimed_by"),
        packet.RequiredString("claim_id"),
        packet.RequiredString("claim_started_at"),
        packet.RequiredString("claim_expires_at"),
        packet.RequiredString("base_sha"),
        packet.RequiredBoolean("handoff_required"),
        packet.RequiredString("worktree"),
        new VerificationRequirement(packet.RequiredBoolean("verification", "required"), packet.RequiredNestedStrings("verification", "commands")),
        new DeliveryContract(
            packet.RequiredBoolean("delivery", "branch_required"),
            packet.RequiredBoolean("delivery", "draft_pr_required"),
            packet.RequiredBoolean("delivery", "merge_forbidden")));

    internal bool IsUnclaimed =>
        string.Equals(ClaimedBy, "unclaimed", StringComparison.Ordinal) &&
        string.Equals(ClaimId, "unclaimed", StringComparison.Ordinal) &&
        string.Equals(ClaimStartedAt, "unclaimed", StringComparison.Ordinal) &&
        string.Equals(ClaimExpiresAt, "unclaimed", StringComparison.Ordinal);

    internal bool IsClaimed =>
        !string.Equals(ClaimedBy, "unclaimed", StringComparison.Ordinal) &&
        !string.Equals(ClaimId, "unclaimed", StringComparison.Ordinal) &&
        !string.Equals(ClaimStartedAt, "unclaimed", StringComparison.Ordinal) &&
        !string.Equals(ClaimExpiresAt, "unclaimed", StringComparison.Ordinal);
}

internal static class TaskPacketGenerator
{
    internal static string Generate(NormalizedTaskInput input, PacketSchemaRegistry registry) => Generate(TaskV2Packet.From(input), registry);

    internal static string Generate(TaskV2Packet input, PacketSchemaRegistry registry)
    {
        var output = new StringBuilder();
        AppendString(output, "schema", "tlaw.agent-task/v2");
        AppendString(output, "task_id", input.TaskId);
        AppendString(output, "source_id", input.SourceId);
        AppendStringSequence(output, "sources", input.Sources);
        AppendString(output, "objective", input.Objective);
        AppendString(output, "work_type", input.WorkType);
        AppendString(output, "preferred_agent", input.PreferredAgent);
        AppendStringSequence(output, "eligible_agents", input.EligibleAgents);
        AppendStringSequence(output, "required_capabilities", input.RequiredCapabilities);
        AppendString(output, "autonomy_level", input.AutonomyLevel);
        AppendStringSequence(output, "forbidden_operations", input.ForbiddenOperations);
        AppendString(output, "claimed_by", input.ClaimedBy);
        AppendString(output, "claim_id", input.ClaimId);
        AppendString(output, "claim_started_at", input.ClaimStartedAt);
        AppendString(output, "claim_expires_at", input.ClaimExpiresAt);
        AppendString(output, "base_sha", input.BaseSha);
        AppendBoolean(output, "handoff_required", input.HandoffRequired);
        AppendString(output, "worktree", input.Worktree);
        AppendLine(output, "verification:");
        AppendBoolean(output, "required", input.Verification.Required, 2);
        AppendStringSequence(output, "commands", input.Verification.Commands, 2);
        AppendLine(output, "delivery:");
        AppendBoolean(output, "branch_required", input.Delivery.BranchRequired, 2);
        AppendBoolean(output, "draft_pr_required", input.Delivery.DraftPrRequired, 2);
        AppendBoolean(output, "merge_forbidden", input.Delivery.MergeForbidden, 2);

        var yaml = output.ToString();
        var validation = PacketValidator.Validate(yaml, registry);
        if (!validation.IsValid)
        {
            var first = validation.Diagnostics.FirstOrDefault();
            throw new TaskPacketInputException(first is null
                ? "Generated task YAML was rejected."
                : $"Generated task YAML was rejected: {first.Code} {first.Path}: {first.Message}");
        }

        return yaml;
    }

    private static void AppendString(StringBuilder output, string name, string value, int indentation = 0)
    {
        AppendIndentation(output, indentation);
        output.Append(name).Append(": ").Append(JsonSerializer.Serialize(value));
        AppendLine(output);
    }

    private static void AppendStringSequence(StringBuilder output, string name, IEnumerable<string> values, int indentation = 0)
    {
        AppendIndentation(output, indentation);
        output.Append(name).Append(':');
        AppendLine(output);
        foreach (var value in values)
        {
            AppendIndentation(output, indentation + 2);
            output.Append("- ").Append(JsonSerializer.Serialize(value));
            AppendLine(output);
        }
    }

    private static void AppendBoolean(StringBuilder output, string name, bool value, int indentation = 0)
    {
        AppendIndentation(output, indentation);
        output.Append(name).Append(": ").Append(value ? "true" : "false");
        AppendLine(output);
    }

    private static void AppendLine(StringBuilder output, string value = "") => output.Append(value).Append('\n');

    private static void AppendIndentation(StringBuilder output, int count) => output.Append(' ', count);
}

internal sealed class TaskPacketInputException(string message) : Exception(message);
