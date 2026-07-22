using Tlaw.AgentProtocol;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class TaskV2ClaimContractTests
{
    [Fact]
    public void Valid_claimed_task_v2_fixture_validates()
    {
        Assert.True(ValidateFixture("task.v2.claimed.valid.yaml").IsValid);
    }

    [Theory]
    [InlineData("invalid/task-v2-claimed-by-ineligible.yaml", "TLAW-PKT-032")]
    [InlineData("invalid/task-v2-nonutc-claim-timestamp.yaml", "TLAW-PKT-033")]
    [InlineData("invalid/task-v2-reversed-claim-timestamps.yaml", "TLAW-PKT-034")]
    public void Claimed_task_v2_fixtures_fail_with_deterministic_diagnostics(string fixture, string diagnostic)
    {
        var result = ValidateFixture(fixture);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, item => item.Code == diagnostic);
    }

    [Fact]
    public void Preferred_agent_must_be_eligible()
    {
        var yaml = ValidUnclaimedTask.Replace("  - codex\n  - claude", "  - claude", StringComparison.Ordinal);

        AssertInvalid(yaml, "TLAW-PKT-029", "eligible_agents");
    }

    [Theory]
    [InlineData("local")]
    [InlineData("grok")]
    public void Implementation_cannot_prefer_local_or_grok(string agent)
    {
        var yaml = ValidUnclaimedTask.Replace("preferred_agent: codex", $"preferred_agent: {agent}", StringComparison.Ordinal);

        AssertInvalid(yaml, "TLAW-PKT-029", "preferred_agent");
    }

    [Theory]
    [InlineData("local")]
    [InlineData("grok")]
    public void Implementation_cannot_make_local_or_grok_eligible(string agent)
    {
        var yaml = ValidUnclaimedTask.Replace("  - claude\nrequired_capabilities:", $"  - claude\n  - {agent}\nrequired_capabilities:", StringComparison.Ordinal);

        AssertInvalid(yaml, "TLAW-PKT-029", "eligible_agents");
    }

    [Fact]
    public void Local_is_rejected_for_non_read_only_work_type()
    {
        var yaml = ValidUnclaimedTask
            .Replace("work_type: implementation", "work_type: planning", StringComparison.Ordinal)
            .Replace("autonomy_level: branch_write", "autonomy_level: read_only", StringComparison.Ordinal)
            .Replace("  - claude\nrequired_capabilities:", "  - claude\n  - local\nrequired_capabilities:", StringComparison.Ordinal);

        AssertInvalid(yaml, "TLAW-PKT-031", "eligible_agents");
    }

    [Fact]
    public void Local_is_rejected_when_autonomy_is_not_read_only()
    {
        var yaml = ValidUnclaimedTask
            .Replace("work_type: implementation", "work_type: read_only_analysis", StringComparison.Ordinal)
            .Replace("  - claude\nrequired_capabilities:", "  - claude\n  - local\nrequired_capabilities:", StringComparison.Ordinal);

        AssertInvalid(yaml, "TLAW-PKT-031", "autonomy_level");
    }

    [Fact]
    public void Partially_unclaimed_claim_fields_are_rejected()
    {
        var yaml = ValidUnclaimedTask.Replace("claimed_by: unclaimed", "claimed_by: codex", StringComparison.Ordinal);

        AssertInvalid(yaml, "TLAW-PKT-030", "claim");
    }

    [Theory]
    [InlineData("2026-07-22T11:30:00Z")]
    [InlineData("2026-07-22T11:30:00+00:00")]
    [InlineData("2026-07-22T14:30:00.0000000+03:00")]
    public void Claimed_timestamp_must_use_the_canonical_utc_format(string timestamp)
    {
        var yaml = ValidClaimedTask.Replace("2026-07-22T11:30:00.0000000Z", timestamp, StringComparison.Ordinal);

        AssertInvalid(yaml, "TLAW-PKT-033", "claim_started_at");
    }

    [Fact]
    public void Claimed_timestamp_must_be_strictly_increasing()
    {
        var yaml = ValidClaimedTask.Replace("2026-07-22T11:35:00.0000000Z", "2026-07-22T11:30:00.0000000Z", StringComparison.Ordinal);

        AssertInvalid(yaml, "TLAW-PKT-034", "claim_expires_at");
    }

    private static void AssertInvalid(string yaml, string code, string path)
    {
        var result = PacketValidator.Validate(yaml, PacketSchemaRegistry.Load(SchemaRoot));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, item => item.Code == code && item.Path == path);
    }

    private static PacketValidationResult ValidateFixture(string fixture) => PacketValidator.Validate(File.ReadAllText(Path.Combine(ExamplesRoot, fixture)), PacketSchemaRegistry.Load(SchemaRoot));

    private static string ValidUnclaimedTask => File.ReadAllText(Path.Combine(ExamplesRoot, "task.v2.valid.yaml"));
    private static string ValidClaimedTask => File.ReadAllText(Path.Combine(ExamplesRoot, "task.v2.claimed.valid.yaml"));
    private static string SchemaRoot => Path.Combine(RepositoryRoot, "docs", "agent", "schemas");
    private static string ExamplesRoot => Path.Combine(SchemaRoot, "examples");
    private static string RepositoryRoot => FindRepositoryRoot();

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
