namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>Repository guard for the bounded TLAW-076 server receive-time mapping seam.</summary>
public sealed class Tlaw076NetworkReceiveTickArchitectureTests
{
    [Fact]
    [Trait("Scope", "TLAW-076")]
    public void Receive_tick_mapper_is_elapsed_time_only_and_contains_no_network_gameplay_ingress()
    {
        var root = FindRepositoryRoot();
        var mapper = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "ReceiveTick", "Gate3ServerReceiveTickMapping.cs");

        Assert.True(File.Exists(mapper), "TLAW-076 requires one bounded authoritative receive-tick mapper.");
        var source = File.ReadAllText(mapper);
        Assert.Contains("static class Gate3ServerReceiveTickMapper", source, StringComparison.Ordinal);
        Assert.Contains("elapsedMilliseconds.Value == 0", source, StringComparison.Ordinal);
        Assert.Contains("checked((elapsedMilliseconds.Value - 1) / MillisecondsPerServerTick)", source, StringComparison.Ordinal);

        Assert.All(new[]
        {
            "FishNet", "FishySteamworks", "Steamworks", "Rpc", "Broadcast", "NetworkManager", "Connection",
            "ActorId", "IntentEnvelope", "IIntentParameters", "ServerReceiveSequence", "AuthoritativeAcceptedIntent",
            "AcceptedIntentTickBatch", "HostSession", "HostTickCadence", "HostTickExecutionService", "Snapshot",
            "Resync", "Reconnect", "Prediction", "StartConnection(", "StopConnection("
        }, forbidden => Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Scope", "TLAW-076")]
    public void Production_owner_uses_its_existing_elapsed_source_without_widening_transport_or_admission_authority()
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, "unity", "TheLogsAreWrong");
        var driver = Path.Combine(project, "Assets", "Gate2", "Authority", "Gate2ProductionHostDriver.cs");
        var lifecycle = Path.Combine(project, "Assets", "Gate3", "Transport", "Gate3TransportLifecycle.cs");
        var localAdmission = Path.Combine(project, "Assets", "Gate2", "Authority", "Gate2LocalIntentAdmissionAdapter.cs");
        var connection = Path.Combine(project, "Assets", "Gate3", "Connection", "Gate3ServerConnectionActorBinding.cs");

        var driverSource = File.ReadAllText(driver);
        Assert.Contains("ObserveAuthoritativeServerReceiveTick", driverSource, StringComparison.Ordinal);
        Assert.Contains("_elapsedTimeSource.ResetSessionOrigin()", driverSource, StringComparison.Ordinal);
        Assert.Contains("new Gate3ServerReceiveTickObservationSource(_elapsedTimeSource)", driverSource, StringComparison.Ordinal);
        Assert.Contains("_receiveTickObservationSource = null", driverSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StartConnection(", driverSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StopConnection(", driverSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ServerReceiveSequence", driverSource, StringComparison.Ordinal);

        var startStopOwners = Directory.GetFiles(Path.Combine(project, "Assets"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                           && !path.Contains(Path.DirectorySeparatorChar + "FishNet" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("StartConnection(", StringComparison.Ordinal)
                           || File.ReadAllText(path).Contains("StopConnection(", StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { Path.GetFullPath(lifecycle) }, startStopOwners);
        Assert.DoesNotContain("Gate3ServerReceiveTick", File.ReadAllText(localAdmission), StringComparison.Ordinal);
        Assert.DoesNotContain("Gate3ServerReceiveTick", File.ReadAllText(connection), StringComparison.Ordinal);
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
