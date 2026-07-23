using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tlaw.Dispatcher;

/// <summary>A deliberately narrow, fail-closed Linear adapter; it never reads issue descriptions.</summary>
public static class LinearCommand
{
    internal const string Endpoint = "https://api.linear.app/graphql";

    public static int Run(string[] args, TextWriter standardOutput, TextWriter standardError) => Run(args, standardOutput, standardError, new HttpLinearTransport());
    internal static int RunForTesting(string[] args, TextWriter standardOutput, TextWriter standardError, ILinearTransport transport) => Run(args, standardOutput, standardError, transport);

    private static int Run(string[] args, TextWriter standardOutput, TextWriter standardError, ILinearTransport transport)
    {
        try
        {
            if (args.Length < 2 || args[0] != "linear") throw new LinearCommandException("Linear command must begin with 'linear snapshot' or 'linear transition'.");
            return args[1] switch
            {
                "snapshot" => Snapshot(LinearSnapshotOptions.Parse(args), transport, standardOutput),
                "transition" => Transition(LinearTransitionOptions.Parse(args), transport, standardOutput),
                _ => throw new LinearCommandException("Linear command must use the exact subcommand 'snapshot' or 'transition'.")
            };
        }
        catch (LinearCommandException exception) { standardError.WriteLine($"FAIL: {exception.Message}"); return 1; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or DecoderFallbackException or InvalidDataException or JsonException or HttpRequestException) { standardError.WriteLine($"FAIL: {exception.Message}"); return 1; }
        catch (Exception) { standardError.WriteLine("FAIL: Linear adapter operation failed unexpectedly."); return 1; }
    }

    private static int Snapshot(LinearSnapshotOptions options, ILinearTransport transport, TextWriter output)
    {
        var profile = LinearDispatchProfile.Parse(TaskPacketCommand.ReadUtf8(options.ProfilePath));
        var snapshot = LinearIssueSnapshot.From(FetchIssue(transport, options.IssueIdentifier, RequireApiKey(options.ApiKeyEnvironment)));
        var normalized = LinearInputJson.Write(profile, snapshot);
        _ = NormalizedTaskInput.Parse(normalized);
        TaskPacketCommand.WriteAtomically(options.OutputPath, normalized);
        TaskPacketCommand.WriteAtomically(options.SnapshotOutputPath, snapshot.ToJson());
        output.WriteLine($"SNAPSHOT: {snapshot.Identifier}");
        return 0;
    }

    private static int Transition(LinearTransitionOptions options, ILinearTransport transport, TextWriter output)
    {
        var apiKey = RequireApiKey(options.ApiKeyEnvironment);
        var snapshot = LinearIssueSnapshot.Parse(TaskPacketCommand.ReadUtf8(options.SnapshotPath));
        if (snapshot.Identifier != options.IssueIdentifier) throw new LinearCommandException("Transition issue identifier must exactly match the supplied snapshot.");
        var live = LinearIssueSnapshot.From(FetchIssue(transport, options.IssueIdentifier, apiKey));
        snapshot.RequireSameConcurrencyFacts(live);
        var evidence = TransitionEvidence.Parse(File.ReadAllBytes(options.EvidencePath));
        if (options.Event == "handoff") return HandoffTransition(options, transport, output, apiKey, snapshot, live, evidence);
        var plan = options.Event == "merge"
            ? MergeTransitionProof.Validate(snapshot, evidence, options, new SystemGitProofRunner())
            : LinearTransitionPlanner.Plan(options.Event, snapshot, evidence);
        if (plan.IsNoOp)
        {
            TaskPacketCommand.WriteAtomically(options.OutputPath, LinearReceiptJson.Write(snapshot, options.Event, plan, evidence.Hash, snapshot.UpdatedAt));
            output.WriteLine($"TRANSITION: {options.Event} (no-op)");
            return 0;
        }

        var target = snapshot.States.SingleOrDefault(state => state.Name == plan.TargetState) ?? throw new LinearCommandException($"Live Linear team has no required target state '{plan.TargetState}'.");
        LinearGraphQl.RequireSuccessfulMutation(transport.Send("IssueUpdate", "mutation IssueUpdate($id:String!,$stateId:String!){issueUpdate(id:$id,input:{stateId:$stateId}){success}}", new { id = snapshot.Id, stateId = target.Id }, apiKey), "issueUpdate");
        var after = LinearIssueSnapshot.From(FetchIssue(transport, options.IssueIdentifier, apiKey));
        if (after.StateId != target.Id) throw new LinearCommandException("Linear may already have changed: post-mutation state verification failed.");
        try { TaskPacketCommand.WriteAtomically(options.OutputPath, LinearReceiptJson.Write(snapshot, options.Event, plan with { TargetStateId = target.Id }, evidence.Hash, after.UpdatedAt)); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException) { throw new LinearCommandException("Linear may already have changed: transition receipt was not published."); }
        output.WriteLine($"TRANSITION: {options.Event} -> {plan.TargetState}");
        return 0;
    }

