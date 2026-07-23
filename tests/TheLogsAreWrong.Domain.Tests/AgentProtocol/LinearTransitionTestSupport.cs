using System.Net;
using System.Text;
using System.Text.Json;
using Tlaw.AgentProtocol;
using Tlaw.Dispatcher;
using Tlaw.Verify;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

/// <summary>Shared deterministic fixtures for the guarded <c>linear transition</c> tests.</summary>
internal static class LinearTransitionTestSupport
{
    internal const string ApiKeyEnv = "TLAW_TEST_LINEAR_KEY";
    internal const string Secret = "never-print-this-secret";
    internal const string Url = "https://linear.app/x/issue/BAR-41";
    internal const string GitHubSource = "https://github.com/baroentgray/the-logs-are-wrong/issues/41";
    internal const string BaseSha = "a2e8acbb4c1264c22ee8e47b570e3cc4bf47ccd5";
    internal const string Updated = "2026-07-23T00:00:00Z";

    internal static PacketSchemaRegistry Registry() => PacketSchemaRegistry.Load(Path.Combine(TaskPacketCommand.FindRepositoryRoot(), "docs", "agent", "schemas"));

    // ---- Linear GraphQL transport doubles ----

    internal static LinearTransportResponse Ok(object value) => new(HttpStatusCode.OK, JsonSerializer.Serialize(value));
    internal static LinearTransportResponse GraphErrors() => Ok(new { errors = new[] { new { message = "denied" } }, data = (object?)null });
    internal static LinearTransportResponse MutationOk(string property) => Ok(new Dictionary<string, object> { ["data"] = new Dictionary<string, object> { [property] = new { success = true } } });
    internal static LinearTransportResponse MutationFalse(string property) => Ok(new Dictionary<string, object> { ["data"] = new Dictionary<string, object> { [property] = new { success = false } } });
    internal static LinearTransportResponse Catalog(object[] team, object[] workspace) => Ok(new { data = new { team = new { id = "team", labels = new { nodes = team } }, workspaceLabels = new { nodes = workspace } } });
    internal static LinearTransportResponse LabelCreate(bool success, string id, string name) => Ok(new { data = new { issueLabelCreate = new { success, issueLabel = new { id, name } } } });
    internal static LinearTransportResponse RelationCreate(bool success, string id, string blockingId, string blockingIdentifier, string blockedId, string blockedIdentifier) => Ok(new { data = new { issueRelationCreate = new { success, issueRelation = new { id, type = "blocks", issue = new { id = blockingId, identifier = blockingIdentifier }, relatedIssue = new { id = blockedId, identifier = blockedIdentifier } } } } });

    internal static object L(string id, string name) => new { id, name };
    internal static LinearRelation Rel(string id, string blockingId, string blockingIdentifier, string blockedId, string blockedIdentifier) => new(id, "blocks", blockingId, blockingIdentifier, blockedId, blockedIdentifier);

    private static string StateId(string name) => name switch { "Backlog" => "backlog", "Todo" => "todo", "In Progress" => "prog", "In Review" => "review", "Done" => "done", _ => "state" };
    private static object[] States() => [new { id = "backlog", name = "Backlog", type = "backlog" }, new { id = "todo", name = "Todo", type = "unstarted" }, new { id = "prog", name = "In Progress", type = "started" }, new { id = "review", name = "In Review", type = "started" }, new { id = "done", name = "Done", type = "completed" }];

    internal static LinearTransportResponse ResponseFor(string id, string identifier, string stateName, string stateType, IReadOnlyList<LinearLabel> labels, IReadOnlyList<LinearRelation> blockedBy) => Ok(new
    {
        data = new
        {
            issue = new
            {
                id,
                identifier,
                url = $"https://linear.app/x/issue/{identifier}",
                title = "T",
                description = "never-print-this-description",
                updatedAt = Updated,
                state = new { id = StateId(stateName), name = stateName, type = stateType },
                team = new { id = "team", key = "BAR", states = new { nodes = States() } },
                labels = new { nodes = labels.Select(l => new { id = l.Id, name = l.Name }) },
                blockedBy = new { nodes = blockedBy.Select(r => new { id = r.Id, type = r.Type, blockingIssue = new { id = r.BlockingIssueId, identifier = r.BlockingIssueIdentifier }, blockedIssue = new { id = r.BlockedIssueId, identifier = r.BlockedIssueIdentifier } }) },
                blocks = new { nodes = Array.Empty<object>() },
                attachments = new { nodes = new[] { new { url = "https://a.test" } } }
            }
        }
    });

    internal static LinearIssueSnapshot SnapshotFor(string id, string identifier, string stateName, string stateType, IReadOnlyList<LinearLabel> labels, IReadOnlyList<LinearRelation> blockedBy) => LinearIssueSnapshot.From(new LinearIssue(
        id, identifier, $"https://linear.app/x/issue/{identifier}", "T", Updated,
        new LinearState(StateId(stateName), stateName, stateType), "team", "BAR",
        [new LinearState("backlog", "Backlog", "backlog"), new LinearState("todo", "Todo", "unstarted"), new LinearState("prog", "In Progress", "started"), new LinearState("review", "In Review", "started"), new LinearState("done", "Done", "completed")],
        labels, ["https://a.test"], blockedBy, []));

