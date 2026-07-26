using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Tests.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Containment;

[Trait("Scope", "TLAW-009")]
public sealed class ContainmentLifecycleTests
{
    private static readonly ContainmentAdvanceService Advance = new();
    private static readonly ContainmentRitualStartService StartRitual = new();
    private static readonly ContainmentRitualCompletionService CompleteRitual = new();
    private static readonly ContainmentRitualCancellationService CancelRitual = new();

    [Fact]
    public void Danger_weight_counts_only_written_off_catalogued_anomalies_and_never_mutates_inputs()
    {
        var fixture = Fixture.LoadP0();
        var initial = RuntimeFixture.CreateInitialState();
        var normal = WriteOff(initial, "log_01");
        var falseSpecies = WriteOff(normal, "log_05");
        var penitent = WriteOff(falseSpecies, "log_03");
        var resin = WriteOff(penitent, "log_06");

        Assert.Equal((ContainmentState.STABLE, ServerTick.Zero, (ServerTick?)null), (initial.Containment.State, initial.Containment.EnteredAt, initial.Containment.DeadlineAt));
        var zeroWeightAdvance = Assert.IsType<ContainmentAdvanceNoChange>(Advance.Advance(initial, ServerTick.From(10), fixture.Shift.Containment, fixture.Anomalies));
        Assert.Same(initial, zeroWeightAdvance.State);
        Assert.Equal(0, DangerWeightResolver.Resolve(initial, fixture.Anomalies));
        Assert.Equal(0, DangerWeightResolver.Resolve(falseSpecies, fixture.Anomalies));
        Assert.Equal(1, DangerWeightResolver.Resolve(penitent, fixture.Anomalies));
        Assert.Equal(2, DangerWeightResolver.Resolve(resin, fixture.Anomalies));
        Assert.Equal(LogState.HELD_WRITTEN_OFF, resin.TryGetLog(LogId.From("log_03"), out var retained) ? retained.State : default);
        Assert.True(initial.ValueEquals(RuntimeFixture.CreateInitialState()));
    }

    [Fact]
    public void Positive_weight_arms_exact_interval_and_later_writeoff_does_not_rebase_it()
    {
        var fixture = Fixture.LoadP0();
        var weightOne = WriteOff(RuntimeFixture.CreateInitialState(), "log_03");
        var armed = Assert.IsType<ContainmentStableIntervalArmed>(Advance.Advance(weightOne, ServerTick.From(10), fixture.Shift.Containment, fixture.Anomalies));

        Assert.Equal(ContainmentState.STABLE, armed.State.Containment.State);
        Assert.Equal(ServerTick.From(10), armed.State.Containment.EnteredAt);
        Assert.Equal(ServerTick.From(100), armed.State.Containment.DeadlineAt);
        Assert.Equal(weightOne.StateVersion.Next(), armed.State.StateVersion);

        var weightTwo = WriteOff(armed.State, "log_06");
        var unchanged = Assert.IsType<ContainmentAdvanceNoChange>(Advance.Advance(weightTwo, ServerTick.From(11), fixture.Shift.Containment, fixture.Anomalies));
        Assert.Same(weightTwo, unchanged.State);
        Assert.Equal(ServerTick.From(100), unchanged.State.Containment.DeadlineAt);
    }

    [Fact]
    public void Weight_one_two_and_three_plus_select_the_closed_p0_intervals()
    {
        var fixture = Fixture.LoadP0();

        Assert.Equal(ServerTick.From(100), Arm(WriteOff(RuntimeFixture.CreateInitialState(), "log_03"), fixture).Containment.DeadlineAt);
        Assert.Equal(ServerTick.From(85), Arm(WriteOff(WriteOff(RuntimeFixture.CreateInitialState(), "log_03"), "log_06"), fixture).Containment.DeadlineAt);
        Assert.Equal(ServerTick.From(70), Arm(WriteOff(WriteOff(WriteOff(RuntimeFixture.CreateInitialState(), "log_03"), "log_06"), "log_08"), fixture).Containment.DeadlineAt);
    }

