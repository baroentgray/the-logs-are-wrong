using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Tests.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Scheduler;

[Trait("Scope", "TLAW-015")]
public sealed class FeedGateJamDerivationTests
{
    private static readonly FeedGateJamDerivationService Derive = new();
    private static readonly FeedDueResolutionService ResolveDue = new();

    [Fact]
    public void Null_default_or_backward_inputs_fail_loudly_before_any_observable_result()
    {
        Assert.Throws<ArgumentNullException>(() => Derive.Derive(null!, ServerTick.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Derive.Derive(RuntimeFixture.CreateInitialState(), default));

        var jammed = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(CreateBlockerState(), JamCause.FEED_GATE_BLOCKED, ServerTick.From(10))).State;
        Assert.Throws<ArgumentOutOfRangeException>(() => Derive.Derive(jammed, ServerTick.From(9)));
    }

    [Fact]
    public void Line_not_clear_precedes_occupancy_and_returns_the_exact_original_state_for_jammed_or_repairing_lines()
    {
        var blocker = CreateBlockerState();
        var jammed = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(blocker, JamCause.FEED_GATE_BLOCKED, ServerTick.From(10))).State;
        var repairing = Assert.IsType<LineRepairStarted>(new LineRepairStartService().Start(jammed, ServerTick.From(10), Fixture.LoadP0().Shift.Scheduler)).State;

