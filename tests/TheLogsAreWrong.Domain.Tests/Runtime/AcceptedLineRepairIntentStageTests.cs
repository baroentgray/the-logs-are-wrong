using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-040")]
public sealed class AcceptedLineRepairIntentStageTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();

    [Fact]
    public void Stage_two_dispatches_line_repair_once_and_feeds_the_exact_returned_state_into_the_next_receipt()
    {
        var initial = FeedGateJammed();
        var start = StartIntent(initial, "start", initial.StateVersion);
        var route = new IntentEnvelope(
            initial.ShiftId, IntentId.From("route"), ActorId.From("hint"), TargetId.From("log_01"),
            LogIntentActions.RouteToProcedure, initial.StateVersion.Next(), ServerTick.Zero, NoIntentParameters.Instance);

        var execution = Execute(initial, Receipt(start, 0), Receipt(route, 1));

        var started = Assert.IsType<LineRepairIntentStarted>(Assert.IsType<LineRepairIntentStageOutcome>(execution.Steps[0].Outcome).Result);
        var routed = Assert.IsType<ManualLogIntentAccepted>(Assert.IsType<ManualRoutingIntentStageOutcome>(execution.Steps[1].Outcome).Result);
        Assert.Same(initial, execution.Steps[0].BeforeState);
        Assert.Same(started.State, execution.Steps[1].BeforeState);
        Assert.Same(routed.State, execution.FinalState);
        Assert.Equal(initial.StateVersion.Next().Next(), execution.FinalState.StateVersion);
        Assert.Equal(LineState.REPAIRING, execution.FinalState.Line.State);
        Assert.Contains(start.IntentId, execution.FinalState.ProcessedIntentIds);
        Assert.Contains(route.IntentId, execution.FinalState.ProcessedIntentIds);
    }

    [Fact]
    public void Accepted_repair_start_followed_by_an_old_version_receipt_is_stale()
    {
        var initial = FeedGateJammed();
        var start = StartIntent(initial, "start", initial.StateVersion);
        var stale = new IntentEnvelope(
            initial.ShiftId, IntentId.From("stale"), ActorId.From("hint"), TargetId.From("log_01"),
            LogIntentActions.RouteToProcedure, initial.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);

        var execution = Execute(initial, Receipt(start, 0), Receipt(stale, 1));

        Assert.IsType<LineRepairIntentStarted>(Assert.IsType<LineRepairIntentStageOutcome>(execution.Steps[0].Outcome).Result);
        var rejected = Assert.IsType<ManualLogIntentRejected>(Assert.IsType<ManualRoutingIntentStageOutcome>(execution.Steps[1].Outcome).Result);
        Assert.Equal(RejectionReason.STALE_STATE_VERSION, rejected.Reason);
        Assert.Same(execution.Steps[0].AfterState, execution.FinalState);
        Assert.DoesNotContain(stale.IntentId, execution.FinalState.ProcessedIntentIds);
    }

    [Fact]
    public void A_repair_rejection_does_not_abort_a_later_valid_receipt()
    {
        var initial = FeedGateJammed();
        var malformed = new IntentEnvelope(
            initial.ShiftId, IntentId.From("malformed"), ActorId.From("hint"), LineRepairIntentTargets.Line,
            LineRepairIntentActions.StartLineRepair, initial.StateVersion, ServerTick.Zero, new ProcedureActionIntentParameters(ItemId.From("holy_water")));
        var valid = StartIntent(initial, "valid", initial.StateVersion);

        var execution = Execute(initial, Receipt(malformed, 0), Receipt(valid, 1));

        var rejected = Assert.IsType<LineRepairIntentRejected>(Assert.IsType<LineRepairIntentStageOutcome>(execution.Steps[0].Outcome).Result);
        Assert.Equal(RejectionReason.MALFORMED_LINE_REPAIR_PARAMETERS, rejected.Reason);
        var started = Assert.IsType<LineRepairIntentStarted>(Assert.IsType<LineRepairIntentStageOutcome>(execution.Steps[1].Outcome).Result);
        Assert.Same(initial, execution.Steps[1].BeforeState);
        Assert.Same(started.State, execution.FinalState);
        Assert.DoesNotContain(malformed.IntentId, execution.FinalState.ProcessedIntentIds);
        Assert.Contains(valid.IntentId, execution.FinalState.ProcessedIntentIds);
    }

    private static AcceptedIntentStageExecution Execute(ShiftRuntimeState state, params AuthoritativeAcceptedIntent[] receipts) =>
        new AcceptedIntentStageExecutor().Execute(
            state,
            AcceptedIntentTickBatchFactory.Create(state.ShiftId, ServerTick.From(10), ImmutableArray.Create(receipts)),
            Fx.Shift.Scheduler,
            ImmutableHashSet<ItemId>.Empty,
            LineNoiseRuntimeState.Create(state.ShiftId),
            Fx.Anomalies,
            Fx.Shift.Containment);

    private static AuthoritativeAcceptedIntent Receipt(IntentEnvelope envelope, long sequence) =>
        new(envelope, RuntimeFixture.BoundActor, ServerTick.From(10), ServerReceiveSequence.From(sequence));

    private static IntentEnvelope StartIntent(ShiftRuntimeState state, string intentId, StateVersion expected) => new(
        state.ShiftId, IntentId.From(intentId), ActorId.From("hint"), LineRepairIntentTargets.Line,
        LineRepairIntentActions.StartLineRepair, expected, ServerTick.Zero, NoIntentParameters.Instance);

    private static ShiftRuntimeState FeedGateJammed()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
        return Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.FEED_GATE_BLOCKED, ServerTick.From(10))).State;
    }
}
