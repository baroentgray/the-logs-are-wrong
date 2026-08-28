using System;
using System.Reflection;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Gate2;
using UnityEngine;

namespace TheLogsAreWrong.Gate3.Tests
{
    /// <summary>Executable TLAW-080 contracts for actor resolution after decoded ingress and before admission.</summary>
    public sealed class Tlaw080ActorResolutionCompositionTests
    {
        [Test]
        public void Decoded_evidence_forwards_the_exact_connection_and_untrusted_hint_to_the_existing_resolver()
        {
            var decoded = Decoded(connectionId: 14, receiveTick: 23, actorHint: "untrusted_hint");
            Gate3ServerConnectionId capturedConnection = default;
            ActorId? capturedHint = null;
            var authoritative = ActorId.From("server_bound_actor");
            var processor = new Gate3ActorResolutionProcessor((connection, hint) =>
            {
                capturedConnection = connection;
                capturedHint = hint;
                return Resolved(authoritative);
            });

            var result = processor.Process(decoded);

            Assert.AreEqual(Gate3AuthoritativeActorResolutionStatus.Resolved, result.Status);
            Assert.IsTrue(result.HasEvidence);
            Assert.AreEqual(decoded.ConnectionId, capturedConnection);
            Assert.AreEqual(decoded.Envelope.ActorIdHint, capturedHint);
            Assert.AreEqual(authoritative, result.Evidence.AuthoritativeActor);
        }

        [Test]
        public void Live_bound_connection_resolves_to_registry_actor_despite_a_different_client_hint_and_preserves_evidence_exactly()
        {
            var registry = new Gate3ServerConnectionActorRegistry();
            var connection = Connection(27);
            var authoritative = ActorId.From("server_actor");
            Assert.AreEqual(Gate3ServerConnectionRegistrationResult.Registered, registry.RegisterLiveConnection(connection));
            Assert.AreEqual(Gate3ServerConnectionActorBindingResult.Bound, registry.Bind(connection, authoritative));
            var decoded = Decoded(connectionId: 27, receiveTick: 37, actorHint: "forged_other_actor");

            var result = new Gate3ActorResolutionProcessor(registry.ResolveAuthoritativeActor).Process(decoded);

            Assert.AreEqual(Gate3AuthoritativeActorResolutionStatus.Resolved, result.Status);
            Assert.IsTrue(result.HasEvidence);
            Assert.AreEqual(connection, result.Evidence.ConnectionId);
            Assert.AreEqual(ServerTick.From(37), result.Evidence.AuthoritativeReceiveTick);
            Assert.AreSame(decoded.Envelope, result.Evidence.Envelope);
            Assert.AreEqual(authoritative, result.Evidence.AuthoritativeActor);
            Assert.AreNotEqual(result.Evidence.Envelope.ActorIdHint, result.Evidence.AuthoritativeActor);
        }

        [Test]
        public void Live_bound_connection_resolves_when_the_input_evidence_actor_hint_is_absent()
        {
            var registry = new Gate3ServerConnectionActorRegistry();
            var connection = Connection(28);
            var authoritative = ActorId.From("server_actor");
            registry.RegisterLiveConnection(connection);
            registry.Bind(connection, authoritative);

            var envelope = CreateEnvelope("present_before_probe");
            SetAutoProperty(envelope, "ActorIdHint", default(ActorId));
            var decoded = Decoded(connection, ServerTick.From(41), envelope);

            var result = new Gate3ActorResolutionProcessor(registry.ResolveAuthoritativeActor).Process(decoded);

            Assert.AreEqual(Gate3AuthoritativeActorResolutionStatus.Resolved, result.Status);
            Assert.IsTrue(result.HasEvidence);
            Assert.IsTrue(result.Evidence.Envelope.ActorIdHint.IsDefault);
            Assert.AreEqual(authoritative, result.Evidence.AuthoritativeActor);
        }

        [TestCase(Gate3AuthoritativeActorResolutionStatus.InvalidConnection)]
        [TestCase(Gate3AuthoritativeActorResolutionStatus.ConnectionNotLive)]
        [TestCase(Gate3AuthoritativeActorResolutionStatus.ActorNotBound)]
        public void Existing_resolver_failures_remain_distinct_and_produce_no_resolved_evidence(Gate3AuthoritativeActorResolutionStatus expected)
        {
            var registry = new Gate3ServerConnectionActorRegistry();
            Gate3DecodedNetworkIntentEvidence decoded;
            if (expected == Gate3AuthoritativeActorResolutionStatus.InvalidConnection)
            {
                decoded = Decoded(default, ServerTick.From(43), CreateEnvelope("untrusted_hint"));
            }
            else
            {
                var connectionId = expected == Gate3AuthoritativeActorResolutionStatus.ConnectionNotLive ? 33 : 34;
                decoded = Decoded(connectionId, 43, "untrusted_hint");
                if (expected == Gate3AuthoritativeActorResolutionStatus.ActorNotBound)
                {
                    registry.RegisterLiveConnection(decoded.ConnectionId);
                }
            }

            var result = new Gate3ActorResolutionProcessor(registry.ResolveAuthoritativeActor).Process(decoded);

            Assert.AreEqual(expected, result.Status);
            Assert.IsFalse(result.HasEvidence);
        }

