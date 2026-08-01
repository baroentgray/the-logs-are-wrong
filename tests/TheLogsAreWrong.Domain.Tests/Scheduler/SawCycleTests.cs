using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
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

    [Fact]
    public void Public_input_guards_reject_before_any_state_bearing_mutation()
    {
        var fixture = Fixture.LoadP0();
        var start = new SawCycleStartService();
        var complete = new SawCycleCompletionService();
        var queued = Queued("log_01");
        var started = Assert.IsType<SawCycleStarted>(start.Start(queued, ServerTick.From(10), fixture.Shift.Scheduler));

        Assert.Throws<ArgumentNullException>(() => start.Start(null!, ServerTick.Zero, fixture.Shift.Scheduler));
        Assert.Throws<ArgumentNullException>(() => start.Start(queued, ServerTick.Zero, null!));
        AssertRejectPreserves(queued, () => start.Start(queued, default, fixture.Shift.Scheduler));
        AssertRejectPreserves(queued, () => start.Start(queued, ServerTick.Zero, fixture.Shift.Scheduler with { SawCycleSeconds = 0 }));
        AssertRejectPreserves(queued, () => start.Start(queued, ServerTick.Zero, fixture.Shift.Scheduler with { SawCycleSeconds = -1 }));
        AssertRejectPreserves(queued, () => start.Start(queued, ServerTick.From(long.MaxValue), fixture.Shift.Scheduler));

        Assert.Throws<ArgumentNullException>(() => complete.Complete(null!, ServerTick.Zero, fixture.Anomalies));
        Assert.Throws<ArgumentNullException>(() => complete.Complete(started.State, ServerTick.Zero, null!));
        AssertRejectPreserves(started.State, () => complete.Complete(started.State, default, fixture.Anomalies));
        AssertRejectPreserves(started.State, () => complete.Complete(started.State, ServerTick.From(9), fixture.Anomalies));
    }

    [Fact]
    public void Start_preserves_clear_jammed_and_repairing_line_values_exactly()
    {
        var fixture = Fixture.LoadP0();
        var clear = Queued("log_01");
        var jammedLine = new LineRuntimeState(LineState.LINE_JAMMED, ServerTick.From(3), JamCause.FEED_GATE_BLOCKED, LogId.From("log_02"), null);
        var repairingLine = new LineRuntimeState(
            LineState.REPAIRING,
            ServerTick.From(4),
            JamCause.INTAKE_AUTOFEED_BLOCKED,
            LogId.From("log_02"),
            new ActiveRepairHold(ServerTick.From(4), ServerTick.From(7), SimulationDuration.FromTicks(3)));
        var inputs = new[]
        {
            clear,
            CloneWith(clear, nameof(ShiftRuntimeState.Line), jammedLine),
            CloneWith(clear, nameof(ShiftRuntimeState.Line), repairingLine)
        };

        foreach (var before in inputs)
        {
            var started = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(before, ServerTick.From(10), fixture.Shift.Scheduler));

            Assert.Equal(before.Line, started.State.Line);
            Assert.Equal((before.Line.State, before.Line.Cause, before.Line.PendingLogId, before.Line.ActiveRepairHold),
                (started.State.Line.State, started.State.Line.Cause, started.State.Line.PendingLogId, started.State.Line.ActiveRepairHold));
        }
    }

    [Fact]
    public void Defensive_reflection_only_saw_shapes_fail_closed_without_mutating_the_fixture()
    {
        var fixture = Fixture.LoadP0();
        var start = new SawCycleStartService();
        var complete = new SawCycleCompletionService();
        var queued = Queued("log_01");
        var noActiveInSaw = RuntimeFixture.MoveHost(queued, "log_01", LogState.IN_SAW);
        var active = Assert.IsType<SawCycleStarted>(start.Start(Queued("log_01"), ServerTick.From(10), fixture.Shift.Scheduler));
        var ownerNotInSaw = ReplaceLogState(active.State, "log_01", LogState.QUEUED_FOR_SAW);
        var ownerMismatch = CloneWith(active.State, nameof(ShiftRuntimeState.ActiveSawCycle), new ActiveSawCycle(LogId.From("log_02"), ServerTick.From(10), SimulationDuration.FromTicks(6)));
        var multipleInSaw = ReplaceLogState(active.State, "log_02", LogState.IN_SAW);
        var multipleQueued = ReplaceLogState(Queued("log_01"), "log_02", LogState.QUEUED_FOR_SAW);
        var overCapacityQueue = ReplaceLogState(ReplaceLogState(active.State, "log_02", LogState.QUEUED_FOR_SAW), "log_03", LogState.QUEUED_FOR_SAW);
        var missingOwner = RemoveLog(active.State, "log_01");

        foreach (var malformed in new[] { noActiveInSaw, ownerNotInSaw, ownerMismatch, multipleInSaw, missingOwner })
        {
            AssertRejectPreserves(malformed, () => complete.Complete(malformed, ServerTick.From(16), fixture.Anomalies));
        }

        foreach (var malformed in new[] { noActiveInSaw, ownerNotInSaw, ownerMismatch, multipleInSaw, multipleQueued, overCapacityQueue, missingOwner })
        {
            AssertRejectPreserves(malformed, () => start.Start(malformed, ServerTick.From(16), fixture.Shift.Scheduler));
        }
    }

    [Theory]
    [InlineData("log_01", false)]
    [InlineData("log_03", false)]
    [InlineData("log_03", true)]
    [InlineData("log_06", false)]
    [InlineData("log_06", true)]
    [InlineData("log_05", false)]
    [InlineData("log_05", true)]
    public void Completion_returns_the_established_full_processing_resolution_without_settlement_execution(string logId, bool completeProcedure)
    {
        var fixture = Fixture.LoadP0();
        var before = Queued(logId);
        if (completeProcedure)
        {
            var log = Log(before, logId);
            Assert.True(new ProcedurePlanResolver(fixture.Anomalies).TryGetPlan(log, out var plan));
            var prepared = new LogRuntimeState(log.LogId, log.TrueSpecies, log.DeclaredSpecies, log.Anomaly, log.State, plan!.GrantedFlags);
            before = CloneWith(before, nameof(ShiftRuntimeState.Logs), before.Logs.SetItem(LogIndex(before, logId), prepared));
        }

        var expected = AnomalyProcessingResolver.Resolve(Log(before, logId), fixture.Anomalies);
        var started = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(before, ServerTick.From(10), fixture.Shift.Scheduler));
        var completed = Assert.IsType<SawCycleCompleted>(new SawCycleCompletionService().Complete(started.State, started.Cycle.DueAt, fixture.Anomalies));

        AssertResolution(expected, completed.Resolution);
        Assert.True(started.State.Inventory.ValueEquals(completed.State.Inventory));
        Assert.Equal(started.State.Containment, completed.State.Containment);
        Assert.Equal(started.State.Line, completed.State.Line);
        Assert.Equal(started.State.ProcessedIntentIds, completed.State.ProcessedIntentIds);
        Assert.Equal(started.State.PendingFeed, completed.State.PendingFeed);
        Assert.Equal(started.State.Logs.Where(log => log.LogId != LogId.From(logId)), completed.State.Logs.Where(log => log.LogId != LogId.From(logId)));
    }

    [Fact]
    public void Start_and_completion_preserve_the_unrelated_runtime_matrix_and_retries_are_reference_no_ops()
    {
        var fixture = Fixture.LoadP0();
        var state = RichQueuedState();
        var start = new SawCycleStartService();
        var started = Assert.IsType<SawCycleStarted>(start.Start(state, ServerTick.From(20), fixture.Shift.Scheduler));
        AssertPreservedUnrelated(state, started.State, "log_01", expectActiveSaw: true);
        Assert.Same(started.State, Assert.IsType<SawCycleStartAlreadyActive>(start.Start(started.State, ServerTick.From(21), fixture.Shift.Scheduler)).State);

        var early = Assert.IsType<SawCycleNotDue>(new SawCycleCompletionService().Complete(started.State, ServerTick.From(25), fixture.Anomalies));
        Assert.Same(started.State, early.State);
        var completed = Assert.IsType<SawCycleCompleted>(new SawCycleCompletionService().Complete(started.State, ServerTick.From(26), fixture.Anomalies));
        AssertPreservedUnrelated(started.State, completed.State, "log_01", expectActiveSaw: false);

        var repeated = Assert.IsType<SawCycleNoActive>(new SawCycleCompletionService().Complete(completed.State, ServerTick.From(27), fixture.Anomalies));
        Assert.Same(completed.State, repeated.State);
        Assert.Null(repeated.State.ActiveSawCycle);
    }

    [Fact]
    public void Journal_cursor_advances_once_per_start_completion_and_same_tick_next_start_only()
    {
        var fixture = Fixture.LoadP0();
        var firstQueued = Queued("log_01");
        var journal = AlignedJournal(firstQueued, ServerTick.Zero);
        var commits = new JournaledMutationCommitService();
        var start = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(firstQueued, ServerTick.Zero, fixture.Shift.Scheduler));
        var firstCommit = Commit(commits, journal, firstQueued, start.State, ServerTick.Zero, "start_one");
        var secondQueued = CloneWith(start.State, nameof(ShiftRuntimeState.Logs), start.State.Logs.SetItem(LogIndex(start.State, "log_02"), CopyWithState(Log(start.State, "log_02"), LogState.QUEUED_FOR_SAW)));
        var complete = Assert.IsType<SawCycleCompleted>(new SawCycleCompletionService().Complete(secondQueued, ServerTick.From(6), fixture.Anomalies));
        var secondCommit = Commit(commits, journal, secondQueued, complete.State, ServerTick.From(6), "complete_one");
        var next = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(complete.State, ServerTick.From(6), fixture.Shift.Scheduler));
        var thirdCommit = Commit(commits, journal, complete.State, next.State, ServerTick.From(6), "start_two");

        Assert.Equal(new[] { firstCommit.Envelope.Sequence, secondCommit.Envelope.Sequence, thirdCommit.Envelope.Sequence }, Enumerable.Range((int)firstCommit.Envelope.Sequence.Value, 3).Select(value => EventSequence.From((long)value)));
        Assert.Equal(new[] { firstCommit.Envelope.StateVersionAfter, secondCommit.Envelope.StateVersionAfter, thirdCommit.Envelope.StateVersionAfter }, new[] { start.State.StateVersion, complete.State.StateVersion, next.State.StateVersion });
        Assert.Equal(new[] { ServerTick.Zero, ServerTick.From(6), ServerTick.From(6) }, new[] { firstCommit.Envelope.ServerTick, secondCommit.Envelope.ServerTick, thirdCommit.Envelope.ServerTick });
        var cursor = (journal.LastSequence, journal.LastStateVersion, journal.Events.Count);
        Assert.Same(next.State, Assert.IsType<SawCycleStartAlreadyActive>(new SawCycleStartService().Start(next.State, ServerTick.From(6), fixture.Shift.Scheduler)).State);
        Assert.Same(next.State, Assert.IsType<SawCycleNotDue>(new SawCycleCompletionService().Complete(next.State, ServerTick.From(7), fixture.Anomalies)).State);
        Assert.Equal(cursor, (journal.LastSequence, journal.LastStateVersion, journal.Events.Count));
    }

    [Fact]
    public void Independent_runs_are_fully_equal_and_only_ticks_or_saw_seconds_change_timing()
    {
        var fixture = Fixture.LoadP0();
        var start = new SawCycleStartService();
        var completion = new SawCycleCompletionService();
        var firstStart = Assert.IsType<SawCycleStarted>(start.Start(Queued("log_05"), ServerTick.From(10), fixture.Shift.Scheduler));
        var secondStart = Assert.IsType<SawCycleStarted>(start.Start(Queued("log_05"), ServerTick.From(10), fixture.Shift.Scheduler));
        var firstComplete = Assert.IsType<SawCycleCompleted>(completion.Complete(firstStart.State, firstStart.Cycle.DueAt, fixture.Anomalies));
        var secondComplete = Assert.IsType<SawCycleCompleted>(completion.Complete(secondStart.State, secondStart.Cycle.DueAt, fixture.Anomalies));

        Assert.Equal(firstStart.Cycle, secondStart.Cycle);
        Assert.True(firstStart.State.ValueEquals(secondStart.State));
        AssertResolution(firstComplete.Resolution, secondComplete.Resolution);
        Assert.True(firstComplete.State.ValueEquals(secondComplete.State));

        var laterStart = Assert.IsType<SawCycleStarted>(start.Start(Queued("log_05"), ServerTick.From(11), fixture.Shift.Scheduler));
        var longerStart = Assert.IsType<SawCycleStarted>(start.Start(Queued("log_05"), ServerTick.From(10), fixture.Shift.Scheduler with { SawCycleSeconds = 9 }));
        var lateComplete = Assert.IsType<SawCycleCompleted>(completion.Complete(firstStart.State, ServerTick.From(17), fixture.Anomalies));

        Assert.Equal(firstStart.Cycle.Duration, laterStart.Cycle.Duration);
        Assert.Equal(firstStart.Cycle.DueAt.Value + 1, laterStart.Cycle.DueAt.Value);
        Assert.Equal((9L, ServerTick.From(19)), (longerStart.Cycle.Duration.Value, longerStart.Cycle.DueAt));
        Assert.Equal(ServerTick.From(17), lateComplete.CompletedAt);
        AssertResolution(firstComplete.Resolution, lateComplete.Resolution);
    }

    private static ShiftRuntimeState RichQueuedState()
    {
        var fixture = Fixture.LoadP0();
        var state = RuntimeFixture.MoveToIntake(ShiftRuntimeState.Create(fixture.Shift), "log_06");
        state = RuntimeFixture.MoveHost(state, "log_06", LogState.AT_PROCEDURE);
        state = Assert.IsType<ProcedureActionCompletedImmediately>(new ProcedureActionStartService().Start(
            state,
            LogId.From("log_06"),
            ItemId.From("salt"),
            ServerTick.From(5),
            fixture.Anomalies)).State;
        state = RuntimeFixture.MoveHost(state, "log_06", LogState.AT_INTAKE);
        state = RuntimeFixture.MoveHost(state, "log_06", LogState.QUEUED_FOR_SAW);
        state = RuntimeFixture.MoveHost(state, "log_06", LogState.IN_SAW);
        state = RuntimeFixture.MoveHost(state, "log_06", LogState.PROCESSED);

        state = RuntimeFixture.MoveToIntake(state, "log_01");
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.QUEUED_FOR_SAW);

        state = RuntimeFixture.MoveToIntake(state, "log_08");
        state = Assert.IsType<ConfirmationTestDueCompleted>(new ConfirmationTestDueCompletionService().CompleteDue(
            Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(
                state,
                LogId.From("log_08"),
                ImmutableHashSet.Create(ItemId.From("sound_meter")),
                ServerTick.From(10),
                LineNoiseRuntimeState.Create(state.ShiftId),
                fixture.Anomalies)).State,
            ServerTick.From(14),
            fixture.Anomalies)).State;
        state = RuntimeFixture.MoveHost(state, "log_08", LogState.HELD_WRITTEN_OFF);

        state = RuntimeFixture.MoveToIntake(state, "log_03");
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_PROCEDURE);
        state = Assert.IsType<ProcedureActionHoldStarted>(new ProcedureActionStartService().Start(
            state,
            LogId.From("log_03"),
            ItemId.From("holy_water"),
            ServerTick.From(19),
            fixture.Anomalies)).State;

        state = RuntimeFixture.MoveToIntake(state, "log_10");
        state = Assert.IsType<ConfirmationTestStarted>(new ConfirmationTestStartService().Start(
            state,
            LogId.From("log_10"),
            ImmutableHashSet.Create(ItemId.From("choir_cassette")),
            ServerTick.From(19),
            LineNoiseRuntimeState.Create(state.ShiftId),
            fixture.Anomalies)).State;

        state = CloneWith(state, nameof(ShiftRuntimeState.PendingFeed), new PendingFeedSchedule(LogId.From("log_02"), FeedScheduleKind.NORMAL, ServerTick.From(19), SimulationDuration.FromTicks(3), null));
        state = CloneWith(state, nameof(ShiftRuntimeState.ProcessedIntentIds), state.ProcessedIntentIds.Add(IntentId.From("tlaw022_preserved")));
        state = CloneWith(state, nameof(ShiftRuntimeState.ActiveIntakeDeadline), new ActiveIntakeDeadline(LogId.From("log_10"), ServerTick.From(19), SimulationDuration.FromTicks(20)));
        state = CloneWith(state, nameof(ShiftRuntimeState.Containment), new ContainmentRuntimeState(ContainmentState.SERVICE_REQUESTED, ServerTick.From(19), ServerTick.From(30)));
        state = CloneWith(state, nameof(ShiftRuntimeState.ActiveContainmentRitual), new ActiveContainmentRitual(ServerTick.From(19), ServerTick.From(24), SimulationDuration.FromTicks(5)));
        return CloneWith(state, nameof(ShiftRuntimeState.Line), new LineRuntimeState(
            LineState.REPAIRING,
            ServerTick.From(19),
            JamCause.INTAKE_AUTOFEED_BLOCKED,
            LogId.From("log_02"),
            new ActiveRepairHold(ServerTick.From(19), ServerTick.From(21), SimulationDuration.FromTicks(2))));
    }

    private static void AssertResolution(ProcessingResolution expected, ProcessingResolution actual)
    {
        Assert.Equal(expected.LogId, actual.LogId);
        Assert.Equal(expected.IsAnomalous, actual.IsAnomalous);
        Assert.Equal(expected.AllRequiredFlagsPresent, actual.AllRequiredFlagsPresent);
        Assert.Equal(expected.TerminalState, actual.TerminalState);
        Assert.Equal(expected.Settlement.LogId, actual.Settlement.LogId);
        Assert.Equal(expected.Settlement.CreditedSpecies, actual.Settlement.CreditedSpecies);
        Assert.Equal(expected.Settlement.CreditedUnits, actual.Settlement.CreditedUnits);
        Assert.Equal(expected.Settlement.CorrectAnomalyDelta, actual.Settlement.CorrectAnomalyDelta);
        Assert.Equal(expected.Effects, actual.Effects);
        Assert.True(expected.ValueEquals(actual));
    }

    private static void AssertPreservedUnrelated(ShiftRuntimeState before, ShiftRuntimeState after, string owner, bool expectActiveSaw)
    {
        Assert.Equal(before.ShiftId, after.ShiftId);
        Assert.Equal(before.ShiftSeed, after.ShiftSeed);
        Assert.Equal(before.ProcessedIntentIds, after.ProcessedIntentIds);
        Assert.Equal(before.PendingFeed, after.PendingFeed);
        Assert.True(before.Inventory.ValueEquals(after.Inventory));
        Assert.Equal(before.ProcedureProgressByLog, after.ProcedureProgressByLog);
        Assert.Equal(before.ActiveProcedureHold, after.ActiveProcedureHold);
        Assert.Equal(before.ActiveConfirmationTest, after.ActiveConfirmationTest);
        Assert.Equal(before.ConfirmationResultsByLog, after.ConfirmationResultsByLog);
        Assert.Equal(before.Containment, after.Containment);
        Assert.Equal(before.ActiveContainmentRitual, after.ActiveContainmentRitual);
        Assert.Equal(before.Line, after.Line);
        Assert.Equal(before.ActiveIntakeDeadline, after.ActiveIntakeDeadline);
        Assert.Equal(before.Logs.Where(log => log.LogId != LogId.From(owner)), after.Logs.Where(log => log.LogId != LogId.From(owner)));
        if (expectActiveSaw)
        {
            Assert.NotNull(after.ActiveSawCycle);
        }
        else
        {
            Assert.Null(after.ActiveSawCycle);
        }
    }

    private static void AssertRejectPreserves(ShiftRuntimeState state, Action action)
    {
        var version = state.StateVersion;
        var logs = state.Logs;
        var active = state.ActiveSawCycle;
        var line = state.Line;
        var inventory = state.Inventory;
        var exception = Record.Exception(action);

        Assert.NotNull(exception);
        Assert.Equal(version, state.StateVersion);
        Assert.Equal(logs, state.Logs);
        Assert.Equal(active, state.ActiveSawCycle);
        Assert.Equal(line, state.Line);
        Assert.Same(inventory, state.Inventory);
    }

    private static ShiftRuntimeState ReplaceLogState(ShiftRuntimeState state, string logId, LogState stateValue) =>
        CloneWith(state, nameof(ShiftRuntimeState.Logs), state.Logs.SetItem(LogIndex(state, logId), CopyWithState(Log(state, logId), stateValue)));

    private static LogRuntimeState CopyWithState(LogRuntimeState log, LogState state) =>
        new(log.LogId, log.TrueSpecies, log.DeclaredSpecies, log.Anomaly, state, log.Flags);

    private static int LogIndex(ShiftRuntimeState state, string logId)
    {
        var id = LogId.From(logId);
        for (var index = 0; index < state.Logs.Length; index++)
        {
            if (state.Logs[index].LogId == id)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Missing fixture log {logId}.");
    }

    private static ShiftRuntimeState RemoveLog(ShiftRuntimeState state, string logId)
    {
        var id = LogId.From(logId);
        var logs = state.Logs.RemoveAt(LogIndex(state, logId));
        var indexes = (ImmutableDictionary<LogId, int>)FindField(typeof(ShiftRuntimeState), "_logIndexes").GetValue(state)!;
        return CloneWith(
            CloneWith(state, nameof(ShiftRuntimeState.Logs), logs),
            "_logIndexes",
            indexes.Remove(id));
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

    private static JournaledMutationCommitted Commit(
        JournaledMutationCommitService commits,
        IEventJournal journal,
        ShiftRuntimeState before,
        ShiftRuntimeState after,
        ServerTick tick,
        string id) =>
        Assert.IsType<JournaledMutationCommitted>(commits.Commit(journal, before, after, tick, Draft(id)));

    private static DomainEventDraft Draft(string id) => new(EventId.From($"tlaw022_{id}"), EventTypeId.From("test.tlaw022.saw"), new SawPayload(id));

    private sealed record SawPayload(string Value) : IDomainEventPayload;
}
