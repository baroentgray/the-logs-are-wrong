using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Runtime;

public sealed class RuntimeInventory
{
    private RuntimeInventory(ImmutableDictionary<ItemId, int> consumableQuantities, ImmutableHashSet<ItemId> reusableItems)
    {
        ConsumableQuantities = consumableQuantities;
        ReusableItems = reusableItems;
    }

    public ImmutableDictionary<ItemId, int> ConsumableQuantities { get; }
    public ImmutableHashSet<ItemId> ReusableItems { get; }

    public static RuntimeInventory Create(ResourcesConfiguration resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(resources.Consumable);
        ArgumentNullException.ThrowIfNull(resources.Reusable);

        var consumables = ImmutableDictionary.CreateBuilder<ItemId, int>();
        foreach (var quantity in resources.Consumable)
        {
            if (quantity.Key.IsDefault)
            {
                throw new ArgumentException("Consumable items must use initialized identifiers.", nameof(resources));
            }

            if (quantity.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(resources), "Consumable quantities cannot be negative.");
            }

            consumables.Add(quantity.Key, quantity.Value);
        }

        var reusable = ImmutableHashSet.CreateBuilder<ItemId>();
        foreach (var item in resources.Reusable)
        {
            if (item.IsDefault)
            {
                throw new ArgumentException("Reusable items must use initialized identifiers.", nameof(resources));
            }

            if (consumables.ContainsKey(item))
            {
                throw new ArgumentException("An item cannot be both consumable and reusable.", nameof(resources));
            }

            reusable.Add(item);
        }

        return new RuntimeInventory(consumables.ToImmutable(), reusable.ToImmutable());
    }

    public int GetConsumableQuantity(ItemId item)
    {
        if (item.IsDefault)
        {
            throw new ArgumentException("Item identifier must be initialized.", nameof(item));
        }

        return ConsumableQuantities.GetValueOrDefault(item);
    }

    public bool IsAvailable(ItemId item)
    {
        if (item.IsDefault)
        {
            throw new ArgumentException("Item identifier must be initialized.", nameof(item));
        }

        return ReusableItems.Contains(item) || GetConsumableQuantity(item) > 0;
    }

    internal bool TryConsume(ItemId item, out RuntimeInventory consumed)
    {
        if (item.IsDefault)
        {
            throw new ArgumentException("Item identifier must be initialized.", nameof(item));
        }

        var quantity = GetConsumableQuantity(item);
        if (quantity <= 0)
        {
            consumed = this;
            return false;
        }

        consumed = new RuntimeInventory(ConsumableQuantities.SetItem(item, quantity - 1), ReusableItems);
        return true;
    }

    public bool ValueEquals(RuntimeInventory? other) =>
        other is not null &&
        ConsumableQuantities.Count == other.ConsumableQuantities.Count &&
        ConsumableQuantities.All(pair => other.ConsumableQuantities.TryGetValue(pair.Key, out var value) && value == pair.Value) &&
        ReusableItems.SetEquals(other.ReusableItems);
}

public sealed record ProcedureProgress
{
    public ProcedureProgress(LogId logId, AnomalyId anomalyId, int completedStepCount, bool isComplete)
    {
        if (logId.IsDefault || anomalyId.IsDefault)
        {
            throw new ArgumentException("Procedure progress must retain initialized log and anomaly identifiers.");
        }

        if (completedStepCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(completedStepCount), "Completed step count cannot be negative.");
        }

        LogId = logId;
        AnomalyId = anomalyId;
        CompletedStepCount = completedStepCount;
        IsComplete = isComplete;
    }

    public LogId LogId { get; }
    public AnomalyId AnomalyId { get; }
    public int CompletedStepCount { get; }
    public bool IsComplete { get; }
}

public enum ItemActionCompletionKind
{
    CorrectProcedureStep,
    ConfiguredWrongAction
}

public enum ProcedureCompletionRejectionReason
{
    TargetNotFound,
    TargetNotAtProcedure,
    NoProcedurePlan,
    MissingItem,
    OutOfOrderItem,
    RepeatedCorrectStep,
    UnconfiguredWrongAction
}

