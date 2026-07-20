using System.Globalization;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Time;

public readonly record struct SimulationDuration : IComparable<SimulationDuration>
{
    private readonly long _ticks;
    private readonly bool _isInitialized;

    private SimulationDuration(long ticks)
    {
        _ticks = ticks;
        _isInitialized = true;
    }

    public long Value => _isInitialized ? _ticks : throw new InvalidOperationException("Simulation duration is uninitialized.");
    public bool IsDefault => !_isInitialized;
    public static SimulationDuration Zero => new(0);

    public static SimulationDuration FromTicks(long ticks) => TryFromTicks(ticks, out var result)
        ? result
        : throw new ArgumentOutOfRangeException(nameof(ticks), ticks, "Simulation duration cannot be negative.");

    public static bool TryFromTicks(long ticks, out SimulationDuration result)
    {
        if (ticks < 0)
        {
            result = default;
            return false;
        }

        result = new SimulationDuration(ticks);
        return true;
    }

    public SimulationDuration Add(SimulationDuration duration) => this + duration;

    public int CompareTo(SimulationDuration other)
    {
        EnsureInitialized();
        other.EnsureInitialized();
        return _ticks.CompareTo(other._ticks);
    }

    public static SimulationDuration operator +(SimulationDuration left, SimulationDuration right)
    {
        left.EnsureInitialized();
        right.EnsureInitialized();
        return FromTicks(checked(left._ticks + right._ticks));
    }

    public static bool operator <(SimulationDuration left, SimulationDuration right) => left.CompareTo(right) < 0;
    public static bool operator >(SimulationDuration left, SimulationDuration right) => left.CompareTo(right) > 0;
    public static bool operator <=(SimulationDuration left, SimulationDuration right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SimulationDuration left, SimulationDuration right) => left.CompareTo(right) >= 0;
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    private void EnsureInitialized()
    {
        if (IsDefault)
        {
            throw new InvalidOperationException("Simulation duration is uninitialized.");
        }
    }
}

public interface ISimulationClock
{
    ServerTick CurrentTick { get; }
}

public sealed class ManualSimulationClock : ISimulationClock
{
    public ServerTick CurrentTick { get; private set; } = ServerTick.Zero;

    public void Advance(SimulationDuration duration) => CurrentTick += duration;

    public void AdvanceOneTick() => Advance(SimulationDuration.FromTicks(1));
}
