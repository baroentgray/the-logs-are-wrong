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
        Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(blocked.State, blocked with { Reason = (DefaultIntakeAutoRouteBlockReason)99 }));
        Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(blocked.State, blocked with { FollowUp = DefaultIntakeAutoRouteFollowUp.ExistingLineConditionRetained }));
        Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(blocked.State, blocked with { LogId = default }));
        Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(blocked.State, blocked with { AttemptedAt = default }));

        var newer = RuntimeFixture.MoveHost(blocked.State, "log_03", LogState.AT_FEED_GATE);
        Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(blocked.State, new DefaultIntakeAutoRouteBlocked(newer, blocked.LogId, blocked.AttemptedAt, blocked.Reason, blocked.FollowUp)));
        var divergent = ShiftRuntimeState.Create(Fixture.LoadP0().Shift);
        Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(divergent, blocked));
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

        var active = StartInitial().State;
        var activeBlocked = new DefaultIntakeAutoRouteBlocked(active, LogId.From("log_01"), ServerTick.From(60), DefaultIntakeAutoRouteBlockReason.SawQueueOccupied, DefaultIntakeAutoRouteFollowUp.IntakeAutoFeedJamDerivationRequired);
        Assert.IsType<IntakeAutoFeedJamDefensiveFailure>(Derive.Derive(active, activeBlocked));
    }

    [Fact]
    public void Newer_current_state_preserves_unrelated_mutation_and_journal_commits_only_the_separate_jam_mutation()
    {
        var blocked = Blocked(ServerTick.From(60));
        var newer = RuntimeFixture.MoveHost(blocked.State, "log_03", LogState.AT_FEED_GATE);
        var entered = Assert.IsType<IntakeAutoFeedJamEntered>(Derive.Derive(newer, blocked));
        Assert.Equal(LogState.AT_FEED_GATE, Log(entered.State, "log_03").State);

        var journal = AlignedJournal(newer, ServerTick.From(60));
        var commit = new JournaledMutationCommitService();
        var accepted = Assert.IsType<JournaledMutationCommitted>(commit.Commit(journal, newer, entered.State, blocked.AttemptedAt, Draft("jam")));
        Assert.Same(newer, accepted.Before);
        Assert.Same(entered.State, accepted.After);
        Assert.Equal(entered.State.StateVersion, journal.LastStateVersion);
        var snapshot = (journal.Events.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion);
        Assert.Same(entered.State, Assert.IsType<IntakeAutoFeedJamExistingLineConditionRetained>(Derive.Derive(entered.State, blocked)).State);
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
    }

    private static InMemoryEventJournal AlignedJournal(ShiftRuntimeState state, ServerTick tick)
    {
        var journal = new InMemoryEventJournal(state.ShiftId);
        for (var version = 1L; version <= state.StateVersion.Value; version++) journal.Append(new EventEnvelope { ShiftId = state.ShiftId, EventId = EventId.From($"seed_{version}"), Sequence = EventSequence.From(version), ServerTick = tick, StateVersionAfter = StateVersion.From(version), EventType = EventTypeId.From("test.seed"), Payload = new Payload($"seed_{version}") });
        return journal;
    }

    private static DomainEventDraft Draft(string id) => new(EventId.From($"tlaw018_{id}"), EventTypeId.From("test.tlaw018"), new Payload(id), null);
    private static LogRuntimeState Log(ShiftRuntimeState state, string id) { Assert.True(state.TryGetLog(LogId.From(id), out var log)); return log; }
    private sealed record Payload(string Value) : IDomainEventPayload;
}