    private static int HandoffTransition(LinearTransitionOptions options, ILinearTransport transport, TextWriter output, string apiKey, LinearIssueSnapshot snapshot, LinearIssueSnapshot live, TransitionEvidence evidence)
    {
        if (snapshot.StateName != "In Progress" || evidence.Schema != "tlaw.dispatcher-handoff-finalization/v1" || evidence.Decision is not ("human" or "reassign")) throw new LinearCommandException("Handoff requires In Progress and correlated finalized human or reassign evidence.");
        if (evidence.Decision == "human" && string.IsNullOrWhiteSpace(options.BlockerIdentifier)) throw new LinearCommandException("Human handoff requires --blocker.");
        if (evidence.Decision == "human" && options.ResolvedBlockerIdentifier is not null) throw new LinearCommandException("Human handoff adds a blocker; it does not resolve one.");
        if (evidence.Decision == "reassign" && options.BlockerIdentifier is not null) throw new LinearCommandException("Reassign handoff must not invent a blocker.");

        var journal = new LinearMutationJournal();
        var labelsAdded = new List<string>();
        var labelsRemoved = new List<string>();
        var blockersAdded = new List<LinearRelation>();
        var blockersRemoved = new List<LinearRelation>();
        try
        {
            // `live` is the pre-mutation refetch taken immediately before dispatch and already concurrency-checked against the snapshot.
            var labels = live.Labels.ToList();
            var relations = live.BlockedBy.ToList();

            // Validate a requested removal before any state mutation so an impossible removal has no side effects.
            (LinearRelation? removalTarget, bool idempotentMissing) = options.ResolvedBlockerIdentifier is null
                ? (null, false)
                : BlockerRelationOps.ValidateRemovable(options, live, snapshot, evidence);

            if (evidence.Decision == "human")
            {
                var blockedLabelId = BlockedLabelResolution.ResolveOrCreate(transport, live.TeamId, apiKey, journal); // (1) blocked_label_created
                BlockerRelationOps.AddBlocker(transport, live, options.BlockerIdentifier!, apiKey, journal, relations, blockersAdded); // (2) blocker_relation_added
                if (!labels.Any(l => string.Equals(l.Id, blockedLabelId, StringComparison.Ordinal)))                 // (3) blocked_label_added
                {
                    UpdateLabels(transport, live.Id, labels.Select(l => l.Id).Append(blockedLabelId), apiKey);
                    labels.Add(new LinearLabel(blockedLabelId, "blocked"));
                    labelsAdded.Add(blockedLabelId);
                    journal.Complete("blocked_label_added");
                }
            }

            if (options.ResolvedBlockerIdentifier is not null)
            {
                if (!idempotentMissing)
                    BlockerRelationOps.PerformDelete(transport, options, live, removalTarget!, apiKey, journal, relations, blockersRemoved); // (5) blocker_relation_removed
                if (relations.Count == 0)                                                                            // (6) blocked_label_removed
                {
                    var blocked = labels.FirstOrDefault(l => string.Equals(l.Name, "blocked", StringComparison.Ordinal));
                    if (blocked is not null)
                    {
                        UpdateLabels(transport, live.Id, labels.Where(l => !string.Equals(l.Id, blocked.Id, StringComparison.Ordinal)).Select(l => l.Id), apiKey);
                        labels.RemoveAll(l => string.Equals(l.Id, blocked.Id, StringComparison.Ordinal));
                        labelsRemoved.Add(blocked.Id);
                        journal.Complete("blocked_label_removed");
                    }
                }
            }

            var todo = live.States.SingleOrDefault(x => string.Equals(x.Name, "Todo", StringComparison.Ordinal)) ?? throw new LinearCommandException("Live Linear team has no Todo state.");
            UpdateState(transport, live.Id, todo.Id, apiKey);                                                        // (4) state_changed
            journal.Complete("state_changed");

            var after = LinearIssueSnapshot.From(FetchIssue(transport, options.IssueIdentifier, apiKey));            // (7) post_mutation_verified
            if (!string.Equals(after.StateName, "Todo", StringComparison.Ordinal)) throw new LinearCommandException("post-mutation state is not Todo");
            if (!SameLabelSet(after.Labels, labels)) throw new LinearCommandException("post-mutation labels do not exactly match the required set");
            if (!SameRelationSet(after.BlockedBy, relations)) throw new LinearCommandException("post-mutation blocker relations do not exactly match the required set");
            journal.Complete("post_mutation_verified");

            var receipt = LinearReceiptJson.WriteHandoff(live, todo, evidence.Hash, after.UpdatedAt, labelsAdded, labelsRemoved, blockersAdded, blockersRemoved, after.BlockedBy, after.Labels);
            try { TaskPacketCommand.WriteAtomically(options.OutputPath, receipt); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException) { throw new LinearCommandException("the durable transition receipt was not published"); }
            journal.Complete("receipt_published");                                                                   // (8) receipt_published
            output.WriteLine("TRANSITION: handoff -> Todo");
            return 0;
        }
        catch (LinearCommandException exception) when (journal.AnyCompleted) { throw new LinearCommandException($"Linear may already have changed. Completed remote operations: {journal.Describe()}. Failed operation: {exception.Message}"); }
    }

    internal static void UpdateLabels(ILinearTransport transport, string issueId, IEnumerable<string> labelIds, string apiKey) =>
        transport.Send("IssueUpdate", "mutation IssueUpdate($id:String!,$labelIds:[String!]){issueUpdate(id:$id,input:{labelIds:$labelIds}){success}}", new { id = issueId, labelIds = labelIds.ToArray() }, apiKey).RequireMutation("issueUpdate");
    private static void UpdateState(ILinearTransport transport, string issueId, string stateId, string apiKey) =>
        transport.Send("IssueUpdate", "mutation IssueUpdate($id:String!,$stateId:String!){issueUpdate(id:$id,input:{stateId:$stateId}){success}}", new { id = issueId, stateId }, apiKey).RequireMutation("issueUpdate");
    internal static bool SameLabelSet(IReadOnlyList<LinearLabel> a, IReadOnlyList<LinearLabel> b) =>
        a.Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(b.Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal));
    internal static bool SameRelationSet(IReadOnlyList<LinearRelation> a, IReadOnlyList<LinearRelation> b)
    {
        if (a.Count != b.Count) return false;
        var byId = new Dictionary<string, LinearRelation>(StringComparer.Ordinal);
        foreach (var r in b) if (!byId.TryAdd(r.Id, r)) return false;
        return a.All(r => byId.TryGetValue(r.Id, out var match) && match == r);
    }

    private static string RequireApiKey(string environmentName)
    {
        if (!Regex.IsMatch(environmentName, "^[A-Z][A-Z0-9_]{0,127}$", RegexOptions.CultureInvariant)) throw new LinearCommandException("API key environment variable name must be uppercase identifier text.");
        var value = Environment.GetEnvironmentVariable(environmentName);
        return string.IsNullOrWhiteSpace(value) ? throw new LinearCommandException("The explicitly named Linear API key environment variable is absent or empty.") : value;
    }

    internal static LinearIssue FetchIssue(ILinearTransport transport, string identifier, string apiKey) => LinearGraphQl.ParseIssue(transport.Send("Issue", "query Issue($identifier:String!){issue(id:$identifier){id identifier url title updatedAt state{id name type} team{id key states{nodes{id name type}}} labels{nodes{id name}} blockedBy{nodes{id type blockingIssue{id identifier} blockedIssue{id identifier}}} blocks{nodes{id type blockingIssue{id identifier} blockedIssue{id identifier}}} attachments{nodes{url}}}}", new { identifier }, apiKey));
}

