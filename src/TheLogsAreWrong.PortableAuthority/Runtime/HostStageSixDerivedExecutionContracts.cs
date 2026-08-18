using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>One immutable trace entry for one applied movement-noise evidence item in frozen host stage 6.</summary>
public sealed class MovementNoiseApplicationStageStep
{
    internal MovementNoiseApplicationStageStep(MovementNoiseRuntimeState beforeState, MovementNoiseApplicationResult result)
    {
        if (beforeState is null) { throw new ArgumentNullException("beforeState"); }
        if (result is null) { throw new ArgumentNullException("result"); }
        BeforeState = beforeState;
        Result = result;
    }

    /// <summary>The exact movement-noise runtime observed before this evidence item was applied.</summary>
    public MovementNoiseRuntimeState BeforeState { get; }

    /// <summary>The exact result returned by the existing movement-noise application service.</summary>
    public MovementNoiseApplicationResult Result { get; }

    /// <summary>The exact resulting movement-noise runtime, always the retained result's own returned state by reference.</summary>
    public MovementNoiseRuntimeState AfterState => Result.State;
}

/// <summary>
/// One immutable trace entry for the conditional source-bound intake-auto-feed jam family in frozen host stage 6.
/// The triggering source and the derivation result are present together, or both absent when the family did not run.
/// </summary>
public sealed class IntakeAutoFeedJamStageStep
{
    internal IntakeAutoFeedJamStageStep(
        ShiftRuntimeState beforeShiftState,
        DefaultIntakeAutoRouteBlocked? source,
        IntakeAutoFeedJamDerivationResult? result)
    {
        if (beforeShiftState is null) { throw new ArgumentNullException("beforeShiftState"); }
        if ((source is null) != (result is null))
        {
            throw new ArgumentException("An intake-auto-feed jam step must retain a result exactly when it retains its triggering source.");
        }

        // The conditional family exists only for a blocked default route whose retained follow-up actually
        // requires this derivation; an ExistingLineConditionRetained source can never be an executed step.
        if (source is not null && source.FollowUp != DefaultIntakeAutoRouteFollowUp.IntakeAutoFeedJamDerivationRequired)
        {
            throw new ArgumentException("An executed intake-auto-feed jam step requires a source whose follow-up is IntakeAutoFeedJamDerivationRequired.");
        }

        BeforeShiftState = beforeShiftState;
        Source = source;
        Result = result;
    }

    /// <summary>The exact state observed before the intake-auto-feed jam family ran, always <c>InitialShiftState</c>.</summary>
    public ShiftRuntimeState BeforeShiftState { get; }

    /// <summary>The exact retained stage-5 blocked-route evidence that required this family, or null when it did not run.</summary>
    public DefaultIntakeAutoRouteBlocked? Source { get; }

    /// <summary>The exact result returned by the existing intake-auto-feed jam derivation service, or null when it did not run.</summary>
    public IntakeAutoFeedJamDerivationResult? Result { get; }

    /// <summary>The exact resulting state: the result's own state when the family ran, otherwise the exact before-state.</summary>
    public ShiftRuntimeState AfterShiftState => Result?.State ?? BeforeShiftState;
}

/// <summary>One immutable trace entry for the always-evaluated feed-gate jam family in frozen host stage 6.</summary>
public sealed class FeedGateJamStageStep
{
    internal FeedGateJamStageStep(ShiftRuntimeState beforeShiftState, FeedGateJamDerivationResult result)
    {
        if (beforeShiftState is null) { throw new ArgumentNullException("beforeShiftState"); }
        if (result is null) { throw new ArgumentNullException("result"); }
        BeforeShiftState = beforeShiftState;
        Result = result;
    }

    /// <summary>The exact state observed before feed-gate jam derivation ran, always the intake-auto-feed step's after-state.</summary>
    public ShiftRuntimeState BeforeShiftState { get; }

    /// <summary>The exact result returned by the existing feed-gate jam derivation service.</summary>
    public FeedGateJamDerivationResult Result { get; }

    /// <summary>The exact resulting state, always the retained result's own returned state by reference.</summary>
    public ShiftRuntimeState AfterShiftState => Result.State;
}

