using System.Text;

namespace Tlaw.AgentProtocol;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("validate" or "project-result" or "validate-document"))
        {
            Console.Error.WriteLine("Usage: tlaw-agent-protocol <validate|project-result|validate-document> <packet-or-document>");
            return 2;
        }

        try
        {
            var packetPath = Path.GetFullPath(args[1]);
            var schemaRoot = Path.Combine(FindRepositoryRoot(packetPath), "docs", "agent", "schemas");
            var contents = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(File.ReadAllBytes(packetPath));
            var registry = PacketSchemaRegistry.Load(schemaRoot);
            var result = args[0] == "validate-document"
                ? OperationalDocumentValidator.ValidateFrontMatter(contents, registry)
                : PacketValidator.Validate(contents, registry);
            if (!result.IsValid)
            {
                foreach (var diagnostic in result.Diagnostics)
                {
                    Console.Error.WriteLine($"{diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}");
                }

                return 1;
            }

            Console.WriteLine(args[0] is "validate" or "validate-document" ? "PASS" : ResultProjector.Project(result.Packet!));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception is IOException or UnauthorizedAccessException or InvalidDataException or DirectoryNotFoundException or ArgumentException or DecoderFallbackException
                ? $"FAIL: {exception.Message}"
                : "FAIL: validator failed unexpectedly.");
            return 1;
        }
    }

    private static string FindRepositoryRoot(string packetPath)
    {
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(packetPath)!); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
