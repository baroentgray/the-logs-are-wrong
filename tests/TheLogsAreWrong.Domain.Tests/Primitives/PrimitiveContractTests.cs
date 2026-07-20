using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Tests.Primitives;

public sealed class PrimitiveContractTests
{
    [Fact]
    public void Event_sequence_accepts_zero_orders_and_increments()
    {
        var sequence = EventSequence.From(0);

        Assert.Equal(1, sequence.Next().Value);
        Assert.True(sequence < sequence.Next());
    }

    [Fact]
    public void Node_capacity_models_unlimited_separately_from_limited()
    {
        Assert.True(NodeCapacity.Unlimited.IsUnlimited);
        Assert.NotEqual(NodeCapacity.Unlimited, NodeCapacity.Limited(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NodeCapacity.Limited(0));
    }

    [Fact]
    public void Numeric_value_objects_enforce_their_declared_ranges_without_clock_arithmetic()
    {
        Assert.Equal(1, StateVersion.From(0).Next().Value);
        Assert.Equal(0, ServerTick.From(0).Value);
        Assert.Equal(-42, new ShiftSeed(-42).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => EventSequence.From(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => StateVersion.From(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ServerTick.From(-1));
    }
}
