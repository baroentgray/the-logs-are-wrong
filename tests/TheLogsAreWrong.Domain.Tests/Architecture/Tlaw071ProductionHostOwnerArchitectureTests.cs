using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>Repository-level guard for the one Unity U3 owner/driver without adding Unity semantics to .NET.</summary>
public sealed class Tlaw071ProductionHostOwnerArchitectureTests
{
    [Fact]
    public void Gate2_has_exactly_one_production_host_session_constructor_and_no_unity_side_host_tick_semantics()
    {
        var root = FindRepositoryRoot();
        var gate2 = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate2");
        var productionSources = Directory.GetFiles(gate2, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}Editor{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();
        var sessionConstructors = productionSources
            .Where(path => Regex.IsMatch(File.ReadAllText(path), @"new\s+HostSession\s*\(", RegexOptions.CultureInvariant))
            .ToArray();

        var expectedDriver = Path.Combine(gate2, "Authority", "Gate2ProductionHostDriver.cs");
        Assert.Equal(new[] { expectedDriver }, sessionConstructors);

        var source = File.ReadAllText(expectedDriver);
        Assert.Contains("HostTickCadence", source, StringComparison.Ordinal);
        Assert.Contains("ExecuteTick(tick, input.AcceptedIntents, input.ActiveTools)", source, StringComparison.Ordinal);
        Assert.Contains("RetireNextDueTick()", source, StringComparison.Ordinal);
        Assert.True(source.IndexOf("ExecuteTick(tick, input.AcceptedIntents, input.ActiveTools)", StringComparison.Ordinal) <
                    source.IndexOf("RetireNextDueTick()", StringComparison.Ordinal),
            "The one shared HostSession must execute before cadence retires that due tick.");

        Assert.All(new[]
        {
            "HostTickExecutionService", "YamlConfigurationLoader", "YamlDotNet", "TheLogsAreWrong.Config.Yaml",
            "FishNet", "FishySteamworks", "Steamworks", "NetworkManager", "Rpc", "Socket"
        }, forbidden => Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal));
    }

    [Fact]
    public void Gate2_process_lease_is_identity_only_and_the_imported_plugin_and_c1_material_are_unchanged()
    {
        var root = FindRepositoryRoot();
        var gate2 = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate2");
        var source = File.ReadAllText(Path.Combine(gate2, "Authority", "Gate2ProductionHostDriver.cs"));

        Assert.DoesNotMatch(new Regex(@"(?:private|public|internal)\s+static\s+(?:readonly\s+)?(?:HostSession|HostTickCadence|ValidatedConfiguration|ShiftRuntimeState|QuotaRuntimeState|IAtomicEventJournal)\s+\w+\s*(?:;|=)", RegexOptions.CultureInvariant), source);
        Assert.DoesNotContain("static/global singleton", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("private static Guid? _currentOwnerId", source, StringComparison.Ordinal);

        var pluginDirectory = Path.Combine(gate2, "Plugins", "PortableAuthority");
        var plugins = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[]
        {
            "System.Collections.Immutable.dll",
            "System.Runtime.CompilerServices.Unsafe.dll",
            "TheLogsAreWrong.PortableAuthority.dll"
        }, plugins.Select(Path.GetFileName));
        Assert.Equal("BD1E5DDA62192587B12737CCE9BBBB272FB75C4B309BA173AF2AA7684E2A7085", Sha256(Path.Combine(pluginDirectory, "TheLogsAreWrong.PortableAuthority.dll")));

        var artifactText = File.ReadAllText(Path.Combine(gate2, "Configuration", "validated-configuration-c1-v1.base64")).Trim();
        var artifact = Convert.FromBase64String(artifactText);
        Assert.Equal(2326, artifact.Length);
        Assert.Equal("94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541", Convert.ToHexString(SHA256.HashData(artifact)));
        var manifest = File.ReadAllText(Path.Combine(gate2, "Configuration", "validated-configuration-c1-v1.manifest"));
        Assert.Contains("canonical_projection_sha256=4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1", manifest, StringComparison.Ordinal);
    }

    private static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root containing AGENTS.md was not found.");
    }
}
