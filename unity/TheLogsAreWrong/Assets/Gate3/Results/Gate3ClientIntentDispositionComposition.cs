using System;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Gate2;
using UnityEngine;

namespace TheLogsAreWrong.Gate3
{
    /// <summary>
    /// The one D-026 production composition. It reserves/replays client-result state before D-025, delegates every
    /// gameplay admission/order decision to the existing D-025 owner, and projects only the returned Stage Two trace
    /// after the real HostSession succeeds. It owns neither gameplay dedupe, sequence, tick execution, nor events.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class Gate3ClientIntentDispositionComposition : MonoBehaviour
    {
        [SerializeField]
        private Gate2ProductionHostDriver _hostDriver;

        [SerializeField]
        private Gate3ActorResolutionComposition _actorResolution;

        [SerializeField]
        private Gate3ProductionAdmissionComposition _admission;

        [SerializeField]
        private Gate3ServerConnectionActorBindingBridge _connectionBinding;

        [SerializeField]
        private Gate3ClientIntentResultCarrier _resultCarrier;

        private Gate3ClientIntentDispositionLedger _ledger;
        private bool _subscribed;
        private bool _tickSubscribed;

        /// <summary>Most recent D-026 reservation/admission disposition; server-local observability only.</summary>
        public Gate3ClientIntentDispositionReservation LastReservation { get; private set; }

        private void Awake()
        {
            if (_hostDriver == null || _actorResolution == null || _admission == null || _connectionBinding == null || _resultCarrier == null)
            {
                throw new InvalidOperationException("The Gate-3 D-026 disposition composition requires the committed host, actor-resolution, admission, connection, and result-carrier components.");
            }

            _admission.AttachResultDispositionComposition(this);
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
            EndSession();
        }

        /// <summary>Called only by D-025 during the same HostSession/shift startup lifecycle.</summary>
        internal void BeginSession(ShiftId shiftId)
        {
            if (_ledger != null)
            {
                throw new InvalidOperationException("The D-026 result ledger already owns a live production session.");
            }

            _ledger = new Gate3ClientIntentDispositionLedger(shiftId);
            if (!_tickSubscribed)
            {
                _hostDriver.AuthoritativeTickSucceeded += OnAuthoritativeTickSucceeded;
                _tickSubscribed = true;
            }
        }

        /// <summary>Clears retained correlation, replay authorization, and capacity accounting with the D-025 session.</summary>
        internal void EndSession()
        {
            if (_tickSubscribed && _hostDriver != null)
            {
                _hostDriver.AuthoritativeTickSucceeded -= OnAuthoritativeTickSucceeded;
                _tickSubscribed = false;
            }

            if (_ledger != null)
            {
                _ledger.Dispose();
                _ledger = null;
            }

            LastReservation = default;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _actorResolution.BeforeResolution += ReserveBeforeResolution;
            _actorResolution.ResolutionProcessed += OnResolutionProcessed;
            _connectionBinding.ConnectionLifetimeRevoked += OnConnectionLifetimeRevoked;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (_actorResolution != null)
            {
                _actorResolution.BeforeResolution -= ReserveBeforeResolution;
                _actorResolution.ResolutionProcessed -= OnResolutionProcessed;
            }

            if (_connectionBinding != null)
            {
                _connectionBinding.ConnectionLifetimeRevoked -= OnConnectionLifetimeRevoked;
            }

            _subscribed = false;
        }

        /// <summary>
        /// The only pre-D-025 reservation point. A retained same-origin replay or a fail-closed capacity/privacy
        /// rejection returns false so the original bytes never re-enter actor resolution, D-024, Stage 2, or a tick.
        /// </summary>
        private bool ReserveBeforeResolution(Gate3DecodedNetworkIntentEvidence decoded)
        {
            if (_ledger == null
                || !_connectionBinding.TryGetLiveConnectionLifetime(decoded.ConnectionId, out var lifetime))
            {
                return false;
            }

            var origin = Gate3NetworkOrigin.From(decoded.ConnectionId, lifetime);
            LastReservation = _ledger.Reserve(decoded.Envelope, origin, decoded.AuthoritativeReceiveTick);
            switch (LastReservation.Status)
            {
                case Gate3ClientIntentDispositionReservationStatus.ReservedPending:
                    return true;
                case Gate3ClientIntentDispositionReservationStatus.ReplaySameOrigin:
                case Gate3ClientIntentDispositionReservationStatus.IntentIdAlreadyUsed:
                case Gate3ClientIntentDispositionReservationStatus.ResultCapacityExhausted:
                    Deliver(origin, LastReservation.Disposition);
                    return false;
                case Gate3ClientIntentDispositionReservationStatus.InvalidEvidence:
                case Gate3ClientIntentDispositionReservationStatus.LedgerDisposed:
                    return false;
                default:
                    throw new InvalidOperationException("Unknown D-026 reservation status.");
            }
        }

        private void OnResolutionProcessed(Gate3DecodedNetworkIntentEvidence decoded, Gate3ActorResolutionResult resolution)
        {
            if (_ledger == null || !_ledger.TryGetDisposition(decoded.Envelope.IntentId, out var current)
                || current.Kind != Gate3ClientIntentDispositionKind.PENDING)
            {
                return;
            }

            if (!TryGetCurrentOrigin(decoded.ConnectionId, out var origin))
            {
                return;
            }

            switch (resolution.Status)
            {
                case Gate3AuthoritativeActorResolutionStatus.ActorNotBound:
                    if (!_ledger.TryTerminalizeAdmission(decoded.Envelope.IntentId, "ACTOR_NOT_BOUND"))
                    {
                        throw new InvalidOperationException("The reserved D-026 actor-not-bound record could not become terminal.");
                    }

                    DeliverCurrent(decoded.Envelope.IntentId);
                    return;

                case Gate3AuthoritativeActorResolutionStatus.Resolved:
                    var admission = _admission.AdmitResolvedNetworkIntent(resolution.Evidence);
                    HandleAdmission(decoded.Envelope.IntentId, admission);
                    return;

                case Gate3AuthoritativeActorResolutionStatus.InvalidConnection:
                case Gate3AuthoritativeActorResolutionStatus.ConnectionNotLive:
                    return;

                default:
                    throw new InvalidOperationException("Unknown actor-resolution status.");
            }
        }

        private void HandleAdmission(IntentId intentId, Gate3NetworkIntentAdmissionResult admission)
        {
            switch (admission.Status)
            {
                case Gate3NetworkIntentAdmissionStatus.Admitted:
                    DeliverCurrent(intentId);
                    return;
                case Gate3NetworkIntentAdmissionStatus.ShiftMismatch:
                    TerminalizeAndDeliver(intentId, "SHIFT_MISMATCH");
                    return;
                case Gate3NetworkIntentAdmissionStatus.ReceiveTickClosed:
                    TerminalizeAndDeliver(intentId, "RECEIVE_TICK_CLOSED");
                    return;
                case Gate3NetworkIntentAdmissionStatus.ReceiveSequenceExhausted:
                    TerminalizeAndDeliver(intentId, "RECEIVE_SEQUENCE_EXHAUSTED");
                    return;
                case Gate3NetworkIntentAdmissionStatus.DuplicateIntentId:
                    TerminalizeAndDeliver(intentId, "INTENT_ID_ALREADY_USED");
                    return;
                case Gate3NetworkIntentAdmissionStatus.InvalidResolvedEvidence:
                case Gate3NetworkIntentAdmissionStatus.BufferDisposed:
                    throw new InvalidOperationException("D-025 returned a server invariant admission status after D-026 reservation.");
                default:
                    throw new InvalidOperationException("Unknown D-025 admission status.");
            }
        }

        private void OnAuthoritativeTickSucceeded(ServerTick tick, HostStageSevenEventExecution execution)
        {
            if (_ledger == null || execution == null || execution.CurrentTick != tick)
            {
                throw new InvalidOperationException("D-026 requires the exact successful HostSession tick and its returned Stage Two trace.");
            }

            foreach (var delivery in _ledger.ProjectSuccessfulTick(execution.StageTwo))
            {
                if (delivery.DeliveryAuthorized)
                {
                    Deliver(delivery.Origin, delivery.Disposition);
                }
            }
        }

        private void OnConnectionLifetimeRevoked(Gate3ServerConnectionId connectionId, Gate3ServerConnectionLifetime lifetime)
        {
            if (_ledger != null)
            {
                _ledger.RevokeDelivery(Gate3NetworkOrigin.From(connectionId, lifetime));
            }
        }

        private void TerminalizeAndDeliver(IntentId intentId, string code)
        {
            if (!_ledger.TryTerminalizeAdmission(intentId, code))
            {
                throw new InvalidOperationException("The reserved D-026 record could not become its required admission terminal result.");
            }

            DeliverCurrent(intentId);
        }

        private void DeliverCurrent(IntentId intentId)
        {
            if (_ledger.TryGetDelivery(intentId, out var delivery) && delivery.DeliveryAuthorized)
            {
                Deliver(delivery.Origin, delivery.Disposition);
            }
        }

        private void Deliver(Gate3NetworkOrigin origin, Gate3ClientIntentDisposition disposition)
        {
            _resultCarrier.TryDeliver(origin, disposition);
        }

        private bool TryGetCurrentOrigin(Gate3ServerConnectionId connectionId, out Gate3NetworkOrigin origin)
        {
            if (_connectionBinding.TryGetLiveConnectionLifetime(connectionId, out var lifetime))
            {
                origin = Gate3NetworkOrigin.From(connectionId, lifetime);
                return true;
            }

            origin = default;
            return false;
        }
    }
}
