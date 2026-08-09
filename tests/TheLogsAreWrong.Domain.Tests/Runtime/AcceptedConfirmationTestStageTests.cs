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

[Trait("Scope", "TLAW-039")]
public sealed class AcceptedConfirmationTestStageTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();

    [Fact]
    public void Stage_two_dispatches_confirmation_start_once_with_exact_result_active_tools_and_retained_noise()
    {
        var state = AtIntake("log_03");
        var retained = LineNoiseRuntimeState.Create(state.ShiftId);
        var receipt = Receipt(StartIntent(state, "start", "log_03", state.StateVersion), 0);

        var execution = Execute(state, retained, Tools("sound_meter"), receipt);

        var step = Assert.Single(execution.Steps);
        Assert.Same(state, step.BeforeState);
        Assert.Same(step.Outcome.State, step.AfterState);
        var outcome = Assert.IsType<ConfirmationTestIntentStageOutcome>(step.Outcome);
        var started = Assert.IsType<ConfirmationTestIntentStarted>(outcome.Result);
        Assert.Same(started.Result.State, execution.FinalState);
        Assert.Contains(receipt.Envelope.IntentId, execution.FinalState.ProcessedIntentIds);
        Assert.Equal(ServerTick.From(14), execution.FinalState.ActiveConfirmationTest!.DueAt);
    }

    [Fact]
    public void Ordered_confirmation_start_then_exact_new_version_route_clears_the_active_test_in_the_existing_route_mutation()
    {
        var initial = AtIntake("log_03");
        var start = StartIntent(initial, "start", "log_03", initial.StateVersion);
        var route = new IntentEnvelope(initial.ShiftId, IntentId.From("route"), ActorId.From("hint"), TargetId.From("log_03"),
            LogIntentActions.RouteToProcedure, initial.StateVersion.Next(), ServerTick.Zero, NoIntentParameters.Instance);

        var execution = Execute(initial, LineNoiseRuntimeState.Create(initial.ShiftId), Tools("sound_meter"), Receipt(start, 0), Receipt(route, 1));

        Assert.IsType<ConfirmationTestIntentStarted>(Assert.IsType<ConfirmationTestIntentStageOutcome>(execution.Steps[0].Outcome).Result);
        var routed = Assert.IsType<ManualLogIntentAccepted>(Assert.IsType<ManualRoutingIntentStageOutcome>(execution.Steps[1].Outcome).Result);
        Assert.Same(execution.Steps[0].AfterState, execution.Steps[1].BeforeState);
        Assert.Null(routed.State.ActiveConfirmationTest);
        Assert.Equal(initial.StateVersion.Next().Next(), execution.FinalState.StateVersion);
        Assert.Contains(IntentId.From("start"), execution.FinalState.ProcessedIntentIds);
        Assert.Contains(IntentId.From("route"), execution.FinalState.ProcessedIntentIds);
    }

    [Fact]
    public void Accepted_confirmation_start_then_old_version_receipt_is_stale_and_a_rejection_does_not_abort_later_receipts()
    {
        var state = AtIntake("log_03");
        var accepted = StartIntent(state, "accepted", "log_03", state.StateVersion);
        var stale = new IntentEnvelope(state.ShiftId, IntentId.From("stale"), ActorId.From("hint"), TargetId.From("log_03"),
            LogIntentActions.RouteToProcedure, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var ordered = Execute(state, LineNoiseRuntimeState.Create(state.ShiftId), Tools("sound_meter"), Receipt(accepted, 0), Receipt(stale, 1));

        Assert.IsType<ConfirmationTestIntentStarted>(Assert.IsType<ConfirmationTestIntentStageOutcome>(ordered.Steps[0].Outcome).Result);
        var staleResult = Assert.IsType<ManualLogIntentRejected>(Assert.IsType<ManualRoutingIntentStageOutcome>(ordered.Steps[1].Outcome).Result);
        Assert.Equal(RejectionReason.STALE_STATE_VERSION, staleResult.Reason);
        Assert.Same(ordered.Steps[0].AfterState, ordered.FinalState);

        var invalid = new IntentEnvelope(state.ShiftId, IntentId.From("invalid"), ActorId.From("hint"), TargetId.From("log_03"),
            ConfirmationIntentActions.StartConfirmationTest, state.StateVersion, ServerTick.Zero, new ProcedureActionIntentParameters(ItemId.From("holy_water")));
        var valid = StartIntent(state, "valid", "log_03", state.StateVersion);
        var recovered = Execute(state, LineNoiseRuntimeState.Create(state.ShiftId), Tools("sound_meter"), Receipt(invalid, 0), Receipt(valid, 1));

        Assert.IsType<ConfirmationTestIntentRejected>(Assert.IsType<ConfirmationTestIntentStageOutcome>(recovered.Steps[0].Outcome).Result);
        Assert.IsType<ConfirmationTestIntentStarted>(Assert.IsType<ConfirmationTestIntentStageOutcome>(recovered.Steps[1].Outcome).Result);
        Assert.Contains(IntentId.From("valid"), recovered.FinalState.ProcessedIntentIds);
        Assert.DoesNotContain(IntentId.From("invalid"), recovered.FinalState.ProcessedIntentIds);
    }

    private static AcceptedIntentStageExecution Execute(
        ShiftRuntimeState state,
        LineNoiseRuntimeState retainedNoise,
        ImmutableHashSet<ItemId> activeTools,
        params AuthoritativeAcceptedIntent[] receipts) =>
        new AcceptedIntentStageExecutor().Execute(
            state,
            AcceptedIntentTickBatchFactory.Create(state.ShiftId, ServerTick.From(10), ImmutableArray.Create(receipts)),
            Fx.Shift.Scheduler,
            activeTools,
            retainedNoise,
            Fx.Anomalies);

    private static AuthoritativeAcceptedIntent Receipt(IntentEnvelope intent, long sequence) =>
        new(intent, RuntimeFixture.BoundActor, ServerTick.From(10), ServerReceiveSequence.From(sequence));

    private static IntentEnvelope StartIntent(ShiftRuntimeState state, string intentId, string logId, StateVersion expected) => new(
        state.ShiftId, IntentId.From(intentId), ActorId.From("hint"), TargetId.From(logId), ConfirmationIntentActions.StartConfirmationTest,
        expected, ServerTick.Zero, NoIntentParameters.Instance);

    private static ShiftRuntimeState AtIntake(string logId) => RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), logId);

    private static ImmutableHashSet<ItemId> Tools(params string[] items) => items.Select(ItemId.From).ToImmutableHashSet();
}
