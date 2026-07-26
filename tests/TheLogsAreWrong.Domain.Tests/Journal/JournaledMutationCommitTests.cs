using System.Reflection;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Tests.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Journal;

[Trait("Scope", "TLAW-011")]
public sealed class JournaledMutationCommitTests
{
    private static readonly JournaledMutationCommitService Commit = new();
    private static readonly RuntimeCheckpointFactory Checkpoints = new();

    [Fact]
    public void Draft_requires_exact_identity_type_payload_and_optional_initialized_causation_only()
    {
        var payload = new CommitPayload("draft");
        var intent = IntentId.From("intent_1");
        var draft = new DomainEventDraft(EventId.From("event_1"), EventTypeId.From("LOG_MOVED"), payload, intent);

        Assert.Equal(EventId.From("event_1"), draft.EventId);
        Assert.Equal(EventTypeId.From("LOG_MOVED"), draft.EventType);
        Assert.Same(payload, draft.Payload);
        Assert.Equal(intent, draft.CausedByIntentId);
        Assert.Throws<ArgumentException>(() => new DomainEventDraft(default, EventTypeId.From("LOG_MOVED"), payload));
        Assert.Throws<ArgumentException>(() => new DomainEventDraft(EventId.From("event_1"), default, payload));
        Assert.Throws<ArgumentNullException>(() => new DomainEventDraft(EventId.From("event_1"), EventTypeId.From("LOG_MOVED"), null!));
        Assert.Throws<ArgumentException>(() => new DomainEventDraft(EventId.From("event_1"), EventTypeId.From("LOG_MOVED"), payload, default(IntentId)));

        var properties = typeof(DomainEventDraft).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(new[] { "EventId", "EventType", "Payload", "CausedByIntentId" }, properties.Select(static property => property.Name));
        Assert.All(properties, property => Assert.False(property.CanWrite));
        var parameterTypes = typeof(DomainEventDraft).GetConstructors().Single().GetParameters().Select(static parameter => parameter.ParameterType);
        Assert.DoesNotContain(typeof(ShiftId), parameterTypes);
        Assert.DoesNotContain(typeof(EventSequence), parameterTypes);
        Assert.DoesNotContain(typeof(ServerTick), parameterTypes);
        Assert.DoesNotContain(typeof(StateVersion), parameterTypes);
    }

    [Fact]
    public void First_and_repeated_real_log_mutations_commit_exact_derived_envelopes_with_same_tick_sequences()
    {
        var before = RuntimeFixture.CreateInitialState();
        var firstAfter = RuntimeFixture.MoveHost(before, "log_01", LogState.AT_FEED_GATE);
        var journal = new InMemoryEventJournal(before.ShiftId);
        var payload = new CommitPayload("first");
        var first = Assert.IsType<JournaledMutationCommitted>(Commit.Commit(
            journal,
            before,
            firstAfter,
            ServerTick.From(5),
            new DomainEventDraft(EventId.From("event_1"), EventTypeId.From("LOG_MOVED"), payload, IntentId.From("intent_1"))));

        Assert.Same(before, first.Before);
        Assert.Same(firstAfter, first.After);
        Assert.Equal(before.ShiftId, first.Envelope.ShiftId);
        Assert.Equal(EventId.From("event_1"), first.Envelope.EventId);
        Assert.Equal(EventSequence.First, first.Envelope.Sequence);
        Assert.Equal(IntentId.From("intent_1"), first.Envelope.CausedByIntentId);
        Assert.Equal(ServerTick.From(5), first.Envelope.ServerTick);
        Assert.Equal(StateVersion.From(1), first.Envelope.StateVersionAfter);
        Assert.Equal(EventTypeId.From("LOG_MOVED"), first.Envelope.EventType);
        Assert.Same(payload, first.Envelope.Payload);

        var secondAfter = RuntimeFixture.MoveHost(first.After, "log_01", LogState.AT_INTAKE);
        var second = Assert.IsType<JournaledMutationCommitted>(Commit.Commit(
            journal,
            first.After,
            secondAfter,
            ServerTick.From(5),
            Draft("event_2")));

        Assert.Equal(EventSequence.From(2), second.Envelope.Sequence);
        Assert.Equal(StateVersion.From(2), second.Envelope.StateVersionAfter);
        Assert.Equal(ServerTick.From(5), second.Envelope.ServerTick);
        Assert.Equal(2, journal.Count);
        Assert.Equal(EventSequence.From(2), journal.LastSequence);
        Assert.Equal(ServerTick.From(5), journal.LastTick);
        Assert.Equal(StateVersion.From(2), journal.LastStateVersion);
    }

