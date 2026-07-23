using Tlaw.AgentProtocol;
using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class ReviewIngestionPolicyTests
{
    [Theory]
    [InlineData("approve", "", "merge", "in_review", "none", 0)]
    [InlineData("approve", "low,info", "merge", "in_review", "low", 0)]
    [InlineData("request_changes", "blocker", "correction", "todo", "blocker", 1)]
    [InlineData("request_changes", "high,medium", "correction", "todo", "high", 2)]
    [InlineData("comment", "medium,info", "human", "in_review", "medium", 1)]
    public void Closed_review_policy_maps_valid_evidence_deterministically(string verdict, string findings, string decision, string state, string highest, int blocking)
    {
        var value = ReviewPolicy.Decide(Review(verdict, findings));
        Assert.Equal((highest, blocking, decision, state), (value.Highest, value.Blocking, value.Decision, value.NextState));
    }

    [Theory]
    [InlineData("approve", "medium")]
    [InlineData("request_changes", "low")]
    public void Contradictory_review_verdict_fails_closed(string verdict, string findings) => Assert.Throws<ReviewIngestionException>(() => ReviewPolicy.Decide(Review(verdict, findings)));

    [Fact]
    public void Expected_head_is_exact_lowercase_hex() => Assert.Throws<ReviewIngestionException>(() => ReviewOptions.Parse(["ingest-review", "--task", "a", "--finalization", "b", "--review", "c", "--expected-head", new string('A', 40), "--output", "d"]));

    private static ProtocolPacket Review(string verdict, string findings)
    {
        var lines = string.IsNullOrEmpty(findings) ? "findings: []" : "findings:\n" + string.Join("\n", findings.Split(',').Select(severity => $"  - severity: {severity}\n    summary: finding\n    evidence: test"));
        var yaml = $"schema: tlaw.agent-review/v1\ntask_id: BAR-37\nreviewed_head: {new string('a', 40)}\nverdict: {verdict}\nhuman_summary: Review.\nevidence:\n  - kind: command\n    reference: dotnet test\n{lines}\n";
        var root = FindRoot(); var value = PacketValidator.Validate(yaml, PacketSchemaRegistry.Load(Path.Combine(root, "docs", "agent", "schemas")));
        return Assert.IsType<ProtocolPacket>(value.Packet);
    }
    private static string FindRoot()
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
}
