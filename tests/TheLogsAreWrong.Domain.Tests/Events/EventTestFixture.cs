using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Tests;

internal sealed record TestEventPayload(string Name) : IDomainEventPayload;

internal static class EventTestFixture
{
    internal static ShiftId Shift => ShiftId.From("P0_SHIFT_A");

    internal static EventEnvelope Event(long sequence, long tick = 0, long stateVersion = 0, ShiftId? shift = null) => new()
    {
        ShiftId = shift ?? Shift,
        EventId = EventId.From($"event_{sequence}"),
        Sequence = EventSequence.From(sequence),
        ServerTick = ServerTick.From(tick),
        StateVersionAfter = StateVersion.From(stateVersion),
        EventType = EventTypeId.From("TEST_EVENT"),
        Payload = new TestEventPayload($"payload_{sequence}")
    };

    internal static SnapshotBoundary Boundary(long lastSequence = 0, long tick = 0, long stateVersion = 0) => new()
    {
        ShiftId = Shift,
        ServerTick = ServerTick.From(tick),
        StateVersion = StateVersion.From(stateVersion),
        LastEventSequence = lastSequence == 0 ? EventSequence.None : EventSequence.From(lastSequence)
    };
}
