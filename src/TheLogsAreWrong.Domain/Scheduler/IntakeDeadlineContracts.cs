using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Scheduler;

public sealed record ActiveIntakeDeadline
{
    public ActiveIntakeDeadline(LogId logId, ServerTick startedAt, SimulationDuration duration)
    {
        if (logId.IsDefault || startedAt.IsDefault || duration.IsDefault || duration <= SimulationDuration.Zero)
        {
            throw new ArgumentException("An active intake deadline requires initialized identity and timing with a positive duration.");
        }

        LogId = logId;
        StartedAt = startedAt;
        Duration = duration;
        DueAt = startedAt + duration;
    }

    public LogId LogId { get; }
    public ServerTick StartedAt { get; }
    public SimulationDuration Duration { get; }
    public ServerTick DueAt { get; }
}

public abstract record IntakeDeadlineStartResult(ShiftRuntimeState State);

public sealed record IntakeDeadlineStarted(
    ShiftRuntimeState State,
    ActiveIntakeDeadline Deadline,
    StateVersion PriorStateVersion,
    StateVersion CurrentStateVersion) : IntakeDeadlineStartResult(State);

public sealed record IntakeDeadlineAlreadyActive(ShiftRuntimeState State, ActiveIntakeDeadline Deadline) : IntakeDeadlineStartResult(State);

public sealed class IntakeDeadlineStartService
{
    public IntakeDeadlineStartResult Start(ShiftRuntimeState state, FeedDueResolved admission, ShiftProfile selectedProfile)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(selectedProfile);
        if (selectedProfile.IntakeTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedProfile), "Selected profile intake timeout must be positive.");
        }

        if (admission.Disposition != FeedDueDisposition.AdmittedToIntake || admission.FollowUpRequirement != FeedDueFollowUpRequirement.IntakeDeadlineStartRequired || admission.CurrentStateVersion != admission.PriorStateVersion.Next())
        {
            throw new ArgumentException("Only a valid admitted-to-intake deadline-start descriptor is accepted.", nameof(admission));
        }

        return StartFromValidatedAdmission(
            state,
            admission.State,
            admission.ConsumedSchedule.LogId,
            admission.ResolvedAt,
            admission.CurrentStateVersion,
            selectedProfile);
    }

    internal static IntakeDeadlineStartResult StartFromRepairedAdmission(
        ShiftRuntimeState state,
        RepairPendingTransitionExecuted repairedAdmission,
        ShiftProfile selectedProfile)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(repairedAdmission);
        ArgumentNullException.ThrowIfNull(selectedProfile);
        ValidateRepairedFeedGateAdmission(repairedAdmission);

        return StartFromValidatedAdmission(
            state,
            repairedAdmission.State,
            repairedAdmission.LogId,
            repairedAdmission.AppliedAt,
            repairedAdmission.CurrentStateVersion,
            selectedProfile);
    }

    private static IntakeDeadlineStartResult StartFromValidatedAdmission(
        ShiftRuntimeState state,
        ShiftRuntimeState admittedState,
        LogId owner,
        ServerTick startedAt,
        StateVersion acceptedStateVersion,
        ShiftProfile selectedProfile)
    {
        if (selectedProfile.IntakeTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedProfile), "Selected profile intake timeout must be positive.");
        }

        var expected = new ActiveIntakeDeadline(owner, startedAt, SimulationDuration.FromTicks(selectedProfile.IntakeTimeoutSeconds));
        if (state.ActiveIntakeDeadline is { } active)
        {
            if (active != expected)
            {
                throw new InvalidOperationException("A contradictory active intake deadline cannot be repaired silently.");
            }

            if (!state.TryGetLog(active.LogId, out var activeLog) || activeLog.State != LogState.AT_INTAKE)
            {
                throw new InvalidOperationException("An active intake deadline must own a log at intake.");
            }

            return new IntakeDeadlineAlreadyActive(state, active);
        }

        if (!ReferenceEquals(state, admittedState) || state.StateVersion != acceptedStateVersion)
        {
            throw new ArgumentException("A new intake deadline must start from the exact admitted runtime state.", nameof(state));
        }

        if (!state.TryGetLog(expected.LogId, out var log) || log.State != LogState.AT_INTAKE)
        {
            throw new InvalidOperationException("The admitted deadline owner must be at intake.");
        }

        var after = state.WithActiveIntakeDeadline(expected);
        return new IntakeDeadlineStarted(after, expected, state.StateVersion, after.StateVersion);
    }

    private static void ValidateRepairedFeedGateAdmission(RepairPendingTransitionExecuted repairedAdmission)
    {
        if (repairedAdmission.State is null ||
            repairedAdmission.PendingTransition is not { } pending ||
            repairedAdmission.AppliedAt.IsDefault ||
            pending.LogId.IsDefault ||
            pending.Cause != JamCause.FEED_GATE_BLOCKED ||
            pending.FromState != LogState.AT_FEED_GATE ||
            pending.ToState != LogState.AT_INTAKE ||
            repairedAdmission.LogId != pending.LogId ||
            repairedAdmission.Cause != pending.Cause ||
            repairedAdmission.Source != pending.FromState ||
            repairedAdmission.Destination != pending.ToState ||
            repairedAdmission.FollowUpRequirement != RepairPendingTransitionFollowUp.IntakeDeadlineStartRequired ||
            repairedAdmission.PriorStateVersion.IsDefault ||
            repairedAdmission.CurrentStateVersion.IsDefault ||
            !repairedAdmission.PriorStateVersion.TryNext(out var expectedCurrentVersion) ||
            repairedAdmission.CurrentStateVersion != expectedCurrentVersion ||
            repairedAdmission.State.StateVersion != repairedAdmission.CurrentStateVersion)
        {
            throw new ArgumentException("Only an exact accepted repaired feed-gate admission can start an intake deadline.", nameof(repairedAdmission));
        }

        if (!repairedAdmission.State.TryGetLog(repairedAdmission.LogId, out var owner))
        {
            throw new InvalidOperationException("A repaired admission deadline owner must resolve in the retained result state.");
        }

        if (owner.State != LogState.AT_INTAKE)
        {
            throw new InvalidOperationException("A repaired admission deadline owner must be at intake.");
        }

        var line = repairedAdmission.State.Line;
        if (line is null ||
            line.State != LineState.LINE_CLEAR ||
            line.Cause is not null ||
            line.PendingLogId is not null ||
            line.ActiveRepairHold is not null)
        {
            throw new InvalidOperationException("A repaired admission deadline requires an exactly clear retained line.");
        }

        if (repairedAdmission.State.ActiveIntakeDeadline is not null)
        {
            throw new InvalidOperationException("A repaired admission result cannot already contain an active intake deadline.");
        }
    }
}

