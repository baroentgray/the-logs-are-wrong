using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Tests.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Scheduler;

[Trait("Scope", "TLAW-022")]
public sealed class SawCycleTests
{
    [Fact]
    public void Active_cycle_is_immutable_and_derives_its_due_tick()
    {
        var cycle = new ActiveSawCycle(LogId.From("log_01"), ServerTick.From(10), SimulationDuration.FromTicks(6));

        Assert.Equal(ServerTick.From(16), cycle.DueAt);
        Assert.DoesNotContain(typeof(ActiveSawCycle).GetProperties(), property => property.SetMethod is not null);
        Assert.Throws<ArgumentException>(() => new ActiveSawCycle(default, ServerTick.Zero, SimulationDuration.FromTicks(1)));
        Assert.Throws<ArgumentException>(() => new ActiveSawCycle(LogId.From("log_01"), default, SimulationDuration.FromTicks(1)));
        Assert.Throws<ArgumentException>(() => new ActiveSawCycle(LogId.From("log_01"), ServerTick.Zero, SimulationDuration.Zero));
        Assert.Throws<OverflowException>(() => new ActiveSawCycle(LogId.From("log_01"), ServerTick.From(long.MaxValue), SimulationDuration.FromTicks(1)));
    }

    [Fact]
    public void Start_moves_the_only_queued_owner_to_saw_in_one_version_step_using_configured_duration()
    {
        var fixture = Fixture.LoadP0();
        var before = Queued("log_01");
        var result = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(before, ServerTick.From(20), fixture.Shift.Scheduler));

