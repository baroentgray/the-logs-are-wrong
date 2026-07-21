using System.Runtime.InteropServices;

namespace Tlaw.Verify;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = VerificationOptions.Parse(args);
        var repositoryRoot = options.RepositoryRoot ?? RepositoryRootFinder.Find(Environment.CurrentDirectory) ?? Path.GetFullPath(Environment.CurrentDirectory);
        var artifactsDirectory = options.ArtifactsDirectory ?? Path.Combine(repositoryRoot, "artifacts", "verification");
        var logsDirectory = Path.Combine(artifactsDirectory, "logs");
        var testResultsDirectory = Path.Combine(artifactsDirectory, "test-results");
        Directory.CreateDirectory(logsDirectory);
        Directory.CreateDirectory(testResultsDirectory);

        var startedAt = DateTimeOffset.UtcNow;
        var commands = new List<CommandEvidence>();
        var report = CreateInitialReport(repositoryRoot, options, startedAt);

        var gitExecutable = ToolExecutableResolver.ResolveGitExecutable();
        var branch = await RunGitValueAsync("branch", ["branch", "--show-current"], gitExecutable, repositoryRoot, logsDirectory, commands);
        var actualHead = await RunGitValueAsync("head", ["rev-parse", "HEAD"], gitExecutable, repositoryRoot, logsDirectory, commands);
        var actualBase = await RunGitValueAsync("base", ["merge-base", "HEAD", "origin/main"], gitExecutable, repositoryRoot, logsDirectory, commands);
        var status = await RunGitValueAsync("status", ["status", "--porcelain=v1"], gitExecutable, repositoryRoot, logsDirectory, commands);
        var dotnetSdk = await RunValueAsync("dotnet-version", "dotnet", ["--version"], repositoryRoot, logsDirectory, commands);
        var isDetachedHead = string.IsNullOrWhiteSpace(branch.Value);
        report = report with
        {
            Branch = isDetachedHead ? null : branch.Value,
            IsDetachedHead = isDetachedHead,
            ActualHeadSha = actualHead.Value,
            ActualBaseSha = actualBase.Value,
            CleanTree = status.Execution.Evidence.ExitCode == 0 && string.IsNullOrWhiteSpace(status.Value),
            Environment = new VerificationEnvironment(RuntimeInformation.OSDescription, dotnetSdk.Value),
            Commands = commands.ToArray()
        };

        var restore = await RunAsync("restore", "dotnet", ["restore", "--property:Configuration=Release"], repositoryRoot, logsDirectory, commands);
        report = report with { Restore = new CheckEvidence(ToStatus(restore.Evidence.ExitCode)), Commands = commands.ToArray() };

        var build = await RunAsync("build", "dotnet", ["build", "--configuration", "Release", "--no-restore"], repositoryRoot, logsDirectory, commands);
        var diagnostics = BuildDiagnosticParser.Parse(build.Output);
        report = report with
        {
            Build = new BuildEvidence(ToStatus(build.Evidence.ExitCode, diagnostics.Errors == 0), diagnostics.Warnings, diagnostics.Errors),
            Commands = commands.ToArray()
        };

        var trxPath = Path.Combine(testResultsDirectory, "verification.trx");
        var test = await RunAsync("test", "dotnet", ["test", "--configuration", "Release", "--no-build", "--no-restore", "--logger", "trx;LogFileName=verification.trx", "--results-directory", testResultsDirectory], repositoryRoot, logsDirectory, commands);
        report = report with { Tests = CreateTestEvidence(test, trxPath), Commands = commands.ToArray() };

        var diffCheck = await RunAsync("diff-check", gitExecutable, ["diff", "--check"], repositoryRoot, logsDirectory, commands);
        var diffRangeCheck = string.IsNullOrWhiteSpace(report.ActualBaseSha)
            ? null
            : await RunAsync("diff-range-check", gitExecutable, ["diff", "--check", $"{report.ActualBaseSha}...HEAD"], repositoryRoot, logsDirectory, commands);
        report = report with
        {
            DiffCheck = new CheckEvidence(ToStatus(diffCheck.Evidence.ExitCode) == EvidenceStatus.PASS && (diffRangeCheck is null || ToStatus(diffRangeCheck.Evidence.ExitCode) == EvidenceStatus.PASS) ? EvidenceStatus.PASS : EvidenceStatus.FAIL),
            Commands = commands.ToArray()
        };

        report = report with
        {
            Gate0 = await VerifyGate0Async(repositoryRoot, gitExecutable, logsDirectory, commands),
            Architecture = ExtractArchitecture(report.Tests),
            DomainDependencies = VerifyDomainDependencies(repositoryRoot),
            FinishedAtUtc = DateTimeOffset.UtcNow,
            Commands = commands.ToArray()
        };

        var outcome = VerificationVerdictEvaluator.Evaluate(report, options.AllowDetachedHead);
        report = report with { Verdict = outcome.Verdict, FailureReasons = outcome.FailureReasons };
        var jsonPath = Path.Combine(artifactsDirectory, "verification.json");
        await File.WriteAllTextAsync(jsonPath, VerificationReportSerializer.Serialize(report));
        var persisted = VerificationReportSerializer.Deserialize(await File.ReadAllTextAsync(jsonPath));
        await File.WriteAllTextAsync(Path.Combine(artifactsDirectory, "verification.md"), MarkdownReportRenderer.Render(persisted));

        Console.WriteLine(report.Verdict);
        return report.Verdict == VerificationVerdict.PASS ? 0 : 1;
    }

    private static VerificationReport CreateInitialReport(string repositoryRoot, VerificationOptions options, DateTimeOffset startedAt) => new(
        "tlaw.verification/v1",
        startedAt,
        startedAt,
        repositoryRoot,
        null,
        false,
        string.Empty,
        options.ExpectedHeadSha ?? string.Empty,
        string.Empty,
        options.ExpectedBaseSha,
        false,
        new VerificationEnvironment(RuntimeInformation.OSDescription, string.Empty),
        [],
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        VerificationVerdict.FAIL,
        []);

    private static async Task<CommandExecution> RunAsync(string name, string executable, IReadOnlyList<string> arguments, string root, string logsDirectory, ICollection<CommandEvidence> commands)
    {
        var execution = await CommandRunner.RunAsync(executable, arguments, root, Path.Combine(logsDirectory, $"{name}.log"));
        commands.Add(execution.Evidence with { LogPath = Path.GetRelativePath(root, execution.Evidence.LogPath) });
        return execution;
    }

    private static async Task<(CommandExecution Execution, string Value)> RunValueAsync(string name, string executable, IReadOnlyList<string> arguments, string root, string logsDirectory, ICollection<CommandEvidence> commands)
    {
        var execution = await RunAsync(name, executable, arguments, root, logsDirectory, commands);
        return (execution, OutputValue(execution));
    }

    private static Task<(CommandExecution Execution, string Value)> RunGitValueAsync(string name, IReadOnlyList<string> arguments, string gitExecutable, string root, string logsDirectory, ICollection<CommandEvidence> commands) =>
        RunValueAsync(name, gitExecutable, arguments, root, logsDirectory, commands);

    private static string OutputValue(CommandExecution execution)
    {
        if (execution.Evidence.ExitCode != 0)
        {
            return string.Empty;
        }

        var separator = $"[stdout]{Environment.NewLine}";
        var start = execution.Output.IndexOf(separator, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var outputStart = start + separator.Length;
        var end = execution.Output.IndexOf($"{Environment.NewLine}[stderr]", outputStart, StringComparison.Ordinal);
        return (end < 0 ? execution.Output[outputStart..] : execution.Output[outputStart..end]).Trim();
    }

    private static EvidenceStatus ToStatus(int exitCode, bool additionalCondition = true) => exitCode == 0 && additionalCondition ? EvidenceStatus.PASS : EvidenceStatus.FAIL;

    private static TestEvidence CreateTestEvidence(CommandExecution execution, string trxPath)
    {
        try
        {
            var counts = TrxCounterParser.Parse(trxPath);
            return new TestEvidence(ToStatus(execution.Evidence.ExitCode, counts.Failed == 0), counts.Passed, counts.Failed, counts.Skipped, counts.Total, trxPath);
        }
        catch (Exception)
        {
            return new TestEvidence(EvidenceStatus.FAIL, 0, 0, 0, 0, null);
        }
    }

    private static async Task<Gate0Evidence> VerifyGate0Async(string repositoryRoot, string gitExecutable, string logsDirectory, ICollection<CommandEvidence> commands)
    {
        try
        {
            var baseline = Gate0BaselineLoader.Load(Path.Combine(repositoryRoot, "tools", "Tlaw.Verify", "Gate0", "gate0-baseline.json"));
            var baselineObjects = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var headObjects = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var objectRequests = baseline.Files
                .SelectMany(file => new[]
                {
                    new GitObjectRequest($"baseline:{file.Path}", $"{baseline.SourceSha}:{file.Path}", file.Path),
                    new GitObjectRequest($"head:{file.Path}", $"HEAD:{file.Path}", file.Path)
                })
                .ToArray();
            var reader = new GitObjectBatchReader();
            var objectRead = await reader.ReadAsync(gitExecutable, ["cat-file", "--batch"], repositoryRoot, Path.Combine(logsDirectory, "gate0-object-reader.log"), objectRequests);
            commands.Add(objectRead.Command with { LogPath = Path.GetRelativePath(repositoryRoot, objectRead.Command.LogPath) });
            foreach (var file in baseline.Files)
            {
                if (objectRead.Objects.TryGetValue($"baseline:{file.Path}", out var source))
                {
                    baselineObjects[file.Path] = source;
                }

                if (objectRead.Objects.TryGetValue($"head:{file.Path}", out var head))
                {
                    headObjects[file.Path] = head;
                }
            }

            var pathspecs = baseline.Files
                .Select(file => file.Path)
                .Concat(["docs", "data", "reviews", "scripts", "source"])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var committed = await ReadGate0ChangeSetAsync("gate0-committed", ["diff", "--name-only", $"{baseline.SourceSha}..HEAD", "--", ..pathspecs], gitExecutable, repositoryRoot, logsDirectory, commands);
            var staged = await ReadGate0ChangeSetAsync("gate0-staged", ["diff", "--cached", "--name-only", "--", ..pathspecs], gitExecutable, repositoryRoot, logsDirectory, commands);
            var unstaged = await ReadGate0ChangeSetAsync("gate0-unstaged", ["diff", "--name-only", "--", ..pathspecs], gitExecutable, repositoryRoot, logsDirectory, commands);
            var untracked = await ReadGate0ChangeSetAsync("gate0-untracked", ["ls-files", "--others", "--exclude-standard", "--", ..pathspecs], gitExecutable, repositoryRoot, logsDirectory, commands);
            return Gate0Verifier.Verify(baseline, new Gate0GitSnapshot(baselineObjects, headObjects, committed, staged, unstaged, untracked, objectRead.Evidence));
        }
        catch (Exception exception)
        {
            return new Gate0Evidence(EvidenceStatus.FAIL, string.Empty, string.Empty, [], [$"gate0-verifier:{exception.GetType().Name}"], [], [], [], []);
        }
    }

    private static async Task<Gate0ChangeSet> ReadGate0ChangeSetAsync(string name, IReadOnlyList<string> arguments, string gitExecutable, string repositoryRoot, string logsDirectory, ICollection<CommandEvidence> commands)
    {
        var execution = await RunAsync(name, gitExecutable, arguments, repositoryRoot, logsDirectory, commands);
        var paths = OutputValue(execution)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => path.Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        return new Gate0ChangeSet(paths, execution.Evidence.ExitCode == 0);
    }

    private static ArchitectureEvidence ExtractArchitecture(TestEvidence? tests)
    {
        if (tests?.TrxPath is null)
        {
            return new ArchitectureEvidence(EvidenceStatus.FAIL, []);
        }

        try
        {
            return ArchitectureEvidenceExtractor.FromTrx(tests.TrxPath);
        }
        catch (Exception)
        {
            return new ArchitectureEvidence(EvidenceStatus.FAIL, []);
        }
    }

    private static DomainDependenciesEvidence VerifyDomainDependencies(string repositoryRoot)
    {
        try
        {
            return DomainDependencyVerifier.Verify(Path.Combine(repositoryRoot, "src", "TheLogsAreWrong.Domain", "TheLogsAreWrong.Domain.csproj"));
        }
        catch (Exception)
        {
            return new DomainDependenciesEvidence(EvidenceStatus.FAIL, []);
        }
    }
}

