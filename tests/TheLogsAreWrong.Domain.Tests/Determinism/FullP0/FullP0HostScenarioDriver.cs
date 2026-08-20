using System.Collections.Immutable;
using System.Globalization;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Determinism;

/// <summary>Immutable per-tick evidence retained by the TLAW-042 driver. Every value comes from the exact host result.</summary>
internal sealed record FullP0HostTickRecord(
    ServerTick Tick,
    string ExecutionKind,
    ImmutableArray<EventTypeId> PublishedEventTypes,
    ImmutableArray<IntentId> AcceptedIntentIds,
    StateVersion StateVersionAfter,
    ContainmentState ContainmentState,
    LogId? ActiveIntakeDeadlineLogId,
    ServerTick? ActiveIntakeDeadlineDueAt,
    LineNoise LineNoise,
    bool LifecycleCompleted);

/// <summary>
/// The immutable result of one complete TLAW-042 scenario run. It retains only the exact separate host-owned states the
/// authoritative host returned, the test-owned journal it appended to, and the canonical projection derived from them.
/// It is not a production aggregate: nothing here is passed back into production except the exact carried-forward states.
/// </summary>
internal sealed class FullP0HostScenarioRun
{
    internal FullP0HostScenarioRun(
        FullP0HostScenarioScript script,
        ShiftRuntimeState finalShiftState,
        QuotaRuntimeState finalQuotaState,
        MovementNoiseRuntimeState finalMovementNoise,
        LineNoiseRuntimeState finalLineNoise,
        HostTickProgressionEvidence finalProgression,
        ShiftLifecycleRuntimeState finalLifecycle,
        InMemoryEventJournal journal,
        ImmutableArray<FullP0HostTickRecord> ticks,
        ImmutableArray<HostStageSevenEventExecution> executions)
    {
        Script = script;
        FinalShiftState = finalShiftState;
        FinalQuotaState = finalQuotaState;
        FinalMovementNoise = finalMovementNoise;
        FinalLineNoise = finalLineNoise;
        FinalProgression = finalProgression;
        FinalLifecycle = finalLifecycle;
        Journal = journal;
        Ticks = ticks;
        Executions = executions;
        Projection = FullP0HostTraceProjection.Create(this);
    }

    public FullP0HostScenarioScript Script { get; }
    public ShiftRuntimeState FinalShiftState { get; }
    public QuotaRuntimeState FinalQuotaState { get; }
    public MovementNoiseRuntimeState FinalMovementNoise { get; }
    public LineNoiseRuntimeState FinalLineNoise { get; }
    public HostTickProgressionEvidence FinalProgression { get; }
    public ShiftLifecycleRuntimeState FinalLifecycle { get; }
    public InMemoryEventJournal Journal { get; }
    public ImmutableArray<FullP0HostTickRecord> Ticks { get; }
    public ImmutableArray<HostStageSevenEventExecution> Executions { get; }
    public FullP0HostTraceProjection Projection { get; }

    public int HostTickCount => Ticks.Length;

    public ShiftCompletionEvidence Completion =>
        FinalLifecycle.Completion ?? throw new InvalidOperationException("This scenario did not reach lifecycle completion.");

    public FullP0HostTickRecord RecordAt(long tick) =>
        Ticks.SingleOrDefault(record => record.Tick.Value == tick)
        ?? throw new InvalidOperationException($"Tick {tick.ToString(CultureInfo.InvariantCulture)} was not executed.");

    public IEnumerable<EventEnvelope> EventsOfType(EventTypeId eventType) =>
        Journal.Events.Where(envelope => envelope.EventType == eventType);

    public HostStageSevenSawCompletedPayload SawCompletionFor(string logId)
    {
        var target = LogId.From(logId);
        return EventsOfType(HostStageSevenEventTypes.SawCycleCompleted)
            .Select(envelope => (HostStageSevenSawCompletedPayload)envelope.Payload)
            .Single(payload => payload.Cycle.LogId == target);
    }
}

/// <summary>
/// The TLAW-042 test-only deterministic full-host scenario driver.
/// <para>
/// It owns no gameplay. For every sequential tick it builds the exact accepted-intent batch, the exact active-tool set
/// and calls the production
/// <see cref="HostTickExecutionService"/> exactly once, and carries forward only the exact states that host result
/// returned. It never skips a tick, never synthesises a checkpoint receipt, never mutates production state directly and
/// never composes stages one through seven itself.
/// </para>
/// </summary>
internal sealed class FullP0HostScenarioDriver
{
    private readonly HostTickExecutionService _host = new();