        Assert.Equal((LogId.From("log_01"), ServerTick.From(20), 6L, ServerTick.From(26)), (result.Cycle.LogId, result.Cycle.StartedAt, result.Cycle.Duration.Value, result.Cycle.DueAt));
        Assert.Equal((before.StateVersion, before.StateVersion.Next(), before.StateVersion.Next()), (result.PriorStateVersion, result.CurrentStateVersion, result.State.StateVersion));
        Assert.Equal(LogState.IN_SAW, Log(result.State, "log_01").State);
        Assert.Same(result.Cycle, result.State.ActiveSawCycle);
        Assert.Equal(LogState.SCHEDULED, Log(result.State, "log_02").State);
    }

    [Fact]
    public void Start_is_source_derived_and_retries_or_missing_owners_do_not_mutate()
    {
        var service = new SawCycleStartService();
        var configuration = Fixture.LoadP0().Shift.Scheduler;
        var noOwner = ShiftRuntimeState.Create(Fixture.LoadP0().Shift);
        var none = Assert.IsType<SawCycleStartNoQueuedOwner>(service.Start(noOwner, ServerTick.Zero, configuration));
        Assert.Same(noOwner, none.State);

        var started = Assert.IsType<SawCycleStarted>(service.Start(Queued("log_01"), ServerTick.From(4), configuration));
        var retry = Assert.IsType<SawCycleStartAlreadyActive>(service.Start(started.State, ServerTick.From(5), configuration));
        Assert.Same(started.State, retry.State);
        Assert.Same(started.Cycle, retry.Cycle);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Start(noOwner, default, configuration));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Start(noOwner, ServerTick.Zero, configuration with { SawCycleSeconds = 0 }));
    }

    [Fact]
    public void Completion_is_due_driven_returns_resolution_and_changes_only_owner_and_active_cycle()
    {
        var fixture = Fixture.LoadP0();
        var started = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(Queued("log_01"), ServerTick.From(10), fixture.Shift.Scheduler));
        var service = new SawCycleCompletionService();

        var early = Assert.IsType<SawCycleNotDue>(service.Complete(started.State, ServerTick.From(15), fixture.Anomalies));
        Assert.Same(started.State, early.State);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Complete(started.State, ServerTick.From(9), fixture.Anomalies));

        var completed = Assert.IsType<SawCycleCompleted>(service.Complete(started.State, ServerTick.From(16), fixture.Anomalies));
        Assert.Equal((LogId.From("log_01"), false, (bool?)null, LogState.PROCESSED, 1, 0), (completed.Resolution.LogId, completed.Resolution.IsAnomalous, completed.Resolution.AllRequiredFlagsPresent, completed.Resolution.TerminalState, completed.Resolution.Settlement.CreditedUnits, completed.Resolution.Settlement.CorrectAnomalyDelta));
        Assert.Equal((started.State.StateVersion, started.State.StateVersion.Next(), started.State.StateVersion.Next()), (completed.PriorStateVersion, completed.CurrentStateVersion, completed.State.StateVersion));
        Assert.Equal(LogState.PROCESSED, Log(completed.State, "log_01").State);
        Assert.Null(completed.State.ActiveSawCycle);
        Assert.True(started.State.Inventory.ValueEquals(completed.State.Inventory));
    }

    [Fact]
    public void Completion_resolves_anomalies_without_applying_their_settlement_or_effects()
    {
        var fixture = Fixture.LoadP0();
        var started = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(Queued("log_03"), ServerTick.Zero, fixture.Shift.Scheduler));
        var completed = Assert.IsType<SawCycleCompleted>(new SawCycleCompletionService().Complete(started.State, ServerTick.From(6), fixture.Anomalies));

        Assert.True(completed.Resolution.IsAnomalous);
        Assert.False(completed.Resolution.AllRequiredFlagsPresent);
        Assert.NotEmpty(completed.Resolution.Effects);
        Assert.True(started.State.Inventory.ValueEquals(completed.State.Inventory));
        Assert.Equal(started.State.Containment, completed.State.Containment);
    }

    [Fact]
    public void Completion_without_an_active_cycle_is_a_reference_no_op_and_repeat_is_safe()
    {
        var state = ShiftRuntimeState.Create(Fixture.LoadP0().Shift);
        var service = new SawCycleCompletionService();
        var first = Assert.IsType<SawCycleNoActive>(service.Complete(state, ServerTick.Zero, Fixture.LoadP0().Anomalies));
        var second = Assert.IsType<SawCycleNoActive>(service.Complete(first.State, ServerTick.From(1), Fixture.LoadP0().Anomalies));

        Assert.Same(state, first.State);
        Assert.Same(state, second.State);
    }

    [Fact]
    public void Start_rejects_overflow_and_in_saw_without_an_active_cycle_before_mutation()
    {
        var service = new SawCycleStartService();
        var configuration = Fixture.LoadP0().Shift.Scheduler;
        var queued = Queued("log_01");
        var noOwner = ShiftRuntimeState.Create(Fixture.LoadP0().Shift);

        Assert.Throws<OverflowException>(() => service.Start(queued, ServerTick.From(long.MaxValue), configuration));
        Assert.Same(noOwner, Assert.IsType<SawCycleStartNoQueuedOwner>(service.Start(noOwner, ServerTick.Zero, configuration)).State);

        var inSaw = RuntimeFixture.MoveHost(queued, "log_01", LogState.IN_SAW);
        Assert.Throws<InvalidOperationException>(() => service.Start(inSaw, ServerTick.From(1), configuration));
        Assert.Equal(LogState.IN_SAW, Log(inSaw, "log_01").State);
        Assert.Null(inSaw.ActiveSawCycle);
    }

    [Fact]
    public void Start_uses_only_saw_cycle_seconds_and_does_not_depend_on_line_state()
    {
        var fixture = Fixture.LoadP0();
        var queued = Queued("log_01");
        var jammed = CloneWith(queued, nameof(ShiftRuntimeState.Line), new TheLogsAreWrong.Domain.Line.LineRuntimeState(TheLogsAreWrong.Domain.Enums.LineState.LINE_JAMMED, ServerTick.Zero, JamCause.FEED_GATE_BLOCKED, LogId.From("log_01"), null));
        var configuration = fixture.Shift.Scheduler with { SawCycleSeconds = 9, NormalFeedDelaySeconds = 999, RepairHoldSeconds = 999 };

        var started = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(jammed, ServerTick.From(3), configuration));

        Assert.Equal((9L, ServerTick.From(12)), (started.Cycle.Duration.Value, started.Cycle.DueAt));
        Assert.Equal(jammed.Line, started.State.Line);
    }

    [Fact]
    public void Completion_accepts_exact_and_late_ticks_but_malformed_catalog_fails_without_mutation()
    {
        var fixture = Fixture.LoadP0();
        var exact = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(Queued("log_01"), ServerTick.Zero, fixture.Shift.Scheduler));
        var late = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(Queued("log_01"), ServerTick.Zero, fixture.Shift.Scheduler));
        var service = new SawCycleCompletionService();

        Assert.Equal(ServerTick.From(6), Assert.IsType<SawCycleCompleted>(service.Complete(exact.State, ServerTick.From(6), fixture.Anomalies)).CompletedAt);
        Assert.Equal(ServerTick.From(7), Assert.IsType<SawCycleCompleted>(service.Complete(late.State, ServerTick.From(7), fixture.Anomalies)).CompletedAt);

        var anomalous = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(Queued("log_03"), ServerTick.Zero, fixture.Shift.Scheduler));
        Assert.Throws<AnomalyDefinitionNotFoundException>(() => service.Complete(anomalous.State, ServerTick.From(6), new AnomalyCatalog(ImmutableDictionary<AnomalyId, AnomalyDefinition>.Empty)));
        Assert.Same(anomalous.Cycle, anomalous.State.ActiveSawCycle);
        Assert.Equal(LogState.IN_SAW, Log(anomalous.State, "log_03").State);
    }

    [Fact]
    public void Same_tick_completion_and_next_start_are_separate_mutations()
    {
        var fixture = Fixture.LoadP0();
        var first = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(Queued("log_01"), ServerTick.Zero, fixture.Shift.Scheduler));
        var second = Log(first.State, "log_02");
        var secondQueuedLog = new LogRuntimeState(second.LogId, second.TrueSpecies, second.DeclaredSpecies, second.Anomaly, LogState.QUEUED_FOR_SAW, second.Flags);
        var withSecondQueued = CloneWith(first.State, nameof(ShiftRuntimeState.Logs), first.State.Logs.SetItem(1, secondQueuedLog));

        var completed = Assert.IsType<SawCycleCompleted>(new SawCycleCompletionService().Complete(withSecondQueued, ServerTick.From(6), fixture.Anomalies));
        var next = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(completed.State, ServerTick.From(6), fixture.Shift.Scheduler));

        Assert.Equal(withSecondQueued.StateVersion.Next(), completed.State.StateVersion);
        Assert.Equal(completed.State.StateVersion.Next(), next.State.StateVersion);
        Assert.Equal((LogState.PROCESSED, LogState.IN_SAW), (Log(next.State, "log_01").State, Log(next.State, "log_02").State));
        Assert.Equal(LogId.From("log_02"), next.Cycle.LogId);
    }

    [Fact]
    public void Deterministic_independent_runs_have_equal_runtime_state_and_descriptor()
    {
        var configuration = Fixture.LoadP0().Shift.Scheduler;
        var first = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(Queued("log_01"), ServerTick.From(2), configuration));
        var second = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(Queued("log_01"), ServerTick.From(2), configuration));

        Assert.Equal(first.Cycle, second.Cycle);
        Assert.Equal((first.PriorStateVersion, first.CurrentStateVersion), (second.PriorStateVersion, second.CurrentStateVersion));
        Assert.True(first.State.ValueEquals(second.State));
    }

    [Fact]
    public void Start_and_completion_commit_as_separate_journal_mutations_while_no_ops_are_pure()
    {
        var fixture = Fixture.LoadP0();
        var before = Queued("log_01");
        var journal = AlignedJournal(before, ServerTick.Zero);
        var commits = new JournaledMutationCommitService();
        var started = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(before, ServerTick.Zero, fixture.Shift.Scheduler));
        var startCommit = Assert.IsType<JournaledMutationCommitted>(commits.Commit(journal, before, started.State, ServerTick.Zero, Draft("start")));
        var completed = Assert.IsType<SawCycleCompleted>(new SawCycleCompletionService().Complete(started.State, ServerTick.From(6), fixture.Anomalies));
        var completionCommit = Assert.IsType<JournaledMutationCommitted>(commits.Commit(journal, started.State, completed.State, ServerTick.From(6), Draft("complete")));

        Assert.True(startCommit.Envelope.Sequence < completionCommit.Envelope.Sequence);
        Assert.Equal(completed.State.StateVersion, journal.LastStateVersion);

        var noOwner = ShiftRuntimeState.Create(fixture.Shift);
        var noOp = Assert.IsType<SawCycleStartNoQueuedOwner>(new SawCycleStartService().Start(noOwner, ServerTick.Zero, fixture.Shift.Scheduler));
        Assert.Same(noOwner, noOp.State);
        var emptyJournal = new InMemoryEventJournal(noOwner.ShiftId);
        Assert.IsType<JournaledMutationCommitRejected>(commits.Commit(emptyJournal, noOp.State, noOp.State, ServerTick.Zero, Draft("noop")));
        Assert.Empty(emptyJournal.Events);
    }

    private static ShiftRuntimeState Queued(string logId)
    {
        var state = ShiftRuntimeState.Create(Fixture.LoadP0().Shift);
        state = RuntimeFixture.MoveHost(state, logId, LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, logId, LogState.AT_INTAKE);
        return RuntimeFixture.MoveHost(state, logId, LogState.QUEUED_FOR_SAW);
    }

    private static LogRuntimeState Log(ShiftRuntimeState state, string logId)
    {
        Assert.True(state.TryGetLog(LogId.From(logId), out var log));
        return log;
    }

    private static T CloneWith<T>(T source, string name, object? value) where T : class
    {
        var clone = Assert.IsType<T>(typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(source, null));
        FindField(typeof(T), name).SetValue(clone, value);
        return clone;
    }

    private static FieldInfo FindField(Type type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? current.GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null) return field;
        }

        throw new MissingFieldException(type.FullName, name);
    }

    private static InMemoryEventJournal AlignedJournal(ShiftRuntimeState state, ServerTick tick)
    {
        var journal = new InMemoryEventJournal(state.ShiftId);
        for (var version = 1L; version <= state.StateVersion.Value; version++)
        {
            journal.Append(new EventEnvelope
            {
                ShiftId = state.ShiftId,
                EventId = EventId.From($"seed_{version}"),
                Sequence = EventSequence.From(version),
                ServerTick = tick,
                StateVersionAfter = StateVersion.From(version),
                EventType = EventTypeId.From("test.tlaw022.seed"),
                Payload = new SawPayload($"seed_{version}")
            });
        }

        return journal;
    }

    private static DomainEventDraft Draft(string id) => new(EventId.From($"tlaw022_{id}"), EventTypeId.From("test.tlaw022.saw"), new SawPayload(id));

    private sealed record SawPayload(string Value) : IDomainEventPayload;
}
