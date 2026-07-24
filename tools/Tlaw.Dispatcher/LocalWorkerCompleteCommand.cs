using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tlaw.AgentProtocol;

namespace Tlaw.Dispatcher;

/// <summary>
/// Trusted host-side correlation of one untrusted local-worker artifact into an existing result/v1 envelope.
/// It deliberately never invokes a provider, changes a lease, or contacts a remote system.
/// </summary>
public static class LocalWorkerCompleteCommand
{
    public static int Run(string[] args, TextWriter standardOutput, TextWriter standardError) => Run(args, standardOutput, standardError, new SystemLeaseClock(), null);

    internal static int RunForTesting(string[] args, TextWriter standardOutput, TextWriter standardError, ILeaseClock clock, LocalWorkerCompleteTestHooks? hooks) => Run(args, standardOutput, standardError, clock, hooks);

    private static int Run(string[] args, TextWriter standardOutput, TextWriter standardError, ILeaseClock clock, LocalWorkerCompleteTestHooks? hooks)
    {
        var published = false;
        try
        {
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(standardOutput);
            ArgumentNullException.ThrowIfNull(standardError);
            ArgumentNullException.ThrowIfNull(clock);
            var options = LocalWorkerCompleteOptions.Parse(args);
            LocalWorkerPathGuard.ValidateComplete(options.OutputPath, options.LeaseStorePath, options.TaskPath, options.ArtifactPath);
            var registry = PacketSchemaRegistry.Load(Path.Combine(TaskPacketCommand.FindRepositoryRoot(), "docs", "agent", "schemas"));
            var task = LocalWorkerCommand.ReadLocalTask(options.TaskPath);

            FileLeaseStore.WithExactActiveLeaseGuard(
                options.LeaseStorePath,
                task.TaskId,
                task.ClaimedBy,
                task.ClaimId,
                task.ClaimStartedAt,
                task.ClaimExpiresAt,
                clock,
                recheck =>
                {
                    recheck();
                    var artifactBytes = File.ReadAllBytes(options.ArtifactPath);
                    var artifact = LocalWorkerArtifact.Parse(LocalWorkerJson.DecodeStrictUtf8(artifactBytes, "Worker artifact"));
                    VerifyArtifact(task, artifact);
                    var result = LocalWorkerCompleteResult.Render(task.TaskId, artifact, Convert.ToHexStringLower(SHA256.HashData(artifactBytes)));
                    VerifyResult(result, registry);
                    hooks?.BeforePublication?.Invoke();
                    recheck();
                    TaskPacketCommand.WriteAtomically(options.OutputPath, result);
                    published = true;
                    return 0;
                });

            try
            {
                standardOutput.WriteLine("LOCAL WORKER: result published");
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException or NotSupportedException)
            {
                standardError.WriteLine("FAIL: result was already published.");
                return 1;
            }

            return 0;
        }
        catch (Exception exception) when (exception is LocalWorkerCommandException or LeaseStoreException or IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or DecoderFallbackException or InvalidDataException or JsonException)
        {
            standardError.WriteLine(published ? "FAIL: result was already published." : $"FAIL: {LocalWorkerDiagnostics.Sanitize(exception)}");
            return 1;
        }
        catch (Exception)
        {
            standardError.WriteLine(published ? "FAIL: result was already published." : "FAIL: local worker completion failed unexpectedly.");
            return 1;
        }
    }

    private static void VerifyArtifact(TaskV2Packet task, LocalWorkerArtifactPacket artifact)
    {
        if (!string.Equals(artifact.TaskId, task.TaskId, StringComparison.Ordinal) ||
            !string.Equals(artifact.ClaimedBy, task.ClaimedBy, StringComparison.Ordinal) ||
            !string.Equals(artifact.ClaimId, task.ClaimId, StringComparison.Ordinal) ||
            !string.Equals(artifact.ClaimStartedAt, task.ClaimStartedAt, StringComparison.Ordinal) ||
            !string.Equals(artifact.ClaimExpiresAt, task.ClaimExpiresAt, StringComparison.Ordinal) ||
            !string.Equals(artifact.ClaimedBy, "local", StringComparison.Ordinal))
        {
            throw new LocalWorkerCommandException("Worker artifact identity does not exactly match the claimed local task packet.");
        }
    }

