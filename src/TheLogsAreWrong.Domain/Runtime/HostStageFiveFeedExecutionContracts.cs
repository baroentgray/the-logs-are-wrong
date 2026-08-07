using System.Linq;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>
/// The immutable result of executing frozen host stage 5 (<c>feed_and_auto_routes</c>) over one exact tick. It retains
/// the exact stage-5 initial state, every exact typed result from the bounded stage-5 order, the retained stage-1 and
/// stage-3 source evidence used to guard the conditional steps, the derived generic-normal-planning trigger, and the
/// exact final state derived only from the last executed step.
/// </summary>
public sealed class HostStageFiveFeedExecution
{
    internal HostStageFiveFeedExecution(
        ShiftRuntimeState initialState,
        InitialFeedPlanningResult initialFeedPlanning,
        LineRepairDueCompletionResult lineRepairSource,
        RepairPendingTransitionExecutionResult? repairExecution,
        IntakeDeadlineStartResult? repairedDeadlineStart,
        NormalFeedPlanningResult? repairedNormalPlanning,
        IntakeDeadlineExpirationResult intakeExpirationSource,
        DefaultIntakeAutoRouteResult? defaultRoute,
        bool genericNormalPlanningRequired,
        NormalFeedPlanningResult? genericNormalPlanning,
        FeedDueResolutionResult feedDue,
        IntakeDeadlineStartResult? ordinaryDeadlineStart)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(initialFeedPlanning);
        ArgumentNullException.ThrowIfNull(lineRepairSource);
        ArgumentNullException.ThrowIfNull(intakeExpirationSource);
        ArgumentNullException.ThrowIfNull(feedDue);

        // Repair-execution presence must match the stage-1 line-repair source.
        if ((lineRepairSource is LineRepairCompleted) != (repairExecution is not null))
        {
            throw new ArgumentException("A repair-execution result must be present exactly when stage-1 evidence is LineRepairCompleted.");
        }

        // Repaired follow-up presence/kind must match the executed repair transition.
        if (repairedDeadlineStart is not null && repairedNormalPlanning is not null)
        {
            throw new ArgumentException("A repaired follow-up cannot use both service families at once.");
        }

        if (repairExecution is RepairPendingTransitionExecuted executed)
        {
            var deadlineExpected = executed.FollowUpRequirement == RepairPendingTransitionFollowUp.IntakeDeadlineStartRequired;
            if (deadlineExpected != (repairedDeadlineStart is not null) || deadlineExpected == (repairedNormalPlanning is not null))
            {
                throw new ArgumentException("The repaired follow-up kind must match the executed follow-up requirement.");
            }
        }
        else if (repairedDeadlineStart is not null || repairedNormalPlanning is not null)
        {
            throw new ArgumentException("A repaired follow-up requires an executed repair transition.");
        }

        // Default-route presence must match the stage-3 intake-expiration source.
        if ((intakeExpirationSource is IntakeDeadlineExpired) != (defaultRoute is not null))
        {
            throw new ArgumentException("A default-route result must be present exactly when stage-3 evidence is IntakeDeadlineExpired.");
        }

        // Generic normal-planning presence must match the derived trigger.
        if (genericNormalPlanningRequired != (genericNormalPlanning is not null))
        {
            throw new ArgumentException("A generic normal-planning result must be present exactly when the derived trigger required it.");
        }

        // Ordinary deadline-start presence must match an admitted-to-intake resolved feed.
        var ordinaryExpected = feedDue is FeedDueResolved resolved &&
            resolved.Disposition == FeedDueDisposition.AdmittedToIntake &&
            resolved.FollowUpRequirement == FeedDueFollowUpRequirement.IntakeDeadlineStartRequired;
        if (ordinaryExpected != (ordinaryDeadlineStart is not null))
        {
            throw new ArgumentException("An ordinary intake-deadline start must be present exactly for an admitted-to-intake resolved feed.");
        }

