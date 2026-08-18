using System.Linq;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>Step 1: the always-executed initial-feed planning family.</summary>
public sealed class InitialFeedPlanningStageStep
{
    internal InitialFeedPlanningStageStep(ShiftRuntimeState beforeState, InitialFeedPlanningResult result)
    {
        if (beforeState is null) { throw new ArgumentNullException("beforeState"); }
        if (result is null) { throw new ArgumentNullException("result"); }
        BeforeState = beforeState;
        Result = result;
    }

    public ShiftRuntimeState BeforeState { get; }
    public InitialFeedPlanningResult Result { get; }
    public ShiftRuntimeState AfterState => Result.State;
}

/// <summary>Step 2: the conditional repair pending-transition execution family.</summary>
public sealed class RepairPendingTransitionStageStep
{
    internal RepairPendingTransitionStageStep(ShiftRuntimeState beforeState, RepairPendingTransitionExecutionResult? result)
    {
        if (beforeState is null) { throw new ArgumentNullException("beforeState"); }
        BeforeState = beforeState;
        Result = result;
    }

    public ShiftRuntimeState BeforeState { get; }
    public RepairPendingTransitionExecutionResult? Result { get; }
    public ShiftRuntimeState AfterState => Result?.State ?? BeforeState;
}

/// <summary>Step 3: the conditional source-bound repaired follow-up family (at most one service).</summary>
public sealed class RepairFollowUpStageStep
{
    internal RepairFollowUpStageStep(
        ShiftRuntimeState beforeState,
        IntakeDeadlineStartResult? deadlineStart,
        NormalFeedPlanningResult? normalPlanning)
    {
        if (beforeState is null) { throw new ArgumentNullException("beforeState"); }
        if (deadlineStart is not null && normalPlanning is not null)
        {
            throw new ArgumentException("A repaired follow-up cannot use both service families at once.");
        }

        BeforeState = beforeState;
        DeadlineStart = deadlineStart;
        NormalPlanning = normalPlanning;
    }

    public ShiftRuntimeState BeforeState { get; }
    public IntakeDeadlineStartResult? DeadlineStart { get; }
    public NormalFeedPlanningResult? NormalPlanning { get; }
    public ShiftRuntimeState AfterState => DeadlineStart?.State ?? NormalPlanning?.State ?? BeforeState;
}

/// <summary>Step 4: the conditional default intake auto-route family.</summary>
public sealed class DefaultIntakeAutoRouteStageStep
{
    internal DefaultIntakeAutoRouteStageStep(ShiftRuntimeState beforeState, DefaultIntakeAutoRouteResult? result)
    {
        if (beforeState is null) { throw new ArgumentNullException("beforeState"); }
        BeforeState = beforeState;
        Result = result;
    }

    public ShiftRuntimeState BeforeState { get; }
    public DefaultIntakeAutoRouteResult? Result { get; }
    public ShiftRuntimeState AfterState => Result?.State ?? BeforeState;
}

/// <summary>Step 5: the evidence-triggered generic normal-feed planning family.</summary>
public sealed class GenericNormalFeedPlanningStageStep
{
    internal GenericNormalFeedPlanningStageStep(ShiftRuntimeState beforeState, bool required, NormalFeedPlanningResult? result)
    {
        if (beforeState is null) { throw new ArgumentNullException("beforeState"); }
        if (required != (result is not null))
        {
            throw new ArgumentException("A generic normal-planning result must be present exactly when the derived trigger required it.");
        }

        BeforeState = beforeState;
        Required = required;
        Result = result;
    }

    public ShiftRuntimeState BeforeState { get; }
    public bool Required { get; }
    public NormalFeedPlanningResult? Result { get; }
    public ShiftRuntimeState AfterState => Result?.State ?? BeforeState;
}

/// <summary>Step 6: the always-executed feed-due resolution family.</summary>
public sealed class FeedDueResolutionStageStep
{
    internal FeedDueResolutionStageStep(ShiftRuntimeState beforeState, FeedDueResolutionResult result)
    {
        if (beforeState is null) { throw new ArgumentNullException("beforeState"); }
        if (result is null) { throw new ArgumentNullException("result"); }
        BeforeState = beforeState;
        Result = result;
    }

