using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Tlaw.Verify;

public enum GitObjectReaderFailureCategory
{
    Missing,
    Ambiguous,
    MalformedHeader,
    PartialHeader,
    InvalidByteCount,
    ExcessiveByteCount,
    UnexpectedObjectType,
    TruncatedBody,
    MissingTrailingDelimiter,
    PrematureProcessExit,
    NonZeroExit,
    Stderr,
    Timeout,
    Cancellation,
    ExtraResponse,
    IncompleteObjectSet,
    ProcessStart
}

public sealed record GitObjectRequest(string Key, string Expression, string Path);

public sealed record GitObjectReaderFailure(GitObjectReaderFailureCategory Category, string? RequestKey, string? Path, string Detail);

public sealed record GitObjectReaderEvidence(
    string Mode,
    int ProcessCount,
    int Requested,
    int Completed,
    EvidenceStatus Status,
    int ExitCode,
    string LogPath,
    IReadOnlyList<GitObjectReaderFailure> Failures);

public sealed record GitObjectBatchReadResult(
    IReadOnlyDictionary<string, byte[]> Objects,
    CommandEvidence Command,
    GitObjectReaderEvidence Evidence);

public interface IGitObjectBatchProcess : IAsyncDisposable
{
    Stream StandardInput { get; }
    Stream StandardOutput { get; }
    bool HasExited { get; }
    Task<string> ReadStandardErrorAsync(CancellationToken cancellationToken);
    Task<int> WaitForExitAsync(CancellationToken cancellationToken);
    void Terminate();
}

public interface IGitObjectBatchProcessFactory
{
    ValueTask<IGitObjectBatchProcess> StartAsync(string executable, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken);
}

public sealed class SystemGitObjectBatchProcessFactory : IGitObjectBatchProcessFactory
{
    public ValueTask<IGitObjectBatchProcess> StartAsync(string executable, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        var process = new Process();
        process.StartInfo.FileName = executable;
        process.StartInfo.WorkingDirectory = workingDirectory;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        return ValueTask.FromResult<IGitObjectBatchProcess>(new SystemGitObjectBatchProcess(process));
    }

    private sealed class SystemGitObjectBatchProcess(Process process) : IGitObjectBatchProcess
    {
        public Stream StandardInput => process.StandardInput.BaseStream;
        public Stream StandardOutput => process.StandardOutput.BaseStream;
        public bool HasExited => process.HasExited;
        public Task<string> ReadStandardErrorAsync(CancellationToken cancellationToken) => process.StandardError.ReadToEndAsync(cancellationToken);
        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => WaitAsync(cancellationToken);

        public void Terminate()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }

        public ValueTask DisposeAsync()
        {
            process.Dispose();
            return ValueTask.CompletedTask;
        }

        private async Task<int> WaitAsync(CancellationToken cancellationToken)
        {
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }
    }
}

public sealed class GitObjectBatchReader
{
    private const int MaximumHeaderBytes = 4096;
    private readonly IGitObjectBatchProcessFactory _processFactory;
    private readonly int _maximumObjectBytes;
    private readonly TimeSpan _timeout;

    public GitObjectBatchReader(IGitObjectBatchProcessFactory? processFactory = null, int maximumObjectBytes = 16 * 1024 * 1024, TimeSpan? timeout = null)
    {
        _processFactory = processFactory ?? new SystemGitObjectBatchProcessFactory();
        _maximumObjectBytes = maximumObjectBytes > 0 ? maximumObjectBytes : throw new ArgumentOutOfRangeException(nameof(maximumObjectBytes));
        _timeout = timeout is { } configured && configured > TimeSpan.Zero ? configured : TimeSpan.FromSeconds(30);
    }

