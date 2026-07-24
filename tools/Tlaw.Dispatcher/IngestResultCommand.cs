using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tlaw.AgentProtocol;

namespace Tlaw.Dispatcher;

public static class IngestResultCommand
{
    public static int Run(string[] args, TextWriter standardOutput, TextWriter standardError)
        => Run(args, standardOutput, standardError, new SystemLeaseClock(), beforePublication: null);

    internal static int RunForTesting(string[] args, TextWriter standardOutput, TextWriter standardError, ILeaseClock clock, Action? beforePublication)
        => Run(args, standardOutput, standardError, clock, beforePublication);

    private static int Run(string[] args, TextWriter standardOutput, TextWriter standardError, ILeaseClock clock, Action? beforePublication)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(clock);

        try
        {
            var options = IngestionOptions.Parse(args);
            IngestionPathGuard.Validate(options);
            var registry = PacketSchemaRegistry.Load(Path.Combine(TaskPacketCommand.FindRepositoryRoot(), "docs", "agent", "schemas"));
            var task = ReadClaimedTask(options.TaskPath, registry);
            var result = ReadResult(options.ResultPath, registry);
            if (!string.Equals(result.Packet.RequiredString("task_id"), task.TaskId, StringComparison.Ordinal))
            {
                throw new IngestResultCommandException("Result task_id does not exactly match the claimed task packet.");
            }

            var resultHash = Convert.ToHexStringLower(SHA256.HashData(result.Bytes));
            var projection = FileLeaseStore.WithActiveLeaseGuard(
                options.LeaseStorePath,
                task.TaskId,
                task.ClaimedBy,
                task.ClaimId,
                clock,
                recheck =>
                {
                    var rendered = ResultProjector.Project(result.Packet);
                    beforePublication?.Invoke();
                    recheck();
                    TaskPacketCommand.WriteAtomically(options.OutputPath, IngestionJson.Write(task, result.Packet, resultHash, rendered));
                    return rendered;
                });
            standardOutput.WriteLine(projection);
            return 0;
        }
        catch (IngestResultCommandException exception)
        {
            standardError.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
        catch (Exception exception) when (exception is LeaseStoreException or IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or DecoderFallbackException or InvalidDataException or JsonException)
        {
            standardError.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
        catch (Exception)
        {
            standardError.WriteLine("FAIL: result ingestion failed unexpectedly.");
            return 1;
        }
    }

    private static TaskV2Packet ReadClaimedTask(string taskPath, PacketSchemaRegistry registry)
    {
        var validation = PacketValidator.Validate(TaskPacketCommand.ReadUtf8(taskPath), registry);
        if (!validation.IsValid)
        {
            throw new IngestResultCommandException(DescribeValidationFailure("Input task packet", validation));
        }

        var packet = validation.Packet!;
        if (!string.Equals(packet.Schema, "tlaw.agent-task/v2", StringComparison.Ordinal))
        {
            throw new IngestResultCommandException("Input task packet must use schema tlaw.agent-task/v2.");
        }

        var task = TaskV2Packet.From(packet);
        if (!task.IsClaimed)
        {
            throw new IngestResultCommandException("Input task packet must be fully claimed before result ingestion.");
        }

        return task;
    }

    private static ValidatedResult ReadResult(string resultPath, PacketSchemaRegistry registry)
    {
        var bytes = File.ReadAllBytes(resultPath);
        var yaml = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        var validation = PacketValidator.Validate(yaml, registry);
        if (!validation.IsValid)
        {
            throw new IngestResultCommandException(DescribeValidationFailure("Input result packet", validation));
        }

        var packet = validation.Packet!;
        if (!string.Equals(packet.Schema, "tlaw.agent-result/v1", StringComparison.Ordinal))
        {
            throw new IngestResultCommandException("Input result packet must use schema tlaw.agent-result/v1.");
        }

        return new ValidatedResult(packet, bytes);
    }

    private static string DescribeValidationFailure(string subject, PacketValidationResult validation)
    {
        var diagnostic = validation.Diagnostics.FirstOrDefault();
        return diagnostic is null
            ? $"{subject} was rejected by the protocol validator."
            : $"{subject} was rejected: {diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}";
    }

    private sealed record ValidatedResult(ProtocolPacket Packet, byte[] Bytes);
}

public sealed class IngestResultCommandException(string message) : Exception(message);

internal sealed record IngestionOptions(string TaskPath, string ResultPath, string LeaseStorePath, string OutputPath)
{
    internal static IngestionOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || !string.Equals(args[0], "ingest-result", StringComparison.Ordinal))
        {
            throw new IngestResultCommandException("Result ingestion command must begin with the exact command name 'ingest-result'.");
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Count; index += 2)
        {
            if (index + 1 >= args.Count)
            {
                throw new IngestResultCommandException("Result ingestion command received an incomplete option set.");
            }

            var name = args[index];
            var value = args[index + 1];
            if (name is not ("--task" or "--result" or "--lease-store" or "--output") || !options.TryAdd(name, value))
            {
                throw new IngestResultCommandException("Result ingestion command received an unknown or duplicate option.");
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new IngestResultCommandException("Result ingestion command received an empty option value.");
            }
        }

