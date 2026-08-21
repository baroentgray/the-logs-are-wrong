using System.Diagnostics;
using TheLogsAreWrong.Config.Yaml;
using TheLogsAreWrong.Domain.Configuration;

namespace Tlaw.ValidatedConfig.Export;

public static class Program
{
    public static int Main(string[] args)
    {
        var write = args.SequenceEqual(new[] { "--write" }, StringComparer.Ordinal);
        var check = args.Length == 0 || args.SequenceEqual(new[] { "--check" }, StringComparer.Ordinal);
        if (!write && !check)
        {
            Console.Error.WriteLine("Usage: Tlaw.ValidatedConfig.Export [--check|--write]");
            return 2;
        }

        try
        {
            var root = RepositoryRoot();
            var shift = File.ReadAllText(Path.Combine(root, "data", "shift_p0.yaml"));
            var anomalies = File.ReadAllText(Path.Combine(root, "data", "anomalies.prototype.yaml"));
            var binding = new ValidatedConfigurationC1SourceBinding(
                Sha256(shift),
                Sha256(anomalies),
                GitBlob(root, "src/TheLogsAreWrong.Config.Yaml/YamlConfigurationLoader.cs"));
            var export = ValidatedConfigurationC1Exporter.Export(shift, anomalies, binding);
            var artifactText = Convert.ToBase64String(export.Artifact) + "\n";
            var manifestText = export.Manifest.Serialize();
            var dataDirectory = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate2", "Configuration");
            var artifactPath = Path.Combine(dataDirectory, "validated-configuration-c1-v1.base64");
            var manifestPath = Path.Combine(dataDirectory, "validated-configuration-c1-v1.manifest");

            if (write)
            {
                Directory.CreateDirectory(dataDirectory);
                File.WriteAllText(artifactPath, artifactText);
                File.WriteAllText(manifestPath, manifestText);
                Console.WriteLine("VALIDATED_CONFIG_C1_EXPORT_WRITTEN");
                return 0;
            }

            if (!File.Exists(artifactPath) || !File.Exists(manifestPath) ||
                !string.Equals(File.ReadAllText(artifactPath), artifactText, StringComparison.Ordinal) ||
                !string.Equals(File.ReadAllText(manifestPath), manifestText, StringComparison.Ordinal))
            {
                Console.Error.WriteLine("VALIDATED_CONFIG_C1_EXPORT_STALE");
                return 1;
            }

            Console.WriteLine("VALIDATED_CONFIG_C1_EXPORT_FRESH");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TheLogsAreWrong.sln"))) return directory.FullName;
        throw new InvalidOperationException("Repository root could not be located.");
    }

    private static string GitBlob(string root, string path)
    {
        var info = new ProcessStartInfo("git", "hash-object " + path) { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Git could not be started.");
        var value = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 || value.Length != 40) throw new InvalidOperationException("Loader source identity could not be resolved: " + error);
        return value;
    }

    private static string Sha256(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
}
