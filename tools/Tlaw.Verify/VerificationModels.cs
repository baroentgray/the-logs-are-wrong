using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tlaw.Verify;

public enum EvidenceStatus
{
    PASS,
    FAIL,
    MISSING,
    NOT_RUN
}

public enum VerificationVerdict
{
    PASS,
    FAIL
}

public sealed record VerificationEnvironment(string Os, string DotnetSdk);

public sealed record CommandEvidence(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    int ExitCode,
    string LogPath);

public sealed record CheckEvidence(EvidenceStatus Status);

public sealed record BuildEvidence(EvidenceStatus Status, int Warnings, int Errors);

public sealed record TestEvidence(EvidenceStatus Status, int Passed, int Failed, int Skipped, int Total, string? TrxPath);

public sealed record Gate0Evidence(EvidenceStatus Status, string BaselineId, string SourceSha, IReadOnlyList<string> CheckedFiles, IReadOnlyList<string> Mismatches);

public sealed record ArchitectureEvidence(EvidenceStatus Status, IReadOnlyList<string> Checks);

public sealed record DomainDependenciesEvidence(EvidenceStatus Status, IReadOnlyList<string> PackageReferences);

public sealed record VerificationReport(
    string Schema,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    string RepositoryRoot,
    string Branch,
    string ActualHeadSha,
    string ExpectedHeadSha,
    string ActualBaseSha,
    string? ExpectedBaseSha,
    bool CleanTree,
    VerificationEnvironment Environment,
    IReadOnlyList<CommandEvidence> Commands,
    CheckEvidence? Restore,
    BuildEvidence? Build,
    TestEvidence? Tests,
    CheckEvidence? DiffCheck,
    Gate0Evidence? Gate0,
    ArchitectureEvidence? Architecture,
    DomainDependenciesEvidence? DomainDependencies,
    VerificationVerdict Verdict,
    IReadOnlyList<string> FailureReasons);

public sealed record VerdictOutcome(VerificationVerdict Verdict, IReadOnlyList<string> FailureReasons);

public static class VerificationReportSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(VerificationReport report) => JsonSerializer.Serialize(report, Options);

    public static VerificationReport Deserialize(string json) => JsonSerializer.Deserialize<VerificationReport>(json, Options)
        ?? throw new InvalidDataException("Verification report JSON is empty.");
}

public static class VerificationVerdictEvaluator
{
    public static VerdictOutcome Evaluate(VerificationReport report)
    {
        var failures = new List<string>();

        Require(!string.IsNullOrWhiteSpace(report.RepositoryRoot), "repository root is missing", failures);
        Require(!string.IsNullOrWhiteSpace(report.Branch), "branch is missing", failures);
        Require(!string.IsNullOrWhiteSpace(report.ActualHeadSha), "actual head is missing", failures);
        Require(!string.IsNullOrWhiteSpace(report.ExpectedHeadSha), "expected head is missing", failures);
        Require(string.Equals(report.ActualHeadSha, report.ExpectedHeadSha, StringComparison.OrdinalIgnoreCase), "actual head does not equal expected head", failures);
        Require(report.CleanTree, "working tree is not clean", failures);

        if (report.ExpectedBaseSha is not null)
        {
            Require(string.Equals(report.ActualBaseSha, report.ExpectedBaseSha, StringComparison.OrdinalIgnoreCase), "actual base does not equal expected base", failures);
        }

        RequireCommand(report.Commands, "dotnet", "restore", failures);
        RequireCommand(report.Commands, "dotnet", "build", failures);
        RequireCommand(report.Commands, "dotnet", "test", failures);
        RequireCommand(report.Commands, "git", "diff", failures);
        RequireResult(report.Restore, "restore", failures);
        RequireResult(report.Build, "build", failures);
        RequireResult(report.Tests, "tests", failures);
        RequireResult(report.DiffCheck, "diff check", failures);
        RequireResult(report.Gate0, "Gate 0", failures);
        RequireResult(report.Architecture, "architecture", failures);
        RequireResult(report.DomainDependencies, "Domain dependencies", failures);

        if (report.Build is not null && report.Build.Errors > 0)
        {
            failures.Add("build reported errors");
        }

        if (report.Tests is not null)
        {
            Require(report.Tests.TrxPath is not null, "tests TRX path is missing", failures);
            Require(report.Tests.Total == report.Tests.Passed + report.Tests.Failed + report.Tests.Skipped, "tests counters are inconsistent", failures);
            Require(report.Tests.Failed == 0, "tests reported failures", failures);
        }

        if (report.Gate0 is not null)
        {
            Require(report.Gate0.Mismatches.Count == 0, "Gate 0 mismatches were found", failures);
        }

        if (report.DomainDependencies is not null)
        {
            Require(report.DomainDependencies.PackageReferences.Count == 0, "Domain PackageReference dependencies were found", failures);
        }

        return new VerdictOutcome(failures.Count == 0 ? VerificationVerdict.PASS : VerificationVerdict.FAIL, failures);
    }

    private static void RequireCommand(IReadOnlyList<CommandEvidence> commands, string executable, string firstArgument, ICollection<string> failures)
    {
        var command = commands.FirstOrDefault(candidate =>
            string.Equals(Path.GetFileNameWithoutExtension(candidate.Executable), executable, StringComparison.OrdinalIgnoreCase) &&
            candidate.Arguments.FirstOrDefault() is { } argument &&
            string.Equals(argument, firstArgument, StringComparison.OrdinalIgnoreCase));

        if (command is null)
        {
            failures.Add($"required command is missing: {executable} {firstArgument}");
        }
        else if (command.ExitCode != 0)
        {
            failures.Add($"command failed: {executable} {firstArgument}");
        }
    }

    private static void RequireResult(CheckEvidence? result, string name, ICollection<string> failures)
    {
        if (result is null)
        {
            failures.Add($"{name} result is missing");
        }
        else if (result.Status != EvidenceStatus.PASS)
        {
            failures.Add($"{name} result is {result.Status}");
        }
    }

    private static void RequireResult(BuildEvidence? result, string name, ICollection<string> failures) => RequireResult(result is null ? null : new CheckEvidence(result.Status), name, failures);
    private static void RequireResult(TestEvidence? result, string name, ICollection<string> failures) => RequireResult(result is null ? null : new CheckEvidence(result.Status), name, failures);
    private static void RequireResult(Gate0Evidence? result, string name, ICollection<string> failures) => RequireResult(result is null ? null : new CheckEvidence(result.Status), name, failures);
    private static void RequireResult(ArchitectureEvidence? result, string name, ICollection<string> failures) => RequireResult(result is null ? null : new CheckEvidence(result.Status), name, failures);
    private static void RequireResult(DomainDependenciesEvidence? result, string name, ICollection<string> failures) => RequireResult(result is null ? null : new CheckEvidence(result.Status), name, failures);

    private static void Require(bool condition, string failure, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(failure);
        }
    }
}