    public ShiftRuntimeState BeforeState { get; }
    public FeedDueResolutionResult Result { get; }
    public ShiftRuntimeState AfterState => Result.State;
}

/// <summary>Step 7: the conditional ordinary intake-deadline start family.</summary>
public sealed class OrdinaryIntakeDeadlineStartStageStep
{
    internal OrdinaryIntakeDeadlineStartStageStep(ShiftRuntimeState beforeState, IntakeDeadlineStartResult? result)
    {
        if (beforeState is null) { throw new ArgumentNullException("beforeState"); }
        BeforeState = beforeState;
        Result = result;
    }

    public ShiftRuntimeState BeforeState { get; }
    public IntakeDeadlineStartResult? Result { get; }
    public ShiftRuntimeState AfterState => Result?.State ?? BeforeState;
}

/// <summary>
/// The immutable, self-defending result of executing frozen host stage 5 (<c>feed_and_auto_routes</c>) over one exact
/// tick. It retains the exact stage-1 and stage-3 source evidence and the seven ordered stage steps, and its constructor
/// rejects any trace whose exact before/result/after reference chain, conditional source/kind guards, or final-state
/// derivation is not internally consistent.
/// </summary>
public sealed class HostStageFiveFeedExecution
{
    internal HostStageFiveFeedExecution(
        ShiftRuntimeState initialState,
        LineRepairDueCompletionResult lineRepairSource,
        IntakeDeadlineExpirationResult intakeExpirationSource,
        InitialFeedPlanningStageStep initialFeedPlanning,
        RepairPendingTransitionStageStep repair,
        RepairFollowUpStageStep repairFollowUp,
        DefaultIntakeAutoRouteStageStep defaultRoute,
        GenericNormalFeedPlanningStageStep genericNormalPlanning,
        FeedDueResolutionStageStep feedDue,
        OrdinaryIntakeDeadlineStartStageStep ordinaryDeadlineStart)
    {
        if (initialState is null) { throw new ArgumentNullException("initialState"); }
        if (lineRepairSource is null) { throw new ArgumentNullException("lineRepairSource"); }
        if (intakeExpirationSource is null) { throw new ArgumentNullException("intakeExpirationSource"); }
        if (initialFeedPlanning is null) { throw new ArgumentNullException("initialFeedPlanning"); }
        if (repair is null) { throw new ArgumentNullException("repair"); }
        if (repairFollowUp is null) { throw new ArgumentNullException("repairFollowUp"); }
        if (defaultRoute is null) { throw new ArgumentNullException("defaultRoute"); }
        if (genericNormalPlanning is null) { throw new ArgumentNullException("genericNormalPlanning"); }
        if (feedDue is null) { throw new ArgumentNullException("feedDue"); }
        if (ordinaryDeadlineStart is null) { throw new ArgumentNullException("ordinaryDeadlineStart"); }

        // Exact ordered before/after reference chain — the closed trace defends itself.
        if (!ReferenceEquals(initialFeedPlanning.BeforeState, initialState) ||
            !ReferenceEquals(repair.BeforeState, initialFeedPlanning.AfterState) ||
            !ReferenceEquals(repairFollowUp.BeforeState, repair.AfterState) ||
            !ReferenceEquals(defaultRoute.BeforeState, repairFollowUp.AfterState) ||
            !ReferenceEquals(genericNormalPlanning.BeforeState, defaultRoute.AfterState) ||
            !ReferenceEquals(feedDue.BeforeState, genericNormalPlanning.AfterState) ||
            !ReferenceEquals(ordinaryDeadlineStart.BeforeState, feedDue.AfterState))
        {
            throw new ArgumentException("Stage-5 steps must form the exact ordered before/after reference chain.");
        }

        // Repair-execution presence must match the stage-1 line-repair source.
        if ((lineRepairSource is LineRepairCompleted) != (repair.Result is not null))
        {
            throw new ArgumentException("A repair-execution result must be present exactly when stage-1 evidence is LineRepairCompleted.");
        }

        // Repaired follow-up presence/kind must match the executed repair transition.
        if (repair.Result is RepairPendingTransitionExecuted executed)
        {
            var deadlineExpected = executed.FollowUpRequirement == RepairPendingTransitionFollowUp.IntakeDeadlineStartRequired;
            if (deadlineExpected != (repairFollowUp.DeadlineStart is not null) || deadlineExpected == (repairFollowUp.NormalPlanning is not null))
            {
                throw new ArgumentException("The repaired follow-up kind must match the executed follow-up requirement.");
            }
        }
        else if (repairFollowUp.DeadlineStart is not null || repairFollowUp.NormalPlanning is not null)
        {
            throw new ArgumentException("A repaired follow-up requires an executed repair transition.");
        }

        // Default-route presence must match the stage-3 intake-expiration source.
        if ((intakeExpirationSource is IntakeDeadlineExpired) != (defaultRoute.Result is not null))
        {
            throw new ArgumentException("A default-route result must be present exactly when stage-3 evidence is IntakeDeadlineExpired.");
        }

        // Ordinary deadline-start presence must match an admitted-to-intake resolved feed.
        var ordinaryExpected = feedDue.Result is FeedDueResolved resolved &&
            resolved.Disposition == FeedDueDisposition.AdmittedToIntake &&
            resolved.FollowUpRequirement == FeedDueFollowUpRequirement.IntakeDeadlineStartRequired;
        if (ordinaryExpected != (ordinaryDeadlineStart.Result is not null))
        {
            throw new ArgumentException("An ordinary intake-deadline start must be present exactly for an admitted-to-intake resolved feed.");
        }

        InitialState = initialState;
        LineRepairSource = lineRepairSource;
        IntakeExpirationSource = intakeExpirationSource;
        InitialFeedPlanningStep = initialFeedPlanning;
        RepairStep = repair;
        RepairFollowUpStep = repairFollowUp;
        DefaultRouteStep = defaultRoute;
        GenericNormalPlanningStep = genericNormalPlanning;
        FeedDueStep = feedDue;
        OrdinaryDeadlineStartStep = ordinaryDeadlineStart;
    }

