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

[Trait("Scope", "TLAW-019")]
public sealed class RepairPendingTransitionExecutionTests
{
    private static readonly RepairPendingTransitionExecutionService Execute = new();

    [Fact]
    public void Null_inputs_and_default_tick_fail_loudly()
    {
        var completion = FeedGateCompletion(ServerTick.From(16));

        Assert.Throws<ArgumentNullException>(() => Execute.Execute(null!, completion, ServerTick.From(16)));
        Assert.Throws<ArgumentNullException>(() => Execute.Execute(completion.State, null!, ServerTick.From(16)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Execute.Execute(completion.State, completion, default));
    }

    [Fact]
    public void Invalid_retained_completion_and_descriptor_shapes_fail_closed()
    {
        var completion = FeedGateCompletion(ServerTick.From(16));
        var jammed = FeedJammed(ServerTick.From(10));
        var malformedLine = new LineRepairCompleted(jammed, null);
        AssertDefensive(completion.State, Execute.Execute(completion.State, malformedLine, ServerTick.From(16)), RepairPendingTransitionExecutionFailureReason.InvalidRetainedCompletion);

        var wrongOwner = RuntimeFixture.MoveHost(completion.State, "log_02", LogState.AT_INTAKE);
        var malformedDescriptor = completion with { State = wrongOwner };
        AssertDefensive(wrongOwner, Execute.Execute(wrongOwner, malformedDescriptor, ServerTick.From(16)), RepairPendingTransitionExecutionFailureReason.InvalidRetainedCompletion);

        var targetOccupied = RuntimeFixture.MoveHost(completion.State, "log_01", LogState.AT_INTAKE);
        var retainedTargetOccupied = completion with { State = targetOccupied };
        AssertDefensive(targetOccupied, Execute.Execute(targetOccupied, retainedTargetOccupied, ServerTick.From(16)), RepairPendingTransitionExecutionFailureReason.InvalidRetainedCompletion);

        var missing = MissingOwnerCurrent(completion);
        var retainedOwnerMissing = completion with { State = missing };
        AssertDefensive(missing, Execute.Execute(missing, retainedOwnerMissing, ServerTick.From(16)), RepairPendingTransitionExecutionFailureReason.InvalidRetainedCompletion);

    }

    [Fact]
    public void Current_state_monotonicity_and_timing_fail_closed()
    {
        var completion = FeedGateCompletion(ServerTick.From(16));
        var older = RuntimeFixture.CreateInitialState();
        AssertDefensive(older, Execute.Execute(older, completion, ServerTick.From(16)), RepairPendingTransitionExecutionFailureReason.CurrentStatePrecedesCompletion);

        var divergent = FeedGateCompletion(ServerTick.From(16));
        AssertDefensive(divergent.State, Execute.Execute(divergent.State, completion, ServerTick.From(16)), RepairPendingTransitionExecutionFailureReason.DivergentSameVersion);

        AssertDefensive(completion.State, Execute.Execute(completion.State, completion, ServerTick.From(15)), RepairPendingTransitionExecutionFailureReason.ExecutionTickPrecedesLine);

        var laterClearLine = WithLine(completion.State, new LineRuntimeState(LineState.LINE_CLEAR, ServerTick.From(17), null, null, null));
        AssertDefensive(laterClearLine, Execute.Execute(laterClearLine, completion, ServerTick.From(16)), RepairPendingTransitionExecutionFailureReason.ExecutionTickPrecedesLine);
    }

    [Fact]
    public void Valid_completion_without_descriptor_is_an_exact_reference_no_op()
    {
        var completion = FeedGateCompletion(ServerTick.From(16)) with { PendingTransition = null };

        var noOp = Assert.IsType<RepairPendingTransitionNoPendingTransition>(Execute.Execute(completion.State, completion, ServerTick.From(16)));

        Assert.Same(completion.State, noOp.State);
        Assert.Equal(completion.State.StateVersion, noOp.State.StateVersion);
    }

    [Fact]
    public void New_jammed_or_repairing_line_is_retained_without_executing_stale_transition()
    {
        var completion = FeedGateCompletion(ServerTick.From(16));
        var jammed = WithLine(completion.State, new LineRuntimeState(LineState.LINE_JAMMED, ServerTick.From(17), JamCause.FEED_GATE_BLOCKED, LogId.From("log_02"), null));
        var retainedJam = Assert.IsType<RepairPendingTransitionExistingLineConditionRetained>(Execute.Execute(jammed, completion, ServerTick.From(17)));
        Assert.Same(jammed, retainedJam.State);
        Assert.Equal((LineState.LINE_JAMMED, JamCause.FEED_GATE_BLOCKED, LogState.AT_FEED_GATE), (retainedJam.State.Line.State, retainedJam.State.Line.Cause, Log(retainedJam.State, "log_02").State));

        var hold = new ActiveRepairHold(ServerTick.From(17), ServerTick.From(23), SimulationDuration.FromTicks(6));
        var repairing = WithLine(completion.State, new LineRuntimeState(LineState.REPAIRING, ServerTick.From(17), JamCause.FEED_GATE_BLOCKED, LogId.From("log_02"), hold));
        var retainedRepair = Assert.IsType<RepairPendingTransitionExistingLineConditionRetained>(Execute.Execute(repairing, completion, ServerTick.From(17)));
        Assert.Same(repairing, retainedRepair.State);
        Assert.Equal(hold, retainedRepair.State.Line.ActiveRepairHold);
    }

    [Fact]
    public void Missing_moved_and_target_occupied_owner_branches_never_select_a_replacement()
    {
        var completion = FeedGateCompletion(ServerTick.From(16));
        var missing = MissingOwnerCurrent(completion);
        var missingResult = Assert.IsType<RepairPendingTransitionOwnerMissing>(Execute.Execute(missing, completion, ServerTick.From(16)));
        Assert.Same(missing, missingResult.State);
        Assert.Equal(LogId.From("log_02"), missingResult.LogId);

        var moved = RuntimeFixture.MoveHost(completion.State, "log_02", LogState.AT_INTAKE);
        var movedResult = Assert.IsType<RepairPendingTransitionNoLongerApplicable>(Execute.Execute(moved, completion, ServerTick.From(16)));
        Assert.Same(moved, movedResult.State);
        Assert.Equal(LogId.From("log_02"), movedResult.LogId);

        var occupied = RuntimeFixture.MoveHost(completion.State, "log_01", LogState.AT_INTAKE);
        var occupiedResult = Assert.IsType<RepairPendingTransitionTargetOccupied>(Execute.Execute(occupied, completion, ServerTick.From(16)));
        Assert.Same(occupied, occupiedResult.State);
        Assert.Equal(LogId.From("log_02"), occupiedResult.LogId);
        Assert.Equal((LogState.AT_INTAKE, LogState.AT_FEED_GATE), (Log(occupied, "log_01").State, Log(occupied, "log_02").State));
    }

    [Fact]
    public void Feed_gate_recovery_composition_executes_exact_pending_transition_and_only_reports_deadline_follow_up()
    {
        var completion = FeedGateComposition(ServerTick.From(9));

        var accepted = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(completion.State, completion, ServerTick.From(9)));

        AssertAccepted(completion.State, completion, accepted, LogState.AT_INTAKE, RepairPendingTransitionFollowUp.IntakeDeadlineStartRequired, ServerTick.From(9));
        Assert.Null(accepted.State.ActiveIntakeDeadline);
        Assert.Null(accepted.State.PendingFeed);
        Assert.Equal(LogState.AT_PROCEDURE, Log(accepted.State, "log_01").State);
    }

