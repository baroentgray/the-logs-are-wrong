using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tlaw.AgentProtocol;

namespace Tlaw.Dispatcher;

public static class RouteCommand
{
    public static int Run(string[] args, TextWriter standardOutput, TextWriter standardError)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        try
        {
            var options = RouteOptions.Parse(args);
            var task = ReadUnclaimedTask(options.TaskPath);
            var snapshots = AgentSnapshotDocument.Parse(TaskPacketCommand.ReadUtf8(options.AgentsPath));
            var selection = ExecutorSelector.Select(new ExecutorSelectionRequest(
                task.WorkType,
                task.AutonomyLevel,
                task.PreferredAgent,
                task.EligibleAgents,
                task.RequiredCapabilities,
                snapshots,
                options.AvailabilityOverrides,
                options.ExecutorOverride));
            TaskPacketCommand.WriteAtomically(options.OutputPath, SelectionJson.Write(task.TaskId, selection));
            standardOutput.WriteLine($"SELECTED: {selection.SelectedAgent} ({ExecutorSelector.ToWireValue(selection.EffectiveAvailability)})");
            return 0;
        }
        catch (RouteCommandException exception)
        {
            standardError.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
        catch (ExecutorSelectionException exception)
        {
            standardError.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or DecoderFallbackException or InvalidDataException or JsonException)
        {
            standardError.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
        catch (Exception)
        {
            standardError.WriteLine("FAIL: route operation failed unexpectedly.");
            return 1;
        }
    }

    private static TaskV2Packet ReadUnclaimedTask(string taskPath)
    {
        var registry = PacketSchemaRegistry.Load(Path.Combine(TaskPacketCommand.FindRepositoryRoot(), "docs", "agent", "schemas"));
        var validation = PacketValidator.Validate(TaskPacketCommand.ReadUtf8(taskPath), registry);
        if (!validation.IsValid)
        {
            throw new RouteCommandException(DescribeValidationFailure(validation));
        }

        var packet = validation.Packet!;
        if (!string.Equals(packet.Schema, "tlaw.agent-task/v2", StringComparison.Ordinal))
        {
            throw new RouteCommandException("Input task packet must use schema tlaw.agent-task/v2.");
        }

        var task = TaskV2Packet.From(packet);
        if (!task.IsUnclaimed)
        {
            throw new RouteCommandException("Input task packet must be fully unclaimed before routing.");
        }

        return task;
    }

    private static string DescribeValidationFailure(PacketValidationResult validation)
    {
        var diagnostic = validation.Diagnostics.FirstOrDefault();
        return diagnostic is null
            ? "Input task packet was rejected by the protocol validator."
            : $"Input task packet was rejected: {diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}";
    }
}

public sealed class RouteCommandException(string message) : Exception(message);

internal sealed record RouteOptions(
    string TaskPath,
    string AgentsPath,
    string OutputPath,
    string? ExecutorOverride,
    IReadOnlyDictionary<string, ExecutorAvailability> AvailabilityOverrides)
{
    internal static RouteOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || !string.Equals(args[0], "route", StringComparison.Ordinal))
        {
            throw new RouteCommandException("Route command must begin with the exact command name 'route'.");
        }

        var singular = new Dictionary<string, string>(StringComparer.Ordinal);
        var overrides = new Dictionary<string, ExecutorAvailability>(StringComparer.Ordinal);
        for (var index = 1; index < args.Count; index += 2)
        {
            if (index + 1 >= args.Count)
            {
                throw new RouteCommandException("Route command received an incomplete option set.");
            }

            var name = args[index];
            var value = args[index + 1];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new RouteCommandException("Route command received an empty option value.");
            }

            if (string.Equals(name, "--availability-override", StringComparison.Ordinal))
            {
                AddAvailabilityOverride(overrides, value);
                continue;
            }

