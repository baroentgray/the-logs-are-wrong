using System.Security.Cryptography;
using System.Text;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>
/// Repository-level TLAW-073 guard. It verifies the accepted Gate-3 transport material without
/// introducing a network runtime dependency into the net10 Domain test assembly.
/// </summary>
public sealed class Tlaw073Gate3TransportBootstrapArchitectureTests
{
    private const string FishNetPackage = "https://github.com/FirstGearGames/FishNet.git?path=Assets/FishNet#4.7.2";
    private const string SteamworksPackage = "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1";
    private const string FishNetCommit = "de19b5d66459f60400ffd0edc443c4da173a01e7";
    private const string SteamworksCommit = "c21a8f0e31c56ae8707130967faf491f7dd7c0d8";
    private const int FishyArtifactBytes = 17_188;
    private const string FishyArtifactSha256 = "5698D16BD29B8B08D35E12A9B817CE69992F70D7C14B64810961691ECD9AFC57";
    private const string FishyImportedTreeSha256 = "FBB559519669296F3E2676FAE011CDD9E9EDC906E5A967D9576E164C34C81C2D";

    [Fact]
    [Trait("Scope", "TLAW-073")]
    public void Accepted_D017_package_identities_and_unmodified_FishySteamworks_release_content_are_materialized()
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, "unity", "TheLogsAreWrong");
        var manifest = File.ReadAllText(Path.Combine(project, "Packages", "manifest.json"));
        var packageLock = File.ReadAllText(Path.Combine(project, "Packages", "packages-lock.json"));
        var acceptance = File.ReadAllText(Path.Combine(root, "docs", "agent", "PACKAGE_PIN_ACCEPTANCE.md"));

        Assert.Contains("\"com.firstgeargames.fishnet\": \"" + FishNetPackage + "\"", manifest, StringComparison.Ordinal);
        Assert.Contains("\"com.rlabrecque.steamworks.net\": \"" + SteamworksPackage + "\"", manifest, StringComparison.Ordinal);
        Assert.Contains("\"source\": \"git\"", packageLock, StringComparison.Ordinal);
        Assert.Contains("\"hash\": \"" + FishNetCommit + "\"", packageLock, StringComparison.Ordinal);
        Assert.Contains("\"hash\": \"" + SteamworksCommit + "\"", packageLock, StringComparison.Ordinal);
        Assert.Contains("FishySteamworks.4.1.1.unitypackage", acceptance, StringComparison.Ordinal);
        Assert.Contains(FishyArtifactSha256, acceptance, StringComparison.Ordinal);
        Assert.Contains(FishyArtifactBytes.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), acceptance, StringComparison.Ordinal);

        var projectVersion = File.ReadAllText(Path.Combine(project, "ProjectSettings", "ProjectVersion.txt"));
        Assert.Contains("m_EditorVersionWithRevision: 6000.3.21f1 (c02631ffc030)", projectVersion, StringComparison.Ordinal);

        var fishyRoot = Path.Combine(project, "Assets", "FishNet", "Plugins", "FishySteamworks");
        Assert.True(Directory.Exists(fishyRoot), "The official FishySteamworks 4.1.1 release contents are required.");
        Assert.Contains("\"name\": \"com.firstgeargames.fishysteamworks\"", File.ReadAllText(Path.Combine(fishyRoot, "package.json")), StringComparison.Ordinal);
        Assert.Contains("\"version\": \"4.1.1\"", File.ReadAllText(Path.Combine(fishyRoot, "package.json")), StringComparison.Ordinal);
        Assert.Equal(FishyImportedTreeSha256, ImportedFishyTreeSha256(fishyRoot));
    }

    [Fact]
    [Trait("Scope", "TLAW-073")]
    public void Transport_bootstrap_is_the_only_network_composition_and_cannot_start_or_join_gameplay()
    {
        var root = FindRepositoryRoot();
        var gate2 = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate2", "Authority");
        var bootstrap = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "Transport", "Gate3TransportBootstrap.cs");
        Assert.True(File.Exists(bootstrap), "TLAW-073 requires one production transport/bootstrap component.");

        var source = File.ReadAllText(bootstrap);
        Assert.Contains("sealed class Gate3TransportBootstrap", source, StringComparison.Ordinal);
        Assert.Contains("NetworkManager", source, StringComparison.Ordinal);
        Assert.Contains("FishySteamworks", source, StringComparison.Ordinal);
        Assert.Contains("_peerToPeer", source, StringComparison.Ordinal);
        Assert.All(new[]
        {
            "HostSession", "HostTickCadence", "HostTickExecutionService", "IntentEnvelope", "ActorId",
            "SubmitLocalIntent", "StartConnection", "StopConnection", "Rpc", "NetworkBehaviour", "NetworkObject",
            "ServerManager", "ClientManager", "SceneManager.LoadScene", "SteamAPI.Init", "SteamAPI.Shutdown"
        }, forbidden => Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal));

        var driver = File.ReadAllText(Path.Combine(gate2, "Gate2ProductionHostDriver.cs"));
        Assert.All(new[] { "FishNet", "FishySteamworks", "Steamworks", "NetworkManager", "Rpc", "Socket" },
            forbidden => Assert.DoesNotContain(forbidden, driver, StringComparison.Ordinal));
    }

    private static string ImportedFishyTreeSha256(string fishyRoot)
    {
        var root = Path.GetFullPath(fishyRoot);
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'), StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(10, files.Length);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files)
        {
            var relative = "Assets/FishNet/Plugins/FishySteamworks/" + Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            hash.AppendData(Encoding.UTF8.GetBytes(relative));
            hash.AppendData([0]);
            hash.AppendData(File.ReadAllBytes(file));
            hash.AppendData([0]);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
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