public sealed class LinearCommandException(string message) : Exception(message);
internal interface ILinearTransport { LinearTransportResponse Send(string operationName, string query, object variables, string apiKey); }
internal sealed record LinearTransportResponse(HttpStatusCode StatusCode, string Body);
internal static class LinearTransportResponseExtensions
{
    internal static void RequireMutation(this LinearTransportResponse response, string property) => LinearGraphQl.RequireSuccessfulMutation(response, property);
}
internal sealed class LinearMutationJournal
{
    // Fixed canonical ordering so a partial-mutation diagnostic is deterministic regardless of execution order.
    private static readonly string[] Order = ["blocked_label_created", "blocker_relation_added", "blocked_label_added", "state_changed", "blocker_relation_removed", "blocked_label_removed", "post_mutation_verified", "receipt_published"];
    private readonly List<string> _completed = [];
    internal bool AnyCompleted => _completed.Count > 0;
    internal void Complete(string operation)
    {
        if (Array.IndexOf(Order, operation) < 0) throw new LinearCommandException($"Unknown mutation journal operation '{operation}'.");
        _completed.Add(operation);
    }
    internal string Describe() => string.Join(", ", _completed.OrderBy(op => Array.IndexOf(Order, op)));
}
internal sealed class HttpLinearTransport : ILinearTransport
{
    private readonly HttpClient _client;
    internal HttpLinearTransport() : this(new HttpClient()) { }
    internal HttpLinearTransport(HttpClient client) => _client = client;
    public LinearTransportResponse Send(string operationName, string query, object variables, string apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, LinearCommand.Endpoint) { Content = new StringContent(JsonSerializer.Serialize(new { query, variables }), Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = _client.Send(request);
        return new LinearTransportResponse(response.StatusCode, response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
    }
}

internal sealed record LinearSnapshotOptions(string IssueIdentifier, string ProfilePath, string ApiKeyEnvironment, string OutputPath, string SnapshotOutputPath)
{
    internal static LinearSnapshotOptions Parse(IReadOnlyList<string> args)
    {
        var values = LinearOptions.Parse(args, "snapshot", ["--issue", "--profile", "--api-key-env", "--output", "--snapshot-output"]);
        return new(values["--issue"], Path.GetFullPath(values["--profile"]), values["--api-key-env"], Path.GetFullPath(values["--output"]), Path.GetFullPath(values["--snapshot-output"]));
    }
}
internal sealed record LinearTransitionOptions(string IssueIdentifier, string Event, string SnapshotPath, string EvidencePath, string ApiKeyEnvironment, string OutputPath, string? VerificationPath, string? MergeSha, string? RepositoryPath, string? BlockerIdentifier, string? ResolvedBlockerIdentifier)
{
    internal static LinearTransitionOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count < 14 || args[0] != "linear" || args[1] != "transition" || (args.Count % 2) != 0) throw new LinearCommandException("Linear transition command requires complete named options.");
        var values = new Dictionary<string,string>(StringComparer.Ordinal);
        var allowed = new HashSet<string>(["--issue","--event","--snapshot","--evidence","--api-key-env","--output","--verification","--merge-sha","--repository","--blocker","--resolve-blocker"], StringComparer.Ordinal);
        for (var index = 2; index < args.Count; index += 2) if (!allowed.Contains(args[index]) || string.IsNullOrWhiteSpace(args[index + 1]) || !values.TryAdd(args[index], args[index + 1])) throw new LinearCommandException("Linear transition command received an unknown, duplicate, or empty option.");
        foreach (var required in new[] { "--issue", "--event", "--snapshot", "--evidence", "--api-key-env", "--output" }) if (!values.ContainsKey(required)) throw new LinearCommandException("Linear transition command is missing a required option.");
        if (values["--event"] is not ("queue" or "claim" or "result" or "review" or "handoff" or "merge")) throw new LinearCommandException("Transition event is not supported.");
        var merge = values["--event"] == "merge";
        if (merge != (values.ContainsKey("--verification") && values.ContainsKey("--merge-sha") && values.ContainsKey("--repository"))) throw new LinearCommandException("Merge transition requires --verification, --merge-sha, and --repository; other events forbid them.");
        if (values["--event"] != "handoff" && (values.ContainsKey("--blocker") || values.ContainsKey("--resolve-blocker"))) throw new LinearCommandException("Blocker options are valid only for handoff.");
        return new(values["--issue"], values["--event"], Path.GetFullPath(values["--snapshot"]), Path.GetFullPath(values["--evidence"]), values["--api-key-env"], Path.GetFullPath(values["--output"]), values.GetValueOrDefault("--verification") is { } verification ? Path.GetFullPath(verification) : null, values.GetValueOrDefault("--merge-sha"), values.GetValueOrDefault("--repository") is { } repository ? Path.GetFullPath(repository) : null, values.GetValueOrDefault("--blocker"), values.GetValueOrDefault("--resolve-blocker"));
    }
}
internal static class LinearOptions
{
    internal static Dictionary<string, string> Parse(IReadOnlyList<string> args, string subcommand, IReadOnlyList<string> allowed)
    {
        if (args.Count != 2 + allowed.Count * 2 || args[0] != "linear" || args[1] != subcommand) throw new LinearCommandException($"Linear {subcommand} command requires its complete named option set exactly once.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 2; index < args.Count; index += 2) if (!allowed.Contains(args[index], StringComparer.Ordinal) || string.IsNullOrWhiteSpace(args[index + 1]) || !values.TryAdd(args[index], args[index + 1])) throw new LinearCommandException($"Linear {subcommand} command received an unknown, duplicate, or empty option.");
        return values;
    }
}

internal sealed record LinearState(string Id, string Name, string Type);
internal sealed record LinearLabel(string Id, string Name);
/// <summary>A blocking relation carrying enough identity to mutate safely: never delete by human identifier alone.</summary>
internal sealed record LinearRelation(string Id, string Type, string BlockingIssueId, string BlockingIssueIdentifier, string BlockedIssueId, string BlockedIssueIdentifier);
internal sealed record LinearLabelCatalog(IReadOnlyList<LinearLabel> TeamLabels, IReadOnlyList<LinearLabel> WorkspaceLabels);
internal sealed record LinearIssue(string Id, string Identifier, string Url, string Title, string UpdatedAt, LinearState State, string TeamId, string TeamKey, IReadOnlyList<LinearState> States, IReadOnlyList<LinearLabel> Labels, IReadOnlyList<string> Attachments, IReadOnlyList<LinearRelation> BlockedBy, IReadOnlyList<LinearRelation> Blocks);

internal static class LinearGraphQl
{
    internal static LinearIssue ParseIssue(LinearTransportResponse response)
    {
        if ((int)response.StatusCode is < 200 or >= 300) throw new LinearCommandException($"Linear HTTP request failed with status {(int)response.StatusCode}.");
        try
        {
            using var document = JsonDocument.Parse(response.Body);
            var root = document.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0) throw new LinearCommandException("Linear GraphQL response contains errors.");
            var issue = Required(Required(root, "data", "GraphQL response"), "issue", "GraphQL response data");
            var team = Required(issue, "team", "issue");
            return new LinearIssue(String(issue, "id"), String(issue, "identifier"), String(issue, "url"), String(issue, "title"), String(issue, "updatedAt"), State(Required(issue, "state", "issue")), String(team, "id"), String(team, "key"), States(Required(Required(team, "states", "team"), "nodes", "states")), Labels(Required(Required(issue, "labels", "issue"), "nodes", "labels")), Strings(Required(Required(issue, "attachments", "issue"), "nodes", "attachments"), "url"), Relations(Required(Required(issue, "blockedBy", "issue"), "nodes", "blockedBy")), Relations(Required(Required(issue, "blocks", "issue"), "nodes", "blocks")));
        }
        catch (JsonException exception) { throw new LinearCommandException($"Linear response is malformed JSON: {exception.Message}"); }
    }
    internal static void RequireSuccessfulMutation(LinearTransportResponse response, string property)
    {
        if ((int)response.StatusCode is < 200 or >= 300) throw new LinearCommandException($"Linear HTTP request failed with status {(int)response.StatusCode}.");
        try
        {
            using var document = JsonDocument.Parse(response.Body); var root = document.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0) throw new LinearCommandException("Linear GraphQL response contains errors.");
            var result = Required(Required(root, "data", "GraphQL response"), property, "GraphQL response data");
            if (!result.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True) throw new LinearCommandException("Linear mutation reported success: false.");
        }
        catch (JsonException exception) { throw new LinearCommandException($"Linear response is malformed JSON: {exception.Message}"); }
    }
    private static JsonElement Required(JsonElement element, string name, string subject) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) ? value : throw new LinearCommandException($"Linear {subject} is missing required property '{name}'.");
    private static string String(JsonElement element, string name) { var value = Required(element, name, "object"); return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new LinearCommandException($"Linear property '{name}' must be a non-empty string."); }
    private static LinearState State(JsonElement e) => new(String(e, "id"), String(e, "name"), String(e, "type"));
    private static IReadOnlyList<LinearState> States(JsonElement e) => e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().Select(State).ToList() : throw new LinearCommandException("Linear states must be an array.");
    private static IReadOnlyList<LinearLabel> Labels(JsonElement e) => e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().Select(x => new LinearLabel(String(x, "id"), String(x, "name"))).ToList() : throw new LinearCommandException("Linear labels must be an array.");
    private static IReadOnlyList<string> Strings(JsonElement e, string property) => e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().Select(x => String(x, property)).ToList() : throw new LinearCommandException("Linear relation must be an array.");
    private static IReadOnlyList<LinearRelation> Relations(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Array) throw new LinearCommandException("Linear relation set must be an array.");
        return e.EnumerateArray().Select(x =>
        {
            var blocking = Required(x, "blockingIssue", "relation");
            var blocked = Required(x, "blockedIssue", "relation");
            return new LinearRelation(String(x, "id"), String(x, "type"), String(blocking, "id"), String(blocking, "identifier"), String(blocked, "id"), String(blocked, "identifier"));
        }).ToList();
    }
    internal static LinearLabelCatalog ParseLabelCatalog(LinearTransportResponse response)
    {
        var data = Data(response);
        var team = Required(data, "team", "GraphQL response data");
        return new LinearLabelCatalog(Labels(Required(Required(team, "labels", "team"), "nodes", "team labels")), Labels(Required(Required(data, "workspaceLabels", "GraphQL response data"), "nodes", "workspace labels")));
    }
    internal static LinearLabel ParseLabelCreate(LinearTransportResponse response)
    {
        var result = Required(Data(response), "issueLabelCreate", "GraphQL response data");
        if (!result.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True) throw new LinearCommandException("Linear mutation reported success: false.");
        var label = Required(result, "issueLabel", "issueLabelCreate");
        return new LinearLabel(String(label, "id"), String(label, "name"));
    }
    internal static LinearRelation ParseRelationCreate(LinearTransportResponse response)
    {
        var result = Required(Data(response), "issueRelationCreate", "GraphQL response data");
        if (!result.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True) throw new LinearCommandException("Linear mutation reported success: false.");
        var relation = Required(result, "issueRelation", "issueRelationCreate");
        var issue = Required(relation, "issue", "relation");
        var related = Required(relation, "relatedIssue", "relation");
        return new LinearRelation(String(relation, "id"), String(relation, "type"), String(issue, "id"), String(issue, "identifier"), String(related, "id"), String(related, "identifier"));
    }
    private static JsonElement Data(LinearTransportResponse response)
    {
        if ((int)response.StatusCode is < 200 or >= 300) throw new LinearCommandException($"Linear HTTP request failed with status {(int)response.StatusCode}.");
        JsonDocument document;
        try { document = JsonDocument.Parse(response.Body); }
        catch (JsonException exception) { throw new LinearCommandException($"Linear response is malformed JSON: {exception.Message}"); }
        var root = document.RootElement;
        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0) throw new LinearCommandException("Linear GraphQL response contains errors.");
        return Required(root, "data", "GraphQL response");
    }
}