/// <summary>One immutable trace entry for the always-evaluated line-noise derivation in frozen host stage 6.</summary>
public sealed class LineNoiseDerivationStageStep
{
    internal LineNoiseDerivationStageStep(
        LineNoiseRuntimeState beforeState,
        ShiftRuntimeState shiftState,
        MovementNoiseRuntimeState movementNoiseState,
        LineNoiseEvaluationResult result)
    {
        if (beforeState is null) { throw new ArgumentNullException("beforeState"); }
        if (shiftState is null) { throw new ArgumentNullException("shiftState"); }
        if (movementNoiseState is null) { throw new ArgumentNullException("movementNoiseState"); }
        if (result is null) { throw new ArgumentNullException("result"); }
        BeforeState = beforeState;
        ShiftState = shiftState;
        MovementNoiseState = movementNoiseState;
        Result = result;
    }

    /// <summary>The exact line-noise runtime observed before this tick's derivation, always the stage-6 initial line-noise runtime.</summary>
    public LineNoiseRuntimeState BeforeState { get; }

    /// <summary>The exact post-jam shift state consumed by the derivation, always the feed-gate step's after-state.</summary>
    public ShiftRuntimeState ShiftState { get; }

    /// <summary>The exact final movement-noise runtime consumed by the derivation.</summary>
    public MovementNoiseRuntimeState MovementNoiseState { get; }

    /// <summary>The exact result returned by the existing line-noise derivation service.</summary>
    public LineNoiseEvaluationResult Result { get; }

    /// <summary>The exact resulting line-noise runtime, always the retained result's own returned state by reference.</summary>
    public LineNoiseRuntimeState AfterState => Result.State;
}

/// <summary>One immutable trace entry for the always-evaluated confirmation-condition update in frozen host stage 6.</summary>
public sealed class ConfirmationConditionStageStep
{
    internal ConfirmationConditionStageStep(
        ShiftRuntimeState beforeShiftState,
        LineNoiseEvaluationResult consumedLineNoise,
        ConfirmationTestConditionResult result)
    {
        if (beforeShiftState is null) { throw new ArgumentNullException("beforeShiftState"); }
        if (consumedLineNoise is null) { throw new ArgumentNullException("consumedLineNoise"); }
        if (result is null) { throw new ArgumentNullException("result"); }
        BeforeShiftState = beforeShiftState;
        ConsumedLineNoise = consumedLineNoise;
        Result = result;
    }

    /// <summary>The exact state observed before confirmation-condition handling ran, always the feed-gate step's after-state.</summary>
    public ShiftRuntimeState BeforeShiftState { get; }

    /// <summary>The exact line-noise evaluation result consumed by this update, always the retained line-noise step's own result.</summary>
    public LineNoiseEvaluationResult ConsumedLineNoise { get; }

    /// <summary>The exact result returned by the existing confirmation-condition service.</summary>
    public ConfirmationTestConditionResult Result { get; }

    /// <summary>The exact resulting state, always the retained result's own returned state by reference.</summary>
    public ShiftRuntimeState AfterShiftState => Result.State;
}

/// <summary>One immutable trace entry for the strict tail-of-stage-6 host-tick completion checkpoint.</summary>
public sealed class HostTickCheckpointStageStep
{
    internal HostTickCheckpointStageStep(
        ShiftRuntimeState postStageShift,
        QuotaRuntimeState postStageQuota,
        HostTickCheckpointResult result)
    {
        if (postStageShift is null) { throw new ArgumentNullException("postStageShift"); }
        if (postStageQuota is null) { throw new ArgumentNullException("postStageQuota"); }
        if (result is null) { throw new ArgumentNullException("result"); }
        PostStageShift = postStageShift;
        PostStageQuota = postStageQuota;
        Result = result;
    }

    /// <summary>The exact post-stage-6 shift state supplied to the checkpoint, always the confirmation step's after-state.</summary>
    public ShiftRuntimeState PostStageShift { get; }

    /// <summary>The exact post-saw quota state supplied to the checkpoint, always <c>stageFour.FinalQuotaState</c>.</summary>
    public QuotaRuntimeState PostStageQuota { get; }

    /// <summary>The exact result returned by the existing host-tick completion checkpoint service.</summary>
    public HostTickCheckpointResult Result { get; }
}

