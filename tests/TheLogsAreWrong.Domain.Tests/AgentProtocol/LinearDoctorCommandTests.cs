using System.Net;
using System.Text;
using System.Text.Json;
using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class LinearDoctorCommandTests
{
    [Fact]
    public void Linear_snapshot_generates_packet_compatible_normalized_input_without_description_or_secret()
    {
        using var workspace = Workspace.Create();
        var profile = workspace.Write("profile.json", Profile());
        var output = Path.Combine(workspace.Path, "input.json");
        var snapshot = Path.Combine(workspace.Path, "snapshot.json");
        Environment.SetEnvironmentVariable("TLAW_TEST_LINEAR_KEY", "never-print-this-secret");
        try
        {
            var result = LinearCommand.RunForTesting(["linear", "snapshot", "--issue", "BAR-41", "--profile", profile, "--api-key-env", "TLAW_TEST_LINEAR_KEY", "--output", output, "--snapshot-output", snapshot], TextWriter.Null, TextWriter.Null, new FakeLinear(IssueResponse()));
            Assert.Equal(0, result);
            var text = File.ReadAllText(output);
            Assert.DoesNotContain("private issue description", text, StringComparison.Ordinal);
            Assert.DoesNotContain("never-print-this-secret", text, StringComparison.Ordinal);
            _ = NormalizedTaskInput.Parse(text);
            Assert.DoesNotContain((byte)'\r', File.ReadAllBytes(output));
        }
        finally { Environment.SetEnvironmentVariable("TLAW_TEST_LINEAR_KEY", null); }
    }

    [Theory]
    [InlineData("{\"errors\":[{\"message\":\"denied\"}],\"data\":{\"issue\":null}}")]
    [InlineData("not-json")]
    public void Linear_graphql_errors_and_malformed_responses_fail_closed(string body)
    {
        var response = new LinearTransportResponse(HttpStatusCode.OK, body);
        Assert.Throws<LinearCommandException>(() => LinearGraphQl.ParseIssue(response));
    }

    [Fact]
    public void Linear_transition_planner_allows_only_contract_edges_and_never_allows_generic_merge()
    {
        var snapshot = LinearIssueSnapshot.From(Issue("In Review"));
        var correction = new TransitionEvidence("tlaw.dispatcher-review-decision/v1", "BAR-41", "todo", "correction", "a".PadLeft(64, 'a'));
        Assert.Equal("Todo", LinearTransitionPlanner.Plan("review", snapshot, correction).TargetState);
        Assert.Throws<LinearCommandException>(() => LinearTransitionPlanner.Plan("merge", snapshot, correction));
        Assert.Throws<LinearCommandException>(() => LinearTransitionPlanner.Plan("queue", snapshot, correction));
    }

    [Fact]
    public void Doctor_config_rejects_public_endpoint_and_shell_syntax()
    {
        Assert.Throws<DoctorCommandException>(() => DoctorConfig.Parse("""{ "schema":"tlaw.dispatcher-adapters/v1", "agents":[ {"agent":"codex","mode":"manual_packet","enabled":true}, {"agent":"claude","mode":"manual_packet","enabled":true}, {"agent":"grok","mode":"cli","enabled":true,"executable":"grok;cmd"}, {"agent":"local","mode":"local_api","enabled":true,"endpoint":"http://10.0.0.1:1234"} ] }"""));
    }

    [Fact]
    public void Doctor_static_output_is_accepted_unchanged_by_route_snapshot_parser()
    {
        var reports = new[]
        {
            new DoctorReport("codex", "manual_packet", true, "configured", "UNKNOWN", "UNKNOWN", "configuration only"),
            new DoctorReport("claude", "manual_packet", true, "configured", "UNKNOWN", "UNKNOWN", "configuration only"),
            new DoctorReport("grok", "cli", true, "grok", "CLI_AVAILABLE", "UNKNOWN", "configuration only"),
            new DoctorReport("local", "local_api", true, "http://127.0.0.1:1234", "CLI_AVAILABLE", "UNKNOWN", "loopback")
        };
        var snapshot = DoctorJson.WriteSnapshot(reports);
        Assert.Equal(4, AgentSnapshotDocument.Parse(snapshot).Count);
    }

    private static string Profile() => """
        {
          "schema":"tlaw.dispatcher-linear-profile/v1",
          "base_sha":"a2e8acbb4c1264c22ee8e47b570e3cc4bf47ccd5",
          "github_source":"https://github.com/baroentgray/the-logs-are-wrong/issues/41",
          "objective":"Implement an adapter contract.",
          "work_type":"implementation", "preferred_agent":"codex", "eligible_agents":["codex","claude"],
          "required_capabilities":["dotnet"], "autonomy_level":"branch_write",
          "forbidden_operations":["Merge pull requests."], "handoff_required":true, "worktree":"task/BAR-41",
          "verification":{"required":true,"commands":["dotnet test --configuration Release"]},
          "delivery":{"branch_required":true,"draft_pr_required":true,"merge_forbidden":true}, "label_profiles":{}
        }
        """;

    private static LinearIssue Issue(string state) => new("uuid", "BAR-41", "https://linear.app/baronet/issue/BAR-41/example", "Title", "2026-07-23T00:00:00Z", new LinearState("state", state, "started"), "team", "BAR", [new LinearState("todo", "Todo", "unstarted"), new LinearState("review", "In Review", "started"), new LinearState("state", state, "started")], [new LinearLabel("label", "configured")], ["https://example.test"], [], []);
    private static string IssueResponse() => JsonSerializer.Serialize(new { data = new { issue = new { id = "uuid", identifier = "BAR-41", url = "https://linear.app/baronet/issue/BAR-41/example", title = "Title", description = "private issue description", updatedAt = "2026-07-23T00:00:00Z", state = new { id = "todo", name = "Todo", type = "unstarted" }, team = new { id = "team", key = "BAR", states = new { nodes = new[] { new { id = "todo", name = "Todo", type = "unstarted" } } } }, labels = new { nodes = Array.Empty<object>() }, blockedBy = new { nodes = Array.Empty<object>() }, blocks = new { nodes = Array.Empty<object>() }, attachments = new { nodes = Array.Empty<object>() } } } });

    private sealed class FakeLinear(string body) : ILinearTransport
    {
        public LinearTransportResponse Send(string operationName, string query, object variables, string apiKey)
        {
            Assert.DoesNotContain(apiKey, query, StringComparison.Ordinal);
            return new LinearTransportResponse(HttpStatusCode.OK, body);
        }
    }
    private sealed class Workspace : IDisposable
    {
        private Workspace(string path) => Path = path;
        internal string Path { get; }
        internal static Workspace Create() { var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tlaw-linear-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return new Workspace(path); }
        internal string Write(string name, string text) { var path = System.IO.Path.Combine(Path, name); File.WriteAllText(path, text, new UTF8Encoding(false)); return path; }
        public void Dispose() => Directory.Delete(Path, true);
    }
}
