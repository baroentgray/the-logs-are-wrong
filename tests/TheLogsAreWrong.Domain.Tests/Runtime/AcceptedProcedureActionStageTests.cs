using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-038")]
public sealed class AcceptedProcedureActionStageTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();

    [Fact]
    public void Stage_two_dispatches_procedure_start_once_with_exact_result_and_catalog()
    {
        var state = AtProcedure("log_03");
        var receipt = Receipt(StartIntent(state, "hold", "log_03", "holy_water", state.StateVersion), 0);

        var execution = Execute(state, receipt);

        var step = Assert.Single(execution.Steps);
        Assert.Same(state, step.BeforeState);
        Assert.Same(step.Outcome.State, step.AfterState);
        var outcome = Assert.IsType<ProcedureActionIntentStageOutcome>(step.Outcome);
        var started = Assert.IsType<ProcedureActionIntentHoldStarted>(outcome.Result);
        Assert.Same(started.Result.State, execution.FinalState);
        Assert.Contains(receipt.Envelope.IntentId, execution.FinalState.ProcessedIntentIds);
    }

    [Fact]
    public void Ordered_route_then_exact_new_version_procedure_start_succeeds_without_regrouping()
    {
        var initial = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_03");
        var route = new IntentEnvelope(initial.ShiftId, IntentId.From("route"), ActorId.From("hint"), TargetId.From("log_03"),
            LogIntentActions.RouteToProcedure, initial.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var start = StartIntent(initial, "start", "log_03", "holy_water", initial.StateVersion.Next());

        var execution = Execute(initial, Receipt(route, 0), Receipt(start, 1));

        var routed = Assert.IsType<ManualRoutingIntentStageOutcome>(execution.Steps[0].Outcome);
        Assert.IsType<ManualLogIntentAccepted>(routed.Result);
        var procedure = Assert.IsType<ProcedureActionIntentStageOutcome>(execution.Steps[1].Outcome);
        Assert.IsType<ProcedureActionIntentHoldStarted>(procedure.Result);
        Assert.Same(execution.Steps[0].AfterState, execution.Steps[1].BeforeState);
        Assert.Equal(initial.StateVersion.Next().Next(), execution.FinalState.StateVersion);
        Assert.Contains(IntentId.From("route"), execution.FinalState.ProcessedIntentIds);
        Assert.Contains(IntentId.From("start"), execution.FinalState.ProcessedIntentIds);
    }

    [Fact]
    public void Accepted_start_then_old_version_receipt_is_stale_and_rejected_start_does_not_abort_later_receipt()
    {
        var state = AtProcedure("log_03");
        var accepted = StartIntent(state, "accepted", "log_03", "holy_water", state.StateVersion);
        var stale = new IntentEnvelope(state.ShiftId, IntentId.From("stale"), ActorId.From("hint"), TargetId.From("log_03"),
            LogIntentActions.WriteOff, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var ordered = Execute(state, Receipt(accepted, 0), Receipt(stale, 1));

        Assert.IsType<ProcedureActionIntentHoldStarted>(Assert.IsType<ProcedureActionIntentStageOutcome>(ordered.Steps[0].Outcome).Result);
        var staleResult = Assert.IsType<ManualLogIntentRejected>(Assert.IsType<ManualRoutingIntentStageOutcome>(ordered.Steps[1].Outcome).Result);
        Assert.Equal(TheLogsAreWrong.Domain.Events.RejectionReason.STALE_STATE_VERSION, staleResult.Reason);
        Assert.Same(ordered.Steps[0].AfterState, ordered.FinalState);

        var invalid = new IntentEnvelope(state.ShiftId, IntentId.From("invalid"), ActorId.From("hint"), TargetId.From("log_03"),
            ProcedureIntentActions.StartProcedureAction, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var valid = StartIntent(state, "valid", "log_03", "holy_water", state.StateVersion);
        var recovered = Execute(state, Receipt(invalid, 0), Receipt(valid, 1));

        Assert.IsType<ProcedureActionIntentRejected>(Assert.IsType<ProcedureActionIntentStageOutcome>(recovered.Steps[0].Outcome).Result);
        Assert.IsType<ProcedureActionIntentHoldStarted>(Assert.IsType<ProcedureActionIntentStageOutcome>(recovered.Steps[1].Outcome).Result);
        Assert.Contains(IntentId.From("valid"), recovered.FinalState.ProcessedIntentIds);
        Assert.DoesNotContain(IntentId.From("invalid"), recovered.FinalState.ProcessedIntentIds);
    }

    private static AcceptedIntentStageExecution Execute(ShiftRuntimeState state, params AuthoritativeAcceptedIntent[] receipts) =>
        new AcceptedIntentStageExecutor().Execute(
            state,
            AcceptedIntentTickBatchFactory.Create(state.ShiftId, ServerTick.From(10), ImmutableArray.Create(receipts)),
            Fx.Shift.Scheduler,
            Fx.Anomalies);

    private static AuthoritativeAcceptedIntent Receipt(IntentEnvelope intent, long sequence) =>
        new(intent, RuntimeFixture.BoundActor, ServerTick.From(10), ServerReceiveSequence.From(sequence));

    private static IntentEnvelope StartIntent(ShiftRuntimeState state, string intentId, string logId, string itemId, StateVersion expected) => new(
        state.ShiftId, IntentId.From(intentId), ActorId.From("hint"), TargetId.From(logId), ProcedureIntentActions.StartProcedureAction,
        expected, ServerTick.Zero, new ProcedureActionIntentParameters(ItemId.From(itemId)));

    private static ShiftRuntimeState AtProcedure(string logId)
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), logId);
        return RuntimeFixture.MoveHost(state, logId, TheLogsAreWrong.Domain.Enums.LogState.AT_PROCEDURE);
    }
}