    [Fact]
    public void Later_tick_commit_advances_only_the_journal_cursor_once()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var firstAfter = RuntimeFixture.MoveHost(initial, "log_01", LogState.AT_FEED_GATE);
        var journal = new InMemoryEventJournal(initial.ShiftId);
        var first = Assert.IsType<JournaledMutationCommitted>(Commit.Commit(journal, initial, firstAfter, ServerTick.From(3), Draft("event_1")));
        var secondAfter = RuntimeFixture.MoveHost(first.After, "log_01", LogState.AT_INTAKE);

        var second = Assert.IsType<JournaledMutationCommitted>(Commit.Commit(journal, first.After, secondAfter, ServerTick.From(9), Draft("event_2")));

        Assert.Equal((EventSequence.From(2), ServerTick.From(9), StateVersion.From(2), 2), (second.Envelope.Sequence, journal.LastTick, journal.LastStateVersion, journal.Count));
    }

    [Fact]
    public void Shift_and_seed_identity_mismatches_reject_without_append()
    {
        var fixture = Fixture.LoadP0();
        var before = RuntimeFixture.CreateInitialState();
        var journal = new InMemoryEventJournal(before.ShiftId);
        var differentShift = ShiftRuntimeState.Create(fixture.Shift with { ShiftId = ShiftId.From("P0_SHIFT_B") });
        var differentSeed = ShiftRuntimeState.Create(fixture.Shift with { Seed = new ShiftSeed(999) });

        var shiftRejected = Assert.IsType<JournaledMutationCommitRejected>(Commit.Commit(journal, before, differentShift, ServerTick.Zero, Draft("event_1")));
        var seedRejected = Assert.IsType<JournaledMutationCommitRejected>(Commit.Commit(journal, before, differentSeed, ServerTick.Zero, Draft("event_2")));

        Assert.Equal(JournaledMutationCommitRejectionReason.RuntimeIdentityMismatch, shiftRejected.Reason);
        Assert.Equal(JournaledMutationCommitRejectionReason.RuntimeIdentityMismatch, seedRejected.Reason);
        Assert.Same(before, shiftRejected.Before);
        Assert.Same(differentShift, shiftRejected.After);
        Assert.Equal((0, EventSequence.None, ServerTick.Zero, StateVersion.Zero), (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion));
    }

    [Fact]
    public void Stale_or_ahead_journal_cursor_rejects_without_mutating_any_cursor()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var before = RuntimeFixture.MoveHost(initial, "log_01", LogState.AT_FEED_GATE);
        var after = RuntimeFixture.MoveHost(before, "log_01", LogState.AT_INTAKE);
        var stale = new InMemoryEventJournal(before.ShiftId);
        var otherShift = new InMemoryEventJournal(ShiftId.From("P0_SHIFT_B"));
        var ahead = new InMemoryEventJournal(before.ShiftId);
        Append(ahead, before.ShiftId, 1, 1, 1);
        Append(ahead, before.ShiftId, 2, 1, 2);

        var staleRejected = Assert.IsType<JournaledMutationCommitRejected>(Commit.Commit(stale, before, after, ServerTick.From(2), Draft("event_1")));
        var otherShiftRejected = Assert.IsType<JournaledMutationCommitRejected>(Commit.Commit(otherShift, before, after, ServerTick.From(2), Draft("event_2")));
        var aheadRejected = Assert.IsType<JournaledMutationCommitRejected>(Commit.Commit(ahead, before, after, ServerTick.From(2), Draft("event_3")));

        Assert.Equal(JournaledMutationCommitRejectionReason.JournalRuntimeCursorMismatch, staleRejected.Reason);
        Assert.Equal(JournaledMutationCommitRejectionReason.JournalRuntimeCursorMismatch, otherShiftRejected.Reason);
        Assert.Equal(JournaledMutationCommitRejectionReason.JournalRuntimeCursorMismatch, aheadRejected.Reason);
        Assert.Equal((0, EventSequence.None, ServerTick.Zero, StateVersion.Zero), (stale.Count, stale.LastSequence, stale.LastTick, stale.LastStateVersion));
        Assert.Equal((0, EventSequence.None, ServerTick.Zero, StateVersion.Zero), (otherShift.Count, otherShift.LastSequence, otherShift.LastTick, otherShift.LastStateVersion));
        Assert.Equal((2, EventSequence.From(2), ServerTick.From(1), StateVersion.From(2)), (ahead.Count, ahead.LastSequence, ahead.LastTick, ahead.LastStateVersion));
    }

    [Fact]
    public void Same_or_skipped_after_version_and_tick_regression_are_rejected_purely()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var journal = new InMemoryEventJournal(initial.ShiftId);
        var sameVersion = Assert.IsType<JournaledMutationCommitRejected>(Commit.Commit(journal, initial, initial, ServerTick.Zero, Draft("event_1")));
        var skipped = RuntimeFixture.MoveHost(RuntimeFixture.MoveHost(initial, "log_01", LogState.AT_FEED_GATE), "log_01", LogState.AT_INTAKE);
        var skippedVersion = Assert.IsType<JournaledMutationCommitRejected>(Commit.Commit(journal, initial, skipped, ServerTick.Zero, Draft("event_2")));

        var before = RuntimeFixture.MoveHost(initial, "log_02", LogState.AT_FEED_GATE);
        var after = RuntimeFixture.MoveHost(before, "log_02", LogState.AT_INTAKE);
        var tickJournal = new InMemoryEventJournal(before.ShiftId);
        Append(tickJournal, before.ShiftId, 1, 10, 1);
        var tickRejected = Assert.IsType<JournaledMutationCommitRejected>(Commit.Commit(tickJournal, before, after, ServerTick.From(9), Draft("event_3")));

        Assert.Equal(JournaledMutationCommitRejectionReason.StateVersionNotNext, sameVersion.Reason);
        Assert.Equal(JournaledMutationCommitRejectionReason.StateVersionNotNext, skippedVersion.Reason);
        Assert.Equal(JournaledMutationCommitRejectionReason.TickRegression, tickRejected.Reason);
        Assert.Equal((0, EventSequence.None, ServerTick.Zero, StateVersion.Zero), (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion));
        Assert.Equal((1, EventSequence.First, ServerTick.From(10), StateVersion.From(1)), (tickJournal.Count, tickJournal.LastSequence, tickJournal.LastTick, tickJournal.LastStateVersion));
    }

    [Fact]
    public void Uninitialized_tick_and_sequence_overflow_fail_without_append()
    {
        var before = RuntimeFixture.CreateInitialState();
        var after = RuntimeFixture.MoveHost(before, "log_01", LogState.AT_FEED_GATE);
        var journal = new InMemoryEventJournal(before.ShiftId);
        var tickRejected = Assert.IsType<JournaledMutationCommitRejected>(Commit.Commit(journal, before, after, default, Draft("event_1")));
        var overflow = new OverflowJournal(before.ShiftId, before.StateVersion);
        var overflowFailed = Assert.IsType<JournaledMutationCommitFailed>(Commit.Commit(overflow, before, after, ServerTick.Zero, Draft("event_2")));

        Assert.Equal(JournaledMutationCommitRejectionReason.TickUninitialized, tickRejected.Reason);
        Assert.Equal(JournaledMutationCommitFailureReason.SequenceOverflow, overflowFailed.Reason);
        Assert.False(overflow.TryAppendCalled);
        Assert.Equal(0, journal.Count);
    }

    [Fact]
    public void Underlying_append_rejection_is_preserved_without_fabricated_envelope_or_input_mutation()
    {
        var before = RuntimeFixture.CreateInitialState();
        var after = RuntimeFixture.MoveHost(before, "log_01", LogState.AT_FEED_GATE);
        var payload = new CommitPayload("immutable");
        var draft = new DomainEventDraft(EventId.From("event_1"), EventTypeId.From("LOG_MOVED"), payload);
        var journal = new RejectingJournal(before.ShiftId, JournalAppendOutcome.SequenceGap);

        var rejected = Assert.IsType<JournaledMutationAppendRejected>(Commit.Commit(journal, before, after, ServerTick.Zero, draft));

        Assert.Equal(JournalAppendOutcome.SequenceGap, rejected.Outcome);
        Assert.Same(before, rejected.Before);
        Assert.Same(after, rejected.After);
        Assert.Equal(EventId.From("event_1"), draft.EventId);
        Assert.Same(payload, draft.Payload);
        Assert.True(before.ValueEquals(RuntimeFixture.CreateInitialState()));
        Assert.Equal((0, EventSequence.None, ServerTick.Zero, StateVersion.Zero), (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion));
    }

    [Fact]
    public void Initial_and_subsequent_checkpoint_capture_exact_aligned_runtime_and_journal_values()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var journal = new InMemoryEventJournal(initial.ShiftId);
        var initialBoundary = Checkpoints.Capture(initial, journal, ServerTick.Zero);

        Assert.Equal((initial.ShiftId, ServerTick.Zero, StateVersion.Zero, EventSequence.None), (initialBoundary.ShiftId, initialBoundary.ServerTick, initialBoundary.StateVersion, initialBoundary.LastEventSequence));

        var after = RuntimeFixture.MoveHost(initial, "log_01", LogState.AT_FEED_GATE);
        Assert.IsType<JournaledMutationCommitted>(Commit.Commit(journal, initial, after, ServerTick.From(7), Draft("event_1")));
        var boundary = Checkpoints.Capture(after, journal, ServerTick.From(7));

        Assert.Equal((after.ShiftId, ServerTick.From(7), StateVersion.From(1), EventSequence.First), (boundary.ShiftId, boundary.ServerTick, boundary.StateVersion, boundary.LastEventSequence));
        Assert.Equal(1, journal.Count);
    }

    [Fact]
    public void Checkpoint_mismatches_and_tick_regression_reject_without_mutating_runtime_or_journal()
    {
        var fixture = Fixture.LoadP0();
        var state = RuntimeFixture.CreateInitialState();
        var journal = new InMemoryEventJournal(state.ShiftId);
        var differentShift = new InMemoryEventJournal(ShiftId.From("P0_SHIFT_B"));
        var ahead = new InMemoryEventJournal(state.ShiftId);
        Append(ahead, state.ShiftId, 1, 5, 1);
        var beforeState = state;

        Assert.Throws<ArgumentException>(() => Checkpoints.Capture(state, differentShift, ServerTick.Zero));
        Assert.Throws<ArgumentException>(() => Checkpoints.Capture(state, ahead, ServerTick.From(5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Checkpoints.Capture(ShiftRuntimeState.Create(fixture.Shift), journal, default));
        Assert.True(state.ValueEquals(beforeState));
        Assert.Equal((0, EventSequence.None, ServerTick.Zero, StateVersion.Zero), (journal.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion));
        Assert.Equal((1, EventSequence.First, ServerTick.From(5), StateVersion.From(1)), (ahead.Count, ahead.LastSequence, ahead.LastTick, ahead.LastStateVersion));
    }

    [Fact]
    public void Captured_checkpoint_reuses_replay_validator_for_valid_tail_and_existing_anomalies()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var journal = new InMemoryEventJournal(initial.ShiftId);
        var firstAfter = RuntimeFixture.MoveHost(initial, "log_01", LogState.AT_FEED_GATE);
        Assert.IsType<JournaledMutationCommitted>(Commit.Commit(journal, initial, firstAfter, ServerTick.From(4), Draft("event_1")));
        var boundary = Checkpoints.Capture(firstAfter, journal, ServerTick.From(4));
        var secondAfter = RuntimeFixture.MoveHost(firstAfter, "log_01", LogState.AT_INTAKE);
        var second = Assert.IsType<JournaledMutationCommitted>(Commit.Commit(journal, firstAfter, secondAfter, ServerTick.From(6), Draft("event_2")));
        var validator = new ReplayValidator();

        Assert.True(validator.Validate(boundary, new[] { second.Envelope }).IsValid);
        Assert.Equal(ReplayAnomaly.DuplicateOfSnapshot, validator.Validate(boundary, new[] { second.Envelope with { Sequence = EventSequence.First } }).Anomaly);
        Assert.Equal(ReplayAnomaly.GapAfterSnapshot, validator.Validate(boundary, new[] { second.Envelope with { Sequence = EventSequence.From(3) } }).Anomaly);
        Assert.Equal(ReplayAnomaly.TickRegression, validator.Validate(boundary, new[] { second.Envelope with { ServerTick = ServerTick.From(3) } }).Anomaly);
        Assert.Equal(ReplayAnomaly.StateVersionSkip, validator.Validate(boundary, new[] { second.Envelope with { StateVersionAfter = StateVersion.From(3) } }).Anomaly);
    }

    [Fact]
    public void Real_line_jam_mutation_commits_through_the_same_generic_boundary_as_log_transitions()
    {
        var state = RuntimeFixture.CreateInitialState();
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.AT_INTAKE);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
        var journal = AlignedJournal(state, ServerTick.From(2));
        var entered = Assert.IsType<LineJamEntered>(new LineJamEntryService().Enter(state, JamCause.FEED_GATE_BLOCKED, ServerTick.From(6)));

        var committed = Assert.IsType<JournaledMutationCommitted>(Commit.Commit(journal, state, entered.State, ServerTick.From(6), Draft("event_4")));

        Assert.Equal(EventSequence.From(4), committed.Envelope.Sequence);
        Assert.Equal(StateVersion.From(4), committed.Envelope.StateVersionAfter);
        Assert.Equal(LineState.LINE_JAMMED, committed.After.Line.State);
        Assert.Equal(4, journal.Count);
    }

    private static DomainEventDraft Draft(string eventId) => new(EventId.From(eventId), EventTypeId.From("STATE_CHANGED"), new CommitPayload(eventId));

    private static void Append(IEventJournal journal, ShiftId shift, long sequence, long tick, long version) =>
        journal.Append(new EventEnvelope
        {
            ShiftId = shift,
            EventId = EventId.From($"seed_{sequence}"),
            Sequence = EventSequence.From(sequence),
            ServerTick = ServerTick.From(tick),
            StateVersionAfter = StateVersion.From(version),
            EventType = EventTypeId.From("SEED"),
            Payload = new CommitPayload($"seed_{sequence}")
        });

    private static InMemoryEventJournal AlignedJournal(ShiftRuntimeState state, ServerTick tick)
    {
        var journal = new InMemoryEventJournal(state.ShiftId);
        for (var version = 1L; version <= state.StateVersion.Value; version++)
        {
            Append(journal, state.ShiftId, version, tick.Value, version);
        }

        return journal;
    }

    private sealed record CommitPayload(string Value) : IDomainEventPayload;

    private sealed class RejectingJournal : IEventJournal
    {
        public RejectingJournal(ShiftId shift, JournalAppendOutcome outcome)
        {
            Shift = shift;
            Outcome = outcome;
        }

        public ShiftId Shift { get; }
        public JournalAppendOutcome Outcome { get; }
        public EventSequence LastSequence => EventSequence.None;
        public ServerTick LastTick => ServerTick.Zero;
        public StateVersion LastStateVersion => StateVersion.Zero;
        public int Count => 0;
        public IReadOnlyList<EventEnvelope> Events => Array.Empty<EventEnvelope>();
        public void Append(EventEnvelope envelope) => throw new JournalInvariantViolationException(Outcome);
        public JournalAppendOutcome TryAppend(EventEnvelope envelope) => Outcome;
    }

    private sealed class OverflowJournal : IEventJournal
    {
        public OverflowJournal(ShiftId shift, StateVersion stateVersion)
        {
            Shift = shift;
            LastStateVersion = stateVersion;
        }

        public ShiftId Shift { get; }
        public EventSequence LastSequence => EventSequence.From(long.MaxValue);
        public ServerTick LastTick => ServerTick.Zero;
        public StateVersion LastStateVersion { get; }
        public int Count => 0;
        public IReadOnlyList<EventEnvelope> Events => Array.Empty<EventEnvelope>();
        public bool TryAppendCalled { get; private set; }
        public void Append(EventEnvelope envelope) => throw new InvalidOperationException();
        public JournalAppendOutcome TryAppend(EventEnvelope envelope)
        {
            TryAppendCalled = true;
            return JournalAppendOutcome.SequenceGap;
        }
    }
}
