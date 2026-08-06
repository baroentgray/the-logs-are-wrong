using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>One immutable trace entry for the intake-deadline expiration family in frozen host stage 3.</summary>
public sealed class IntakeDeadlineExpirationStageStep
{
    internal IntakeDeadlineExpirationStageStep(ShiftRuntimeState beforeState, IntakeDeadlineExpirationResult result)
    {
        BeforeState = beforeState;
        Result = result;
    }

    /// <summary>The exact state observed before intake-deadline expiration ran.</summary>
    public ShiftRuntimeState BeforeState { get; }

    /// <summary>The exact result returned by the existing intake-deadline expiration service, retaining any default-route follow-up as data only.</summary>
    public IntakeDeadlineExpirationResult Result { get; }

    /// <summary>The exact resulting state, always the retained result's own returned state by reference.</summary>
    public ShiftRuntimeState AfterState => Result.State;
}

/// <summary>One immutable trace entry for the containment-deadline advancement family in frozen host stage 3.</summary>
public sealed class ContainmentDeadlineAdvanceStageStep
{
    internal ContainmentDeadlineAdvanceStageStep(ShiftRuntimeState beforeState, ContainmentAdvanceResult result)
    {
        BeforeState = beforeState;
        Result = result;
    }

    /// <summary>The exact state observed before containment advancement ran.</summary>
    public ShiftRuntimeState BeforeState { get; }

    /// <summary>The exact result returned by the existing containment advancement service, retaining any incident descriptor as data only.</summary>
    public ContainmentAdvanceResult Result { get; }

    /// <summary>The exact resulting state, always the retained result's own returned state by reference.</summary>
    public ShiftRuntimeState AfterState => Result.State;
}

/// <summary>
/// The immutable result of executing frozen host stage 3 (<c>deadline_expirations</c>) over one exact tick.
/// It retains the exact initial state, the two fixed-order deadline steps, and the exact final state derived from
/// the containment step.
/// </summary>
public sealed class HostStageThreeDeadlineExecution
{
    internal HostStageThreeDeadlineExecution(
        ShiftRuntimeState initialState,
        IntakeDeadlineExpirationStageStep intakeDeadline,
        ContainmentDeadlineAdvanceStageStep containment)
    {
        InitialState = initialState;
        IntakeDeadline = intakeDeadline;
        Containment = containment;
    }

    /// <summary>The exact state before any deadline family ran.</summary>
    public ShiftRuntimeState InitialState { get; }

    /// <summary>The first fixed-order step: intake-deadline expiration.</summary>
    public IntakeDeadlineExpirationStageStep IntakeDeadline { get; }

    /// <summary>The second fixed-order step: containment-deadline advancement.</summary>
    public ContainmentDeadlineAdvanceStageStep Containment { get; }

    /// <summary>The exact final stage-3 state, always the containment step's after-state by reference.</summary>
    public ShiftRuntimeState FinalState => Containment.AfterState;
}

/// <summary>
/// Pure frozen host stage 3 executor. It invokes the two existing deadline services exactly once each, in the bounded
/// Gate 1 canonical order intake-deadline expiration → containment advancement, feeding the exact intake result state
/// into containment. It sorts nothing, inspects no due tick to reorder, assigns no version, executes no default route
/// or incident effect, starts no ritual, executes no other host stage, and emits no event. Programmer/invariant
/// exceptions from a service propagate; because every state is immutable and local, no partial result escapes.
/// </summary>
public sealed class HostStageThreeDeadlineExecutor
{
    private readonly IntakeDeadlineExpirationService _intakeDeadlineExpirationService = new();
    private readonly ContainmentAdvanceService _containmentAdvanceService = new();

    /// <summary>
    /// Executes stage 3 for the exact <paramref name="currentTick"/> starting from <paramref name="initialState"/>.
    /// </summary>
    public HostStageThreeDeadlineExecution Execute(
        ShiftRuntimeState initialState,
        ServerTick currentTick,
        ContainmentConfiguration containmentConfiguration,
        AnomalyCatalog anomalyCatalog)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        if (currentTick.IsDefault)
        {
            throw new ArgumentException("Current tick must be initialized.", nameof(currentTick));
        }

        ArgumentNullException.ThrowIfNull(containmentConfiguration);
        ArgumentNullException.ThrowIfNull(anomalyCatalog);
        ArgumentNullException.ThrowIfNull(anomalyCatalog.Definitions);

        var intakeResult = _intakeDeadlineExpirationService.Expire(initialState, currentTick);
        var intakeStep = new IntakeDeadlineExpirationStageStep(initialState, intakeResult);

        var containmentResult = _containmentAdvanceService.Advance(intakeStep.AfterState, currentTick, containmentConfiguration, anomalyCatalog);
        var containmentStep = new ContainmentDeadlineAdvanceStageStep(intakeStep.AfterState, containmentResult);

        return new HostStageThreeDeadlineExecution(initialState, intakeStep, containmentStep);
    }
}
