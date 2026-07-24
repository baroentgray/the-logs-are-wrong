using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tlaw.AgentProtocol;
using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class LocalWorkerCompleteCommandTests
{
    [Fact]
    public void Valid_artifact_and_exact_active_local_lease_publish_a_deterministic_schema_valid_result()
    {
        using var workspace = CompletionWorkspace.Create();
        var clock = new MutableLeaseClock("2026-07-24T10:00:00.0000000Z");
        var fixture = workspace.CreateClaimedLocalTask(clock, "BAR-27");
        var artifact = workspace.Write("artifact.json", ValidArtifact(fixture.Lease));
        var first = Path.Combine(workspace.Path, "first-result.yaml");
        var second = Path.Combine(workspace.Path, "second-result.yaml");
        var taskBefore = File.ReadAllBytes(fixture.TaskPath);
        var artifactBefore = File.ReadAllBytes(artifact);
        var leaseBefore = workspace.SnapshotStore();

        Assert.Equal(0, Run(fixture.TaskPath, fixture.StorePath, artifact, first, clock));
        Assert.Equal(0, Run(fixture.TaskPath, fixture.StorePath, artifact, second, clock));

        var firstBytes = File.ReadAllBytes(first);
        Assert.Equal(firstBytes, File.ReadAllBytes(second));
        Assert.False(firstBytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.DoesNotContain((byte)'\r', firstBytes);
        Assert.Equal((byte)'\n', firstBytes[^1]);
        Assert.NotEqual((byte)'\n', firstBytes[^2]);
        Assert.Equal(taskBefore, File.ReadAllBytes(fixture.TaskPath));
        Assert.Equal(artifactBefore, File.ReadAllBytes(artifact));
        Assert.Equal(leaseBefore, workspace.SnapshotStore());

        var validation = PacketValidator.Validate(Encoding.UTF8.GetString(firstBytes), Registry());
        Assert.True(validation.IsValid, string.Join("; ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var result = validation.Packet!;
        Assert.Equal("tlaw.agent-result/v1", result.Schema);
        Assert.Equal("BAR-27", result.RequiredString("task_id"));
        Assert.Equal("success", result.RequiredString("status"));
        Assert.Equal("Validated local read-only analysis artifact produced.", result.RequiredString("human_summary"));
        Assert.False(result.RequiredBoolean("human", "required"));
        var evidence = Assert.Single(result.Evidence());
        Assert.Equal("file", evidence.Kind);
        Assert.Contains("tlaw.local-worker-artifact/v1", evidence.Reference, StringComparison.Ordinal);
        Assert.Contains(Convert.ToHexStringLower(SHA256.HashData(artifactBefore)), evidence.Reference, StringComparison.Ordinal);
        Assert.Contains("operation=contract_extraction", evidence.Reference, StringComparison.Ordinal);
        Assert.Contains("model=approved-local", evidence.Reference, StringComparison.Ordinal);
        Assert.Contains("non_authoritative=true", evidence.Reference, StringComparison.Ordinal);
        Assert.Contains("commands_executed=false", evidence.Reference, StringComparison.Ordinal);
        Assert.DoesNotContain("Read-only analysis", result.RequiredString("human_summary"), StringComparison.Ordinal);
        AssertPropertyOrder(firstBytes, "schema", "task_id", "status", "human_summary", "evidence", "human");
    }

    [Theory]
    [InlineData("task-id")]
    [InlineData("claimed-by")]
    [InlineData("claim-id")]
    [InlineData("claim-start")]
    [InlineData("claim-expiry")]
    [InlineData("malformed")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("invalid-hash")]
    [InlineData("unsupported-schema")]
    [InlineData("authority")]
    [InlineData("commands")]
    [InlineData("verification-claim")]
    [InlineData("modified-model")]
    public void Invalid_or_mismatched_artifact_fails_closed_and_preserves_an_existing_output(string variant)
    {
        using var workspace = CompletionWorkspace.Create();
        var clock = new MutableLeaseClock("2026-07-24T10:00:00.0000000Z");
        var fixture = workspace.CreateClaimedLocalTask(clock, "BAR-27");
        var artifact = workspace.Write("artifact.json", MutateArtifact(ValidArtifact(fixture.Lease), fixture.Lease, variant));
        var output = workspace.Write("result.yaml", "previous result\n");
        var taskBefore = File.ReadAllBytes(fixture.TaskPath);
        var artifactBefore = File.ReadAllBytes(artifact);
        var leaseBefore = workspace.SnapshotStore();

        Assert.NotEqual(0, Run(fixture.TaskPath, fixture.StorePath, artifact, output, clock));

        Assert.Equal("previous result\n", File.ReadAllText(output));
        Assert.Equal(taskBefore, File.ReadAllBytes(fixture.TaskPath));
        Assert.Equal(artifactBefore, File.ReadAllBytes(artifact));
        Assert.Equal(leaseBefore, workspace.SnapshotStore());
        Assert.Empty(Directory.EnumerateFiles(workspace.Path, ".result.yaml.*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Theory]
    [InlineData("bom")]
    [InlineData("invalid-utf8")]
    public void Artifact_with_bom_or_invalid_utf8_fails_closed(string variant)
    {
        using var workspace = CompletionWorkspace.Create();
        var clock = new MutableLeaseClock("2026-07-24T10:00:00.0000000Z");
        var fixture = workspace.CreateClaimedLocalTask(clock, "BAR-27");
        var artifact = Path.Combine(workspace.Path, "artifact.json");
        var bytes = Encoding.UTF8.GetBytes(ValidArtifact(fixture.Lease));
        File.WriteAllBytes(artifact, variant == "bom" ? [0xEF, 0xBB, 0xBF, .. bytes] : [0xFF, 0xFE, .. bytes]);
        var output = workspace.Write("result.yaml", "previous result\n");

        Assert.NotEqual(0, Run(fixture.TaskPath, fixture.StorePath, artifact, output, clock));
        Assert.Equal("previous result\n", File.ReadAllText(output));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("malformed")]
    [InlineData("replaced")]
    [InlineData("busy")]
    public void Missing_expired_malformed_replaced_or_busy_lease_fails_closed(string variant)
    {
        using var workspace = CompletionWorkspace.Create();
        var clock = new MutableLeaseClock("2026-07-24T10:00:00.0000000Z");
        var fixture = workspace.CreateClaimedLocalTask(clock, "BAR-27", TimeSpan.FromMinutes(1));
        var artifact = workspace.Write("artifact.json", ValidArtifact(fixture.Lease));
        var output = workspace.Write("result.yaml", "previous result\n");

        if (variant == "missing") File.Delete(fixture.LeasePath);
        if (variant == "expired") clock.Advance(TimeSpan.FromMinutes(1));
        if (variant == "malformed") File.WriteAllText(fixture.LeasePath, "{ malformed", new UTF8Encoding(false));
        if (variant == "replaced")
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            _ = new FileLeaseStore(fixture.StorePath, clock).Acquire("BAR-27", "local", TimeSpan.FromMinutes(5));
        }

        if (variant == "busy")
        {
            using var lockHandle = new FileStream(fixture.LockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.NotEqual(0, Run(fixture.TaskPath, fixture.StorePath, artifact, output, clock));
        }
        else
        {
            Assert.NotEqual(0, Run(fixture.TaskPath, fixture.StorePath, artifact, output, clock));
        }

        Assert.Equal("previous result\n", File.ReadAllText(output));
    }

    [Theory]
    [InlineData("task")]
    [InlineData("artifact")]
    [InlineData("repository")]
    [InlineData("git")]
    [InlineData("lease-store")]
    public void Output_aliases_and_forbidden_locations_fail_before_publication(string location)
    {
        using var workspace = CompletionWorkspace.Create();
        var clock = new MutableLeaseClock("2026-07-24T10:00:00.0000000Z");
        var fixture = workspace.CreateClaimedLocalTask(clock, "BAR-27");
        var artifact = workspace.Write("artifact.json", ValidArtifact(fixture.Lease));
        var output = location switch
        {
            "task" => fixture.TaskPath,
            "artifact" => artifact,
            "repository" => Path.Combine(RepositoryRoot(), ".bar27-complete-forbidden.yaml"),
            "git" => Path.Combine(RepositoryRoot(), ".git", "bar27-complete-forbidden.yaml"),
            _ => Path.Combine(fixture.StorePath, "result.yaml")
        };
        var taskBefore = File.ReadAllBytes(fixture.TaskPath);
        var artifactBefore = File.ReadAllBytes(artifact);
        var leaseBefore = workspace.SnapshotStore();

        Assert.NotEqual(0, Run(fixture.TaskPath, fixture.StorePath, artifact, output, clock));

        Assert.Equal(taskBefore, File.ReadAllBytes(fixture.TaskPath));
        Assert.Equal(artifactBefore, File.ReadAllBytes(artifact));
        Assert.Equal(leaseBefore, workspace.SnapshotStore());
        Assert.False(location is "repository" or "git" && File.Exists(output));
    }

    [Theory]
    [InlineData("expiry")]
    [InlineData("replacement")]
    public void Lease_is_rechecked_immediately_before_result_publication(string boundary)
    {
        using var workspace = CompletionWorkspace.Create();
        var clock = new MutableLeaseClock("2026-07-24T10:00:00.0000000Z");
        var fixture = workspace.CreateClaimedLocalTask(clock, "BAR-27", TimeSpan.FromMinutes(1));
        var artifact = workspace.Write("artifact.json", ValidArtifact(fixture.Lease));
        var output = workspace.Write("result.yaml", "previous result\n");
        var hooks = boundary == "expiry"
            ? new LocalWorkerCompleteTestHooks(() => clock.Advance(TimeSpan.FromMinutes(1)))
            : new LocalWorkerCompleteTestHooks(() => File.WriteAllText(fixture.LeasePath, LeaseJson("BAR-27", "local", Guid.NewGuid().ToString("N"), clock.UtcNow, clock.UtcNow.AddMinutes(5)), new UTF8Encoding(false)));

        Assert.NotEqual(0, LocalWorkerCompleteCommand.RunForTesting(Args(fixture.TaskPath, fixture.StorePath, artifact, output), TextWriter.Null, TextWriter.Null, clock, hooks));
        Assert.Equal("previous result\n", File.ReadAllText(output));
    }

    [Fact]
    public void Complete_then_existing_ingestion_and_finalization_reaches_only_in_review()
    {
        using var workspace = CompletionWorkspace.Create();
        var clock = new MutableLeaseClock("2026-07-24T10:00:00.0000000Z");
        var fixture = workspace.CreateClaimedLocalTask(clock, "BAR-27");
        var artifact = workspace.Write("artifact.json", ValidArtifact(fixture.Lease));
        var result = Path.Combine(workspace.Path, "result.yaml");
        var ingestion = Path.Combine(workspace.Path, "ingestion.json");
        var finalization = Path.Combine(workspace.Path, "finalization.json");

        Assert.Equal(0, LocalWorkerCompleteCommand.RunForTesting(Args(fixture.TaskPath, fixture.StorePath, artifact, result), TextWriter.Null, TextWriter.Null, clock, null));
        Assert.Equal(0, IngestResultCommand.RunForTesting(["ingest-result", "--task", fixture.TaskPath, "--result", result, "--lease-store", fixture.StorePath, "--output", ingestion], TextWriter.Null, TextWriter.Null, clock, null));
        Assert.Equal(0, FinalizeResultCommand.RunForTesting(["finalize-result", "--task", fixture.TaskPath, "--result", result, "--ingestion", ingestion, "--lease-store", fixture.StorePath, "--output", finalization], TextWriter.Null, TextWriter.Null, clock));

        using var record = JsonDocument.Parse(File.ReadAllBytes(finalization));
        Assert.Equal("success", record.RootElement.GetProperty("result_status").GetString());
        Assert.Equal("completion", record.RootElement.GetProperty("release_reason").GetString());
        Assert.Equal("in_review", record.RootElement.GetProperty("next_state").GetString());
        Assert.DoesNotContain("Done", File.ReadAllText(finalization), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(LocalLeaseStatus.Missing, new FileLeaseStore(fixture.StorePath, clock).Inspect("BAR-27").Status);

        var staleSnapshot = LinearTransitionTestSupport.SnapshotFor("uuid27", "BAR-27", "In Progress", "started", [], []);
        var staleTransport = new LinearTransitionTestSupport.QueueLinear()
            .On("Issue:BAR-27", LinearTransitionTestSupport.ResponseFor("uuid27", "BAR-27", "Todo", "unstarted", [], []));
        Assert.NotEqual(0, RunLinearResultTransition(workspace, staleSnapshot, fixture.TaskPath, finalization, staleTransport));
        Assert.False(staleTransport.AnyMutation);

        var snapshot = LinearTransitionTestSupport.SnapshotFor("uuid27", "BAR-27", "In Progress", "started", [], []);
        var transport = new LinearTransitionTestSupport.QueueLinear()
            .On("Issue:BAR-27",
                LinearTransitionTestSupport.ResponseFor("uuid27", "BAR-27", "In Progress", "started", [], []),
                LinearTransitionTestSupport.ResponseFor("uuid27", "BAR-27", "In Review", "started", [], []))
            .On("IssueUpdate", LinearTransitionTestSupport.MutationOk("issueUpdate"));
        var receipt = Path.Combine(workspace.Path, "linear-receipt.json");

        Assert.Equal(0, RunLinearResultTransition(workspace, snapshot, fixture.TaskPath, finalization, transport, receipt));
        Assert.Single(transport.Calls, call => call.Op == "IssueUpdate");
        Assert.DoesNotContain(transport.Calls, call => call.Vars.Contains("done", StringComparison.OrdinalIgnoreCase));
        using var receiptRecord = JsonDocument.Parse(File.ReadAllBytes(receipt));
        Assert.Equal("result", receiptRecord.RootElement.GetProperty("event").GetString());
        Assert.Equal("In Review", receiptRecord.RootElement.GetProperty("resulting_state_name").GetString());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(finalization))), receiptRecord.RootElement.GetProperty("evidence_sha256")[0].GetString());
    }

    [Fact]
    public void Stdout_failure_after_publication_reports_the_durable_result_without_rollback()
    {
        using var workspace = CompletionWorkspace.Create();
        var clock = new MutableLeaseClock("2026-07-24T10:00:00.0000000Z");
        var fixture = workspace.CreateClaimedLocalTask(clock, "BAR-27");
        var artifact = workspace.Write("artifact.json", ValidArtifact(fixture.Lease));
        var output = Path.Combine(workspace.Path, "result.yaml");
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        Assert.NotEqual(0, LocalWorkerCompleteCommand.RunForTesting(Args(fixture.TaskPath, fixture.StorePath, artifact, output), new ThrowingTextWriter(), error, clock, null));
        Assert.True(File.Exists(output));
        Assert.Contains("already published", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static int Run(string task, string store, string artifact, string output, ILeaseClock clock) =>
        LocalWorkerCompleteCommand.RunForTesting(Args(task, store, artifact, output), TextWriter.Null, TextWriter.Null, clock, null);

    private static int RunLinearResultTransition(CompletionWorkspace workspace, LinearIssueSnapshot snapshot, string task, string finalization, ILinearTransport transport, string? output = null)
    {
        var snapshotPath = workspace.Write("linear-snapshot-" + Guid.NewGuid().ToString("N") + ".json", snapshot.ToJson());
        var environment = "TLAW_BAR27_LINEAR_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        Environment.SetEnvironmentVariable(environment, "test-key");
        try
        {
            return LinearCommand.RunForTesting(["linear", "transition", "--issue", "BAR-27", "--event", "result", "--snapshot", snapshotPath, "--task", task, "--api-key-env", environment, "--finalization", finalization, "--output", output ?? Path.Combine(workspace.Path, "linear-receipt.json")], TextWriter.Null, TextWriter.Null, transport);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environment, null);
        }
    }

    private static string[] Args(string task, string store, string artifact, string output) => ["local-worker", "complete", "--task", task, "--lease-store", store, "--artifact", artifact, "--output", output];

    private static string ValidArtifact(LocalLease lease)
    {
        var task = new TaskV2Packet(lease.TaskId, "BAR-27", ["docs/agent/AGENT_PROTOCOL.md"], "Read-only analysis.", "read_only_analysis", "local", ["local"], ["local_reasoning"], "read_only", ["Do not execute commands."], lease.ClaimedBy, lease.ClaimId, Format(lease.ClaimStartedAt), Format(lease.ClaimExpiresAt), "46a3c0401a4071565674057734681406ac453a3e", true, "task/BAR-27-local-result-finalization", new VerificationRequirement(true, ["dotnet test --configuration Release"]), new DeliveryContract(true, true, true));
        var materials = new[] { new LocalWorkerMaterial("protocol", "", Sha256("material bytes")) };
        var response = new LocalLmStudioResponse("Read-only analysis.", Encoding.UTF8.GetBytes("provider response"));
        return LocalWorkerArtifact.Render(task, "contract_extraction", new LocalLmStudioModel("approved-local", "llm", "Approved", "local", "qwen"), materials, new LocalWorkerPrompt("system", "user", new string('a', 64)), response);
    }

    private static string MutateArtifact(string artifact, LocalLease lease, string variant) => variant switch
    {
        "task-id" => artifact.Replace($"\"task_id\": \"{lease.TaskId}\"", "\"task_id\": \"OTHER\"", StringComparison.Ordinal),
        "claimed-by" => artifact.Replace("\"claimed_by\": \"local\"", "\"claimed_by\": \"codex\"", StringComparison.Ordinal),
        "claim-id" => artifact.Replace($"\"claim_id\": \"{lease.ClaimId}\"", $"\"claim_id\": \"{Guid.NewGuid():N}\"", StringComparison.Ordinal),
        "claim-start" => artifact.Replace($"\"claim_started_at\": \"{Format(lease.ClaimStartedAt)}\"", $"\"claim_started_at\": \"{Format(lease.ClaimStartedAt.AddSeconds(1))}\"", StringComparison.Ordinal),
        "claim-expiry" => artifact.Replace($"\"claim_expires_at\": \"{Format(lease.ClaimExpiresAt)}\"", $"\"claim_expires_at\": \"{Format(lease.ClaimExpiresAt.AddSeconds(1))}\"", StringComparison.Ordinal),
        "malformed" => "{",
        "duplicate" => artifact[..^2] + ",\n  \"task_id\": \"duplicate\"\n}\n",
        "unknown" => artifact[..^2] + ",\n  \"unknown\": true\n}\n",
        "invalid-hash" => artifact.Replace(new string('a', 64), "not-a-hash", StringComparison.Ordinal),
        "unsupported-schema" => artifact.Replace("tlaw.local-worker-artifact/v1", "tlaw.local-worker-artifact/v999", StringComparison.Ordinal),
        "authority" => artifact.Replace("\"non_authoritative\": true", "\"non_authoritative\": false", StringComparison.Ordinal),
        "commands" => artifact.Replace("\"commands_executed\": false", "\"commands_executed\": true", StringComparison.Ordinal),
        "verification-claim" => artifact.Replace("Read-only analysis.", "I executed the build successfully.", StringComparison.Ordinal),
        "modified-model" => artifact.Replace("\"type\": \"llm\"", "\"type\": \"embedding\"", StringComparison.Ordinal),
        _ => throw new ArgumentOutOfRangeException(nameof(variant))
    };

    private static PacketSchemaRegistry Registry() => PacketSchemaRegistry.Load(Path.Combine(RepositoryRoot(), "docs", "agent", "schemas"));
    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static string Sha256(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Format(DateTimeOffset value) => FileLeaseStore.FormatCanonicalTimestamp(value);
    private static string LeaseJson(string taskId, string claimedBy, string claimId, DateTimeOffset started, DateTimeOffset expiry) => $$"""{"schema":"tlaw.local-lease/v1","task_id":"{{taskId}}","claimed_by":"{{claimedBy}}","claim_id":"{{claimId}}","claim_started_at":"{{Format(started)}}","claim_expires_at":"{{Format(expiry)}}"}""";
    private static void AssertPropertyOrder(byte[] bytes, params string[] properties)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var positions = properties.Select(property => text.IndexOf($"{property}:", StringComparison.Ordinal)).ToArray();
        Assert.DoesNotContain(positions, position => position < 0);
        Assert.Equal(positions.Order(), positions);
    }

    private sealed class MutableLeaseClock(string value) : ILeaseClock
    {
        private DateTimeOffset _now = DateTimeOffset.ParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        public DateTimeOffset UtcNow => _now;
        internal void Advance(TimeSpan value) => _now = _now.Add(value);
    }

    private sealed class ThrowingTextWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override void WriteLine(string? value) => throw new IOException("stdout intentionally failed");
    }

    private sealed record ClaimedTask(string TaskPath, string StorePath, string LeasePath, string LockPath, LocalLease Lease);

    private sealed class CompletionWorkspace : IDisposable
    {
        private CompletionWorkspace(string path)
        {
            Path = path;
            StorePath = System.IO.Path.Combine(path, "lease-store");
            Directory.CreateDirectory(StorePath);
        }

        internal string Path { get; }
        internal string StorePath { get; }
        internal static CompletionWorkspace Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tlaw-bar27-complete-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new CompletionWorkspace(path);
        }

        internal ClaimedTask CreateClaimedLocalTask(ILeaseClock clock, string taskId, TimeSpan? ttl = null)
        {
            var lease = new FileLeaseStore(StorePath, clock).Acquire(taskId, "local", ttl ?? TimeSpan.FromHours(1));
            var task = Write("task.yaml", TaskYaml(lease));
            var hash = Sha256(taskId);
            return new ClaimedTask(task, StorePath, System.IO.Path.Combine(StorePath, "leases", hash + ".json"), System.IO.Path.Combine(StorePath, "locks", hash + ".lock"), lease);
        }

        internal string Write(string name, string contents)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, contents, new UTF8Encoding(false));
            return path;
        }

        internal IReadOnlyDictionary<string, byte[]> SnapshotStore() => Directory.EnumerateFiles(StorePath, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(path => System.IO.Path.GetRelativePath(StorePath, path), File.ReadAllBytes, StringComparer.Ordinal);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private static string TaskYaml(LocalLease lease) => $$"""
        schema: tlaw.agent-task/v2
        task_id: {{lease.TaskId}}
        source_id: BAR-27
        sources: [https://linear.app/x/issue/BAR-27, docs/agent/AGENT_PROTOCOL.md]
        objective: Produce read-only analysis.
        work_type: read_only_analysis
        preferred_agent: local
        eligible_agents: [local]
        required_capabilities: [local_reasoning]
        autonomy_level: read_only
        forbidden_operations: [Execute commands.]
        claimed_by: {{lease.ClaimedBy}}
        claim_id: {{lease.ClaimId}}
        claim_started_at: {{Format(lease.ClaimStartedAt)}}
        claim_expires_at: {{Format(lease.ClaimExpiresAt)}}
        base_sha: 46a3c0401a4071565674057734681406ac453a3e
        handoff_required: true
        worktree: task/BAR-27-local-result-finalization
        verification: { required: true, commands: [dotnet test --configuration Release] }
        delivery: { branch_required: true, draft_pr_required: true, merge_forbidden: true }
        """;
}
