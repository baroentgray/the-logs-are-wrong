namespace Tlaw.Verify;

public static class MarkdownReportRenderer
{
    public static string Render(VerificationReport report) => string.Join(Environment.NewLine,
    [
        "# TLAW verification evidence",
        string.Empty,
        $"Verdict: {report.Verdict}",
        string.Empty,
        "| Evidence | Status |",
        "| --- | --- |",
        $"| Restore | {report.Restore?.Status} |",
        $"| Build | {report.Build?.Status} (warnings: {report.Build?.Warnings}, errors: {report.Build?.Errors}) |",
        $"| Tests | {report.Tests?.Status} (passed: {report.Tests?.Passed}, failed: {report.Tests?.Failed}, skipped: {report.Tests?.Skipped}, total: {report.Tests?.Total}) |",
        $"| Diff check | {report.DiffCheck?.Status} |",
        $"| Gate 0 | {report.Gate0?.Status} |",
        $"| Architecture | {report.Architecture?.Status} |",
        $"| Domain dependencies | {report.DomainDependencies?.Status} |",
        string.Empty,
        $"Repository: {report.RepositoryRoot}",
        $"Branch: {report.Branch ?? "(detached)"}",
        $"Detached HEAD: {report.IsDetachedHead}",
        $"Actual head: {report.ActualHeadSha}",
        $"Expected head: {report.ExpectedHeadSha}",
        $"Actual base: {report.ActualBaseSha}",
        $"Expected base: {report.ExpectedBaseSha ?? "(not supplied)"}",
        $"Clean tree: {report.CleanTree}",
        $"TRX: {report.Tests?.TrxPath ?? "(missing)"}",
        string.Empty,
        "## Failure reasons",
        report.FailureReasons.Count == 0 ? "None" : string.Join(Environment.NewLine, report.FailureReasons.Select(reason => $"- {reason}")),
        string.Empty
    ]);
}