    public async Task<GitObjectBatchReadResult> ReadAsync(string executable, IReadOnlyList<string> arguments, string workingDirectory, string logPath, IReadOnlyList<GitObjectRequest> requests, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        var startedAt = DateTimeOffset.UtcNow;
        var objects = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var failures = new List<GitObjectReaderFailure>();
        var completed = 0;
        var exitCode = -1;
        var stderr = string.Empty;
        IGitObjectBatchProcess? process = null;
        Task<string>? stderrTask = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        try
        {
            process = await _processFactory.StartAsync(executable, arguments, workingDirectory, timeout.Token);
            // Stderr is diagnostic evidence.  It must remain readable while the
            // request timeout/cancellation tears down the child process.
            stderrTask = process.ReadStandardErrorAsync(CancellationToken.None);
            foreach (var request in requests)
            {
                await WriteRequestAsync(process.StandardInput, request.Expression, timeout.Token);
                var header = await ReadLineAsync(process.StandardOutput, request, timeout.Token);
                if (header is null)
                {
                    throw new BatchProtocolException(GitObjectReaderFailureCategory.PartialHeader, request, "EOF before batch header delimiter.");
                }

                var bodyLength = ParseHeader(header, request);
                var body = new byte[bodyLength];
                if (!await ReadExactlyAsync(process.StandardOutput, body, timeout.Token))
                {
                    throw new BatchProtocolException(GitObjectReaderFailureCategory.TruncatedBody, request, "EOF before the declared object byte count.");
                }

                var delimiter = await ReadByteAsync(process.StandardOutput, timeout.Token);
                if (delimiter != '\n')
                {
                    throw new BatchProtocolException(GitObjectReaderFailureCategory.MissingTrailingDelimiter, request, "Batch response did not end with its required LF delimiter.");
                }

                objects[request.Key] = body;
                completed++;
            }

            await process.StandardInput.DisposeAsync();
            var hasExtraOutput = await ReadByteAsync(process.StandardOutput, timeout.Token) >= 0;
            exitCode = hasExtraOutput
                ? await TerminateAndWaitAsync(process)
                : await process.WaitForExitAsync(timeout.Token);
            stderr = await stderrTask;
            if (hasExtraOutput)
            {
                failures.Add(new GitObjectReaderFailure(GitObjectReaderFailureCategory.ExtraResponse, null, null, "Received unexpected response bytes after the requested object set."));
            }

            if (exitCode != 0)
            {
                failures.Add(new GitObjectReaderFailure(GitObjectReaderFailureCategory.NonZeroExit, null, null, $"git cat-file exited with {exitCode}."));
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                failures.Add(new GitObjectReaderFailure(GitObjectReaderFailureCategory.Stderr, null, null, "git cat-file wrote to stderr."));
            }
        }
        catch (BatchProtocolException exception)
        {
            var category = exception.Category;
            if (process is not null)
            {
                var exitedBeforeTermination = process.HasExited;
                exitCode = await TerminateAndWaitAsync(process);
                stderr = await ReadStderrAsync(stderrTask);
                if (exitedBeforeTermination &&
                    (category == GitObjectReaderFailureCategory.PartialHeader || category == GitObjectReaderFailureCategory.TruncatedBody) &&
                    exitCode != 0)
                {
                    category = GitObjectReaderFailureCategory.PrematureProcessExit;
                }
            }

            failures.Add(new GitObjectReaderFailure(category, exception.Request.Key, exception.Request.Path, exception.Message));
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (process is not null)
            {
                exitCode = await TerminateAndWaitAsync(process);
                stderr = await ReadStderrAsync(stderrTask);
            }

            var category = cancellationToken.IsCancellationRequested ? GitObjectReaderFailureCategory.Cancellation : GitObjectReaderFailureCategory.Timeout;
            var request = requests.ElementAtOrDefault(completed);
            failures.Add(new GitObjectReaderFailure(category, request?.Key, request?.Path, category == GitObjectReaderFailureCategory.Timeout ? "Git object reader timed out." : "Git object reader was cancelled."));
        }
        catch (Exception exception)
        {
            if (process is not null)
            {
                exitCode = await TerminateAndWaitAsync(process);
                stderr = await ReadStderrAsync(stderrTask);
            }

            failures.Add(new GitObjectReaderFailure(GitObjectReaderFailureCategory.ProcessStart, null, null, $"{exception.GetType().Name}: {exception.Message}"));
        }
        finally
        {
            if (process is not null)
            {
                await process.DisposeAsync();
            }
        }

        var finishedAt = DateTimeOffset.UtcNow;
        var status = failures.Count == 0 && objects.Count == requests.Count ? EvidenceStatus.PASS : EvidenceStatus.FAIL;
        if (status == EvidenceStatus.FAIL && failures.Count == 0)
        {
            failures.Add(new GitObjectReaderFailure(GitObjectReaderFailureCategory.IncompleteObjectSet, null, null, "The requested-object set was incomplete."));
        }

        var command = new CommandEvidence(executable, arguments.ToArray(), workingDirectory, startedAt, finishedAt, exitCode, logPath);
        var evidence = new GitObjectReaderEvidence("git-cat-file-batch", 1, requests.Count, completed, status, exitCode, logPath, failures.ToArray());
        await File.WriteAllTextAsync(logPath, BuildLog(command, evidence, stderr), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return new GitObjectBatchReadResult(objects, command, evidence);
    }

    private int ParseHeader(string header, GitObjectRequest request)
    {
        if (header.EndsWith(" missing", StringComparison.Ordinal))
        {
            throw new BatchProtocolException(GitObjectReaderFailureCategory.Missing, request, "Git reported the requested object as missing.");
        }

        if (header.EndsWith(" ambiguous", StringComparison.Ordinal))
        {
            throw new BatchProtocolException(GitObjectReaderFailureCategory.Ambiguous, request, "Git reported the requested object as ambiguous.");
        }

        var parts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !IsObjectSha(parts[0]))
        {
            throw new BatchProtocolException(GitObjectReaderFailureCategory.MalformedHeader, request, "Batch header did not match '<sha> <type> <byte-count>'.");
        }

        if (!string.Equals(parts[1], "blob", StringComparison.Ordinal))
        {
            throw new BatchProtocolException(GitObjectReaderFailureCategory.UnexpectedObjectType, request, $"Expected blob but received '{parts[1]}'.");
        }

        if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var byteCount) || byteCount < 0)
        {
            throw new BatchProtocolException(GitObjectReaderFailureCategory.InvalidByteCount, request, "Batch header byte count is invalid.");
        }

        if (byteCount > _maximumObjectBytes)
        {
            throw new BatchProtocolException(GitObjectReaderFailureCategory.ExcessiveByteCount, request, $"Batch object exceeds the {_maximumObjectBytes} byte safety maximum.");
        }

        return byteCount;
    }

    private static bool IsObjectSha(string value) =>
        (value.Length == 40 || value.Length == 64) && value.All(character => char.IsAsciiHexDigit(character));

    private static async Task WriteRequestAsync(Stream input, string expression, CancellationToken cancellationToken)
    {
        var bytes = Encoding.ASCII.GetBytes($"{expression}\n");
        await input.WriteAsync(bytes, cancellationToken);
        await input.FlushAsync(cancellationToken);
    }

    private static async Task<string?> ReadLineAsync(Stream output, GitObjectRequest request, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        for (var index = 0; index < MaximumHeaderBytes; index++)
        {
            var value = await ReadByteAsync(output, cancellationToken);
            if (value < 0)
            {
                return null;
            }

            if (value == '\n')
            {
                return Encoding.ASCII.GetString(buffer.ToArray());
            }

            buffer.WriteByte((byte)value);
        }

        throw new BatchProtocolException(GitObjectReaderFailureCategory.MalformedHeader, request, "Batch header exceeded the safety maximum.");
    }

    private static async Task<bool> ReadExactlyAsync(Stream output, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await output.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }

    private static async Task<int> ReadByteAsync(Stream output, CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        var read = await output.ReadAsync(buffer, cancellationToken);
        return read == 0 ? -1 : buffer[0];
    }

    private static async Task<int> TerminateAndWaitAsync(IGitObjectBatchProcess process)
    {
        if (!process.HasExited)
        {
            process.Terminate();
        }

        return await process.WaitForExitAsync(CancellationToken.None);
    }

    private static async Task<string> ReadStderrAsync(Task<string>? stderrTask)
    {
        if (stderrTask is null)
        {
            return string.Empty;
        }

        try
        {
            return await stderrTask;
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
    }

    private static string BuildLog(CommandEvidence command, GitObjectReaderEvidence evidence, string stderr) => string.Join(Environment.NewLine,
    [
        $"executable: {command.Executable}",
        $"arguments: {string.Join(" ", command.Arguments)}",
        $"workingDirectory: {command.WorkingDirectory}",
        $"startedAtUtc: {command.StartedAtUtc:O}",
        $"finishedAtUtc: {command.FinishedAtUtc:O}",
        $"exitCode: {command.ExitCode}",
        $"mode: {evidence.Mode}",
        $"processCount: {evidence.ProcessCount}",
        $"requested: {evidence.Requested}",
        $"completed: {evidence.Completed}",
        $"status: {evidence.Status}",
        $"failures: {string.Join(" | ", evidence.Failures.Select(failure => $"{failure.Category}:{failure.Path ?? "(none)"}"))}",
        $"stderrPresent: {!string.IsNullOrWhiteSpace(stderr)}",
        $"stderrByteCount: {Encoding.UTF8.GetByteCount(stderr)}",
        $"stderrSha256: {Sha256Hasher.HashBytes(Encoding.UTF8.GetBytes(stderr))}"
    ]);

    private sealed class BatchProtocolException(GitObjectReaderFailureCategory category, GitObjectRequest request, string message) : Exception(message)
    {
        public GitObjectReaderFailureCategory Category { get; } = category;
        public GitObjectRequest Request { get; } = request;
    }
}
