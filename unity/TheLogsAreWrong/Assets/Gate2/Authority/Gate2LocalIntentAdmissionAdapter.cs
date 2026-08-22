using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Gate2
{
    /// <summary>Local ingress outcomes only; Stage Two remains the sole gameplay validation authority.</summary>
    public enum LocalIntentAdmissionRejection
    {
        None,
        OwnerNotRunning,
        AdapterDisposed,
        NullEnvelope,
        ShiftMismatch,
        AuthoritativeActorUnbound,
        DuplicateIntentId,
        ReceiveSequenceExhausted
    }

    /// <summary>Reports whether local ingress retained exact evidence for the current open host tick.</summary>
    public sealed class LocalIntentAdmissionResult
    {
        private LocalIntentAdmissionResult(AuthoritativeAcceptedIntent acceptedIntent, LocalIntentAdmissionRejection rejection)
        {
            AcceptedIntent = acceptedIntent;
            Rejection = rejection;
        }

        public bool Accepted => AcceptedIntent != null;

        public AuthoritativeAcceptedIntent AcceptedIntent { get; }

        public LocalIntentAdmissionRejection Rejection { get; }

        internal static LocalIntentAdmissionResult Accept(AuthoritativeAcceptedIntent acceptedIntent)
        {
            if (acceptedIntent == null) throw new ArgumentNullException(nameof(acceptedIntent));
            return new LocalIntentAdmissionResult(acceptedIntent, LocalIntentAdmissionRejection.None);
        }

        internal static LocalIntentAdmissionResult Reject(LocalIntentAdmissionRejection rejection)
        {
            if (rejection == LocalIntentAdmissionRejection.None)
            {
                throw new ArgumentOutOfRangeException(nameof(rejection));
            }

            return new LocalIntentAdmissionResult(null, rejection);
        }
    }

    /// <summary>
    /// Plain-C# local authoritative ingress for exactly one live production owner. It retains exact client envelope
    /// references and separately trusted actors for its one open tick, then delegates all batch validation and
    /// ordering to the imported PortableAuthority factory.
    /// </summary>
    public sealed class Gate2LocalIntentAdmissionAdapter : IAlreadyAdmittedHostInputSource, IDisposable
    {
        private readonly ShiftId _shiftId;
        private readonly List<AuthoritativeAcceptedIntent> _accepted = new List<AuthoritativeAcceptedIntent>();
        private readonly HashSet<IntentId> _intentIds = new HashSet<IntentId>();
        private ServerTick _openAdmissionTick = ServerTick.Zero;
        private ServerReceiveSequence _nextReceiveSequence = ServerReceiveSequence.Zero;
        private bool _receiveSequenceExhausted;
        private bool _disposed;

        public Gate2LocalIntentAdmissionAdapter(ShiftId shiftId)
        {
            if (shiftId.IsDefault) throw new ArgumentException("Shift identifier must be initialized.", nameof(shiftId));
            _shiftId = shiftId;
        }

        public ServerTick OpenAdmissionTick => _openAdmissionTick;

        /// <summary>
        /// Retains an exact envelope and a separately trusted local actor for the current open tick. This boundary
        /// performs no gameplay, state-version, target, action, parameter, or active-tool validation.
        /// </summary>
        public LocalIntentAdmissionResult SubmitLocalIntent(IntentEnvelope envelope, ActorId authoritativeActor)
        {
            if (_disposed) return LocalIntentAdmissionResult.Reject(LocalIntentAdmissionRejection.AdapterDisposed);
            if (envelope == null) return LocalIntentAdmissionResult.Reject(LocalIntentAdmissionRejection.NullEnvelope);
            if (envelope.ShiftId != _shiftId) return LocalIntentAdmissionResult.Reject(LocalIntentAdmissionRejection.ShiftMismatch);
            if (authoritativeActor.IsDefault) return LocalIntentAdmissionResult.Reject(LocalIntentAdmissionRejection.AuthoritativeActorUnbound);
            if (_intentIds.Contains(envelope.IntentId)) return LocalIntentAdmissionResult.Reject(LocalIntentAdmissionRejection.DuplicateIntentId);
            if (_receiveSequenceExhausted) return LocalIntentAdmissionResult.Reject(LocalIntentAdmissionRejection.ReceiveSequenceExhausted);

            var accepted = new AuthoritativeAcceptedIntent(envelope, authoritativeActor, _openAdmissionTick, _nextReceiveSequence);
            _accepted.Add(accepted);
            _intentIds.Add(envelope.IntentId);
            if (!_nextReceiveSequence.TryNext(out var followingSequence))
            {
                _receiveSequenceExhausted = true;
            }
            else
            {
                _nextReceiveSequence = followingSequence;
            }

            return LocalIntentAdmissionResult.Accept(accepted);
        }

        /// <summary>
        /// Materializes exactly the current open tick through the one PortableAuthority accepted-batch factory.
        /// Success alone opens the next tick and resets its zero-based receive sequence.
        /// </summary>
        public AlreadyAdmittedHostTickInput GetInput(ShiftId shiftId, ServerTick tick)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Gate2LocalIntentAdmissionAdapter));
            if (shiftId != _shiftId) throw new ArgumentException("Input shift must equal the adapter shift.", nameof(shiftId));
            if (tick != _openAdmissionTick) throw new ArgumentException("Input tick must equal the adapter open tick.", nameof(tick));

            var nextTick = ServerTick.From(checked(_openAdmissionTick.Value + 1));
            var batch = AcceptedIntentTickBatchFactory.Create(_shiftId, _openAdmissionTick, _accepted);
            _accepted.Clear();
            _intentIds.Clear();
            _nextReceiveSequence = ServerReceiveSequence.Zero;
            _receiveSequenceExhausted = false;
            _openAdmissionTick = nextTick;
            return new AlreadyAdmittedHostTickInput(batch, ImmutableHashSet<ItemId>.Empty);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _accepted.Clear();
            _intentIds.Clear();
            _disposed = true;
        }
    }
}