    /// <summary>The exact stage-5 initial state (<c>stageFour.FinalShiftState</c>).</summary>
    public ShiftRuntimeState InitialState { get; }

    /// <summary>The retained stage-1 line-repair source evidence guarding the repair-execution step.</summary>
    public LineRepairDueCompletionResult LineRepairSource { get; }

    /// <summary>The retained stage-3 intake-expiration source evidence guarding the default-route step.</summary>
    public IntakeDeadlineExpirationResult IntakeExpirationSource { get; }

    // ----- Ordered closed trace -----

    public InitialFeedPlanningStageStep InitialFeedPlanningStep { get; }
    public RepairPendingTransitionStageStep RepairStep { get; }
    public RepairFollowUpStageStep RepairFollowUpStep { get; }
    public DefaultIntakeAutoRouteStageStep DefaultRouteStep { get; }
    public GenericNormalFeedPlanningStageStep GenericNormalPlanningStep { get; }
    public FeedDueResolutionStageStep FeedDueStep { get; }
    public OrdinaryIntakeDeadlineStartStageStep OrdinaryDeadlineStartStep { get; }

    // ----- Convenience projections onto the retained step results -----

    public InitialFeedPlanningResult InitialFeedPlanning => InitialFeedPlanningStep.Result;
    public RepairPendingTransitionExecutionResult? RepairExecution => RepairStep.Result;
    public IntakeDeadlineStartResult? RepairedDeadlineStart => RepairFollowUpStep.DeadlineStart;
    public NormalFeedPlanningResult? RepairedNormalPlanning => RepairFollowUpStep.NormalPlanning;
    public DefaultIntakeAutoRouteResult? DefaultRoute => DefaultRouteStep.Result;
    public bool GenericNormalPlanningRequired => GenericNormalPlanningStep.Required;
    public NormalFeedPlanningResult? GenericNormalPlanning => GenericNormalPlanningStep.Result;
    public FeedDueResolutionResult FeedDue => FeedDueStep.Result;
    public IntakeDeadlineStartResult? OrdinaryDeadlineStart => OrdinaryDeadlineStartStep.Result;

    /// <summary>The exact final stage-5 state, always the last (ordinary deadline-start) step's after-state.</summary>
    public ShiftRuntimeState FinalState => OrdinaryDeadlineStartStep.AfterState;
}

