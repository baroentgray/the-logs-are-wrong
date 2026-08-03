using System.Reflection;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Intents;

[Trait("Scope", "TLAW-029")]
public sealed class AcceptedIntentBatchTests
{
    [Fact]
    public void Receipt_retains_exact_envelope_parameter_and_authoritative_actor_independent_from_hint()
    {
        var parameters = new TestParameters("opaque");
        var envelope = CreateEnvelope(ShiftId.From("P0_SHIFT_A"), IntentId.From("intent_01"), ActorId.From("untrusted_hint"), parameters);
        var receipt = new AuthoritativeAcceptedIntent(envelope, ActorId.From("bound_actor"), ServerTick.From(17), ServerReceiveSequence.From(3));

        Assert.Same(envelope, receipt.Envelope);
        Assert.Same(parameters, receipt.Envelope.Parameters);
        Assert.Equal(ActorId.From("untrusted_hint"), receipt.Envelope.ActorIdHint);
        Assert.Equal(ActorId.From("bound_actor"), receipt.AuthoritativeActor);
        Assert.NotEqual(receipt.Envelope.ActorIdHint, receipt.AuthoritativeActor);
        Assert.Equal(TargetId.From("target"), receipt.Envelope.TargetId);
        Assert.Equal(IntentActionId.From("inspect"), receipt.Envelope.Action);
        Assert.Equal(StateVersion.Zero, receipt.Envelope.ExpectedStateVersion);
        Assert.Equal(ServerTick.Zero, receipt.Envelope.ClientObservedTick);
        Assert.Equal(ServerTick.From(17), receipt.ReceivedAtTick);
        Assert.Equal(ServerReceiveSequence.From(3), receipt.ReceiveSequence);
    }

    [Fact]
    public void Receipt_and_factory_reject_null_or_default_evidence_loudly()
    {
        var shift = ShiftId.From("P0_SHIFT_A");
        var envelope = CreateEnvelope(shift, IntentId.From("intent_01"), ActorId.From("hint"), NoIntentParameters.Instance);

        Assert.Throws<ArgumentNullException>(() => new AuthoritativeAcceptedIntent(null!, ActorId.From("bound"), ServerTick.Zero, ServerReceiveSequence.Zero));
        Assert.Throws<ArgumentException>(() => new AuthoritativeAcceptedIntent(envelope, default, ServerTick.Zero, ServerReceiveSequence.Zero));
        Assert.Throws<ArgumentException>(() => new AuthoritativeAcceptedIntent(envelope, ActorId.From("bound"), default, ServerReceiveSequence.Zero));
        Assert.Throws<ArgumentException>(() => new AuthoritativeAcceptedIntent(envelope, ActorId.From("bound"), ServerTick.Zero, default));
        Assert.Throws<ArgumentException>(() => AcceptedIntentTickBatchFactory.Create(default, ServerTick.Zero, Array.Empty<AuthoritativeAcceptedIntent>()));
        Assert.Throws<ArgumentException>(() => AcceptedIntentTickBatchFactory.Create(shift, default, Array.Empty<AuthoritativeAcceptedIntent>()));
        Assert.Throws<ArgumentNullException>(() => AcceptedIntentTickBatchFactory.Create(shift, ServerTick.Zero, null!));
        Assert.Throws<ArgumentException>(() => AcceptedIntentTickBatchFactory.Create(shift, ServerTick.Zero, new AuthoritativeAcceptedIntent?[] { null! }!));
    }

    [Fact]
    public void Empty_batch_retains_exact_shift_and_tick()
    {
        var shift = ShiftId.From("P0_SHIFT_A");
        var tick = ServerTick.From(23);

        var batch = AcceptedIntentTickBatchFactory.Create(shift, tick, Array.Empty<AuthoritativeAcceptedIntent>());

        Assert.Equal(shift, batch.ShiftId);
        Assert.Equal(tick, batch.CurrentTick);
        Assert.Empty(batch.Intents);
    }

    [Fact]
    public void Single_sequence_zero_is_accepted_and_retained_exactly()
    {
        var shift = ShiftId.From("P0_SHIFT_A");
        var receipt = Accepted(shift, ServerTick.From(4), ServerReceiveSequence.Zero, "intent_01");

        var batch = AcceptedIntentTickBatchFactory.Create(shift, ServerTick.From(4), new[] { receipt });

        Assert.Equal(new[] { receipt }, batch.Intents);
        Assert.Same(receipt, Assert.Single(batch.Intents));
    }

