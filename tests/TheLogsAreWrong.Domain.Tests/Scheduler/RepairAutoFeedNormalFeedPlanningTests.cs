using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Logs;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Tests.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Scheduler;

[Trait("Scope", "TLAW-021")]
public sealed class RepairAutoFeedNormalFeedPlanningTests
{
    private static readonly RepairAutoFeedNormalFeedPlanningService Planning = new();
    private static readonly RepairPendingTransitionExecutionService Execute = new();

    [Theory]
    [InlineData(66, 66, 66, 71)]
    [InlineData(70, 72, 72, 77)]
    public void Exact_due_and_late_repaired_auto_routes_schedule_the_first_remaining_log_at_applied_at(
        long completedAt,
        long appliedAt,
        long expectedScheduledAt,
        long expectedDueAt)
    {
        var (repaired, configuration) = RepairedAutoRoute(ServerTick.From(completedAt), ServerTick.From(appliedAt));
        var before = repaired.State;
        var scheduled = Assert.IsType<NormalFeedScheduled>(Planning.Plan(before, repaired, configuration));

        AssertExactRepairedAutoRoute(repaired);
        Assert.Equal((LogId.From("log_03"), FeedScheduleKind.NORMAL, ServerTick.From(expectedScheduledAt), 5L, ServerTick.From(expectedDueAt), (IntentId?)null),
            (scheduled.Schedule.LogId, scheduled.Schedule.Kind, scheduled.Schedule.ScheduledAt, scheduled.Schedule.Delay.Value, scheduled.Schedule.DueAt, scheduled.Schedule.CausedByIntentId));
        Assert.Equal((before.StateVersion, before.StateVersion.Next(), scheduled.CurrentStateVersion),
            (scheduled.PriorStateVersion, scheduled.State.StateVersion, scheduled.CurrentStateVersion));
        Assert.Equal(before.Logs, scheduled.State.Logs);
        Assert.Same(before.Line, scheduled.State.Line);
        Assert.Same(before.Inventory, scheduled.State.Inventory);
        Assert.Same(before.ProcedureProgressByLog, scheduled.State.ProcedureProgressByLog);
        Assert.Same(before.ConfirmationResultsByLog, scheduled.State.ConfirmationResultsByLog);
        Assert.Same(before.Containment, scheduled.State.Containment);
        Assert.True(before.ProcessedIntentIds.SetEquals(scheduled.State.ProcessedIntentIds));
        Assert.Equal(LogState.QUEUED_FOR_SAW, Log(scheduled.State, repaired.LogId).State);
        Assert.Equal(0, scheduled.State.GetNodeOccupancy(NodeId.INTAKE));
    }

    [Fact]
    public void Null_invalid_delay_and_overflow_fail_before_mutation()
    {
        var (repaired, configuration) = RepairedAutoRoute(ServerTick.From(66), ServerTick.From(66));
        var state = repaired.State;

        Assert.Throws<ArgumentNullException>(() => Planning.Plan(null!, repaired, configuration));
        Assert.Throws<ArgumentNullException>(() => Planning.Plan(state, null!, configuration));
        Assert.Throws<ArgumentNullException>(() => Planning.Plan(state, repaired, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Planning.Plan(state, repaired, configuration with { NormalFeedDelaySeconds = 0 }));

        var overflowing = CloneWith(repaired, nameof(RepairPendingTransitionExecuted.AppliedAt), ServerTick.From(long.MaxValue));
        Assert.Throws<OverflowException>(() => Planning.Plan(state, overflowing, configuration));
        Assert.Same(state, repaired.State);
        Assert.Null(state.PendingFeed);
    }

    [Fact]
    public void Matching_retry_is_only_the_exact_expected_one_step_scheduled_state()
    {
        var (repaired, configuration) = RepairedAutoRoute(ServerTick.From(66), ServerTick.From(66));
        var scheduled = Assert.IsType<NormalFeedScheduled>(Planning.Plan(repaired.State, repaired, configuration));

        var retry = Assert.IsType<NormalFeedPlanningNoOp>(Planning.Plan(scheduled.State, repaired, configuration));
        Assert.Equal(NormalFeedPlanningNoOpReason.FeedAlreadyPending, retry.Reason);
        Assert.Same(scheduled.State, retry.State);
        Assert.Same(scheduled.Schedule, retry.State.PendingFeed);
        Assert.Equal(scheduled.State.StateVersion, retry.State.StateVersion);

        var differentSchedule = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(repaired.State, repaired.AppliedAt, configuration with { NormalFeedDelaySeconds = 6 }));
        Assert.Throws<InvalidOperationException>(() => Planning.Plan(differentSchedule.State, repaired, configuration));
        Assert.Throws<InvalidOperationException>(() => Planning.Plan(scheduled.State, repaired, configuration with { NormalFeedDelaySeconds = 6 }));
        Assert.Same(scheduled.Schedule, scheduled.State.PendingFeed);
    }

