using System.Globalization;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Sequencing;

public sealed class SequencingContractTests
{
    [Fact]
    public void Event_sequence_none_is_default_and_first_assigned_value_is_one()
    {
        Assert.Equal(default, EventSequence.None);
        Assert.True(EventSequence.None.IsDefault);
        Assert.Equal(1, EventSequence.First.Value);
        Assert.Equal(EventSequence.First, EventSequence.None.Next());
        Assert.Equal(EventSequence.None, EventSequence.From(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => EventSequence.From(-1));
        Assert.False(EventSequence.From(long.MaxValue).TryNext(out _));
        Assert.Throws<OverflowException>(() => EventSequence.From(long.MaxValue).Next());
    }

    [Fact]
    public void State_version_zero_is_initial_but_default_is_invalid()
    {
        Assert.True(default(StateVersion).IsDefault);
        Assert.NotEqual(default(StateVersion), StateVersion.Zero);
        Assert.Equal(0, StateVersion.Zero.Value);
        Assert.Equal(1, StateVersion.Zero.Next().Value);
        Assert.Throws<InvalidOperationException>(() => _ = default(StateVersion).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => StateVersion.From(-1));
        Assert.False(StateVersion.From(long.MaxValue).TryNext(out _));
        Assert.Throws<OverflowException>(() => StateVersion.From(long.MaxValue).Next());
    }

    [Fact]
    public void Server_receive_sequence_is_a_distinct_zero_based_ordering_type()
    {
        Assert.True(default(ServerReceiveSequence).IsDefault);
        Assert.NotEqual(default(ServerReceiveSequence), ServerReceiveSequence.Zero);
        Assert.Equal(0, ServerReceiveSequence.Zero.Value);
        Assert.True(ServerReceiveSequence.Zero < ServerReceiveSequence.From(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ServerReceiveSequence.From(-1));
        Assert.Throws<OverflowException>(() => ServerReceiveSequence.From(long.MaxValue).Next());
    }

    [Fact]
    public void Ordering_notions_are_distinct_value_types()
    {
        Assert.NotEqual(typeof(ServerTick), typeof(EventSequence));
        Assert.NotEqual(typeof(ServerTick), typeof(StateVersion));
        Assert.NotEqual(typeof(ServerTick), typeof(ServerReceiveSequence));
        Assert.NotEqual(typeof(EventSequence), typeof(StateVersion));
        Assert.NotEqual(typeof(EventSequence), typeof(ServerReceiveSequence));
        Assert.NotEqual(typeof(StateVersion), typeof(ServerReceiveSequence));
    }

    [Fact]
    public void Ordering_values_are_value_based_and_culture_invariant()
    {
        var sequence = EventSequence.From(42);
        var receive = ServerReceiveSequence.From(42);
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal("42", sequence.ToString());
            Assert.Equal("42", receive.ToString());
            Assert.Equal("42", StateVersion.From(42).ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        Assert.Equal(sequence.GetHashCode(), EventSequence.From(42).GetHashCode());
        Assert.Equal(receive.GetHashCode(), ServerReceiveSequence.From(42).GetHashCode());
    }

    [Fact]
    public void Event_ordering_key_orders_by_tick_then_sequence_and_matches_valid_sequence_order()
    {
        var keys = new[]
        {
            new EventOrderingKey(ServerTick.From(0), EventSequence.From(1)),
            new EventOrderingKey(ServerTick.From(0), EventSequence.From(2)),
            new EventOrderingKey(ServerTick.From(1), EventSequence.From(3))
        };

        Assert.Equal(keys.Select(static key => key.Sequence), keys.OrderBy(static key => key).Select(static key => key.Sequence));
        Assert.True(new EventOrderingKey(ServerTick.From(1), EventSequence.From(99)) < new EventOrderingKey(ServerTick.From(2), EventSequence.From(1)));
    }

    [Fact]
    public void Canonical_host_tick_stages_match_the_frozen_gate_zero_order()
    {
        Assert.Equal(new[]
        {
            HostTickStage.hold_and_procedure_completions,
            HostTickStage.accepted_intents_by_server_receive_sequence,
            HostTickStage.deadline_expirations,
            HostTickStage.saw_transitions,
            HostTickStage.feed_and_auto_routes,
            HostTickStage.derived_states,
            HostTickStage.event_emission
        }, HostTickStages.CanonicalOrder);
    }
}
