using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Determinism;

/// <summary>
/// TLAW-046 reconstruction evidence against the real merged TLAW-042 full-host driver and its real stage-7 journal.
/// Nothing here re-runs the scenario script during replay: reconstruction consumes only the published journal.
/// </summary>
[Trait("Scope", "TLAW-046")]
public sealed class ShiftReplayScenarioTests
{
    private static (FullP0HostScenarioRun Run, ValidatedConfiguration Configuration) Execute(FullP0HostScenarioScript script)
    {
        var configuration = Fixture.LoadP0();
        return (new FullP0HostScenarioDriver().Run(configuration, script), configuration);
    }

    private static ShiftSnapshot CaptureFinal(FullP0HostScenarioRun run)
    {
        var captured = Assert.IsType<ShiftSnapshotCaptured>(new ShiftSnapshotCaptureService().Capture(run.Executions[^1]));
        return captured.Snapshot;
    }

    private static ShiftSnapshot ReplayAll(ValidatedConfiguration configuration, FullP0HostScenarioScript script, FullP0HostScenarioRun run)
    {
        var result = new ShiftReplayService().ReplayAll(configuration.Shift, script.Profile, run.Journal.Events);
        return Assert.IsType<ShiftReplaySucceeded>(Describe(result)).Snapshot;
    }

    /// <summary>Surfaces the exact replay failure instead of an opaque type-assertion message.</summary>
    private static ShiftReplayResult Describe(ShiftReplayResult result)
    {
        Assert.True(
            result is ShiftReplaySucceeded,
            result switch
            {
                ShiftReplayOrderingRejected ordering => $"ordering rejected: {ordering.Anomaly} at {ordering.Position}",
                ShiftReplaySemanticRejected semantic => $"semantic rejected: {semantic.Failure} at {semantic.Position} for {semantic.EventType}: {semantic.Detail}",
                _ => "replay succeeded"
            });
        return result;
    }

    [Fact]
    public void Canonical_learning_full_replay_reproduces_the_uninterrupted_live_final_snapshot()
    {
        var script = FullP0HostScenarioScript.LearningCorrectPath();
        var (run, configuration) = Execute(script);
        var live = CaptureFinal(run);
        var replayed = ReplayAll(configuration, script, run);

        Assert.Null(live.FirstDifference(replayed));
        Assert.True(live.StructurallyEquals(replayed));

        // Non-vacuous: the compared snapshot really carries a completed twelve-log shift.
        Assert.Equal(172, live.ServerTick.Value);
        Assert.Equal(12, live.Logs.Length);
        Assert.NotNull(live.Objectives.Completion);
        Assert.True(live.Objectives.Completion!.ObjectivesSatisfied);
        Assert.Equal(115, run.Journal.Events.Count);
        Assert.Equal(run.Journal.LastSequence, replayed.LastEventSequence);
        Assert.Equal(run.FinalShiftState.StateVersion, replayed.StateVersion);
    }

    [Fact]
    public void Pressure_full_timeout_full_replay_reproduces_a_materially_different_live_final_snapshot()
    {
        var script = FullP0HostScenarioScript.PressureFullTimeout();
        var (run, configuration) = Execute(script);
        var live = CaptureFinal(run);
        var replayed = ReplayAll(configuration, script, run);

        Assert.Null(live.FirstDifference(replayed));
        Assert.True(live.StructurallyEquals(replayed));

        // Materially different from the Learning path: hard-deadline completion with a log still in the saw.
        Assert.Equal(600, live.ServerTick.Value);
        Assert.Equal(ShiftCompletionReason.HardDeadline, live.Objectives.Completion!.Reason);
        Assert.False(live.Objectives.Completion.AllLogsTerminal);
        Assert.NotNull(live.SchedulerState.ActiveSawCycle);
    }

