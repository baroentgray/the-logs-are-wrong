using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class ResultFinalizationCommandTests
{
    [Theory]
    [InlineData("success", "completion", "in_review")]
    [InlineData("failed", "error", "todo")]
    public void Non_human_terminal_result_releases_exact_lease_and_writes_closed_finalization(string status, string reason, string nextState)
    {
        using var workspace = FinalizationWorkspace.Create();
        var lease = workspace.Acquire("BAR-36", "codex");
        var task = workspace.Write("task.yaml", TaskYaml("BAR-36", lease));
        var result = workspace.Write("result.yaml", ResultYaml("BAR-36", status));
        var ingestion = workspace.Write("ingestion.json", IngestionJson(lease, status, File.ReadAllBytes(result), humanRequired: false));
        var output = Path.Combine(workspace.Path, "finalization.json");
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);

        Assert.Equal(0, FinalizeResultCommand.Run(Args(task, result, ingestion, workspace.StorePath, output), stdout, TextWriter.Null));

        Assert.Equal($"FINALIZED: {reason} -> {nextState}" + Environment.NewLine, stdout.ToString());
        Assert.Equal(LocalLeaseStatus.Missing, new FileLeaseStore(workspace.StorePath, new SystemLeaseClock()).Inspect("BAR-36").Status);
        var bytes = File.ReadAllBytes(output);
        Assert.False(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        Assert.Equal("tlaw.dispatcher-finalization/v1", root.GetProperty("schema").GetString());
        Assert.Equal(lease.ClaimId, root.GetProperty("claim_id").GetString());
        Assert.Equal(status, root.GetProperty("result_status").GetString());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(result))), root.GetProperty("result_sha256").GetString());
        Assert.Equal(reason, root.GetProperty("release_reason").GetString());
        Assert.Equal(nextState, root.GetProperty("next_state").GetString());
        AssertPropertyOrder(bytes, "schema", "task_id", "claimed_by", "claim_id", "result_status", "result_sha256", "release_reason", "next_state");
    }

    [Theory]
    [InlineData("blocked")]
    [InlineData("human")]
    public void Blocked_or_human_required_result_fails_closed_without_release_or_output_mutation(string variant)
    {
        using var workspace = FinalizationWorkspace.Create();
        var lease = workspace.Acquire("BAR-36", "codex");
        var task = workspace.Write("task.yaml", TaskYaml("BAR-36", lease));
        var result = workspace.Write("result.yaml", variant == "blocked" ? ResultYaml("BAR-36", "blocked") : HumanResultYaml("BAR-36"));
        var ingestion = workspace.Write("ingestion.json", IngestionJson(lease, variant == "blocked" ? "blocked" : "blocked", File.ReadAllBytes(result), variant == "human"));
        var output = workspace.Write("finalization.json", "prior\n");
        var before = workspace.SnapshotStore();

        Assert.NotEqual(0, FinalizeResultCommand.Run(Args(task, result, ingestion, workspace.StorePath, output), TextWriter.Null, TextWriter.Null));

        Assert.Equal("prior\n", File.ReadAllText(output));
        Assert.Equal(before, workspace.SnapshotStore());
    }

    [Fact]
    public void Repeated_finalization_is_rejected_as_already_released()
    {
        using var workspace = FinalizationWorkspace.Create();
        var lease = workspace.Acquire("BAR-36", "codex");
        var task = workspace.Write("task.yaml", TaskYaml("BAR-36", lease));
        var result = workspace.Write("result.yaml", ResultYaml("BAR-36", "success"));
        var ingestion = workspace.Write("ingestion.json", IngestionJson(lease, "success", File.ReadAllBytes(result), false));
        var output = Path.Combine(workspace.Path, "finalization.json");

        Assert.Equal(0, FinalizeResultCommand.Run(Args(task, result, ingestion, workspace.StorePath, output), TextWriter.Null, TextWriter.Null));
        var first = File.ReadAllBytes(output);
        Assert.NotEqual(0, FinalizeResultCommand.Run(Args(task, result, ingestion, workspace.StorePath, output), TextWriter.Null, TextWriter.Null));
        Assert.Equal(first, File.ReadAllBytes(output));
    }

    [Theory]
    [InlineData("task-id")]
    [InlineData("agent")]
    [InlineData("token")]
    [InlineData("status")]
    [InlineData("human")]
    [InlineData("sha")]
    public void Contradictory_task_result_or_ingestion_evidence_preserves_all_pre_release_state(string mismatch)
    {
        using var workspace = FinalizationWorkspace.Create();
        var lease = workspace.Acquire("BAR-36", "codex");
        var task = workspace.Write("task.yaml", mismatch == "task-id" ? TaskYaml("OTHER", lease) : TaskYaml("BAR-36", lease));
        var result = workspace.Write("result.yaml", ResultYaml(mismatch == "task-id" ? "BAR-36" : "BAR-36", "success"));
        var ingestionLease = mismatch switch
        {
            "agent" => lease with { ClaimedBy = "claude" },
            "token" => lease with { ClaimId = Guid.NewGuid().ToString("N") },
            _ => lease
        };
        var ingestion = workspace.Write("ingestion.json", IngestionJson(ingestionLease, mismatch == "status" ? "failed" : "success", mismatch == "sha" ? Encoding.UTF8.GetBytes("wrong") : File.ReadAllBytes(result), mismatch == "human"));
        var output = workspace.Write("finalization.json", "prior\n");
        var before = workspace.SnapshotStore();

        Assert.NotEqual(0, FinalizeResultCommand.Run(Args(task, result, ingestion, workspace.StorePath, output), TextWriter.Null, TextWriter.Null));
        Assert.Equal("prior\n", File.ReadAllText(output));
        Assert.Equal(before, workspace.SnapshotStore());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("malformed")]
    [InlineData("released")]
    [InlineData("replaced")]
    public void Missing_expired_malformed_released_or_replaced_lease_fails_closed(string variant)
    {
        using var workspace = FinalizationWorkspace.Create();
        var clock = new MutableLeaseClock("2026-07-23T10:00:00.0000000Z");
        var lease = workspace.Acquire("BAR-36", "codex", clock);
        if (variant == "missing") File.Delete(workspace.LeaseFile());
        if (variant == "expired") clock.Advance(TimeSpan.FromHours(1));
        if (variant == "released") new FileLeaseStore(workspace.StorePath, clock).Release("BAR-36", lease.ClaimId, LeaseReleaseReason.Completion);
        if (variant == "malformed") File.WriteAllText(workspace.LeaseFile(), "{ bad", new UTF8Encoding(false));
        if (variant == "replaced") { clock.Advance(TimeSpan.FromHours(1)); workspace.Acquire("BAR-36", "claude", clock); }
        var task = workspace.Write("task.yaml", TaskYaml("BAR-36", lease));
        var result = workspace.Write("result.yaml", ResultYaml("BAR-36", "success"));
        var ingestion = workspace.Write("ingestion.json", IngestionJson(lease, "success", File.ReadAllBytes(result), false));
        var output = workspace.Write("finalization.json", "prior\n");

        Assert.NotEqual(0, FinalizeResultCommand.RunForTesting(Args(task, result, ingestion, workspace.StorePath, output), TextWriter.Null, TextWriter.Null, clock));
        Assert.Equal("prior\n", File.ReadAllText(output));
    }

    [Theory]
    [InlineData("task")]
    [InlineData("result")]
    [InlineData("ingestion")]
    [InlineData("lease")]
    [InlineData("lease-child")]
    public void Output_aliases_are_rejected_before_release(string alias)
    {
        using var workspace = FinalizationWorkspace.Create();
        var lease = workspace.Acquire("BAR-36", "codex");
        var task = workspace.Write("task.yaml", TaskYaml("BAR-36", lease));
        var result = workspace.Write("result.yaml", ResultYaml("BAR-36", "success"));
        var ingestion = workspace.Write("ingestion.json", IngestionJson(lease, "success", File.ReadAllBytes(result), false));
        var output = alias switch { "task" => task, "result" => result, "ingestion" => ingestion, "lease" => workspace.LeaseFile(), _ => Path.Combine(workspace.StorePath, "other.json") };
        var before = workspace.SnapshotStore();

        Assert.NotEqual(0, FinalizeResultCommand.Run(Args(task, result, ingestion, workspace.StorePath, output), TextWriter.Null, TextWriter.Null));
        Assert.Equal(before, workspace.SnapshotStore());
        Assert.Empty(Directory.EnumerateFiles(workspace.Path, ".*.tmp", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("malformed")]
    [InlineData("invalid-utf8")]
    public void Closed_ingestion_parser_rejects_invalid_json_without_release(string variant)
    {
        using var workspace = FinalizationWorkspace.Create();
        var lease = workspace.Acquire("BAR-36", "codex");
        var task = workspace.Write("task.yaml", TaskYaml("BAR-36", lease));
        var result = workspace.Write("result.yaml", ResultYaml("BAR-36", "success"));
        var content = IngestionJson(lease, "success", File.ReadAllBytes(result), false);
        content = variant switch { "unknown" => content.Replace("\n}", ",\n  \"extra\": true\n}", StringComparison.Ordinal), "duplicate" => content.Replace("\n}", ",\n  \"claim_id\": \"duplicate\"\n}", StringComparison.Ordinal), "malformed" => "{", _ => content };
        var ingestion = workspace.Write("ingestion.json", content);
        if (variant == "invalid-utf8") File.WriteAllBytes(ingestion, [0xFF, 0xFE]);
        var output = Path.Combine(workspace.Path, "finalization.json");

        Assert.NotEqual(0, FinalizeResultCommand.Run(Args(task, result, ingestion, workspace.StorePath, output), TextWriter.Null, TextWriter.Null));
        Assert.Equal(LocalLeaseStatus.Active, new FileLeaseStore(workspace.StorePath, new SystemLeaseClock()).Inspect("BAR-36").Status);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Publication_failure_after_release_reports_the_non_transactional_state_honestly()
    {
        using var workspace = FinalizationWorkspace.Create();
        var lease = workspace.Acquire("BAR-36", "codex");
        var task = workspace.Write("task.yaml", TaskYaml("BAR-36", lease));
        var result = workspace.Write("result.yaml", ResultYaml("BAR-36", "success"));
        var ingestion = workspace.Write("ingestion.json", IngestionJson(lease, "success", File.ReadAllBytes(result), false));
        var output = Path.Combine(workspace.Path, "missing", "finalization.json");
        using var errors = new StringWriter(CultureInfo.InvariantCulture);

        Assert.NotEqual(0, FinalizeResultCommand.Run(Args(task, result, ingestion, workspace.StorePath, output), TextWriter.Null, errors));
        Assert.Equal(LocalLeaseStatus.Missing, new FileLeaseStore(workspace.StorePath, new SystemLeaseClock()).Inspect("BAR-36").Status);
        Assert.Contains("already released", errors.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not published", errors.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string[] Args(string task, string result, string ingestion, string store, string output) => ["finalize-result", "--task", task, "--result", result, "--ingestion", ingestion, "--lease-store", store, "--output", output];
    private static string TaskYaml(string taskId, LocalLease lease) => $$"""
        schema: tlaw.agent-task/v2
        task_id: {{taskId}}
        source_id: BAR-36
        sources: [docs/agent/AGENT_PROTOCOL.md]
        objective: Finalize a result.
        work_type: implementation
        preferred_agent: codex
        eligible_agents: [codex]
        required_capabilities: [dotnet]
        autonomy_level: branch_write
        forbidden_operations: [Merge pull requests.]
        claimed_by: {{lease.ClaimedBy}}
        claim_id: {{lease.ClaimId}}
        claim_started_at: {{Format(lease.ClaimStartedAt)}}
        claim_expires_at: {{Format(lease.ClaimExpiresAt)}}
        base_sha: 0f36ef2e48a40f80a7ddd2428113a24ffb9564f4
        handoff_required: true
        worktree: task/BAR-36-result-finalization
        verification: { required: true, commands: [dotnet test --configuration Release] }
        delivery: { branch_required: true, draft_pr_required: true, merge_forbidden: true }
        """;
    private static string ResultYaml(string taskId, string status) => $$"""
        schema: tlaw.agent-result/v1
        task_id: {{taskId}}
        status: {{status}}
        human_summary: Finalization evidence.
        evidence: [{ kind: command, reference: dotnet test }]
        human: { required: false, question: No decision is required., safe_options: [] }
        """;
    private static string HumanResultYaml(string taskId) => ResultYaml(taskId, "blocked").Replace("required: false", "required: true", StringComparison.Ordinal).Replace("safe_options: []", "safe_options: [Stop safely]", StringComparison.Ordinal);
    private static string IngestionJson(LocalLease lease, string status, byte[] result, bool humanRequired) => $$"""
        {
          "schema": "tlaw.dispatcher-ingestion/v1",
          "task_id": "{{lease.TaskId}}",
          "claimed_by": "{{lease.ClaimedBy}}",
          "claim_id": "{{lease.ClaimId}}",
          "result_status": "{{status}}",
          "result_sha256": "{{Convert.ToHexStringLower(SHA256.HashData(result))}}",
          "human_required": {{humanRequired.ToString().ToLowerInvariant()}},
          "projection": "Evidence"
        }
        """;
    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
    private static void AssertPropertyOrder(byte[] json, params string[] properties) => Assert.True(properties.Select(property => Encoding.UTF8.GetString(json).IndexOf($"\"{property}\"", StringComparison.Ordinal)).SequenceEqual(properties.Select(property => Encoding.UTF8.GetString(json).IndexOf($"\"{property}\"", StringComparison.Ordinal)).Order()));

    private sealed class MutableLeaseClock(string value) : ILeaseClock { private DateTimeOffset _now = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal); public DateTimeOffset UtcNow => _now; internal void Advance(TimeSpan value) => _now = _now.Add(value); }
    private sealed class FinalizationWorkspace : IDisposable
    {
        private FinalizationWorkspace(string path) { Path = path; StorePath = System.IO.Path.Combine(path, "lease-store"); Directory.CreateDirectory(StorePath); }
        internal string Path { get; } internal string StorePath { get; }
        internal static FinalizationWorkspace Create() { var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tlaw-finalization-{Guid.NewGuid():N}"); Directory.CreateDirectory(path); return new FinalizationWorkspace(path); }
        internal LocalLease Acquire(string id, string agent, ILeaseClock? clock = null) => new FileLeaseStore(StorePath, clock ?? new SystemLeaseClock()).Acquire(id, agent, TimeSpan.FromHours(1));
        internal string Write(string name, string content) { var path = System.IO.Path.Combine(Path, name); File.WriteAllText(path, content, new UTF8Encoding(false)); return path; }
        internal string LeaseFile() => Directory.EnumerateFiles(StorePath, "*.json", SearchOption.AllDirectories).Single();
        internal IReadOnlyDictionary<string, byte[]> SnapshotStore() => Directory.EnumerateFiles(StorePath, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).ToDictionary(path => System.IO.Path.GetRelativePath(StorePath, path), File.ReadAllBytes, StringComparer.Ordinal);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
