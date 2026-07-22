using System.Text.Json;

namespace Tlaw.AgentProtocol;

public sealed class PacketSchemaRegistry
{
    private static readonly IReadOnlyDictionary<string, string> FileByIdentifier = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["tlaw.agent-task/v1"] = "task.schema.json",
        ["tlaw.agent-task/v2"] = "task.v2.schema.json",
        ["tlaw.agent-result/v1"] = "result.schema.json",
        ["tlaw.agent-review/v1"] = "review.schema.json",
        ["tlaw.agent-handoff/v1"] = "handoff.schema.json"
    };

    private readonly IReadOnlyDictionary<string, JsonDocument> schemas;

    private PacketSchemaRegistry(IReadOnlyDictionary<string, JsonDocument> schemas)
    {
        this.schemas = schemas;
    }

    public static PacketSchemaRegistry Load(string schemaRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaRoot);

        var loaded = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);
        foreach (var (identifier, fileName) in FileByIdentifier)
        {
            var path = Path.Combine(schemaRoot, fileName);
            using var input = File.OpenRead(path);
            var document = JsonDocument.Parse(input);
            var actualIdentifier = document.RootElement.GetProperty("$id").GetString();
            if (!string.Equals(identifier, actualIdentifier, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Schema '{fileName}' must declare '$id' '{identifier}'.");
            }

            loaded.Add(identifier, JsonDocument.Parse(document.RootElement.GetRawText()));
        }

        return new PacketSchemaRegistry(loaded);
    }

    internal bool TryGet(string schema, out JsonDocument document) => schemas.TryGetValue(schema, out document!);
}