    [Fact]
    public void Shuffled_two_zero_one_is_ordered_only_by_receive_sequence_and_retains_exact_receipts()
    {
        var shift = ShiftId.From("P0_SHIFT_A");
        var tick = ServerTick.From(9);
        var two = Accepted(shift, tick, ServerReceiveSequence.From(2), "intent_02");
        var zero = Accepted(shift, tick, ServerReceiveSequence.Zero, "intent_00");
        var one = Accepted(shift, tick, ServerReceiveSequence.From(1), "intent_01");

        var batch = AcceptedIntentTickBatchFactory.Create(shift, tick, new[] { two, zero, one });

        Assert.Equal(new[] { zero, one, two }, batch.Intents);
        Assert.All(batch.Intents, receipt => Assert.Equal(tick, receipt.ReceivedAtTick));
    }

    [Fact]
    public void Independent_equivalent_shuffled_inputs_return_value_equivalent_ordering()
    {
        var shift = ShiftId.From("P0_SHIFT_A");
        var tick = ServerTick.From(9);
        var first = AcceptedIntentTickBatchFactory.Create(shift, tick, new[]
        {
            Accepted(shift, tick, ServerReceiveSequence.From(2), "intent_02"),
            Accepted(shift, tick, ServerReceiveSequence.Zero, "intent_00"),
            Accepted(shift, tick, ServerReceiveSequence.From(1), "intent_01")
        });
        var second = AcceptedIntentTickBatchFactory.Create(shift, tick, new[]
        {
            Accepted(shift, tick, ServerReceiveSequence.From(1), "intent_01"),
            Accepted(shift, tick, ServerReceiveSequence.From(2), "intent_02"),
            Accepted(shift, tick, ServerReceiveSequence.Zero, "intent_00")
        });

        Assert.Equal(first.ShiftId, second.ShiftId);
        Assert.Equal(first.CurrentTick, second.CurrentTick);
        Assert.Equal(
            first.Intents.Select(receipt => (receipt.Envelope.IntentId, receipt.AuthoritativeActor, receipt.ReceivedAtTick, receipt.ReceiveSequence)),
            second.Intents.Select(receipt => (receipt.Envelope.IntentId, receipt.AuthoritativeActor, receipt.ReceivedAtTick, receipt.ReceiveSequence)));
    }

    [Fact]
    public void Factory_materializes_the_source_once_and_leaves_the_caller_collection_unchanged()
    {
        var shift = ShiftId.From("P0_SHIFT_A");
        var tick = ServerTick.From(9);
        var two = Accepted(shift, tick, ServerReceiveSequence.From(2), "intent_02");
        var zero = Accepted(shift, tick, ServerReceiveSequence.Zero, "intent_00");
        var one = Accepted(shift, tick, ServerReceiveSequence.From(1), "intent_01");
        var source = new List<AuthoritativeAcceptedIntent> { two, zero, one };

        var batch = AcceptedIntentTickBatchFactory.Create(shift, tick, new SingleEnumerationEnumerable(source));

        Assert.Equal(new[] { two, zero, one }, source);
        Assert.Equal(new[] { zero, one, two }, batch.Intents);
    }

    [Fact]
    public void First_sequence_above_zero_is_rejected()
    {
        var shift = ShiftId.From("P0_SHIFT_A");
        var receipt = Accepted(shift, ServerTick.Zero, ServerReceiveSequence.From(1), "intent_01");

        Assert.Throws<ArgumentException>(() => AcceptedIntentTickBatchFactory.Create(shift, ServerTick.Zero, new[] { receipt }));
    }

    [Fact]
    public void Duplicate_sequence_is_rejected()
    {
        var shift = ShiftId.From("P0_SHIFT_A");
        var zero = Accepted(shift, ServerTick.Zero, ServerReceiveSequence.Zero, "intent_00");
        var duplicateZero = Accepted(shift, ServerTick.Zero, ServerReceiveSequence.Zero, "intent_01");

        Assert.Throws<ArgumentException>(() => AcceptedIntentTickBatchFactory.Create(shift, ServerTick.Zero, new[] { zero, duplicateZero }));
    }

    [Fact]
    public void Sequence_gap_is_rejected()
    {
        var shift = ShiftId.From("P0_SHIFT_A");
        var zero = Accepted(shift, ServerTick.Zero, ServerReceiveSequence.Zero, "intent_00");
        var two = Accepted(shift, ServerTick.Zero, ServerReceiveSequence.From(2), "intent_02");

        Assert.Throws<ArgumentException>(() => AcceptedIntentTickBatchFactory.Create(shift, ServerTick.Zero, new[] { zero, two }));
    }

