using System;
using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Gate2;
using UnityEngine;

namespace TheLogsAreWrong.Gate3
{
    /// <summary>Server-local result for trusted listen-host ingress; it is never a client response protocol.</summary>
    public enum Gate3NetworkedLocalIntentSubmissionStatus
    {
        Admitted,
        OwnerNotRunning,
        ReceiveTickUnavailable,
        AdmissionRejected
    }

    /// <summary>Preserves a shared-owner admission result without manufacturing any network-visible outcome.</summary>
    public readonly struct Gate3NetworkedLocalIntentSubmissionResult
    {
        private Gate3NetworkedLocalIntentSubmissionResult(
            Gate3NetworkedLocalIntentSubmissionStatus status,
            Gate3NetworkIntentAdmissionResult admission)
        {
            Status = status;
            Admission = admission;
        }

        public Gate3NetworkedLocalIntentSubmissionStatus Status { get; }
        public Gate3NetworkIntentAdmissionResult Admission { get; }
        public bool HasAcceptedIntent => Admission.HasAcceptedIntent;

        internal static Gate3NetworkedLocalIntentSubmissionResult Admitted(Gate3NetworkIntentAdmissionResult admission) =>
            new Gate3NetworkedLocalIntentSubmissionResult(Gate3NetworkedLocalIntentSubmissionStatus.Admitted, admission);

        internal static Gate3NetworkedLocalIntentSubmissionResult OwnerNotRunning() =>
            new Gate3NetworkedLocalIntentSubmissionResult(Gate3NetworkedLocalIntentSubmissionStatus.OwnerNotRunning, default);

        internal static Gate3NetworkedLocalIntentSubmissionResult ReceiveTickUnavailable() =>
            new Gate3NetworkedLocalIntentSubmissionResult(Gate3NetworkedLocalIntentSubmissionStatus.ReceiveTickUnavailable, default);

        internal static Gate3NetworkedLocalIntentSubmissionResult AdmissionRejected(Gate3NetworkIntentAdmissionResult admission) =>
            new Gate3NetworkedLocalIntentSubmissionResult(Gate3NetworkedLocalIntentSubmissionStatus.AdmissionRejected, admission);
    }