    [Fact]
    public void State_cycle_uses_stored_deadlines_and_emits_one_exact_incident_descriptor()
    {
        var fixture = Fixture.LoadP0();
        var armed = Arm(WriteOff(RuntimeFixture.CreateInitialState(), "log_03"), fixture);

        var beforeDue = Assert.IsType<ContainmentAdvanceNoChange>(Advance.Advance(armed, ServerTick.From(99), fixture.Shift.Containment, fixture.Anomalies));
        Assert.Same(armed, beforeDue.State);

        var request = Assert.IsType<ContainmentStateAdvanced>(Advance.Advance(armed, ServerTick.From(100), fixture.Shift.Containment, fixture.Anomalies)).State;
        Assert.Equal(ContainmentState.SERVICE_REQUESTED, request.Containment.State);
        Assert.Equal(ServerTick.From(100), request.Containment.EnteredAt);
        Assert.Equal(ServerTick.From(120), request.Containment.DeadlineAt);

        var overdue = Assert.IsType<ContainmentStateAdvanced>(Advance.Advance(request, ServerTick.From(120), fixture.Shift.Containment, fixture.Anomalies)).State;
        Assert.Equal(ContainmentState.OVERDUE, overdue.Containment.State);
        Assert.Equal(ServerTick.From(120), overdue.Containment.EnteredAt);
        Assert.Equal(ServerTick.From(130), overdue.Containment.DeadlineAt);

        var incident = Assert.IsType<ContainmentIncidentEntered>(Advance.Advance(overdue, ServerTick.From(130), fixture.Shift.Containment, fixture.Anomalies));
        Assert.Equal(ContainmentState.INCIDENT, incident.State.Containment.State);
        Assert.Null(incident.State.Containment.DeadlineAt);
        Assert.Equal("forced_line_pause", incident.Descriptor.Type);
        Assert.Equal(8, incident.Descriptor.Duration.Value);
        Assert.Equal(ServerTick.From(130), incident.Descriptor.TriggeredAt);
        Assert.Equal(overdue.StateVersion.Next(), incident.State.StateVersion);

        var repeated = Assert.IsType<ContainmentAdvanceNoChange>(Advance.Advance(incident.State, ServerTick.From(130), fixture.Shift.Containment, fixture.Anomalies));
        Assert.Same(incident.State, repeated.State);
    }

    [Fact]
    public void Late_calls_advance_one_boundary_per_call_using_deadlines_not_caller_tick()
    {
        var fixture = Fixture.LoadP0();
        var state = Arm(WriteOff(RuntimeFixture.CreateInitialState(), "log_03"), fixture);

        var request = Assert.IsType<ContainmentStateAdvanced>(Advance.Advance(state, ServerTick.From(135), fixture.Shift.Containment, fixture.Anomalies)).State;
        Assert.Equal((ContainmentState.SERVICE_REQUESTED, ServerTick.From(100), ServerTick.From(120)), (request.Containment.State, request.Containment.EnteredAt, request.Containment.DeadlineAt));

        var overdue = Assert.IsType<ContainmentStateAdvanced>(Advance.Advance(request, ServerTick.From(135), fixture.Shift.Containment, fixture.Anomalies)).State;
        Assert.Equal((ContainmentState.OVERDUE, ServerTick.From(120), ServerTick.From(130)), (overdue.Containment.State, overdue.Containment.EnteredAt, overdue.Containment.DeadlineAt));

        var incident = Assert.IsType<ContainmentIncidentEntered>(Advance.Advance(overdue, ServerTick.From(135), fixture.Shift.Containment, fixture.Anomalies));
        Assert.Equal(ServerTick.From(130), incident.Descriptor.TriggeredAt);
    }