    public FullP0HostScenarioRun Run(ValidatedConfiguration configuration, FullP0HostScenarioScript script)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(script);
        if (configuration.Shift.Seed != script.Seed)
        {
            throw new ArgumentException("The scenario script seed must match the supplied validated configuration.", nameof(script));
        }

        if (!configuration.Shift.Profiles.ContainsKey(script.Profile))
        {
            throw new ArgumentException("The scenario profile must exist in the supplied validated configuration.", nameof(script));
        }

        var shift = ShiftRuntimeState.Create(configuration.Shift);
        var quota = QuotaRuntimeState.Create(configuration.Shift);
        var movementNoise = MovementNoiseRuntimeState.Create(shift.ShiftId);
        var lineNoise = LineNoiseRuntimeState.Create(shift.ShiftId);
        var progression = HostTickProgressionEvidence.Create(shift.ShiftId);
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration.Shift, script.Profile);
        var journal = new InMemoryEventJournal(shift.ShiftId);

        var records = ImmutableArray.CreateBuilder<FullP0HostTickRecord>();
        var executions = ImmutableArray.CreateBuilder<HostStageSevenEventExecution>();

        for (var value = 0L; value <= script.FinalTick.Value; value++)
        {
            var tick = ServerTick.From(value);
            var scripted = script.TickAt(tick);
            var batch = BuildBatch(script, shift, tick, scripted);
            var execution = _host.Execute(
                shift,
                quota,
                movementNoise,
                lineNoise,
                progression,
                lifecycle,
                batch,
                scripted.ActiveTools,
                journal,
                tick,
                configuration.Shift.Scheduler,
                configuration.Shift,
                configuration.Shift.Containment,
                configuration.Anomalies);

            var published = RequireExpectedPublication(script, tick, scripted, execution);
            RequireScriptedIntentOutcomes(script, tick, scripted, execution);

            var checkpoint = execution.Checkpoint as HostTickCheckpointAdvanced
                ?? throw new InvalidOperationException(Describe(script, tick, $"checkpoint was {execution.Checkpoint.GetType().Name} rather than an advanced host tick."));

            shift = execution.FinalShiftState;
            quota = execution.FinalQuotaState;
            movementNoise = execution.StageSix.FinalMovementNoise;
            lineNoise = execution.FinalLineNoise;
            progression = checkpoint.Progression;
            lifecycle = checkpoint.Receipt.Lifecycle;

            records.Add(new FullP0HostTickRecord(
                tick,
                execution.GetType().Name,
                published,
                batch.Intents.Select(receipt => receipt.Envelope.IntentId).ToImmutableArray(),
                shift.StateVersion,
                shift.Containment.State,
                shift.ActiveIntakeDeadline?.LogId,
                shift.ActiveIntakeDeadline?.DueAt,
                lineNoise.Current,
                lifecycle.IsCompleted));
            executions.Add(execution);

            if (lifecycle.IsCompleted)
            {
                if (tick != script.FinalTick)
                {
                    throw new InvalidOperationException(Describe(script, tick, "the lifecycle completed before the exact scripted final tick."));
                }

                break;
            }
        }

        if (script.RequiresLifecycleCompletion && !lifecycle.IsCompleted)
        {
            throw new InvalidOperationException($"{script.ScenarioId}: the scenario did not complete by its exact scripted final tick.");
        }

        return new FullP0HostScenarioRun(
            script,
            shift,
            quota,
            movementNoise,
            lineNoise,
            progression,
            lifecycle,
            journal,
            records.ToImmutable(),
            executions.ToImmutable());
    }

    private static AcceptedIntentTickBatch BuildBatch(
        FullP0HostScenarioScript script,
        ShiftRuntimeState shift,
        ServerTick tick,
        FullP0ScriptedTick scripted)
    {
        var receipts = ImmutableArray.CreateBuilder<AuthoritativeAcceptedIntent>(scripted.Intents.Length);
        var receiveSequence = ServerReceiveSequence.Zero;
        for (var index = 0; index < scripted.Intents.Length; index++)
        {
            var intent = scripted.Intents[index];
            var envelope = new IntentEnvelope(
                shift.ShiftId,
                IntentIdFor(script, tick, index),
                ActorHint,
                intent.Target,
                intent.Action,
                StateVersion.From(checked(shift.StateVersion.Value + intent.ExpectedStateVersionOffset)),
                tick,
                intent.Parameters);
            receipts.Add(new AuthoritativeAcceptedIntent(envelope, BoundActor, tick, receiveSequence));
            receiveSequence = receiveSequence.Next();
        }

        return AcceptedIntentTickBatchFactory.Create(shift.ShiftId, tick, receipts.MoveToImmutable());
    }

    private static ImmutableArray<EventTypeId> RequireExpectedPublication(
        FullP0HostScenarioScript script,
        ServerTick tick,
        FullP0ScriptedTick scripted,
        HostStageSevenEventExecution execution)
    {
        if (scripted.ExpectedEvents.IsEmpty)
        {
            if (execution is not HostStageSevenNoNewPublication)
            {
                throw new InvalidOperationException(Describe(script, tick, $"the script declared no publication but the host returned {execution.GetType().Name}."));
            }

            return [];
        }

        if (execution is not HostStageSevenPublished publishedResult)
        {
            throw new InvalidOperationException(Describe(script, tick, $"the script declared {scripted.ExpectedEvents.Length.ToString(CultureInfo.InvariantCulture)} publications but the host returned {execution.GetType().Name}."));
        }

        var actual = publishedResult.Publications.Select(publication => publication.Envelope.EventType).ToImmutableArray();
        if (!actual.SequenceEqual(scripted.ExpectedEvents))
        {
            throw new InvalidOperationException(Describe(
                script,
                tick,
                $"expected publications [{string.Join(", ", scripted.ExpectedEvents)}] but the host published [{string.Join(", ", actual)}]."));
        }

        return actual;
    }

    private static void RequireScriptedIntentOutcomes(
        FullP0HostScenarioScript script,
        ServerTick tick,
        FullP0ScriptedTick scripted,
        HostStageSevenEventExecution execution)
    {
        var steps = execution.StageTwo.Steps;
        if (steps.Length != scripted.Intents.Length)
        {
            throw new InvalidOperationException(Describe(script, tick, "the host did not evaluate exactly the scripted accepted-intent batch."));
        }

        for (var index = 0; index < steps.Length; index++)
        {
            var expected = scripted.Intents[index].ExpectedOutcome;
            var actual = steps[index].Outcome;
            var matches = expected switch
            {
                FullP0ScriptedIntentOutcome.ManualRoutingAccepted => actual is ManualRoutingIntentStageOutcome { Result: ManualLogIntentAccepted },
                FullP0ScriptedIntentOutcome.EarlyFeedScheduled => actual is EarlyFeedIntentStageOutcome { Result: EarlyFeedScheduled },
                FullP0ScriptedIntentOutcome.ProcedureActionHoldStarted => actual is ProcedureActionIntentStageOutcome { Result: ProcedureActionIntentHoldStarted },
                FullP0ScriptedIntentOutcome.ProcedureActionCompletedImmediately => actual is ProcedureActionIntentStageOutcome { Result: ProcedureActionIntentCompletedImmediately },
                FullP0ScriptedIntentOutcome.ConfirmationTestStarted => actual is ConfirmationTestIntentStageOutcome { Result: ConfirmationTestIntentStarted },
                FullP0ScriptedIntentOutcome.LineRepairStarted => actual is LineRepairIntentStageOutcome { Result: LineRepairIntentStarted },
                FullP0ScriptedIntentOutcome.ContainmentRitualStarted => actual is ContainmentRitualIntentStageOutcome { Result: ContainmentRitualIntentStarted },
                _ => false
            };

            if (!matches)
            {
                throw new InvalidOperationException(Describe(script, tick, $"receipt {index.ToString(CultureInfo.InvariantCulture)} expected {expected} but the host returned {actual.GetType().Name}."));
            }
        }
    }

    private static string Describe(FullP0HostScenarioScript script, ServerTick tick, string detail) =>
        $"{script.ScenarioId} tick {tick.Value.ToString(CultureInfo.InvariantCulture)}: {detail}";

    internal static readonly ActorId ActorHint = ActorId.From("tlaw042_actor_hint");
    internal static readonly ActorId BoundActor = ActorId.From("tlaw042_bound_actor");

    /// <summary>Deterministic intent identity derived only from the exact scenario, tick and receive ordinal.</summary>
    internal static IntentId IntentIdFor(FullP0HostScenarioScript script, ServerTick tick, int ordinal) =>
        IntentId.From($"{script.IdentityNamespace}#t{tick.Value.ToString(CultureInfo.InvariantCulture)}#i{ordinal.ToString(CultureInfo.InvariantCulture)}");
}