internal sealed record LinearIssueSnapshot(string Id, string Identifier, string Url, string TeamId, string TeamKey, string StateId, string StateName, string StateType, string UpdatedAt, IReadOnlyList<LinearState> States, IReadOnlyList<LinearLabel> Labels, IReadOnlyList<string> Attachments, IReadOnlyList<LinearRelation> BlockedBy, IReadOnlyList<LinearRelation> Blocks)
{
    internal static LinearIssueSnapshot From(LinearIssue issue) => new(issue.Id, issue.Identifier, issue.Url, issue.TeamId, issue.TeamKey, issue.State.Id, issue.State.Name, issue.State.Type, issue.UpdatedAt, issue.States, issue.Labels, issue.Attachments, issue.BlockedBy, issue.Blocks);
    internal void RequireSameConcurrencyFacts(LinearIssueSnapshot live)
    {
        if (Id != live.Id || Identifier != live.Identifier || TeamId != live.TeamId || TeamKey != live.TeamKey || StateId != live.StateId || StateName != live.StateName || StateType != live.StateType || UpdatedAt != live.UpdatedAt || !Labels.SequenceEqual(live.Labels) || !BlockedBy.SequenceEqual(live.BlockedBy) || !Blocks.SequenceEqual(live.Blocks)) throw new LinearCommandException("Linear snapshot is stale or relevant issue facts changed; no mutation was attempted.");
    }
    private static object Wire(LinearRelation r) => new { relation_id = r.Id, type = r.Type, blocking_issue_id = r.BlockingIssueId, blocking_issue_identifier = r.BlockingIssueIdentifier, blocked_issue_id = r.BlockedIssueId, blocked_issue_identifier = r.BlockedIssueIdentifier };
    internal string ToJson() => JsonSerializer.Serialize(new { schema = "tlaw.dispatcher-linear-snapshot/v1", id = Id, identifier = Identifier, url = Url, team_id = TeamId, team_key = TeamKey, state_id = StateId, state_name = StateName, state_type = StateType, updated_at = UpdatedAt, states = States.Select(x => new { id = x.Id, name = x.Name, type = x.Type }), labels = Labels.Select(x => new { id = x.Id, name = x.Name }), attachments = Attachments, blocked_by = BlockedBy.Select(Wire), blocks = Blocks.Select(Wire) }, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    internal static LinearIssueSnapshot Parse(string text)
    {
        using var document = JsonDocument.Parse(text); var root = document.RootElement;
        static string S(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()! : throw new LinearCommandException($"Linear snapshot property '{n}' is required.");
        static List<string> A(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()! : throw new LinearCommandException($"Snapshot '{n}' is invalid.")).ToList() : throw new LinearCommandException($"Snapshot '{n}' is required.");
        static List<LinearRelation> R(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => new LinearRelation(S(x, "relation_id"), S(x, "type"), S(x, "blocking_issue_id"), S(x, "blocking_issue_identifier"), S(x, "blocked_issue_id"), S(x, "blocked_issue_identifier"))).ToList() : throw new LinearCommandException($"Snapshot '{n}' is required.");
        if (S(root, "schema") != "tlaw.dispatcher-linear-snapshot/v1") throw new LinearCommandException("Linear snapshot schema is invalid.");
        var states = root.GetProperty("states").EnumerateArray().Select(x => new LinearState(S(x, "id"), S(x, "name"), S(x, "type"))).ToList();
        var labels = root.GetProperty("labels").EnumerateArray().Select(x => new LinearLabel(S(x, "id"), S(x, "name"))).ToList();
        return new(S(root,"id"),S(root,"identifier"),S(root,"url"),S(root,"team_id"),S(root,"team_key"),S(root,"state_id"),S(root,"state_name"),S(root,"state_type"),S(root,"updated_at"),states,labels,A(root,"attachments"),R(root,"blocked_by"),R(root,"blocks"));
    }
}

internal sealed record LinearDispatchProfile(string BaseSha, string GitHubSource, string Objective, string WorkType, string PreferredAgent, IReadOnlyList<string> EligibleAgents, IReadOnlyList<string> RequiredCapabilities, string AutonomyLevel, IReadOnlyList<string> ForbiddenOperations, bool HandoffRequired, string Worktree, VerificationRequirement Verification, DeliveryContract Delivery)
{
    internal static LinearDispatchProfile Parse(string json)
    {
        using var document = JsonDocument.Parse(json); var root = document.RootElement;
        var allowed = new HashSet<string>(["schema", "base_sha", "github_source", "objective", "work_type", "preferred_agent", "eligible_agents", "required_capabilities", "autonomy_level", "forbidden_operations", "handoff_required", "worktree", "verification", "delivery", "label_profiles"], StringComparer.Ordinal);
        if (root.ValueKind != JsonValueKind.Object) throw new LinearCommandException("Linear dispatch profile must be a JSON object.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject()) if (!allowed.Contains(property.Name) || !seen.Add(property.Name)) throw new LinearCommandException("Linear dispatch profile contains an unknown or duplicate property.");
        if (seen.Count != allowed.Count || S(root, "schema") != "tlaw.dispatcher-linear-profile/v1") throw new LinearCommandException("Linear dispatch profile has an invalid schema or missing required field.");
        var sha = S(root, "base_sha");
        if (!Regex.IsMatch(sha, "^[0-9a-f]{40}$", RegexOptions.CultureInvariant)) throw new LinearCommandException("Linear dispatch profile base_sha must be 40 lowercase hexadecimal characters.");
        var source = S(root, "github_source");
        if (!Regex.IsMatch(source, "^https://github\\.com/[^/\\s]+/[^/\\s]+/(issues|pull)/[1-9][0-9]*$", RegexOptions.CultureInvariant)) throw new LinearCommandException("Linear dispatch profile github_source must be exactly one GitHub issue or pull-request HTTPS URL.");
        var labelProfiles = root.GetProperty("label_profiles");
        if (labelProfiles.ValueKind != JsonValueKind.Object || labelProfiles.EnumerateObject().Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Value.ValueKind != JsonValueKind.Object || x.Value.EnumerateObject().Any())) throw new LinearCommandException("Linear label_profiles permits only explicit empty marker objects.");
        return new(sha, source, S(root,"objective"), S(root,"work_type"), S(root,"preferred_agent"), A(root,"eligible_agents"), A(root,"required_capabilities"), S(root,"autonomy_level"), A(root,"forbidden_operations"), B(root,"handoff_required"), S(root,"worktree"), new VerificationRequirement(B(root.GetProperty("verification"),"required"),A(root.GetProperty("verification"),"commands")), new DeliveryContract(B(root.GetProperty("delivery"),"branch_required"),B(root.GetProperty("delivery"),"draft_pr_required"),B(root.GetProperty("delivery"),"merge_forbidden")));
    }
    private static string S(JsonElement e, string n) => e.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.GetString()) ? p.GetString()! : throw new LinearCommandException($"Linear dispatch profile property '{n}' must be a non-empty string.");
    private static bool B(JsonElement e, string n) => e.TryGetProperty(n, out var p) && p.ValueKind is JsonValueKind.True or JsonValueKind.False ? p.GetBoolean() : throw new LinearCommandException($"Linear dispatch profile property '{n}' must be boolean.");
    private static IReadOnlyList<string> A(JsonElement e, string n)
    {
        if (!e.TryGetProperty(n, out var p) || p.ValueKind != JsonValueKind.Array || p.GetArrayLength() == 0) throw new LinearCommandException($"Linear dispatch profile property '{n}' must be a non-empty array.");
        var values = p.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()) ? x.GetString()! : throw new LinearCommandException($"Linear dispatch profile property '{n}' is invalid.")).ToList();
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Count) throw new LinearCommandException($"Linear dispatch profile property '{n}' has duplicate values.");
        return values;
    }
}

