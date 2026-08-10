using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Journal;

/// <summary>Why a coherent snapshot could not be captured from the supplied host evidence.</summary>
public enum ShiftSnapshotCaptureRejection
{
    BlockedCheckpoint,
    ShiftMismatch,
    JournalCursorMisaligned,
    UninitializedEvidence
}

public abstract record ShiftSnapshotCaptureResult;

public sealed record ShiftSnapshotCaptured(ShiftSnapshot Snapshot) : ShiftSnapshotCaptureResult;

public sealed record ShiftSnapshotCaptureRejected(ShiftSnapshotCaptureRejection Reason) : ShiftSnapshotCaptureResult;

/// <summary>
/// The one narrow capture boundary. It produces a value-only <see cref="ShiftSnapshot"/> from a coherent completed
/// host-tick publication, and it runs no gameplay, appends no event and mutates nothing.
/// <para>
/// The tick, state version and journal boundary always come from the exact same completed trace: the executed tick,
/// the final stage-six shift version and the after-publication journal cursor. Progression and lifecycle come from the
/// checkpoint result rather than from the stage-six pre-checkpoint inputs, so a snapshot never records the lifecycle a
/// tick was entered with instead of the one it completed with.
/// </para>
/// </summary>
public sealed class ShiftSnapshotCaptureService
{
    public ShiftSnapshotCaptureResult Capture(HostStageSevenEventExecution execution)
    {
        ArgumentNullException.ThrowIfNull(execution);

        if (execution is HostStageSevenBlocked)
        {
            return new ShiftSnapshotCaptureRejected(ShiftSnapshotCaptureRejection.BlockedCheckpoint);
        }

        var (progression, lifecycle) = execution.Checkpoint switch
        {
            HostTickCheckpointAdvanced advanced => (advanced.Progression, advanced.Receipt.Lifecycle),
            HostTickCheckpointReplayed replayed => (replayed.Progression, replayed.Receipt.Lifecycle),
            _ => (null, null)
        };

        if (progression is null || lifecycle is null)
        {
            return new ShiftSnapshotCaptureRejected(ShiftSnapshotCaptureRejection.BlockedCheckpoint);
        }

        var shift = execution.FinalShiftState;
        var quota = execution.FinalQuotaState;
        var movementNoise = execution.StageSix.FinalMovementNoise;
        var lineNoise = execution.FinalLineNoise;
        var cursor = execution.AfterCursor;

        if (shift.ShiftId.IsDefault || execution.CurrentTick.IsDefault || shift.StateVersion.IsDefault)
        {
            return new ShiftSnapshotCaptureRejected(ShiftSnapshotCaptureRejection.UninitializedEvidence);
        }

        if (cursor.ShiftId != shift.ShiftId || movementNoise.ShiftId != shift.ShiftId || lineNoise.ShiftId != shift.ShiftId ||
            progression.ShiftId != shift.ShiftId || lifecycle.ShiftId != shift.ShiftId)
        {
            return new ShiftSnapshotCaptureRejected(ShiftSnapshotCaptureRejection.ShiftMismatch);
        }

        if (cursor.LastStateVersion != shift.StateVersion)
        {
            return new ShiftSnapshotCaptureRejected(ShiftSnapshotCaptureRejection.JournalCursorMisaligned);
        }

        if (progression.LastCompletedTick is { } completedTick && completedTick != execution.CurrentTick)
        {
            return new ShiftSnapshotCaptureRejected(ShiftSnapshotCaptureRejection.JournalCursorMisaligned);
        }

        return new ShiftSnapshotCaptured(Project(
            shift,
            quota,
            movementNoise,
            lineNoise,
            progression,
            lifecycle,
            execution.CurrentTick,
            cursor.LastSequence));
    }

