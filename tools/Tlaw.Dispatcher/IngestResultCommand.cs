using System.Text;
using System.Text.Json;
using Tlaw.AgentProtocol;

namespace Tlaw.Dispatcher;

public static class IngestResultCommand
{
    public static int Run(string[] args, TextWriter standardOutput, TextWriter standardError)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        try
        {
            var options = IngestionOptions.Parse(args);
            var registry = PacketSchemaRegistry.Load(Path.Combine(TaskPacketCommand.FindRepositoryRoot(), "docs", "agent", "schemas"));
            var task = ReadClaimedTask(options.TaskPath, registry);
            var result = ReadResult(options.ResultPath, registry);
            if (!string.Equals(result.RequiredString("task_id"), task.TaskId, StringComparison.Ordinal))
            {
                throw new IngestResultCommandException("Result task_id does not exactly match the claimed task packet.");
            }

            var inspection = FileLeaseStore.InspectReadOnly(options.LeaseStorePath, task.TaskId, new SystemLeaseClock());
            RequireMatchingActiveLease(task, inspection);

            var projection = ResultProjector.Project(result);
            TaskPacketCommand.WriteAtomically(options.OutputPath, IngestionJson.Write(task, result, projection));
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

    private static ProtocolPacket ReadResult(string resultPath, PacketSchemaRegistry registry)
    {
        var validation = PacketValidator.Validate(TaskPacketCommand.ReadUtf8(resultPath), registry);
        if (!validation.IsValid)
        {
            throw new IngestResultCommandException(DescribeValidationFailure("Input result packet", validation));
        }

        var packet = validation.Packet!;
        if (!string.Equals(packet.Schema, "tlaw.agent-result/v1", StringComparison.Ordinal))
        {
            throw new IngestResultCommandException("Input result packet must use schema tlaw.agent-result/v1.");
        }

        return packet;
    }

    private static void RequireMatchingActiveLease(TaskV2Packet task, LocalLeaseInspection inspection)
    {
        if (inspection.Status == LocalLeaseStatus.Missing || inspection.Lease is null)
        {
            throw new IngestResultCommandException($"No lease exists for claimed task '{task.TaskId}'.");
        }

        if (inspection.Status == LocalLeaseStatus.Expired)
        {
            throw new IngestResultCommandException($"Lease for claimed task '{task.TaskId}' has expired.");
        }

        var lease = inspection.Lease;
        if (!string.Equals(lease.TaskId, task.TaskId, StringComparison.Ordinal))
        {
            throw new IngestResultCommandException("Active lease task identity does not match the claimed task packet.");
        }

        if (!string.Equals(lease.ClaimedBy, task.ClaimedBy, StringComparison.Ordinal))
        {
            throw new IngestResultCommandException("Active lease claimed_by does not match the claimed task packet.");
        }

        if (!string.Equals(lease.ClaimId, task.ClaimId, StringComparison.Ordinal))
        {
            throw new IngestResultCommandException("Active lease claim_id does not match the claimed task packet.");
        }
    }

    private static string DescribeValidationFailure(string subject, PacketValidationResult validation)
    {
        var diagnostic = validation.Diagnostics.FirstOrDefault();
        return diagnostic is null
            ? $"{subject} was rejected by the protocol validator."
            : $"{subject} was rejected: {diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}";
    }
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

internal static class IngestionJson
{
    internal static string Write(TaskV2Packet task, ProtocolPacket result, string projection)
    {
        var output = new StringBuilder();
        AppendLine(output, "{");
        AppendString(output, "schema", "tlaw.dispatcher-ingestion/v1", trailingComma: true);
        AppendString(output, "task_id", task.TaskId, trailingComma: true);
        AppendString(output, "claimed_by", task.ClaimedBy, trailingComma: true);
        AppendString(output, "claim_id", task.ClaimId, trailingComma: true);
        AppendString(output, "result_status", result.RequiredString("status"), trailingComma: true);
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
