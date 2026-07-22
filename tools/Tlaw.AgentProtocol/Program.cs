using System.Text;

namespace Tlaw.AgentProtocol;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("validate" or "project-result"))
        {
            Console.Error.WriteLine("Usage: tlaw-agent-protocol <validate|project-result> <packet.yaml>");
            return 2;
        }

        try
        {
            var packetPath = Path.GetFullPath(args[1]);
            var schemaRoot = Path.Combine(FindRepositoryRoot(packetPath), "docs", "agent", "schemas");
            var yaml = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(File.ReadAllBytes(packetPath));
            var result = PacketValidator.Validate(yaml, PacketSchemaRegistry.Load(schemaRoot));
            if (!result.IsValid)
            {
                foreach (var diagnostic in result.Diagnostics)
                {
                    Console.Error.WriteLine($"{diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}");
                }

                return 1;
            }

            Console.WriteLine(args[0] == "validate" ? "PASS" : ResultProjector.Project(result.Packet!));
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
