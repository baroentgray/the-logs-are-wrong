using System.Text;
using Tlaw.Verify;

namespace TheLogsAreWrong.Domain.Tests.Verification;

public sealed class GitObjectBatchReaderTests
{
    private const string ObjectSha = "0123456789012345678901234567890123456789";

    [Fact]
    public async Task Successful_multi_object_read_preserves_request_order()
    {
        var process = FakeProcess.FromBytes(Combine(Frame("first"), Frame("second")));
        var result = await ReadAsync(process, [Request("first"), Request("second")]);

        Assert.Equal(EvidenceStatus.PASS, result.Evidence.Status);
        Assert.Equal("one", Encoding.UTF8.GetString(result.Objects["first"]));
        Assert.Equal("two", Encoding.UTF8.GetString(result.Objects["second"]));
        Assert.Equal("HEAD:first\nHEAD:second\n", Encoding.ASCII.GetString(process.Input.ToArray()));
        Assert.Equal(1, result.Evidence.ProcessCount);
        Assert.Equal(2, result.Evidence.Completed);
    }

    [Fact]
    public async Task Canonical_git_object_bytes_keep_lf_and_crlf_equivalent()
    {
        var process = FakeProcess.FromBytes(Combine(Frame("first", "approved\n"), Frame("second", "approved\r\n")));
        var result = await ReadAsync(process, [Request("first"), Request("second")]);

        Assert.Equal(EvidenceStatus.PASS, result.Evidence.Status);
        Assert.Equal(Sha256Hasher.HashCanonicalGitObject(result.Objects["first"]), Sha256Hasher.HashCanonicalGitObject(result.Objects["second"]));
    }

    [Fact]
    public async Task Missing_object_is_a_typed_failure()
    {
        var result = await ReadAsync(FakeProcess.FromText("HEAD:first missing\n"), [Request("first")]);

        AssertFailure(result, GitObjectReaderFailureCategory.Missing, "first");
    }

    [Fact]
    public async Task Ambiguous_object_is_a_typed_failure()
    {
        var result = await ReadAsync(FakeProcess.FromText("HEAD:first ambiguous\n"), [Request("first")]);

        AssertFailure(result, GitObjectReaderFailureCategory.Ambiguous, "first");
    }

    [Fact]
    public async Task Malformed_header_is_a_typed_failure()
    {
        var result = await ReadAsync(FakeProcess.FromText("not a batch header\n"), [Request("first")]);

        AssertFailure(result, GitObjectReaderFailureCategory.MalformedHeader, "first");
    }

    [Fact]
    public async Task Partial_header_eof_is_a_typed_failure()
    {
        var result = await ReadAsync(FakeProcess.FromText($"{ObjectSha} blob 3"), [Request("first")]);

        AssertFailure(result, GitObjectReaderFailureCategory.PartialHeader, "first");
    }

    [Fact]
    public async Task Invalid_or_excessive_byte_count_is_a_typed_failure()
    {
        var invalid = await ReadAsync(FakeProcess.FromText($"{ObjectSha} blob nope\n"), [Request("first")]);
        var excessive = await ReadAsync(FakeProcess.FromText($"{ObjectSha} blob 65\n"), [Request("first")]);

        AssertFailure(invalid, GitObjectReaderFailureCategory.InvalidByteCount, "first");
        AssertFailure(excessive, GitObjectReaderFailureCategory.ExcessiveByteCount, "first");
    }

    [Fact]
    public async Task Unexpected_object_type_is_a_typed_failure()
    {
        var result = await ReadAsync(FakeProcess.FromText($"{ObjectSha} tree 0\n\n"), [Request("first")]);

        AssertFailure(result, GitObjectReaderFailureCategory.UnexpectedObjectType, "first");
    }

    [Fact]
    public async Task Truncated_body_and_missing_trailing_delimiter_are_typed_failures()
    {
        var truncated = await ReadAsync(FakeProcess.FromBytes(Combine(Header(5), Encoding.UTF8.GetBytes("one"))), [Request("first")]);
        var delimiter = await ReadAsync(FakeProcess.FromBytes(Combine(Header(3), Encoding.UTF8.GetBytes("one"))), [Request("first")]);

        AssertFailure(truncated, GitObjectReaderFailureCategory.TruncatedBody, "first");
        AssertFailure(delimiter, GitObjectReaderFailureCategory.MissingTrailingDelimiter, "first");
    }

    [Fact]
    public async Task Premature_process_exit_mid_stream_is_a_typed_failure()
    {
        var result = await ReadAsync(FakeProcess.FromBytes(Combine(Header(5), Encoding.UTF8.GetBytes("one")), exitCode: -1073741819, hasExited: true), [Request("first")]);

        AssertFailure(result, GitObjectReaderFailureCategory.PrematureProcessExit, "first");
    }