public sealed record ItemActionCompletionDescriptor(
    LogId LogId,
    ItemId AttemptedItem,
    ItemActionCompletionKind Kind,
    bool ItemConsumed,
    ProcedureProgress? PriorProgress,
    ProcedureProgress? CurrentProgress,
    ImmutableHashSet<FlagId> NewlyGrantedFlags,
    ImmutableArray<EffectDefinition> Effects,
    StateVersion PriorStateVersion,
    StateVersion CurrentStateVersion);

public abstract record ItemActionCompletionResult(ShiftRuntimeState State);

public sealed record ItemActionCompletionAccepted(ShiftRuntimeState State, ItemActionCompletionDescriptor Descriptor)
    : ItemActionCompletionResult(State);

public sealed record ItemActionCompletionRejected(ShiftRuntimeState State, ProcedureCompletionRejectionReason Reason)
    : ItemActionCompletionResult(State);

public sealed class ItemActionCompletionService
{
    public ItemActionCompletionResult Complete(ShiftRuntimeState state, LogId logId, ItemId attemptedItem, AnomalyCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        return CompleteCore(state, logId, attemptedItem, catalog, state.ActiveProcedureHold);
    }

    internal ItemActionCompletionResult CompleteActiveProcedureHold(ShiftRuntimeState state, ActiveProcedureHold activeProcedureHold, AnomalyCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(activeProcedureHold);
        return CompleteCore(state, activeProcedureHold.LogId, activeProcedureHold.AttemptedItem, catalog, null);
    }

    private static ItemActionCompletionResult CompleteCore(
        ShiftRuntimeState state,
        LogId logId,
        ItemId attemptedItem,
        AnomalyCatalog catalog,
        ActiveProcedureHold? activeProcedureHold)
    {
        if (logId.IsDefault)
        {
            throw new ArgumentException("Log identifier must be initialized.", nameof(logId));
        }

        if (attemptedItem.IsDefault)
        {
            throw new ArgumentException("Attempted item identifier must be initialized.", nameof(attemptedItem));
        }

        ArgumentNullException.ThrowIfNull(catalog);
        if (!state.TryGetLog(logId, out var log))
        {
            return Reject(state, ProcedureCompletionRejectionReason.TargetNotFound);
        }

        if (log.State != LogState.AT_PROCEDURE)
        {
            return Reject(state, ProcedureCompletionRejectionReason.TargetNotAtProcedure);
        }

        var plans = new ProcedurePlanResolver(catalog);
        if (!plans.TryGetPlan(log, out var plan))
        {
            return Reject(state, ProcedureCompletionRejectionReason.NoProcedurePlan);
        }

        if (plan!.Steps.IsEmpty)
        {
            throw new ArgumentException("Procedure plans must contain at least one step before completion.", nameof(catalog));
        }

        var hasPriorProgress = state.TryGetProcedureProgress(logId, out var priorProgress);
        if (hasPriorProgress && priorProgress.AnomalyId != plan.AnomalyId)
        {
            throw new ArgumentException("Stored procedure progress must match the log procedure plan.", nameof(state));
        }

        if (hasPriorProgress && priorProgress.CompletedStepCount > plan.Steps.Length)
        {
            throw new ArgumentException("Stored procedure progress exceeds the procedure plan.", nameof(state));
        }

        if (hasPriorProgress && priorProgress.IsComplete)
        {
            if (plan.Steps.Any(step => step.Item == attemptedItem))
            {
                return Reject(state, ProcedureCompletionRejectionReason.RepeatedCorrectStep);
            }

            return CompleteConfiguredWrongAction(state, log, attemptedItem, catalog, priorProgress, activeProcedureHold);
        }

        var nextStepIndex = hasPriorProgress ? priorProgress.CompletedStepCount : 0;
        if (plan.Steps[nextStepIndex].Item == attemptedItem)
        {
            return CompleteCorrectStep(state, log, plan, nextStepIndex, priorProgress, attemptedItem, activeProcedureHold);
        }

        var wrongAction = WrongActionResolver.Resolve(log, attemptedItem, catalog);
        if (wrongAction is ConfiguredWrongActionResolution configured)
        {
            return CompleteConfiguredWrongAction(state, log, configured, priorProgress, activeProcedureHold);
        }

        if (!state.Inventory.IsAvailable(attemptedItem))
        {
            return Reject(state, ProcedureCompletionRejectionReason.MissingItem);
        }

        return plan.Steps.Any(step => step.Item == attemptedItem)
            ? Reject(state, ProcedureCompletionRejectionReason.OutOfOrderItem)
            : Reject(state, ProcedureCompletionRejectionReason.UnconfiguredWrongAction);
    }

