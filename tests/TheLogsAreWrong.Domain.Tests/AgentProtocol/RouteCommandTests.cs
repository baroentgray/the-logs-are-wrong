using System.Text;
using System.Text.Json;
using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class RouteCommandTests
{
    [Fact]
    public void Route_writes_a_deterministic_lf_no_bom_selection_without_touching_lease_state()
    {
        using var workspace = RouteWorkspace.Create();
        var task = workspace.Write("task.yaml", ValidTaskYaml());
        var agents = workspace.Write("agents.json", Snapshot(Agent("codex", "AVAILABLE"), Agent("claude", "AVAILABLE")));
        var firstOutput = Path.Combine(workspace.Path, "first-selection.json");
        var secondOutput = Path.Combine(workspace.Path, "second-selection.json");
        using var output = new StringWriter();
        using var errors = new StringWriter();

        Assert.Equal(0, RouteCommand.Run(["route", "--task", task, "--agents", agents, "--output", firstOutput], output, errors));
        Assert.Equal(0, RouteCommand.Run(["route", "--task", task, "--agents", agents, "--output", secondOutput], TextWriter.Null, TextWriter.Null));

        var first = File.ReadAllBytes(firstOutput);
        Assert.Equal(first, File.ReadAllBytes(secondOutput));
        Assert.False(first.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.DoesNotContain((byte)'\r', first);
        Assert.Equal((byte)'\n', first[^1]);
        Assert.Equal("SELECTED: codex (AVAILABLE)" + Environment.NewLine, output.ToString());
        Assert.Equal(string.Empty, errors.ToString());
        Assert.DoesNotContain(Directory.EnumerateFiles(workspace.Path, "*.json", SearchOption.AllDirectories), path => path.Contains("lease", StringComparison.OrdinalIgnoreCase));

        using var selection = JsonDocument.Parse(first);
        Assert.Equal("tlaw.dispatcher-selection/v1", selection.RootElement.GetProperty("schema").GetString());
        Assert.Equal("BAR-34", selection.RootElement.GetProperty("task_id").GetString());
        Assert.Equal("codex", selection.RootElement.GetProperty("selected_agent").GetString());
        Assert.False(selection.RootElement.GetProperty("executor_override_applied").GetBoolean());
        Assert.Equal(0, selection.RootElement.GetProperty("availability_overrides").GetArrayLength());
    }

    [Fact]
    public void Availability_override_changes_selection_and_is_recorded()
    {
        using var workspace = RouteWorkspace.Create();
        var task = workspace.Write("task.yaml", ValidTaskYaml());
        var agents = workspace.Write("agents.json", Snapshot(Agent("codex", "DEGRADED"), Agent("claude", "AVAILABLE")));
        var output = Path.Combine(workspace.Path, "selection.json");

        Assert.Equal(0, RouteCommand.Run(
            ["route", "--task", task, "--agents", agents, "--output", output, "--availability-override", "codex=AVAILABLE"],
            TextWriter.Null,
            TextWriter.Null));

        using var selection = JsonDocument.Parse(File.ReadAllText(output));
        Assert.Equal("codex", selection.RootElement.GetProperty("selected_agent").GetString());
        var applied = selection.RootElement.GetProperty("availability_overrides").EnumerateArray().Single();
        Assert.Equal("codex", applied.GetProperty("agent").GetString());
        Assert.Equal("DEGRADED", applied.GetProperty("original").GetString());
        Assert.Equal("AVAILABLE", applied.GetProperty("effective").GetString());
    }

    [Fact]
    public void Executor_override_requires_a_valid_candidate_and_can_combine_with_a_valid_availability_override()
    {
        using var workspace = RouteWorkspace.Create();
        var task = workspace.Write("task.yaml", ValidTaskYaml());
        var agents = workspace.Write("agents.json", Snapshot(Agent("codex", "AVAILABLE"), Agent("claude", "OFFLINE")));
        var output = Path.Combine(workspace.Path, "selection.json");

        Assert.NotEqual(0, RouteCommand.Run(
            ["route", "--task", task, "--agents", agents, "--output", output, "--executor-override", "claude"],
            TextWriter.Null,
            TextWriter.Null));
        Assert.False(File.Exists(output));

        Assert.Equal(0, RouteCommand.Run(
            ["route", "--task", task, "--agents", agents, "--output", output, "--executor-override", "claude", "--availability-override", "claude=DEGRADED"],
            TextWriter.Null,
            TextWriter.Null));

        using var selection = JsonDocument.Parse(File.ReadAllText(output));
        Assert.Equal("claude", selection.RootElement.GetProperty("selected_agent").GetString());
        Assert.True(selection.RootElement.GetProperty("executor_override_applied").GetBoolean());
        Assert.Equal("DEGRADED", selection.RootElement.GetProperty("effective_availability").GetString());
    }

    [Theory]
    [InlineData("--executor-override", "grok")]
    [InlineData("--availability-override", "grok=AVAILABLE")]
    [InlineData("--availability-override", "codex=UNAVAILABLE")]
    public void Invalid_override_target_or_state_fails_without_output(string option, string value)
    {
        using var workspace = RouteWorkspace.Create();
        var task = workspace.Write("task.yaml", ValidTaskYaml());
        var agents = workspace.Write("agents.json", Snapshot(Agent("codex", "AVAILABLE"), Agent("claude", "AVAILABLE")));
        var output = Path.Combine(workspace.Path, "selection.json");

        var exitCode = RouteCommand.Run(["route", "--task", task, "--agents", agents, "--output", output, option, value], TextWriter.Null, TextWriter.Null);

        Assert.NotEqual(0, exitCode);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Executor_override_rejects_an_incapable_eligible_agent()
    {
        using var workspace = RouteWorkspace.Create();
        var task = workspace.Write("task.yaml", ValidTaskYaml());
        var agents = workspace.Write("agents.json", Snapshot(Agent("codex", "AVAILABLE"), Agent("claude", "AVAILABLE", "dotnet")));
        var output = Path.Combine(workspace.Path, "selection.json");

        Assert.NotEqual(0, RouteCommand.Run(
            ["route", "--task", task, "--agents", agents, "--output", output, "--executor-override", "claude"],
            TextWriter.Null,
            TextWriter.Null));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Duplicate_availability_overrides_and_non_repeating_options_fail_closed()
    {
        using var workspace = RouteWorkspace.Create();
        var task = workspace.Write("task.yaml", ValidTaskYaml());
        var agents = workspace.Write("agents.json", Snapshot(Agent("codex", "AVAILABLE"), Agent("claude", "AVAILABLE")));
        var output = Path.Combine(workspace.Path, "selection.json");

        Assert.NotEqual(0, RouteCommand.Run(
            ["route", "--task", task, "--agents", agents, "--output", output, "--availability-override", "codex=DEGRADED", "--availability-override", "codex=AVAILABLE"],
            TextWriter.Null,
            TextWriter.Null));
        Assert.NotEqual(0, RouteCommand.Run(
            ["route", "--task", task, "--task", task, "--agents", agents, "--output", output],
            TextWriter.Null,
            TextWriter.Null));
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("QUOTA_EXHAUSTED")]
    [InlineData("OFFLINE")]
    [InlineData("UNKNOWN")]
    public void No_selectable_executor_is_explicit_and_preserves_existing_output(string availability)
    {
        using var workspace = RouteWorkspace.Create();
        var task = workspace.Write("task.yaml", ValidTaskYaml());
        var agents = workspace.Write("agents.json", Snapshot(Agent("codex", availability), Agent("claude", availability)));
        var output = Path.Combine(workspace.Path, "selection.json");
        const string previous = "previous selection\n";
        File.WriteAllText(output, previous, new UTF8Encoding(false));
        using var errors = new StringWriter();

        var exitCode = RouteCommand.Run(["route", "--task", task, "--agents", agents, "--output", output], TextWriter.Null, errors);

        Assert.NotEqual(0, exitCode);
        Assert.StartsWith("FAIL: no selectable executor", errors.ToString(), StringComparison.Ordinal);
        Assert.Equal(previous, File.ReadAllText(output));
    }

    [Theory]
    [InlineData("{\"schema\":\"tlaw.dispatcher-agent-snapshot/v1\",\"agents\":[],\"schema\":\"tlaw.dispatcher-agent-snapshot/v1\"}")]
    [InlineData("{\"schema\":\"tlaw.dispatcher-agent-snapshot/v1\",\"agents\":[{\"agent\":\"codex\",\"agent\":\"codex\",\"capabilities\":[\"dotnet\",\"yaml_protocol\"],\"availability\":\"AVAILABLE\"},{\"agent\":\"claude\",\"capabilities\":[\"dotnet\",\"yaml_protocol\"],\"availability\":\"AVAILABLE\"}]}")]
    [InlineData("{\"schema\":\"tlaw.dispatcher-agent-snapshot/v1\",\"agents\":[{\"agent\":\"codex\",\"capabilities\":[\"dotnet\",\"dotnet\"],\"availability\":\"AVAILABLE\"},{\"agent\":\"claude\",\"capabilities\":[\"dotnet\",\"yaml_protocol\"],\"availability\":\"AVAILABLE\"}]}")]
    public void Duplicate_root_nested_and_capability_entries_fail_closed(string snapshot)
    {
        using var workspace = RouteWorkspace.Create();
        var task = workspace.Write("task.yaml", ValidTaskYaml());
        var agents = workspace.Write("agents.json", snapshot);
        var output = Path.Combine(workspace.Path, "selection.json");

        Assert.NotEqual(0, RouteCommand.Run(["route", "--task", task, "--agents", agents, "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("BROKEN")]
    [InlineData("")]
    public void Malformed_or_unknown_availability_fails_closed(string availability)
    {
        using var workspace = RouteWorkspace.Create();
        var task = workspace.Write("task.yaml", ValidTaskYaml());
        var agents = workspace.Write("agents.json", Snapshot(Agent("codex", availability), Agent("claude", "AVAILABLE")));
        var output = Path.Combine(workspace.Path, "selection.json");

        Assert.NotEqual(0, RouteCommand.Run(["route", "--task", task, "--agents", agents, "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Missing_eligible_snapshot_duplicate_agent_and_unknown_properties_fail_closed()
    {
        using var workspace = RouteWorkspace.Create();
        var task = workspace.Write("task.yaml", ValidTaskYaml());
        var missing = workspace.Write("missing.json", Snapshot(Agent("codex", "AVAILABLE")));
        var duplicate = workspace.Write("duplicate.json", Snapshot(Agent("codex", "AVAILABLE"), Agent("codex", "DEGRADED"), Agent("claude", "AVAILABLE")));
        var unknown = workspace.Write("unknown.json", "{\"schema\":\"tlaw.dispatcher-agent-snapshot/v1\",\"agents\":[],\"unexpected\":true}");
        var output = Path.Combine(workspace.Path, "selection.json");

        Assert.NotEqual(0, RouteCommand.Run(["route", "--task", task, "--agents", missing, "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.NotEqual(0, RouteCommand.Run(["route", "--task", task, "--agents", duplicate, "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.NotEqual(0, RouteCommand.Run(["route", "--task", task, "--agents", unknown, "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Claimed_v2_v1_and_invalid_task_packets_are_rejected_before_selection()
    {
        using var workspace = RouteWorkspace.Create();
        var claimed = workspace.Write("claimed.yaml", ValidTaskYaml().Replace("claimed_by: unclaimed", "claimed_by: codex", StringComparison.Ordinal));
        var v1 = workspace.Write("v1.yaml", File.ReadAllText(Path.Combine(ExamplesRoot, "task.valid.yaml")));
        var invalid = workspace.Write("invalid.yaml", "schema: tlaw.agent-task/v2\n");
        var agents = workspace.Write("agents.json", "not json");
        var output = Path.Combine(workspace.Path, "selection.json");

        Assert.NotEqual(0, RouteCommand.Run(["route", "--task", claimed, "--agents", agents, "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.NotEqual(0, RouteCommand.Run(["route", "--task", v1, "--agents", agents, "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.NotEqual(0, RouteCommand.Run(["route", "--task", invalid, "--agents", agents, "--output", output], TextWriter.Null, TextWriter.Null));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Route_diagnostics_never_claim_launch_or_dispatch()
    {
        using var workspace = RouteWorkspace.Create();
        var task = workspace.Write("task.yaml", ValidTaskYaml());
        var agents = workspace.Write("agents.json", Snapshot(Agent("codex", "OFFLINE"), Agent("claude", "OFFLINE")));
        var output = Path.Combine(workspace.Path, "selection.json");
        using var errors = new StringWriter();

        Assert.NotEqual(0, RouteCommand.Run(["route", "--task", task, "--agents", agents, "--output", output], TextWriter.Null, errors));

        Assert.DoesNotContain("launch", errors.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dispatch", errors.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string ValidTaskYaml() => """
        schema: tlaw.agent-task/v2
        task_id: BAR-34
        source_id: BAR-34
        sources:
          - docs/agent/AGENT_PROTOCOL.md
        objective: Select a local executor without dispatching it.
        work_type: implementation
        preferred_agent: codex
        eligible_agents:
          - codex
          - claude
        required_capabilities:
          - dotnet
          - yaml_protocol
        autonomy_level: branch_write
        forbidden_operations:
          - Merge pull requests.
        claimed_by: unclaimed
        claim_id: unclaimed
        claim_started_at: unclaimed
        claim_expires_at: unclaimed
        base_sha: dbc08bdd3fbebb20443f2c5d30748105ab5ee669
        handoff_required: true
        worktree: task/BAR-34-dispatcher-routing
        verification:
          required: true
          commands:
            - dotnet test --configuration Release
        delivery:
          branch_required: true
          draft_pr_required: true
          merge_forbidden: true
        """;

    private static string Snapshot(params SnapshotAgent[] agents) => $$"""
        {
          "schema": "tlaw.dispatcher-agent-snapshot/v1",
          "agents": [
        {{string.Join(",\n", agents.Select(agent => $$"""    { "agent": "{{agent.Name}}", "capabilities": [{{string.Join(", ", agent.Capabilities.Select(value => JsonSerializer.Serialize(value)))}}], "availability": "{{agent.Availability}}" }"""))}}
          ]
        }
        """;

    private static SnapshotAgent Agent(string name, string availability, params string[]? capabilities) => new(name, capabilities is { Length: > 0 } ? capabilities : ["dotnet", "yaml_protocol"], availability);

    private static string ExamplesRoot => Path.Combine(FindRepositoryRoot(), "docs", "agent", "schemas", "examples");

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

    private sealed record SnapshotAgent(string Name, IReadOnlyList<string> Capabilities, string Availability);

    private sealed class RouteWorkspace : IDisposable
    {
        private RouteWorkspace(string path)
        {
            Path = path;
        }

        internal string Path { get; }

        internal static RouteWorkspace Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tlaw-route-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new RouteWorkspace(path);
        }

        internal string Write(string name, string content)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
