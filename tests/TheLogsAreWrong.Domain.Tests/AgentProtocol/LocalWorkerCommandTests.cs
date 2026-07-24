using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class LocalWorkerCommandTests
{
    [Fact]
    public void Run_publishes_a_stable_untrusted_artifact_after_exact_loopback_requests_and_lease_rechecks()
    {
        using var workspace = Workspace.Create();
        var clock = new FakeClock(new DateTimeOffset(2026, 7, 24, 10, 0, 0, TimeSpan.Zero));
        var fixture = ClaimedLocalTask.Create(workspace, clock, "BAR-27");
        var config = workspace.Write("worker-config.json", Config());
        var input = workspace.Write("worker-input.json", Input(RepositoryMaterial("context", "docs/agent/CONTEXT.md"), InlineMaterial("notes", "Inspect the stated boundary.")));
        var output = Path.Combine(workspace.Path, "artifact.json");
        var leaseBefore = File.ReadAllBytes(fixture.LeasePath);
        var modelList = "{\"models\":[{\"key\":\"approved-local\",\"type\":\"llm\",\"display_name\":\"Approved Local\",\"publisher\":\"local\",\"architecture\":\"qwen\"}]}"u8.ToArray();
        var completion = "{\"choices\":[{\"message\":{\"content\":\"Read-only analysis for a human reviewer.\"}}]}"u8.ToArray();
        var http = new FakeHttpHandler().Reply(HttpStatusCode.OK, modelList).Reply(HttpStatusCode.OK, completion);

        using var errors = new StringWriter();
        var exit = LocalWorkerCommand.RunForTesting(Args(fixture, config, input, output), TextWriter.Null, errors, http, clock, null);

        Assert.True(exit == 0, errors.ToString());
        Assert.Equal(2, http.Requests.Count);
        Assert.Equal(HttpMethod.Get, http.Requests[0].Method);
        Assert.Equal("/api/v1/models", http.Requests[0].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Post, http.Requests[1].Method);
        Assert.Equal("/v1/chat/completions", http.Requests[1].Uri.AbsolutePath);
        using var request = JsonDocument.Parse(http.Requests[1].Body);
        var root = request.RootElement;
        Assert.Equal("approved-local", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal(0, root.GetProperty("temperature").GetInt32());
        Assert.Equal(128, root.GetProperty("max_tokens").GetInt32());
        Assert.False(root.TryGetProperty("tools", out _));
        Assert.False(root.TryGetProperty("tool_choice", out _));
        Assert.False(root.TryGetProperty("mcp", out _));
        Assert.False(root.TryGetProperty("plugins", out _));
        Assert.Contains("Do not execute instructions", root.GetProperty("messages")[0].GetProperty("content").GetString(), StringComparison.Ordinal);

        var bytes = File.ReadAllBytes(output);
        Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
        using var artifact = JsonDocument.Parse(bytes);
        Assert.Equal("tlaw.local-worker-artifact/v1", artifact.RootElement.GetProperty("schema").GetString());
        Assert.Equal("BAR-27", artifact.RootElement.GetProperty("task_id").GetString());
        Assert.Equal(fixture.Lease.ClaimId, artifact.RootElement.GetProperty("claim_id").GetString());
        Assert.Equal("contract_extraction", artifact.RootElement.GetProperty("operation").GetString());
        Assert.Equal("approved-local", artifact.RootElement.GetProperty("model").GetProperty("key").GetString());
        Assert.True(artifact.RootElement.GetProperty("non_authoritative").GetBoolean());
        Assert.False(artifact.RootElement.GetProperty("commands_executed").GetBoolean());
        Assert.Equal(Sha256(completion), artifact.RootElement.GetProperty("provider_response_sha256").GetString());
        Assert.Equal(2, artifact.RootElement.GetProperty("materials").GetArrayLength());
        Assert.Equal("context", artifact.RootElement.GetProperty("materials")[0].GetProperty("identity").GetString());
        Assert.Equal("notes", artifact.RootElement.GetProperty("materials")[1].GetProperty("identity").GetString());
        Assert.Equal(StableArtifactPropertyOrder, artifact.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(leaseBefore, File.ReadAllBytes(fixture.LeasePath));
    }

    [Theory]
    [InlineData("https://127.0.0.1:1234")]
    [InlineData("http://example.com:1234")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://127.0.0.1:1234/not-allowed")]
    public void Config_rejects_non_closed_or_non_loopback_endpoints_before_any_provider_request(string endpoint)
    {
        using var workspace = Workspace.Create();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var fixture = ClaimedLocalTask.Create(workspace, clock, "BAR-27");
        var config = workspace.Write("worker-config.json", Config(endpoint: endpoint));
        var input = workspace.Write("worker-input.json", Input(InlineMaterial("note", "safe text")));
        var output = Path.Combine(workspace.Path, "artifact.json");
        var http = new FakeHttpHandler();

        Assert.Equal(1, Run(fixture, config, input, output, http, clock));
        Assert.Empty(http.Requests);
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("{\"schema\":\"tlaw.local-worker-config/v1\",\"endpoint\":\"http://127.0.0.1:1234\",\"model\":\"approved-local\",\"timeout_ms\":999,\"max_output_tokens\":128}")]
    [InlineData("{\"schema\":\"tlaw.local-worker-config/v1\",\"endpoint\":\"http://127.0.0.1:1234\",\"model\":\"approved-local\",\"timeout_ms\":5000,\"max_output_tokens\":4097}")]
    [InlineData("{\"schema\":\"tlaw.local-worker-config/v1\",\"endpoint\":\"http://127.0.0.1:1234\",\"model\":\"approved-local\",\"timeout_ms\":5000,\"max_output_tokens\":128,\"headers\":{}}")]
    [InlineData("{\"schema\":\"tlaw.local-worker-config/v1\",\"endpoint\":\"http://127.0.0.1:1234\",\"model\":\"approved-local\",\"api_token_env\":\"not-an-env-name\",\"timeout_ms\":5000,\"max_output_tokens\":128}")]
    public void Config_rejects_unbounded_or_unsupported_fields_before_any_provider_request(string configJson)
    {
        using var workspace = Workspace.Create();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var fixture = ClaimedLocalTask.Create(workspace, clock, "BAR-27");
        var config = workspace.Write("worker-config.json", configJson);
        var input = workspace.Write("worker-input.json", Input(InlineMaterial("note", "safe text")));
        var output = Path.Combine(workspace.Path, "artifact.json");
        var http = new FakeHttpHandler();

        Assert.Equal(1, Run(fixture, config, input, output, http, clock));
        Assert.Empty(http.Requests);
    }

    [Fact]
    public void Config_token_is_sent_only_as_bearer_auth_and_never_leaks_to_diagnostics_or_artifact()
    {
        using var workspace = Workspace.Create();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var fixture = ClaimedLocalTask.Create(workspace, clock, "BAR-27");
        var secret = "top-secret-local-token";
        var config = workspace.Write("worker-config.json", Config(apiTokenEnvironment: "TLAW_WORKER_TOKEN"));
        var input = workspace.Write("worker-input.json", Input(InlineMaterial("note", "safe text")));
        var output = Path.Combine(workspace.Path, "artifact.json");
        var http = new FakeHttpHandler().Reply(HttpStatusCode.Unauthorized, Encoding.UTF8.GetBytes(secret));
        Environment.SetEnvironmentVariable("TLAW_WORKER_TOKEN", secret);
        try
        {
            using var errors = new StringWriter();
            var exit = LocalWorkerCommand.RunForTesting(Args(fixture, config, input, output), TextWriter.Null, errors, http, clock, null);

            Assert.Equal(1, exit);
            Assert.Single(http.Requests);
            Assert.Equal($"Bearer {secret}", http.Requests[0].Authorization);
            Assert.DoesNotContain(secret, errors.ToString(), StringComparison.Ordinal);
            Assert.False(File.Exists(output));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TLAW_WORKER_TOKEN", null);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidModelLists))]
    public void Model_list_must_be_non_empty_unique_and_name_an_exact_usable_llm(byte[] response)
    {
        using var workspace = Workspace.Create();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var fixture = ClaimedLocalTask.Create(workspace, clock, "BAR-27");
        var config = workspace.Write("worker-config.json", Config());
        var input = workspace.Write("worker-input.json", Input(InlineMaterial("note", "safe text")));
        var output = workspace.Write("artifact.json", "previous\n");
        var leaseBefore = File.ReadAllBytes(fixture.LeasePath);
        var http = new FakeHttpHandler().Reply(HttpStatusCode.OK, response);

        Assert.Equal(1, Run(fixture, config, input, output, http, clock));
        Assert.Single(http.Requests);
        Assert.Equal("previous\n", File.ReadAllText(output));
        Assert.Equal(leaseBefore, File.ReadAllBytes(fixture.LeasePath));
    }

    public static IEnumerable<object[]> InvalidModelLists()
    {
        yield return ["{\"models\":[]}"u8.ToArray()];
        yield return ["{\"models\":[{\"key\":\"approved-local\",\"type\":\"embedding\"}]}"u8.ToArray()];
        yield return ["{\"models\":[{\"key\":\"other\",\"type\":\"llm\"}]}"u8.ToArray()];
        yield return ["{\"models\":[{\"key\":\"approved-local\",\"type\":\"llm\"},{\"key\":\"approved-local\",\"type\":\"llm\"}]}"u8.ToArray()];
        yield return ["not-json"u8.ToArray()];
    }

    [Fact]
    public void Model_list_timeout_fails_closed_without_artifact_or_chat_request()
    {
        using var workspace = Workspace.Create();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var fixture = ClaimedLocalTask.Create(workspace, clock, "BAR-27");
        var config = workspace.Write("worker-config.json", Config());
        var input = workspace.Write("worker-input.json", Input(InlineMaterial("note", "safe text")));
        var output = Path.Combine(workspace.Path, "artifact.json");
        var http = new FakeHttpHandler().Throw(new TaskCanceledException("simulated timeout"));

        Assert.Equal(1, Run(fixture, config, input, output, http, clock));
        Assert.Single(http.Requests);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Task_and_lease_identity_must_match_exactly()
    {
        using var workspace = Workspace.Create();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var fixture = ClaimedLocalTask.Create(workspace, clock, "BAR-27");
        var mismatched = workspace.Write("mismatched-task.yaml", ClaimedLocalTask.TaskYaml(fixture.Lease with { ClaimId = "0e221c4b8ed84e6dae7eea27008eb449" }));
        var config = workspace.Write("worker-config.json", Config());
        var input = workspace.Write("worker-input.json", Input(InlineMaterial("note", "safe text")));
        var output = Path.Combine(workspace.Path, "artifact.json");
        var http = new FakeHttpHandler();

        Assert.Equal(1, LocalWorkerCommand.RunForTesting(Args(mismatched, fixture.StorePath, config, input, output), TextWriter.Null, TextWriter.Null, http, clock, null));
        Assert.Empty(http.Requests);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Expired_or_busy_lease_fails_before_any_provider_request()
    {
        using var workspace = Workspace.Create();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var fixture = ClaimedLocalTask.Create(workspace, clock, "BAR-27", TimeSpan.FromMinutes(1));
        var config = workspace.Write("worker-config.json", Config());
        var input = workspace.Write("worker-input.json", Input(InlineMaterial("note", "safe text")));
        var output = Path.Combine(workspace.Path, "artifact.json");
        clock.Advance(TimeSpan.FromMinutes(1));
        var expiredHttp = new FakeHttpHandler();

        Assert.Equal(1, Run(fixture, config, input, output, expiredHttp, clock));
        Assert.Empty(expiredHttp.Requests);

        var activeClock = new FakeClock(DateTimeOffset.UtcNow);
        var active = ClaimedLocalTask.Create(workspace, activeClock, "BAR-28");
        var lockPath = LockPath(active.StorePath, active.Lease.TaskId);
        using var heldLock = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var busyHttp = new FakeHttpHandler();

        Assert.Equal(1, Run(active, config, input, Path.Combine(workspace.Path, "busy.json"), busyHttp, activeClock));
        Assert.Empty(busyHttp.Requests);
    }

    [Theory]
    [InlineData("chat")]
    [InlineData("publication")]
    public void Lease_is_rechecked_immediately_before_chat_and_publication(string boundary)
    {
        using var workspace = Workspace.Create();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var fixture = ClaimedLocalTask.Create(workspace, clock, "BAR-27", TimeSpan.FromMinutes(1));
        var config = workspace.Write("worker-config.json", Config());
        var input = workspace.Write("worker-input.json", Input(InlineMaterial("note", "safe text")));
        var output = workspace.Write("artifact.json", "previous\n");
        var http = ReadyHttp("analysis");
        var hooks = boundary == "chat"
            ? new LocalWorkerTestHooks(BeforeChat: () => clock.Advance(TimeSpan.FromMinutes(1)))
            : new LocalWorkerTestHooks(BeforePublication: () => clock.Advance(TimeSpan.FromMinutes(1)));

        Assert.Equal(1, LocalWorkerCommand.RunForTesting(Args(fixture, config, input, output), TextWriter.Null, TextWriter.Null, http, clock, hooks));
        Assert.Equal(boundary == "chat" ? 1 : 2, http.Requests.Count);
        Assert.Equal("previous\n", File.ReadAllText(output));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData(".git/config")]
    [InlineData("C:\\absolute.txt")]
    [InlineData("docs/../agent/CONTEXT.md")]
    public void Input_rejects_repository_material_traversal_and_absolute_or_git_paths(string path)
    {
        using var workspace = Workspace.Create();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var fixture = ClaimedLocalTask.Create(workspace, clock, "BAR-27");
        var config = workspace.Write("worker-config.json", Config());
        var input = workspace.Write("worker-input.json", Input(RepositoryMaterial("bad", path)));
        var http = new FakeHttpHandler();

        Assert.Equal(1, Run(fixture, config, input, Path.Combine(workspace.Path, "artifact.json"), http, clock));
        Assert.Empty(http.Requests);
    }

    [Fact]
    public void Input_rejects_nul_binary_invalid_utf8_excess_count_and_excess_size_materials()
    {
        using var workspace = Workspace.Create();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var fixture = ClaimedLocalTask.Create(workspace, clock, "BAR-27");
        var config = workspace.Write("worker-config.json", Config());
        foreach (var manifest in new[]
        {
            Input(InlineMaterial("nul", "not\0text")),
            Input(Enumerable.Range(0, LocalWorkerInputManifest.MaximumMaterialCount + 1).Select(index => InlineMaterial($"m{index}", "x")).ToArray()),
            Input(InlineMaterial("large", new string('x', LocalWorkerInputManifest.MaximumTotalBytes + 1)))
        })
        {
            var input = workspace.Write(Guid.NewGuid().ToString("N") + ".json", manifest);
            var http = new FakeHttpHandler();
            Assert.Equal(1, Run(fixture, config, input, Path.Combine(workspace.Path, Guid.NewGuid().ToString("N") + ".json"), http, clock));
            Assert.Empty(http.Requests);
        }

        using var material = RepositoryMaterialFile.Create("invalid.bin", [0xFF, 0xFE, 0x00]);
        var invalidUtf8 = workspace.Write("invalid-utf8.json", Input(RepositoryMaterial("invalid", material.RepositoryRelativePath)));
        var invalidHttp = new FakeHttpHandler();
        Assert.Equal(1, Run(fixture, config, invalidUtf8, Path.Combine(workspace.Path, "invalid.json"), invalidHttp, clock));
        Assert.Empty(invalidHttp.Requests);
    }

    [Fact]
    public void Input_rejects_a_symbolic_link_or_junction_escape_when_supported()
    {
        using var workspace = Workspace.Create();
        using var target = Workspace.Create();
        using var source = RepositoryMaterialFile.Create("placeholder.txt", Encoding.UTF8.GetBytes("safe"));
        var link = source.CreateSymbolicLink(target.Write("outside.txt", "outside\n"));
        if (link is null)
        {
            return;
        }

        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var fixture = ClaimedLocalTask.Create(workspace, clock, "BAR-27");
        var config = workspace.Write("worker-config.json", Config());
        var input = workspace.Write("worker-input.json", Input(RepositoryMaterial("link", link)));
        var http = new FakeHttpHandler();

        Assert.Equal(1, Run(fixture, config, input, Path.Combine(workspace.Path, "artifact.json"), http, clock));
        Assert.Empty(http.Requests);
    }

    [Fact]
    public void Failure_after_provider_response_preserves_existing_output_and_all_repository_and_lease_bytes()
    {
        using var workspace = Workspace.Create();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var fixture = ClaimedLocalTask.Create(workspace, clock, "BAR-27");
        var config = workspace.Write("worker-config.json", Config());
        var input = workspace.Write("worker-input.json", Input(InlineMaterial("note", "safe text")));
        var output = workspace.Write("artifact.json", "previous\n");
        var taskBefore = File.ReadAllBytes(fixture.TaskPath);
        var leaseBefore = File.ReadAllBytes(fixture.LeasePath);
        var http = ReadyHttp("All tests passed.");

        Assert.Equal(1, Run(fixture, config, input, output, http, clock));
        Assert.Equal("previous\n", File.ReadAllText(output));
        Assert.Equal(taskBefore, File.ReadAllBytes(fixture.TaskPath));
        Assert.Equal(leaseBefore, File.ReadAllBytes(fixture.LeasePath));
    }

    [Theory]
    [InlineData("{\"models\":[{\"key\":\"ready\",\"type\":\"llm\"}]}", "AVAILABLE")]
    [InlineData("{\"models\":[]}", "DEGRADED")]
    [InlineData("{\"models\":[{\"key\":\"embed\",\"type\":\"embedding\"}]}", "DEGRADED")]
    [InlineData("not-json", "DEGRADED")]
    public void Doctor_live_local_probe_uses_a_bounded_native_model_list(string modelList, string expectedAvailability)
    {
        var handler = new FakeHttpHandler().Reply(HttpStatusCode.OK, Encoding.UTF8.GetBytes(modelList));
        var probe = new SystemDoctorProbe(handler);

        var result = probe.ProbeLocal(new Uri("http://127.0.0.1:1234"));

        Assert.Single(handler.Requests);
        Assert.Equal("/api/v1/models", handler.Requests[0].Uri.AbsolutePath);
        Assert.Equal(expectedAvailability == "AVAILABLE" ? "LOCAL_LLM_AVAILABLE" : "DEGRADED", result.Stage);
    }

    private static int Run(ClaimedLocalTask fixture, string config, string input, string output, FakeHttpHandler http, FakeClock clock) =>
        LocalWorkerCommand.RunForTesting(Args(fixture, config, input, output), TextWriter.Null, TextWriter.Null, http, clock, null);

    private static string[] Args(ClaimedLocalTask fixture, string config, string input, string output) => Args(fixture.TaskPath, fixture.StorePath, config, input, output);
    private static string[] Args(string task, string store, string config, string input, string output) => ["local-worker", "run", "--task", task, "--lease-store", store, "--config", config, "--input", input, "--output", output];

    private static FakeHttpHandler ReadyHttp(string analysis) => new FakeHttpHandler()
        .Reply(HttpStatusCode.OK, "{\"models\":[{\"key\":\"approved-local\",\"type\":\"llm\",\"display_name\":\"Approved\",\"publisher\":\"local\",\"architecture\":\"qwen\"}]}"u8.ToArray())
        .Reply(HttpStatusCode.OK, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = analysis } } }
        })));

    private static string Config(string endpoint = "http://127.0.0.1:1234", string? apiTokenEnvironment = null) => JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["schema"] = "tlaw.local-worker-config/v1",
        ["endpoint"] = endpoint,
        ["model"] = "approved-local",
        ["api_token_env"] = apiTokenEnvironment,
        ["timeout_ms"] = 5000,
        ["max_output_tokens"] = 128
    }.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value));

    private static string Input(params Dictionary<string, object?>[] materials) => JsonSerializer.Serialize(new
    {
        schema = "tlaw.local-worker-input/v1",
        operation = "contract_extraction",
        materials
    });

    private static Dictionary<string, object?> RepositoryMaterial(string identity, string path) => new(StringComparer.Ordinal)
    {
        ["identity"] = identity,
        ["repository_path"] = path
    };

    private static Dictionary<string, object?> InlineMaterial(string identity, string text) => new(StringComparer.Ordinal)
    {
        ["identity"] = identity,
        ["inline_utf8"] = text
    };

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string LockPath(string store, string taskId) => Path.Combine(store, "locks", Sha256(Encoding.UTF8.GetBytes(taskId)) + ".lock");
    private static readonly string[] StableArtifactPropertyOrder = ["schema", "task_id", "claimed_by", "claim_id", "claim_started_at", "claim_expires_at", "operation", "model", "materials", "effective_prompt_sha256", "provider_response_sha256", "analysis", "non_authoritative", "commands_executed"];

    private sealed class FakeClock(DateTimeOffset now) : ILeaseClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;
        internal void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, byte[] Body, string? Authorization);

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = [];
        internal List<CapturedRequest> Requests { get; } = [];

        internal FakeHttpHandler Reply(HttpStatusCode status, byte[] body)
        {
            _responses.Enqueue(_ => new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
            return this;
        }

        internal FakeHttpHandler Throw(Exception exception)
        {
            _responses.Enqueue(_ => throw exception);
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body, request.Headers.Authorization?.ToString()));
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("Unexpected HTTP request.");
            }

            return _responses.Dequeue()(request);
        }
    }

    private sealed class Workspace : IDisposable
    {
        private Workspace(string path) => Path = path;
        internal string Path { get; }

        internal static Workspace Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tlaw-bar27-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new Workspace(path);
        }

        internal string Write(string name, string contents)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, contents, new UTF8Encoding(false));
            return path;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed record ClaimedLocalTask(string TaskPath, string StorePath, string LeasePath, LocalLease Lease)
    {
        internal static ClaimedLocalTask Create(Workspace workspace, ILeaseClock clock, string taskId, TimeSpan? ttl = null)
        {
            var store = Path.Combine(workspace.Path, taskId + "-leases");
            var lease = new FileLeaseStore(store, clock).Acquire(taskId, "local", ttl ?? TimeSpan.FromMinutes(5));
            var task = workspace.Write(taskId + ".yaml", TaskYaml(lease));
            return new ClaimedLocalTask(task, store, Path.Combine(store, "leases", Sha256(Encoding.UTF8.GetBytes(taskId)) + ".json"), lease);
        }

        internal static string TaskYaml(LocalLease lease) => $$"""
            schema: tlaw.agent-task/v2
            task_id: {{lease.TaskId}}
            source_id: BAR-27
            sources:
              - https://linear.app/baronet/issue/BAR-27/tlaw-auto-002-local-lm-studio-read-only-worker
              - docs/agent/AGENT_PROTOCOL.md
            objective: Produce read-only contract extraction.
            work_type: read_only_analysis
            preferred_agent: local
            eligible_agents:
              - local
            required_capabilities:
              - local_reasoning
            autonomy_level: read_only
            forbidden_operations:
              - Commit changes.
              - Push changes.
              - Merge changes.
            claimed_by: {{lease.ClaimedBy}}
            claim_id: {{lease.ClaimId}}
            claim_started_at: {{FileLeaseStore.FormatCanonicalTimestamp(lease.ClaimStartedAt)}}
            claim_expires_at: {{FileLeaseStore.FormatCanonicalTimestamp(lease.ClaimExpiresAt)}}
            base_sha: f7dde960b646d4c5e1efaa71b2ce57e879c7a789
            handoff_required: true
            worktree: task/BAR-27-lmstudio-readonly-boundary
            verification:
              required: true
              commands:
                - dotnet test --configuration Release
            delivery:
              branch_required: true
              draft_pr_required: true
              merge_forbidden: true
            """;
    }

    private sealed class RepositoryMaterialFile : IDisposable
    {
        private readonly string _directory;
        private RepositoryMaterialFile(string directory, string repositoryRelativePath) { _directory = directory; RepositoryRelativePath = repositoryRelativePath; }
        internal string RepositoryRelativePath { get; }

        internal static RepositoryMaterialFile Create(string fileName, byte[] bytes)
        {
            var root = RepositoryRoot();
            var name = ".bar27-test-" + Guid.NewGuid().ToString("N");
            var directory = Path.Combine(root, name);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, fileName), bytes);
            return new RepositoryMaterialFile(directory, name + "/" + fileName);
        }

        internal string? CreateSymbolicLink(string target)
        {
            var path = Path.Combine(_directory, "link.txt");
            try
            {
                File.CreateSymbolicLink(path, target);
                return Path.GetFileName(_directory) + "/link.txt";
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (PlatformNotSupportedException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Repository root was not found from test working directory.");
    }
}
