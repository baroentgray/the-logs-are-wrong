using System.Text.RegularExpressions;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>
/// Repository-level boundary guard for the Gate-2 local intent admission adapter. These source contracts ensure
/// Unity remains a thin caller of PortableAuthority rather than a second accepted-batch or host-tick authority.
/// </summary>
public sealed class Tlaw072LocalIntentAdmissionArchitectureTests
{
    [Fact]
    [Trait("Scope", "TLAW-072")]
    public void Gate2_has_one_plain_csharp_local_admission_adapter_with_no_network_or_gameplay_authority()
    {
        var root = FindRepositoryRoot();
        var adapter = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate2", "Authority", "Gate2LocalIntentAdmissionAdapter.cs");
        Assert.True(File.Exists(adapter), "TLAW-072 requires one explicit Gate-2 local intent admission adapter.");

        var source = File.ReadAllText(adapter);
        Assert.Contains("sealed class Gate2LocalIntentAdmissionAdapter : IAlreadyAdmittedHostInputSource", source, StringComparison.Ordinal);
        Assert.Contains("LocalIntentAdmissionResult SubmitLocalIntent(IntentEnvelope envelope, ActorId authoritativeActor)", source, StringComparison.Ordinal);
        Assert.Contains("ServerTick _openAdmissionTick = ServerTick.Zero", source, StringComparison.Ordinal);
        Assert.Contains("ServerReceiveSequence _nextReceiveSequence = ServerReceiveSequence.Zero", source, StringComparison.Ordinal);
        Assert.Contains("AcceptedIntentTickBatchFactory.Create", source, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(source, @"AcceptedIntentTickBatchFactory\.Create", RegexOptions.CultureInvariant).Cast<Match>());
        Assert.DoesNotContain("UnityEngine", source, StringComparison.Ordinal);

        Assert.All(new[]
        {
            "HostSession", "HostTickCadence", "HostTickExecutionService", "YamlConfigurationLoader", "YamlDotNet",
            "FishNet", "FishySteamworks", "Steamworks", "NetworkManager", "Rpc", "Socket", "Network"
        }, forbidden => Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Scope", "TLAW-072")]
    public void Production_driver_uses_the_adapter_as_its_only_runtime_local_ingress_without_reordering_the_tick_pump()
    {
        var root = FindRepositoryRoot();
        var driver = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate2", "Authority", "Gate2ProductionHostDriver.cs");
        var source = File.ReadAllText(driver);

        Assert.Contains("Gate2LocalIntentAdmissionAdapter", source, StringComparison.Ordinal);
        Assert.Contains("LocalIntentAdmissionResult SubmitLocalIntent(IntentEnvelope envelope, ActorId authoritativeActor)", source, StringComparison.Ordinal);
        Assert.Contains("_inputSource.GetInput(_session.ShiftState.ShiftId, tick)", source, StringComparison.Ordinal);
        Assert.Contains("_session.ExecuteTick(tick, input.AcceptedIntents, input.ActiveTools)", source, StringComparison.Ordinal);
        Assert.Contains("_cadence.RetireNextDueTick()", source, StringComparison.Ordinal);
        Assert.Contains("#if UNITY_EDITOR", source, StringComparison.Ordinal);
        Assert.Contains("LastSuccessfulTickResultForTesting", source, StringComparison.Ordinal);

        var getInput = source.IndexOf("_inputSource.GetInput(_session.ShiftState.ShiftId, tick)", StringComparison.Ordinal);
        var execute = source.IndexOf("_session.ExecuteTick(tick, input.AcceptedIntents, input.ActiveTools)", StringComparison.Ordinal);
        var retire = source.IndexOf("_cadence.RetireNextDueTick()", StringComparison.Ordinal);
        Assert.True(getInput < execute && execute < retire,
            "The production path must remain Accumulate -> GetInput -> HostSession.ExecuteTick -> RetireNextDueTick.");

        Assert.All(new[]
        {
            "YamlConfigurationLoader", "YamlDotNet", "FishNet", "FishySteamworks", "Steamworks", "NetworkManager", "Rpc", "Socket"
        }, forbidden => Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal));
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
