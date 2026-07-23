using System.Text.Json;
using Tlaw.AgentProtocol;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class AgentProtocolContractTests
{
    [Fact]
    public void Every_registered_schema_validates_its_positive_fixture()
    {
        var registry = PacketSchemaRegistry.Load(SchemaRoot);

        foreach (var fixture in PositiveFixturePaths)
        {
            var result = PacketValidator.Validate(File.ReadAllText(Path.Combine(ExamplesRoot, fixture)), registry);

            Assert.True(result.IsValid, $"{fixture}: {string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Message))}");
        }
    }

    [Theory]
    [InlineData("invalid/malformed.yaml")]
    [InlineData("invalid/missing-schema.yaml")]
    [InlineData("invalid/unknown-schema.yaml")]
    [InlineData("invalid/unsafe-tag.yaml")]
    [InlineData("invalid/anchor.yaml")]
    [InlineData("invalid/duplicate-key.yaml")]
    [InlineData("invalid/missing-evidence.yaml")]
    [InlineData("invalid/human-pause-missing-options.yaml")]
    [InlineData("invalid/anchor-flow-mapping.yaml")]
    [InlineData("invalid/anchor-block-mapping.yaml")]
    [InlineData("invalid/anchor-flow-sequence.yaml")]
    [InlineData("invalid/tag-flow-mapping.yaml")]
    [InlineData("invalid/tag-block-sequence.yaml")]
    [InlineData("invalid/alias-after-anchored-collection.yaml")]
    [InlineData("invalid/merge-key-anchored-mapping.yaml")]
    [InlineData("invalid/malformed-nested-after-anchor.yaml")]
    [InlineData("invalid/review-missing-reviewed-head.yaml")]
    [InlineData("invalid/review-short-reviewed-head.yaml")]
    [InlineData("invalid/review-long-reviewed-head.yaml")]
    [InlineData("invalid/review-nonhex-reviewed-head.yaml")]
    [InlineData("invalid/review-extra-field.yaml")]
    [InlineData("invalid/invalid-closed-enum.yaml")]
    [InlineData("invalid/task-missing-main-sha.yaml")]
    [InlineData("invalid/task-short-main-sha.yaml")]
    [InlineData("invalid/handoff-missing-main-sha.yaml")]
    [InlineData("invalid/handoff-short-main-sha.yaml")]
    [InlineData("invalid/handoff-missing-next-action.yaml")]
    [InlineData("invalid/over-limit-human-summary.yaml")]
    [InlineData("invalid/task-v2-nonhex-base-sha.yaml")]
    [InlineData("invalid/task-v2-missing-source.yaml")]
    [InlineData("invalid/task-v2-missing-policy.yaml")]
    [InlineData("invalid/task-v2-unknown-field.yaml")]
    [InlineData("invalid/task-v2-local-implementation.yaml")]
    [InlineData("invalid/task-v2-grok-implementation.yaml")]
    [InlineData("invalid/task-v2-claimed-by-ineligible.yaml")]
    [InlineData("invalid/task-v2-nonutc-claim-timestamp.yaml")]
    [InlineData("invalid/task-v2-reversed-claim-timestamps.yaml")]
    public void Invalid_protocol_fixtures_fail_visibly(string fixture)
    {
        var result = PacketValidator.Validate(File.ReadAllText(Path.Combine(ExamplesRoot, fixture)), PacketSchemaRegistry.Load(SchemaRoot));

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Theory]
    [InlineData("invalid/anchor-flow-mapping.yaml", "TLAW-PKT-008")]
    [InlineData("invalid/anchor-block-mapping.yaml", "TLAW-PKT-008")]
    [InlineData("invalid/anchor-flow-sequence.yaml", "TLAW-PKT-008")]
    [InlineData("invalid/tag-flow-mapping.yaml", "TLAW-PKT-009")]
    [InlineData("invalid/tag-block-sequence.yaml", "TLAW-PKT-009")]
    [InlineData("invalid/alias-after-anchored-collection.yaml", "TLAW-PKT-007")]
    [InlineData("invalid/merge-key-anchored-mapping.yaml", "TLAW-PKT-010")]
    [InlineData("invalid/malformed-nested-after-anchor.yaml", "TLAW-PKT-006")]
    public void Rejected_collection_constructs_fail_with_a_deterministic_protocol_diagnostic(string fixture, string expectedCode)
    {
        var result = PacketValidator.Validate(File.ReadAllText(Path.Combine(ExamplesRoot, fixture)), PacketSchemaRegistry.Load(SchemaRoot));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
    }

    [Theory]
    [InlineData("invalid/review-missing-reviewed-head.yaml", "TLAW-PKT-020")]
    [InlineData("invalid/review-short-reviewed-head.yaml", "TLAW-PKT-017")]
    [InlineData("invalid/review-long-reviewed-head.yaml", "TLAW-PKT-018")]
    [InlineData("invalid/review-nonhex-reviewed-head.yaml", "TLAW-PKT-026")]
    [InlineData("invalid/review-extra-field.yaml", "TLAW-PKT-021")]
    [InlineData("invalid/invalid-closed-enum.yaml", "TLAW-PKT-016")]
    [InlineData("invalid/task-missing-main-sha.yaml", "TLAW-PKT-020")]
    [InlineData("invalid/task-short-main-sha.yaml", "TLAW-PKT-017")]
    [InlineData("invalid/handoff-missing-main-sha.yaml", "TLAW-PKT-020")]
    [InlineData("invalid/handoff-short-main-sha.yaml", "TLAW-PKT-017")]
    [InlineData("invalid/handoff-missing-next-action.yaml", "TLAW-PKT-020")]
    [InlineData("invalid/over-limit-human-summary.yaml", "TLAW-PKT-022")]
    [InlineData("invalid/task-v2-nonhex-base-sha.yaml", "TLAW-PKT-026")]
    [InlineData("invalid/task-v2-missing-source.yaml", "TLAW-PKT-020")]
    [InlineData("invalid/task-v2-missing-policy.yaml", "TLAW-PKT-020")]
    [InlineData("invalid/task-v2-unknown-field.yaml", "TLAW-PKT-021")]
    [InlineData("invalid/task-v2-local-implementation.yaml", "TLAW-PKT-029")]
    [InlineData("invalid/task-v2-grok-implementation.yaml", "TLAW-PKT-029")]
    [InlineData("invalid/task-v2-claimed-by-ineligible.yaml", "TLAW-PKT-032")]
    [InlineData("invalid/task-v2-nonutc-claim-timestamp.yaml", "TLAW-PKT-033")]
    [InlineData("invalid/task-v2-reversed-claim-timestamps.yaml", "TLAW-PKT-034")]
    public void Closed_protocol_contracts_reject_the_required_boundary_cases(string fixture, string expectedCode)
    {
        var result = PacketValidator.Validate(File.ReadAllText(Path.Combine(ExamplesRoot, fixture)), PacketSchemaRegistry.Load(SchemaRoot));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
    }

    [Fact]
    public void Review_packets_preserve_the_exact_reviewed_head_evidence()
    {
        var result = PacketValidator.Validate(File.ReadAllText(Path.Combine(ExamplesRoot, "review.valid.yaml")), PacketSchemaRegistry.Load(SchemaRoot));

        Assert.True(result.IsValid);
        Assert.Equal("ef51ab1750164361f69fe3cfb9a32e0a0da9c2e3", result.Packet!.ReviewedHead);
    }

    [Fact]
    public void Result_projection_is_concise_and_needs_no_free_form_trailing_report()
    {
        var result = PacketValidator.Validate(File.ReadAllText(Path.Combine(ExamplesRoot, "result.valid.yaml")), PacketSchemaRegistry.Load(SchemaRoot));

        Assert.True(result.IsValid);
        var projection = ResultProjector.Project(result.Packet!);

        Assert.Contains("SUCCESS", projection);
        Assert.Contains("Gate 0 unchanged", projection);
        Assert.True(NonEmptyLines(projection).Count <= 6, projection);
    }

    [Fact]
    public void Required_human_decision_projects_only_the_pause_payload()
    {
        const string packet = """
            schema: tlaw.agent-result/v1
            task_id: BAR-31
            status: blocked
            human_summary: |
              A human decision is required.
            evidence:
              - kind: source
                reference: https://linear.app/baronet/issue/BAR-31
            human:
              required: true
              question: Which approved source should resolve the conflict?
              safe_options:
                - Use the architecture decision.
                - Stop and request a new decision.
            """;
        var result = PacketValidator.Validate(packet, PacketSchemaRegistry.Load(SchemaRoot));

        Assert.True(result.IsValid);
        var projection = ResultProjector.Project(result.Packet!);

        Assert.Contains("A human decision is required.", projection);
        Assert.Contains("Which approved source should resolve the conflict?", projection);
        Assert.Contains("Use the architecture decision.", projection);
        Assert.DoesNotContain("task_id", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("blocked", projection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Human_summary_and_required_pause_payload_have_strict_limits()
    {
        const string packet = """
            schema: tlaw.agent-result/v1
            task_id: BAR-31
            status: blocked
            human_summary: |
              one
              two
              three
              four
              five
              six
            evidence:
              - kind: source
                reference: https://linear.app/baronet/issue/BAR-31
            human:
              required: true
              question: ""
              safe_options: []
            """;
        var result = PacketValidator.Validate(packet, PacketSchemaRegistry.Load(SchemaRoot));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "TLAW-PKT-022");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "TLAW-PKT-024");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "TLAW-PKT-025");
    }

    [Fact]
    public void Manifest_paths_exist_and_startup_order_begins_with_the_binding_entry_points()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(AgentRoot, "context-manifest.json")));
        var startupOrder = manifest.RootElement.GetProperty("startup_order").EnumerateArray().Select(value => value.GetString()).ToArray();
        var sources = manifest.RootElement.GetProperty("sources").EnumerateArray().Select(value => value.GetProperty("path").GetString()).ToArray();

        Assert.Equal("AGENTS.md", startupOrder[0]);
        Assert.Equal("docs/agent/CONTEXT_INDEX.md", startupOrder[1]);
        Assert.All(startupOrder.Concat(sources).Where(path => path is not null), path => Assert.True(File.Exists(Path.Combine(RepositoryRoot, path!)), path));
    }

    private static IReadOnlyList<string> PositiveFixturePaths { get; } = ["task.valid.yaml", "task.v2.valid.yaml", "task.v2.claimed.valid.yaml", "result.valid.yaml", "review.valid.yaml", "handoff.valid.yaml", "handoff.v2.valid.yaml"];

    private static string RepositoryRoot => FindRepositoryRoot();
    private static string AgentRoot => Path.Combine(RepositoryRoot, "docs", "agent");
    private static string SchemaRoot => Path.Combine(AgentRoot, "schemas");
    private static string ExamplesRoot => Path.Combine(SchemaRoot, "examples");

    private static IReadOnlyList<string> NonEmptyLines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
