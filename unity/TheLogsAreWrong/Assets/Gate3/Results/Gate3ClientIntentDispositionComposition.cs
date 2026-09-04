using System;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
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
        private PendingResolutionAttempt _pendingResolutionAttempt;
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
            _pendingResolutionAttempt = null;
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
        /// The only pre-D-025 retention point. Existing D-026 correlation deliberately remains eligible for the sole
        /// D-024 duplicate decision; only capacity/invariant failures stop the decoded evidence before resolution.
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
                case Gate3ClientIntentDispositionReservationStatus.ExistingIntentIdRequiresD024:
                    _pendingResolutionAttempt = new PendingResolutionAttempt(decoded.Envelope, origin, decoded.AuthoritativeReceiveTick, LastReservation);
                    return true;
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
            var attempt = TakePendingResolutionAttempt(decoded);
            if (_ledger == null || attempt == null)
            {
                return;
            }

            if (!TryGetCurrentOrigin(decoded.ConnectionId, out var currentOrigin) || currentOrigin != attempt.Origin)
            {
                return;
            }

            switch (resolution.Status)
            {
                case Gate3AuthoritativeActorResolutionStatus.ActorNotBound:
                    if (attempt.Reservation.CreatedRecord)
                    {
                        TerminalizeAndDeliver(decoded.Envelope.IntentId, "ACTOR_NOT_BOUND");
                    }
                    else
                    {
                        Deliver(attempt.Origin, _ledger.CreateUnretainedAdmissionRejection(
                            decoded.Envelope,
                            decoded.AuthoritativeReceiveTick,
                            "ACTOR_NOT_BOUND"));
                    }
                    return;

                case Gate3AuthoritativeActorResolutionStatus.Resolved:
                    var admission = _admission.AdmitResolvedNetworkIntent(resolution.Evidence);
                    HandleAdmission(attempt, admission);
                    return;

                case Gate3AuthoritativeActorResolutionStatus.InvalidConnection:
                case Gate3AuthoritativeActorResolutionStatus.ConnectionNotLive:
                    return;

                default:
                    throw new InvalidOperationException("Unknown actor-resolution status.");
            }
        }

        private void HandleAdmission(PendingResolutionAttempt attempt, Gate3NetworkIntentAdmissionResult admission)
        {
            switch (admission.Status)
            {
                case Gate3NetworkIntentAdmissionStatus.Admitted:
                    if (!_ledger.TryBeginAdmittedAfterD024(
                            attempt.Envelope,
                            attempt.Origin,
                            attempt.AuthoritativeReceiveTick,
                            attempt.Reservation.CreatedRecord))
                    {
                        throw new InvalidOperationException("D-024 admitted evidence whose D-026 result correlation could not begin pending state.");
                    }

                    DeliverCurrent(attempt.Envelope.IntentId);
                    return;
                case Gate3NetworkIntentAdmissionStatus.ShiftMismatch:
                    TerminalizeOrDeliverUnretained(attempt, "SHIFT_MISMATCH");
                    return;
                case Gate3NetworkIntentAdmissionStatus.ReceiveTickClosed:
                    TerminalizeOrDeliverUnretained(attempt, "RECEIVE_TICK_CLOSED");
                    return;
                case Gate3NetworkIntentAdmissionStatus.ReceiveSequenceExhausted:
                    TerminalizeOrDeliverUnretained(attempt, "RECEIVE_SEQUENCE_EXHAUSTED");
                    return;
                case Gate3NetworkIntentAdmissionStatus.DuplicateIntentId:
                    var replay = _ledger.ResolveDuplicateAfterD024(
                        attempt.Envelope,
                        attempt.Origin,
                        attempt.AuthoritativeReceiveTick,
                        attempt.Reservation.CreatedRecord);
                    if (replay.DeliveryAuthorized)
                    {
                        Deliver(replay.Origin, replay.Disposition);
                    }

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

        private void TerminalizeOrDeliverUnretained(PendingResolutionAttempt attempt, string code)
        {
            if (attempt.Reservation.CreatedRecord)
            {
                TerminalizeAndDeliver(attempt.Envelope.IntentId, code);
                return;
            }

            Deliver(attempt.Origin, _ledger.CreateUnretainedAdmissionRejection(
                attempt.Envelope,
                attempt.AuthoritativeReceiveTick,
                code));
        }

        private PendingResolutionAttempt TakePendingResolutionAttempt(Gate3DecodedNetworkIntentEvidence decoded)
        {
            var attempt = _pendingResolutionAttempt;
            _pendingResolutionAttempt = null;
            if (attempt == null || !ReferenceEquals(attempt.Envelope, decoded.Envelope))
            {
                throw new InvalidOperationException("D-026 actor resolution did not preserve the exact reservation evidence.");
            }

            return attempt;
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

        private sealed class PendingResolutionAttempt
        {
            internal PendingResolutionAttempt(
                IntentEnvelope envelope,
                Gate3NetworkOrigin origin,
                ServerTick authoritativeReceiveTick,
                Gate3ClientIntentDispositionReservation reservation)
            {
                Envelope = envelope;
                Origin = origin;
                AuthoritativeReceiveTick = authoritativeReceiveTick;
                Reservation = reservation;
            }

            internal IntentEnvelope Envelope { get; }
            internal Gate3NetworkOrigin Origin { get; }
            internal ServerTick AuthoritativeReceiveTick { get; }
            internal Gate3ClientIntentDispositionReservation Reservation { get; }
        }
    }
}
