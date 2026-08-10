using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Tests.Determinism;

namespace TheLogsAreWrong.Domain.Tests.Journal;

/// <summary>
/// TLAW-046 correction evidence. Restoration must fail closed on contradictory snapshot/runtime/lifecycle evidence
/// rather than producing impossible runtime state, while every genuinely captured snapshot still restores exactly and
/// stays usable as the previous checkpoint of the next real sequential host tick.
/// </summary>
[Trait("Scope", "TLAW-046")]
public sealed class ShiftSnapshotRestoreCorrelationTests
{
    private static readonly ShiftSnapshotCaptureService Capture = new();
    private static readonly ShiftSnapshotRestoreService Restore = new();

    /// <summary>
    /// The exact frozen messages of the established saw-correlation invariant. Asserting them proves the rejection came
    /// from the reused correlation boundary rather than from an incidental earlier failure.
    /// </summary>
    private const string SawOwnershipViolation = "Active saw ownership must retain exactly one in-saw runtime log.";
    private const string SawOwnershipMissing = "An in-saw runtime log requires active saw ownership evidence.";

    private static (ValidatedConfiguration Configuration, FullP0HostScenarioScript Script, FullP0HostScenarioRun Run) Execute(Func<FullP0HostScenarioScript> factory)
    {
        var configuration = Fixture.LoadP0();
        var script = factory();
        return (configuration, script, new FullP0HostScenarioDriver().Run(configuration, script));
    }

