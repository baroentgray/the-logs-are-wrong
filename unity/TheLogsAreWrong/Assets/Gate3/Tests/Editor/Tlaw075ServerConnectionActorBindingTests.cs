using System.Reflection;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Identifiers;
using UnityEngine;

namespace TheLogsAreWrong.Gate3.Tests
{
    /// <summary>Deterministic contracts for the transient server-owned connection-to-actor boundary.</summary>
    public sealed class Tlaw075ServerConnectionActorBindingTests
    {
        [Test]
        public void Registering_a_server_observed_connection_keeps_it_live_and_unbound()
        {
            var registry = new Gate3ServerConnectionActorRegistry();
            var connection = Connection(0);

            Assert.AreEqual(Gate3ServerConnectionRegistrationResult.Registered, registry.RegisterLiveConnection(connection));
            Assert.AreEqual(Gate3ServerConnectionRegistrationResult.AlreadyLive, registry.RegisterLiveConnection(connection));
            Assert.IsTrue(registry.IsLive(connection));
            Assert.AreEqual(1, registry.LiveConnectionCount);
            Assert.AreEqual(0, registry.BindingCount);

            var resolution = registry.ResolveAuthoritativeActor(connection, ActorId.From("untrusted_hint"));
            Assert.AreEqual(Gate3AuthoritativeActorResolutionStatus.ActorNotBound, resolution.Status);
            Assert.IsFalse(resolution.HasActor);
        }

        [Test]
        public void Trusted_binding_resolves_the_stored_actor_and_ignores_the_untrusted_hint()
        {
            var registry = new Gate3ServerConnectionActorRegistry();
            var connection = Connection(4);
            var authoritative = ActorId.From("server_actor");
            registry.RegisterLiveConnection(connection);

            Assert.AreEqual(Gate3ServerConnectionActorBindingResult.Bound, registry.Bind(connection, authoritative));
            Assert.AreEqual(Gate3ServerConnectionActorBindingResult.AlreadyBound, registry.Bind(connection, authoritative));
            Assert.AreEqual(1, registry.BindingCount);

            var resolution = registry.ResolveAuthoritativeActor(connection, ActorId.From("forged_other_actor"));
            Assert.AreEqual(Gate3AuthoritativeActorResolutionStatus.Resolved, resolution.Status);
            Assert.IsTrue(resolution.HasActor);
            Assert.AreEqual(authoritative, resolution.Actor);
        }

        [Test]
        public void Conflicts_fail_closed_without_partially_mutating_either_connection()
        {
            var registry = new Gate3ServerConnectionActorRegistry();
            var first = Connection(7);
            var second = Connection(8);
            var firstActor = ActorId.From("first_actor");
            var secondActor = ActorId.From("second_actor");
            registry.RegisterLiveConnection(first);
            registry.RegisterLiveConnection(second);
            Assert.AreEqual(Gate3ServerConnectionActorBindingResult.Bound, registry.Bind(first, firstActor));

            Assert.AreEqual(Gate3ServerConnectionActorBindingResult.ConnectionAlreadyBound, registry.Bind(first, secondActor));
            Assert.AreEqual(Gate3ServerConnectionActorBindingResult.ActorAlreadyBound, registry.Bind(second, firstActor));
            Assert.AreEqual(1, registry.BindingCount);

            Assert.AreEqual(firstActor, registry.ResolveAuthoritativeActor(first, secondActor).Actor);
            Assert.AreEqual(Gate3AuthoritativeActorResolutionStatus.ActorNotBound,
                registry.ResolveAuthoritativeActor(second, firstActor).Status);
            Assert.AreEqual(Gate3ServerConnectionActorBindingResult.Bound, registry.Bind(second, secondActor));
            Assert.AreEqual(2, registry.BindingCount);
        }

