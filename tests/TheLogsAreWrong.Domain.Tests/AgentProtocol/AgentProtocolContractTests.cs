using System.Text.Json;
using Tlaw.AgentProtocol;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class AgentProtocolContractTests
{
    [Fact]
    public void Every_v1_schema_validates_its_positive_fixture()
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
    public void Invalid_protocol_fixtures_fail_visibly(string fixture)
    {
        var result = PacketValidator.Validate(File.ReadAllText(Path.Combine(ExamplesRoot, fixture)), PacketSchemaRegistry.Load(SchemaRoot));

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Diagnostics);
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
              question: 
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

    private static IReadOnlyList<string> PositiveFixturePaths { get; } = ["task.valid.yaml", "result.valid.yaml", "review.valid.yaml", "handoff.valid.yaml"];

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
