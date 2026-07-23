using System.Net;
using System.Text;
using System.Text.Json;
using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

/// <summary>
/// Deterministic fake-GraphQL coverage for the two guarded handoff details:
/// exact <c>blocked</c> label resolution and strict blocker-relation mutation semantics.
/// </summary>
public sealed class LinearHandoffMutationTests
{
    private const string Secret = "never-print-this-secret";
    private const string Updated = "2026-07-23T00:00:00Z";

    // ---------- 1. Exact blocked-label resolution ----------

    [Fact]
    public void Select_existing_returns_exact_team_label()
        => Assert.Equal("t1", BlockedLabelResolution.SelectExisting(new LinearLabelCatalog([new("t1", "blocked")], [new("w1", "other")])));

    [Fact]
    public void Select_existing_returns_exact_workspace_label_when_team_has_none()
        => Assert.Equal("w1", BlockedLabelResolution.SelectExisting(new LinearLabelCatalog([new("t1", "other")], [new("w1", "blocked")])));

    [Fact]
    public void Select_existing_ignores_case_different_and_near_names()
        => Assert.Null(BlockedLabelResolution.SelectExisting(new LinearLabelCatalog([new("t1", "Blocked"), new("t2", "blocker")], [new("w1", "is-blocked"), new("w2", "blocked-task")])));

    [Fact]
    public void Select_existing_fails_closed_on_duplicate_exact_names()
        => Assert.Throws<LinearCommandException>(() => BlockedLabelResolution.SelectExisting(new LinearLabelCatalog([new("t1", "blocked"), new("t2", "blocked")], [])));

    [Fact]
    public void Resolve_returns_catalog_label_without_creating_when_present()
    {
        var journal = new LinearMutationJournal();
        var transport = new QueueLinear().On("LabelCatalog", Catalog([L("bl", "blocked")], []));
        Assert.Equal("bl", BlockedLabelResolution.ResolveOrCreate(transport, "team", Secret, journal));
        Assert.Empty(journal.Describe());
        Assert.DoesNotContain(transport.Calls, c => c.Op == "IssueLabelCreate");
    }

    [Fact]
    public void Resolve_creates_and_verifies_when_absent_everywhere()
    {
        var journal = new LinearMutationJournal();
        var transport = new QueueLinear()
            .On("LabelCatalog", Catalog([], []), Catalog([L("new", "blocked")], []))
            .On("IssueLabelCreate", LabelCreate(success: true, "new", "blocked"));
        Assert.Equal("new", BlockedLabelResolution.ResolveOrCreate(transport, "team", Secret, journal));
        Assert.Equal("blocked_label_created", journal.Describe());
    }