    /// <summary>
    /// The pristine pre-execution snapshot for a validated configuration and selected profile. It executes no host tick
    /// and uses the established zero/none conventions: state version zero, <see cref="EventSequence.None"/>, no
    /// completed checkpoint and every manifest log still <c>SCHEDULED</c>.
    /// </summary>
    public ShiftSnapshot CreateInitial(ShiftConfiguration configuration, ProfileId selectedProfileId)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var shift = ShiftRuntimeState.Create(configuration);
        var quota = QuotaRuntimeState.Create(configuration);
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, selectedProfileId);

        return Project(
            shift,
            quota,
            MovementNoiseRuntimeState.Create(shift.ShiftId),
            LineNoiseRuntimeState.Create(shift.ShiftId),
            HostTickProgressionEvidence.Create(shift.ShiftId),
            lifecycle,
            ServerTick.Zero,
            EventSequence.None);
    }

    /// <summary>Projects the exact separate host-owned runtime values into the frozen snapshot shape.</summary>
    internal static ShiftSnapshot Project(
        ShiftRuntimeState shift,
        QuotaRuntimeState quota,
        MovementNoiseRuntimeState movementNoise,
        LineNoiseRuntimeState lineNoise,
        HostTickProgressionEvidence progression,
        ShiftLifecycleRuntimeState lifecycle,
        ServerTick serverTick,
        EventSequence lastEventSequence)
    {
        var logs = shift.Logs
            .Select(log => new SnapshotLog(
                log.LogId,
                log.TrueSpecies,
                log.DeclaredSpecies,
                log.Anomaly,
                log.State,
                log.Flags.ToImmutableArray(),
                shift.ProcedureProgressByLog.TryGetValue(log.LogId, out var progress)
                    ? new SnapshotProcedureProgress(progress.LogId, progress.AnomalyId, progress.CompletedStepCount, progress.IsComplete)
                    : null,
                shift.ConfirmationResultsByLog.TryGetValue(log.LogId, out var result)
                    ? new SnapshotConfirmationResult(result.LogId, result.AnomalyId, result.Result, result.RequiredTools.ToImmutableArray(), result.Duration, result.CompletedAt)
                    : null))
            .ToImmutableArray();

        var schedulerState = new SnapshotSchedulerState(
            shift.PendingFeed is { } feed ? new SnapshotPendingFeed(feed.LogId, feed.Kind, feed.ScheduledAt, feed.Delay, feed.CausedByIntentId) : null,
            shift.ActiveIntakeDeadline is { } deadline ? new SnapshotIntakeDeadline(deadline.LogId, deadline.StartedAt, deadline.Duration) : null,
            shift.ActiveProcedureHold is { } hold
                ? new SnapshotProcedureHold(hold.LogId, hold.AnomalyId, hold.AttemptedItem, hold.ProcedureStepIndex, hold.StartedAt, hold.Duration)
                : null,
            shift.ActiveConfirmationTest is { } confirmation
                ? new SnapshotConfirmationTest(
                    confirmation.LogId,
                    confirmation.AnomalyId,
                    confirmation.Plan.RequiredTools.ToImmutableArray(),
                    confirmation.Plan.Duration,
                    confirmation.Plan.Continuous,
                    confirmation.Plan.RequiredLineNoise,
                    confirmation.Plan.ResetWhenConditionLost,
                    confirmation.Plan.Result,
                    confirmation.AccumulatedValidDuration,
                    confirmation.SegmentStartedAt,
                    confirmation.IsRunning,
                    confirmation.LastConditionBoundaryAt)
                : null,
            shift.ActiveSawCycle is { } cycle ? new SnapshotSawCycle(cycle.LogId, cycle.StartedAt, cycle.Duration) : null,
            shift.ProcessedIntentIds.ToImmutableArray(),
            new SnapshotProgression(progression.HasCompletedTick, progression.LastReceipt?.ShiftCompleted ?? false));

        var lineState = new SnapshotLineState(
            shift.Line.State,
            shift.Line.EnteredAt,
            shift.Line.Cause,
            shift.Line.PendingLogId,
            shift.Line.ActiveRepairHold is { } repair ? new SnapshotRepairHold(repair.StartedAt, repair.Duration) : null,
            new SnapshotLineNoise(
                lineNoise.Current,
                lineNoise.LastEvaluatedAt,
                lineNoise.LastChangedAt,
                lineNoise.LatestSources.SawActive,
                lineNoise.LatestSources.MovementNoiseActive,
                lineNoise.LatestSources.RepairActive),
            movementNoise.LastAcceptedMovement is { } movement
                ? new SnapshotMovementNoise(
                    movement.Source,
                    movement.LogId,
                    movement.SourceState,
                    movement.DestinationState,
                    movement.PriorStateVersion,
                    movement.CurrentStateVersion,
                    movement.AcceptedAt,
                    movementNoise.StartedAt,
                    movementNoise.DueAt)
                : null);

        var containmentState = new SnapshotContainmentState(
            shift.Containment.State,
            shift.Containment.EnteredAt,
            shift.Containment.DeadlineAt,
            shift.ActiveContainmentRitual is { } ritual ? new SnapshotContainmentRitual(ritual.StartedAt, ritual.Duration) : null);

        var inventory = new SnapshotInventory(
            shift.Inventory.ConsumableQuantities.Select(pair => new SnapshotConsumable(pair.Key, pair.Value)).ToImmutableArray(),
            shift.Inventory.ReusableItems.ToImmutableArray());

        var quotaState = new SnapshotQuota(
            quota.CreditedBySpecies.Select(pair => new SnapshotSpeciesCount(pair.Key, pair.Value)).ToImmutableArray(),
            quota.TotalCreditedUnits,
            quota.CorrectlyProcessedAnomalies,
            quota.SettledLogIds.ToImmutableArray());

        var objectives = new SnapshotObjectives(
            lifecycle.SelectedProfileId,
            quota.TargetTotal,
            quota.TargetBySpecies.Select(pair => new SnapshotSpeciesCount(pair.Key, pair.Value)).ToImmutableArray(),
            quota.MinimumCorrectlyProcessedAnomalies,
            lifecycle.StartedAt,
            lifecycle.HardDeadlineDuration,
            lifecycle.Completion is { } completion
                ? new SnapshotCompletion(
                    completion.CompletedAt,
                    completion.Reason,
                    completion.AllLogsTerminal,
                    completion.HardDeadlineReached,
                    completion.ObjectivesSatisfied,
                    completion.ProcessedCount,
                    completion.WrittenOffCount)
                : null);

        return new ShiftSnapshot(
            shift.ShiftId,
            serverTick,
            shift.StateVersion,
            lastEventSequence,
            schedulerState,
            logs,
            lineState,
            containmentState,
            inventory,
            quotaState,
            objectives);
    }
}
