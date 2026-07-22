using System.Globalization;
using System.Text;
using System.Text.Json;
using Tlaw.AgentProtocol;
using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class ResultIngestionCommandTests
{
    [Fact]
    public void Matching_claimed_task_active_lease_and_success_result_write_deterministic_lf_no_bom_ingestion()
    {
        using var workspace = IngestionWorkspace.Create();
        var lease = workspace.Acquire("BAR-35", "codex");
        var task = workspace.Write("task.yaml", TaskYaml("BAR-35", lease));
        var result = workspace.Write("result.yaml", ResultYaml("BAR-35", "success"));
        var firstOutput = Path.Combine(workspace.Path, "first-ingestion.json");
        var secondOutput = Path.Combine(workspace.Path, "second-ingestion.json");
        using var standardOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var standardError = new StringWriter(CultureInfo.InvariantCulture);
        var leaseBefore = workspace.SnapshotStore();
        var taskBefore = File.ReadAllBytes(task);
        var resultBefore = File.ReadAllBytes(result);

        Assert.Equal(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", result, "--lease-store", workspace.StorePath, "--output", firstOutput], standardOutput, standardError));
        Assert.Equal(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", result, "--lease-store", workspace.StorePath, "--output", secondOutput], TextWriter.Null, TextWriter.Null));

        var first = File.ReadAllBytes(firstOutput);
        Assert.Equal(first, File.ReadAllBytes(secondOutput));
        Assert.False(first.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.DoesNotContain((byte)'\r', first);
        Assert.Equal((byte)'\n', first[^1]);
        Assert.False(first.Length > 1 && first[^2] == (byte)'\n');
        Assert.Equal("SUCCESS\nCompleted local evidence ingestion." + Environment.NewLine, standardOutput.ToString());
        Assert.Equal(string.Empty, standardError.ToString());
        Assert.Equal(leaseBefore, workspace.SnapshotStore());
        Assert.Equal(taskBefore, File.ReadAllBytes(task));
        Assert.Equal(resultBefore, File.ReadAllBytes(result));

        using var document = JsonDocument.Parse(first);
        var root = document.RootElement;
        Assert.Equal("tlaw.dispatcher-ingestion/v1", root.GetProperty("schema").GetString());
        Assert.Equal("BAR-35", root.GetProperty("task_id").GetString());
        Assert.Equal("codex", root.GetProperty("claimed_by").GetString());
        Assert.Equal(lease.ClaimId, root.GetProperty("claim_id").GetString());
        Assert.Equal("success", root.GetProperty("result_status").GetString());
        Assert.False(root.GetProperty("human_required").GetBoolean());
        Assert.Equal("SUCCESS\nCompleted local evidence ingestion.", root.GetProperty("projection").GetString());
        AssertPropertyOrder(first, "schema", "task_id", "claimed_by", "claim_id", "result_status", "human_required", "projection");
    }

    [Fact]
    public void Existing_output_is_replaced_only_after_complete_success_and_preserved_on_failure()
    {
        using var workspace = IngestionWorkspace.Create();
        var lease = workspace.Acquire("BAR-35", "codex");
        var task = workspace.Write("task.yaml", TaskYaml("BAR-35", lease));
        var validResult = workspace.Write("result.yaml", ResultYaml("BAR-35", "success"));
        var mismatch = workspace.Write("mismatch.yaml", ResultYaml("OTHER", "success"));
        var output = Path.Combine(workspace.Path, "ingestion.json");
        var original = "previous ingestion\n";
        File.WriteAllText(output, original, new UTF8Encoding(false));

        Assert.NotEqual(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", mismatch, "--lease-store", workspace.StorePath, "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.Equal(original, File.ReadAllText(output));

        Assert.Equal(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", validResult, "--lease-store", workspace.StorePath, "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.NotEqual(original, File.ReadAllText(output));
        Assert.Empty(Directory.EnumerateFiles(workspace.Path, ".ingestion.json.*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void Human_required_blocked_result_projects_only_summary_question_evidence_and_safe_options()
    {
        using var workspace = IngestionWorkspace.Create();
        var lease = workspace.Acquire("BAR-35", "codex");
        var task = workspace.Write("task.yaml", TaskYaml("BAR-35", lease));
        var result = workspace.Write("result.yaml", BlockedHumanResultYaml("BAR-35"));
        var output = Path.Combine(workspace.Path, "ingestion.json");
        using var standardOutput = new StringWriter(CultureInfo.InvariantCulture);

        Assert.Equal(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", result, "--lease-store", workspace.StorePath, "--output", output], standardOutput, TextWriter.Null));

        var projection = "Awaiting a human decision.\nQuestion: Which verified recovery path should be used?\nEvidence: artifacts/evidence.txt\nOption: Retry after repair\nOption: Stop safely";
        Assert.Equal(projection + Environment.NewLine, standardOutput.ToString());
        Assert.DoesNotContain("task_id", standardOutput.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("claim_id", standardOutput.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(workspace.StorePath, standardOutput.ToString(), StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(File.ReadAllText(output));
        Assert.True(document.RootElement.GetProperty("human_required").GetBoolean());
        Assert.Equal(projection, document.RootElement.GetProperty("projection").GetString());
    }

    [Fact]
    public void Non_human_failed_result_remains_failed_without_lease_mutation()
    {
        using var workspace = IngestionWorkspace.Create();
        var lease = workspace.Acquire("BAR-35", "codex");
        var task = workspace.Write("task.yaml", TaskYaml("BAR-35", lease));
        var result = workspace.Write("result.yaml", ResultYaml("BAR-35", "failed"));
        var output = Path.Combine(workspace.Path, "ingestion.json");
        var before = workspace.SnapshotStore();
        using var standardOutput = new StringWriter(CultureInfo.InvariantCulture);

        Assert.Equal(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", result, "--lease-store", workspace.StorePath, "--output", output], standardOutput, TextWriter.Null));

        Assert.Equal("FAILED\nCompleted local evidence ingestion." + Environment.NewLine, standardOutput.ToString());
        using var document = JsonDocument.Parse(File.ReadAllText(output));
        Assert.Equal("failed", document.RootElement.GetProperty("result_status").GetString());
        Assert.Equal(before, workspace.SnapshotStore());
    }

    [Theory]
    [InlineData("--task")]
    [InlineData("--result")]
    [InlineData("--lease-store")]
    [InlineData("--output")]
    public void Unknown_duplicate_missing_and_empty_command_options_fail_without_output(string duplicate)
    {
        using var workspace = IngestionWorkspace.Create();
        var lease = workspace.Acquire("BAR-35", "codex");
        var task = workspace.Write("task.yaml", TaskYaml("BAR-35", lease));
        var result = workspace.Write("result.yaml", ResultYaml("BAR-35", "success"));
        var output = Path.Combine(workspace.Path, "ingestion.json");

        Assert.NotEqual(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", result, "--lease-store", workspace.StorePath, "--output", output, duplicate, task], TextWriter.Null, TextWriter.Null));
        Assert.NotEqual(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", result, "--lease-store", workspace.StorePath, "--unexpected", output], TextWriter.Null, TextWriter.Null));
        Assert.NotEqual(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", result, "--lease-store", workspace.StorePath, "--output"], TextWriter.Null, TextWriter.Null));
        Assert.NotEqual(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", result, "--lease-store", workspace.StorePath, "--output", ""], TextWriter.Null, TextWriter.Null));
        Assert.NotEqual(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", result, "--lease-store", "relative-store", "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("unclaimed")]
    [InlineData("partial")]
    [InlineData("v1")]
    [InlineData("unknown")]
    public void Unclaimed_partial_v1_and_unknown_task_packets_fail_closed(string variant)
    {
        using var workspace = IngestionWorkspace.Create();
        var lease = workspace.Acquire("BAR-35", "codex");
        var taskYaml = variant switch
        {
            "unclaimed" => UnclaimedTaskYaml("BAR-35"),
            "partial" => TaskYaml("BAR-35", lease).Replace($"claim_id: {lease.ClaimId}", "claim_id: unclaimed", StringComparison.Ordinal),
            "v1" => File.ReadAllText(Path.Combine(ExamplesRoot, "task.valid.yaml")),
            _ => "schema: tlaw.agent-task/v999\ntask_id: BAR-35\n"
        };
        var task = workspace.Write($"{variant}-task.yaml", taskYaml);
        var result = workspace.Write("result.yaml", ResultYaml("BAR-35", "success"));
        var output = Path.Combine(workspace.Path, "ingestion.json");

        Assert.NotEqual(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", result, "--lease-store", workspace.StorePath, "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("task-mismatch")]
    [InlineData("review")]
    [InlineData("handoff")]
    [InlineData("unknown-schema")]
    [InlineData("malformed")]
    [InlineData("duplicate-key")]
    [InlineData("missing-evidence")]
    [InlineData("anchor")]
    [InlineData("alias")]
    [InlineData("tag")]
    [InlineData("merge-key")]
    public void Invalid_or_non_result_packets_fail_closed_without_replacing_output(string variant)
    {
        using var workspace = IngestionWorkspace.Create();
        var lease = workspace.Acquire("BAR-35", "codex");
        var task = workspace.Write("task.yaml", TaskYaml("BAR-35", lease));
        var resultYaml = variant switch
        {
            "task-mismatch" => ResultYaml("BAR-OTHER", "success"),
            "review" => File.ReadAllText(Path.Combine(ExamplesRoot, "review.valid.yaml")),
            "handoff" => File.ReadAllText(Path.Combine(ExamplesRoot, "handoff.valid.yaml")),
            "unknown-schema" => "schema: tlaw.agent-result/v999\ntask_id: BAR-35\n",
            "malformed" => "schema: [\n",
            "duplicate-key" => "schema: tlaw.agent-result/v1\nschema: tlaw.agent-result/v1\n",
            "missing-evidence" => MissingEvidenceResultYaml("BAR-35"),
            "anchor" => "schema: tlaw.agent-result/v1\ntask_id: BAR-35\nstatus: success\nhuman_summary: &summary rejected\nevidence:\n  - kind: command\n    reference: evidence\nhuman:\n  required: false\n  question: None\n  safe_options: []\n",
            "alias" => "schema: tlaw.agent-result/v1\ntask_id: *summary\n",
            "tag" => "schema: tlaw.agent-result/v1\ntask_id: !unsafe BAR-35\n",
            _ => "schema: tlaw.agent-result/v1\n<<: { task_id: BAR-35 }\n"
        };
        var result = workspace.Write($"{variant}.yaml", resultYaml);
        var output = Path.Combine(workspace.Path, "ingestion.json");
        const string previous = "previous\n";
        File.WriteAllText(output, previous, new UTF8Encoding(false));

        Assert.NotEqual(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", result, "--lease-store", workspace.StorePath, "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.Equal(previous, File.ReadAllText(output));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("corrupt")]
    [InlineData("duplicate")]
    [InlineData("wrong-task")]
    [InlineData("wrong-agent")]
    [InlineData("wrong-token")]
    [InlineData("replaced")]
    public void Missing_expired_corrupt_and_mismatched_lease_evidence_fails_closed(string variant)
    {
        using var workspace = IngestionWorkspace.Create();
        LocalLease? lease = null;
        if (variant == "expired")
        {
            lease = workspace.Acquire("BAR-35", "codex", new TestLeaseClock("2000-01-01T00:00:00.0000000Z"));
        }
        else if (variant != "missing")
        {
            lease = workspace.Acquire("BAR-35", "codex");
        }

        var taskLease = lease ?? new LocalLease("BAR-35", "codex", Guid.NewGuid().ToString("N"), DateTimeOffset.ParseExact("2026-01-01T00:00:00.0000000Z", "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal), DateTimeOffset.ParseExact("2099-01-01T00:00:00.0000000Z", "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal));
        var taskYaml = TaskYaml("BAR-35", taskLease);
        if (variant == "wrong-agent")
        {
            taskYaml = TaskYaml("BAR-35", taskLease, claimedBy: "claude");
        }
        else if (variant == "wrong-token")
        {
            taskYaml = TaskYaml("BAR-35", taskLease, claimId: Guid.NewGuid().ToString("N"));
        }

        if (lease is not null && variant is "corrupt" or "duplicate" or "wrong-task" or "replaced")
        {
            var leaseFile = workspace.LeaseFile();
            var contents = File.ReadAllText(leaseFile);
            contents = variant switch
            {
                "corrupt" => "{ not json",
                "duplicate" => AppendDuplicateProperty(contents, "claim_id", Guid.NewGuid().ToString("N")),
                "wrong-task" => contents.Replace("\"task_id\":\"BAR-35\"", "\"task_id\":\"OTHER\"", StringComparison.Ordinal),
                "replaced" => LeaseJson("BAR-35", "claude", Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)),
                _ => contents
            };
            File.WriteAllText(leaseFile, contents, new UTF8Encoding(false));
        }

        var task = workspace.Write("task.yaml", taskYaml);
        var result = workspace.Write("result.yaml", ResultYaml("BAR-35", "success"));
        var output = Path.Combine(workspace.Path, "ingestion.json");
        const string previous = "previous\n";
        File.WriteAllText(output, previous, new UTF8Encoding(false));
        var before = workspace.SnapshotStore();
        using var errors = new StringWriter(CultureInfo.InvariantCulture);

        Assert.NotEqual(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", result, "--lease-store", workspace.StorePath, "--output", output], TextWriter.Null, errors));
        Assert.Equal(previous, File.ReadAllText(output));
        Assert.Equal(before, workspace.SnapshotStore());
        Assert.DoesNotContain("launch", errors.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dispatch", errors.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("linear", errors.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github", errors.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Successful_and_failed_ingestion_preserve_task_result_and_lease_store_bytes()
    {
        using var workspace = IngestionWorkspace.Create();
        var lease = workspace.Acquire("BAR-35", "codex");
        var task = workspace.Write("task.yaml", TaskYaml("BAR-35", lease));
        var result = workspace.Write("result.yaml", ResultYaml("BAR-35", "success"));
        var mismatch = workspace.Write("mismatch.yaml", ResultYaml("OTHER", "success"));
        var successOutput = Path.Combine(workspace.Path, "success.json");
        var failureOutput = Path.Combine(workspace.Path, "failure.json");
        var taskBefore = File.ReadAllBytes(task);
        var resultBefore = File.ReadAllBytes(result);
        var mismatchBefore = File.ReadAllBytes(mismatch);
        var leaseBefore = workspace.SnapshotStore();

        Assert.Equal(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", result, "--lease-store", workspace.StorePath, "--output", successOutput], TextWriter.Null, TextWriter.Null));
        Assert.NotEqual(0, IngestResultCommand.Run(["ingest-result", "--task", task, "--result", mismatch, "--lease-store", workspace.StorePath, "--output", failureOutput], TextWriter.Null, TextWriter.Null));

        Assert.Equal(taskBefore, File.ReadAllBytes(task));
        Assert.Equal(resultBefore, File.ReadAllBytes(result));
        Assert.Equal(mismatchBefore, File.ReadAllBytes(mismatch));
        Assert.Equal(leaseBefore, workspace.SnapshotStore());
        Assert.False(File.Exists(failureOutput));
    }

    private static string TaskYaml(string taskId, LocalLease lease, string? claimedBy = null, string? claimId = null) => $$"""
        schema: tlaw.agent-task/v2
        task_id: {{taskId}}
        source_id: BAR-35
        sources:
          - docs/agent/AGENT_PROTOCOL.md
        objective: Ingest correlated local result evidence.
        work_type: implementation
        preferred_agent: codex
        eligible_agents:
          - codex
          - claude
        required_capabilities:
          - dotnet
          - yaml_protocol
        autonomy_level: branch_write
        forbidden_operations:
          - Merge pull requests.
        claimed_by: {{claimedBy ?? lease.ClaimedBy}}
        claim_id: {{claimId ?? lease.ClaimId}}
        claim_started_at: {{FormatCanonicalTimestamp(lease.ClaimStartedAt)}}
        claim_expires_at: {{FormatCanonicalTimestamp(lease.ClaimExpiresAt)}}
        base_sha: 1344840c7eaa48deb88076c14a6c5ac273525f28
        handoff_required: true
        worktree: task/BAR-35-result-ingestion
        verification:
          required: true
          commands:
            - dotnet test --configuration Release
        delivery:
          branch_required: true
          draft_pr_required: true
          merge_forbidden: true
        """;

    private static string UnclaimedTaskYaml(string taskId) => TaskYaml(taskId, new LocalLease(taskId, "unclaimed", "unclaimed", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)).Replace("claim_started_at: 1970-01-01T00:00:00.0000000Z", "claim_started_at: unclaimed", StringComparison.Ordinal).Replace("claim_expires_at: 1970-01-01T00:00:00.0000000Z", "claim_expires_at: unclaimed", StringComparison.Ordinal);

    private static string ResultYaml(string taskId, string status) => $$"""
        schema: tlaw.agent-result/v1
        task_id: {{taskId}}
        status: {{status}}
        human_summary: Completed local evidence ingestion.
        evidence:
          - kind: command
            reference: dotnet test --configuration Release
        human:
          required: false
          question: No human decision is required.
          safe_options: []
        """;

    private static string MissingEvidenceResultYaml(string taskId) => $$"""
        schema: tlaw.agent-result/v1
        task_id: {{taskId}}
        status: success
        human_summary: Missing evidence must fail.
        human:
          required: false
          question: No human decision is required.
          safe_options: []
        """;

    private static string BlockedHumanResultYaml(string taskId) => $$"""
        schema: tlaw.agent-result/v1
        task_id: {{taskId}}
        status: blocked
        human_summary: Awaiting a human decision.
        evidence:
          - kind: file
            reference: artifacts/evidence.txt
        human:
          required: true
          question: Which verified recovery path should be used?
          safe_options:
            - Retry after repair
            - Stop safely
        """;

    private static string LeaseJson(string taskId, string claimedBy, string claimId, DateTimeOffset startedAt, DateTimeOffset expiresAt) => JsonSerializer.Serialize(new
    {
        schema = "tlaw.local-lease/v1",
        task_id = taskId,
        claimed_by = claimedBy,
        claim_id = claimId,
        claim_started_at = FormatCanonicalTimestamp(startedAt),
        claim_expires_at = FormatCanonicalTimestamp(expiresAt)
    });

    private static string FormatCanonicalTimestamp(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    private static string AppendDuplicateProperty(string json, string property, string value)
    {
        var closingBrace = json.LastIndexOf('}');
        Assert.True(closingBrace >= 0);
        return $"{json[..closingBrace]},\"{property}\":\"{value}\"{json[closingBrace..]}";
    }

    private static void AssertPropertyOrder(byte[] json, params string[] properties)
    {
        var text = Encoding.UTF8.GetString(json);
        var positions = properties.Select(property => text.IndexOf($"\"{property}\"", StringComparison.Ordinal)).ToArray();
        Assert.All(positions, position => Assert.True(position >= 0));
        Assert.True(positions.SequenceEqual(positions.Order()));
    }

    private static string ExamplesRoot => Path.Combine(FindRepositoryRoot(), "docs", "agent", "schemas", "examples");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class TestLeaseClock(string timestamp) : ILeaseClock
    {
        public DateTimeOffset UtcNow { get; } = DateTimeOffset.ParseExact(timestamp, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    private sealed class IngestionWorkspace : IDisposable
    {
        private IngestionWorkspace(string path)
        {
            Path = path;
            StorePath = System.IO.Path.Combine(path, "lease-store");
            Directory.CreateDirectory(StorePath);
        }

        internal string Path { get; }
        internal string StorePath { get; }

        internal static IngestionWorkspace Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tlaw-ingestion-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new IngestionWorkspace(path);
        }

        internal LocalLease Acquire(string taskId, string executor, ILeaseClock? clock = null) => new FileLeaseStore(StorePath, clock ?? new SystemLeaseClock()).Acquire(taskId, executor, TimeSpan.FromHours(1));

        internal string Write(string name, string content)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        internal string LeaseFile() => Directory.EnumerateFiles(StorePath, "*.json", SearchOption.AllDirectories).Single();

        internal IReadOnlyDictionary<string, byte[]> SnapshotStore() => Directory.Exists(StorePath)
            ? Directory.EnumerateFiles(StorePath, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(path => System.IO.Path.GetRelativePath(StorePath, path), File.ReadAllBytes, StringComparer.Ordinal)
            : new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
