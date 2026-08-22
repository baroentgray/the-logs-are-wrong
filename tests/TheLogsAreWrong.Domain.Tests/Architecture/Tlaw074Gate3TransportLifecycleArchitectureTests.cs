using System.Security.Cryptography;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>
/// Repository-level TLAW-074 guard. It keeps the opt-in transport lifecycle separate from
/// authoritative gameplay and confines vendor transport control to one TLAW-owned seam.
/// </summary>
public sealed class Tlaw074Gate3TransportLifecycleArchitectureTests
{
    [Fact]
    [Trait("Scope", "TLAW-074")]
    public void Lifecycle_control_is_confined_to_one_transport_only_production_seam()
    {
        var root = FindRepositoryRoot();
        var assets = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets");
        var lifecycle = Path.Combine(assets, "Gate3", "Transport", "Gate3TransportLifecycle.cs");

        Assert.True(File.Exists(lifecycle), "TLAW-074 requires exactly one explicit production transport lifecycle seam.");
        var source = File.ReadAllText(lifecycle);
        Assert.Contains("sealed class Gate3TransportLifecycle", source, StringComparison.Ordinal);
        Assert.Contains("StartConnection(true)", source, StringComparison.Ordinal);
        Assert.Contains("StartConnection(false)", source, StringComparison.Ordinal);
        Assert.Contains("StopConnection(false)", source, StringComparison.Ordinal);
        Assert.Contains("StopConnection(true)", source, StringComparison.Ordinal);
        Assert.Contains("OnServerConnectionState", source, StringComparison.Ordinal);
        Assert.Contains("OnClientConnectionState", source, StringComparison.Ordinal);

        Assert.All(new[]
        {
            "HostSession", "HostTickCadence", "HostTickExecutionService", "ActorId", "IntentEnvelope",
            "SubmitLocalIntent", "NetworkObject", "NetworkBehaviour", "Rpc", "EventId", "StateVersion",
            "EventSequence", "Snapshot", "SteamAPI.Init", "SteamAPI.Shutdown"
        }, forbidden => Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal));

        var ownedCallSites = Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                           && !path.Contains(Path.DirectorySeparatorChar + "FishNet" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("StartConnection(", StringComparison.Ordinal)
                           || File.ReadAllText(path).Contains("StopConnection(", StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { Path.GetFullPath(lifecycle) }, ownedCallSites);
    }

    [Fact]
    [Trait("Scope", "TLAW-074")]
    public void Existing_inert_bootstrap_and_C1_identity_contracts_remain_explicit()
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, "unity", "TheLogsAreWrong");
        var bootstrap = File.ReadAllText(Path.Combine(project, "Assets", "Gate3", "Transport", "Gate3TransportBootstrap.cs"));
        var manifest = File.ReadAllText(Path.Combine(project, "Assets", "Gate2", "Configuration", "validated-configuration-c1-v1.manifest"));

        Assert.Contains("TLAW073_TRANSPORT_INERT", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("StartConnection", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("StopConnection", bootstrap, StringComparison.Ordinal);
        Assert.Contains("artifact_byte_length=2326", manifest, StringComparison.Ordinal);
        Assert.Contains("artifact_sha256=94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541", manifest, StringComparison.Ordinal);
        Assert.Contains("canonical_projection_sha256=4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1", manifest, StringComparison.Ordinal);

        var steamManager = Path.Combine(project, "Assets", "Scripts", "Steamworks.NET", "SteamManager.cs");
        Assert.True(File.Exists(steamManager), "The official Steam runtime manager carried by the accepted Fishy artifact is required for real lifecycle evidence.");
        Assert.Equal("0CB2C43F2DFEA8C8808D1F086CF4281EF33E1724EC560AB250832BFF8AB8401F", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(steamManager))));
    }

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