    [Fact]
    public void Duplicate_intent_id_is_rejected_even_when_other_evidence_differs()
    {
        var shift = ShiftId.From("P0_SHIFT_A");
        var zero = Accepted(shift, ServerTick.Zero, ServerReceiveSequence.Zero, "duplicate", "bound_a");
        var one = Accepted(shift, ServerTick.Zero, ServerReceiveSequence.From(1), "duplicate", "bound_b");

        Assert.Throws<ArgumentException>(() => AcceptedIntentTickBatchFactory.Create(shift, ServerTick.Zero, new[] { zero, one }));
    }

    [Fact]
    public void Cross_shift_evidence_is_rejected()
    {
        var batchShift = ShiftId.From("P0_SHIFT_A");
        var receipt = Accepted(ShiftId.From("OTHER_SHIFT"), ServerTick.Zero, ServerReceiveSequence.Zero, "intent_00");

        Assert.Throws<ArgumentException>(() => AcceptedIntentTickBatchFactory.Create(batchShift, ServerTick.Zero, new[] { receipt }));
    }

    [Fact]
    public void Earlier_receive_tick_is_rejected()
    {
        var shift = ShiftId.From("P0_SHIFT_A");
        var receipt = Accepted(shift, ServerTick.From(4), ServerReceiveSequence.Zero, "intent_00");

        Assert.Throws<ArgumentException>(() => AcceptedIntentTickBatchFactory.Create(shift, ServerTick.From(5), new[] { receipt }));
    }

    [Fact]
    public void Later_receive_tick_is_rejected()
    {
        var shift = ShiftId.From("P0_SHIFT_A");
        var receipt = Accepted(shift, ServerTick.From(6), ServerReceiveSequence.Zero, "intent_00");

        Assert.Throws<ArgumentException>(() => AcceptedIntentTickBatchFactory.Create(shift, ServerTick.From(5), new[] { receipt }));
    }

    [Fact]
    public void Invalid_input_exposes_no_partial_batch_and_can_be_followed_by_a_valid_call()
    {
        var shift = ShiftId.From("P0_SHIFT_A");
        var zero = Accepted(shift, ServerTick.Zero, ServerReceiveSequence.Zero, "intent_00");
        var gap = Accepted(shift, ServerTick.Zero, ServerReceiveSequence.From(2), "intent_02");

        Assert.Throws<ArgumentException>(() => AcceptedIntentTickBatchFactory.Create(shift, ServerTick.Zero, new[] { zero, gap }));

        var valid = AcceptedIntentTickBatchFactory.Create(shift, ServerTick.Zero, new[] { zero });
        Assert.Same(zero, Assert.Single(valid.Intents));
    }

    [Fact]
    public void Checked_sequence_successor_guard_cannot_wrap_silently()
    {
        var helper = Assert.Single(typeof(AcceptedIntentTickBatchFactory).GetMethods(BindingFlags.Static | BindingFlags.NonPublic), method => method.Name == "NextSequence");

        var exception = Assert.Throws<TargetInvocationException>(() => helper.Invoke(null, new object[] { ServerReceiveSequence.From(long.MaxValue) }));

        Assert.IsType<OverflowException>(exception.InnerException);
    }

    private static AuthoritativeAcceptedIntent Accepted(
        ShiftId shift,
        ServerTick receivedAtTick,
        ServerReceiveSequence receiveSequence,
        string intentId,
        string authoritativeActor = "bound") =>
        new(CreateEnvelope(shift, IntentId.From(intentId), ActorId.From($"hint_{intentId}"), NoIntentParameters.Instance), ActorId.From(authoritativeActor), receivedAtTick, receiveSequence);

    private static IntentEnvelope CreateEnvelope(ShiftId shift, IntentId intentId, ActorId actorHint, IIntentParameters parameters) =>
        new(shift, intentId, actorHint, TargetId.From("target"), IntentActionId.From("inspect"), StateVersion.Zero, ServerTick.Zero, parameters);

    private sealed record TestParameters(string Value) : IIntentParameters;

    private sealed class SingleEnumerationEnumerable(IEnumerable<AuthoritativeAcceptedIntent> source) : IEnumerable<AuthoritativeAcceptedIntent>
    {
        private readonly IEnumerable<AuthoritativeAcceptedIntent> _source = source;
        private bool _enumerated;

        public IEnumerator<AuthoritativeAcceptedIntent> GetEnumerator()
        {
            if (_enumerated)
            {
                throw new InvalidOperationException("The factory must enumerate the source exactly once.");
            }

            _enumerated = true;
            return _source.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
