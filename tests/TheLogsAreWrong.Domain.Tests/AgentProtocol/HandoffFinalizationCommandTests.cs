using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Tlaw.AgentProtocol;
using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class HandoffFinalizationCommandTests
{
    [Theory]
    [InlineData("ready", "manual_cancel", false)]
    [InlineData("ready", "quota_exhaustion", false)]
    [InlineData("ready", "timeout", true)]
    [InlineData("blocked", "manual_cancel", false)]
    [InlineData("blocked", "timeout", true)]
    public void Correlated_handoff_releases_exact_lease_and_publishes_an_unclaimed_routable_task(string status, string reason, bool expire)
    {
        using var workspace = HandoffWorkspace.Create();
        var lease = workspace.Acquire();
        if (expire) workspace.Clock.Advance(TimeSpan.FromMinutes(5));
        var task = workspace.Write("task.yaml", workspace.TaskYaml(lease));
        var handoff = workspace.Write("handoff.yaml", workspace.HandoffYaml(lease, status));
        var ingestion = workspace.Write("ingestion.json", workspace.IngestionJson(lease, handoff, status, reason));
        var output = Path.Combine(workspace.Path, "continuation.yaml");
        var lockBefore = File.ReadAllBytes(workspace.LockFile());
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);

        Assert.Equal(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, output), stdout, TextWriter.Null, workspace.Clock));

        Assert.Equal($"HANDOFF FINALIZED: {(status == "ready" ? "reassign" : "human")}", stdout.ToString().Trim());
        Assert.Equal(LocalLeaseStatus.Missing, new FileLeaseStore(workspace.StorePath, workspace.Clock).Inspect("BAR-40").Status);
        Assert.Equal(lockBefore, File.ReadAllBytes(workspace.LockFile()));
        var continuation = File.ReadAllText(output);
        var continuationValidation = PacketValidator.Validate(continuation, Registry);
        Assert.True(continuationValidation.IsValid);
        Assert.True(TaskV2Packet.From(continuationValidation.Packet!).IsUnclaimed);
        Assert.DoesNotContain((byte)'\r', File.ReadAllBytes(output));
        var agents = workspace.Write("agents.json", "{\"schema\":\"tlaw.dispatcher-agent-snapshot/v1\",\"agents\":[{\"agent\":\"codex\",\"capabilities\":[\"dotnet\",\"yaml_protocol\"],\"availability\":\"AVAILABLE\"},{\"agent\":\"claude\",\"capabilities\":[\"dotnet\",\"yaml_protocol\"],\"availability\":\"AVAILABLE\"}]}");
        Assert.Equal(0, RouteCommand.Run(["route", "--task", output, "--agents", agents, "--output", Path.Combine(workspace.Path, "selection.json")], TextWriter.Null, TextWriter.Null));
    }

    [Theory]
    [InlineData("task_id", "OTHER")]
    [InlineData("source_id", "OTHER")]
    [InlineData("claimed_by", "claude")]
    [InlineData("claim_id", "0123456789abcdef0123456789abcdef")]
    [InlineData("branch", "task/OTHER")]
    [InlineData("head_sha", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    [InlineData("handoff_status", "blocked")]
    [InlineData("handoff_sha256", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Correlation_mismatches_preserve_the_lease_and_prior_output(string property, string value)
    {
        using var workspace = HandoffWorkspace.Create();
        var lease = workspace.Acquire();
        var task = workspace.Write("task.yaml", workspace.TaskYaml(lease));
        var handoff = workspace.Write("handoff.yaml", workspace.HandoffYaml(lease, "ready"));
        var ingestion = workspace.Write("ingestion.json", workspace.IngestionJson(lease, handoff, "ready", "manual_cancel").Replace($"\"{property}\": \"{PropertyValue(property, lease, handoff)}\"", $"\"{property}\": \"{value}\"", StringComparison.Ordinal));
        var output = workspace.Write("continuation.yaml", "prior\n");
        var storeBefore = workspace.SnapshotStore();

        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, output), TextWriter.Null, TextWriter.Null, workspace.Clock));

        Assert.Equal("prior\n", File.ReadAllText(output));
        Assert.Equal(storeBefore, workspace.SnapshotStore());
        Assert.Empty(Directory.EnumerateFiles(workspace.Path, ".continuation.yaml.*.tmp"));
    }

    [Theory]
    [InlineData("lease_status", "expired")]
    [InlineData("release_reason", "timeout")]
    [InlineData("decision", "human")]
    [InlineData("lease_action", "other")]
    [InlineData("next_state", "review")]
    [InlineData("blocked", "true")]
    public void Contradictory_ingestion_mapping_fails_before_release(string property, string value)
    {
        using var workspace = HandoffWorkspace.Create();
        var lease = workspace.Acquire();
        var task = workspace.Write("task.yaml", workspace.TaskYaml(lease));
        var handoff = workspace.Write("handoff.yaml", workspace.HandoffYaml(lease, "ready"));
        var original = workspace.IngestionJson(lease, handoff, "ready", "manual_cancel");
        var ingestion = workspace.Write("ingestion.json", property == "blocked" ? original.Replace("\"blocked\": false", "\"blocked\": true", StringComparison.Ordinal) : original.Replace($"\"{property}\": \"{PropertyValue(property, lease, handoff)}\"", $"\"{property}\": \"{value}\"", StringComparison.Ordinal));

        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, Path.Combine(workspace.Path, "out.yaml")), TextWriter.Null, TextWriter.Null, workspace.Clock));
        Assert.Equal(LocalLeaseStatus.Active, new FileLeaseStore(workspace.StorePath, workspace.Clock).Inspect("BAR-40").Status);
    }

    [Theory]
    [InlineData("task_id", "null")]
    [InlineData("source_id", "17")]
    [InlineData("claimed_by", "\"\"")]
    [InlineData("claim_id", "[]")]
    [InlineData("branch", "false")]
    [InlineData("schema", "\"wrong\"")]
    public void Strict_ingestion_parser_rejects_wrong_type_null_or_empty_identity(string property, string replacement)
    {
        using var workspace = HandoffWorkspace.Create();
        var lease = workspace.Acquire();
        var handoff = workspace.Write("handoff.yaml", workspace.HandoffYaml(lease, "ready"));
        var json = workspace.IngestionJson(lease, handoff, "ready", "manual_cancel");
        var pattern = new System.Text.RegularExpressions.Regex($"\\\"{property}\\\"\\s*:\\s*(\\\"[^\\\"]*\\\"|true|false|null|\\d+|\\[\\])", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var changed = pattern.Replace(json, $"\"{property}\": {replacement}", 1);

        Assert.Throws<HandoffIngestionException>(() => HandoffIngestionRecord.Parse(Encoding.UTF8.GetBytes(changed)));
    }

    [Fact]
    public void Invalid_options_and_output_aliases_fail_before_reading_or_releasing()
    {
        using var workspace = HandoffWorkspace.Create();
        var lease = workspace.Acquire();
        var task = workspace.Write("task.yaml", workspace.TaskYaml(lease));
        var handoff = workspace.Write("handoff.yaml", workspace.HandoffYaml(lease, "ready"));
        var ingestion = workspace.Write("ingestion.json", workspace.IngestionJson(lease, handoff, "ready", "manual_cancel"));
        var before = workspace.SnapshotStore();

        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(["finalize-handoff", "--task", task], TextWriter.Null, TextWriter.Null, workspace.Clock));
        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, task), TextWriter.Null, TextWriter.Null, workspace.Clock));
        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, handoff), TextWriter.Null, TextWriter.Null, workspace.Clock));
        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, ingestion), TextWriter.Null, TextWriter.Null, workspace.Clock));
        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, workspace.LeaseFile()), TextWriter.Null, TextWriter.Null, workspace.Clock));
        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, workspace.LockFile()), TextWriter.Null, TextWriter.Null, workspace.Clock));
        Assert.Equal(before, workspace.SnapshotStore());
    }

    [Fact]
    public void Publication_failure_after_release_is_reported_honestly_without_rollback()
    {
        using var workspace = HandoffWorkspace.Create();
        var lease = workspace.Acquire();
        var task = workspace.Write("task.yaml", workspace.TaskYaml(lease));
        var handoff = workspace.Write("handoff.yaml", workspace.HandoffYaml(lease, "ready"));
        var ingestion = workspace.Write("ingestion.json", workspace.IngestionJson(lease, handoff, "ready", "manual_cancel"));
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, Path.Combine(workspace.Path, "missing", "out.yaml")), TextWriter.Null, error, workspace.Clock));
        Assert.Equal(LocalLeaseStatus.Missing, new FileLeaseStore(workspace.StorePath, workspace.Clock).Inspect("BAR-40").Status);
        Assert.Contains("the lease was already released but the continuation task was not published", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Base_sha_mismatch_and_missing_store_fail_before_any_release_or_store_creation()
    {
        using var workspace = HandoffWorkspace.Create();
        var lease = workspace.Acquire();
        var task = workspace.Write("task.yaml", workspace.TaskYaml(lease));
        var handoff = workspace.Write("handoff.yaml", workspace.HandoffYaml(lease, "ready").Replace("base_sha: 9608094501d18fe8472a7ebb66676777e6174e53", "base_sha: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", StringComparison.Ordinal));
        var ingestion = workspace.Write("ingestion.json", workspace.IngestionJson(lease, handoff, "ready", "manual_cancel"));
        var prior = workspace.SnapshotStore();

        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, Path.Combine(workspace.Path, "out.yaml")), TextWriter.Null, TextWriter.Null, workspace.Clock));
        Assert.Equal(prior, workspace.SnapshotStore());
        var validHandoff = workspace.Write("valid-handoff.yaml", workspace.HandoffYaml(lease, "ready"));
        var validIngestion = workspace.Write("valid-ingestion.json", workspace.IngestionJson(lease, validHandoff, "ready", "manual_cancel"));
        var missingStore = Path.Combine(workspace.Path, "missing-store");
        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, validHandoff, validIngestion, missingStore, Path.Combine(workspace.Path, "other.yaml")), TextWriter.Null, TextWriter.Null, workspace.Clock));
        Assert.False(Directory.Exists(missingStore));
    }

    [Theory]
    [InlineData("claimed_by: \"codex\"", "claimed_by: \"unclaimed\"")]
    [InlineData("claim_started_at:", "claim_started_at: unclaimed #")]
    [InlineData("schema: \"tlaw.agent-task/v2\"", "schema: \"tlaw.agent-task/v1\"")]
    public void Unclaimed_partial_or_v1_task_cannot_finalize_a_handoff(string find, string replace)
    {
        using var workspace = HandoffWorkspace.Create();
        var lease = workspace.Acquire();
        var task = workspace.Write("task.yaml", workspace.TaskYaml(lease).Replace(find, replace, StringComparison.Ordinal));
        var handoff = workspace.Write("handoff.yaml", workspace.HandoffYaml(lease, "ready"));
        var ingestion = workspace.Write("ingestion.json", workspace.IngestionJson(lease, handoff, "ready", "manual_cancel"));

        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, Path.Combine(workspace.Path, "out.yaml")), TextWriter.Null, TextWriter.Null, workspace.Clock));
        Assert.Equal(LocalLeaseStatus.Active, new FileLeaseStore(workspace.StorePath, workspace.Clock).Inspect("BAR-40").Status);
    }

    [Fact]
    public void Invalid_handoff_and_ingestion_bytes_are_rejected_without_release()
    {
        using var workspace = HandoffWorkspace.Create();
        var lease = workspace.Acquire();
        var task = workspace.Write("task.yaml", workspace.TaskYaml(lease));
        var handoff = workspace.Write("handoff.yaml", workspace.HandoffYaml(lease, "ready"));
        var ingestion = workspace.Write("ingestion.json", workspace.IngestionJson(lease, handoff, "ready", "manual_cancel"));
        File.WriteAllBytes(handoff, [0xff, 0xfe]);
        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, Path.Combine(workspace.Path, "out.yaml")), TextWriter.Null, TextWriter.Null, workspace.Clock));
        Assert.Equal(LocalLeaseStatus.Active, new FileLeaseStore(workspace.StorePath, workspace.Clock).Inspect("BAR-40").Status);
        File.WriteAllText(handoff, workspace.HandoffYaml(lease, "ready"), new UTF8Encoding(false));
        File.WriteAllText(ingestion, "{", new UTF8Encoding(false));
        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, Path.Combine(workspace.Path, "out.yaml")), TextWriter.Null, TextWriter.Null, workspace.Clock));
        Assert.Equal(LocalLeaseStatus.Active, new FileLeaseStore(workspace.StorePath, workspace.Clock).Inspect("BAR-40").Status);
        File.WriteAllText(handoff, workspace.HandoffYaml(lease, "ready").Replace("schema: tlaw.agent-handoff/v2", "schema: tlaw.agent-handoff/v1", StringComparison.Ordinal), new UTF8Encoding(false));
        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, Path.Combine(workspace.Path, "out.yaml")), TextWriter.Null, TextWriter.Null, workspace.Clock));
        Assert.Equal(LocalLeaseStatus.Active, new FileLeaseStore(workspace.StorePath, workspace.Clock).Inspect("BAR-40").Status);
    }

    [Fact]
    public void General_release_still_rejects_the_former_owner_of_an_expired_lease()
    {
        using var workspace = HandoffWorkspace.Create();
        var lease = workspace.Acquire();
        workspace.Clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Throws<LeaseGuardException>(() => new FileLeaseStore(workspace.StorePath, workspace.Clock).Release(lease.TaskId, lease.ClaimId, LeaseReleaseReason.Timeout));
        Assert.Equal(LocalLeaseStatus.Expired, new FileLeaseStore(workspace.StorePath, workspace.Clock).Inspect(lease.TaskId).Status);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("disposed")]
    [InlineData("invalid")]
    [InlineData("unsupported")]
    public void Stdout_failure_after_release_and_publication_reports_durable_state(string kind)
    {
        using var workspace = HandoffWorkspace.Create();
        var lease = workspace.Acquire();
        var task = workspace.Write("task.yaml", workspace.TaskYaml(lease));
        var handoff = workspace.Write("handoff.yaml", workspace.HandoffYaml(lease, "ready"));
        var ingestion = workspace.Write("ingestion.json", workspace.IngestionJson(lease, handoff, "ready", "manual_cancel"));
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        Assert.NotEqual(0, FinalizeHandoffCommand.RunForTesting(Args(task, handoff, ingestion, workspace.StorePath, Path.Combine(workspace.Path, "out.yaml")), new ThrowingWriter(kind), error, workspace.Clock));
        Assert.Equal(LocalLeaseStatus.Missing, new FileLeaseStore(workspace.StorePath, workspace.Clock).Inspect("BAR-40").Status);
        Assert.Contains("the lease was already released and the continuation task was already published", error.ToString(), StringComparison.Ordinal);
    }

    private static string PropertyValue(string property, LocalLease lease, string handoff) => property switch
    {
        "task_id" => lease.TaskId,
        "source_id" => "BAR-26",
        "claimed_by" => lease.ClaimedBy,
        "claim_id" => lease.ClaimId,
        "branch" => "task/BAR-40-handoff-finalization",
        "head_sha" => "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "handoff_status" => "ready",
        "handoff_sha256" => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(handoff))),
        "lease_status" => "active",
        "release_reason" => "manual_cancel",
        "decision" => "reassign",
        "lease_action" => "release_required",
        "next_state" => "todo",
        _ => throw new ArgumentOutOfRangeException(nameof(property))
    };

    private static string[] Args(string task, string handoff, string ingestion, string store, string output) => ["finalize-handoff", "--task", task, "--handoff", handoff, "--ingestion", ingestion, "--lease-store", store, "--output", output];
    private static PacketSchemaRegistry Registry => PacketSchemaRegistry.Load(Path.Combine(Root, "docs", "agent", "schemas"));
    private static string Root { get { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName; throw new DirectoryNotFoundException(); } }

    private sealed class ThrowingWriter(string kind) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override void WriteLine(string? value) => throw kind switch { "io" => new IOException(), "disposed" => new ObjectDisposedException("writer"), "invalid" => new InvalidOperationException(), _ => new NotSupportedException() };
    }

    private sealed class MutableClock : ILeaseClock
    {
        private DateTimeOffset _now = DateTimeOffset.Parse("2026-07-23T10:00:00.0000000Z", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        public DateTimeOffset UtcNow => _now;
        internal void Advance(TimeSpan value) => _now = _now.Add(value);
    }

    private sealed class HandoffWorkspace : IDisposable
    {
        private HandoffWorkspace(string path) { Path = path; StorePath = System.IO.Path.Combine(path, "store"); Directory.CreateDirectory(StorePath); Clock = new MutableClock(); }
        internal string Path { get; }
        internal string StorePath { get; }
        internal MutableClock Clock { get; }
        internal static HandoffWorkspace Create() { var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tlaw-handoff-finalization-{Guid.NewGuid():N}"); Directory.CreateDirectory(path); return new HandoffWorkspace(path); }
        internal LocalLease Acquire() => new FileLeaseStore(StorePath, Clock).Acquire("BAR-40", "codex", TimeSpan.FromMinutes(5));
        internal string Write(string name, string text) { var path = System.IO.Path.Combine(Path, name); File.WriteAllText(path, text, new UTF8Encoding(false)); return path; }
        internal string LeaseFile() => Directory.EnumerateFiles(System.IO.Path.Combine(StorePath, "leases"), "*.json").Single();
        internal string LockFile() => Directory.EnumerateFiles(System.IO.Path.Combine(StorePath, "locks"), "*.lock").Single();
        internal IReadOnlyDictionary<string, byte[]> SnapshotStore() => Directory.EnumerateFiles(StorePath, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).ToDictionary(path => System.IO.Path.GetRelativePath(StorePath, path), File.ReadAllBytes, StringComparer.Ordinal);
        internal string TaskYaml(LocalLease lease)
        {
            var template = PacketValidator.Validate(File.ReadAllText(System.IO.Path.Combine(Root, "docs", "agent", "schemas", "examples", "task.v2.claimed.valid.yaml")), Registry);
            Assert.True(template.IsValid, string.Join("; ", template.Diagnostics.Select(diagnostic => diagnostic.Message)));
            return TaskPacketGenerator.Generate(TaskV2Packet.From(template.Packet!) with
            {
                TaskId = "BAR-40",
                SourceId = "BAR-26",
                ClaimedBy = lease.ClaimedBy,
                ClaimId = lease.ClaimId,
                ClaimStartedAt = Format(lease.ClaimStartedAt),
                ClaimExpiresAt = Format(lease.ClaimExpiresAt),
                BaseSha = "9608094501d18fe8472a7ebb66676777e6174e53",
                Worktree = "task/BAR-40-handoff-finalization"
            }, Registry);
        }

        internal string HandoffYaml(LocalLease lease, string status)
        {
            var handoff = File.ReadAllText(System.IO.Path.Combine(Root, "docs", "agent", "schemas", "examples", "handoff.v2.valid.yaml"))
                .Replace("task_id: BAR-38", "task_id: BAR-40", StringComparison.Ordinal)
                .Replace("source_id: BAR-38", "source_id: BAR-26", StringComparison.Ordinal)
                .Replace("status: ready", $"status: {status}", StringComparison.Ordinal)
                .Replace("claim_id: 0123456789abcdef0123456789abcdef", $"claim_id: {lease.ClaimId}", StringComparison.Ordinal)
                .Replace("base_sha: 33f8466cac487d5bd335f6bce7d33bd3814db64c", "base_sha: 9608094501d18fe8472a7ebb66676777e6174e53", StringComparison.Ordinal)
                .Replace("branch: task/BAR-38-handoff-v2", "branch: task/BAR-40-handoff-finalization", StringComparison.Ordinal);
            return status == "blocked" ? handoff.Replace("known_failures: []", "known_failures:\n  - blocked", StringComparison.Ordinal) : handoff;
        }
        internal string IngestionJson(LocalLease lease, string handoff, string status, string reason)
        {
            var taskValidation = PacketValidator.Validate(TaskYaml(lease), Registry);
            var handoffValidation = PacketValidator.Validate(File.ReadAllText(handoff), Registry);
            Assert.True(taskValidation.IsValid, string.Join("; ", taskValidation.Diagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.True(handoffValidation.IsValid, string.Join("; ", handoffValidation.Diagnostics.Select(diagnostic => diagnostic.Message)));
            return HandoffIngestionJson.Write(TaskV2Packet.From(taskValidation.Packet!), handoffValidation.Packet!, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(handoff))), reason == "timeout" ? LocalLeaseStatus.Expired : LocalLeaseStatus.Active, reason, status == "ready" ? "reassign" : "human");
        }
        private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