    [Fact]
    public void Ritual_start_rejects_stable_and_starts_once_in_all_active_containment_states()
    {
        var fixture = Fixture.LoadP0();
        var stable = RuntimeFixture.CreateInitialState();
        var rejected = Assert.IsType<ContainmentRitualStartRejected>(StartRitual.Start(stable, ServerTick.From(10), fixture.Shift.Containment));
        Assert.Equal(ContainmentRitualStartRejectionReason.NO_ACTIVE_REQUEST, rejected.Reason);
        Assert.Same(stable, rejected.State);

        var request = ToRequest(fixture);
        var started = Assert.IsType<ContainmentRitualStarted>(StartRitual.Start(request, ServerTick.From(110), fixture.Shift.Containment));
        Assert.Equal(ServerTick.From(110), started.Ritual.StartedAt);
        Assert.Equal(ServerTick.From(114), started.Ritual.DueAt);
        Assert.Equal(request.Containment, started.State.Containment);
        Assert.Equal(request.StateVersion.Next(), started.State.StateVersion);

        var second = Assert.IsType<ContainmentRitualStartRejected>(StartRitual.Start(started.State, ServerTick.From(111), fixture.Shift.Containment));
        Assert.Equal(ContainmentRitualStartRejectionReason.RITUAL_ALREADY_ACTIVE, second.Reason);
        Assert.Same(started.State, second.State);

        var overdue = Assert.IsType<ContainmentStateAdvanced>(Advance.Advance(request, ServerTick.From(120), fixture.Shift.Containment, fixture.Anomalies)).State;
        Assert.IsType<ContainmentRitualStarted>(StartRitual.Start(overdue, ServerTick.From(121), fixture.Shift.Containment));
        var incident = Assert.IsType<ContainmentIncidentEntered>(Advance.Advance(overdue, ServerTick.From(130), fixture.Shift.Containment, fixture.Anomalies)).State;
        Assert.IsType<ContainmentRitualStarted>(StartRitual.Start(incident, ServerTick.From(131), fixture.Shift.Containment));
    }

    [Fact]
    public void Ritual_due_completion_and_cancellation_are_atomic_and_once_only()
    {
        var fixture = Fixture.LoadP0();
        var request = ToRequest(fixture);
        var started = Assert.IsType<ContainmentRitualStarted>(StartRitual.Start(request, ServerTick.From(110), fixture.Shift.Containment));

        var early = Assert.IsType<ContainmentRitualNotDue>(CompleteRitual.CompleteDue(started.State, ServerTick.From(113), fixture.Shift.Containment, fixture.Anomalies));
        Assert.Same(started.State, early.State);
        var complete = Assert.IsType<ContainmentRitualCompleted>(CompleteRitual.CompleteDue(started.State, ServerTick.From(114), fixture.Shift.Containment, fixture.Anomalies));
        Assert.Null(complete.State.ActiveContainmentRitual);
        Assert.Equal((ContainmentState.STABLE, ServerTick.From(114), ServerTick.From(204)), (complete.State.Containment.State, complete.State.Containment.EnteredAt, complete.State.Containment.DeadlineAt));
        Assert.Equal(started.State.StateVersion.Next(), complete.State.StateVersion);
        Assert.IsType<ContainmentRitualNoActive>(CompleteRitual.CompleteDue(complete.State, ServerTick.From(115), fixture.Shift.Containment, fixture.Anomalies));

        var lateStarted = Assert.IsType<ContainmentRitualStarted>(StartRitual.Start(ToRequest(fixture), ServerTick.From(110), fixture.Shift.Containment));
        var lateComplete = Assert.IsType<ContainmentRitualCompleted>(CompleteRitual.CompleteDue(lateStarted.State, ServerTick.From(119), fixture.Shift.Containment, fixture.Anomalies));
        Assert.Equal(ServerTick.From(209), lateComplete.State.Containment.DeadlineAt);

        var restart = Assert.IsType<ContainmentRitualStarted>(StartRitual.Start(request, ServerTick.From(111), fixture.Shift.Containment));
        var cancelled = Assert.IsType<ContainmentRitualCancelled>(CancelRitual.Cancel(restart.State));
        Assert.Null(cancelled.State.ActiveContainmentRitual);
        Assert.Equal(request.Containment, cancelled.State.Containment);
        var noActive = Assert.IsType<ContainmentRitualCancellationRejected>(CancelRitual.Cancel(cancelled.State));
        Assert.Same(cancelled.State, noActive.State);
    }

