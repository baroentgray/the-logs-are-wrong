namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>Repository guard for the bounded D-026 client-intent disposition/replay boundary.</summary>
public sealed class Tlaw086ClientIntentDispositionArchitectureTests
{
    [Fact]
    [Trait("Scope", "TLAW-086")]
    public void One_plain_csharp_result_ledger_has_the_frozen_v1_limits_and_no_gameplay_or_transport_authority()
    {
        var root = FindRepositoryRoot();
        var ledger = Read(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "Results", "Gate3ClientIntentDispositionLedger.cs");

        Assert.Contains("public const int Capacity = 4096", ledger, StringComparison.Ordinal);
        Assert.Contains("public const int MaxPayloadBytes = 1024", ledger, StringComparison.Ordinal);
        Assert.Contains("public const int MaxRejectionCodeUtf8Bytes = 64", ledger, StringComparison.Ordinal);
        Assert.Contains("WriteUInt16LittleEndian(output, SchemaVersion)", ledger, StringComparison.Ordinal);
        Assert.Contains("HasLeadingUtf8Bom", ledger, StringComparison.Ordinal);
        Assert.Contains("Gate3ClientIntentDispositionKind.PENDING", ledger, StringComparison.Ordinal);
        Assert.Contains("Gate3ClientIntentDispositionKind.APPLIED", ledger, StringComparison.Ordinal);
        Assert.Contains("Gate3ClientIntentDispositionKind.REJECTED", ledger, StringComparison.Ordinal);
        Assert.Contains("RESULT_CAPACITY_EXHAUSTED", ledger, StringComparison.Ordinal);
        Assert.Contains("INTENT_ID_ALREADY_USED", ledger, StringComparison.Ordinal);
        Assert.Contains("INTENT_ALREADY_PROCESSED", ledger, StringComparison.Ordinal);
        Assert.Contains("UNSUPPORTED_ACTION", ledger, StringComparison.Ordinal);
        Assert.Contains("ExistingIntentIdRequiresD024", ledger, StringComparison.Ordinal);
        Assert.Contains("ResolveDuplicateAfterD024", ledger, StringComparison.Ordinal);
        Assert.Contains("IsPreD024RetainedRejection", ledger, StringComparison.Ordinal);
        Assert.Contains("TryMapStageTwoRejection", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("ToString()", ledger, StringComparison.Ordinal);

        Assert.All(new[]
        {
            "UnityEngine", "MonoBehaviour", "using FishNet", "FishySteamworks", "Steamworks", "Broadcast", "NetworkConnection",
            "ServerReceiveSequence", "AuthoritativeAcceptedIntent", "AcceptedIntentTickBatchFactory", "new HostSession", "ExecuteTick(",
            "EventSequence", "StartConnection(", "StopConnection(", "Snapshot", "Resync", "Reconnect", "Prediction", "Rpc"
        }, forbidden => Assert.DoesNotContain(forbidden, ledger, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Scope", "TLAW-086")]
    public void Composition_reserves_before_d025_then_projects_only_after_the_exact_successful_host_tick()
    {
        var root = FindRepositoryRoot();
        var composition = Read(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "Results", "Gate3ClientIntentDispositionComposition.cs");
        var actorResolution = Read(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "ActorResolution", "Gate3ActorResolutionComposition.cs");
        var driver = Read(root, "unity", "TheLogsAreWrong", "Assets", "Gate2", "Authority", "Gate2ProductionHostDriver.cs");
        var admission = Read(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "Admission", "Gate3ProductionAdmissionComposition.cs");
        var d024 = Read(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "Admission", "Gate3NetworkIntentAdmissionBuffer.cs");

        Assert.Contains("BeforeResolution += ReserveBeforeResolution", composition, StringComparison.Ordinal);
        Assert.Contains("ResolutionProcessed += OnResolutionProcessed", composition, StringComparison.Ordinal);
        Assert.Contains("_ledger.Reserve(decoded.Envelope, origin, decoded.AuthoritativeReceiveTick)", composition, StringComparison.Ordinal);
        Assert.Contains("_admission.AdmitResolvedNetworkIntent(resolution.Evidence)", composition, StringComparison.Ordinal);
        Assert.Contains("ExistingIntentIdRequiresD024", composition, StringComparison.Ordinal);
        Assert.Contains("ResolveDuplicateAfterD024", composition, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(composition, "_admission.AdmitResolvedNetworkIntent(resolution.Evidence)"));
        Assert.Contains("_hostDriver.AuthoritativeTickSucceeded += OnAuthoritativeTickSucceeded", composition, StringComparison.Ordinal);
        Assert.Contains("_ledger.ProjectSuccessfulTick(execution.StageTwo)", composition, StringComparison.Ordinal);
        Assert.Contains("ACTOR_NOT_BOUND", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitTrustedLocalIntent", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("ServerReceiveSequence", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("new HostSession", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteTick(", composition, StringComparison.Ordinal);

        Assert.Contains("BeforeResolution", actorResolution, StringComparison.Ordinal);
        Assert.Contains("if (!CanContinueToResolution(decoded))", actorResolution, StringComparison.Ordinal);
        Assert.Contains("ResolutionProcessed?.Invoke(decoded, LastResult)", actorResolution, StringComparison.Ordinal);
        Assert.DoesNotContain("Gate3ClientIntentDispositionLedger", actorResolution, StringComparison.Ordinal);

        Assert.Contains("AttachResultDispositionComposition", admission, StringComparison.Ordinal);
        Assert.Contains("_resultDisposition?.BeginSession(shiftId)", admission, StringComparison.Ordinal);
        Assert.Contains("_resultDisposition?.EndSession()", admission, StringComparison.Ordinal);
        Assert.DoesNotContain("Gate3ClientIntentDispositionLedger", admission, StringComparison.Ordinal);

        var shiftMismatch = d024.IndexOf("if (envelope.ShiftId != _shiftId)", StringComparison.Ordinal);
        var duplicate = d024.IndexOf("if (!_seenIntentIds.Add(envelope.IntentId))", StringComparison.Ordinal);
        Assert.True(shiftMismatch >= 0 && duplicate > shiftMismatch,
            "D-024 must retain its frozen shift-before-duplicate ordering; D-026 may only consume the resulting status.");

        var ledgerSource = Read(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "Results", "Gate3ClientIntentDispositionLedger.cs");
        Assert.Contains("rejectionCode == \"ACTOR_NOT_BOUND\" || rejectionCode == \"SHIFT_MISMATCH\"", ledgerSource, StringComparison.Ordinal);

        var execute = driver.IndexOf("var result = _session.ExecuteTick(tick, input.AcceptedIntents, input.ActiveTools);", StringComparison.Ordinal);
        var published = driver.IndexOf("AuthoritativeTickSucceeded?.Invoke(tick, result);", StringComparison.Ordinal);
        var retired = driver.IndexOf("var retired = _cadence.RetireNextDueTick();", StringComparison.Ordinal);
        Assert.True(execute >= 0 && published > execute && retired > published,
            "D-026 may observe only the exact returned successful HostSession tick before the due cadence tick retires.");
    }

    [Fact]
    [Trait("Scope", "TLAW-086")]
    public void Reliable_result_carrier_and_scene_wiring_keep_delivery_origin_bound_without_transport_lifecycle_ownership()
    {
        var root = FindRepositoryRoot();
        var assets = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets");
        var carrier = File.ReadAllText(Path.Combine(assets, "Gate3", "Results", "Gate3ClientIntentResultCarrier.cs"));
        var bridge = File.ReadAllText(Path.Combine(assets, "Gate3", "Connection", "Gate3ServerConnectionActorBindingBridge.cs"));
        var authoring = File.ReadAllText(Path.Combine(assets, "Gate2", "Editor", "Gate2BootstrapAuthoring.cs"));
        var scene = File.ReadAllText(Path.Combine(assets, "Gate2", "Bootstrap", "Gate2Bootstrap.unity"));
        var carrierMeta = File.ReadAllText(Path.Combine(assets, "Gate3", "Results", "Gate3ClientIntentResultCarrier.cs.meta"));
        var compositionMeta = File.ReadAllText(Path.Combine(assets, "Gate3", "Results", "Gate3ClientIntentDispositionComposition.cs.meta"));

        Assert.Contains("struct Gate3ClientIntentResultCarrierBroadcast : IBroadcast", carrier, StringComparison.Ordinal);
        Assert.Contains("requireAuthenticated: true", carrier, StringComparison.Ordinal);
        Assert.Contains("channel: Channel.Reliable", carrier, StringComparison.Ordinal);
        Assert.Contains("currentLifetime != origin.Lifetime", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("RegisterBroadcast", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("StartConnection(", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("StopConnection(", carrier, StringComparison.Ordinal);

        Assert.Contains("Gate3ServerConnectionLifetime", bridge, StringComparison.Ordinal);
        Assert.Contains("ConnectionLifetimeRevoked", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("Gate3ClientIntentDispositionLedger", bridge, StringComparison.Ordinal);

        Assert.Contains("Gate3ClientIntentResultCarrier", authoring, StringComparison.Ordinal);
        Assert.Contains("Gate3ClientIntentDispositionComposition", authoring, StringComparison.Ordinal);
        AssertSceneContainsScript(scene, carrierMeta);
        AssertSceneContainsScript(scene, compositionMeta);
    }

    private static void AssertSceneContainsScript(string scene, string meta)
    {
        var guid = meta.Split('\n').Single(line => line.StartsWith("guid: ", StringComparison.Ordinal))["guid: ".Length..].Trim();
        Assert.Contains("m_Script: {fileID: 11500000, guid: " + guid + ", type: 3}", scene, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] segments)
    {
        var path = Path.Combine([root, .. segments]);
        Assert.True(File.Exists(path), "Required TLAW-086 path is missing: " + path);
        return File.ReadAllText(path);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        for (var index = source.IndexOf(value, StringComparison.Ordinal); index >= 0; index = source.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
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
