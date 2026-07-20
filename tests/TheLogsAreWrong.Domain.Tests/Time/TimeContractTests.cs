using System.Globalization;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Time;

public sealed class TimeContractTests
{
    [Fact]
    public void Zero_valid_time_values_are_distinct_from_detectable_defaults()
    {
        Assert.True(default(ServerTick).IsDefault);
        Assert.True(default(SimulationDuration).IsDefault);
        Assert.NotEqual(default(ServerTick), ServerTick.Zero);
        Assert.NotEqual(default(SimulationDuration), SimulationDuration.Zero);
        Assert.Equal(0, ServerTick.Zero.Value);
        Assert.Equal(0, SimulationDuration.Zero.Value);
        Assert.Throws<InvalidOperationException>(() => _ = default(ServerTick).Value);
        Assert.Throws<InvalidOperationException>(() => _ = default(SimulationDuration).Value);
    }

    [Fact]
    public void Time_values_reject_negative_values_and_have_consistent_ordering_and_hashing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ServerTick.From(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationDuration.FromTicks(-1));
        Assert.True(ServerTick.From(1) > ServerTick.Zero);
        Assert.True(SimulationDuration.FromTicks(2) > SimulationDuration.FromTicks(1));
        Assert.Equal(ServerTick.From(4).GetHashCode(), ServerTick.From(4).GetHashCode());
        Assert.Equal(SimulationDuration.FromTicks(4).GetHashCode(), SimulationDuration.FromTicks(4).GetHashCode());
    }

    [Fact]
    public void Tick_and_duration_arithmetic_is_checked_and_never_converts_seconds()
    {
        var tick = ServerTick.From(7);
        var duration = SimulationDuration.FromTicks(3);

        Assert.Equal(10, (tick + duration).Value);
        Assert.Equal(3, (ServerTick.From(10) - tick).Value);
        Assert.False(tick.TrySubtract(ServerTick.From(8), out _));
        Assert.Throws<InvalidOperationException>(() => tick.Subtract(ServerTick.From(8)));
        Assert.Equal(5, (SimulationDuration.FromTicks(2) + SimulationDuration.FromTicks(3)).Value);
        Assert.Throws<OverflowException>(() => _ = ServerTick.From(long.MaxValue) + SimulationDuration.FromTicks(1));
        Assert.Throws<OverflowException>(() => _ = SimulationDuration.FromTicks(long.MaxValue) + SimulationDuration.FromTicks(1));
    }

    [Fact]
    public void Numeric_string_representations_are_invariant_culture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");

            Assert.Equal("42", ServerTick.From(42).ToString());
            Assert.Equal("42", SimulationDuration.FromTicks(42).ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Manual_clock_advances_only_by_explicit_simulation_durations()
    {
        ISimulationClock clock = new ManualSimulationClock();

        Assert.Equal(ServerTick.Zero, clock.CurrentTick);
        ((ManualSimulationClock)clock).Advance(SimulationDuration.Zero);
        Assert.Equal(ServerTick.Zero, clock.CurrentTick);
        ((ManualSimulationClock)clock).AdvanceOneTick();
        ((ManualSimulationClock)clock).Advance(SimulationDuration.FromTicks(4));

        Assert.Equal(ServerTick.From(5), clock.CurrentTick);
    }
}
