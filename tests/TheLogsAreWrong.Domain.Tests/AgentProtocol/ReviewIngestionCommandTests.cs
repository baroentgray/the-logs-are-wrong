using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class ReviewIngestionCommandTests
{
    [Theory]
    [InlineData("approve", "", "merge", "in_review")]
    [InlineData("request_changes", "medium", "correction", "todo")]
    [InlineData("comment", "high", "human", "in_review")]
    public void Closed_evidence_writes_deterministic_decision_record(string verdict, string findings, string decision, string nextState)
    {
        using var workspace = ReviewWorkspace.Create();
        var task = workspace.Write("task.yaml", ClaimedTaskYaml);
        var finalization = workspace.Write("finalization.json", FinalizationJson());
        var review = workspace.Write("review.yaml", ReviewYaml(verdict, findings));
        var output = Path.Combine(workspace.Path, "decision.json");
        var taskBefore = File.ReadAllBytes(task);
        var finalizationBefore = File.ReadAllBytes(finalization);
        var reviewBytes = File.ReadAllBytes(review);
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();

        Assert.Equal(0, IngestReviewCommand.Run(Arguments(task, finalization, review, output), standardOutput, standardError));
        var bytes = File.ReadAllBytes(output);

        Assert.False(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.Equal($"REVIEW: {decision}{Environment.NewLine}", standardOutput.ToString());
        Assert.Equal(string.Empty, standardError.ToString());
        Assert.Equal(taskBefore, File.ReadAllBytes(task));
        Assert.Equal(finalizationBefore, File.ReadAllBytes(finalization));
        Assert.Equal(reviewBytes, File.ReadAllBytes(review));

        using var document = JsonDocument.Parse(bytes);
        Assert.Equal("tlaw.dispatcher-review-decision/v1", document.RootElement.GetProperty("schema").GetString());
        Assert.Equal("BAR-37", document.RootElement.GetProperty("task_id").GetString());
        Assert.Equal(ExpectedHead, document.RootElement.GetProperty("reviewed_head").GetString());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(reviewBytes)), document.RootElement.GetProperty("review_sha256").GetString());
        Assert.Equal(verdict, document.RootElement.GetProperty("verdict").GetString());
        Assert.Equal(decision, document.RootElement.GetProperty("decision").GetString());
        Assert.Equal(nextState, document.RootElement.GetProperty("next_state").GetString());
        AssertPropertyOrder(bytes, "schema", "task_id", "reviewed_head", "review_sha256", "verdict", "highest_severity", "blocking_findings", "decision", "next_state");
    }

    [Theory]
    [InlineData("approve", "medium")]
    [InlineData("request_changes", "low")]
    [InlineData("comment", "invalid")]
    public void Contradictory_or_invalid_review_preserves_existing_output(string verdict, string findings)
    {
        using var workspace = ReviewWorkspace.Create();
        var task = workspace.Write("task.yaml", ClaimedTaskYaml);
        var finalization = workspace.Write("finalization.json", FinalizationJson());
        var review = workspace.Write("review.yaml", ReviewYaml(verdict, findings));
        var output = workspace.Write("decision.json", "previous decision\n");
        using var errors = new StringWriter();

        Assert.NotEqual(0, IngestReviewCommand.Run(Arguments(task, finalization, review, output), TextWriter.Null, errors));
        Assert.Equal("previous decision\n", File.ReadAllText(output));
        Assert.StartsWith("FAIL: ", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Strict_options_evidence_identity_and_output_aliases_fail_closed()
    {
        using var workspace = ReviewWorkspace.Create();
        var task = workspace.Write("task.yaml", ClaimedTaskYaml);
        var finalization = workspace.Write("finalization.json", FinalizationJson());
        var review = workspace.Write("review.yaml", ReviewYaml("approve", ""));
        var output = Path.Combine(workspace.Path, "decision.json");

        Assert.NotEqual(0, IngestReviewCommand.Run(["ingest-review", "--task", task, "--finalization", finalization, "--review", review, "--expected-head", new string('0', 40), "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.NotEqual(0, IngestReviewCommand.Run(Arguments(task, finalization, review, task), TextWriter.Null, TextWriter.Null));
        Assert.NotEqual(0, IngestReviewCommand.Run(Arguments(task, finalization, review, output, ExpectedHead[..39]), TextWriter.Null, TextWriter.Null));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Publication_is_retained_when_stdout_fails()
    {
        using var workspace = ReviewWorkspace.Create();
        var task = workspace.Write("task.yaml", ClaimedTaskYaml);
        var finalization = workspace.Write("finalization.json", FinalizationJson());
        var review = workspace.Write("review.yaml", ReviewYaml("approve", ""));
        var output = Path.Combine(workspace.Path, "decision.json");
        using var errors = new StringWriter();

        Assert.NotEqual(0, IngestReviewCommand.Run(Arguments(task, finalization, review, output), new ThrowingTextWriter(), errors));
        Assert.True(File.Exists(output));
        Assert.Contains("already published", errors.ToString(), StringComparison.Ordinal);
    }

    private static string[] Arguments(string task, string finalization, string review, string output, string? expectedHead = null) => ["ingest-review", "--task", task, "--finalization", finalization, "--review", review, "--expected-head", expectedHead ?? ExpectedHead, "--output", output];

    private static string FinalizationJson() => "{\"schema\":\"tlaw.dispatcher-finalization/v1\",\"task_id\":\"BAR-37\",\"claimed_by\":\"codex\",\"claim_id\":\"0e221c4b8ed84e6dae7eea27008eb449\",\"result_status\":\"success\",\"result_sha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"release_reason\":\"completion\",\"next_state\":\"in_review\"}";

    private static string ReviewYaml(string verdict, string findings)
    {
        var renderedFindings = string.IsNullOrEmpty(findings) ? "findings: []" : $"findings:\n  - severity: {findings}\n    summary: Finding.\n    evidence: test";
        return $"schema: tlaw.agent-review/v1\ntask_id: BAR-37\nreviewed_head: {ExpectedHead}\nverdict: {verdict}\nhuman_summary: Review evidence.\nevidence:\n  - kind: command\n    reference: dotnet test\n{renderedFindings}\n";
    }

    private static void AssertPropertyOrder(byte[] json, params string[] properties)
    {
        var text = Encoding.UTF8.GetString(json);
        var positions = properties.Select(property => text.IndexOf($"\"{property}\"", StringComparison.Ordinal)).ToArray();
        Assert.All(positions, position => Assert.True(position >= 0));
        Assert.True(positions.SequenceEqual(positions.Order()));
    }

    private const string ExpectedHead = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ClaimedTaskYaml = """
        schema: tlaw.agent-task/v2
        task_id: BAR-37
        source_id: BAR-37
        sources:
          - https://linear.app/baronet/issue/BAR-37
        objective: Ingest review evidence.
        work_type: implementation
        preferred_agent: codex
        eligible_agents:
          - codex
        required_capabilities:
          - dotnet
        autonomy_level: branch_write
        forbidden_operations:
          - Merge pull requests.
        claimed_by: codex
        claim_id: 0e221c4b8ed84e6dae7eea27008eb449
        claim_started_at: 2026-07-22T11:30:00.0000000Z
        claim_expires_at: 2026-07-22T11:35:00.0000000Z
        base_sha: 97cc8a6684af4b1c11d52bcc3cbe07c4cca72d8b
        handoff_required: true
        worktree: task/BAR-37-review-ingestion
        verification:
          required: true
          commands:
            - dotnet test --configuration Release
        delivery:
          branch_required: true
          draft_pr_required: true
          merge_forbidden: true
        """;

    private sealed class ThrowingTextWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override void WriteLine(string? value) => throw new IOException("stdout intentionally failed");
    }

    private sealed class ReviewWorkspace : IDisposable
    {
        private ReviewWorkspace(string path) => Path = path;
        internal string Path { get; }
        internal static ReviewWorkspace Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tlaw-review-ingestion-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new ReviewWorkspace(path);
        }
        internal string Write(string name, string contents)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, contents, new UTF8Encoding(false));
            return path;
        }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
