using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Journal;

/// <summary>Why a snapshot could not be restored against the supplied validated configuration.</summary>
public enum ShiftSnapshotRestoreRejection
{
    ShiftMismatch,
    ProfileMismatch,
    ManifestMismatch,
    ObjectivesMismatch,
    InventoryMismatch,
    ContradictoryEvidence
}

public abstract record ShiftSnapshotRestoreResult;

public sealed record ShiftSnapshotRestoreRejected(ShiftSnapshotRestoreRejection Reason, string Detail) : ShiftSnapshotRestoreResult;

/// <summary>
/// Snapshot-specific reconstruction evidence. It carries the exact independent immutable instances of the established
/// separate host-owned states so a caller can resume simulation, and it is deliberately not accepted as an input by
/// <c>HostTickExecutionService</c>: D-013 separation is preserved because the host still receives each state on its own
/// parameter. This record is a restoration result, never a live aggregate.
/// </summary>
public sealed record ShiftSnapshotRestored(
    ShiftRuntimeState ShiftState,
    QuotaRuntimeState QuotaState,
    MovementNoiseRuntimeState MovementNoise,
    LineNoiseRuntimeState LineNoise,
    HostTickProgressionEvidence Progression,
    ShiftLifecycleRuntimeState Lifecycle,
    ServerTick ServerTick,
    SnapshotBoundary Boundary) : ShiftSnapshotRestoreResult;

/// <summary>
/// Deterministic restoration of the separate host-owned runtime states from an immutable snapshot value.
/// <para>
/// Correlated evidence is rebuilt in dependency order — shift state, then movement noise, then line noise, then quota,
/// then lifecycle and finally checkpoint progression — because the later values retain the earlier ones. Every
/// reconstruction goes through an existing validated factory or the one snapshot-specific seam, so all frozen
/// invariants still apply and no gameplay rule is re-executed.
/// </para>
/// </summary>
public sealed class ShiftSnapshotRestoreService
{
    public ShiftSnapshotRestoreResult Restore(ShiftSnapshot snapshot, ShiftConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.ShiftId != snapshot.ShiftId)
        {
            return Reject(ShiftSnapshotRestoreRejection.ShiftMismatch, $"configuration shift {configuration.ShiftId} != snapshot shift {snapshot.ShiftId}");
        }

        if (configuration.Profiles is null || !configuration.Profiles.ContainsKey(snapshot.Objectives.SelectedProfileId))
        {
            return Reject(ShiftSnapshotRestoreRejection.ProfileMismatch, $"profile {snapshot.Objectives.SelectedProfileId} is not configured");
        }

        var manifestRejection = ValidateManifest(snapshot, configuration);
        if (manifestRejection is not null)
        {
            return manifestRejection;
        }

        var objectivesRejection = ValidateObjectives(snapshot, configuration);
        if (objectivesRejection is not null)
        {
            return objectivesRejection;
        }

        ShiftRuntimeState shift;
        QuotaRuntimeState quota;
        MovementNoiseRuntimeState movementNoise;
        LineNoiseRuntimeState lineNoise;
        ShiftLifecycleRuntimeState lifecycle;
        HostTickProgressionEvidence progression;

        try
        {
            var inventory = RestoreInventory(snapshot, configuration);
            if (inventory is null)
            {
                return Reject(ShiftSnapshotRestoreRejection.InventoryMismatch, "snapshot inventory does not match the configured resources");
            }

            shift = RestoreShift(snapshot, configuration, inventory);
            quota = QuotaRuntimeState.Create(configuration).WithSettlement(
                snapshot.Quota.CreditedBySpecies.ToImmutableDictionary(entry => entry.Species, entry => entry.Units),
                snapshot.Quota.TotalCreditedUnits,
                snapshot.Quota.CorrectlyProcessedAnomalies,
                snapshot.Quota.SettledLogIds.ToImmutableHashSet());
            movementNoise = RestoreMovementNoise(snapshot);
            lineNoise = RestoreLineNoise(snapshot, shift, movementNoise);
            lifecycle = RestoreLifecycle(snapshot, configuration, shift, quota);
            progression = RestoreProgression(snapshot, shift, quota, lifecycle);
        }
        catch (ArgumentException exception)
        {
            return Reject(ShiftSnapshotRestoreRejection.ContradictoryEvidence, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Reject(ShiftSnapshotRestoreRejection.ContradictoryEvidence, exception.Message);
        }

        return new ShiftSnapshotRestored(shift, quota, movementNoise, lineNoise, progression, lifecycle, snapshot.ServerTick, snapshot.Boundary);
    }

