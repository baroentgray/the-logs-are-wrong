using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Tests.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Scheduler;

[Trait("Scope", "TLAW-020")]
public sealed class RepairFeedGateIntakeDeadlineTests
{
    private static readonly RepairFeedGateIntakeDeadlineStartService Start = new();
    private static readonly RepairPendingTransitionExecutionService Execute = new();

    [Theory]
    [InlineData(9, 9, "learning", 60, 69)]
    [InlineData(12, 12, "pressure", 45, 57)]
    public void Exact_and_late_repaired_feed_gate_admissions_start_the_selected_profile_deadline(
        long completedAt,
        long executedAt,
        string profileId,
        long expectedDuration,
        long expectedDueAt)
    {
        var repaired = RepairedFeedGateAdmission(ServerTick.From(completedAt), ServerTick.From(executedAt));
        var before = repaired.State;
        var profile = Profile(profileId);

        AssertRepairedFeedGateAdmission(repaired);
        var started = Assert.IsType<IntakeDeadlineStarted>(Start.Start(before, repaired, profile));

        Assert.Equal((repaired.LogId, repaired.AppliedAt, expectedDuration, ServerTick.From(expectedDueAt), before.StateVersion, before.StateVersion.Next()),
            (started.Deadline.LogId, started.Deadline.StartedAt, started.Deadline.Duration.Value, started.Deadline.DueAt, started.PriorStateVersion, started.CurrentStateVersion));
        Assert.Equal(started.CurrentStateVersion, started.State.StateVersion);
        Assert.Equal(LogState.AT_INTAKE, Log(started.State, repaired.LogId).State);
        AssertPreservesOnlyDeadline(before, started.State);
    }

