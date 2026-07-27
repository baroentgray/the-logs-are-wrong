using System.Reflection;
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

[Trait("Scope", "TLAW-017")]
public sealed class DefaultIntakeAutoRouteTests
{
    private static readonly DefaultIntakeAutoRouteService Route = new();

    [Fact]
    public void Invalid_input_missing_owner_and_active_deadline_fail_closed()
    {
        var expired = ExpireInitial(ServerTick.From(60));
        Assert.Throws<ArgumentNullException>(() => Route.Attempt(null!, expired.FollowUp, ServerTick.From(60)));
        Assert.Throws<ArgumentNullException>(() => Route.Attempt(expired.State, null!, ServerTick.From(60)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Route.Attempt(expired.State, expired.FollowUp, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => Route.Attempt(expired.State, expired.FollowUp, ServerTick.From(59)));

        var missing = new DefaultAutoRouteRequired(LogId.From("missing"), ServerTick.From(60));
        Assert.Same(expired.State, Assert.IsType<DefaultIntakeAutoRouteOwnerMissing>(Route.Attempt(expired.State, missing, ServerTick.From(60))).State);

        var active = StartInitial().State;
        Assert.Throws<InvalidOperationException>(() => Route.Attempt(active, expired.FollowUp, ServerTick.From(60)));
    }

    [Fact]
    public void Exact_due_and_late_tlaw016_expirations_route_only_the_required_owner_when_saw_queue_is_free()
    {
        var exact = ExpireInitial(ServerTick.From(60));
        var exactRoute = Assert.IsType<DefaultIntakeAutoRouteApplied>(Route.Attempt(exact.State, exact.FollowUp, ServerTick.From(60)));
        AssertApplied(exact, exactRoute, ServerTick.From(60));

        var late = ExpireInitial(ServerTick.From(61));
        var lateRoute = Assert.IsType<DefaultIntakeAutoRouteApplied>(Route.Attempt(late.State, late.FollowUp, ServerTick.From(61)));
        AssertApplied(late, lateRoute, ServerTick.From(61));
    }

    [Fact]
    public void Occupied_saw_queue_is_an_exact_reference_no_op_with_closed_follow_up_and_repeated_evaluation_is_deterministic()
    {
        var expired = ExpireWithOccupiedSawQueue();
        var blockedState = expired.State;

        var first = Assert.IsType<DefaultIntakeAutoRouteBlocked>(Route.Attempt(blockedState, expired.FollowUp, ServerTick.From(60)));
        var second = Assert.IsType<DefaultIntakeAutoRouteBlocked>(Route.Attempt(blockedState, expired.FollowUp, ServerTick.From(60)));
        Assert.Same(blockedState, first.State);
        Assert.Same(blockedState, second.State);
        Assert.Equal(blockedState.StateVersion, first.State.StateVersion);
        Assert.Equal((LogId.From("log_01"), ServerTick.From(60), DefaultIntakeAutoRouteBlockReason.SawQueueOccupied, DefaultIntakeAutoRouteFollowUp.IntakeAutoFeedJamDerivationRequired), (first.LogId, first.AttemptedAt, first.Reason, first.FollowUp));
        Assert.Equal(first, second);
        Assert.Equal(LogState.AT_INTAKE, Log(blockedState, "log_01").State);
        Assert.Equal(LogState.QUEUED_FOR_SAW, Log(blockedState, "log_02").State);
    }

    [Fact]
    public void Existing_jammed_or_repairing_line_retains_its_condition_for_blocked_and_allows_free_queue_routes()
    {
        var blocked = ExpireWithOccupiedSawQueue().State;
        var jammed = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(blocked, JamCause.INTAKE_AUTOFEED_BLOCKED, ServerTick.From(60))).State;
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jammed, ServerTick.From(60), Fixture.LoadP0().Shift.Scheduler)).State;
        Assert.Equal(DefaultIntakeAutoRouteFollowUp.ExistingLineConditionRetained, Assert.IsType<DefaultIntakeAutoRouteBlocked>(Route.Attempt(jammed, Requirement(), ServerTick.From(60))).FollowUp);
        Assert.Equal(DefaultIntakeAutoRouteFollowUp.ExistingLineConditionRetained, Assert.IsType<DefaultIntakeAutoRouteBlocked>(Route.Attempt(repairing, Requirement(), ServerTick.From(60))).FollowUp);

