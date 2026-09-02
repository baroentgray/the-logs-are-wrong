using System;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using UnityEngine;

namespace TheLogsAreWrong.Gate3
{
    /// <summary>Successful server-local identity evidence only; it is not an admitted or accepted intent.</summary>
    public readonly struct Gate3ResolvedNetworkIntentEvidence
    {
        internal Gate3ResolvedNetworkIntentEvidence(
            Gate3ServerConnectionId connectionId,
            ServerTick authoritativeReceiveTick,
            IntentEnvelope envelope,
            ActorId authoritativeActor)
        {
            ConnectionId = connectionId;
            AuthoritativeReceiveTick = authoritativeReceiveTick;
            Envelope = envelope;
            AuthoritativeActor = authoritativeActor;
        }

        public Gate3ServerConnectionId ConnectionId { get; }
        public ServerTick AuthoritativeReceiveTick { get; }
        public IntentEnvelope Envelope { get; }
        public ActorId AuthoritativeActor { get; }
    }

    /// <summary>Bounded server-local outcome preserving the existing TLAW-075 resolution statuses.</summary>
    public readonly struct Gate3ActorResolutionResult
    {
        private Gate3ActorResolutionResult(
            Gate3AuthoritativeActorResolutionStatus status,
            Gate3ResolvedNetworkIntentEvidence evidence,
            bool hasEvidence)
        {
            Status = status;
            Evidence = evidence;
            HasEvidence = hasEvidence;
        }

        public Gate3AuthoritativeActorResolutionStatus Status { get; }
        public Gate3ResolvedNetworkIntentEvidence Evidence { get; }
        public bool HasEvidence { get; }

        internal static Gate3ActorResolutionResult Rejected(Gate3AuthoritativeActorResolutionStatus status)
        {
            return new Gate3ActorResolutionResult(status, default, false);
        }

        internal static Gate3ActorResolutionResult Resolved(Gate3ResolvedNetworkIntentEvidence evidence)
        {
            return new Gate3ActorResolutionResult(Gate3AuthoritativeActorResolutionStatus.Resolved, evidence, true);
        }
    }

    /// <summary>
    /// Composes existing decoded ingress evidence with the existing server-owned binding resolver and stops
    /// before sequencing, admission, gameplay validation, execution, or any network response boundary.
    /// </summary>
    public sealed class Gate3ActorResolutionProcessor
    {
        private readonly Func<Gate3ServerConnectionId, ActorId?, Gate3AuthoritativeActorResolution> _resolveAuthoritativeActor;

        public Gate3ActorResolutionProcessor(
            Func<Gate3ServerConnectionId, ActorId?, Gate3AuthoritativeActorResolution> resolveAuthoritativeActor)
        {
            _resolveAuthoritativeActor = resolveAuthoritativeActor ?? throw new ArgumentNullException(nameof(resolveAuthoritativeActor));
        }

        public Gate3ActorResolutionResult Process(Gate3DecodedNetworkIntentEvidence decoded)
        {
            var resolution = _resolveAuthoritativeActor(decoded.ConnectionId, decoded.Envelope.ActorIdHint);
            if (!resolution.HasActor)
            {
                return Gate3ActorResolutionResult.Rejected(resolution.Status);
            }

            return Gate3ActorResolutionResult.Resolved(
                new Gate3ResolvedNetworkIntentEvidence(
                    decoded.ConnectionId,
                    decoded.AuthoritativeReceiveTick,
                    decoded.Envelope,
                    resolution.Actor));
        }
    }

    /// <summary>
    /// Thin production subscriber between the existing TLAW-079 decoded-output seam and the existing
    /// TLAW-075 resolver. It owns neither carrier registration nor transport/binding lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Gate3ActorResolutionComposition : MonoBehaviour
    {
        [SerializeField]
        private Gate3IntentCarrierIngress _carrierIngress;

        [SerializeField]
        private Gate3ServerConnectionActorBindingBridge _connectionBinding;

        private Gate3ActorResolutionProcessor _processor;
        private bool _subscribed;

        /// <summary>Most recent server-local resolution outcome; it is never serialized or transmitted.</summary>
        public Gate3ActorResolutionResult LastResult { get; private set; }

        /// <summary>
        /// Publishes the exact successful resolved evidence once. Subscribers own later admission; this component
        /// remains only the decoded-evidence to authoritative-actor-resolution boundary.
        /// </summary>
        public event Action<Gate3ResolvedNetworkIntentEvidence> Resolved;

        private void Awake()
        {
            if (_carrierIngress == null || _connectionBinding == null)
            {
                throw new InvalidOperationException("The Gate-3 actor-resolution composition requires the committed carrier ingress and connection binding bridge.");
            }

            _processor = new Gate3ActorResolutionProcessor(_connectionBinding.ResolveAuthoritativeActor);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _carrierIngress.Decoded += OnDecoded;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _carrierIngress == null)
            {
                return;
            }

            _carrierIngress.Decoded -= OnDecoded;
            _subscribed = false;
        }

        private void OnDecoded(Gate3DecodedNetworkIntentEvidence decoded)
        {
            LastResult = _processor.Process(decoded);
            if (LastResult.HasEvidence)
            {
                Resolved?.Invoke(LastResult.Evidence);
            }
        }
    }
}