    [Fact]
    public void Write_off_all_suspicious_full_replay_reproduces_the_live_final_snapshot()
    {
        var script = FullP0HostScenarioScript.WriteOffAllSuspicious();
        var (run, configuration) = Execute(script);
        var live = CaptureFinal(run);
        var replayed = ReplayAll(configuration, script, run);

        Assert.Null(live.FirstDifference(replayed));
        Assert.Equal(5, live.Logs.Count(log => log.State == Domain.Enums.LogState.HELD_WRITTEN_OFF));
        Assert.False(live.Objectives.Completion!.ObjectivesSatisfied);
    }

    [Fact]
    public void Mid_shift_snapshot_plus_journal_tail_reconstructs_the_uninterrupted_live_final_snapshot()
    {
        var script = FullP0HostScenarioScript.LearningCorrectPath();
        var (run, configuration) = Execute(script);
        var live = CaptureFinal(run);

        // A deterministic mid-shift published checkpoint: the containment ritual start at tick 154.
        var midExecution = run.Executions.Single(execution => execution.CurrentTick.Value == 154);
        var mid = Assert.IsType<ShiftSnapshotCaptured>(new ShiftSnapshotCaptureService().Capture(midExecution)).Snapshot;
        Assert.Equal(154, mid.ServerTick.Value);
        Assert.NotNull(mid.ContainmentState.ActiveRitual);

        var tail = run.Journal.Events.Where(envelope => envelope.Sequence > mid.LastEventSequence).ToArray();
        Assert.NotEmpty(tail);
        Assert.Equal(run.Journal.Events.Count, tail.Length + mid.LastEventSequence.Value);

        var replayed = Assert.IsType<ShiftReplaySucceeded>(Describe(new ShiftReplayService().ReplayFrom(mid, tail, configuration.Shift))).Snapshot;
        Assert.Null(live.FirstDifference(replayed));

        // The restored separate runtime values round-trip back to the same final snapshot.
        var restored = Assert.IsType<ShiftSnapshotRestored>(new ShiftSnapshotRestoreService().Restore(replayed, configuration.Shift));
        var reprojected = new ShiftSnapshotCaptureService().CaptureRestored(restored);
        Assert.Null(live.FirstDifference(reprojected));
    }

    [Fact]
    public void Ten_independent_full_replays_produce_structurally_equal_snapshots()
    {
        var snapshots = new List<ShiftSnapshot>();
        for (var iteration = 0; iteration < 10; iteration++)
        {
            var script = FullP0HostScenarioScript.LearningCorrectPath();
            var (run, configuration) = Execute(script);
            snapshots.Add(ReplayAll(configuration, script, run));
        }

        Assert.Equal(10, snapshots.Count);
        foreach (var snapshot in snapshots.Skip(1))
        {
            Assert.Null(snapshots[0].FirstDifference(snapshot));
            Assert.True(snapshots[0].StructurallyEquals(snapshot));
            Assert.NotSame(snapshots[0], snapshot);
        }
    }

    [Fact]
    public void Full_replay_needs_no_intents_and_leaves_its_inputs_unchanged()
    {
        var script = FullP0HostScenarioScript.LearningCorrectPath();
        var (run, configuration) = Execute(script);
        var initial = new ShiftSnapshotCaptureService().CreateInitial(configuration.Shift, script.Profile);
        var events = run.Journal.Events.ToArray();
        var journalCount = run.Journal.Count;

        var replayed = Assert.IsType<ShiftReplaySucceeded>(Describe(new ShiftReplayService().ReplayFrom(initial, events, configuration.Shift))).Snapshot;

        // The pristine input snapshot is untouched and the journal was only read.
        Assert.Equal(0, initial.StateVersion.Value);
        Assert.Equal(EventSequence.None, initial.LastEventSequence);
        Assert.Null(initial.Objectives.Completion);
        Assert.Equal(journalCount, run.Journal.Count);
        Assert.Equal(events, run.Journal.Events);
        Assert.NotEqual(initial.StateVersion, replayed.StateVersion);
    }
}
