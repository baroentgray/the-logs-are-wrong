using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-039")]
public sealed class ConfirmationTestIntentHandlerTests
{
    private static readonly ConfirmationTestIntentHandler Handler = new();
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();

    [Fact]
    public void Penitent_start_retains_the_exact_tlaw008_result_and_records_the_intent_atomically()
    {
        var before = AtIntake("log_03");
        var intent = StartIntent(before, "penitent_start", "log_03", before.StateVersion);

        var result = Assert.IsType<ConfirmationTestIntentStarted>(Handler.Handle(
            before, intent, RuntimeFixture.BoundActor, ServerTick.From(10), Tools("sound_meter"), Quiet(before), Fx.Anomalies));

        Assert.Same(result.Result.State, result.State);
        Assert.Equal(before.StateVersion.Next(), result.State.StateVersion);
        Assert.Contains(intent.IntentId, result.State.ProcessedIntentIds);
        Assert.Equal(2, result.State.Inventory.GetConsumableQuantity(ItemId.From("holy_water")));
        Assert.Same(before.Inventory, result.State.Inventory);
        Assert.True(result.State.TryGetLog(LogId.From("log_03"), out var log));
        Assert.Same(before.Logs.Single(item => item.LogId == log.LogId), log);
        Assert.Empty(log.Flags);
        Assert.Null(result.State.ActiveProcedureHold);
        Assert.False(result.State.TryGetConfirmationResult(LogId.From("log_03"), out _));
        var active = Assert.IsType<ActiveConfirmationTest>(result.State.ActiveConfirmationTest);
        Assert.Equal(ServerTick.From(10), active.SegmentStartedAt);
        Assert.Equal(ServerTick.From(14), active.DueAt);
        Assert.Equal(LineNoise.QUIET, Quiet(before).Current);
    }