        [Test]
        public void Production_composition_consumes_the_carrier_success_event_and_stops_at_resolved_local_evidence()
        {
            var root = new GameObject("Tlaw080ProductionComposition");
            root.SetActive(false);
            try
            {
                var networkManager = root.AddComponent<NetworkManager>();
                var serverManager = root.AddComponent<ServerManager>();
                SetAutoProperty(networkManager, "ServerManager", serverManager);
                SetAutoProperty(serverManager, "NetworkManager", networkManager);
                var transportManager = root.AddComponent<TransportManager>();
                var transport = root.AddComponent<FishySteamworks.FishySteamworks>();
                transportManager.Transport = transport;
                var hostDriver = root.AddComponent<Gate2ProductionHostDriver>();
                var carrier = root.AddComponent<Gate3IntentCarrierIngress>();
                var binding = root.AddComponent<Gate3ServerConnectionActorBindingBridge>();
                var composition = root.AddComponent<Gate3ActorResolutionComposition>();

                SetPrivateField(carrier, "_networkManager", networkManager);
                SetPrivateField(carrier, "_hostDriver", hostDriver);
                SetPrivateField(binding, "_transport", transport);
                SetPrivateField(composition, "_carrierIngress", carrier);
                SetPrivateField(composition, "_connectionBinding", binding);
                InvokeLifecycle(binding, "Awake");
                InvokeLifecycle(carrier, "Awake");
                InvokeLifecycle(composition, "Awake");
                SetPrivateField(carrier, "_processor", new Gate3IntentCarrierIngressProcessor(() => Observed(47)));
                transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionState.Started, 39, 0));
                var authoritative = ActorId.From("server_bound_actor");
                Assert.AreEqual(Gate3ServerConnectionActorBindingResult.Bound, binding.BindTrustedServerActor(Connection(39), authoritative));

                InvokeLifecycle(composition, "OnEnable");
                InvokeCarrierCallback(carrier, new NetworkConnection { ClientId = 39 }, new Gate3IntentCarrierBroadcast { Payload = Encode(CreateEnvelope("forged_actor")) });

                Assert.AreEqual(Gate3AuthoritativeActorResolutionStatus.Resolved, composition.LastResult.Status);
                Assert.IsTrue(composition.LastResult.HasEvidence);
                Assert.AreEqual(39, composition.LastResult.Evidence.ConnectionId.TransportConnectionId);
                Assert.AreEqual(ServerTick.From(47), composition.LastResult.Evidence.AuthoritativeReceiveTick);
                Assert.AreEqual(authoritative, composition.LastResult.Evidence.AuthoritativeActor);
                Assert.AreEqual("forged_actor", composition.LastResult.Evidence.Envelope.ActorIdHint.Value);

                InvokeLifecycle(composition, "OnDisable");
                InvokeLifecycle(composition, "OnDisable");
                InvokeLifecycle(composition, "OnDestroy");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Gate3DecodedNetworkIntentEvidence Decoded(int connectionId, long receiveTick, string actorHint)
        {
            var result = new Gate3IntentCarrierIngressProcessor(() => Observed(receiveTick)).Process(
                new NetworkConnection { ClientId = connectionId },
                new Gate3IntentCarrierBroadcast { Payload = Encode(CreateEnvelope(actorHint)) },
                Channel.Reliable);
            Assert.IsTrue(result.HasEvidence);
            return result.Evidence;
        }

        private static Gate3DecodedNetworkIntentEvidence Decoded(Gate3ServerConnectionId connectionId, ServerTick receiveTick, IntentEnvelope envelope)
        {
            var constructor = typeof(Gate3DecodedNetworkIntentEvidence).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Gate3ServerConnectionId), typeof(ServerTick), typeof(IntentEnvelope) },
                null);
            Assert.IsNotNull(constructor, "TLAW-079 decoded evidence must keep its internal producer-only constructor.");
            return (Gate3DecodedNetworkIntentEvidence)constructor.Invoke(new object[] { connectionId, receiveTick, envelope });
        }

        private static Gate3AuthoritativeActorResolution Resolved(ActorId actor)
        {
            var method = typeof(Gate3AuthoritativeActorResolution).GetMethod("Resolved", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (Gate3AuthoritativeActorResolution)method.Invoke(null, new object[] { actor });
        }

        private static Gate3ServerConnectionId Connection(int serverObservedTransportId)
        {
            Assert.IsTrue(Gate3ServerConnectionId.TryFromServerObservedTransportId(serverObservedTransportId, out var connection));
            return connection;
        }

        private static Gate3ServerReceiveTickObservation Observed(long tick)
        {
            var method = typeof(Gate3ServerReceiveTickObservation).GetMethod("Observed", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (Gate3ServerReceiveTickObservation)method.Invoke(null, new object[] { ServerTick.From(tick) });
        }

        private static byte[] Encode(IntentEnvelope envelope)
        {
            Assert.IsTrue(Gate3IntentWireV1Codec.TryEncode(envelope, out var payload, out var failure), failure.ToString());
            return payload;
        }

        private static IntentEnvelope CreateEnvelope(string actorHint)
        {
            return new IntentEnvelope(
                ShiftId.From("shift"),
                IntentId.From("intent"),
                ActorId.From(actorHint),
                TargetId.From("target"),
                IntentActionId.From("action"),
                StateVersion.From(7),
                ServerTick.From(11),
                NoIntentParameters.Instance);
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            var field = target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Expected auto-property backing field for {target.GetType().Name}.{propertyName}.");
            field.SetValue(target, value);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Expected private field {target.GetType().Name}.{fieldName}.");
            field.SetValue(target, value);
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Expected lifecycle method {target.GetType().Name}.{methodName}.");
            method.Invoke(target, null);
        }

        private static void InvokeCarrierCallback(Gate3IntentCarrierIngress carrier, NetworkConnection connection, Gate3IntentCarrierBroadcast broadcast)
        {
            var method = typeof(Gate3IntentCarrierIngress).GetMethod("OnCarrierBroadcast", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Expected the one TLAW-079 server callback.");
            method.Invoke(carrier, new object[] { connection, broadcast, Channel.Reliable });
        }
    }
}
