using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Logs;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Scheduler;

public abstract record RepairPendingTransitionExecutionResult(ShiftRuntimeState State);

public sealed record RepairPendingTransitionNoPendingTransition(ShiftRuntimeState State)
    : RepairPendingTransitionExecutionResult(State);

public sealed record RepairPendingTransitionExistingLineConditionRetained(ShiftRuntimeState State)
    : RepairPendingTransitionExecutionResult(State);

public sealed record RepairPendingTransitionOwnerMissing(ShiftRuntimeState State, LogId LogId)
    : RepairPendingTransitionExecutionResult(State);

public sealed record RepairPendingTransitionNoLongerApplicable(ShiftRuntimeState State, LogId LogId)
    : RepairPendingTransitionExecutionResult(State);

public sealed record RepairPendingTransitionTargetOccupied(ShiftRuntimeState State, LogId LogId)
    : RepairPendingTransitionExecutionResult(State);

public enum RepairPendingTransitionExecutionFailureReason
{
    InvalidRetainedCompletion,
    CurrentStatePrecedesCompletion,
    DivergentSameVersion,
    ExecutionTickPrecedesLine,
    CurrentShapeInvalid,
    HostTransitionContradictory
}

public sealed record RepairPendingTransitionExecutionDefensiveFailure(
    ShiftRuntimeState State,
    RepairPendingTransitionExecutionFailureReason Reason)
    : RepairPendingTransitionExecutionResult(State);

public enum RepairPendingTransitionFollowUp
{
    IntakeDeadlineStartRequired,
    NormalFeedPlanningEvaluationRequired
}

public sealed record RepairPendingTransitionExecuted : RepairPendingTransitionExecutionResult
{
    public RepairPendingTransitionExecuted(
        ShiftRuntimeState state,
        PendingLineTransitionDescriptor pendingTransition,
        ServerTick appliedAt,
        StateVersion priorStateVersion,
        StateVersion currentStateVersion,
        RepairPendingTransitionFollowUp followUpRequirement)
        : base(state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(pendingTransition);
        if (appliedAt.IsDefault || !Enum.IsDefined(followUpRequirement) ||
            currentStateVersion != priorStateVersion.Next() || state.StateVersion != currentStateVersion)
        {
            throw new ArgumentException("An accepted pending transition execution requires exact state, timing, version, and follow-up data.");
        }

        PendingTransition = pendingTransition;
        AppliedAt = appliedAt;
        LogId = pendingTransition.LogId;
        Cause = pendingTransition.Cause;
        Source = pendingTransition.FromState;
        Destination = pendingTransition.ToState;
        PriorStateVersion = priorStateVersion;
        CurrentStateVersion = currentStateVersion;
        FollowUpRequirement = followUpRequirement;
    }

    public PendingLineTransitionDescriptor PendingTransition { get; }
    public ServerTick AppliedAt { get; }
    public LogId LogId { get; }
    public JamCause Cause { get; }
    public LogState Source { get; }
    public LogState Destination { get; }
    public StateVersion PriorStateVersion { get; }
    public StateVersion CurrentStateVersion { get; }
    public RepairPendingTransitionFollowUp FollowUpRequirement { get; }
}

/// <summary>Host-owned feed-and-auto-routes seam that executes only an accepted repair completion's frozen pending transition.</summary>
public sealed class RepairPendingTransitionExecutionService
{
    private readonly HostLogTransitionService _transitions = new();

