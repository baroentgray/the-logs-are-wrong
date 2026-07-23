using System.Text;
using Tlaw.AgentProtocol;
using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class HandoffPreparationCommandTests
{
    [Theory]
    [InlineData("ready", "[]", "[]")]
    [InlineData("blocked", "[\"failure\"]", "[]")]
    [InlineData("blocked", "[]", "[\"question\"]")]
    public void Closed_snapshot_prepares_a_valid_handoff(string status, string failures, string questions)
    {
        using var w = Workspace.Create(); var task = w.Write("task.yaml", Task); var snapshot = w.Write("snapshot.json", Snapshot(status, failures, questions)); var output = Path.Combine(w.Path, "handoff.yaml");
        Assert.Equal(0, PrepareHandoffCommand.Run(Args(task, snapshot, output), TextWriter.Null, TextWriter.Null));
        var bytes = File.ReadAllBytes(output); Assert.DoesNotContain((byte)'\r', bytes); Assert.Equal((byte)'\n', bytes[^1]);
        Assert.True(PacketValidator.Validate(File.ReadAllText(output), PacketSchemaRegistry.Load(Path.Combine(Root, "docs", "agent", "schemas"))).IsValid);
    }

    [Theory]
    [InlineData("\"status\":\"ready\"", "\"status\":\"invalid\"")]
    [InlineData("\"remaining_work\":[\"review\"]", "\"remaining_work\":[]")]
    [InlineData("\"files_changed\":[]", "\"files_changed\":[\"../escape\"]")]
    [InlineData("\"head_sha\":\"019490a5d1b7b37cfa23cf3599e25f7a61afe334\"", "\"head_sha\":\"0000000000000000000000000000000000000000\"")]
    public void Invalid_snapshot_preserves_output(string find, string replace)
    {
        using var w = Workspace.Create(); var task = w.Write("task.yaml", Task); var snapshot = w.Write("snapshot.json", Snapshot().Replace(find, replace, StringComparison.Ordinal)); var output = w.Write("out.yaml", "previous\n");
        Assert.NotEqual(0, PrepareHandoffCommand.Run(Args(task, snapshot, output), TextWriter.Null, TextWriter.Null)); Assert.Equal("previous\n", File.ReadAllText(output));
    }

    [Fact]
    public void Options_aliases_determinism_and_stdout_failure_are_covered()
    {
        using var w = Workspace.Create(); var task = w.Write("task.yaml", Task); var snapshot = w.Write("snapshot.json", Snapshot()); var first = Path.Combine(w.Path, "one.yaml"); var second = Path.Combine(w.Path, "two.yaml");
        Assert.NotEqual(0, PrepareHandoffCommand.Run(["prepare-handoff", "--task", task, "--snapshot", snapshot, "--output"], TextWriter.Null, TextWriter.Null)); Assert.NotEqual(0, PrepareHandoffCommand.Run(Args(task, snapshot, task), TextWriter.Null, TextWriter.Null));
        Assert.Equal(0, PrepareHandoffCommand.Run(Args(task, snapshot, first), TextWriter.Null, TextWriter.Null)); Assert.Equal(0, PrepareHandoffCommand.Run(Args(task, snapshot, second), TextWriter.Null, TextWriter.Null)); Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        using var error = new StringWriter(); var published = Path.Combine(w.Path, "published.yaml"); Assert.NotEqual(0, PrepareHandoffCommand.Run(Args(task, snapshot, published), new ThrowingWriter(), error)); Assert.True(File.Exists(published)); Assert.Contains("already published", error.ToString(), StringComparison.Ordinal);
    }

    private static string[] Args(string task, string snapshot, string output) => ["prepare-handoff", "--task", task, "--snapshot", snapshot, "--output", output];
    private static string Snapshot(string status = "ready", string failures = "[]", string questions = "[]") => $"{{\"schema\":\"tlaw.dispatcher-handoff-input/v1\",\"status\":\"{status}\",\"head_sha\":\"019490a5d1b7b37cfa23cf3599e25f7a61afe334\",\"commits\":[],\"completed_work\":[],\"remaining_work\":[\"review\"],\"files_changed\":[],\"commands_run\":[{{\"command\":\"dotnet test\",\"exit_code\":0}}],\"evidence\":[{{\"kind\":\"command\",\"reference\":\"dotnet test\"}}],\"known_failures\":{failures},\"open_questions\":{questions},\"human_summary\":\"summary\",\"next_action\":\"review\"}}";
    private static string Root { get { for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "AGENTS.md"))) return d.FullName; throw new DirectoryNotFoundException(); } }
    private const string Task = "schema: tlaw.agent-task/v2\ntask_id: BAR-26-increment-2\nsource_id: BAR-26\nsources:\n  - https://linear.app/baronet/issue/BAR-26\nobjective: handoff\nwork_type: implementation\npreferred_agent: codex\neligible_agents:\n  - codex\nrequired_capabilities:\n  - dotnet\nautonomy_level: branch_write\nforbidden_operations:\n  - Merge pull requests.\nclaimed_by: codex\nclaim_id: 0e221c4b8ed84e6dae7eea27008eb449\nclaim_started_at: 2026-07-22T11:30:00.0000000Z\nclaim_expires_at: 2026-07-22T11:35:00.0000000Z\nbase_sha: 019490a5d1b7b37cfa23cf3599e25f7a61afe334\nhandoff_required: true\nworktree: task/BAR-26-dispatcher-leases\nverification:\n  required: true\n  commands:\n    - dotnet test\ndelivery:\n  branch_required: true\n  draft_pr_required: true\n  merge_forbidden: true\n";
    private sealed class ThrowingWriter : TextWriter { public override Encoding Encoding => Encoding.UTF8; public override void WriteLine(string? value) => throw new IOException(); }
    private sealed class Workspace : IDisposable { private Workspace(string path) => Path = path; internal string Path { get; } internal static Workspace Create() { var p = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"handoff-{Guid.NewGuid():N}"); Directory.CreateDirectory(p); return new(p); } internal string Write(string name, string text) { var p = System.IO.Path.Combine(Path, name); File.WriteAllText(p, text, new UTF8Encoding(false)); return p; } public void Dispose() => Directory.Delete(Path, true); }
}
