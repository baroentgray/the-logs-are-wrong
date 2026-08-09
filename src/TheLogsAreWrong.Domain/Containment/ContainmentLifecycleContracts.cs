using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Containment;

public sealed record ContainmentRuntimeState
{
    public ContainmentRuntimeState(ContainmentState state, ServerTick enteredAt, ServerTick? deadlineAt)
    {
        if (!Enum.IsDefined(state) || enteredAt.IsDefault)
        {
            throw new ArgumentException("Containment state and entered tick must be initialized.");
        }

        if (state == ContainmentState.INCIDENT && deadlineAt is not null)
        {
            throw new ArgumentException("Incident containment cannot have a deadline.", nameof(deadlineAt));
        }

        if (state is ContainmentState.SERVICE_REQUESTED or ContainmentState.OVERDUE && deadlineAt is null)
        {
            throw new ArgumentException("Active containment requests require a deadline.", nameof(deadlineAt));
        }

        if (deadlineAt is { } deadline && (deadline.IsDefault || deadline <= enteredAt))
        {
            throw new ArgumentException("Containment deadline must be initialized and later than entry.", nameof(deadlineAt));
        }

        State = state;
        EnteredAt = enteredAt;
        DeadlineAt = deadlineAt;
    }

    public ContainmentState State { get; }
    public ServerTick EnteredAt { get; }
    public ServerTick? DeadlineAt { get; }
}

public sealed record ActiveContainmentRitual
{
    public ActiveContainmentRitual(ServerTick startedAt, ServerTick dueAt, SimulationDuration duration)
    {
        if (startedAt.IsDefault || dueAt.IsDefault || duration.IsDefault || duration <= SimulationDuration.Zero)
        {
            throw new ArgumentException("Containment ritual timing must be initialized and positive.");
        }

        if (dueAt != startedAt + duration)
        {
            throw new ArgumentException("Containment ritual due tick must equal start tick plus duration.", nameof(dueAt));
        }

        StartedAt = startedAt;
        DueAt = dueAt;
        Duration = duration;
    }

    public ServerTick StartedAt { get; }
    public ServerTick DueAt { get; }
    public SimulationDuration Duration { get; }
}

public sealed record ContainmentIncidentDescriptor
{
    public ContainmentIncidentDescriptor(string type, SimulationDuration duration, ServerTick triggeredAt)
    {
        if (string.IsNullOrWhiteSpace(type) || duration.IsDefault || duration <= SimulationDuration.Zero || triggeredAt.IsDefault)
        {
            throw new ArgumentException("Containment incident descriptor must be initialized and positive.");
        }

        Type = type;
        Duration = duration;
        TriggeredAt = triggeredAt;
    }

    public string Type { get; }
    public SimulationDuration Duration { get; }
    public ServerTick TriggeredAt { get; }
}

public static class DangerWeightResolver
{
    public static int Resolve(ShiftRuntimeState state, AnomalyCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(catalog.Definitions);

        var total = 0;
        foreach (var log in state.Logs)
        {
            if (log.State != LogState.HELD_WRITTEN_OFF || log.Anomaly is not { } anomalyId)
            {
                continue;
            }

            if (anomalyId.IsDefault || !catalog.Definitions.TryGetValue(anomalyId, out var definition) || definition is null || definition.Id.IsDefault || definition.Id != anomalyId || definition.DangerWeight < 0)
            {
                throw new InvalidOperationException("Written-off anomaly must resolve to an exact non-negative catalog definition.");
            }

            total = checked(total + definition.DangerWeight);
        }

        return total;
    }
}

public abstract record ContainmentAdvanceResult(ShiftRuntimeState State);
public sealed record ContainmentAdvanceNoChange(ShiftRuntimeState State) : ContainmentAdvanceResult(State);
public sealed record ContainmentStableIntervalArmed(ShiftRuntimeState State) : ContainmentAdvanceResult(State);
public sealed record ContainmentStateAdvanced(ShiftRuntimeState State) : ContainmentAdvanceResult(State);
public sealed record ContainmentIncidentEntered(ShiftRuntimeState State, ContainmentIncidentDescriptor Descriptor) : ContainmentAdvanceResult(State);
public sealed record ContainmentRitualCompletionRequired(ShiftRuntimeState State) : ContainmentAdvanceResult(State);
public sealed record ContainmentAdvanceFailed(ShiftRuntimeState State, ContainmentAdvanceFailureReason Reason) : ContainmentAdvanceResult(State);

public enum ContainmentAdvanceFailureReason
{
    RitualStateInvalid
}

