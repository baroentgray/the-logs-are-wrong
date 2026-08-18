using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>One immutable trace entry for the procedure due-completion family in frozen host stage 1.</summary>
public sealed class ProcedureDueCompletionStageStep
{
    internal ProcedureDueCompletionStageStep(ShiftRuntimeState beforeState, ProcedureActionDueCompletionResult result)
    {
        BeforeState = beforeState;
        Result = result;
    }

    /// <summary>The exact state observed before the procedure family ran.</summary>
    public ShiftRuntimeState BeforeState { get; }

    /// <summary>The exact result returned by the existing procedure due-completion service.</summary>
    public ProcedureActionDueCompletionResult Result { get; }

    /// <summary>The exact resulting state, always the retained result's own returned state by reference.</summary>
    public ShiftRuntimeState AfterState => Result.State;
}

/// <summary>One immutable trace entry for the confirmation due-completion family in frozen host stage 1.</summary>
public sealed class ConfirmationDueCompletionStageStep
{
    internal ConfirmationDueCompletionStageStep(ShiftRuntimeState beforeState, ConfirmationTestDueCompletionResult result)
    {
        BeforeState = beforeState;
        Result = result;
    }

    /// <summary>The exact state observed before the confirmation family ran.</summary>
    public ShiftRuntimeState BeforeState { get; }

    /// <summary>The exact result returned by the existing confirmation due-completion service.</summary>
    public ConfirmationTestDueCompletionResult Result { get; }

    /// <summary>The exact resulting state, always the retained result's own returned state by reference.</summary>
    public ShiftRuntimeState AfterState => Result.State;
}

/// <summary>One immutable trace entry for the containment-ritual due-completion family in frozen host stage 1.</summary>
public sealed class ContainmentRitualDueCompletionStageStep
{
    internal ContainmentRitualDueCompletionStageStep(ShiftRuntimeState beforeState, ContainmentRitualCompletionResult result)
    {
        BeforeState = beforeState;
        Result = result;
    }

    /// <summary>The exact state observed before the containment-ritual family ran.</summary>
    public ShiftRuntimeState BeforeState { get; }

    /// <summary>The exact result returned by the existing containment-ritual completion service.</summary>
    public ContainmentRitualCompletionResult Result { get; }

    /// <summary>The exact resulting state, always the retained result's own returned state by reference.</summary>
    public ShiftRuntimeState AfterState => Result.State;
}

/// <summary>One immutable trace entry for the line-repair due-completion family in frozen host stage 1.</summary>
public sealed class LineRepairDueCompletionStageStep
{
    internal LineRepairDueCompletionStageStep(ShiftRuntimeState beforeState, LineRepairDueCompletionResult result)
    {
        BeforeState = beforeState;
        Result = result;
    }

    /// <summary>The exact state observed before the line-repair family ran.</summary>
    public ShiftRuntimeState BeforeState { get; }

    /// <summary>The exact result returned by the existing line-repair due-completion service, retaining any pending transition as data only.</summary>
    public LineRepairDueCompletionResult Result { get; }

    /// <summary>The exact resulting state, always the retained result's own returned state by reference.</summary>
    public ShiftRuntimeState AfterState => Result.State;
}

/// <summary>
/// The immutable result of executing frozen host stage 1 (<c>hold_and_procedure_completions</c>) over one exact tick.
/// It retains the exact initial state, the four fixed-order completion steps, and the exact final state derived from
/// the line-repair step.
/// </summary>
public sealed class HostStageOneCompletionExecution
{
    internal HostStageOneCompletionExecution(
        ShiftRuntimeState initialState,
        ProcedureDueCompletionStageStep procedure,
        ConfirmationDueCompletionStageStep confirmation,
        ContainmentRitualDueCompletionStageStep containmentRitual,
        LineRepairDueCompletionStageStep lineRepair)
    {
        InitialState = initialState;
        Procedure = procedure;
        Confirmation = confirmation;
        ContainmentRitual = containmentRitual;
        LineRepair = lineRepair;
    }

    /// <summary>The exact state before any completion family ran.</summary>
    public ShiftRuntimeState InitialState { get; }

    /// <summary>The first fixed-order step: procedure due completion.</summary>
    public ProcedureDueCompletionStageStep Procedure { get; }

    /// <summary>The second fixed-order step: confirmation-test due completion.</summary>
    public ConfirmationDueCompletionStageStep Confirmation { get; }

    /// <summary>The third fixed-order step: containment-ritual due completion.</summary>
    public ContainmentRitualDueCompletionStageStep ContainmentRitual { get; }

    /// <summary>The fourth fixed-order step: line-repair due completion.</summary>
    public LineRepairDueCompletionStageStep LineRepair { get; }

    /// <summary>The exact final stage-1 state, always the line-repair step's after-state by reference.</summary>
    public ShiftRuntimeState FinalState => LineRepair.AfterState;
}

/// <summary>
/// Pure frozen host stage 1 executor. It invokes the four existing due-completion services exactly once each, in the
/// bounded Gate 1 canonical order procedure → confirmation → containment ritual → line repair, feeding each returned
/// state into the next family. It sorts nothing, inspects no due tick to reorder, assigns no version, executes no other
/// host stage, executes no pending line transition, and emits no event. Programmer/invariant exceptions from a service
/// propagate; because every state is immutable and local, no partial result escapes.
/// </summary>
public sealed class HostStageOneCompletionExecutor
{
    private readonly ProcedureActionDueCompletionService _procedureService = new();
    private readonly ConfirmationTestDueCompletionService _confirmationService = new();
    private readonly ContainmentRitualCompletionService _containmentRitualService = new();
    private readonly LineRepairDueCompletionService _lineRepairService = new();

    /// <summary>
    /// Executes stage 1 for the exact <paramref name="currentTick"/> starting from <paramref name="initialState"/>.
    /// </summary>
    public HostStageOneCompletionExecution Execute(
        ShiftRuntimeState initialState,
        ServerTick currentTick,
        AnomalyCatalog anomalyCatalog,
        ContainmentConfiguration containmentConfiguration)
    {
        if (initialState is null) { throw new ArgumentNullException("initialState"); }
        if (currentTick.IsDefault)
        {
            throw new ArgumentException("Current tick must be initialized.", nameof(currentTick));
        }

        if (anomalyCatalog is null) { throw new ArgumentNullException("anomalyCatalog"); }
        if (containmentConfiguration is null) { throw new ArgumentNullException("containmentConfiguration"); }

        var procedureResult = _procedureService.CompleteDue(initialState, currentTick, anomalyCatalog);
        var procedureStep = new ProcedureDueCompletionStageStep(initialState, procedureResult);

        var confirmationResult = _confirmationService.CompleteDue(procedureStep.AfterState, currentTick, anomalyCatalog);
        var confirmationStep = new ConfirmationDueCompletionStageStep(procedureStep.AfterState, confirmationResult);

        var containmentResult = _containmentRitualService.CompleteDue(confirmationStep.AfterState, currentTick, containmentConfiguration, anomalyCatalog);
        var containmentStep = new ContainmentRitualDueCompletionStageStep(confirmationStep.AfterState, containmentResult);

        var lineRepairResult = _lineRepairService.CompleteDue(containmentStep.AfterState, currentTick);
        var lineRepairStep = new LineRepairDueCompletionStageStep(containmentStep.AfterState, lineRepairResult);

        return new HostStageOneCompletionExecution(initialState, procedureStep, confirmationStep, containmentStep, lineRepairStep);
    }
}
