using System;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace TheLogsAreWrong.Gate3
{
    /// <summary>D-026 outer framing. Its sole field is the frozen V1 client-result payload.</summary>
    public struct Gate3ClientIntentResultCarrierBroadcast : IBroadcast
    {
        public byte[] Payload;
    }

    /// <summary>Server-local observability for the one bounded reliable result-delivery seam.</summary>
    public enum Gate3ClientIntentResultDeliveryStatus
    {
        Delivered,
        DeliveryNotAuthorized,
        ServerUnavailable,
        CodecFailure
    }

    /// <summary>
    /// The one Reliable server-to-original-client carrier. It takes only a retained D-026 result and an exact live
    /// server-observed origin; it has no receiver registration, ACK, timer, query, transport lifecycle, or gameplay role.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Gate3ClientIntentResultCarrier : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager _networkManager;

        [SerializeField]
        private Gate3ServerConnectionActorBindingBridge _connectionBinding;

        public Gate3ClientIntentResultDeliveryStatus LastDeliveryStatus { get; private set; }
        public byte[] LastDeliveredPayload { get; private set; }

        private void Awake()
        {
            if (_networkManager == null || _networkManager.ServerManager == null || _connectionBinding == null)
            {
                throw new InvalidOperationException("The Gate-3 D-026 result carrier requires the committed NetworkManager and connection binding bridge.");
            }
        }

        /// <summary>Attempts one immediate Reliable delivery to the exact original currently live connection lifetime.</summary>
        internal bool TryDeliver(Gate3NetworkOrigin origin, Gate3ClientIntentDisposition disposition)
        {
            LastDeliveredPayload = null;
            if (!origin.IsValid
                || disposition == null
                || !_connectionBinding.TryGetLiveConnectionLifetime(origin.ConnectionId, out var currentLifetime)
                || currentLifetime != origin.Lifetime)
            {
                LastDeliveryStatus = Gate3ClientIntentResultDeliveryStatus.DeliveryNotAuthorized;
                return false;
            }

            var server = _networkManager.ServerManager;
            if (server == null
                || !server.Started
                || !server.Clients.TryGetValue(origin.ConnectionId.TransportConnectionId, out var connection)
                || connection == null
                || connection.ClientId != origin.ConnectionId.TransportConnectionId
                || !connection.IsAuthenticated)
            {
                LastDeliveryStatus = Gate3ClientIntentResultDeliveryStatus.ServerUnavailable;
                return false;
            }

            if (!Gate3ClientIntentResultV1Codec.TryEncode(disposition, out var payload, out _))
            {
                LastDeliveryStatus = Gate3ClientIntentResultDeliveryStatus.CodecFailure;
                throw new InvalidOperationException("A ledger-owned D-026 disposition did not satisfy its frozen V1 codec contract.");
            }

            server.Broadcast(connection, new Gate3ClientIntentResultCarrierBroadcast { Payload = payload }, requireAuthenticated: true, channel: Channel.Reliable);
            LastDeliveredPayload = payload;
            LastDeliveryStatus = Gate3ClientIntentResultDeliveryStatus.Delivered;
            return true;
        }
    }
}