    private static ShiftSnapshotRestoreRejected Reject(ShiftSnapshotRestoreRejection reason, string detail) => new(reason, detail);

    private static ShiftSnapshotRestoreRejected? ValidateManifest(ShiftSnapshot snapshot, ShiftConfiguration configuration)
    {
        if (configuration.Manifest.IsDefaultOrEmpty || configuration.Manifest.Length != snapshot.Logs.Length)
        {
            return Reject(ShiftSnapshotRestoreRejection.ManifestMismatch, "snapshot manifest length does not match configuration");
        }

        for (var index = 0; index < snapshot.Logs.Length; index++)
        {
            var log = snapshot.Logs[index];
            var configured = configuration.Manifest[index];
            if (log.LogId != configured.Id || log.TrueSpecies != configured.TrueSpecies ||
                log.DeclaredSpecies != configured.DeclaredSpecies || log.Anomaly != configured.Anomaly)
            {
                return Reject(ShiftSnapshotRestoreRejection.ManifestMismatch, $"snapshot log {log.LogId} does not match the configured manifest entry at index {index}");
            }
        }

        return null;
    }

    private static ShiftSnapshotRestoreRejected? ValidateObjectives(ShiftSnapshot snapshot, ShiftConfiguration configuration)
    {
        var objectives = configuration.Objectives;
        if (objectives?.Quota?.BySpecies is null ||
            objectives.Quota.Total != snapshot.Objectives.TargetTotal ||
            objectives.MinCorrectlyProcessedAnomalies != snapshot.Objectives.MinimumCorrectlyProcessedAnomalies ||
            objectives.Quota.BySpecies.Count != snapshot.Objectives.TargetBySpecies.Length ||
            snapshot.Objectives.TargetBySpecies.Any(entry => !objectives.Quota.BySpecies.TryGetValue(entry.Species, out var units) || units != entry.Units))
        {
            return Reject(ShiftSnapshotRestoreRejection.ObjectivesMismatch, "snapshot objectives do not match the configured objectives");
        }

        var profile = configuration.Profiles[snapshot.Objectives.SelectedProfileId];
        if (snapshot.Objectives.HardDeadlineDuration.Value != profile.HardShiftDeadlineSeconds || snapshot.Objectives.StartedAt != ServerTick.Zero)
        {
            return Reject(ShiftSnapshotRestoreRejection.ObjectivesMismatch, "snapshot lifecycle window does not match the selected profile");
        }

        return null;
    }

    /// <summary>Rebuilds the inventory from the pristine configured resources using the established consumption seam.</summary>
    private static RuntimeInventory? RestoreInventory(ShiftSnapshot snapshot, ShiftConfiguration configuration)
    {
        var inventory = RuntimeInventory.Create(configuration.Resources);
        if (inventory.ConsumableQuantities.Count != snapshot.Inventory.Consumables.Length ||
            inventory.ReusableItems.Count != snapshot.Inventory.ReusableItems.Length ||
            snapshot.Inventory.ReusableItems.Any(item => !inventory.ReusableItems.Contains(item)))
        {
            return null;
        }

        foreach (var consumable in snapshot.Inventory.Consumables)
        {
            if (!inventory.ConsumableQuantities.TryGetValue(consumable.Item, out var configured) || consumable.Quantity > configured)
            {
                return null;
            }

            for (var consumed = configured; consumed > consumable.Quantity; consumed--)
            {
                if (!inventory.TryConsume(consumable.Item, out inventory))
                {
                    return null;
                }
            }
        }

        return inventory;
    }