        if (!options.TryGetValue("--task", out var task) ||
            !options.TryGetValue("--result", out var result) ||
            !options.TryGetValue("--lease-store", out var leaseStore) ||
            !options.TryGetValue("--output", out var output) ||
            options.Count != 4)
        {
            throw new IngestResultCommandException("Result ingestion command requires --task, --result, --lease-store, and --output exactly once.");
        }

        if (!Path.IsPathFullyQualified(leaseStore))
        {
            throw new IngestResultCommandException("Result ingestion lease-store path must be absolute.");
        }

        return new IngestionOptions(Path.GetFullPath(task), Path.GetFullPath(result), Path.GetFullPath(leaseStore), Path.GetFullPath(output));
    }
}

internal static class IngestionPathGuard
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    internal static void Validate(IngestionOptions options)
    {
        ValidateOutput(options.OutputPath, options.LeaseStorePath, options.TaskPath, options.ResultPath);
    }

    internal static void ValidateOutput(string outputPath, string leaseStorePath, params string[] protectedInputs)
    {
        var leaseStore = Resolve(leaseStorePath);
        var output = Resolve(outputPath);
        if (protectedInputs.Any(path => SamePath(output, Resolve(path))))
        {
            throw new IngestResultCommandException("Command output must not alias a protected input.");
        }

        if (IsAtOrWithin(output, leaseStore))
        {
            throw new IngestResultCommandException("Command output must not be inside the lease store.");
        }
    }

    internal static void ValidateOutputWithoutLease(string outputPath, params string[] protectedInputs)
    {
        var output = Resolve(outputPath);
        if (protectedInputs.Any(path => SamePath(output, Resolve(path))))
        {
            throw new IngestResultCommandException("Command output must not alias a protected input.");
        }
    }

    internal static string Resolve(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath) ?? throw new IngestResultCommandException("Result ingestion path has no filesystem root.");
        var segments = fullPath[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        for (var index = 0; index < segments.Length; index++)
        {
            var candidate = Path.Combine(current, segments[index]);
            if (!TryGetAttributes(candidate, out var attributes))
            {
                current = candidate;
                for (var remaining = index + 1; remaining < segments.Length; remaining++)
                {
                    current = Path.Combine(current, segments[remaining]);
                }

                break;
            }

            if ((attributes & FileAttributes.ReparsePoint) == 0)
            {
                current = candidate;
                continue;
            }

            try
            {
                FileSystemInfo link = (attributes & FileAttributes.Directory) != 0
                    ? new DirectoryInfo(candidate)
                    : new FileInfo(candidate);
                var target = link.ResolveLinkTarget(returnFinalTarget: true)
                    ?? throw new IngestResultCommandException("Result ingestion path contains an unresolved symbolic link or junction.");
                current = target.FullName;
            }
            catch (IngestResultCommandException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                throw new IngestResultCommandException("Result ingestion path cannot be resolved safely.");
            }
        }

        return TrimTrailingSeparators(Path.GetFullPath(current));
    }

    private static bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            attributes = default;
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IngestResultCommandException("Result ingestion path cannot be inspected safely.");
        }
    }

    internal static StringComparer PathComparer => PathComparison == StringComparison.OrdinalIgnoreCase
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    internal static bool SamePath(string left, string right) => string.Equals(left, right, PathComparison);

    internal static bool IsAtOrWithin(string path, string root)
    {
        if (SamePath(path, root))
        {
            return true;
        }

        var rootedPrefix = root.EndsWith(Path.DirectorySeparatorChar) || root.EndsWith(Path.AltDirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return path.StartsWith(rootedPrefix, PathComparison);
    }

    private static string TrimTrailingSeparators(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        return path.Length > root.Length
            ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : path;
    }
}

internal static class IngestionJson
{
    internal static string Write(TaskV2Packet task, ProtocolPacket result, string resultSha256, string projection)
    {
        var output = new StringBuilder();
        AppendLine(output, "{");
        AppendString(output, "schema", "tlaw.dispatcher-ingestion/v1", trailingComma: true);
        AppendString(output, "task_id", task.TaskId, trailingComma: true);
        AppendString(output, "claimed_by", task.ClaimedBy, trailingComma: true);
        AppendString(output, "claim_id", task.ClaimId, trailingComma: true);
        AppendString(output, "result_status", result.RequiredString("status"), trailingComma: true);
        AppendString(output, "result_sha256", resultSha256, trailingComma: true);
        output.Append("  \"human_required\": ").Append(result.RequiredBoolean("human", "required") ? "true" : "false");
        AppendLine(output, ",");
        AppendString(output, "projection", projection);
        AppendLine(output, "}");
        return output.ToString();
    }

    private static void AppendString(StringBuilder output, string name, string value, bool trailingComma = false)
    {
        output.Append("  ")
            .Append(JsonSerializer.Serialize(name))
            .Append(": ")
            .Append(JsonSerializer.Serialize(value));
        AppendLine(output, trailingComma ? "," : string.Empty);
    }

    private static void AppendLine(StringBuilder output, string value = "") => output.Append(value).Append('\n');
}