        var freeJammed = CreateLineState(LineState.LINE_JAMMED);
        var freeRepairing = CreateLineState(LineState.REPAIRING);
        var jammedRoute = Assert.IsType<DefaultIntakeAutoRouteApplied>(Route.Attempt(freeJammed, Requirement(), ServerTick.From(60)));
        var repairingRoute = Assert.IsType<DefaultIntakeAutoRouteApplied>(Route.Attempt(freeRepairing, Requirement(), ServerTick.From(60)));
        Assert.Equal(freeJammed.Line, jammedRoute.State.Line);
        Assert.Equal(freeRepairing.Line, repairingRoute.State.Line);
    }

    [Fact]
    public void Current_state_may_include_an_unrelated_accepted_mutation_and_owner_no_longer_at_intake_is_a_typed_no_op()
    {
        var expired = ExpireInitial(ServerTick.From(60));
        var newer = RuntimeFixture.MoveHost(expired.State, "log_02", LogState.AT_FEED_GATE);
        var applied = Assert.IsType<DefaultIntakeAutoRouteApplied>(Route.Attempt(newer, expired.FollowUp, ServerTick.From(60)));
        Assert.Equal(LogState.AT_FEED_GATE, Log(applied.State, "log_02").State);
        Assert.Equal(newer.StateVersion.Next(), applied.State.StateVersion);

        var away = RuntimeFixture.MoveHost(expired.State, "log_01", LogState.AT_PROCEDURE);
        var noLongerApplicable = Assert.IsType<DefaultIntakeAutoRouteNoLongerApplicable>(Route.Attempt(away, expired.FollowUp, ServerTick.From(60)));
        Assert.Same(away, noLongerApplicable.State);
        Assert.Equal(LogId.From("log_01"), noLongerApplicable.LogId);
    }

    [Fact]
    public void Journal_boundary_commits_expiration_and_route_separately_and_never_commits_blocked_or_no_longer_applicable_results()
    {
        var commit = new JournaledMutationCommitService();
        var initial = RuntimeFixture.CreateInitialState();
        var journal = new InMemoryEventJournal(initial.ShiftId);
        var plan = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        Commit(commit, journal, initial, plan.State, ServerTick.Zero, "plan");
        var admission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(plan.State, ServerTick.Zero));
        Commit(commit, journal, plan.State, admission.State, ServerTick.Zero, "admission");
        var started = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, Profile("learning")));
        Commit(commit, journal, admission.State, started.State, ServerTick.Zero, "start");
        var expired = Assert.IsType<IntakeDeadlineExpired>(new IntakeDeadlineExpirationService().Expire(started.State, ServerTick.From(60)));
        var expirationCommit = Commit(commit, journal, started.State, expired.State, ServerTick.From(60), "expiration");
        var routed = Assert.IsType<DefaultIntakeAutoRouteApplied>(Route.Attempt(expired.State, expired.FollowUp, ServerTick.From(60)));
        var routeCommit = Commit(commit, journal, expired.State, routed.State, ServerTick.From(60), "route");
        Assert.Same(expired.State, expirationCommit.After);
        Assert.Same(routed.State, routeCommit.After);
        Assert.Equal((5, routed.State.StateVersion, ServerTick.From(60)), (journal.Events.Count, journal.LastStateVersion, journal.LastTick));

        var snapshot = (journal.Events.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion);
        var noLongerApplicable = Assert.IsType<DefaultIntakeAutoRouteNoLongerApplicable>(Route.Attempt(routed.State, expired.FollowUp, ServerTick.From(60)));
        Assert.Same(routed.State, noLongerApplicable.State);
        Assert.Equal(snapshot, (journal.Events.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion));

        var blockedExpiration = ExpireWithOccupiedSawQueue();
        var blockedJournal = AlignedJournal(blockedExpiration.State, ServerTick.From(60));
        var blockedSnapshot = (blockedJournal.Events.Count, blockedJournal.LastSequence, blockedJournal.LastTick, blockedJournal.LastStateVersion);
        var blocked = Assert.IsType<DefaultIntakeAutoRouteBlocked>(Route.Attempt(blockedExpiration.State, blockedExpiration.FollowUp, ServerTick.From(60)));
        Assert.Same(blockedExpiration.State, blocked.State);
        Assert.Equal(blockedSnapshot, (blockedJournal.Events.Count, blockedJournal.LastSequence, blockedJournal.LastTick, blockedJournal.LastStateVersion));
    }

    [Fact]
    public void Service_surface_and_independent_results_are_deterministic_and_value_equality_observes_the_route()
    {
        var method = typeof(DefaultIntakeAutoRouteService).GetMethod(nameof(DefaultIntakeAutoRouteService.Attempt), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        Assert.Equal(new[] { typeof(ShiftRuntimeState), typeof(DefaultAutoRouteRequired), typeof(ServerTick) }, method.GetParameters().Select(parameter => parameter.ParameterType));

        var firstExpiration = ExpireInitial(ServerTick.From(60));
        var secondExpiration = ExpireInitial(ServerTick.From(60));
        var first = Assert.IsType<DefaultIntakeAutoRouteApplied>(Route.Attempt(firstExpiration.State, firstExpiration.FollowUp, ServerTick.From(60)));
        var second = Assert.IsType<DefaultIntakeAutoRouteApplied>(Route.Attempt(secondExpiration.State, secondExpiration.FollowUp, ServerTick.From(60)));
        Assert.Equal((first.LogId, first.AttemptedAt, first.PriorStateVersion, first.CurrentStateVersion), (second.LogId, second.AttemptedAt, second.PriorStateVersion, second.CurrentStateVersion));
        Assert.True(first.State.ValueEquals(second.State));
        Assert.False(firstExpiration.State.ValueEquals(first.State));
    }

    private static IntakeDeadlineExpired ExpireInitial(ServerTick tick)
    {
        var started = StartInitial();
        return Assert.IsType<IntakeDeadlineExpired>(new IntakeDeadlineExpirationService().Expire(started.State, tick));
    }

    private static IntakeDeadlineStarted StartInitial()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var plan = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        var admission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(plan.State, ServerTick.Zero));
        return Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, Profile("learning")));
    }

    private static IntakeDeadlineExpired ExpireWithOccupiedSawQueue()
    {
        var fixture = Fixture.LoadP0();
        var scheduler = fixture.Shift.Scheduler with { Capacities = fixture.Shift.Scheduler.Capacities.SetItem(NodeId.INTAKE, NodeCapacity.Limited(2)) };
        var initial = ShiftRuntimeState.Create(fixture.Shift with { Scheduler = scheduler });
        var plan = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, scheduler));
        var admission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(plan.State, ServerTick.Zero));
        var started = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, Profile("learning")));
        var withSecondLog = RuntimeFixture.MoveHost(started.State, "log_02", LogState.AT_FEED_GATE);
        withSecondLog = RuntimeFixture.MoveHost(withSecondLog, "log_02", LogState.AT_INTAKE);
        withSecondLog = RuntimeFixture.MoveHost(withSecondLog, "log_02", LogState.QUEUED_FOR_SAW);
        return Assert.IsType<IntakeDeadlineExpired>(new IntakeDeadlineExpirationService().Expire(withSecondLog, ServerTick.From(60)));
    }

    private static ShiftRuntimeState CreateLineState(LineState lineState)
    {
        var occupied = ExpireWithOccupiedSawQueue().State;
        var jammed = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(occupied, JamCause.INTAKE_AUTOFEED_BLOCKED, ServerTick.From(60))).State;
        var free = RuntimeFixture.MoveHost(jammed, "log_02", LogState.IN_SAW);
        return lineState == LineState.LINE_JAMMED
            ? free
            : Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(free, ServerTick.From(60), Fixture.LoadP0().Shift.Scheduler)).State;
    }

    private static DefaultAutoRouteRequired Requirement() => new(LogId.From("log_01"), ServerTick.From(60));
    private static InMemoryEventJournal AlignedJournal(ShiftRuntimeState state, ServerTick tick)
    {
        var journal = new InMemoryEventJournal(state.ShiftId);
        for (var version = 1L; version <= state.StateVersion.Value; version++)
        {
            journal.Append(new EventEnvelope { ShiftId = state.ShiftId, EventId = EventId.From($"seed_{version}"), Sequence = EventSequence.From(version), ServerTick = tick, StateVersionAfter = StateVersion.From(version), EventType = EventTypeId.From("test.seed"), Payload = new RoutePayload($"seed_{version}") });
        }

        return journal;
    }
    private static ShiftProfile Profile(string id) => Fixture.LoadP0().Shift.Profiles[ProfileId.From(id)];
    private static LogRuntimeState Log(ShiftRuntimeState state, string id) { Assert.True(state.TryGetLog(LogId.From(id), out var log)); return log; }
    private static JournaledMutationCommitted Commit(JournaledMutationCommitService service, IEventJournal journal, ShiftRuntimeState before, ShiftRuntimeState after, ServerTick tick, string id) => Assert.IsType<JournaledMutationCommitted>(service.Commit(journal, before, after, tick, new DomainEventDraft(EventId.From($"tlaw017_{id}"), EventTypeId.From("test.tlaw017"), new RoutePayload(id), null)));
    private static void AssertApplied(IntakeDeadlineExpired expired, DefaultIntakeAutoRouteApplied route, ServerTick tick)
    {
        Assert.Equal((LogId.From("log_01"), tick, LogState.AT_INTAKE, LogState.QUEUED_FOR_SAW, expired.State.StateVersion, expired.State.StateVersion.Next()), (route.LogId, route.AttemptedAt, route.Source, route.Destination, route.PriorStateVersion, route.CurrentStateVersion));
        Assert.Null(route.State.ActiveIntakeDeadline);
        Assert.Equal(LogState.QUEUED_FOR_SAW, Log(route.State, "log_01").State);
        Assert.Equal(LogState.SCHEDULED, Log(route.State, "log_02").State);
        Assert.Equal(expired.State.PendingFeed, route.State.PendingFeed);
        Assert.Equal(expired.State.Inventory, route.State.Inventory);
        Assert.Equal(expired.State.ProcedureProgressByLog, route.State.ProcedureProgressByLog);
        Assert.Equal(expired.State.ActiveProcedureHold, route.State.ActiveProcedureHold);
        Assert.Equal(expired.State.ActiveConfirmationTest, route.State.ActiveConfirmationTest);
        Assert.Equal(expired.State.ConfirmationResultsByLog, route.State.ConfirmationResultsByLog);
        Assert.Equal(expired.State.Containment, route.State.Containment);
        Assert.Equal(expired.State.ActiveContainmentRitual, route.State.ActiveContainmentRitual);
        Assert.Equal(expired.State.Line, route.State.Line);
        Assert.True(expired.State.ProcessedIntentIds.SetEquals(route.State.ProcessedIntentIds));
    }

    private sealed record RoutePayload(string Value) : IDomainEventPayload;
}