    private static ItemActionCompletionResult CompleteCorrectStep(
        ShiftRuntimeState state,
        LogRuntimeState log,
        ProcedurePlan plan,
        int nextStepIndex,
        ProcedureProgress? priorProgress,
        ItemId attemptedItem,
        ActiveProcedureHold? activeProcedureHold)
    {
        var step = plan.Steps[nextStepIndex];
        if (!state.Inventory.IsAvailable(attemptedItem))
        {
            return Reject(state, ProcedureCompletionRejectionReason.MissingItem);
        }

        var inventory = state.Inventory;
        var consumed = false;
        if (step.Consumes)
        {
            if (!inventory.TryConsume(attemptedItem, out inventory))
            {
                return Reject(state, ProcedureCompletionRejectionReason.MissingItem);
            }

            consumed = true;
        }

        var completedStepCount = checked(nextStepIndex + 1);
        var progress = new ProcedureProgress(log.LogId, plan.AnomalyId, completedStepCount, completedStepCount == plan.Steps.Length);
        var newlyGrantedFlags = progress.IsComplete
            ? plan.GrantedFlags.Except(log.Flags).ToImmutableHashSet()
            : ImmutableHashSet<FlagId>.Empty;
        var updatedState = state.ApplyItemAction(log.LogId, inventory, progress, newlyGrantedFlags, activeProcedureHold);
        return new ItemActionCompletionAccepted(
            updatedState,
            new ItemActionCompletionDescriptor(
                log.LogId,
                attemptedItem,
                ItemActionCompletionKind.CorrectProcedureStep,
                consumed,
                priorProgress,
                progress,
                newlyGrantedFlags,
                ImmutableArray<EffectDefinition>.Empty,
                state.StateVersion,
                updatedState.StateVersion));
    }

    private static ItemActionCompletionResult CompleteConfiguredWrongAction(
        ShiftRuntimeState state,
        LogRuntimeState log,
        ItemId attemptedItem,
        AnomalyCatalog catalog,
        ProcedureProgress? priorProgress,
        ActiveProcedureHold? activeProcedureHold)
    {
        var resolution = WrongActionResolver.Resolve(log, attemptedItem, catalog);
        return resolution is ConfiguredWrongActionResolution configured
            ? CompleteConfiguredWrongAction(state, log, configured, priorProgress, activeProcedureHold)
            : Reject(state, state.Inventory.IsAvailable(attemptedItem)
                ? ProcedureCompletionRejectionReason.UnconfiguredWrongAction
                : ProcedureCompletionRejectionReason.MissingItem);
    }

    private static ItemActionCompletionResult CompleteConfiguredWrongAction(
        ShiftRuntimeState state,
        LogRuntimeState log,
        ConfiguredWrongActionResolution configured,
        ProcedureProgress? priorProgress,
        ActiveProcedureHold? activeProcedureHold)
    {
        if (!configured.LeavesStateUnchanged)
        {
            throw new ArgumentException("Current procedure completion only supports unchanged-state wrong actions.", nameof(configured));
        }

        if (!state.Inventory.IsAvailable(configured.AttemptedItem))
        {
            return Reject(state, ProcedureCompletionRejectionReason.MissingItem);
        }

        var inventory = state.Inventory;
        if (configured.Consumes && !inventory.TryConsume(configured.AttemptedItem, out inventory))
        {
            return Reject(state, ProcedureCompletionRejectionReason.MissingItem);
        }

        var updatedState = state.ApplyItemAction(log.LogId, inventory, null, ImmutableHashSet<FlagId>.Empty, activeProcedureHold);
        return new ItemActionCompletionAccepted(
            updatedState,
            new ItemActionCompletionDescriptor(
                log.LogId,
                configured.AttemptedItem,
                ItemActionCompletionKind.ConfiguredWrongAction,
                configured.Consumes,
                priorProgress,
                priorProgress,
                ImmutableHashSet<FlagId>.Empty,
                configured.Effects,
                state.StateVersion,
                updatedState.StateVersion));
    }

    private static ItemActionCompletionRejected Reject(ShiftRuntimeState state, ProcedureCompletionRejectionReason reason) => new(state, reason);
}