    [Fact]
    public void Intake_auto_feed_recovery_composition_executes_exact_pending_transition_and_only_reports_planning_follow_up()
    {
        var completion = IntakeAutoFeedComposition(ServerTick.From(66));

        var accepted = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(completion.State, completion, ServerTick.From(66)));

        AssertAccepted(completion.State, completion, accepted, LogState.QUEUED_FOR_SAW, RepairPendingTransitionFollowUp.NormalFeedPlanningEvaluationRequired, ServerTick.From(66));
        Assert.Null(accepted.State.ActiveIntakeDeadline);
        Assert.Null(accepted.State.PendingFeed);
        Assert.Equal(LogState.IN_SAW, Log(accepted.State, "log_02").State);
    }

    [Fact]
    public void Intake_auto_feed_owner_confirmation_is_cleared_only_by_the_accepted_host_transition()
    {
        var completion = IntakeAutoFeedCompositionWithOwnerConfirmation(ServerTick.From(66));
        var before = completion.State;
        var pending = Assert.IsType<PendingLineTransitionDescriptor>(completion.PendingTransition);
        var active = Assert.IsType<ActiveConfirmationTest>(before.ActiveConfirmationTest);
        var results = before.ConfirmationResultsByLog;

        Assert.Equal((LogId.From("log_10"), JamCause.INTAKE_AUTOFEED_BLOCKED, LogState.AT_INTAKE, LogState.QUEUED_FOR_SAW), (pending.LogId, pending.Cause, pending.FromState, pending.ToState));
        Assert.Equal(LogId.From("log_10"), active.LogId);
        Assert.Equal(LogState.AT_INTAKE, Log(before, "log_10").State);
        Assert.Equal(LineState.LINE_CLEAR, before.Line.State);
        Assert.Null(before.ActiveIntakeDeadline);
        Assert.Equal(0, before.GetNodeOccupancy(NodeId.SAW_QUEUE));
        Assert.Null(before.PendingFeed);
        Assert.True(before.TryGetConfirmationResult(LogId.From("log_06"), out _));

        var accepted = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(before, completion, ServerTick.From(66)));

        Assert.Equal((LogState.QUEUED_FOR_SAW, RepairPendingTransitionFollowUp.NormalFeedPlanningEvaluationRequired, before.StateVersion.Next()), (Log(accepted.State, "log_10").State, accepted.FollowUpRequirement, accepted.State.StateVersion));
        Assert.Null(accepted.State.ActiveConfirmationTest);
        Assert.Same(results, accepted.State.ConfirmationResultsByLog);
        Assert.Same(before.Line, accepted.State.Line);
        Assert.Null(accepted.State.ActiveIntakeDeadline);
        Assert.Null(accepted.State.PendingFeed);
        Assert.Equal(LogState.IN_SAW, Log(accepted.State, "log_02").State);
        Assert.True(before.ProcessedIntentIds.SetEquals(accepted.State.ProcessedIntentIds));
    }

    [Fact]
    public void Newer_unrelated_active_confirmation_is_preserved_by_an_accepted_auto_transition()
    {
        var completion = IntakeAutoFeedComposition(ServerTick.From(66));
        var unrelated = ActiveConfirmationFor("log_06", ServerTick.From(59));
        var newer = RuntimeFixture.MoveHost(completion.State, "log_06", LogState.AT_FEED_GATE);
        newer = RuntimeFixture.MoveHost(newer, "log_06", LogState.AT_INTAKE);
        newer = RuntimeFixture.MoveHost(newer, "log_06", LogState.AT_PROCEDURE);
        newer = WithActiveConfirmation(newer, unrelated);
        var pending = Assert.IsType<PendingLineTransitionDescriptor>(completion.PendingTransition);

        Assert.True(newer.StateVersion > completion.State.StateVersion);
        Assert.Equal(LogState.AT_INTAKE, Log(newer, pending.LogId).State);
        Assert.Equal(1, newer.GetNodeOccupancy(NodeId.INTAKE));
        Assert.Equal(0, newer.GetNodeOccupancy(NodeId.SAW_QUEUE));
        Assert.Null(newer.ActiveIntakeDeadline);
        Assert.Same(unrelated, newer.ActiveConfirmationTest);
        Assert.Equal(LogId.From("log_06"), unrelated.LogId);

        var accepted = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(newer, completion, ServerTick.From(66)));

        Assert.Equal(LogState.QUEUED_FOR_SAW, Log(accepted.State, pending.LogId).State);
        Assert.Same(unrelated, accepted.State.ActiveConfirmationTest);
        Assert.Same(newer.ConfirmationResultsByLog, accepted.State.ConfirmationResultsByLog);
        Assert.Same(newer.Line, accepted.State.Line);
        Assert.Null(accepted.State.ActiveIntakeDeadline);
        Assert.Null(accepted.State.PendingFeed);
    }

    [Fact]
    public void Exact_due_and_late_repair_completions_are_accepted_without_starting_follow_ups()
    {
        var exact = FeedGateComposition(ServerTick.From(9));
        var exactAccepted = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(exact.State, exact, ServerTick.From(9)));
        var late = FeedGateComposition(ServerTick.From(12));
        var lateAccepted = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(late.State, late, ServerTick.From(12)));

        Assert.Equal((LogState.AT_INTAKE, RepairPendingTransitionFollowUp.IntakeDeadlineStartRequired), (Log(exactAccepted.State, "log_02").State, exactAccepted.FollowUpRequirement));
        Assert.Equal((LogState.AT_INTAKE, RepairPendingTransitionFollowUp.IntakeDeadlineStartRequired), (Log(lateAccepted.State, "log_02").State, lateAccepted.FollowUpRequirement));
        Assert.Null(exactAccepted.State.ActiveIntakeDeadline);
        Assert.Null(lateAccepted.State.ActiveIntakeDeadline);

        var autoExact = IntakeAutoFeedComposition(ServerTick.From(66));
        var autoExactAccepted = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(autoExact.State, autoExact, ServerTick.From(66)));
        var autoLate = IntakeAutoFeedComposition(ServerTick.From(69));
        var autoLateAccepted = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(autoLate.State, autoLate, ServerTick.From(69)));
        Assert.Equal((RepairPendingTransitionFollowUp.NormalFeedPlanningEvaluationRequired, LogState.QUEUED_FOR_SAW), (autoExactAccepted.FollowUpRequirement, Log(autoExactAccepted.State, "log_01").State));
        Assert.Equal((RepairPendingTransitionFollowUp.NormalFeedPlanningEvaluationRequired, LogState.QUEUED_FOR_SAW), (autoLateAccepted.FollowUpRequirement, Log(autoLateAccepted.State, "log_01").State));

    }

    [Fact]
    public void Newer_unrelated_mutations_are_preserved_and_repeated_execution_becomes_no_longer_applicable()
    {
        var completion = FeedGateCompletion(ServerTick.From(16));
        var newer = RuntimeFixture.MoveHost(completion.State, "log_01", LogState.AT_INTAKE);
        newer = RuntimeFixture.MoveHost(newer, "log_01", LogState.AT_PROCEDURE);

        var accepted = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(newer, completion, ServerTick.From(16)));
        Assert.Equal(LogState.AT_PROCEDURE, Log(accepted.State, "log_01").State);
        Assert.Equal(newer.StateVersion.Next(), accepted.CurrentStateVersion);
        Assert.Same(accepted.State, Assert.IsType<RepairPendingTransitionNoLongerApplicable>(Execute.Execute(accepted.State, completion, ServerTick.From(16))).State);
    }

    [Fact]
    public void Accepted_output_is_deterministic_and_execution_tick_is_controlled_sensitivity()
    {
        var firstCompletion = FeedGateCompletion(ServerTick.From(16));
        var first = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(firstCompletion.State, firstCompletion, ServerTick.From(20)));
        var secondCompletion = FeedGateCompletion(ServerTick.From(16));
        var second = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(secondCompletion.State, secondCompletion, ServerTick.From(20)));

        Assert.Equal((first.PendingTransition, first.LogId, first.Cause, first.Source, first.Destination, first.AppliedAt, first.PriorStateVersion, first.CurrentStateVersion, first.FollowUpRequirement), (second.PendingTransition, second.LogId, second.Cause, second.Source, second.Destination, second.AppliedAt, second.PriorStateVersion, second.CurrentStateVersion, second.FollowUpRequirement));
        Assert.True(first.State.ValueEquals(second.State));
        Assert.False(firstCompletion.State.ValueEquals(first.State));

        var changedTickCompletion = FeedGateCompletion(ServerTick.From(16));
        var changedTick = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(changedTickCompletion.State, changedTickCompletion, ServerTick.From(21)));
        Assert.Equal((first.LogId, first.Cause, first.Source, first.Destination, first.FollowUpRequirement), (changedTick.LogId, changedTick.Cause, changedTick.Source, changedTick.Destination, changedTick.FollowUpRequirement));
        Assert.Equal((ServerTick.From(20), ServerTick.From(21)), (first.AppliedAt, changedTick.AppliedAt));
        Assert.True(first.State.ValueEquals(changedTick.State));
    }

    [Fact]
    public void Journal_commits_jam_repair_start_completion_and_pending_movement_separately_while_no_ops_leave_cursor_unchanged()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var journal = new InMemoryEventJournal(initial.ShiftId);
        var commits = new JournaledMutationCommitService();
        var firstGate = RuntimeFixture.MoveHost(initial, "log_01", LogState.AT_FEED_GATE);
        Commit(commits, journal, initial, firstGate, ServerTick.From(1), "first_gate");
        var intake = RuntimeFixture.MoveHost(firstGate, "log_01", LogState.AT_INTAKE);
        Commit(commits, journal, firstGate, intake, ServerTick.From(1), "intake");
        var gate = RuntimeFixture.MoveHost(intake, "log_02", LogState.AT_FEED_GATE);
        Commit(commits, journal, intake, gate, ServerTick.From(2), "gate");
        var jam = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(gate, JamCause.FEED_GATE_BLOCKED, ServerTick.From(10)));
        var jamCommit = Commit(commits, journal, gate, jam.State, ServerTick.From(10), "jam");
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jam.State, ServerTick.From(10), Fixture.LoadP0().Shift.Scheduler));
        var repairCommit = Commit(commits, journal, jam.State, repairing.State, ServerTick.From(10), "repair_start");
        var unblocked = RuntimeFixture.MoveHost(repairing.State, "log_01", LogState.AT_PROCEDURE);
        Commit(commits, journal, repairing.State, unblocked, ServerTick.From(11), "unblock");
        var completion = Assert.IsType<LineRepairCompleted>(new LineRepairDueCompletionService().CompleteDue(unblocked, ServerTick.From(16)));
        var completionCommit = Commit(commits, journal, unblocked, completion.State, ServerTick.From(16), "repair_complete");
        var executed = Assert.IsType<RepairPendingTransitionExecuted>(Execute.Execute(completion.State, completion, ServerTick.From(16)));
        var movementCommit = Commit(commits, journal, completion.State, executed.State, ServerTick.From(16), "pending_transition");

        Assert.True(jamCommit.Envelope.Sequence < repairCommit.Envelope.Sequence);
        Assert.True(repairCommit.Envelope.Sequence < completionCommit.Envelope.Sequence);
        Assert.True(completionCommit.Envelope.Sequence < movementCommit.Envelope.Sequence);
        Assert.Equal(journal.Count, (int)journal.LastSequence.Value);
        Assert.Equal(journal.Count, (int)journal.LastStateVersion.Value);
        Assert.Equal(executed.State.StateVersion, journal.LastStateVersion);
        Assert.Same(completion.State, movementCommit.Before);
        Assert.Same(executed.State, movementCommit.After);
        var cursor = (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion);
        Assert.Same(executed.State, Assert.IsType<RepairPendingTransitionNoLongerApplicable>(Execute.Execute(executed.State, completion, ServerTick.From(16))).State);
        Assert.Equal(cursor, (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion));
    }

    [Fact]
    public void Public_input_surface_is_closed()
    {
        var method = typeof(RepairPendingTransitionExecutionService).GetMethod(nameof(RepairPendingTransitionExecutionService.Execute), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        Assert.Equal(new[] { typeof(ShiftRuntimeState), typeof(LineRepairCompleted), typeof(ServerTick) }, method.GetParameters().Select(parameter => parameter.ParameterType));
    }

    private static LineRepairCompleted FeedGateComposition(ServerTick completedAt)
    {
        var fixture = Fixture.LoadP0();
        var initial = ShiftRuntimeState.Create(fixture.Shift);
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, fixture.Shift.Scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        var deadline = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admitted.State, admitted, fixture.Shift.Profiles[ProfileId.From("learning")]));
        var earlyIntent = new IntentEnvelope(deadline.State.ShiftId, IntentId.From("tlaw019_early"), ActorId.From("untrusted"), FeedPlanningTargets.FeedGate, FeedPlanningIntentActions.RequestEarlyFeed, deadline.State.StateVersion, ServerTick.From(1), NoIntentParameters.Instance);
        var early = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(deadline.State, earlyIntent, RuntimeFixture.BoundActor, ServerTick.From(1), fixture.Shift.Scheduler));
        var atGate = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(early.State, ServerTick.From(3)));
        var jam = Assert.IsType<FeedGateJamDerived>(new FeedGateJamDerivationService().Derive(atGate.State, ServerTick.From(3)));
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jam.State, ServerTick.From(3), fixture.Shift.Scheduler));
        var unblocked = RuntimeFixture.MoveHost(repairing.State, "log_01", LogState.AT_PROCEDURE);
        return Assert.IsType<LineRepairCompleted>(new LineRepairDueCompletionService().CompleteDue(unblocked, completedAt));
    }

    private static LineRepairCompleted IntakeAutoFeedComposition(ServerTick completedAt)
    {
        var fixture = Fixture.LoadP0();
        var scheduler = fixture.Shift.Scheduler with { Capacities = fixture.Shift.Scheduler.Capacities.SetItem(NodeId.INTAKE, NodeCapacity.Limited(2)) };
        var initial = ShiftRuntimeState.Create(fixture.Shift with { Scheduler = scheduler });
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        var deadline = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admitted.State, admitted, fixture.Shift.Profiles[ProfileId.From("learning")]));
        var queue = RuntimeFixture.MoveHost(RuntimeFixture.MoveHost(RuntimeFixture.MoveHost(deadline.State, "log_02", LogState.AT_FEED_GATE), "log_02", LogState.AT_INTAKE), "log_02", LogState.QUEUED_FOR_SAW);
        var expired = Assert.IsType<IntakeDeadlineExpired>(new IntakeDeadlineExpirationService().Expire(queue, ServerTick.From(60)));
        var blocked = Assert.IsType<DefaultIntakeAutoRouteBlocked>(new DefaultIntakeAutoRouteService().Attempt(expired.State, expired.FollowUp, ServerTick.From(60)));
        var jam = Assert.IsType<IntakeAutoFeedJamEntered>(new IntakeAutoFeedJamDerivationService().Derive(blocked.State, blocked));
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jam.State, ServerTick.From(60), scheduler));
        var unblocked = RuntimeFixture.MoveHost(repairing.State, "log_02", LogState.IN_SAW);
        return Assert.IsType<LineRepairCompleted>(new LineRepairDueCompletionService().CompleteDue(unblocked, completedAt));
    }

    private static LineRepairCompleted IntakeAutoFeedCompositionWithOwnerConfirmation(ServerTick completedAt)
    {
        var fixture = Fixture.LoadP0();
        var owner = LogId.From("log_10");
        var scheduler = fixture.Shift.Scheduler with { Capacities = fixture.Shift.Scheduler.Capacities.SetItem(NodeId.INTAKE, NodeCapacity.Limited(2)) };
        var configuration = fixture.Shift with
        {
            Scheduler = scheduler,
            Manifest = fixture.Shift.Manifest.OrderBy(log => log.Id == owner ? 0 : 1).ToImmutableArray()
        };
        var initial = ShiftRuntimeState.Create(configuration);
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, scheduler));
        var admitted = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        var deadline = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admitted.State, admitted, configuration.Profiles[ProfileId.From("learning")]));
        var secondAtIntake = RuntimeFixture.MoveHost(RuntimeFixture.MoveHost(deadline.State, "log_06", LogState.AT_FEED_GATE), "log_06", LogState.AT_INTAKE);
        var priorConfirmation = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(secondAtIntake, LogId.From("log_06"), ImmutableHashSet.Create(ItemId.From("choir_cassette")), ServerTick.From(1), LineNoise.QUIET, fixture.Anomalies));
        var completedConfirmation = Assert.IsType<ConfirmationTestDueCompleted>(new ConfirmationTestDueCompletionService().CompleteDue(priorConfirmation.State, ServerTick.From(5), fixture.Anomalies));
        var ownerAtIntake = RuntimeFixture.MoveHost(completedConfirmation.State, "log_06", LogState.AT_PROCEDURE);
        var ownerConfirmation = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(ownerAtIntake, owner, ImmutableHashSet.Create(ItemId.From("choir_cassette")), ServerTick.From(59), LineNoise.QUIET, fixture.Anomalies));
        var queue = RuntimeFixture.MoveHost(RuntimeFixture.MoveHost(RuntimeFixture.MoveHost(ownerConfirmation.State, "log_02", LogState.AT_FEED_GATE), "log_02", LogState.AT_INTAKE), "log_02", LogState.QUEUED_FOR_SAW);
        var expired = Assert.IsType<IntakeDeadlineExpired>(new IntakeDeadlineExpirationService().Expire(queue, ServerTick.From(60)));
        var blocked = Assert.IsType<DefaultIntakeAutoRouteBlocked>(new DefaultIntakeAutoRouteService().Attempt(expired.State, expired.FollowUp, ServerTick.From(60)));
        var jam = Assert.IsType<IntakeAutoFeedJamEntered>(new IntakeAutoFeedJamDerivationService().Derive(blocked.State, blocked));
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jam.State, ServerTick.From(60), scheduler));
        var unblocked = RuntimeFixture.MoveHost(repairing.State, "log_02", LogState.IN_SAW);
        return Assert.IsType<LineRepairCompleted>(new LineRepairDueCompletionService().CompleteDue(unblocked, completedAt));
    }

    private static LineRepairCompleted FeedGateCompletion(ServerTick completedAt)
    {
        var initial = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var gate = RuntimeFixture.MoveHost(initial, "log_02", LogState.AT_FEED_GATE);
        var jam = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(gate, JamCause.FEED_GATE_BLOCKED, ServerTick.From(10)));
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jam.State, ServerTick.From(10), Fixture.LoadP0().Shift.Scheduler));
        var unblocked = RuntimeFixture.MoveHost(repairing.State, "log_01", LogState.AT_PROCEDURE);
        return Assert.IsType<LineRepairCompleted>(new LineRepairDueCompletionService().CompleteDue(unblocked, completedAt));
    }

    private static ShiftRuntimeState FeedJammed(ServerTick tick)
    {
        var initial = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var gate = RuntimeFixture.MoveHost(initial, "log_02", LogState.AT_FEED_GATE);
        return Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(gate, JamCause.FEED_GATE_BLOCKED, tick)).State;
    }

    private static ShiftRuntimeState MissingOwnerCurrent(LineRepairCompleted completion)
    {
        var fixture = Fixture.LoadP0();
        var withoutOwner = fixture.Shift with { Manifest = fixture.Shift.Manifest.Where(log => log.Id != LogId.From("log_02")).ToImmutableArray() };
        var state = ShiftRuntimeState.Create(withoutOwner);
        while (state.StateVersion <= completion.State.StateVersion)
        {
            state = Log(state, "log_03").State switch
            {
                LogState.SCHEDULED => RuntimeFixture.MoveHost(state, "log_03", LogState.AT_FEED_GATE),
                LogState.AT_FEED_GATE => RuntimeFixture.MoveHost(state, "log_03", LogState.AT_INTAKE),
                LogState.AT_INTAKE => RuntimeFixture.MoveHost(state, "log_03", LogState.AT_PROCEDURE),
                LogState.AT_PROCEDURE => RuntimeFixture.MoveHost(state, "log_03", LogState.AT_INTAKE),
                _ => throw new InvalidOperationException("Fixture log entered an unexpected state.")
            };
        }

        return state;
    }

    private static ShiftRuntimeState WithLine(ShiftRuntimeState state, LineRuntimeState line)
    {
        var mutation = typeof(ShiftRuntimeState).GetMethod("WithLine", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsType<ShiftRuntimeState>(mutation.Invoke(state, [line]));
    }

    private static ActiveConfirmationTest ActiveConfirmationFor(string logId, ServerTick tick)
    {
        var fixture = Fixture.LoadP0();
        var atIntake = RuntimeFixture.MoveToIntake(ShiftRuntimeState.Create(fixture.Shift), logId);
        var started = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(atIntake, LogId.From(logId), ImmutableHashSet.Create(ItemId.From("choir_cassette")), tick, LineNoise.QUIET, fixture.Anomalies));
        return Assert.IsType<ActiveConfirmationTest>(started.State.ActiveConfirmationTest);
    }

    private static ShiftRuntimeState WithActiveConfirmation(ShiftRuntimeState state, ActiveConfirmationTest active)
    {
        var mutation = typeof(ShiftRuntimeState).GetMethod("WithActiveConfirmation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsType<ShiftRuntimeState>(mutation.Invoke(state, [active, null]));
    }

    private static void AssertAccepted(ShiftRuntimeState before, LineRepairCompleted completion, RepairPendingTransitionExecuted accepted, LogState destination, RepairPendingTransitionFollowUp followUp, ServerTick appliedAt)
    {
        var pending = Assert.IsType<PendingLineTransitionDescriptor>(completion.PendingTransition);
        Assert.Same(pending, accepted.PendingTransition);
        Assert.Equal((pending.LogId, pending.Cause, pending.FromState, destination, appliedAt, before.StateVersion, before.StateVersion.Next(), followUp), (accepted.LogId, accepted.Cause, accepted.Source, accepted.Destination, accepted.AppliedAt, accepted.PriorStateVersion, accepted.CurrentStateVersion, accepted.FollowUpRequirement));
        Assert.Equal(accepted.CurrentStateVersion, accepted.State.StateVersion);
        Assert.Equal(destination, Log(accepted.State, pending.LogId).State);
        Assert.Equal(before.Line, accepted.State.Line);
        Assert.Equal(LineState.LINE_CLEAR, accepted.State.Line.State);
        Assert.Null(accepted.State.Line.ActiveRepairHold);
        Assert.Null(accepted.State.ActiveIntakeDeadline);
        Assert.True(before.ProcessedIntentIds.SetEquals(accepted.State.ProcessedIntentIds));
        Assert.Equal(before.PendingFeed, accepted.State.PendingFeed);
        Assert.Equal(before.Inventory, accepted.State.Inventory);
        Assert.Equal(before.ProcedureProgressByLog, accepted.State.ProcedureProgressByLog);
        Assert.Equal(before.ActiveProcedureHold, accepted.State.ActiveProcedureHold);
        Assert.Equal(before.ActiveConfirmationTest, accepted.State.ActiveConfirmationTest);
        Assert.Equal(before.ConfirmationResultsByLog, accepted.State.ConfirmationResultsByLog);
        Assert.Equal(before.Containment, accepted.State.Containment);
        Assert.Equal(before.ActiveContainmentRitual, accepted.State.ActiveContainmentRitual);
        Assert.Equal(before.ShiftId, accepted.State.ShiftId);
        Assert.Equal(before.ShiftSeed, accepted.State.ShiftSeed);
    }

    private static void AssertDefensive(ShiftRuntimeState expected, RepairPendingTransitionExecutionResult result, RepairPendingTransitionExecutionFailureReason reason)
    {
        var failed = Assert.IsType<RepairPendingTransitionExecutionDefensiveFailure>(result);
        Assert.Same(expected, failed.State);
        Assert.Equal(reason, failed.Reason);
        Assert.Equal(expected.StateVersion, failed.State.StateVersion);
    }

    private static JournaledMutationCommitted Commit(JournaledMutationCommitService commits, IEventJournal journal, ShiftRuntimeState before, ShiftRuntimeState after, ServerTick tick, string id) => Assert.IsType<JournaledMutationCommitted>(commits.Commit(journal, before, after, tick, new DomainEventDraft(EventId.From($"tlaw019_{id}"), EventTypeId.From("test.tlaw019"), new Payload(id))));

    private static LogRuntimeState Log(ShiftRuntimeState state, string id) => Log(state, LogId.From(id));

    private static LogRuntimeState Log(ShiftRuntimeState state, LogId id)
    {
        Assert.True(state.TryGetLog(id, out var log));
        return log;
    }

    private sealed record Payload(string Value) : IDomainEventPayload;
}