    [Fact]
    public void Due_ritual_wins_same_tick_priority_and_active_ritual_survives_escalation_until_due()
    {
        var fixture = Fixture.LoadP0();
        var request = ToRequest(fixture);
        var startsAtRequestDeadline = Assert.IsType<ContainmentRitualStarted>(StartRitual.Start(request, ServerTick.From(120), fixture.Shift.Containment));
        var overdue = Assert.IsType<ContainmentStateAdvanced>(Advance.Advance(startsAtRequestDeadline.State, ServerTick.From(120), fixture.Shift.Containment, fixture.Anomalies)).State;
        Assert.Equal(ContainmentState.OVERDUE, overdue.Containment.State);
        Assert.NotNull(overdue.ActiveContainmentRitual);

        var separateOverdue = Assert.IsType<ContainmentStateAdvanced>(Advance.Advance(ToRequest(fixture), ServerTick.From(120), fixture.Shift.Containment, fixture.Anomalies)).State;
        var startsAtOverdueDeadline = Assert.IsType<ContainmentRitualStarted>(StartRitual.Start(separateOverdue, ServerTick.From(130), fixture.Shift.Containment));
        var incident = Assert.IsType<ContainmentIncidentEntered>(Advance.Advance(startsAtOverdueDeadline.State, ServerTick.From(130), fixture.Shift.Containment, fixture.Anomalies)).State;
        Assert.NotNull(incident.ActiveContainmentRitual);

        var priority = Assert.IsType<ContainmentRitualCompletionRequired>(Advance.Advance(startsAtRequestDeadline.State, ServerTick.From(124), fixture.Shift.Containment, fixture.Anomalies));
        Assert.Same(startsAtRequestDeadline.State, priority.State);
        var complete = Assert.IsType<ContainmentRitualCompleted>(CompleteRitual.CompleteDue(priority.State, ServerTick.From(124), fixture.Shift.Containment, fixture.Anomalies));
        Assert.Equal(ContainmentState.STABLE, complete.State.Containment.State);
    }

    [Fact]
    public void Incident_can_resolve_then_emit_one_new_descriptor_only_after_a_later_cycle()
    {
        var fixture = Fixture.LoadP0();
        var incident = ToIncident(fixture);
        var ritual = Assert.IsType<ContainmentRitualStarted>(StartRitual.Start(incident, ServerTick.From(130), fixture.Shift.Containment));
        var resolved = Assert.IsType<ContainmentRitualCompleted>(CompleteRitual.CompleteDue(ritual.State, ServerTick.From(134), fixture.Shift.Containment, fixture.Anomalies)).State;
        Assert.Equal(ServerTick.From(224), resolved.Containment.DeadlineAt);

        var request = Assert.IsType<ContainmentStateAdvanced>(Advance.Advance(resolved, ServerTick.From(224), fixture.Shift.Containment, fixture.Anomalies)).State;
        var overdue = Assert.IsType<ContainmentStateAdvanced>(Advance.Advance(request, ServerTick.From(244), fixture.Shift.Containment, fixture.Anomalies)).State;
        var next = Assert.IsType<ContainmentIncidentEntered>(Advance.Advance(overdue, ServerTick.From(254), fixture.Shift.Containment, fixture.Anomalies));
        Assert.Equal(ServerTick.From(254), next.Descriptor.TriggeredAt);
    }