    public RepairPendingTransitionExecutionResult Execute(
        ShiftRuntimeState state,
        LineRepairCompleted completion,
        ServerTick currentTick)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(completion);
        if (currentTick.IsDefault)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "The authoritative execution tick must be initialized.");
        }

        if (!HasValidRetainedShape(completion))
        {
            return new RepairPendingTransitionExecutionDefensiveFailure(state, RepairPendingTransitionExecutionFailureReason.InvalidRetainedCompletion);
        }

        var retainedState = completion.State;
        if (state.StateVersion < retainedState.StateVersion)
        {
            return new RepairPendingTransitionExecutionDefensiveFailure(state, RepairPendingTransitionExecutionFailureReason.CurrentStatePrecedesCompletion);
        }

        if (state.StateVersion == retainedState.StateVersion && !ReferenceEquals(state, retainedState))
        {
            return new RepairPendingTransitionExecutionDefensiveFailure(state, RepairPendingTransitionExecutionFailureReason.DivergentSameVersion);
        }

        if (currentTick < retainedState.Line.EnteredAt || currentTick < state.Line.EnteredAt)
        {
            return new RepairPendingTransitionExecutionDefensiveFailure(state, RepairPendingTransitionExecutionFailureReason.ExecutionTickPrecedesLine);
        }

        if (completion.PendingTransition is not { } pending)
        {
            return new RepairPendingTransitionNoPendingTransition(state);
        }

        if (state.Line.State != LineState.LINE_CLEAR)
        {
            return new RepairPendingTransitionExistingLineConditionRetained(state);
        }

        if (!state.TryGetLog(pending.LogId, out var owner))
        {
            return new RepairPendingTransitionOwnerMissing(state, pending.LogId);
        }

        if (owner.State != pending.FromState)
        {
            return new RepairPendingTransitionNoLongerApplicable(state, pending.LogId);
        }

        if (IsTargetOccupied(state, pending.ToState))
        {
            return new RepairPendingTransitionTargetOccupied(state, pending.LogId);
        }

        if (!HasValidCurrentShape(state, pending))
        {
            return new RepairPendingTransitionExecutionDefensiveFailure(state, RepairPendingTransitionExecutionFailureReason.CurrentShapeInvalid);
        }

        var transition = _transitions.Apply(state, pending.LogId, pending.ToState);
        if (transition is not HostLogTransitionAccepted accepted || !MatchesAcceptedTransition(state, pending, accepted))
        {
            return new RepairPendingTransitionExecutionDefensiveFailure(state, RepairPendingTransitionExecutionFailureReason.HostTransitionContradictory);
        }

        return new RepairPendingTransitionExecuted(
            accepted.State,
            pending,
            currentTick,
            accepted.Descriptor.PriorStateVersion,
            accepted.Descriptor.CurrentStateVersion,
            GetFollowUp(pending.Cause));
    }

    private static bool HasValidRetainedShape(LineRepairCompleted completion)
    {
        if (completion.State is null ||
            completion.State.Line.State != LineState.LINE_CLEAR ||
            completion.State.Line.Cause is not null ||
            completion.State.Line.PendingLogId is not null ||
            completion.State.Line.ActiveRepairHold is not null)
        {
            return false;
        }

        if (completion.PendingTransition is not { } pending)
        {
            return true;
        }

        return IsExactPendingMapping(pending) &&
            completion.State.TryGetLog(pending.LogId, out var retainedOwner) &&
            retainedOwner.State == pending.FromState &&
            !IsTargetOccupied(completion.State, pending.ToState);
    }

    private static bool HasValidCurrentShape(ShiftRuntimeState state, PendingLineTransitionDescriptor pending)
    {
        var sourceNode = LogStateNodes.GetNode(pending.FromState);
        return sourceNode is not null &&
            state.GetNodeOccupancy(sourceNode.Value) == 1 &&
            state.ActiveIntakeDeadline is null &&
            (state.ActiveConfirmationTest is null || state.ActiveConfirmationTest.LogId != pending.LogId);
    }

    private static bool IsExactPendingMapping(PendingLineTransitionDescriptor pending)
    {
        if (pending.LogId.IsDefault || !Enum.IsDefined(pending.Cause) || !Enum.IsDefined(pending.FromState) || !Enum.IsDefined(pending.ToState))
        {
            return false;
        }

        return pending.Cause switch
        {
            JamCause.FEED_GATE_BLOCKED => pending.FromState == LogState.AT_FEED_GATE && pending.ToState == LogState.AT_INTAKE,
            JamCause.INTAKE_AUTOFEED_BLOCKED => pending.FromState == LogState.AT_INTAKE && pending.ToState == LogState.QUEUED_FOR_SAW,
            _ => false
        };
    }

    private static bool IsTargetOccupied(ShiftRuntimeState state, LogState target)
    {
        var targetNode = LogStateNodes.GetNode(target);
        return targetNode is null || state.GetNodeOccupancy(targetNode.Value) != 0;
    }

    private static RepairPendingTransitionFollowUp GetFollowUp(JamCause cause) => cause switch
    {
        JamCause.FEED_GATE_BLOCKED => RepairPendingTransitionFollowUp.IntakeDeadlineStartRequired,
        JamCause.INTAKE_AUTOFEED_BLOCKED => RepairPendingTransitionFollowUp.NormalFeedPlanningEvaluationRequired,
        _ => throw new ArgumentOutOfRangeException(nameof(cause))
    };

    private static bool MatchesAcceptedTransition(
        ShiftRuntimeState suppliedState,
        PendingLineTransitionDescriptor pending,
        HostLogTransitionAccepted accepted)
    {
        var descriptor = accepted.Descriptor;
        if (descriptor.LogId != pending.LogId ||
            descriptor.FromState != pending.FromState ||
            descriptor.ToState != pending.ToState ||
            descriptor.PriorStateVersion != suppliedState.StateVersion ||
            descriptor.CurrentStateVersion != suppliedState.StateVersion.Next() ||
            accepted.State.StateVersion != descriptor.CurrentStateVersion ||
            !ReferenceEquals(accepted.State.Line, suppliedState.Line) ||
            accepted.State.Line.State != LineState.LINE_CLEAR ||
            accepted.State.Line.ActiveRepairHold is not null ||
            accepted.State.ActiveIntakeDeadline is not null)
        {
            return false;
        }

        if (accepted.State.ShiftId != suppliedState.ShiftId ||
            accepted.State.ShiftSeed != suppliedState.ShiftSeed ||
            !accepted.State.ProcessedIntentIds.SetEquals(suppliedState.ProcessedIntentIds) ||
            !ReferenceEquals(accepted.State.PendingFeed, suppliedState.PendingFeed) ||
            !ReferenceEquals(accepted.State.Inventory, suppliedState.Inventory) ||
            !ReferenceEquals(accepted.State.ProcedureProgressByLog, suppliedState.ProcedureProgressByLog) ||
            !ReferenceEquals(accepted.State.ActiveProcedureHold, suppliedState.ActiveProcedureHold) ||
            !ReferenceEquals(accepted.State.ActiveConfirmationTest, suppliedState.ActiveConfirmationTest) ||
            !ReferenceEquals(accepted.State.ConfirmationResultsByLog, suppliedState.ConfirmationResultsByLog) ||
            !ReferenceEquals(accepted.State.Containment, suppliedState.Containment) ||
            !ReferenceEquals(accepted.State.ActiveContainmentRitual, suppliedState.ActiveContainmentRitual) ||
            accepted.State.Logs.Length != suppliedState.Logs.Length)
        {
            return false;
        }

        for (var index = 0; index < suppliedState.Logs.Length; index++)
        {
            var before = suppliedState.Logs[index];
            var after = accepted.State.Logs[index];
            if (before.LogId != after.LogId || before.TrueSpecies != after.TrueSpecies || before.DeclaredSpecies != after.DeclaredSpecies || before.Anomaly != after.Anomaly || !before.Flags.SetEquals(after.Flags))
            {
                return false;
            }

            if (before.LogId == pending.LogId)
            {
                if (before.State != pending.FromState || after.State != pending.ToState)
                {
                    return false;
                }
            }
            else if (before.State != after.State)
            {
                return false;
            }
        }

        return true;
    }
}