internal static class LinearInputJson
{
    internal static string Write(LinearDispatchProfile p, LinearIssueSnapshot s)
    {
        var value = new { schema = "tlaw.dispatcher-input/v1", task_id = s.Identifier, source_id = s.Identifier, sources = new[] { s.Url, p.GitHubSource }, objective = p.Objective, work_type = p.WorkType, preferred_agent = p.PreferredAgent, eligible_agents = p.EligibleAgents, required_capabilities = p.RequiredCapabilities, autonomy_level = p.AutonomyLevel, forbidden_operations = p.ForbiddenOperations, claimed_by = "unclaimed", claim_id = "unclaimed", claim_started_at = "unclaimed", claim_expires_at = "unclaimed", base_sha = p.BaseSha, handoff_required = p.HandoffRequired, worktree = p.Worktree, verification = new { required = p.Verification.Required, commands = p.Verification.Commands }, delivery = new { branch_required = p.Delivery.BranchRequired, draft_pr_required = p.Delivery.DraftPrRequired, merge_forbidden = p.Delivery.MergeForbidden } };
        return JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }
}

internal sealed record TransitionEvidence(string Schema, string TaskId, string? NextState, string? Decision, string Hash)
{
    internal static TransitionEvidence Parse(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes); var root = document.RootElement;
        static string? Value(JsonElement e, string n) => e.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        return new(Value(root,"schema") ?? throw new LinearCommandException("Transition evidence schema is required."), Value(root,"task_id") ?? throw new LinearCommandException("Transition evidence task_id is required."), Value(root,"next_state"), Value(root,"decision"), Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
}
internal sealed record LinearTransitionPlan(string TargetState, string? TargetStateId, bool IsNoOp = false);
internal static class LinearTransitionPlanner
{
    internal static LinearTransitionPlan Plan(string evt, LinearIssueSnapshot snapshot, TransitionEvidence evidence)
    {
        if (snapshot.Identifier != evidence.TaskId) throw new LinearCommandException("Transition evidence task identity does not match Linear issue.");
        return evt switch
        {
            "queue" when snapshot.StateName == "Backlog" && evidence.Schema == "tlaw.agent-task/v2" => new("Todo", null),
            "claim" when snapshot.StateName == "Todo" && evidence.Schema == "tlaw.agent-task/v2" => new("In Progress", null),
            "result" when snapshot.StateName == "In Progress" && evidence.Schema == "tlaw.dispatcher-finalization/v1" && evidence.NextState == "in_review" => new("In Review", null),
            "result" when snapshot.StateName == "In Progress" && evidence.Schema == "tlaw.dispatcher-finalization/v1" && evidence.NextState == "todo" => new("Todo", null),
            "review" when snapshot.StateName == "In Review" && evidence.Schema == "tlaw.dispatcher-review-decision/v1" && evidence.Decision == "correction" && evidence.NextState == "todo" => new("Todo", null),
            "review" when snapshot.StateName == "In Review" && evidence.Schema == "tlaw.dispatcher-review-decision/v1" && (evidence.Decision is "human" or "merge") && evidence.NextState == "in_review" => new("In Review", snapshot.StateId, true),
            "handoff" when snapshot.StateName == "In Progress" && evidence.Schema == "tlaw.dispatcher-handoff-finalization/v1" => new("Todo", null),
            "merge" => throw new LinearCommandException("Merge transition requires repository-native verifier and Git ancestry evidence; the supplied generic evidence is insufficient."),
            _ => throw new LinearCommandException("Transition event, current Linear state, and deterministic evidence do not form an allowed edge.")
        };
    }
}
/// <summary>Resolves the exact ordinal <c>blocked</c> label against live team then workspace catalogs, creating it fail-closed when absent.</summary>
internal static class BlockedLabelResolution
{
    private const string LabelName = "blocked";
    private const string LabelDescription = "Task is blocked by an explicit Linear issue relation.";
    private const string LabelColor = "#EB5757"; // fixed repository-defined presentation color; carries no workflow authority.