        Assert.Same(jammed, Assert.IsType<FeedGateJamDerivationLineNotClear>(Derive.Derive(jammed, ServerTick.From(10))).State);
        Assert.Same(repairing, Assert.IsType<FeedGateJamDerivationLineNotClear>(Derive.Derive(repairing, ServerTick.From(10))).State);
    }

    [Fact]
    public void No_feed_gate_log_and_intake_available_are_typed_exact_reference_no_ops()
    {
        var initial = RuntimeFixture.CreateInitialState();
        Assert.Same(initial, Assert.IsType<FeedGateJamDerivationNoFeedGateLog>(Derive.Derive(initial, ServerTick.Zero)).State);

        var gateOnly = RuntimeFixture.MoveHost(RuntimeFixture.CreateInitialState(), "log_01", LogState.AT_FEED_GATE);
        var intakeAvailable = Assert.IsType<FeedGateJamDerivationIntakeAvailable>(Derive.Derive(gateOnly, ServerTick.From(1)));
        Assert.Same(gateOnly, intakeAvailable.State);
        Assert.Equal(gateOnly.StateVersion, intakeAvailable.State.StateVersion);
        Assert.Equal(LogState.AT_FEED_GATE, Log(intakeAvailable.State, "log_01").State);
    }

    [Fact]
    public void Exact_blocker_shape_derives_the_runtime_feed_gate_log_and_only_changes_line_and_version()
    {
        var before = CreateBlockerState();
        var accepted = Assert.IsType<FeedGateJamDerived>(Derive.Derive(before, ServerTick.From(10)));

        Assert.Equal(LogId.From("log_02"), accepted.BlockedLogId);
        Assert.Equal((ServerTick.From(10), JamCause.FEED_GATE_BLOCKED), (accepted.EnteredAt, accepted.Cause));
        Assert.Equal(before.StateVersion, accepted.PriorStateVersion);
        Assert.Equal(before.StateVersion.Next(), accepted.CurrentStateVersion);
        Assert.Equal(accepted.CurrentStateVersion, accepted.State.StateVersion);
        Assert.Equal((LineState.LINE_JAMMED, JamCause.FEED_GATE_BLOCKED, LogId.From("log_02"), ServerTick.From(10)), (accepted.State.Line.State, accepted.State.Line.Cause, accepted.State.Line.PendingLogId, accepted.State.Line.EnteredAt));
        Assert.Null(accepted.State.Line.ActiveRepairHold);
        Assert.Equal(before.ShiftId, accepted.State.ShiftId);
        Assert.Equal(before.ShiftSeed, accepted.State.ShiftSeed);
        Assert.Equal(before.PendingFeed, accepted.State.PendingFeed);
        Assert.True(before.ProcessedIntentIds.SetEquals(accepted.State.ProcessedIntentIds));
        Assert.Equal(before.Inventory, accepted.State.Inventory);
        Assert.Equal(before.Containment, accepted.State.Containment);
        Assert.Equal(before.ActiveContainmentRitual, accepted.State.ActiveContainmentRitual);
        Assert.Equal(LogStates(before), LogStates(accepted.State));
    }

    [Fact]
    public void Repeated_derivation_is_idempotent_and_pre_and_post_states_are_not_value_equal()
    {
        var before = CreateBlockerState();
        var accepted = Assert.IsType<FeedGateJamDerived>(Derive.Derive(before, ServerTick.From(10)));
        var repeated = Assert.IsType<FeedGateJamDerivationLineNotClear>(Derive.Derive(accepted.State, ServerTick.From(10)));

        Assert.Same(accepted.State, repeated.State);
        Assert.False(before.ValueEquals(accepted.State));
    }

    [Fact]
    public void Contradictory_multiple_feed_gate_candidates_fail_closed_without_selecting_an_arbitrary_log()
    {
        var state = CreateWithFeedGateCapacity(2);
        state = RuntimeFixture.MoveToIntake(state, "log_03");
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);

        var failure = Assert.IsType<FeedGateJamDerivationDefensiveFailure>(Derive.Derive(state, ServerTick.From(10)));
        Assert.Equal(FeedGateJamDerivationFailureReason.FeedGateRuntimeShapeInvalid, failure.Reason);
        Assert.Same(state, failure.State);
    }

    [Fact]
    public void Service_exposes_no_caller_controlled_cause_log_or_line_mutation_seam()
    {
        var method = typeof(FeedGateJamDerivationService).GetMethod(nameof(FeedGateJamDerivationService.Derive), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        Assert.Equal(new[] { typeof(ShiftRuntimeState), typeof(ServerTick) }, method.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(typeof(FeedGateJamDerivationService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), candidate => candidate.GetParameters().Any(parameter => parameter.ParameterType is var type && (type == typeof(JamCause) || type == typeof(LogId))));
    }

    [Fact]
    public void Tlaw014_intake_placement_is_a_separate_mutation_and_does_not_require_a_jam()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        var placed = Assert.IsType<FeedDueResolved>(ResolveDue.Resolve(planned.State, ServerTick.Zero));
        var noJam = Assert.IsType<FeedGateJamDerivationNoFeedGateLog>(Derive.Derive(placed.State, ServerTick.Zero));

        Assert.Equal(FeedDueFollowUpRequirement.IntakeDeadlineStartRequired, placed.FollowUpRequirement);
        Assert.Same(placed.State, noJam.State);
        Assert.Equal(LineState.LINE_CLEAR, noJam.State.Line.State);
    }

    [Fact]
    public void Tlaw014_feed_gate_placement_then_derives_one_separate_feed_gate_blocked_jam_without_repair()
    {
        var before = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var planned = PlanEarly(before, ServerTick.From(10));
        var placed = Assert.IsType<FeedDueResolved>(ResolveDue.Resolve(planned.State, ServerTick.From(12)));
        var derived = Assert.IsType<FeedGateJamDerived>(Derive.Derive(placed.State, ServerTick.From(12)));

        Assert.Equal(FeedDueFollowUpRequirement.FeedGateJamDerivationRequired, placed.FollowUpRequirement);
        Assert.Equal(placed.State.StateVersion.Next(), derived.State.StateVersion);
        Assert.Equal((JamCause.FEED_GATE_BLOCKED, LogId.From("log_02")), (derived.State.Line.Cause, derived.State.Line.PendingLogId));
        Assert.Null(derived.State.Line.ActiveRepairHold);
    }

    [Fact]
    public void Initial_normal_and_early_post_placement_states_all_derive_only_from_current_runtime_shape()
    {
        var initialPlan = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(RuntimeFixture.CreateInitialState(), ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        var initialPlaced = Assert.IsType<FeedDueResolved>(ResolveDue.Resolve(RuntimeFixture.MoveToIntake(initialPlan.State, "log_02"), ServerTick.Zero));

        var normalBase = MoveFirstLogOutOfSupply(RuntimeFixture.CreateInitialState());
        var normalPlan = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(normalBase, ServerTick.From(10), Fixture.LoadP0().Shift.Scheduler));
        var normalPlaced = Assert.IsType<FeedDueResolved>(ResolveDue.Resolve(RuntimeFixture.MoveToIntake(normalPlan.State, "log_03"), ServerTick.From(15)));

        var earlyPlan = PlanEarly(RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01"), ServerTick.From(10));
        var earlyPlaced = Assert.IsType<FeedDueResolved>(ResolveDue.Resolve(earlyPlan.State, ServerTick.From(12)));

        Assert.Equal(new[] { LogId.From("log_01"), LogId.From("log_02"), LogId.From("log_02") }, new[]
        {
            Assert.IsType<FeedGateJamDerived>(Derive.Derive(initialPlaced.State, ServerTick.Zero)).BlockedLogId,
            Assert.IsType<FeedGateJamDerived>(Derive.Derive(normalPlaced.State, ServerTick.From(15))).BlockedLogId,
            Assert.IsType<FeedGateJamDerived>(Derive.Derive(earlyPlaced.State, ServerTick.From(12))).BlockedLogId
        });
    }

    [Fact]
    public void Independent_derivations_are_value_equal_and_occupancy_or_tick_changes_are_controlled()
    {
        var first = Assert.IsType<FeedGateJamDerived>(Derive.Derive(CreateBlockerState(), ServerTick.From(10)));
        var second = Assert.IsType<FeedGateJamDerived>(Derive.Derive(CreateBlockerState(), ServerTick.From(10)));
        Assert.Equal((first.BlockedLogId, first.Cause, first.EnteredAt, first.PriorStateVersion, first.CurrentStateVersion), (second.BlockedLogId, second.Cause, second.EnteredAt, second.PriorStateVersion, second.CurrentStateVersion));
        Assert.True(first.State.ValueEquals(second.State));

        var intakeVacated = RuntimeFixture.MoveHost(CreateBlockerState(), "log_01", LogState.AT_PROCEDURE);
        Assert.IsType<FeedGateJamDerivationIntakeAvailable>(Derive.Derive(intakeVacated, ServerTick.From(10)));
        Assert.IsType<FeedGateJamDerivationNoFeedGateLog>(Derive.Derive(RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01"), ServerTick.From(10)));

        var later = Assert.IsType<FeedGateJamDerived>(Derive.Derive(CreateBlockerState(), ServerTick.From(11)));
        Assert.Equal(ServerTick.From(11), later.EnteredAt);
        Assert.Equal(first.BlockedLogId, later.BlockedLogId);
    }

    [Fact]
    public void Derivation_preserves_active_procedure_confirmation_result_and_containment_runtime()
    {
        var state = CreateRichBlockerState();
        var beforeProcedure = state.ActiveProcedureHold;
        var beforeConfirmation = state.ActiveConfirmationTest;
        var beforeResults = state.ConfirmationResultsByLog;
        var beforeContainment = state.Containment;

        var derived = Assert.IsType<FeedGateJamDerived>(Derive.Derive(state, ServerTick.From(16)));

        Assert.Equal(beforeProcedure, derived.State.ActiveProcedureHold);
        Assert.True(ConfirmationEqual(beforeConfirmation, derived.State.ActiveConfirmationTest));
        Assert.Equal(beforeResults, derived.State.ConfirmationResultsByLog);
        Assert.Equal(beforeContainment, derived.State.Containment);
    }

    [Fact]
    public void Journaled_host_commits_keep_cursors_contiguous_and_no_op_derivation_is_not_committed()
    {
        var before = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var journal = AlignedJournal(before, ServerTick.Zero);
        var commits = new JournaledMutationCommitService();
        var planned = PlanEarly(before, ServerTick.From(10));
        Commit(commits, journal, before, planned.State, ServerTick.From(10), "feed_plan", planned.Schedule.CausedByIntentId, "plan");
        var placed = Assert.IsType<FeedDueResolved>(ResolveDue.Resolve(planned.State, ServerTick.From(12)));
        Commit(commits, journal, planned.State, placed.State, ServerTick.From(12), "feed_due", placed.ConsumedSchedule.CausedByIntentId, "place");
        var derived = Assert.IsType<FeedGateJamDerived>(Derive.Derive(placed.State, ServerTick.From(12)));
        Commit(commits, journal, placed.State, derived.State, ServerTick.From(12), "feed_gate_jam", null, $"{derived.BlockedLogId.Value}@{derived.EnteredAt.Value}");

        Assert.Equal(derived.State.StateVersion, journal.LastStateVersion);
        Assert.Equal(journal.Events.Count, (int)journal.LastSequence.Value);
        Assert.Equal($"{derived.BlockedLogId.Value}@{derived.EnteredAt.Value}", Assert.IsType<FeedGateJamPayload>(journal.Events[^1].Payload).Value);
        var noOp = Assert.IsType<FeedGateJamDerivationLineNotClear>(Derive.Derive(derived.State, ServerTick.From(12)));
        Assert.Same(derived.State, noOp.State);
        Assert.Equal(3 + (int)before.StateVersion.Value, journal.Events.Count);
    }

    private static ShiftRuntimeState CreateBlockerState()
    {
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        return RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
    }

    private static ShiftRuntimeState CreateWithFeedGateCapacity(int capacity)
    {
        var fixture = Fixture.LoadP0();
        var scheduler = fixture.Shift.Scheduler with { Capacities = fixture.Shift.Scheduler.Capacities.SetItem(NodeId.FEED_GATE, NodeCapacity.Limited(capacity)) };
        return ShiftRuntimeState.Create(fixture.Shift with { Scheduler = scheduler });
    }

    private static EarlyFeedScheduled PlanEarly(ShiftRuntimeState state, ServerTick tick)
    {
        var intent = new IntentEnvelope(state.ShiftId, IntentId.From($"early_{tick.Value}"), ActorId.From("hint"), FeedPlanningTargets.FeedGate, FeedPlanningIntentActions.RequestEarlyFeed, state.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        return Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(state, intent, RuntimeFixture.BoundActor, tick, Fixture.LoadP0().Shift.Scheduler));
    }

    private static ShiftRuntimeState MoveFirstLogOutOfSupply(ShiftRuntimeState state)
    {
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.AT_INTAKE);
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.IN_SAW);
        return RuntimeFixture.MoveHost(state, "log_01", LogState.PROCESSED);
    }

    private static ShiftRuntimeState CreateRichBlockerState()
    {
        var fixture = Fixture.LoadP0();
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_03");
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_PROCEDURE);
        state = Assert.IsType<ProcedureActionHoldStarted>(new ProcedureActionStartService().Start(state, LogId.From("log_03"), ItemId.From("holy_water"), ServerTick.From(10), fixture.Anomalies)).State;

        state = RuntimeFixture.MoveToIntake(state, "log_08");
        var completedConfirmation = Assert.IsType<ConfirmationTestDueCompleted>(new ConfirmationTestDueCompletionService().CompleteDue(
            Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(state, LogId.From("log_08"), ImmutableHashSet.Create(ItemId.From("sound_meter")), ServerTick.From(11), LineNoise.QUIET, fixture.Anomalies)).State,
            ServerTick.From(15), fixture.Anomalies));
        state = RuntimeFixture.MoveHost(completedConfirmation.State, "log_08", LogState.HELD_WRITTEN_OFF);
        state = Assert.IsType<ContainmentStableIntervalArmed>(new ContainmentAdvanceService().Advance(state, ServerTick.From(16), fixture.Shift.Containment, fixture.Anomalies)).State;

        state = RuntimeFixture.MoveToIntake(state, "log_06");
        state = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(state, LogId.From("log_06"), ImmutableHashSet.Create(ItemId.From("choir_cassette")), ServerTick.From(16), LineNoise.QUIET, fixture.Anomalies)).State;
        return RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
    }

    private static Dictionary<string, LogState> LogStates(ShiftRuntimeState state) => state.Logs.ToDictionary(log => log.LogId.ToString(), log => log.State, StringComparer.Ordinal);

    private static LogRuntimeState Log(ShiftRuntimeState state, string id)
    {
        Assert.True(state.TryGetLog(LogId.From(id), out var log));
        return log;
    }

    private static bool ConfirmationEqual(ActiveConfirmationTest? left, ActiveConfirmationTest? right) =>
        left is null ? right is null : right is not null && left.LogId == right.LogId && left.DueAt == right.DueAt && left.IsRunning == right.IsRunning;

    private static void Commit(JournaledMutationCommitService service, IEventJournal journal, ShiftRuntimeState before, ShiftRuntimeState after, ServerTick tick, string eventId, IntentId? cause, string payload) =>
        Assert.IsType<JournaledMutationCommitted>(service.Commit(journal, before, after, tick, new DomainEventDraft(EventId.From(eventId), EventTypeId.From("test.scheduler.feed_gate_jam"), new FeedGateJamPayload(payload), cause)));

    private static InMemoryEventJournal AlignedJournal(ShiftRuntimeState state, ServerTick tick)
    {
        var journal = new InMemoryEventJournal(state.ShiftId);
        for (var version = 1L; version <= state.StateVersion.Value; version++)
        {
            journal.Append(new EventEnvelope { ShiftId = state.ShiftId, EventId = EventId.From($"seed_{version}"), Sequence = EventSequence.From(version), ServerTick = tick, StateVersionAfter = StateVersion.From(version), EventType = EventTypeId.From("test.seed"), Payload = new FeedGateJamPayload($"seed_{version}") });
        }

        return journal;
    }

    private sealed record FeedGateJamPayload(string Value) : IDomainEventPayload;
}