/// <summary>
/// The immutable, self-defending result of executing frozen host stage 6 (<c>derived_states</c>) over one exact tick.
/// It retains the exact initial shift and movement-noise runtimes, the ordered movement-application trace, the two
/// fixed-precedence jam steps, the line-noise and confirmation steps, and the tail checkpoint step. Its constructor
/// rejects any trace whose exact before/result/after reference chain is not internally consistent.
/// </summary>
public sealed class HostStageSixDerivedExecution
{
    internal HostStageSixDerivedExecution(
        ShiftRuntimeState initialShiftState,
        MovementNoiseRuntimeState initialMovementNoise,
        LineNoiseRuntimeState initialLineNoise,
        QuotaRuntimeState quotaState,
        ImmutableArray<MovementNoiseApplicationStageStep> movementSteps,
        IntakeAutoFeedJamStageStep intakeAutoFeedJamStep,
        FeedGateJamStageStep feedGateJamStep,
        LineNoiseDerivationStageStep lineNoiseStep,
        ConfirmationConditionStageStep confirmationStep,
        HostTickCheckpointStageStep checkpointStep,
        HostTickProgressionEvidence progression,
        ShiftLifecycleRuntimeState lifecycle,
        ImmutableHashSet<ItemId> activeTools,
        ServerTick currentTick)
    {
        if (initialShiftState is null) { throw new ArgumentNullException("initialShiftState"); }
        if (initialMovementNoise is null) { throw new ArgumentNullException("initialMovementNoise"); }
        if (initialLineNoise is null) { throw new ArgumentNullException("initialLineNoise"); }
        if (quotaState is null) { throw new ArgumentNullException("quotaState"); }
        if (intakeAutoFeedJamStep is null) { throw new ArgumentNullException("intakeAutoFeedJamStep"); }
        if (feedGateJamStep is null) { throw new ArgumentNullException("feedGateJamStep"); }
        if (lineNoiseStep is null) { throw new ArgumentNullException("lineNoiseStep"); }
        if (confirmationStep is null) { throw new ArgumentNullException("confirmationStep"); }
        if (checkpointStep is null) { throw new ArgumentNullException("checkpointStep"); }
        if (progression is null) { throw new ArgumentNullException("progression"); }
        if (lifecycle is null) { throw new ArgumentNullException("lifecycle"); }
        if (activeTools is null) { throw new ArgumentNullException("activeTools"); }
        if (movementSteps.IsDefault)
        {
            throw new ArgumentException("Movement steps must be an initialized immutable array.", nameof(movementSteps));
        }

        // Exact ordered movement-runtime reference chain — the closed trace defends itself.
        var expectedMovementBefore = initialMovementNoise;
        foreach (var step in movementSteps)
        {
            if (!ReferenceEquals(step.BeforeState, expectedMovementBefore))
            {
                throw new ArgumentException("Movement steps must form the exact ordered before/after reference chain.");
            }

            expectedMovementBefore = step.AfterState;
        }

        var finalMovementNoise = expectedMovementBefore;

        // Exact ordered shift-state reference chain across both jam families, line noise, confirmation, and checkpoint.
        if (!ReferenceEquals(intakeAutoFeedJamStep.BeforeShiftState, initialShiftState) ||
            !ReferenceEquals(feedGateJamStep.BeforeShiftState, intakeAutoFeedJamStep.AfterShiftState) ||
            !ReferenceEquals(lineNoiseStep.ShiftState, feedGateJamStep.AfterShiftState) ||
            !ReferenceEquals(confirmationStep.BeforeShiftState, feedGateJamStep.AfterShiftState) ||
            !ReferenceEquals(checkpointStep.PostStageShift, confirmationStep.AfterShiftState))
        {
            throw new ArgumentException("Stage-6 steps must form the exact ordered shift-state reference chain.");
        }

        // Line-noise derivation must consume the exact final movement-noise runtime.
        if (!ReferenceEquals(lineNoiseStep.MovementNoiseState, finalMovementNoise))
        {
            throw new ArgumentException("Line-noise derivation must consume the exact final movement-noise runtime.");
        }

        // Line-noise derivation must start from the exact retained initial line-noise runtime.
        if (!ReferenceEquals(lineNoiseStep.BeforeState, initialLineNoise))
        {
            throw new ArgumentException("Line-noise derivation must start from the exact retained initial line-noise runtime.");
        }

        // Confirmation must consume the exact same line-noise result instance line-noise derivation produced.
        if (!ReferenceEquals(confirmationStep.ConsumedLineNoise, lineNoiseStep.Result))
        {
            throw new ArgumentException("Confirmation-condition handling must consume the exact retained line-noise result.");
        }

        // Stage 6 never mutates quota: the checkpoint must receive the exact retained quota reference.
        if (!ReferenceEquals(checkpointStep.PostStageQuota, quotaState))
        {
            throw new ArgumentException("The checkpoint must receive the exact retained stage-four final quota reference.");
        }

        InitialShiftState = initialShiftState;
        InitialMovementNoise = initialMovementNoise;
        InitialLineNoise = initialLineNoise;
        InitialQuotaState = quotaState;
        MovementSteps = movementSteps;
        FinalMovementNoise = finalMovementNoise;
        IntakeAutoFeedJamStep = intakeAutoFeedJamStep;
        FeedGateJamStep = feedGateJamStep;
        LineNoiseStep = lineNoiseStep;
        ConfirmationStep = confirmationStep;
        CheckpointStep = checkpointStep;
        Progression = progression;
        Lifecycle = lifecycle;
        ActiveTools = activeTools;
        CurrentTick = currentTick;
    }

