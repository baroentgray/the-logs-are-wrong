using System.Reflection;
using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Tests.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Scheduler;

[Trait("Scope", "TLAW-018")]
public sealed class IntakeAutoFeedJamDerivationTests
{
    private static readonly IntakeAutoFeedJamDerivationService Derive = new();

    [Fact]
    public void Null_malformed_and_stale_inputs_fail_closed()
    {
        var blocked = Blocked(ServerTick.From(60));
        Assert.Throws<ArgumentNullException>(() => Derive.Derive(null!, blocked));
        Assert.Throws<ArgumentNullException>(() => Derive.Derive(blocked.State, null!));
        Assert.Equal(IntakeAutoFeedJamDefensiveFailureReason.InvalidBlockedDescriptor, Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(blocked.State, blocked with { Reason = (DefaultIntakeAutoRouteBlockReason)99 })).Reason);
        Assert.Equal(IntakeAutoFeedJamDefensiveFailureReason.InvalidBlockedDescriptor, Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(blocked.State, blocked with { FollowUp = DefaultIntakeAutoRouteFollowUp.ExistingLineConditionRetained })).Reason);
        Assert.Equal(IntakeAutoFeedJamDefensiveFailureReason.InvalidBlockedDescriptor, Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(blocked.State, blocked with { LogId = default })).Reason);
        Assert.Equal(IntakeAutoFeedJamDefensiveFailureReason.InvalidBlockedDescriptor, Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(blocked.State, blocked with { AttemptedAt = default })).Reason);

        var newer = RuntimeFixture.MoveHost(blocked.State, "log_03", LogState.AT_FEED_GATE);
        Assert.Equal(IntakeAutoFeedJamDefensiveFailureReason.CurrentStatePrecedesBlocked, Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(blocked.State, new DefaultIntakeAutoRouteBlocked(newer, blocked.LogId, blocked.AttemptedAt, blocked.Reason, blocked.FollowUp))).Reason);
        var older = ShiftRuntimeState.Create(Fixture.LoadP0().Shift);
        Assert.Equal(IntakeAutoFeedJamDefensiveFailureReason.CurrentStatePrecedesBlocked, Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(older, blocked)).Reason);
        var divergent = Blocked(ServerTick.From(60));
        Assert.Equal(IntakeAutoFeedJamDefensiveFailureReason.DivergentSameVersion, Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(divergent.State, blocked)).Reason);
    }

    [Fact]
    public void Exact_due_and_late_composition_enter_one_intake_auto_feed_jam_without_moving_logs()
    {
        var exact = Blocked(ServerTick.From(60));
        var accepted = Assert.IsType<IntakeAutoFeedJamEntered>(Derive.Derive(exact.State, exact));
        AssertAccepted(exact, accepted);

        var late = Blocked(ServerTick.From(61));
        var lateAccepted = Assert.IsType<IntakeAutoFeedJamEntered>(Derive.Derive(late.State, late));
        AssertAccepted(late, lateAccepted);
        Assert.Equal(ServerTick.From(61), lateAccepted.EnteredAt);
    }

    [Fact]
    public void Existing_line_owner_changes_blocker_clear_and_active_deadline_are_typed_or_loud_without_layering_a_second_jam()
    {
        var blocked = Blocked(ServerTick.From(60));
        var jammed = Assert.IsType<IntakeAutoFeedJamEntered>(Derive.Derive(blocked.State, blocked)).State;
        Assert.Same(jammed, Assert.IsType<IntakeAutoFeedJamExistingLineConditionRetained>(Derive.Derive(jammed, blocked)).State);
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jammed, ServerTick.From(60), Fixture.LoadP0().Shift.Scheduler)).State;
        Assert.Same(repairing, Assert.IsType<IntakeAutoFeedJamExistingLineConditionRetained>(Derive.Derive(repairing, blocked)).State);

        var away = RuntimeFixture.MoveHost(blocked.State, "log_01", LogState.AT_PROCEDURE);
        Assert.Same(away, Assert.IsType<IntakeAutoFeedJamNoLongerApplicable>(Derive.Derive(away, blocked)).State);
        var cleared = RuntimeFixture.MoveHost(blocked.State, "log_02", LogState.IN_SAW);
        Assert.Same(cleared, Assert.IsType<IntakeAutoFeedJamBlockerCleared>(Derive.Derive(cleared, blocked)).State);

        var active = NewerStateWithActiveDeadline(blocked);
        Assert.Throws<InvalidOperationException>(() => Derive.Derive(active, blocked));
    }

    [Fact]
    public void Newer_current_state_preserves_unrelated_mutation_and_journal_commits_only_the_separate_jam_mutation()
    {
        var blocked = Blocked(ServerTick.From(60));
        var newer = RuntimeFixture.MoveHost(blocked.State, "log_03", LogState.AT_FEED_GATE);
        var entered = Assert.IsType<IntakeAutoFeedJamEntered>(Derive.Derive(newer, blocked));
        Assert.Equal(LogState.AT_FEED_GATE, Log(entered.State, "log_03").State);

        var (blockedForJournal, journal, commits) = BlockedWithJournal(ServerTick.From(60));
        var journalNewer = RuntimeFixture.MoveHost(blockedForJournal.State, "log_03", LogState.AT_FEED_GATE);
        Commit(commits, journal, blockedForJournal.State, journalNewer, ServerTick.From(60), "unrelated");
        var journalEntered = Assert.IsType<IntakeAutoFeedJamEntered>(Derive.Derive(journalNewer, blockedForJournal));
        var commit = commits;
        var accepted = Commit(commit, journal, journalNewer, journalEntered.State, blockedForJournal.AttemptedAt, "jam");
        Assert.Same(journalNewer, accepted.Before);
        Assert.Same(journalEntered.State, accepted.After);
        Assert.Equal(journalEntered.State.StateVersion, journal.LastStateVersion);
        var snapshot = (journal.Events.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion);
        Assert.Same(journalEntered.State, Assert.IsType<IntakeAutoFeedJamExistingLineConditionRetained>(Derive.Derive(journalEntered.State, blockedForJournal)).State);
        Assert.Equal(snapshot, (journal.Events.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion));
    }

    [Fact]
    public void Public_surface_is_closed_and_independent_inputs_are_deterministic()
    {
        var method = typeof(IntakeAutoFeedJamDerivationService).GetMethod(nameof(IntakeAutoFeedJamDerivationService.Derive), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        Assert.Equal(new[] { typeof(ShiftRuntimeState), typeof(DefaultIntakeAutoRouteBlocked) }, method.GetParameters().Select(x => x.ParameterType));
        var firstBlocked = Blocked(ServerTick.From(60));
        var first = Assert.IsType<IntakeAutoFeedJamEntered>(Derive.Derive(firstBlocked.State, firstBlocked));
        var secondBlocked = Blocked(ServerTick.From(60));
        var second = Assert.IsType<IntakeAutoFeedJamEntered>(Derive.Derive(secondBlocked.State, secondBlocked));
        Assert.Equal((first.LogId, first.EnteredAt, first.Cause, first.PriorStateVersion, first.CurrentStateVersion), (second.LogId, second.EnteredAt, second.Cause, second.PriorStateVersion, second.CurrentStateVersion));
        Assert.True(first.State.ValueEquals(second.State));
    }

    [Fact]
    public void Owner_missing_multiple_intake_and_later_clear_line_are_exact_defensive_or_no_op_branches()
    {
        var blocked = Blocked(ServerTick.From(60));
        var missing = OwnerMissingCurrent(blocked);
        var missingResult = Assert.IsType<IntakeAutoFeedJamOwnerMissing>(Derive.Derive(missing, blocked));
        Assert.Same(missing, missingResult.State);
        Assert.Equal(blocked.LogId, missingResult.LogId);
        Assert.Equal(missing.StateVersion, missingResult.State.StateVersion);

        var ambiguous = RuntimeFixture.MoveHost(blocked.State, "log_03", LogState.AT_FEED_GATE);
        ambiguous = RuntimeFixture.MoveHost(ambiguous, "log_03", LogState.AT_INTAKE);
        var ambiguousResult = Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(ambiguous, blocked));
        Assert.Equal(IntakeAutoFeedJamDefensiveFailureReason.CurrentShapeInvalid, ambiguousResult.Reason);
        Assert.Same(ambiguous, ambiguousResult.State);
        Assert.Equal((LogState.AT_INTAKE, LogState.AT_INTAKE, LineState.LINE_CLEAR), (Log(ambiguous, "log_01").State, Log(ambiguous, "log_03").State, ambiguous.Line.State));

        var laterLine = WithLine(blocked.State, new LineRuntimeState(LineState.LINE_CLEAR, ServerTick.From(61), null, null, null));
        var timingResult = Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(laterLine, blocked));
        Assert.Equal(IntakeAutoFeedJamDefensiveFailureReason.AttemptTickPrecedesLine, timingResult.Reason);
        Assert.Same(laterLine, timingResult.State);
    }

    [Fact]
    public void Attempted_at_isolated_sensitivity_changes_only_accepted_line_entry_timing()
    {
        var firstBlocked = Blocked(ServerTick.From(60));
        var secondBlocked = Blocked(ServerTick.From(61));
        Assert.True(firstBlocked.State.ValueEquals(secondBlocked.State));
        var first = Assert.IsType<IntakeAutoFeedJamEntered>(Derive.Derive(firstBlocked.State, firstBlocked));
        var second = Assert.IsType<IntakeAutoFeedJamEntered>(Derive.Derive(secondBlocked.State, secondBlocked));
        Assert.Equal((first.LogId, first.Cause, first.PriorStateVersion, first.CurrentStateVersion), (second.LogId, second.Cause, second.PriorStateVersion, second.CurrentStateVersion));
        Assert.Equal((ServerTick.From(60), ServerTick.From(61)), (first.EnteredAt, second.EnteredAt));
        Assert.Equal((ServerTick.From(60), ServerTick.From(61)), (first.State.Line.EnteredAt, second.State.Line.EnteredAt));
        Assert.Equal(first.State.Logs, second.State.Logs);
        Assert.Equal(first.State.Inventory.ConsumableQuantities, second.State.Inventory.ConsumableQuantities);
        Assert.True(first.State.Inventory.ReusableItems.SetEquals(second.State.Inventory.ReusableItems));
    }

    private static DefaultIntakeAutoRouteBlocked Blocked(ServerTick tick)
    {
        var fixture = Fixture.LoadP0();
        var scheduler = fixture.Shift.Scheduler with { Capacities = fixture.Shift.Scheduler.Capacities.SetItem(NodeId.INTAKE, NodeCapacity.Limited(2)) };
        var initial = ShiftRuntimeState.Create(fixture.Shift with { Scheduler = scheduler });
        var plan = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, scheduler));
        var admission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(plan.State, ServerTick.Zero));
        var started = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, fixture.Shift.Profiles[ProfileId.From("learning")]));
        var state = RuntimeFixture.MoveHost(started.State, "log_02", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_INTAKE);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.QUEUED_FOR_SAW);
        var expired = Assert.IsType<IntakeDeadlineExpired>(new IntakeDeadlineExpirationService().Expire(state, tick));
        return Assert.IsType<DefaultIntakeAutoRouteBlocked>(new DefaultIntakeAutoRouteService().Attempt(expired.State, expired.FollowUp, tick));
    }

    private static IntakeDeadlineStarted StartInitial()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var plan = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        var admission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(plan.State, ServerTick.Zero));
        return Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, Fixture.LoadP0().Shift.Profiles[ProfileId.From("learning")]));
    }

    private static void AssertAccepted(DefaultIntakeAutoRouteBlocked blocked, IntakeAutoFeedJamEntered entered)
    {
        Assert.Equal((blocked.LogId, blocked.AttemptedAt, JamCause.INTAKE_AUTOFEED_BLOCKED, blocked.State.StateVersion, blocked.State.StateVersion.Next()), (entered.LogId, entered.EnteredAt, entered.Cause, entered.PriorStateVersion, entered.CurrentStateVersion));
        Assert.Equal((LineState.LINE_JAMMED, JamCause.INTAKE_AUTOFEED_BLOCKED, blocked.LogId), (entered.State.Line.State, entered.State.Line.Cause, entered.State.Line.PendingLogId));
        Assert.Null(entered.State.Line.ActiveRepairHold);
        Assert.Null(entered.State.ActiveIntakeDeadline);
        Assert.Equal(LogState.AT_INTAKE, Log(entered.State, "log_01").State);
        Assert.Equal(LogState.QUEUED_FOR_SAW, Log(entered.State, "log_02").State);
        Assert.Equal(blocked.State.ShiftId, entered.State.ShiftId);
        Assert.Equal(blocked.State.ShiftSeed, entered.State.ShiftSeed);
        Assert.Equal(blocked.State.Logs, entered.State.Logs);
        Assert.Equal(blocked.State.PendingFeed, entered.State.PendingFeed);
        Assert.Equal(blocked.State.Inventory, entered.State.Inventory);
        Assert.Equal(blocked.State.ProcedureProgressByLog, entered.State.ProcedureProgressByLog);
        Assert.Equal(blocked.State.ActiveProcedureHold, entered.State.ActiveProcedureHold);
        Assert.Equal(blocked.State.ActiveConfirmationTest, entered.State.ActiveConfirmationTest);
        Assert.Equal(blocked.State.ConfirmationResultsByLog, entered.State.ConfirmationResultsByLog);
        Assert.Equal(blocked.State.Containment, entered.State.Containment);
        Assert.Equal(blocked.State.ActiveContainmentRitual, entered.State.ActiveContainmentRitual);
        Assert.True(blocked.State.ProcessedIntentIds.SetEquals(entered.State.ProcessedIntentIds));
        Assert.False(blocked.State.ValueEquals(entered.State));
    }

    private static ShiftRuntimeState NewerStateWithActiveDeadline(DefaultIntakeAutoRouteBlocked blocked)
    {
        var mutation = typeof(ShiftRuntimeState).GetMethod("WithActiveIntakeDeadline", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsType<ShiftRuntimeState>(mutation.Invoke(blocked.State, [new ActiveIntakeDeadline(blocked.LogId, ServerTick.From(60), TheLogsAreWrong.Domain.Time.SimulationDuration.FromTicks(60))]));
    }

    private static ShiftRuntimeState WithLine(ShiftRuntimeState state, LineRuntimeState line)
    {
        var mutation = typeof(ShiftRuntimeState).GetMethod("WithLine", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsType<ShiftRuntimeState>(mutation.Invoke(state, [line]));
    }

    private static ShiftRuntimeState OwnerMissingCurrent(DefaultIntakeAutoRouteBlocked blocked)
    {
        var fixture = Fixture.LoadP0();
        var scheduler = fixture.Shift.Scheduler with { Capacities = fixture.Shift.Scheduler.Capacities.SetItem(NodeId.INTAKE, NodeCapacity.Limited(2)) };
        var configuration = fixture.Shift with { Scheduler = scheduler, Manifest = fixture.Shift.Manifest.Where(log => log.Id != blocked.LogId).ToImmutableArray() };
        var state = ShiftRuntimeState.Create(configuration);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_INTAKE);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_INTAKE);
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_PROCEDURE);
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_INTAKE);
        return RuntimeFixture.MoveHost(state, "log_03", LogState.AT_PROCEDURE);
    }

    private static (DefaultIntakeAutoRouteBlocked Blocked, InMemoryEventJournal Journal, JournaledMutationCommitService Commits) BlockedWithJournal(ServerTick tick)
    {
        var fixture = Fixture.LoadP0();
        var scheduler = fixture.Shift.Scheduler with { Capacities = fixture.Shift.Scheduler.Capacities.SetItem(NodeId.INTAKE, NodeCapacity.Limited(2)) };
        var initial = ShiftRuntimeState.Create(fixture.Shift with { Scheduler = scheduler });
        var journal = new InMemoryEventJournal(initial.ShiftId);
        var commits = new JournaledMutationCommitService();
        var plan = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, scheduler));
        Commit(commits, journal, initial, plan.State, ServerTick.Zero, "plan");
        var admission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(plan.State, ServerTick.Zero));
        Commit(commits, journal, plan.State, admission.State, ServerTick.Zero, "admission");
        var started = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, fixture.Shift.Profiles[ProfileId.From("learning")]));
        Commit(commits, journal, admission.State, started.State, ServerTick.Zero, "start");
        var gate = RuntimeFixture.MoveHost(started.State, "log_02", LogState.AT_FEED_GATE);
        Commit(commits, journal, started.State, gate, ServerTick.Zero, "gate");
        var intake = RuntimeFixture.MoveHost(gate, "log_02", LogState.AT_INTAKE);
        Commit(commits, journal, gate, intake, ServerTick.Zero, "second_intake");
        var queue = RuntimeFixture.MoveHost(intake, "log_02", LogState.QUEUED_FOR_SAW);
        Commit(commits, journal, intake, queue, ServerTick.Zero, "queue");
        var expired = Assert.IsType<IntakeDeadlineExpired>(new IntakeDeadlineExpirationService().Expire(queue, tick));
        Commit(commits, journal, queue, expired.State, tick, "expiration");
        var blocked = Assert.IsType<DefaultIntakeAutoRouteBlocked>(new DefaultIntakeAutoRouteService().Attempt(expired.State, expired.FollowUp, tick));
        Assert.Same(expired.State, blocked.State);
        return (blocked, journal, commits);
    }

    private static DomainEventDraft Draft(string id) => new(EventId.From($"tlaw018_{id}"), EventTypeId.From("test.tlaw018"), new Payload(id), null);
    private static JournaledMutationCommitted Commit(JournaledMutationCommitService commits, IEventJournal journal, ShiftRuntimeState before, ShiftRuntimeState after, ServerTick tick, string id) => Assert.IsType<JournaledMutationCommitted>(commits.Commit(journal, before, after, tick, Draft(id)));
    private static LogRuntimeState Log(ShiftRuntimeState state, string id) { Assert.True(state.TryGetLog(LogId.From(id), out var log)); return log; }
    private sealed record Payload(string Value) : IDomainEventPayload;
}
