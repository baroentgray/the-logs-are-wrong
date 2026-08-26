namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>Repository guard for the TLAW-075 transient Gate-3 identity boundary.</summary>
public sealed class Tlaw075ServerConnectionActorBindingArchitectureTests
{
    [Fact]
    [Trait("Scope", "TLAW-075")]
    public void Gate3_binding_is_one_transport_observed_transient_registry_with_no_gameplay_ingress()
    {
        var root = FindRepositoryRoot();
        var assets = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets");
        var registry = Path.Combine(assets, "Gate3", "Connection", "Gate3ServerConnectionActorBinding.cs");
        var bridge = Path.Combine(assets, "Gate3", "Connection", "Gate3ServerConnectionActorBindingBridge.cs");

        Assert.True(File.Exists(registry), "TLAW-075 requires one explicit Gate-3 server connection-to-actor registry.");
        Assert.True(File.Exists(bridge), "TLAW-075 requires one explicit Gate-3 Fishy binding bridge.");
        var source = File.ReadAllText(registry) + Environment.NewLine + File.ReadAllText(bridge);
        Assert.Contains("sealed class Gate3ServerConnectionActorRegistry", source, StringComparison.Ordinal);
        Assert.Contains("sealed class Gate3ServerConnectionActorBindingBridge", source, StringComparison.Ordinal);
        Assert.Contains("OnRemoteConnectionState", source, StringComparison.Ordinal);
        Assert.Contains("OnServerConnectionState", source, StringComparison.Ordinal);
        Assert.Contains("ActorNotBound", source, StringComparison.Ordinal);

        Assert.All(new[]
        {
            "StartConnection(", "StopConnection(", "HostSession", "HostTickCadence", "HostTickExecutionService",
            "IntentEnvelope", "ServerReceiveSequence", "AuthoritativeAcceptedIntent", "AcceptedIntentTickBatch",
            "Rpc", "Snapshot", "Resync", "Reconnect", "Prediction", "ShiftRuntimeState", "EventSequence"
        }, forbidden => Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Scope", "TLAW-075")]
    public void Existing_transport_owner_configuration_and_authority_boundaries_remain_explicit()
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, "unity", "TheLogsAreWrong");
        var lifecycle = Path.Combine(project, "Assets", "Gate3", "Transport", "Gate3TransportLifecycle.cs");
        var authoring = Path.Combine(project, "Assets", "Gate2", "Editor", "Gate2BootstrapAuthoring.cs");
        var scene = Path.Combine(project, "Assets", "Gate2", "Bootstrap", "Gate2Bootstrap.unity");
        var bridgeMeta = Path.Combine(project, "Assets", "Gate3", "Connection", "Gate3ServerConnectionActorBindingBridge.cs.meta");
        var manifest = Path.Combine(project, "Assets", "Gate2", "Configuration", "validated-configuration-c1-v1.manifest");

        var startStopOwners = Directory.GetFiles(Path.Combine(project, "Assets"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                           && !path.Contains(Path.DirectorySeparatorChar + "FishNet" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("StartConnection(", StringComparison.Ordinal)
                           || File.ReadAllText(path).Contains("StopConnection(", StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { Path.GetFullPath(lifecycle) }, startStopOwners);
        var authoringSource = File.ReadAllText(authoring);
        Assert.Contains("root.AddComponent<Gate3ServerConnectionActorBindingBridge>()", authoringSource, StringComparison.Ordinal);
        Assert.Contains("connectionBindingSerialized.FindProperty(\"_transport\")", authoringSource, StringComparison.Ordinal);
        var sceneSource = File.ReadAllText(scene);
        var bridgeGuid = File.ReadLines(bridgeMeta).Single(line => line.StartsWith("guid: ", StringComparison.Ordinal))["guid: ".Length..];
        Assert.Contains("m_Script: {fileID: 11500000, guid: " + bridgeGuid + ", type: 3}", sceneSource, StringComparison.Ordinal);
        Assert.Contains("_peerToPeer: 1", sceneSource, StringComparison.Ordinal);
        Assert.Contains("artifact_sha256=94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541", File.ReadAllText(manifest), StringComparison.Ordinal);
        Assert.Contains("canonical_projection_sha256=4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1", File.ReadAllText(manifest), StringComparison.Ordinal);
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