    [Fact]
    public void Resolve_reports_created_when_verification_refetch_fails()
    {
        var journal = new LinearMutationJournal();
        var transport = new QueueLinear()
            .On("LabelCatalog", Catalog([], []), GraphErrors())
            .On("IssueLabelCreate", LabelCreate(success: true, "new", "blocked"));
        var error = Assert.Throws<LinearCommandException>(() => BlockedLabelResolution.ResolveOrCreate(transport, "team", Secret, journal));
        Assert.Contains("blocked_label_created", journal.Describe(), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_fails_and_does_not_journal_when_create_returns_success_false()
    {
        var journal = new LinearMutationJournal();
        var transport = new QueueLinear()
            .On("LabelCatalog", Catalog([], []))
            .On("IssueLabelCreate", LabelCreate(success: false, "new", "blocked"));
        Assert.Throws<LinearCommandException>(() => BlockedLabelResolution.ResolveOrCreate(transport, "team", Secret, journal));
        Assert.DoesNotContain("blocked_label_created", journal.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_fails_on_http200_graphql_errors_during_creation()
    {
        var journal = new LinearMutationJournal();
        var transport = new QueueLinear()
            .On("LabelCatalog", Catalog([], []))
            .On("IssueLabelCreate", GraphErrors());
        Assert.Throws<LinearCommandException>(() => BlockedLabelResolution.ResolveOrCreate(transport, "team", Secret, journal));
        Assert.DoesNotContain("blocked_label_created", journal.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_never_leaks_api_key_in_create_diagnostics()
    {
        var journal = new LinearMutationJournal();
        var transport = new QueueLinear()
            .On("LabelCatalog", Catalog([], []))
            .On("IssueLabelCreate", new LinearTransportResponse(HttpStatusCode.InternalServerError, "boom"));
        var error = Assert.Throws<LinearCommandException>(() => BlockedLabelResolution.ResolveOrCreate(transport, "team", Secret, journal));
        Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, journal.Describe(), StringComparison.Ordinal);
    }

    // ---------- 2. Strict blocker relations: adding ----------

    [Fact]
    public void Add_blocker_creates_exact_correctly_oriented_relation()
    {
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []);
        var journal = new LinearMutationJournal();
        var relations = new List<LinearRelation>();
        var added = new List<LinearRelation>();
        var transport = new QueueLinear()
            .On("Issue:BAR-9", ResponseFor("uuid9", "BAR-9", "Todo", "unstarted", [], []))
            .On("IssueRelationCreate", RelationCreate(success: true, "rel1", "uuid9", "BAR-9", "uuid41", "BAR-41"))
            .On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41")]));
        BlockerRelationOps.AddBlocker(transport, live, "BAR-9", Secret, journal, relations, added);
        Assert.Equal("rel1", Assert.Single(added).Id);
        Assert.Equal("blocker_relation_added", journal.Describe());
    }

    [Fact]
    public void Add_blocker_is_idempotent_for_existing_exact_relation()
    {
        var existing = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [existing]);
        var journal = new LinearMutationJournal();
        var relations = new List<LinearRelation> { existing };
        var added = new List<LinearRelation>();
        var transport = new QueueLinear().On("Issue:BAR-9", ResponseFor("uuid9", "BAR-9", "Todo", "unstarted", [], []));
        BlockerRelationOps.AddBlocker(transport, live, "BAR-9", Secret, journal, relations, added);
        Assert.Empty(added);
        Assert.Empty(journal.Describe());
        Assert.DoesNotContain(transport.Calls, c => c.Op == "IssueRelationCreate");
    }

    [Fact]
    public void Add_blocker_rejects_self_blocking()
    {
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []);
        var transport = new QueueLinear().On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], []));
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.AddBlocker(transport, live, "BAR-41", Secret, new LinearMutationJournal(), [], []));
    }

    [Fact]
    public void Add_blocker_fails_closed_on_duplicate_correctly_oriented_relations()
    {
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41"), Rel("rel2", "uuid9", "BAR-9", "uuid41", "BAR-41")]);
        var transport = new QueueLinear().On("Issue:BAR-9", ResponseFor("uuid9", "BAR-9", "Todo", "unstarted", [], []));
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.AddBlocker(transport, live, "BAR-9", Secret, new LinearMutationJournal(), [], []));
    }

    [Fact]
    public void Add_blocker_reports_created_when_verification_refetch_missing()
    {
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []);
        var journal = new LinearMutationJournal();
        var transport = new QueueLinear()
            .On("Issue:BAR-9", ResponseFor("uuid9", "BAR-9", "Todo", "unstarted", [], []))
            .On("IssueRelationCreate", RelationCreate(success: true, "rel1", "uuid9", "BAR-9", "uuid41", "BAR-41"))
            .On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], []));
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.AddBlocker(transport, live, "BAR-9", Secret, journal, [], []));
        Assert.Equal("blocker_relation_added", journal.Describe());
    }

    // ---------- 2. Strict blocker relations: validating a removal ----------

    [Fact]
    public void Validate_removable_returns_exact_target()
    {
        var target = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [target]);
        var (result, idempotent) = BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9"), snap, snap, Evidence());
        Assert.False(idempotent);
        Assert.Equal("rel1", result!.Id);
    }

    [Fact]
    public void Validate_removable_rejects_reversed_relation()
    {
        var reversed = Rel("rel1", "uuid41", "BAR-41", "uuid9", "BAR-9"); // BAR-41 blocks BAR-9, not the requested orientation
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [reversed]);
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9"), snap, snap, Evidence()));
    }

    [Fact]
    public void Validate_removable_refuses_changed_relation_uuid()
    {
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41")]);
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel2", "uuid9", "BAR-9", "uuid41", "BAR-41")]);
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9"), live, snap, Evidence()));
    }

    [Fact]
    public void Validate_removable_refuses_changed_orientation()
    {
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41")]);
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel1", "uuid9", "BAR-9", "uuidOTHER", "BAR-77")]);
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9"), live, snap, Evidence()));
    }

    [Fact]
    public void Validate_removable_refuses_changed_blocking_issue_uuid()
    {
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41")]);
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel1", "uuidOTHER", "BAR-9", "uuid41", "BAR-41")]);
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9"), live, snap, Evidence()));
    }

    [Fact]
    public void Validate_removable_fails_closed_on_duplicate_snapshot_relations()
    {
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41"), Rel("rel2", "uuid9", "BAR-9", "uuid41", "BAR-41")]);
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9"), snap, snap, Evidence()));
    }

    [Fact]
    public void Missing_relation_without_receipt_fails()
    {
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []);
        var options = Options(resolve: "BAR-9", output: Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.json"));
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.ValidateRemovable(options, snap, snap, Evidence()));
    }

    [Fact]
    public void Missing_relation_with_matching_receipt_is_idempotent_success()
    {
        using var workspace = Workspace.Create();
        var receipt = workspace.Write("receipt.json", Receipt(["hash-abc"], removed: [Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41")], remaining: []));
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []);
        var (target, idempotent) = BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9", output: receipt), snap, snap, Evidence("hash-abc"));
        Assert.True(idempotent);
        Assert.Null(target);
    }

    [Fact]
    public void Missing_relation_with_different_evidence_hash_fails()
    {
        using var workspace = Workspace.Create();
        var receipt = workspace.Write("receipt.json", Receipt(["other-hash"], removed: [Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41")], remaining: []));
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []);
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9", output: receipt), snap, snap, Evidence("hash-abc")));
    }

    // ---------- 2. Strict blocker relations: performing a deletion ----------

    [Fact]
    public void Perform_delete_uses_exact_uuid_and_preserves_unrelated_relations()
    {
        var target = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var unrelated = Rel("rel2", "uuid7", "BAR-7", "uuid41", "BAR-41");
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [target, unrelated]);
        var journal = new LinearMutationJournal();
        var relations = new List<LinearRelation> { target, unrelated };
        var removed = new List<LinearRelation>();
        var transport = new QueueLinear()
            .On("IssueRelationDelete", MutationOk("issueRelationDelete"))
            .On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], [unrelated]));
        BlockerRelationOps.PerformDelete(transport, Options(resolve: "BAR-9"), live, target, Secret, journal, relations, removed);
        Assert.Equal("blocker_relation_removed", journal.Describe());
        Assert.Equal("rel2", Assert.Single(relations).Id);
        Assert.Equal("rel1", Assert.Single(removed).Id);
        var delete = Assert.Single(transport.Calls, c => c.Op == "IssueRelationDelete");
        Assert.Equal("rel1", JsonDocument.Parse(delete.Vars).RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public void Perform_delete_detects_equivalent_duplicate_after_deletion()
    {
        var target = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [target]);
        var journal = new LinearMutationJournal();
        var transport = new QueueLinear()
            .On("IssueRelationDelete", MutationOk("issueRelationDelete"))
            .On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("relDup", "uuid9", "BAR-9", "uuid41", "BAR-41")]));
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.PerformDelete(transport, Options(resolve: "BAR-9"), live, target, Secret, journal, [target], []));
        Assert.Equal("blocker_relation_removed", journal.Describe());
    }

    [Fact]
    public void Relation_diagnostics_never_leak_the_api_key()
    {
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []);
        var transport = new QueueLinear()
            .On("Issue:BAR-9", ResponseFor("uuid9", "BAR-9", "Todo", "unstarted", [], []))
            .On("IssueRelationCreate", new LinearTransportResponse(HttpStatusCode.InternalServerError, "boom"));
        var error = Assert.Throws<LinearCommandException>(() => BlockerRelationOps.AddBlocker(transport, live, "BAR-9", Secret, new LinearMutationJournal(), [], []));
        Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
    }

    // ---------- 4. Final expected handoff outcomes (end-to-end) ----------

    [Fact]
    public void Human_handoff_creates_label_adds_blocker_and_moves_to_todo()
    {
        using var workspace = Workspace.Create();
        var snapshot = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []);
        var rel = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var blocked = L("bl", "blocked");
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], []),                                 // Transition live refetch
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], [rel]),                              // AddBlocker verification
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [new("bl", "blocked")], [rel]))              // post-mutation verification
            .On("LabelCatalog", Catalog([], []), Catalog([L("bl", "blocked")], []))
            .On("IssueLabelCreate", LabelCreate(success: true, "bl", "blocked"))
            .On("Issue:BAR-9", ResponseFor("uuid9", "BAR-9", "Todo", "unstarted", [], []))
            .On("IssueRelationCreate", RelationCreate(success: true, "rel1", "uuid9", "BAR-9", "uuid41", "BAR-41"))
            .On("IssueUpdate", MutationOk("issueUpdate"), MutationOk("issueUpdate"));
        var output = new StringWriter();
        var exit = RunHandoff(workspace, snapshot, "human", transport, output, blocker: "BAR-9");
        Assert.Equal(0, exit);
        Assert.Contains("handoff -> Todo", output.ToString(), StringComparison.Ordinal);
        var receipt = LinearReceiptRecord.Parse(File.ReadAllText(workspace.OutputPath));
        Assert.Equal("rel1", Assert.Single(receipt.RemainingBlockers).Id);
        Assert.Contains("bl", receipt.ResultingLabelIds);
    }

    [Fact]
    public void Human_handoff_does_not_duplicate_an_existing_issue_label()
    {
        using var workspace = Workspace.Create();
        var existingLabel = L("bl", "blocked");
        var snapshot = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], []);
        var rel = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], []),
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]),
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [new("bl", "blocked")], [rel]))
            .On("LabelCatalog", Catalog([L("bl", "blocked")], []))
            .On("Issue:BAR-9", ResponseFor("uuid9", "BAR-9", "Todo", "unstarted", [], []))
            .On("IssueRelationCreate", RelationCreate(success: true, "rel1", "uuid9", "BAR-9", "uuid41", "BAR-41"))
            .On("IssueUpdate", MutationOk("issueUpdate"));
        var exit = RunHandoff(workspace, snapshot, "human", transport, new StringWriter(), blocker: "BAR-9");
        Assert.Equal(0, exit);
        Assert.Single(transport.Calls, c => c.Op == "IssueUpdate"); // only the state change, no duplicate label attach
    }

    [Fact]
    public void Reassign_without_resolution_preserves_labels_and_blockers()
    {
        using var workspace = Workspace.Create();
        var rel = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var snapshot = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]);
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]),
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [new("bl", "blocked")], [rel]))
            .On("IssueUpdate", MutationOk("issueUpdate"));
        var exit = RunHandoff(workspace, snapshot, "reassign", transport, new StringWriter());
        Assert.Equal(0, exit);
        Assert.Single(transport.Calls, c => c.Op == "IssueUpdate");
        var receipt = LinearReceiptRecord.Parse(File.ReadAllText(workspace.OutputPath));
        Assert.Equal("rel1", Assert.Single(receipt.RemainingBlockers).Id);
        Assert.Contains("bl", receipt.ResultingLabelIds);
    }

    [Fact]
    public void Reassign_resolving_last_blocker_removes_blocked_label()
    {
        using var workspace = Workspace.Create();
        var rel = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var snapshot = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]);
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]),         // live
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], []),            // post-delete refetch
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [], []))                                     // post-mutation
            .On("IssueRelationDelete", MutationOk("issueRelationDelete"))
            .On("IssueUpdate", MutationOk("issueUpdate"), MutationOk("issueUpdate"));                              // label removal + state change
        var exit = RunHandoff(workspace, snapshot, "reassign", transport, new StringWriter(), resolve: "BAR-9");
        Assert.Equal(0, exit);
        var receipt = LinearReceiptRecord.Parse(File.ReadAllText(workspace.OutputPath));
        Assert.Empty(receipt.RemainingBlockers);
        Assert.Empty(receipt.ResultingLabelIds);
    }

    [Fact]
    public void Reassign_resolving_one_of_many_keeps_blocked_label()
    {
        using var workspace = Workspace.Create();
        var resolved = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var remaining = Rel("rel2", "uuid7", "BAR-7", "uuid41", "BAR-41");
        var snapshot = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [resolved, remaining]);
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [resolved, remaining]),
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [remaining]),
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [new("bl", "blocked")], [remaining]))
            .On("IssueRelationDelete", MutationOk("issueRelationDelete"))
            .On("IssueUpdate", MutationOk("issueUpdate"));
        var exit = RunHandoff(workspace, snapshot, "reassign", transport, new StringWriter(), resolve: "BAR-9");
        Assert.Equal(0, exit);
        Assert.Single(transport.Calls, c => c.Op == "IssueUpdate"); // state only; blocked label kept
        var receipt = LinearReceiptRecord.Parse(File.ReadAllText(workspace.OutputPath));
        Assert.Equal("rel2", Assert.Single(receipt.RemainingBlockers).Id);
        Assert.Contains("bl", receipt.ResultingLabelIds);
    }

    [Fact]
    public void Deletion_then_label_removal_failure_reports_completed_durable_operation()
    {
        using var workspace = Workspace.Create();
        var rel = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var snapshot = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]);
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]),
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], []))
            .On("IssueRelationDelete", MutationOk("issueRelationDelete"))
            .On("IssueUpdate", MutationFalse("issueUpdate")); // label removal reports success:false
        var error = new StringWriter();
        var exit = RunHandoff(workspace, snapshot, "reassign", transport, new StringWriter(), resolve: "BAR-9", standardError: error);
        Assert.Equal(1, exit);
        Assert.Contains("Linear may already have changed", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("blocker_relation_removed", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Deletion_then_state_failure_reports_completed_durable_operation()
    {
        using var workspace = Workspace.Create();
        var resolved = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var remaining = Rel("rel2", "uuid7", "BAR-7", "uuid41", "BAR-41");
        var snapshot = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [resolved, remaining]);
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [resolved, remaining]),
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [remaining]))
            .On("IssueRelationDelete", MutationOk("issueRelationDelete"))
            .On("IssueUpdate", MutationFalse("issueUpdate")); // state change reports success:false
        var error = new StringWriter();
        var exit = RunHandoff(workspace, snapshot, "reassign", transport, new StringWriter(), resolve: "BAR-9", standardError: error);
        Assert.Equal(1, exit);
        Assert.Contains("blocker_relation_removed", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Deletion_then_receipt_failure_reports_completed_durable_operation()
    {
        using var workspace = Workspace.Create();
        var rel = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var snapshot = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [rel]);
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], [rel]),
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], []),
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [], []))
            .On("IssueRelationDelete", MutationOk("issueRelationDelete"))
            .On("IssueUpdate", MutationOk("issueUpdate"));
        var missingDirectory = Path.Combine(workspace.Path, "nope", "receipt.json");
        var error = new StringWriter();
        var exit = RunHandoff(workspace, snapshot, "reassign", transport, new StringWriter(), resolve: "BAR-9", standardError: error, outputOverride: missingDirectory);
        Assert.Equal(1, exit);
        Assert.Contains("Linear may already have changed", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("blocker_relation_removed", error.ToString(), StringComparison.Ordinal);
    }

    // ---------- helpers ----------

    private static int RunHandoff(Workspace workspace, LinearIssueSnapshot snapshot, string decision, ILinearTransport transport, TextWriter standardOutput, string? blocker = null, string? resolve = null, TextWriter? standardError = null, string? outputOverride = null)
    {
        var snapshotPath = workspace.Write("snapshot.json", snapshot.ToJson());
        var evidencePath = workspace.Write("evidence.json", $"{{\"schema\":\"tlaw.dispatcher-handoff-finalization/v1\",\"task_id\":\"BAR-41\",\"decision\":\"{decision}\"}}");
        var output = outputOverride ?? workspace.OutputPath;
        var args = new List<string> { "linear", "transition", "--issue", "BAR-41", "--event", "handoff", "--snapshot", snapshotPath, "--evidence", evidencePath, "--api-key-env", "TLAW_TEST_LINEAR_KEY", "--output", output };
        if (blocker is not null) { args.Add("--blocker"); args.Add(blocker); }
        if (resolve is not null) { args.Add("--resolve-blocker"); args.Add(resolve); }
        Environment.SetEnvironmentVariable("TLAW_TEST_LINEAR_KEY", Secret);
        try { return LinearCommand.RunForTesting([.. args], standardOutput, standardError ?? TextWriter.Null, transport); }
        finally { Environment.SetEnvironmentVariable("TLAW_TEST_LINEAR_KEY", null); }
    }

    private static LinearTransitionOptions Options(string? resolve = null, string? blocker = null, string output = "")
        => new("BAR-41", "handoff", "snapshot.json", "evidence.json", "TLAW_TEST_LINEAR_KEY", output, null, null, null, blocker, resolve);

    private static TransitionEvidence Evidence(string hash = "hash-abc")
        => new("tlaw.dispatcher-handoff-finalization/v1", "BAR-41", null, "reassign", hash);

    private static LinearRelation Rel(string id, string blockingId, string blockingIdentifier, string blockedId, string blockedIdentifier)
        => new(id, "blocks", blockingId, blockingIdentifier, blockedId, blockedIdentifier);

    private static object L(string id, string name) => new { id, name };

    private static string Receipt(string[] evidenceHashes, LinearRelation[] removed, LinearRelation[] remaining)
        => JsonSerializer.Serialize(new
        {
            schema = "tlaw.dispatcher-linear-transition/v1",
            issue_identifier = "BAR-41",
            @event = "handoff",
            evidence_sha256 = evidenceHashes,
            resulting_label_ids = Array.Empty<string>(),
            blockers_removed = removed.Select(WireRelation),
            remaining_blockers = remaining.Select(WireRelation)
        });

    private static object WireRelation(LinearRelation r)
        => new { relation_id = r.Id, type = r.Type, blocking_issue_id = r.BlockingIssueId, blocking_issue_identifier = r.BlockingIssueIdentifier, blocked_issue_id = r.BlockedIssueId, blocked_issue_identifier = r.BlockedIssueIdentifier };

    private static LinearTransportResponse Catalog(object[] team, object[] workspace)
        => Ok(new { data = new { team = new { id = "team", labels = new { nodes = team } }, workspaceLabels = new { nodes = workspace } } });

    private static LinearTransportResponse LabelCreate(bool success, string id, string name)
        => Ok(new { data = new { issueLabelCreate = new { success, issueLabel = new { id, name } } } });

    private static LinearTransportResponse RelationCreate(bool success, string id, string blockingId, string blockingIdentifier, string blockedId, string blockedIdentifier)
        => Ok(new { data = new { issueRelationCreate = new { success, issueRelation = new { id, type = "blocks", issue = new { id = blockingId, identifier = blockingIdentifier }, relatedIssue = new { id = blockedId, identifier = blockedIdentifier } } } } });

    private static LinearTransportResponse MutationOk(string property)
        => Ok(new Dictionary<string, object> { ["data"] = new Dictionary<string, object> { [property] = new { success = true } } });

    private static LinearTransportResponse MutationFalse(string property)
        => Ok(new Dictionary<string, object> { ["data"] = new Dictionary<string, object> { [property] = new { success = false } } });

    private static LinearTransportResponse GraphErrors()
        => Ok(new { errors = new[] { new { message = "denied" } }, data = (object?)null });

    private static LinearTransportResponse Ok(object value)
        => new(HttpStatusCode.OK, JsonSerializer.Serialize(value));

    private static string StateId(string name) => name switch { "Todo" => "todo", "In Progress" => "prog", "In Review" => "review", _ => "state" };
    private static object[] States() => [new { id = "todo", name = "Todo", type = "unstarted" }, new { id = "prog", name = "In Progress", type = "started" }, new { id = "review", name = "In Review", type = "started" }];

    private static LinearTransportResponse ResponseFor(string id, string identifier, string stateName, string stateType, IReadOnlyList<LinearLabel> labels, IReadOnlyList<LinearRelation> blockedBy)
        => Ok(new
        {
            data = new
            {
                issue = new
                {
                    id,
                    identifier,
                    url = $"https://linear.app/x/issue/{identifier}",
                    title = "T",
                    description = "never-print-this-description",
                    updatedAt = Updated,
                    state = new { id = StateId(stateName), name = stateName, type = stateType },
                    team = new { id = "team", key = "BAR", states = new { nodes = States() } },
                    labels = new { nodes = labels.Select(l => new { id = l.Id, name = l.Name }) },
                    blockedBy = new { nodes = blockedBy.Select(r => new { id = r.Id, type = r.Type, blockingIssue = new { id = r.BlockingIssueId, identifier = r.BlockingIssueIdentifier }, blockedIssue = new { id = r.BlockedIssueId, identifier = r.BlockedIssueIdentifier } }) },
                    blocks = new { nodes = Array.Empty<object>() },
                    attachments = new { nodes = new[] { new { url = "https://a.test" } } }
                }
            }
        });

    private static LinearIssueSnapshot SnapshotFor(string id, string identifier, string stateName, string stateType, IReadOnlyList<LinearLabel> labels, IReadOnlyList<LinearRelation> blockedBy)
        => LinearIssueSnapshot.From(new LinearIssue(id, identifier, $"https://linear.app/x/issue/{identifier}", "T", Updated,
            new LinearState(StateId(stateName), stateName, stateType), "team", "BAR",
            [new LinearState("todo", "Todo", "unstarted"), new LinearState("prog", "In Progress", "started"), new LinearState("review", "In Review", "started")],
            labels, ["https://a.test"], blockedBy, []));

    private sealed class QueueLinear : ILinearTransport
    {
        private readonly Dictionary<string, Queue<LinearTransportResponse>> _responses = new(StringComparer.Ordinal);
        internal List<(string Op, string Vars)> Calls { get; } = [];

        internal QueueLinear On(string key, params LinearTransportResponse[] responses)
        {
            if (!_responses.TryGetValue(key, out var queue)) _responses[key] = queue = new Queue<LinearTransportResponse>();
            foreach (var response in responses) queue.Enqueue(response);
            return this;
        }

        public LinearTransportResponse Send(string operationName, string query, object variables, string apiKey)
        {
            Assert.DoesNotContain(apiKey, query, StringComparison.Ordinal);
            var vars = JsonSerializer.Serialize(variables);
            Assert.DoesNotContain(apiKey, vars, StringComparison.Ordinal);
            Calls.Add((operationName, vars));
            var key = operationName == "Issue" ? "Issue:" + JsonDocument.Parse(vars).RootElement.GetProperty("identifier").GetString() : operationName;
            if (!_responses.TryGetValue(key, out var queue) || queue.Count == 0) throw new InvalidOperationException($"No scripted response for '{key}'.");
            return queue.Dequeue();
        }
    }

    private sealed class Workspace : IDisposable
    {
        private Workspace(string path) => Path = path;
        internal string Path { get; }
        internal string OutputPath => System.IO.Path.Combine(Path, "receipt.json");
        internal static Workspace Create() { var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tlaw-handoff-mutation-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return new Workspace(path); }
        internal string Write(string name, string text) { var path = System.IO.Path.Combine(Path, name); File.WriteAllText(path, text, new UTF8Encoding(false)); return path; }
        public void Dispose() => Directory.Delete(Path, true);
    }
}
