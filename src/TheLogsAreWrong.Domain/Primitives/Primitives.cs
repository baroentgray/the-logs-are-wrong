namespace TheLogsAreWrong.Domain.Primitives;

public readonly record struct ShiftSeed(int Value);

public readonly record struct EventSequence : IComparable<EventSequence>
{
    public long Value { get; }
    private EventSequence(long value) => Value = value;
    public static EventSequence From(long value) => value >= 0 ? new EventSequence(value) : throw new ArgumentOutOfRangeException(nameof(value));
    public EventSequence Next() => new(checked(Value + 1));
    public int CompareTo(EventSequence other) => Value.CompareTo(other.Value);
    public static bool operator <(EventSequence left, EventSequence right) => left.CompareTo(right) < 0;
    public static bool operator >(EventSequence left, EventSequence right) => left.CompareTo(right) > 0;
    public static bool operator <=(EventSequence left, EventSequence right) => left.CompareTo(right) <= 0;
    public static bool operator >=(EventSequence left, EventSequence right) => left.CompareTo(right) >= 0;
}

public readonly record struct StateVersion : IComparable<StateVersion>
{
    public long Value { get; }
    private StateVersion(long value) => Value = value;
    public static StateVersion From(long value) => value >= 0 ? new StateVersion(value) : throw new ArgumentOutOfRangeException(nameof(value));
    public StateVersion Next() => new(checked(Value + 1));
    public int CompareTo(StateVersion other) => Value.CompareTo(other.Value);
    public static bool operator <(StateVersion left, StateVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(StateVersion left, StateVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(StateVersion left, StateVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(StateVersion left, StateVersion right) => left.CompareTo(right) >= 0;
}

public readonly record struct ServerTick : IComparable<ServerTick>
{
    public long Value { get; }
    private ServerTick(long value) => Value = value;
    public static ServerTick From(long value) => value >= 0 ? new ServerTick(value) : throw new ArgumentOutOfRangeException(nameof(value));
    public int CompareTo(ServerTick other) => Value.CompareTo(other.Value);
    public static bool operator <(ServerTick left, ServerTick right) => left.CompareTo(right) < 0;
    public static bool operator >(ServerTick left, ServerTick right) => left.CompareTo(right) > 0;
    public static bool operator <=(ServerTick left, ServerTick right) => left.CompareTo(right) <= 0;
    public static bool operator >=(ServerTick left, ServerTick right) => left.CompareTo(right) >= 0;
}

public readonly record struct NodeCapacity
{
    public int? Limit { get; }
    public bool IsUnlimited => Limit is null;
    private NodeCapacity(int? limit) => Limit = limit;
    public static NodeCapacity Unlimited { get; } = new(null);
    public static NodeCapacity Limited(int value) => value > 0 ? new NodeCapacity(value) : throw new ArgumentOutOfRangeException(nameof(value));
}
