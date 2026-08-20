using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-067")]
public sealed class HostSessionTests
{
    private static readonly ValidatedConfiguration Fx = Fixture.LoadP0();
    private static readonly ProfileId LearningId = ProfileId.From("learning");

    [Fact]
    public void Production_session_is_plain_nonstatic_and_owns_the_only_caller_facing_tick_boundary()
    {
        Assert.True(typeof(HostSession).IsSealed);
        Assert.False(typeof(HostSession).IsAbstract);
        Assert.DoesNotContain(typeof(HostSession).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            field => !field.IsLiteral);

        var execute = Assert.Single(typeof(HostSession).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => method.Name == nameof(HostSession.ExecuteTick));

        Assert.Equal(typeof(HostStageSevenEventExecution), execute.ReturnType);
        Assert.Equal(
            [typeof(ServerTick), typeof(AcceptedIntentTickBatch), typeof(ImmutableHashSet<ItemId>)],
            execute.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void Host_tick_and_stage_seven_do_not_accept_caller_supplied_event_identity_or_plan_cardinality()
    {
        var hostExecute = Assert.Single(typeof(HostTickExecutionService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => method.Name == nameof(HostTickExecutionService.Execute));
        var stageSevenExecute = Assert.Single(typeof(HostStageSevenEventExecutor).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => method.Name == nameof(HostStageSevenEventExecutor.Execute));

        Assert.DoesNotContain(hostExecute.GetParameters(), parameter => parameter.ParameterType == typeof(ImmutableArray<EventId>));
        Assert.DoesNotContain(stageSevenExecute.GetParameters(), parameter => parameter.ParameterType == typeof(ImmutableArray<EventId>));
    }

    [Fact]
    public void Invalid_tick_input_fails_before_carrying_any_session_state()
    {
        using var session = new HostSession(Fx.Shift, Fx.Anomalies, LearningId);
        var beforeShift = session.ShiftState;
        var beforeQuota = session.QuotaState;
        var beforeJournalCount = session.Journal.Count;
        var mismatchedBatch = AcceptedIntentTickBatchFactory.Create(Fx.Shift.ShiftId, ServerTick.From(1), ImmutableArray<AuthoritativeAcceptedIntent>.Empty);

        Assert.Throws<ArgumentException>(() => session.ExecuteTick(ServerTick.Zero, mismatchedBatch, ImmutableHashSet<ItemId>.Empty));

        Assert.Same(beforeShift, session.ShiftState);
        Assert.Same(beforeQuota, session.QuotaState);
        Assert.Equal(beforeJournalCount, session.Journal.Count);
        Assert.Equal(0, session.SuccessfulTickCount);
    }

    [Fact]
    public void Multi_publication_batch_failure_after_the_first_staged_acceptance_leaves_the_entire_session_continuity_unchanged()
    {
        var journal = new ThrowAfterFirstStagedPublicationJournal(Fx.Shift.ShiftId);
        using var session = new HostSession(Fx.Shift, Fx.Anomalies, LearningId, journal);
        var batch = EmptyBatch(session.ShiftState.ShiftId, ServerTick.Zero);
        var before = Snapshot(session);

        Assert.Throws<InvalidOperationException>(() => session.ExecuteTick(ServerTick.Zero, batch, ImmutableHashSet<ItemId>.Empty));

        Assert.Equal(1, journal.StagedAcceptedPublicationCount);
        Assert.True(journal.ThrewOnLaterPublication);
        AssertSessionContinuityUnchanged(before, session);

        journal.ThrowOnLaterPublication = false;
        var retried = Assert.IsType<HostStageSevenPublished>(session.ExecuteTick(ServerTick.Zero, batch, ImmutableHashSet<ItemId>.Empty));
        Assert.True(retried.Publications.Length > 1);
        Assert.Equal(1, session.SuccessfulTickCount);
        Assert.Equal(retried.Publications.Length, session.Journal.Count);
    }

    [Fact]
    public void Four_consecutive_ticks_carry_only_shared_host_results_and_replay_deterministically()
    {
        var first = RunFourTicks();
        var second = RunFourTicks();

        Assert.Equal(4, first.SuccessfulTickCount);
        Assert.Equal(first.Projection, second.Projection);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(first.Events.Select(envelope => envelope.EventId), second.Events.Select(envelope => envelope.EventId));
        Assert.Equal(Enumerable.Range(1, first.Events.Length).Select(value => (long)value), first.Events.Select(envelope => envelope.Sequence.Value));
        Assert.All(first.Events, envelope => Assert.StartsWith($"host:{Fx.Shift.ShiftId}:", envelope.EventId.ToString(), StringComparison.Ordinal));
        Assert.Contains(first.Executions, execution => execution is HostStageSevenPublished published && published.AssignedEventIds.Length > 1);
        Assert.Contains(first.Executions, execution => execution is HostStageSevenNoNewPublication noPublication && noPublication.AssignedEventIds.IsEmpty);
    }

    [Fact]
    public void Reentrant_and_disposed_ticks_fail_closed_without_creating_a_static_session_owner()
    {
        var reentrantJournal = new ReentrantJournal(Fx.Shift.ShiftId);
        var firstBatch = EmptyBatch(Fx.Shift.ShiftId, ServerTick.Zero);
        HostSession? reentrantSession = null;
        reentrantJournal.BeforeAppend = () => reentrantSession!.ExecuteTick(ServerTick.Zero, firstBatch, ImmutableHashSet<ItemId>.Empty);
        using (reentrantSession = new HostSession(Fx.Shift, Fx.Anomalies, LearningId, reentrantJournal))
        {
            var beforeShift = reentrantSession.ShiftState;
            Assert.Throws<InvalidOperationException>(() => reentrantSession.ExecuteTick(ServerTick.Zero, firstBatch, ImmutableHashSet<ItemId>.Empty));
            Assert.Same(beforeShift, reentrantSession.ShiftState);
            Assert.Equal(0, reentrantSession.Journal.Count);
            Assert.Equal(0, reentrantSession.SuccessfulTickCount);
        }

        using var session = new HostSession(Fx.Shift, Fx.Anomalies, LearningId);
        var batch = EmptyBatch(session.ShiftState.ShiftId, ServerTick.Zero);

        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.ExecuteTick(ServerTick.Zero, batch, ImmutableHashSet<ItemId>.Empty));
    }

    private static SessionRun RunFourTicks()
    {
        using var session = new HostSession(Fx.Shift, Fx.Anomalies, LearningId);
        var executions = ImmutableArray.CreateBuilder<HostStageSevenEventExecution>();
        for (var tick = 0L; tick < 4; tick++)
        {
            var currentTick = ServerTick.From(tick);
            executions.Add(session.ExecuteTick(currentTick, EmptyBatch(session.ShiftState.ShiftId, currentTick), ImmutableHashSet<ItemId>.Empty));
        }

        var events = session.Journal.Events.ToImmutableArray();
        var projection = string.Join("\n", executions.Select((execution, index) =>
            $"{index}|{execution.GetType().Name}|{execution.FinalShiftState.StateVersion}|{execution.AfterCursor.LastSequence}")) +
            "\n" + string.Join("\n", events.Select(envelope =>
                $"{envelope.Sequence}|{envelope.EventId}|{envelope.EventType}|{envelope.StateVersionAfter}"));
        return new SessionRun(executions.ToImmutable(), events, session.SuccessfulTickCount, projection, Sha256(projection));
    }

    private static AcceptedIntentTickBatch EmptyBatch(ShiftId shiftId, ServerTick tick) =>
        AcceptedIntentTickBatchFactory.Create(shiftId, tick, ImmutableArray<AuthoritativeAcceptedIntent>.Empty);

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static SessionSnapshot Snapshot(HostSession session) => new(
        session.ShiftState,
        session.QuotaState,
        session.MovementNoise,
        session.LineNoise,
        session.Progression,
        session.Lifecycle,
        session.SuccessfulTickCount,
        session.Journal.Count,
        session.Journal.LastSequence,
        session.Journal.LastTick,
        session.Journal.LastStateVersion,
        session.Journal.Events.ToImmutableArray());

    private static void AssertSessionContinuityUnchanged(SessionSnapshot before, HostSession session)
    {
        Assert.Same(before.ShiftState, session.ShiftState);
        Assert.Same(before.QuotaState, session.QuotaState);
        Assert.Same(before.MovementNoise, session.MovementNoise);
        Assert.Same(before.LineNoise, session.LineNoise);
        Assert.Same(before.Progression, session.Progression);
        Assert.Same(before.Lifecycle, session.Lifecycle);
        Assert.Equal(before.SuccessfulTickCount, session.SuccessfulTickCount);
        Assert.Equal(before.JournalCount, session.Journal.Count);
        Assert.Equal(before.LastSequence, session.Journal.LastSequence);
        Assert.Equal(before.LastTick, session.Journal.LastTick);
        Assert.Equal(before.LastStateVersion, session.Journal.LastStateVersion);
        Assert.Equal(before.Events, session.Journal.Events);
    }

    private sealed record SessionRun(
        ImmutableArray<HostStageSevenEventExecution> Executions,
        ImmutableArray<EventEnvelope> Events,
        int SuccessfulTickCount,
        string Projection,
        string Sha256);

    private sealed record SessionSnapshot(
        ShiftRuntimeState ShiftState,
        QuotaRuntimeState QuotaState,
        MovementNoiseRuntimeState MovementNoise,
        LineNoiseRuntimeState LineNoise,
        HostTickProgressionEvidence Progression,
        ShiftLifecycleRuntimeState Lifecycle,
        int SuccessfulTickCount,
        int JournalCount,
        EventSequence LastSequence,
        ServerTick LastTick,
        StateVersion LastStateVersion,
        ImmutableArray<EventEnvelope> Events);

    private sealed class ThrowAfterFirstStagedPublicationJournal : IAtomicEventJournal
    {
        private readonly InMemoryEventJournal _committed;

        public ThrowAfterFirstStagedPublicationJournal(ShiftId shiftId) => _committed = new InMemoryEventJournal(shiftId);

        public bool ThrowOnLaterPublication { get; set; } = true;
        public int StagedAcceptedPublicationCount { get; private set; }
        public bool ThrewOnLaterPublication { get; private set; }
        public ShiftId Shift => _committed.Shift;
        public EventSequence LastSequence => _committed.LastSequence;
        public ServerTick LastTick => _committed.LastTick;
        public StateVersion LastStateVersion => _committed.LastStateVersion;
        public int Count => _committed.Count;
        public IReadOnlyList<EventEnvelope> Events => _committed.Events;
        public void Append(EventEnvelope envelope) => _committed.Append(envelope);
        public JournalAppendOutcome TryAppend(EventEnvelope envelope) => _committed.TryAppend(envelope);

        public JournalAppendOutcome TryAppendBatch(IReadOnlyList<EventEnvelope> envelopes)
        {
            var staged = new InMemoryEventJournal(Shift);
            foreach (var envelope in envelopes)
            {
                if (ThrowOnLaterPublication && StagedAcceptedPublicationCount == 1)
                {
                    ThrewOnLaterPublication = true;
                    throw new InvalidOperationException("TLAW067_TEST_LATER_PUBLICATION_FAILURE");
                }

                var outcome = staged.TryAppend(envelope);
                if (outcome != JournalAppendOutcome.Accepted)
                {
                    return outcome;
                }

                StagedAcceptedPublicationCount++;
            }

            return _committed.TryAppendBatch(envelopes);
        }
    }

    private sealed class ReentrantJournal : IAtomicEventJournal
    {
        private readonly InMemoryEventJournal _inner;

        public ReentrantJournal(ShiftId shiftId) => _inner = new InMemoryEventJournal(shiftId);

        public Action? BeforeAppend { get; set; }
        public ShiftId Shift => _inner.Shift;
        public EventSequence LastSequence => _inner.LastSequence;
        public ServerTick LastTick => _inner.LastTick;
        public StateVersion LastStateVersion => _inner.LastStateVersion;
        public int Count => _inner.Count;
        public IReadOnlyList<EventEnvelope> Events => _inner.Events;
        public void Append(EventEnvelope envelope)
        {
            BeforeAppend?.Invoke();
            _inner.Append(envelope);
        }

        public JournalAppendOutcome TryAppend(EventEnvelope envelope)
        {
            BeforeAppend?.Invoke();
            return _inner.TryAppend(envelope);
        }

        public JournalAppendOutcome TryAppendBatch(IReadOnlyList<EventEnvelope> envelopes)
        {
            BeforeAppend?.Invoke();
            return _inner.TryAppendBatch(envelopes);
        }
    }
}