    /// <summary>The exact stage-6 initial shift state (<c>stageFive.FinalState</c>).</summary>
    public ShiftRuntimeState InitialShiftState { get; }

    /// <summary>The exact movement-noise runtime retained from the preceding published tick.</summary>
    public MovementNoiseRuntimeState InitialMovementNoise { get; }

    /// <summary>The exact line-noise runtime retained from the preceding published tick.</summary>
    public LineNoiseRuntimeState InitialLineNoise { get; }

    /// <summary>The exact quota state stage 6 observed, always <c>stageFour.FinalQuotaState</c>. Stage 6 never settles quota.</summary>
    public QuotaRuntimeState InitialQuotaState { get; }

    /// <summary>The exact final quota state, always the same reference as <see cref="InitialQuotaState"/>.</summary>
    public QuotaRuntimeState FinalQuotaState => InitialQuotaState;

    /// <summary>The exact ordered movement-application trace, one step per accepted evidence item in frozen host order.</summary>
    public ImmutableArray<MovementNoiseApplicationStageStep> MovementSteps { get; }

    /// <summary>The exact final movement-noise runtime: the last step's after-state, or <see cref="InitialMovementNoise"/> when empty.</summary>
    public MovementNoiseRuntimeState FinalMovementNoise { get; }

    /// <summary>The first fixed-order step: conditional source-bound intake-auto-feed jam derivation.</summary>
    public IntakeAutoFeedJamStageStep IntakeAutoFeedJamStep { get; }

    /// <summary>The second fixed-order step: always-evaluated feed-gate jam derivation.</summary>
    public FeedGateJamStageStep FeedGateJamStep { get; }

    /// <summary>The third fixed-order step: always-evaluated line-noise derivation.</summary>
    public LineNoiseDerivationStageStep LineNoiseStep { get; }

    /// <summary>The fourth fixed-order step: always-evaluated confirmation-condition update.</summary>
    public ConfirmationConditionStageStep ConfirmationStep { get; }

    /// <summary>The fifth and strictly last fixed-order step: the host-tick completion checkpoint.</summary>
    public HostTickCheckpointStageStep CheckpointStep { get; }

    /// <summary>The exact input progression evidence supplied to the checkpoint.</summary>
    public HostTickProgressionEvidence Progression { get; }

    /// <summary>The exact input lifecycle evidence supplied to the checkpoint.</summary>
    public ShiftLifecycleRuntimeState Lifecycle { get; }

    /// <summary>The exact input active-tool set supplied to confirmation-condition handling.</summary>
    public ImmutableHashSet<ItemId> ActiveTools { get; }

    /// <summary>The exact tick this stage-6 execution ran for.</summary>
    public ServerTick CurrentTick { get; }

