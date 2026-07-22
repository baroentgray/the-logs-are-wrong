using System.Globalization;
using System.Text;
using Tlaw.AgentProtocol;
using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class LocalLeaseLifecycleTests
{
    [Fact]
    public void Acquire_is_persistent_and_rejects_a_second_active_claim()
    {
        using var workspace = LeaseWorkspace.Create();
        var clock = new TestLeaseClock("2026-07-22T11:30:00.0000000Z");
        var firstStore = new FileLeaseStore(workspace.StorePath, clock);
        var acquired = firstStore.Acquire("BAR-26", "codex", TimeSpan.FromMinutes(5));

        var reopenedStore = new FileLeaseStore(workspace.StorePath, clock);
        var inspection = reopenedStore.Inspect("BAR-26");

        Assert.Equal(LocalLeaseStatus.Active, inspection.Status);
        Assert.Equal(acquired, inspection.Lease);
        Assert.Throws<LeaseConflictException>(() => reopenedStore.Acquire("BAR-26", "claude", TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Different_task_ids_have_independent_leases()
    {
        using var workspace = LeaseWorkspace.Create();
        var store = new FileLeaseStore(workspace.StorePath, new TestLeaseClock("2026-07-22T11:30:00.0000000Z"));

        var first = store.Acquire("BAR-26-A", "codex", TimeSpan.FromMinutes(5));
        var second = store.Acquire("BAR-26-B", "claude", TimeSpan.FromMinutes(5));

        Assert.NotEqual(first.ClaimId, second.ClaimId);
        Assert.Equal(LocalLeaseStatus.Active, store.Inspect("BAR-26-A").Status);
        Assert.Equal(LocalLeaseStatus.Active, store.Inspect("BAR-26-B").Status);
    }

    [Fact]
    public async Task Independent_store_instances_use_the_filesystem_lock_so_only_one_concurrent_acquire_succeeds()
    {
        using var workspace = LeaseWorkspace.Create();
        var clock = new TestLeaseClock("2026-07-22T11:30:00.0000000Z");

        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var start = new Barrier(2);
            var taskId = $"BAR-26-concurrent-{attempt}";
            var attempts = new[] { "codex", "claude" }
                .Select(executor => Task.Run(() =>
                {
                    var store = new FileLeaseStore(workspace.StorePath, clock);
                    start.SignalAndWait();
                    try
                    {
                        return (Lease: store.Acquire(taskId, executor, TimeSpan.FromMinutes(5)), Error: (Exception?)null);
                    }
                    catch (Exception exception)
                    {
                        return (Lease: (LocalLease?)null, Error: exception);
                    }
                }))
                .ToArray();

            var results = await Task.WhenAll(attempts);

            Assert.Single(results, result => result.Lease is not null);
            Assert.Single(results, result => result.Error is LeaseConflictException);
        }
    }

    [Theory]
    [InlineData(LeaseReleaseReason.Completion)]
    [InlineData(LeaseReleaseReason.Error)]
    [InlineData(LeaseReleaseReason.Timeout)]
    [InlineData(LeaseReleaseReason.QuotaExhaustion)]
    [InlineData(LeaseReleaseReason.ManualCancel)]
    public void Guarded_release_accepts_only_a_matching_active_claim(LeaseReleaseReason reason)
    {
        using var workspace = LeaseWorkspace.Create();
        var store = new FileLeaseStore(workspace.StorePath, new TestLeaseClock("2026-07-22T11:30:00.0000000Z"));
        var lease = store.Acquire("BAR-26", "codex", TimeSpan.FromMinutes(5));

        store.Release("BAR-26", lease.ClaimId, reason);

        Assert.Equal(LocalLeaseStatus.Missing, store.Inspect("BAR-26").Status);
    }

    [Fact]
    public void Guarded_release_rejects_an_incorrect_active_fencing_token()
    {
        using var workspace = LeaseWorkspace.Create();
        var store = new FileLeaseStore(workspace.StorePath, new TestLeaseClock("2026-07-22T11:30:00.0000000Z"));
        store.Acquire("BAR-26", "codex", TimeSpan.FromMinutes(5));

        Assert.Throws<LeaseGuardException>(() => store.Release("BAR-26", Guid.NewGuid().ToString("N"), LeaseReleaseReason.Error));
        Assert.Equal(LocalLeaseStatus.Active, store.Inspect("BAR-26").Status);
    }

    [Fact]
    public void Expired_lease_may_be_taken_over_but_the_old_fencing_token_cannot_release_it()
    {
        using var workspace = LeaseWorkspace.Create();
        var clock = new TestLeaseClock("2026-07-22T11:30:00.0000000Z");
        var store = new FileLeaseStore(workspace.StorePath, clock);
        var expired = store.Acquire("BAR-26", "codex", TimeSpan.FromMinutes(1));
        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(LocalLeaseStatus.Expired, store.Inspect("BAR-26").Status);
        Assert.Throws<LeaseGuardException>(() => store.Release("BAR-26", expired.ClaimId, LeaseReleaseReason.Timeout));
        var takeover = store.Acquire("BAR-26", "claude", TimeSpan.FromMinutes(5));

        Assert.NotEqual(expired.ClaimId, takeover.ClaimId);
        Assert.Throws<LeaseGuardException>(() => store.Release("BAR-26", expired.ClaimId, LeaseReleaseReason.Error));
        Assert.Equal(takeover, store.Inspect("BAR-26").Lease);
    }

    [Fact]
    public void Invalid_ttl_and_corrupt_persistent_state_fail_closed()
    {
        using var workspace = LeaseWorkspace.Create();
        var store = new FileLeaseStore(workspace.StorePath, new TestLeaseClock("2026-07-22T11:30:00.0000000Z"));

        Assert.Throws<ArgumentOutOfRangeException>(() => store.Acquire("BAR-26", "codex", TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Acquire("BAR-26", "codex", TimeSpan.FromMinutes(-1)));

        store.Acquire("BAR-26", "codex", TimeSpan.FromMinutes(5));
        var leaseFile = Directory.EnumerateFiles(workspace.StorePath, "*.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(leaseFile, "{ not valid json", new UTF8Encoding(false));

        Assert.Throws<LeaseStoreException>(() => new FileLeaseStore(workspace.StorePath, new TestLeaseClock("2026-07-22T11:30:00.0000000Z")).Inspect("BAR-26"));
    }

    [Fact]
    public void Duplicate_claim_id_fails_closed_and_neither_token_can_mutate_the_persisted_lease()
    {
        using var workspace = LeaseWorkspace.Create();
        var store = new FileLeaseStore(workspace.StorePath, new TestLeaseClock("2026-07-22T11:30:00.0000000Z"));
        var legitimateLease = store.Acquire("BAR-26", "codex", TimeSpan.FromMinutes(5));
        var injectedClaimId = Guid.NewGuid().ToString("N");
        var leaseFile = Directory.EnumerateFiles(workspace.StorePath, "*.json", SearchOption.AllDirectories).Single();
        var corruptRecord = AppendDuplicateRootProperty(File.ReadAllText(leaseFile), "claim_id", injectedClaimId);
        File.WriteAllText(leaseFile, corruptRecord, new UTF8Encoding(false));

        Assert.Throws<LeaseStoreException>(() => store.Inspect("BAR-26"));
        Assert.Throws<LeaseStoreException>(() => store.Acquire("BAR-26", "claude", TimeSpan.FromMinutes(5)));
        Assert.Throws<LeaseStoreException>(() => store.Release("BAR-26", legitimateLease.ClaimId, LeaseReleaseReason.Error));
        Assert.Throws<LeaseStoreException>(() => store.Release("BAR-26", injectedClaimId, LeaseReleaseReason.Error));
        Assert.True(File.Exists(leaseFile));
        Assert.Equal(corruptRecord, File.ReadAllText(leaseFile));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("task_id")]
    [InlineData("claimed_by")]
    [InlineData("claim_id")]
    [InlineData("claim_started_at")]
    [InlineData("claim_expires_at")]
    public void Every_duplicate_recognized_lease_root_property_fails_closed_without_rewriting_the_record(string property)
    {
        using var workspace = LeaseWorkspace.Create();
        var store = new FileLeaseStore(workspace.StorePath, new TestLeaseClock("2026-07-22T11:30:00.0000000Z"));
        store.Acquire("BAR-26", "codex", TimeSpan.FromMinutes(5));
        var leaseFile = Directory.EnumerateFiles(workspace.StorePath, "*.json", SearchOption.AllDirectories).Single();
        var corruptRecord = AppendDuplicateRootProperty(File.ReadAllText(leaseFile), property, "injected");
        File.WriteAllText(leaseFile, corruptRecord, new UTF8Encoding(false));

        Assert.Throws<LeaseStoreException>(() => store.Inspect("BAR-26"));
        Assert.Throws<LeaseStoreException>(() => store.Acquire("BAR-26", "claude", TimeSpan.FromMinutes(5)));
        Assert.True(File.Exists(leaseFile));
        Assert.Equal(corruptRecord, File.ReadAllText(leaseFile));
    }

    [Fact]
    public void Lease_command_emits_a_claimed_v2_packet_accepted_by_the_protocol_validator()
    {
        using var workspace = LeaseWorkspace.Create();
        var input = workspace.Write("task.yaml", File.ReadAllText(Path.Combine(ExamplesRoot, "task.v2.valid.yaml")));
        var output = Path.Combine(workspace.Path, "claimed.yaml");
        using var errors = new StringWriter(CultureInfo.InvariantCulture);

        var exitCode = LeaseCommand.Run(
            ["lease", "acquire", "--task", input, "--store", workspace.StorePath, "--executor", "codex", "--ttl", "00:05:00", "--output", output],
            TextWriter.Null,
            errors);

        Assert.Equal(0, exitCode);
        var validation = PacketValidator.Validate(File.ReadAllText(output), PacketSchemaRegistry.Load(SchemaRoot));
        Assert.True(validation.IsValid, string.Join(" | ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal("codex", validation.Packet!.RequiredString("claimed_by"));
        Assert.NotEqual("unclaimed", validation.Packet.RequiredString("claim_id"));
        Assert.DoesNotContain('\r', File.ReadAllText(output));
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:01:00")]
    [InlineData("not-a-duration")]
    public void Lease_command_rejects_invalid_ttl_without_creating_output(string ttl)
    {
        using var workspace = LeaseWorkspace.Create();
        var input = workspace.Write("task.yaml", File.ReadAllText(Path.Combine(ExamplesRoot, "task.v2.valid.yaml")));
        var output = Path.Combine(workspace.Path, "claimed.yaml");
        using var errors = new StringWriter(CultureInfo.InvariantCulture);

        var exitCode = LeaseCommand.Run(
            ["lease", "acquire", "--task", input, "--store", workspace.StorePath, "--executor", "codex", "--ttl", ttl, "--output", output],
            TextWriter.Null,
            errors);

        Assert.NotEqual(0, exitCode);
        Assert.False(File.Exists(output));
        Assert.StartsWith("FAIL:", errors.ToString(), StringComparison.Ordinal);
        Assert.Equal(LocalLeaseStatus.Missing, new FileLeaseStore(workspace.StorePath, new TestLeaseClock("2026-07-22T11:30:00.0000000Z")).Inspect("BAR-26-increment-1").Status);
    }

    [Fact]
    public void Lease_command_rejects_claimed_input_or_an_ineligible_executor_without_creating_output()
    {
        using var workspace = LeaseWorkspace.Create();
        var unclaimed = workspace.Write("unclaimed.yaml", File.ReadAllText(Path.Combine(ExamplesRoot, "task.v2.valid.yaml")));
        var claimed = workspace.Write("claimed.yaml", File.ReadAllText(Path.Combine(ExamplesRoot, "task.v2.claimed.valid.yaml")));
        var ineligibleOutput = Path.Combine(workspace.Path, "ineligible-output.yaml");
        var claimedOutput = Path.Combine(workspace.Path, "claimed-output.yaml");

        Assert.NotEqual(0, LeaseCommand.Run(["lease", "acquire", "--task", unclaimed, "--store", workspace.StorePath, "--executor", "grok", "--ttl", "00:05:00", "--output", ineligibleOutput], TextWriter.Null, TextWriter.Null));
        Assert.NotEqual(0, LeaseCommand.Run(["lease", "acquire", "--task", claimed, "--store", workspace.StorePath, "--executor", "codex", "--ttl", "00:05:00", "--output", claimedOutput], TextWriter.Null, TextWriter.Null));

        Assert.False(File.Exists(ineligibleOutput));
        Assert.False(File.Exists(claimedOutput));
        Assert.Equal(LocalLeaseStatus.Missing, new FileLeaseStore(workspace.StorePath, new TestLeaseClock("2026-07-22T11:30:00.0000000Z")).Inspect("BAR-26-increment-1").Status);
    }

    [Fact]
    public void Lease_command_rejects_unknown_release_reason_without_releasing_the_claim()
    {
        using var workspace = LeaseWorkspace.Create();
        var clock = new TestLeaseClock("2026-07-22T11:30:00.0000000Z");
        var store = new FileLeaseStore(workspace.StorePath, clock);
        var lease = store.Acquire("BAR-26", "codex", TimeSpan.FromMinutes(5));
        using var errors = new StringWriter(CultureInfo.InvariantCulture);

        var exitCode = LeaseCommand.Run(
            ["lease", "release", "--task-id", "BAR-26", "--store", workspace.StorePath, "--claim-id", lease.ClaimId, "--reason", "unknown"],
            TextWriter.Null,
            errors);

        Assert.NotEqual(0, exitCode);
        Assert.StartsWith("FAIL:", errors.ToString(), StringComparison.Ordinal);
        Assert.Equal(LocalLeaseStatus.Active, store.Inspect("BAR-26").Status);
    }

    [Fact]
    public void Publication_failure_rolls_back_the_exact_new_lease_and_preserves_no_output()
    {
        using var workspace = LeaseWorkspace.Create();
        var input = workspace.Write("task.yaml", File.ReadAllText(Path.Combine(ExamplesRoot, "task.v2.valid.yaml")));
        var unavailableOutput = Path.Combine(workspace.Path, "missing", "claimed.yaml");
        using var errors = new StringWriter(CultureInfo.InvariantCulture);

        var exitCode = LeaseCommand.Run(
            ["lease", "acquire", "--task", input, "--store", workspace.StorePath, "--executor", "codex", "--ttl", "00:05:00", "--output", unavailableOutput],
            TextWriter.Null,
            errors);

        Assert.NotEqual(0, exitCode);
        Assert.StartsWith("FAIL: Claimed packet publication failed and the exact lease was rolled back:", errors.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(unavailableOutput));
        Assert.Equal(LocalLeaseStatus.Missing, new FileLeaseStore(workspace.StorePath, new TestLeaseClock("2026-07-22T11:30:00.0000000Z")).Inspect("BAR-26-increment-1").Status);
    }

    private static string RepositoryRoot => FindRepositoryRoot();
    private static string SchemaRoot => Path.Combine(RepositoryRoot, "docs", "agent", "schemas");
    private static string ExamplesRoot => Path.Combine(SchemaRoot, "examples");

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

    private static string AppendDuplicateRootProperty(string json, string property, string value)
    {
        var closingBrace = json.LastIndexOf('}');
        Assert.True(closingBrace >= 0, "Expected a root JSON object.");
        return $"{json[..closingBrace]},\"{property}\":\"{value}\"{json[closingBrace..]}";
    }

    private sealed class TestLeaseClock(string timestamp) : ILeaseClock
    {
        private DateTimeOffset _utcNow = DateTimeOffset.ParseExact(timestamp, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        public DateTimeOffset UtcNow => _utcNow;

        internal void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private sealed class LeaseWorkspace : IDisposable
    {
        private LeaseWorkspace(string path)
        {
            Path = path;
            StorePath = System.IO.Path.Combine(path, "lease-store");
            Directory.CreateDirectory(StorePath);
        }

        internal string Path { get; }
        internal string StorePath { get; }

        internal static LeaseWorkspace Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tlaw-local-lease-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new LeaseWorkspace(path);
        }

        internal string Write(string name, string content)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
