using System.Globalization;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>
/// Exact non-negative elapsed-time evidence in milliseconds for authoritative cadence only.
/// </summary>
public readonly record struct AuthoritativeElapsedMilliseconds : IComparable<AuthoritativeElapsedMilliseconds>
{
    private readonly long _value;
    private readonly bool _isInitialized;

    private AuthoritativeElapsedMilliseconds(long value)
    {
        _value = value;
        _isInitialized = true;
    }

    public long Value => _isInitialized ? _value : throw new InvalidOperationException("Authoritative elapsed milliseconds are uninitialized.");
    public bool IsDefault => !_isInitialized;
    public static AuthoritativeElapsedMilliseconds Zero => new(0);

    public static AuthoritativeElapsedMilliseconds FromMilliseconds(long value) => TryFromMilliseconds(value, out var result)
        ? result
        : throw new ArgumentOutOfRangeException(nameof(value), value, "Authoritative elapsed milliseconds cannot be negative.");

    public static bool TryFromMilliseconds(long value, out AuthoritativeElapsedMilliseconds result)
    {
        if (value < 0)
        {
            result = default;
            return false;
        }

        result = new AuthoritativeElapsedMilliseconds(value);
        return true;
    }

    public int CompareTo(AuthoritativeElapsedMilliseconds other)
    {
        EnsureInitialized();
        other.EnsureInitialized();
        return _value.CompareTo(other._value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    private void EnsureInitialized()
    {
        if (IsDefault)
        {
            throw new InvalidOperationException("Authoritative elapsed milliseconds are uninitialized.");
        }
    }
}

/// <summary>
/// A compact inclusive range of due authoritative ticks. Count represents backlog without allocating one item per tick.
/// </summary>
public readonly record struct DueServerTickRange
{
    public DueServerTickRange(ServerTick first, long count)
    {
        if (first.IsDefault)
        {
            throw new ArgumentException("The first due tick must be initialized.", nameof(first));
        }

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "A due range must contain at least one tick.");
        }

        First = first;
        Count = count;
        Last = first + SimulationDuration.FromTicks(checked(count - 1));
    }

    public ServerTick First { get; }
    public ServerTick Last { get; }
    public long Count { get; }
}

/// <summary>
/// Plain-C# exact-integer cadence for deciding which one-second authoritative ticks are due.
/// It owns only elapsed remainder, due backlog, and the next due tick cursor.
/// </summary>
public sealed class HostTickCadence
{
    public const long MillisecondsPerServerTick = 1000;

    private long _remainderMilliseconds;
    private long _dueTickCount;
    private ServerTick _nextDueTick;
    private bool _serverTickRangeExhausted;

    public HostTickCadence()
        : this(ServerTick.Zero)
    {
    }

    public HostTickCadence(ServerTick firstDueTick)
    {
        if (firstDueTick.IsDefault)
        {
            throw new ArgumentException("The first due tick must be initialized.", nameof(firstDueTick));
        }

        _nextDueTick = firstDueTick;
    }

    public AuthoritativeElapsedMilliseconds RemainderMilliseconds => AuthoritativeElapsedMilliseconds.FromMilliseconds(_remainderMilliseconds);
    public long DueTickCount => _dueTickCount;

    /// <summary>
    /// Accumulates exact elapsed evidence and makes every newly due tick visible in the retained backlog.
    /// </summary>
    public void Accumulate(AuthoritativeElapsedMilliseconds elapsed)
    {
        var totalMilliseconds = checked(_remainderMilliseconds + elapsed.Value);
        var generatedTickCount = totalMilliseconds / MillisecondsPerServerTick;
        var remainderMilliseconds = totalMilliseconds % MillisecondsPerServerTick;
        var dueTickCount = checked(_dueTickCount + generatedTickCount);

        if (generatedTickCount > 0 && _serverTickRangeExhausted)
        {
            throw new OverflowException("The server-tick range is exhausted.");
        }

        if (dueTickCount > 0)
        {
            _ = new DueServerTickRange(_nextDueTick, dueTickCount);
        }

        _remainderMilliseconds = remainderMilliseconds;
        _dueTickCount = dueTickCount;
    }

    /// <summary>
    /// Returns the complete pending due range without materializing one element per due tick.
    /// </summary>
    public DueServerTickRange? GetDueTickRange() => _dueTickCount == 0
        ? null
        : new DueServerTickRange(_nextDueTick, _dueTickCount);

    public bool TryGetDueTicks(out DueServerTickRange dueTicks)
    {
        var range = GetDueTickRange();
        if (range is null)
        {
            dueTicks = default;
            return false;
        }

        dueTicks = range.Value;
        return true;
    }

    /// <summary>
    /// Explicitly acknowledges exactly the next due tick. No accumulated backlog is retired implicitly.
    /// </summary>
    public ServerTick RetireNextDueTick()
    {
        if (_dueTickCount == 0)
        {
            throw new InvalidOperationException("No authoritative tick is due.");
        }

        var retiredTick = _nextDueTick;
        var remainingDueTickCount = _dueTickCount - 1;
        var rangeExhausted = _serverTickRangeExhausted;
        var nextDueTick = _nextDueTick;
        if (remainingDueTickCount > 0 || retiredTick.Value < long.MaxValue)
        {
            nextDueTick = retiredTick + SimulationDuration.FromTicks(1);
        }
        else
        {
            rangeExhausted = true;
        }

        _nextDueTick = nextDueTick;
        _dueTickCount = remainingDueTickCount;
        _serverTickRangeExhausted = rangeExhausted;
        return retiredTick;
    }
}