    internal sealed class QueueLinear : ILinearTransport
    {
        private readonly Dictionary<string, Queue<LinearTransportResponse>> _responses = new(StringComparer.Ordinal);
        internal List<(string Op, string Vars)> Calls { get; } = [];
        internal bool AnyMutation => Calls.Any(c => c.Op is "IssueUpdate" or "IssueRelationCreate" or "IssueRelationDelete" or "IssueLabelCreate");

        internal QueueLinear On(string key, params LinearTransportResponse[] responses)
        {
            if (!_responses.TryGetValue(key, out var queue)) _responses[key] = queue = new Queue<LinearTransportResponse>();
            foreach (var response in responses) queue.Enqueue(response);
            return this;
        }

        public LinearTransportResponse Send(string operationName, string query, object variables, string apiKey)
        {
            Assert.DoesNotContain(apiKey, query, StringComparison.Ordinal);
            var vars = JsonSerializer.Serialize(variables);
            Assert.DoesNotContain(apiKey, vars, StringComparison.Ordinal);
            Calls.Add((operationName, vars));
            var key = operationName == "Issue" ? "Issue:" + JsonDocument.Parse(vars).RootElement.GetProperty("identifier").GetString() : operationName;
            if (!_responses.TryGetValue(key, out var queue) || queue.Count == 0) throw new InvalidOperationException($"No scripted response for '{key}'.");
            return queue.Dequeue();
        }
    }

    /// <summary>A transport that fails the test if any GraphQL call is attempted; proves a rejected transition is inert.</summary>
    internal sealed class NoCallLinear : ILinearTransport
    {
        public LinearTransportResponse Send(string operationName, string query, object variables, string apiKey)
        {
            Assert.Fail($"A rejected transition attempted a GraphQL call: {operationName}.");
            throw new InvalidOperationException();
        }
    }

    internal sealed class FakeClock(DateTimeOffset now) : ILeaseClock { public DateTimeOffset UtcNow => now; }

    internal sealed class FakeGit(string originMain) : IGitProofRunner
    {
        public GitProofResult Run(string repositoryPath, params string[] arguments)
            => arguments.Length == 2 && arguments[0] == "rev-parse" && arguments[1] == "origin/main"
                ? new GitProofResult(0, originMain, false)
                : new GitProofResult(0, string.Empty, false);
    }

    // ---- typed evidence writers ----

    internal static TaskV2Packet UnclaimedTask(string worktree = "task/BAR-41") => new(
        "BAR-41", "BAR-41", [Url, GitHubSource],
        "Implement an adapter contract.", "implementation", "codex", ["codex", "claude"], ["dotnet", "yaml_protocol"],
        "branch_write", ["Merge pull requests.", "Write main."], "unclaimed", "unclaimed", "unclaimed", "unclaimed",
        BaseSha, true, worktree, new VerificationRequirement(true, ["dotnet test --configuration Release"]),
        new DeliveryContract(true, true, true));

    internal static TaskV2Packet ClaimedTask(string claimedBy, string claimId, string startedAt, string expiresAt) =>
        UnclaimedTask() with { ClaimedBy = claimedBy, ClaimId = claimId, ClaimStartedAt = startedAt, ClaimExpiresAt = expiresAt };

    internal static string WriteTask(Workspace workspace, TaskV2Packet task, string name = "task.yaml")
    {
        var yaml = TaskPacketGenerator.Generate(task, Registry());
        return workspace.Write(name, yaml);
    }

    internal static string WriteFinalization(Workspace workspace, string status, string releaseReason, string nextState, string claimedBy = "codex", string claimId = "0e221c4b8ed84e6dae7eea27008eb449", string resultSha = "abc123", string name = "finalization.json")
    {
        var hash = resultSha.Length == 64 ? resultSha : resultSha.PadRight(64, '0');
        var json = $$"""
        {
          "schema": "tlaw.dispatcher-finalization/v1",
          "task_id": "BAR-41",
          "claimed_by": {{JsonSerializer.Serialize(claimedBy)}},
          "claim_id": {{JsonSerializer.Serialize(claimId)}},
          "result_status": {{JsonSerializer.Serialize(status)}},
          "result_sha256": "{{hash}}",
          "release_reason": {{JsonSerializer.Serialize(releaseReason)}},
          "next_state": {{JsonSerializer.Serialize(nextState)}}
        }
        """;
        return workspace.Write(name, json);
    }

