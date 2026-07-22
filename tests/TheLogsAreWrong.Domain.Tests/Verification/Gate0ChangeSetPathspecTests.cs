using System.Text;
using Tlaw.Verify;

namespace TheLogsAreWrong.Domain.Tests.Verification;

public sealed class Gate0ChangeSetPathspecTests
{
    [Fact]
    public void Docs_agent_subtree_is_ignored_for_committed_staged_unstaged_and_untracked_change_sets()
    {
        var committed = Gate0ChangeSetPathspecs.CreateChangeSet(["docs/agent/CONTEXT.md"], succeeded: true);
        var staged = Gate0ChangeSetPathspecs.CreateChangeSet(["docs\\agent\\STATUS.md"], succeeded: true);
        var unstaged = Gate0ChangeSetPathspecs.CreateChangeSet(["docs/agent/schemas/task.schema.json"], succeeded: true);
        var untracked = Gate0ChangeSetPathspecs.CreateChangeSet(["docs/agent/schemas/examples/task.valid.yaml"], succeeded: true);

        Assert.Empty(committed.Paths);
        Assert.Empty(staged.Paths);
        Assert.Empty(unstaged.Paths);
        Assert.Empty(untracked.Paths);
        Assert.Equal(EvidenceStatus.PASS, VerifyGate(committed, staged, unstaged, untracked).Status);
    }

    [Fact]
    public void Docs_agent_exclusion_is_exact_anchored_and_keeps_all_sibling_docs_paths_protected()
    {
        var baseline = Baseline();
        var pathspecs = Gate0ChangeSetPathspecs.Build(baseline);
        var changeSet = Gate0ChangeSetPathspecs.CreateChangeSet(
        [
            "docs/agent/CONTEXT.md",
            "docs/agent",
            "docs/agentx/CONTEXT.md",
            "docs/agents/CONTEXT.md",
            "docs/other/CONTEXT.md"
        ],
        succeeded: true);

        Assert.Contains("docs", pathspecs);
        Assert.Contains(Gate0ChangeSetPathspecs.DocsAgentExclusionPathspec, pathspecs);
        Assert.DoesNotContain("docs/agent", pathspecs);
        Assert.Equal(
            ["docs/agent", "docs/agents/CONTEXT.md", "docs/agentx/CONTEXT.md", "docs/other/CONTEXT.md"],
            changeSet.Paths);
        Assert.Contains("committed:docs/agentx/CONTEXT.md", VerifyGate(changeSet).Mismatches);
        Assert.Contains("committed:docs/other/CONTEXT.md", VerifyGate(changeSet).Mismatches);
    }

    [Fact]
    public void Frozen_docs_modification_deletion_addition_and_rename_evidence_remain_protected()
    {
        var content = Encoding.UTF8.GetBytes("approved\n");
        var baseline = Baseline(content);
        var changeSet = Gate0ChangeSetPathspecs.CreateChangeSet(
        [
            "docs/frozen.md",
            "docs/added.md",
            "docs/renamed.md"
        ],
        succeeded: true);
        var deletedSnapshot = new Gate0GitSnapshot(
            new Dictionary<string, byte[]> { ["docs/frozen.md"] = content },
            new Dictionary<string, byte[]>(),
            changeSet,
            new Gate0ChangeSet([], true),
            new Gate0ChangeSet([], true),
            new Gate0ChangeSet([], true));

        var gate = Gate0Verifier.Verify(baseline, deletedSnapshot);

        Assert.Equal(EvidenceStatus.FAIL, gate.Status);
        Assert.Contains("head-object:docs/frozen.md", gate.Mismatches);
        Assert.Contains("committed:docs/frozen.md", gate.Mismatches);
        Assert.Contains("committed:docs/added.md", gate.Mismatches);
        Assert.Contains("committed:docs/renamed.md", gate.Mismatches);
    }

    [Fact]
    public void Building_change_set_pathspecs_does_not_change_the_baseline_or_frozen_hashes()
    {
        var content = Encoding.UTF8.GetBytes("approved\n");
        var baseline = Baseline(content);
        var expectedHash = baseline.Files.Single().Sha256;

        _ = Gate0ChangeSetPathspecs.Build(baseline);
        var gate = VerifyGate();

        Assert.Equal(expectedHash, baseline.Files.Single().Sha256);
        Assert.Equal(Sha256Hasher.HashCanonicalGitObject(content), expectedHash);
        Assert.Equal(EvidenceStatus.PASS, gate.Status);
    }

    [Fact]
    public void Failed_change_set_and_reader_infrastructure_failure_remain_fail_closed()
    {
        var content = Encoding.UTF8.GetBytes("approved\n");
        var reader = new GitObjectReaderEvidence(
            "git-cat-file-batch", 1, 2, 0, EvidenceStatus.FAIL, -1073741819, "logs/gate0-object-reader.log",
            [new GitObjectReaderFailure(GitObjectReaderFailureCategory.PrematureProcessExit, "head:docs/frozen.md", "docs/frozen.md", "fixture")]);
        var snapshot = new Gate0GitSnapshot(
            new Dictionary<string, byte[]> { ["docs/frozen.md"] = content },
            new Dictionary<string, byte[]> { ["docs/frozen.md"] = content },
            Gate0ChangeSetPathspecs.CreateChangeSet([], succeeded: false),
            new Gate0ChangeSet([], true),
            new Gate0ChangeSet([], true),
            new Gate0ChangeSet([], true),
            reader);

        var gate = Gate0Verifier.Verify(Baseline(content), snapshot);

        Assert.Equal(EvidenceStatus.FAIL, gate.Status);
        Assert.Contains("committed-check-failed", gate.Mismatches);
        Assert.Contains("git-object-reader:PrematureProcessExit:docs/frozen.md", gate.Mismatches);
    }

    private static Gate0Evidence VerifyGate(
        Gate0ChangeSet? committed = null,
        Gate0ChangeSet? staged = null,
        Gate0ChangeSet? unstaged = null,
        Gate0ChangeSet? untracked = null)
    {
        var content = Encoding.UTF8.GetBytes("approved\n");
        return Gate0Verifier.Verify(
            Baseline(content),
            new Gate0GitSnapshot(
                new Dictionary<string, byte[]> { ["docs/frozen.md"] = content },
                new Dictionary<string, byte[]> { ["docs/frozen.md"] = content },
                committed ?? new Gate0ChangeSet([], true),
                staged ?? new Gate0ChangeSet([], true),
                unstaged ?? new Gate0ChangeSet([], true),
                untracked ?? new Gate0ChangeSet([], true)));
    }

    private static Gate0Baseline Baseline(byte[]? content = null)
    {
        var bytes = content ?? Encoding.UTF8.GetBytes("approved\n");
        return new Gate0Baseline("fixture", "base", [new Gate0FileHash("docs/frozen.md", Sha256Hasher.HashCanonicalGitObject(bytes))]);
    }
}
