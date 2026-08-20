using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Sequencing;
using TheLogsAreWrong.Domain.Tests.Determinism;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Journal;

[Trait("Scope", "TLAW-047")]
public sealed class PenitentSawFailureWindowSnapshotTests
{
    private static readonly ShiftSnapshotCaptureService Capture = new();
    private static readonly ShiftSnapshotRestoreService Restore = new();

    [Fact]
    public void Active_window_from_the_real_incorrect_penitent_host_tick_captures_and_restores_losslessly()
    {
        var (configuration, run) = RunIncorrectPenitent();
        var snapshot = Captures(run)
            .Single(candidate => candidate.SchedulerState.ActiveSawFailureWindow is not null);
        var window = snapshot.SchedulerState.ActiveSawFailureWindow!;
        var completion = run.SawCompletionFor("log_03");

        Assert.Equal(completion.CompletedAt, window.StartedAt);
        Assert.Equal(8, window.Duration.Value);
        Assert.Equal(completion.CompletedAt + SimulationDuration.FromTicks(8), window.StartedAt + window.Duration);

        var restored = Assert.IsType<ShiftSnapshotRestored>(Restore.Restore(snapshot, configuration.Shift));
        Assert.True(snapshot.StructurallyEquals(Capture.CaptureRestored(restored)));
        Assert.IsType<SawFailureWindow>(restored.ShiftState.ActiveSawFailureWindow);
    }