/// <summary>
/// Pure frozen host stage 5 executor. It consumes exact immutable stage 1–4 execution evidence (without re-executing any
/// of those stages), starts from <c>stageFour.FinalShiftState</c>, and composes the existing feed/route/repair-follow-up
/// services in the bounded Gate 1 order. Conditional services run only from their exact source evidence. It sorts nothing,
/// assigns no version, derives no jam/noise, runs no other host stage, and emits no event. Programmer/invariant exceptions
/// from a delegated service propagate; because every state is immutable and local, no partial result escapes.
/// </summary>
public sealed class HostStageFiveFeedExecutor
{
    private readonly InitialFeedPlanningService _initialFeedPlanningService = new();
    private readonly RepairPendingTransitionExecutionService _repairPendingTransitionExecutionService = new();
    private readonly RepairFeedGateIntakeDeadlineStartService _repairFeedGateIntakeDeadlineStartService = new();
    private readonly RepairAutoFeedNormalFeedPlanningService _repairAutoFeedNormalFeedPlanningService = new();
    private readonly DefaultIntakeAutoRouteService _defaultIntakeAutoRouteService = new();
    private readonly NormalFeedPlanningService _normalFeedPlanningService = new();
    private readonly FeedDueResolutionService _feedDueResolutionService = new();
    private readonly IntakeDeadlineStartService _intakeDeadlineStartService = new();

