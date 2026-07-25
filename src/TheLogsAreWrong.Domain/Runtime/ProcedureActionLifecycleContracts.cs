using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Runtime;

public sealed record ActiveProcedureHold
{
    public ActiveProcedureHold(
        LogId logId,
        AnomalyId anomalyId,
        ItemId attemptedItem,
        int procedureStepIndex,
        ServerTick startTick,
        ServerTick dueTick,
        SimulationDuration duration)
    {
        if (logId.IsDefault || anomalyId.IsDefault || attemptedItem.IsDefault)
        {
            throw new ArgumentException("Active procedure hold identities must be initialized.");
        }

        if (procedureStepIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(procedureStepIndex), "Procedure step index cannot be negative.");
        }

        if (startTick.IsDefault || dueTick.IsDefault)
        {
            throw new ArgumentException("Active procedure hold ticks must be initialized.");
        }

        if (duration.IsDefault || duration.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Active procedure hold duration must be positive.");
        }

        if (dueTick != startTick + duration)
        {
            throw new ArgumentException("Active procedure hold due tick must equal start tick plus duration.", nameof(dueTick));
        }

        LogId = logId;
        AnomalyId = anomalyId;
        AttemptedItem = attemptedItem;
        ProcedureStepIndex = procedureStepIndex;
        StartedAt = startTick;
        DueAt = dueTick;
        Duration = duration;
    }

    public LogId LogId { get; }
    public AnomalyId AnomalyId { get; }
    public ItemId AttemptedItem { get; }
    public int ProcedureStepIndex { get; }
    public ServerTick StartedAt { get; }
    public ServerTick DueAt { get; }
    public SimulationDuration Duration { get; }
}

public enum ProcedureActionStartRejectionReason
{
    TargetNotFound,
    TargetNotAtProcedure,
    ActiveHoldAlreadyExists,
    NoProcedurePlan,
    MissingItem,
    OutOfOrderItem,
    RepeatedCorrectStep,
    UnconfiguredWrongAction
}

public abstract record ProcedureActionStartResult(ShiftRuntimeState State);

public sealed record ProcedureActionHoldStarted(ShiftRuntimeState State, ActiveProcedureHold Hold)
    : ProcedureActionStartResult(State);

public sealed record ProcedureActionCompletedImmediately(ShiftRuntimeState State, ItemActionCompletionDescriptor Descriptor)
    : ProcedureActionStartResult(State);

public sealed record ProcedureActionStartRejected(ShiftRuntimeState State, ProcedureActionStartRejectionReason Reason)
    : ProcedureActionStartResult(State);

public sealed class ProcedureActionStartService
{
    private readonly ItemActionCompletionService _completion = new();

    public ProcedureActionStartResult Start(
        ShiftRuntimeState state,
        LogId logId,
        ItemId attemptedItem,
        ServerTick currentTick,
        AnomalyCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (logId.IsDefault)
        {
            throw new ArgumentException("Log identifier must be initialized.", nameof(logId));
        }

        if (attemptedItem.IsDefault)
        {
            throw new ArgumentException("Attempted item identifier must be initialized.", nameof(attemptedItem));
        }

        if (currentTick.IsDefault)
        {
            throw new ArgumentException("Current tick must be initialized.", nameof(currentTick));
        }

        ArgumentNullException.ThrowIfNull(catalog);
        if (!state.TryGetLog(logId, out var log))
        {
            return Reject(state, ProcedureActionStartRejectionReason.TargetNotFound);
        }

        if (log.State != LogState.AT_PROCEDURE)
        {
            return Reject(state, ProcedureActionStartRejectionReason.TargetNotAtProcedure);
        }

        if (state.ActiveProcedureHold is not null)
        {
            return Reject(state, ProcedureActionStartRejectionReason.ActiveHoldAlreadyExists);
        }

        var plans = new ProcedurePlanResolver(catalog);
        if (!plans.TryGetPlan(log, out var plan))
        {
            return Reject(state, ProcedureActionStartRejectionReason.NoProcedurePlan);
        }

        if (plan!.Steps.IsEmpty)
        {
            throw new ArgumentException("Procedure plans must contain at least one step before an action can start.", nameof(catalog));
        }

        var hasPriorProgress = state.TryGetProcedureProgress(logId, out var priorProgress);
        ValidateProgress(plan, priorProgress, hasPriorProgress, state);
        if (hasPriorProgress && priorProgress.IsComplete)
        {
            return plan.Steps.Any(step => step.Item == attemptedItem)
                ? Reject(state, ProcedureActionStartRejectionReason.RepeatedCorrectStep)
                : CompleteImmediately(state, logId, attemptedItem, catalog);
        }

        var nextStepIndex = hasPriorProgress ? priorProgress.CompletedStepCount : 0;
        var nextStep = plan.Steps[nextStepIndex];
        if (nextStep.Item == attemptedItem)
        {
            if (!state.Inventory.IsAvailable(attemptedItem))
            {
                return Reject(state, ProcedureActionStartRejectionReason.MissingItem);
            }

            if (nextStep.HoldSeconds is { } holdSeconds && holdSeconds > 0)
            {
                var duration = SimulationDuration.FromTicks(holdSeconds);
                var hold = new ActiveProcedureHold(
                    logId,
                    plan.AnomalyId,
                    attemptedItem,
                    nextStepIndex,
                    currentTick,
                    currentTick + duration,
                    duration);
                var updatedState = state.StartActiveProcedureHold(hold);
                return new ProcedureActionHoldStarted(updatedState, hold);
            }

            return CompleteImmediately(state, logId, attemptedItem, catalog);
        }

        if (WrongActionResolver.Resolve(log, attemptedItem, catalog) is ConfiguredWrongActionResolution)
        {
            return CompleteImmediately(state, logId, attemptedItem, catalog);
        }

        if (!state.Inventory.IsAvailable(attemptedItem))
        {
            return Reject(state, ProcedureActionStartRejectionReason.MissingItem);
        }

        return plan.Steps.Any(step => step.Item == attemptedItem)
            ? Reject(state, ProcedureActionStartRejectionReason.OutOfOrderItem)
            : Reject(state, ProcedureActionStartRejectionReason.UnconfiguredWrongAction);
    }

