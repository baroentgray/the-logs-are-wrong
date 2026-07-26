using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Tests.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Line;

[Trait("Scope", "TLAW-010")]
public sealed class JamRepairLifecycleTests
{
    private static readonly LineJamEntryService EnterJam = new();
    private static readonly LineRepairStartService StartRepair = new();
    private static readonly LineRepairDueCompletionService CompleteRepair = new();

    [Fact]
    public void Initial_line_is_clear_and_public_values_reject_contradictory_combinations()
    {
        var initial = RuntimeFixture.CreateInitialState();

        Assert.Equal((LineState.LINE_CLEAR, ServerTick.Zero, (JamCause?)null, (LogId?)null), (initial.Line.State, initial.Line.EnteredAt, initial.Line.Cause, initial.Line.PendingLogId));
        Assert.Null(initial.Line.ActiveRepairHold);
        Assert.Throws<ArgumentException>(() => new LineRuntimeState(LineState.LINE_CLEAR, ServerTick.Zero, JamCause.FEED_GATE_BLOCKED, LogId.From("log_01"), null));
        Assert.Throws<ArgumentException>(() => new LineRuntimeState(LineState.LINE_JAMMED, ServerTick.Zero, null, null, null));
        Assert.Throws<ArgumentException>(() => new LineRuntimeState(LineState.REPAIRING, ServerTick.Zero, JamCause.FEED_GATE_BLOCKED, LogId.From("log_01"), null));
        Assert.Throws<ArgumentException>(() => new ActiveRepairHold(ServerTick.Zero, ServerTick.From(5), TheLogsAreWrong.Domain.Time.SimulationDuration.FromTicks(6)));
        Assert.Throws<ArgumentException>(() => new PendingLineTransitionDescriptor(default, LogState.AT_FEED_GATE, LogState.AT_INTAKE, JamCause.FEED_GATE_BLOCKED));
    }

    [Fact]
    public void Repairing_line_entry_must_equal_active_repair_start()
    {
        var hold = new ActiveRepairHold(ServerTick.From(10), ServerTick.From(16), TheLogsAreWrong.Domain.Time.SimulationDuration.FromTicks(6));

        Assert.Throws<ArgumentException>(() => new LineRuntimeState(LineState.REPAIRING, ServerTick.From(11), JamCause.FEED_GATE_BLOCKED, LogId.From("log_01"), hold));
        Assert.Throws<ArgumentException>(() => new LineRuntimeState(LineState.REPAIRING, ServerTick.From(9), JamCause.FEED_GATE_BLOCKED, LogId.From("log_01"), hold));

        var valid = new LineRuntimeState(LineState.REPAIRING, ServerTick.From(10), JamCause.FEED_GATE_BLOCKED, LogId.From("log_01"), hold);
        Assert.Equal((ServerTick.From(10), ServerTick.From(10)), (valid.EnteredAt, valid.ActiveRepairHold!.StartedAt));
    }

    [Fact]
    public void Feed_gate_jam_derives_the_exact_pending_log_and_moves_nothing()
    {
        var state = CreateFeedGateShape();
        var priorVersion = state.StateVersion;
        var entered = Assert.IsType<LineJamEntered>(EnterJam.Enter(state, JamCause.FEED_GATE_BLOCKED, ServerTick.From(10)));

        Assert.Equal((LineState.LINE_JAMMED, ServerTick.From(10), JamCause.FEED_GATE_BLOCKED, LogId.From("log_02")), (entered.State.Line.State, entered.State.Line.EnteredAt, entered.State.Line.Cause, entered.State.Line.PendingLogId));
        Assert.Null(entered.State.Line.ActiveRepairHold);
        Assert.Equal(priorVersion.Next(), entered.State.StateVersion);
        Assert.Equal(LogState.AT_FEED_GATE, GetLog(entered.State, "log_02").State);
        Assert.Equal(LogState.AT_INTAKE, GetLog(entered.State, "log_01").State);

        var second = Assert.IsType<LineJamEntryRejected>(EnterJam.Enter(entered.State, JamCause.FEED_GATE_BLOCKED, ServerTick.From(11)));
        Assert.Equal(LineJamEntryRejectionReason.LINE_NOT_CLEAR, second.Reason);
        Assert.Same(entered.State, second.State);
    }