    [Fact]
    public async Task Non_zero_exit_after_complete_responses_is_a_typed_failure()
    {
        var result = await ReadAsync(FakeProcess.FromBytes(Frame("first"), exitCode: 17), [Request("first")]);

        AssertFailure(result, GitObjectReaderFailureCategory.NonZeroExit, null);
    }

    [Fact]
    public async Task Extra_output_and_stderr_are_typed_failures_without_logging_raw_content()
    {
        var extraProcess = FakeProcess.FromBytes(Combine(Frame("first"), [0x01]));
        var extra = await ReadAsync(extraProcess, [Request("first")]);
        var stderrText = "raw-gate-content-must-not-appear-in-evidence";
        var logPath = LogPath();
        var stderrProcess = FakeProcess.FromBytes(Frame("first"), stderr: stderrText);
        var reader = new GitObjectBatchReader(new FakeFactory(stderrProcess), maximumObjectBytes: 64, timeout: TimeSpan.FromSeconds(1));
        var stderr = await reader.ReadAsync("git", ["cat-file", "--batch"], "/repo", logPath, [Request("first")], TestContext.Current.CancellationToken);

        AssertFailure(extra, GitObjectReaderFailureCategory.ExtraResponse, null);
        AssertFailure(stderr, GitObjectReaderFailureCategory.Stderr, null);
        Assert.True(extraProcess.Terminated);
        Assert.DoesNotContain(stderrText, await File.ReadAllTextAsync(logPath, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Incomplete_object_set_fails_closed()
    {
        var requests = new[]
        {
            new GitObjectRequest("duplicate", "HEAD:first", "first"),
            new GitObjectRequest("duplicate", "HEAD:second", "second")
        };

        var result = await ReadAsync(FakeProcess.FromBytes(Combine(Frame("first"), Frame("second"))), requests);

        AssertFailure(result, GitObjectReaderFailureCategory.IncompleteObjectSet, null);
    }

    [Fact]
    public async Task Timeout_or_cancellation_is_a_typed_failure()
    {
        var process = new FakeProcess(new BlockingReadStream(), exitCode: 0);
        var reader = new GitObjectBatchReader(new FakeFactory(process), maximumObjectBytes: 64, timeout: TimeSpan.FromMilliseconds(25));
        var result = await reader.ReadAsync("git", ["cat-file", "--batch"], "/repo", LogPath(), [Request("first")], TestContext.Current.CancellationToken);

        AssertFailure(result, GitObjectReaderFailureCategory.Timeout, "first");
        Assert.True(process.Terminated);
    }

    [Fact]
    public async Task Partial_completion_followed_by_failure_is_explicit()
    {
        var process = FakeProcess.FromBytes(Combine(Frame("first"), Encoding.ASCII.GetBytes("HEAD:second missing\n")));
        var result = await ReadAsync(process, [Request("first"), Request("second")]);

        Assert.Equal(1, result.Evidence.Completed);
        Assert.Equal("one", Encoding.UTF8.GetString(result.Objects["first"]));
        AssertFailure(result, GitObjectReaderFailureCategory.Missing, "second");
    }

    [Fact]
    public void Reader_failure_propagates_to_final_verifier_fail_with_concrete_reason()
    {
        var reader = FailedReaderEvidence(GitObjectReaderFailureCategory.PrematureProcessExit, "docs/contract.md");
        var bytes = Encoding.UTF8.GetBytes("approved\n");
        var baseline = new Gate0Baseline("fixture", "base", [new Gate0FileHash("docs/contract.md", Sha256Hasher.HashCanonicalGitObject(bytes))]);
        var snapshot = new Gate0GitSnapshot(
            new Dictionary<string, byte[]> { ["docs/contract.md"] = bytes },
            new Dictionary<string, byte[]> { ["docs/contract.md"] = bytes },
            new Gate0ChangeSet([], true), new Gate0ChangeSet([], true), new Gate0ChangeSet([], true), new Gate0ChangeSet([], true), reader);
        var gate = Gate0Verifier.Verify(baseline, snapshot);
        var report = PassingReport() with
        {
            Gate0 = gate
        };

        var outcome = VerificationVerdictEvaluator.Evaluate(report);

        Assert.Equal(EvidenceStatus.FAIL, gate.Status);
        Assert.Contains("git-object-reader:PrematureProcessExit:docs/contract.md", gate.Mismatches);
        Assert.Equal(VerificationVerdict.FAIL, outcome.Verdict);
        Assert.Contains(outcome.FailureReasons, reason => reason.Contains("PrematureProcessExit", StringComparison.Ordinal));
    }

    [Fact]
    public void Successful_reader_allows_gate_comparison()
    {
        var bytes = Encoding.UTF8.GetBytes("approved\n");
        var baseline = new Gate0Baseline("fixture", "base", [new Gate0FileHash("docs/contract.md", Sha256Hasher.HashCanonicalGitObject(bytes))]);
        var reader = new GitObjectReaderEvidence("batch", 1, 2, 2, EvidenceStatus.PASS, 0, "logs/batch.log", []);
        var snapshot = new Gate0GitSnapshot(
            new Dictionary<string, byte[]> { ["docs/contract.md"] = bytes },
            new Dictionary<string, byte[]> { ["docs/contract.md"] = bytes },
            new Gate0ChangeSet([], true), new Gate0ChangeSet([], true), new Gate0ChangeSet([], true), new Gate0ChangeSet([], true), reader);

        Assert.Equal(EvidenceStatus.PASS, Gate0Verifier.Verify(baseline, snapshot).Status);
    }

    private static async Task<GitObjectBatchReadResult> ReadAsync(FakeProcess process, IReadOnlyList<GitObjectRequest> requests)
    {
        var reader = new GitObjectBatchReader(new FakeFactory(process), maximumObjectBytes: 64, timeout: TimeSpan.FromSeconds(1));
        return await reader.ReadAsync("git", ["cat-file", "--batch"], "/repo", LogPath(), requests, TestContext.Current.CancellationToken);
    }

    private static GitObjectRequest Request(string key) => new(key, $"HEAD:{key}", key);

    private static byte[] Frame(string key, string? content = null)
    {
        var bytes = Encoding.UTF8.GetBytes(content ?? (key == "first" ? "one" : "two"));
        return Header(bytes.Length).Concat(bytes).Append((byte)'\n').ToArray();
    }

    private static byte[] Header(int count) => Encoding.ASCII.GetBytes($"{ObjectSha} blob {count}\n");

    private static byte[] Combine(params byte[][] parts) => parts.SelectMany(static part => part).ToArray();

    private static void AssertFailure(GitObjectBatchReadResult result, GitObjectReaderFailureCategory category, string? key)
    {
        Assert.Equal(EvidenceStatus.FAIL, result.Evidence.Status);
        Assert.Contains(result.Evidence.Failures, failure => failure.Category == category && failure.RequestKey == key);
    }

    private static string LogPath() => Path.Combine(Path.GetTempPath(), "tlaw-verify-tests", Guid.NewGuid().ToString("N"), "batch.log");

    private static GitObjectReaderEvidence FailedReaderEvidence(GitObjectReaderFailureCategory category, string path) =>
        new("batch", 1, 2, 1, EvidenceStatus.FAIL, -1073741819, "logs/batch.log", [new GitObjectReaderFailure(category, path, path, "fixture")]);

    private static VerificationReport PassingReport() => new(
        "tlaw.verification/v1", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "/repo", "main", false, "head", "head", "base", null, true,
        new VerificationEnvironment("test", "10"),
        [new CommandEvidence("dotnet", ["restore"], "/repo", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 0, "restore.log"),
         new CommandEvidence("dotnet", ["build"], "/repo", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 0, "build.log"),
         new CommandEvidence("dotnet", ["test"], "/repo", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 0, "test.log"),
         new CommandEvidence("git", ["diff", "--check"], "/repo", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 0, "diff.log")],
        new CheckEvidence(EvidenceStatus.PASS), new BuildEvidence(EvidenceStatus.PASS, 0, 0), new TestEvidence(EvidenceStatus.PASS, 1, 0, 0, 1, "tests.trx"),
        new CheckEvidence(EvidenceStatus.PASS), new Gate0Evidence(EvidenceStatus.PASS, "fixture", "base", [], [], [], [], [], []),
        new ArchitectureEvidence(EvidenceStatus.PASS, ["ArchitectureGuardTests: Passed"]), new DomainDependenciesEvidence(EvidenceStatus.PASS, []), VerificationVerdict.PASS, []);

    private sealed class FakeFactory(FakeProcess process) : IGitObjectBatchProcessFactory
    {
        public ValueTask<IGitObjectBatchProcess> StartAsync(string executable, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken) => ValueTask.FromResult<IGitObjectBatchProcess>(process);
    }

    private sealed class FakeProcess : IGitObjectBatchProcess
    {
        private readonly Stream _output;
        private readonly int _exitCode;

        public FakeProcess(Stream output, int exitCode, string stderr = "", bool hasExited = false)
        {
            _output = output;
            _exitCode = exitCode;
            Stderr = stderr;
            HasExited = hasExited;
        }

        public MemoryStream Input { get; } = new();
        public string Stderr { get; }
        public bool Terminated { get; private set; }
        public bool HasExited { get; }
        public Stream StandardInput => Input;
        public Stream StandardOutput => _output;
        public static FakeProcess FromBytes(byte[] bytes, int exitCode = 0, string stderr = "", bool hasExited = false) => new(new MemoryStream(bytes), exitCode, stderr, hasExited);
        public static FakeProcess FromText(string text, int exitCode = 0, string stderr = "", bool hasExited = false) => FromBytes(Encoding.ASCII.GetBytes(text), exitCode, stderr, hasExited);
        public Task<string> ReadStandardErrorAsync(CancellationToken cancellationToken) => Task.FromResult(Stderr);
        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(Terminated && _exitCode == 0 ? -1 : _exitCode);
        public void Terminate() => Terminated = true;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