    private static ShiftSnapshot CaptureAt(FullP0HostScenarioRun run, Func<ShiftSnapshot, bool> predicate) =>
        run.Executions
            .Select(execution => Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(execution)).Snapshot)
            .First(predicate);

    private static ShiftSnapshot With(
        ShiftSnapshot source,
        SnapshotSchedulerState? scheduler = null,
        ImmutableArray<SnapshotLog>? logs = null,
        SnapshotObjectives? objectives = null,
        ServerTick? serverTick = null) => new(
        source.ShiftId,
        serverTick ?? source.ServerTick,
        source.StateVersion,
        source.LastEventSequence,
        scheduler ?? source.SchedulerState,
        logs ?? source.Logs,
        source.LineState,
        source.ContainmentState,
        source.Inventory,
        source.Quota,
        objectives ?? source.Objectives);

    private static SnapshotSchedulerState WithSaw(SnapshotSchedulerState source, SnapshotSawCycle? cycle) => new(
        source.PendingFeed, source.ActiveIntakeDeadline, source.ActiveProcedureHold, source.ActiveConfirmationTest,
        cycle, source.ProcessedIntentIds, source.Progression);

    private static SnapshotLog WithState(SnapshotLog source, LogState state) => new(
        source.LogId, source.TrueSpecies, source.DeclaredSpecies, source.Anomaly, state,
        source.Flags, source.ProcedureProgress, source.ConfirmationResult);

    private static SnapshotObjectives WithoutCompletion(SnapshotObjectives source) => new(
        source.SelectedProfileId, source.TargetTotal, source.TargetBySpecies, source.MinimumCorrectlyProcessedAnomalies,
        source.StartedAt, source.HardDeadlineDuration, null);

    // ----- active saw correlation -----

    [Fact]
    public void An_active_saw_cycle_whose_owner_is_not_in_the_saw_is_rejected()
    {
        var (configuration, _, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var sawSnapshot = CaptureAt(run, snapshot => snapshot.SchedulerState.ActiveSawCycle is not null);
        var owner = sawSnapshot.SchedulerState.ActiveSawCycle!.LogId;

        // The cycle owner is moved out of the saw while the cycle still claims it.
        var logs = sawSnapshot.Logs
            .Select(log => log.LogId == owner ? WithState(log, LogState.QUEUED_FOR_SAW) : log)
            .ToImmutableArray();

        var rejected = Assert.IsType<ShiftSnapshotRestoreRejected>(Restore.Restore(With(sawSnapshot, logs: logs), configuration.Shift));
        Assert.Equal(ShiftSnapshotRestoreRejection.ContradictoryEvidence, rejected.Reason);
        Assert.Equal(SawOwnershipViolation, rejected.Detail);
    }

    [Fact]
    public void An_active_saw_cycle_owned_by_a_log_that_is_not_the_saw_occupant_is_rejected()
    {
        var (configuration, _, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var sawSnapshot = CaptureAt(run, snapshot => snapshot.SchedulerState.ActiveSawCycle is not null);
        var cycle = sawSnapshot.SchedulerState.ActiveSawCycle!;
        var other = sawSnapshot.Logs.First(log => log.LogId != cycle.LogId);

        // Ownership is reassigned to a log that does not occupy the saw.
        var scheduler = WithSaw(sawSnapshot.SchedulerState, new SnapshotSawCycle(other.LogId, cycle.StartedAt, cycle.Duration));

        var rejected = Assert.IsType<ShiftSnapshotRestoreRejected>(Restore.Restore(With(sawSnapshot, scheduler: scheduler), configuration.Shift));
        Assert.Equal(ShiftSnapshotRestoreRejection.ContradictoryEvidence, rejected.Reason);
        Assert.Equal(SawOwnershipViolation, rejected.Detail);
    }

    [Fact]
    public void A_saw_occupant_with_no_active_cycle_is_rejected()
    {
        var (configuration, _, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var sawSnapshot = CaptureAt(run, snapshot => snapshot.SchedulerState.ActiveSawCycle is not null);
        Assert.Contains(sawSnapshot.Logs, log => log.State == LogState.IN_SAW);

        // The occupant stays in the saw while its ownership evidence is dropped.
        var scheduler = WithSaw(sawSnapshot.SchedulerState, null);

        var rejected = Assert.IsType<ShiftSnapshotRestoreRejected>(Restore.Restore(With(sawSnapshot, scheduler: scheduler), configuration.Shift));
        Assert.Equal(ShiftSnapshotRestoreRejection.ContradictoryEvidence, rejected.Reason);
        Assert.Equal(SawOwnershipMissing, rejected.Detail);
    }

    [Fact]
    public void Two_logs_occupying_the_saw_at_once_is_rejected()
    {
        var (configuration, _, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var sawSnapshot = CaptureAt(run, snapshot => snapshot.SchedulerState.ActiveSawCycle is not null);
        var owner = sawSnapshot.SchedulerState.ActiveSawCycle!.LogId;
        var intruder = sawSnapshot.Logs.First(log => log.LogId != owner && log.State != LogState.IN_SAW);

        var logs = sawSnapshot.Logs
            .Select(log => log.LogId == intruder.LogId ? WithState(log, LogState.IN_SAW) : log)
            .ToImmutableArray();

        var rejected = Assert.IsType<ShiftSnapshotRestoreRejected>(Restore.Restore(With(sawSnapshot, logs: logs), configuration.Shift));
        Assert.Equal(ShiftSnapshotRestoreRejection.ContradictoryEvidence, rejected.Reason);
        Assert.Equal(SawOwnershipViolation, rejected.Detail);
    }

    // ----- lifecycle and progression correlation -----

    [Fact]
    public void An_active_snapshot_exactly_at_the_hard_deadline_with_no_completion_is_rejected()
    {
        var (configuration, _, run) = Execute(FullP0HostScenarioScript.PressureFullTimeout);
        var final = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(run.Executions[^1])).Snapshot;

        // The Pressure run completes exactly at its hard deadline, so dropping completion is the deadline contradiction.
        Assert.NotNull(final.Objectives.Completion);
        Assert.Equal(final.Objectives.HardDeadlineDuration.Value, final.ServerTick.Value);

        var rejected = Assert.IsType<ShiftSnapshotRestoreRejected>(
            Restore.Restore(With(final, objectives: WithoutCompletion(final.Objectives)), configuration.Shift));
        Assert.Equal(ShiftSnapshotRestoreRejection.ContradictoryEvidence, rejected.Reason);
        Assert.Contains("hard deadline", rejected.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_active_snapshot_after_the_hard_deadline_is_rejected()
    {
        var (configuration, _, run) = Execute(FullP0HostScenarioScript.PressureFullTimeout);
        var final = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(run.Executions[^1])).Snapshot;
        var beyond = ServerTick.From(final.ServerTick.Value + 1);

        var rejected = Assert.IsType<ShiftSnapshotRestoreRejected>(
            Restore.Restore(With(final, objectives: WithoutCompletion(final.Objectives), serverTick: beyond), configuration.Shift));
        Assert.Equal(ShiftSnapshotRestoreRejection.ContradictoryEvidence, rejected.Reason);
    }

    [Fact]
    public void An_active_snapshot_whose_logs_are_all_terminal_is_rejected()
    {
        var (configuration, _, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var final = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(run.Executions[^1])).Snapshot;

        // The canonical Learning run ends with every log terminal well before its hard deadline.
        Assert.All(final.Logs, log => Assert.Contains(log.State, new[] { LogState.PROCESSED, LogState.HELD_WRITTEN_OFF }));
        Assert.True(final.ServerTick.Value < final.Objectives.HardDeadlineDuration.Value);

        var rejected = Assert.IsType<ShiftSnapshotRestoreRejected>(
            Restore.Restore(With(final, objectives: WithoutCompletion(final.Objectives)), configuration.Shift));
        Assert.Equal(ShiftSnapshotRestoreRejection.ContradictoryEvidence, rejected.Reason);
        Assert.Contains("terminal", rejected.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Completion_evidence_that_contradicts_the_reconstructed_facts_is_rejected()
    {
        var (configuration, _, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var final = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(run.Executions[^1])).Snapshot;
        var completion = final.Objectives.Completion!;

        var tamperedReason = new SnapshotCompletion(
            completion.CompletedAt, ShiftCompletionReason.HardDeadline, completion.AllLogsTerminal,
            completion.HardDeadlineReached, completion.ObjectivesSatisfied, completion.ProcessedCount, completion.WrittenOffCount);
        var rejectedReason = Assert.IsType<ShiftSnapshotRestoreRejected>(Restore.Restore(
            With(final, objectives: new SnapshotObjectives(
                final.Objectives.SelectedProfileId, final.Objectives.TargetTotal, final.Objectives.TargetBySpecies,
                final.Objectives.MinimumCorrectlyProcessedAnomalies, final.Objectives.StartedAt,
                final.Objectives.HardDeadlineDuration, tamperedReason)),
            configuration.Shift));
        Assert.Equal(ShiftSnapshotRestoreRejection.ContradictoryEvidence, rejectedReason.Reason);

        var tamperedCounts = new SnapshotCompletion(
            completion.CompletedAt, completion.Reason, completion.AllLogsTerminal, completion.HardDeadlineReached,
            completion.ObjectivesSatisfied, completion.ProcessedCount + 1, completion.WrittenOffCount);
        var rejectedCounts = Assert.IsType<ShiftSnapshotRestoreRejected>(Restore.Restore(
            With(final, objectives: new SnapshotObjectives(
                final.Objectives.SelectedProfileId, final.Objectives.TargetTotal, final.Objectives.TargetBySpecies,
                final.Objectives.MinimumCorrectlyProcessedAnomalies, final.Objectives.StartedAt,
                final.Objectives.HardDeadlineDuration, tamperedCounts)),
            configuration.Shift));
        Assert.Equal(ShiftSnapshotRestoreRejection.ContradictoryEvidence, rejectedCounts.Reason);
    }

    [Fact]
    public void A_snapshot_claiming_no_completed_host_tick_after_execution_is_rejected()
    {
        var (configuration, _, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var mid = CaptureAt(run, snapshot => snapshot.ServerTick.Value == 20);
        var scheduler = mid.SchedulerState;

        var tampered = With(mid, scheduler: new SnapshotSchedulerState(
            scheduler.PendingFeed, scheduler.ActiveIntakeDeadline, scheduler.ActiveProcedureHold,
            scheduler.ActiveConfirmationTest, scheduler.ActiveSawCycle, scheduler.ProcessedIntentIds,
            new SnapshotProgression(false, false)));

        var rejected = Assert.IsType<ShiftSnapshotRestoreRejected>(Restore.Restore(tampered, configuration.Shift));
        Assert.Equal(ShiftSnapshotRestoreRejection.ContradictoryEvidence, rejected.Reason);
    }

    [Fact]
    public void A_checkpoint_receipt_claiming_completion_while_the_lifecycle_is_active_is_rejected()
    {
        var (configuration, _, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var mid = CaptureAt(run, snapshot => snapshot.ServerTick.Value == 20);
        Assert.Null(mid.Objectives.Completion);
        var scheduler = mid.SchedulerState;

        var tampered = With(mid, scheduler: new SnapshotSchedulerState(
            scheduler.PendingFeed, scheduler.ActiveIntakeDeadline, scheduler.ActiveProcedureHold,
            scheduler.ActiveConfirmationTest, scheduler.ActiveSawCycle, scheduler.ProcessedIntentIds,
            new SnapshotProgression(true, true)));

        var rejected = Assert.IsType<ShiftSnapshotRestoreRejected>(Restore.Restore(tampered, configuration.Shift));
        Assert.Equal(ShiftSnapshotRestoreRejection.ContradictoryEvidence, rejected.Reason);
    }

    // ----- valid evidence is untouched -----

    [Fact]
    public void Every_captured_snapshot_of_every_scenario_still_restores_exactly()
    {
        foreach (var factory in new[]
                 {
                     FullP0HostScenarioScript.LearningCorrectPath,
                     FullP0HostScenarioScript.PressureFullTimeout,
                     FullP0HostScenarioScript.WriteOffAllSuspicious,
                     FullP0HostScenarioScript.IncorrectPenitent,
                     FullP0HostScenarioScript.IncorrectResin
                 })
        {
            var (configuration, _, run) = Execute(factory);
            foreach (var execution in run.Executions)
            {
                var snapshot = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(execution)).Snapshot;
                var restored = Assert.IsType<ShiftSnapshotRestored>(Restore.Restore(snapshot, configuration.Shift));
                Assert.Null(snapshot.FirstDifference(Capture.CaptureRestored(restored)));
            }
        }
    }

    [Fact]
    public void The_pristine_initial_snapshot_still_restores()
    {
        var configuration = Fixture.LoadP0();
        var initial = Capture.CreateInitial(configuration.Shift, ProfileId.From("learning"));

        var restored = Assert.IsType<ShiftSnapshotRestored>(Restore.Restore(initial, configuration.Shift));
        Assert.Null(initial.FirstDifference(Capture.CaptureRestored(restored)));
        Assert.False(restored.Progression.HasCompletedTick);
    }

    // ----- restored evidence drives the next real sequential host tick -----

    [Fact]
    public void Restored_mid_shift_state_is_valid_previous_checkpoint_evidence_for_the_next_real_host_tick()
    {
        var (configuration, script, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);

        // A mid-shift tick whose successor is executed by the frozen script without any accepted intent, so the
        // restored state alone must reproduce it.
        var index = run.Executions.ToArray().ToList().FindIndex(execution =>
            execution.CurrentTick.Value > 0 &&
            execution.CurrentTick.Value < run.Executions[^1].CurrentTick.Value &&
            script.TickAt(ServerTick.From(execution.CurrentTick.Value + 1)).Intents.IsEmpty &&
            run.Journal.Events.Any(envelope => envelope.ServerTick.Value == execution.CurrentTick.Value + 1));
        Assert.True(index > 0, "the canonical run must contain an intent-free publishing successor tick");

        var mid = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(run.Executions[index])).Snapshot;
        var restored = Assert.IsType<ShiftSnapshotRestored>(Restore.Restore(mid, configuration.Shift));

        var nextTick = ServerTick.From(mid.ServerTick.Value + 1);
        var scripted = script.TickAt(nextTick);
        var expected = run.Journal.Events.Where(envelope => envelope.ServerTick == nextTick).ToArray();

        var journal = new InMemoryEventJournal(mid.ShiftId);
        foreach (var envelope in run.Journal.Events.Where(envelope => envelope.Sequence <= mid.LastEventSequence))
        {
            journal.Append(envelope);
        }

        var execution = new HostTickExecutionService().Execute(
            restored.ShiftState,
            restored.QuotaState,
            restored.MovementNoise,
            restored.LineNoise,
            restored.Progression,
            restored.Lifecycle,
            AcceptedIntentTickBatchFactory.Create(mid.ShiftId, nextTick, ImmutableArray<AuthoritativeAcceptedIntent>.Empty),
            scripted.ActiveTools,
            journal,
            expected.Select(envelope => envelope.EventId).ToImmutableArray(),
            nextTick,
            configuration.Shift.Scheduler,
            configuration.Shift,
            configuration.Shift.Containment,
            configuration.Anomalies);

        // The restored evidence advances the checkpoint and reproduces the live tick exactly.
        Assert.IsType<HostTickCheckpointAdvanced>(execution.Checkpoint);
        Assert.Equal(
            expected.Select(envelope => envelope.EventType),
            Assert.IsType<HostStageSevenPublished>(execution).Publications.Select(publication => publication.Envelope.EventType));

        var live = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(run.Executions[index + 1])).Snapshot;
        var reconstructed = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(execution)).Snapshot;
        Assert.Null(live.FirstDifference(reconstructed));
    }
}