    [Fact]
    public void Closed_input_surface_null_invalid_profile_and_overflow_fail_before_mutation()
    {
        var repaired = RepairedFeedGateAdmission(ServerTick.From(9), ServerTick.From(9));
        var state = repaired.State;

        Assert.Throws<ArgumentNullException>(() => Start.Start(null!, repaired, Profile("learning")));
        Assert.Throws<ArgumentNullException>(() => Start.Start(state, null!, Profile("learning")));
        Assert.Throws<ArgumentNullException>(() => Start.Start(state, repaired, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Start.Start(state, repaired, new ShiftProfile(0, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Start.Start(state, repaired, new ShiftProfile(-1, 1)));

        var overflowing = CloneWith(repaired, nameof(RepairPendingTransitionExecuted.AppliedAt), ServerTick.From(long.MaxValue));
        Assert.Throws<OverflowException>(() => Start.Start(state, overflowing, new ShiftProfile(1, 1)));
        Assert.Same(state, repaired.State);
        Assert.Null(state.ActiveIntakeDeadline);
    }

    [Fact]
    public void Matching_retry_is_an_exact_reference_no_op_and_a_contradictory_deadline_fails_loudly()
    {
        var repaired = RepairedFeedGateAdmission(ServerTick.From(9), ServerTick.From(9));
        var started = Assert.IsType<IntakeDeadlineStarted>(Start.Start(repaired.State, repaired, Profile("learning")));

        var retry = Assert.IsType<IntakeDeadlineAlreadyActive>(Start.Start(started.State, repaired, Profile("learning")));
        Assert.Same(started.State, retry.State);
        Assert.Same(started.Deadline, retry.Deadline);
        Assert.Equal(started.State.StateVersion, retry.State.StateVersion);

        Assert.Throws<InvalidOperationException>(() => Start.Start(started.State, repaired, Profile("pressure")));
        Assert.Same(started.State, retry.State);
    }

    [Fact]
    public void Sibling_and_fabricated_repaired_admissions_fail_closed_without_state_mutation()
    {
        var repaired = RepairedFeedGateAdmission(ServerTick.From(9), ServerTick.From(9));
        var sibling = RepairedAutoFeedAdmission(ServerTick.From(66));
        var profile = Profile("learning");

        AssertRejected(sibling.State, sibling, profile);
        AssertRejected(repaired.State, CloneWith(repaired, nameof(RepairPendingTransitionExecuted.FollowUpRequirement), RepairPendingTransitionFollowUp.NormalFeedPlanningEvaluationRequired), profile);
        AssertRejected(repaired.State, CloneWith(repaired, nameof(RepairPendingTransitionExecuted.LogId), LogId.From("log_01")), profile);
        AssertRejected(repaired.State, CloneWith(repaired, nameof(RepairPendingTransitionExecuted.Cause), JamCause.INTAKE_AUTOFEED_BLOCKED), profile);
        AssertRejected(repaired.State, CloneWith(repaired, nameof(RepairPendingTransitionExecuted.Source), LogState.AT_INTAKE), profile);
        AssertRejected(repaired.State, CloneWith(repaired, nameof(RepairPendingTransitionExecuted.Destination), LogState.QUEUED_FOR_SAW), profile);
        AssertRejected(repaired.State, CloneWith(repaired, nameof(RepairPendingTransitionExecuted.PriorStateVersion), repaired.PriorStateVersion.Next()), profile);
        AssertRejected(repaired.State, CloneWith(repaired, nameof(RepairPendingTransitionExecuted.CurrentStateVersion), repaired.CurrentStateVersion.Next()), profile);
    }

    [Fact]
    public void Retained_owner_line_deadline_and_current_state_identity_contradictions_fail_before_start()
    {
        var repaired = RepairedFeedGateAdmission(ServerTick.From(9), ServerTick.From(9));
        var profile = Profile("learning");
        var ownerMoved = RuntimeFixture.MoveHost(repaired.State, "log_02", LogState.QUEUED_FOR_SAW);
        var ownerWrongState = CloneWith(repaired.State, nameof(ShiftRuntimeState.Logs), ownerMoved.Logs);
        var withoutOwner = CloneWith(ownerWrongState, nameof(ShiftRuntimeState.Logs), ownerWrongState.Logs.Where(log => log.LogId != repaired.LogId).ToImmutableArray());
        withoutOwner = CloneWith(withoutOwner, "_logIndexes", GetField<ImmutableDictionary<LogId, int>>(repaired.State, "_logIndexes").Remove(repaired.LogId));
        var activeDeadlineState = CloneWith(repaired.State, nameof(ShiftRuntimeState.ActiveIntakeDeadline), new ActiveIntakeDeadline(repaired.LogId, repaired.AppliedAt, SimulationDuration.FromTicks(60)));
        var jammedState = CloneWith(repaired.State, nameof(ShiftRuntimeState.Line), new LineRuntimeState(LineState.LINE_JAMMED, repaired.AppliedAt, JamCause.FEED_GATE_BLOCKED, repaired.LogId, null));
        var hold = new ActiveRepairHold(repaired.AppliedAt, repaired.AppliedAt + SimulationDuration.FromTicks(6), SimulationDuration.FromTicks(6));
        var repairingState = CloneWith(repaired.State, nameof(ShiftRuntimeState.Line), new LineRuntimeState(LineState.REPAIRING, repaired.AppliedAt, JamCause.FEED_GATE_BLOCKED, repaired.LogId, hold));
        var retainedCause = CloneWith(repaired.State.Line, nameof(LineRuntimeState.Cause), (JamCause?)JamCause.FEED_GATE_BLOCKED);
        var retainedCauseState = CloneWith(repaired.State, nameof(ShiftRuntimeState.Line), retainedCause);

        AssertRejected(ownerWrongState, CloneWith(repaired, nameof(RepairPendingTransitionExecutionResult.State), ownerWrongState), profile);
        AssertRejected(withoutOwner, CloneWith(repaired, nameof(RepairPendingTransitionExecutionResult.State), withoutOwner), profile);
        AssertRejected(activeDeadlineState, CloneWith(repaired, nameof(RepairPendingTransitionExecutionResult.State), activeDeadlineState), profile);
        AssertRejected(jammedState, CloneWith(repaired, nameof(RepairPendingTransitionExecutionResult.State), jammedState), profile);
        AssertRejected(repairingState, CloneWith(repaired, nameof(RepairPendingTransitionExecutionResult.State), repairingState), profile);
        AssertRejected(retainedCauseState, CloneWith(repaired, nameof(RepairPendingTransitionExecutionResult.State), retainedCauseState), profile);

        var older = CloneWith(repaired.State, nameof(ShiftRuntimeState.StateVersion), StateVersion.From(repaired.State.StateVersion.Value - 1));
        var newer = RuntimeFixture.MoveHost(repaired.State, "log_03", LogState.AT_FEED_GATE);
        var divergentSameVersion = RepairedFeedGateAdmission(ServerTick.From(9), ServerTick.From(9)).State;
        AssertRejected(older, repaired, profile);
        AssertRejected(newer, repaired, profile);
        AssertRejected(divergentSameVersion, repaired, profile);
    }

    [Fact]
    public void Accepted_deadline_start_is_a_separate_journal_mutation_and_retries_or_rejections_do_not_advance_cursors()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var journal = new InMemoryEventJournal(initial.ShiftId);
        var commits = new JournaledMutationCommitService();
        var firstGate = RuntimeFixture.MoveHost(initial, "log_01", LogState.AT_FEED_GATE);
        Commit(commits, journal, initial, firstGate, ServerTick.From(1), "first_gate");
        var intake = RuntimeFixture.MoveHost(firstGate, "log_01", LogState.AT_INTAKE);
        Commit(commits, journal, firstGate, intake, ServerTick.From(1), "intake");
        var gate = RuntimeFixture.MoveHost(intake, "log_02", LogState.AT_FEED_GATE);
        Commit(commits, journal, intake, gate, ServerTick.From(2), "feed_gate");
        var jam = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(gate, JamCause.FEED_GATE_BLOCKED, ServerTick.From(3)));
        var jamCommit = Commit(commits, journal, gate, jam.State, ServerTick.From(3), "jam");
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jam.State, ServerTick.From(3), Fixture.LoadP0().Shift.Scheduler));
        var repairCommit = Commit(commits, journal, jam.State, repairing.State, ServerTick.From(3), "repair_start");
        var unblocked = RuntimeFixture.MoveHost(repairing.State, "log_01", LogState.AT_PROCEDURE);
        Commit(commits, journal, repairing.State, unblocked, ServerTick.From(4), "unblock");
        var completion = Assert.IsType<LineRepairCompleted>(new LineRepairDueCompletionService().CompleteDue(unblocked, ServerTick.From(9)));
        var completionCommit = Commit(commits, journal, unblocked, completion.State, ServerTick.From(9), "repair_complete");
        var repaired = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(completion.State, completion, ServerTick.From(9)));
        var movementCommit = Commit(commits, journal, completion.State, repaired.State, ServerTick.From(9), "repaired_admission");
        var deadline = Assert.IsType<IntakeDeadlineStarted>(Start.Start(repaired.State, repaired, Profile("learning")));
        var deadlineCommit = Commit(commits, journal, repaired.State, deadline.State, ServerTick.From(9), "deadline_start");

        Assert.True(jamCommit.Envelope.Sequence < repairCommit.Envelope.Sequence);
        Assert.True(repairCommit.Envelope.Sequence < completionCommit.Envelope.Sequence);
        Assert.True(completionCommit.Envelope.Sequence < movementCommit.Envelope.Sequence);
        Assert.True(movementCommit.Envelope.Sequence < deadlineCommit.Envelope.Sequence);
        Assert.Equal(journal.Count, (int)journal.LastSequence.Value);
        Assert.Equal(deadline.State.StateVersion, journal.LastStateVersion);
        Assert.Same(repaired.State, deadlineCommit.Before);
        Assert.Same(deadline.State, deadlineCommit.After);

        var cursor = (journal.Count, journal.LastSequence, journal.LastStateVersion, journal.LastTick);
        var retry = Assert.IsType<IntakeDeadlineAlreadyActive>(Start.Start(deadline.State, repaired, Profile("learning")));
        Assert.IsType<JournaledMutationCommitRejected>(commits.Commit(journal, retry.State, retry.State, ServerTick.From(9), Draft("deadline_retry")));
        Assert.Equal(cursor, (journal.Count, journal.LastSequence, journal.LastStateVersion, journal.LastTick));
    }

    [Fact]
    public void Independent_inputs_are_deterministic_and_profile_or_applied_tick_changes_only_deadline_timing()
    {
        var firstAdmission = RepairedFeedGateAdmission(ServerTick.From(9), ServerTick.From(10));
        var secondAdmission = RepairedFeedGateAdmission(ServerTick.From(9), ServerTick.From(10));
        var first = Assert.IsType<IntakeDeadlineStarted>(Start.Start(firstAdmission.State, firstAdmission, Profile("learning")));
        var second = Assert.IsType<IntakeDeadlineStarted>(Start.Start(secondAdmission.State, secondAdmission, Profile("learning")));
        Assert.Equal((first.Deadline, first.PriorStateVersion, first.CurrentStateVersion), (second.Deadline, second.PriorStateVersion, second.CurrentStateVersion));
        Assert.True(first.State.ValueEquals(second.State));

        var pressureAdmission = RepairedFeedGateAdmission(ServerTick.From(9), ServerTick.From(10));
        var pressure = Assert.IsType<IntakeDeadlineStarted>(Start.Start(pressureAdmission.State, pressureAdmission, Profile("pressure")));
        Assert.Equal((first.Deadline.LogId, first.Deadline.StartedAt), (pressure.Deadline.LogId, pressure.Deadline.StartedAt));
        Assert.Equal((60L, 70L), (first.Deadline.Duration.Value, first.Deadline.DueAt.Value));
        Assert.Equal((45L, 55L), (pressure.Deadline.Duration.Value, pressure.Deadline.DueAt.Value));

        var laterAdmission = RepairedFeedGateAdmission(ServerTick.From(9), ServerTick.From(11));
        var later = Assert.IsType<IntakeDeadlineStarted>(Start.Start(laterAdmission.State, laterAdmission, Profile("learning")));
        Assert.Equal((ServerTick.From(10), ServerTick.From(11)), (first.Deadline.StartedAt, later.Deadline.StartedAt));
        Assert.Equal((ServerTick.From(70), ServerTick.From(71)), (first.Deadline.DueAt, later.Deadline.DueAt));
    }

    [Fact]
    public void Existing_tlaw016_public_start_and_repaired_tlaw020_start_preserve_equivalent_deadline_semantics()
    {
        var fixture = Fixture.LoadP0();
        var initial = ShiftRuntimeState.Create(fixture.Shift);
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, fixture.Shift.Scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        var existing = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admitted.State, admitted, Profile("learning")));
        var repairedAdmission = RepairedFeedGateAdmission(ServerTick.From(9), ServerTick.From(9));
        var repaired = Assert.IsType<IntakeDeadlineStarted>(Start.Start(repairedAdmission.State, repairedAdmission, Profile("learning")));

        Assert.Equal((admitted.ConsumedSchedule.LogId, admitted.ResolvedAt), (existing.Deadline.LogId, existing.Deadline.StartedAt));
        Assert.Equal((repairedAdmission.LogId, repairedAdmission.AppliedAt), (repaired.Deadline.LogId, repaired.Deadline.StartedAt));
        Assert.Equal(existing.Deadline.Duration, repaired.Deadline.Duration);
        Assert.Equal(existing.PriorStateVersion.Next(), existing.CurrentStateVersion);
        Assert.Equal(repaired.PriorStateVersion.Next(), repaired.CurrentStateVersion);
        Assert.Same(existing.Deadline, existing.State.ActiveIntakeDeadline);
        Assert.Same(repaired.Deadline, repaired.State.ActiveIntakeDeadline);
    }

    [Fact]
    public void Public_input_surface_is_closed()
    {
        var method = typeof(RepairFeedGateIntakeDeadlineStartService).GetMethod(nameof(RepairFeedGateIntakeDeadlineStartService.Start), [typeof(ShiftRuntimeState), typeof(RepairPendingTransitionExecuted), typeof(ShiftProfile)]);
        Assert.NotNull(method);
        Assert.Equal(new[] { typeof(ShiftRuntimeState), typeof(RepairPendingTransitionExecuted), typeof(ShiftProfile) }, method!.GetParameters().Select(parameter => parameter.ParameterType));
    }

    private static RepairPendingTransitionExecuted RepairedFeedGateAdmission(ServerTick completedAt, ServerTick executedAt)
    {
        var fixture = Fixture.LoadP0();
        var initial = ShiftRuntimeState.Create(fixture.Shift);
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, fixture.Shift.Scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        var deadline = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admitted.State, admitted, Profile("learning")));
        var intent = new IntentEnvelope(deadline.State.ShiftId, IntentId.From("tlaw020_early"), ActorId.From("untrusted"), FeedPlanningTargets.FeedGate, FeedPlanningIntentActions.RequestEarlyFeed, deadline.State.StateVersion, ServerTick.From(1), NoIntentParameters.Instance);
        var early = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(deadline.State, intent, RuntimeFixture.BoundActor, ServerTick.From(1), fixture.Shift.Scheduler));
        var atGate = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(early.State, ServerTick.From(3)));
        var jam = Assert.IsType<FeedGateJamDerived>(new FeedGateJamDerivationService().Derive(atGate.State, ServerTick.From(3)));
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jam.State, ServerTick.From(3), fixture.Shift.Scheduler));
        var unblocked = RuntimeFixture.MoveHost(repairing.State, "log_01", LogState.AT_PROCEDURE);
        var completion = Assert.IsType<LineRepairCompleted>(new LineRepairDueCompletionService().CompleteDue(unblocked, completedAt));
        return Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(completion.State, completion, executedAt));
    }

    private static RepairPendingTransitionExecuted RepairedAutoFeedAdmission(ServerTick completedAt)
    {
        var fixture = Fixture.LoadP0();
        var scheduler = fixture.Shift.Scheduler with { Capacities = fixture.Shift.Scheduler.Capacities.SetItem(NodeId.INTAKE, NodeCapacity.Limited(2)) };
        var initial = ShiftRuntimeState.Create(fixture.Shift with { Scheduler = scheduler });
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        var deadline = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admitted.State, admitted, Profile("learning")));
        var queue = RuntimeFixture.MoveHost(RuntimeFixture.MoveHost(RuntimeFixture.MoveHost(deadline.State, "log_02", LogState.AT_FEED_GATE), "log_02", LogState.AT_INTAKE), "log_02", LogState.QUEUED_FOR_SAW);
        var expired = Assert.IsType<IntakeDeadlineExpired>(new IntakeDeadlineExpirationService().Expire(queue, ServerTick.From(60)));
        var blocked = Assert.IsType<DefaultIntakeAutoRouteBlocked>(new DefaultIntakeAutoRouteService().Attempt(expired.State, expired.FollowUp, ServerTick.From(60)));
        var jam = Assert.IsType<IntakeAutoFeedJamEntered>(new IntakeAutoFeedJamDerivationService().Derive(blocked.State, blocked));
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jam.State, ServerTick.From(60), scheduler));
        var unblocked = RuntimeFixture.MoveHost(repairing.State, "log_02", LogState.IN_SAW);
        var completion = Assert.IsType<LineRepairCompleted>(new LineRepairDueCompletionService().CompleteDue(unblocked, completedAt));
        return Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(completion.State, completion, completedAt));
    }

    private static void AssertRepairedFeedGateAdmission(RepairPendingTransitionExecuted repaired)
    {
        Assert.Equal((JamCause.FEED_GATE_BLOCKED, LogState.AT_FEED_GATE, LogState.AT_INTAKE, RepairPendingTransitionFollowUp.IntakeDeadlineStartRequired),
            (repaired.Cause, repaired.Source, repaired.Destination, repaired.FollowUpRequirement));
        Assert.Equal(LogState.AT_INTAKE, Log(repaired.State, repaired.LogId).State);
        Assert.Null(repaired.State.ActiveIntakeDeadline);
        Assert.Equal(LineState.LINE_CLEAR, repaired.State.Line.State);
        Assert.Null(repaired.State.Line.Cause);
        Assert.Null(repaired.State.Line.PendingLogId);
        Assert.Null(repaired.State.Line.ActiveRepairHold);
    }

    private static void AssertPreservesOnlyDeadline(ShiftRuntimeState before, ShiftRuntimeState after)
    {
        Assert.Equal(before.Logs, after.Logs);
        Assert.Same(before.Line, after.Line);
        Assert.Same(before.PendingFeed, after.PendingFeed);
        Assert.Same(before.Inventory, after.Inventory);
        Assert.Same(before.ProcedureProgressByLog, after.ProcedureProgressByLog);
        Assert.Same(before.ActiveProcedureHold, after.ActiveProcedureHold);
        Assert.Same(before.ActiveConfirmationTest, after.ActiveConfirmationTest);
        Assert.Same(before.ConfirmationResultsByLog, after.ConfirmationResultsByLog);
        Assert.Same(before.Containment, after.Containment);
        Assert.Same(before.ActiveContainmentRitual, after.ActiveContainmentRitual);
        Assert.True(before.ProcessedIntentIds.SetEquals(after.ProcessedIntentIds));
        Assert.Equal((before.ShiftId, before.ShiftSeed), (after.ShiftId, after.ShiftSeed));
    }

    private static void AssertRejected(ShiftRuntimeState state, RepairPendingTransitionExecuted admission, ShiftProfile profile)
    {
        var version = state.StateVersion;
        Assert.ThrowsAny<Exception>(() => Start.Start(state, admission, profile));
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

    private static TField GetField<TField>(object source, string name) => Assert.IsType<TField>(FindField(source.GetType(), name).GetValue(source));

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

    private static DomainEventDraft Draft(string id) => new(EventId.From($"tlaw020_{id}"), EventTypeId.From("test.tlaw020"), new Payload(id));

    private sealed record Payload(string Value) : IDomainEventPayload;
}
