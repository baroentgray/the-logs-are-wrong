using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-040")]
public sealed class LineRepairIntentHandlerTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static readonly LineRepairIntentHandler Handler = new();

    [Fact]
    public void Feed_gate_repair_start_retains_exact_tlaw010_result_and_atomically_marks_the_accepted_intent()
    {
        var before = FeedGateJammed();
        var intent = StartIntent(before, "feed_repair", before.StateVersion);

        var result = Assert.IsType<LineRepairIntentStarted>(Handler.Handle(
            before, intent, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Shift.Scheduler));

        Assert.Same(result.Result.State, result.State);
        Assert.Empty(before.ProcessedIntentIds);
        Assert.Equal(before.StateVersion.Next(), result.State.StateVersion);
        Assert.Contains(intent.IntentId, result.State.ProcessedIntentIds);
        Assert.Equal(JamCause.FEED_GATE_BLOCKED, result.State.Line.Cause);
        Assert.Equal(LogId.From("log_02"), result.State.Line.PendingLogId);
        Assert.Equal(LineState.REPAIRING, result.State.Line.State);
        var hold = Assert.IsType<ActiveRepairHold>(result.State.Line.ActiveRepairHold);
        Assert.Same(hold, result.Result.Hold);
        Assert.Equal(ServerTick.From(10), hold.StartedAt);
        Assert.Equal(ServerTick.From(16), hold.DueAt);
        Assert.Equal(SimulationDuration.FromTicks(6), hold.Duration);
        Assert.Same(before.Inventory, result.State.Inventory);
        Assert.Same(before.ProcedureProgressByLog, result.State.ProcedureProgressByLog);
        Assert.Same(before.ActiveProcedureHold, result.State.ActiveProcedureHold);
        Assert.Same(before.ActiveConfirmationTest, result.State.ActiveConfirmationTest);
        Assert.Same(before.ConfirmationResultsByLog, result.State.ConfirmationResultsByLog);
        Assert.All(before.Logs, log =>
        {
            Assert.True(result.State.TryGetLog(log.LogId, out var after));
            Assert.Same(log, after);
        });
    }

    [Fact]
    public void Intake_auto_feed_repair_start_preserves_the_exact_existing_cause_and_pending_log()
    {
        var before = IntakeAutoFeedJammed();
        var intent = StartIntent(before, "auto_repair", before.StateVersion);

        var result = Assert.IsType<LineRepairIntentStarted>(Handler.Handle(
            before, intent, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Shift.Scheduler));

        Assert.Equal(JamCause.INTAKE_AUTOFEED_BLOCKED, result.State.Line.Cause);
        Assert.Equal(LogId.From("log_02"), result.State.Line.PendingLogId);
        Assert.Equal(LineState.REPAIRING, result.State.Line.State);
        Assert.Equal(ServerTick.From(16), result.State.Line.ActiveRepairHold!.DueAt);
        Assert.Contains(intent.IntentId, result.State.ProcessedIntentIds);
    }

    [Fact]
    public void Guard_order_and_unsupported_contract_shapes_leave_the_exact_input_unmarked()
    {
        var state = FeedGateJammed();
        var wrongShift = new IntentEnvelope(
            ShiftId.From("OTHER_SHIFT"), IntentId.From("wrong_shift"), ActorId.From("hint"), TargetId.From("OTHER"),
            IntentActionId.From("unsupported"), StateVersion.From(99), ServerTick.Zero, new ProcedureActionIntentParameters(ItemId.From("holy_water")));
        AssertRejected(Handler.Handle(state, wrongShift, null, ServerTick.From(10), Fx.Shift.Scheduler), RejectionReason.SHIFT_MISMATCH, state);

        var missingActor = StartIntent(state, "missing_actor", state.StateVersion);
        AssertRejected(Handler.Handle(state, missingActor, null, ServerTick.From(10), Fx.Shift.Scheduler), RejectionReason.ACTOR_NOT_BOUND, state);

        var stale = StartIntent(state, "stale", state.StateVersion.Next());
        AssertRejected(Handler.Handle(state, stale, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Shift.Scheduler), RejectionReason.STALE_STATE_VERSION, state);

        var unsupportedAction = new IntentEnvelope(
            state.ShiftId, IntentId.From("unsupported_action"), ActorId.From("hint"), LineRepairIntentTargets.Line,
            IntentActionId.From("unowned"), state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var action = Assert.IsType<LineRepairIntentUnsupportedAction>(Handler.Handle(state, unsupportedAction, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Shift.Scheduler));
        Assert.Same(state, action.State);

        var unsupportedTarget = new IntentEnvelope(
            state.ShiftId, IntentId.From("unsupported_target"), ActorId.From("hint"), TargetId.From("OTHER"),
            LineRepairIntentActions.StartLineRepair, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var target = Assert.IsType<LineRepairIntentUnsupportedTarget>(Handler.Handle(state, unsupportedTarget, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Shift.Scheduler));
        Assert.Same(state, target.State);

        var malformed = new IntentEnvelope(
            state.ShiftId, IntentId.From("malformed"), ActorId.From("hint"), LineRepairIntentTargets.Line,
            LineRepairIntentActions.StartLineRepair, state.StateVersion, ServerTick.Zero, new ProcedureActionIntentParameters(ItemId.From("holy_water")));
        AssertRejected(Handler.Handle(state, malformed, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Shift.Scheduler), RejectionReason.MALFORMED_LINE_REPAIR_PARAMETERS, state);
    }

    [Fact]
    public void Underlying_no_jam_and_already_repairing_rejections_retain_the_exact_tlaw010_result_without_marking_an_intent()
    {
        var clear = RuntimeFixture.CreateInitialState();
        AssertUnderlyingRejected(clear, "no_jam", RejectionReason.NO_ACTIVE_JAM, LineRepairStartRejectionReason.NO_ACTIVE_JAM, ServerTick.From(10));

        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(
            FeedGateJammed(), ServerTick.From(10), Fx.Shift.Scheduler)).State;
        AssertUnderlyingRejected(repairing, "already_repairing", RejectionReason.REPAIR_ALREADY_ACTIVE, LineRepairStartRejectionReason.REPAIR_ALREADY_ACTIVE, ServerTick.From(11));
    }

    [Fact]
    public void Duplicate_is_ignored_and_the_public_non_intent_start_remains_unmarked()
    {
        var before = FeedGateJammed();
        var intent = StartIntent(before, "duplicate", before.StateVersion);
        var accepted = Assert.IsType<LineRepairIntentStarted>(Handler.Handle(
            before, intent, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Shift.Scheduler));

        var duplicateIntent = StartIntent(accepted.State, "duplicate", accepted.State.StateVersion);
        var duplicate = Assert.IsType<LineRepairIntentDuplicateIgnored>(Handler.Handle(
            accepted.State, duplicateIntent, RuntimeFixture.BoundActor, ServerTick.From(11), Fx.Shift.Scheduler));
        Assert.Same(accepted.State, duplicate.State);
        Assert.Equal(intent.IntentId, Assert.Single(duplicate.State.ProcessedIntentIds));

        var direct = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(
            FeedGateJammed(), ServerTick.From(10), Fx.Shift.Scheduler));
        Assert.Empty(direct.State.ProcessedIntentIds);
    }

    [Fact]
    public void Stored_intake_pending_target_may_move_during_repair_without_inventing_a_replacement_or_processed_id()
    {
        var before = IntakeAutoFeedJammed();
        var intent = StartIntent(before, "moving_pending", before.StateVersion);
        var started = Assert.IsType<LineRepairIntentStarted>(Handler.Handle(
            before, intent, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Shift.Scheduler));

        var pendingMoved = RuntimeFixture.MoveHost(started.State, "log_02", LogState.AT_PROCEDURE);
        var blockerCleared = RuntimeFixture.MoveHost(pendingMoved, "log_01", LogState.IN_SAW);
        var completion = Assert.IsType<LineRepairCompleted>(new LineRepairDueCompletionService().CompleteDue(blockerCleared, ServerTick.From(16)));

        Assert.Null(completion.PendingTransition);
        Assert.Equal(LineState.LINE_CLEAR, completion.State.Line.State);
        Assert.Equal(intent.IntentId, Assert.Single(completion.State.ProcessedIntentIds));
        Assert.True(completion.State.TryGetLog(LogId.From("log_02"), out var moved));
        Assert.Equal(LogState.AT_PROCEDURE, moved.State);
    }

    private static void AssertUnderlyingRejected(
        ShiftRuntimeState state,
        string intentId,
        RejectionReason reason,
        LineRepairStartRejectionReason underlyingReason,
        ServerTick tick)
    {
        var result = Assert.IsType<LineRepairIntentUnderlyingRejected>(Handler.Handle(
            state, StartIntent(state, intentId, state.StateVersion), RuntimeFixture.BoundActor, tick, Fx.Shift.Scheduler));

        Assert.Same(result.Result.State, result.State);
        Assert.Equal(reason, result.Reason);
        Assert.Equal(underlyingReason, result.Result.Reason);
        AssertUnchanged(state, result.State, result.IntentId);
    }

    private static void AssertRejected(LineRepairIntentResult result, RejectionReason reason, ShiftRuntimeState expected)
    {
        var rejected = Assert.IsType<LineRepairIntentRejected>(result);
        Assert.Equal(reason, rejected.Reason);
        AssertUnchanged(expected, rejected.State, rejected.IntentId);
    }

    private static void AssertUnchanged(ShiftRuntimeState before, ShiftRuntimeState after, IntentId intentId)
    {
        Assert.Same(before, after);
        Assert.Equal(before.StateVersion, after.StateVersion);
        Assert.DoesNotContain(intentId, after.ProcessedIntentIds);
        Assert.Same(before.Inventory, after.Inventory);
        Assert.Same(before.ProcedureProgressByLog, after.ProcedureProgressByLog);
        Assert.Same(before.ActiveProcedureHold, after.ActiveProcedureHold);
        Assert.Same(before.ActiveConfirmationTest, after.ActiveConfirmationTest);
        Assert.Same(before.ConfirmationResultsByLog, after.ConfirmationResultsByLog);
        Assert.Same(before.Line, after.Line);
        Assert.All(before.Logs, log =>
        {
            Assert.True(after.TryGetLog(log.LogId, out var unchanged));
            Assert.Same(log, unchanged);
        });
    }

    private static IntentEnvelope StartIntent(ShiftRuntimeState state, string intentId, StateVersion expected) => new(
        state.ShiftId, IntentId.From(intentId), ActorId.From("untrusted_hint"), LineRepairIntentTargets.Line,
        LineRepairIntentActions.StartLineRepair, expected, ServerTick.Zero, NoIntentParameters.Instance);

    private static ShiftRuntimeState FeedGateJammed()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
        return Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.FEED_GATE_BLOCKED, ServerTick.From(10))).State;
    }

    private static ShiftRuntimeState IntakeAutoFeedJammed()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveToIntake(state, "log_02");
        return Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.INTAKE_AUTOFEED_BLOCKED, ServerTick.From(10))).State;
    }
}
