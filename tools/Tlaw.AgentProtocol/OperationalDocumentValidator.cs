namespace Tlaw.AgentProtocol;

public static class OperationalDocumentValidator
{
    public static PacketValidationResult ValidateFrontMatter(string markdown, PacketSchemaRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(registry);

        const string openingDelimiter = "---\n";
        if (!markdown.StartsWith(openingDelimiter, StringComparison.Ordinal))
        {
            return MissingFrontMatter();
        }

        var closingDelimiterIndex = markdown.IndexOf("\n---\n", openingDelimiter.Length, StringComparison.Ordinal);
        if (closingDelimiterIndex < 0)
        {
            return MissingFrontMatter();
        }

        return PacketValidator.Validate(markdown.Substring(openingDelimiter.Length, closingDelimiterIndex - openingDelimiter.Length), registry);
    }

    private static PacketValidationResult MissingFrontMatter() => new(
        null,
        [new PacketDiagnostic("TLAW-OPS-001", "(front-matter)", "Operational documents must begin with a complete YAML front matter block.")]);
}
