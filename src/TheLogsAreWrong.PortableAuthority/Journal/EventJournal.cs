using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Journal;

public enum JournalAppendOutcome
{
    Accepted,
    Duplicate,
    OutOfOrder,
    SequenceGap,
    TickRegression,
    StateVersionRegression,
    StateVersionSkip,
    ShiftMismatch,
    DefaultValue
}

public interface IEventJournal
{
    ShiftId Shift { get; }
    EventSequence LastSequence { get; }
    ServerTick LastTick { get; }
    StateVersion LastStateVersion { get; }
    int Count { get; }
    IReadOnlyList<EventEnvelope> Events { get; }
    void Append(EventEnvelope envelope);
    JournalAppendOutcome TryAppend(EventEnvelope envelope);
}

public sealed class JournalInvariantViolationException : InvalidOperationException
{
    public JournalInvariantViolationException(JournalAppendOutcome outcome)
        : base($"Journal append rejected: {outcome}.")
    {
        Outcome = outcome;
    }

    public JournalAppendOutcome Outcome { get; }
}

public sealed class InMemoryEventJournal : IEventJournal
{
    private readonly List<EventEnvelope> _events = [];
    private readonly IReadOnlyList<EventEnvelope> _readOnlyEvents;

    public InMemoryEventJournal(ShiftId shift)
    {
        if (shift.IsDefault)
        {
            throw new ArgumentException("Journal shift must be initialized.", nameof(shift));
        }

        Shift = shift;
        LastSequence = EventSequence.None;
        LastTick = ServerTick.Zero;
        LastStateVersion = StateVersion.Zero;
        _readOnlyEvents = _events.AsReadOnly();
    }

    public ShiftId Shift { get; }
    public EventSequence LastSequence { get; private set; }
    public ServerTick LastTick { get; private set; }
    public StateVersion LastStateVersion { get; private set; }
    public int Count => _events.Count;
    public IReadOnlyList<EventEnvelope> Events => _readOnlyEvents;

    public void Append(EventEnvelope envelope)
    {
        var outcome = TryAppend(envelope);
        if (outcome != JournalAppendOutcome.Accepted)
        {
            throw new JournalInvariantViolationException(outcome);
        }
    }

    public JournalAppendOutcome TryAppend(EventEnvelope envelope)
    {
        var outcome = Validate(envelope);
        if (outcome != JournalAppendOutcome.Accepted)
        {
            return outcome;
        }

        _events.Add(envelope);
        LastSequence = envelope.Sequence;
        LastTick = envelope.ServerTick;
        LastStateVersion = envelope.StateVersionAfter;
        return JournalAppendOutcome.Accepted;
    }

    private JournalAppendOutcome Validate(EventEnvelope envelope)
    {
        if (HasDefaultField(envelope))
        {
            return JournalAppendOutcome.DefaultValue;
        }

        if (envelope.ShiftId != Shift)
        {
            return JournalAppendOutcome.ShiftMismatch;
        }

        var expectedSequence = LastSequence.Next();
        if (envelope.Sequence < expectedSequence)
        {
            return envelope.Sequence == LastSequence
                ? JournalAppendOutcome.Duplicate
                : JournalAppendOutcome.OutOfOrder;
        }

        if (envelope.Sequence > expectedSequence)
        {
            return JournalAppendOutcome.SequenceGap;
        }

        if (envelope.ServerTick < LastTick)
        {
            return JournalAppendOutcome.TickRegression;
        }

        if (envelope.StateVersionAfter < LastStateVersion)
        {
            return JournalAppendOutcome.StateVersionRegression;
        }

        if (envelope.StateVersionAfter > LastStateVersion && (!LastStateVersion.TryNext(out var nextVersion) || envelope.StateVersionAfter != nextVersion))
        {
            return JournalAppendOutcome.StateVersionSkip;
        }

        return JournalAppendOutcome.Accepted;
    }

    internal static bool HasDefaultField(EventEnvelope envelope) =>
        envelope.ShiftId.IsDefault ||
        envelope.EventId.IsDefault ||
        envelope.Sequence.IsDefault ||
        envelope.ServerTick.IsDefault ||
        envelope.StateVersionAfter.IsDefault ||
        envelope.EventType.IsDefault ||
        envelope.Payload is null;
}
