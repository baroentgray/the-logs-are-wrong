namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>Repository guard for the bounded plain-C# D-024 Gate-3 admission owner.</summary>
public sealed class Tlaw082NetworkAdmissionBufferArchitectureTests
{
    [Fact]
    [Trait("Scope", "TLAW-082")]
    public void One_plain_csharp_admission_owner_consumes_only_resolved_evidence_and_uses_existing_accepted_batch_authority()
    {
        var root = FindRepositoryRoot();
        var sourcePath = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "Admission", "Gate3NetworkIntentAdmissionBuffer.cs");

        Assert.True(File.Exists(sourcePath), "TLAW-082 requires one plain-C# Gate-3 admission and sequencing owner.");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("sealed class Gate3NetworkIntentAdmissionBuffer", source, StringComparison.Ordinal);
        Assert.Contains("Gate3ResolvedNetworkIntentEvidence", source, StringComparison.Ordinal);
        Assert.Contains("Gate3NetworkIntentAdmissionStatus", source, StringComparison.Ordinal);
        Assert.Contains("ServerReceiveSequence.Zero", source, StringComparison.Ordinal);
        Assert.Contains("TryNext", source, StringComparison.Ordinal);
        Assert.Contains("AuthoritativeAcceptedIntent", source, StringComparison.Ordinal);
        Assert.Contains("AcceptedIntentTickBatchFactory.Create", source, StringComparison.Ordinal);
        Assert.Contains("_seenIntentIds", source, StringComparison.Ordinal);
        Assert.Contains("_sealedReceiveTicks", source, StringComparison.Ordinal);
        Assert.Contains("evidence.AuthoritativeReceiveTick", source, StringComparison.Ordinal);

        Assert.All(new[]
        {
            "UnityEngine", "MonoBehaviour", "FishNet", "FishySteamworks", "Steamworks", "Broadcast", "NetworkConnection",
            "Gate3IntentCarrier", "Gate3IntentWire", "TryDecode", "ResolveAuthoritativeActor", "Gate3ActorResolution",
            "Gate2LocalIntentAdmissionAdapter", "HostSession", "HostTick", "StageTwo", "Stage 2", "Rpc", "StartConnection(",
            "StopConnection(", "Snapshot", "Resync", "Reconnect", "Prediction", "EventSequence"
        }, forbidden => Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Scope", "TLAW-082")]
    public void Predecessor_boundaries_remain_unwired_to_the_new_admission_owner()
    {
        var root = FindRepositoryRoot();
        var assets = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate3");
        var predecessorPaths = new[]
        {
            Path.Combine(assets, "ActorResolution", "Gate3ActorResolutionComposition.cs"),
            Path.Combine(assets, "IntentCarrier", "Gate3IntentCarrierIngress.cs"),
            Path.Combine(assets, "ReceiveTick", "Gate3ServerReceiveTickMapping.cs"),
            Path.Combine(assets, "Connection", "Gate3ServerConnectionActorBinding.cs"),
            Path.Combine(assets, "Transport", "Gate3TransportLifecycle.cs")
        };

        Assert.All(predecessorPaths, path =>
            Assert.DoesNotContain("Gate3NetworkIntentAdmissionBuffer", File.ReadAllText(path), StringComparison.Ordinal));
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