    internal static string WriteReviewDecision(Workspace workspace, string verdict, string highest, int blocking, string decision, string nextState, string reviewedHead = "1111111111111111111111111111111111111111", string name = "decision.json")
    {
        var json = $$"""
        {
          "schema": "tlaw.dispatcher-review-decision/v1",
          "task_id": "BAR-41",
          "reviewed_head": "{{reviewedHead}}",
          "review_sha256": "{{new string('a', 64)}}",
          "verdict": {{JsonSerializer.Serialize(verdict)}},
          "highest_severity": {{JsonSerializer.Serialize(highest)}},
          "blocking_findings": {{blocking}},
          "decision": {{JsonSerializer.Serialize(decision)}},
          "next_state": {{JsonSerializer.Serialize(nextState)}}
        }
        """;
        return workspace.Write(name, json);
    }

    internal static string WriteIngestion(Workspace workspace, string decision, string worktree = "task/BAR-41", string name = "ingestion.json")
    {
        var status = decision == "reassign" ? "ready" : "blocked";
        var blocked = decision == "human" ? "true" : "false";
        var json = $$"""
        {
          "schema": "tlaw.dispatcher-handoff-ingestion/v1",
          "task_id": "BAR-41",
          "source_id": "BAR-41",
          "claimed_by": "codex",
          "claim_id": "0e221c4b8ed84e6dae7eea27008eb449",
          "handoff_sha256": "{{new string('b', 64)}}",
          "handoff_status": "{{status}}",
          "head_sha": "1111111111111111111111111111111111111111",
          "branch": {{JsonSerializer.Serialize(worktree)}},
          "lease_status": "active",
          "lease_action": "release_required",
          "release_reason": "manual_cancel",
          "decision": "{{decision}}",
          "next_state": "todo",
          "blocked": {{blocked}}
        }
        """;
        return workspace.Write(name, json);
    }

    // ---- verification artifact ----

    internal static VerificationReport PassReport(string headSha)
    {
        var now = DateTimeOffset.Parse("2026-07-23T18:59:29Z");
        CommandEvidence Command(string exe, params string[] args) => new(exe, args, "C:/repo", now, now, 0, "logs/x.log");
        return new VerificationReport(
            "tlaw.verification/v1", now, now, "C:/repo", "main", false, headSha, headSha, BaseSha, null, true,
            new VerificationEnvironment("Windows", "10.0.103"),
            [Command("dotnet", "restore"), Command("dotnet", "build", "--configuration", "Release"), Command("dotnet", "test", "--configuration", "Release"), Command("git", "diff", "--check")],
            new CheckEvidence(EvidenceStatus.PASS),
            new BuildEvidence(EvidenceStatus.PASS, 0, 0),
            new TestEvidence(EvidenceStatus.PASS, 589, 0, 0, 589, "verification.trx"),
            new CheckEvidence(EvidenceStatus.PASS),
            new Gate0Evidence(EvidenceStatus.PASS, "gate0-approved", "4056157d8df6742d60711fa4a34b92364b2cb2dc", ["AGENTS.md"], [], [], [], [], [], new GitObjectReaderEvidence("git-cat-file-batch", 1, 52, 52, EvidenceStatus.PASS, 0, "logs/g.log", [])),
            new ArchitectureEvidence(EvidenceStatus.PASS, ["Architecture: Passed"]),
            new DomainDependenciesEvidence(EvidenceStatus.PASS, []),
            VerificationVerdict.PASS, []);
    }

    internal static byte[] SerializeReport(VerificationReport report) => new UTF8Encoding(false).GetBytes(VerificationReportSerializer.Serialize(report));

    // ---- integration runner ----

    internal static (int Exit, string Out, string Err) RunTransition(Workspace workspace, LinearIssueSnapshot snapshot, string taskPath, string evt, ILinearTransport transport, IEnumerable<(string Key, string Value)> extra, ILeaseClock? clock = null, IGitProofRunner? git = null, string? outputOverride = null)
    {
        var snapshotPath = workspace.Write("snapshot.json", snapshot.ToJson());
        var output = outputOverride ?? workspace.OutputPath;
        // A per-call environment variable name avoids any cross-test race on a shared process-global variable.
        var envName = "TLAW_TEST_KEY_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var args = new List<string> { "linear", "transition", "--issue", "BAR-41", "--event", evt, "--snapshot", snapshotPath, "--task", taskPath, "--api-key-env", envName, "--output", output };
        foreach (var (key, value) in extra) { args.Add(key); args.Add(value); }
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();
        Environment.SetEnvironmentVariable(envName, Secret);
        try { var exit = LinearCommand.RunForTesting([.. args], standardOutput, standardError, transport, clock, git); return (exit, standardOutput.ToString(), standardError.ToString()); }
        finally { Environment.SetEnvironmentVariable(envName, null); }
    }

    internal sealed class Workspace : IDisposable
    {
        private Workspace(string path) => Path = path;
        internal string Path { get; }
        internal string OutputPath => System.IO.Path.Combine(Path, "receipt.json");
        internal string LeaseStore => System.IO.Path.Combine(Path, "lease-store");
        internal static Workspace Create() { var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tlaw-linear-transition-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return new Workspace(path); }
        internal string Write(string name, string text) { var path = System.IO.Path.Combine(Path, name); File.WriteAllText(path, text, new UTF8Encoding(false)); return path; }
        public void Dispose() { try { Directory.Delete(Path, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}