    /// <summary>Executes stage 5 for the exact <paramref name="currentTick"/> from the exact post-stage-4 state.</summary>
    public HostStageFiveFeedExecution Execute(
        HostStageOneCompletionExecution stageOne,
        AcceptedIntentStageExecution stageTwo,
        HostStageThreeDeadlineExecution stageThree,
        HostStageFourSawExecution stageFour,
        ServerTick currentTick,
        SchedulerConfiguration schedulerConfiguration,
        ShiftProfile selectedProfile)
    {
        if (stageOne is null) { throw new ArgumentNullException("stageOne"); }
        if (stageTwo is null) { throw new ArgumentNullException("stageTwo"); }
        if (stageThree is null) { throw new ArgumentNullException("stageThree"); }
        if (stageFour is null) { throw new ArgumentNullException("stageFour"); }
        if (currentTick.IsDefault)
        {
            throw new ArgumentException("Current tick must be initialized.", nameof(currentTick));
        }

        if (schedulerConfiguration is null) { throw new ArgumentNullException("schedulerConfiguration"); }
        if (selectedProfile is null) { throw new ArgumentNullException("selectedProfile"); }

        if (!ReferenceEquals(stageTwo.InitialState, stageOne.FinalState) ||
            !ReferenceEquals(stageThree.InitialState, stageTwo.FinalState) ||
            !ReferenceEquals(stageFour.InitialShiftState, stageThree.FinalState))
        {
            throw new ArgumentException("The supplied stage executions must form the exact host-tick state chain.");
        }

        var shiftId = stageFour.FinalShiftState.ShiftId;
        if (stageOne.FinalState.ShiftId != shiftId ||
            stageTwo.InitialState.ShiftId != shiftId || stageTwo.FinalState.ShiftId != shiftId ||
            stageThree.InitialState.ShiftId != shiftId || stageThree.FinalState.ShiftId != shiftId ||
            stageFour.InitialShiftState.ShiftId != shiftId ||
            stageTwo.Batch.ShiftId != shiftId)
        {
            throw new ArgumentException("All supplied stage evidence must belong to one exact shift.");
        }

        if (stageTwo.Batch.CurrentTick != currentTick)
        {
            throw new ArgumentException("The accepted-intent batch tick must equal the stage-5 current tick.", nameof(currentTick));
        }

        var initialState = stageFour.FinalShiftState;

        // Step 1 — initial feed planning.
        var initialResult = _initialFeedPlanningService.Plan(initialState, currentTick, schedulerConfiguration);
        var initialStep = new InitialFeedPlanningStageStep(initialState, initialResult);

        // Step 2 — repair pending-transition execution (only for a LineRepairCompleted stage-1 source).
        var lineRepairSource = stageOne.LineRepair.Result;
        var beforeRepair = initialStep.AfterState;
        RepairPendingTransitionExecutionResult? repairResult = null;
        if (lineRepairSource is LineRepairCompleted completion)
        {
            repairResult = _repairPendingTransitionExecutionService.Execute(beforeRepair, completion, currentTick);
        }

        var repairStep = new RepairPendingTransitionStageStep(beforeRepair, repairResult);

        // Step 3 — exact source-bound repaired follow-up (only when the repair transition executed).
        var beforeFollowUp = repairStep.AfterState;
        IntakeDeadlineStartResult? repairedDeadlineStart = null;
        NormalFeedPlanningResult? repairedNormalPlanning = null;
        if (repairResult is RepairPendingTransitionExecuted executed)
        {
            if (executed.FollowUpRequirement == RepairPendingTransitionFollowUp.IntakeDeadlineStartRequired)
            {
                repairedDeadlineStart = _repairFeedGateIntakeDeadlineStartService.Start(beforeFollowUp, executed, selectedProfile);
            }
            else
            {
                repairedNormalPlanning = _repairAutoFeedNormalFeedPlanningService.Plan(beforeFollowUp, executed, schedulerConfiguration);
            }
        }

        var repairFollowUpStep = new RepairFollowUpStageStep(beforeFollowUp, repairedDeadlineStart, repairedNormalPlanning);

        // Step 4 — conditional default intake auto-route (only for an IntakeDeadlineExpired stage-3 source).
        var intakeExpirationSource = stageThree.IntakeDeadline.Result;
        var beforeDefault = repairFollowUpStep.AfterState;
        DefaultIntakeAutoRouteResult? defaultResult = null;
        if (intakeExpirationSource is IntakeDeadlineExpired expired)
        {
            defaultResult = _defaultIntakeAutoRouteService.Attempt(beforeDefault, expired.FollowUp, currentTick);
        }

        var defaultStep = new DefaultIntakeAutoRouteStageStep(beforeDefault, defaultResult);

        // Step 5 — evidence-triggered generic normal-feed planning.
        var beforeGeneric = defaultStep.AfterState;
        var stage2VacatedIntake = stageTwo.Steps.Any(step =>
            step.Outcome is ManualRoutingIntentStageOutcome manualRouting &&
            manualRouting.Result is ManualLogIntentAccepted accepted &&
            accepted.Transition.FromState == LogState.AT_INTAKE &&
            accepted.Transition.ToState != LogState.AT_INTAKE);
        var lineRepairCompleted = lineRepairSource is LineRepairCompleted;
        var defaultRouteVacatedIntake = defaultResult is DefaultIntakeAutoRouteApplied;
        var repairedAutoFeedPlannerAlreadyRan = repairedNormalPlanning is not null;
        var genericNormalPlanningRequired =
            (stage2VacatedIntake || lineRepairCompleted || defaultRouteVacatedIntake) &&
            !repairedAutoFeedPlannerAlreadyRan;
        NormalFeedPlanningResult? genericResult = null;
        if (genericNormalPlanningRequired)
        {
            genericResult = _normalFeedPlanningService.Plan(beforeGeneric, currentTick, schedulerConfiguration);
        }

        var genericStep = new GenericNormalFeedPlanningStageStep(beforeGeneric, genericNormalPlanningRequired, genericResult);

        // Step 6 — feed-due resolution (always).
        var beforeFeedDue = genericStep.AfterState;
        var feedDueResult = _feedDueResolutionService.Resolve(beforeFeedDue, currentTick);
        var feedDueStep = new FeedDueResolutionStageStep(beforeFeedDue, feedDueResult);

        // Step 7 — ordinary intake-deadline start (only for an admitted-to-intake resolved feed).
        var beforeOrdinary = feedDueStep.AfterState;
        IntakeDeadlineStartResult? ordinaryResult = null;
        if (feedDueResult is FeedDueResolved resolved &&
            resolved.Disposition == FeedDueDisposition.AdmittedToIntake &&
            resolved.FollowUpRequirement == FeedDueFollowUpRequirement.IntakeDeadlineStartRequired)
        {
            ordinaryResult = _intakeDeadlineStartService.Start(beforeOrdinary, resolved, selectedProfile);
        }

        var ordinaryStep = new OrdinaryIntakeDeadlineStartStageStep(beforeOrdinary, ordinaryResult);

        return new HostStageFiveFeedExecution(
            initialState,
            lineRepairSource,
            intakeExpirationSource,
            initialStep,
            repairStep,
            repairFollowUpStep,
            defaultStep,
            genericStep,
            feedDueStep,
            ordinaryStep);
    }
}
