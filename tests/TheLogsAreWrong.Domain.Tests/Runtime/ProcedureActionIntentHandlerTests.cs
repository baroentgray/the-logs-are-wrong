using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-038")]
public sealed class ProcedureActionIntentHandlerTests
{
    private static readonly ProcedureActionIntentHandler Handler = new();
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();

    [Fact]
    public void Typed_start_action_parameters_retain_only_the_attempted_item()
    {
        var parameters = new ProcedureActionIntentParameters(ItemId.From("holy_water"));

        Assert.Equal(IntentActionId.From("start_procedure_action"), ProcedureIntentActions.StartProcedureAction);
        Assert.Equal(ItemId.From("holy_water"), parameters.AttemptedItem);
        Assert.IsAssignableFrom<IIntentParameters>(parameters);
        Assert.DoesNotContain(typeof(ProcedureActionIntentParameters).GetProperties(), property => property.PropertyType == typeof(LogId));
        Assert.Throws<ArgumentException>(() => new ProcedureActionIntentParameters(default));
    }

    [Fact]
    public void Penitent_hold_start_is_the_exact_tlaw007_result_and_records_the_intent_atomically()
    {
        var before = AtProcedure("log_03");
        var intent = StartIntent(before, "penitent_start", "log_03", "holy_water", before.StateVersion);

        var result = Assert.IsType<ProcedureActionIntentHoldStarted>(Handler.Handle(
            before, intent, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Anomalies));

        Assert.Same(result.Result.State, result.State);
        Assert.Equal(before.StateVersion.Next(), result.State.StateVersion);
        Assert.Contains(intent.IntentId, result.State.ProcessedIntentIds);
        Assert.Equal(2, result.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.False(result.State.TryGetProcedureProgress(LogId.From("log_03"), out _));
        Assert.Equal(ServerTick.From(13), result.Result.Hold.DueAt);
        Assert.Equal(ItemId.From("holy_water"), result.Result.Hold.AttemptedItem);
    }

    [Fact]
    public void Resin_immediate_start_is_the_exact_tlaw007_result_and_records_the_intent_atomically()
    {
        var before = AtProcedure("log_06");
        var intent = StartIntent(before, "resin_salt", "log_06", "salt", before.StateVersion);

        var result = Assert.IsType<ProcedureActionIntentCompletedImmediately>(Handler.Handle(
            before, intent, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Anomalies));

        Assert.Same(result.Result.State, result.State);
        Assert.Equal(before.StateVersion.Next(), result.State.StateVersion);
        Assert.Contains(intent.IntentId, result.State.ProcessedIntentIds);
        Assert.Equal(ItemActionCompletionKind.CorrectProcedureStep, result.Result.Descriptor.Kind);
        Assert.Equal(1, result.State.Inventory.GetConsumableQuantity(ItemId.From("salt")));
        Assert.Null(result.State.ActiveProcedureHold);
    }

    [Fact]
    public void Guard_order_and_rejections_preserve_the_exact_state_without_marking_the_intent()
    {
        var state = AtProcedure("log_03");

        var wrongShift = new IntentEnvelope(
            ShiftId.From("OTHER_SHIFT"), IntentId.From("wrong_shift"), ActorId.From("hint"), TargetId.From("missing"),
            ProcedureIntentActions.StartProcedureAction, StateVersion.From(99), ServerTick.Zero,
            new ProcedureActionIntentParameters(ItemId.From("holy_water")));
        AssertRejected(Handler.Handle(state, wrongShift, null, ServerTick.From(10), Fx.Anomalies), RejectionReason.SHIFT_MISMATCH, state);

        var missingActor = StartIntent(state, "missing_actor", "log_03", "holy_water", state.StateVersion);
        AssertRejected(Handler.Handle(state, missingActor, null, ServerTick.From(10), Fx.Anomalies), RejectionReason.ACTOR_NOT_BOUND, state);

        var stale = StartIntent(state, "stale", "log_03", "holy_water", state.StateVersion.Next());
        AssertRejected(Handler.Handle(state, stale, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Anomalies), RejectionReason.STALE_STATE_VERSION, state);

        var unsupported = new IntentEnvelope(
            state.ShiftId, IntentId.From("unsupported"), ActorId.From("hint"), TargetId.From("log_03"),
            LogIntentActions.RouteToProcedure, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var unowned = Assert.IsType<ProcedureActionIntentUnsupported>(Handler.Handle(state, unsupported, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Anomalies));
        Assert.Same(state, unowned.State);

        var malformed = new IntentEnvelope(
            state.ShiftId, IntentId.From("malformed"), ActorId.From("hint"), TargetId.From("log_03"),
            ProcedureIntentActions.StartProcedureAction, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        AssertRejected(Handler.Handle(state, malformed, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Anomalies), RejectionReason.MALFORMED_PROCEDURE_PARAMETERS, state);

        var missingTarget = StartIntent(state, "missing_target", "missing", "holy_water", state.StateVersion);
        AssertRejected(Handler.Handle(state, missingTarget, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Anomalies), RejectionReason.TARGET_NOT_FOUND, state);
    }

    [Fact]
    public void Procedure_specific_rejections_retain_the_underlying_tlaw007_result_and_do_not_mark_the_intent()
    {
        var normal = AtProcedure("log_01");
        AssertUnderlyingRejected(normal, "no_plan", "log_01", "holy_water", RejectionReason.PROCEDURE_NO_PLAN, ProcedureActionStartRejectionReason.NoProcedurePlan);

        var resin = AtProcedure("log_06");
        AssertUnderlyingRejected(resin, "out_of_order", "log_06", "red_tape", RejectionReason.PROCEDURE_OUT_OF_ORDER_ITEM, ProcedureActionStartRejectionReason.OutOfOrderItem);
        AssertUnderlyingRejected(resin, "unconfigured", "log_06", "hamster_statue", RejectionReason.PROCEDURE_UNCONFIGURED_ITEM, ProcedureActionStartRejectionReason.UnconfiguredWrongAction);

        var held = Assert.IsType<ProcedureActionHoldStarted>(new ProcedureActionStartService().Start(
            AtProcedure("log_03"), LogId.From("log_03"), ItemId.From("holy_water"), ServerTick.From(10), Fx.Anomalies)).State;
        AssertUnderlyingRejected(held, "active_hold", "log_03", "holy_water", RejectionReason.PROCEDURE_HOLD_ACTIVE, ProcedureActionStartRejectionReason.ActiveHoldAlreadyExists);

        var completed = Assert.IsType<ProcedureActionCompletedImmediately>(new ProcedureActionStartService().Start(
            AtProcedure("log_05"), LogId.From("log_05"), ItemId.From("relabel_stamp"), ServerTick.From(10), Fx.Anomalies)).State;
        AssertUnderlyingRejected(completed, "repeated", "log_05", "relabel_stamp", RejectionReason.PROCEDURE_REPEATED_STEP, ProcedureActionStartRejectionReason.RepeatedCorrectStep);
    }

    [Fact]
    public void Existing_target_outside_procedure_retains_the_exact_underlying_rejection_without_any_mutation()
    {
        var state = RuntimeFixture.CreateInitialState();
        var intent = StartIntent(state, "not_at_procedure", "log_03", "holy_water", state.StateVersion);

        var result = Assert.IsType<ProcedureActionIntentUnderlyingRejected>(Handler.Handle(
            state, intent, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Anomalies));

        Assert.Equal(RejectionReason.TARGET_NOT_IN_STATE, result.Reason);
        Assert.Equal(ProcedureActionStartRejectionReason.TargetNotAtProcedure, result.Result.Reason);
        AssertUnchangedProcedureRejection(state, result, intent.IntentId, LogId.From("log_03"));
    }

    [Fact]
    public void Missing_required_item_retains_the_exact_underlying_rejection_without_any_mutation()
    {
        var state = AtProcedure("log_06");
        var intent = StartIntent(state, "missing_item", "log_06", "SALT", state.StateVersion);

        var result = Assert.IsType<ProcedureActionIntentUnderlyingRejected>(Handler.Handle(
            state, intent, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Anomalies));

        Assert.Equal(RejectionReason.MISSING_ITEM, result.Reason);
        Assert.Equal(ProcedureActionStartRejectionReason.MissingItem, result.Result.Reason);
        AssertUnchangedProcedureRejection(state, result, intent.IntentId, LogId.From("log_06"));
    }

    [Fact]
    public void Existing_non_intent_start_and_due_completion_remain_unmarked()
    {
        var before = AtProcedure("log_03");
        var started = Assert.IsType<ProcedureActionHoldStarted>(new ProcedureActionStartService().Start(
            before, LogId.From("log_03"), ItemId.From("holy_water"), ServerTick.From(10), Fx.Anomalies));
        var completed = Assert.IsType<ProcedureActionDueCompleted>(new ProcedureActionDueCompletionService().CompleteDue(
            started.State, ServerTick.From(13), Fx.Anomalies));

        Assert.Empty(started.State.ProcessedIntentIds);
        Assert.Empty(completed.State.ProcessedIntentIds);
        Assert.Equal(started.State.StateVersion.Next(), completed.State.StateVersion);
    }

    [Fact]
    public void Duplicate_procedure_intent_is_ignored_without_another_state_or_processed_intent_mutation()
    {
        var before = AtProcedure("log_03");
        var first = StartIntent(before, "duplicate", "log_03", "holy_water", before.StateVersion);
        var accepted = Assert.IsType<ProcedureActionIntentHoldStarted>(Handler.Handle(
            before, first, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Anomalies));
        var duplicate = StartIntent(accepted.State, "duplicate", "log_03", "holy_water", accepted.State.StateVersion);

        var ignored = Assert.IsType<ProcedureActionIntentDuplicateIgnored>(Handler.Handle(
            accepted.State, duplicate, RuntimeFixture.BoundActor, ServerTick.From(10), Fx.Anomalies));

        Assert.Same(accepted.State, ignored.State);
        Assert.Equal(before.StateVersion.Next(), ignored.State.StateVersion);
        Assert.Equal(first.IntentId, Assert.Single(ignored.State.ProcessedIntentIds));
    }

    private static void AssertUnderlyingRejected(
        ShiftRuntimeState state,
        string intentId,
        string logId,
        string itemId,
        RejectionReason reason,
        ProcedureActionStartRejectionReason underlyingReason)
    {
        var result = Assert.IsType<ProcedureActionIntentUnderlyingRejected>(Handler.Handle(
            state,
            StartIntent(state, intentId, logId, itemId, state.StateVersion),
            RuntimeFixture.BoundActor,
            ServerTick.From(10),
            Fx.Anomalies));
        Assert.Equal(reason, result.Reason);
        Assert.Equal(underlyingReason, result.Result.Reason);
        Assert.Same(state, result.State);
        Assert.DoesNotContain(IntentId.From(intentId), state.ProcessedIntentIds);
    }

    private static void AssertRejected(ProcedureActionIntentResult result, RejectionReason reason, ShiftRuntimeState expected)
    {
        var rejected = Assert.IsType<ProcedureActionIntentRejected>(result);
        Assert.Equal(reason, rejected.Reason);
        Assert.Same(expected, rejected.State);
        Assert.DoesNotContain(rejected.IntentId, rejected.State.ProcessedIntentIds);
    }

    private static void AssertUnchangedProcedureRejection(
        ShiftRuntimeState before,
        ProcedureActionIntentUnderlyingRejected result,
        IntentId rejectedIntentId,
        LogId targetLogId)
    {
        Assert.Same(before, result.Result.State);
        Assert.Same(before, result.State);
        Assert.Equal(before.StateVersion, result.State.StateVersion);
        Assert.DoesNotContain(rejectedIntentId, result.State.ProcessedIntentIds);
        Assert.Same(before.Inventory, result.State.Inventory);
        Assert.Same(before.ProcedureProgressByLog, result.State.ProcedureProgressByLog);
        Assert.Null(result.State.ActiveProcedureHold);
        Assert.False(result.State.TryGetProcedureProgress(targetLogId, out _));
        Assert.True(before.TryGetLog(targetLogId, out var beforeLog));
        Assert.True(result.State.TryGetLog(targetLogId, out var afterLog));
        Assert.Same(beforeLog, afterLog);
        Assert.Equal(beforeLog.Flags, afterLog.Flags);
    }

    private static IntentEnvelope StartIntent(ShiftRuntimeState state, string intentId, string logId, string itemId, StateVersion expected) => new(
        state.ShiftId,
        IntentId.From(intentId),
        ActorId.From("untrusted_hint"),
        TargetId.From(logId),
        ProcedureIntentActions.StartProcedureAction,
        expected,
        ServerTick.Zero,
        new ProcedureActionIntentParameters(ItemId.From(itemId)));

    private static ShiftRuntimeState AtProcedure(string logId)
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), logId);
        return RuntimeFixture.MoveHost(state, logId, LogState.AT_PROCEDURE);
    }
}