public sealed class ContainmentAdvanceService
{
    public ContainmentAdvanceResult Advance(ShiftRuntimeState state, ServerTick currentTick, ContainmentConfiguration configuration, AnomalyCatalog catalog)
    {
        ContainmentGuards.Validate(state, currentTick, configuration, catalog);
        var containment = state.Containment;

        if (state.ActiveContainmentRitual is { } ritual)
        {
            if (currentTick < ritual.StartedAt)
            {
                throw new ArgumentOutOfRangeException(nameof(currentTick), "Current tick cannot precede an active containment ritual.");
            }

            if (currentTick >= ritual.DueAt)
            {
                return new ContainmentRitualCompletionRequired(state);
            }

            if (containment.State == ContainmentState.STABLE)
            {
                return new ContainmentAdvanceFailed(state, ContainmentAdvanceFailureReason.RitualStateInvalid);
            }
        }

        var dangerWeight = DangerWeightResolver.Resolve(state, catalog);
        if (containment.State == ContainmentState.STABLE && containment.DeadlineAt is null)
        {
            if (dangerWeight == 0)
            {
                return new ContainmentAdvanceNoChange(state);
            }

            var armed = new ContainmentRuntimeState(
                ContainmentState.STABLE,
                currentTick,
                currentTick + ContainmentIntervals.StableInterval(configuration, dangerWeight));
            return new ContainmentStableIntervalArmed(state.WithContainment(armed, state.ActiveContainmentRitual));
        }

        if (containment.State == ContainmentState.INCIDENT || currentTick < containment.DeadlineAt!.Value)
        {
            return new ContainmentAdvanceNoChange(state);
        }

        return containment.State switch
        {
            ContainmentState.STABLE => new ContainmentStateAdvanced(state.WithContainment(
                new ContainmentRuntimeState(
                    ContainmentState.SERVICE_REQUESTED,
                    containment.DeadlineAt.Value,
                    containment.DeadlineAt.Value + ContainmentIntervals.Positive(configuration.ServiceRequestedGraceSeconds, nameof(configuration.ServiceRequestedGraceSeconds))),
                state.ActiveContainmentRitual)),
            ContainmentState.SERVICE_REQUESTED => new ContainmentStateAdvanced(state.WithContainment(
                new ContainmentRuntimeState(
                    ContainmentState.OVERDUE,
                    containment.DeadlineAt.Value,
                    containment.DeadlineAt.Value + ContainmentIntervals.Positive(configuration.OverdueSeconds, nameof(configuration.OverdueSeconds))),
                state.ActiveContainmentRitual)),
            ContainmentState.OVERDUE => EnterIncident(state, containment, configuration),
            _ => throw new InvalidOperationException("Containment state cannot advance.")
        };
    }

    private static ContainmentIncidentEntered EnterIncident(ShiftRuntimeState state, ContainmentRuntimeState containment, ContainmentConfiguration configuration)
    {
        var incident = configuration.PrototypeIncident ?? throw new ArgumentException("Containment incident configuration is required.", nameof(configuration));
        var descriptor = new ContainmentIncidentDescriptor(
            incident.Type,
            ContainmentIntervals.Positive(incident.DurationSeconds, nameof(incident.DurationSeconds)),
            containment.DeadlineAt!.Value);
        var next = new ContainmentRuntimeState(ContainmentState.INCIDENT, containment.DeadlineAt.Value, null);
        return new ContainmentIncidentEntered(state.WithContainment(next, state.ActiveContainmentRitual), descriptor);
    }
}

public enum ContainmentRitualStartRejectionReason
{
    NO_ACTIVE_REQUEST,
    RITUAL_ALREADY_ACTIVE
}

public abstract record ContainmentRitualStartResult(ShiftRuntimeState State);
public sealed record ContainmentRitualStarted(ShiftRuntimeState State, ActiveContainmentRitual Ritual) : ContainmentRitualStartResult(State);
public sealed record ContainmentRitualStartRejected(ShiftRuntimeState State, ContainmentRitualStartRejectionReason Reason) : ContainmentRitualStartResult(State);

public sealed class ContainmentRitualStartService
{
    public ContainmentRitualStartResult Start(ShiftRuntimeState state, ServerTick currentTick, ContainmentConfiguration configuration)
        => StartCore(state, currentTick, configuration, null);

    /// <summary>Starts a ritual for one already-authoritative accepted intent, atomically retaining its exact identity.</summary>
    internal ContainmentRitualStartResult StartForAuthoritativeIntent(
        ShiftRuntimeState state,
        ServerTick currentTick,
        ContainmentConfiguration configuration,
        IntentId processedIntentId)
    {
        if (processedIntentId.IsDefault)
        {
            throw new ArgumentException("Processed intent identifier must be initialized.", nameof(processedIntentId));
        }

        return StartCore(state, currentTick, configuration, processedIntentId);
    }

