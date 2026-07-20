using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Tests.Journal;

public sealed class JournalContractTests
{
    [Fact]
    public void Empty_journal_exposes_the_none_sequence_and_initial_boundary_values()
    {
        IEventJournal journal = new InMemoryEventJournal(EventTestFixture.Shift);

        Assert.Equal(EventSequence.None, journal.LastSequence);
        Assert.Equal(ServerTick.Zero, journal.LastTick);
        Assert.Equal(StateVersion.Zero, journal.LastStateVersion);
        Assert.Equal(0, journal.Count);
        Assert.Empty(journal.Events);
    }

    [Fact]
    public void Accepted_append_is_contiguous_and_preserves_insertion_order()
    {
        var journal = new InMemoryEventJournal(EventTestFixture.Shift);
        var first = EventTestFixture.Event(1, tick: 3, stateVersion: 0);
        var second = EventTestFixture.Event(2, tick: 3, stateVersion: 1);

        Assert.Equal(JournalAppendOutcome.Accepted, journal.TryAppend(first));
        Assert.Equal(JournalAppendOutcome.Accepted, journal.TryAppend(second));
        Assert.Equal(new[] { first, second }, journal.Events);
        Assert.Equal(EventSequence.From(2), journal.LastSequence);
        Assert.Equal(ServerTick.From(3), journal.LastTick);
        Assert.Equal(StateVersion.From(1), journal.LastStateVersion);
    }

    [Fact]
    public void Duplicate_sequence_is_rejected_without_mutating_the_journal()
    {
        var journal = JournalWithFirstEvent();

        AssertRejected(journal, EventTestFixture.Event(1), JournalAppendOutcome.Duplicate);
    }

    [Fact]
    public void Lower_sequence_is_rejected_as_out_of_order_without_mutating_the_journal()
    {
        var journal = new InMemoryEventJournal(EventTestFixture.Shift);
        journal.Append(EventTestFixture.Event(1));
        journal.Append(EventTestFixture.Event(2));

        AssertRejected(journal, EventTestFixture.Event(1), JournalAppendOutcome.OutOfOrder);
    }

    [Fact]
    public void Skipped_sequence_is_rejected_without_mutating_the_journal()
    {
        var journal = JournalWithFirstEvent();

        AssertRejected(journal, EventTestFixture.Event(3), JournalAppendOutcome.SequenceGap);
    }

    [Fact]
    public void Tick_regression_is_rejected_but_equal_tick_is_accepted()
    {
        var journal = new InMemoryEventJournal(EventTestFixture.Shift);
        journal.Append(EventTestFixture.Event(1, tick: 2));

        Assert.Equal(JournalAppendOutcome.Accepted, journal.TryAppend(EventTestFixture.Event(2, tick: 2)));
        AssertRejected(journal, EventTestFixture.Event(3, tick: 1), JournalAppendOutcome.TickRegression);
    }

    [Fact]
    public void State_version_decrease_and_skip_are_rejected_but_same_version_is_accepted()
    {
        var journal = new InMemoryEventJournal(EventTestFixture.Shift);
        journal.Append(EventTestFixture.Event(1, stateVersion: 1));

        Assert.Equal(JournalAppendOutcome.Accepted, journal.TryAppend(EventTestFixture.Event(2, stateVersion: 1)));
        AssertRejected(journal, EventTestFixture.Event(3, stateVersion: 0), JournalAppendOutcome.StateVersionRegression);
        AssertRejected(journal, EventTestFixture.Event(3, stateVersion: 3), JournalAppendOutcome.StateVersionSkip);
    }

    [Fact]
    public void Mismatched_shift_and_default_fields_are_rejected_without_mutating_the_journal()
    {
        var journal = JournalWithFirstEvent();

        AssertRejected(journal, EventTestFixture.Event(2, shift: TheLogsAreWrong.Domain.Identifiers.ShiftId.From("P0_SHIFT_B")), JournalAppendOutcome.ShiftMismatch);
        AssertRejected(journal, EventTestFixture.Event(2) with { ServerTick = default }, JournalAppendOutcome.DefaultValue);
    }

    [Fact]
    public void First_append_must_be_sequence_one_and_append_throws_its_typed_outcome()
    {
        var journal = new InMemoryEventJournal(EventTestFixture.Shift);

        var exception = Assert.Throws<JournalInvariantViolationException>(() => journal.Append(EventTestFixture.Event(2)));

        Assert.Equal(JournalAppendOutcome.SequenceGap, exception.Outcome);
        Assert.Equal(0, journal.Count);
        AssertRejected(journal, EventTestFixture.Event(1, stateVersion: 2), JournalAppendOutcome.StateVersionSkip);
    }

    private static InMemoryEventJournal JournalWithFirstEvent()
    {
        var journal = new InMemoryEventJournal(EventTestFixture.Shift);
        journal.Append(EventTestFixture.Event(1));
        return journal;
    }

    private static void AssertRejected(InMemoryEventJournal journal, EventEnvelope envelope, JournalAppendOutcome expectedOutcome)
    {
        var countBefore = journal.Count;

        Assert.Equal(expectedOutcome, journal.TryAppend(envelope));
        Assert.Equal(countBefore, journal.Count);
    }
}
