using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Determinism;

[Trait("Scope", "TLAW-047")]
public sealed class PenitentSawFailureWindowContinuationTests
{
    private static readonly ShiftSnapshotCaptureService Capture = new();
    private static readonly ShiftSnapshotRestoreService Restore = new();

    [Fact]
    public void Real_host_continuation_keeps_the_saw_blocked_until_exact_expiry_while_non_saw_work_continues()
    {
        var configuration = Fixture.LoadP0();
        var run = Execute(configuration);
        var window = Assert.IsType<SawFailureWindow>(run.Executions[19].FinalShiftState.ActiveSawFailureWindow);

        Assert.Equal(34, run.HostTickCount);
        Assert.Equal(ServerTick.From(19), run.SawCompletionFor("log_03").CompletedAt);
        Assert.Equal((ServerTick.From(19), ServerTick.From(27), 8L), (window.StartedAt, window.DueAt, window.Duration.Value));
        Assert.Equal(Enumerable.Range(0, 34).Select(value => (long)value), run.Ticks.Select(record => record.Tick.Value));

        foreach (var tick in Enumerable.Range(19, 8))
        {
            var blocked = Assert.IsType<SawCycleStartBlockedByFailureWindow>(run.Executions[tick].StageFour.Start.Result);
            Assert.Same(window, blocked.Window);
            Assert.Same(run.Executions[tick].StageFour.Completion.AfterShiftState, blocked.State);
        }

        var resumed = Assert.IsType<SawCycleStarted>(run.Executions[27].StageFour.Start.Result);
        Assert.Equal((LogId.From("log_04"), ServerTick.From(27)), (resumed.Cycle.LogId, resumed.Cycle.StartedAt));
        Assert.Equal(
            [HostStageSevenEventTypes.EarlyFeedRequested, HostStageSevenEventTypes.FeedScheduled, HostStageSevenEventTypes.LogRouted],
            run.RecordAt(20).PublishedEventTypes);
        var earlyFeed = Assert.IsType<EarlyFeedScheduled>(
            Assert.IsType<EarlyFeedIntentStageOutcome>(run.Executions[20].StageTwo.Steps[0].Outcome).Result);
        Assert.Equal((LogId.From("log_05"), ServerTick.From(20), ServerTick.From(22)), (earlyFeed.Schedule.LogId, earlyFeed.Schedule.ScheduledAt, earlyFeed.Schedule.DueAt));
        Assert.IsType<ManualLogIntentAccepted>(Assert.IsType<ManualRoutingIntentStageOutcome>(run.Executions[20].StageTwo.Steps[1].Outcome).Result);
        Assert.Equal(
            [HostStageSevenEventTypes.LogAdmittedToIntake, HostStageSevenEventTypes.IntakeDeadlineStarted],
            run.RecordAt(22).PublishedEventTypes);
        var admitted = Assert.IsType<FeedDueResolved>(run.Executions[22].StageFive.FeedDue);
        Assert.Equal((earlyFeed.Schedule.LogId, earlyFeed.Schedule.DueAt), (admitted.ConsumedSchedule.LogId, admitted.ResolvedAt));

        Assert.Equal(
            (SimulationDuration.FromTicks(840), ServerTick.From(840)),
            (run.FinalLifecycle.HardDeadlineDuration, run.FinalLifecycle.HardDeadlineAt));
        Assert.False(run.FinalLifecycle.IsCompleted);

        Assert.All(run.Executions, execution =>
        {
            Assert.Equal(LineState.LINE_CLEAR, execution.FinalShiftState.Line.State);
            Assert.Null(execution.FinalShiftState.Line.ActiveRepairHold);
            Assert.IsType<LineRepairNoActive>(execution.StageOne.LineRepair.Result);
            Assert.DoesNotContain(execution.StageTwo.Steps, step => step.Outcome is LineRepairIntentStageOutcome);
        });

        var activeWindowSources = run.Executions[26].StageSix.FinalLineNoise.LatestSources;
        Assert.Equal(LineNoise.QUIET, run.Executions[26].StageSix.FinalLineNoise.Current);
        Assert.False(activeWindowSources.SawActive);
        Assert.False(activeWindowSources.MovementNoiseActive);
        Assert.False(activeWindowSources.RepairActive);
    }

    [Fact]
    public void Active_window_snapshot_plus_exact_journal_tail_replays_to_the_uninterrupted_continuation_snapshot()
    {
        var configuration = Fixture.LoadP0();
        var run = Execute(configuration);
        var checkpoint = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(run.Executions[20])).Snapshot;
        var uninterrupted = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(run.Executions[^1])).Snapshot;
        var tail = run.Journal.Events
            .Where(envelope => envelope.Sequence > checkpoint.LastEventSequence)
            .ToArray();

        Assert.NotNull(checkpoint.SchedulerState.ActiveSawFailureWindow);
        Assert.NotEmpty(tail);
        Assert.All(tail, envelope => Assert.True(envelope.Sequence > checkpoint.LastEventSequence));

        var replayed = Assert.IsType<ShiftReplaySucceeded>(
            new ShiftReplayService().ReplayFrom(checkpoint, tail, configuration.Shift));

        Assert.True(uninterrupted.StructurallyEquals(replayed.Snapshot));
        var restored = Assert.IsType<ShiftSnapshotRestored>(Restore.Restore(replayed.Snapshot, configuration.Shift));
        Assert.True(replayed.Snapshot.StructurallyEquals(Capture.CaptureRestored(restored)));
    }

    [Fact]
    public void Ten_independent_continuations_have_identical_window_blocking_host_journal_and_snapshot_projections()
    {
        var configuration = Fixture.LoadP0();
        var runs = Enumerable.Range(0, 10).Select(_ => Execute(configuration)).ToArray();
        var first = runs[0];
        var firstWindow = Assert.IsType<SawFailureWindow>(first.Executions[19].FinalShiftState.ActiveSawFailureWindow);
        var firstBlocked = Enumerable.Range(19, 8)
            .Select(tick => first.Executions[tick].StageFour.Start.Result.GetType())
            .ToArray();
        var firstSnapshot = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(first.Executions[^1])).Snapshot;

        foreach (var run in runs.Skip(1))
        {
            var window = Assert.IsType<SawFailureWindow>(run.Executions[19].FinalShiftState.ActiveSawFailureWindow);
            Assert.Equal((firstWindow.StartedAt, firstWindow.Duration, firstWindow.DueAt), (window.StartedAt, window.Duration, window.DueAt));
            Assert.Equal(firstBlocked, Enumerable.Range(19, 8).Select(tick => run.Executions[tick].StageFour.Start.Result.GetType()));
            Assert.True(first.Projection.StructurallyEquals(run.Projection));
            Assert.True(firstSnapshot.StructurallyEquals(Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(run.Executions[^1])).Snapshot));
        }
    }

    private static FullP0HostScenarioRun Execute(TheLogsAreWrong.Domain.Configuration.ValidatedConfiguration configuration) =>
        new FullP0HostScenarioDriver().Run(configuration, FullP0HostScenarioScript.IncorrectPenitentFailureWindowContinuation());
}
