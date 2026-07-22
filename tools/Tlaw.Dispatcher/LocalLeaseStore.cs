using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tlaw.Dispatcher;

public interface ILeaseClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemLeaseClock : ILeaseClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public enum LocalLeaseStatus
{
    Missing,
    Active,
    Expired
}

public enum LeaseReleaseReason
{
    Completion,
    Error,
    Timeout,
    QuotaExhaustion,
    ManualCancel
}

public sealed record LocalLease(
    string TaskId,
    string ClaimedBy,
    string ClaimId,
    DateTimeOffset ClaimStartedAt,
    DateTimeOffset ClaimExpiresAt);

public sealed record LocalLeaseInspection(LocalLeaseStatus Status, LocalLease? Lease);

public class LeaseStoreException(string message, Exception? innerException = null) : Exception(message, innerException);

public sealed class LeaseConflictException(string message, Exception? innerException = null) : LeaseStoreException(message, innerException);

public sealed class LeaseGuardException(string message) : LeaseStoreException(message);

public sealed class FileLeaseStore
{
    private const string LeaseSchema = "tlaw.local-lease/v1";
    private readonly string _storePath;
    private readonly ILeaseClock _clock;

    public FileLeaseStore(string storePath, ILeaseClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storePath);
        ArgumentNullException.ThrowIfNull(clock);
        if (!Path.IsPathFullyQualified(storePath))
        {
            throw new ArgumentException("The lease store path must be absolute.", nameof(storePath));
        }

