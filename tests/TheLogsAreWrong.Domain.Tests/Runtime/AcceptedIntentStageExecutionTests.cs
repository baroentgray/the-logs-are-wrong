using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Sequencing;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-030")]
public sealed class AcceptedIntentStageExecutionTests
{
    private static readonly ServerTick BatchTick = ServerTick.From(7);
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static readonly SchedulerConfiguration Scheduler = Fx.Shift.Scheduler;

    public static TheoryData<IntentActionId, LogState, LogState> ManualActions => new()
    {
        { LogIntentActions.RouteToProcedure, LogState.AT_INTAKE, LogState.AT_PROCEDURE },
        { LogIntentActions.ReturnFromProcedure, LogState.AT_PROCEDURE, LogState.AT_INTAKE },
        { LogIntentActions.RouteToSawQueue, LogState.AT_INTAKE, LogState.QUEUED_FOR_SAW },
        { LogIntentActions.WriteOff, LogState.AT_INTAKE, LogState.HELD_WRITTEN_OFF }
    };

    // ----- API and invariant boundary -----

    [Fact]
    public void Null_state_batch_and_configuration_reject_loudly()
    {
        var state = RuntimeFixture.CreateInitialState();
        var batch = EmptyBatch(state.ShiftId);
        var executor = new AcceptedIntentStageExecutor();

        var tools = ImmutableHashSet<ItemId>.Empty;
        var noise = LineNoiseRuntimeState.Create(state.ShiftId);
        Assert.Throws<ArgumentNullException>(() => executor.Execute(null!, batch, Scheduler, tools, noise, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(state, null!, Scheduler, tools, noise, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(state, batch, null!, tools, noise, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(state, batch, Scheduler, null!, noise, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(state, batch, Scheduler, tools, null!, Fx.Anomalies));
        Assert.Throws<ArgumentNullException>(() => executor.Execute(state, batch, Scheduler, tools, noise, null!));
    }

    [Fact]
    public void Cross_shift_batch_rejects_before_any_handler_execution()
    {
        var state = RuntimeFixture.CreateInitialState();
        var otherShift = ShiftId.From("OTHER_SHIFT");
        var envelope = new IntentEnvelope(
            otherShift, IntentId.From("intent_01"), ActorId.From("untrusted_hint"), TargetId.From("log_01"),
            LogIntentActions.RouteToProcedure, StateVersion.Zero, ServerTick.Zero, NoIntentParameters.Instance);
        var receipt = new AuthoritativeAcceptedIntent(envelope, ActorId.From("host_bound_actor"), BatchTick, ServerReceiveSequence.Zero);
        var crossShiftBatch = AcceptedIntentTickBatchFactory.Create(otherShift, BatchTick, new[] { receipt });

        Assert.NotEqual(state.ShiftId, crossShiftBatch.ShiftId);
        Assert.Throws<ArgumentException>(() => new AcceptedIntentStageExecutor().Execute(state, crossShiftBatch, Scheduler, ImmutableHashSet<ItemId>.Empty, LineNoiseRuntimeState.Create(state.ShiftId), Fx.Anomalies));
    }

    [Fact]
    public void Empty_batch_retains_exact_initial_final_state_and_batch_with_zero_steps()
    {
        var state = RuntimeFixture.CreateInitialState();
        var batch = EmptyBatch(state.ShiftId);

        var execution = Execute(state, batch);

        Assert.Empty(execution.Steps);
        Assert.Same(batch, execution.Batch);
        Assert.Same(state, execution.InitialState);
        Assert.Same(state, execution.FinalState);
        // A pristine empty tick creates no initial feed plan.
        Assert.Null(execution.FinalState.PendingFeed);
    }

    [Fact]
    public void Stage_result_step_and_outcomes_expose_no_public_constructor_field_or_setter()
    {
        var publicInstance = BindingFlags.Public | BindingFlags.Instance;
        var types = new[]
        {
            typeof(AcceptedIntentStageExecution),
            typeof(AcceptedIntentStageStep),
            typeof(AcceptedIntentStageOutcome),
            typeof(ManualRoutingIntentStageOutcome),
            typeof(EarlyFeedIntentStageOutcome),
            typeof(UnsupportedIntentStageOutcome)
        };

        Assert.All(types, type =>
        {
            Assert.Empty(type.GetConstructors(publicInstance));
            Assert.Empty(type.GetFields(publicInstance));
            Assert.All(type.GetProperties(publicInstance), property => Assert.Null(property.SetMethod));
        });
    }

    [Fact]
    public void Executor_execute_accepts_only_state_batch_configuration_confirmation_evidence_and_anomaly_catalog()
    {
        var execute = Assert.Single(
            typeof(AcceptedIntentStageExecutor).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => method.Name == "Execute");

        Assert.Equal(typeof(AcceptedIntentStageExecution), execute.ReturnType);
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(AcceptedIntentTickBatch), typeof(SchedulerConfiguration), typeof(ImmutableHashSet<ItemId>), typeof(LineNoiseRuntimeState), typeof(AnomalyCatalog) },
            execute.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(execute.GetParameters(), parameter =>
            parameter.ParameterType == typeof(object) ||
            parameter.ParameterType == typeof(bool) ||
            parameter.ParameterType == typeof(string) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
    }

    // ----- Exact identity trace -----

    [Fact]
    public void Executor_retains_exact_batch_initial_and_final_references_and_before_after_chain()
    {
        var state = IntakeState();
        var first = Routing(state, "first", "log_01", LogIntentActions.RouteToProcedure, state.StateVersion);
        var second = Routing(state, "second", "log_01", LogIntentActions.ReturnFromProcedure, state.StateVersion.Next());
        var receipts = new[] { Receipt(first, 0), Receipt(second, 1) };
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, receipts);

        var execution = Execute(state, batch);

        Assert.Same(batch, execution.Batch);
        Assert.Same(state, execution.InitialState);
        Assert.Equal(2, execution.Steps.Length);
        Assert.Same(receipts[0], execution.Steps[0].Receipt);
        Assert.Same(receipts[1], execution.Steps[1].Receipt);
        Assert.Same(state, execution.Steps[0].BeforeState);
        Assert.Same(execution.Steps[0].AfterState, execution.Steps[1].BeforeState);
        Assert.Same(execution.Steps[0].Outcome.State, execution.Steps[0].AfterState);
        Assert.Same(execution.Steps[1].AfterState, execution.FinalState);
        // Caller inputs remain unchanged by the immutable executor.
        Assert.Equal(StateVersion.From(2), state.StateVersion);
    }

    [Fact]
    public void Accepted_routing_retains_exact_manual_result_and_authoritative_actor_not_hint()
    {
        var state = IntakeState();
        var envelope = Routing(state, "intent_01", "log_01", LogIntentActions.RouteToProcedure, state.StateVersion, hint: "attacker_claim");
        var receipt = Receipt(envelope, 0, actor: "host_bound_actor");
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, new[] { receipt });

        var execution = Execute(state, batch);

        var outcome = Assert.IsType<ManualRoutingIntentStageOutcome>(Assert.Single(execution.Steps).Outcome);
        var accepted = Assert.IsType<ManualLogIntentAccepted>(outcome.Result);
        Assert.Same(accepted.State, outcome.State);
        Assert.Equal(ActorId.From("host_bound_actor"), accepted.Transition.AuthoritativeActor);
        Assert.NotEqual(receipt.Envelope.ActorIdHint, accepted.Transition.AuthoritativeActor);
        Assert.Same(NoIntentParameters.Instance, execution.Steps[0].Receipt.Envelope.Parameters);
    }

    [Fact]
    public void Early_feed_retains_exact_result_actor_causation_tick_and_configured_delay()
    {
        var state = RuntimeFixture.CreateInitialState();
        var envelope = EarlyFeed(state, "early_01", state.StateVersion, hint: "attacker_claim");
        var receipt = Receipt(envelope, 0, actor: "host_bound_actor");
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, new[] { receipt });

        var execution = Execute(state, batch);

        var outcome = Assert.IsType<EarlyFeedIntentStageOutcome>(Assert.Single(execution.Steps).Outcome);
        var scheduled = Assert.IsType<EarlyFeedScheduled>(outcome.Result);
        Assert.Same(scheduled.State, outcome.State);
        Assert.Same(scheduled.State, execution.FinalState);
        Assert.Equal(ActorId.From("host_bound_actor"), scheduled.AuthoritativeActor);
        Assert.NotEqual(receipt.Envelope.ActorIdHint, scheduled.AuthoritativeActor);
        Assert.Equal(IntentId.From("early_01"), scheduled.Schedule.CausedByIntentId);
        Assert.Equal(FeedScheduleKind.EARLY, scheduled.Schedule.Kind);
        Assert.Equal(BatchTick, scheduled.Schedule.ScheduledAt);
        Assert.Equal(SimulationDuration.FromTicks(Scheduler.EarlyFeedDelaySeconds), scheduled.Schedule.Delay);
    }

    // ----- All five owned actions dispatch through the existing handlers -----

    [Theory]
    [MemberData(nameof(ManualActions))]
    public void Each_manual_routing_action_dispatches_through_the_manual_handler(IntentActionId action, LogState from, LogState to)
    {
        var state = StateWithFirstLogAt(from);
        var envelope = Routing(state, "intent_01", "log_01", action, state.StateVersion);
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, new[] { Receipt(envelope, 0) });

        var execution = Execute(state, batch);

        var step = Assert.Single(execution.Steps);
        var outcome = Assert.IsType<ManualRoutingIntentStageOutcome>(step.Outcome);
        var accepted = Assert.IsType<ManualLogIntentAccepted>(outcome.Result);
        Assert.Equal(to, accepted.Transition.ToState);
        Assert.Same(accepted.State, step.AfterState);
        Assert.Same(accepted.State, execution.FinalState);
        Assert.Equal(state.StateVersion.Next(), execution.FinalState.StateVersion);
    }