    private static ContainmentRitualStartResult StartCore(
        ShiftRuntimeState state,
        ServerTick currentTick,
        ContainmentConfiguration configuration,
        IntentId? processedIntentId)
    {
        ContainmentGuards.Validate(state, currentTick, configuration);
        if (state.ActiveContainmentRitual is not null)
        {
            return new ContainmentRitualStartRejected(state, ContainmentRitualStartRejectionReason.RITUAL_ALREADY_ACTIVE);
        }

        if (state.Containment.State == ContainmentState.STABLE)
        {
            return new ContainmentRitualStartRejected(state, ContainmentRitualStartRejectionReason.NO_ACTIVE_REQUEST);
        }

        var duration = ContainmentIntervals.Positive(configuration.RitualHoldSeconds, nameof(configuration.RitualHoldSeconds));
        var ritual = new ActiveContainmentRitual(currentTick, currentTick + duration, duration);
        return new ContainmentRitualStarted(state.WithContainmentAndProcessedIntent(state.Containment, ritual, processedIntentId), ritual);
    }
}

public abstract record ContainmentRitualCompletionResult(ShiftRuntimeState State);
public sealed record ContainmentRitualNoActive(ShiftRuntimeState State) : ContainmentRitualCompletionResult(State);
public sealed record ContainmentRitualNotDue(ShiftRuntimeState State) : ContainmentRitualCompletionResult(State);
public sealed record ContainmentRitualCompleted(ShiftRuntimeState State) : ContainmentRitualCompletionResult(State);
public sealed record ContainmentRitualCompletionFailed(ShiftRuntimeState State, ContainmentRitualCompletionFailureReason Reason) : ContainmentRitualCompletionResult(State);

public enum ContainmentRitualCompletionFailureReason
{
    RitualStateInvalid
}

public sealed class ContainmentRitualCompletionService
{
    public ContainmentRitualCompletionResult CompleteDue(ShiftRuntimeState state, ServerTick currentTick, ContainmentConfiguration configuration, AnomalyCatalog catalog)
    {
        ContainmentGuards.Validate(state, currentTick, configuration, catalog);
        if (state.ActiveContainmentRitual is not { } ritual)
        {
            return new ContainmentRitualNoActive(state);
        }

        if (currentTick < ritual.StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Current tick cannot precede an active containment ritual.");
        }

        if (currentTick < ritual.DueAt)
        {
            return new ContainmentRitualNotDue(state);
        }

        if (state.Containment.State == ContainmentState.STABLE)
        {
            return new ContainmentRitualCompletionFailed(state, ContainmentRitualCompletionFailureReason.RitualStateInvalid);
        }

        var dangerWeight = DangerWeightResolver.Resolve(state, catalog);
        ServerTick? deadline = dangerWeight == 0 ? null : currentTick + ContainmentIntervals.StableInterval(configuration, dangerWeight);
        var stable = new ContainmentRuntimeState(ContainmentState.STABLE, currentTick, deadline);
        return new ContainmentRitualCompleted(state.WithContainment(stable, null));
    }
}

public abstract record ContainmentRitualCancellationResult(ShiftRuntimeState State);
public sealed record ContainmentRitualCancelled(ShiftRuntimeState State) : ContainmentRitualCancellationResult(State);
public sealed record ContainmentRitualCancellationRejected(ShiftRuntimeState State) : ContainmentRitualCancellationResult(State);

public sealed class ContainmentRitualCancellationService
{
    public ContainmentRitualCancellationResult Cancel(ShiftRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ActiveContainmentRitual is null)
        {
            return new ContainmentRitualCancellationRejected(state);
        }

        return new ContainmentRitualCancelled(state.WithContainment(state.Containment, null));
    }
}

internal static class ContainmentGuards
{
    internal static void Validate(ShiftRuntimeState state, ServerTick currentTick, ContainmentConfiguration configuration, AnomalyCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(configuration);
        if (currentTick.IsDefault || currentTick < state.Containment.EnteredAt)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Current tick cannot precede containment state entry.");
        }

        if (catalog is not null)
        {
            ArgumentNullException.ThrowIfNull(catalog.Definitions);
        }
    }
}

internal static class ContainmentIntervals
{
    internal static SimulationDuration StableInterval(ContainmentConfiguration configuration, int dangerWeight)
    {
        if (dangerWeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dangerWeight), "Containment interval requires positive danger weight.");
        }

        ArgumentNullException.ThrowIfNull(configuration.IntervalByDangerWeight);
        var key = dangerWeight == 1 ? "1" : dangerWeight == 2 ? "2" : "3_or_more";
        if (!configuration.IntervalByDangerWeight.TryGetValue(key, out var seconds))
        {
            throw new ArgumentException("Containment configuration is missing a required danger interval.", nameof(configuration));
        }

        return Positive(seconds, key);
    }

    internal static SimulationDuration Positive(int ticks, string parameterName) => ticks > 0
        ? SimulationDuration.FromTicks(ticks)
        : throw new ArgumentOutOfRangeException(parameterName, "Containment timing must be positive.");
}