    [Fact]
    public void Auto_feed_jam_derives_the_exact_intake_log_and_rejects_invalid_shapes()
    {
        var state = CreateAutoFeedShape();
        var entered = Assert.IsType<LineJamEntered>(EnterJam.Enter(state, JamCause.INTAKE_AUTOFEED_BLOCKED, ServerTick.From(10)));
        Assert.Equal((JamCause.INTAKE_AUTOFEED_BLOCKED, LogId.From("log_01")), (entered.State.Line.Cause, entered.State.Line.PendingLogId));
        Assert.Equal(LogState.AT_INTAKE, GetLog(entered.State, "log_01").State);
        Assert.Equal(LogState.QUEUED_FOR_SAW, GetLog(entered.State, "log_02").State);

        var missing = Assert.IsType<LineJamEntryRejected>(EnterJam.Enter(RuntimeFixture.CreateInitialState(), JamCause.FEED_GATE_BLOCKED, ServerTick.From(10)));
        Assert.Equal(LineJamEntryRejectionReason.CAUSE_RUNTIME_SHAPE_INVALID, missing.Reason);
        var defaultCause = Assert.IsType<LineJamEntryRejected>(EnterJam.Enter(RuntimeFixture.CreateInitialState(), default, ServerTick.From(10)));
        Assert.Equal(LineJamEntryRejectionReason.CAUSE_RUNTIME_SHAPE_INVALID, defaultCause.Reason);
        var undefinedCause = Assert.IsType<LineJamEntryRejected>(EnterJam.Enter(RuntimeFixture.CreateInitialState(), (JamCause)99, ServerTick.From(10)));
        Assert.Equal(LineJamEntryRejectionReason.CAUSE_RUNTIME_SHAPE_INVALID, undefinedCause.Reason);
    }

    [Fact]
    public void Ambiguous_feed_gate_shape_rejects_without_selecting_a_pending_log()
    {
        var state = CreateWithCapacity(NodeId.FEED_GATE, 2);
        state = RuntimeFixture.MoveToIntake(state, "log_03");
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);

