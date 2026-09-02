using System;
using System.Collections.Generic;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Gate3
{
    /// <summary>Server-local D-024 admission outcomes; these values are not a client-visible protocol.</summary>
    public enum Gate3NetworkIntentAdmissionStatus
    {
        Admitted,
        DuplicateIntentId,
        ShiftMismatch,
        ReceiveTickClosed,
        ReceiveSequenceExhausted,
        InvalidResolvedEvidence,
        BufferDisposed
    }

    /// <summary>Reports whether resolved network evidence became one existing accepted-intent receipt.</summary>
    public readonly struct Gate3NetworkIntentAdmissionResult
    {
        private Gate3NetworkIntentAdmissionResult(
            Gate3NetworkIntentAdmissionStatus status,
            AuthoritativeAcceptedIntent acceptedIntent)
        {
            Status = status;
            AcceptedIntent = acceptedIntent;
        }

        public Gate3NetworkIntentAdmissionStatus Status { get; }
        public AuthoritativeAcceptedIntent AcceptedIntent { get; }
        public bool HasAcceptedIntent => AcceptedIntent != null;

        internal static Gate3NetworkIntentAdmissionResult Admitted(AuthoritativeAcceptedIntent acceptedIntent)
        {
            if (acceptedIntent == null) throw new ArgumentNullException(nameof(acceptedIntent));
            return new Gate3NetworkIntentAdmissionResult(Gate3NetworkIntentAdmissionStatus.Admitted, acceptedIntent);
        }

        internal static Gate3NetworkIntentAdmissionResult Rejected(Gate3NetworkIntentAdmissionStatus status)
        {
            if (status == Gate3NetworkIntentAdmissionStatus.Admitted)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new Gate3NetworkIntentAdmissionResult(status, null);
        }
    }

    /// <summary>
    /// Source-neutral server-local evidence that has reached the one D-025 shared admission owner. The trusted
    /// source boundary is outside this value: local evidence is normalized by the production composition, while
    /// network evidence reaches the same owner only through successful actor resolution.
    /// </summary>
    internal readonly struct Gate3ProductionAdmissionEvidence
    {
        internal Gate3ProductionAdmissionEvidence(
            IntentEnvelope envelope,
            ActorId authoritativeActor,
            ServerTick authoritativeReceiveTick)
        {
            Envelope = envelope;
            AuthoritativeActor = authoritativeActor;
            AuthoritativeReceiveTick = authoritativeReceiveTick;
        }

        public IntentEnvelope Envelope { get; }
        public ActorId AuthoritativeActor { get; }
        public ServerTick AuthoritativeReceiveTick { get; }
    }

    /// <summary>Server-local materialization outcomes for one exact receive tick.</summary>
    public enum Gate3NetworkIntentMaterializationStatus
    {
        Materialized,
        ShiftMismatch,
        ReceiveTickAlreadySealed,
        InvalidReceiveTick,
        BufferDisposed
    }

    /// <summary>Reports whether one exact receive-tick bucket was sealed and materialized.</summary>
    public readonly struct Gate3NetworkIntentMaterializationResult
    {
        private Gate3NetworkIntentMaterializationResult(
            Gate3NetworkIntentMaterializationStatus status,
            AcceptedIntentTickBatch batch)
        {
            Status = status;
            Batch = batch;
        }

        public Gate3NetworkIntentMaterializationStatus Status { get; }
        public AcceptedIntentTickBatch Batch { get; }
        public bool HasBatch => Batch != null;

        internal static Gate3NetworkIntentMaterializationResult Materialized(AcceptedIntentTickBatch batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            return new Gate3NetworkIntentMaterializationResult(Gate3NetworkIntentMaterializationStatus.Materialized, batch);
        }

        internal static Gate3NetworkIntentMaterializationResult Rejected(Gate3NetworkIntentMaterializationStatus status)
        {
            if (status == Gate3NetworkIntentMaterializationStatus.Materialized)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new Gate3NetworkIntentMaterializationResult(status, null);
        }
    }

    /// <summary>
    /// One lifecycle-bound plain-C# D-024 owner. It accepts only already-resolved evidence, remembers each
    /// current-shift intent identity for the session, assigns one sequence in admission-call order per exact
    /// receive tick, and stops at the existing accepted-intent batch contract.
    /// </summary>
    public sealed class Gate3NetworkIntentAdmissionBuffer : IDisposable
    {
        private readonly object _sync = new object();
        private readonly ShiftId _shiftId;
        private readonly HashSet<IntentId> _seenIntentIds = new HashSet<IntentId>();
        private readonly Dictionary<ServerTick, PendingReceiveTickBucket> _pendingByReceiveTick = new Dictionary<ServerTick, PendingReceiveTickBucket>();
        private readonly HashSet<ServerTick> _sealedReceiveTicks = new HashSet<ServerTick>();
        private bool _disposed;

        public Gate3NetworkIntentAdmissionBuffer(ShiftId shiftId)
        {
            if (shiftId.IsDefault)
            {
                throw new ArgumentException("Shift identifier must be initialized.", nameof(shiftId));
            }

            _shiftId = shiftId;
        }

        /// <summary>Consumes one successful resolved evidence item in the server's serialized admission-call order.</summary>
        public Gate3NetworkIntentAdmissionResult Admit(Gate3ResolvedNetworkIntentEvidence evidence)
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return Gate3NetworkIntentAdmissionResult.Rejected(Gate3NetworkIntentAdmissionStatus.BufferDisposed);
                }

                if (!IsValidResolvedEvidence(evidence))
                {
                    return Gate3NetworkIntentAdmissionResult.Rejected(Gate3NetworkIntentAdmissionStatus.InvalidResolvedEvidence);
                }

                return AdmitResolved(new Gate3ProductionAdmissionEvidence(
                    evidence.Envelope,
                    evidence.AuthoritativeActor,
                    evidence.AuthoritativeReceiveTick));
            }
        }

        /// <summary>
        /// Consumes trusted local networked-production evidence under the same one sequence, dedupe, bucket, and
        /// seal state as resolved network evidence. It constructs no receipt before entering this shared owner.
        /// </summary>
        internal Gate3NetworkIntentAdmissionResult Admit(Gate3ProductionAdmissionEvidence evidence)
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return Gate3NetworkIntentAdmissionResult.Rejected(Gate3NetworkIntentAdmissionStatus.BufferDisposed);
                }

                if (!IsValidProductionEvidence(evidence))
                {
                    return Gate3NetworkIntentAdmissionResult.Rejected(Gate3NetworkIntentAdmissionStatus.InvalidResolvedEvidence);
                }

                return AdmitResolved(evidence);
            }
        }

        /// <summary>
        /// Atomically seals one exact current-shift receive tick before returning the one existing batch form.
        /// A second call never republishes the same receipts.
        /// </summary>
        public Gate3NetworkIntentMaterializationResult Materialize(ShiftId shiftId, ServerTick receiveTick)
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return Gate3NetworkIntentMaterializationResult.Rejected(Gate3NetworkIntentMaterializationStatus.BufferDisposed);
                }

                if (shiftId != _shiftId)
                {
                    return Gate3NetworkIntentMaterializationResult.Rejected(Gate3NetworkIntentMaterializationStatus.ShiftMismatch);
                }

                if (receiveTick.IsDefault)
                {
                    return Gate3NetworkIntentMaterializationResult.Rejected(Gate3NetworkIntentMaterializationStatus.InvalidReceiveTick);
                }

                if (!_sealedReceiveTicks.Add(receiveTick))
                {
                    return Gate3NetworkIntentMaterializationResult.Rejected(Gate3NetworkIntentMaterializationStatus.ReceiveTickAlreadySealed);
                }

                if (!_pendingByReceiveTick.TryGetValue(receiveTick, out var bucket))
                {
                    return Gate3NetworkIntentMaterializationResult.Materialized(
                        AcceptedIntentTickBatchFactory.Create(_shiftId, receiveTick, Array.Empty<AuthoritativeAcceptedIntent>()));
                }

                _pendingByReceiveTick.Remove(receiveTick);
                return Gate3NetworkIntentMaterializationResult.Materialized(
                    AcceptedIntentTickBatchFactory.Create(_shiftId, receiveTick, bucket.AcceptedIntents));
            }
        }

        /// <summary>Clears pending retained evidence and permanently closes this lifecycle-bound owner.</summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _pendingByReceiveTick.Clear();
                _sealedReceiveTicks.Clear();
                _seenIntentIds.Clear();
            }
        }

        private static bool IsValidResolvedEvidence(Gate3ResolvedNetworkIntentEvidence evidence)
        {
            return evidence.ConnectionId.IsValid
                   && IsValidProductionEvidence(new Gate3ProductionAdmissionEvidence(
                       evidence.Envelope,
                       evidence.AuthoritativeActor,
                       evidence.AuthoritativeReceiveTick));
        }

        private Gate3NetworkIntentAdmissionResult AdmitResolved(Gate3ProductionAdmissionEvidence evidence)
        {
            var envelope = evidence.Envelope;
            if (envelope.ShiftId != _shiftId)
            {
                return Gate3NetworkIntentAdmissionResult.Rejected(Gate3NetworkIntentAdmissionStatus.ShiftMismatch);
            }

            if (!_seenIntentIds.Add(envelope.IntentId))
            {
                return Gate3NetworkIntentAdmissionResult.Rejected(Gate3NetworkIntentAdmissionStatus.DuplicateIntentId);
            }

            var receiveTick = evidence.AuthoritativeReceiveTick;
            if (_sealedReceiveTicks.Contains(receiveTick))
            {
                return Gate3NetworkIntentAdmissionResult.Rejected(Gate3NetworkIntentAdmissionStatus.ReceiveTickClosed);
            }

            if (!_pendingByReceiveTick.TryGetValue(receiveTick, out var bucket))
            {
                bucket = new PendingReceiveTickBucket();
                _pendingByReceiveTick.Add(receiveTick, bucket);
            }

            if (bucket.IsExhausted)
            {
                return Gate3NetworkIntentAdmissionResult.Rejected(Gate3NetworkIntentAdmissionStatus.ReceiveSequenceExhausted);
            }

            var receiveSequence = bucket.NextSequence;
            var acceptedIntent = new AuthoritativeAcceptedIntent(
                envelope,
                evidence.AuthoritativeActor,
                receiveTick,
                receiveSequence);
            bucket.AcceptedIntents.Add(acceptedIntent);

            if (!receiveSequence.TryNext(out var successor))
            {
                bucket.IsExhausted = true;
            }
            else
            {
                bucket.NextSequence = successor;
            }

            return Gate3NetworkIntentAdmissionResult.Admitted(acceptedIntent);
        }

        private static bool IsValidProductionEvidence(Gate3ProductionAdmissionEvidence evidence)
        {
            var envelope = evidence.Envelope;
            return !evidence.AuthoritativeReceiveTick.IsDefault
                   && !evidence.AuthoritativeActor.IsDefault
                   && envelope != null
                   && !envelope.ShiftId.IsDefault
                   && !envelope.IntentId.IsDefault
                   && !envelope.ActorIdHint.IsDefault
                   && !envelope.TargetId.IsDefault
                   && !envelope.Action.IsDefault
                   && !envelope.ExpectedStateVersion.IsDefault
                   && !envelope.ClientObservedTick.IsDefault
                   && envelope.Parameters != null;
        }

        private sealed class PendingReceiveTickBucket
        {
            public readonly List<AuthoritativeAcceptedIntent> AcceptedIntents = new List<AuthoritativeAcceptedIntent>();
            public ServerReceiveSequence NextSequence = ServerReceiveSequence.Zero;
            public bool IsExhausted;
        }
    }
}
