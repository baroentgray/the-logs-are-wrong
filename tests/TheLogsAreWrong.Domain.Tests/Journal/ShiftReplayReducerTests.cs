using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Tests.Determinism;

namespace TheLogsAreWrong.Domain.Tests.Journal;

/// <summary>
/// TLAW-046 reducer coverage and fail-closed evidence. Ordering rejection always comes from the existing
/// <see cref="ReplayValidator"/> taxonomy; semantic rejection uses the separate TLAW-046 taxonomy.
/// </summary>
[Trait("Scope", "TLAW-046")]
public sealed class ShiftReplayReducerTests
{
    private static readonly ShiftReplayService Replay = new();
    private static readonly ShiftSnapshotCaptureService Capture = new();

    private static readonly Func<FullP0HostScenarioScript>[] AllScripts =
    [
        FullP0HostScenarioScript.LearningCorrectPath,
        FullP0HostScenarioScript.LearningFullTimeout,
        FullP0HostScenarioScript.PressureFullTimeout,
        FullP0HostScenarioScript.WriteOffAllSuspicious,
        FullP0HostScenarioScript.IncorrectPenitent,
        FullP0HostScenarioScript.IncorrectFalseSpecies,
        FullP0HostScenarioScript.IncorrectResin,
        FullP0HostScenarioScript.ResinWrongHolyWaterRecovery
    ];

    private static (ValidatedConfiguration Configuration, FullP0HostScenarioScript Script, FullP0HostScenarioRun Run) Execute(Func<FullP0HostScenarioScript> factory)
    {
        var configuration = Fixture.LoadP0();
        var script = factory();
        return (configuration, script, new FullP0HostScenarioDriver().Run(configuration, script));
    }

    private static ShiftSnapshot Initial(ValidatedConfiguration configuration, FullP0HostScenarioScript script) =>
        Capture.CreateInitial(configuration.Shift, script.Profile);

    private static ShiftSnapshot ReplayAll(ValidatedConfiguration configuration, FullP0HostScenarioScript script, FullP0HostScenarioRun run) =>
        Assert.IsType<ShiftReplaySucceeded>(Replay.ReplayAll(configuration.Shift, script.Profile, run.Journal.Events)).Snapshot;

