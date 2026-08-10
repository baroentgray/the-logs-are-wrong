using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
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
}