    /// <summary>Returns the exact team-or-workspace label id, or <c>null</c> when no exact match exists; throws when ambiguous.</summary>
    internal static string? SelectExisting(LinearLabelCatalog catalog)
    {
        var team = catalog.TeamLabels.Where(l => string.Equals(l.Name, LabelName, StringComparison.Ordinal)).ToList();
        if (team.Count > 1) throw new LinearCommandException("the 'blocked' label is ambiguous in the team label catalog");
        if (team.Count == 1) return team[0].Id;
        var workspace = catalog.WorkspaceLabels.Where(l => string.Equals(l.Name, LabelName, StringComparison.Ordinal)).ToList();
        if (workspace.Count > 1) throw new LinearCommandException("the 'blocked' label is ambiguous in the workspace label catalog");
        return workspace.Count == 1 ? workspace[0].Id : null;
    }

    internal static string ResolveOrCreate(ILinearTransport transport, string teamId, string apiKey, LinearMutationJournal journal)
    {
        var existing = SelectExisting(FetchCatalog(transport, teamId, apiKey));
        if (existing is not null) return existing;

        var created = LinearGraphQl.ParseLabelCreate(transport.Send("IssueLabelCreate", "mutation IssueLabelCreate($name:String!,$description:String!,$color:String!,$teamId:String!){issueLabelCreate(input:{name:$name,description:$description,color:$color,teamId:$teamId}){success issueLabel{id name}}}", new { name = LabelName, description = LabelDescription, color = LabelColor, teamId }, apiKey));
        journal.Complete("blocked_label_created"); // remote object now durably exists; refetch/verification failures must still report it.
        if (!string.Equals(created.Name, LabelName, StringComparison.Ordinal)) throw new LinearCommandException("the newly created 'blocked' label returned an unexpected name");

        var refetched = FetchCatalog(transport, teamId, apiKey).TeamLabels.Where(l => string.Equals(l.Name, LabelName, StringComparison.Ordinal)).ToList();
        if (refetched.Count != 1) throw new LinearCommandException("the newly created 'blocked' label could not be uniquely verified by refetch");
        if (!string.Equals(refetched[0].Id, created.Id, StringComparison.Ordinal)) throw new LinearCommandException("the refetched 'blocked' label id did not match the creation result");
        return created.Id;
    }

    private static LinearLabelCatalog FetchCatalog(ILinearTransport transport, string teamId, string apiKey) =>
        LinearGraphQl.ParseLabelCatalog(transport.Send("LabelCatalog", "query LabelCatalog($teamId:String!){team(id:$teamId){id labels{nodes{id name}}} workspaceLabels{nodes{id name}}}", new { teamId }, apiKey));
}

/// <summary>Strict blocker-relation add/remove that only ever mutates by exact relation UUID and correct orientation.</summary>
internal static class BlockerRelationOps
{
    internal static bool IsBlockedByRelationFor(LinearRelation r, string blockerIdentifier, string blockedIssueId, string blockedIssueIdentifier) =>
        string.Equals(r.Type, "blocks", StringComparison.Ordinal)
        && string.Equals(r.BlockingIssueIdentifier, blockerIdentifier, StringComparison.Ordinal)
        && string.Equals(r.BlockedIssueId, blockedIssueId, StringComparison.Ordinal)
        && string.Equals(r.BlockedIssueIdentifier, blockedIssueIdentifier, StringComparison.Ordinal);

    internal static void AddBlocker(ILinearTransport transport, LinearIssueSnapshot live, string blockerIdentifier, string apiKey, LinearMutationJournal journal, List<LinearRelation> relations, List<LinearRelation> blockersAdded)
    {
        var blocker = LinearCommand.FetchIssue(transport, blockerIdentifier, apiKey);
        if (!string.Equals(blocker.Identifier, blockerIdentifier, StringComparison.Ordinal)) throw new LinearCommandException("the resolved blocker issue identifier does not match the request");
        if (string.Equals(blocker.Id, live.Id, StringComparison.Ordinal) || string.Equals(blocker.Identifier, live.Identifier, StringComparison.Ordinal)) throw new LinearCommandException("an issue cannot block itself");

        var existing = live.BlockedBy.Where(r => IsBlockedByRelationFor(r, blockerIdentifier, live.Id, live.Identifier) && string.Equals(r.BlockingIssueId, blocker.Id, StringComparison.Ordinal)).ToList();
        if (existing.Count > 1) throw new LinearCommandException("duplicate correctly oriented blocker relations already exist");
        if (existing.Count == 1) return; // idempotent existing correctly oriented relation.

        var created = LinearGraphQl.ParseRelationCreate(transport.Send("IssueRelationCreate", "mutation IssueRelationCreate($issueId:String!,$relatedIssueId:String!){issueRelationCreate(input:{issueId:$issueId,relatedIssueId:$relatedIssueId,type:blocks}){success issueRelation{id type issue{id identifier} relatedIssue{id identifier}}}}", new { issueId = blocker.Id, relatedIssueId = live.Id }, apiKey));
        journal.Complete("blocker_relation_added"); // remote relation now durably exists; verification failures must still report it.
        if (!RelationMatches(created, blocker.Id, blockerIdentifier, live.Id, live.Identifier)) throw new LinearCommandException("created blocker relation orientation is incorrect");

        var after = LinearIssueSnapshot.From(LinearCommand.FetchIssue(transport, live.Identifier, apiKey));
        var verified = after.BlockedBy.Where(r => string.Equals(r.Id, created.Id, StringComparison.Ordinal)).ToList();
        if (verified.Count != 1 || !RelationMatches(verified[0], blocker.Id, blockerIdentifier, live.Id, live.Identifier)) throw new LinearCommandException("created blocker relation could not be verified by refetch");
        if (!LinearCommand.SameRelationSet(after.BlockedBy.Where(r => !string.Equals(r.Id, created.Id, StringComparison.Ordinal)).ToList(), live.BlockedBy)) throw new LinearCommandException("blocker relation creation altered unrelated relations");
        relations.Add(created);
        blockersAdded.Add(created);
    }