    /// <summary>
    /// The one bounded D-025 production composition. It normalizes trusted local evidence and receives exact
    /// successful actor-resolution evidence, then delegates both to one lifecycle-bound D-024 owner before any
    /// accepted-intent construction. It owns no independent sequence, dedupe, batch merge, or gameplay rule.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Gate3ProductionAdmissionComposition : MonoBehaviour
    {
        [SerializeField]
        private Gate2ProductionHostDriver _hostDriver;

        [SerializeField]
        private Gate3ActorResolutionComposition _actorResolution;

        private Gate3NetworkIntentAdmissionBuffer _sharedOwner;
        private Gate3ProductionAdmissionInputSource _inputSource;
        private Func<Gate3ServerReceiveTickObservation> _observeReceiveTick;
        private bool _subscribed;

        /// <summary>Most recent shared-owner disposition of resolved network evidence; server-local only.</summary>
        public Gate3NetworkIntentAdmissionResult LastNetworkAdmission { get; private set; }

        private void Awake()
        {
            if (_hostDriver == null || _actorResolution == null)
            {
                throw new InvalidOperationException("The Gate-3 production admission composition requires the committed host driver and actor-resolution composition.");
            }

            _hostDriver.ConfigureNetworkedProductionAdmission(this);
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

        /// <summary>
        /// Called only by the one host owner after C1 materialization establishes the current session shift and
        /// the TLAW-076 session-time observation source. Replacing a session always disposes its old owner first.
        /// </summary>
        internal IAlreadyAdmittedHostInputSource BeginSession(
            ShiftId shiftId,
            Func<Gate3ServerReceiveTickObservation> observeReceiveTick)
        {
            if (_sharedOwner != null || _inputSource != null)
            {
                throw new InvalidOperationException("The production admission composition already owns a live session.");
            }

            if (shiftId.IsDefault)
            {
                throw new ArgumentException("Shift identifier must be initialized.", nameof(shiftId));
            }

            _observeReceiveTick = observeReceiveTick ?? throw new ArgumentNullException(nameof(observeReceiveTick));
            _sharedOwner = new Gate3NetworkIntentAdmissionBuffer(shiftId);
            _inputSource = new Gate3ProductionAdmissionInputSource(_sharedOwner, _observeReceiveTick);
            return _inputSource;
        }

        /// <summary>
        /// Normalizes trusted listen-host evidence using the same authoritative elapsed-time observation as network
        /// ingress. The supplied actor remains exact; local submission never crosses a transport boundary.
        /// </summary>
        public Gate3NetworkedLocalIntentSubmissionResult SubmitTrustedLocalIntent(IntentEnvelope envelope, ActorId authoritativeActor)
        {
            if (_sharedOwner == null || _inputSource == null || _observeReceiveTick == null)
            {
                return Gate3NetworkedLocalIntentSubmissionResult.OwnerNotRunning();
            }

            var observation = _observeReceiveTick();
            if (!observation.HasReceiveTick)
            {
                return Gate3NetworkedLocalIntentSubmissionResult.ReceiveTickUnavailable();
            }

            var admission = _sharedOwner.Admit(new Gate3ProductionAdmissionEvidence(
                envelope,
                authoritativeActor,
                observation.ReceiveTick));
            return admission.HasAcceptedIntent
                ? Gate3NetworkedLocalIntentSubmissionResult.Admitted(admission)
                : Gate3NetworkedLocalIntentSubmissionResult.AdmissionRejected(admission);
        }

        /// <summary>Disposes all lifecycle-bound shared state so retained old ingress cannot enter a fresh session.</summary>
        internal void EndSession()
        {
            if (_inputSource != null)
            {
                _inputSource.Dispose();
                _inputSource = null;
            }

            if (_sharedOwner != null)
            {
                _sharedOwner.Dispose();
                _sharedOwner = null;
            }

            _observeReceiveTick = null;
            LastNetworkAdmission = default;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _actorResolution.Resolved += OnResolvedNetworkIntent;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _actorResolution == null)
            {
                return;
            }

            _actorResolution.Resolved -= OnResolvedNetworkIntent;
            _subscribed = false;
        }

        private void OnResolvedNetworkIntent(Gate3ResolvedNetworkIntentEvidence evidence)
        {
            if (_sharedOwner == null)
            {
                LastNetworkAdmission = Gate3NetworkIntentAdmissionResult.Rejected(Gate3NetworkIntentAdmissionStatus.BufferDisposed);
                return;
            }

            LastNetworkAdmission = _sharedOwner.Admit(evidence);
        }
    }

    /// <summary>
    /// The one final host input source for a networked production session. It owns no receipt state: materializing
    /// delegates to the shared owner, and its temporal gate only observes whether the exact receive window closed.
    /// </summary>
    internal sealed class Gate3ProductionAdmissionInputSource : IAlreadyAdmittedHostInputSource, IIngressBeforeSealHostInputSource, IDisposable
    {
        private readonly Gate3NetworkIntentAdmissionBuffer _sharedOwner;
        private readonly Func<Gate3ServerReceiveTickObservation> _observeReceiveTick;
        private bool _disposed;

        internal Gate3ProductionAdmissionInputSource(
            Gate3NetworkIntentAdmissionBuffer sharedOwner,
            Func<Gate3ServerReceiveTickObservation> observeReceiveTick)
        {
            _sharedOwner = sharedOwner ?? throw new ArgumentNullException(nameof(sharedOwner));
            _observeReceiveTick = observeReceiveTick ?? throw new ArgumentNullException(nameof(observeReceiveTick));
        }

        public bool CanSeal(ShiftId shiftId, ServerTick tick)
        {
            EnsureActive();
            var observation = _observeReceiveTick();
            if (!observation.HasReceiveTick)
            {
                throw new InvalidOperationException("The authoritative receive-time observation is unavailable while a due host tick remains open.");
            }

            return observation.ReceiveTick > tick;
        }

        public AlreadyAdmittedHostTickInput GetInput(ShiftId shiftId, ServerTick tick)
        {
            EnsureActive();
            var materialized = _sharedOwner.Materialize(shiftId, tick);
            if (!materialized.HasBatch)
            {
                throw new InvalidOperationException("The shared production admission owner refused to materialize the requested host tick: " + materialized.Status + ".");
            }

            return new AlreadyAdmittedHostTickInput(materialized.Batch, ImmutableHashSet<ItemId>.Empty);
        }

        public void Dispose()
        {
            _disposed = true;
        }

        private void EnsureActive()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Gate3ProductionAdmissionInputSource));
            }
        }
    }
}
