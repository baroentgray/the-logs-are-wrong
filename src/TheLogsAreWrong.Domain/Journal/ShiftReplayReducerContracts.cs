using System.Collections.Immutable;
using System.Globalization;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Journal;

/// <summary>
/// The closed semantic-reduction failure taxonomy. It is deliberately separate from <see cref="ReplayAnomaly"/>:
/// ordering remains the existing validator's authority and is never expressed here.
/// </summary>
public enum ShiftReplaySemanticFailure
{
    UnknownEventType,
    PayloadTypeMismatch,
    ShiftMismatch,
    UnknownLog,
    StateVersionMismatch,
    ObservationalVersionMismatch,
    ContradictoryState,
    QuotaSettlementMismatch,
    CausationMismatch
}

public abstract record ShiftReplayResult;

public sealed record ShiftReplaySucceeded(ShiftSnapshot Snapshot) : ShiftReplayResult;

/// <summary>The existing <see cref="ReplayValidator"/> rejected journal ordering before any semantic reduction ran.</summary>
public sealed record ShiftReplayOrderingRejected(ReplayAnomaly Anomaly, int Position) : ShiftReplayResult;

/// <summary>Ordering was valid but an event contradicted the reconstructed state; no partial snapshot is published.</summary>
public sealed record ShiftReplaySemanticRejected(ShiftReplaySemanticFailure Failure, int Position, EventTypeId EventType, string Detail) : ShiftReplayResult;

/// <summary>
/// Deterministic snapshot reconstruction from the frozen stage-7 journal.
/// <para>
/// Ordering is always validated first by the existing <see cref="ReplayValidator"/> from the snapshot's own
/// <see cref="SnapshotBoundary"/>; only a fully valid tail is reduced. Reduction then reads the merged stage-7 payloads
/// as the sole authority: it never re-runs an intent, a host stage, anomaly resolution, quota resolution or a procedure,
/// never sorts, and never infers causation from ordering. Configuration supplies only static frozen facts such as the
/// manifest and the mechanical movement-noise duration.
/// </para>
/// </summary>
public sealed class ShiftReplayService
{
    private readonly ReplayValidator _validator = new();
    private readonly ShiftSnapshotCaptureService _capture = new();

    /// <summary>Replays the exact journal tail whose sequence is greater than <paramref name="snapshot"/>'s boundary.</summary>
    public ShiftReplayResult ReplayFrom(ShiftSnapshot snapshot, IReadOnlyList<EventEnvelope> tail, ShiftConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(tail);
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.ShiftId != snapshot.ShiftId)
        {
            return new ShiftReplaySemanticRejected(ShiftReplaySemanticFailure.ShiftMismatch, -1, default, "configuration shift does not match the snapshot shift");
        }

        // Ordering is the existing validator's authority and always runs before any reduction.
        var ordering = _validator.Validate(snapshot.Boundary, tail);
        if (!ordering.IsValid)
        {
            return new ShiftReplayOrderingRejected(ordering.Anomaly!.Value, ordering.Position!.Value);
        }

        var state = ReductionState.From(snapshot, configuration);
        for (var position = 0; position < tail.Count; position++)
        {
            var failure = Reduce(state, tail[position], position);
            if (failure is not null)
            {
                return failure;
            }
        }

