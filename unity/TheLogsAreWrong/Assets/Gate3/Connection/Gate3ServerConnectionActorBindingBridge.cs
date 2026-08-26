using System;
using FishNet.Transporting;
using TheLogsAreWrong.Domain.Identifiers;
using UnityEngine;

namespace TheLogsAreWrong.Gate3
{
    /// <summary>
    /// Production bridge from the existing server-side Fishy callbacks into the transient registry.
    /// It does not start or stop transport and does not select actors for newly live connections.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Gate3ServerConnectionActorBindingBridge : MonoBehaviour
    {
        [SerializeField]
        private FishySteamworks.FishySteamworks _transport;

        private readonly Gate3ServerConnectionActorRegistry _registry = new Gate3ServerConnectionActorRegistry();
        private bool _subscribed;

        public int LiveConnectionCount => _registry.LiveConnectionCount;
        public int BindingCount => _registry.BindingCount;

        private void Awake()
        {
            if (_transport == null)
            {
                throw new InvalidOperationException("The Gate-3 connection binding bridge requires the committed Fishy transport.");
            }

            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _registry.ClearForServerTeardown();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            _registry.ClearForServerTeardown();
        }

        /// <summary>For future trusted server composition only; this layer makes no actor-allocation decision.</summary>
        public Gate3ServerConnectionActorBindingResult BindTrustedServerActor(Gate3ServerConnectionId connectionId, ActorId authoritativeActor)
        {
            return _registry.Bind(connectionId, authoritativeActor);
        }

        /// <summary>Resolves only the stored server binding; the optional hint is never authority.</summary>
        public Gate3AuthoritativeActorResolution ResolveAuthoritativeActor(Gate3ServerConnectionId connectionId, ActorId? actorIdHint)
        {
            return _registry.ResolveAuthoritativeActor(connectionId, actorIdHint);
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _transport.OnRemoteConnectionState += OnRemoteConnectionState;
            _transport.OnServerConnectionState += OnServerConnectionState;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _transport == null)
            {
                return;
            }

            _transport.OnRemoteConnectionState -= OnRemoteConnectionState;
            _transport.OnServerConnectionState -= OnServerConnectionState;
            _subscribed = false;
        }

        private void OnRemoteConnectionState(RemoteConnectionStateArgs state)
        {
            if (!Gate3ServerConnectionId.TryFromServerObservedTransportId(state.ConnectionId, out var connectionId))
            {
                Debug.LogError("TLAW075_SERVER_CONNECTION_REJECTED_INVALID_ID");
                return;
            }

            if (state.ConnectionState == RemoteConnectionState.Started)
            {
                if (_registry.RegisterLiveConnection(connectionId) == Gate3ServerConnectionRegistrationResult.Registered)
                {
                    Debug.Log("TLAW075_SERVER_CONNECTION_REGISTERED=" + connectionId);
                }

                return;
            }

            if (state.ConnectionState == RemoteConnectionState.Stopped)
            {
                _registry.Disconnect(connectionId);
            }
        }

        private void OnServerConnectionState(ServerConnectionStateArgs state)
        {
            if (state.ConnectionState == LocalConnectionState.Stopped)
            {
                _registry.ClearForServerTeardown();
            }
        }
    }
}
