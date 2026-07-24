using System.Text.Json;
using Tlaw.Dispatcher;
using static TheLogsAreWrong.Domain.Tests.AgentProtocol.LinearTransitionTestSupport;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

/// <summary>
/// Exact <c>blocked</c> label resolution, strict blocker-relation mutation semantics, and the corrected
/// reassign ordering (durable state change before any resolved-blocker deletion).
/// </summary>
public sealed class LinearHandoffMutationTests
{
    private const string Hash = "hash-abc";

    // ---------- exact blocked-label resolution ----------

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
        var transport = new QueueLinear().On("LabelCatalog", Catalog([], [])).On("IssueLabelCreate", GraphErrors());
        Assert.Throws<LinearCommandException>(() => BlockedLabelResolution.ResolveOrCreate(transport, "team", Secret, journal));
        Assert.DoesNotContain("blocked_label_created", journal.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_never_leaks_api_key_in_create_diagnostics()
    {
        var journal = new LinearMutationJournal();
        var transport = new QueueLinear()
            .On("LabelCatalog", Catalog([], []))
            .On("IssueLabelCreate", new LinearTransportResponse(System.Net.HttpStatusCode.InternalServerError, "boom"));
        var error = Assert.Throws<LinearCommandException>(() => BlockedLabelResolution.ResolveOrCreate(transport, "team", Secret, journal));
        Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, journal.Describe(), StringComparison.Ordinal);
    }

    // ---------- strict blocker relations: adding ----------

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
        var transport = new QueueLinear().On("Issue:BAR-9", ResponseFor("uuid9", "BAR-9", "Todo", "unstarted", [], []));
        BlockerRelationOps.AddBlocker(transport, live, "BAR-9", Secret, journal, relations, []);
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

    // ---------- strict blocker relations: validating a removal ----------

