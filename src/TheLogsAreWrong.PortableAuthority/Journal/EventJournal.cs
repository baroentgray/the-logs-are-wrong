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

/// <summary>
/// Explicit all-or-nothing journal boundary required by authoritative Stage Seven publication.
/// A non-accepted result or thrown pre-commit validation must leave the exposed cursor and events unchanged.
/// </summary>
public interface IAtomicEventJournal : IEventJournal
{
    JournalAppendOutcome TryAppendBatch(IReadOnlyList<EventEnvelope> envelopes);
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

public sealed class InMemoryEventJournal : IAtomicEventJournal
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
        var outcome = Validate(envelope, LastSequence, LastTick, LastStateVersion);
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

    /// <summary>
    /// Validates the complete contiguous append against a staged cursor, then commits all envelopes
    /// together. A rejected or thrown pre-commit validation leaves the exposed journal unchanged.
    /// </summary>
    public JournalAppendOutcome TryAppendBatch(IReadOnlyList<EventEnvelope> envelopes)
    {
        if (envelopes is null)
        {
            throw new ArgumentNullException(nameof(envelopes));
        }

        var sequence = LastSequence;
        var tick = LastTick;
        var stateVersion = LastStateVersion;
        foreach (var envelope in envelopes)
        {
            var outcome = Validate(envelope, sequence, tick, stateVersion);
            if (outcome != JournalAppendOutcome.Accepted)
            {
                return outcome;
            }

            sequence = envelope.Sequence;
            tick = envelope.ServerTick;
            stateVersion = envelope.StateVersionAfter;
        }

        if (envelopes.Count == 0)
        {
            return JournalAppendOutcome.Accepted;
        }

        _events.AddRange(envelopes);
        LastSequence = sequence;
        LastTick = tick;
        LastStateVersion = stateVersion;
        return JournalAppendOutcome.Accepted;
    }

    private JournalAppendOutcome Validate(
        EventEnvelope envelope,
        EventSequence lastSequence,
        ServerTick lastTick,
        StateVersion lastStateVersion)
    {
        if (HasDefaultField(envelope))
        {
            return JournalAppendOutcome.DefaultValue;
        }

        if (envelope.ShiftId != Shift)
        {
            return JournalAppendOutcome.ShiftMismatch;
        }

        var expectedSequence = lastSequence.Next();
        if (envelope.Sequence < expectedSequence)
        {
            return envelope.Sequence == lastSequence
                ? JournalAppendOutcome.Duplicate
                : JournalAppendOutcome.OutOfOrder;
        }

        if (envelope.Sequence > expectedSequence)
        {
            return JournalAppendOutcome.SequenceGap;
        }

        if (envelope.ServerTick < lastTick)
        {
            return JournalAppendOutcome.TickRegression;
        }

        if (envelope.StateVersionAfter < lastStateVersion)
        {
            return JournalAppendOutcome.StateVersionRegression;
        }

        if (envelope.StateVersionAfter > lastStateVersion && (!lastStateVersion.TryNext(out var nextVersion) || envelope.StateVersionAfter != nextVersion))
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
