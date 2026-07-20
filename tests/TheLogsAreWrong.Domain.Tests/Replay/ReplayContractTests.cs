using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Tests.Replay;

public sealed class ReplayContractTests
{
    [Fact]
    public void Empty_boundary_and_full_journal_stream_validate()
    {
        var events = new[]
        {
            EventTestFixture.Event(1, tick: 0, stateVersion: 0),
            EventTestFixture.Event(2, tick: 1, stateVersion: 1)
        };

        var result = new ReplayValidator().Validate(EventTestFixture.Boundary(), events);

        Assert.True(result.IsValid);
        Assert.Null(result.Anomaly);
        Assert.Null(result.Position);
    }

    [Fact]
    public void Mid_stream_boundary_validates_tail_and_rejects_prepended_head_as_snapshot_duplicate()
    {
        var boundary = EventTestFixture.Boundary(lastSequence: 1, tick: 0, stateVersion: 0);
        var tail = new[] { EventTestFixture.Event(2, tick: 1, stateVersion: 1) };

        Assert.True(new ReplayValidator().Validate(boundary, tail).IsValid);
        AssertAnomaly(boundary, new[] { EventTestFixture.Event(1), tail[0] }, ReplayAnomaly.DuplicateOfSnapshot, 0);
    }

    [Fact]
    public void Gap_after_snapshot_is_reported_at_the_first_event()
    {
        AssertAnomaly(EventTestFixture.Boundary(lastSequence: 1), new[] { EventTestFixture.Event(3) }, ReplayAnomaly.GapAfterSnapshot, 0);
    }

    [Fact]
    public void Subsequent_duplicate_and_out_of_order_sequences_are_distinguished()
    {
        var boundary = EventTestFixture.Boundary();

        AssertAnomaly(boundary, new[] { EventTestFixture.Event(1), EventTestFixture.Event(1) }, ReplayAnomaly.Duplicate, 1);
        AssertAnomaly(boundary, new[] { EventTestFixture.Event(1), EventTestFixture.Event(2), EventTestFixture.Event(1) }, ReplayAnomaly.OutOfOrder, 2);
    }

    [Fact]
    public void Subsequent_gap_tick_and_state_version_regressions_are_detected()
    {
        var boundary = EventTestFixture.Boundary();

        AssertAnomaly(boundary, new[] { EventTestFixture.Event(1), EventTestFixture.Event(3) }, ReplayAnomaly.SequenceGap, 1);
        AssertAnomaly(boundary, new[] { EventTestFixture.Event(1, tick: 2), EventTestFixture.Event(2, tick: 1) }, ReplayAnomaly.TickRegression, 1);
        AssertAnomaly(boundary, new[] { EventTestFixture.Event(1, stateVersion: 1), EventTestFixture.Event(2, stateVersion: 0) }, ReplayAnomaly.StateVersionRegression, 1);
        AssertAnomaly(boundary, new[] { EventTestFixture.Event(1), EventTestFixture.Event(2, stateVersion: 2) }, ReplayAnomaly.StateVersionSkip, 1);
    }

    [Fact]
    public void Shift_mismatch_and_default_values_are_detected_without_mutation()
    {
        var boundary = EventTestFixture.Boundary();

        AssertAnomaly(boundary, new[] { EventTestFixture.Event(1, shift: TheLogsAreWrong.Domain.Identifiers.ShiftId.From("P0_SHIFT_B")) }, ReplayAnomaly.ShiftMismatch, 0);
        AssertAnomaly(boundary, new[] { EventTestFixture.Event(1) with { ServerTick = default } }, ReplayAnomaly.DefaultValue, 0);
    }

    [Fact]
    public void Replay_is_deterministic_and_does_not_mutate_the_input_stream()
    {
        var boundary = EventTestFixture.Boundary();
        var events = new[] { EventTestFixture.Event(1), EventTestFixture.Event(3) };
        var validator = new ReplayValidator();

        var first = validator.Validate(boundary, events);
        var second = validator.Validate(boundary, events);

        Assert.Equal(first, second);
        Assert.Equal(new[] { EventSequence.First, EventSequence.From(3) }, events.Select(static item => item.Sequence));
    }

    private static void AssertAnomaly(SnapshotBoundary boundary, IReadOnlyList<EventEnvelope> events, ReplayAnomaly anomaly, int position)
    {
        var result = new ReplayValidator().Validate(boundary, events);

        Assert.False(result.IsValid);
        Assert.Equal(anomaly, result.Anomaly);
        Assert.Equal(position, result.Position);
    }
}
