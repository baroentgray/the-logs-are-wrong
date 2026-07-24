using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tlaw.AgentProtocol;
using Tlaw.Verify;

namespace Tlaw.Dispatcher;

/// <summary>
/// Typed, repository-native authorization for a Linear transition. A schema string is never authority:
/// every event parses and correlates real dispatcher evidence before any GraphQL mutation is attempted.
/// </summary>
internal sealed record AuthorizedTransition(string Event, string TargetState, bool IsNoOp, string EvidenceHash, HandoffAuthorization? Handoff = null, MergeAuthorization? Merge = null);

internal sealed record HandoffAuthorization(string Decision);
internal sealed record MergeAuthorization(string ReviewedHead, string MergeSha, string RepositoryPath);

internal static class LinearTransitionAuthorizer
{
    private const string CanonicalTimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    internal static (TaskV2Packet Task, byte[] Bytes) LoadTask(string path, PacketSchemaRegistry registry)
    {
        var bytes = File.ReadAllBytes(path);
        var yaml = new UTF8Encoding(false, true).GetString(bytes);
        var validation = PacketValidator.Validate(yaml, registry);
        if (!validation.IsValid || validation.Packet is null || !string.Equals(validation.Packet.Schema, "tlaw.agent-task/v2", StringComparison.Ordinal))
        {
            throw new LinearCommandException("Transition task must be a valid tlaw.agent-task/v2 packet.");
        }

        return (TaskV2Packet.From(validation.Packet), bytes);
    }

    /// <summary>Every transition requires the task packet to name the exact Linear issue and carry its live URL.</summary>
    internal static void RequireIssueIdentity(TaskV2Packet task, LinearIssueSnapshot snapshot)
    {
        if (!string.Equals(task.TaskId, snapshot.Identifier, StringComparison.Ordinal) || !string.Equals(task.SourceId, snapshot.Identifier, StringComparison.Ordinal))
        {
            throw new LinearCommandException("Transition task_id and source_id must equal the Linear issue identifier.");
        }

        if (!task.Sources.Contains(snapshot.Url, StringComparer.Ordinal))
        {
            throw new LinearCommandException("Transition task sources must contain the exact live Linear issue URL.");
        }
    }

    internal static AuthorizedTransition Authorize(LinearTransitionOptions options, TaskV2Packet task, byte[] taskBytes, PacketSchemaRegistry registry, LinearIssueSnapshot snapshot, IGitProofRunner git) => options.Event switch
    {
        "queue" => Queue(task, taskBytes, snapshot),
        "claim" => Claim(task, taskBytes, snapshot),
        "result" => Result(options, task, snapshot),
        "review" => Review(options, task, snapshot),
        "handoff" => Handoff(options, task, snapshot),
        "merge" => Merge(options, task, snapshot, git),
        _ => throw new LinearCommandException("Transition event is not supported.")
    };

    private static AuthorizedTransition Queue(TaskV2Packet task, byte[] taskBytes, LinearIssueSnapshot snapshot)
    {
        if (!string.Equals(snapshot.StateName, "Backlog", StringComparison.Ordinal)) throw new LinearCommandException("Queue transition requires a Backlog issue.");
        if (!task.IsUnclaimed) throw new LinearCommandException("Queue transition requires a fully unclaimed task; no claim field may be partially populated.");
        return new AuthorizedTransition("queue", "Todo", false, Sha(taskBytes));
    }

