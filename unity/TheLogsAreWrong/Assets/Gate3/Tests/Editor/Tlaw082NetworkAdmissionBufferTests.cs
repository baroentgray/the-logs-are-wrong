using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Gate3.Tests
{
    /// <summary>Executable D-024 contracts for the plain-C# Gate-3 network-admission buffer.</summary>
    public sealed class Tlaw082NetworkAdmissionBufferTests
    {
        private static readonly ShiftId CurrentShift = ShiftId.From("current_shift");

        [Test]
        public void First_current_shift_evidence_is_admitted_at_sequence_zero_and_materializes_the_existing_batch_contract()
        {
            using var buffer = new Gate3NetworkIntentAdmissionBuffer(CurrentShift);
            var envelope = Envelope(intent: "first");

            var admission = buffer.Admit(Resolved(connection: 11, receiveTick: 17, envelope, actor: "trusted_actor"));
            var materialized = buffer.Materialize(CurrentShift, ServerTick.From(17));

            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.Admitted, admission.Status);
            Assert.IsTrue(admission.HasAcceptedIntent);
            Assert.AreSame(envelope, admission.AcceptedIntent.Envelope);
            Assert.AreEqual(ActorId.From("trusted_actor"), admission.AcceptedIntent.AuthoritativeActor);
            Assert.AreEqual(ServerTick.From(17), admission.AcceptedIntent.ReceivedAtTick);
            Assert.AreEqual(ServerReceiveSequence.Zero, admission.AcceptedIntent.ReceiveSequence);
            Assert.AreEqual(Gate3NetworkIntentMaterializationStatus.Materialized, materialized.Status);
            Assert.IsTrue(materialized.HasBatch);
            Assert.AreEqual(CurrentShift, materialized.Batch.ShiftId);
            Assert.AreEqual(ServerTick.From(17), materialized.Batch.CurrentTick);
            CollectionAssert.AreEqual(new[] { admission.AcceptedIntent }, materialized.Batch.Intents);
        }

        [Test]
        public void Connections_share_one_serial_sequence_per_exact_receive_tick_and_the_next_tick_restarts_at_zero()
        {
            using var buffer = new Gate3NetworkIntentAdmissionBuffer(CurrentShift);

            var first = buffer.Admit(Resolved(connection: 1, receiveTick: 31, Envelope("first"), "actor_one"));
            var second = buffer.Admit(Resolved(connection: 2, receiveTick: 31, Envelope("second"), "actor_two"));
            var nextTick = buffer.Admit(Resolved(connection: 3, receiveTick: 32, Envelope("next"), "actor_three"));
            var firstBatch = buffer.Materialize(CurrentShift, ServerTick.From(31));
            var nextBatch = buffer.Materialize(CurrentShift, ServerTick.From(32));

            Assert.AreEqual(ServerReceiveSequence.Zero, first.AcceptedIntent.ReceiveSequence);
            Assert.AreEqual(ServerReceiveSequence.From(1), second.AcceptedIntent.ReceiveSequence);
            Assert.AreEqual(ServerReceiveSequence.Zero, nextTick.AcceptedIntent.ReceiveSequence);
            CollectionAssert.AreEqual(new[] { first.AcceptedIntent, second.AcceptedIntent }, firstBatch.Batch.Intents);
            CollectionAssert.AreEqual(new[] { nextTick.AcceptedIntent }, nextBatch.Batch.Intents);
        }

        [Test]
        public void Duplicate_intent_ids_are_session_lifetime_across_ticks_and_connections_and_consume_no_sequence()
        {
            using var buffer = new Gate3NetworkIntentAdmissionBuffer(CurrentShift);

            var first = buffer.Admit(Resolved(connection: 1, receiveTick: 41, Envelope("same"), "actor_one"));
            var duplicateSameTick = buffer.Admit(Resolved(connection: 2, receiveTick: 41, Envelope("same"), "actor_two"));
            var duplicateOtherTick = buffer.Admit(Resolved(connection: 3, receiveTick: 42, Envelope("same"), "actor_three"));
            var afterDuplicate = buffer.Admit(Resolved(connection: 4, receiveTick: 41, Envelope("after"), "actor_four"));
            var batch = buffer.Materialize(CurrentShift, ServerTick.From(41));

            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.Admitted, first.Status);
            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.DuplicateIntentId, duplicateSameTick.Status);
            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.DuplicateIntentId, duplicateOtherTick.Status);
            Assert.IsFalse(duplicateSameTick.HasAcceptedIntent);
            Assert.IsFalse(duplicateOtherTick.HasAcceptedIntent);
            Assert.AreEqual(ServerReceiveSequence.From(1), afterDuplicate.AcceptedIntent.ReceiveSequence);
            CollectionAssert.AreEqual(new[] { first.AcceptedIntent, afterDuplicate.AcceptedIntent }, batch.Batch.Intents);
        }

        [Test]
        public void Shift_mismatch_precedes_ledger_and_sequence_state_and_cannot_poison_the_current_shift()
        {
            using var buffer = new Gate3NetworkIntentAdmissionBuffer(CurrentShift);
            var foreign = Envelope(intent: "reusable", shift: "other_shift");

            var rejected = buffer.Admit(Resolved(connection: 2, receiveTick: 51, foreign, "actor"));
            var accepted = buffer.Admit(Resolved(connection: 2, receiveTick: 51, Envelope("reusable"), "actor"));

            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.ShiftMismatch, rejected.Status);
            Assert.IsFalse(rejected.HasAcceptedIntent);
            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.Admitted, accepted.Status);
            Assert.AreEqual(ServerReceiveSequence.Zero, accepted.AcceptedIntent.ReceiveSequence);
        }

        [Test]
        public void Exact_resolved_actor_and_original_envelope_are_preserved_despite_a_forged_actor_hint()
        {
            using var buffer = new Gate3NetworkIntentAdmissionBuffer(CurrentShift);
            var envelope = Envelope("forged_hint", actorHint: "forged_client_actor");

            var result = buffer.Admit(Resolved(connection: 7, receiveTick: 61, envelope, "trusted_server_actor"));

            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.Admitted, result.Status);
            Assert.AreSame(envelope, result.AcceptedIntent.Envelope);
            Assert.AreEqual(ActorId.From("trusted_server_actor"), result.AcceptedIntent.AuthoritativeActor);
            Assert.AreNotEqual(result.AcceptedIntent.Envelope.ActorIdHint, result.AcceptedIntent.AuthoritativeActor);
        }

        [Test]
        public void Exact_tick_materialization_seals_before_return_and_a_late_first_seen_intent_cannot_roll_forward_or_resurrect()
        {
            using var buffer = new Gate3NetworkIntentAdmissionBuffer(CurrentShift);
            var lateEnvelope = Envelope("late");

            var sealedBatch = buffer.Materialize(CurrentShift, ServerTick.From(71));
            var repeatedMaterialization = buffer.Materialize(CurrentShift, ServerTick.From(71));
            var late = buffer.Admit(Resolved(connection: 4, receiveTick: 71, lateEnvelope, "actor"));
            var retransmissionAtFutureTick = buffer.Admit(Resolved(connection: 4, receiveTick: 72, lateEnvelope, "actor"));
            var future = buffer.Admit(Resolved(connection: 4, receiveTick: 72, Envelope("future"), "actor"));

            Assert.AreEqual(Gate3NetworkIntentMaterializationStatus.Materialized, sealedBatch.Status);
            Assert.IsEmpty(sealedBatch.Batch.Intents);
            Assert.AreEqual(Gate3NetworkIntentMaterializationStatus.ReceiveTickAlreadySealed, repeatedMaterialization.Status);
            Assert.IsFalse(repeatedMaterialization.HasBatch);
            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.ReceiveTickClosed, late.Status);
            Assert.IsFalse(late.HasAcceptedIntent);
            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.DuplicateIntentId, retransmissionAtFutureTick.Status);
            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.Admitted, future.Status);
            Assert.AreEqual(ServerTick.From(72), future.AcceptedIntent.ReceivedAtTick);
            Assert.AreEqual(ServerReceiveSequence.Zero, future.AcceptedIntent.ReceiveSequence);
        }

        [Test]
        public void Future_receive_tick_buckets_survive_older_backlog_materialization_without_rewriting_received_ticks()
        {
            using var buffer = new Gate3NetworkIntentAdmissionBuffer(CurrentShift);

            var future = buffer.Admit(Resolved(connection: 1, receiveTick: 91, Envelope("future"), "actor"));
            var older = buffer.Admit(Resolved(connection: 2, receiveTick: 84, Envelope("older"), "actor"));
            var olderBatch = buffer.Materialize(CurrentShift, ServerTick.From(84));
            var futureBatch = buffer.Materialize(CurrentShift, ServerTick.From(91));

            Assert.AreEqual(ServerTick.From(91), future.AcceptedIntent.ReceivedAtTick);
            Assert.AreEqual(ServerTick.From(84), older.AcceptedIntent.ReceivedAtTick);
            CollectionAssert.AreEqual(new[] { older.AcceptedIntent }, olderBatch.Batch.Intents);
            CollectionAssert.AreEqual(new[] { future.AcceptedIntent }, futureBatch.Batch.Intents);
        }

        [Test]
        public void Last_representable_sequence_is_admitted_once_then_the_tick_is_exhausted_without_gap_reuse_or_resurrection()
        {
            using var buffer = new Gate3NetworkIntentAdmissionBuffer(CurrentShift);
            var tick = ServerTick.From(101);
            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.Admitted,
                buffer.Admit(Resolved(connection: 1, receiveTick: tick.Value, Envelope("seed"), "actor")).Status);
            SetPendingTickNextSequence(buffer, tick, ServerReceiveSequence.From(long.MaxValue));

            var last = buffer.Admit(Resolved(connection: 2, receiveTick: tick.Value, Envelope("last"), "actor"));
            var exhausted = buffer.Admit(Resolved(connection: 3, receiveTick: tick.Value, Envelope("exhausted"), "actor"));
            var retransmissionAtFutureTick = buffer.Admit(Resolved(connection: 4, receiveTick: tick.Value + 1, Envelope("exhausted"), "actor"));
            var otherTick = buffer.Admit(Resolved(connection: 5, receiveTick: tick.Value + 1, Envelope("other_tick"), "actor"));

            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.Admitted, last.Status);
            Assert.AreEqual(ServerReceiveSequence.From(long.MaxValue), last.AcceptedIntent.ReceiveSequence);
            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.ReceiveSequenceExhausted, exhausted.Status);
            Assert.IsFalse(exhausted.HasAcceptedIntent);
            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.DuplicateIntentId, retransmissionAtFutureTick.Status);
            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.Admitted, otherTick.Status);
            Assert.AreEqual(ServerReceiveSequence.Zero, otherTick.AcceptedIntent.ReceiveSequence);
        }

        [Test]
        public void Structurally_valid_gameplay_invalid_evidence_is_not_prevalidated_by_network_admission()
        {
            using var buffer = new Gate3NetworkIntentAdmissionBuffer(CurrentShift);
            var gameplayInvalid = new IntentEnvelope(
                CurrentShift,
                IntentId.From("gameplay_invalid"),
                ActorId.From("forged_hint"),
                TargetId.From("nonexistent_target"),
                IntentActionId.From("unsupported_action"),
                StateVersion.From(999),
                ServerTick.From(2),
                NoIntentParameters.Instance);

            var result = buffer.Admit(Resolved(connection: 9, receiveTick: 111, gameplayInvalid, "trusted_actor"));

            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.Admitted, result.Status);
            Assert.AreSame(gameplayInvalid, result.AcceptedIntent.Envelope);
        }

        [Test]
        public void Dispose_is_deterministic_and_later_admission_or_materialization_fails_closed()
        {
            var buffer = new Gate3NetworkIntentAdmissionBuffer(CurrentShift);
            buffer.Dispose();
            buffer.Dispose();

            var admission = buffer.Admit(Resolved(connection: 1, receiveTick: 121, Envelope("after_dispose"), "actor"));
            var materialization = buffer.Materialize(CurrentShift, ServerTick.From(121));

            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.BufferDisposed, admission.Status);
            Assert.IsFalse(admission.HasAcceptedIntent);
            Assert.AreEqual(Gate3NetworkIntentMaterializationStatus.BufferDisposed, materialization.Status);
            Assert.IsFalse(materialization.HasBatch);
        }

        private static Gate3ResolvedNetworkIntentEvidence Resolved(int connection, long receiveTick, IntentEnvelope envelope, string actor)
        {
            Assert.IsTrue(Gate3ServerConnectionId.TryFromServerObservedTransportId(connection, out var connectionId));
            var constructor = typeof(Gate3ResolvedNetworkIntentEvidence).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Gate3ServerConnectionId), typeof(ServerTick), typeof(IntentEnvelope), typeof(ActorId) },
                null);
            Assert.IsNotNull(constructor, "TLAW-080 resolved evidence must retain its producer-only constructor.");
            return (Gate3ResolvedNetworkIntentEvidence)constructor.Invoke(new object[]
            {
                connectionId,
                ServerTick.From(receiveTick),
                envelope,
                ActorId.From(actor)
            });
        }

        private static IntentEnvelope Envelope(
            string intent,
            string shift = "current_shift",
            string actorHint = "client_hint")
        {
            return new IntentEnvelope(
                ShiftId.From(shift),
                IntentId.From(intent),
                ActorId.From(actorHint),
                TargetId.From("target"),
                IntentActionId.From("action"),
                StateVersion.Zero,
                ServerTick.Zero,
                NoIntentParameters.Instance);
        }

        private static void SetPendingTickNextSequence(
            Gate3NetworkIntentAdmissionBuffer buffer,
            ServerTick tick,
            ServerReceiveSequence sequence)
        {
            var bucketsField = typeof(Gate3NetworkIntentAdmissionBuffer).GetField(
                "_pendingByReceiveTick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(bucketsField, "The bounded exhaustion test requires the one per-tick pending-bucket store.");
            var buckets = (System.Collections.IDictionary)bucketsField.GetValue(buffer);
            var bucket = buckets[tick];
            Assert.IsNotNull(bucket, "The focused setup must create the requested receive-tick bucket.");
            var nextSequenceField = bucket.GetType().GetField("NextSequence", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(nextSequenceField, "The bucket must retain the one ServerReceiveSequence allocation state.");
            nextSequenceField.SetValue(bucket, sequence);
        }
    }
}
