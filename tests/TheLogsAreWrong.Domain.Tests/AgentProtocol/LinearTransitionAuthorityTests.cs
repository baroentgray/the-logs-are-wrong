using Tlaw.Dispatcher;
using static TheLogsAreWrong.Domain.Tests.AgentProtocol.LinearTransitionTestSupport;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

/// <summary>
/// A schema string is not authority: each event parses and correlates real repository-native evidence
/// (task/v2, BAR-36 finalization, BAR-37 decision, BAR-39 ingestion, BAR-40 continuation, lease, verifier)
/// before any GraphQL call. Every rejected case proves zero GraphQL was attempted.
/// </summary>
public sealed class LinearTransitionAuthorityTests
{
    private const string MergeSha = "cafebabecafebabecafebabecafebabecafebabe";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-23T12:00:00Z");

    // ---------- queue ----------

    [Fact]
    public void Queue_promotes_unclaimed_backlog_task()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, UnclaimedTask());
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "Backlog", "backlog", [], []),
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [], []))
            .On("IssueUpdate", MutationOk("issueUpdate"));
        var (exit, stdout, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "Backlog", "backlog", [], []), task, "queue", transport, []);
        Assert.Equal(0, exit);
        Assert.Contains("queue -> Todo", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Forged_queue_evidence_cannot_mutate()
    {
        using var ws = Workspace.Create();
        var forged = ws.Write("task.yaml", "schema: tlaw.agent-task/v2\ntask_id: BAR-41\n");
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "Backlog", "backlog", [], []), forged, "queue", new NoCallLinear(), []);
        Assert.Equal(1, exit);
    }

    [Fact]
    public void Claimed_packet_cannot_queue()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, ClaimedTask("codex", "tok0000000000000000000000000000", "2026-07-23T11:30:00.0000000Z", "2026-07-23T11:35:00.0000000Z"));
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "Backlog", "backlog", [], []), task, "queue", new NoCallLinear(), []);
        Assert.Equal(1, exit);
    }

    // ---------- claim ----------

    [Fact]
    public void Claim_promotes_todo_with_matching_active_lease()
    {
        using var ws = Workspace.Create();
        var lease = AcquireLease(ws, Now, TimeSpan.FromMinutes(30));
        var task = WriteTask(ws, ClaimedFromLease(lease));
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [], []),
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], []))
            .On("IssueUpdate", MutationOk("issueUpdate"));
        var (exit, stdout, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "Todo", "unstarted", [], []), task, "claim", transport, [("--lease-store", ws.LeaseStore)], clock: new FakeClock(Now));
        Assert.Equal(0, exit);
        Assert.Contains("claim -> In Progress", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Unclaimed_packet_cannot_claim()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, UnclaimedTask());
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "Todo", "unstarted", [], []), task, "claim", new NoCallLinear(), [("--lease-store", ws.LeaseStore)], clock: new FakeClock(Now));
        Assert.Equal(1, exit);
    }

    [Fact]
    public void Partial_claim_cannot_claim()
    {
        using var ws = Workspace.Create();
        // A partially-populated claim (agent set, token still the unclaimed sentinel) is rejected by the task validator.
        var claimed = TaskPacketGenerator.Generate(ClaimedTask("codex", "0e221c4b8ed84e6dae7eea27008eb449", "2026-07-23T11:30:00.0000000Z", "2026-07-23T11:35:00.0000000Z"), Registry());
        var partial = claimed.Replace("claim_id: 0e221c4b8ed84e6dae7eea27008eb449", "claim_id: unclaimed", StringComparison.Ordinal);
        var task = ws.Write("task.yaml", partial);
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "Todo", "unstarted", [], []), task, "claim", new NoCallLinear(), [("--lease-store", ws.LeaseStore)], clock: new FakeClock(Now));
        Assert.Equal(1, exit);
    }

    [Fact]
    public void Claim_without_lease_fails()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, ClaimedTask("codex", "0e221c4b8ed84e6dae7eea27008eb449", "2026-07-23T11:30:00.0000000Z", "2026-07-23T11:35:00.0000000Z"));
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "Todo", "unstarted", [], []), task, "claim", new NoCallLinear(), [("--lease-store", ws.LeaseStore)], clock: new FakeClock(Now));
        Assert.Equal(1, exit);
    }

    [Fact]
    public void Expired_lease_fails()
    {
        using var ws = Workspace.Create();
        var lease = AcquireLease(ws, Now, TimeSpan.FromMinutes(5));
        var task = WriteTask(ws, ClaimedFromLease(lease));
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "Todo", "unstarted", [], []), task, "claim", new NoCallLinear(), [("--lease-store", ws.LeaseStore)], clock: new FakeClock(Now.AddHours(1)));
        Assert.Equal(1, exit);
    }

    [Fact]
    public void Wrong_agent_or_token_fails()
    {
        using var ws = Workspace.Create();
        var lease = AcquireLease(ws, Now, TimeSpan.FromMinutes(30));
        var task = WriteTask(ws, ClaimedFromLease(lease) with { ClaimId = "0000000000000000000000000000dead" });
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "Todo", "unstarted", [], []), task, "claim", new NoCallLinear(), [("--lease-store", ws.LeaseStore)], clock: new FakeClock(Now));
        Assert.Equal(1, exit);
    }

    // ---------- result ----------

    [Fact]
    public void Result_moves_in_progress_to_in_review_on_success_finalization()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, ClaimedTask("codex", "0e221c4b8ed84e6dae7eea27008eb449", "2026-07-23T11:30:00.0000000Z", "2026-07-23T11:35:00.0000000Z"));
        var finalization = WriteFinalization(ws, "success", "completion", "in_review", claimId: "0e221c4b8ed84e6dae7eea27008eb449", resultSha: new string('c', 64));
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], []),
                ResponseFor("uuid41", "BAR-41", "In Review", "started", [], []))
            .On("IssueUpdate", MutationOk("issueUpdate"));
        var (exit, stdout, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []), task, "result", transport, [("--finalization", finalization)]);
        Assert.Equal(0, exit);
        Assert.Contains("result -> In Review", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Forged_bar36_finalization_fails()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, ClaimedTask("codex", "0e221c4b8ed84e6dae7eea27008eb449", "2026-07-23T11:30:00.0000000Z", "2026-07-23T11:35:00.0000000Z"));
        var forged = ws.Write("finalization.json", "{\"schema\":\"tlaw.dispatcher-finalization/v1\",\"task_id\":\"BAR-41\"}");
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []), task, "result", new NoCallLinear(), [("--finalization", forged)]);
        Assert.Equal(1, exit);
    }

    [Fact]
    public void Bar36_task_claim_mismatch_fails()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, ClaimedTask("codex", "0e221c4b8ed84e6dae7eea27008eb449", "2026-07-23T11:30:00.0000000Z", "2026-07-23T11:35:00.0000000Z"));
        var finalization = WriteFinalization(ws, "success", "completion", "in_review", claimId: "a-different-claim-id", resultSha: new string('c', 64));
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []), task, "result", new NoCallLinear(), [("--finalization", finalization)]);
        Assert.Equal(1, exit);
    }

    // ---------- review ----------

    [Fact]
    public void Review_correction_returns_in_review_to_todo()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, ClaimedTask("codex", "0e221c4b8ed84e6dae7eea27008eb449", "2026-07-23T11:30:00.0000000Z", "2026-07-23T11:35:00.0000000Z"));
        var decision = WriteReviewDecision(ws, "request_changes", "high", 2, "correction", "todo");
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Review", "started", [], []),
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [], []))
            .On("IssueUpdate", MutationOk("issueUpdate"));
        var (exit, stdout, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Review", "started", [], []), task, "review", transport, [("--review-decision", decision)]);
        Assert.Equal(0, exit);
        Assert.Contains("review -> Todo", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Forged_bar37_decision_fails()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, ClaimedTask("codex", "0e221c4b8ed84e6dae7eea27008eb449", "2026-07-23T11:30:00.0000000Z", "2026-07-23T11:35:00.0000000Z"));
        var forged = ws.Write("decision.json", "{\"schema\":\"tlaw.dispatcher-review-decision/v1\",\"task_id\":\"BAR-41\"}");
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Review", "started", [], []), task, "review", new NoCallLinear(), [("--review-decision", forged)]);
        Assert.Equal(1, exit);
    }

    [Fact]
    public void Inconsistent_review_decision_fails()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, ClaimedTask("codex", "0e221c4b8ed84e6dae7eea27008eb449", "2026-07-23T11:30:00.0000000Z", "2026-07-23T11:35:00.0000000Z"));
        // decision merge but verdict request_changes and blocking > 0: internally inconsistent.
        var decision = WriteReviewDecision(ws, "request_changes", "high", 3, "merge", "in_review");
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Review", "started", [], []), task, "review", new NoCallLinear(), [("--review-decision", decision)]);
        Assert.Equal(1, exit);
    }

    [Fact]
    public void Review_merge_decision_alone_remains_a_no_op()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, ClaimedTask("codex", "0e221c4b8ed84e6dae7eea27008eb449", "2026-07-23T11:30:00.0000000Z", "2026-07-23T11:35:00.0000000Z"));
        var decision = WriteReviewDecision(ws, "approve", "low", 0, "merge", "in_review");
        var transport = new QueueLinear().On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "In Review", "started", [], []));
        var (exit, stdout, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Review", "started", [], []), task, "review", transport, [("--review-decision", decision)]);
        Assert.Equal(0, exit);
        Assert.Contains("review (no-op)", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain(transport.Calls, c => c.Op == "IssueUpdate");
    }

    // ---------- handoff authority ----------

    [Fact]
    public void Synthetic_handoff_finalization_schema_fails()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, UnclaimedTask());
        var synthetic = ws.Write("ingestion.json", "{\"schema\":\"tlaw.dispatcher-handoff-finalization/v1\",\"task_id\":\"BAR-41\",\"decision\":\"reassign\"}");
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []), task, "handoff", new NoCallLinear(), [("--handoff-ingestion", synthetic), ("--lease-store", ws.LeaseStore)], clock: new FakeClock(Now));
        Assert.Equal(1, exit);
    }

    [Fact]
    public void Bar39_ingestion_without_bar40_continuation_fails()
    {
        using var ws = Workspace.Create();
        var claimedTask = WriteTask(ws, ClaimedTask("codex", "0e221c4b8ed84e6dae7eea27008eb449", "2026-07-23T11:30:00.0000000Z", "2026-07-23T11:35:00.0000000Z"));
        var ingestion = WriteIngestion(ws, "reassign");
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []), claimedTask, "handoff", new NoCallLinear(), [("--handoff-ingestion", ingestion), ("--lease-store", ws.LeaseStore)], clock: new FakeClock(Now));
        Assert.Equal(1, exit);
    }

    [Fact]
    public void Continuation_without_ingestion_fails()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, UnclaimedTask());
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []), task, "handoff", new NoCallLinear(), [("--lease-store", ws.LeaseStore)], clock: new FakeClock(Now));
        Assert.Equal(1, exit);
    }

    [Fact]
    public void Retired_lease_still_present_fails()
    {
        using var ws = Workspace.Create();
        AcquireLease(ws, Now, TimeSpan.FromMinutes(30)); // lease still present for BAR-41
        var task = WriteTask(ws, UnclaimedTask());
        var ingestion = WriteIngestion(ws, "reassign");
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []), task, "handoff", new NoCallLinear(), [("--handoff-ingestion", ingestion), ("--lease-store", ws.LeaseStore)], clock: new FakeClock(Now));
        Assert.Equal(1, exit);
    }

    [Fact]
    public void Mismatched_handoff_worktree_fails()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, UnclaimedTask(worktree: "task/BAR-41"));
        var ingestion = WriteIngestion(ws, "reassign", worktree: "task/BAR-99"); // branch disagrees with the continuation worktree
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []), task, "handoff", new NoCallLinear(), [("--handoff-ingestion", ingestion), ("--lease-store", ws.LeaseStore)], clock: new FakeClock(Now));
        Assert.Equal(1, exit);
    }

    // ---------- merge ----------

    [Fact]
    public void Only_complete_merge_evidence_reaches_done()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, ClaimedTask("codex", "0e221c4b8ed84e6dae7eea27008eb449", "2026-07-23T11:30:00.0000000Z", "2026-07-23T11:35:00.0000000Z"));
        var decision = WriteReviewDecision(ws, "approve", "low", 0, "merge", "in_review");
        var verification = ws.Write("verification.json", System.Text.Encoding.UTF8.GetString(SerializeReport(PassReport(MergeSha))));
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Review", "started", [], []),
                ResponseFor("uuid41", "BAR-41", "Done", "completed", [], []))
            .On("IssueUpdate", MutationOk("issueUpdate"));
        var extra = new[] { ("--review-decision", decision), ("--verification", verification), ("--merge-sha", MergeSha), ("--repository", ws.Path) };
        var (exit, stdout, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Review", "started", [], []), task, "merge", transport, extra, git: new FakeGit(MergeSha));
        Assert.Equal(0, exit);
        Assert.Contains("merge -> Done", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Merge_with_wrong_origin_main_is_rejected()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, ClaimedTask("codex", "0e221c4b8ed84e6dae7eea27008eb449", "2026-07-23T11:30:00.0000000Z", "2026-07-23T11:35:00.0000000Z"));
        var decision = WriteReviewDecision(ws, "approve", "low", 0, "merge", "in_review");
        var verification = ws.Write("verification.json", System.Text.Encoding.UTF8.GetString(SerializeReport(PassReport(MergeSha))));
        var extra = new[] { ("--review-decision", decision), ("--verification", verification), ("--merge-sha", MergeSha), ("--repository", ws.Path) };
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Review", "started", [], []), task, "merge", new NoCallLinear(), extra, git: new FakeGit(new string('d', 40)));
        Assert.Equal(1, exit);
    }

    [Fact]
    public void Merge_review_decision_that_is_not_merge_is_rejected()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, ClaimedTask("codex", "0e221c4b8ed84e6dae7eea27008eb449", "2026-07-23T11:30:00.0000000Z", "2026-07-23T11:35:00.0000000Z"));
        var decision = WriteReviewDecision(ws, "request_changes", "high", 2, "correction", "todo");
        var verification = ws.Write("verification.json", System.Text.Encoding.UTF8.GetString(SerializeReport(PassReport(MergeSha))));
        var extra = new[] { ("--review-decision", decision), ("--verification", verification), ("--merge-sha", MergeSha), ("--repository", ws.Path) };
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Review", "started", [], []), task, "merge", new NoCallLinear(), extra, git: new FakeGit(MergeSha));
        Assert.Equal(1, exit);
    }

    // ---------- identity correlation ----------

    [Fact]
    public void Task_that_does_not_name_the_issue_is_rejected()
    {
        using var ws = Workspace.Create();
        var task = WriteTask(ws, UnclaimedTask() with { SourceId = "BAR-999" });
        var (exit, _, _) = RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "Backlog", "backlog", [], []), task, "queue", new NoCallLinear(), []);
        Assert.Equal(1, exit);
    }

    // ---------- helpers ----------

    private static LocalLease AcquireLease(Workspace ws, DateTimeOffset now, TimeSpan ttl)
    {
        var store = new FileLeaseStore(ws.LeaseStore, new FakeClock(now));
        return store.Acquire("BAR-41", "codex", ttl);
    }

    private static TaskV2Packet ClaimedFromLease(LocalLease lease) => ClaimedTask(
        lease.ClaimedBy, lease.ClaimId,
        FileLeaseStore.FormatCanonicalTimestamp(lease.ClaimStartedAt),
        FileLeaseStore.FormatCanonicalTimestamp(lease.ClaimExpiresAt));
}