        return new ShiftReplaySucceeded(state.ToSnapshot());
    }

    /// <summary>Full replay from the pristine initial manifest snapshot and the complete ordered journal.</summary>
    public ShiftReplayResult ReplayAll(ShiftConfiguration configuration, ProfileId selectedProfileId, IReadOnlyList<EventEnvelope> events)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(events);
        return ReplayFrom(_capture.CreateInitial(configuration, selectedProfileId), events, configuration);
    }

    private static ShiftReplayResult? Reduce(ReductionState state, EventEnvelope envelope, int position)
    {
        if (envelope is null || envelope.Payload is null || envelope.EventType.IsDefault)
        {
            return Fail(ShiftReplaySemanticFailure.PayloadTypeMismatch, position, default, "envelope or payload is missing");
        }

        if (envelope.ShiftId != state.ShiftId)
        {
            return Fail(ShiftReplaySemanticFailure.ShiftMismatch, position, envelope.EventType, $"event shift {envelope.ShiftId}");
        }

        if (!ReplayEventCatalog.TryResolve(envelope.EventType, out var kind))
        {
            return Fail(ShiftReplaySemanticFailure.UnknownEventType, position, envelope.EventType, "event type is outside the frozen stage-seven catalog");
        }

        var context = new ReductionContext(state, envelope, position);
        return kind switch
        {
            ReplayEventKind.FeedScheduled => ReduceFeedScheduled(context),
            ReplayEventKind.EarlyFeedRequested => ReduceEarlyFeedRequested(context),
            ReplayEventKind.LogPlacedAtFeedGate => ReduceLogPlacedAtFeedGate(context),
            ReplayEventKind.LogAdmittedToIntake => ReduceLogAdmittedToIntake(context),
            ReplayEventKind.IntakeDeadlineStarted => ReduceIntakeDeadlineStarted(context),
            ReplayEventKind.IntakeDeadlineExpired => ReduceIntakeDeadlineExpired(context),
            ReplayEventKind.AutoRouteAttempted => ReduceAutoRouteAttempted(context),
            ReplayEventKind.LineJammed => ReduceLineJammed(context),
            ReplayEventKind.RepairStarted => ReduceRepairStarted(context),
            ReplayEventKind.RepairCompleted => ReduceRepairCompleted(context),
            ReplayEventKind.SawCycleStarted => ReduceSawCycleStarted(context),
            ReplayEventKind.SawCycleCompleted => ReduceSawCycleCompleted(context),
            ReplayEventKind.LineNoiseChanged => ReduceLineNoiseChanged(context),
            ReplayEventKind.LogRouted => ReduceLogTransition(context),
            ReplayEventKind.LogWrittenOff => ReduceLogTransition(context),
            ReplayEventKind.ProcedureActionStarted => ReduceProcedureActionStarted(context),
            ReplayEventKind.ProcedureActionCompleted => ReduceProcedureActionCompleted(context),
            ReplayEventKind.ConfirmationTestStarted => ReduceConfirmationTestStarted(context),
            ReplayEventKind.ConfirmationTestCompleted => ReduceConfirmationTestCompleted(context),
            ReplayEventKind.ContainmentRitualStarted => ReduceContainmentRitualStarted(context),
            ReplayEventKind.ContainmentRitualCompleted => ReduceContainmentRitualCompleted(context),
            ReplayEventKind.ContainmentStateChanged => ReduceContainmentStateChanged(context),
            ReplayEventKind.ConfirmationConditionUpdated => ReduceConfirmationConditionUpdated(context),
            ReplayEventKind.ShiftCompleted => ReduceShiftCompleted(context),
            _ => Fail(ShiftReplaySemanticFailure.UnknownEventType, position, envelope.EventType, "unmapped catalog entry")
        };
    }

    // ----- individual reductions -----

    private static ShiftReplayResult? ReduceFeedScheduled(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenFeedSchedulePayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenFeedSchedulePayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (!context.State.HasLog(payload.LogId))
        {
            return context.UnknownLog(payload.LogId);
        }

        if (context.State.PendingFeed is not null)
        {
            return context.Contradiction("a pending feed already exists");
        }

        if (payload.DueAt != payload.ScheduledAt + payload.Delay)
        {
            return context.Contradiction("feed schedule due tick does not equal scheduled tick plus delay");
        }

        context.State.PendingFeed = new SnapshotPendingFeed(payload.LogId, payload.Kind, payload.ScheduledAt, payload.Delay, payload.CausedByIntentId);
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceEarlyFeedRequested(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenFeedSchedulePayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenFeedSchedulePayload));
        }

        if (context.RequireObservation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        context.CompleteObservation();
        return null;
    }

    private static ShiftReplayResult? ReduceLogPlacedAtFeedGate(ReductionContext context) => ReduceFeedAdmission(context, LogState.AT_FEED_GATE);

    private static ShiftReplayResult? ReduceLogAdmittedToIntake(ReductionContext context) => ReduceFeedAdmission(context, LogState.AT_INTAKE);

    /// <summary>
    /// Admission is source-state sensitive: a feed-due admission consumes the pending feed and starts from
    /// <c>SCHEDULED</c>, while a repaired feed-gate transition starts from <c>AT_FEED_GATE</c> and consumes nothing.
    /// </summary>
    private static ShiftReplayResult? ReduceFeedAdmission(ReductionContext context, LogState destination)
    {
        if (context.Envelope.Payload is not HostStageSevenLogTransitionPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenLogTransitionPayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (payload.ToState != destination)
        {
            return context.Contradiction($"transition destination {payload.ToState} does not match {destination}");
        }

        if (!context.State.TryGetLog(payload.LogId, out var log))
        {
            return context.UnknownLog(payload.LogId);
        }

        if (log.State != payload.FromState)
        {
            return context.Contradiction($"log {payload.LogId} is {log.State} but the event reports {payload.FromState}");
        }

        if (payload.FromState == LogState.SCHEDULED)
        {
            if (context.State.PendingFeed is not { } pending || pending.LogId != payload.LogId)
            {
                return context.Contradiction("a scheduled admission requires the matching pending feed");
            }

            context.State.PendingFeed = null;
        }

        context.State.SetLogState(payload.LogId, payload.ToState);
        context.State.ApplyMovement(
            payload.FromState == LogState.SCHEDULED ? MovementNoiseAcceptedSource.FeedDueResolved : MovementNoiseAcceptedSource.RepairPendingTransition,
            payload,
            context.Envelope.ServerTick);
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceIntakeDeadlineStarted(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenIntakeDeadlinePayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenIntakeDeadlinePayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (!context.State.TryGetLog(payload.LogId, out var log) )
        {
            return context.UnknownLog(payload.LogId);
        }

        if (log.State != LogState.AT_INTAKE)
        {
            return context.Contradiction($"an intake deadline requires its owner at intake but {payload.LogId} is {log.State}");
        }

        if (context.State.ActiveIntakeDeadline is not null)
        {
            return context.Contradiction("only one active intake deadline is permitted");
        }

        context.State.ActiveIntakeDeadline = new SnapshotIntakeDeadline(payload.LogId, payload.StartedAt, payload.Duration);
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceIntakeDeadlineExpired(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenIntakeDeadlinePayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenIntakeDeadlinePayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (context.State.ActiveIntakeDeadline is not { } deadline || deadline.LogId != payload.LogId)
        {
            return context.Contradiction("expiration requires the matching active intake deadline");
        }

        context.State.ActiveIntakeDeadline = null;
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceAutoRouteAttempted(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenAutoRoutePayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenAutoRoutePayload));
        }

        if (payload.Outcome != HostStageSevenAutoRouteOutcome.Applied)
        {
            if (context.RequireObservation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } observationFailure)
            {
                return observationFailure;
            }

            context.CompleteObservation();
            return null;
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (payload.Source is not { } source || payload.Destination is not { } destination)
        {
            return context.Contradiction("an applied auto route requires exact source and destination states");
        }

        if (!context.State.TryGetLog(payload.LogId, out var log))
        {
            return context.UnknownLog(payload.LogId);
        }

        if (log.State != source)
        {
            return context.Contradiction($"log {payload.LogId} is {log.State} but the auto route reports {source}");
        }

        context.State.SetLogState(payload.LogId, destination);
        context.State.ApplyMovement(
            context.State.ConsumeRepairFollowUp(payload.LogId, JamCause.INTAKE_AUTOFEED_BLOCKED)
                ? MovementNoiseAcceptedSource.RepairPendingTransition
                : MovementNoiseAcceptedSource.DefaultIntakeAutoRoute,
            payload.LogId,
            source,
            destination,
            payload.PriorStateVersion,
            payload.CurrentStateVersion,
            context.Envelope.ServerTick);
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceLineJammed(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenLineJamPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenLineJamPayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (context.State.LineStateValue != LineState.LINE_CLEAR)
        {
            return context.Contradiction("a jam requires a clear line");
        }

        if (!context.State.HasLog(payload.LogId))
        {
            return context.UnknownLog(payload.LogId);
        }

        context.State.LineStateValue = LineState.LINE_JAMMED;
        context.State.LineEnteredAt = payload.EnteredAt;
        context.State.LineCause = payload.Cause;
        context.State.LinePendingLogId = payload.LogId;
        context.State.RepairHold = null;
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceRepairStarted(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenRepairStartedPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenRepairStartedPayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (context.State.LineStateValue != LineState.LINE_JAMMED || context.State.LinePendingLogId != payload.PendingLogId || context.State.LineCause != payload.Cause)
        {
            return context.Contradiction("repair start requires the exact matching jammed line");
        }

        context.State.LineStateValue = LineState.REPAIRING;
        context.State.LineEnteredAt = payload.StartedAt;
        context.State.RepairHold = new SnapshotRepairHold(payload.StartedAt, payload.Duration);
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceRepairCompleted(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenRepairPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenRepairPayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (context.State.LineStateValue != LineState.REPAIRING)
        {
            return context.Contradiction("repair completion requires a repairing line");
        }

        var current = payload.CurrentLine;
        context.State.LineStateValue = current.State;
        context.State.LineEnteredAt = current.EnteredAt;
        context.State.LineCause = current.Cause;
        context.State.LinePendingLogId = current.PendingLogId;
        context.State.RepairHold = current.ActiveRepairHold is { } hold ? new SnapshotRepairHold(hold.StartedAt, hold.Duration) : null;

        // The retained pending transition is what stage five executes next; remember it so the following admission or
        // auto route is attributed to the repair rather than to a feed or a default route.
        context.State.RememberRepairFollowUp(payload.PendingTransition);
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceSawCycleStarted(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenSawStartedPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenSawStartedPayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (context.State.ActiveSawCycle is not null)
        {
            return context.Contradiction("only one active saw cycle is permitted");
        }

        if (context.State.ActiveSawFailureWindow is { } failureWindow &&
            new SawFailureWindow(failureWindow.StartedAt, failureWindow.Duration).IsActiveAt(context.Envelope.ServerTick))
        {
            return context.Contradiction("a saw cycle cannot start while the saw failure window is active");
        }

        if (!context.State.TryGetLog(payload.Cycle.LogId, out var log))
        {
            return context.UnknownLog(payload.Cycle.LogId);
        }

        if (log.State != LogState.QUEUED_FOR_SAW)
        {
            return context.Contradiction($"a saw cycle must start from a queued owner but {payload.Cycle.LogId} is {log.State}");
        }

        context.State.SetLogState(payload.Cycle.LogId, LogState.IN_SAW);
        context.State.ActiveSawCycle = new SnapshotSawCycle(payload.Cycle.LogId, payload.Cycle.StartedAt, payload.Cycle.Duration);
        context.State.ApplyMovement(
            MovementNoiseAcceptedSource.SawCycleStarted,
            payload.Cycle.LogId,
            LogState.QUEUED_FOR_SAW,
            LogState.IN_SAW,
            payload.PriorStateVersion,
            payload.CurrentStateVersion,
            context.Envelope.ServerTick);
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceSawCycleCompleted(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenSawCompletedPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenSawCompletedPayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (context.State.ActiveSawCycle is not { } cycle || cycle.LogId != payload.Cycle.LogId)
        {
            return context.Contradiction("saw completion requires the matching active cycle");
        }

        if (!context.State.TryGetLog(payload.Cycle.LogId, out var log) || log.State != LogState.IN_SAW)
        {
            return context.Contradiction("saw completion requires an in-saw owner");
        }

        if (payload.QuotaApplicationLogId != payload.Cycle.LogId || payload.QuotaSettlement.LogId != payload.Cycle.LogId)
        {
            return Fail(ShiftReplaySemanticFailure.QuotaSettlementMismatch, context.Position, context.Envelope.EventType, "quota evidence does not belong to the completed cycle");
        }

        if (payload.Resolution.LogId != payload.Cycle.LogId || payload.Resolution.TerminalState != LogState.PROCESSED ||
            payload.CompletedAt != context.Envelope.ServerTick)
        {
            return context.Contradiction("saw completion resolution or timing does not match the completed cycle");
        }

        context.State.SetLogState(payload.Cycle.LogId, LogState.PROCESSED);
        context.State.ActiveSawCycle = null;
        try
        {
            var window = SawFailureWindowFactory.FromCompletion(log.Anomaly, payload.Resolution, payload.CompletedAt);
            if (window is not null &&
                context.State.ActiveSawFailureWindow is { } existing &&
                new SawFailureWindow(existing.StartedAt, existing.Duration).IsActiveAt(payload.CompletedAt))
            {
                return context.Contradiction("an incorrect Penitent saw completion cannot overlap an active saw failure window");
            }

            context.State.ActiveSawFailureWindow = window is { } value
                ? new SnapshotSawFailureWindow(value.StartedAt, value.Duration)
                : null;
        }
        catch (ArgumentException exception)
        {
            return context.Contradiction(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return context.Contradiction(exception.Message);
        }

        // Quota is applied exactly once from the accepted settlement evidence the host already resolved.
        if (payload.QuotaApplicationOutcome == HostStageSevenSawQuotaOutcome.Accepted)
        {
            if (payload.AcceptedQuotaSettlement is not { } accepted)
            {
                return Fail(ShiftReplaySemanticFailure.QuotaSettlementMismatch, context.Position, context.Envelope.EventType, "an accepted settlement requires its descriptor");
            }

            if (context.State.IsSettled(payload.Cycle.LogId))
            {
                return Fail(ShiftReplaySemanticFailure.QuotaSettlementMismatch, context.Position, context.Envelope.EventType, "an accepted settlement cannot credit an already-settled log");
            }

            if (context.State.TotalCreditedUnits != accepted.PriorTotalCreditedUnits || context.State.CorrectlyProcessedAnomalies != accepted.PriorCorrectAnomalyCount)
            {
                return Fail(ShiftReplaySemanticFailure.QuotaSettlementMismatch, context.Position, context.Envelope.EventType, "settlement prior totals contradict the reconstructed quota");
            }

            if (context.State.ApplySettlement(accepted) is { } quotaFailure)
            {
                return Fail(ShiftReplaySemanticFailure.QuotaSettlementMismatch, context.Position, context.Envelope.EventType, quotaFailure);
            }
        }
        else if (!context.State.IsSettled(payload.Cycle.LogId))
        {
            return Fail(ShiftReplaySemanticFailure.QuotaSettlementMismatch, context.Position, context.Envelope.EventType, "an already-applied settlement requires a settled log");
        }

        context.State.ApplyMovement(
            MovementNoiseAcceptedSource.SawCycleCompleted,
            payload.Cycle.LogId,
            LogState.IN_SAW,
            LogState.PROCESSED,
            payload.PriorStateVersion,
            payload.CurrentStateVersion,
            context.Envelope.ServerTick);
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceLineNoiseChanged(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenLineNoisePayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenLineNoisePayload));
        }

        if (context.RequireObservation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (payload.Change.ChangedAt != context.Envelope.ServerTick)
        {
            return context.Contradiction("line-noise change tick must equal the event tick");
        }

        if (context.State.RecordLineNoiseChange(payload.Change.Current, payload.Change.ChangedAt) is { } contradiction)
        {
            return context.Contradiction(contradiction);
        }

        context.CompleteObservation();
        return null;
    }

    private static ShiftReplayResult? ReduceLogTransition(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenLogTransitionPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenLogTransitionPayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (context.Envelope.CausedByIntentId is null)
        {
            return Fail(ShiftReplaySemanticFailure.CausationMismatch, context.Position, context.Envelope.EventType, "a manual routing event requires its exact causing intent");
        }

        if (!context.State.TryGetLog(payload.LogId, out var log))
        {
            return context.UnknownLog(payload.LogId);
        }

        if (log.State != payload.FromState)
        {
            return context.Contradiction($"log {payload.LogId} is {log.State} but the event reports {payload.FromState}");
        }

        context.State.SetLogState(payload.LogId, payload.ToState);
        context.State.ApplyMovement(MovementNoiseAcceptedSource.ManualLogIntent, payload, context.Envelope.ServerTick);
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceProcedureActionStarted(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenProcedureActionStartedPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenProcedureActionStartedPayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (!context.State.TryGetLog(payload.LogId, out var log))
        {
            return context.UnknownLog(payload.LogId);
        }

        if (log.State != LogState.AT_PROCEDURE || log.Anomaly != payload.AnomalyId)
        {
            return context.Contradiction("a procedure hold requires its anomalous owner at the procedure position");
        }

        if (context.State.ActiveProcedureHold is not null)
        {
            return context.Contradiction("only one active procedure hold is permitted");
        }

        context.State.ActiveProcedureHold = new SnapshotProcedureHold(
            payload.LogId, payload.AnomalyId, payload.AttemptedItem, payload.ProcedureStepIndex, payload.StartedAt, payload.Duration);
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceProcedureActionCompleted(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenProcedurePayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenProcedurePayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        var descriptor = payload.Descriptor;
        if (!context.State.TryGetLog(descriptor.LogId, out var log))
        {
            return context.UnknownLog(descriptor.LogId);
        }

        if (log.State != LogState.AT_PROCEDURE)
        {
            return context.Contradiction("a procedure completion requires its owner at the procedure position");
        }

        if (descriptor.ItemConsumed && context.State.ConsumeItem(descriptor.AttemptedItem) is { } consumeFailure)
        {
            return context.Contradiction(consumeFailure);
        }

        context.State.SetProcedureProgress(descriptor.LogId, descriptor.CurrentProgress);
        context.State.AddFlags(descriptor.LogId, descriptor.NewlyGrantedFlags);

        // Both the due stage-one completion and the immediate stage-two completion leave no active hold.
        context.State.ActiveProcedureHold = null;
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceConfirmationTestStarted(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenConfirmationTestStartedPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenConfirmationTestStartedPayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (!context.State.TryGetLog(payload.LogId, out var log))
        {
            return context.UnknownLog(payload.LogId);
        }

        if (log.State != LogState.AT_INTAKE || log.Anomaly != payload.AnomalyId)
        {
            return context.Contradiction("a confirmation start requires its anomalous owner at intake");
        }

        if (context.State.ActiveConfirmationTest is not null)
        {
            return context.Contradiction("only one active confirmation test is permitted");
        }

        if (payload.DueAt != payload.SegmentStartedAt + payload.Duration)
        {
            return context.Contradiction("confirmation due tick does not equal segment start plus duration");
        }

        context.State.ActiveConfirmationTest = new SnapshotConfirmationTest(
            payload.LogId,
            payload.AnomalyId,
            payload.RequiredTools.ToImmutableArray(),
            payload.Duration,
            payload.Continuous,
            payload.RequiredLineNoise,
            payload.ResetWhenConditionLost,
            payload.Result,
            SimulationDuration.Zero,
            payload.SegmentStartedAt,
            true,
            payload.SegmentStartedAt);
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceConfirmationTestCompleted(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenConfirmationPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenConfirmationPayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        var result = payload.Result;
        if (!context.State.HasLog(result.LogId))
        {
            return context.UnknownLog(result.LogId);
        }

        if (context.State.ActiveConfirmationTest is not { } active || active.LogId != result.LogId)
        {
            return context.Contradiction("confirmation completion requires the matching active test");
        }

        context.State.ActiveConfirmationTest = null;
        context.State.SetConfirmationResult(result);
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceContainmentRitualStarted(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenContainmentRitualStartedPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenContainmentRitualStartedPayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (context.State.ContainmentStateValue != payload.ContainmentState || context.State.ContainmentEnteredAt != payload.ContainmentEnteredAt ||
            context.State.ContainmentDeadlineAt != payload.ContainmentDeadlineAt)
        {
            return context.Contradiction("ritual start must retain the exact existing containment evidence");
        }

        if (context.State.ActiveContainmentRitual is not null)
        {
            return context.Contradiction("only one active containment ritual is permitted");
        }

        context.State.ActiveContainmentRitual = new SnapshotContainmentRitual(payload.RitualStartedAt, payload.RitualDuration);
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceContainmentRitualCompleted(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenContainmentPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenContainmentPayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (context.State.ActiveContainmentRitual is null)
        {
            return context.Contradiction("ritual completion requires an active ritual");
        }

        context.State.SetContainment(payload.CurrentContainment);

        // A completed ritual always clears itself; the payload's ritual is the completed one, not a surviving hold.
        context.State.ActiveContainmentRitual = null;
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceContainmentStateChanged(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenContainmentPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenContainmentPayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        context.State.SetContainment(payload.CurrentContainment);

        // Stage three retains the after-state ritual, so an in-flight ritual survives a containment advance.
        context.State.ActiveContainmentRitual = payload.Ritual is { } ritual ? new SnapshotContainmentRitual(ritual.StartedAt, ritual.Duration) : null;
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceConfirmationConditionUpdated(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenConfirmationConditionPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenConfirmationConditionPayload));
        }

        if (context.BeginMutation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        context.State.ActiveConfirmationTest = payload.Current is { } current ? ReductionState.ProjectConfirmation(current) : null;
        context.CompleteMutation();
        return null;
    }

    private static ShiftReplayResult? ReduceShiftCompleted(ReductionContext context)
    {
        if (context.Envelope.Payload is not HostStageSevenShiftCompletedPayload payload)
        {
            return context.PayloadMismatch(nameof(HostStageSevenShiftCompletedPayload));
        }

        if (context.RequireObservation(payload.PriorStateVersion, payload.CurrentStateVersion) is { } failure)
        {
            return failure;
        }

        if (context.State.Completion is not null)
        {
            return context.Contradiction("a completed shift cannot complete again");
        }

        if (payload.CompletedAt != context.Envelope.ServerTick)
        {
            return context.Contradiction("completion tick must equal the event tick");
        }

        if (payload.HardDeadlineAt != context.State.HardDeadlineAt)
        {
            return context.Contradiction("completion hard deadline contradicts the selected profile");
        }

        if (payload.TotalCreditedUnits != context.State.TotalCreditedUnits || payload.CorrectlyProcessedAnomalies != context.State.CorrectlyProcessedAnomalies)
        {
            return context.Contradiction("completion quota summary contradicts the reconstructed quota");
        }

        var (processed, writtenOff) = context.State.CountTerminal();
        if (payload.ProcessedCount != processed || payload.WrittenOffCount != writtenOff)
        {
            return context.Contradiction("completion terminal counts contradict the reconstructed manifest");
        }

        context.State.Completion = new SnapshotCompletion(
            payload.CompletedAt,
            payload.Reason,
            payload.AllLogsTerminal,
            payload.HardDeadlineReached,
            payload.ObjectivesSatisfied,
            payload.ProcessedCount,
            payload.WrittenOffCount);
        context.CompleteObservation();
        return null;
    }

    private static ShiftReplaySemanticRejected Fail(ShiftReplaySemanticFailure failure, int position, EventTypeId eventType, string detail) =>
        new(failure, position, eventType, detail);
}

/// <summary>The frozen stage-seven catalog, resolved exactly and never by name guessing.</summary>
internal enum ReplayEventKind
{
    FeedScheduled,
    EarlyFeedRequested,
    LogPlacedAtFeedGate,
    LogAdmittedToIntake,
    IntakeDeadlineStarted,
    IntakeDeadlineExpired,
    AutoRouteAttempted,
    LineJammed,
    RepairStarted,
    RepairCompleted,
    SawCycleStarted,
    SawCycleCompleted,
    LineNoiseChanged,
    LogRouted,
    LogWrittenOff,
    ProcedureActionStarted,
    ProcedureActionCompleted,
    ConfirmationTestStarted,
    ConfirmationTestCompleted,
    ContainmentRitualStarted,
    ContainmentRitualCompleted,
    ContainmentStateChanged,
    ConfirmationConditionUpdated,
    ShiftCompleted
}

internal static class ReplayEventCatalog
{
    private static readonly ImmutableDictionary<EventTypeId, ReplayEventKind> Kinds = ImmutableDictionary.CreateRange(
    [
        KeyValuePair.Create(HostStageSevenEventTypes.FeedScheduled, ReplayEventKind.FeedScheduled),
        KeyValuePair.Create(HostStageSevenEventTypes.EarlyFeedRequested, ReplayEventKind.EarlyFeedRequested),
        KeyValuePair.Create(HostStageSevenEventTypes.LogPlacedAtFeedGate, ReplayEventKind.LogPlacedAtFeedGate),
        KeyValuePair.Create(HostStageSevenEventTypes.LogAdmittedToIntake, ReplayEventKind.LogAdmittedToIntake),
        KeyValuePair.Create(HostStageSevenEventTypes.IntakeDeadlineStarted, ReplayEventKind.IntakeDeadlineStarted),
        KeyValuePair.Create(HostStageSevenEventTypes.IntakeDeadlineExpired, ReplayEventKind.IntakeDeadlineExpired),
        KeyValuePair.Create(HostStageSevenEventTypes.AutoRouteAttempted, ReplayEventKind.AutoRouteAttempted),
        KeyValuePair.Create(HostStageSevenEventTypes.LineJammed, ReplayEventKind.LineJammed),
        KeyValuePair.Create(HostStageSevenEventTypes.RepairStarted, ReplayEventKind.RepairStarted),
        KeyValuePair.Create(HostStageSevenEventTypes.RepairCompleted, ReplayEventKind.RepairCompleted),
        KeyValuePair.Create(HostStageSevenEventTypes.SawCycleStarted, ReplayEventKind.SawCycleStarted),
        KeyValuePair.Create(HostStageSevenEventTypes.SawCycleCompleted, ReplayEventKind.SawCycleCompleted),
        KeyValuePair.Create(HostStageSevenEventTypes.LineNoiseChanged, ReplayEventKind.LineNoiseChanged),
        KeyValuePair.Create(HostStageSevenEventTypes.LogRouted, ReplayEventKind.LogRouted),
        KeyValuePair.Create(HostStageSevenEventTypes.LogWrittenOff, ReplayEventKind.LogWrittenOff),
        KeyValuePair.Create(HostStageSevenEventTypes.ProcedureActionStarted, ReplayEventKind.ProcedureActionStarted),
        KeyValuePair.Create(HostStageSevenEventTypes.ProcedureActionCompleted, ReplayEventKind.ProcedureActionCompleted),
        KeyValuePair.Create(HostStageSevenEventTypes.ConfirmationTestStarted, ReplayEventKind.ConfirmationTestStarted),
        KeyValuePair.Create(HostStageSevenEventTypes.ConfirmationTestCompleted, ReplayEventKind.ConfirmationTestCompleted),
        KeyValuePair.Create(HostStageSevenEventTypes.ContainmentRitualStarted, ReplayEventKind.ContainmentRitualStarted),
        KeyValuePair.Create(HostStageSevenEventTypes.ContainmentRitualCompleted, ReplayEventKind.ContainmentRitualCompleted),
        KeyValuePair.Create(HostStageSevenEventTypes.ContainmentStateChanged, ReplayEventKind.ContainmentStateChanged),
        KeyValuePair.Create(HostStageSevenEventTypes.ConfirmationConditionUpdated, ReplayEventKind.ConfirmationConditionUpdated),
        KeyValuePair.Create(HostStageSevenEventTypes.ShiftCompleted, ReplayEventKind.ShiftCompleted)
    ]);

    internal static int Count => Kinds.Count;

    internal static bool TryResolve(EventTypeId eventType, out ReplayEventKind kind) => Kinds.TryGetValue(eventType, out kind);
}

/// <summary>One event's reduction context, carrying the shared version and tick bookkeeping.</summary>
internal sealed class ReductionContext
{
    internal ReductionContext(ReductionState state, EventEnvelope envelope, int position)
    {
        State = state;
        Envelope = envelope;
        Position = position;
    }

    internal ReductionState State { get; }
    internal EventEnvelope Envelope { get; }
    internal int Position { get; }

    /// <summary>Validates a state-changing event's exact one-step version evidence before it is applied.</summary>
    internal ShiftReplayResult? BeginMutation(StateVersion prior, StateVersion current)
    {
        if (prior != State.StateVersion)
        {
            return new ShiftReplaySemanticRejected(ShiftReplaySemanticFailure.StateVersionMismatch, Position, Envelope.EventType,
                $"payload prior version {prior} != reconstructed {State.StateVersion}");
        }

        if (!prior.TryNext(out var expected) || current != expected || Envelope.StateVersionAfter != expected)
        {
            return new ShiftReplaySemanticRejected(ShiftReplaySemanticFailure.StateVersionMismatch, Position, Envelope.EventType,
                $"a state-changing event must advance exactly one version; payload {current}, envelope {Envelope.StateVersionAfter}");
        }

        return null;
    }

    /// <summary>Validates an observational event's unchanged version evidence.</summary>
    internal ShiftReplayResult? RequireObservation(StateVersion prior, StateVersion current)
    {
        if (prior != State.StateVersion || current != State.StateVersion || Envelope.StateVersionAfter != State.StateVersion)
        {
            return new ShiftReplaySemanticRejected(ShiftReplaySemanticFailure.ObservationalVersionMismatch, Position, Envelope.EventType,
                $"an observation must retain version {State.StateVersion}; payload {prior}/{current}, envelope {Envelope.StateVersionAfter}");
        }

        return null;
    }

    internal void CompleteMutation()
    {
        State.AdvanceVersion();
        State.RecordCausation(Envelope.CausedByIntentId);
        State.AdvanceBoundary(Envelope);
    }

    internal void CompleteObservation() => State.AdvanceBoundary(Envelope);

    internal ShiftReplayResult? PayloadMismatch(string expected) =>
        new ShiftReplaySemanticRejected(ShiftReplaySemanticFailure.PayloadTypeMismatch, Position, Envelope.EventType,
            $"expected {expected} but found {Envelope.Payload.GetType().Name}");

    internal ShiftReplayResult? UnknownLog(LogId logId) =>
        new ShiftReplaySemanticRejected(ShiftReplaySemanticFailure.UnknownLog, Position, Envelope.EventType, $"log {logId} is not in the manifest");

    internal ShiftReplayResult? Contradiction(string detail) =>
        new ShiftReplaySemanticRejected(ShiftReplaySemanticFailure.ContradictoryState, Position, Envelope.EventType, detail);
}
