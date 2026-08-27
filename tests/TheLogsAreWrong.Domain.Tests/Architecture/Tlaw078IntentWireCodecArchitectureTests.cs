namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>Repository guard for the bounded D-023 Gate-3 V1 intent wire codec.</summary>
public sealed class Tlaw078IntentWireCodecArchitectureTests
{
    [Fact]
    [Trait("Scope", "TLAW-078")]
    public void One_plain_csharp_v1_codec_preserves_the_frozen_D023_wire_contract()
    {
        var root = FindRepositoryRoot();
        var sourcePath = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "IntentWire", "Gate3IntentWireV1Codec.cs");
        Assert.True(File.Exists(sourcePath), "TLAW-078 requires the one Gate-3-owned D-023 V1 codec/materializer.");

        var source = File.ReadAllText(sourcePath);
        Assert.Contains("public const ushort SchemaVersion = 1", source, StringComparison.Ordinal);
        Assert.Contains("public const int MaxPayloadBytes = 2048", source, StringComparison.Ordinal);
        Assert.Contains("NONE = 1", source, StringComparison.Ordinal);
        Assert.Contains("PROCEDURE_ACTION = 2", source, StringComparison.Ordinal);
        Assert.Contains("TryEncode", source, StringComparison.Ordinal);
        Assert.Contains("TryDecode", source, StringComparison.Ordinal);
        Assert.Contains("UTF8Encoding(false, true)", source, StringComparison.Ordinal);
        Assert.Contains("HasLeadingUtf8Bom", source, StringComparison.Ordinal);
        Assert.Contains("0xef", source, StringComparison.Ordinal);
        Assert.Contains("0xbb", source, StringComparison.Ordinal);
        Assert.Contains("0xbf", source, StringComparison.Ordinal);
        Assert.Contains("WriteUInt16LittleEndian", source, StringComparison.Ordinal);
        Assert.Contains("WriteInt64LittleEndian", source, StringComparison.Ordinal);

        Assert.All(new[]
        {
            "FishNet", "FishySteamworks", "Steamworks", "Rpc", "Broadcast", "NetworkManager",
            "Gate3ServerConnection", "ResolveAuthoritativeActor", "Gate3ServerReceiveTick",
            "ServerReceiveSequence", "AuthoritativeAcceptedIntent", "AcceptedIntentTickBatch",
            "HostSession", "StartConnection(", "StopConnection(", "Snapshot", "Resync", "Reconnect", "Prediction"
        }, forbidden => Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Scope", "TLAW-078")]
    public void D023_failure_taxonomy_and_existing_later_gate_boundaries_remain_explicit()
    {
        var root = FindRepositoryRoot();
        var decisions = File.ReadAllText(Path.Combine(root, "docs", "agent", "DECISIONS.md"));
        var codec = File.ReadAllText(Path.Combine(root, "unity", "TheLogsAreWrong", "Assets", "Gate3", "IntentWire", "Gate3IntentWireV1Codec.cs"));

        Assert.All(new[]
        {
            "MESSAGE_TOO_LARGE", "TRUNCATED_OR_MALFORMED_FRAME", "INVALID_UTF8", "UNSUPPORTED_SCHEMA_VERSION",
            "INVALID_IDENTIFIER", "INVALID_NUMERIC_FIELD", "UNSUPPORTED_PARAMETER_KIND",
            "PARAMETER_PAYLOAD_MISMATCH", "TRAILING_DATA"
        }, failure =>
        {
            Assert.Contains(failure, decisions, StringComparison.Ordinal);
            Assert.Contains(failure, codec, StringComparison.Ordinal);
        });

        var project = Path.Combine(root, "unity", "TheLogsAreWrong", "Assets");
        Assert.DoesNotContain("Gate3IntentWire", File.ReadAllText(Path.Combine(project, "Gate2", "Authority", "Gate2LocalIntentAdmissionAdapter.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("Gate3IntentWire", File.ReadAllText(Path.Combine(project, "Gate3", "Transport", "Gate3TransportLifecycle.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("Gate3IntentWire", File.ReadAllText(Path.Combine(project, "Gate3", "Connection", "Gate3ServerConnectionActorBinding.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("Gate3IntentWire", File.ReadAllText(Path.Combine(project, "Gate3", "ReceiveTick", "Gate3ServerReceiveTickMapping.cs")), StringComparison.Ordinal);
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