        var rejected = Assert.IsType<LineJamEntryRejected>(EnterJam.Enter(state, JamCause.FEED_GATE_BLOCKED, ServerTick.From(10)));
        Assert.Equal(LineJamEntryRejectionReason.CAUSE_RUNTIME_SHAPE_INVALID, rejected.Reason);
        Assert.Same(state, rejected.State);
    }

    [Fact]
    public void Repair_start_requires_jam_and_creates_exact_six_tick_hold_once()
    {
        var fixture = Fixture.LoadP0();
        var clear = RuntimeFixture.CreateInitialState();
        var noJam = Assert.IsType<LineRepairStartRejected>(StartRepair.Start(clear, ServerTick.From(10), fixture.Shift.Scheduler));
        Assert.Equal(LineRepairStartRejectionReason.NO_ACTIVE_JAM, noJam.Reason);
        Assert.Same(clear, noJam.State);

        var jammed = FeedJammed();
        var started = Assert.IsType<LineRepairStarted>(StartRepair.Start(jammed, ServerTick.From(10), fixture.Shift.Scheduler));
        Assert.Equal(LineState.REPAIRING, started.State.Line.State);
        Assert.Equal(JamCause.FEED_GATE_BLOCKED, started.State.Line.Cause);
        Assert.Equal(LogId.From("log_02"), started.State.Line.PendingLogId);
        Assert.Equal((ServerTick.From(10), ServerTick.From(16), 6L), (started.Hold.StartedAt, started.Hold.DueAt, started.Hold.Duration.Value));
        Assert.Equal(jammed.StateVersion.Next(), started.State.StateVersion);
        Assert.Equal(LogState.AT_FEED_GATE, GetLog(started.State, "log_02").State);

        var duplicate = Assert.IsType<LineRepairStartRejected>(StartRepair.Start(started.State, ServerTick.From(11), fixture.Shift.Scheduler));
        Assert.Equal(LineRepairStartRejectionReason.REPAIR_ALREADY_ACTIVE, duplicate.Reason);
        Assert.Same(started.State, duplicate.State);
    }

    [Fact]
    public void Repair_due_returns_unchanged_while_feed_gate_blocker_remains_then_completes_same_hold()
    {
        var fixture = Fixture.LoadP0();
        var repairing = FeedRepairing(fixture);
        var early = Assert.IsType<LineRepairNotDue>(CompleteRepair.CompleteDue(repairing, ServerTick.From(15)));
        Assert.Same(repairing, early.State);

        var blocked = Assert.IsType<LineRepairBlockingConditionRemains>(CompleteRepair.CompleteDue(repairing, ServerTick.From(16)));
        Assert.Same(repairing, blocked.State);
        Assert.Equal(ServerTick.From(16), blocked.State.Line.ActiveRepairHold!.DueAt);

        var unblocked = RuntimeFixture.MoveHost(blocked.State, "log_01", LogState.AT_PROCEDURE);
        var completed = Assert.IsType<LineRepairCompleted>(CompleteRepair.CompleteDue(unblocked, ServerTick.From(16)));
        Assert.Equal(LineState.LINE_CLEAR, completed.State.Line.State);
        Assert.Null(completed.State.Line.Cause);
        Assert.Null(completed.State.Line.PendingLogId);
        var descriptor = Assert.IsType<PendingLineTransitionDescriptor>(completed.PendingTransition);
        Assert.Equal((LogId.From("log_02"), LogState.AT_FEED_GATE, LogState.AT_INTAKE, JamCause.FEED_GATE_BLOCKED), (descriptor.LogId, descriptor.FromState, descriptor.ToState, descriptor.Cause));
        Assert.Equal(LogState.AT_FEED_GATE, GetLog(completed.State, "log_02").State);
        Assert.Equal(unblocked.StateVersion.Next(), completed.State.StateVersion);
        Assert.IsType<LineRepairNoActive>(CompleteRepair.CompleteDue(completed.State, ServerTick.From(17)));
    }

    [Fact]
    public void Auto_feed_repair_rechecks_saw_queue_and_returns_exact_data_only_descriptor_at_late_completion()
    {
        var fixture = Fixture.LoadP0();
        var repairing = AutoRepairing(fixture);
        var blocked = Assert.IsType<LineRepairBlockingConditionRemains>(CompleteRepair.CompleteDue(repairing, ServerTick.From(16)));
        Assert.Same(repairing, blocked.State);

        var unblocked = RuntimeFixture.MoveHost(blocked.State, "log_02", LogState.IN_SAW);
        var completed = Assert.IsType<LineRepairCompleted>(CompleteRepair.CompleteDue(unblocked, ServerTick.From(22)));
        var descriptor = Assert.IsType<PendingLineTransitionDescriptor>(completed.PendingTransition);
        Assert.Equal((LogId.From("log_01"), LogState.AT_INTAKE, LogState.QUEUED_FOR_SAW, JamCause.INTAKE_AUTOFEED_BLOCKED), (descriptor.LogId, descriptor.FromState, descriptor.ToState, descriptor.Cause));
        Assert.Equal(LogState.AT_INTAKE, GetLog(completed.State, "log_01").State);
    }

    [Fact]
    public void Moved_auto_feed_target_completes_without_replacement_descriptor()
    {
        var fixture = Fixture.LoadP0();
        var repairing = AutoRepairing(fixture);
        var targetMoved = RuntimeFixture.MoveHost(repairing, "log_01", LogState.AT_PROCEDURE);
        var unblocked = RuntimeFixture.MoveHost(targetMoved, "log_02", LogState.IN_SAW);

        var completed = Assert.IsType<LineRepairCompleted>(CompleteRepair.CompleteDue(unblocked, ServerTick.From(16)));
        Assert.Null(completed.PendingTransition);
        Assert.Equal(LogState.AT_PROCEDURE, GetLog(completed.State, "log_01").State);
        Assert.Equal(LineState.LINE_CLEAR, completed.State.Line.State);
    }

    [Fact]
    public void Timing_rejects_backward_calls_and_checked_repair_due_overflow()
    {
        var fixture = Fixture.LoadP0();
        var jammed = FeedJammed();
        Assert.Throws<ArgumentOutOfRangeException>(() => StartRepair.Start(jammed, ServerTick.From(9), fixture.Shift.Scheduler));
        var repairing = FeedRepairing(fixture);
        Assert.Throws<ArgumentOutOfRangeException>(() => CompleteRepair.CompleteDue(repairing, ServerTick.From(9)));

        var lateJam = Assert.IsType<LineJamEntered>(EnterJam.Enter(CreateFeedGateShape(), JamCause.FEED_GATE_BLOCKED, ServerTick.From(long.MaxValue - 2)));
        Assert.Throws<OverflowException>(() => StartRepair.Start(lateJam.State, ServerTick.From(long.MaxValue - 2), fixture.Shift.Scheduler));
    }

    [Fact]
    public void Jam_and_repair_preserve_procedure_confirmation_and_containment_lifecycles()
    {
        var fixture = Fixture.LoadP0();
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_10");
        var confirmationStart = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(state, LogId.From("log_10"), ImmutableHashSet.Create(ItemId.From("choir_cassette")), ServerTick.From(1), LineNoise.QUIET, fixture.Anomalies));
        var confirmationDone = Assert.IsType<ConfirmationTestDueCompleted>(new ConfirmationTestDueCompletionService().CompleteDue(confirmationStart.State, ServerTick.From(5), fixture.Anomalies));
        state = RuntimeFixture.MoveHost(confirmationDone.State, "log_10", LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveToIntake(state, "log_08");
        state = RuntimeFixture.MoveHost(state, "log_08", LogState.HELD_WRITTEN_OFF);
        state = RuntimeFixture.MoveToIntake(state, "log_03");
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_PROCEDURE);
        var procedure = Assert.IsType<ProcedureActionHoldStarted>(new ProcedureActionStartService().Start(state, LogId.From("log_03"), ItemId.From("holy_water"), ServerTick.From(10), fixture.Anomalies));
        state = procedure.State;
        state = RuntimeFixture.MoveToIntake(state, "log_06");
        var activeConfirmation = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(state, LogId.From("log_06"), ImmutableHashSet.Create(ItemId.From("choir_cassette")), ServerTick.From(11), LineNoise.QUIET, fixture.Anomalies));
        state = RuntimeFixture.MoveHost(activeConfirmation.State, "log_02", LogState.AT_FEED_GATE);
        var containment = Assert.IsType<ContainmentStableIntervalArmed>(new ContainmentAdvanceService().Advance(state, ServerTick.From(12), fixture.Shift.Containment, fixture.Anomalies));

        var jammed = Assert.IsType<LineJamEntered>(EnterJam.Enter(containment.State, JamCause.FEED_GATE_BLOCKED, ServerTick.From(12))).State;
        var repairing = Assert.IsType<LineRepairStarted>(StartRepair.Start(jammed, ServerTick.From(12), fixture.Shift.Scheduler)).State;
        Assert.Equal(procedure.Hold, repairing.ActiveProcedureHold);
        Assert.NotNull(repairing.ActiveConfirmationTest);
        Assert.True(repairing.TryGetConfirmationResult(LogId.From("log_10"), out _));
        Assert.Equal(containment.State.Containment, repairing.Containment);

        var transitioned = RuntimeFixture.MoveHost(repairing, "log_06", LogState.HELD_WRITTEN_OFF);
        Assert.Equal(repairing.Line, transitioned.Line);
    }

    [Fact]
    public void Identical_sequences_are_value_equal_and_line_state_participates_in_value_equality()
    {
        var left = FeedJammed();
        var right = FeedJammed();
        Assert.True(left.ValueEquals(right));

        var clear = CreateFeedGateShape();
        Assert.False(left.ValueEquals(clear));
    }

    private static ShiftRuntimeState FeedJammed() => Assert.IsType<LineJamEntered>(EnterJam.Enter(CreateFeedGateShape(), JamCause.FEED_GATE_BLOCKED, ServerTick.From(10))).State;

    private static ShiftRuntimeState FeedRepairing(ValidatedConfiguration fixture) => Assert.IsType<LineRepairStarted>(StartRepair.Start(FeedJammed(), ServerTick.From(10), fixture.Shift.Scheduler)).State;

    private static ShiftRuntimeState AutoRepairing(ValidatedConfiguration fixture) => Assert.IsType<LineRepairStarted>(StartRepair.Start(Assert.IsType<LineJamEntered>(EnterJam.Enter(CreateAutoFeedShape(), JamCause.INTAKE_AUTOFEED_BLOCKED, ServerTick.From(10))).State, ServerTick.From(10), fixture.Shift.Scheduler)).State;

    private static ShiftRuntimeState CreateFeedGateShape()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        return RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
    }

    private static ShiftRuntimeState CreateAutoFeedShape()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_02");
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.QUEUED_FOR_SAW);
        return RuntimeFixture.MoveToIntake(state, "log_01");
    }

    private static ShiftRuntimeState CreateWithCapacity(NodeId node, int capacity)
    {
        var fixture = Fixture.LoadP0();
        var scheduler = fixture.Shift.Scheduler with { Capacities = fixture.Shift.Scheduler.Capacities.SetItem(node, NodeCapacity.Limited(capacity)) };
        return ShiftRuntimeState.Create(fixture.Shift with { Scheduler = scheduler });
    }

    private static LogRuntimeState GetLog(ShiftRuntimeState state, string logId)
    {
        Assert.True(state.TryGetLog(LogId.From(logId), out var log));
        return log;
    }
}