        [Test]
        public void Default_or_invalid_identity_and_default_actor_fail_closed()
        {
            var registry = new Gate3ServerConnectionActorRegistry();
            Assert.IsFalse(Gate3ServerConnectionId.TryFromServerObservedTransportId(-1, out var invalid));

            Assert.AreEqual(Gate3ServerConnectionRegistrationResult.InvalidConnection, registry.RegisterLiveConnection(default));
            Assert.AreEqual(Gate3ServerConnectionRegistrationResult.InvalidConnection, registry.RegisterLiveConnection(invalid));
            Assert.AreEqual(Gate3ServerConnectionActorBindingResult.InvalidConnection, registry.Bind(default, ActorId.From("actor")));

            var connection = Connection(12);
            registry.RegisterLiveConnection(connection);
            Assert.AreEqual(Gate3ServerConnectionActorBindingResult.InvalidActor, registry.Bind(connection, default));
            Assert.AreEqual(Gate3AuthoritativeActorResolutionStatus.InvalidConnection,
                registry.ResolveAuthoritativeActor(default, null).Status);
            Assert.AreEqual(0, registry.BindingCount);
        }

        [Test]
        public void Disconnect_removes_the_binding_and_a_recycled_connection_starts_unbound()
        {
            var registry = new Gate3ServerConnectionActorRegistry();
            var recycled = Connection(16);
            var actor = ActorId.From("prior_actor");
            registry.RegisterLiveConnection(recycled);
            registry.Bind(recycled, actor);

            Assert.AreEqual(Gate3ServerConnectionDisconnectResult.Disconnected, registry.Disconnect(recycled));
            Assert.AreEqual(Gate3ServerConnectionDisconnectResult.AlreadyAbsent, registry.Disconnect(recycled));
            Assert.AreEqual(Gate3AuthoritativeActorResolutionStatus.ConnectionNotLive,
                registry.ResolveAuthoritativeActor(recycled, null).Status);

            Assert.AreEqual(Gate3ServerConnectionRegistrationResult.Registered, registry.RegisterLiveConnection(recycled));
            Assert.AreEqual(Gate3AuthoritativeActorResolutionStatus.ActorNotBound,
                registry.ResolveAuthoritativeActor(recycled, actor).Status);
            Assert.AreEqual(0, registry.BindingCount);
        }

        [Test]
        public void Server_teardown_clears_every_transient_live_connection_and_binding()
        {
            var registry = new Gate3ServerConnectionActorRegistry();
            var first = Connection(21);
            var second = Connection(22);
            registry.RegisterLiveConnection(first);
            registry.RegisterLiveConnection(second);
            registry.Bind(first, ActorId.From("actor_one"));
            registry.Bind(second, ActorId.From("actor_two"));

            registry.ClearForServerTeardown();

            Assert.AreEqual(0, registry.LiveConnectionCount);
            Assert.AreEqual(0, registry.BindingCount);
            Assert.AreEqual(Gate3AuthoritativeActorResolutionStatus.ConnectionNotLive,
                registry.ResolveAuthoritativeActor(first, null).Status);
            Assert.AreEqual(Gate3AuthoritativeActorResolutionStatus.ConnectionNotLive,
                registry.ResolveAuthoritativeActor(second, null).Status);
        }

        [Test]
        public void Production_bridge_observes_the_real_fishy_listen_host_connection_callback_and_server_teardown()
        {
            var root = new GameObject("Tlaw075BridgeProbe");
            root.SetActive(false);
            try
            {
                root.AddComponent<NetworkManager>();
                var transportManager = root.AddComponent<TransportManager>();
                var transport = root.AddComponent<FishySteamworks.FishySteamworks>();
                transportManager.Transport = transport;
                var bridge = root.AddComponent<Gate3ServerConnectionActorBindingBridge>();
                typeof(Gate3ServerConnectionActorBindingBridge)
                    .GetField("_transport", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(bridge, transport);
                typeof(Gate3ServerConnectionActorBindingBridge)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(bridge, null);

                transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionState.Started, short.MaxValue, 0));
                Assert.AreEqual(1, bridge.LiveConnectionCount);
                Assert.AreEqual(0, bridge.BindingCount);

                transport.HandleServerConnectionState(new ServerConnectionStateArgs(LocalConnectionState.Stopped, 0));
                Assert.AreEqual(0, bridge.LiveConnectionCount);
                Assert.AreEqual(0, bridge.BindingCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Gate3ServerConnectionId Connection(int serverObservedTransportId)
        {
            Assert.IsTrue(Gate3ServerConnectionId.TryFromServerObservedTransportId(serverObservedTransportId, out var connection));
            return connection;
        }
    }
}
