using System;
using System.Linq;
using System.Reflection;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Transporting;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Gate3.Tests
{
    /// <summary>Executable TLAW-079 contracts for authenticated carrier ingress before any admission boundary.</summary>
    public sealed class Tlaw079IntentCarrierIngressTests
    {
        [Test]
        public void Carrier_is_one_fishnet_broadcast_with_only_the_d023_payload_bytes()
        {
            Assert.IsTrue(typeof(IBroadcast).IsAssignableFrom(typeof(Gate3IntentCarrierBroadcast)));
            var fields = typeof(Gate3IntentCarrierBroadcast).GetFields(BindingFlags.Instance | BindingFlags.Public);
            Assert.AreEqual(1, fields.Length);
            Assert.AreEqual("Payload", fields[0].Name);
            Assert.AreEqual(typeof(byte[]), fields[0].FieldType);
        }

        [Test]
        public void Reliable_none_payload_returns_exact_connection_receive_tick_and_decoded_evidence()
        {
            var envelope = CreateEnvelope();
            var processor = CreateProcessor(Observed(4));

            var result = processor.Process(Connection(0), new Gate3IntentCarrierBroadcast { Payload = Encode(envelope) }, Channel.Reliable);

            Assert.AreEqual(Gate3IntentCarrierIngressStatus.Decoded, result.Status);
            Assert.IsTrue(result.HasEvidence);
            Assert.AreEqual(0, result.Evidence.ConnectionId.TransportConnectionId);
            Assert.AreEqual(4, result.Evidence.AuthoritativeReceiveTick.Value);
            AssertEnvelope(result.Evidence.Envelope, envelope);
        }

        [Test]
        public void Procedure_payload_preserves_attempted_item_and_structurally_valid_unknown_action()
        {
            var envelope = CreateEnvelope(
                action: "future_structurally_valid_action",
                parameters: new ProcedureActionIntentParameters(ItemId.From("holy_water")));
            var result = CreateProcessor(Observed(7)).Process(
                Connection(3),
                new Gate3IntentCarrierBroadcast { Payload = Encode(envelope) },
                Channel.Reliable);

            Assert.AreEqual(Gate3IntentCarrierIngressStatus.Decoded, result.Status);
            Assert.AreEqual("future_structurally_valid_action", result.Evidence.Envelope.Action.Value);
            var parameters = result.Evidence.Envelope.Parameters as ProcedureActionIntentParameters;
            Assert.IsNotNull(parameters);
            Assert.AreEqual("holy_water", parameters.AttemptedItem.Value);
        }

        [Test]
        public void Unreliable_carrier_fails_closed_before_receive_observation_or_decode()
        {
            var observations = 0;
            var processor = new Gate3IntentCarrierIngressProcessor(() =>
            {
                observations++;
                return Observed(0);
            });

            var result = processor.Process(Connection(1), new Gate3IntentCarrierBroadcast { Payload = new byte[] { 0 } }, Channel.Unreliable);

            Assert.AreEqual(Gate3IntentCarrierIngressStatus.UnexpectedChannel, result.Status);
            Assert.IsFalse(result.HasEvidence);
            Assert.AreEqual(Gate3IntentWireV1Failure.NONE, result.CodecFailure);
            Assert.AreEqual(0, observations);
        }

        [Test]
        public void Invalid_server_connection_id_fails_closed_without_observation_or_client_payload_substitution()
        {
            var observations = 0;
            var processor = new Gate3IntentCarrierIngressProcessor(() =>
            {
                observations++;
                return Observed(0);
            });

            var result = processor.Process(Connection(-1), new Gate3IntentCarrierBroadcast { Payload = Encode(CreateEnvelope(actor: "client_claim")) }, Channel.Reliable);

            Assert.AreEqual(Gate3IntentCarrierIngressStatus.InvalidServerConnection, result.Status);
            Assert.IsFalse(result.HasEvidence);
            Assert.AreEqual(0, observations);
        }

        [Test]
        public void Receive_tick_unavailable_wins_before_malformed_payload_decode_and_produces_no_evidence()
        {
            var observations = 0;
            var processor = new Gate3IntentCarrierIngressProcessor(() =>
            {
                observations++;
                return Rejected(Gate3ServerReceiveTickObservationStatus.OwnerNotRunning);
            });

            var result = processor.Process(Connection(2), new Gate3IntentCarrierBroadcast { Payload = new byte[] { 0 } }, Channel.Reliable);

            Assert.AreEqual(Gate3IntentCarrierIngressStatus.ReceiveTickUnavailable, result.Status);
            Assert.AreEqual(Gate3ServerReceiveTickObservationStatus.OwnerNotRunning, result.ReceiveTickStatus);
            Assert.AreEqual(Gate3IntentWireV1Failure.NONE, result.CodecFailure);
            Assert.IsFalse(result.HasEvidence);
            Assert.AreEqual(1, observations);
        }

        [Test]
        public void Clock_fault_receive_tick_outcome_stops_before_decode_without_reinterpreting_the_fault()
        {
            var result = CreateProcessor(Rejected(Gate3ServerReceiveTickObservationStatus.ClockFaulted)).Process(
                Connection(2),
                new Gate3IntentCarrierBroadcast { Payload = new byte[] { 0 } },
                Channel.Reliable);

            Assert.AreEqual(Gate3IntentCarrierIngressStatus.ReceiveTickUnavailable, result.Status);
            Assert.AreEqual(Gate3ServerReceiveTickObservationStatus.ClockFaulted, result.ReceiveTickStatus);
            Assert.IsFalse(result.HasEvidence);
        }

        [Test]
        public void Existing_d023_failures_remain_local_and_produce_no_decoded_evidence()
        {
            var malformed = CreateProcessor(Observed(1)).Process(
                Connection(4),
                new Gate3IntentCarrierBroadcast { Payload = new byte[] { 0 } },
                Channel.Reliable);
            Assert.AreEqual(Gate3IntentCarrierIngressStatus.CodecFailure, malformed.Status);
            Assert.AreEqual(Gate3IntentWireV1Failure.TRUNCATED_OR_MALFORMED_FRAME, malformed.CodecFailure);
            Assert.IsFalse(malformed.HasEvidence);

            var oversized = CreateProcessor(Observed(1)).Process(
                Connection(4),
                new Gate3IntentCarrierBroadcast { Payload = new byte[Gate3IntentWireV1Codec.MaxPayloadBytes + 1] },
                Channel.Reliable);
            Assert.AreEqual(Gate3IntentCarrierIngressStatus.CodecFailure, oversized.Status);
            Assert.AreEqual(Gate3IntentWireV1Failure.MESSAGE_TOO_LARGE, oversized.CodecFailure);
            Assert.IsFalse(oversized.HasEvidence);
        }

        [Test]
        public void Decoded_actor_hint_is_preserved_only_as_client_evidence()
        {
            var envelope = CreateEnvelope(actor: "untrusted_client_hint");
            var result = CreateProcessor(Observed(9)).Process(
                Connection(6),
                new Gate3IntentCarrierBroadcast { Payload = Encode(envelope) },
                Channel.Reliable);

            Assert.IsTrue(result.HasEvidence);
            Assert.AreEqual(6, result.Evidence.ConnectionId.TransportConnectionId);
            Assert.AreEqual("untrusted_client_hint", result.Evidence.Envelope.ActorIdHint.Value);
        }

        private static Gate3IntentCarrierIngressProcessor CreateProcessor(Gate3ServerReceiveTickObservation observation)
        {
            return new Gate3IntentCarrierIngressProcessor(() => observation);
        }

        private static NetworkConnection Connection(int clientId)
        {
            return new NetworkConnection { ClientId = clientId };
        }

        private static Gate3ServerReceiveTickObservation Observed(long tick)
        {
            return (Gate3ServerReceiveTickObservation)typeof(Gate3ServerReceiveTickObservation)
                .GetMethod("Observed", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { ServerTick.From(tick) });
        }

        private static Gate3ServerReceiveTickObservation Rejected(Gate3ServerReceiveTickObservationStatus status)
        {
            return (Gate3ServerReceiveTickObservation)typeof(Gate3ServerReceiveTickObservation)
                .GetMethod("Rejected", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { status });
        }

        private static byte[] Encode(IntentEnvelope envelope)
        {
            Assert.IsTrue(Gate3IntentWireV1Codec.TryEncode(envelope, out var payload, out var failure), failure.ToString());
            return payload;
        }

        private static IntentEnvelope CreateEnvelope(
            string shift = "shift",
            string intent = "intent",
            string actor = "actor_hint",
            string target = "target",
            string action = "action",
            IIntentParameters parameters = null)
        {
            return new IntentEnvelope(
                ShiftId.From(shift),
                IntentId.From(intent),
                ActorId.From(actor),
                TargetId.From(target),
                IntentActionId.From(action),
                StateVersion.From(7),
                ServerTick.From(11),
                parameters ?? NoIntentParameters.Instance);
        }

        private static void AssertEnvelope(IntentEnvelope actual, IntentEnvelope expected)
        {
            Assert.AreEqual(expected.ShiftId, actual.ShiftId);
            Assert.AreEqual(expected.IntentId, actual.IntentId);
            Assert.AreEqual(expected.ActorIdHint, actual.ActorIdHint);
            Assert.AreEqual(expected.TargetId, actual.TargetId);
            Assert.AreEqual(expected.Action, actual.Action);
            Assert.AreEqual(expected.ExpectedStateVersion, actual.ExpectedStateVersion);
            Assert.AreEqual(expected.ClientObservedTick, actual.ClientObservedTick);
            Assert.AreEqual(expected.Parameters.GetType(), actual.Parameters.GetType());
        }
    }
}