    private ProcedureActionStartResult CompleteImmediately(ShiftRuntimeState state, LogId logId, ItemId attemptedItem, AnomalyCatalog catalog)
    {
        var completion = _completion.Complete(state, logId, attemptedItem, catalog);
        return completion switch
        {
            ItemActionCompletionAccepted accepted => new ProcedureActionCompletedImmediately(accepted.State, accepted.Descriptor),
            ItemActionCompletionRejected rejected => Reject(rejected.State, Map(rejected.Reason)),
            _ => throw new InvalidOperationException("Unknown item action completion result.")
        };
    }

    private static void ValidateProgress(ProcedurePlan plan, ProcedureProgress progress, bool hasProgress, ShiftRuntimeState state)
    {
        if (!hasProgress)
        {
            return;
        }

        if (progress.AnomalyId != plan.AnomalyId || progress.CompletedStepCount > plan.Steps.Length || (progress.IsComplete && progress.CompletedStepCount != plan.Steps.Length) || (!progress.IsComplete && progress.CompletedStepCount == plan.Steps.Length))
        {
            throw new ArgumentException("Stored procedure progress must exactly match the procedure plan.", nameof(state));
        }
    }

    private static ProcedureActionStartRejected Reject(ShiftRuntimeState state, ProcedureActionStartRejectionReason reason) => new(state, reason);

    private static ProcedureActionStartRejectionReason Map(ProcedureCompletionRejectionReason reason) => reason switch
    {
        ProcedureCompletionRejectionReason.TargetNotFound => ProcedureActionStartRejectionReason.TargetNotFound,
        ProcedureCompletionRejectionReason.TargetNotAtProcedure => ProcedureActionStartRejectionReason.TargetNotAtProcedure,
        ProcedureCompletionRejectionReason.NoProcedurePlan => ProcedureActionStartRejectionReason.NoProcedurePlan,
        ProcedureCompletionRejectionReason.MissingItem => ProcedureActionStartRejectionReason.MissingItem,
        ProcedureCompletionRejectionReason.OutOfOrderItem => ProcedureActionStartRejectionReason.OutOfOrderItem,
        ProcedureCompletionRejectionReason.RepeatedCorrectStep => ProcedureActionStartRejectionReason.RepeatedCorrectStep,
        ProcedureCompletionRejectionReason.UnconfiguredWrongAction => ProcedureActionStartRejectionReason.UnconfiguredWrongAction,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown procedure completion rejection reason.")
    };
}

public enum ProcedureActionCancellationRejectionReason
{
    NoActiveHold,
    HeldLogMismatch
}

public abstract record ProcedureActionCancellationResult(ShiftRuntimeState State);

public sealed record ProcedureActionCancelled(ShiftRuntimeState State, ActiveProcedureHold Hold)
    : ProcedureActionCancellationResult(State);

public sealed record ProcedureActionCancellationRejected(ShiftRuntimeState State, ProcedureActionCancellationRejectionReason Reason)
    : ProcedureActionCancellationResult(State);

public sealed class ProcedureActionCancellationService
{
    public ProcedureActionCancellationResult Cancel(ShiftRuntimeState state, LogId logId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (logId.IsDefault)
        {
            throw new ArgumentException("Log identifier must be initialized.", nameof(logId));
        }

        if (state.ActiveProcedureHold is not { } hold)
        {
            return new ProcedureActionCancellationRejected(state, ProcedureActionCancellationRejectionReason.NoActiveHold);
        }

        if (hold.LogId != logId)
        {
            return new ProcedureActionCancellationRejected(state, ProcedureActionCancellationRejectionReason.HeldLogMismatch);
        }

        return new ProcedureActionCancelled(state.ClearActiveProcedureHold(), hold);
    }
}

