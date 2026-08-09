using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>
/// Closed immutable classification of exactly one stage-2 receipt evaluated against the evolving shift state.
/// Every kind exposes the exact resulting <see cref="ShiftRuntimeState"/> produced by the owning existing handler,
/// or the exact unchanged state for an action no stage-2 handler family owns.
/// </summary>
public abstract class AcceptedIntentStageOutcome
{
    private protected AcceptedIntentStageOutcome(ShiftRuntimeState state) => State = state;

    /// <summary>The exact resulting state; for unsupported actions this is the exact unchanged state reference.</summary>
    public ShiftRuntimeState State { get; }
}

/// <summary>A receipt owned by <see cref="ManualLogIntentHandler"/> retaining its exact returned result.</summary>
public sealed class ManualRoutingIntentStageOutcome : AcceptedIntentStageOutcome
{
    internal ManualRoutingIntentStageOutcome(ManualLogIntentResult result)
        : base(result.State) => Result = result;

    /// <summary>The exact <see cref="ManualLogIntentResult"/> returned by the existing manual routing handler.</summary>
    public ManualLogIntentResult Result { get; }
}

/// <summary>A receipt owned by <see cref="EarlyFeedIntentHandler"/> retaining its exact returned result.</summary>
public sealed class EarlyFeedIntentStageOutcome : AcceptedIntentStageOutcome
{
    internal EarlyFeedIntentStageOutcome(EarlyFeedIntentResult result)
        : base(result.State) => Result = result;

    /// <summary>The exact <see cref="EarlyFeedIntentResult"/> returned by the existing early-feed handler.</summary>
    public EarlyFeedIntentResult Result { get; }
}

/// <summary>A receipt owned by <see cref="ProcedureActionIntentHandler"/> retaining its exact returned result.</summary>
public sealed class ProcedureActionIntentStageOutcome : AcceptedIntentStageOutcome
{
    internal ProcedureActionIntentStageOutcome(ProcedureActionIntentResult result)
        : base(result.State) => Result = result;

    /// <summary>The exact <see cref="ProcedureActionIntentResult"/> returned by the procedure-action handler.</summary>
    public ProcedureActionIntentResult Result { get; }
}

/// <summary>A receipt owned by <see cref="ConfirmationTestIntentHandler"/> retaining its exact returned result.</summary>
public sealed class ConfirmationTestIntentStageOutcome : AcceptedIntentStageOutcome
{
    internal ConfirmationTestIntentStageOutcome(ConfirmationTestIntentResult result)
        : base(result.State) => Result = result;

    /// <summary>The exact <see cref="ConfirmationTestIntentResult"/> returned by the confirmation handler.</summary>
    public ConfirmationTestIntentResult Result { get; }
}

/// <summary>An action outside the six owned stage-2 actions, retained without invoking any handler.</summary>
public sealed class UnsupportedIntentStageOutcome : AcceptedIntentStageOutcome
{
    internal UnsupportedIntentStageOutcome(ShiftRuntimeState state, IntentActionId action)
        : base(state) => Action = action;

    /// <summary>The exact unowned action classified stage-locally without extending the frozen rejection taxonomy.</summary>
    public IntentActionId Action { get; }
}

/// <summary>One immutable per-receipt trace entry retaining the exact receipt, before-state and closed outcome.</summary>
public sealed class AcceptedIntentStageStep
{
    internal AcceptedIntentStageStep(
        AuthoritativeAcceptedIntent receipt,
        ShiftRuntimeState beforeState,
        AcceptedIntentStageOutcome outcome)
    {
        Receipt = receipt;
        BeforeState = beforeState;
        Outcome = outcome;
    }

    /// <summary>The exact receipt evaluated at this step.</summary>
    public AuthoritativeAcceptedIntent Receipt { get; }

    /// <summary>The exact state observed before this receipt was evaluated.</summary>
    public ShiftRuntimeState BeforeState { get; }

    /// <summary>The exact closed outcome produced for this receipt.</summary>
    public AcceptedIntentStageOutcome Outcome { get; }

    /// <summary>The exact resulting state; always the outcome's own returned state by reference.</summary>
    public ShiftRuntimeState AfterState => Outcome.State;
}

/// <summary>
/// The immutable result of executing frozen host stage 2 over one already ordered accepted-intent batch.
/// It retains the exact input batch, the exact initial and final states, and the exact ordered per-receipt trace.
/// </summary>
public sealed class AcceptedIntentStageExecution
{
    internal AcceptedIntentStageExecution(
        AcceptedIntentTickBatch batch,
        ShiftRuntimeState initialState,
        ShiftRuntimeState finalState,
        ImmutableArray<AcceptedIntentStageStep> steps)
    {
        Batch = batch;
        InitialState = initialState;
        FinalState = finalState;
        Steps = steps;
    }

