using Tlaw.AgentProtocol;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class HandoffV2ValidationTests
{
    [Fact]
    public void V1_fixture_remains_valid_and_is_not_a_v2_packet() { var r = Validate("handoff.valid.yaml"); Assert.True(r.IsValid); Assert.Equal("tlaw.agent-handoff/v1", r.Packet!.Schema); }
    [Theory]
    [InlineData("head_sha: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "head_sha: AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("head_sha: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "head_sha: 0000000000000000000000000000000000000000")]
    [InlineData("commits:\n  - aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "commits:\n  - aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n  - aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("remaining_work:\n  - Run independent review.", "remaining_work: []")]
    [InlineData("known_failures: []", "known_failures:\n  - duplicate\n  - duplicate")]
    [InlineData("files_changed:\n  - docs/agent/schemas/handoff.v2.schema.json", "files_changed:\n  - ../escape")]
    [InlineData("files_changed:\n  - docs/agent/schemas/handoff.v2.schema.json", "files_changed:\n  - C:/escape")]
    [InlineData("files_changed:\n  - docs/agent/schemas/handoff.v2.schema.json", "files_changed:\n  - a//b")]
    [InlineData("files_changed:\n  - docs/agent/schemas/handoff.v2.schema.json", "files_changed:\n  - a\\b")]
    [InlineData("files_changed:\n  - docs/agent/schemas/handoff.v2.schema.json", "files_changed:\n  - /absolute")]
    [InlineData("completed_work:\n  - Added the handoff v2 schema.", "completed_work:\n  - duplicate\n  - duplicate")]
    [InlineData("open_questions: []", "open_questions:\n  - duplicate\n  - duplicate")]
    [InlineData("commits:\n  - aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "commits: []")]
    [InlineData("evidence:\n  - kind: command\n    reference: dotnet test --configuration Release", "evidence:\n  - kind: network\n    reference: x")]
    [InlineData("next_action: Review the Draft PR without merging it.", "next_action: \"\"")]
    [InlineData("next_action: Review the Draft PR without merging it.", "next_action: \"\"")]
    [InlineData("evidence:\n  - kind: command\n    reference: dotnet test --configuration Release", "evidence: []")]
    [InlineData("status: ready", "status: blocked")]
    public void Invalid_v2_boundaries_fail_visibly(string find, string replace) { var yaml = File.ReadAllText(Path.Combine(Examples, "handoff.v2.valid.yaml")).Replace(find, replace, StringComparison.Ordinal); Assert.False(PacketValidator.Validate(yaml, Registry).IsValid); }
    [Theory]
    [InlineData("files_changed: []")]
    [InlineData("files_changed:\n  - docs/agent/file.txt\n  - nested/path/file.txt")]
    [InlineData("human_summary: |\n  one\n  two\n  three\n  four\n  five")]
    public void Valid_v2_variants_are_accepted(string replacement) { var yaml = File.ReadAllText(Path.Combine(Examples, "handoff.v2.valid.yaml")); if (replacement.StartsWith("human_summary", StringComparison.Ordinal)) yaml = yaml.Replace("human_summary: Handoff v2 preparation is complete.", replacement, StringComparison.Ordinal); else yaml = yaml.Replace("files_changed:\n  - docs/agent/schemas/handoff.v2.schema.json", replacement, StringComparison.Ordinal); Assert.True(PacketValidator.Validate(yaml, Registry).IsValid); }
    private static PacketValidationResult Validate(string fixture) => PacketValidator.Validate(File.ReadAllText(Path.Combine(Examples, fixture)), Registry);
    private static PacketSchemaRegistry Registry => PacketSchemaRegistry.Load(Path.Combine(Root, "docs", "agent", "schemas"));
    private static string Examples => Path.Combine(Root, "docs", "agent", "schemas", "examples");
    private static string Root { get { for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "AGENTS.md"))) return d.FullName; throw new DirectoryNotFoundException(); } }
}
