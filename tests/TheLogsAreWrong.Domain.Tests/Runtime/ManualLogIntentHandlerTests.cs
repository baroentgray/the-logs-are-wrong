using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-003")]
public sealed class ManualLogIntentHandlerTests
{
    public static TheoryData<IntentActionId, LogState, LogState> ManualActions => new()
    {
        { LogIntentActions.RouteToProcedure, LogState.AT_INTAKE, LogState.AT_PROCEDURE },
        { LogIntentActions.ReturnFromProcedure, LogState.AT_PROCEDURE, LogState.AT_INTAKE },
        { LogIntentActions.RouteToSawQueue, LogState.AT_INTAKE, LogState.QUEUED_FOR_SAW },
        { LogIntentActions.WriteOff, LogState.AT_INTAKE, LogState.HELD_WRITTEN_OFF },
        { LogIntentActions.WriteOff, LogState.AT_PROCEDURE, LogState.HELD_WRITTEN_OFF }
    };

    [Theory]
    [MemberData(nameof(ManualActions))]
    public void Each_manual_routing_action_uses_the_shared_transition_seam(IntentActionId action, LogState from, LogState to)
    {
        var state = StateWithFirstLogAt(from);
        var intent = RuntimeFixture.RoutingIntent("intent_01", "log_01", action, state);

        var result = new ManualLogIntentHandler().Handle(state, intent, RuntimeFixture.BoundActor);

        var accepted = Assert.IsType<ManualLogIntentAccepted>(result);
        Assert.Equal(LogId.From("log_01"), accepted.Transition.LogId);
        Assert.Equal(from, accepted.Transition.FromState);
        Assert.Equal(to, accepted.Transition.ToState);
        Assert.Equal(state.StateVersion, accepted.Transition.PriorStateVersion);
        Assert.Equal(state.StateVersion.Next(), accepted.Transition.CurrentStateVersion);
        Assert.Equal(RuntimeFixture.BoundActor, accepted.Transition.AuthoritativeActor);
        Assert.Equal(to, accepted.State.Logs[0].State);
        Assert.Equal(state.StateVersion.Next(), accepted.State.StateVersion);
        Assert.Contains(IntentId.From("intent_01"), accepted.State.ProcessedIntentIds);
    }

    [Fact]
    public void Guard_order_is_shift_then_binding_then_version_then_target_then_state_or_capacity()
    {
        var handler = new ManualLogIntentHandler();
        var state = RuntimeFixture.CreateInitialState();

        var wrongShift = new IntentEnvelope(
            ShiftId.From("OTHER_SHIFT"), IntentId.From("wrong_shift"), ActorId.From("hint"), TargetId.From("missing"),
            LogIntentActions.RouteToProcedure, StateVersion.From(99), ServerTick.Zero, NoIntentParameters.Instance);
        AssertRejected(handler.Handle(state, wrongShift, null), RejectionReason.SHIFT_MISMATCH, state);

        var missingActor = RuntimeFixture.RoutingIntent("missing_actor", "missing", LogIntentActions.RouteToProcedure, state);
        AssertRejected(handler.Handle(state, missingActor, null), RejectionReason.ACTOR_NOT_BOUND, state);

        var stale = new IntentEnvelope(
            state.ShiftId, IntentId.From("stale"), ActorId.From("hint"), TargetId.From("missing"),
            LogIntentActions.RouteToProcedure, StateVersion.From(1), ServerTick.Zero, NoIntentParameters.Instance);
        AssertRejected(handler.Handle(state, stale, RuntimeFixture.BoundActor), RejectionReason.STALE_STATE_VERSION, state);

        var missingTarget = RuntimeFixture.RoutingIntent("missing_target", "missing", LogIntentActions.RouteToProcedure, state);
        AssertRejected(handler.Handle(state, missingTarget, RuntimeFixture.BoundActor), RejectionReason.TARGET_NOT_FOUND, state);

        var wrongState = RuntimeFixture.RoutingIntent("wrong_state", "log_01", LogIntentActions.RouteToProcedure, state);
        AssertRejected(handler.Handle(state, wrongState, RuntimeFixture.BoundActor), RejectionReason.TARGET_NOT_IN_STATE, state);
    }

    [Fact]
    public void Capacity_rejections_are_deterministic_and_preserve_state_version_and_processed_ids()
    {
        var handler = new ManualLogIntentHandler();
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var first = handler.Handle(state, RuntimeFixture.RoutingIntent("first", "log_01", LogIntentActions.RouteToProcedure, state), RuntimeFixture.BoundActor);
        state = Assert.IsType<ManualLogIntentAccepted>(first).State;
        state = RuntimeFixture.MoveToIntake(state, "log_02");
        var before = state;

        var result = handler.Handle(state, RuntimeFixture.RoutingIntent("occupied", "log_02", LogIntentActions.RouteToProcedure, state), RuntimeFixture.BoundActor);

        AssertRejected(result, RejectionReason.TARGET_OCCUPIED, before);
        Assert.DoesNotContain(IntentId.From("occupied"), before.ProcessedIntentIds);
    }

    [Fact]
    public void Actor_hint_never_controls_authority_or_impersonates_the_bound_actor()
    {
        var handler = new ManualLogIntentHandler();
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var intent = RuntimeFixture.RoutingIntent("actor_test", "log_01", LogIntentActions.RouteToProcedure, state, "attacker_claim");

        var result = Assert.IsType<ManualLogIntentAccepted>(handler.Handle(state, intent, RuntimeFixture.BoundActor));

        Assert.Equal(RuntimeFixture.BoundActor, result.Transition.AuthoritativeActor);
        Assert.NotEqual(intent.ActorIdHint, result.Transition.AuthoritativeActor);
    }