    [Fact]
    public void Containment_mutations_preserve_existing_runtime_data_and_participate_in_value_equality()
    {
        var fixture = Fixture.LoadP0();
        var state = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_10");
        var completedConfirmationStart = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(state, LogId.From("log_10"), ImmutableHashSet.Create(ItemId.From("choir_cassette")), ServerTick.From(1), LineNoise.QUIET, fixture.Anomalies));
        var completedConfirmation = Assert.IsType<ConfirmationTestDueCompleted>(new ConfirmationTestDueCompletionService().CompleteDue(completedConfirmationStart.State, ServerTick.From(5), fixture.Anomalies));
        state = RuntimeFixture.MoveHost(completedConfirmation.State, "log_10", LogState.QUEUED_FOR_SAW);
        state = WriteOff(state, "log_08");
        state = RuntimeFixture.MoveToIntake(state, "log_03");
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_PROCEDURE);
        var procedure = Assert.IsType<ProcedureActionHoldStarted>(new ProcedureActionStartService().Start(state, LogId.From("log_03"), ItemId.From("holy_water"), ServerTick.From(10), fixture.Anomalies));
        state = procedure.State;
        state = RuntimeFixture.MoveToIntake(state, "log_06");
        var confirmation = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(state, LogId.From("log_06"), ImmutableHashSet.Create(ItemId.From("choir_cassette")), ServerTick.From(11), LineNoise.QUIET, fixture.Anomalies));

        var armed = Arm(confirmation.State, fixture);
        Assert.Equal(procedure.Hold, armed.ActiveProcedureHold);
        Assert.NotNull(armed.ActiveConfirmationTest);
        Assert.True(armed.TryGetConfirmationResult(LogId.From("log_10"), out var completedResult));
        Assert.Equal(ServerTick.From(5), completedResult.CompletedAt);
        Assert.True(armed.TryGetLog(LogId.From("log_08"), out var writtenOff));
        Assert.Equal(LogState.HELD_WRITTEN_OFF, writtenOff.State);

        var independent = Arm(confirmation.State, fixture);
        Assert.True(armed.ValueEquals(independent));
        var ritual = Assert.IsType<ContainmentRitualStarted>(StartRitual.Start(ToRequest(fixture), ServerTick.From(110), fixture.Shift.Containment));
        Assert.False(armed.ValueEquals(ritual.State));
    }

    [Fact]
    public void Invalid_catalog_and_checked_timing_overflow_fail_loudly_without_mutating_state()
    {
        var fixture = Fixture.LoadP0();
        var state = WriteOff(RuntimeFixture.CreateInitialState(), "log_03");
        var missing = new AnomalyCatalog(ImmutableDictionary<AnomalyId, AnomalyDefinition>.Empty);
        Assert.Throws<InvalidOperationException>(() => DangerWeightResolver.Resolve(state, missing));

        var overlargeDefinition = fixture.Anomalies.Definitions[AnomalyId.From("PENITENT_TRUNK")] with { DangerWeight = int.MaxValue };
        var overlargeCatalog = new AnomalyCatalog(fixture.Anomalies.Definitions.SetItem(AnomalyId.From("PENITENT_TRUNK"), overlargeDefinition));
        var twoPenitents = WriteOff(state, "log_08");
        Assert.Throws<OverflowException>(() => DangerWeightResolver.Resolve(twoPenitents, overlargeCatalog));

        var mismatchedDefinition = fixture.Anomalies.Definitions[AnomalyId.From("PENITENT_TRUNK")] with { Id = AnomalyId.From("RESIN_BLASPHEMER") };
        var mismatchedCatalog = new AnomalyCatalog(fixture.Anomalies.Definitions.SetItem(AnomalyId.From("PENITENT_TRUNK"), mismatchedDefinition));
        Assert.Throws<InvalidOperationException>(() => DangerWeightResolver.Resolve(state, mismatchedCatalog));
        Assert.Equal(1, DangerWeightResolver.Resolve(state, fixture.Anomalies));

        var request = ToRequest(fixture);
        Assert.Throws<OverflowException>(() => StartRitual.Start(request, ServerTick.From(long.MaxValue - 1), fixture.Shift.Containment));
        Assert.Throws<ArgumentOutOfRangeException>(() => Advance.Advance(request, ServerTick.From(99), fixture.Shift.Containment, fixture.Anomalies));
    }

    private static ShiftRuntimeState Arm(ShiftRuntimeState state, ValidatedConfiguration fixture) =>
        Assert.IsType<ContainmentStableIntervalArmed>(Advance.Advance(state, ServerTick.From(10), fixture.Shift.Containment, fixture.Anomalies)).State;

    private static ShiftRuntimeState ToRequest(ValidatedConfiguration fixture) =>
        Assert.IsType<ContainmentStateAdvanced>(Advance.Advance(Arm(WriteOff(RuntimeFixture.CreateInitialState(), "log_03"), fixture), ServerTick.From(100), fixture.Shift.Containment, fixture.Anomalies)).State;

    private static ShiftRuntimeState ToIncident(ValidatedConfiguration fixture)
    {
        var request = ToRequest(fixture);
        var overdue = Assert.IsType<ContainmentStateAdvanced>(Advance.Advance(request, ServerTick.From(120), fixture.Shift.Containment, fixture.Anomalies)).State;
        return Assert.IsType<ContainmentIncidentEntered>(Advance.Advance(overdue, ServerTick.From(130), fixture.Shift.Containment, fixture.Anomalies)).State;
    }

    private static ShiftRuntimeState WriteOff(ShiftRuntimeState state, string logId)
    {
        state = RuntimeFixture.MoveToIntake(state, logId);
        return RuntimeFixture.MoveHost(state, logId, LogState.HELD_WRITTEN_OFF);
    }
}
