using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tlaw.Dispatcher;
using static TheLogsAreWrong.Domain.Tests.AgentProtocol.LinearTransitionTestSupport;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

/// <summary>
/// Deterministic lock-held authority checks. Transport callbacks provide synchronization points;
/// these tests never depend on scheduling delays or sleeps.
/// </summary>
public sealed class LinearTransitionLeaseGuardTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-23T12:00:00Z");

    [Fact]
    public void Claim_holds_the_existing_lock_during_live_fetch_and_issue_update()
    {
        using var ws = Workspace.Create();
        var clock = new MutableClock(Now);
        var lease = Acquire(ws, clock);
        var task = WriteTask(ws, ClaimedFrom(lease));
        var transport = ClaimTransport();
        transport.BeforeSend = operation =>
        {
            if (operation is "Issue" or "IssueUpdate") AssertLockHeld(ws);
        };

        var (exit, _, _) = RunTransition(ws, TodoSnapshot(), task, "claim", transport, [("--lease-store", ws.LeaseStore)], clock);

        Assert.Equal(0, exit);
        AssertLeaseBytesUnchanged(ws, lease);
    }

    [Fact]
    public void Claim_concurrent_release_and_replacement_cannot_interleave_during_mutation()
    {
        using var ws = Workspace.Create();
        var clock = new MutableClock(Now);
        var lease = Acquire(ws, clock);
        var task = WriteTask(ws, ClaimedFrom(lease));
        var store = new FileLeaseStore(ws.LeaseStore, clock);
        var transport = ClaimTransport();
        transport.BeforeSend = operation =>
        {
            if (operation != "IssueUpdate") return;
            Assert.Throws<LeaseConflictException>(() => store.Release("BAR-41", lease.ClaimId, LeaseReleaseReason.ManualCancel));
            Assert.Throws<LeaseConflictException>(() => store.Acquire("BAR-41", "claude", TimeSpan.FromMinutes(30)));
        };

        var (exit, _, _) = RunTransition(ws, TodoSnapshot(), task, "claim", transport, [("--lease-store", ws.LeaseStore)], clock);

        Assert.Equal(0, exit);
        AssertLeaseBytesUnchanged(ws, lease);
    }

    [Fact]
    public void Claim_expired_before_initial_lock_held_check_performs_zero_graphql()
    {
        using var ws = Workspace.Create();
        var clock = new MutableClock(Now);
        var lease = Acquire(ws, clock, TimeSpan.FromMinutes(1));
        clock.UtcNow = Now.AddMinutes(1);

        var (exit, _, _) = RunTransition(ws, TodoSnapshot(), WriteTask(ws, ClaimedFrom(lease)), "claim", new NoCallLinear(), [("--lease-store", ws.LeaseStore)], clock);

        Assert.Equal(1, exit);
    }

    [Fact]
    public void Claim_expiring_between_fetch_and_final_pre_mutation_recheck_performs_zero_mutations()
    {
        using var ws = Workspace.Create();
        var clock = new MutableClock(Now);
        var lease = Acquire(ws, clock);
        var transport = new QueueLinear().On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [], []));
        transport.BeforeSend = operation => { if (operation == "Issue") clock.UtcNow = Now.AddHours(1); };

        var (exit, _, _) = RunTransition(ws, TodoSnapshot(), WriteTask(ws, ClaimedFrom(lease)), "claim", transport, [("--lease-store", ws.LeaseStore)], clock);

        Assert.Equal(1, exit);
        Assert.DoesNotContain(transport.Calls, call => call.Op == "IssueUpdate");
    }

    [Fact]
    public void Claim_identity_changed_before_final_recheck_performs_zero_mutations()
    {
        using var ws = Workspace.Create();
        var clock = new MutableClock(Now);
        var lease = Acquire(ws, clock);
        var replacement = lease with { ClaimId = Guid.NewGuid().ToString("N") };
        var transport = new QueueLinear().On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [], []));
        transport.BeforeSend = operation => { if (operation == "Issue") WriteLease(ws, replacement); };

        var (exit, _, _) = RunTransition(ws, TodoSnapshot(), WriteTask(ws, ClaimedFrom(lease)), "claim", transport, [("--lease-store", ws.LeaseStore)], clock);

        Assert.Equal(1, exit);
        Assert.DoesNotContain(transport.Calls, call => call.Op == "IssueUpdate");
    }

    [Fact]
    public void Claim_missing_or_busy_existing_lock_performs_zero_graphql()
    {
        using var missing = Workspace.Create();
        var missingClock = new MutableClock(Now);
        var missingLease = Acquire(missing, missingClock);
        File.Delete(LockPath(missing));
        var missingResult = RunTransition(missing, TodoSnapshot(), WriteTask(missing, ClaimedFrom(missingLease)), "claim", new NoCallLinear(), [("--lease-store", missing.LeaseStore)], missingClock);
        Assert.Equal(1, missingResult.Exit);

        using var busy = Workspace.Create();
        var busyClock = new MutableClock(Now);
        var busyLease = Acquire(busy, busyClock);
        using var held = new FileStream(LockPath(busy), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var busyResult = RunTransition(busy, TodoSnapshot(), WriteTask(busy, ClaimedFrom(busyLease)), "claim", new NoCallLinear(), [("--lease-store", busy.LeaseStore)], busyClock);
        Assert.Equal(1, busyResult.Exit);
    }

    [Fact]
    public void Claim_post_mutation_lease_failure_reports_honest_partial_state()
    {
        using var ws = Workspace.Create();
        var clock = new MutableClock(Now);
        var lease = Acquire(ws, clock);
        var issues = 0;
        var transport = ClaimTransport();
        transport.BeforeSend = operation => { if (operation == "Issue" && ++issues == 2) clock.UtcNow = Now.AddHours(1); };

        var (exit, _, error) = RunTransition(ws, TodoSnapshot(), WriteTask(ws, ClaimedFrom(lease)), "claim", transport, [("--lease-store", ws.LeaseStore)], clock);

        Assert.Equal(1, exit);
        Assert.Contains("Linear may already have changed", error, StringComparison.Ordinal);
        Assert.Single(transport.Calls, call => call.Op == "IssueUpdate");
    }

    [Fact]
    public void Handoff_holds_absent_lease_guard_during_fetch_and_mutation_without_creating_a_lease()
    {
        using var ws = Workspace.Create();
        var clock = new MutableClock(Now);
        EnsureRetiredLock(ws, clock);
        var transport = HandoffTransport();
        transport.BeforeSend = operation =>
        {
            if (operation is "Issue" or "IssueUpdate") AssertLockHeld(ws);
        };

        var (exit, _, _) = RunHandoff(ws, clock, transport);

        Assert.Equal(0, exit);
        Assert.False(File.Exists(LeasePath(ws)));
    }

    [Fact]
    public void Handoff_new_lease_acquisition_cannot_interleave_during_mutation()
    {
        using var ws = Workspace.Create();
        var clock = new MutableClock(Now);
        EnsureRetiredLock(ws, clock);
        var store = new FileLeaseStore(ws.LeaseStore, clock);
        var transport = HandoffTransport();
        transport.BeforeSend = operation =>
        {
            if (operation == "IssueUpdate") Assert.Throws<LeaseConflictException>(() => store.Acquire("BAR-41", "claude", TimeSpan.FromMinutes(30)));
        };

        var (exit, _, _) = RunHandoff(ws, clock, transport);

        Assert.Equal(0, exit);
        Assert.False(File.Exists(LeasePath(ws)));
    }

    [Fact]
    public void Handoff_lease_appearing_before_pre_mutation_recheck_performs_zero_mutations()
    {
        using var ws = Workspace.Create();
        var clock = new MutableClock(Now);
        EnsureRetiredLock(ws, clock);
        var transport = new QueueLinear().On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], []));
        transport.BeforeSend = operation =>
        {
            if (operation == "Issue") WriteLease(ws, NewLease(clock));
        };

        var (exit, _, _) = RunHandoff(ws, clock, transport);

        Assert.Equal(1, exit);
        Assert.DoesNotContain(transport.Calls, call => call.Op == "IssueUpdate");
    }

    [Fact]
    public void Handoff_lease_appearing_after_durable_state_mutation_reports_honest_partial_state()
    {
        using var ws = Workspace.Create();
        var clock = new MutableClock(Now);
        EnsureRetiredLock(ws, clock);
        var issues = 0;
        var transport = HandoffTransport();
        transport.BeforeSend = operation =>
        {
            if (operation == "Issue" && ++issues == 2) WriteLease(ws, NewLease(clock));
        };

        var (exit, _, error) = RunHandoff(ws, clock, transport);

        Assert.Equal(1, exit);
        Assert.Contains("Linear may already have changed", error, StringComparison.Ordinal);
        Assert.Single(transport.Calls, call => call.Op == "IssueUpdate");
    }

    [Fact]
    public void Handoff_missing_or_busy_existing_lock_performs_zero_graphql()
    {
        using var missing = Workspace.Create();
        var missingClock = new MutableClock(Now);
        EnsureRetiredLock(missing, missingClock);
        File.Delete(LockPath(missing));
        var missingResult = RunHandoff(missing, missingClock, new NoCallLinear());
        Assert.Equal(1, missingResult.Exit);

        using var busy = Workspace.Create();
        var busyClock = new MutableClock(Now);
        EnsureRetiredLock(busy, busyClock);
        using var held = new FileStream(LockPath(busy), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var busyResult = RunHandoff(busy, busyClock, new NoCallLinear());
        Assert.Equal(1, busyResult.Exit);
    }

    private static QueueLinear ClaimTransport() => new QueueLinear()
        .On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [], []), ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], []))
        .On("IssueUpdate", MutationOk("issueUpdate"));

    private static QueueLinear HandoffTransport() => new QueueLinear()
        .On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], []), ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [], []))
        .On("IssueUpdate", MutationOk("issueUpdate"));

    private static LinearIssueSnapshot TodoSnapshot() => SnapshotFor("uuid41", "BAR-41", "Todo", "unstarted", [], []);

    private static (int Exit, string Out, string Err) RunHandoff(Workspace ws, ILeaseClock clock, ILinearTransport transport)
    {
        var task = WriteTask(ws, UnclaimedTask());
        var ingestion = WriteIngestion(ws, "reassign");
        return RunTransition(ws, SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []), task, "handoff", transport, [("--handoff-ingestion", ingestion), ("--lease-store", ws.LeaseStore)], clock);
    }

    private static LocalLease Acquire(Workspace ws, ILeaseClock clock, TimeSpan? ttl = null)
        => new FileLeaseStore(ws.LeaseStore, clock).Acquire("BAR-41", "codex", ttl ?? TimeSpan.FromMinutes(30));

    private static void EnsureRetiredLock(Workspace ws, ILeaseClock clock)
    {
        var store = new FileLeaseStore(ws.LeaseStore, clock);
        var lease = store.Acquire("BAR-41", "codex", TimeSpan.FromMinutes(30));
        store.Release("BAR-41", lease.ClaimId, LeaseReleaseReason.ManualCancel);
    }

    private static TaskV2Packet ClaimedFrom(LocalLease lease) => ClaimedTask(
        lease.ClaimedBy, lease.ClaimId,
        FileLeaseStore.FormatCanonicalTimestamp(lease.ClaimStartedAt),
        FileLeaseStore.FormatCanonicalTimestamp(lease.ClaimExpiresAt));

    private static LocalLease NewLease(ILeaseClock clock) => new("BAR-41", "claude", Guid.NewGuid().ToString("N"), clock.UtcNow, clock.UtcNow.AddMinutes(30));

    private static void AssertLockHeld(Workspace ws)
        => Assert.Throws<IOException>(() =>
        {
            using var _ = new FileStream(LockPath(ws), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        });

    private static void AssertLeaseBytesUnchanged(Workspace ws, LocalLease expected)
    {
        var json = File.ReadAllText(LeasePath(ws));
        Assert.Contains(expected.ClaimId, json, StringComparison.Ordinal);
        Assert.Contains(expected.ClaimedBy, json, StringComparison.Ordinal);
    }

    private static void WriteLease(Workspace ws, LocalLease lease)
    {
        var json = JsonSerializer.Serialize(new
        {
            schema = "tlaw.local-lease/v1",
            task_id = lease.TaskId,
            claimed_by = lease.ClaimedBy,
            claim_id = lease.ClaimId,
            claim_started_at = FileLeaseStore.FormatCanonicalTimestamp(lease.ClaimStartedAt),
            claim_expires_at = FileLeaseStore.FormatCanonicalTimestamp(lease.ClaimExpiresAt)
        });
        File.WriteAllText(LeasePath(ws), json, new UTF8Encoding(false));
    }

    private static string LockPath(Workspace ws) => Path.Combine(ws.LeaseStore, "locks", LeaseHash("BAR-41") + ".lock");
    private static string LeasePath(Workspace ws) => Path.Combine(ws.LeaseStore, "leases", LeaseHash("BAR-41") + ".json");
    private static string LeaseHash(string taskId) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(taskId)));

    private sealed class MutableClock(DateTimeOffset now) : ILeaseClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }
}