    [Fact]
    public void Accepted_intent_increments_once_while_rejection_and_duplicate_no_op_do_not_increment()
    {
        var handler = new ManualLogIntentHandler();
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var accepted = Assert.IsType<ManualLogIntentAccepted>(handler.Handle(
            state,
            RuntimeFixture.RoutingIntent("once", "log_01", LogIntentActions.RouteToProcedure, state),
            RuntimeFixture.BoundActor));

        var afterAccepted = accepted.State;
        Assert.Equal(StateVersion.From(3), afterAccepted.StateVersion);

        var duplicate = RuntimeFixture.RoutingIntent("once", "log_01", LogIntentActions.ReturnFromProcedure, afterAccepted);
        var duplicateResult = Assert.IsType<DuplicateIntentIgnored>(handler.Handle(afterAccepted, duplicate, RuntimeFixture.BoundActor));
        Assert.Same(afterAccepted, duplicateResult.State);
        Assert.Equal(afterAccepted.StateVersion, duplicateResult.State.StateVersion);

        var rejected = handler.Handle(afterAccepted, RuntimeFixture.RoutingIntent("wrong_state", "log_01", LogIntentActions.RouteToSawQueue, afterAccepted), RuntimeFixture.BoundActor);
        AssertRejected(rejected, RejectionReason.TARGET_NOT_IN_STATE, afterAccepted);
    }

    [Fact]
    public void Conflicting_same_version_intents_applied_in_receive_order_accept_once_then_reject_stale()
    {
        var handler = new ManualLogIntentHandler();
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var expected = state.StateVersion;
        var first = new IntentEnvelope(state.ShiftId, IntentId.From("first"), ActorId.From("hint_a"), TargetId.From("log_01"), LogIntentActions.RouteToProcedure, expected, ServerTick.Zero, NoIntentParameters.Instance);
        var second = new IntentEnvelope(state.ShiftId, IntentId.From("second"), ActorId.From("hint_b"), TargetId.From("log_01"), LogIntentActions.RouteToSawQueue, expected, ServerTick.Zero, NoIntentParameters.Instance);

        var firstResult = Assert.IsType<ManualLogIntentAccepted>(handler.Handle(state, first, RuntimeFixture.BoundActor));
        var secondResult = handler.Handle(firstResult.State, second, RuntimeFixture.BoundActor);

        AssertRejected(secondResult, RejectionReason.STALE_STATE_VERSION, firstResult.State);
    }

    [Fact]
    public void Unknown_action_fails_closed_without_extending_frozen_rejection_reasons()
    {
        var handler = new ManualLogIntentHandler();
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var intent = RuntimeFixture.RoutingIntent("unknown", "log_01", IntentActionId.From("invent_action"), state);

        var result = Assert.IsType<UnsupportedIntentAction>(handler.Handle(state, intent, RuntimeFixture.BoundActor));

        Assert.Equal(IntentActionId.From("invent_action"), result.Action);
        Assert.Same(state, result.State);
        Assert.Equal(state.StateVersion, result.State.StateVersion);
        Assert.Empty(state.ProcessedIntentIds);
    }

    [Fact]
    public void Saw_queue_manual_routes_are_point_of_no_return_and_write_off_is_irreversible()
    {
        var handler = new ManualLogIntentHandler();
        var queued = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        queued = RuntimeFixture.MoveHost(queued, "log_01", LogState.QUEUED_FOR_SAW);

        AssertRejected(handler.Handle(queued, RuntimeFixture.RoutingIntent("queue_proc", "log_01", LogIntentActions.RouteToProcedure, queued), RuntimeFixture.BoundActor), RejectionReason.TARGET_NOT_IN_STATE, queued);
        AssertRejected(handler.Handle(queued, RuntimeFixture.RoutingIntent("queue_write", "log_01", LogIntentActions.WriteOff, queued), RuntimeFixture.BoundActor), RejectionReason.TARGET_NOT_IN_STATE, queued);

        var writtenOff = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var wroteOff = Assert.IsType<ManualLogIntentAccepted>(handler.Handle(writtenOff, RuntimeFixture.RoutingIntent("write", "log_01", LogIntentActions.WriteOff, writtenOff), RuntimeFixture.BoundActor));
        AssertRejected(handler.Handle(wroteOff.State, RuntimeFixture.RoutingIntent("terminal", "log_01", LogIntentActions.RouteToProcedure, wroteOff.State), RuntimeFixture.BoundActor), RejectionReason.TARGET_NOT_IN_STATE, wroteOff.State);
    }

    private static void AssertRejected(ManualLogIntentResult result, RejectionReason reason, ShiftRuntimeState expectedState)
    {
        var rejected = Assert.IsType<ManualLogIntentRejected>(result);
        Assert.Equal(reason, rejected.Reason);
        Assert.Same(expectedState, rejected.State);
        Assert.Equal(expectedState.StateVersion, rejected.State.StateVersion);
    }

    private static ShiftRuntimeState StateWithFirstLogAt(LogState state)
    {
        var runtime = RuntimeFixture.CreateInitialState();
        return state switch
        {
            LogState.AT_INTAKE => RuntimeFixture.MoveToIntake(runtime, "log_01"),
            LogState.AT_PROCEDURE => RuntimeFixture.MoveHost(RuntimeFixture.MoveToIntake(runtime, "log_01"), "log_01", LogState.AT_PROCEDURE),
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
    }
}