    [Fact]
    public void Established_retained_state_no_ops_are_preserved_without_reinterpretation()
    {
        var feedGateContext = AutoRouteCompletion(ServerTick.From(66));
        var gateOccupied = RuntimeFixture.MoveHost(feedGateContext.Completion.State, "log_03", LogState.AT_FEED_GATE);
        var feedGateRepaired = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(gateOccupied, feedGateContext.Completion, ServerTick.From(66)));
        var feedGate = Assert.IsType<NormalFeedPlanningNoOp>(Planning.Plan(feedGateRepaired.State, feedGateRepaired, feedGateContext.Configuration));
        Assert.Equal(NormalFeedPlanningNoOpReason.FeedGateOccupied, feedGate.Reason);
        Assert.Same(feedGateRepaired.State, feedGate.State);

        var pendingContext = AutoRouteCompletion(ServerTick.From(66));
        var earlyIntent = new IntentEnvelope(pendingContext.Completion.State.ShiftId, IntentId.From("tlaw021_pending"), ActorId.From("untrusted"), FeedPlanningTargets.FeedGate, FeedPlanningIntentActions.RequestEarlyFeed, pendingContext.Completion.State.StateVersion, ServerTick.From(66), NoIntentParameters.Instance);
        var pending = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(pendingContext.Completion.State, earlyIntent, RuntimeFixture.BoundActor, ServerTick.From(66), pendingContext.Configuration));
        var pendingRepaired = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(pending.State, pendingContext.Completion, ServerTick.From(66)));
        var alreadyPending = Assert.IsType<NormalFeedPlanningNoOp>(Planning.Plan(pendingRepaired.State, pendingRepaired, pendingContext.Configuration));
        Assert.Equal(NormalFeedPlanningNoOpReason.FeedAlreadyPending, alreadyPending.Reason);
        Assert.Same(pendingRepaired.State, alreadyPending.State);

        var fixture = Fixture.LoadP0();
        var scheduler = fixture.Shift.Scheduler with { Capacities = fixture.Shift.Scheduler.Capacities.SetItem(NodeId.INTAKE, NodeCapacity.Limited(2)) };
        var twoLogs = fixture.Shift with { Scheduler = scheduler, Manifest = fixture.Shift.Manifest.Take(2).ToImmutableArray() };
        var (noMoreRepaired, noMoreConfiguration) = RepairedAutoRoute(ServerTick.From(66), ServerTick.From(66), twoLogs);
        var noMore = Assert.IsType<NormalFeedPlanningNoOp>(Planning.Plan(noMoreRepaired.State, noMoreRepaired, noMoreConfiguration));
        Assert.Equal(NormalFeedPlanningNoOpReason.NoMoreLogs, noMore.Reason);
        Assert.Same(noMoreRepaired.State, noMore.State);
    }

    [Fact]
    public void Sibling_fabricated_retained_shape_and_nonexact_current_states_fail_closed()
    {
        var (repaired, configuration) = RepairedAutoRoute(ServerTick.From(66), ServerTick.From(66));
        var sibling = RepairedFeedGateAdmission(ServerTick.From(9), ServerTick.From(9));

        AssertRejected(sibling.State, sibling, configuration);
        AssertRejected(repaired.State, CloneWith(repaired, nameof(RepairPendingTransitionExecuted.FollowUpRequirement), RepairPendingTransitionFollowUp.IntakeDeadlineStartRequired), configuration);
        AssertRejected(repaired.State, CloneWith(repaired, nameof(RepairPendingTransitionExecuted.Cause), JamCause.FEED_GATE_BLOCKED), configuration);
        AssertRejected(repaired.State, CloneWith(repaired, nameof(RepairPendingTransitionExecuted.Source), LogState.AT_FEED_GATE), configuration);
        AssertRejected(repaired.State, CloneWith(repaired, nameof(RepairPendingTransitionExecuted.Destination), LogState.AT_INTAKE), configuration);
        AssertRejected(repaired.State, CloneWith(repaired, nameof(RepairPendingTransitionExecuted.PriorStateVersion), repaired.PriorStateVersion.Next()), configuration);
        AssertRejected(repaired.State, CloneWith(repaired, nameof(RepairPendingTransitionExecuted.CurrentStateVersion), repaired.CurrentStateVersion.Next()), configuration);

        var older = AutoRouteCompletion(ServerTick.From(66)).Completion.State;
        var divergentSameVersion = RepairedAutoRoute(ServerTick.From(66), ServerTick.From(66)).Repaired.State;
        var newer = RuntimeFixture.MoveHost(repaired.State, "log_03", LogState.AT_FEED_GATE);
        Assert.Throws<InvalidOperationException>(() => Planning.Plan(older, repaired, configuration));
        Assert.Throws<InvalidOperationException>(() => Planning.Plan(divergentSameVersion, repaired, configuration));
        Assert.Throws<InvalidOperationException>(() => Planning.Plan(newer, repaired, configuration));
    }

    [Fact]
    public void Retained_owner_line_deadline_and_intake_contradictions_fail_before_planning()
    {
        var (repaired, configuration) = RepairedAutoRoute(ServerTick.From(66), ServerTick.From(66));
        var ownerMovedLog = CloneWith(Log(repaired.State, repaired.LogId), nameof(LogRuntimeState.State), LogState.IN_SAW);
        var ownerMoved = CloneWith(repaired.State, nameof(ShiftRuntimeState.Logs), repaired.State.Logs.SetItem(0, ownerMovedLog));
        var activeDeadline = CloneWith(repaired.State, nameof(ShiftRuntimeState.ActiveIntakeDeadline), new ActiveIntakeDeadline(repaired.LogId, repaired.AppliedAt, SimulationDuration.FromTicks(60)));
        var jammed = CloneWith(repaired.State, nameof(ShiftRuntimeState.Line), new LineRuntimeState(LineState.LINE_JAMMED, repaired.AppliedAt, JamCause.INTAKE_AUTOFEED_BLOCKED, repaired.LogId, null));
        var retainedCause = CloneWith(repaired.State.Line, nameof(LineRuntimeState.Cause), (JamCause?)JamCause.INTAKE_AUTOFEED_BLOCKED);
        var wrongIntakeLog = CloneWith(Log(repaired.State, LogId.From("log_03")), nameof(LogRuntimeState.State), LogState.AT_INTAKE);
        var wrongIntake = CloneWith(repaired.State, nameof(ShiftRuntimeState.Logs), repaired.State.Logs.SetItem(2, wrongIntakeLog));

        AssertRejected(ownerMoved, CloneWith(repaired, nameof(RepairPendingTransitionExecutionResult.State), ownerMoved), configuration);
        AssertRejected(activeDeadline, CloneWith(repaired, nameof(RepairPendingTransitionExecutionResult.State), activeDeadline), configuration);
        AssertRejected(jammed, CloneWith(repaired, nameof(RepairPendingTransitionExecutionResult.State), jammed), configuration);
        var retainedCauseState = CloneWith(repaired.State, nameof(ShiftRuntimeState.Line), retainedCause);
        AssertRejected(retainedCauseState, CloneWith(repaired, nameof(RepairPendingTransitionExecutionResult.State), retainedCauseState), configuration);
        AssertRejected(wrongIntake, CloneWith(repaired, nameof(RepairPendingTransitionExecutionResult.State), wrongIntake), configuration);
    }

    [Fact]
    public void Journal_commits_keep_jam_repair_movement_and_normal_feed_schedule_separate()
    {
        var fixture = Fixture.LoadP0();
        var scheduler = fixture.Shift.Scheduler with { Capacities = fixture.Shift.Scheduler.Capacities.SetItem(NodeId.INTAKE, NodeCapacity.Limited(2)) };
        var initial = ShiftRuntimeState.Create(fixture.Shift with { Scheduler = scheduler });
        var journal = new InMemoryEventJournal(initial.ShiftId);
        var commits = new JournaledMutationCommitService();
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, scheduler));
        Commit(commits, journal, initial, planned.State, ServerTick.Zero, "initial");
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        Commit(commits, journal, planned.State, admitted.State, ServerTick.Zero, "admit");
        var deadline = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admitted.State, admitted, Profile("learning")));
        Commit(commits, journal, admitted.State, deadline.State, ServerTick.Zero, "deadline");
        var atGate = RuntimeFixture.MoveHost(deadline.State, "log_02", LogState.AT_FEED_GATE);
        Commit(commits, journal, deadline.State, atGate, ServerTick.From(1), "gate");
        var atIntake = RuntimeFixture.MoveHost(atGate, "log_02", LogState.AT_INTAKE);
        Commit(commits, journal, atGate, atIntake, ServerTick.From(1), "second_intake");
        var queued = RuntimeFixture.MoveHost(atIntake, "log_02", LogState.QUEUED_FOR_SAW);
        Commit(commits, journal, atIntake, queued, ServerTick.From(1), "queue");
        var expired = Assert.IsType<IntakeDeadlineExpired>(new IntakeDeadlineExpirationService().Expire(queued, ServerTick.From(60)));
        Commit(commits, journal, queued, expired.State, ServerTick.From(60), "expire");
        var blocked = Assert.IsType<DefaultIntakeAutoRouteBlocked>(new DefaultIntakeAutoRouteService().Attempt(expired.State, expired.FollowUp, ServerTick.From(60)));
        var jam = Assert.IsType<IntakeAutoFeedJamEntered>(new IntakeAutoFeedJamDerivationService().Derive(blocked.State, blocked));
        var jamCommit = Commit(commits, journal, expired.State, jam.State, ServerTick.From(60), "jam");
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jam.State, ServerTick.From(60), scheduler));
        var repairCommit = Commit(commits, journal, jam.State, repairing.State, ServerTick.From(60), "repair_start");
        var unblocked = RuntimeFixture.MoveHost(repairing.State, "log_02", LogState.IN_SAW);
        Commit(commits, journal, repairing.State, unblocked, ServerTick.From(61), "unblock");
        var completion = Assert.IsType<LineRepairCompleted>(new LineRepairDueCompletionService().CompleteDue(unblocked, ServerTick.From(66)));
        var completionCommit = Commit(commits, journal, unblocked, completion.State, ServerTick.From(66), "repair_complete");
        var repaired = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(completion.State, completion, ServerTick.From(66)));
        var movementCommit = Commit(commits, journal, completion.State, repaired.State, ServerTick.From(66), "repaired_auto_route");
        var scheduled = Assert.IsType<NormalFeedScheduled>(Planning.Plan(repaired.State, repaired, scheduler));
        var schedulingCommit = Commit(commits, journal, repaired.State, scheduled.State, ServerTick.From(66), "normal_feed");

        Assert.True(jamCommit.Envelope.Sequence < repairCommit.Envelope.Sequence);
        Assert.True(repairCommit.Envelope.Sequence < completionCommit.Envelope.Sequence);
        Assert.True(completionCommit.Envelope.Sequence < movementCommit.Envelope.Sequence);
        Assert.True(movementCommit.Envelope.Sequence < schedulingCommit.Envelope.Sequence);
        Assert.Equal(journal.Count, (int)journal.LastSequence.Value);
        Assert.Equal(scheduled.State.StateVersion, journal.LastStateVersion);
        Assert.Same(repaired.State, schedulingCommit.Before);
        Assert.Same(scheduled.State, schedulingCommit.After);

        var cursor = (journal.Count, journal.LastSequence, journal.LastStateVersion, journal.LastTick);
        var retry = Assert.IsType<NormalFeedPlanningNoOp>(Planning.Plan(scheduled.State, repaired, scheduler));
        Assert.IsType<JournaledMutationCommitRejected>(commits.Commit(journal, retry.State, retry.State, ServerTick.From(66), Draft("retry")));
        Assert.Equal(cursor, (journal.Count, journal.LastSequence, journal.LastStateVersion, journal.LastTick));
    }

    [Fact]
    public void Independent_inputs_are_deterministic_and_tick_or_configuration_only_change_expected_timing()
    {
        var (firstRepaired, configuration) = RepairedAutoRoute(ServerTick.From(66), ServerTick.From(67));
        var (secondRepaired, _) = RepairedAutoRoute(ServerTick.From(66), ServerTick.From(67));
        var first = Assert.IsType<NormalFeedScheduled>(Planning.Plan(firstRepaired.State, firstRepaired, configuration));
        var second = Assert.IsType<NormalFeedScheduled>(Planning.Plan(secondRepaired.State, secondRepaired, configuration));
        Assert.Equal((first.Schedule, first.PriorStateVersion, first.CurrentStateVersion), (second.Schedule, second.PriorStateVersion, second.CurrentStateVersion));
        Assert.True(first.State.ValueEquals(second.State));

        var (laterRepaired, _) = RepairedAutoRoute(ServerTick.From(66), ServerTick.From(68));
        var later = Assert.IsType<NormalFeedScheduled>(Planning.Plan(laterRepaired.State, laterRepaired, configuration));
        Assert.Equal((ServerTick.From(67), ServerTick.From(68)), (first.Schedule.ScheduledAt, later.Schedule.ScheduledAt));
        Assert.Equal((ServerTick.From(72), ServerTick.From(73)), (first.Schedule.DueAt, later.Schedule.DueAt));

        var (customRepaired, _) = RepairedAutoRoute(ServerTick.From(66), ServerTick.From(67));
        var custom = Assert.IsType<NormalFeedScheduled>(Planning.Plan(customRepaired.State, customRepaired, configuration with { NormalFeedDelaySeconds = 9 }));
        Assert.Equal((first.Schedule.LogId, first.Schedule.ScheduledAt), (custom.Schedule.LogId, custom.Schedule.ScheduledAt));
        Assert.Equal((5L, 9L, 72L, 76L), (first.Schedule.Delay.Value, custom.Schedule.Delay.Value, first.Schedule.DueAt.Value, custom.Schedule.DueAt.Value));
    }

    private static (RepairPendingTransitionExecuted Repaired, SchedulerConfiguration Configuration) RepairedAutoRoute(ServerTick completedAt, ServerTick appliedAt, ShiftConfiguration? shift = null)
    {
        var context = AutoRouteCompletion(completedAt, shift);
        return (Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(context.Completion.State, context.Completion, appliedAt)), context.Configuration);
    }

    private static (LineRepairCompleted Completion, SchedulerConfiguration Configuration) AutoRouteCompletion(ServerTick completedAt, ShiftConfiguration? shift = null)
    {
        var fixture = Fixture.LoadP0();
        var configuration = shift ?? fixture.Shift with { Scheduler = fixture.Shift.Scheduler with { Capacities = fixture.Shift.Scheduler.Capacities.SetItem(NodeId.INTAKE, NodeCapacity.Limited(2)) } };
        var scheduler = configuration.Scheduler;
        var initial = ShiftRuntimeState.Create(configuration);
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        var deadline = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admitted.State, admitted, configuration.Profiles[ProfileId.From("learning")]));
        var queue = RuntimeFixture.MoveHost(RuntimeFixture.MoveHost(RuntimeFixture.MoveHost(deadline.State, "log_02", LogState.AT_FEED_GATE), "log_02", LogState.AT_INTAKE), "log_02", LogState.QUEUED_FOR_SAW);
        var expired = Assert.IsType<IntakeDeadlineExpired>(new IntakeDeadlineExpirationService().Expire(queue, ServerTick.From(60)));
        var blocked = Assert.IsType<DefaultIntakeAutoRouteBlocked>(new DefaultIntakeAutoRouteService().Attempt(expired.State, expired.FollowUp, ServerTick.From(60)));
        var jam = Assert.IsType<IntakeAutoFeedJamEntered>(new IntakeAutoFeedJamDerivationService().Derive(blocked.State, blocked));
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jam.State, ServerTick.From(60), scheduler));
        var unblocked = RuntimeFixture.MoveHost(repairing.State, "log_02", LogState.IN_SAW);
        return (Assert.IsType<LineRepairCompleted>(new LineRepairDueCompletionService().CompleteDue(unblocked, completedAt)), scheduler);
    }

    private static RepairPendingTransitionExecuted RepairedFeedGateAdmission(ServerTick completedAt, ServerTick appliedAt)
    {
        var fixture = Fixture.LoadP0();
        var initial = ShiftRuntimeState.Create(fixture.Shift);
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, fixture.Shift.Scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        var deadline = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admitted.State, admitted, Profile("learning")));
        var intent = new IntentEnvelope(deadline.State.ShiftId, IntentId.From("tlaw021_early"), ActorId.From("untrusted"), FeedPlanningTargets.FeedGate, FeedPlanningIntentActions.RequestEarlyFeed, deadline.State.StateVersion, ServerTick.From(1), NoIntentParameters.Instance);
        var early = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(deadline.State, intent, RuntimeFixture.BoundActor, ServerTick.From(1), fixture.Shift.Scheduler));
        var gate = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(early.State, ServerTick.From(3)));
        var jam = Assert.IsType<FeedGateJamDerived>(new FeedGateJamDerivationService().Derive(gate.State, ServerTick.From(3)));
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jam.State, ServerTick.From(3), fixture.Shift.Scheduler));
        var unblocked = RuntimeFixture.MoveHost(repairing.State, "log_01", LogState.AT_PROCEDURE);
        var completion = Assert.IsType<LineRepairCompleted>(new LineRepairDueCompletionService().CompleteDue(unblocked, completedAt));
        return Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(completion.State, completion, appliedAt));
    }

    private static void AssertExactRepairedAutoRoute(RepairPendingTransitionExecuted repaired)
    {
        Assert.Equal((JamCause.INTAKE_AUTOFEED_BLOCKED, LogState.AT_INTAKE, LogState.QUEUED_FOR_SAW, RepairPendingTransitionFollowUp.NormalFeedPlanningEvaluationRequired),
            (repaired.Cause, repaired.Source, repaired.Destination, repaired.FollowUpRequirement));
        Assert.Equal(LogState.QUEUED_FOR_SAW, Log(repaired.State, repaired.LogId).State);
        Assert.Equal(0, repaired.State.GetNodeOccupancy(NodeId.INTAKE));
        Assert.Null(repaired.State.ActiveIntakeDeadline);
        Assert.Equal(LineState.LINE_CLEAR, repaired.State.Line.State);
        Assert.Null(repaired.State.Line.Cause);
        Assert.Null(repaired.State.Line.PendingLogId);
        Assert.Null(repaired.State.Line.ActiveRepairHold);
    }

    private static void AssertRejected(ShiftRuntimeState state, RepairPendingTransitionExecuted repaired, SchedulerConfiguration configuration)
    {
        var version = state.StateVersion;
        Assert.ThrowsAny<Exception>(() => Planning.Plan(state, repaired, configuration));
        Assert.Equal(version, state.StateVersion);
    }

    private static ShiftProfile Profile(string id) => Fixture.LoadP0().Shift.Profiles[ProfileId.From(id)];

    private static LogRuntimeState Log(ShiftRuntimeState state, LogId id)
    {
        Assert.True(state.TryGetLog(id, out var log));
        return log;
    }

    private static T CloneWith<T>(T source, string name, object? value) where T : class
    {
        var clone = Assert.IsType<T>(typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(source, null));
        FindField(typeof(T), name).SetValue(clone, value);
        return clone;
    }

    private static FieldInfo FindField(Type type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? current.GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null) return field;
        }

        throw new MissingFieldException(type.FullName, name);
    }

    private static JournaledMutationCommitted Commit(JournaledMutationCommitService commits, IEventJournal journal, ShiftRuntimeState before, ShiftRuntimeState after, ServerTick tick, string id) =>
        Assert.IsType<JournaledMutationCommitted>(commits.Commit(journal, before, after, tick, Draft(id)));

    private static DomainEventDraft Draft(string id) => new(EventId.From($"tlaw021_{id}"), EventTypeId.From("test.tlaw021"), new Payload(id));

    private sealed record Payload(string Value) : IDomainEventPayload;
}
