using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace Tlaw.AgentProtocol;

public static class PacketValidator
{
    public static PacketValidationResult Validate(string yaml, PacketSchemaRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        ArgumentNullException.ThrowIfNull(registry);

        var diagnostics = new List<PacketDiagnostic>();
        JsonObject? root = ParseSafeYaml(yaml, diagnostics);
        if (root is null)
        {
            return new PacketValidationResult(null, Order(diagnostics));
        }

        if (root["schema"] is not JsonValue schemaValue || !schemaValue.TryGetValue<string>(out var schema) || string.IsNullOrWhiteSpace(schema))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-001", "schema", "Schema version is required."));
            return new PacketValidationResult(null, Order(diagnostics));
        }

        if (!registry.TryGet(schema, out var definition))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-002", "schema", "Schema version is unknown."));
            return new PacketValidationResult(null, Order(diagnostics));
        }

        ValidateSchema(root, definition.RootElement, "", diagnostics);
        ValidateSemantics(root, schema, diagnostics);
        return diagnostics.Count == 0
            ? new PacketValidationResult(new ProtocolPacket(root), [])
            : new PacketValidationResult(null, Order(diagnostics));
    }

    private static JsonObject? ParseSafeYaml(string yaml, ICollection<PacketDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-003", "(document)", "YAML input cannot be empty."));
            return null;
        }

        try
        {
            PreScan(yaml, diagnostics);
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            if (stream.Documents.Count != 1)
            {
                diagnostics.Add(new PacketDiagnostic("TLAW-PKT-004", "(stream)", "Exactly one YAML document is required."));
                return null;
            }

            if (stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                diagnostics.Add(new PacketDiagnostic("TLAW-PKT-005", "(root)", "The packet root must be a mapping."));
                return null;
            }

            return ToJsonObject(root, "(root)", diagnostics);
        }
        catch (YamlException exception)
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-006", "(document)", $"YAML could not be parsed at line {exception.Start.Line + 1}, column {exception.Start.Column + 1}."));
            return null;
        }
        catch (Exception)
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-027", "(document)", "YAML safety scan failed."));
            return null;
        }
    }

    private static void PreScan(string yaml, ICollection<PacketDiagnostic> diagnostics)
    {
        var parser = new Parser(new StringReader(yaml));
        var collections = new Stack<CollectionState>();
        while (parser.MoveNext())
        {
            switch (parser.Current)
            {
                case AnchorAlias alias:
                    diagnostics.Add(new PacketDiagnostic("TLAW-PKT-007", "(node)", $"YAML aliases are not supported at line {alias.Start.Line + 1}."));
                    ConsumeNode(collections);
                    break;
                case NodeEvent nodeEvent when !nodeEvent.Anchor.IsEmpty:
                    diagnostics.Add(new PacketDiagnostic("TLAW-PKT-008", "(node)", $"YAML anchors are not supported at line {nodeEvent.Start.Line + 1}."));
                    HandleNodeEvent(nodeEvent, collections, diagnostics);
                    break;
                case NodeEvent nodeEvent when !nodeEvent.Tag.IsEmpty:
                    diagnostics.Add(new PacketDiagnostic("TLAW-PKT-009", "(node)", $"YAML tags are not supported at line {nodeEvent.Start.Line + 1}."));
                    HandleNodeEvent(nodeEvent, collections, diagnostics);
                    break;
                case Scalar scalar:
                    HandleScalar(scalar, collections, diagnostics);
                    break;
                case MappingStart:
                    StartCollection(isMapping: true, collections);
                    break;
                case MappingEnd:
                    EndCollection(collections, diagnostics);
                    break;
                case SequenceStart:
                    StartCollection(isMapping: false, collections);
                    break;
                case SequenceEnd:
                    EndCollection(collections, diagnostics);
                    break;
            }
        }

        if (collections.Count != 0)
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-028", "(document)", "YAML collection did not terminate."));
        }
    }

    private static void HandleNodeEvent(NodeEvent nodeEvent, Stack<CollectionState> collections, ICollection<PacketDiagnostic> diagnostics)
    {
        if (nodeEvent is Scalar scalar)
        {
            HandleScalar(scalar, collections, diagnostics);
            return;
        }

        if (nodeEvent is MappingStart)
        {
            StartCollection(isMapping: true, collections);
            return;
        }

        if (nodeEvent is SequenceStart)
        {
            StartCollection(isMapping: false, collections);
        }
    }

    private static void HandleScalar(Scalar scalar, Stack<CollectionState> collections, ICollection<PacketDiagnostic> diagnostics)
    {
        if (collections.TryPeek(out var mapping) && mapping.IsMapping && mapping.ExpectsKey)
        {
            var key = scalar.Value ?? string.Empty;
            if (string.Equals(key, "<<", StringComparison.Ordinal))
            {
                diagnostics.Add(new PacketDiagnostic("TLAW-PKT-010", key, "YAML merge keys are not supported."));
            }

            if (!mapping.Keys.Add(key))
            {
                diagnostics.Add(new PacketDiagnostic("TLAW-PKT-011", key, "Duplicate mapping key."));
            }

            mapping.ExpectsKey = false;
            return;
        }

        ConsumeNode(collections);
    }

    private static JsonObject? ToJsonObject(YamlMappingNode mapping, string path, ICollection<PacketDiagnostic> diagnostics)
    {
        var result = new JsonObject();
        foreach (var (rawKey, rawValue) in mapping.Children)
        {
            if (rawKey is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value))
            {
                diagnostics.Add(new PacketDiagnostic("TLAW-PKT-012", path, "Mapping keys must be non-empty strings."));
                continue;
            }

            result[key.Value] = ToJson(rawValue, Join(path, key.Value), diagnostics);
        }

        return result;
    }

    private static JsonNode? ToJson(YamlNode node, string path, ICollection<PacketDiagnostic> diagnostics) => node switch
    {
        YamlMappingNode mapping => ToJsonObject(mapping, path, diagnostics),
        YamlSequenceNode sequence => new JsonArray(sequence.Children.Select(child => ToJson(child, path, diagnostics)).ToArray()),
        YamlScalarNode scalar => ToJsonScalar(scalar),
        _ => AddUnsupportedNode(path, diagnostics)
    };

    private static JsonNode? ToJsonScalar(YamlScalarNode scalar)
    {
        if (scalar.Style == ScalarStyle.Plain && bool.TryParse(scalar.Value, out var boolean))
        {
            return JsonValue.Create(boolean);
        }

        return JsonValue.Create(scalar.Value ?? string.Empty);
    }

    private static JsonNode? AddUnsupportedNode(string path, ICollection<PacketDiagnostic> diagnostics)
    {
        diagnostics.Add(new PacketDiagnostic("TLAW-PKT-013", path, "Unsupported YAML node."));
        return null;
    }

    private static void ValidateSchema(JsonNode? value, JsonElement schema, string path, ICollection<PacketDiagnostic> diagnostics)
    {
        if (schema.TryGetProperty("type", out var type) && !MatchesType(value, type.GetString()))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-014", DisplayPath(path), $"Expected YAML {type.GetString()}."));
            return;
        }

        if (schema.TryGetProperty("const", out var constant) && !JsonEquivalent(value, constant))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-015", DisplayPath(path), "Value does not match the required constant."));
        }

        if (schema.TryGetProperty("enum", out var values) && !values.EnumerateArray().Any(candidate => JsonEquivalent(value, candidate)))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-016", DisplayPath(path), "Value is outside the allowed enum."));
        }

        if (value is JsonValue scalar && scalar.TryGetValue<string>(out var text))
        {
            if (schema.TryGetProperty("minLength", out var minLength) && text.Length < minLength.GetInt32())
            {
                diagnostics.Add(new PacketDiagnostic("TLAW-PKT-017", DisplayPath(path), "String is shorter than the allowed minimum."));
            }

            if (schema.TryGetProperty("maxLength", out var maxLength) && text.Length > maxLength.GetInt32())
            {
                diagnostics.Add(new PacketDiagnostic("TLAW-PKT-018", DisplayPath(path), "String is longer than the allowed maximum."));
            }

            if (schema.TryGetProperty("pattern", out var pattern) && !Regex.IsMatch(text, pattern.GetString()!, RegexOptions.CultureInvariant))
            {
                diagnostics.Add(new PacketDiagnostic("TLAW-PKT-026", DisplayPath(path), "String does not match the required pattern."));
            }
        }

        if (value is JsonArray array)
        {
            if (schema.TryGetProperty("minItems", out var minItems) && array.Count < minItems.GetInt32())
            {
                diagnostics.Add(new PacketDiagnostic("TLAW-PKT-019", DisplayPath(path), "Array has fewer entries than required."));
            }

            if (schema.TryGetProperty("items", out var itemSchema))
            {
                for (var index = 0; index < array.Count; index++)
                {
                    ValidateSchema(array[index], itemSchema, $"{path}[{index}]", diagnostics);
                }
            }
        }

        if (value is JsonObject objectValue)
        {
            if (schema.TryGetProperty("required", out var required))
            {
                foreach (var property in required.EnumerateArray().Select(item => item.GetString()!))
                {
                    if (!objectValue.ContainsKey(property))
                    {
                        diagnostics.Add(new PacketDiagnostic("TLAW-PKT-020", Join(path, property), "Required property is missing."));
                    }
                }
            }

            var properties = schema.TryGetProperty("properties", out var definedProperties) ? definedProperties : default;
            foreach (var property in objectValue)
            {
                if (properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(property.Key, out var propertySchema))
                {
                    ValidateSchema(property.Value, propertySchema, Join(path, property.Key), diagnostics);
                }
                else if (schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.False)
                {
                    diagnostics.Add(new PacketDiagnostic("TLAW-PKT-021", Join(path, property.Key), "Property is not allowed by this schema."));
                }
            }
        }
    }

    private static void ValidateSemantics(JsonObject root, string schema, ICollection<PacketDiagnostic> diagnostics)
    {
        if (schema == "tlaw.agent-task/v2")
        {
            ValidateTaskV2Semantics(root, diagnostics);
            return;
        }

        if (schema is not ("tlaw.agent-result/v1" or "tlaw.agent-review/v1" or "tlaw.agent-handoff/v1"))
        {
            return;
        }

        if (root["human_summary"]?.GetValue<string>() is string summary && NonEmptyLineCount(summary) > 5)
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-022", "human_summary", "Human summary may contain at most five non-empty lines."));
        }

        if (schema != "tlaw.agent-result/v1" || root["human"] is not JsonObject human || human["required"]?.GetValue<bool>() is not true)
        {
            return;
        }

        if (!string.Equals(root["status"]?.GetValue<string>(), "blocked", StringComparison.Ordinal))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-023", "status", "A required human decision must use blocked status."));
        }

        if (human["question"]?.GetValue<string>() is not string question || string.IsNullOrWhiteSpace(question))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-024", "human.question", "A required human decision must provide a question."));
        }

        if (human["safe_options"] is not JsonArray options || options.Count == 0)
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-025", "human.safe_options", "A required human decision must provide safe options."));
        }
    }

    private static void ValidateTaskV2Semantics(JsonObject root, ICollection<PacketDiagnostic> diagnostics)
    {
        var workType = OptionalString(root, "work_type");
        var preferredAgent = OptionalString(root, "preferred_agent");
        var eligibleAgents = OptionalStrings(root, "eligible_agents");
        var autonomyLevel = OptionalString(root, "autonomy_level");

        if (string.Equals(workType, "implementation", StringComparison.Ordinal))
        {
            if (preferredAgent is "local" or "grok")
            {
                diagnostics.Add(new PacketDiagnostic("TLAW-PKT-029", "preferred_agent", "Implementation cannot automatically prefer the local model or Grok."));
            }

            foreach (var eligibleAgent in eligibleAgents.Where(agent => agent is "local" or "grok"))
            {
                diagnostics.Add(new PacketDiagnostic("TLAW-PKT-029", "eligible_agents", "Implementation cannot automatically authorize the local model or Grok."));
            }
        }

        if (preferredAgent is not null && eligibleAgents.Count > 0 && !eligibleAgents.Contains(preferredAgent, StringComparer.Ordinal))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-029", "eligible_agents", "The preferred agent must be eligible for the task."));
        }

        if (eligibleAgents.Contains("local", StringComparer.Ordinal) && !string.Equals(workType, "read_only_analysis", StringComparison.Ordinal))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-031", "eligible_agents", "The local model may only be eligible for read-only analysis."));
        }

        if (eligibleAgents.Contains("local", StringComparer.Ordinal) && !string.Equals(autonomyLevel, "read_only", StringComparison.Ordinal))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-031", "autonomy_level", "The local model may only receive read-only autonomy."));
        }

        var claimFields = new[] { "claimed_by", "claim_id", "claim_started_at", "claim_expires_at" }
            .Select(field => OptionalString(root, field))
            .ToArray();
        if (claimFields.Any(value => value is null))
        {
            return;
        }

        var unclaimedCount = claimFields.Count(value => string.Equals(value, "unclaimed", StringComparison.Ordinal));
        if (unclaimedCount == 4)
        {
            return;
        }

        if (unclaimedCount > 0)
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-030", "claim", "Claim fields must all be 'unclaimed' or all be supplied by one prepared claim snapshot."));
            return;
        }

        var claimedBy = claimFields[0]!;
        var claimId = claimFields[1]!;
        var claimStartedAt = claimFields[2]!;
        var claimExpiresAt = claimFields[3]!;

        if (!eligibleAgents.Contains(claimedBy, StringComparer.Ordinal))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-032", "claimed_by", "A claimed task/v2 packet must identify an eligible agent in claimed_by."));
        }

        if (string.IsNullOrWhiteSpace(claimId) || string.Equals(claimId, "unclaimed", StringComparison.Ordinal))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-032", "claim_id", "A claimed task/v2 packet must use a non-sentinel claim_id."));
        }

        var hasValidStart = TryParseCanonicalUtcTimestamp(claimStartedAt, out var claimStartedAtValue);
        if (!hasValidStart)
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-033", "claim_started_at", "Claim timestamps must use canonical UTC RFC3339 format yyyy-MM-dd'T'HH:mm:ss.fffffffZ."));
        }

        var hasValidExpiry = TryParseCanonicalUtcTimestamp(claimExpiresAt, out var claimExpiresAtValue);
        if (!hasValidExpiry)
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-033", "claim_expires_at", "Claim timestamps must use canonical UTC RFC3339 format yyyy-MM-dd'T'HH:mm:ss.fffffffZ."));
        }

        if (hasValidStart && hasValidExpiry && claimExpiresAtValue <= claimStartedAtValue)
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-034", "claim_expires_at", "claim_expires_at must be strictly later than claim_started_at."));
        }

        if (string.Equals(workType, "implementation", StringComparison.Ordinal) &&
            (string.Equals(claimedBy, "local", StringComparison.Ordinal) || string.Equals(claimedBy, "grok", StringComparison.Ordinal)))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-029", "claimed_by", "Implementation task/v2 packets must not be claimed by local or grok."));
        }
    }

    private static bool TryParseCanonicalUtcTimestamp(string value, out DateTimeOffset timestamp)
    {
        const string format = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

        if (!DateTime.TryParseExact(
                value,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            timestamp = default;
            return false;
        }

        timestamp = new DateTimeOffset(parsed, TimeSpan.Zero);
        return string.Equals(timestamp.ToString(format, CultureInfo.InvariantCulture), value, StringComparison.Ordinal);
    }

    private static string? OptionalString(JsonObject root, string name) => root[name] is JsonValue value && value.TryGetValue<string>(out var text)
        ? text
        : null;

    private static IReadOnlyList<string> OptionalStrings(JsonObject root, string name) => root[name] is JsonArray values
        ? values.Select(value => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : null).Where(text => text is not null).Cast<string>().ToArray()
        : [];

    private static bool MatchesType(JsonNode? value, string? type) => type switch
    {
        "object" => value is JsonObject,
        "array" => value is JsonArray,
        "string" => value is JsonValue scalar && scalar.TryGetValue<string>(out _),
        "boolean" => value is JsonValue scalar && scalar.TryGetValue<bool>(out _),
        _ => false
    };

    private static bool JsonEquivalent(JsonNode? value, JsonElement expected) => expected.ValueKind switch
    {
        JsonValueKind.String => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) && string.Equals(text, expected.GetString(), StringComparison.Ordinal),
        JsonValueKind.True or JsonValueKind.False => value is JsonValue scalar && scalar.TryGetValue<bool>(out var boolean) && boolean == expected.GetBoolean(),
        _ => false
    };

    private static int NonEmptyLineCount(string value) => value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;

    private static string Join(string parent, string child) => string.IsNullOrEmpty(parent) ? child : $"{parent}.{child}";

    private static string DisplayPath(string path) => string.IsNullOrEmpty(path) ? "(root)" : path;

    private static IReadOnlyList<PacketDiagnostic> Order(IEnumerable<PacketDiagnostic> diagnostics) => diagnostics
        .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
        .ThenBy(diagnostic => diagnostic.Path, StringComparer.Ordinal)
        .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
        .ToArray();

    private static void ConsumeNode(Stack<CollectionState> collections)
    {
        if (collections.TryPeek(out var mapping) && mapping.IsMapping && !mapping.ExpectsKey)
        {
            mapping.ExpectsKey = true;
        }
    }

    private static void StartCollection(bool isMapping, Stack<CollectionState> collections)
    {
        ConsumeNode(collections);
        collections.Push(new CollectionState(isMapping));
    }

    private static void EndCollection(Stack<CollectionState> collections, ICollection<PacketDiagnostic> diagnostics)
    {
        if (!collections.TryPop(out _))
        {
            diagnostics.Add(new PacketDiagnostic("TLAW-PKT-028", "(document)", "YAML collection ended without a matching start."));
        }
    }

    private sealed class CollectionState(bool isMapping)
    {
        internal bool IsMapping { get; } = isMapping;
        internal bool ExpectsKey { get; set; } = isMapping;
        internal HashSet<string> Keys { get; } = new(StringComparer.Ordinal);
    }

}
