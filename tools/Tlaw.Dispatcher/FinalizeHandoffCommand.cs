using System.Security.Cryptography;
using System.Text;
using Tlaw.AgentProtocol;

namespace Tlaw.Dispatcher;

public static class FinalizeHandoffCommand
{
    public static int Run(string[] args, TextWriter standardOutput, TextWriter standardError) =>
        Run(args, standardOutput, standardError, new SystemLeaseClock());

    internal static int RunForTesting(string[] args, TextWriter standardOutput, TextWriter standardError, ILeaseClock clock)
    {
        return Run(args, standardOutput, standardError, clock);
    }

    private static int Run(string[] args, TextWriter standardOutput, TextWriter standardError, ILeaseClock clock)
    {
        try
        {
            var options = FinalizeHandoffOptions.Parse(args);
            IngestionPathGuard.ValidateOutput(options.OutputPath, options.LeaseStorePath, options.TaskPath, options.HandoffPath, options.IngestionPath);

            var registry = PacketSchemaRegistry.Load(Path.Combine(TaskPacketCommand.FindRepositoryRoot(), "docs", "agent", "schemas"));
            var task = ReadTask(options.TaskPath, registry);
            var handoffBytes = File.ReadAllBytes(options.HandoffPath);
            var handoff = ReadHandoff(handoffBytes, registry);
            var ingestion = HandoffIngestionRecord.Parse(options.IngestionPath);
            VerifyCorrelation(task, handoff, handoffBytes, ingestion);

            var continuation = task with
            {
                ClaimedBy = "unclaimed",
                ClaimId = "unclaimed",
                ClaimStartedAt = "unclaimed",
                ClaimExpiresAt = "unclaimed"
            };
            var continuationYaml = TaskPacketGenerator.Generate(continuation, registry);
            var continuationValidation = PacketValidator.Validate(continuationYaml, registry);
            if (!continuationValidation.IsValid || continuationValidation.Packet is null || !string.Equals(continuationValidation.Packet.Schema, "tlaw.agent-task/v2", StringComparison.Ordinal) || !TaskV2Packet.From(continuationValidation.Packet).IsUnclaimed)
            {
                throw new HandoffFinalizationException("The continuation task could not be rendered as a valid unclaimed task/v2 packet.");
            }

            var expectedStatus = ingestion.ReleaseReason == "timeout" ? LocalLeaseStatus.Expired : LocalLeaseStatus.Active;
            var released = false;
            try
            {
                FileLeaseStore.WithMatchingLeaseStateRelease(
                    options.LeaseStorePath,
                    task.TaskId,
                    task.ClaimedBy,
                    task.ClaimId,
                    expectedStatus,
                    clock,
                    () =>
                    {
                        released = true;
                        TaskPacketCommand.WriteAtomically(options.OutputPath, continuationYaml);
                        return 0;
                    });
            }
            catch (Exception exception) when (released)
            {
                throw new HandoffFinalizationPublicationException("the lease was already released but the continuation task was not published", exception);
            }

            try
            {
                standardOutput.WriteLine($"HANDOFF FINALIZED: {ingestion.Decision}");
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException or NotSupportedException)
            {
                standardError.WriteLine("FAIL: the lease was already released and the continuation task was already published.");
                return 1;
            }

            return 0;
        }
        catch (HandoffFinalizationPublicationException exception)
        {
            standardError.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
        catch (Exception exception) when (exception is HandoffFinalizationException or HandoffIngestionException or IngestResultCommandException or LeaseStoreException or IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or DecoderFallbackException or InvalidDataException or System.Text.Json.JsonException)
        {
            standardError.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
        catch (Exception)
        {
            standardError.WriteLine("FAIL: handoff finalization failed unexpectedly.");
            return 1;
        }
    }

    private static TaskV2Packet ReadTask(string path, PacketSchemaRegistry registry)
    {
        var validation = PacketValidator.Validate(TaskPacketCommand.ReadUtf8(path), registry);
        if (!validation.IsValid || validation.Packet is null || !string.Equals(validation.Packet.Schema, "tlaw.agent-task/v2", StringComparison.Ordinal))
        {
            throw new HandoffFinalizationException("Input task packet must be a valid tlaw.agent-task/v2 packet.");
        }

        var task = TaskV2Packet.From(validation.Packet);
        if (!task.IsClaimed)
        {
            throw new HandoffFinalizationException("Input task packet must be fully claimed before handoff finalization.");
        }

        return task;
    }

    private static ProtocolPacket ReadHandoff(byte[] handoffBytes, PacketSchemaRegistry registry)
    {
        var handoffYaml = new UTF8Encoding(false, true).GetString(handoffBytes);
        var validation = PacketValidator.Validate(handoffYaml, registry);
        if (!validation.IsValid || validation.Packet is null || !string.Equals(validation.Packet.Schema, "tlaw.agent-handoff/v2", StringComparison.Ordinal))
        {
            throw new HandoffFinalizationException("Input handoff packet must be a valid tlaw.agent-handoff/v2 packet.");
        }

        return validation.Packet;
    }

    private static void VerifyCorrelation(TaskV2Packet task, ProtocolPacket handoff, byte[] handoffBytes, HandoffIngestionRecord ingestion)
    {
        var exactHash = Convert.ToHexStringLower(SHA256.HashData(handoffBytes));
        if (!string.Equals(task.TaskId, handoff.RequiredString("task_id"), StringComparison.Ordinal) ||
            !string.Equals(task.TaskId, ingestion.TaskId, StringComparison.Ordinal) ||
            !string.Equals(task.SourceId, handoff.RequiredString("source_id"), StringComparison.Ordinal) ||
            !string.Equals(task.SourceId, ingestion.SourceId, StringComparison.Ordinal) ||
            !string.Equals(task.ClaimedBy, handoff.RequiredString("claimed_by"), StringComparison.Ordinal) ||
            !string.Equals(task.ClaimedBy, ingestion.ClaimedBy, StringComparison.Ordinal) ||
            !string.Equals(task.ClaimId, handoff.RequiredString("claim_id"), StringComparison.Ordinal) ||
            !string.Equals(task.ClaimId, ingestion.ClaimId, StringComparison.Ordinal) ||
            !string.Equals(task.BaseSha, handoff.RequiredString("base_sha"), StringComparison.Ordinal) ||
            !string.Equals(task.Worktree, handoff.RequiredString("branch"), StringComparison.Ordinal) ||
            !string.Equals(task.Worktree, ingestion.Branch, StringComparison.Ordinal) ||
            !string.Equals(handoff.RequiredString("head_sha"), ingestion.HeadSha, StringComparison.Ordinal) ||
            !string.Equals(handoff.RequiredString("status"), ingestion.HandoffStatus, StringComparison.Ordinal) ||
            !string.Equals(exactHash, ingestion.HandoffSha256, StringComparison.Ordinal) ||
            !string.Equals(ingestion.LeaseAction, "release_required", StringComparison.Ordinal) ||
            !string.Equals(ingestion.NextState, "todo", StringComparison.Ordinal))
        {
            throw new HandoffFinalizationException("Task, handoff, and ingestion evidence must agree exactly.");
        }
    }
}

public sealed class HandoffFinalizationException(string message) : Exception(message);
public sealed class HandoffFinalizationPublicationException(string message, Exception innerException) : Exception(message, innerException);

internal sealed record FinalizeHandoffOptions(string TaskPath, string HandoffPath, string IngestionPath, string LeaseStorePath, string OutputPath)
{
    internal static FinalizeHandoffOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count != 11 || !string.Equals(args[0], "finalize-handoff", StringComparison.Ordinal))
        {
            throw new HandoffFinalizationException("Handoff finalization requires exactly five named options.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Count; index += 2)
        {
            if (!values.TryAdd(args[index], args[index + 1]) || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                throw new HandoffFinalizationException("Handoff finalization received an unknown, duplicate, or empty option.");
            }
        }

        if (!values.TryGetValue("--task", out var task) ||
            !values.TryGetValue("--handoff", out var handoff) ||
            !values.TryGetValue("--ingestion", out var ingestion) ||
            !values.TryGetValue("--lease-store", out var leaseStore) ||
            !values.TryGetValue("--output", out var output) ||
            values.Count != 5 ||
            !Path.IsPathFullyQualified(leaseStore))
        {
            throw new HandoffFinalizationException("Handoff finalization requires --task, --handoff, --ingestion, --lease-store, and --output exactly once; lease-store must be absolute.");
        }

        return new FinalizeHandoffOptions(Path.GetFullPath(task), Path.GetFullPath(handoff), Path.GetFullPath(ingestion), Path.GetFullPath(leaseStore), Path.GetFullPath(output));
    }
}
