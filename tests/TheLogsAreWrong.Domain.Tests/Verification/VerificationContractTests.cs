using System.Text.Json;
using Tlaw.Verify;

namespace TheLogsAreWrong.Domain.Tests.Verification;

public sealed class VerificationContractTests
{
    [Fact]
    public void Complete_passing_evidence_aggregates_to_pass()
    {
        var report = PassingReport();

        var outcome = VerificationVerdictEvaluator.Evaluate(report);

        Assert.Equal(VerificationVerdict.PASS, outcome.Verdict);
        Assert.Empty(outcome.FailureReasons);
    }

    [Fact]
    public void Missing_required_result_aggregates_to_fail()
    {
        var report = PassingReport() with { Tests = null };

        var outcome = VerificationVerdictEvaluator.Evaluate(report);

        Assert.Equal(VerificationVerdict.FAIL, outcome.Verdict);
        Assert.Contains(outcome.FailureReasons, reason => reason.Contains("tests", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Failed_command_aggregates_to_fail()
    {
        var report = PassingReport() with
        {
            Commands = [new CommandEvidence("dotnet", ["test"], "/repo", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1, "logs/test.log")]
        };

        var outcome = VerificationVerdictEvaluator.Evaluate(report);

        Assert.Equal(VerificationVerdict.FAIL, outcome.Verdict);
        Assert.Contains(outcome.FailureReasons, reason => reason.Contains("command", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Expected_head_mismatch_invalidates_evidence()
    {
        var report = PassingReport() with { ActualHeadSha = "bbbb", ExpectedHeadSha = "aaaa" };

        var outcome = VerificationVerdictEvaluator.Evaluate(report);

        Assert.Equal(VerificationVerdict.FAIL, outcome.Verdict);
        Assert.Contains(outcome.FailureReasons, reason => reason.Contains("expected head", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Trx_counter_parser_reads_structured_test_counts()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "VerificationFixtures", "sample.trx");

        var counts = TrxCounterParser.Parse(path);

        Assert.Equal(5, counts.Total);
        Assert.Equal(3, counts.Passed);
        Assert.Equal(1, counts.Failed);
        Assert.Equal(1, counts.Skipped);
    }

    [Fact]
    public void Build_output_parser_counts_warning_and_error_diagnostics()
    {
        const string log = "one.cs(1,1): warning CS0618: obsolete\n" +
                           "two.cs(2,1): warning CS0168: unused\n" +
                           "three.cs(3,1): error CS1002: expected\n";

        var counts = BuildDiagnosticParser.Parse(log);

        Assert.Equal(2, counts.Warnings);
        Assert.Equal(1, counts.Errors);
    }

    [Fact]
    public void Gate_baseline_success_and_mismatch_are_detected()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var file = Path.Combine(root, "docs", "contract.md");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "approved");
            var baseline = new Gate0Baseline("fixture", "4056157", [new Gate0FileHash("docs/contract.md", Sha256Hasher.HashFile(file))]);

            Assert.Equal(EvidenceStatus.PASS, Gate0Verifier.Verify(root, baseline).Status);

            File.WriteAllText(file, "changed");
            var mismatch = Gate0Verifier.Verify(root, baseline);

            Assert.Equal(EvidenceStatus.FAIL, mismatch.Status);
            Assert.Equal(["docs/contract.md"], mismatch.Mismatches);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Domain_package_reference_detector_reports_runtime_dependencies()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var project = Path.Combine(root, "TheLogsAreWrong.Domain.csproj");
            File.WriteAllText(project, "<Project><ItemGroup><PackageReference Include=\"Example.Runtime\" Version=\"1.0.0\" /></ItemGroup></Project>");

            var dependency = DomainDependencyVerifier.Verify(project);

            Assert.Equal(EvidenceStatus.FAIL, dependency.Status);
            Assert.Equal(["Example.Runtime"], dependency.PackageReferences);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Report_serialization_matches_the_known_fixture_contract()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "VerificationFixtures", "known-report.json");
        using var expected = JsonDocument.Parse(File.ReadAllText(fixturePath));
        using var actual = JsonDocument.Parse(VerificationReportSerializer.Serialize(PassingReport()));

        Assert.Equal(expected.RootElement.GetProperty("schema").GetString(), actual.RootElement.GetProperty("schema").GetString());
        Assert.Equal(expected.RootElement.GetProperty("verdict").GetString(), actual.RootElement.GetProperty("verdict").GetString());
        Assert.Equal(expected.RootElement.GetProperty("tests").GetProperty("total").GetInt32(), actual.RootElement.GetProperty("tests").GetProperty("total").GetInt32());
        Assert.Equal(expected.RootElement.GetProperty("build").GetProperty("warnings").GetInt32(), actual.RootElement.GetProperty("build").GetProperty("warnings").GetInt32());
    }

    private static VerificationReport PassingReport() => new(
        "tlaw.verification/v1",
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        "/repo",
        "main",
        "aaaa",
        "aaaa",
        "base",
        null,
        true,
        new VerificationEnvironment("test", "10.0.103"),
        [
            new CommandEvidence("dotnet", ["restore"], "/repo", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 0, "logs/restore.log"),
            new CommandEvidence("dotnet", ["build"], "/repo", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 0, "logs/build.log"),
            new CommandEvidence("dotnet", ["test"], "/repo", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 0, "logs/test.log"),
            new CommandEvidence("git", ["diff", "--check"], "/repo", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 0, "logs/diff.log")
        ],
        new CheckEvidence(EvidenceStatus.PASS),
        new BuildEvidence(EvidenceStatus.PASS, 0, 0),
        new TestEvidence(EvidenceStatus.PASS, 3, 0, 0, 3, "test.trx"),
        new CheckEvidence(EvidenceStatus.PASS),
        new Gate0Evidence(EvidenceStatus.PASS, "fixture", "4056157", ["docs/contract.md"], []),
        new ArchitectureEvidence(EvidenceStatus.PASS, ["ArchitectureGuardTests: PASS"]),
        new DomainDependenciesEvidence(EvidenceStatus.PASS, []),
        VerificationVerdict.PASS,
        []);

    private static string CreateTemporaryRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "tlaw-verify-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