    private static void VerifyResult(string result, PacketSchemaRegistry registry)
    {
        var validation = PacketValidator.Validate(result, registry);
        if (!validation.IsValid || validation.Packet is null || !string.Equals(validation.Packet.Schema, "tlaw.agent-result/v1", StringComparison.Ordinal) ||
            !string.Equals(validation.Packet.RequiredString("status"), "success", StringComparison.Ordinal) || validation.Packet.RequiredBoolean("human", "required"))
        {
            throw new LocalWorkerCommandException("Correlated worker result did not validate as a non-human success result/v1 packet.");
        }
    }
}

internal sealed record LocalWorkerCompleteTestHooks(Action? BeforePublication = null);

internal sealed record LocalWorkerCompleteOptions(string TaskPath, string LeaseStorePath, string ArtifactPath, string OutputPath)
{
    private static readonly string[] RequiredOptions = ["--task", "--lease-store", "--artifact", "--output"];

    internal static LocalWorkerCompleteOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count != 10 || !string.Equals(args[0], "local-worker", StringComparison.Ordinal) || !string.Equals(args[1], "complete", StringComparison.Ordinal))
        {
            throw new LocalWorkerCommandException("Usage: tlaw local-worker complete --task <claimed-task-v2.yaml> --lease-store <absolute-path> --artifact <local-worker-artifact-v1.json> --output <agent-result-v1.yaml>.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 2; index < args.Count; index += 2)
        {
            if (index + 1 >= args.Count || !RequiredOptions.Contains(args[index], StringComparer.Ordinal) || string.IsNullOrWhiteSpace(args[index + 1]) || !values.TryAdd(args[index], args[index + 1]))
            {
                throw new LocalWorkerCommandException("Local worker completion accepts exactly its closed named option set.");
            }
        }

        if (values.Count != RequiredOptions.Length || RequiredOptions.Any(option => !values.ContainsKey(option)) || !Path.IsPathFullyQualified(values["--lease-store"]))
        {
            throw new LocalWorkerCommandException("Local worker completion requires each closed option exactly once; lease-store must be absolute.");
        }

        return new LocalWorkerCompleteOptions(Path.GetFullPath(values["--task"]), Path.GetFullPath(values["--lease-store"]), Path.GetFullPath(values["--artifact"]), Path.GetFullPath(values["--output"]));
    }
}

internal static class LocalWorkerCompleteResult
{
    private const string Summary = "Validated local read-only analysis artifact produced.";

    internal static string Render(string taskId, LocalWorkerArtifactPacket artifact, string artifactSha256)
    {
        var reference = $"tlaw.local-worker-artifact/v1; sha256={artifactSha256}; operation={artifact.Operation}; model={artifact.Model.Key}; non_authoritative=true; commands_executed=false";
        var output = new StringBuilder();
        AppendString(output, "schema", "tlaw.agent-result/v1");
        AppendString(output, "task_id", taskId);
        AppendString(output, "status", "success");
        AppendString(output, "human_summary", Summary);
        output.Append("evidence:\n");
        output.Append("  - kind: file\n");
        output.Append("    reference: ").Append(JsonSerializer.Serialize(reference)).Append('\n');
        output.Append("human:\n");
        output.Append("  required: false\n");
        output.Append("  question: ").Append(JsonSerializer.Serialize("No human decision is required.")).Append('\n');
        output.Append("  safe_options: []\n");
        return output.ToString();
    }

    private static void AppendString(StringBuilder output, string name, string value) => output.Append(name).Append(": ").Append(JsonSerializer.Serialize(value)).Append('\n');
}
