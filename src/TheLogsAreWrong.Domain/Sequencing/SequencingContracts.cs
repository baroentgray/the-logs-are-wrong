using System.Globalization;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Sequencing;

public readonly record struct ServerReceiveSequence : IComparable<ServerReceiveSequence>
{
    private readonly long _value;
    private readonly bool _isInitialized;

    private ServerReceiveSequence(long value)
    {
        _value = value;
        _isInitialized = true;
    }

    public long Value => _isInitialized ? _value : throw new InvalidOperationException("Server receive sequence is uninitialized.");
    public bool IsDefault => !_isInitialized;
    public static ServerReceiveSequence Zero => new(0);

    public static ServerReceiveSequence From(long value) => TryFrom(value, out var result)
        ? result
        : throw new ArgumentOutOfRangeException(nameof(value), value, "Server receive sequence cannot be negative.");

    public static bool TryFrom(long value, out ServerReceiveSequence result)
    {
        if (value < 0)
        {
            result = default;
            return false;
        }

        result = new ServerReceiveSequence(value);
        return true;
    }

    public ServerReceiveSequence Next() => new(checked(Value + 1));

    public bool TryNext(out ServerReceiveSequence result)
    {
        if (IsDefault || _value == long.MaxValue)
        {
            result = default;
            return false;
        }

        result = new ServerReceiveSequence(_value + 1);
        return true;
    }

    public int CompareTo(ServerReceiveSequence other)
    {
        EnsureInitialized();
        other.EnsureInitialized();
        return _value.CompareTo(other._value);
    }

    public static bool operator <(ServerReceiveSequence left, ServerReceiveSequence right) => left.CompareTo(right) < 0;
    public static bool operator >(ServerReceiveSequence left, ServerReceiveSequence right) => left.CompareTo(right) > 0;
    public static bool operator <=(ServerReceiveSequence left, ServerReceiveSequence right) => left.CompareTo(right) <= 0;
    public static bool operator >=(ServerReceiveSequence left, ServerReceiveSequence right) => left.CompareTo(right) >= 0;
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    private void EnsureInitialized()
    {
        if (IsDefault)
        {
            throw new InvalidOperationException("Server receive sequence is uninitialized.");
        }
    }
}

public readonly record struct EventOrderingKey(ServerTick Tick, EventSequence Sequence) : IComparable<EventOrderingKey>
{
    public int CompareTo(EventOrderingKey other)
    {
        var tickComparison = Tick.CompareTo(other.Tick);
        return tickComparison != 0 ? tickComparison : Sequence.CompareTo(other.Sequence);
    }

    public static bool operator <(EventOrderingKey left, EventOrderingKey right) => left.CompareTo(right) < 0;
    public static bool operator >(EventOrderingKey left, EventOrderingKey right) => left.CompareTo(right) > 0;
    public static bool operator <=(EventOrderingKey left, EventOrderingKey right) => left.CompareTo(right) <= 0;
    public static bool operator >=(EventOrderingKey left, EventOrderingKey right) => left.CompareTo(right) >= 0;
}

public static class HostTickStages
{
    public static IReadOnlyList<HostTickStage> CanonicalOrder { get; } = Array.AsReadOnly(new[]
    {
        HostTickStage.hold_and_procedure_completions,
        HostTickStage.accepted_intents_by_server_receive_sequence,
        HostTickStage.deadline_expirations,
        HostTickStage.saw_transitions,
        HostTickStage.feed_and_auto_routes,
        HostTickStage.derived_states,
        HostTickStage.event_emission
    });
}