    private static AuthorizedTransition Claim(TaskV2Packet task, byte[] taskBytes, LinearIssueSnapshot snapshot)
    {
        if (!string.Equals(snapshot.StateName, "Todo", StringComparison.Ordinal)) throw new LinearCommandException("Claim transition requires a Todo issue.");
        if (!task.IsClaimed) throw new LinearCommandException("Claim transition requires a fully claimed task.");
        if (!task.EligibleAgents.Contains(task.ClaimedBy, StringComparer.Ordinal)) throw new LinearCommandException("Claim transition requires claimed_by to be eligible under the packet.");
        if (string.Equals(task.ClaimId, "unclaimed", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(task.ClaimId)) throw new LinearCommandException("Claim transition requires an exact non-sentinel fencing token.");
        RequireCanonicalTimestamp(task.ClaimStartedAt);
        RequireCanonicalTimestamp(task.ClaimExpiresAt);

        return new AuthorizedTransition("claim", "In Progress", false, Sha(taskBytes));
    }

    private static AuthorizedTransition Result(LinearTransitionOptions options, TaskV2Packet task, LinearIssueSnapshot snapshot)
    {
        if (!string.Equals(snapshot.StateName, "In Progress", StringComparison.Ordinal)) throw new LinearCommandException("Result transition requires an In Progress issue.");
        if (!task.IsClaimed) throw new LinearCommandException("Result transition requires a fully claimed task.");
        var bytes = File.ReadAllBytes(options.FinalizationPath!);
        var finalization = DispatcherFinalization.Parse(bytes);
        if (!string.Equals(task.TaskId, finalization.TaskId, StringComparison.Ordinal) ||
            !string.Equals(task.ClaimedBy, finalization.ClaimedBy, StringComparison.Ordinal) ||
            !string.Equals(task.ClaimId, finalization.ClaimId, StringComparison.Ordinal))
        {
            throw new LinearCommandException("Result finalization must correlate the exact task, claimed agent, and claim id.");
        }

        var target = finalization.NextState switch
        {
            "in_review" => "In Review",
            "todo" => "Todo",
            _ => throw new LinearCommandException("Result finalization next_state is not a documented BAR-36 value.")
        };
        return new AuthorizedTransition("result", target, false, Sha(bytes));
    }

    private static AuthorizedTransition Review(LinearTransitionOptions options, TaskV2Packet task, LinearIssueSnapshot snapshot)
    {
        if (!string.Equals(snapshot.StateName, "In Review", StringComparison.Ordinal)) throw new LinearCommandException("Review transition requires an In Review issue.");
        if (!task.IsClaimed) throw new LinearCommandException("Review transition requires a fully claimed task.");
        var bytes = File.ReadAllBytes(options.ReviewDecisionPath!);
        var decision = DispatcherReviewDecision.Parse(bytes);
        if (!string.Equals(task.TaskId, decision.TaskId, StringComparison.Ordinal)) throw new LinearCommandException("Review decision must correlate the exact task id.");
        return decision.Decision switch
        {
            "correction" => new AuthorizedTransition("review", "Todo", false, Sha(bytes)),
            "human" => new AuthorizedTransition("review", snapshot.StateName, true, Sha(bytes)),
            "merge" => new AuthorizedTransition("review", snapshot.StateName, true, Sha(bytes)),
            _ => throw new LinearCommandException("Review decision is not a supported edge.")
        };
    }

    private static AuthorizedTransition Handoff(LinearTransitionOptions options, TaskV2Packet task, LinearIssueSnapshot snapshot)
    {
        if (!string.Equals(snapshot.StateName, "In Progress", StringComparison.Ordinal)) throw new LinearCommandException("Handoff transition requires an In Progress issue.");
        if (!task.IsUnclaimed) throw new LinearCommandException("Handoff transition requires a fully unclaimed BAR-40 continuation task.");
        var bytes = File.ReadAllBytes(options.HandoffIngestionPath!);
        var ingestion = HandoffIngestionRecord.Parse(bytes);
        if (!string.Equals(task.TaskId, ingestion.TaskId, StringComparison.Ordinal) ||
            !string.Equals(task.SourceId, ingestion.SourceId, StringComparison.Ordinal) ||
            !string.Equals(task.Worktree, ingestion.Branch, StringComparison.Ordinal))
        {
            throw new LinearCommandException("Handoff continuation identity, source, and worktree must agree with the ingestion evidence.");
        }

        return new AuthorizedTransition("handoff", "Todo", false, Sha(bytes), Handoff: new HandoffAuthorization(ingestion.Decision));
    }

    private static AuthorizedTransition Merge(LinearTransitionOptions options, TaskV2Packet task, LinearIssueSnapshot snapshot, IGitProofRunner git)
    {
        if (!string.Equals(snapshot.StateName, "In Review", StringComparison.Ordinal)) throw new LinearCommandException("Only an In Review Linear issue may transition to Done.");
        if (!task.IsClaimed) throw new LinearCommandException("Merge transition requires a fully claimed task.");
        var decision = DispatcherReviewDecision.Parse(File.ReadAllBytes(options.ReviewDecisionPath!));
        if (!string.Equals(task.TaskId, decision.TaskId, StringComparison.Ordinal)) throw new LinearCommandException("Merge review decision must correlate the exact task id.");
        if (!string.Equals(decision.Decision, "merge", StringComparison.Ordinal)) throw new LinearCommandException("Merge transition requires an approved BAR-37 merge review decision.");

        var verificationBytes = File.ReadAllBytes(options.VerificationPath!);
        RepositoryVerificationArtifact.Validate(verificationBytes, options.MergeSha!);
        MergeGitProof.Require(decision.ReviewedHead, options.MergeSha!, options.RepositoryPath!, git);
        // The receipt hashes the exact original verifier bytes.
        return new AuthorizedTransition("merge", "Done", false, Sha(verificationBytes), Merge: new MergeAuthorization(decision.ReviewedHead, options.MergeSha!, options.RepositoryPath!));
    }

    private static void RequireCanonicalTimestamp(string value)
    {
        if (!DateTimeOffset.TryParseExact(value, CanonicalTimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _))
        {
            throw new LinearCommandException("Claim transition requires canonical claim timestamps.");
        }
    }

    internal static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

/// <summary>Strict typed parser for a BAR-36 <c>tlaw.dispatcher-finalization/v1</c> record.</summary>
internal sealed record DispatcherFinalization(string TaskId, string ClaimedBy, string ClaimId, string ResultStatus, string ResultSha256, string ReleaseReason, string NextState)
{
    internal static DispatcherFinalization Parse(byte[] bytes)
    {
        _ = new UTF8Encoding(false, true).GetString(bytes);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new LinearCommandException("Finalization must be one JSON object.");
        var allowed = new HashSet<string>(["schema", "task_id", "claimed_by", "claim_id", "result_status", "result_sha256", "release_reason", "next_state"], StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject()) if (!allowed.Contains(property.Name) || !seen.Add(property.Name)) throw new LinearCommandException("Finalization contains an unknown or duplicate property.");
        if (seen.Count != allowed.Count) throw new LinearCommandException("Finalization is missing a required property.");

        var schema = Str(root, "schema");
        var hash = Str(root, "result_sha256");
        var status = Str(root, "result_status");
        var reason = Str(root, "release_reason");
        var next = Str(root, "next_state");
        if (!string.Equals(schema, "tlaw.dispatcher-finalization/v1", StringComparison.Ordinal)) throw new LinearCommandException("Finalization schema is invalid.");
        if (!IsLowerHex(hash, 64)) throw new LinearCommandException("Finalization result_sha256 must be lowercase SHA-256.");
        var documented = (status, reason, next) is ("success", "completion", "in_review") or ("failed", "error", "todo");
        if (!documented) throw new LinearCommandException("Finalization status, release reason, and next_state are not a documented BAR-36 mapping.");
        return new DispatcherFinalization(Str(root, "task_id"), Str(root, "claimed_by"), Str(root, "claim_id"), status, hash, reason, next);
    }

    private static string Str(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new LinearCommandException($"Finalization field '{name}' must be a non-empty string.");
    private static bool IsLowerHex(string value, int length) => value.Length == length && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

/// <summary>Strict typed parser for a BAR-37 <c>tlaw.dispatcher-review-decision/v1</c> record.</summary>
internal sealed record DispatcherReviewDecision(string TaskId, string ReviewedHead, string ReviewSha256, string Verdict, string HighestSeverity, int BlockingFindings, string Decision, string NextState)
{
    internal static DispatcherReviewDecision Parse(byte[] bytes)
    {
        _ = new UTF8Encoding(false, true).GetString(bytes);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new LinearCommandException("Review decision must be one JSON object.");
        var allowed = new HashSet<string>(["schema", "task_id", "reviewed_head", "review_sha256", "verdict", "highest_severity", "blocking_findings", "decision", "next_state"], StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject()) if (!allowed.Contains(property.Name) || !seen.Add(property.Name)) throw new LinearCommandException("Review decision contains an unknown or duplicate property.");
        if (seen.Count != allowed.Count) throw new LinearCommandException("Review decision is missing a required property.");

        var schema = Str(root, "schema");
        var reviewedHead = Str(root, "reviewed_head");
        var reviewSha = Str(root, "review_sha256");
        var verdict = Str(root, "verdict");
        var highest = Str(root, "highest_severity");
        var blocking = Int(root, "blocking_findings");
        var decision = Str(root, "decision");
        var next = Str(root, "next_state");

        if (!string.Equals(schema, "tlaw.dispatcher-review-decision/v1", StringComparison.Ordinal)) throw new LinearCommandException("Review decision schema is invalid.");
        if (!IsLowerHex(reviewedHead, 40) || reviewedHead == new string('0', 40)) throw new LinearCommandException("Review decision reviewed_head is invalid.");
        if (!IsLowerHex(reviewSha, 64)) throw new LinearCommandException("Review decision review_sha256 must be lowercase SHA-256.");
        if (verdict is not ("approve" or "request_changes" or "comment")) throw new LinearCommandException("Review decision verdict is not supported.");
        if (highest is not ("none" or "info" or "low" or "medium" or "high" or "blocker")) throw new LinearCommandException("Review decision highest_severity is not supported.");
        if (decision is not ("merge" or "correction" or "human")) throw new LinearCommandException("Review decision decision is not supported.");
        if (next is not ("in_review" or "todo")) throw new LinearCommandException("Review decision next_state is not supported.");
        if (blocking < 0) throw new LinearCommandException("Review decision blocking_findings must be non-negative.");

        var blockingSeverity = highest is "medium" or "high" or "blocker";
        var consistent = decision switch
        {
            "merge" => string.Equals(verdict, "approve", StringComparison.Ordinal) && blocking == 0 && string.Equals(next, "in_review", StringComparison.Ordinal) && !blockingSeverity,
            "correction" => string.Equals(verdict, "request_changes", StringComparison.Ordinal) && blocking > 0 && string.Equals(next, "todo", StringComparison.Ordinal) && blockingSeverity,
            "human" => string.Equals(verdict, "comment", StringComparison.Ordinal) && string.Equals(next, "in_review", StringComparison.Ordinal),
            _ => false
        };
        if (!consistent) throw new LinearCommandException("Review decision verdict, severity, blocking findings, decision, and next_state are internally inconsistent.");
        return new DispatcherReviewDecision(Str(root, "task_id"), reviewedHead, reviewSha, verdict, highest, blocking, decision, next);
    }

    private static string Str(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new LinearCommandException($"Review decision field '{name}' must be a non-empty string.");
    private static int Int(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : throw new LinearCommandException($"Review decision field '{name}' must be an integer.");
    private static bool IsLowerHex(string value, int length) => value.Length == length && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

/// <summary>
/// Strict acceptance of the real <c>tlaw.verification/v1</c> artifact produced by <c>Tlaw.Verify</c>.
/// Reuses the repository's own verification model and verdict evaluator so field names and value shapes
/// cannot drift, then adds the merge-specific head and closure requirements.
/// </summary>
internal static class RepositoryVerificationArtifact
{
    private static readonly string[] ClosedTopLevel =
    [
        "schema", "startedAtUtc", "finishedAtUtc", "repositoryRoot", "branch", "isDetachedHead",
        "actualHeadSha", "expectedHeadSha", "actualBaseSha", "expectedBaseSha", "cleanTree", "environment",
        "commands", "restore", "build", "tests", "diffCheck", "gate0", "architecture", "domainDependencies",
        "verdict", "failureReasons"
    ];

    internal static void Validate(byte[] bytes, string mergeSha)
    {
        _ = new UTF8Encoding(false, true).GetString(bytes);
        RequireClosedTopLevel(bytes);

        VerificationReport report;
        try
        {
            report = VerificationReportSerializer.Deserialize(new UTF8Encoding(false, true).GetString(bytes));
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            throw new LinearCommandException("Verification artifact is malformed or empty.");
        }

        if (!string.Equals(report.Schema, "tlaw.verification/v1", StringComparison.Ordinal)) throw new LinearCommandException("Verification artifact schema is not tlaw.verification/v1.");

        var outcome = VerificationVerdictEvaluator.Evaluate(report, allowDetachedHead: true);
        if (outcome.Verdict != VerificationVerdict.PASS) throw new LinearCommandException($"Verification artifact does not pass every required stage: {string.Join("; ", outcome.FailureReasons)}.");
        if (report.IsDetachedHead && !string.IsNullOrEmpty(report.Branch)) throw new LinearCommandException("Detached verification artifacts must not name a branch.");
        if (!report.IsDetachedHead && string.IsNullOrWhiteSpace(report.Branch)) throw new LinearCommandException("Non-detached verification artifacts must name a branch.");
        if (report.Verdict != VerificationVerdict.PASS) throw new LinearCommandException("Verification artifact verdict is not PASS.");
        if (report.FailureReasons.Count != 0) throw new LinearCommandException("Verification artifact reports non-empty failure reasons.");
        if (!report.CleanTree) throw new LinearCommandException("Verification artifact working tree is not clean.");
        if (!string.Equals(report.ExpectedHeadSha, mergeSha, StringComparison.Ordinal) || !string.Equals(report.ActualHeadSha, mergeSha, StringComparison.Ordinal))
        {
            throw new LinearCommandException("Verification artifact expected and actual head must both equal the supplied merge SHA.");
        }
    }

    private static void RequireClosedTopLevel(byte[] bytes)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) throw new LinearCommandException("Verification artifact must be a JSON object.");
            var depth = 0;
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        depth++;
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        if (depth == 0) { RequireComplete(seen); return; }
                        depth--;
                        break;
                    case JsonTokenType.PropertyName when depth == 0:
                        var name = reader.GetString()!;
                        if (!ClosedTopLevel.Contains(name) || !seen.Add(name)) throw new LinearCommandException("Verification artifact has an unknown or duplicate top-level property.");
                        break;
                }
            }
        }
        catch (JsonException)
        {
            throw new LinearCommandException("Verification artifact is malformed JSON.");
        }

        RequireComplete(seen);
    }

    private static void RequireComplete(HashSet<string> seen)
    {
        if (seen.Count != ClosedTopLevel.Length) throw new LinearCommandException("Verification artifact is missing a required top-level property.");
    }
}

/// <summary>The Git ancestry and reachability half of the merge-only Done proof.</summary>
internal static class MergeGitProof
{
    internal static void Require(string reviewedHead, string mergeSha, string repositoryPath, IGitProofRunner git)
    {
        if (!IsLowerHex(mergeSha, 40)) throw new LinearCommandException("Merge SHA must be 40 lowercase hexadecimal characters.");
        if (!Directory.Exists(repositoryPath)) throw new LinearCommandException("Merge repository path does not exist.");
        RequireGit(git.Run(repositoryPath, "cat-file", "-e", reviewedHead + "^{commit}"));
        RequireGit(git.Run(repositoryPath, "cat-file", "-e", mergeSha + "^{commit}"));
        RequireGit(git.Run(repositoryPath, "merge-base", "--is-ancestor", reviewedHead, mergeSha));
        RequireGit(git.Run(repositoryPath, "merge-base", "--is-ancestor", mergeSha, "origin/main"));
        var origin = git.Run(repositoryPath, "rev-parse", "origin/main");
        RequireGit(origin);
        if (!string.Equals(origin.StandardOutput.Trim(), mergeSha, StringComparison.Ordinal)) throw new LinearCommandException("Git origin/main is not exactly the supplied merge SHA.");
    }

    private static void RequireGit(GitProofResult result)
    {
        if (result.TimedOut) throw new LinearCommandException("Git merge proof timed out.");
        if (result.ExitCode != 0) throw new LinearCommandException("Git merge proof failed.");
    }

    private static bool IsLowerHex(string value, int length) => value.Length == length && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
