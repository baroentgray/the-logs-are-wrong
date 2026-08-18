using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Journal;

public sealed class DomainEventDraft
{
    public DomainEventDraft(EventId eventId, EventTypeId eventType, IDomainEventPayload payload, IntentId? causedByIntentId = null)
    {
        if (eventId.IsDefault)
        {
            throw new ArgumentException("Event identity must be initialized.", nameof(eventId));
        }

        if (eventType.IsDefault)
        {
            throw new ArgumentException("Event type must be initialized.", nameof(eventType));
        }

        if (payload is null) { throw new ArgumentNullException("payload"); }
        if (causedByIntentId is { } intentId && intentId.IsDefault)
        {
            throw new ArgumentException("Causation intent must be initialized when present.", nameof(causedByIntentId));
        }

        EventId = eventId;
        EventType = eventType;
        Payload = payload;
        CausedByIntentId = causedByIntentId;
    }

    public EventId EventId { get; }
    public EventTypeId EventType { get; }
    public IDomainEventPayload Payload { get; }
    public IntentId? CausedByIntentId { get; }
}

public abstract record JournaledMutationCommitResult(ShiftRuntimeState Before, ShiftRuntimeState After);

public sealed record JournaledMutationCommitted(ShiftRuntimeState Before, ShiftRuntimeState After, EventEnvelope Envelope)
    : JournaledMutationCommitResult(Before, After);

public sealed record JournaledMutationCommitRejected(
    ShiftRuntimeState Before,
    ShiftRuntimeState After,
    JournaledMutationCommitRejectionReason Reason)
    : JournaledMutationCommitResult(Before, After);

public sealed record JournaledMutationAppendRejected(
    ShiftRuntimeState Before,
    ShiftRuntimeState After,
    JournalAppendOutcome Outcome)
    : JournaledMutationCommitResult(Before, After);

public sealed record JournaledMutationCommitFailed(
    ShiftRuntimeState Before,
    ShiftRuntimeState After,
    JournaledMutationCommitFailureReason Reason)
    : JournaledMutationCommitResult(Before, After);

public enum JournaledMutationCommitRejectionReason
{
    RuntimeIdentityMismatch,
    JournalRuntimeCursorMismatch,
    StateVersionNotNext,
    TickUninitialized,
    TickRegression
}

public enum JournaledMutationCommitFailureReason
{
    StateVersionOverflow,
    SequenceOverflow,
    JournalInvariantInvalid
}

public sealed class JournaledMutationCommitService
{
    public JournaledMutationCommitResult Commit(
        IEventJournal journal,
        ShiftRuntimeState before,
        ShiftRuntimeState after,
        ServerTick currentTick,
        DomainEventDraft draft)
    {
        if (journal is null) { throw new ArgumentNullException("journal"); }
        if (before is null) { throw new ArgumentNullException("before"); }
        if (after is null) { throw new ArgumentNullException("after"); }
        if (draft is null) { throw new ArgumentNullException("draft"); }

        if (before.ShiftId.IsDefault || after.ShiftId.IsDefault || before.ShiftId != after.ShiftId || before.ShiftSeed != after.ShiftSeed)
        {
            return new JournaledMutationCommitRejected(before, after, JournaledMutationCommitRejectionReason.RuntimeIdentityMismatch);
        }

        if (currentTick.IsDefault)
        {
            return new JournaledMutationCommitRejected(before, after, JournaledMutationCommitRejectionReason.TickUninitialized);
        }

        if (journal.Shift.IsDefault || journal.LastStateVersion.IsDefault || journal.LastTick.IsDefault)
        {
            return new JournaledMutationCommitFailed(before, after, JournaledMutationCommitFailureReason.JournalInvariantInvalid);
        }

        if (journal.Shift != before.ShiftId)
        {
            return new JournaledMutationCommitRejected(before, after, JournaledMutationCommitRejectionReason.JournalRuntimeCursorMismatch);
        }

        if (journal.LastStateVersion != before.StateVersion)
        {
            return new JournaledMutationCommitRejected(before, after, JournaledMutationCommitRejectionReason.JournalRuntimeCursorMismatch);
        }

        if (before.StateVersion.IsDefault || after.StateVersion.IsDefault)
        {
            return new JournaledMutationCommitFailed(before, after, JournaledMutationCommitFailureReason.JournalInvariantInvalid);
        }

        if (!before.StateVersion.TryNext(out var expectedVersion))
        {
            return new JournaledMutationCommitFailed(before, after, JournaledMutationCommitFailureReason.StateVersionOverflow);
        }

        if (after.StateVersion != expectedVersion)
        {
            return new JournaledMutationCommitRejected(before, after, JournaledMutationCommitRejectionReason.StateVersionNotNext);
        }

        if (currentTick < journal.LastTick)
        {
            return new JournaledMutationCommitRejected(before, after, JournaledMutationCommitRejectionReason.TickRegression);
        }

        if (!journal.LastSequence.TryNext(out var nextSequence))
        {
            return new JournaledMutationCommitFailed(before, after, JournaledMutationCommitFailureReason.SequenceOverflow);
        }

        var envelope = new EventEnvelope
        {
            ShiftId = before.ShiftId,
            EventId = draft.EventId,
            Sequence = nextSequence,
            CausedByIntentId = draft.CausedByIntentId,
            ServerTick = currentTick,
            StateVersionAfter = after.StateVersion,
            EventType = draft.EventType,
            Payload = draft.Payload
        };

        var outcome = journal.TryAppend(envelope);
        return outcome == JournalAppendOutcome.Accepted
            ? new JournaledMutationCommitted(before, after, envelope)
            : new JournaledMutationAppendRejected(before, after, outcome);
    }
}

public sealed class RuntimeCheckpointFactory
{
    public SnapshotBoundary Capture(ShiftRuntimeState runtime, IEventJournal journal, ServerTick currentTick)
    {
        if (runtime is null) { throw new ArgumentNullException("runtime"); }
        if (journal is null) { throw new ArgumentNullException("journal"); }
        if (currentTick.IsDefault)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Checkpoint tick must be initialized.");
        }

        if (journal.Shift.IsDefault || journal.Shift != runtime.ShiftId)
        {
            throw new ArgumentException("Journal shift must match runtime shift.", nameof(journal));
        }

        if (journal.LastStateVersion.IsDefault || journal.LastStateVersion != runtime.StateVersion)
        {
            throw new ArgumentException("Journal state version must match runtime state version.", nameof(journal));
        }

        if (journal.LastTick.IsDefault || currentTick < journal.LastTick)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Checkpoint tick cannot precede the journal cursor.");
        }

        return new SnapshotBoundary
        {
            ShiftId = runtime.ShiftId,
            ServerTick = currentTick,
            StateVersion = runtime.StateVersion,
            LastEventSequence = journal.LastSequence
        };
    }
}
