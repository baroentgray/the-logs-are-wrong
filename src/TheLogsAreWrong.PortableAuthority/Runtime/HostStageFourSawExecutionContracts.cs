using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>One immutable trace entry for saw-cycle completion in frozen host stage 4.</summary>
public sealed class SawCycleCompletionStageStep
{
    internal SawCycleCompletionStageStep(ShiftRuntimeState beforeShiftState, SawCycleCompletionResult result)
    {
        BeforeShiftState = beforeShiftState;
        Result = result;
    }

    /// <summary>The exact shift state observed before saw-cycle completion ran.</summary>
    public ShiftRuntimeState BeforeShiftState { get; }

    /// <summary>The exact result returned by the existing saw-cycle completion service.</summary>
    public SawCycleCompletionResult Result { get; }

    /// <summary>The exact resulting shift state, always the retained result's own returned state by reference.</summary>
    public ShiftRuntimeState AfterShiftState => Result.State;
}

/// <summary>
/// One immutable trace entry for the conditional quota application in frozen host stage 4. Quota application exists
/// if and only if the completion result was <see cref="SawCycleCompleted"/>.
/// </summary>
public sealed class SawQuotaApplicationStageStep
{
    internal SawQuotaApplicationStageStep(
        SawCycleCompletionResult completionResult,
        QuotaRuntimeState beforeQuotaState,
        SawQuotaApplicationResult? result)
    {
        if (completionResult is null) { throw new ArgumentNullException("completionResult"); }
        if (beforeQuotaState is null) { throw new ArgumentNullException("beforeQuotaState"); }

        if (completionResult is SawCycleCompleted completed)
        {
            if (result is null)
            {
                throw new InvalidOperationException("A completed saw cycle requires an exact quota application result.");
            }

            if (!ReferenceEquals(result.ShiftState, completed.State))
            {
                throw new InvalidOperationException("The quota application must retain the exact completion shift-state reference.");
            }

            if (result.CompletedLogId != completed.Cycle.LogId || !ReferenceEquals(result.Resolution, completed.Resolution))
            {
                throw new InvalidOperationException("The quota application must retain the exact completed identity and processing resolution.");
            }
        }
        else if (result is not null)
        {
            throw new InvalidOperationException("A non-completed saw cycle cannot carry a quota application result.");
        }

        CompletionResult = completionResult;
        BeforeQuotaState = beforeQuotaState;
        Result = result;
        WasRequired = completionResult is SawCycleCompleted;
        AfterQuotaState = result is not null ? result.QuotaState : beforeQuotaState;
    }

    /// <summary>The exact completion result that decided whether quota work exists.</summary>
    public SawCycleCompletionResult CompletionResult { get; }

    /// <summary>The exact quota state observed before this step.</summary>
    public QuotaRuntimeState BeforeQuotaState { get; }

    /// <summary>The exact quota application result, present if and only if the completion was a completed saw cycle.</summary>
    public SawQuotaApplicationResult? Result { get; }

    /// <summary>Whether quota application was required, derived from the completion type.</summary>
    public bool WasRequired { get; }

    /// <summary>The exact resulting quota state: the quota result's state when present, otherwise the exact before-state.</summary>
    public QuotaRuntimeState AfterQuotaState { get; }
}

/// <summary>One immutable trace entry for automatic saw-cycle start in frozen host stage 4.</summary>
public sealed class SawCycleStartStageStep
{
    internal SawCycleStartStageStep(ShiftRuntimeState beforeShiftState, SawCycleStartResult result)
    {
        BeforeShiftState = beforeShiftState;
        Result = result;
    }

    /// <summary>The exact shift state observed before automatic saw start ran.</summary>
    public ShiftRuntimeState BeforeShiftState { get; }

    /// <summary>The exact result returned by the existing saw-cycle start service.</summary>
    public SawCycleStartResult Result { get; }

    /// <summary>The exact resulting shift state, always the retained result's own returned state by reference.</summary>
    public ShiftRuntimeState AfterShiftState => Result.State;
}

