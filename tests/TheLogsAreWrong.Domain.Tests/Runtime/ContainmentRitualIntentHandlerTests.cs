using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-041")]
public sealed class ContainmentRitualIntentHandlerTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static readonly ContainmentRitualIntentHandler Handler = new();

    [Fact]
    public void Service_requested_start_retains_the_exact_tlaw009_result_and_atomically_marks_the_accepted_intent()
    {
        var before = Requested();
        var intent = StartIntent(before, "request_start", before.StateVersion);

        var result = Assert.IsType<ContainmentRitualIntentStarted>(Handler.Handle(
            before, intent, RuntimeFixture.BoundActor, ServerTick.From(110), Fx.Shift.Containment));

        Assert.Same(result.Result.State, result.State);
        Assert.Same(before.Containment, result.State.Containment);
        Assert.Same(result.Result.Ritual, result.State.ActiveContainmentRitual);
        Assert.Equal(ContainmentState.SERVICE_REQUESTED, result.State.Containment.State);
        Assert.Equal(before.StateVersion.Next(), result.State.StateVersion);
        Assert.Equal(intent.IntentId, Assert.Single(result.State.ProcessedIntentIds));
        Assert.Equal(ServerTick.From(110), result.Result.Ritual.StartedAt);
        Assert.Equal(ServerTick.From(114), result.Result.Ritual.DueAt);
        Assert.Equal(SimulationDuration.FromTicks(4), result.Result.Ritual.Duration);
        AssertUnrelatedRuntimeUnchanged(before, result.State);
    }

    [Fact]
    public void Overdue_and_incident_starts_preserve_the_exact_existing_containment_shape()
    {
        var overdue = Overdue();
        var overdueResult = Assert.IsType<ContainmentRitualIntentStarted>(Handler.Handle(
            overdue, StartIntent(overdue, "overdue_start", overdue.StateVersion), RuntimeFixture.BoundActor, ServerTick.From(121), Fx.Shift.Containment));
        Assert.Same(overdue.Containment, overdueResult.State.Containment);
        Assert.Equal(ContainmentState.OVERDUE, overdueResult.State.Containment.State);
        Assert.Equal(ServerTick.From(130), overdueResult.State.Containment.DeadlineAt);

        var incident = Incident();
        var incidentResult = Assert.IsType<ContainmentRitualIntentStarted>(Handler.Handle(
            incident, StartIntent(incident, "incident_start", incident.StateVersion), RuntimeFixture.BoundActor, ServerTick.From(131), Fx.Shift.Containment));
        Assert.Same(incident.Containment, incidentResult.State.Containment);
        Assert.Equal(ContainmentState.INCIDENT, incidentResult.State.Containment.State);
        Assert.Null(incidentResult.State.Containment.DeadlineAt);
        Assert.Equal(ServerTick.From(135), incidentResult.Result.Ritual.DueAt);
    }

    [Fact]
    public void Guard_order_and_closed_unsupported_shapes_leave_the_exact_input_unmarked()
    {
        var state = Requested();
        var wrongShift = new IntentEnvelope(
            ShiftId.From("OTHER_SHIFT"), IntentId.From("wrong_shift"), ActorId.From("hint"), TargetId.From("OTHER"),
            IntentActionId.From("unsupported"), StateVersion.From(99), ServerTick.Zero, new ProcedureActionIntentParameters(ItemId.From("holy_water")));
        AssertRejected(Handler.Handle(state, wrongShift, null, ServerTick.From(110), Fx.Shift.Containment), RejectionReason.SHIFT_MISMATCH, state);

        var missingActor = StartIntent(state, "missing_actor", state.StateVersion);
        AssertRejected(Handler.Handle(state, missingActor, null, ServerTick.From(110), Fx.Shift.Containment), RejectionReason.ACTOR_NOT_BOUND, state);

        var stale = StartIntent(state, "stale", state.StateVersion.Next());
        AssertRejected(Handler.Handle(state, stale, RuntimeFixture.BoundActor, ServerTick.From(110), Fx.Shift.Containment), RejectionReason.STALE_STATE_VERSION, state);

        var unsupportedAction = new IntentEnvelope(
            state.ShiftId, IntentId.From("unsupported_action"), ActorId.From("hint"), ContainmentRitualIntentTargets.Containment,
            IntentActionId.From("unowned"), state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var action = Assert.IsType<ContainmentRitualIntentUnsupportedAction>(Handler.Handle(state, unsupportedAction, RuntimeFixture.BoundActor, ServerTick.From(110), Fx.Shift.Containment));
        Assert.Same(state, action.State);
        Assert.DoesNotContain(unsupportedAction.IntentId, action.State.ProcessedIntentIds);

        var unsupportedTarget = new IntentEnvelope(
            state.ShiftId, IntentId.From("unsupported_target"), ActorId.From("hint"), TargetId.From("OTHER"),
            ContainmentRitualIntentActions.StartContainmentRitual, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var target = Assert.IsType<ContainmentRitualIntentUnsupportedTarget>(Handler.Handle(state, unsupportedTarget, RuntimeFixture.BoundActor, ServerTick.From(110), Fx.Shift.Containment));
        Assert.Same(state, target.State);
        Assert.DoesNotContain(unsupportedTarget.IntentId, target.State.ProcessedIntentIds);

        var malformed = new IntentEnvelope(
            state.ShiftId, IntentId.From("malformed"), ActorId.From("hint"), ContainmentRitualIntentTargets.Containment,
            ContainmentRitualIntentActions.StartContainmentRitual, state.StateVersion, ServerTick.Zero, new ProcedureActionIntentParameters(ItemId.From("holy_water")));
        AssertRejected(Handler.Handle(state, malformed, RuntimeFixture.BoundActor, ServerTick.From(110), Fx.Shift.Containment), RejectionReason.MALFORMED_CONTAINMENT_RITUAL_PARAMETERS, state);
    }

    [Fact]
    public void Stable_and_already_active_rejections_retain_the_exact_tlaw009_result_without_marking_an_intent()
    {
        var stable = RuntimeFixture.CreateInitialState();
        AssertUnderlyingRejected(stable, "stable", RejectionReason.NO_ACTIVE_REQUEST, ContainmentRitualStartRejectionReason.NO_ACTIVE_REQUEST, ServerTick.From(10));

        var active = Assert.IsType<ContainmentRitualStarted>(new ContainmentRitualStartService().Start(
            Requested(), ServerTick.From(110), Fx.Shift.Containment)).State;
        AssertUnderlyingRejected(active, "active", RejectionReason.RITUAL_ALREADY_ACTIVE, ContainmentRitualStartRejectionReason.RITUAL_ALREADY_ACTIVE, ServerTick.From(111));
    }

    [Fact]
    public void Duplicate_and_non_intent_start_completion_and_cancellation_never_invent_or_readd_processed_ids()
    {
        var before = Requested();
        var intent = StartIntent(before, "accepted", before.StateVersion);
        var accepted = Assert.IsType<ContainmentRitualIntentStarted>(Handler.Handle(
            before, intent, RuntimeFixture.BoundActor, ServerTick.From(110), Fx.Shift.Containment));

        var duplicate = Assert.IsType<ContainmentRitualIntentDuplicateIgnored>(Handler.Handle(
            accepted.State, StartIntent(accepted.State, "accepted", accepted.State.StateVersion), RuntimeFixture.BoundActor, ServerTick.From(111), Fx.Shift.Containment));
        Assert.Same(accepted.State, duplicate.State);
        Assert.Equal(intent.IntentId, Assert.Single(duplicate.State.ProcessedIntentIds));

        var direct = Assert.IsType<ContainmentRitualStarted>(new ContainmentRitualStartService().Start(
            Requested(), ServerTick.From(110), Fx.Shift.Containment));
        Assert.Empty(direct.State.ProcessedIntentIds);

        var completed = Assert.IsType<ContainmentRitualCompleted>(new ContainmentRitualCompletionService().CompleteDue(
            accepted.State, ServerTick.From(114), Fx.Shift.Containment, Fx.Anomalies));
        Assert.Equal(intent.IntentId, Assert.Single(completed.State.ProcessedIntentIds));

        var cancelled = Assert.IsType<ContainmentRitualCancelled>(new ContainmentRitualCancellationService().Cancel(accepted.State));
        Assert.Equal(intent.IntentId, Assert.Single(cancelled.State.ProcessedIntentIds));
    }

    private static void AssertUnderlyingRejected(
        ShiftRuntimeState state,
        string intentId,
        RejectionReason reason,
        ContainmentRitualStartRejectionReason underlyingReason,
        ServerTick tick)
    {
        var result = Assert.IsType<ContainmentRitualIntentUnderlyingRejected>(Handler.Handle(
            state, StartIntent(state, intentId, state.StateVersion), RuntimeFixture.BoundActor, tick, Fx.Shift.Containment));

        Assert.Same(result.Result.State, result.State);
        Assert.Equal(reason, result.Reason);
        Assert.Equal(underlyingReason, result.Result.Reason);
        AssertUnchanged(state, result.State, result.IntentId);
    }

    private static void AssertRejected(ContainmentRitualIntentResult result, RejectionReason reason, ShiftRuntimeState expected)
    {
        var rejected = Assert.IsType<ContainmentRitualIntentRejected>(result);
        Assert.Equal(reason, rejected.Reason);
        AssertUnchanged(expected, rejected.State, rejected.IntentId);
    }

    private static void AssertUnchanged(ShiftRuntimeState before, ShiftRuntimeState after, IntentId intentId)
    {
        Assert.Same(before, after);
        Assert.Equal(before.StateVersion, after.StateVersion);
        Assert.DoesNotContain(intentId, after.ProcessedIntentIds);
        AssertUnrelatedRuntimeUnchanged(before, after);
        Assert.Same(before.Containment, after.Containment);
        Assert.Same(before.ActiveContainmentRitual, after.ActiveContainmentRitual);
    }

    private static void AssertUnrelatedRuntimeUnchanged(ShiftRuntimeState before, ShiftRuntimeState after)
    {
        Assert.Same(before.Inventory, after.Inventory);
        Assert.Same(before.ProcedureProgressByLog, after.ProcedureProgressByLog);
        Assert.Same(before.ActiveProcedureHold, after.ActiveProcedureHold);
        Assert.Same(before.ActiveConfirmationTest, after.ActiveConfirmationTest);
        Assert.Same(before.ConfirmationResultsByLog, after.ConfirmationResultsByLog);
        Assert.Same(before.Line, after.Line);
        Assert.Same(before.ActiveIntakeDeadline, after.ActiveIntakeDeadline);
        Assert.Same(before.ActiveSawCycle, after.ActiveSawCycle);
        Assert.Equal(before.PendingFeed, after.PendingFeed);
        Assert.All(before.Logs, log =>
        {
            Assert.True(after.TryGetLog(log.LogId, out var unchanged));
            Assert.Same(log, unchanged);
        });
    }

    private static IntentEnvelope StartIntent(ShiftRuntimeState state, string intentId, StateVersion expected) => new(
        state.ShiftId, IntentId.From(intentId), ActorId.From("untrusted_hint"), ContainmentRitualIntentTargets.Containment,
        ContainmentRitualIntentActions.StartContainmentRitual, expected, ServerTick.Zero, NoIntentParameters.Instance);

    private static ShiftRuntimeState Requested()
    {
        var writtenOff = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_03");
        writtenOff = RuntimeFixture.MoveHost(writtenOff, "log_03", LogState.HELD_WRITTEN_OFF);
        var armed = Assert.IsType<ContainmentStableIntervalArmed>(new ContainmentAdvanceService().Advance(
            writtenOff, ServerTick.From(10), Fx.Shift.Containment, Fx.Anomalies)).State;
        return Assert.IsType<ContainmentStateAdvanced>(new ContainmentAdvanceService().Advance(
            armed, ServerTick.From(100), Fx.Shift.Containment, Fx.Anomalies)).State;
    }

    private static ShiftRuntimeState Overdue() => Assert.IsType<ContainmentStateAdvanced>(new ContainmentAdvanceService().Advance(
        Requested(), ServerTick.From(120), Fx.Shift.Containment, Fx.Anomalies)).State;

    private static ShiftRuntimeState Incident() => Assert.IsType<ContainmentIncidentEntered>(new ContainmentAdvanceService().Advance(
        Overdue(), ServerTick.From(130), Fx.Shift.Containment, Fx.Anomalies)).State;
}
