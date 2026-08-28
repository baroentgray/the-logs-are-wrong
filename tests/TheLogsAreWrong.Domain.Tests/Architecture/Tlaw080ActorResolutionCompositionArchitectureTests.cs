namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>Repository guard for the bounded TLAW-080 authoritative actor-resolution composition seam.</summary>
public sealed class Tlaw080ActorResolutionCompositionArchitectureTests
{
    [Fact]
    [Trait("Scope", "TLAW-080")]
    public void One_gate3_composition_consumes_decoded_evidence_and_stops_at_resolved_local_evidence()
    {
        var root = FindRepositoryRoot();
        var sourcePath = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "ActorResolution", "Gate3ActorResolutionComposition.cs");

        Assert.True(File.Exists(sourcePath), "TLAW-080 requires one bounded Gate-3 actor-resolution composition seam.");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("sealed class Gate3ActorResolutionProcessor", source, StringComparison.Ordinal);
        Assert.Contains("struct Gate3ResolvedNetworkIntentEvidence", source, StringComparison.Ordinal);
        Assert.Contains("Gate3DecodedNetworkIntentEvidence", source, StringComparison.Ordinal);
        Assert.Contains("_resolveAuthoritativeActor(decoded.ConnectionId, decoded.Envelope.ActorIdHint)", source, StringComparison.Ordinal);
        Assert.Contains("Gate3AuthoritativeActorResolutionStatus", source, StringComparison.Ordinal);
        Assert.Contains("AuthoritativeReceiveTick", source, StringComparison.Ordinal);
        Assert.Contains("AuthoritativeActor", source, StringComparison.Ordinal);

        Assert.All(new[]
        {
            "FishNet", "FishySteamworks", "Steamworks", "Broadcast", "Gate3IntentWire", "TryDecode", "NetworkConnection",
            "BindTrustedServerActor", "Gate3ServerConnectionActorRegistry", "Dictionary<", "ServerReceiveSequence",
            "AuthoritativeAcceptedIntent", "AcceptedIntentTickBatch", "HostSession", "HostTick", "StageTwo", "Stage 2",
            "Rpc", ".Broadcast<", "StartConnection(", "StopConnection(", "Snapshot", "Resync", "Reconnect", "Prediction"
        }, forbidden => Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Scope", "TLAW-080")]
    public void Production_wiring_uses_existing_tlaw079_output_and_tlaw075_resolver_without_widening_predecessors()
    {
        var root = FindRepositoryRoot();
        var assets = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets");
        var carrierPath = Path.Combine(assets, "Gate3", "IntentCarrier", "Gate3IntentCarrierIngress.cs");
        var bindingPath = Path.Combine(assets, "Gate3", "Connection", "Gate3ServerConnectionActorBindingBridge.cs");
        var transportPath = Path.Combine(assets, "Gate3", "Transport", "Gate3TransportLifecycle.cs");
        var tickPath = Path.Combine(assets, "Gate3", "ReceiveTick", "Gate3ServerReceiveTickMapping.cs");
        var codecPath = Path.Combine(assets, "Gate3", "IntentWire", "Gate3IntentWireV1Codec.cs");
        var authoringPath = Path.Combine(assets, "Gate2", "Editor", "Gate2BootstrapAuthoring.cs");
        var scenePath = Path.Combine(assets, "Gate2", "Bootstrap", "Gate2Bootstrap.unity");
        var compositionMetaPath = Path.Combine(assets, "Gate3", "ActorResolution", "Gate3ActorResolutionComposition.cs.meta");

        var carrier = File.ReadAllText(carrierPath);
        Assert.Contains("event Action<Gate3DecodedNetworkIntentEvidence> Decoded", carrier, StringComparison.Ordinal);
        Assert.Contains("Decoded?.Invoke(LastResult.Evidence)", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveAuthoritativeActor", carrier, StringComparison.Ordinal);

        Assert.DoesNotContain("Gate3ActorResolution", File.ReadAllText(bindingPath), StringComparison.Ordinal);
        Assert.DoesNotContain("Gate3ActorResolution", File.ReadAllText(transportPath), StringComparison.Ordinal);
        Assert.DoesNotContain("Gate3ActorResolution", File.ReadAllText(tickPath), StringComparison.Ordinal);
        Assert.DoesNotContain("Gate3ActorResolution", File.ReadAllText(codecPath), StringComparison.Ordinal);

        var authoring = File.ReadAllText(authoringPath);
        Assert.Contains("Assets/Gate3/ActorResolution/Gate3ActorResolutionComposition.cs", authoring, StringComparison.Ordinal);
        Assert.Contains("root.AddComponent<Gate3ActorResolutionComposition>()", authoring, StringComparison.Ordinal);
        Assert.Contains("_carrierIngress", authoring, StringComparison.Ordinal);
        Assert.Contains("_connectionBinding", authoring, StringComparison.Ordinal);

        var guid = File.ReadLines(compositionMetaPath).Single(line => line.StartsWith("guid: ", StringComparison.Ordinal))["guid: ".Length..];
        var scene = File.ReadAllText(scenePath);
        Assert.Contains("m_Script: {fileID: 11500000, guid: " + guid + ", type: 3}", scene, StringComparison.Ordinal);
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