    [Fact]
    public void Restoring_a_still_active_window_with_an_active_saw_fails_closed()
    {
        var configuration = Fixture.LoadP0();
        var run = new FullP0HostScenarioDriver().Run(configuration, FullP0HostScenarioScript.LearningCorrectPath());
        var snapshot = Captures(run).First(candidate => candidate.SchedulerState.ActiveSawCycle is not null);
        var scheduler = WithWindow(
            snapshot.SchedulerState,
            new SnapshotSawFailureWindow(snapshot.ServerTick, SimulationDuration.FromTicks(8)));

        var rejected = Assert.IsType<ShiftSnapshotRestoreRejected>(Restore.Restore(With(snapshot, scheduler, snapshot.ServerTick), configuration.Shift));

        Assert.Equal(ShiftSnapshotRestoreRejection.ContradictoryEvidence, rejected.Reason);
        Assert.Contains("active saw cycle cannot coexist", rejected.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Correctly_prepared_penitent_scenario_never_creates_a_failure_window()
    {
        var configuration = Fixture.LoadP0();
        var run = new FullP0HostScenarioDriver().Run(configuration, FullP0HostScenarioScript.LearningCorrectPath());

        Assert.DoesNotContain(Captures(run), snapshot => snapshot.SchedulerState.ActiveSawFailureWindow is not null);
    }

    [Fact]
    public void Expired_restored_window_is_historical_evidence_and_does_not_return_the_blocked_start_result()
    {
        var (configuration, run) = RunIncorrectPenitent();
        var active = Captures(run).Single(candidate => candidate.SchedulerState.ActiveSawFailureWindow is not null);
        var window = active.SchedulerState.ActiveSawFailureWindow!;
        var expired = With(active, active.SchedulerState, window.StartedAt + window.Duration);

        var restored = Assert.IsType<ShiftSnapshotRestored>(Restore.Restore(expired, configuration.Shift));
        var start = new SawCycleStartService().Start(restored.ShiftState, window.StartedAt + window.Duration, configuration.Shift.Scheduler);

        Assert.False(restored.ShiftState.ActiveSawFailureWindow!.IsActiveAt(expired.ServerTick));
        Assert.IsNotType<SawCycleStartBlockedByFailureWindow>(start);
    }

    [Fact]
    public void Full_replay_of_the_real_incorrect_penitent_trace_reconstructs_the_window_from_existing_completion_evidence()
    {
        var (configuration, run) = RunIncorrectPenitent();
        var expected = Captures(run).Last();

        var replayed = Assert.IsType<ShiftReplaySucceeded>(
            new ShiftReplayService().ReplayAll(configuration.Shift, run.Script.Profile, run.Journal.Events));

        Assert.True(expected.StructurallyEquals(replayed.Snapshot));
        Assert.Equal(24, typeof(HostStageSevenEventTypes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Count(field => field.FieldType == typeof(EventTypeId)));
    }

    [Fact]
    public void A_restored_intake_deadline_expires_through_real_host_ticks_while_the_failure_window_remains_active()
    {
        var (configuration, run) = RunIncorrectPenitent();
        var source = Captures(run).Single(snapshot => snapshot.ServerTick == ServerTick.From(19));
        var deadlineSnapshot = With(
            source,
            WithDeadline(
                source.SchedulerState,
                new SnapshotIntakeDeadline(LogId.From("log_04"), source.ServerTick, SimulationDuration.FromTicks(2))),
            source.ServerTick);
        var restored = Assert.IsType<ShiftSnapshotRestored>(Restore.Restore(deadlineSnapshot, configuration.Shift));
        var journal = JournalAtBoundary(run, deadlineSnapshot);
        var host = new HostTickExecutionService();

        var waiting = host.Execute(
            restored.ShiftState,
            restored.QuotaState,
            restored.MovementNoise,
            restored.LineNoise,
            restored.Progression,
            restored.Lifecycle,
            Batch(restored.ShiftState.ShiftId, ServerTick.From(20)),
            ImmutableHashSet<ItemId>.Empty,
            journal,
            ServerTick.From(20),
            configuration.Shift.Scheduler,
            configuration.Shift,
            configuration.Shift.Containment,
            configuration.Anomalies);

        var waitingCheckpoint = Assert.IsType<HostTickCheckpointAdvanced>(waiting.Checkpoint);
        Assert.IsType<HostStageSevenNoNewPublication>(waiting);
        Assert.IsType<SawCycleStartBlockedByFailureWindow>(waiting.StageFour.Start.Result);
        Assert.Equal(ServerTick.From(21), Assert.IsType<ActiveIntakeDeadline>(waiting.FinalShiftState.ActiveIntakeDeadline).DueAt);

        var expired = host.Execute(
            waiting.FinalShiftState,
            waiting.FinalQuotaState,
            waiting.StageSix.FinalMovementNoise,
            waiting.FinalLineNoise,
            waitingCheckpoint.Progression,
            waitingCheckpoint.Receipt.Lifecycle,
            Batch(waiting.FinalShiftState.ShiftId, ServerTick.From(21)),
            ImmutableHashSet<ItemId>.Empty,
            journal,
            ServerTick.From(21),
            configuration.Shift.Scheduler,
            configuration.Shift,
            configuration.Shift.Containment,
            configuration.Anomalies);

        var published = Assert.IsType<HostStageSevenPublished>(expired);
        Assert.Equal(
            [HostStageSevenEventTypes.IntakeDeadlineExpired, HostStageSevenEventTypes.AutoRouteAttempted, HostStageSevenEventTypes.FeedScheduled],
            published.Publications.Select(publication => publication.Envelope.EventType));
        Assert.IsType<IntakeDeadlineExpired>(expired.StageThree.IntakeDeadline.Result);
        Assert.IsType<DefaultIntakeAutoRouteApplied>(expired.StageFive.DefaultRoute);
        Assert.IsType<SawCycleStartBlockedByFailureWindow>(expired.StageFour.Start.Result);
        Assert.Null(expired.FinalShiftState.ActiveIntakeDeadline);
        Assert.Equal(LogState.QUEUED_FOR_SAW, Log(expired.FinalShiftState, "log_04").State);
        Assert.Equal((ServerTick.From(19), ServerTick.From(27)), (expired.FinalShiftState.ActiveSawFailureWindow!.StartedAt, expired.FinalShiftState.ActiveSawFailureWindow.DueAt));
        Assert.Equal(ServerTick.From(840), expired.Checkpoint is HostTickCheckpointAdvanced advanced
            ? advanced.Receipt.Lifecycle.HardDeadlineAt
            : throw new InvalidOperationException("The real host tick must advance its checkpoint."));
    }

    private static (ValidatedConfiguration Configuration, FullP0HostScenarioRun Run) RunIncorrectPenitent()
    {
        var configuration = Fixture.LoadP0();
        return (configuration, new FullP0HostScenarioDriver().Run(configuration, FullP0HostScenarioScript.IncorrectPenitent()));
    }

    private static ImmutableArray<ShiftSnapshot> Captures(FullP0HostScenarioRun run) => run.Executions
        .Select(execution => Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(execution)).Snapshot)
        .ToImmutableArray();

    private static ShiftSnapshot With(ShiftSnapshot source, SnapshotSchedulerState scheduler, ServerTick serverTick) => new(
        source.ShiftId,
        serverTick,
        source.StateVersion,
        source.LastEventSequence,
        scheduler,
        source.Logs,
        source.LineState,
        source.ContainmentState,
        source.Inventory,
        source.Quota,
        source.Objectives);

    private static SnapshotSchedulerState WithWindow(SnapshotSchedulerState source, SnapshotSawFailureWindow? window) => new(
        source.PendingFeed,
        source.ActiveIntakeDeadline,
        source.ActiveProcedureHold,
        source.ActiveConfirmationTest,
        source.ActiveSawCycle,
        window,
        source.ProcessedIntentIds,
        source.Progression);

    private static SnapshotSchedulerState WithDeadline(SnapshotSchedulerState source, SnapshotIntakeDeadline deadline) => new(
        source.PendingFeed,
        deadline,
        source.ActiveProcedureHold,
        source.ActiveConfirmationTest,
        source.ActiveSawCycle,
        source.ActiveSawFailureWindow,
        source.ProcessedIntentIds,
        source.Progression);

    private static AcceptedIntentTickBatch Batch(ShiftId shiftId, ServerTick tick) =>
        AcceptedIntentTickBatchFactory.Create(shiftId, tick, []);

    private static InMemoryEventJournal JournalAtBoundary(FullP0HostScenarioRun run, ShiftSnapshot snapshot)
    {
        var journal = new InMemoryEventJournal(snapshot.ShiftId);
        foreach (var envelope in run.Journal.Events.Where(envelope => envelope.Sequence <= snapshot.LastEventSequence))
        {
            journal.Append(envelope);
        }

        return journal;
    }

    private static LogRuntimeState Log(ShiftRuntimeState state, string logId)
    {
        Assert.True(state.TryGetLog(LogId.From(logId), out var log));
        return log;
    }
}
