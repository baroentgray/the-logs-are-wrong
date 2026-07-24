using System.Net;
using System.Text;
using System.Text.Json;
using Tlaw.AgentProtocol;
using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class LocalWorkerCommandTests
{
    [Fact]
    public void Run_writes_an_untrusted_artifact_and_a_valid_result_from_a_local_model()
    {
        using var workspace = Workspace.Create();
        var task = workspace.Write("task.yaml", ClaimedLocalTask());
        var input = workspace.Write("excerpt.txt", "The dispatcher accepts only claimed task/v2 packets.\n");
        var artifact = Path.Combine(workspace.Path, "artifact.md");
        var result = Path.Combine(workspace.Path, "result.yaml");
        var model = new FakeLocalModel("The packet is claimed by local and has read-only autonomy.");

        var exit = LocalWorkerCommand.RunForTesting(
            ["local-worker", "run", "--task", task, "--input", input, "--artifact-kind", "contract-extraction", "--endpoint", "http://127.0.0.1:1234", "--model", "Qwen3-Coder-30B-A3B-Instruct-Q3_K_S", "--artifact", artifact, "--result", result],
            TextWriter.Null,
            TextWriter.Null,
            model);

        Assert.Equal(0, exit);
        Assert.Equal(1, model.CallCount);
        Assert.Contains("untrusted", File.ReadAllText(artifact), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No build or test command was run", File.ReadAllText(artifact), StringComparison.Ordinal);
        Assert.Contains("commit", model.Prompt, StringComparison.OrdinalIgnoreCase);

        var validation = PacketValidator.Validate(File.ReadAllText(result), PacketSchemaRegistry.Load(SchemaRoot));
        Assert.True(validation.IsValid, string.Join(" | ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal("success", validation.Packet!.RequiredString("status"));
    }

    [Theory]
    [InlineData("implementation", "read_only")]
    [InlineData("read_only_analysis", "branch_write")]
    public void Run_rejects_a_task_outside_the_local_read_only_policy_before_calling_the_model(string workType, string autonomyLevel)
    {
        using var workspace = Workspace.Create();
        var task = workspace.Write("task.yaml", ClaimedLocalTask(workType, autonomyLevel));
        var input = workspace.Write("excerpt.txt", "safe input\n");
        var artifact = Path.Combine(workspace.Path, "artifact.md");
        var result = Path.Combine(workspace.Path, "result.yaml");
        var model = new FakeLocalModel("must not be read");
        using var errors = new StringWriter();

        var exit = LocalWorkerCommand.RunForTesting(
            ["local-worker", "run", "--task", task, "--input", input, "--artifact-kind", "contract-extraction", "--endpoint", "http://127.0.0.1:1234", "--model", "model", "--artifact", artifact, "--result", result],
            TextWriter.Null,
            errors,
            model);

        Assert.Equal(1, exit);
        Assert.Equal(0, model.CallCount);
        Assert.False(File.Exists(artifact));
        Assert.StartsWith("FAIL:", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_rejects_a_non_loopback_endpoint_before_calling_the_model()
    {
        using var workspace = Workspace.Create();
        var task = workspace.Write("task.yaml", ClaimedLocalTask());
        var input = workspace.Write("excerpt.txt", "safe input\n");
        var artifact = Path.Combine(workspace.Path, "artifact.md");
        var result = Path.Combine(workspace.Path, "result.yaml");
        var model = new FakeLocalModel("must not be read");

        var exit = LocalWorkerCommand.RunForTesting(
            ["local-worker", "run", "--task", task, "--input", input, "--artifact-kind", "contract-extraction", "--endpoint", "https://example.com", "--model", "model", "--artifact", artifact, "--result", result],
            TextWriter.Null,
            TextWriter.Null,
            model);

        Assert.Equal(1, exit);
        Assert.Equal(0, model.CallCount);
        Assert.False(File.Exists(artifact));
    }

    [Fact]
    public void Run_rejects_a_repository_output_path_before_calling_the_model()
    {
        using var workspace = Workspace.Create();
        var task = workspace.Write("task.yaml", ClaimedLocalTask());
        var input = workspace.Write("excerpt.txt", "safe input\n");
        var result = Path.Combine(workspace.Path, "result.yaml");
        var model = new FakeLocalModel("must not be read");

        var exit = LocalWorkerCommand.RunForTesting(
            ["local-worker", "run", "--task", task, "--input", input, "--artifact-kind", "contract-extraction", "--endpoint", "http://127.0.0.1:1234", "--model", "model", "--artifact", Path.Combine(RepositoryRoot(), "AGENTS.md"), "--result", result],
            TextWriter.Null,
            TextWriter.Null,
            model);

        Assert.Equal(1, exit);
        Assert.Equal(0, model.CallCount);
    }

    [Fact]
    public void Dry_run_writes_a_boundary_receipt_without_contacting_the_model()
    {
        using var workspace = Workspace.Create();
        var task = workspace.Write("task.yaml", ClaimedLocalTask());
        var input = workspace.Write("excerpt.txt", "safe input\n");
        var artifact = Path.Combine(workspace.Path, "dry-run.md");
        var model = new FakeLocalModel("must not be read");

        var exit = LocalWorkerCommand.RunForTesting(
            ["local-worker", "run", "--task", task, "--input", input, "--artifact-kind", "contract-extraction", "--endpoint", "http://localhost:1234", "--model", "model", "--artifact", artifact, "--dry-run"],
            TextWriter.Null,
            TextWriter.Null,
            model);

        Assert.Equal(0, exit);
        Assert.Equal(0, model.CallCount);
        Assert.Contains("DRY RUN", File.ReadAllText(artifact), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_fails_closed_when_model_output_claims_unverified_test_success()
    {
        using var workspace = Workspace.Create();
        var task = workspace.Write("task.yaml", ClaimedLocalTask());
        var input = workspace.Write("excerpt.txt", "safe input\n");
        var artifact = Path.Combine(workspace.Path, "artifact.md");
        var result = Path.Combine(workspace.Path, "result.yaml");
        var model = new FakeLocalModel("All tests passed with zero failures.");

        var exit = LocalWorkerCommand.RunForTesting(
            ["local-worker", "run", "--task", task, "--input", input, "--artifact-kind", "test-case-draft", "--endpoint", "http://localhost:1234", "--model", "model", "--artifact", artifact, "--result", result],
            TextWriter.Null,
            TextWriter.Null,
            model);

        Assert.Equal(1, exit);
        Assert.Equal(1, model.CallCount);
        Assert.False(File.Exists(artifact));
        Assert.False(File.Exists(result));
    }

    [Fact]
    public void Complete_uses_the_guarded_result_transition_to_in_review_never_done()
    {
        using var workspace = Workspace.Create();
        var storePath = Path.Combine(workspace.Path, "leases");
        var lease = new FileLeaseStore(storePath, new SystemLeaseClock()).Acquire("BAR-27", "local", TimeSpan.FromMinutes(5));
        var task = workspace.Write("task.yaml", ClaimedLocalTask(lease: lease));
        var input = workspace.Write("excerpt.txt", "safe input\n");
        var artifact = Path.Combine(workspace.Path, "artifact.md");
        var result = Path.Combine(workspace.Path, "result.yaml");
        Assert.Equal(0, LocalWorkerCommand.RunForTesting(
            ["local-worker", "run", "--task", task, "--input", input, "--artifact-kind", "preliminary-review", "--endpoint", "http://127.0.0.1:1234", "--model", "model", "--artifact", artifact, "--result", result],
            TextWriter.Null,
            TextWriter.Null,
            new FakeLocalModel("The supplied excerpt has one open question.")));

        var snapshot = workspace.Write("snapshot.json", InProgressSnapshot());
        var ingestion = Path.Combine(workspace.Path, "ingestion.json");
        var finalization = Path.Combine(workspace.Path, "finalization.json");
        var receipt = Path.Combine(workspace.Path, "transition.json");
        var linear = new FakeLinear()
            .On("Issue", IssueResponse("In Progress", "started", "progress"))
            .On("IssueUpdate", MutationResponse())
            .On("Issue", IssueResponse("In Review", "started", "review"));
        Environment.SetEnvironmentVariable("TLAW_LOCAL_WORKER_TEST_LINEAR_KEY", "test-key");
        try
        {
            var exit = LocalWorkerCommand.CompleteForTesting(
                ["local-worker", "complete", "--task", task, "--result", result, "--lease-store", storePath, "--ingestion", ingestion, "--finalization", finalization, "--issue", "BAR-27", "--snapshot", snapshot, "--api-key-env", "TLAW_LOCAL_WORKER_TEST_LINEAR_KEY", "--transition-output", receipt],
                TextWriter.Null,
                TextWriter.Null,
                linear,
                new SystemLeaseClock());

            Assert.Equal(0, exit);
            Assert.Equal(1, linear.Count("IssueUpdate"));
            var receiptText = File.ReadAllText(receipt);
            Assert.Contains("\"resulting_state_name\": \"In Review\"", receiptText, StringComparison.Ordinal);
            Assert.DoesNotContain("Done", receiptText, StringComparison.Ordinal);
            Assert.Equal(LocalLeaseStatus.Missing, new FileLeaseStore(storePath, new SystemLeaseClock()).Inspect("BAR-27").Status);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TLAW_LOCAL_WORKER_TEST_LINEAR_KEY", null);
        }
    }

    [Fact]
    public void Completion_rejects_an_attempt_to_supply_a_done_target_before_any_linear_call()
    {
        using var workspace = Workspace.Create();
        var linear = new FakeLinear();
        using var errors = new StringWriter();

        var exit = LocalWorkerCommand.CompleteForTesting(
            ["local-worker", "complete", "--target-state", "Done"],
            TextWriter.Null,
            errors,
            linear,
            new SystemLeaseClock());

        Assert.Equal(1, exit);
        Assert.Equal(0, linear.TotalCalls);
        Assert.StartsWith("FAIL:", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Worker_source_has_no_process_or_git_write_capability()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "tools", "Tlaw.Dispatcher", "LocalWorkerCommand.cs"));

        Assert.DoesNotContain("ProcessStartInfo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Diagnostics.Process", source, StringComparison.Ordinal);
        Assert.DoesNotContain("git ", source, StringComparison.OrdinalIgnoreCase);
    }

    private static string ClaimedLocalTask(string workType = "read_only_analysis", string autonomyLevel = "read_only", LocalLease? lease = null)
    {
        var claim = lease ?? new LocalLease(
            "BAR-27-sample",
            "local",
            "0e221c4b8ed84e6dae7eea27008eb449",
            DateTimeOffset.Parse("2026-07-24T10:00:00.0000000Z"),
            DateTimeOffset.Parse("2026-07-24T10:30:00.0000000Z"));
        return $$"""
            schema: tlaw.agent-task/v2
            task_id: {{claim.TaskId}}
            source_id: BAR-27
            sources:
              - https://linear.app/baronet/issue/BAR-27/tlaw-auto-002-local-lm-studio-read-only-worker
              - docs/agent/AGENT_PROTOCOL.md
            objective: Produce an offline contract extraction.
            work_type: {{workType}}
            preferred_agent: local
            eligible_agents:
              - local
            required_capabilities:
              - local_reasoning
            autonomy_level: {{autonomyLevel}}
            forbidden_operations:
              - Commit changes.
              - Push a branch.
              - Modify a pull request.
              - Merge code.
            claimed_by: {{claim.ClaimedBy}}
            claim_id: {{claim.ClaimId}}
            claim_started_at: {{FileLeaseStore.FormatCanonicalTimestamp(claim.ClaimStartedAt)}}
            claim_expires_at: {{FileLeaseStore.FormatCanonicalTimestamp(claim.ClaimExpiresAt)}}
            base_sha: f7dde960b646d4c5e1efaa71b2ce57e879c7a789
            handoff_required: true
            worktree: task/BAR-27-lmstudio-readonly-boundary
            verification:
              required: true
              commands:
                - dotnet test --configuration Release
            delivery:
              branch_required: true
              draft_pr_required: true
              merge_forbidden: true
            """;
    }

    private static string InProgressSnapshot() => """
        {
          "schema": "tlaw.dispatcher-linear-snapshot/v1",
          "id": "issue-27",
          "identifier": "BAR-27",
          "url": "https://linear.app/baronet/issue/BAR-27/tlaw-auto-002-local-lm-studio-read-only-worker",
          "team_id": "team",
          "team_key": "BAR",
          "state_id": "progress",
          "state_name": "In Progress",
          "state_type": "started",
          "updated_at": "2026-07-24T10:00:00Z",
          "states": [
            { "id": "progress", "name": "In Progress", "type": "started" },
            { "id": "review", "name": "In Review", "type": "started" }
          ],
          "labels": [],
          "attachments": [],
          "blocked_by": [],
          "blocks": []
        }
        """;

    private static string IssueResponse(string stateName, string stateType, string stateId) => JsonSerializer.Serialize(new
    {
        data = new
        {
            issue = new
            {
                id = "issue-27",
                identifier = "BAR-27",
                url = "https://linear.app/baronet/issue/BAR-27/tlaw-auto-002-local-lm-studio-read-only-worker",
                title = "Local worker",
                updatedAt = "2026-07-24T10:00:00Z",
                state = new { id = stateId, name = stateName, type = stateType },
                team = new
                {
                    id = "team",
                    key = "BAR",
                    states = new { nodes = new[] { new { id = "progress", name = "In Progress", type = "started" }, new { id = "review", name = "In Review", type = "started" } } }
                },
                labels = new { nodes = Array.Empty<object>() },
                blockedBy = new { nodes = Array.Empty<object>() },
                blocks = new { nodes = Array.Empty<object>() },
                attachments = new { nodes = Array.Empty<object>() }
            }
        }
    });

    private static string MutationResponse() => """{ "data": { "issueUpdate": { "success": true } } }""";

    private static string SchemaRoot => Path.Combine(RepositoryRoot(), "docs", "agent", "schemas");

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test directory.");
    }

    private sealed class FakeLocalModel(string response) : ILocalLmStudioClient
    {
        internal int CallCount { get; private set; }
        internal string Prompt { get; private set; } = string.Empty;

        public string Complete(LocalLmStudioEndpoint endpoint, string model, string prompt)
        {
            CallCount++;
            Prompt = prompt;
            return response;
        }
    }

    private sealed class FakeLinear : ILinearTransport
    {
        private readonly Dictionary<string, Queue<LinearTransportResponse>> _responses = new(StringComparer.Ordinal);
        private readonly List<string> _operations = [];

        internal int TotalCalls => _operations.Count;
        internal FakeLinear On(string operation, string response)
        {
            if (!_responses.TryGetValue(operation, out var values))
            {
                values = new Queue<LinearTransportResponse>();
                _responses.Add(operation, values);
            }

            values.Enqueue(new LinearTransportResponse(HttpStatusCode.OK, response));
            return this;
        }

        internal int Count(string operation) => _operations.Count(value => string.Equals(value, operation, StringComparison.Ordinal));

        public LinearTransportResponse Send(string operationName, string query, object variables, string apiKey)
        {
            _operations.Add(operationName);
            Assert.True(_responses.TryGetValue(operationName, out var values) && values.Count > 0, $"Unexpected Linear operation '{operationName}'.");
            return values.Dequeue();
        }
    }

    private sealed class Workspace : IDisposable
    {
        private Workspace(string path) => Path = path;
        internal string Path { get; }

        internal static Workspace Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tlaw-local-worker-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new Workspace(path);
        }

        internal string Write(string name, string text)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