    [Fact]
    public void Request_early_feed_dispatches_through_the_early_feed_handler_with_intake_occupied()
    {
        var state = IntakeState();
        var envelope = EarlyFeed(state, "early_01", state.StateVersion);
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, new[] { Receipt(envelope, 0) });

        var execution = Execute(state, batch);

        var outcome = Assert.IsType<EarlyFeedIntentStageOutcome>(Assert.Single(execution.Steps).Outcome);
        var scheduled = Assert.IsType<EarlyFeedScheduled>(outcome.Result);
        // Intake occupancy is deliberately allowed for early feed; the next manifest log is reserved.
        Assert.Equal(LogState.AT_INTAKE, execution.FinalState.Logs[0].State);
        Assert.Equal(LogId.From("log_02"), scheduled.Schedule.LogId);
    }

    // ----- Existing result variants remain unchanged where reachable -----

    [Fact]
    public void Manual_rejection_variant_is_retained_and_state_is_unchanged()
    {
        var state = IntakeState();
        // log_02 is still SCHEDULED, so route_to_procedure cannot apply and rejects.
        var envelope = Routing(state, "rejected", "log_02", LogIntentActions.RouteToProcedure, state.StateVersion);
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, new[] { Receipt(envelope, 0) });

        var execution = Execute(state, batch);

        var step = Assert.Single(execution.Steps);
        var rejected = Assert.IsType<ManualLogIntentRejected>(Assert.IsType<ManualRoutingIntentStageOutcome>(step.Outcome).Result);
        Assert.Equal(RejectionReason.TARGET_NOT_IN_STATE, rejected.Reason);
        Assert.Same(state, step.AfterState);
        Assert.Same(state, execution.FinalState);
    }

    [Fact]
    public void Manual_duplicate_variant_is_retained_when_state_already_processed_the_intent()
    {
        var setupState = IntakeState();
        var setup = new ManualLogIntentHandler().Handle(
            setupState,
            Routing(setupState, "dup", "log_01", LogIntentActions.RouteToProcedure, setupState.StateVersion),
            RuntimeFixture.BoundActor);
        var processedState = Assert.IsType<ManualLogIntentAccepted>(setup).State;

        // Reuse the exact same intent id already recorded as processed, with the exact current version.
        var envelope = Routing(processedState, "dup", "log_01", LogIntentActions.ReturnFromProcedure, processedState.StateVersion);
        var batch = AcceptedIntentTickBatchFactory.Create(processedState.ShiftId, BatchTick, new[] { Receipt(envelope, 0) });

        var execution = Execute(processedState, batch);

        var duplicate = Assert.IsType<DuplicateIntentIgnored>(
            Assert.IsType<ManualRoutingIntentStageOutcome>(Assert.Single(execution.Steps).Outcome).Result);
        Assert.Equal(IntentId.From("dup"), duplicate.IntentId);
        Assert.Same(processedState, execution.FinalState);
    }

    [Fact]
    public void Early_feed_rejected_duplicate_and_unsupported_variants_are_retained()
    {
        var state = RuntimeFixture.CreateInitialState();
        var accepted = new EarlyFeedIntentHandler().Handle(
            state,
            EarlyFeed(state, "efdup", state.StateVersion),
            RuntimeFixture.BoundActor,
            BatchTick,
            Scheduler);
        var pendingState = Assert.IsType<EarlyFeedScheduled>(accepted).State;

        var duplicateEnvelope = EarlyFeed(pendingState, "efdup", pendingState.StateVersion);
        var rejectedEnvelope = EarlyFeed(pendingState, "ef_pending", pendingState.StateVersion);
        var unsupportedEnvelope = EarlyFeed(pendingState, "ef_target", pendingState.StateVersion, target: "log_01");
        var batch = AcceptedIntentTickBatchFactory.Create(pendingState.ShiftId, BatchTick, new[]
        {
            Receipt(duplicateEnvelope, 0),
            Receipt(rejectedEnvelope, 1),
            Receipt(unsupportedEnvelope, 2)
        });

        var execution = Execute(pendingState, batch);

        var duplicate = Assert.IsType<DuplicateEarlyFeedIntentIgnored>(EarlyResult(execution.Steps[0]));
        Assert.Equal(IntentId.From("efdup"), duplicate.IntentId);
        var rejected = Assert.IsType<EarlyFeedIntentRejected>(EarlyResult(execution.Steps[1]));
        Assert.Equal(RejectionReason.FEED_ALREADY_PENDING, rejected.Reason);
        var unsupported = Assert.IsType<UnsupportedEarlyFeedIntent>(EarlyResult(execution.Steps[2]));
        Assert.Equal(EarlyFeedIntentUnsupportedReason.Target, unsupported.Reason);
        // None of these variants mutate state.
        Assert.Same(pendingState, execution.FinalState);
    }

    [Fact]
    public void Unknown_action_produces_a_stage_local_unsupported_outcome_without_calling_a_handler()
    {
        var state = IntakeState();
        var action = IntentActionId.From("invent_action");
        var envelope = Routing(state, "unknown", "log_01", action, state.StateVersion);
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, new[] { Receipt(envelope, 0) });

        var execution = Execute(state, batch);

        var step = Assert.Single(execution.Steps);
        var unsupported = Assert.IsType<UnsupportedIntentStageOutcome>(step.Outcome);
        Assert.Equal(action, unsupported.Action);
        Assert.Same(state, step.BeforeState);
        Assert.Same(state, step.AfterState);
        Assert.Same(state, execution.FinalState);
        // Unknown actions are never marked processed and never extend the rejection taxonomy.
        Assert.DoesNotContain(IntentId.From("unknown"), execution.FinalState.ProcessedIntentIds);
    }

    // ----- Mixed ordered batches -----

    [Fact]
    public void Accepted_routing_then_old_version_intent_becomes_stale()
    {
        var state = IntakeState();
        var accepted = Routing(state, "accepted", "log_01", LogIntentActions.RouteToProcedure, state.StateVersion);
        var stale = Routing(state, "stale", "log_01", LogIntentActions.RouteToSawQueue, state.StateVersion);
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, new[] { Receipt(accepted, 0), Receipt(stale, 1) });

        var execution = Execute(state, batch);

        Assert.IsType<ManualLogIntentAccepted>(ManualResult(execution.Steps[0]));
        var rejected = Assert.IsType<ManualLogIntentRejected>(ManualResult(execution.Steps[1]));
        Assert.Equal(RejectionReason.STALE_STATE_VERSION, rejected.Reason);
        Assert.Same(execution.Steps[0].AfterState, execution.Steps[1].AfterState);
    }

    [Fact]
    public void Accepted_routing_then_exact_new_version_routing_succeeds()
    {
        var state = IntakeState();
        var first = Routing(state, "first", "log_01", LogIntentActions.RouteToProcedure, state.StateVersion);
        var second = Routing(state, "second", "log_01", LogIntentActions.ReturnFromProcedure, state.StateVersion.Next());
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, new[] { Receipt(first, 0), Receipt(second, 1) });

        var execution = Execute(state, batch);

        Assert.IsType<ManualLogIntentAccepted>(ManualResult(execution.Steps[0]));
        var second_accepted = Assert.IsType<ManualLogIntentAccepted>(ManualResult(execution.Steps[1]));
        Assert.Equal(LogState.AT_INTAKE, second_accepted.Transition.ToState);
        Assert.Equal(state.StateVersion.Next().Next(), execution.FinalState.StateVersion);
    }

    [Fact]
    public void Accepted_early_feed_then_exact_new_version_routing_sees_evolved_state()
    {
        var state = IntakeState();
        var early = EarlyFeed(state, "early", state.StateVersion);
        var routing = Routing(state, "routing", "log_01", LogIntentActions.RouteToProcedure, state.StateVersion.Next());
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, new[] { Receipt(early, 0), Receipt(routing, 1) });

        var execution = Execute(state, batch);

        Assert.IsType<EarlyFeedScheduled>(EarlyResult(execution.Steps[0]));
        var accepted = Assert.IsType<ManualLogIntentAccepted>(ManualResult(execution.Steps[1]));
        Assert.Equal(LogState.AT_PROCEDURE, accepted.Transition.ToState);
        Assert.Equal(state.StateVersion.Next(), accepted.Transition.PriorStateVersion);
    }

    [Fact]
    public void Routing_rejection_then_later_valid_intent_still_executes()
    {
        var state = IntakeState();
        var rejected = Routing(state, "rejected", "log_02", LogIntentActions.RouteToProcedure, state.StateVersion);
        var valid = Routing(state, "valid", "log_01", LogIntentActions.RouteToProcedure, state.StateVersion);
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, new[] { Receipt(rejected, 0), Receipt(valid, 1) });

        var execution = Execute(state, batch);

        Assert.IsType<ManualLogIntentRejected>(ManualResult(execution.Steps[0]));
        Assert.Same(state, execution.Steps[1].BeforeState);
        Assert.IsType<ManualLogIntentAccepted>(ManualResult(execution.Steps[1]));
        Assert.Equal(state.StateVersion.Next(), execution.FinalState.StateVersion);
    }

    [Fact]
    public void Unknown_action_then_later_valid_intent_still_executes()
    {
        var state = IntakeState();
        var unknown = Routing(state, "unknown", "log_01", IntentActionId.From("invent_action"), state.StateVersion);
        var valid = Routing(state, "valid", "log_01", LogIntentActions.RouteToProcedure, state.StateVersion);
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, new[] { Receipt(unknown, 0), Receipt(valid, 1) });

        var execution = Execute(state, batch);

        Assert.IsType<UnsupportedIntentStageOutcome>(execution.Steps[0].Outcome);
        Assert.Same(state, execution.Steps[1].BeforeState);
        Assert.IsType<ManualLogIntentAccepted>(ManualResult(execution.Steps[1]));
    }

    [Fact]
    public void Mixed_manual_early_and_unknown_outcomes_retain_exact_batch_order()
    {
        var (execution, receipts) = BuildMixedExecution();

        Assert.Equal(3, execution.Steps.Length);
        Assert.Same(receipts[0], execution.Steps[0].Receipt);
        Assert.Same(receipts[1], execution.Steps[1].Receipt);
        Assert.Same(receipts[2], execution.Steps[2].Receipt);
        Assert.IsType<ManualRoutingIntentStageOutcome>(execution.Steps[0].Outcome);
        Assert.IsType<EarlyFeedIntentStageOutcome>(execution.Steps[1].Outcome);
        Assert.IsType<UnsupportedIntentStageOutcome>(execution.Steps[2].Outcome);
    }

    [Fact]
    public void Independent_equivalent_sequences_produce_value_equivalent_trace_and_final_state()
    {
        var (first, _) = BuildMixedExecution();
        var (second, _) = BuildMixedExecution();

        Assert.Equal(
            first.Steps.Select(step => step.Outcome.GetType()),
            second.Steps.Select(step => step.Outcome.GetType()));
        Assert.Equal(
            first.Steps.Select(step => step.AfterState.StateVersion),
            second.Steps.Select(step => step.AfterState.StateVersion));
        Assert.True(first.FinalState.ValueEquals(second.FinalState));
    }

    // ----- Stage separation -----

    [Fact]
    public void Routing_that_vacates_intake_creates_no_pending_feed_and_no_other_stage_work()
    {
        var state = IntakeState();
        var envelope = Routing(state, "vacate", "log_01", LogIntentActions.RouteToSawQueue, state.StateVersion);
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, new[] { Receipt(envelope, 0) });

        var execution = Execute(state, batch);
        var final = execution.FinalState;

        Assert.Equal(LogState.QUEUED_FOR_SAW, final.Logs[0].State);
        // No stage-5 normal feed planning, no stage-3/4 deadline or saw work, no stage-6 line/containment derivation.
        Assert.Null(final.PendingFeed);
        Assert.Null(final.ActiveIntakeDeadline);
        Assert.Null(final.ActiveSawCycle);
        Assert.Equal(LineState.LINE_CLEAR, final.Line.State);
        Assert.Equal(ContainmentState.STABLE, final.Containment.State);
        // The executor assigns no version of its own beyond the one the handler produced.
        Assert.Equal(state.StateVersion.Next(), final.StateVersion);
    }

    [Fact]
    public void Executor_assigns_no_state_version_when_no_receipt_is_accepted()
    {
        var state = IntakeState();
        var rejected = Routing(state, "rejected", "log_02", LogIntentActions.RouteToProcedure, state.StateVersion);
        var unknown = Routing(state, "unknown", "log_01", IntentActionId.From("invent_action"), state.StateVersion);
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, new[] { Receipt(rejected, 0), Receipt(unknown, 1) });

        var execution = Execute(state, batch);

        Assert.Same(state, execution.FinalState);
        Assert.Equal(state.StateVersion, execution.FinalState.StateVersion);
        Assert.Empty(execution.FinalState.ProcessedIntentIds);
    }

    // ----- Helpers -----

    private static AcceptedIntentStageExecution Execute(ShiftRuntimeState initialState, AcceptedIntentTickBatch batch)
        => new AcceptedIntentStageExecutor().Execute(initialState, batch, Scheduler, ImmutableHashSet<ItemId>.Empty, LineNoiseRuntimeState.Create(initialState.ShiftId), Fx.Anomalies);

    private static (AcceptedIntentStageExecution Execution, AuthoritativeAcceptedIntent[] Receipts) BuildMixedExecution()
    {
        var state = IntakeState();
        var routing = Routing(state, "routing", "log_01", LogIntentActions.RouteToProcedure, state.StateVersion);
        var early = EarlyFeed(state, "early", state.StateVersion.Next());
        var unknown = Routing(state, "unknown", "log_01", IntentActionId.From("invent_action"), state.StateVersion.Next());
        var receipts = new[] { Receipt(routing, 0), Receipt(early, 1), Receipt(unknown, 2) };
        var batch = AcceptedIntentTickBatchFactory.Create(state.ShiftId, BatchTick, receipts);
        return (Execute(state, batch), receipts);
    }

    private static ManualLogIntentResult ManualResult(AcceptedIntentStageStep step)
        => Assert.IsType<ManualRoutingIntentStageOutcome>(step.Outcome).Result;

    private static EarlyFeedIntentResult EarlyResult(AcceptedIntentStageStep step)
        => Assert.IsType<EarlyFeedIntentStageOutcome>(step.Outcome).Result;

    private static ShiftRuntimeState IntakeState() => RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");

    private static ShiftRuntimeState StateWithFirstLogAt(LogState logState) => logState switch
    {
        LogState.AT_INTAKE => IntakeState(),
        LogState.AT_PROCEDURE => RuntimeFixture.MoveHost(IntakeState(), "log_01", LogState.AT_PROCEDURE),
        _ => throw new ArgumentOutOfRangeException(nameof(logState))
    };

    private static AuthoritativeAcceptedIntent Receipt(IntentEnvelope envelope, long sequence, string actor = "host_bound_actor")
        => new(envelope, ActorId.From(actor), BatchTick, ServerReceiveSequence.From(sequence));

    private static AcceptedIntentTickBatch EmptyBatch(ShiftId shiftId)
        => AcceptedIntentTickBatchFactory.Create(shiftId, BatchTick, Array.Empty<AuthoritativeAcceptedIntent>());

    private static IntentEnvelope Routing(
        ShiftRuntimeState state,
        string intentId,
        string logId,
        IntentActionId action,
        StateVersion expectedVersion,
        string hint = "untrusted_hint") => new(
            state.ShiftId,
            IntentId.From(intentId),
            ActorId.From(hint),
            TargetId.From(logId),
            action,
            expectedVersion,
            ServerTick.Zero,
            NoIntentParameters.Instance);

    private static IntentEnvelope EarlyFeed(
        ShiftRuntimeState state,
        string intentId,
        StateVersion expectedVersion,
        string target = "FEED_GATE",
        string hint = "untrusted_hint") => new(
            state.ShiftId,
            IntentId.From(intentId),
            ActorId.From(hint),
            TargetId.From(target),
            FeedPlanningIntentActions.RequestEarlyFeed,
            expectedVersion,
            ServerTick.Zero,
            NoIntentParameters.Instance);
}