    internal static (LinearRelation? Target, bool IdempotentMissing) ValidateRemovable(LinearTransitionOptions options, LinearIssueSnapshot live, LinearIssueSnapshot snapshot, TransitionEvidence evidence)
    {
        var resolved = options.ResolvedBlockerIdentifier!;
        var snapshotMatches = snapshot.BlockedBy.Where(r => IsBlockedByRelationFor(r, resolved, snapshot.Id, snapshot.Identifier)).ToList();
        if (snapshotMatches.Count > 1) throw new LinearCommandException("the supplied snapshot has duplicate blocker relations for the resolved blocker");
        if (snapshotMatches.Count == 0)
            return TryIdempotentMissing(options, live, snapshot, evidence, resolved)
                ? (null, true)
                : throw new LinearCommandException("the resolved blocker relation is absent and no matching durable receipt proves prior removal");

        var target = snapshotMatches[0];
        var liveMatches = live.BlockedBy.Where(r => string.Equals(r.Id, target.Id, StringComparison.Ordinal)).ToList();
        if (liveMatches.Count != 1) throw new LinearCommandException("live Linear no longer contains the exact resolved blocker relation UUID");
        var liveRelation = liveMatches[0];
        if (!string.Equals(liveRelation.Type, "blocks", StringComparison.Ordinal) || !string.Equals(liveRelation.Type, target.Type, StringComparison.Ordinal)) throw new LinearCommandException("the resolved blocker relation type changed");
        if (!string.Equals(liveRelation.BlockingIssueId, target.BlockingIssueId, StringComparison.Ordinal) || !string.Equals(liveRelation.BlockingIssueIdentifier, resolved, StringComparison.Ordinal)) throw new LinearCommandException("the resolved blocker blocking-issue identity changed");
        if (!string.Equals(liveRelation.BlockedIssueId, live.Id, StringComparison.Ordinal)) throw new LinearCommandException("the resolved blocker relation is not oriented against the current issue");
        if (live.BlockedBy.Count(r => IsBlockedByRelationFor(r, resolved, live.Id, live.Identifier)) != 1) throw new LinearCommandException("live Linear has duplicate blocker relations for the resolved blocker");
        return (target, false);
    }

    internal static void PerformDelete(ILinearTransport transport, LinearTransitionOptions options, LinearIssueSnapshot live, LinearRelation target, string apiKey, LinearMutationJournal journal, List<LinearRelation> relations, List<LinearRelation> blockersRemoved)
    {
        transport.Send("IssueRelationDelete", "mutation IssueRelationDelete($id:String!){issueRelationDelete(id:$id){success}}", new { id = target.Id }, apiKey).RequireMutation("issueRelationDelete");
        journal.Complete("blocker_relation_removed"); // remote deletion now durable; verification failures must still report it.
        var after = LinearIssueSnapshot.From(LinearCommand.FetchIssue(transport, options.IssueIdentifier, apiKey));
        if (after.BlockedBy.Any(r => string.Equals(r.Id, target.Id, StringComparison.Ordinal))) throw new LinearCommandException("post-delete refetch still contains the removed relation UUID");
        if (after.BlockedBy.Any(r => IsBlockedByRelationFor(r, target.BlockingIssueIdentifier, after.Id, after.Identifier))) throw new LinearCommandException("post-delete refetch contains an equivalent duplicate blocker relation");
        var expectedRemaining = live.BlockedBy.Where(r => !string.Equals(r.Id, target.Id, StringComparison.Ordinal)).ToList();
        if (!LinearCommand.SameRelationSet(after.BlockedBy, expectedRemaining)) throw new LinearCommandException("post-delete refetch altered unrelated blocker relations");
        relations.RemoveAll(r => string.Equals(r.Id, target.Id, StringComparison.Ordinal));
        blockersRemoved.Add(target);
    }

    private static bool RelationMatches(LinearRelation r, string blockingId, string blockingIdentifier, string blockedId, string blockedIdentifier) =>
        string.Equals(r.Type, "blocks", StringComparison.Ordinal)
        && string.Equals(r.BlockingIssueId, blockingId, StringComparison.Ordinal)
        && string.Equals(r.BlockingIssueIdentifier, blockingIdentifier, StringComparison.Ordinal)
        && string.Equals(r.BlockedIssueId, blockedId, StringComparison.Ordinal)
        && string.Equals(r.BlockedIssueIdentifier, blockedIdentifier, StringComparison.Ordinal);

    private static bool TryIdempotentMissing(LinearTransitionOptions options, LinearIssueSnapshot live, LinearIssueSnapshot snapshot, TransitionEvidence evidence, string resolved)
    {
        if (!File.Exists(options.OutputPath)) return false;
        LinearReceiptRecord receipt;
        try { receipt = LinearReceiptRecord.Parse(File.ReadAllText(options.OutputPath)); }
        catch (LinearCommandException) { return false; }
        if (!string.Equals(receipt.Event, "handoff", StringComparison.Ordinal) || !string.Equals(receipt.IssueIdentifier, snapshot.Identifier, StringComparison.Ordinal)) return false;
        var removed = receipt.BlockersRemoved.Where(r => string.Equals(r.BlockingIssueIdentifier, resolved, StringComparison.Ordinal)).ToList();
        if (removed.Count != 1) return false;
        var entry = removed[0];
        if (!string.Equals(entry.BlockedIssueId, snapshot.Id, StringComparison.Ordinal)) return false;
        if (!receipt.EvidenceHashes.Contains(evidence.Hash, StringComparer.Ordinal)) return false;
        // The removed relation must be genuinely absent live, and remaining labels/relations must match the receipt exactly.
        if (live.BlockedBy.Any(r => string.Equals(r.Id, entry.Id, StringComparison.Ordinal))) return false;
        if (live.BlockedBy.Any(r => IsBlockedByRelationFor(r, resolved, live.Id, live.Identifier))) return false;
        if (!LinearCommand.SameRelationSet(live.BlockedBy, receipt.RemainingBlockers)) return false;
        return live.Labels.Select(l => l.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(receipt.ResultingLabelIds.OrderBy(x => x, StringComparer.Ordinal));
    }
}

internal static class LinearReceiptJson
{
    internal static string Write(LinearIssueSnapshot before, string evt, LinearTransitionPlan plan, string hash, string postUpdatedAt) =>
        Serialize(before.Identifier, evt, before.StateId, before.StateName, plan.TargetStateId ?? "resolved-after-mutation", plan.TargetState, hash, postUpdatedAt, [], [], [], [], before.BlockedBy, before.Labels);
    internal static string WriteHandoff(LinearIssueSnapshot before, LinearState resultingState, string hash, string postUpdatedAt, IReadOnlyList<string> labelsAdded, IReadOnlyList<string> labelsRemoved, IReadOnlyList<LinearRelation> blockersAdded, IReadOnlyList<LinearRelation> blockersRemoved, IReadOnlyList<LinearRelation> remainingBlockers, IReadOnlyList<LinearLabel> resultingLabels) =>
        Serialize(before.Identifier, "handoff", before.StateId, before.StateName, resultingState.Id, resultingState.Name, hash, postUpdatedAt, labelsAdded, labelsRemoved, blockersAdded, blockersRemoved, remainingBlockers, resultingLabels);