    // ----- Convenience projections onto the retained step results -----

    /// <summary>The exact retained stage-5 blocked-route evidence, or null when the intake-auto-feed family did not run.</summary>
    public DefaultIntakeAutoRouteBlocked? IntakeAutoFeedJamSource => IntakeAutoFeedJamStep.Source;

    /// <summary>The exact intake-auto-feed jam derivation result, or null when the family did not run.</summary>
    public IntakeAutoFeedJamDerivationResult? IntakeAutoFeedJam => IntakeAutoFeedJamStep.Result;

    /// <summary>The exact feed-gate jam derivation result.</summary>
    public FeedGateJamDerivationResult FeedGateJam => FeedGateJamStep.Result;

    /// <summary>The exact line-noise evaluation result.</summary>
    public LineNoiseEvaluationResult LineNoiseEvaluation => LineNoiseStep.Result;

    /// <summary>The exact confirmation-condition result.</summary>
    public ConfirmationTestConditionResult Confirmation => ConfirmationStep.Result;

    /// <summary>The exact host-tick checkpoint result.</summary>
    public HostTickCheckpointResult Checkpoint => CheckpointStep.Result;

    /// <summary>The exact final stage-6 shift state, always the confirmation step's after-state (the checkpoint's exact post-stage input).</summary>
    public ShiftRuntimeState FinalShiftState => ConfirmationStep.AfterShiftState;

    /// <summary>The exact final line-noise runtime, always the line-noise step's after-state by reference.</summary>
    public LineNoiseRuntimeState FinalLineNoise => LineNoiseStep.AfterState;
}

/// <summary>
/// Pure frozen host stage 6 executor. It applies accepted movement evidence from stages 2, 4 and 5 to the movement-noise
/// runtime in fixed host order, derives the intake-auto-feed and feed-gate jam families in that fixed precedence, derives
/// line noise from the exact post-jam state and final movement-noise runtime, updates confirmation conditions from that
/// exact line-noise result, and completes the host-tick checkpoint as the strict tail of stage 6. It sorts nothing,
/// assigns no version, executes no other host stage, advances no containment, settles no quota, and emits no event.
/// Programmer/invariant exceptions from a delegated service propagate; because every state is immutable and local, no
/// partial result escapes.
/// </summary>
public sealed class HostStageSixDerivedExecutor
{
    private readonly MovementNoiseApplicationService _movementNoiseService = new();
    private readonly IntakeAutoFeedJamDerivationService _intakeAutoFeedJamDerivationService = new();
    private readonly FeedGateJamDerivationService _feedGateJamDerivationService = new();
    private readonly LineNoiseDerivationService _lineNoiseDerivationService = new();
    private readonly ConfirmationTestConditionService _confirmationTestConditionService = new();
    private readonly HostTickCompletionCheckpointService _hostTickCompletionCheckpointService = new();