            if (name is not ("--task" or "--agents" or "--output" or "--executor-override") || !singular.TryAdd(name, value))
            {
                throw new RouteCommandException("Route command received an unknown or duplicate option.");
            }
        }

        if (!singular.TryGetValue("--task", out var task) || !singular.TryGetValue("--agents", out var agents) || !singular.TryGetValue("--output", out var output))
        {
            throw new RouteCommandException("Route command requires --task, --agents, and --output exactly once.");
        }

        return new RouteOptions(
            Path.GetFullPath(task),
            Path.GetFullPath(agents),
            Path.GetFullPath(output),
            singular.GetValueOrDefault("--executor-override"),
            overrides);
    }

    private static void AddAvailabilityOverride(IDictionary<string, ExecutorAvailability> overrides, string value)
    {
        var separator = value.IndexOf('=');
        if (separator <= 0 || separator != value.LastIndexOf('=') || separator == value.Length - 1)
        {
            throw new RouteCommandException("Availability override must use the exact form <agent>=<STATE>.");
        }

        var agent = value[..separator];
        var state = value[(separator + 1)..];
        if (!AgentSnapshotDocument.TryParseAvailability(state, out var availability))
        {
            throw new RouteCommandException("Availability override state must be AVAILABLE, DEGRADED, QUOTA_EXHAUSTED, OFFLINE, or UNKNOWN.");
        }

        if (!overrides.TryAdd(agent, availability))
        {
            throw new RouteCommandException($"Availability override for agent '{agent}' is duplicated.");
        }
    }
}

internal static class AgentSnapshotDocument
{
    private const string Schema = "tlaw.dispatcher-agent-snapshot/v1";
    private static readonly IReadOnlySet<string> RootProperties = new HashSet<string>(StringComparer.Ordinal) { "schema", "agents" };
    private static readonly IReadOnlySet<string> AgentProperties = new HashSet<string>(StringComparer.Ordinal) { "agent", "capabilities", "availability" };
    private static readonly IReadOnlySet<string> KnownAgents = new HashSet<string>(StringComparer.Ordinal) { "codex", "claude", "grok", "local" };
    private static readonly Regex CapabilityPattern = new("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);

    internal static IReadOnlyList<AgentSnapshot> Parse(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        var root = document.RootElement;
        RequireObject(root, "snapshot root");
        RejectDuplicateProperties(root, "snapshot root");
        RejectUnknownProperties(root, RootProperties, "snapshot root");
        RequireExactString(root, "schema", Schema, "snapshot root");

        var agents = RequiredArray(root, "agents", "snapshot root");
        if (agents.GetArrayLength() == 0)
        {
            throw new RouteCommandException("Agent snapshot must contain at least one agent record.");
        }

        var snapshots = new List<AgentSnapshot>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in agents.EnumerateArray())
        {
            RequireObject(entry, "agent record");
            RejectDuplicateProperties(entry, "agent record");
            RejectUnknownProperties(entry, AgentProperties, "agent record");
            var agent = RequiredString(entry, "agent", "agent record");
            if (!KnownAgents.Contains(agent))
            {
                throw new RouteCommandException($"Agent snapshot contains unknown agent '{agent}'.");
            }

            if (!names.Add(agent))
            {
                throw new RouteCommandException($"Agent snapshot contains duplicate agent '{agent}'.");
            }

            var capabilities = ParseCapabilities(entry);
            var availabilityText = RequiredString(entry, "availability", "agent record");
            if (!TryParseAvailability(availabilityText, out var availability))
            {
                throw new RouteCommandException($"Agent snapshot availability '{availabilityText}' is not supported.");
            }

            snapshots.Add(new AgentSnapshot(agent, capabilities, availability));
        }

        return snapshots;
    }

    internal static bool TryParseAvailability(string value, out ExecutorAvailability availability)
    {
        availability = value switch
        {
            "AVAILABLE" => ExecutorAvailability.Available,
            "DEGRADED" => ExecutorAvailability.Degraded,
            "QUOTA_EXHAUSTED" => ExecutorAvailability.QuotaExhausted,
            "OFFLINE" => ExecutorAvailability.Offline,
            "UNKNOWN" => ExecutorAvailability.Unknown,
            _ => default
        };
        return value is "AVAILABLE" or "DEGRADED" or "QUOTA_EXHAUSTED" or "OFFLINE" or "UNKNOWN";
    }