    [Fact]
    public void Validate_removable_returns_exact_target()
    {
        var target = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [target]);
        var (result, idempotent) = BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9"), snap, snap, Hash);
        Assert.False(idempotent);
        Assert.Equal("rel1", result!.Id);
    }

    [Fact]
    public void Validate_removable_rejects_reversed_relation()
    {
        var reversed = Rel("rel1", "uuid41", "BAR-41", "uuid9", "BAR-9");
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [reversed]);
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9"), snap, snap, Hash));
    }

    [Fact]
    public void Validate_removable_refuses_changed_relation_uuid()
    {
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41")]);
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel2", "uuid9", "BAR-9", "uuid41", "BAR-41")]);
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9"), live, snap, Hash));
    }

    [Fact]
    public void Validate_removable_refuses_changed_orientation()
    {
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41")]);
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel1", "uuid9", "BAR-9", "uuidOTHER", "BAR-77")]);
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9"), live, snap, Hash));
    }

    [Fact]
    public void Validate_removable_refuses_changed_blocking_issue_uuid()
    {
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41")]);
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel1", "uuidOTHER", "BAR-9", "uuid41", "BAR-41")]);
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9"), live, snap, Hash));
    }

    [Fact]
    public void Validate_removable_fails_closed_on_duplicate_snapshot_relations()
    {
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], [Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41"), Rel("rel2", "uuid9", "BAR-9", "uuid41", "BAR-41")]);
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9"), snap, snap, Hash));
    }

    [Fact]
    public void Missing_relation_without_receipt_fails()
    {
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []);
        var options = Options(resolve: "BAR-9", output: Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.json"));
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.ValidateRemovable(options, snap, snap, Hash));
    }

    [Fact]
    public void Missing_relation_with_matching_receipt_is_idempotent_success()
    {
        using var workspace = Workspace.Create();
        var receipt = workspace.Write("receipt.json", Receipt([Hash], removed: [Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41")]));
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []);
        var (target, idempotent) = BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9", output: receipt), snap, snap, Hash);
        Assert.True(idempotent);
        Assert.Null(target);
    }

    [Fact]
    public void Missing_relation_with_different_evidence_hash_fails()
    {
        using var workspace = Workspace.Create();
        var receipt = workspace.Write("receipt.json", Receipt(["other-hash"], removed: [Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41")]));
        var snap = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []);
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.ValidateRemovable(Options(resolve: "BAR-9", output: receipt), snap, snap, Hash));
    }

    // ---------- strict blocker relations: performing a deletion ----------

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
            .On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [], [unrelated]));
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
            .On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [], [Rel("relDup", "uuid9", "BAR-9", "uuid41", "BAR-41")]));
        Assert.Throws<LinearCommandException>(() => BlockerRelationOps.PerformDelete(transport, Options(resolve: "BAR-9"), live, target, Secret, journal, [target], []));
        Assert.Equal("blocker_relation_removed", journal.Describe());
    }

    [Fact]
    public void Relation_diagnostics_never_leak_the_api_key()
    {
        var live = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []);
        var transport = new QueueLinear()
            .On("Issue:BAR-9", ResponseFor("uuid9", "BAR-9", "Todo", "unstarted", [], []))
            .On("IssueRelationCreate", new LinearTransportResponse(System.Net.HttpStatusCode.InternalServerError, "boom"));
        var error = Assert.Throws<LinearCommandException>(() => BlockerRelationOps.AddBlocker(transport, live, "BAR-9", Secret, new LinearMutationJournal(), [], []));
        Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
    }

    // ---------- end-to-end handoff outcomes ----------

    [Fact]
    public void Human_handoff_creates_label_adds_blocker_and_moves_to_todo()
    {
        using var workspace = Workspace.Create();
        var rel = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var snapshot = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []);
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], []),
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], [rel]),
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [new("bl", "blocked")], [rel]))
            .On("LabelCatalog", Catalog([], []), Catalog([L("bl", "blocked")], []))
            .On("IssueLabelCreate", LabelCreate(success: true, "bl", "blocked"))
            .On("Issue:BAR-9", ResponseFor("uuid9", "BAR-9", "Todo", "unstarted", [], []))
            .On("IssueRelationCreate", RelationCreate(success: true, "rel1", "uuid9", "BAR-9", "uuid41", "BAR-41"))
            .On("IssueUpdate", MutationOk("issueUpdate"), MutationOk("issueUpdate"));
        var (exit, standardOutput, _) = RunHandoff(workspace, snapshot, "human", transport, blocker: "BAR-9");
        Assert.Equal(0, exit);
        Assert.Contains("handoff -> Todo", standardOutput, StringComparison.Ordinal);
        var receipt = LinearReceiptRecord.Parse(File.ReadAllText(workspace.OutputPath));
        Assert.Equal("rel1", Assert.Single(receipt.RemainingBlockers).Id);
        Assert.Contains("bl", receipt.ResultingLabelIds);
    }

    [Fact]
    public void Human_handoff_add_path_keeps_add_before_state_order()
    {
        using var workspace = Workspace.Create();
        var rel = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var snapshot = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [], []);
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], []),
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [], [rel]),
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [new("bl", "blocked")], [rel]))
            .On("LabelCatalog", Catalog([L("bl", "blocked")], []))
            .On("Issue:BAR-9", ResponseFor("uuid9", "BAR-9", "Todo", "unstarted", [], []))
            .On("IssueRelationCreate", RelationCreate(success: true, "rel1", "uuid9", "BAR-9", "uuid41", "BAR-41"))
            .On("IssueUpdate", MutationOk("issueUpdate"), MutationOk("issueUpdate"));
        var (exit, _, _) = RunHandoff(workspace, snapshot, "human", transport, blocker: "BAR-9");
        Assert.Equal(0, exit);
        var relationCreate = transport.Calls.FindIndex(c => c.Op == "IssueRelationCreate");
        var stateUpdate = transport.Calls.FindLastIndex(c => c.Op == "IssueUpdate" && c.Vars.Contains("stateId", StringComparison.Ordinal));
        Assert.True(relationCreate >= 0 && relationCreate < stateUpdate, "relation must be added before the state change on the human path");
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
        var (exit, _, _) = RunHandoff(workspace, snapshot, "reassign", transport);
        Assert.Equal(0, exit);
        Assert.Single(transport.Calls, c => c.Op == "IssueUpdate");
        var receipt = LinearReceiptRecord.Parse(File.ReadAllText(workspace.OutputPath));
        Assert.Equal("rel1", Assert.Single(receipt.RemainingBlockers).Id);
        Assert.Contains("bl", receipt.ResultingLabelIds);
    }

    [Fact]
    public void Reassign_resolving_last_blocker_changes_state_before_deletion_then_removes_label()
    {
        using var workspace = Workspace.Create();
        var rel = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var snapshot = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]);
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]),   // live
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [new("bl", "blocked")], []),           // post-delete refetch (state already changed)
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [], []))                               // post-mutation verification
            .On("IssueRelationDelete", MutationOk("issueRelationDelete"))
            .On("IssueUpdate", MutationOk("issueUpdate"), MutationOk("issueUpdate"));                        // state change, then label removal
        var (exit, _, _) = RunHandoff(workspace, snapshot, "reassign", transport, resolve: "BAR-9");
        Assert.Equal(0, exit);
        var stateUpdate = transport.Calls.FindIndex(c => c.Op == "IssueUpdate" && c.Vars.Contains("stateId", StringComparison.Ordinal));
        var delete = transport.Calls.FindIndex(c => c.Op == "IssueRelationDelete");
        Assert.True(stateUpdate >= 0 && stateUpdate < delete, "state change must be durable before the relation deletion");
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
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [new("bl", "blocked")], [remaining]),
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [new("bl", "blocked")], [remaining]))
            .On("IssueRelationDelete", MutationOk("issueRelationDelete"))
            .On("IssueUpdate", MutationOk("issueUpdate"));
        var (exit, _, _) = RunHandoff(workspace, snapshot, "reassign", transport, resolve: "BAR-9");
        Assert.Equal(0, exit);
        Assert.Single(transport.Calls, c => c.Op == "IssueUpdate");
        var receipt = LinearReceiptRecord.Parse(File.ReadAllText(workspace.OutputPath));
        Assert.Equal("rel2", Assert.Single(receipt.RemainingBlockers).Id);
        Assert.Contains("bl", receipt.ResultingLabelIds);
    }

    // ---------- corrected reassign ordering: failure semantics ----------

    [Fact]
    public void Reassign_state_failure_leaves_relation_and_labels_untouched()
    {
        using var workspace = Workspace.Create();
        var rel = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var snapshot = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]);
        var transport = new QueueLinear()
            .On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]))
            .On("IssueUpdate", MutationFalse("issueUpdate")); // state change fails first
        var (exit, _, error) = RunHandoff(workspace, snapshot, "reassign", transport, resolve: "BAR-9");
        Assert.Equal(1, exit);
        Assert.DoesNotContain(transport.Calls, c => c.Op == "IssueRelationDelete");
        Assert.DoesNotContain("Linear may already have changed", error, StringComparison.Ordinal);
        Assert.DoesNotContain("blocker_relation_removed", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Reassign_relation_delete_failure_reports_durable_state_change()
    {
        using var workspace = Workspace.Create();
        var rel = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var snapshot = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]);
        var transport = new QueueLinear()
            .On("Issue:BAR-41", ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]))
            .On("IssueUpdate", MutationOk("issueUpdate"))       // state change succeeds
            .On("IssueRelationDelete", MutationFalse("issueRelationDelete")); // deletion fails
        var (exit, _, error) = RunHandoff(workspace, snapshot, "reassign", transport, resolve: "BAR-9");
        Assert.Equal(1, exit);
        Assert.Contains("Linear may already have changed", error, StringComparison.Ordinal);
        Assert.Contains("state_changed", error, StringComparison.Ordinal);
        Assert.DoesNotContain("blocker_relation_removed", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Reassign_label_removal_failure_reports_state_and_relation_in_canonical_order()
    {
        using var workspace = Workspace.Create();
        var rel = Rel("rel1", "uuid9", "BAR-9", "uuid41", "BAR-41");
        var snapshot = SnapshotFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]);
        var transport = new QueueLinear()
            .On("Issue:BAR-41",
                ResponseFor("uuid41", "BAR-41", "In Progress", "started", [new("bl", "blocked")], [rel]),
                ResponseFor("uuid41", "BAR-41", "Todo", "unstarted", [new("bl", "blocked")], []))
            .On("IssueUpdate", MutationOk("issueUpdate"), MutationFalse("issueUpdate")) // state ok, then label removal fails
            .On("IssueRelationDelete", MutationOk("issueRelationDelete"));
        var (exit, _, error) = RunHandoff(workspace, snapshot, "reassign", transport, resolve: "BAR-9");
        Assert.Equal(1, exit);
        Assert.Contains("Completed remote operations: state_changed, blocker_relation_removed", error, StringComparison.Ordinal);
    }

    // ---------- helpers ----------

    private static (int Exit, string Out, string Err) RunHandoff(Workspace workspace, LinearIssueSnapshot snapshot, string decision, ILinearTransport transport, string? blocker = null, string? resolve = null)
    {
        var clock = new FakeClock(DateTimeOffset.Parse("2026-07-23T00:00:00Z"));
        var store = new FileLeaseStore(workspace.LeaseStore, clock);
        var lease = store.Acquire("BAR-41", "codex", TimeSpan.FromMinutes(30));
        store.Release("BAR-41", lease.ClaimId, LeaseReleaseReason.ManualCancel);
        var taskPath = WriteTask(workspace, UnclaimedTask());
        var ingestionPath = WriteIngestion(workspace, decision);
        var extra = new List<(string, string)> { ("--handoff-ingestion", ingestionPath), ("--lease-store", workspace.LeaseStore) };
        if (blocker is not null) extra.Add(("--blocker", blocker));
        if (resolve is not null) extra.Add(("--resolve-blocker", resolve));
        return RunTransition(workspace, snapshot, taskPath, "handoff", transport, extra, clock: clock);
    }

    private static LinearTransitionOptions Options(string? resolve = null, string output = "")
        => new("BAR-41", "handoff", "snapshot.json", "task.yaml", ApiKeyEnv, output, null, null, null, null, null, null, null, null, resolve);

    private static string Receipt(string[] evidenceHashes, LinearRelation[] removed)
        => JsonSerializer.Serialize(new
        {
            schema = "tlaw.dispatcher-linear-transition/v1",
            issue_identifier = "BAR-41",
            @event = "handoff",
            evidence_sha256 = evidenceHashes,
            resulting_label_ids = Array.Empty<string>(),
            blockers_removed = removed.Select(r => new { relation_id = r.Id, type = r.Type, blocking_issue_id = r.BlockingIssueId, blocking_issue_identifier = r.BlockingIssueIdentifier, blocked_issue_id = r.BlockedIssueId, blocked_issue_identifier = r.BlockedIssueIdentifier }),
            remaining_blockers = Array.Empty<object>()
        });
}
