using System.Security.Cryptography;
using TheLogsAreWrong.Domain.Configuration;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

internal static class Tlaw070TrustedDeployment
{
    internal const string ArtifactSha256 = "94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541";
    internal const string ProjectionSha256 = "4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1";
    internal static ValidatedConfigurationC1SourceBinding Binding { get; } = new(
        "CD08DDFC6F354A1FDDEC7EE751007C95920CDBD26AFA6350A068C350D88277E7",
        "6517C145AD41410131FF50BF691FE9C37FB33E1CB8E065E42ADB97364F4785D7",
        "23651feb72bfa432685f8ef1850648d355baed57");

    internal static byte[] ReadArtifact() => Convert.FromBase64String(File.ReadAllText(PathFor("validated-configuration-c1-v1.base64")).Trim());
    internal static string ReadManifest() => File.ReadAllText(PathFor("validated-configuration-c1-v1.manifest"));

    private static string PathFor(string file) => Path.Combine(RepositoryRoot(), "unity", "TheLogsAreWrong", "Assets", "Gate2", "Configuration", file);
    private static string RepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TheLogsAreWrong.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root could not be located for the TLAW-070 deployment material.");
    }
}
