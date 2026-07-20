using System.Globalization;
using TheLogsAreWrong.Domain.Configuration.Diagnostics;
using TheLogsAreWrong.Config.Yaml.Dto;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace TheLogsAreWrong.Config.Yaml.Parsing;

internal static class YamlDocumentParser
{
    internal static RawYamlDocument? Parse(string yaml, ConfigurationDocument document, string malformedCode, List<ConfigurationDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            Add(diagnostics, "TLAW-CFG-011", document, "(document)", "YAML input cannot be empty.");
            return null;
        }

        try
        {
            PreScan(yaml, document, diagnostics);
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            if (stream.Documents.Count != 1)
            {
                Add(diagnostics, "TLAW-CFG-010", document, "(stream)", "Exactly one YAML document is required.");
                return null;
            }

            if (stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                Add(diagnostics, "TLAW-CFG-008", document, "(root)", "The root node must be a mapping.");
                return null;
            }

            return new RawYamlDocument(root);
        }
        catch (YamlException exception)
        {
            Add(diagnostics, malformedCode, document, "(document)", "YAML could not be parsed.", checked((int)exception.Start.Line + 1), checked((int)exception.Start.Column + 1));
            return null;
        }
    }

    private static void PreScan(string yaml, ConfigurationDocument document, List<ConfigurationDiagnostic> diagnostics)
    {
        var parser = new Parser(new StringReader(yaml));
        var collections = new Stack<CollectionState>();

        while (parser.MoveNext())
        {
            var current = parser.Current;
            switch (current)
            {
                case AnchorAlias alias:
                    Add(diagnostics, "TLAW-CFG-009", document, "(node)", "YAML aliases are not supported.", checked((int)alias.Start.Line + 1), checked((int)alias.Start.Column + 1));
                    ConsumeNode(collections);
                    break;
                case NodeEvent nodeEvent when !nodeEvent.Anchor.IsEmpty:
                    Add(diagnostics, "TLAW-CFG-009", document, "(node)", "YAML anchors are not supported.", checked((int)nodeEvent.Start.Line + 1), checked((int)nodeEvent.Start.Column + 1));
                    HandleNodeEvent(nodeEvent, collections, diagnostics, document);
                    break;
                case Scalar scalar:
                    if (collections.TryPeek(out var mapping) && mapping.IsMapping && mapping.ExpectsKey)
                    {
                        var key = scalar.Value ?? string.Empty;
                        if (!mapping.Keys.Add(key))
                        {
                            Add(diagnostics, "TLAW-CFG-005", document, key, "Duplicate mapping key.", checked((int)scalar.Start.Line + 1), checked((int)scalar.Start.Column + 1));
                        }

                        mapping.ExpectsKey = false;
                    }
                    else
                    {
                        ConsumeNode(collections);
                    }

                    break;
                case MappingStart mappingStart:
                    ConsumeNode(collections);
                    collections.Push(new CollectionState(true));
                    break;
                case MappingEnd:
                    collections.Pop();
                    break;
                case SequenceStart sequenceStart:
                    ConsumeNode(collections);
                    collections.Push(new CollectionState(false));
                    break;
                case SequenceEnd:
                    collections.Pop();
                    break;
            }
        }
    }

    private static void HandleNodeEvent(NodeEvent nodeEvent, Stack<CollectionState> collections, List<ConfigurationDiagnostic> diagnostics, ConfigurationDocument document)
    {
        switch (nodeEvent)
        {
            case Scalar scalar:
                if (collections.TryPeek(out var mapping) && mapping.IsMapping && mapping.ExpectsKey)
                {
                    var key = scalar.Value ?? string.Empty;
                    if (!mapping.Keys.Add(key))
                    {
                        Add(diagnostics, "TLAW-CFG-005", document, key, "Duplicate mapping key.", checked((int)scalar.Start.Line + 1), checked((int)scalar.Start.Column + 1));
                    }

                    mapping.ExpectsKey = false;
                }
                else
                {
                    ConsumeNode(collections);
                }

                break;
            case MappingStart:
            case SequenceStart:
                ConsumeNode(collections);
                break;
        }
    }

    private static void ConsumeNode(Stack<CollectionState> collections)
    {
        if (collections.TryPeek(out var mapping) && mapping.IsMapping && !mapping.ExpectsKey)
        {
            mapping.ExpectsKey = true;
        }
    }

    private static void Add(List<ConfigurationDiagnostic> diagnostics, string code, ConfigurationDocument document, string path, string message, int? line = null, int? column = null) =>
        diagnostics.Add(new ConfigurationDiagnostic(code, DiagnosticSeverity.Error, document, path, message, line, column));

    private sealed class CollectionState(bool isMapping)
    {
        internal bool IsMapping { get; } = isMapping;
        internal bool ExpectsKey { get; set; } = isMapping;
        internal HashSet<string> Keys { get; } = new(StringComparer.Ordinal);
    }
}