    [Fact]
    public void Guard_order_duplicate_unsupported_malformed_and_target_rejections_preserve_the_exact_input_state()
    {
        var state = AtIntake("log_03");
        var wrongShift = new IntentEnvelope(
            ShiftId.From("OTHER_SHIFT"), IntentId.From("wrong_shift"), ActorId.From("hint"), TargetId.From("missing"),
            ConfirmationIntentActions.StartConfirmationTest, StateVersion.From(99), ServerTick.Zero, NoIntentParameters.Instance);
        AssertRejected(Handler.Handle(state, wrongShift, null, ServerTick.From(10), Tools("sound_meter"), Quiet(state), Fx.Anomalies), RejectionReason.SHIFT_MISMATCH, state);

        var missingActor = StartIntent(state, "missing_actor", "log_03", state.StateVersion);
        AssertRejected(Handler.Handle(state, missingActor, null, ServerTick.From(10), Tools("sound_meter"), Quiet(state), Fx.Anomalies), RejectionReason.ACTOR_NOT_BOUND, state);

        var stale = StartIntent(state, "stale", "log_03", state.StateVersion.Next());
        AssertRejected(Handler.Handle(state, stale, RuntimeFixture.BoundActor, ServerTick.From(10), Tools("sound_meter"), Quiet(state), Fx.Anomalies), RejectionReason.STALE_STATE_VERSION, state);

        var unsupported = new IntentEnvelope(state.ShiftId, IntentId.From("unsupported"), ActorId.From("hint"), TargetId.From("log_03"),
            LogIntentActions.RouteToProcedure, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var unowned = Assert.IsType<ConfirmationTestIntentUnsupported>(Handler.Handle(state, unsupported, RuntimeFixture.BoundActor, ServerTick.From(10), Tools("sound_meter"), Quiet(state), Fx.Anomalies));
        Assert.Same(state, unowned.State);

        var malformed = new IntentEnvelope(state.ShiftId, IntentId.From("malformed"), ActorId.From("hint"), TargetId.From("log_03"),
            ConfirmationIntentActions.StartConfirmationTest, state.StateVersion, ServerTick.Zero, new ProcedureActionIntentParameters(ItemId.From("holy_water")));
        AssertRejected(Handler.Handle(state, malformed, RuntimeFixture.BoundActor, ServerTick.From(10), Tools("sound_meter"), Quiet(state), Fx.Anomalies), RejectionReason.MALFORMED_CONFIRMATION_PARAMETERS, state);

        var missingTarget = StartIntent(state, "missing_target", "missing", state.StateVersion);
        AssertRejected(Handler.Handle(state, missingTarget, RuntimeFixture.BoundActor, ServerTick.From(10), Tools("sound_meter"), Quiet(state), Fx.Anomalies), RejectionReason.TARGET_NOT_FOUND, state);
    }

    [Fact]
    public void Every_underlying_confirmation_rejection_retains_exact_tlaw008_evidence_and_does_not_mutate()
    {
        var outside = RuntimeFixture.CreateInitialState();
        AssertUnderlyingRejected(outside, "outside", "log_03", Tools("sound_meter"), Quiet(outside), RejectionReason.TARGET_NOT_IN_STATE, ConfirmationTestStartRejectionReason.TargetNotAtIntake);

        var noPlan = AtIntake("log_01");
        AssertUnderlyingRejected(noPlan, "no_plan", "log_01", ImmutableHashSet<ItemId>.Empty, Quiet(noPlan), RejectionReason.CONFIRMATION_NO_PLAN, ConfirmationTestStartRejectionReason.NoConfirmationPlan);

        var missingTool = AtIntake("log_03");
        AssertUnderlyingRejected(missingTool, "missing_tool", "log_03", ImmutableHashSet<ItemId>.Empty, Quiet(missingTool), RejectionReason.CONFIRMATION_REQUIRED_TOOL_UNAVAILABLE, ConfirmationTestStartRejectionReason.MissingRequiredTool);

        var wrongNoise = AtIntake("log_03");
        AssertUnderlyingRejected(wrongNoise, "wrong_noise", "log_03", Tools("sound_meter"), Loud(ServerTick.From(10)), RejectionReason.CONFIRMATION_REQUIRED_LINE_NOISE_NOT_MET, ConfirmationTestStartRejectionReason.RequiredLineNoiseNotMet);

        var active = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(
            AtIntake("log_03"), LogId.From("log_03"), Tools("sound_meter"), ServerTick.From(10), Quiet(AtIntake("log_03")), Fx.Anomalies)).State;
        AssertUnderlyingRejected(active, "active", "log_03", Tools("sound_meter"), Quiet(active), RejectionReason.CONFIRMATION_ACTIVE, ConfirmationTestStartRejectionReason.ActiveConfirmationAlreadyExists);

        var completedStart = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(
            AtIntake("log_03"), LogId.From("log_03"), Tools("sound_meter"), ServerTick.From(10), Quiet(AtIntake("log_03")), Fx.Anomalies));
        var completed = Assert.IsType<ConfirmationTestDueCompleted>(new ConfirmationTestDueCompletionService().CompleteDue(completedStart.State, ServerTick.From(14), Fx.Anomalies)).State;
        AssertUnderlyingRejected(completed, "confirmed", "log_03", Tools("sound_meter"), Quiet(completed), RejectionReason.CONFIRMATION_ALREADY_COMPLETED, ConfirmationTestStartRejectionReason.AlreadyConfirmed);
    }

    [Fact]
    public void Duplicate_confirmation_intent_is_ignored_without_another_state_or_processed_intent_mutation()
    {
        var before = AtIntake("log_03");
        var first = StartIntent(before, "duplicate", "log_03", before.StateVersion);
        var accepted = Assert.IsType<ConfirmationTestIntentStarted>(Handler.Handle(
            before, first, RuntimeFixture.BoundActor, ServerTick.From(10), Tools("sound_meter"), Quiet(before), Fx.Anomalies));
        var duplicate = StartIntent(accepted.State, "duplicate", "log_03", accepted.State.StateVersion);

        var ignored = Assert.IsType<ConfirmationTestIntentDuplicateIgnored>(Handler.Handle(
            accepted.State, duplicate, RuntimeFixture.BoundActor, ServerTick.From(10), Tools("sound_meter"), Quiet(accepted.State), Fx.Anomalies));

        Assert.Same(accepted.State, ignored.State);
        Assert.Equal(before.StateVersion.Next(), ignored.State.StateVersion);
        Assert.Equal(first.IntentId, Assert.Single(ignored.State.ProcessedIntentIds));
    }

    [Fact]
    public void Resin_and_false_species_start_with_their_exact_configured_plans_without_tool_consumption()
    {
        var resin = AtIntake("log_06");
        var resinStarted = Assert.IsType<ConfirmationTestIntentStarted>(Handler.Handle(
            resin, StartIntent(resin, "resin", "log_06", resin.StateVersion), RuntimeFixture.BoundActor, ServerTick.From(10), Tools("choir_cassette"), Loud(ServerTick.From(10)), Fx.Anomalies));
        var resinActive = Assert.IsType<ActiveConfirmationTest>(resinStarted.State.ActiveConfirmationTest);
        Assert.Equal(ServerTick.From(14), resinActive.DueAt);
        Assert.Null(resinActive.Plan.RequiredLineNoise);
        Assert.Contains(IntentId.From("resin"), resinStarted.State.ProcessedIntentIds);
        Assert.Same(resin.Inventory, resinStarted.State.Inventory);

        var falseSpecies = AtIntake("log_05");
        var falseStarted = Assert.IsType<ConfirmationTestIntentStarted>(Handler.Handle(
            falseSpecies, StartIntent(falseSpecies, "false_species", "log_05", falseSpecies.StateVersion), RuntimeFixture.BoundActor, ServerTick.From(10), Tools("scale", "caliper"), Quiet(falseSpecies), Fx.Anomalies));
        var falseActive = Assert.IsType<ActiveConfirmationTest>(falseStarted.State.ActiveConfirmationTest);
        Assert.Equal(ServerTick.From(16), falseActive.DueAt);
        Assert.False(falseActive.Plan.Continuous);
        Assert.Same(falseSpecies.Inventory, falseStarted.State.Inventory);
    }

    [Fact]
    public void Existing_non_intent_start_and_due_completion_remain_unmarked()
    {
        var before = AtIntake("log_03");
        var started = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(
            before, LogId.From("log_03"), Tools("sound_meter"), ServerTick.From(10), Quiet(before), Fx.Anomalies));
        var completed = Assert.IsType<ConfirmationTestDueCompleted>(new ConfirmationTestDueCompletionService().CompleteDue(
            started.State, ServerTick.From(14), Fx.Anomalies));

        Assert.Empty(started.State.ProcessedIntentIds);
        Assert.Empty(completed.State.ProcessedIntentIds);
        Assert.Equal(started.State.StateVersion.Next(), completed.State.StateVersion);
    }

    private static void AssertUnderlyingRejected(
        ShiftRuntimeState state,
        string intentId,
        string logId,
        ImmutableHashSet<ItemId> activeTools,
        LineNoiseRuntimeState lineNoise,
        RejectionReason reason,
        ConfirmationTestStartRejectionReason underlyingReason)
    {
        var intent = StartIntent(state, intentId, logId, state.StateVersion);
        var result = Assert.IsType<ConfirmationTestIntentUnderlyingRejected>(Handler.Handle(
            state, intent, RuntimeFixture.BoundActor, ServerTick.From(10), activeTools, lineNoise, Fx.Anomalies));

        Assert.Equal(reason, result.Reason);
        Assert.Equal(underlyingReason, result.Result.Reason);
        Assert.Same(result.Result.State, result.State);
        AssertUnchanged(state, result.State, intent.IntentId, LogId.From(logId));
    }

    private static void AssertRejected(ConfirmationTestIntentResult result, RejectionReason reason, ShiftRuntimeState expected)
    {
        var rejected = Assert.IsType<ConfirmationTestIntentRejected>(result);
        Assert.Equal(reason, rejected.Reason);
        AssertUnchanged(expected, rejected.State, rejected.IntentId, null);
    }

    private static void AssertUnchanged(ShiftRuntimeState before, ShiftRuntimeState after, IntentId intentId, LogId? target)
    {
        Assert.Same(before, after);
        Assert.Equal(before.StateVersion, after.StateVersion);
        Assert.DoesNotContain(intentId, after.ProcessedIntentIds);
        Assert.Same(before.Inventory, after.Inventory);
        Assert.Same(before.ProcedureProgressByLog, after.ProcedureProgressByLog);
        Assert.Same(before.ActiveProcedureHold, after.ActiveProcedureHold);
        Assert.Same(before.ActiveConfirmationTest, after.ActiveConfirmationTest);
        Assert.Same(before.ConfirmationResultsByLog, after.ConfirmationResultsByLog);
        if (target is { } logId)
        {
            Assert.True(before.TryGetLog(logId, out var beforeLog));
            Assert.True(after.TryGetLog(logId, out var afterLog));
            Assert.Same(beforeLog, afterLog);
            Assert.Equal(beforeLog.Flags, afterLog.Flags);
        }
    }

    private static IntentEnvelope StartIntent(ShiftRuntimeState state, string intentId, string logId, StateVersion expected) => new(
        state.ShiftId, IntentId.From(intentId), ActorId.From("untrusted_hint"), TargetId.From(logId),
        ConfirmationIntentActions.StartConfirmationTest, expected, ServerTick.Zero, NoIntentParameters.Instance);

    private static ShiftRuntimeState AtIntake(string logId) => RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), logId);

    private static ImmutableHashSet<ItemId> Tools(params string[] items) => items.Select(ItemId.From).ToImmutableHashSet();

    private static LineNoiseRuntimeState Quiet(ShiftRuntimeState state) => LineNoiseRuntimeState.Create(state.ShiftId);

    private static LineNoiseRuntimeState Loud(ServerTick tick)
    {
        var source = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        source = RuntimeFixture.MoveHost(source, "log_01", LogState.QUEUED_FOR_SAW);
        var saw = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(source, tick, Fx.Shift.Scheduler));
        return new LineNoiseDerivationService().Evaluate(
            LineNoiseRuntimeState.Create(saw.State.ShiftId), saw.State, MovementNoiseRuntimeState.Create(saw.State.ShiftId), tick).State;
    }
}