    /// <summary>Executes stage 6 for the exact <paramref name="currentTick"/> from the exact post-stage-5 evidence.</summary>
    public HostStageSixDerivedExecution Execute(
        HostStageOneCompletionExecution stageOne,
        AcceptedIntentStageExecution stageTwo,
        HostStageThreeDeadlineExecution stageThree,
        HostStageFourSawExecution stageFour,
        HostStageFiveFeedExecution stageFive,
        MovementNoiseRuntimeState initialMovementNoise,
        LineNoiseRuntimeState initialLineNoise,
        HostTickProgressionEvidence progression,
        ShiftLifecycleRuntimeState lifecycle,
        ImmutableHashSet<ItemId> activeTools,
        ServerTick currentTick,
        SchedulerConfiguration schedulerConfiguration,
        ShiftConfiguration shiftConfiguration,
        AnomalyCatalog anomalyCatalog)
    {
        if (stageOne is null) { throw new ArgumentNullException("stageOne"); }
        if (stageTwo is null) { throw new ArgumentNullException("stageTwo"); }
        if (stageThree is null) { throw new ArgumentNullException("stageThree"); }
        if (stageFour is null) { throw new ArgumentNullException("stageFour"); }
        if (stageFive is null) { throw new ArgumentNullException("stageFive"); }
        if (initialMovementNoise is null) { throw new ArgumentNullException("initialMovementNoise"); }
        if (initialLineNoise is null) { throw new ArgumentNullException("initialLineNoise"); }
        if (progression is null) { throw new ArgumentNullException("progression"); }
        if (lifecycle is null) { throw new ArgumentNullException("lifecycle"); }
        if (activeTools is null) { throw new ArgumentNullException("activeTools"); }
        if (currentTick.IsDefault)
        {
            throw new ArgumentException("Current tick must be initialized.", nameof(currentTick));
        }

        if (schedulerConfiguration is null) { throw new ArgumentNullException("schedulerConfiguration"); }
        if (shiftConfiguration is null) { throw new ArgumentNullException("shiftConfiguration"); }
        if (anomalyCatalog is null) { throw new ArgumentNullException("anomalyCatalog"); }
        if (anomalyCatalog.Definitions is null) { throw new ArgumentNullException("anomalyCatalog.Definitions"); }
        if (activeTools.Any(tool => tool.IsDefault))
        {
            throw new ArgumentException("Active-tool evidence cannot contain a default item.", nameof(activeTools));
        }

        if (!ReferenceEquals(stageTwo.InitialState, stageOne.FinalState) ||
            !ReferenceEquals(stageThree.InitialState, stageTwo.FinalState) ||
            !ReferenceEquals(stageFour.InitialShiftState, stageThree.FinalState) ||
            !ReferenceEquals(stageFive.InitialState, stageFour.FinalShiftState))
        {
            throw new ArgumentException("The supplied stage executions must form the exact host-tick state chain.");
        }

        if (!ReferenceEquals(stageFive.LineRepairSource, stageOne.LineRepair.Result) ||
            !ReferenceEquals(stageFive.IntakeExpirationSource, stageThree.IntakeDeadline.Result))
        {
            throw new ArgumentException("Stage-5 retained source evidence must match its exact stage-1/stage-3 origin.");
        }

        var shiftId = stageFive.FinalState.ShiftId;
        if (stageOne.FinalState.ShiftId != shiftId ||
            stageTwo.InitialState.ShiftId != shiftId || stageTwo.FinalState.ShiftId != shiftId ||
            stageThree.InitialState.ShiftId != shiftId || stageThree.FinalState.ShiftId != shiftId ||
            stageFour.InitialShiftState.ShiftId != shiftId || stageFour.FinalShiftState.ShiftId != shiftId ||
            stageFive.InitialState.ShiftId != shiftId ||
            initialMovementNoise.ShiftId != shiftId || initialLineNoise.ShiftId != shiftId ||
            progression.ShiftId != shiftId || lifecycle.ShiftId != shiftId ||
            shiftConfiguration.ShiftId != shiftId || stageTwo.Batch.ShiftId != shiftId)
        {
            throw new ArgumentException("All supplied stage-6 evidence must belong to one exact shift.");
        }

        if (stageTwo.Batch.CurrentTick != currentTick)
        {
            throw new ArgumentException("The accepted-intent batch tick must equal the stage-6 current tick.", nameof(currentTick));
        }

        var initialShiftState = stageFive.FinalState;

        // Step 1 — movement-noise application, consuming exact accepted evidence in frozen host order.
        var movementSteps = ImmutableArray.CreateBuilder<MovementNoiseApplicationStageStep>();
        var movementRuntime = initialMovementNoise;

        foreach (var step in stageTwo.Steps)
        {
            if (step.Outcome is ManualRoutingIntentStageOutcome manual && manual.Result is ManualLogIntentAccepted accepted)
            {
                movementRuntime = ApplyMovement(movementSteps, movementRuntime,
                    _movementNoiseService.Apply(movementRuntime, accepted, currentTick, schedulerConfiguration));
            }
        }

        if (stageFour.Completion.Result is SawCycleCompleted sawCompleted)
        {
            movementRuntime = ApplyMovement(movementSteps, movementRuntime,
                _movementNoiseService.Apply(movementRuntime, sawCompleted, schedulerConfiguration));
        }

        if (stageFour.Start.Result is SawCycleStarted sawStarted)
        {
            movementRuntime = ApplyMovement(movementSteps, movementRuntime,
                _movementNoiseService.Apply(movementRuntime, sawStarted, schedulerConfiguration));
        }

        if (stageFive.RepairExecution is RepairPendingTransitionExecuted repairExecuted)
        {
            movementRuntime = ApplyMovement(movementSteps, movementRuntime,
                _movementNoiseService.Apply(movementRuntime, repairExecuted, schedulerConfiguration));
        }

        if (stageFive.DefaultRoute is DefaultIntakeAutoRouteApplied defaultApplied)
        {
            movementRuntime = ApplyMovement(movementSteps, movementRuntime,
                _movementNoiseService.Apply(movementRuntime, defaultApplied, schedulerConfiguration));
        }

        if (stageFive.FeedDue is FeedDueResolved feedDueResolved)
        {
            movementRuntime = ApplyMovement(movementSteps, movementRuntime,
                _movementNoiseService.Apply(movementRuntime, feedDueResolved, schedulerConfiguration));
        }

        var finalMovementNoise = movementRuntime;

        // Step 2 — conditional source-bound intake-auto-feed jam derivation (before feed-gate derivation).
        DefaultIntakeAutoRouteBlocked? intakeAutoFeedSource = null;
        IntakeAutoFeedJamDerivationResult? intakeAutoFeedResult = null;
        if (stageFive.DefaultRoute is DefaultIntakeAutoRouteBlocked blocked &&
            blocked.FollowUp == DefaultIntakeAutoRouteFollowUp.IntakeAutoFeedJamDerivationRequired)
        {
            intakeAutoFeedSource = blocked;
            intakeAutoFeedResult = _intakeAutoFeedJamDerivationService.Derive(initialShiftState, blocked);
        }

        var intakeAutoFeedJamStep = new IntakeAutoFeedJamStageStep(initialShiftState, intakeAutoFeedSource, intakeAutoFeedResult);

        // Step 3 — always-evaluated feed-gate jam derivation, from the exact post-intake-auto-feed state.
        var feedGateResult = _feedGateJamDerivationService.Derive(intakeAutoFeedJamStep.AfterShiftState, currentTick);
        var feedGateJamStep = new FeedGateJamStageStep(intakeAutoFeedJamStep.AfterShiftState, feedGateResult);

        // Step 4 — always-evaluated line-noise derivation, after both jam families.
        var lineNoiseResult = _lineNoiseDerivationService.Evaluate(initialLineNoise, feedGateJamStep.AfterShiftState, finalMovementNoise, currentTick);
        var lineNoiseStep = new LineNoiseDerivationStageStep(initialLineNoise, feedGateJamStep.AfterShiftState, finalMovementNoise, lineNoiseResult);

        // Step 5 — always-evaluated confirmation-condition update, from the exact current-tick line-noise result.
        var confirmationResult = _confirmationTestConditionService.Update(
            feedGateJamStep.AfterShiftState, currentTick, lineNoiseResult, activeTools, anomalyCatalog);
        var confirmationStep = new ConfirmationConditionStageStep(feedGateJamStep.AfterShiftState, lineNoiseResult, confirmationResult);

        // Step 6 — the strict tail-of-stage-6 completion checkpoint.
        var checkpointResult = _hostTickCompletionCheckpointService.Complete(
            progression, lifecycle, confirmationStep.AfterShiftState, stageFour.FinalQuotaState, currentTick, shiftConfiguration);
        var checkpointStep = new HostTickCheckpointStageStep(confirmationStep.AfterShiftState, stageFour.FinalQuotaState, checkpointResult);

        return new HostStageSixDerivedExecution(
            initialShiftState,
            initialMovementNoise,
            initialLineNoise,
            stageFour.FinalQuotaState,
            movementSteps.ToImmutable(),
            intakeAutoFeedJamStep,
            feedGateJamStep,
            lineNoiseStep,
            confirmationStep,
            checkpointStep,
            progression,
            lifecycle,
            activeTools,
            currentTick);
    }

    private static MovementNoiseRuntimeState ApplyMovement(
        ImmutableArray<MovementNoiseApplicationStageStep>.Builder steps,
        MovementNoiseRuntimeState beforeState,
        MovementNoiseApplicationResult result)
    {
        steps.Add(new MovementNoiseApplicationStageStep(beforeState, result));
        return result.State;
    }
}