    /// <summary>The exact input batch this stage executed.</summary>
    public AcceptedIntentTickBatch Batch { get; }

    /// <summary>The exact state before any receipt was evaluated.</summary>
    public ShiftRuntimeState InitialState { get; }

    /// <summary>The exact state after the final receipt, or the exact initial state for an empty batch.</summary>
    public ShiftRuntimeState FinalState { get; }

    /// <summary>The exact ordered trace, one step per batch receipt, in the batch's existing order.</summary>
    public ImmutableArray<AcceptedIntentStageStep> Steps { get; }
}

/// <summary>
/// Pure frozen host stage 2 (<c>accepted_intents_by_server_receive_sequence</c>) executor. It consumes the already
/// validated and ordered TLAW-029 batch, dispatches each receipt to the one existing handler family that owns its
/// exact action, and feeds each returned state into the next receipt. It performs no other host stage, sorts nothing,
/// inspects no receive sequence, assigns no version, and emits no event.
/// </summary>
public sealed class AcceptedIntentStageExecutor
{
    private readonly ManualLogIntentHandler _manualRoutingHandler = new();
    private readonly EarlyFeedIntentHandler _earlyFeedHandler = new();
    private readonly ProcedureActionIntentHandler _procedureActionHandler = new();
    private readonly ConfirmationTestIntentHandler _confirmationTestHandler = new();

    /// <summary>
    /// Executes stage 2 over <paramref name="batch"/> starting from <paramref name="initialState"/>. The batch must
    /// belong to the executing shift; a cross-shift batch is a loud host-invariant failure raised before any handler runs.
    /// </summary>
    public AcceptedIntentStageExecution Execute(
        ShiftRuntimeState initialState,
        AcceptedIntentTickBatch batch,
        SchedulerConfiguration schedulerConfiguration,
        ImmutableHashSet<ItemId> activeTools,
        LineNoiseRuntimeState initialLineNoise,
        AnomalyCatalog anomalyCatalog)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(schedulerConfiguration);
        ArgumentNullException.ThrowIfNull(activeTools);
        ArgumentNullException.ThrowIfNull(initialLineNoise);
        ArgumentNullException.ThrowIfNull(anomalyCatalog);
        if (batch.ShiftId != initialState.ShiftId)
        {
            throw new ArgumentException(
                "Accepted-intent batch evidence must belong to the executing shift.",
                nameof(batch));
        }

        var steps = ImmutableArray.CreateBuilder<AcceptedIntentStageStep>(batch.Intents.Length);
        var currentState = initialState;
        foreach (var receipt in batch.Intents)
        {
            var beforeState = currentState;
            var outcome = EvaluateReceipt(beforeState, receipt, batch.CurrentTick, schedulerConfiguration, activeTools, initialLineNoise, anomalyCatalog);
            steps.Add(new AcceptedIntentStageStep(receipt, beforeState, outcome));
            currentState = outcome.State;
        }

        return new AcceptedIntentStageExecution(batch, initialState, currentState, steps.MoveToImmutable());
    }

    private AcceptedIntentStageOutcome EvaluateReceipt(
        ShiftRuntimeState state,
        AuthoritativeAcceptedIntent receipt,
        ServerTick currentTick,
        SchedulerConfiguration schedulerConfiguration,
        ImmutableHashSet<ItemId> activeTools,
        LineNoiseRuntimeState initialLineNoise,
        AnomalyCatalog anomalyCatalog)
    {
        var envelope = receipt.Envelope;
        var action = envelope.Action;
        if (IsManualRoutingAction(action))
        {
            return new ManualRoutingIntentStageOutcome(
                _manualRoutingHandler.Handle(state, envelope, receipt.AuthoritativeActor));
        }

        if (action == FeedPlanningIntentActions.RequestEarlyFeed)
        {
            return new EarlyFeedIntentStageOutcome(
                _earlyFeedHandler.Handle(state, envelope, receipt.AuthoritativeActor, currentTick, schedulerConfiguration));
        }

        if (action == ProcedureIntentActions.StartProcedureAction)
        {
            return new ProcedureActionIntentStageOutcome(
                _procedureActionHandler.Handle(state, envelope, receipt.AuthoritativeActor, currentTick, anomalyCatalog));
        }

        if (action == ConfirmationIntentActions.StartConfirmationTest)
        {
            return new ConfirmationTestIntentStageOutcome(
                _confirmationTestHandler.Handle(state, envelope, receipt.AuthoritativeActor, currentTick, activeTools, initialLineNoise, anomalyCatalog));
        }

        return new UnsupportedIntentStageOutcome(state, action);
    }

    private static bool IsManualRoutingAction(IntentActionId action) =>
        action == LogIntentActions.RouteToProcedure ||
        action == LogIntentActions.ReturnFromProcedure ||
        action == LogIntentActions.RouteToSawQueue ||
        action == LogIntentActions.WriteOff;
}
