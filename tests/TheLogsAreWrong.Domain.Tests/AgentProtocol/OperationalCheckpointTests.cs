using System.Text.Json;
using Tlaw.AgentProtocol;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

[Trait("Scope", "TLAW-AUTO-009")]
public sealed class OperationalCheckpointTests
{
    [Fact]
    public void New_operational_schemas_validate_their_positive_fixtures()
    {
        foreach (var fixture in new[] { "current-state.valid.yaml", "active-runs.empty.valid.yaml", "active-runs.ordered.valid.yaml", "chat-handoff.post-merge.valid.yaml" })
        {
            var result = PacketValidator.Validate(File.ReadAllText(Path.Combine(Examples, fixture)), Registry);
            Assert.True(result.IsValid, $"{fixture}: {string.Join(" | ", result.Diagnostics.Select(item => item.Code))}");
        }
    }

    [Theory]
    [InlineData("invalid/current-state-missing-schema.yaml")]
    [InlineData("invalid/current-state-unknown-version.yaml")]
    [InlineData("invalid/current-state-malformed-sha.yaml")]
    [InlineData("invalid/current-state-unknown-stage.yaml")]
    [InlineData("invalid/current-state-missing-verification.yaml")]
    [InlineData("invalid/active-runs-unknown-status.yaml")]
    [InlineData("invalid/active-runs-unknown-kind.yaml")]
    [InlineData("invalid/active-runs-duplicate-identity.yaml")]
    [InlineData("invalid/active-runs-unstable-order.yaml")]
    [InlineData("invalid/active-runs-unsafe-anchor.yaml")]
    [InlineData("invalid/active-runs-unsafe-tag.yaml")]
    public void Invalid_operational_fixtures_fail_visibly(string fixture)
    {
        var result = PacketValidator.Validate(File.ReadAllText(Path.Combine(Examples, fixture)), Registry);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Active_run_identity_is_unique_and_stably_ordered_when_operations_are_present()
    {
        var duplicate = PacketValidator.Validate(File.ReadAllText(Path.Combine(Examples, "invalid", "active-runs-duplicate-identity.yaml")), Registry);
        var unordered = PacketValidator.Validate(File.ReadAllText(Path.Combine(Examples, "invalid", "active-runs-unstable-order.yaml")), Registry);

        Assert.Contains(duplicate.Diagnostics, item => item.Code == "TLAW-PKT-041");
        Assert.Contains(unordered.Diagnostics, item => item.Code == "TLAW-PKT-042");
    }

    [Fact]
    public void Active_runs_accept_unfinished_verification_kinds_and_reject_unknown_kinds()
    {
        var orderedFixture = File.ReadAllText(Path.Combine(Examples, "active-runs.ordered.valid.yaml"));
        var ordered = PacketValidator.Validate(orderedFixture, Registry);
        var unknownKind = PacketValidator.Validate(File.ReadAllText(Path.Combine(Examples, "invalid", "active-runs-unknown-kind.yaml")), Registry);

        Assert.Contains("kind: verification", orderedFixture, StringComparison.Ordinal);
        Assert.Contains("kind: post_merge_verification", orderedFixture, StringComparison.Ordinal);
        Assert.True(ordered.IsValid, string.Join(" | ", ordered.Diagnostics.Select(item => item.Code)));
        Assert.False(unknownKind.IsValid);
        Assert.Contains(unknownKind.Diagnostics, item => item.Code == "TLAW-PKT-016");
    }

    [Fact]
    public void Current_operational_policy_has_no_status_cache_or_alternative_reviewer()
    {
        var context = File.ReadAllText(Path.Combine(AgentRoot, "CONTEXT.md"));
        var automation = File.ReadAllText(Path.Combine(AgentRoot, "AUTOMATION.md"));

        Assert.DoesNotContain("STATUS.md", context, StringComparison.Ordinal);
        Assert.Contains("CURRENT_STATE.md", context, StringComparison.Ordinal);
        Assert.Contains("single non-authoritative volatile current-state cache", context, StringComparison.Ordinal);
        Assert.Contains("Codex is the implementation executor.", automation, StringComparison.Ordinal);
        Assert.Contains("Grok is the sole authoritative reviewer", automation, StringComparison.Ordinal);
        Assert.Contains("no automatic fallback or return to Claude", automation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Claude plans/reviews", automation, StringComparison.Ordinal);
        Assert.DoesNotContain("alternative review", automation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Current_state_replaces_the_old_status_cache_and_all_operational_documents_have_valid_front_matter()
    {
        Assert.True(File.Exists(Path.Combine(AgentRoot, "CURRENT_STATE.md")));
        Assert.False(File.Exists(Path.Combine(AgentRoot, "STATUS.md")));

        foreach (var document in new[] { "CURRENT_STATE.md", "ACTIVE_RUNS.md", "HANDOFF.md" })
        {
            var result = OperationalDocumentValidator.ValidateFrontMatter(File.ReadAllText(Path.Combine(AgentRoot, document)), Registry);
            Assert.True(result.IsValid, $"{document}: {string.Join(" | ", result.Diagnostics.Select(item => item.Code))}");
        }
    }

    [Fact]
    public void Prepared_target_checkpoint_is_explicit_about_tlaw015_completion_and_does_not_fabricate_an_active_gameplay_task()
    {
        var current = File.ReadAllText(Path.Combine(AgentRoot, "CURRENT_STATE.md"));
        var activeRuns = File.ReadAllText(Path.Combine(AgentRoot, "ACTIVE_RUNS.md"));
        var handoff = File.ReadAllText(Path.Combine(AgentRoot, "HANDOFF.md"));

        Assert.Contains("snapshot_kind: prepared_target", current, StringComparison.Ordinal);
        Assert.Contains("e13b439d2929b969d179b012b2cfee05f66467c5", current, StringComparison.Ordinal);
        Assert.Contains("TLAW-015", current, StringComparison.Ordinal);
        Assert.Contains("active_task: null", current, StringComparison.Ordinal);
        Assert.Contains("runs: []", activeRuns, StringComparison.Ordinal);
        Assert.Contains("TLAW-013", handoff, StringComparison.Ordinal);
        Assert.Contains("TLAW-014", handoff, StringComparison.Ordinal);
        Assert.Contains("TLAW-015", handoff, StringComparison.Ordinal);
        Assert.DoesNotContain("TLAW-016", current, StringComparison.Ordinal);
        Assert.DoesNotContain("TLAW-016", activeRuns, StringComparison.Ordinal);
        Assert.DoesNotContain("TLAW-016", handoff, StringComparison.Ordinal);
    }

    [Fact]
    public void A_new_control_chat_can_reconstruct_the_verified_main_policy_and_next_action_without_old_chat_history()
    {
        var index = File.ReadAllText(Path.Combine(AgentRoot, "CONTEXT_INDEX.md"));
        var handoff = File.ReadAllText(Path.Combine(AgentRoot, "HANDOFF.md"));

        Assert.Contains("live GitHub, Linear, and CI", index, StringComparison.Ordinal);
        Assert.Contains("old chat", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Grok", handoff, StringComparison.Ordinal);
        Assert.Contains("e13b439d2929b969d179b012b2cfee05f66467c5", handoff, StringComparison.Ordinal);
        Assert.Contains("validate GitHub, Linear, and CI before any write", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("select or define the next task", handoff, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Context_manifest_lists_the_new_non_authoritative_caches_and_no_longer_lists_status()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(AgentRoot, "context-manifest.json")));
        var startup = manifest.RootElement.GetProperty("startup_order").EnumerateArray().Select(item => item.GetString()).ToArray();
        var sources = manifest.RootElement.GetProperty("sources").EnumerateArray().Select(item => item.GetProperty("path").GetString()).ToArray();

        Assert.DoesNotContain("docs/agent/STATUS.md", startup);
        Assert.DoesNotContain("docs/agent/STATUS.md", sources);
        Assert.Contains("docs/agent/CURRENT_STATE.md", startup);
        Assert.Contains("docs/agent/ACTIVE_RUNS.md", startup);
        Assert.Contains("docs/agent/HANDOFF.md", startup);
        Assert.All(startup.Concat(sources).Where(path => path is not null), path => Assert.True(File.Exists(Path.Combine(RepositoryRoot, path!)), path));
    }

    private static PacketSchemaRegistry Registry => PacketSchemaRegistry.Load(Path.Combine(AgentRoot, "schemas"));
    private static string RepositoryRoot => FindRepositoryRoot();
    private static string AgentRoot => Path.Combine(RepositoryRoot, "docs", "agent");
    private static string Examples => Path.Combine(AgentRoot, "schemas", "examples");

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
}