        InitialState = initialState;
        InitialFeedPlanning = initialFeedPlanning;
        LineRepairSource = lineRepairSource;
        RepairExecution = repairExecution;
        RepairedDeadlineStart = repairedDeadlineStart;
        RepairedNormalPlanning = repairedNormalPlanning;
        IntakeExpirationSource = intakeExpirationSource;
        DefaultRoute = defaultRoute;
        GenericNormalPlanningRequired = genericNormalPlanningRequired;
        GenericNormalPlanning = genericNormalPlanning;
        FeedDue = feedDue;
        OrdinaryDeadlineStart = ordinaryDeadlineStart;
    }

    /// <summary>The exact stage-5 initial state (<c>stageFour.FinalShiftState</c>).</summary>
    public ShiftRuntimeState InitialState { get; }

    /// <summary>Step 1: the exact initial-feed planning result.</summary>
    public InitialFeedPlanningResult InitialFeedPlanning { get; }

    /// <summary>The retained stage-1 line-repair source evidence guarding the repair-execution step.</summary>
    public LineRepairDueCompletionResult LineRepairSource { get; }

    /// <summary>Step 2: the exact repair pending-transition execution result, present only for a LineRepairCompleted source.</summary>
    public RepairPendingTransitionExecutionResult? RepairExecution { get; }

    /// <summary>Step 3a: the exact repaired FEED_GATE intake-deadline start result, present only for that executed branch.</summary>
    public IntakeDeadlineStartResult? RepairedDeadlineStart { get; }

    /// <summary>Step 3b: the exact repaired INTAKE auto-feed normal-planning result, present only for that executed branch.</summary>
    public NormalFeedPlanningResult? RepairedNormalPlanning { get; }

    /// <summary>The retained stage-3 intake-expiration source evidence guarding the default-route step.</summary>
    public IntakeDeadlineExpirationResult IntakeExpirationSource { get; }

    /// <summary>Step 4: the exact default intake auto-route result, present only for an IntakeDeadlineExpired source.</summary>
    public DefaultIntakeAutoRouteResult? DefaultRoute { get; }

    /// <summary>Whether generic normal-feed planning was required, derived only from the closed current-tick evidence.</summary>
    public bool GenericNormalPlanningRequired { get; }

    /// <summary>Step 5: the exact generic normal-feed planning result, present only when the derived trigger required it.</summary>
    public NormalFeedPlanningResult? GenericNormalPlanning { get; }

    /// <summary>Step 6: the exact feed-due resolution result (always evaluated).</summary>
    public FeedDueResolutionResult FeedDue { get; }

    /// <summary>Step 7: the exact ordinary intake-deadline start result, present only for an admitted-to-intake resolved feed.</summary>
    public IntakeDeadlineStartResult? OrdinaryDeadlineStart { get; }

    /// <summary>The exact final stage-5 state, derived only from the last executed step.</summary>
    public ShiftRuntimeState FinalState => OrdinaryDeadlineStart?.State ?? FeedDue.State;
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
        ArgumentNullException.ThrowIfNull(stageOne);
        ArgumentNullException.ThrowIfNull(stageTwo);
        ArgumentNullException.ThrowIfNull(stageThree);
        ArgumentNullException.ThrowIfNull(stageFour);
        if (currentTick.IsDefault)
        {
            throw new ArgumentException("Current tick must be initialized.", nameof(currentTick));
        }

        ArgumentNullException.ThrowIfNull(schedulerConfiguration);
        ArgumentNullException.ThrowIfNull(selectedProfile);

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

        var currentState = stageFour.FinalShiftState;

        // Step 1 — initial feed planning.
        var initialFeedPlanning = _initialFeedPlanningService.Plan(currentState, currentTick, schedulerConfiguration);
        currentState = initialFeedPlanning.State;

        // Step 2 — repair pending-transition execution (only for a LineRepairCompleted stage-1 source).
        var lineRepairSource = stageOne.LineRepair.Result;
        RepairPendingTransitionExecutionResult? repairExecution = null;
        if (lineRepairSource is LineRepairCompleted completion)
        {
            repairExecution = _repairPendingTransitionExecutionService.Execute(currentState, completion, currentTick);
            currentState = repairExecution.State;
        }

        // Step 3 — exact source-bound repaired follow-up (only when the repair transition executed).
        IntakeDeadlineStartResult? repairedDeadlineStart = null;
        NormalFeedPlanningResult? repairedNormalPlanning = null;
        if (repairExecution is RepairPendingTransitionExecuted executed)
        {
            if (executed.FollowUpRequirement == RepairPendingTransitionFollowUp.IntakeDeadlineStartRequired)
            {
                repairedDeadlineStart = _repairFeedGateIntakeDeadlineStartService.Start(currentState, executed, selectedProfile);
                currentState = repairedDeadlineStart.State;
            }
            else
            {
                repairedNormalPlanning = _repairAutoFeedNormalFeedPlanningService.Plan(currentState, executed, schedulerConfiguration);
                currentState = repairedNormalPlanning.State;
            }
        }

        // Step 4 — conditional default intake auto-route (only for an IntakeDeadlineExpired stage-3 source).
        var intakeExpirationSource = stageThree.IntakeDeadline.Result;
        DefaultIntakeAutoRouteResult? defaultRoute = null;
        if (intakeExpirationSource is IntakeDeadlineExpired expired)
        {
            defaultRoute = _defaultIntakeAutoRouteService.Attempt(currentState, expired.FollowUp, currentTick);
            currentState = defaultRoute.State;
        }

        // Step 5 — evidence-triggered generic normal-feed planning.
        var stage2VacatedIntake = stageTwo.Steps.Any(step =>
            step.Outcome is ManualRoutingIntentStageOutcome manualRouting &&
            manualRouting.Result is ManualLogIntentAccepted accepted &&
            accepted.Transition.FromState == LogState.AT_INTAKE &&
            accepted.Transition.ToState != LogState.AT_INTAKE);
        var lineRepairCompleted = lineRepairSource is LineRepairCompleted;
        var defaultRouteVacatedIntake = defaultRoute is DefaultIntakeAutoRouteApplied;
        var repairedAutoFeedPlannerAlreadyRan = repairedNormalPlanning is not null;
        var genericNormalPlanningRequired =
            (stage2VacatedIntake || lineRepairCompleted || defaultRouteVacatedIntake) &&
            !repairedAutoFeedPlannerAlreadyRan;
        NormalFeedPlanningResult? genericNormalPlanning = null;
        if (genericNormalPlanningRequired)
        {
            genericNormalPlanning = _normalFeedPlanningService.Plan(currentState, currentTick, schedulerConfiguration);
            currentState = genericNormalPlanning.State;
        }

        // Step 6 — feed-due resolution (always).
        var feedDue = _feedDueResolutionService.Resolve(currentState, currentTick);
        currentState = feedDue.State;

        // Step 7 — ordinary intake-deadline start (only for an admitted-to-intake resolved feed).
        IntakeDeadlineStartResult? ordinaryDeadlineStart = null;
        if (feedDue is FeedDueResolved resolved &&
            resolved.Disposition == FeedDueDisposition.AdmittedToIntake &&
            resolved.FollowUpRequirement == FeedDueFollowUpRequirement.IntakeDeadlineStartRequired)
        {
            ordinaryDeadlineStart = _intakeDeadlineStartService.Start(currentState, resolved, selectedProfile);
        }

        return new HostStageFiveFeedExecution(
            stageFour.FinalShiftState,
            initialFeedPlanning,
            lineRepairSource,
            repairExecution,
            repairedDeadlineStart,
            repairedNormalPlanning,
            intakeExpirationSource,
            defaultRoute,
            genericNormalPlanningRequired,
            genericNormalPlanning,
            feedDue,
            ordinaryDeadlineStart);
    }
}