    private static object Wire(LinearRelation r) => new { relation_id = r.Id, type = r.Type, blocking_issue_id = r.BlockingIssueId, blocking_issue_identifier = r.BlockingIssueIdentifier, blocked_issue_id = r.BlockedIssueId, blocked_issue_identifier = r.BlockedIssueIdentifier };
    private static string Serialize(string identifier, string evt, string previousStateId, string previousStateName, string resultingStateId, string resultingStateName, string hash, string postUpdatedAt, IReadOnlyList<string> labelsAdded, IReadOnlyList<string> labelsRemoved, IReadOnlyList<LinearRelation> blockersAdded, IReadOnlyList<LinearRelation> blockersRemoved, IReadOnlyList<LinearRelation> remainingBlockers, IReadOnlyList<LinearLabel> resultingLabels) =>
        JsonSerializer.Serialize(new { schema = "tlaw.dispatcher-linear-transition/v1", issue_identifier = identifier, @event = evt, previous_state_id = previousStateId, previous_state_name = previousStateName, resulting_state_id = resultingStateId, resulting_state_name = resultingStateName, evidence_sha256 = new[] { hash }, labels_added = labelsAdded, labels_removed = labelsRemoved, resulting_label_ids = resultingLabels.Select(l => l.Id), blockers_added = blockersAdded.Select(Wire), blockers_removed = blockersRemoved.Select(Wire), remaining_blockers = remainingBlockers.Select(Wire), post_mutation_updated_at = postUpdatedAt }, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
}

internal sealed record LinearReceiptRecord(string Event, string IssueIdentifier, IReadOnlyList<string> EvidenceHashes, IReadOnlyList<string> ResultingLabelIds, IReadOnlyList<LinearRelation> BlockersRemoved, IReadOnlyList<LinearRelation> RemainingBlockers)
{
    internal static LinearReceiptRecord Parse(string text)
    {
        using var document = JsonDocument.Parse(text); var root = document.RootElement;
        static string S(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()! : throw new LinearCommandException($"Linear receipt property '{n}' is required.");
        static List<string> Str(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()! : throw new LinearCommandException($"Linear receipt '{n}' is invalid.")).ToList() : throw new LinearCommandException($"Linear receipt '{n}' is required.");
        static List<LinearRelation> Rel(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => new LinearRelation(S(x, "relation_id"), S(x, "type"), S(x, "blocking_issue_id"), S(x, "blocking_issue_identifier"), S(x, "blocked_issue_id"), S(x, "blocked_issue_identifier"))).ToList() : throw new LinearCommandException($"Linear receipt '{n}' is required.");
        if (S(root, "schema") != "tlaw.dispatcher-linear-transition/v1") throw new LinearCommandException("Linear receipt schema is invalid.");
        return new(S(root, "event"), S(root, "issue_identifier"), Str(root, "evidence_sha256"), Str(root, "resulting_label_ids"), Rel(root, "blockers_removed"), Rel(root, "remaining_blockers"));
    }
}

internal interface IGitProofRunner { GitProofResult Run(string repositoryPath, params string[] arguments); }
internal sealed record GitProofResult(int ExitCode, string StandardOutput, bool TimedOut);
internal sealed class SystemGitProofRunner : IGitProofRunner
{
    public GitProofResult Run(string repositoryPath, params string[] arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = repositoryPath, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process is null) throw new LinearCommandException("Git proof process did not start.");
        if (!process.WaitForExit(5000)) { process.Kill(true); return new GitProofResult(-1, string.Empty, true); }
        return new GitProofResult(process.ExitCode, process.StandardOutput.ReadToEnd(), false);
    }
}
internal static class MergeTransitionProof
{
    internal static LinearTransitionPlan Validate(LinearIssueSnapshot snapshot, TransitionEvidence review, LinearTransitionOptions options, IGitProofRunner git)
    {
        if (snapshot.StateName != "In Review") throw new LinearCommandException("Only an In Review Linear issue may transition to Done.");
        if (review.Schema != "tlaw.dispatcher-review-decision/v1" || review.Decision != "merge" || review.NextState != "in_review") throw new LinearCommandException("Merge requires an approved BAR-37 merge review decision.");
        if (options.VerificationPath is null || options.MergeSha is null || options.RepositoryPath is null || !Regex.IsMatch(options.MergeSha, "^[0-9a-f]{40}$", RegexOptions.CultureInvariant) || !Directory.Exists(options.RepositoryPath)) throw new LinearCommandException("Merge proof paths or merge SHA are invalid.");
        var reviewedHead = MergeEvidence.ReadReviewedHead(File.ReadAllBytes(options.EvidencePath));
        VerificationArtifact.Validate(File.ReadAllBytes(options.VerificationPath), options.MergeSha);
        RequireGit(git.Run(options.RepositoryPath, "cat-file", "-e", reviewedHead + "^{commit}"));
        RequireGit(git.Run(options.RepositoryPath, "cat-file", "-e", options.MergeSha + "^{commit}"));
        RequireGit(git.Run(options.RepositoryPath, "merge-base", "--is-ancestor", reviewedHead, options.MergeSha));
        RequireGit(git.Run(options.RepositoryPath, "merge-base", "--is-ancestor", options.MergeSha, "origin/main"));
        var origin = git.Run(options.RepositoryPath, "rev-parse", "origin/main");
        RequireGit(origin);
        if (origin.StandardOutput.Trim() != options.MergeSha) throw new LinearCommandException("Git origin/main is not exactly the supplied merge SHA.");
        return new LinearTransitionPlan("Done", null);
    }
    private static void RequireGit(GitProofResult result) { if (result.TimedOut) throw new LinearCommandException("Git merge proof timed out."); if (result.ExitCode != 0) throw new LinearCommandException("Git merge proof failed."); }
}
internal static class MergeEvidence
{
    internal static string ReadReviewedHead(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes); var root = document.RootElement;
        static string S(JsonElement e, string n) => e.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : throw new LinearCommandException($"Review decision missing '{n}'.");
        if (S(root,"schema") != "tlaw.dispatcher-review-decision/v1" || S(root,"verdict") != "approve" || S(root,"decision") != "merge" || S(root,"next_state") != "in_review" || !root.TryGetProperty("blocking_findings",out var b) || b.ValueKind != JsonValueKind.Number || b.GetInt32() != 0) throw new LinearCommandException("Review decision does not prove an approved merge.");
        var head=S(root,"reviewed_head"); return Regex.IsMatch(head,"^[0-9a-f]{40}$",RegexOptions.CultureInvariant) ? head : throw new LinearCommandException("Review decision reviewed_head is invalid.");
    }
}
internal static class VerificationArtifact
{
    internal static void Validate(byte[] bytes, string mergeSha)
    {
        using var document=JsonDocument.Parse(bytes);var root=document.RootElement;
        static string S(JsonElement e,string n)=>e.TryGetProperty(n,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString()!:throw new LinearCommandException($"Verification artifact missing '{n}'.");
        if(S(root,"schema")!="tlaw.verification/v1" || S(root,"verdict")!="PASS")throw new LinearCommandException("Verification artifact is not PASS.");
        if(S(root,"expected_head")!=mergeSha||S(root,"actual_head")!=mergeSha)throw new LinearCommandException("Verification artifact head does not match merge SHA.");
        foreach(var gate in new[]{"build","tests","diff_check","gate0","canonical_objects","architecture","domain_dependencies","clean_tree"})if(!root.TryGetProperty(gate,out var v)||(v.ValueKind!=JsonValueKind.True&&!(v.ValueKind==JsonValueKind.String&&v.GetString()=="PASS")))throw new LinearCommandException($"Verification artifact gate '{gate}' is missing or failed.");
    }
}
