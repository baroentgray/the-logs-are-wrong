using System;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Gate2;
using UnityEngine;

namespace TheLogsAreWrong.Gate3
{
    /// <summary>V1 carrier outer framing; its sole data field is the already-frozen D-023 payload.</summary>
    public struct Gate3IntentCarrierBroadcast : IBroadcast
    {
        public byte[] Payload;
    }

    /// <summary>Server-local outcome of the bounded carrier seam. These values are not a network ABI.</summary>
    public enum Gate3IntentCarrierIngressStatus
    {
        Decoded,
        UnexpectedChannel,
        InvalidServerConnection,
        ReceiveTickUnavailable,
        CodecFailure
    }

    /// <summary>Successful carrier evidence only; it has not been admitted, ordered, or executed.</summary>
    public readonly struct Gate3DecodedNetworkIntentEvidence
    {
        internal Gate3DecodedNetworkIntentEvidence(
            Gate3ServerConnectionId connectionId,
            ServerTick authoritativeReceiveTick,
            IntentEnvelope envelope)
        {
            ConnectionId = connectionId;
            AuthoritativeReceiveTick = authoritativeReceiveTick;
            Envelope = envelope;
        }

        public Gate3ServerConnectionId ConnectionId { get; }
        public ServerTick AuthoritativeReceiveTick { get; }
        public IntentEnvelope Envelope { get; }
    }

    /// <summary>Bounded local result. Failure creates neither a Domain event nor a network response.</summary>
    public readonly struct Gate3IntentCarrierIngressResult
    {
        private Gate3IntentCarrierIngressResult(
            Gate3IntentCarrierIngressStatus status,
            Gate3ServerReceiveTickObservationStatus receiveTickStatus,
            Gate3IntentWireV1Failure codecFailure,
            Gate3DecodedNetworkIntentEvidence evidence,
            bool hasEvidence)
        {
            Status = status;
            ReceiveTickStatus = receiveTickStatus;
            CodecFailure = codecFailure;
            Evidence = evidence;
            HasEvidence = hasEvidence;
        }

        public Gate3IntentCarrierIngressStatus Status { get; }
        public Gate3ServerReceiveTickObservationStatus ReceiveTickStatus { get; }
        public Gate3IntentWireV1Failure CodecFailure { get; }
        public Gate3DecodedNetworkIntentEvidence Evidence { get; }
        public bool HasEvidence { get; }

        internal static Gate3IntentCarrierIngressResult Rejected(Gate3IntentCarrierIngressStatus status)
        {
            return new Gate3IntentCarrierIngressResult(
                status,
                Gate3ServerReceiveTickObservationStatus.Observed,
                Gate3IntentWireV1Failure.NONE,
                default,
                false);
        }

        internal static Gate3IntentCarrierIngressResult ReceiveTickUnavailable(Gate3ServerReceiveTickObservationStatus status)
        {
            return new Gate3IntentCarrierIngressResult(
                Gate3IntentCarrierIngressStatus.ReceiveTickUnavailable,
                status,
                Gate3IntentWireV1Failure.NONE,
                default,
                false);
        }

        internal static Gate3IntentCarrierIngressResult CodecRejected(Gate3IntentWireV1Failure failure)
        {
            return new Gate3IntentCarrierIngressResult(
                Gate3IntentCarrierIngressStatus.CodecFailure,
                Gate3ServerReceiveTickObservationStatus.Observed,
                failure,
                default,
                false);
        }

        internal static Gate3IntentCarrierIngressResult Decoded(Gate3DecodedNetworkIntentEvidence evidence)
        {
            return new Gate3IntentCarrierIngressResult(
                Gate3IntentCarrierIngressStatus.Decoded,
                Gate3ServerReceiveTickObservationStatus.Observed,
                Gate3IntentWireV1Failure.NONE,
                evidence,
                true);
        }
    }

    /// <summary>
    /// Plain local carrier processor. It captures existing authoritative receive-time evidence before the one
    /// D-023 decoder call, and deliberately ends before every later authority boundary.
    /// </summary>
    public sealed class Gate3IntentCarrierIngressProcessor
    {
        private readonly Func<Gate3ServerReceiveTickObservation> _observeAuthoritativeReceiveTick;

        public Gate3IntentCarrierIngressProcessor(Func<Gate3ServerReceiveTickObservation> observeAuthoritativeReceiveTick)
        {
            _observeAuthoritativeReceiveTick = observeAuthoritativeReceiveTick ?? throw new ArgumentNullException(nameof(observeAuthoritativeReceiveTick));
        }

        public Gate3IntentCarrierIngressResult Process(NetworkConnection connection, Gate3IntentCarrierBroadcast carrier, Channel channel)
        {
            if (channel != Channel.Reliable)
            {
                return Gate3IntentCarrierIngressResult.Rejected(Gate3IntentCarrierIngressStatus.UnexpectedChannel);
            }

            var serverSuppliedClientId = connection == null ? -1 : connection.ClientId;
            if (!Gate3ServerConnectionId.TryFromServerObservedTransportId(serverSuppliedClientId, out var connectionId))
            {
                return Gate3IntentCarrierIngressResult.Rejected(Gate3IntentCarrierIngressStatus.InvalidServerConnection);
            }

            var receiveTick = _observeAuthoritativeReceiveTick();
            if (!receiveTick.HasReceiveTick)
            {
                return Gate3IntentCarrierIngressResult.ReceiveTickUnavailable(receiveTick.Status);
            }

            var payload = carrier.Payload;
            if (!Gate3IntentWireV1Codec.TryDecode(payload, out var envelope, out var codecFailure))
            {
                return Gate3IntentCarrierIngressResult.CodecRejected(codecFailure);
            }

            return Gate3IntentCarrierIngressResult.Decoded(
                new Gate3DecodedNetworkIntentEvidence(connectionId, receiveTick.ReceiveTick, envelope));
        }
    }

    /// <summary>
    /// Production server registration for the one authenticated V1 carrier. It only registers/unregisters the
    /// callback; it does not own or request any transport lifecycle transition.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Gate3IntentCarrierIngress : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager _networkManager;

        [SerializeField]
        private Gate2ProductionHostDriver _hostDriver;

        private Gate3IntentCarrierIngressProcessor _processor;
        private bool _registered;

        /// <summary>Most recent server-local callback outcome; never serialized or transmitted.</summary>
        public Gate3IntentCarrierIngressResult LastResult { get; private set; }

        private void Awake()
        {
            if (_networkManager == null || _hostDriver == null || _networkManager.ServerManager == null)
            {
                throw new InvalidOperationException("The Gate-3 intent carrier ingress requires the committed NetworkManager and production host owner.");
            }

            _processor = new Gate3IntentCarrierIngressProcessor(_hostDriver.ObserveAuthoritativeServerReceiveTick);
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnDestroy()
        {
            Unregister();
        }

        private void Register()
        {
            if (_registered)
            {
                return;
            }

            _networkManager.ServerManager.RegisterBroadcast<Gate3IntentCarrierBroadcast>(OnCarrierBroadcast, requireAuthentication: true);
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered || _networkManager == null || _networkManager.ServerManager == null)
            {
                return;
            }

            _networkManager.ServerManager.UnregisterBroadcast<Gate3IntentCarrierBroadcast>(OnCarrierBroadcast);
            _registered = false;
        }

        private void OnCarrierBroadcast(NetworkConnection connection, Gate3IntentCarrierBroadcast carrier, Channel channel)
        {
            LastResult = _processor.Process(connection, carrier, channel);
        }
    }
}