    private static ShiftSnapshot Live(FullP0HostScenarioRun run) =>
        Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(run.Executions[^1])).Snapshot;

    private static EventEnvelope Rebuild(EventEnvelope source, EventSequence? sequence = null, ServerTick? tick = null, StateVersion? version = null, ShiftId? shift = null, EventId? eventId = null) => new()
    {
        ShiftId = shift ?? source.ShiftId,
        EventId = eventId ?? source.EventId,
        Sequence = sequence ?? source.Sequence,
        CausedByIntentId = source.CausedByIntentId,
        ServerTick = tick ?? source.ServerTick,
        StateVersionAfter = version ?? source.StateVersionAfter,
        EventType = source.EventType,
        Payload = source.Payload
    };

    // ----- catalog coverage -----

    [Fact]
    public void Every_frozen_stage_seven_event_type_is_reduced_by_a_real_published_event()
    {
        var frozen = typeof(HostStageSevenEventTypes)
            .GetFields()
            .Where(field => field.FieldType == typeof(EventTypeId))
            .Select(field => (EventTypeId)field.GetValue(null)!)
            .ToImmutableHashSet();
        Assert.Equal(24, frozen.Count);

        var reduced = ImmutableHashSet<EventTypeId>.Empty;
        foreach (var factory in AllScripts)
        {
            var (configuration, script, run) = Execute(factory);
            var replayed = ReplayAll(configuration, script, run);
            Assert.Null(Live(run).FirstDifference(replayed));
            reduced = reduced.Union(run.Journal.Events.Select(envelope => envelope.EventType));
        }

        // The frozen scripts never lose a confirmation condition mid-test, so that one type is reached by its own
        // deliberately interrupted confirmation below. Together the two sources must cover the whole catalog.
        var interrupted = InterruptedConfirmation();
        reduced = reduced.Union(interrupted.Journal.Events.Select(envelope => envelope.EventType));

        Assert.Empty(frozen.Except(reduced));
    }

    [Fact]
    public void Confirmation_condition_updated_is_reduced_from_a_real_interrupted_confirmation()
    {
        var interrupted = InterruptedConfirmation();

        Assert.Equal(HostStageSevenEventTypes.ConfirmationConditionUpdated, interrupted.Journal.Events[^1].EventType);

        var replayed = Assert.IsType<ShiftReplaySucceeded>(
            Replay.ReplayAll(interrupted.Configuration.Shift, interrupted.Profile, interrupted.Journal.Events)).Snapshot;

        Assert.Null(interrupted.Live.FirstDifference(replayed));
        Assert.NotNull(replayed.SchedulerState.ActiveConfirmationTest);
        Assert.False(replayed.SchedulerState.ActiveConfirmationTest!.IsRunning);
    }

    private sealed record InterruptedConfirmationRun(
        ValidatedConfiguration Configuration,
        ProfileId Profile,
        InMemoryEventJournal Journal,
        ShiftSnapshot Live);

    /// <summary>
    /// Takes the canonical Learning run up to the tick that starts the Penitent confirmation, then executes one further
    /// real host tick with no active tool so the running confirmation loses its condition.
    /// </summary>
    private static InterruptedConfirmationRun InterruptedConfirmation()
    {
        var (configuration, script, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);

        var started = run.Executions.First(execution => execution.FinalShiftState.ActiveConfirmationTest is not null);
        var checkpoint = Assert.IsType<HostTickCheckpointAdvanced>(started.Checkpoint);

        var journal = new InMemoryEventJournal(started.FinalShiftState.ShiftId);
        foreach (var envelope in run.Journal.Events.Where(envelope => envelope.Sequence <= started.AfterCursor.LastSequence))
        {
            journal.Append(envelope);
        }

        var tick = ServerTick.From(started.CurrentTick.Value + 1);
        var execution = new HostTickExecutionService().Execute(
            started.FinalShiftState,
            started.FinalQuotaState,
            started.StageSix.FinalMovementNoise,
            started.FinalLineNoise,
            checkpoint.Progression,
            checkpoint.Receipt.Lifecycle,
            AcceptedIntentTickBatchFactory.Create(started.FinalShiftState.ShiftId, tick, ImmutableArray<AuthoritativeAcceptedIntent>.Empty),
            ImmutableHashSet<ItemId>.Empty,
            journal,
            ImmutableArray.Create(EventId.From("tlaw046_condition_lost")),
            tick,
            configuration.Shift.Scheduler,
            configuration.Shift,
            configuration.Shift.Containment,
            configuration.Anomalies);

        Assert.IsType<HostStageSevenPublished>(execution);
        var live = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(execution)).Snapshot;
        return new InterruptedConfirmationRun(configuration, script.Profile, journal, live);
    }

    // ----- ordering is the existing validator's authority -----

    [Theory]
    [InlineData(ReplayAnomaly.ShiftMismatch)]
    [InlineData(ReplayAnomaly.GapAfterSnapshot)]
    [InlineData(ReplayAnomaly.Duplicate)]
    [InlineData(ReplayAnomaly.OutOfOrder)]
    [InlineData(ReplayAnomaly.SequenceGap)]
    [InlineData(ReplayAnomaly.TickRegression)]
    [InlineData(ReplayAnomaly.StateVersionRegression)]
    [InlineData(ReplayAnomaly.StateVersionSkip)]
    [InlineData(ReplayAnomaly.DefaultValue)]
    public void Malformed_tails_are_rejected_by_the_existing_replay_validator_before_semantic_reduction(ReplayAnomaly expected)
    {
        var (configuration, script, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var initial = Initial(configuration, script);
        var events = run.Journal.Events.ToArray();
        var tail = expected switch
        {
            ReplayAnomaly.ShiftMismatch => [Rebuild(events[0], shift: ShiftId.From("TLAW046_OTHER"))],
            ReplayAnomaly.GapAfterSnapshot => new[] { Rebuild(events[0], sequence: EventSequence.From(3)) },
            ReplayAnomaly.Duplicate => new[] { events[0], events[0] },
            ReplayAnomaly.OutOfOrder => new[] { events[0], events[1], events[2], Rebuild(events[3], sequence: events[0].Sequence) },
            ReplayAnomaly.SequenceGap => new[] { events[0], Rebuild(events[1], sequence: EventSequence.From(5)) },
            ReplayAnomaly.TickRegression => TickRegression(events),
            ReplayAnomaly.StateVersionRegression => VersionRegression(events),
            ReplayAnomaly.StateVersionSkip => new[] { Rebuild(events[0], version: StateVersion.From(9)) },
            ReplayAnomaly.DefaultValue => new[] { Rebuild(events[0], eventId: default(EventId)) },
            _ => throw new ArgumentOutOfRangeException(nameof(expected))
        };

        var result = Assert.IsType<ShiftReplayOrderingRejected>(Replay.ReplayFrom(initial, tail, configuration.Shift));
        Assert.Equal(expected, result.Anomaly);

        // A rejected tail never mutates the supplied snapshot.
        Assert.Equal(0, initial.StateVersion.Value);
        Assert.Equal(EventSequence.None, initial.LastEventSequence);
    }

    private static EventEnvelope[] TickRegression(EventEnvelope[] events)
    {
        Assert.True(events[^1].ServerTick.Value > 0, "the canonical journal must publish beyond tick zero for this proof to be non-vacuous");
        return [.. events.Take(events.Length - 1), Rebuild(events[^1], tick: ServerTick.Zero)];
    }

    private static EventEnvelope[] VersionRegression(EventEnvelope[] events)
    {
        var index = Array.FindIndex(events, envelope => envelope.StateVersionAfter.Value > 1);
        Assert.True(index > 0, "the canonical journal must advance the state version for this proof to be non-vacuous");
        return [.. events.Take(index), Rebuild(events[index], version: StateVersion.Zero)];
    }

    [Fact]
    public void An_event_already_covered_by_the_snapshot_boundary_is_rejected_as_a_snapshot_duplicate()
    {
        var (configuration, _, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var mid = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(run.Executions[10])).Snapshot;
        Assert.True(mid.LastEventSequence.Value > 1);

        var alreadyCovered = run.Journal.Events.First(envelope => envelope.Sequence <= mid.LastEventSequence);
        var result = Assert.IsType<ShiftReplayOrderingRejected>(
            Replay.ReplayFrom(mid, [alreadyCovered], configuration.Shift));
        Assert.Equal(ReplayAnomaly.DuplicateOfSnapshot, result.Anomaly);
    }

    // ----- semantic fail-closed -----

    [Fact]
    public void An_event_type_outside_the_frozen_catalog_fails_closed()
    {
        var (configuration, script, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var first = run.Journal.Events[0];
        var unknown = new EventEnvelope
        {
            ShiftId = first.ShiftId,
            EventId = first.EventId,
            Sequence = first.Sequence,
            CausedByIntentId = first.CausedByIntentId,
            ServerTick = first.ServerTick,
            StateVersionAfter = first.StateVersionAfter,
            EventType = EventTypeId.From("NotAFrozenEventType"),
            Payload = first.Payload
        };

        var result = Assert.IsType<ShiftReplaySemanticRejected>(
            Replay.ReplayFrom(Initial(configuration, script), [unknown], configuration.Shift));
        Assert.Equal(ShiftReplaySemanticFailure.UnknownEventType, result.Failure);
        Assert.Equal(0, result.Position);
    }

    [Fact]
    public void An_event_type_carrying_the_wrong_payload_fails_closed()
    {
        var (configuration, script, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var first = run.Journal.Events[0];
        var mismatched = new EventEnvelope
        {
            ShiftId = first.ShiftId,
            EventId = first.EventId,
            Sequence = first.Sequence,
            CausedByIntentId = first.CausedByIntentId,
            ServerTick = first.ServerTick,
            StateVersionAfter = first.StateVersionAfter,
            EventType = HostStageSevenEventTypes.SawCycleStarted,
            Payload = first.Payload
        };

        var result = Assert.IsType<ShiftReplaySemanticRejected>(
            Replay.ReplayFrom(Initial(configuration, script), [mismatched], configuration.Shift));
        Assert.Equal(ShiftReplaySemanticFailure.PayloadTypeMismatch, result.Failure);
    }

    [Fact]
    public void An_observational_event_whose_version_moves_fails_closed()
    {
        var (configuration, script, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var events = run.Journal.Events.ToArray();
        var index = Array.FindIndex(events, envelope => envelope.EventType == HostStageSevenEventTypes.LineNoiseChanged);
        Assert.True(index >= 0, "the canonical journal must publish a line-noise change");

        var tail = events.Take(index).Append(Rebuild(events[index], version: events[index].StateVersionAfter.Next())).ToArray();
        var result = Assert.IsType<ShiftReplaySemanticRejected>(
            Replay.ReplayFrom(Initial(configuration, script), tail, configuration.Shift));
        Assert.Equal(ShiftReplaySemanticFailure.ObservationalVersionMismatch, result.Failure);
        Assert.Equal(index, result.Position);
    }

    [Fact]
    public void A_tail_for_another_shift_fails_closed_before_reduction()
    {
        var (configuration, script, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var otherConfiguration = configuration.Shift with { ShiftId = ShiftId.From("TLAW046_OTHER") };

        var result = Assert.IsType<ShiftReplaySemanticRejected>(
            Replay.ReplayFrom(Initial(configuration, script), run.Journal.Events, otherConfiguration));
        Assert.Equal(ShiftReplaySemanticFailure.ShiftMismatch, result.Failure);
    }

    [Fact]
    public void A_rejected_reduction_publishes_no_partial_snapshot_and_mutates_no_input()
    {
        var (configuration, script, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var initial = Initial(configuration, script);
        var events = run.Journal.Events.ToArray();
        var index = Array.FindIndex(events, envelope => envelope.EventType == HostStageSevenEventTypes.LineNoiseChanged);
        var tampered = events.Take(index).Append(Rebuild(events[index], version: events[index].StateVersionAfter.Next())).ToArray();
        var journalCount = run.Journal.Count;

        var result = Replay.ReplayFrom(initial, tampered, configuration.Shift);

        Assert.IsNotType<ShiftReplaySucceeded>(result);
        Assert.Equal(0, initial.StateVersion.Value);
        Assert.Equal(EventSequence.None, initial.LastEventSequence);
        Assert.All(initial.Logs, log => Assert.Equal(LogState.SCHEDULED, log.State));
        Assert.Equal(journalCount, run.Journal.Count);
    }

    [Fact]
    public void Reduction_consumes_the_supplied_order_and_never_sorts()
    {
        var (configuration, script, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var events = run.Journal.Events.ToArray();
        var sameTick = events.TakeWhile(envelope => envelope.ServerTick == events[0].ServerTick).ToArray();
        Assert.True(sameTick.Length > 1, "the opening tick must publish more than one event for this proof to be non-vacuous");

        var reordered = new[] { sameTick[1], sameTick[0] }.Concat(events.Skip(2)).ToArray();
        Assert.IsNotType<ShiftReplaySucceeded>(Replay.ReplayFrom(Initial(configuration, script), reordered, configuration.Shift));
    }

    // ----- quota -----

    [Fact]
    public void Quota_is_reconstructed_exactly_once_from_accepted_settlement_evidence()
    {
        var (configuration, script, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var live = Live(run);
        var replayed = ReplayAll(configuration, script, run);

        Assert.True(live.Quota.StructurallyEquals(replayed.Quota));
        Assert.Equal(replayed.Quota.SettledLogIds.Length, replayed.Quota.SettledLogIds.Distinct().Count());
        Assert.True(replayed.Quota.TotalCreditedUnits > 0);
        Assert.True(replayed.Quota.CorrectlyProcessedAnomalies > 0);
        Assert.Equal(replayed.Quota.TotalCreditedUnits, replayed.Quota.CreditedBySpecies.Sum(entry => entry.Units));
    }

    [Fact]
    public void Incorrect_penitent_processing_replays_as_data_only_with_no_effect_state()
    {
        var (configuration, script, run) = Execute(FullP0HostScenarioScript.IncorrectPenitent);
        var live = Live(run);
        var replayed = ReplayAll(configuration, script, run);

        Assert.Null(live.FirstDifference(replayed));
        Assert.Equal(live.Quota.CorrectlyProcessedAnomalies, replayed.Quota.CorrectlyProcessedAnomalies);

        // No D-015 effect runtime is reconstructed: the reducer holds no saw-failure or forced-pause state.
        Assert.Null(replayed.LineState.ActiveRepairHold);
        Assert.Equal(live.LineState.State, replayed.LineState.State);
    }

    [Fact]
    public void Incorrect_false_species_declared_credit_replays_exactly_once()
    {
        var (configuration, script, run) = Execute(FullP0HostScenarioScript.IncorrectFalseSpecies);
        var live = Live(run);
        var replayed = ReplayAll(configuration, script, run);

        Assert.True(live.Quota.StructurallyEquals(replayed.Quota));
        Assert.Equal(replayed.Quota.CreditedBySpecies.Length, replayed.Quota.CreditedBySpecies.Select(entry => entry.Species).Distinct().Count());
        Assert.Equal(replayed.Quota.TotalCreditedUnits, replayed.Quota.CreditedBySpecies.Sum(entry => entry.Units));
    }

    [Fact]
    public void Incorrect_resin_processing_replays_without_any_button_lock_state()
    {
        var (configuration, script, run) = Execute(FullP0HostScenarioScript.IncorrectResin);
        var live = Live(run);
        var replayed = ReplayAll(configuration, script, run);

        Assert.Null(live.FirstDifference(replayed));
        Assert.Equal(live.Quota.TotalCreditedUnits, replayed.Quota.TotalCreditedUnits);
    }
}
