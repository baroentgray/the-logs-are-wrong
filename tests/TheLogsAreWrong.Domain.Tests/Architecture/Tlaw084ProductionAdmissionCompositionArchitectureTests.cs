namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>Repository guard for the bounded D-025 production admission composition.</summary>
public sealed class Tlaw084ProductionAdmissionCompositionArchitectureTests
{
    [Fact]
    [Trait("Scope", "TLAW-084")]
    public void One_shared_owner_normalizes_local_and_resolved_network_evidence_before_existing_receipts()
    {
        var root = FindRepositoryRoot();
        var admission = Read(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "Admission", "Gate3NetworkIntentAdmissionBuffer.cs");
        var composition = Read(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "Admission", "Gate3ProductionAdmissionComposition.cs");

        Assert.Contains("struct Gate3ProductionAdmissionEvidence", admission, StringComparison.Ordinal);
        Assert.Contains("Admit(Gate3ProductionAdmissionEvidence evidence)", admission, StringComparison.Ordinal);
        Assert.Contains("AdmitResolved", admission, StringComparison.Ordinal);
        Assert.Contains("ServerReceiveSequence.Zero", admission, StringComparison.Ordinal);
        Assert.Contains("AcceptedIntentTickBatchFactory.Create", admission, StringComparison.Ordinal);

        Assert.Contains("sealed class Gate3ProductionAdmissionComposition", composition, StringComparison.Ordinal);
        Assert.Contains("Gate3NetworkIntentAdmissionBuffer", composition, StringComparison.Ordinal);
        Assert.Contains("Gate3ResolvedNetworkIntentEvidence", composition, StringComparison.Ordinal);
        Assert.Contains("SubmitTrustedLocalIntent", composition, StringComparison.Ordinal);
        Assert.Contains("IAlreadyAdmittedHostInputSource", composition, StringComparison.Ordinal);
        Assert.Contains("IIngressBeforeSealHostInputSource", composition, StringComparison.Ordinal);
        Assert.Contains("Resolved += OnResolvedNetworkIntent", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("Gate2LocalIntentAdmissionAdapter", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthoritativeAcceptedIntent(", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("ServerReceiveSequence", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("FishNet", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("StartConnection(", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("StopConnection(", composition, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Scope", "TLAW-084")]
    public void Driver_has_one_networked_input_source_and_defers_seal_until_receive_time_has_advanced_past_the_due_tick()
    {
        var root = FindRepositoryRoot();
        var driver = Read(root, "unity", "TheLogsAreWrong", "Assets", "Gate2", "Authority", "Gate2ProductionHostDriver.cs");

        Assert.Contains("IIngressBeforeSealHostInputSource", driver, StringComparison.Ordinal);
        Assert.Contains("ConfigureNetworkedProductionAdmission", driver, StringComparison.Ordinal);
        Assert.Contains("BeginSession(configuration.Shift.ShiftId, ObserveAuthoritativeServerReceiveTick)", driver, StringComparison.Ordinal);
        Assert.Contains("_inputSource is IIngressBeforeSealHostInputSource ingressBeforeSeal", driver, StringComparison.Ordinal);
        Assert.Contains("!ingressBeforeSeal.CanSeal(_session.ShiftState.ShiftId, tick)", driver, StringComparison.Ordinal);
        Assert.Contains("_inputSource.GetInput(_session.ShiftState.ShiftId, tick)", driver, StringComparison.Ordinal);
        Assert.DoesNotContain("MergeAccepted", driver, StringComparison.Ordinal);
        Assert.DoesNotContain("Concat", driver, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Scope", "TLAW-084")]
    public void Production_scene_wires_the_bounded_composition_without_rewiring_predecessor_boundaries()
    {
        var root = FindRepositoryRoot();
        var assets = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets");
        var authoring = File.ReadAllText(Path.Combine(assets, "Gate2", "Editor", "Gate2BootstrapAuthoring.cs"));
        var scene = File.ReadAllText(Path.Combine(assets, "Gate2", "Bootstrap", "Gate2Bootstrap.unity"));
        var meta = Read(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "Admission", "Gate3ProductionAdmissionComposition.cs.meta");
        var adapter = Read(root, "unity", "TheLogsAreWrong", "Assets", "Gate2", "Authority", "Gate2LocalIntentAdmissionAdapter.cs");
        var actorResolution = Read(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "ActorResolution", "Gate3ActorResolutionComposition.cs");

        Assert.Contains("Assets/Gate3/Admission/Gate3ProductionAdmissionComposition.cs", authoring, StringComparison.Ordinal);
        Assert.Contains("root.AddComponent<Gate3ProductionAdmissionComposition>()", authoring, StringComparison.Ordinal);
        Assert.Contains("_hostDriver", authoring, StringComparison.Ordinal);
        Assert.Contains("_actorResolution", authoring, StringComparison.Ordinal);
        var guid = meta.Split('\n').Single(line => line.StartsWith("guid: ", StringComparison.Ordinal))["guid: ".Length..].Trim();
        Assert.Contains("m_Script: {fileID: 11500000, guid: " + guid + ", type: 3}", scene, StringComparison.Ordinal);

        Assert.DoesNotContain("Gate3ProductionAdmission", adapter, StringComparison.Ordinal);
        Assert.Contains("event Action<Gate3ResolvedNetworkIntentEvidence> Resolved", actorResolution, StringComparison.Ordinal);
        Assert.DoesNotContain("Gate3NetworkIntentAdmissionBuffer", actorResolution, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] segments)
    {
        var path = Path.Combine([root, .. segments]);
        Assert.True(File.Exists(path), "Required TLAW-084 path is missing: " + path);
        return File.ReadAllText(path);
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
