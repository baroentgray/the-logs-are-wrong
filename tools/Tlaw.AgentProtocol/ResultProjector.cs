namespace Tlaw.AgentProtocol;

public static class ResultProjector
{
    public static string Project(ProtocolPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (!string.Equals(packet.Schema, "tlaw.agent-result/v1", StringComparison.Ordinal))
        {
            throw new ArgumentException("Only result packets can be projected.", nameof(packet));
        }

        var summary = packet.RequiredString("human_summary").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (packet.RequiredBoolean("human", "required"))
        {
            return string.Join('\n', summary
                .Concat([$"Question: {packet.RequiredNestedString("human", "question")}"])
                .Concat(packet.Evidence().Select(evidence => $"Evidence: {evidence.Reference}"))
                .Concat(packet.RequiredNestedStrings("human", "safe_options").Select(option => $"Option: {option}")));
        }

        return string.Join('\n', [$"{packet.RequiredString("status").ToUpperInvariant()}", ..summary]);
    }
}
