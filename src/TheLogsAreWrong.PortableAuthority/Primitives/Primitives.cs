using System.Globalization;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Primitives;

public readonly record struct ShiftSeed(int Value);

public readonly record struct EventSequence : IComparable<EventSequence>
{
    private readonly long _value;

    private EventSequence(long value) => _value = value;

    public long Value => _value;
    public bool IsDefault => _value == 0;
    public static EventSequence None => default;
    public static EventSequence First => new(1);

    public static EventSequence From(long value) => TryFrom(value, out var result)
        ? result
        : throw new ArgumentOutOfRangeException(nameof(value), value, "Event sequence cannot be negative.");

    public static bool TryFrom(long value, out EventSequence result)
    {
        if (value < 0)
        {
            result = default;
            return false;
        }

        result = new EventSequence(value);
        return true;
    }

    public EventSequence Next() => new(checked(_value + 1));

    public bool TryNext(out EventSequence result)
    {
        if (_value == long.MaxValue)
        {
            result = default;
            return false;
        }

        result = new EventSequence(_value + 1);
        return true;
    }

    public int CompareTo(EventSequence other) => _value.CompareTo(other._value);
    public static bool operator <(EventSequence left, EventSequence right) => left.CompareTo(right) < 0;
    public static bool operator >(EventSequence left, EventSequence right) => left.CompareTo(right) > 0;
    public static bool operator <=(EventSequence left, EventSequence right) => left.CompareTo(right) <= 0;
    public static bool operator >=(EventSequence left, EventSequence right) => left.CompareTo(right) >= 0;
    public override string ToString() => _value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct StateVersion : IComparable<StateVersion>
{
    private readonly long _value;
    private readonly bool _isInitialized;

    private StateVersion(long value)
    {
        _value = value;
        _isInitialized = true;
    }

    public long Value => _isInitialized ? _value : throw new InvalidOperationException("State version is uninitialized.");
    public bool IsDefault => !_isInitialized;
    public static StateVersion Zero => new(0);

    public static StateVersion From(long value) => TryFrom(value, out var result)
        ? result
        : throw new ArgumentOutOfRangeException(nameof(value), value, "State version cannot be negative.");

    public static bool TryFrom(long value, out StateVersion result)
    {
        if (value < 0)
        {
            result = default;
            return false;
        }

        result = new StateVersion(value);
        return true;
    }

    public StateVersion Next() => new(checked(Value + 1));

    public bool TryNext(out StateVersion result)
    {
        if (IsDefault || _value == long.MaxValue)
        {
            result = default;
            return false;
        }

        result = new StateVersion(_value + 1);
        return true;
    }

    public int CompareTo(StateVersion other)
    {
        EnsureInitialized();
        other.EnsureInitialized();
        return _value.CompareTo(other._value);
    }

    public static bool operator <(StateVersion left, StateVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(StateVersion left, StateVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(StateVersion left, StateVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(StateVersion left, StateVersion right) => left.CompareTo(right) >= 0;
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    private void EnsureInitialized()
    {
        if (IsDefault)
        {
            throw new InvalidOperationException("State version is uninitialized.");
        }
    }
}

public readonly record struct ServerTick : IComparable<ServerTick>
{
    private readonly long _value;
    private readonly bool _isInitialized;

    private ServerTick(long value)
    {
        _value = value;
        _isInitialized = true;
    }

    public long Value => _isInitialized ? _value : throw new InvalidOperationException("Server tick is uninitialized.");
    public bool IsDefault => !_isInitialized;
    public static ServerTick Zero => new(0);

    public static ServerTick From(long value) => TryFrom(value, out var result)
        ? result
        : throw new ArgumentOutOfRangeException(nameof(value), value, "Server tick cannot be negative.");

    public static bool TryFrom(long value, out ServerTick result)
    {
        if (value < 0)
        {
            result = default;
            return false;
        }

        result = new ServerTick(value);
        return true;
    }

    public ServerTick Add(SimulationDuration duration) => this + duration;

    public SimulationDuration Subtract(ServerTick earlier)
    {
        if (!TrySubtract(earlier, out var duration))
        {
            throw new InvalidOperationException("Cannot subtract a later server tick from an earlier server tick.");
        }

        return duration;
    }

    public bool TrySubtract(ServerTick earlier, out SimulationDuration duration)
    {
        EnsureInitialized();
        earlier.EnsureInitialized();
        if (_value < earlier._value)
        {
            duration = default;
            return false;
        }

        duration = SimulationDuration.FromTicks(_value - earlier._value);
        return true;
    }

    public int CompareTo(ServerTick other)
    {
        EnsureInitialized();
        other.EnsureInitialized();
        return _value.CompareTo(other._value);
    }

    public static ServerTick operator +(ServerTick tick, SimulationDuration duration)
    {
        tick.EnsureInitialized();
        return From(checked(tick._value + duration.Value));
    }

    public static SimulationDuration operator -(ServerTick later, ServerTick earlier) => later.Subtract(earlier);
    public static bool operator <(ServerTick left, ServerTick right) => left.CompareTo(right) < 0;
    public static bool operator >(ServerTick left, ServerTick right) => left.CompareTo(right) > 0;
    public static bool operator <=(ServerTick left, ServerTick right) => left.CompareTo(right) <= 0;
    public static bool operator >=(ServerTick left, ServerTick right) => left.CompareTo(right) >= 0;
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    private void EnsureInitialized()
    {
        if (IsDefault)
        {
            throw new InvalidOperationException("Server tick is uninitialized.");
        }
    }
}

public readonly record struct NodeCapacity
{
    public int? Limit { get; }
    public bool IsUnlimited => Limit is null;
    private NodeCapacity(int? limit) => Limit = limit;
    public static NodeCapacity Unlimited { get; } = new(null);
    public static NodeCapacity Limited(int value) => value > 0 ? new NodeCapacity(value) : throw new ArgumentOutOfRangeException(nameof(value));
}
