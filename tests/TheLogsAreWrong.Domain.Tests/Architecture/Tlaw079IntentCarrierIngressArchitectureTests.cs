namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>Repository guard for the bounded TLAW-079 authenticated FishNet carrier ingress seam.</summary>
public sealed class Tlaw079IntentCarrierIngressArchitectureTests
{
    [Fact]
    [Trait("Scope", "TLAW-079")]
    public void One_authenticated_reliable_carrier_ends_at_decoded_ingress_evidence()
    {
        var root = FindRepositoryRoot();
        var sourcePath = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "IntentCarrier", "Gate3IntentCarrierIngress.cs");
        Assert.True(File.Exists(sourcePath), "TLAW-079 requires the one production authenticated carrier ingress seam.");

        var source = File.ReadAllText(sourcePath);
        Assert.Contains("public struct Gate3IntentCarrierBroadcast : IBroadcast", source, StringComparison.Ordinal);
        Assert.Contains("public byte[] Payload", source, StringComparison.Ordinal);
        Assert.Equal(1, Count(source, "IBroadcast"));
        Assert.Contains("RegisterBroadcast<Gate3IntentCarrierBroadcast>(OnCarrierBroadcast, requireAuthentication: true)", source, StringComparison.Ordinal);
        Assert.Contains("UnregisterBroadcast<Gate3IntentCarrierBroadcast>(OnCarrierBroadcast)", source, StringComparison.Ordinal);
        Assert.Contains("NetworkConnection connection", source, StringComparison.Ordinal);
        Assert.Contains("Channel channel", source, StringComparison.Ordinal);
        Assert.Contains("connection.ClientId", source, StringComparison.Ordinal);
        Assert.Contains("Channel.Reliable", source, StringComparison.Ordinal);
        Assert.Contains("ObserveAuthoritativeServerReceiveTick", source, StringComparison.Ordinal);
        Assert.Contains("_observeAuthoritativeReceiveTick()", source, StringComparison.Ordinal);
        Assert.Contains("Gate3IntentWireV1Codec.TryDecode(payload", source, StringComparison.Ordinal);
        Assert.Contains("Gate3DecodedNetworkIntentEvidence", source, StringComparison.Ordinal);
        Assert.True(
            source.IndexOf("_observeAuthoritativeReceiveTick()", StringComparison.Ordinal)
            < source.IndexOf("Gate3IntentWireV1Codec.TryDecode(payload", StringComparison.Ordinal),
            "TLAW-076 receive tick must be captured before D-023 decode.");

        Assert.All(new[]
        {
            "ResolveAuthoritativeActor", "ActorId", "ACTOR_NOT_BOUND", "ServerReceiveSequence",
            "AuthoritativeAcceptedIntent", "AcceptedIntentTickBatch", "HostSession", ".Broadcast<",
            "StartConnection(", "StopConnection(", "Snapshot", "Resync", "Reconnect", "Prediction"
        }, forbidden => Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal));

        var authoring = File.ReadAllText(Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate2", "Editor", "Gate2BootstrapAuthoring.cs"));
        Assert.Contains("Assets/Gate3/IntentCarrier/Gate3IntentCarrierIngress.cs", authoring, StringComparison.Ordinal);
        Assert.Contains("root.AddComponent<Gate3IntentCarrierIngress>()", authoring, StringComparison.Ordinal);
        Assert.Contains("_networkManager", authoring, StringComparison.Ordinal);
        Assert.Contains("_hostDriver", authoring, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Scope", "TLAW-079")]
    public void Predecessor_owners_and_the_frozen_D023_codec_remain_unchanged_by_carrier_ingress()
    {
        var root = FindRepositoryRoot();
        var assets = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets");
        var carrier = File.ReadAllText(Path.Combine(assets, "Gate3", "IntentCarrier", "Gate3IntentCarrierIngress.cs"));
        var codec = File.ReadAllText(Path.Combine(assets, "Gate3", "IntentWire", "Gate3IntentWireV1Codec.cs"));

        Assert.Contains("Gate3IntentWireV1Codec.TryDecode(payload", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("TryEncode", carrier, StringComparison.Ordinal);
        Assert.Contains("public const ushort SchemaVersion = 1", codec, StringComparison.Ordinal);
        Assert.Contains("public const int MaxPayloadBytes = 2048", codec, StringComparison.Ordinal);
        Assert.Contains("MESSAGE_TOO_LARGE", codec, StringComparison.Ordinal);

        Assert.All(new[]
        {
            Path.Combine(assets, "Gate3", "Transport", "Gate3TransportLifecycle.cs"),
            Path.Combine(assets, "Gate3", "Connection", "Gate3ServerConnectionActorBindingBridge.cs"),
            Path.Combine(assets, "Gate3", "ReceiveTick", "Gate3ServerReceiveTickMapping.cs"),
            Path.Combine(assets, "Gate3", "IntentWire", "Gate3IntentWireV1Codec.cs")
        }, predecessor => Assert.DoesNotContain("Gate3IntentCarrier", File.ReadAllText(predecessor), StringComparison.Ordinal));
    }

    private static int Count(string value, string fragment)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(fragment, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += fragment.Length;
        }

        return count;
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
