using System.Text;
using Tlaw.AgentProtocol;
using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class DispatcherPacketGenerationTests
{
    [Fact]
    public void Packet_generation_has_stable_schema_order_and_byte_identical_output()
    {
        using var workspace = TemporaryWorkspace.Create();
        var input = workspace.Write("task-input.json", ValidNormalizedInput);
        var firstOutput = Path.Combine(workspace.Path, "first.yaml");
        var secondOutput = Path.Combine(workspace.Path, "second.yaml");

        Assert.Equal(0, TaskPacketCommand.Run(["packet", "--input", input, "--output", firstOutput], TextWriter.Null, TextWriter.Null));
        Assert.Equal(0, TaskPacketCommand.Run(["packet", "--input", input, "--output", secondOutput], TextWriter.Null, TextWriter.Null));

        var first = File.ReadAllBytes(firstOutput);
        var second = File.ReadAllBytes(secondOutput);
        Assert.Equal(first, second);
        Assert.False(first.Contains((byte)'\r'), "Task packet output must use LF line endings on every platform.");

        var yaml = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(first);
        Assert.True(PacketValidator.Validate(yaml, PacketSchemaRegistry.Load(SchemaRoot)).IsValid);
        AssertOrdered(yaml, "schema:", "task_id:", "source_id:", "sources:", "objective:", "work_type:", "preferred_agent:", "eligible_agents:", "required_capabilities:", "autonomy_level:", "forbidden_operations:", "claimed_by:", "claim_id:", "claim_started_at:", "claim_expires_at:", "base_sha:", "handoff_required:", "worktree:", "verification:", "delivery:");
    }

    [Fact]
    public void Packet_generation_preserves_references_unclaimed_fields_and_forbidden_operations_without_issue_body()
    {
        using var workspace = TemporaryWorkspace.Create();
        var input = workspace.Write("task-input.json", ValidNormalizedInput);
        var output = Path.Combine(workspace.Path, "task.yaml");

        Assert.Equal(0, TaskPacketCommand.Run(["packet", "--input", input, "--output", output], TextWriter.Null, TextWriter.Null));

        var yaml = File.ReadAllText(output);
        Assert.Contains("https://linear.app/baronet/issue/BAR-26/tlaw-auto-001-linear-driven-agent-dispatcher-mvp", yaml);
        Assert.Contains("docs/agent/AGENT_PROTOCOL.md", yaml);
        Assert.Contains("- \"Merge pull requests.\"", yaml);
        Assert.Contains("claimed_by: \"unclaimed\"", yaml);
        Assert.Contains("claim_id: \"unclaimed\"", yaml);
        Assert.DoesNotContain("Full issue body must never appear in a task packet.", yaml);
    }

    [Theory]
    [InlineData("base_sha", "c4ad144fac76584faf7948956c172e20df9a5a79", "not-a-sha")]
    [InlineData("unexpected", "not-present", "true")]
    [InlineData("issue_body", "not-present", "Full issue body must never appear in a task packet.")]
    public void Invalid_normalized_input_fails_without_creating_output(string property, string expectedValue, string replacement)
    {
        using var workspace = TemporaryWorkspace.Create();
        var content = property == "base_sha"
            ? ValidNormalizedInput.Replace($"\"{property}\": \"{expectedValue}\"", $"\"{property}\": \"{replacement}\"", StringComparison.Ordinal)
            : ValidNormalizedInput.Replace("  \"schema\":", $"  \"{property}\": {System.Text.Json.JsonSerializer.Serialize(replacement)},\n  \"schema\":", StringComparison.Ordinal);
        var input = workspace.Write("invalid-input.json", content);
        var output = Path.Combine(workspace.Path, "task.yaml");
        using var errors = new StringWriter();

        var exitCode = TaskPacketCommand.Run(["packet", "--input", input, "--output", output], TextWriter.Null, errors);

        Assert.NotEqual(0, exitCode);
        Assert.False(File.Exists(output));
        Assert.StartsWith("FAIL:", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_task_is_accepted_by_the_existing_protocol_validator()
    {
        using var workspace = TemporaryWorkspace.Create();
        var input = workspace.Write("task-input.json", ValidNormalizedInput);
        var output = Path.Combine(workspace.Path, "task.yaml");

        Assert.Equal(0, TaskPacketCommand.Run(["packet", "--input", input, "--output", output], TextWriter.Null, TextWriter.Null));

        var validation = PacketValidator.Validate(File.ReadAllText(output), PacketSchemaRegistry.Load(SchemaRoot));
        Assert.True(validation.IsValid, string.Join(" | ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal("tlaw.agent-task/v2", validation.Packet!.Schema);
    }

    [Fact]
    public void Valid_generation_replaces_existing_output_and_invalid_generation_preserves_it()
    {
        using var workspace = TemporaryWorkspace.Create();
        var validInput = workspace.Write("task-input.json", ValidNormalizedInput);
        var invalidInput = workspace.Write("invalid-input.json", ValidNormalizedInput.Replace("c4ad144fac76584faf7948956c172e20df9a5a79", "not-a-sha", StringComparison.Ordinal));
        var output = Path.Combine(workspace.Path, "task.yaml");
        File.WriteAllText(output, "previous packet\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Assert.Equal(0, TaskPacketCommand.Run(["packet", "--input", validInput, "--output", output], TextWriter.Null, TextWriter.Null));
        var generated = File.ReadAllText(output);
        Assert.NotEqual("previous packet\n", generated);

        Assert.NotEqual(0, TaskPacketCommand.Run(["packet", "--input", invalidInput, "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.Equal(generated, File.ReadAllText(output));
    }

    private static void AssertOrdered(string text, params string[] keys)
    {
        var previous = -1;
        foreach (var key in keys)
        {
            var current = text.IndexOf(key, StringComparison.Ordinal);
            Assert.True(current > previous, $"'{key}' was missing or out of order.");
            previous = current;
        }
    }

    private static string SchemaRoot => Path.Combine(FindRepositoryRoot(), "docs", "agent", "schemas");

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

    private const string ValidNormalizedInput = """
        {
          "schema": "tlaw.dispatcher-input/v1",
          "task_id": "BAR-26-increment-1",
          "source_id": "BAR-26",
          "sources": [
            "https://linear.app/baronet/issue/BAR-26/tlaw-auto-001-linear-driven-agent-dispatcher-mvp",
            "docs/agent/AGENT_PROTOCOL.md"
          ],
          "objective": "Generate a validated task packet from a local normalized snapshot.",
          "work_type": "implementation",
          "preferred_agent": "codex",
          "eligible_agents": ["codex", "claude"],
          "required_capabilities": ["dotnet", "yaml_protocol"],
          "autonomy_level": "branch_write",
          "forbidden_operations": ["Merge pull requests.", "Write main.", "Contact Linear."],
          "claimed_by": "unclaimed",
          "claim_id": "unclaimed",
          "claim_started_at": "unclaimed",
          "claim_expires_at": "unclaimed",
          "base_sha": "c4ad144fac76584faf7948956c172e20df9a5a79",
          "handoff_required": true,
          "worktree": "task/BAR-26-dispatcher-packet-generation",
          "verification": {
            "required": true,
            "commands": ["dotnet test --configuration Release"]
          },
          "delivery": {
            "branch_required": true,
            "draft_pr_required": true,
            "merge_forbidden": true
          }
        }
        """;

    private sealed class TemporaryWorkspace : IDisposable
    {
        private TemporaryWorkspace(string path)
        {
            Path = path;
        }

        internal string Path { get; }

        internal static TemporaryWorkspace Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tlaw-dispatcher-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryWorkspace(path);
        }

        internal string Write(string name, string content)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return path;
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
