using System;
using System.Collections.Generic;
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
        private readonly Dictionary<Gate3ServerConnectionId, Gate3ServerConnectionLifetime> _liveLifetimes = new Dictionary<Gate3ServerConnectionId, Gate3ServerConnectionLifetime>();
        private long _nextConnectionLifetime;
        private bool _subscribed;

        public int LiveConnectionCount => _registry.LiveConnectionCount;
        public int BindingCount => _registry.BindingCount;

        /// <summary>Signals a server-observed connection lifetime revocation without exposing actor policy.</summary>
        public event Action<Gate3ServerConnectionId, Gate3ServerConnectionLifetime> ConnectionLifetimeRevoked;

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
            ClearForServerTeardown();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            ClearForServerTeardown();
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

        /// <summary>Returns the exact currently live lifetime for D-026 origin correlation; ClientId alone is insufficient.</summary>
        public bool TryGetLiveConnectionLifetime(Gate3ServerConnectionId connectionId, out Gate3ServerConnectionLifetime lifetime)
        {
            if (_registry.IsLive(connectionId) && _liveLifetimes.TryGetValue(connectionId, out lifetime))
            {
                return true;
            }

            lifetime = default;
            return false;
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
                    _liveLifetimes.Add(connectionId, Gate3ServerConnectionLifetime.From(checked(_nextConnectionLifetime + 1)));
                    _nextConnectionLifetime = checked(_nextConnectionLifetime + 1);
                    Debug.Log("TLAW075_SERVER_CONNECTION_REGISTERED=" + connectionId);
                }

                return;
            }

            if (state.ConnectionState == RemoteConnectionState.Stopped)
            {
                RevokeConnectionLifetime(connectionId);
            }
        }

        private void OnServerConnectionState(ServerConnectionStateArgs state)
        {
            if (state.ConnectionState == LocalConnectionState.Stopped)
            {
                ClearForServerTeardown();
            }
        }

        private void RevokeConnectionLifetime(Gate3ServerConnectionId connectionId)
        {
            var disconnect = _registry.Disconnect(connectionId);
            if (disconnect != Gate3ServerConnectionDisconnectResult.Disconnected
                || !_liveLifetimes.TryGetValue(connectionId, out var lifetime))
            {
                return;
            }

            _liveLifetimes.Remove(connectionId);
            ConnectionLifetimeRevoked?.Invoke(connectionId, lifetime);
        }

        private void ClearForServerTeardown()
        {
            if (_liveLifetimes.Count > 0)
            {
                var lifetimes = new List<KeyValuePair<Gate3ServerConnectionId, Gate3ServerConnectionLifetime>>(_liveLifetimes);
                _liveLifetimes.Clear();
                foreach (var pair in lifetimes)
                {
                    ConnectionLifetimeRevoked?.Invoke(pair.Key, pair.Value);
                }
            }

            _registry.ClearForServerTeardown();
        }
    }
}
