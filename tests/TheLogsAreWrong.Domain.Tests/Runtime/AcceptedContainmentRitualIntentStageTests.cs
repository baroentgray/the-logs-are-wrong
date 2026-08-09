using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Sequencing;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-041")]
public sealed class AcceptedContainmentRitualIntentStageTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();

    [Fact]
    public void Stage_two_dispatches_containment_ritual_once_and_feeds_the_exact_returned_state_into_the_next_receipt()
    {
        var initial = RuntimeFixture.MoveToIntake(Requested(), "log_01");
        var start = StartIntent(initial, "start", initial.StateVersion);
        var route = new IntentEnvelope(
            initial.ShiftId, IntentId.From("route"), ActorId.From("hint"), TargetId.From("log_01"),
            LogIntentActions.RouteToProcedure, initial.StateVersion.Next(), ServerTick.Zero, NoIntentParameters.Instance);

        var execution = Execute(initial, Receipt(start, 0), Receipt(route, 1));

        var started = Assert.IsType<ContainmentRitualIntentStarted>(Assert.IsType<ContainmentRitualIntentStageOutcome>(execution.Steps[0].Outcome).Result);
        var routed = Assert.IsType<ManualLogIntentAccepted>(Assert.IsType<ManualRoutingIntentStageOutcome>(execution.Steps[1].Outcome).Result);
        Assert.Same(initial, execution.Steps[0].BeforeState);
        Assert.Same(started.State, execution.Steps[1].BeforeState);
        Assert.Same(routed.State, execution.FinalState);
        Assert.Equal(initial.StateVersion.Next().Next(), execution.FinalState.StateVersion);
        Assert.Same(initial.Containment, execution.FinalState.Containment);
        Assert.NotNull(execution.FinalState.ActiveContainmentRitual);
        Assert.Contains(start.IntentId, execution.FinalState.ProcessedIntentIds);
        Assert.Contains(route.IntentId, execution.FinalState.ProcessedIntentIds);
    }

    [Fact]
    public void Accepted_ritual_start_followed_by_an_old_version_receipt_is_stale()
    {
        var initial = RuntimeFixture.MoveToIntake(Requested(), "log_01");
        var start = StartIntent(initial, "start", initial.StateVersion);
        var stale = new IntentEnvelope(
            initial.ShiftId, IntentId.From("stale"), ActorId.From("hint"), TargetId.From("log_01"),
            LogIntentActions.RouteToProcedure, initial.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);

        var execution = Execute(initial, Receipt(start, 0), Receipt(stale, 1));

        Assert.IsType<ContainmentRitualIntentStarted>(Assert.IsType<ContainmentRitualIntentStageOutcome>(execution.Steps[0].Outcome).Result);
        var rejected = Assert.IsType<ManualLogIntentRejected>(Assert.IsType<ManualRoutingIntentStageOutcome>(execution.Steps[1].Outcome).Result);
        Assert.Equal(RejectionReason.STALE_STATE_VERSION, rejected.Reason);
        Assert.Same(execution.Steps[0].AfterState, execution.FinalState);
        Assert.DoesNotContain(stale.IntentId, execution.FinalState.ProcessedIntentIds);
    }

    [Fact]
    public void A_ritual_rejection_does_not_abort_a_later_valid_receipt()
    {
        var initial = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var rejectedStart = StartIntent(initial, "stable", initial.StateVersion);
        var validRoute = new IntentEnvelope(
            initial.ShiftId, IntentId.From("route"), ActorId.From("hint"), TargetId.From("log_01"),
            LogIntentActions.RouteToProcedure, initial.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);

        var execution = Execute(initial, Receipt(rejectedStart, 0), Receipt(validRoute, 1));

        var rejected = Assert.IsType<ContainmentRitualIntentUnderlyingRejected>(Assert.IsType<ContainmentRitualIntentStageOutcome>(execution.Steps[0].Outcome).Result);
        Assert.Equal(RejectionReason.NO_ACTIVE_REQUEST, rejected.Reason);
        var routed = Assert.IsType<ManualLogIntentAccepted>(Assert.IsType<ManualRoutingIntentStageOutcome>(execution.Steps[1].Outcome).Result);
        Assert.Same(initial, execution.Steps[1].BeforeState);
        Assert.Same(routed.State, execution.FinalState);
        Assert.DoesNotContain(rejectedStart.IntentId, execution.FinalState.ProcessedIntentIds);
        Assert.Contains(validRoute.IntentId, execution.FinalState.ProcessedIntentIds);
    }

    [Fact]
    public void Later_receipts_observe_the_evolved_state_after_a_successful_ritual_start()
    {
        var initial = Requested();
        var first = StartIntent(initial, "accepted", initial.StateVersion);
        var alreadyActive = StartIntent(initial, "active", initial.StateVersion.Next());

        var execution = Execute(initial, Receipt(first, 0), Receipt(alreadyActive, 1));

        var firstResult = Assert.IsType<ContainmentRitualIntentStarted>(Assert.IsType<ContainmentRitualIntentStageOutcome>(execution.Steps[0].Outcome).Result);
        var activeResult = Assert.IsType<ContainmentRitualIntentUnderlyingRejected>(Assert.IsType<ContainmentRitualIntentStageOutcome>(execution.Steps[1].Outcome).Result);
        Assert.Same(firstResult.State, execution.Steps[1].BeforeState);
        Assert.Equal(RejectionReason.RITUAL_ALREADY_ACTIVE, activeResult.Reason);
        Assert.Same(firstResult.State, execution.FinalState);
        Assert.Equal(first.IntentId, Assert.Single(execution.FinalState.ProcessedIntentIds));
    }

    private static AcceptedIntentStageExecution Execute(ShiftRuntimeState state, params AuthoritativeAcceptedIntent[] receipts) =>
        new AcceptedIntentStageExecutor().Execute(
            state,
            AcceptedIntentTickBatchFactory.Create(state.ShiftId, ServerTick.From(110), ImmutableArray.Create(receipts)),
            Fx.Shift.Scheduler,
            ImmutableHashSet<ItemId>.Empty,
            LineNoiseRuntimeState.Create(state.ShiftId),
            Fx.Anomalies,
            Fx.Shift.Containment);

    private static AuthoritativeAcceptedIntent Receipt(IntentEnvelope envelope, long sequence) =>
        new(envelope, RuntimeFixture.BoundActor, ServerTick.From(110), ServerReceiveSequence.From(sequence));

    private static IntentEnvelope StartIntent(ShiftRuntimeState state, string intentId, StateVersion expected) => new(
        state.ShiftId, IntentId.From(intentId), ActorId.From("hint"), ContainmentRitualIntentTargets.Containment,
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
}