    private static ShiftRuntimeState RestoreShift(ShiftSnapshot snapshot, ShiftConfiguration configuration, RuntimeInventory inventory)
    {
        var logs = snapshot.Logs
            .Select(log => new LogRuntimeState(
                log.LogId,
                log.TrueSpecies,
                log.DeclaredSpecies,
                log.Anomaly,
                log.State,
                log.Flags.ToImmutableHashSet()))
            .ToImmutableArray();

        var progressByLog = snapshot.Logs
            .Where(log => log.ProcedureProgress is not null)
            .ToImmutableDictionary(
                log => log.LogId,
                log => new ProcedureProgress(log.ProcedureProgress!.LogId, log.ProcedureProgress.AnomalyId, log.ProcedureProgress.CompletedStepCount, log.ProcedureProgress.IsComplete));

        var resultsByLog = snapshot.Logs
            .Where(log => log.ConfirmationResult is not null)
            .ToImmutableDictionary(
                log => log.LogId,
                log => new ConfirmationTestResult(
                    log.ConfirmationResult!.LogId,
                    log.ConfirmationResult.AnomalyId,
                    log.ConfirmationResult.Result,
                    log.ConfirmationResult.RequiredTools.ToImmutableHashSet(),
                    log.ConfirmationResult.Duration,
                    log.ConfirmationResult.CompletedAt));

        var scheduler = snapshot.SchedulerState;
        var line = snapshot.LineState;

        return ShiftRuntimeState.RestoreForSnapshot(
            configuration,
            snapshot.StateVersion,
            logs,
            scheduler.ProcessedIntentIds.ToImmutableHashSet(),
            scheduler.PendingFeed is { } feed
                ? new PendingFeedSchedule(feed.LogId, feed.Kind, feed.ScheduledAt, feed.Delay, feed.CausedByIntentId)
                : null,
            inventory,
            progressByLog,
            scheduler.ActiveProcedureHold is { } hold
                ? new ActiveProcedureHold(hold.LogId, hold.AnomalyId, hold.AttemptedItem, hold.ProcedureStepIndex, hold.StartedAt, hold.StartedAt + hold.Duration, hold.Duration)
                : null,
            RestoreConfirmationTest(scheduler.ActiveConfirmationTest),
            resultsByLog,
            new ContainmentRuntimeState(snapshot.ContainmentState.State, snapshot.ContainmentState.EnteredAt, snapshot.ContainmentState.DeadlineAt),
            snapshot.ContainmentState.ActiveRitual is { } ritual
                ? new ActiveContainmentRitual(ritual.StartedAt, ritual.StartedAt + ritual.Duration, ritual.Duration)
                : null,
            new LineRuntimeState(
                line.State,
                line.EnteredAt,
                line.Cause,
                line.PendingLogId,
                line.ActiveRepairHold is { } repair ? new ActiveRepairHold(repair.StartedAt, repair.StartedAt + repair.Duration, repair.Duration) : null),
            scheduler.ActiveIntakeDeadline is { } deadline ? new ActiveIntakeDeadline(deadline.LogId, deadline.StartedAt, deadline.Duration) : null,
            scheduler.ActiveSawCycle is { } cycle ? new ActiveSawCycle(cycle.LogId, cycle.StartedAt, cycle.Duration) : null);
    }