/// <summary>
/// The immutable result of executing frozen host stage 4 (<c>saw_transitions</c>) over one exact tick. It retains the
/// exact initial shift and quota states, the three fixed-order steps, and the exact final shift and quota states.
/// </summary>
public sealed class HostStageFourSawExecution
{
    internal HostStageFourSawExecution(
        ShiftRuntimeState initialShiftState,
        QuotaRuntimeState initialQuotaState,
        SawCycleCompletionStageStep completion,
        SawQuotaApplicationStageStep quota,
        SawCycleStartStageStep start)
    {
        InitialShiftState = initialShiftState;
        InitialQuotaState = initialQuotaState;
        Completion = completion;
        Quota = quota;
        Start = start;
    }

    /// <summary>The exact shift state before any stage-4 work ran.</summary>
    public ShiftRuntimeState InitialShiftState { get; }

    /// <summary>The exact quota state before any stage-4 work ran.</summary>
    public QuotaRuntimeState InitialQuotaState { get; }

    /// <summary>The first fixed-order step: saw-cycle completion.</summary>
    public SawCycleCompletionStageStep Completion { get; }

    /// <summary>The second fixed-order step: conditional quota application.</summary>
    public SawQuotaApplicationStageStep Quota { get; }

    /// <summary>The third fixed-order step: automatic saw-cycle start.</summary>
    public SawCycleStartStageStep Start { get; }

    /// <summary>The exact final shift state, always the start step's after-state by reference.</summary>
    public ShiftRuntimeState FinalShiftState => Start.AfterShiftState;

    /// <summary>The exact final quota state, always the quota step's after-state by reference.</summary>
    public QuotaRuntimeState FinalQuotaState => Quota.AfterQuotaState;
}

/// <summary>
/// Pure frozen host stage 4 executor. It invokes saw-cycle completion once, applies quota once only when the completion
/// is a completed saw cycle, then invokes automatic saw start once from the exact completion after-state. It sorts
/// nothing, assigns no version, re-resolves no anomaly, constructs no settlement, executes no effect, runs no other host
/// stage, and emits no event. Programmer/invariant exceptions from a service propagate; because every state is immutable
/// and local, no partial result escapes.
/// </summary>
public sealed class HostStageFourSawExecutor
{
    private readonly SawCycleCompletionService _sawCycleCompletionService = new();
    private readonly SawQuotaApplicationService _sawQuotaApplicationService = new();
    private readonly SawCycleStartService _sawCycleStartService = new();

    /// <summary>
    /// Executes stage 4 for the exact <paramref name="currentTick"/> from <paramref name="initialShiftState"/> and the
    /// separate <paramref name="initialQuotaState"/>.
    /// </summary>
    public HostStageFourSawExecution Execute(
        ShiftRuntimeState initialShiftState,
        QuotaRuntimeState initialQuotaState,
        ServerTick currentTick,
        SchedulerConfiguration schedulerConfiguration,
        AnomalyCatalog anomalyCatalog)
    {
        if (initialShiftState is null) { throw new ArgumentNullException("initialShiftState"); }
        if (initialQuotaState is null) { throw new ArgumentNullException("initialQuotaState"); }
        if (currentTick.IsDefault)
        {
            throw new ArgumentException("Current tick must be initialized.", nameof(currentTick));
        }

        if (schedulerConfiguration is null) { throw new ArgumentNullException("schedulerConfiguration"); }
        if (anomalyCatalog is null) { throw new ArgumentNullException("anomalyCatalog"); }
        if (anomalyCatalog.Definitions is null) { throw new ArgumentNullException("anomalyCatalog.Definitions"); }

        var completionResult = _sawCycleCompletionService.Complete(initialShiftState, currentTick, anomalyCatalog);
        var completionStep = new SawCycleCompletionStageStep(initialShiftState, completionResult);

        SawQuotaApplicationResult? quotaResult = null;
        if (completionResult is SawCycleCompleted completed)
        {
            quotaResult = _sawQuotaApplicationService.Apply(completed, initialQuotaState);
        }

        var quotaStep = new SawQuotaApplicationStageStep(completionResult, initialQuotaState, quotaResult);

        var startResult = _sawCycleStartService.Start(completionStep.AfterShiftState, currentTick, schedulerConfiguration);
        var startStep = new SawCycleStartStageStep(completionStep.AfterShiftState, startResult);

        return new HostStageFourSawExecution(initialShiftState, initialQuotaState, completionStep, quotaStep, startStep);
    }
}
