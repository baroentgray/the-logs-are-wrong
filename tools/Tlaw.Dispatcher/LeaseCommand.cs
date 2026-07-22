using System.Globalization;
using System.Text;
using Tlaw.AgentProtocol;

namespace Tlaw.Dispatcher;

public static class LeaseCommand
{
    public static int Run(string[] args, TextWriter standardOutput, TextWriter standardError)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        if (args.Length < 2 || !string.Equals(args[0], "lease", StringComparison.Ordinal))
        {
            WriteUsage(standardError);
            return 2;
        }

        try
        {
            return args[1] switch
            {
                "acquire" => Acquire(args[2..], standardOutput),
                "status" => Status(args[2..], standardOutput),
                "release" => Release(args[2..], standardOutput),
                _ => WriteUsageAndReturn(standardError)
            };
        }
        catch (LeaseCommandException exception)
        {
            standardError.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
        catch (Exception exception) when (exception is LeaseStoreException or IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or DecoderFallbackException or InvalidDataException)
        {
            standardError.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
        catch (Exception)
        {
            standardError.WriteLine("FAIL: lease operation failed unexpectedly.");
            return 1;
        }
    }

    private static int Acquire(IReadOnlyList<string> arguments, TextWriter standardOutput)
    {
        var options = ParseOptions(arguments, ["--task", "--store", "--executor", "--ttl", "--output"]);
        var taskPath = Path.GetFullPath(options["--task"]);
        var outputPath = Path.GetFullPath(options["--output"]);
        var executor = options["--executor"];
        var ttl = ParseTtl(options["--ttl"]);
        var registry = PacketSchemaRegistry.Load(Path.Combine(TaskPacketCommand.FindRepositoryRoot(), "docs", "agent", "schemas"));
        var validation = PacketValidator.Validate(TaskPacketCommand.ReadUtf8(taskPath), registry);
        if (!validation.IsValid)
        {
            throw new LeaseCommandException(DescribeValidationFailure("Input task packet", validation));
        }

        var packet = validation.Packet!;
        if (!string.Equals(packet.Schema, "tlaw.agent-task/v2", StringComparison.Ordinal))
        {
            throw new LeaseCommandException("Input task packet must use schema tlaw.agent-task/v2.");
        }

        var task = TaskV2Packet.From(packet);
        if (!task.IsUnclaimed)
        {
            throw new LeaseCommandException("Input task packet must be fully unclaimed before lease acquisition.");
        }

        if (!task.EligibleAgents.Contains(executor, StringComparer.Ordinal))
        {
            throw new LeaseCommandException($"Executor '{executor}' is not eligible for task '{task.TaskId}'.");
        }

        var store = new FileLeaseStore(options["--store"], new SystemLeaseClock());
        var lease = store.Acquire(task.TaskId, executor, ttl);
        try
        {
            var claimed = task with
            {
                ClaimedBy = lease.ClaimedBy,
                ClaimId = lease.ClaimId,
                ClaimStartedAt = FileLeaseStore.FormatCanonicalTimestamp(lease.ClaimStartedAt),
                ClaimExpiresAt = FileLeaseStore.FormatCanonicalTimestamp(lease.ClaimExpiresAt)
            };
            var yaml = TaskPacketGenerator.Generate(claimed, registry);
            TaskPacketCommand.WriteAtomically(outputPath, yaml);
        }
        catch (Exception publicationException)
        {
            try
            {
                store.Release(task.TaskId, lease.ClaimId, LeaseReleaseReason.Error);
            }
            catch (Exception rollbackException)
            {
                throw new LeaseCommandException(
                    $"Claimed packet publication failed after reserving task '{task.TaskId}'. The lease remains active with claim_id '{lease.ClaimId}'. Recover with: lease release --task-id {task.TaskId} --store {options["--store"]} --claim-id {lease.ClaimId} --reason error. Publication: {publicationException.Message}; rollback: {rollbackException.Message}",
                    publicationException);
            }

            throw new LeaseCommandException($"Claimed packet publication failed and the exact lease was rolled back: {publicationException.Message}", publicationException);
        }

        standardOutput.WriteLine($"PASS task_id={task.TaskId} claim_id={lease.ClaimId} claim_expires_at={FileLeaseStore.FormatCanonicalTimestamp(lease.ClaimExpiresAt)}");
        return 0;
    }

    private static int Status(IReadOnlyList<string> arguments, TextWriter standardOutput)
    {
        var options = ParseOptions(arguments, ["--task-id", "--store"]);
        var inspection = new FileLeaseStore(options["--store"], new SystemLeaseClock()).Inspect(options["--task-id"]);
        if (inspection.Lease is null)
        {
            standardOutput.WriteLine("MISSING");
            return 0;
        }

        standardOutput.WriteLine($"{inspection.Status.ToString().ToUpperInvariant()} task_id={inspection.Lease.TaskId} claim_id={inspection.Lease.ClaimId} claimed_by={inspection.Lease.ClaimedBy} claim_started_at={FileLeaseStore.FormatCanonicalTimestamp(inspection.Lease.ClaimStartedAt)} claim_expires_at={FileLeaseStore.FormatCanonicalTimestamp(inspection.Lease.ClaimExpiresAt)}");
        return 0;
    }

    private static int Release(IReadOnlyList<string> arguments, TextWriter standardOutput)
    {
        var options = ParseOptions(arguments, ["--task-id", "--store", "--claim-id", "--reason"]);
        if (!TryParseReason(options["--reason"], out var reason))
        {
            throw new LeaseCommandException("Lease release reason must be one of: completion, error, timeout, quota_exhaustion, manual_cancel.");
        }

        new FileLeaseStore(options["--store"], new SystemLeaseClock()).Release(options["--task-id"], options["--claim-id"], reason);
        standardOutput.WriteLine("PASS");
        return 0;
    }

    private static IReadOnlyDictionary<string, string> ParseOptions(IReadOnlyList<string> arguments, IReadOnlyList<string> expected)
    {
        if (arguments.Count != expected.Count * 2)
        {
            throw new LeaseCommandException("Lease command received an incomplete or unexpected option set.");
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Count; index += 2)
        {
            var name = arguments[index];
            var value = arguments[index + 1];
            if (!expected.Contains(name, StringComparer.Ordinal) || !options.TryAdd(name, value) || string.IsNullOrWhiteSpace(value))
            {
                throw new LeaseCommandException("Lease command received an incomplete or unexpected option set.");
            }
        }

        return options;
    }

    private static TimeSpan ParseTtl(string value)
    {
        if (!TimeSpan.TryParseExact(value, "c", CultureInfo.InvariantCulture, out var ttl) || ttl <= TimeSpan.Zero)
        {
            throw new LeaseCommandException("Lease TTL must be a strictly positive invariant duration such as 00:05:00.");
        }

        return ttl;
    }

    private static bool TryParseReason(string value, out LeaseReleaseReason reason)
    {
        reason = value switch
        {
            "completion" => LeaseReleaseReason.Completion,
            "error" => LeaseReleaseReason.Error,
            "timeout" => LeaseReleaseReason.Timeout,
            "quota_exhaustion" => LeaseReleaseReason.QuotaExhaustion,
            "manual_cancel" => LeaseReleaseReason.ManualCancel,
            _ => default
        };

        return value is "completion" or "error" or "timeout" or "quota_exhaustion" or "manual_cancel";
    }

    private static string DescribeValidationFailure(string subject, PacketValidationResult validation)
    {
        var diagnostic = validation.Diagnostics.FirstOrDefault();
        return diagnostic is null
            ? $"{subject} was rejected by the protocol validator."
            : $"{subject} was rejected: {diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}";
    }

    private static int WriteUsageAndReturn(TextWriter standardError)
    {
        WriteUsage(standardError);
        return 2;
    }

    private static void WriteUsage(TextWriter standardError)
    {
        standardError.WriteLine("Usage: tlaw-dispatcher lease acquire --task <unclaimed-task-v2.yaml> --store <absolute-lease-store> --executor <agent> --ttl <duration> --output <claimed-task-v2.yaml>");
        standardError.WriteLine("   or: tlaw-dispatcher lease status --task-id <task-id> --store <absolute-lease-store>");
        standardError.WriteLine("   or: tlaw-dispatcher lease release --task-id <task-id> --store <absolute-lease-store> --claim-id <claim-id> --reason <completion|error|timeout|quota_exhaustion|manual_cancel>");
    }
}

public sealed class LeaseCommandException(string message, Exception? innerException = null) : Exception(message, innerException);
