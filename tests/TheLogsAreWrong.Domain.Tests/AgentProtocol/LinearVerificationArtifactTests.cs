using Tlaw.Dispatcher;
using Tlaw.Verify;
using static TheLogsAreWrong.Domain.Tests.AgentProtocol.LinearTransitionTestSupport;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

/// <summary>
/// The merge proof accepts only the real <c>tlaw.verification/v1</c> artifact produced by <c>Tlaw.Verify</c>,
/// validated through the repository's own model and verdict evaluator (never an invented flat-boolean schema).
/// </summary>
public sealed class LinearVerificationArtifactTests
{
    private const string MergeSha = "cafebabecafebabecafebabecafebabecafebabe";

    [Fact]
    public void Genuine_pass_artifact_reaches_the_git_proof_stage()
    {
        // No exception means the strict parser accepted the real artifact shape and would proceed to Git ancestry.
        RepositoryVerificationArtifact.Validate(SerializeReport(PassReport(MergeSha)), MergeSha);
    }

    [Fact]
    public void Genuine_detached_ci_style_pass_artifact_reaches_the_git_proof_stage()
    {
        RepositoryVerificationArtifact.Validate(SerializeReport(DetachedPassReport()), MergeSha);
    }

    [Fact]
    public void Real_serializer_round_trip_is_accepted()
    {
        // Build through the repository's actual serializer, exactly as Tlaw.Verify writes verification.json.
        var json = VerificationReportSerializer.Serialize(PassReport(MergeSha));
        RepositoryVerificationArtifact.Validate(new System.Text.UTF8Encoding(false).GetBytes(json), MergeSha);
    }

    [Fact]
    public void Expected_head_mismatch_is_rejected()
    {
        var report = DetachedPassReport() with { ExpectedHeadSha = new string('0', 39) + "1" };
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(report), MergeSha));
    }

    [Fact]
    public void Actual_head_mismatch_is_rejected()
    {
        var report = DetachedPassReport() with { ActualHeadSha = new string('0', 39) + "1" };
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(report), MergeSha));
    }

    [Fact]
    public void Head_not_equal_to_merge_sha_is_rejected()
    {
        var other = new string('a', 40);
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(PassReport(other)), MergeSha));
    }

    [Fact]
    public void Fail_verdict_is_rejected()
    {
        var report = PassReport(MergeSha) with { Verdict = VerificationVerdict.FAIL };
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(report), MergeSha));
    }

    [Fact]
    public void Non_empty_failure_reasons_are_rejected()
    {
        var report = DetachedPassReport() with { FailureReasons = ["something failed"] };
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(report), MergeSha));
    }

    [Fact]
    public void Failed_nested_build_is_rejected()
    {
        var report = DetachedPassReport() with { Build = new BuildEvidence(EvidenceStatus.FAIL, 0, 3) };
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(report), MergeSha));
    }

    [Fact]
    public void Non_detached_artifact_with_missing_branch_is_rejected()
    {
        var report = PassReport(MergeSha) with { Branch = null, IsDetachedHead = false };
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(report), MergeSha));
    }

    [Fact]
    public void Detached_artifact_with_a_branch_is_rejected_as_a_contradictory_shape()
    {
        var report = DetachedPassReport() with { Branch = "task/BAR-41-linear-doctor" };
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(report), MergeSha));
    }

    [Fact]
    public void Failed_nested_tests_are_rejected()
    {
        var report = PassReport(MergeSha) with { Tests = new TestEvidence(EvidenceStatus.FAIL, 588, 1, 0, 589, "verification.trx") };
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(report), MergeSha));
    }

    [Fact]
    public void Failed_gate0_is_rejected()
    {
        var report = PassReport(MergeSha) with { Gate0 = new Gate0Evidence(EvidenceStatus.FAIL, "b", "s", ["AGENTS.md"], ["AGENTS.md"], [], [], [], [], new GitObjectReaderEvidence("git-cat-file-batch", 1, 1, 1, EvidenceStatus.PASS, 0, "l", [])) };
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(report), MergeSha));
    }

    [Fact]
    public void Failed_canonical_object_reader_is_rejected()
    {
        var reader = new GitObjectReaderEvidence("git-cat-file-batch", 1, 52, 40, EvidenceStatus.FAIL, 1, "l", [new GitObjectReaderFailure(GitObjectReaderFailureCategory.NonZeroExit, "k", "p", "boom")]);
        var report = PassReport(MergeSha) with { Gate0 = new Gate0Evidence(EvidenceStatus.PASS, "b", "s", ["AGENTS.md"], [], [], [], [], [], reader) };
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(report), MergeSha));
    }

    [Fact]
    public void Failed_architecture_is_rejected()
    {
        var report = PassReport(MergeSha) with { Architecture = new ArchitectureEvidence(EvidenceStatus.FAIL, ["Architecture: Failed"]) };
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(report), MergeSha));
    }

    [Fact]
    public void Failed_domain_dependencies_are_rejected()
    {
        var report = PassReport(MergeSha) with { DomainDependencies = new DomainDependenciesEvidence(EvidenceStatus.FAIL, ["YamlDotNet"]) };
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(report), MergeSha));
    }

    [Fact]
    public void Dirty_tree_is_rejected()
    {
        var report = PassReport(MergeSha) with { CleanTree = false };
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(report), MergeSha));
    }

    [Fact]
    public void Malformed_artifact_is_rejected()
    {
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(new System.Text.UTF8Encoding(false).GetBytes("not json at all"), MergeSha));
    }

    [Fact]
    public void Duplicate_top_level_property_is_rejected()
    {
        var json = VerificationReportSerializer.Serialize(PassReport(MergeSha))
            .Replace("\"cleanTree\": true,", "\"cleanTree\": true,\n  \"cleanTree\": true,", StringComparison.Ordinal);
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(new System.Text.UTF8Encoding(false).GetBytes(json), MergeSha));
    }

    [Fact]
    public void Unknown_top_level_property_is_rejected()
    {
        var json = VerificationReportSerializer.Serialize(PassReport(MergeSha))
            .Replace("\"cleanTree\": true,", "\"cleanTree\": true,\n  \"unexpected\": 1,", StringComparison.Ordinal);
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(new System.Text.UTF8Encoding(false).GetBytes(json), MergeSha));
    }

    [Fact]
    public void Wrong_schema_is_rejected()
    {
        var report = PassReport(MergeSha) with { Schema = "tlaw.verification/v2" };
        Assert.Throws<LinearCommandException>(() => RepositoryVerificationArtifact.Validate(SerializeReport(report), MergeSha));
    }

    private static VerificationReport DetachedPassReport() => PassReport(MergeSha) with { Branch = null, IsDetachedHead = true };
}
