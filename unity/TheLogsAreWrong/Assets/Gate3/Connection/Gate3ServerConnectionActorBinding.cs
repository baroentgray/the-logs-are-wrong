using System;
using System.Collections.Generic;
using TheLogsAreWrong.Domain.Identifiers;

namespace TheLogsAreWrong.Gate3
{
    /// <summary>
    /// Canonical key for an identity observed by the server-side transport callback. The default
    /// value is deliberately invalid because Fishy's first reusable connection id is zero.
    /// </summary>
    public readonly struct Gate3ServerConnectionId : IEquatable<Gate3ServerConnectionId>
    {
        private readonly int _transportConnectionId;
        private readonly bool _isServerObserved;

        private Gate3ServerConnectionId(int transportConnectionId)
        {
            _transportConnectionId = transportConnectionId;
            _isServerObserved = true;
        }

        public bool IsValid => _isServerObserved && _transportConnectionId >= 0;

        public int TransportConnectionId => IsValid
            ? _transportConnectionId
            : throw new InvalidOperationException("The default Gate-3 server connection identity is invalid.");

        /// <summary>Creates a key only from the integer supplied by a server-observed transport callback.</summary>
        public static bool TryFromServerObservedTransportId(int transportConnectionId, out Gate3ServerConnectionId connectionId)
        {
            if (transportConnectionId < 0)
            {
                connectionId = default;
                return false;
            }

            connectionId = new Gate3ServerConnectionId(transportConnectionId);
            return true;
        }

        public bool Equals(Gate3ServerConnectionId other)
        {
            return _transportConnectionId == other._transportConnectionId && _isServerObserved == other._isServerObserved;
        }

        public override bool Equals(object obj) => obj is Gate3ServerConnectionId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_transportConnectionId, _isServerObserved);
        public override string ToString() => IsValid ? _transportConnectionId.ToString() : "invalid";
        public static bool operator ==(Gate3ServerConnectionId left, Gate3ServerConnectionId right) => left.Equals(right);
        public static bool operator !=(Gate3ServerConnectionId left, Gate3ServerConnectionId right) => !left.Equals(right);
    }

    public enum Gate3ServerConnectionRegistrationResult
    {
        Registered,
        AlreadyLive,
        InvalidConnection
    }

    public enum Gate3ServerConnectionActorBindingResult
    {
        Bound,
        AlreadyBound,
        InvalidConnection,
        ConnectionNotLive,
        InvalidActor,
        ConnectionAlreadyBound,
        ActorAlreadyBound
    }

    public enum Gate3ServerConnectionDisconnectResult
    {
        Disconnected,
        AlreadyAbsent,
        InvalidConnection
    }

    public enum Gate3AuthoritativeActorResolutionStatus
    {
        Resolved,
        InvalidConnection,
        ConnectionNotLive,
        ActorNotBound
    }

    /// <summary>Typed identity-resolution result. It carries no gameplay message or event.</summary>
    public readonly struct Gate3AuthoritativeActorResolution
    {
        private Gate3AuthoritativeActorResolution(Gate3AuthoritativeActorResolutionStatus status, ActorId actor)
        {
            Status = status;
            Actor = actor;
        }

        public Gate3AuthoritativeActorResolutionStatus Status { get; }
        public ActorId Actor { get; }
        public bool HasActor => Status == Gate3AuthoritativeActorResolutionStatus.Resolved;

        internal static Gate3AuthoritativeActorResolution Resolved(ActorId actor)
        {
            return new Gate3AuthoritativeActorResolution(Gate3AuthoritativeActorResolutionStatus.Resolved, actor);
        }

        internal static Gate3AuthoritativeActorResolution Rejected(Gate3AuthoritativeActorResolutionStatus status)
        {
            return new Gate3AuthoritativeActorResolution(status, default);
        }
    }

    /// <summary>
    /// Transient server-owned live-connection registry. It holds only connection identity and an
    /// optional server-supplied actor binding; it does not create actor assignments.
    /// </summary>
    public sealed class Gate3ServerConnectionActorRegistry
    {
        private readonly Dictionary<Gate3ServerConnectionId, ActorId?> _liveConnections = new Dictionary<Gate3ServerConnectionId, ActorId?>();
        private readonly Dictionary<ActorId, Gate3ServerConnectionId> _connectionByActor = new Dictionary<ActorId, Gate3ServerConnectionId>();

        public int LiveConnectionCount => _liveConnections.Count;
        public int BindingCount => _connectionByActor.Count;

        public Gate3ServerConnectionRegistrationResult RegisterLiveConnection(Gate3ServerConnectionId connectionId)
        {
            if (!connectionId.IsValid)
            {
                return Gate3ServerConnectionRegistrationResult.InvalidConnection;
            }

            if (_liveConnections.ContainsKey(connectionId))
            {
                return Gate3ServerConnectionRegistrationResult.AlreadyLive;
            }

            _liveConnections.Add(connectionId, null);
            return Gate3ServerConnectionRegistrationResult.Registered;
        }

        public Gate3ServerConnectionActorBindingResult Bind(Gate3ServerConnectionId connectionId, ActorId authoritativeActor)
        {
            if (!connectionId.IsValid)
            {
                return Gate3ServerConnectionActorBindingResult.InvalidConnection;
            }

            if (authoritativeActor.IsDefault)
            {
                return Gate3ServerConnectionActorBindingResult.InvalidActor;
            }

            if (!_liveConnections.TryGetValue(connectionId, out var existingActor))
            {
                return Gate3ServerConnectionActorBindingResult.ConnectionNotLive;
            }

            if (existingActor.HasValue)
            {
                return existingActor.Value == authoritativeActor
                    ? Gate3ServerConnectionActorBindingResult.AlreadyBound
                    : Gate3ServerConnectionActorBindingResult.ConnectionAlreadyBound;
            }

            if (_connectionByActor.ContainsKey(authoritativeActor))
            {
                return Gate3ServerConnectionActorBindingResult.ActorAlreadyBound;
            }

            _liveConnections[connectionId] = authoritativeActor;
            _connectionByActor.Add(authoritativeActor, connectionId);
            return Gate3ServerConnectionActorBindingResult.Bound;
        }

        public Gate3AuthoritativeActorResolution ResolveAuthoritativeActor(Gate3ServerConnectionId connectionId, ActorId? actorIdHint)
        {
            _ = actorIdHint;
            if (!connectionId.IsValid)
            {
                return Gate3AuthoritativeActorResolution.Rejected(Gate3AuthoritativeActorResolutionStatus.InvalidConnection);
            }

            if (!_liveConnections.TryGetValue(connectionId, out var actor))
            {
                return Gate3AuthoritativeActorResolution.Rejected(Gate3AuthoritativeActorResolutionStatus.ConnectionNotLive);
            }

            return actor.HasValue
                ? Gate3AuthoritativeActorResolution.Resolved(actor.Value)
                : Gate3AuthoritativeActorResolution.Rejected(Gate3AuthoritativeActorResolutionStatus.ActorNotBound);
        }

        public Gate3ServerConnectionDisconnectResult Disconnect(Gate3ServerConnectionId connectionId)
        {
            if (!connectionId.IsValid)
            {
                return Gate3ServerConnectionDisconnectResult.InvalidConnection;
            }

            if (!_liveConnections.TryGetValue(connectionId, out var actor))
            {
                return Gate3ServerConnectionDisconnectResult.AlreadyAbsent;
            }

            _liveConnections.Remove(connectionId);
            if (actor.HasValue)
            {
                _connectionByActor.Remove(actor.Value);
            }

            return Gate3ServerConnectionDisconnectResult.Disconnected;
        }

        public void ClearForServerTeardown()
        {
            _liveConnections.Clear();
            _connectionByActor.Clear();
        }

        public bool IsLive(Gate3ServerConnectionId connectionId)
        {
            return connectionId.IsValid && _liveConnections.ContainsKey(connectionId);
        }
    }

}