public sealed record VerificationOptions(string? ExpectedHeadSha, string? ExpectedBaseSha, string? RepositoryRoot, string? ArtifactsDirectory, bool AllowDetachedHead)
{
    public static VerificationOptions Parse(IReadOnlyList<string> arguments)
    {
        string? expectedHead = null;
        string? expectedBase = null;
        string? repositoryRoot = null;
        string? artifactsDirectory = null;
        var allowDetachedHead = false;
        for (var index = 0; index < arguments.Count; index++)
        {
            var option = arguments[index];
            if (option == "--allow-detached-head")
            {
                allowDetachedHead = true;
                continue;
            }

            if (index + 1 >= arguments.Count)
            {
                throw new ArgumentException($"Missing value for '{option}'.");
            }

            var value = arguments[++index];
            switch (option)
            {
                case "--expected-head": expectedHead = value; break;
                case "--expected-base": expectedBase = value; break;
                case "--repository-root": repositoryRoot = Path.GetFullPath(value); break;
                case "--artifacts-directory": artifactsDirectory = Path.GetFullPath(value); break;
                default: throw new ArgumentException($"Unknown argument '{option}'.");
            }
        }

        return new VerificationOptions(expectedHead, expectedBase, repositoryRoot, artifactsDirectory, allowDetachedHead);
    }
}

public static class RepositoryRootFinder
{
    public static string? Find(string startDirectory)
    {
        for (var current = new DirectoryInfo(startDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
        }

        return null;
    }
}