    private static ActiveConfirmationTest? RestoreConfirmationTest(SnapshotConfirmationTest? confirmation)
    {
        if (confirmation is null)
        {
            return null;
        }

        var plan = new ConfirmationTestPlan(
            confirmation.AnomalyId,
            confirmation.RequiredTools.ToImmutableHashSet(),
            confirmation.PlanDuration,
            confirmation.Continuous,
            confirmation.RequiredLineNoise,
            confirmation.ResetWhenConditionLost,
            confirmation.Result);

        var remaining = SimulationDuration.FromTicks(plan.Duration.Value - confirmation.AccumulatedValidDuration.Value);
        return new ActiveConfirmationTest(
            confirmation.LogId,
            confirmation.AnomalyId,
            plan,
            confirmation.AccumulatedValidDuration,
            confirmation.SegmentStartedAt,
            confirmation.SegmentStartedAt is { } segment ? segment + remaining : null,
            confirmation.IsRunning,
            confirmation.LastConditionBoundaryAt);
    }

    private static MovementNoiseRuntimeState RestoreMovementNoise(ShiftSnapshot snapshot)
    {
        var runtime = MovementNoiseRuntimeState.Create(snapshot.ShiftId);
        if (snapshot.LineState.MovementNoise is not { } movement)
        {
            return runtime;
        }

        return runtime.Apply(
            new MovementNoiseAcceptedMovement(
                movement.Source,
                movement.LogId,
                movement.SourceState,
                movement.DestinationState,
                movement.PriorStateVersion,
                movement.CurrentStateVersion,
                movement.AcceptedAt),
            movement.StartedAt,
            movement.DueAt);
    }

    private static LineNoiseRuntimeState RestoreLineNoise(ShiftSnapshot snapshot, ShiftRuntimeState shift, MovementNoiseRuntimeState movementNoise)
    {
        var runtime = LineNoiseRuntimeState.Create(snapshot.ShiftId);
        var noise = snapshot.LineState.LineNoise;
        if (noise.LastEvaluatedAt is not { } evaluatedAt)
        {
            return runtime;
        }

        var sources = new LineNoiseSourceSnapshot(noise.SawActive, noise.MovementNoiseActive, noise.RepairActive);
        if (sources.DerivedValue != noise.Current)
        {
            throw new ArgumentException("Snapshot line noise must agree with its retained sources.", nameof(snapshot));
        }

        var evidence = new LineNoiseEvaluationEvidence(shift, movementNoise, evaluatedAt, sources);
        return runtime.Apply(noise.Current, evaluatedAt, sources, evidence, noise.LastChangedAt);
    }

    private static ShiftLifecycleRuntimeState RestoreLifecycle(ShiftSnapshot snapshot, ShiftConfiguration configuration, ShiftRuntimeState shift, QuotaRuntimeState quota)
    {
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, snapshot.Objectives.SelectedProfileId);
        if (snapshot.Objectives.Completion is not { } completion)
        {
            return lifecycle;
        }

        var evidence = new ShiftCompletionEvidence(
            completion.CompletedAt,
            lifecycle.HardDeadlineAt,
            completion.Reason,
            completion.AllLogsTerminal,
            completion.HardDeadlineReached,
            completion.ObjectivesSatisfied,
            completion.ProcessedCount,
            completion.WrittenOffCount,
            shift,
            quota);
        return lifecycle.Complete(evidence);
    }

    private static HostTickProgressionEvidence RestoreProgression(
        ShiftSnapshot snapshot,
        ShiftRuntimeState shift,
        QuotaRuntimeState quota,
        ShiftLifecycleRuntimeState lifecycle)
    {
        var progression = HostTickProgressionEvidence.Create(snapshot.ShiftId);
        if (!snapshot.SchedulerState.Progression.HasCompletedTick)
        {
            return progression;
        }

        ShiftCompletionEvaluationResult evaluation = lifecycle.IsCompleted
            ? new ShiftCompletionNewlyCompleted(lifecycle, shift, quota)
            : new ShiftCompletionActive(lifecycle, shift, quota, false, false);

        return progression.Advance(HostTickCompletionReceipt.Create(snapshot.ServerTick, evaluation));
    }
}
