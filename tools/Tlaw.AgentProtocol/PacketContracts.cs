using System.Text.Json.Nodes;

namespace Tlaw.AgentProtocol;

public sealed record PacketDiagnostic(string Code, string Path, string Message);

public sealed record PacketValidationResult(ProtocolPacket? Packet, IReadOnlyList<PacketDiagnostic> Diagnostics)
{
    public bool IsValid => Packet is not null && Diagnostics.Count == 0;
}

public sealed class ProtocolPacket
{
    internal ProtocolPacket(JsonObject root)
    {
        Root = root;
    }

    internal JsonObject Root { get; }

    public string Schema => RequiredString("schema");

    public string ReviewedHead => Schema == "tlaw.agent-review/v1"
        ? RequiredString("reviewed_head")
        : throw new InvalidOperationException("Only review packets contain reviewed-head evidence.");

    public string RequiredString(string name) => Root[name]?.GetValue<string>()
        ?? throw new InvalidOperationException($"Required string '{name}' is missing.");

    public bool RequiredBoolean(string name) => Root[name]?.GetValue<bool>() is bool value
        ? value
        : throw new InvalidOperationException($"Required boolean '{name}' is missing.");

    public bool RequiredBoolean(string objectName, string name) => Root[objectName] is JsonObject nested && nested[name]?.GetValue<bool>() is bool value
        ? value
        : throw new InvalidOperationException($"Required boolean '{objectName}.{name}' is missing.");

    public string RequiredNestedString(string objectName, string name) => Root[objectName] is JsonObject nested && nested[name]?.GetValue<string>() is string value
        ? value
        : throw new InvalidOperationException($"Required string '{objectName}.{name}' is missing.");

    public IReadOnlyList<string> RequiredStrings(string name) => Root[name] is JsonArray values
        ? values.Select(value => value?.GetValue<string>() ?? throw new InvalidOperationException($"Required string '{name}' is missing.")).ToArray()
        : throw new InvalidOperationException($"Required array '{name}' is missing.");

    public IReadOnlyList<string> RequiredNestedStrings(string objectName, string name) => Root[objectName] is JsonObject nested && nested[name] is JsonArray values
        ? values.Select(value => value?.GetValue<string>() ?? throw new InvalidOperationException($"Required string '{objectName}.{name}' is missing.")).ToArray()
        : throw new InvalidOperationException($"Required array '{objectName}.{name}' is missing.");

    public IReadOnlyList<string> RequiredObjectStrings(string arrayName, string propertyName) => Root[arrayName] is JsonArray values
        ? values.Select(value => value is JsonObject item && item[propertyName]?.GetValue<string>() is string text ? text : throw new InvalidOperationException($"Required string '{arrayName}.{propertyName}' is missing.")).ToArray()
        : throw new InvalidOperationException($"Required object array '{arrayName}' is missing.");

    public IReadOnlyList<(string Kind, string Reference)> Evidence() => Root["evidence"] is JsonArray values
        ? values.Select(value => value as JsonObject ?? throw new InvalidOperationException("Evidence entry is not an object."))
            .Select(value => (value["kind"]?.GetValue<string>() ?? throw new InvalidOperationException("Evidence kind is missing."), value["reference"]?.GetValue<string>() ?? throw new InvalidOperationException("Evidence reference is missing.")))
            .ToArray()
        : throw new InvalidOperationException("Evidence is missing.");
}