public enum ProcedureActionDueCompletionFailureReason
{
    HeldLogNotAtProcedure,
    HeldLogIdentityMismatch,
    NoProcedurePlan,
    StoredProgressMismatch,
    HeldStepMismatch,
    CompletionRejected
}

public abstract record ProcedureActionDueCompletionResult(ShiftRuntimeState State);

public sealed record ProcedureActionNoActiveHold(ShiftRuntimeState State)
    : ProcedureActionDueCompletionResult(State);

public sealed record ProcedureActionNotDue(ShiftRuntimeState State, ServerTick DueAt)
    : ProcedureActionDueCompletionResult(State);

public sealed record ProcedureActionDueCompleted(ShiftRuntimeState State, ItemActionCompletionDescriptor Descriptor)
    : ProcedureActionDueCompletionResult(State);

public sealed record ProcedureActionDueCompletionFailed(
    ShiftRuntimeState State,
    ProcedureActionDueCompletionFailureReason Reason,
    ProcedureCompletionRejectionReason? CompletionRejection)
    : ProcedureActionDueCompletionResult(State);

public sealed class ProcedureActionDueCompletionService
{
    private readonly ItemActionCompletionService _completion = new();

    public ProcedureActionDueCompletionResult CompleteDue(ShiftRuntimeState state, ServerTick currentTick, AnomalyCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (currentTick.IsDefault)
        {
            throw new ArgumentException("Current tick must be initialized.", nameof(currentTick));
        }

        ArgumentNullException.ThrowIfNull(catalog);
        if (state.ActiveProcedureHold is not { } hold)
        {
            return new ProcedureActionNoActiveHold(state);
        }

        if (currentTick < hold.DueAt)
        {
            return new ProcedureActionNotDue(state, hold.DueAt);
        }

        var validation = ValidateDueHold(state, hold, catalog);
        if (validation is { } failure)
        {
            return new ProcedureActionDueCompletionFailed(state, failure, null);
        }

        var completion = _completion.CompleteActiveProcedureHold(state, hold, catalog);
        return completion switch
        {
            ItemActionCompletionAccepted accepted => new ProcedureActionDueCompleted(accepted.State, accepted.Descriptor),
            ItemActionCompletionRejected rejected => new ProcedureActionDueCompletionFailed(
                rejected.State,
                ProcedureActionDueCompletionFailureReason.CompletionRejected,
                rejected.Reason),
            _ => throw new InvalidOperationException("Unknown item action completion result.")
        };
    }

    private static ProcedureActionDueCompletionFailureReason? ValidateDueHold(ShiftRuntimeState state, ActiveProcedureHold hold, AnomalyCatalog catalog)
    {
        if (!state.TryGetLog(hold.LogId, out var log) || log.State != LogState.AT_PROCEDURE)
        {
            return ProcedureActionDueCompletionFailureReason.HeldLogNotAtProcedure;
        }

        if (log.Anomaly != hold.AnomalyId)
        {
            return ProcedureActionDueCompletionFailureReason.HeldLogIdentityMismatch;
        }

        var plans = new ProcedurePlanResolver(catalog);
        ProcedurePlan? plan;
        try
        {
            if (!plans.TryGetPlan(log, out plan))
            {
                return ProcedureActionDueCompletionFailureReason.NoProcedurePlan;
            }
        }
        catch (AnomalyDefinitionNotFoundException)
        {
            return ProcedureActionDueCompletionFailureReason.NoProcedurePlan;
        }

        if (plan!.Steps.IsEmpty)
        {
            return ProcedureActionDueCompletionFailureReason.NoProcedurePlan;
        }

        var hasProgress = state.TryGetProcedureProgress(hold.LogId, out var progress);
        if (hasProgress && (progress.AnomalyId != plan.AnomalyId || progress.IsComplete || progress.CompletedStepCount >= plan.Steps.Length))
        {
            return ProcedureActionDueCompletionFailureReason.StoredProgressMismatch;
        }

        var expectedIndex = hasProgress ? progress.CompletedStepCount : 0;
        if (expectedIndex != hold.ProcedureStepIndex)
        {
            return ProcedureActionDueCompletionFailureReason.HeldStepMismatch;
        }

        var expectedStep = plan.Steps[expectedIndex];
        if (expectedStep.Item != hold.AttemptedItem || expectedStep.HoldSeconds is not { } holdSeconds || holdSeconds <= 0 || SimulationDuration.FromTicks(holdSeconds) != hold.Duration)
        {
            return ProcedureActionDueCompletionFailureReason.HeldStepMismatch;
        }

        return null;
    }
}