    private static IReadOnlyList<string> ParseCapabilities(JsonElement agent)
    {
        var values = RequiredArray(agent, "capabilities", "agent record");
        if (values.GetArrayLength() == 0)
        {
            throw new RouteCommandException("Agent snapshot capabilities must be non-empty.");
        }

        var capabilities = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()) || !CapabilityPattern.IsMatch(value.GetString()!))
            {
                throw new RouteCommandException("Agent snapshot capability must use the task-v2 capability format.");
            }

            var capability = value.GetString()!;
            if (!seen.Add(capability))
            {
                throw new RouteCommandException($"Agent snapshot contains duplicate capability '{capability}'.");
            }

            capabilities.Add(capability);
        }

        return capabilities;
    }

    private static void RequireObject(JsonElement element, string subject)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new RouteCommandException($"{subject} must be an object.");
        }
    }

    private static JsonElement RequiredArray(JsonElement objectElement, string name, string subject)
    {
        if (!objectElement.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new RouteCommandException($"{subject} property '{name}' must be an array.");
        }

        return value;
    }

    private static string RequiredString(JsonElement objectElement, string name, string subject)
    {
        if (!objectElement.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new RouteCommandException($"{subject} property '{name}' must be a non-empty string.");
        }

        return value.GetString()!;
    }

    private static void RequireExactString(JsonElement objectElement, string name, string expected, string subject)
    {
        if (!string.Equals(RequiredString(objectElement, name, subject), expected, StringComparison.Ordinal))
        {
            throw new RouteCommandException($"{subject} property '{name}' must be '{expected}'.");
        }
    }

    private static void RejectDuplicateProperties(JsonElement objectElement, string subject)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in objectElement.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw new RouteCommandException($"{subject} contains duplicate property '{property.Name}'.");
            }
        }
    }

    private static void RejectUnknownProperties(JsonElement objectElement, IReadOnlySet<string> allowed, string subject)
    {
        foreach (var property in objectElement.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw new RouteCommandException($"{subject} contains unknown property '{property.Name}'.");
            }
        }
    }
}

internal static class SelectionJson
{
    internal static string Write(string taskId, ExecutorSelection selection)
    {
        var output = new StringBuilder();
        AppendLine(output, "{");
        AppendString(output, "schema", "tlaw.dispatcher-selection/v1", trailingComma: true);
        AppendString(output, "task_id", taskId, trailingComma: true);
        AppendString(output, "selected_agent", selection.SelectedAgent, trailingComma: true);
        AppendString(output, "effective_availability", ExecutorSelector.ToWireValue(selection.EffectiveAvailability), trailingComma: true);
        output.Append("  \"executor_override_applied\": ").Append(selection.ExecutorOverrideApplied ? "true" : "false");
        AppendLine(output, ",");
        AppendLine(output, "  \"availability_overrides\": [");
        for (var index = 0; index < selection.AvailabilityOverrides.Count; index++)
        {
            var item = selection.AvailabilityOverrides[index];
            AppendLine(output, "    {");
            AppendString(output, "agent", item.Agent, indentation: 6, trailingComma: true);
            AppendString(output, "original", ExecutorSelector.ToWireValue(item.Original), indentation: 6, trailingComma: true);
            AppendString(output, "effective", ExecutorSelector.ToWireValue(item.Effective), indentation: 6, trailingComma: false);
            output.Append("    }");
            AppendLine(output, index == selection.AvailabilityOverrides.Count - 1 ? string.Empty : ",");
        }

        AppendLine(output, "  ]");
        AppendLine(output, "}");
        return output.ToString();
    }

    private static void AppendString(StringBuilder output, string name, string value, int indentation = 2, bool trailingComma = false)
    {
        output.Append(' ', indentation)
            .Append(JsonSerializer.Serialize(name))
            .Append(": ")
            .Append(JsonSerializer.Serialize(value));
        AppendLine(output, trailingComma ? "," : string.Empty);
    }

    private static void AppendLine(StringBuilder output, string value = "") => output.Append(value).Append('\n');
}