public sealed record DefaultAutoRouteRequired
{
    public DefaultAutoRouteRequired(LogId logId, ServerTick dueAt)
    {
        if (logId.IsDefault || dueAt.IsDefault)
        {
            throw new ArgumentException("A default auto-route requirement requires initialized identity and due tick.");
        }

        LogId = logId;
        DueAt = dueAt;
    }

    public LogId LogId { get; }
    public ServerTick DueAt { get; }
}

public abstract record IntakeDeadlineExpirationResult(ShiftRuntimeState State);
public sealed record IntakeDeadlineNoActiveDeadline(ShiftRuntimeState State) : IntakeDeadlineExpirationResult(State);
public sealed record IntakeDeadlineNotDueYet(ShiftRuntimeState State) : IntakeDeadlineExpirationResult(State);
public sealed record IntakeDeadlineExpired(
    ShiftRuntimeState State,
    ActiveIntakeDeadline ExpiredDeadline,
    ServerTick ExpiredAt,
    DefaultAutoRouteRequired FollowUp,
    StateVersion PriorStateVersion,
    StateVersion CurrentStateVersion) : IntakeDeadlineExpirationResult(State);

public sealed class IntakeDeadlineExpirationService
{
    public IntakeDeadlineExpirationResult Expire(ShiftRuntimeState state, ServerTick currentTick)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (currentTick.IsDefault)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Current intake-deadline tick must be initialized.");
        }

        if (state.ActiveIntakeDeadline is not { } deadline)
        {
            return new IntakeDeadlineNoActiveDeadline(state);
        }

        if (currentTick < deadline.DueAt)
        {
            return new IntakeDeadlineNotDueYet(state);
        }

        if (!state.TryGetLog(deadline.LogId, out var log) || log.State != LogState.AT_INTAKE)
        {
            throw new InvalidOperationException("An active intake deadline owner must remain at intake until it is cleared.");
        }

        var after = state.ClearActiveIntakeDeadline();
        return new IntakeDeadlineExpired(after, deadline, currentTick, new DefaultAutoRouteRequired(deadline.LogId, deadline.DueAt), state.StateVersion, after.StateVersion);
    }
}