        _storePath = Path.GetFullPath(storePath);
        _clock = clock;
        Directory.CreateDirectory(LeasesDirectory);
        Directory.CreateDirectory(LocksDirectory);
    }

    public LocalLease Acquire(string taskId, string executor, TimeSpan ttl)
    {
        ValidateIdentity(taskId, nameof(taskId));
        ValidateIdentity(executor, nameof(executor));
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "Lease TTL must be strictly positive.");
        }

        return WithTaskLock(taskId, () =>
        {
            var now = _clock.UtcNow.ToUniversalTime();
            var existing = ReadLease(taskId);
            if (existing is not null && existing.ClaimExpiresAt > now)
            {
                throw new LeaseConflictException($"Task '{taskId}' already has an active lease with claim_id '{existing.ClaimId}'.");
            }

            var lease = new LocalLease(taskId, executor, Guid.NewGuid().ToString("N"), now, now.Add(ttl));
            WriteLease(lease);
            return lease;
        });
    }

    public LocalLeaseInspection Inspect(string taskId)
    {
        ValidateIdentity(taskId, nameof(taskId));

        return WithTaskLock(taskId, () =>
        {
            var lease = ReadLease(taskId);
            if (lease is null)
            {
                return new LocalLeaseInspection(LocalLeaseStatus.Missing, null);
            }

            var status = lease.ClaimExpiresAt > _clock.UtcNow.ToUniversalTime()
                ? LocalLeaseStatus.Active
                : LocalLeaseStatus.Expired;
            return new LocalLeaseInspection(status, lease);
        });
    }

    public void Release(string taskId, string claimId, LeaseReleaseReason reason)
    {
        ValidateIdentity(taskId, nameof(taskId));
        ValidateIdentity(claimId, nameof(claimId));
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), "Lease release reason is not supported.");
        }

        WithTaskLock(taskId, () =>
        {
            var lease = ReadLease(taskId) ?? throw new LeaseGuardException($"Task '{taskId}' has no lease to release.");
            if (lease.ClaimExpiresAt <= _clock.UtcNow.ToUniversalTime())
            {
                throw new LeaseGuardException($"Task '{taskId}' lease has expired and cannot be released by its former holder.");
            }

            if (!string.Equals(lease.ClaimId, claimId, StringComparison.Ordinal))
            {
                throw new LeaseGuardException($"Claim ID does not match the active lease for task '{taskId}'.");
            }

            try
            {
                File.Delete(LeasePath(taskId));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new LeaseStoreException($"Could not release the active lease for task '{taskId}'.", exception);
            }

            return 0;
        });
    }

    private string LeasesDirectory => Path.Combine(_storePath, "leases");
    private string LocksDirectory => Path.Combine(_storePath, "locks");

    private T WithTaskLock<T>(string taskId, Func<T> action)
    {
        var lockPath = Path.Combine(LocksDirectory, $"{TaskHash(taskId)}.lock");
        FileStream lockHandle;
        try
        {
            lockHandle = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new LeaseConflictException($"A lease operation for task '{taskId}' is already in progress.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new LeaseStoreException($"The lease lock for task '{taskId}' cannot be opened.", exception);
        }

        using (lockHandle)
        {
            return action();
        }
    }

    private LocalLease? ReadLease(string taskId)
    {
        var path = LeasePath(taskId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new LeaseStoreException($"Lease state for task '{taskId}' is not an object.");
            }

            var allowed = new HashSet<string>(StringComparer.Ordinal)
            {
                "schema", "task_id", "claimed_by", "claim_id", "claim_started_at", "claim_expires_at"
            };
            foreach (var property in root.EnumerateObject())
            {
                if (!allowed.Contains(property.Name))
                {
                    throw new LeaseStoreException($"Lease state for task '{taskId}' contains an unknown property.");
                }
            }

            if (!string.Equals(RequiredString(root, "schema", taskId), LeaseSchema, StringComparison.Ordinal))
            {
                throw new LeaseStoreException($"Lease state for task '{taskId}' has an unknown schema.");
            }

            var storedTaskId = RequiredString(root, "task_id", taskId);
            if (!string.Equals(taskId, storedTaskId, StringComparison.Ordinal))
            {
                throw new LeaseStoreException($"Lease state task identity does not match '{taskId}'.");
            }

            var claimedBy = RequiredString(root, "claimed_by", taskId);
            var claimId = RequiredString(root, "claim_id", taskId);
            if (!Guid.TryParseExact(claimId, "N", out _))
            {
                throw new LeaseStoreException($"Lease state for task '{taskId}' has an invalid fencing token.");
            }

            var startedAt = ParseCanonicalTimestamp(RequiredString(root, "claim_started_at", taskId), taskId, "claim_started_at");
            var expiresAt = ParseCanonicalTimestamp(RequiredString(root, "claim_expires_at", taskId), taskId, "claim_expires_at");
            if (expiresAt <= startedAt)
            {
                throw new LeaseStoreException($"Lease state for task '{taskId}' has non-increasing timestamps.");
            }

            return new LocalLease(taskId, claimedBy, claimId, startedAt, expiresAt);
        }
        catch (LeaseStoreException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or DecoderFallbackException)
        {
            throw new LeaseStoreException($"Lease state for task '{taskId}' is unreadable or corrupt.", exception);
        }
    }

    private void WriteLease(LocalLease lease)
    {
        var path = LeasePath(lease.TaskId);
        var temporaryPath = Path.Combine(LeasesDirectory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        var contents = JsonSerializer.Serialize(new LeaseState(
            LeaseSchema,
            lease.TaskId,
            lease.ClaimedBy,
            lease.ClaimId,
            FormatCanonicalTimestamp(lease.ClaimStartedAt),
            FormatCanonicalTimestamp(lease.ClaimExpiresAt)));

        try
        {
            File.WriteAllText(temporaryPath, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new LeaseStoreException($"Could not persist the lease for task '{lease.TaskId}'.", exception);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private string LeasePath(string taskId) => Path.Combine(LeasesDirectory, $"{TaskHash(taskId)}.json");

    private static string TaskHash(string taskId) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(taskId)));

    private static void ValidateIdentity(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Lease identity must be non-empty.", parameterName);
        }
    }

    private static string RequiredString(JsonElement root, string name, string taskId)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new LeaseStoreException($"Lease state for task '{taskId}' is missing '{name}'.");
        }

        return value.GetString()!;
    }

    internal static string FormatCanonicalTimestamp(DateTimeOffset timestamp) => timestamp.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseCanonicalTimestamp(string value, string taskId, string property)
    {
        const string format = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
        if (!DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            throw new LeaseStoreException($"Lease state for task '{taskId}' has a non-canonical '{property}'.");
        }

        var timestamp = new DateTimeOffset(parsed, TimeSpan.Zero);
        if (!string.Equals(FormatCanonicalTimestamp(timestamp), value, StringComparison.Ordinal))
        {
            throw new LeaseStoreException($"Lease state for task '{taskId}' has a non-canonical '{property}'.");
        }

        return timestamp;
    }

    private sealed record LeaseState(
        [property: JsonPropertyName("schema")] string Schema,
        [property: JsonPropertyName("task_id")] string TaskId,
        [property: JsonPropertyName("claimed_by")] string ClaimedBy,
        [property: JsonPropertyName("claim_id")] string ClaimId,
        [property: JsonPropertyName("claim_started_at")] string ClaimStartedAt,
        [property: JsonPropertyName("claim_expires_at")] string ClaimExpiresAt);
}
