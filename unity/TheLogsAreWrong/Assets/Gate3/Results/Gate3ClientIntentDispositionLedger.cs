using System;
using System.Collections.Generic;
using System.Text;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Gate3
{
    /// <summary>Frozen D-026 V1 tags. Zero is deliberately reserved and invalid on the wire.</summary>
    public enum Gate3ClientIntentDispositionKind : byte
    {
        RESERVED_INVALID = 0,
        PENDING = 1,
        APPLIED = 2,
        REJECTED = 3
    }

    /// <summary>Local fail-closed outcomes for the D-026 client-result V1 codec; they are never serialized.</summary>
    public enum Gate3ClientIntentResultV1Failure
    {
        NONE = 0,
        MESSAGE_TOO_LARGE,
        TRUNCATED_OR_MALFORMED_FRAME,
        INVALID_UTF8,
        UNSUPPORTED_SCHEMA_VERSION,
        INVALID_IDENTIFIER,
        INVALID_NUMERIC_FIELD,
        INVALID_DISPOSITION,
        INVALID_STATE_VERSION_FLAG,
        DISPOSITION_PAYLOAD_MISMATCH,
        INVALID_REJECTION_CODE,
        TRAILING_DATA
    }

    /// <summary>One deterministic D-026 client-visible semantic result. It carries no connection or gameplay-state payload.</summary>
    public sealed class Gate3ClientIntentDisposition
    {
        public Gate3ClientIntentDisposition(
            ShiftId shiftId,
            IntentId intentId,
            Gate3ClientIntentDispositionKind kind,
            ServerTick authoritativeReceiveTick,
            StateVersion? stateVersion,
            string rejectionCode)
        {
            ShiftId = shiftId;
            IntentId = intentId;
            Kind = kind;
            AuthoritativeReceiveTick = authoritativeReceiveTick;
            StateVersion = stateVersion;
            // V1 always carries the rejection-code field.  Canonicalize its empty form so an
            // encode/decode round trip has one plain-C# representation for PENDING/APPLIED.
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public ShiftId ShiftId { get; }
        public IntentId IntentId { get; }
        public Gate3ClientIntentDispositionKind Kind { get; }
        public ServerTick AuthoritativeReceiveTick { get; }
        public StateVersion? StateVersion { get; }
        public string RejectionCode { get; }
    }

    /// <summary>
    /// D-026's one plain-C# V1 result codec. It has its own frozen ABI and only shares the established strict
    /// Gate-3 identifier rules with D-023; it does not depend on FishNet runtime types or D-023 bytes.
    /// </summary>
    public static class Gate3ClientIntentResultV1Codec
    {
        public const ushort SchemaVersion = 1;
        public const int MaxPayloadBytes = 1024;
        public const int MaxIdentifierUtf8Bytes = 256;
        public const int MaxRejectionCodeUtf8Bytes = 64;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static bool TryEncode(Gate3ClientIntentDisposition result, out byte[] payload, out Gate3ClientIntentResultV1Failure failure)
        {
            payload = null;
            failure = Gate3ClientIntentResultV1Failure.NONE;
            if (result == null)
            {
                failure = Gate3ClientIntentResultV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                return false;
            }

            var output = new List<byte>();
            WriteUInt16LittleEndian(output, SchemaVersion);
            if (!TryWriteIdentifier(result.ShiftId.Value, IsShiftId, output, out failure)
                || !TryWriteIdentifier(result.IntentId.Value, IsIntentId, output, out failure))
            {
                return false;
            }

            if (!IsKnownDisposition(result.Kind))
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_DISPOSITION;
                return false;
            }

            if (result.AuthoritativeReceiveTick.IsDefault || result.AuthoritativeReceiveTick.Value < 0)
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_NUMERIC_FIELD;
                return false;
            }

            if (!ValidateDispositionPayload(result.Kind, result.StateVersion, result.RejectionCode, out var rejectionBytes, out failure))
            {
                return false;
            }

            output.Add((byte)result.Kind);
            WriteInt64LittleEndian(output, result.AuthoritativeReceiveTick.Value);
            output.Add(result.StateVersion.HasValue ? (byte)1 : (byte)0);
            if (result.StateVersion.HasValue)
            {
                if (result.StateVersion.Value.IsDefault || result.StateVersion.Value.Value < 0)
                {
                    failure = Gate3ClientIntentResultV1Failure.INVALID_NUMERIC_FIELD;
                    return false;
                }

                WriteInt64LittleEndian(output, result.StateVersion.Value.Value);
            }

            WriteUInt16LittleEndian(output, (ushort)rejectionBytes.Length);
            output.AddRange(rejectionBytes);
            if (output.Count > MaxPayloadBytes)
            {
                failure = Gate3ClientIntentResultV1Failure.MESSAGE_TOO_LARGE;
                return false;
            }

            payload = output.ToArray();
            return true;
        }

        public static bool TryDecode(byte[] payload, out Gate3ClientIntentDisposition result, out Gate3ClientIntentResultV1Failure failure)
        {
            result = null;
            failure = Gate3ClientIntentResultV1Failure.NONE;
            if (payload == null || payload.Length > MaxPayloadBytes)
            {
                failure = payload != null && payload.Length > MaxPayloadBytes
                    ? Gate3ClientIntentResultV1Failure.MESSAGE_TOO_LARGE
                    : Gate3ClientIntentResultV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                return false;
            }

            var reader = new Reader(payload);
            if (!reader.TryReadUInt16LittleEndian(out var schemaVersion))
            {
                failure = Gate3ClientIntentResultV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                return false;
            }

            if (schemaVersion != SchemaVersion)
            {
                failure = Gate3ClientIntentResultV1Failure.UNSUPPORTED_SCHEMA_VERSION;
                return false;
            }

            if (!TryReadIdentifier(reader, IsShiftId, out var shift, out failure)
                || !TryReadIdentifier(reader, IsIntentId, out var intent, out failure)
                || !reader.TryReadByte(out var rawKind)
                || !reader.TryReadInt64LittleEndian(out var receiveTick)
                || !reader.TryReadByte(out var hasStateVersion))
            {
                if (failure == Gate3ClientIntentResultV1Failure.NONE)
                {
                    failure = Gate3ClientIntentResultV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                }

                return false;
            }

            var kind = (Gate3ClientIntentDispositionKind)rawKind;
            if (!IsKnownDisposition(kind))
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_DISPOSITION;
                return false;
            }

            if (receiveTick < 0)
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_NUMERIC_FIELD;
                return false;
            }

            if (hasStateVersion != 0 && hasStateVersion != 1)
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_STATE_VERSION_FLAG;
                return false;
            }

            StateVersion? stateVersion = null;
            if (hasStateVersion == 1)
            {
                if (!reader.TryReadInt64LittleEndian(out var rawStateVersion))
                {
                    failure = Gate3ClientIntentResultV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                    return false;
                }

                if (rawStateVersion < 0)
                {
                    failure = Gate3ClientIntentResultV1Failure.INVALID_NUMERIC_FIELD;
                    return false;
                }

                stateVersion = StateVersion.From(rawStateVersion);
            }

            if (!reader.TryReadUInt16LittleEndian(out var rejectionLength))
            {
                failure = Gate3ClientIntentResultV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                return false;
            }

            if (rejectionLength > MaxRejectionCodeUtf8Bytes || !reader.TryReadBytes(rejectionLength, out var rejectionBytes))
            {
                failure = rejectionLength > MaxRejectionCodeUtf8Bytes
                    ? Gate3ClientIntentResultV1Failure.INVALID_REJECTION_CODE
                    : Gate3ClientIntentResultV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                return false;
            }

            string rejectionCode;
            try
            {
                rejectionCode = StrictUtf8.GetString(rejectionBytes);
            }
            catch (DecoderFallbackException)
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_UTF8;
                return false;
            }

            if (HasLeadingUtf8Bom(rejectionBytes))
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_UTF8;
                return false;
            }

            if (reader.Remaining != 0)
            {
                failure = Gate3ClientIntentResultV1Failure.TRAILING_DATA;
                return false;
            }

            if (!ValidateDispositionPayload(kind, stateVersion, rejectionCode, out _, out failure))
            {
                return false;
            }

            result = new Gate3ClientIntentDisposition(
                ShiftId.From(shift),
                IntentId.From(intent),
                kind,
                ServerTick.From(receiveTick),
                stateVersion,
                rejectionCode);
            return true;
        }

        private static bool ValidateDispositionPayload(
            Gate3ClientIntentDispositionKind kind,
            StateVersion? stateVersion,
            string rejectionCode,
            out byte[] rejectionBytes,
            out Gate3ClientIntentResultV1Failure failure)
        {
            rejectionBytes = null;
            failure = Gate3ClientIntentResultV1Failure.NONE;
            if (kind == Gate3ClientIntentDispositionKind.PENDING)
            {
                if (stateVersion.HasValue || !string.IsNullOrEmpty(rejectionCode))
                {
                    failure = Gate3ClientIntentResultV1Failure.DISPOSITION_PAYLOAD_MISMATCH;
                    return false;
                }

                rejectionBytes = Array.Empty<byte>();
                return true;
            }

            if (kind == Gate3ClientIntentDispositionKind.APPLIED)
            {
                if (!stateVersion.HasValue || !string.IsNullOrEmpty(rejectionCode))
                {
                    failure = Gate3ClientIntentResultV1Failure.DISPOSITION_PAYLOAD_MISMATCH;
                    return false;
                }

                rejectionBytes = Array.Empty<byte>();
                return true;
            }

            if (kind != Gate3ClientIntentDispositionKind.REJECTED)
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_DISPOSITION;
                return false;
            }

            if (string.IsNullOrEmpty(rejectionCode))
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_REJECTION_CODE;
                return false;
            }

            try
            {
                rejectionBytes = StrictUtf8.GetBytes(rejectionCode);
            }
            catch (EncoderFallbackException)
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_UTF8;
                return false;
            }

            if (HasLeadingUtf8Bom(rejectionBytes))
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_UTF8;
                return false;
            }

            if (rejectionBytes.Length < 1 || rejectionBytes.Length > MaxRejectionCodeUtf8Bytes)
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_REJECTION_CODE;
                return false;
            }

            return true;
        }

        private static bool TryWriteIdentifier(string value, Func<string, bool> domainValid, List<byte> output, out Gate3ClientIntentResultV1Failure failure)
        {
            failure = Gate3ClientIntentResultV1Failure.NONE;
            if (value == null || !domainValid(value))
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_IDENTIFIER;
                return false;
            }

            byte[] utf8;
            try
            {
                utf8 = StrictUtf8.GetBytes(value);
            }
            catch (EncoderFallbackException)
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_UTF8;
                return false;
            }

            if (HasLeadingUtf8Bom(utf8))
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_UTF8;
                return false;
            }

            if (utf8.Length < 1 || utf8.Length > MaxIdentifierUtf8Bytes)
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_IDENTIFIER;
                return false;
            }

            WriteUInt16LittleEndian(output, (ushort)utf8.Length);
            output.AddRange(utf8);
            return true;
        }

        private static bool TryReadIdentifier(Reader reader, Func<string, bool> domainValid, out string value, out Gate3ClientIntentResultV1Failure failure)
        {
            value = null;
            failure = Gate3ClientIntentResultV1Failure.NONE;
            if (!reader.TryReadUInt16LittleEndian(out var length))
            {
                failure = Gate3ClientIntentResultV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                return false;
            }

            if (length < 1 || length > MaxIdentifierUtf8Bytes)
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_IDENTIFIER;
                return false;
            }

            if (!reader.TryReadBytes(length, out var utf8))
            {
                failure = Gate3ClientIntentResultV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                return false;
            }

            try
            {
                value = StrictUtf8.GetString(utf8);
            }
            catch (DecoderFallbackException)
            {
                failure = Gate3ClientIntentResultV1Failure.INVALID_UTF8;
                return false;
            }

            if (HasLeadingUtf8Bom(utf8))
            {
                value = null;
                failure = Gate3ClientIntentResultV1Failure.INVALID_UTF8;
                return false;
            }

            if (!domainValid(value))
            {
                value = null;
                failure = Gate3ClientIntentResultV1Failure.INVALID_IDENTIFIER;
                return false;
            }

            return true;
        }

        private static bool IsKnownDisposition(Gate3ClientIntentDispositionKind kind) =>
            kind == Gate3ClientIntentDispositionKind.PENDING
            || kind == Gate3ClientIntentDispositionKind.APPLIED
            || kind == Gate3ClientIntentDispositionKind.REJECTED;

        private static bool HasLeadingUtf8Bom(byte[] utf8) =>
            utf8 != null && utf8.Length >= 3 && utf8[0] == 0xef && utf8[1] == 0xbb && utf8[2] == 0xbf;

        private static bool IsShiftId(string value) => ShiftId.TryFrom(value, out _);
        private static bool IsIntentId(string value) => IntentId.TryFrom(value, out _);

        private static void WriteUInt16LittleEndian(List<byte> output, ushort value)
        {
            output.Add((byte)value);
            output.Add((byte)(value >> 8));
        }

        private static void WriteInt64LittleEndian(List<byte> output, long value)
        {
            var unsignedValue = unchecked((ulong)value);
            for (var index = 0; index < 8; index++)
            {
                output.Add((byte)(unsignedValue >> (index * 8)));
            }
        }

        private sealed class Reader
        {
            private readonly byte[] _payload;
            private int _offset;

            internal Reader(byte[] payload) => _payload = payload;
            internal int Remaining => _payload.Length - _offset;

            internal bool TryReadByte(out byte value)
            {
                if (Remaining < 1)
                {
                    value = 0;
                    return false;
                }

                value = _payload[_offset++];
                return true;
            }

            internal bool TryReadUInt16LittleEndian(out ushort value)
            {
                if (Remaining < 2)
                {
                    value = 0;
                    return false;
                }

                value = (ushort)(_payload[_offset] | (_payload[_offset + 1] << 8));
                _offset += 2;
                return true;
            }

            internal bool TryReadInt64LittleEndian(out long value)
            {
                if (Remaining < 8)
                {
                    value = 0;
                    return false;
                }

                ulong unsignedValue = 0;
                for (var index = 0; index < 8; index++)
                {
                    unsignedValue |= (ulong)_payload[_offset + index] << (index * 8);
                }

                _offset += 8;
                value = unchecked((long)unsignedValue);
                return true;
            }

            internal bool TryReadBytes(int length, out byte[] value)
            {
                if (length < 0 || Remaining < length)
                {
                    value = null;
                    return false;
                }

                value = new byte[length];
                Buffer.BlockCopy(_payload, _offset, value, 0, length);
                _offset += length;
                return true;
            }
        }
    }

    /// <summary>Server-observed original connection plus a never-reused live-connection lifetime token.</summary>
    public readonly struct Gate3NetworkOrigin : IEquatable<Gate3NetworkOrigin>
    {
        private Gate3NetworkOrigin(Gate3ServerConnectionId connectionId, Gate3ServerConnectionLifetime lifetime)
        {
            ConnectionId = connectionId;
            Lifetime = lifetime;
        }

        public Gate3ServerConnectionId ConnectionId { get; }
        public Gate3ServerConnectionLifetime Lifetime { get; }
        public bool IsValid => ConnectionId.IsValid && Lifetime.IsValid;

        public static Gate3NetworkOrigin From(Gate3ServerConnectionId connectionId, Gate3ServerConnectionLifetime lifetime)
        {
            if (!connectionId.IsValid || !lifetime.IsValid)
            {
                throw new ArgumentException("A D-026 network origin requires one valid server-observed connection lifetime.");
            }

            return new Gate3NetworkOrigin(connectionId, lifetime);
        }

        public bool Equals(Gate3NetworkOrigin other) => ConnectionId == other.ConnectionId && Lifetime == other.Lifetime;
        public override bool Equals(object obj) => obj is Gate3NetworkOrigin other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ConnectionId, Lifetime);
        public static bool operator ==(Gate3NetworkOrigin left, Gate3NetworkOrigin right) => left.Equals(right);
        public static bool operator !=(Gate3NetworkOrigin left, Gate3NetworkOrigin right) => !left.Equals(right);
    }

    public enum Gate3ClientIntentDispositionReservationStatus
    {
        ReservedPending,
        ExistingIntentIdRequiresD024,
        ResultCapacityExhausted,
        InvalidEvidence,
        LedgerDisposed
    }

    /// <summary>
    /// One server-local capacity/correlation reservation. Existing IntentId state is deliberately not a gameplay
    /// duplicate decision: eligible evidence must continue to the one D-024 owner before D-026 chooses delivery.
    /// </summary>
    public readonly struct Gate3ClientIntentDispositionReservation
    {
        internal Gate3ClientIntentDispositionReservation(Gate3ClientIntentDispositionReservationStatus status, Gate3ClientIntentDisposition disposition)
        {
            Status = status;
            Disposition = disposition;
        }

        public Gate3ClientIntentDispositionReservationStatus Status { get; }
        public Gate3ClientIntentDisposition Disposition { get; }
        public bool HasDisposition => Disposition != null;
        public bool CreatedRecord => Status == Gate3ClientIntentDispositionReservationStatus.ReservedPending;
    }

    /// <summary>
    /// The one bounded D-026 session ledger. It retains client-result correlation only; it neither admits gameplay
    /// intents nor assigns sequences, materializes batches, executes host ticks, or publishes Domain events.
    /// </summary>
    public sealed class Gate3ClientIntentDispositionLedger : IDisposable
    {
        public const int Capacity = 4096;

        private readonly ShiftId _shiftId;
        private readonly Dictionary<IntentId, Record> _records = new Dictionary<IntentId, Record>();
        private bool _disposed;

        public Gate3ClientIntentDispositionLedger(ShiftId shiftId)
        {
            if (shiftId.IsDefault)
            {
                throw new ArgumentException("Shift identifier must be initialized.", nameof(shiftId));
            }

            _shiftId = shiftId;
        }

        public int Count => _records.Count;

        /// <summary>
        /// Reserves retention before a genuinely new decoded network intent may enter the unchanged D-025 owner.
        /// An existing result correlation never preempts D-024: it only avoids allocating a second D-026 record.
        /// </summary>
        public Gate3ClientIntentDispositionReservation Reserve(IntentEnvelope envelope, Gate3NetworkOrigin origin, ServerTick authoritativeReceiveTick)
        {
            if (_disposed)
            {
                return new Gate3ClientIntentDispositionReservation(Gate3ClientIntentDispositionReservationStatus.LedgerDisposed, null);
            }

            if (!IsValidNewEvidence(envelope, origin, authoritativeReceiveTick))
            {
                return new Gate3ClientIntentDispositionReservation(Gate3ClientIntentDispositionReservationStatus.InvalidEvidence, null);
            }

            if (_records.TryGetValue(envelope.IntentId, out var existing))
            {
                return new Gate3ClientIntentDispositionReservation(
                    Gate3ClientIntentDispositionReservationStatus.ExistingIntentIdRequiresD024,
                    existing.Disposition);
            }

            if (_records.Count >= Capacity)
            {
                return new Gate3ClientIntentDispositionReservation(
                    Gate3ClientIntentDispositionReservationStatus.ResultCapacityExhausted,
                    Rejected(envelope.ShiftId, envelope.IntentId, authoritativeReceiveTick, null, "RESULT_CAPACITY_EXHAUSTED"));
            }

            var pending = Pending(envelope.ShiftId, envelope.IntentId, authoritativeReceiveTick);
            _records.Add(envelope.IntentId, new Record(origin, pending));
            return new Gate3ClientIntentDispositionReservation(Gate3ClientIntentDispositionReservationStatus.ReservedPending, pending);
        }

        /// <summary>
        /// Keeps a newly reserved pending record, or restarts only a retained result that provably never consumed a
        /// D-024 IntentId, after D-024 has now actually admitted the exact evidence. ActorNotBound never reached
        /// D-024, and D-024 rejects a shift mismatch before it records the IntentId, so both remain result
        /// correlation for a new gameplay admission rather than a D-026 duplicate decision.
        /// </summary>
        public bool TryBeginAdmittedAfterD024(
            IntentEnvelope envelope,
            Gate3NetworkOrigin currentOrigin,
            ServerTick authoritativeReceiveTick,
            bool reservationCreatedRecord)
        {
            if (_disposed
                || !IsValidNewEvidence(envelope, currentOrigin, authoritativeReceiveTick)
                || !_records.TryGetValue(envelope.IntentId, out var retained))
            {
                return false;
            }

            if (reservationCreatedRecord)
            {
                return retained.Origin == currentOrigin
                       && retained.Disposition.Kind == Gate3ClientIntentDispositionKind.PENDING;
            }

            if (retained.Disposition.Kind != Gate3ClientIntentDispositionKind.REJECTED
                || !IsPreD024RetainedRejection(retained.Disposition.RejectionCode))
            {
                return false;
            }

            retained.Origin = currentOrigin;
            retained.DeliveryAuthorized = true;
            retained.Disposition = Pending(envelope.ShiftId, envelope.IntentId, authoritativeReceiveTick);
            return true;
        }

        /// <summary>
        /// Resolves result replay/privacy only after the existing D-024 shared owner reported DuplicateIntentId.
        /// This method never determines whether gameplay admission may continue and never mutates another origin's
        /// retained result.
        /// </summary>
        public Gate3ClientIntentDispositionDelivery ResolveDuplicateAfterD024(
            IntentEnvelope envelope,
            Gate3NetworkOrigin currentOrigin,
            ServerTick authoritativeReceiveTick,
            bool reservationCreatedRecord)
        {
            if (_disposed || !IsValidNewEvidence(envelope, currentOrigin, authoritativeReceiveTick))
            {
                throw new InvalidOperationException("D-026 duplicate replay requires exact valid decoded evidence after D-024.");
            }

            if (!_records.TryGetValue(envelope.IntentId, out var retained))
            {
                throw new InvalidOperationException("D-024 reported a duplicate without the D-026 reservation correlation.");
            }

            if (reservationCreatedRecord)
            {
                if (retained.Origin != currentOrigin
                    || !TryTerminalize(envelope.IntentId, null, "INTENT_ID_ALREADY_USED"))
                {
                    throw new InvalidOperationException("A newly reserved D-026 record could not become its D-024 duplicate terminal result.");
                }

                return new Gate3ClientIntentDispositionDelivery(retained.Origin, retained.Disposition, retained.DeliveryAuthorized);
            }

            if (retained.Origin == currentOrigin && retained.DeliveryAuthorized)
            {
                return new Gate3ClientIntentDispositionDelivery(retained.Origin, retained.Disposition, true);
            }

            return new Gate3ClientIntentDispositionDelivery(
                currentOrigin,
                Rejected(envelope.ShiftId, envelope.IntentId, authoritativeReceiveTick, null, "INTENT_ID_ALREADY_USED"),
                true);
        }

        /// <summary>Creates an unretained current-origin admission rejection without exposing another origin's record.</summary>
        public Gate3ClientIntentDisposition CreateUnretainedAdmissionRejection(
            IntentEnvelope envelope,
            ServerTick authoritativeReceiveTick,
            string rejectionCode)
        {
            if (_disposed
                || envelope == null
                || envelope.ShiftId.IsDefault
                || envelope.IntentId.IsDefault
                || authoritativeReceiveTick.IsDefault
                || authoritativeReceiveTick.Value < 0
                || string.IsNullOrEmpty(rejectionCode))
            {
                throw new InvalidOperationException("D-026 requires valid decoded evidence for an unretained admission result.");
            }

            return Rejected(envelope.ShiftId, envelope.IntentId, authoritativeReceiveTick, null, rejectionCode);
        }

        /// <summary>Commits one admission-stage terminal mapping while preserving the pre-admission origin and receive tick.</summary>
        public bool TryTerminalizeAdmission(IntentId intentId, string rejectionCode)
        {
            return TryTerminalize(intentId, null, rejectionCode);
        }

        /// <summary>Projects a terminal result only from one exact stage-two trace after HostSession success returned.</summary>
        public IReadOnlyList<Gate3ClientIntentDispositionDelivery> ProjectSuccessfulTick(AcceptedIntentStageExecution stageTwo)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Gate3ClientIntentDispositionLedger));
            }

            if (stageTwo == null)
            {
                throw new ArgumentNullException(nameof(stageTwo));
            }

            var deliveries = new List<Gate3ClientIntentDispositionDelivery>();
            foreach (var step in stageTwo.Steps)
            {
                var intentId = step.Receipt.Envelope.IntentId;
                if (!_records.TryGetValue(intentId, out var record) || record.Disposition.Kind != Gate3ClientIntentDispositionKind.PENDING)
                {
                    continue;
                }

                if (!TryProjectStageTwoStep(step, record.Disposition.AuthoritativeReceiveTick, out var terminal))
                {
                    throw new InvalidOperationException("The closed stage-two outcome has no approved D-026 disposition mapping.");
                }

                record.Disposition = terminal;
                deliveries.Add(new Gate3ClientIntentDispositionDelivery(record.Origin, record.Disposition, record.DeliveryAuthorized));
            }

            return deliveries;
        }

        public bool TryGetDisposition(IntentId intentId, out Gate3ClientIntentDisposition disposition)
        {
            if (_records.TryGetValue(intentId, out var record))
            {
                disposition = record.Disposition;
                return true;
            }

            disposition = null;
            return false;
        }

        public bool IsDeliveryAuthorized(IntentId intentId, Gate3NetworkOrigin origin) =>
            _records.TryGetValue(intentId, out var record) && record.Origin == origin && record.DeliveryAuthorized;

        /// <summary>Returns retained origin correlation only for a record owned by this current session ledger.</summary>
        public bool TryGetDelivery(IntentId intentId, out Gate3ClientIntentDispositionDelivery delivery)
        {
            if (_records.TryGetValue(intentId, out var record))
            {
                delivery = new Gate3ClientIntentDispositionDelivery(record.Origin, record.Disposition, record.DeliveryAuthorized);
                return true;
            }

            delivery = default;
            return false;
        }

        /// <summary>Disconnect permanently ends network delivery/replay authorization while retaining gameplay identity state.</summary>
        public void RevokeDelivery(Gate3NetworkOrigin origin)
        {
            foreach (var record in _records.Values)
            {
                if (record.Origin == origin)
                {
                    record.DeliveryAuthorized = false;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _records.Clear();
        }

        /// <summary>Explicit stable mapping; unknown values cannot leak enum names or exception text into the protocol.</summary>
        public static bool TryMapStageTwoRejection(RejectionReason reason, out string code)
        {
            switch (reason)
            {
                case RejectionReason.SHIFT_MISMATCH: code = "SHIFT_MISMATCH"; return true;
                case RejectionReason.ACTOR_NOT_BOUND: code = "ACTOR_NOT_BOUND"; return true;
                case RejectionReason.STALE_STATE_VERSION: code = "STALE_STATE_VERSION"; return true;
                case RejectionReason.TARGET_NOT_FOUND: code = "TARGET_NOT_FOUND"; return true;
                case RejectionReason.TARGET_NOT_IN_STATE: code = "TARGET_NOT_IN_STATE"; return true;
                case RejectionReason.TARGET_OCCUPIED: code = "TARGET_OCCUPIED"; return true;
                case RejectionReason.MISSING_ITEM: code = "MISSING_ITEM"; return true;
                case RejectionReason.HOLD_NOT_COMPLETE: code = "HOLD_NOT_COMPLETE"; return true;
                case RejectionReason.FEED_ALREADY_PENDING: code = "FEED_ALREADY_PENDING"; return true;
                case RejectionReason.FEED_GATE_OCCUPIED: code = "FEED_GATE_OCCUPIED"; return true;
                case RejectionReason.LINE_NOT_CLEAR: code = "LINE_NOT_CLEAR"; return true;
                case RejectionReason.BLOCKING_CONDITION_REMAINS: code = "BLOCKING_CONDITION_REMAINS"; return true;
                case RejectionReason.NO_ACTIVE_REQUEST: code = "NO_ACTIVE_REQUEST"; return true;
                case RejectionReason.NO_MORE_LOGS: code = "NO_MORE_LOGS"; return true;
                case RejectionReason.MALFORMED_PROCEDURE_PARAMETERS: code = "MALFORMED_PROCEDURE_PARAMETERS"; return true;
                case RejectionReason.PROCEDURE_HOLD_ACTIVE: code = "PROCEDURE_HOLD_ACTIVE"; return true;
                case RejectionReason.PROCEDURE_NO_PLAN: code = "PROCEDURE_NO_PLAN"; return true;
                case RejectionReason.PROCEDURE_OUT_OF_ORDER_ITEM: code = "PROCEDURE_OUT_OF_ORDER_ITEM"; return true;
                case RejectionReason.PROCEDURE_REPEATED_STEP: code = "PROCEDURE_REPEATED_STEP"; return true;
                case RejectionReason.PROCEDURE_UNCONFIGURED_ITEM: code = "PROCEDURE_UNCONFIGURED_ITEM"; return true;
                case RejectionReason.MALFORMED_CONFIRMATION_PARAMETERS: code = "MALFORMED_CONFIRMATION_PARAMETERS"; return true;
                case RejectionReason.CONFIRMATION_ACTIVE: code = "CONFIRMATION_ACTIVE"; return true;
                case RejectionReason.CONFIRMATION_ALREADY_COMPLETED: code = "CONFIRMATION_ALREADY_COMPLETED"; return true;
                case RejectionReason.CONFIRMATION_NO_PLAN: code = "CONFIRMATION_NO_PLAN"; return true;
                case RejectionReason.CONFIRMATION_REQUIRED_TOOL_UNAVAILABLE: code = "CONFIRMATION_REQUIRED_TOOL_UNAVAILABLE"; return true;
                case RejectionReason.CONFIRMATION_REQUIRED_LINE_NOISE_NOT_MET: code = "CONFIRMATION_REQUIRED_LINE_NOISE_NOT_MET"; return true;
                case RejectionReason.MALFORMED_LINE_REPAIR_PARAMETERS: code = "MALFORMED_LINE_REPAIR_PARAMETERS"; return true;
                case RejectionReason.NO_ACTIVE_JAM: code = "NO_ACTIVE_JAM"; return true;
                case RejectionReason.REPAIR_ALREADY_ACTIVE: code = "REPAIR_ALREADY_ACTIVE"; return true;
                case RejectionReason.MALFORMED_CONTAINMENT_RITUAL_PARAMETERS: code = "MALFORMED_CONTAINMENT_RITUAL_PARAMETERS"; return true;
                case RejectionReason.RITUAL_ALREADY_ACTIVE: code = "RITUAL_ALREADY_ACTIVE"; return true;
                default: code = null; return false;
            }
        }

        private bool TryTerminalize(IntentId intentId, StateVersion? stateVersion, string rejectionCode)
        {
            if (_disposed || !_records.TryGetValue(intentId, out var record) || record.Disposition.Kind != Gate3ClientIntentDispositionKind.PENDING)
            {
                return false;
            }

            record.Disposition = Rejected(record.Disposition.ShiftId, intentId, record.Disposition.AuthoritativeReceiveTick, stateVersion, rejectionCode);
            return true;
        }

        private bool TryProjectStageTwoStep(AcceptedIntentStageStep step, ServerTick authoritativeReceiveTick, out Gate3ClientIntentDisposition terminal)
        {
            var intentId = step.Receipt.Envelope.IntentId;
            var stateVersion = step.AfterState.StateVersion;
            switch (step.Outcome)
            {
                case ManualRoutingIntentStageOutcome { Result: ManualLogIntentRejected rejected }:
                    return TryMapTypedRejection(intentId, authoritativeReceiveTick, stateVersion, rejected.Reason, out terminal);
                case EarlyFeedIntentStageOutcome { Result: EarlyFeedIntentRejected rejected }:
                    return TryMapTypedRejection(intentId, authoritativeReceiveTick, stateVersion, rejected.Reason, out terminal);
                case ProcedureActionIntentStageOutcome { Result: ProcedureActionIntentRejected rejected }:
                    return TryMapTypedRejection(intentId, authoritativeReceiveTick, stateVersion, rejected.Reason, out terminal);
                case ProcedureActionIntentStageOutcome { Result: ProcedureActionIntentUnderlyingRejected rejected }:
                    return TryMapTypedRejection(intentId, authoritativeReceiveTick, stateVersion, rejected.Reason, out terminal);
                case ConfirmationTestIntentStageOutcome { Result: ConfirmationTestIntentRejected rejected }:
                    return TryMapTypedRejection(intentId, authoritativeReceiveTick, stateVersion, rejected.Reason, out terminal);
                case ConfirmationTestIntentStageOutcome { Result: ConfirmationTestIntentUnderlyingRejected rejected }:
                    return TryMapTypedRejection(intentId, authoritativeReceiveTick, stateVersion, rejected.Reason, out terminal);
                case LineRepairIntentStageOutcome { Result: LineRepairIntentRejected rejected }:
                    return TryMapTypedRejection(intentId, authoritativeReceiveTick, stateVersion, rejected.Reason, out terminal);
                case LineRepairIntentStageOutcome { Result: LineRepairIntentUnderlyingRejected rejected }:
                    return TryMapTypedRejection(intentId, authoritativeReceiveTick, stateVersion, rejected.Reason, out terminal);
                case ContainmentRitualIntentStageOutcome { Result: ContainmentRitualIntentRejected rejected }:
                    return TryMapTypedRejection(intentId, authoritativeReceiveTick, stateVersion, rejected.Reason, out terminal);
                case ContainmentRitualIntentStageOutcome { Result: ContainmentRitualIntentUnderlyingRejected rejected }:
                    return TryMapTypedRejection(intentId, authoritativeReceiveTick, stateVersion, rejected.Reason, out terminal);
                case ManualRoutingIntentStageOutcome { Result: DuplicateIntentIgnored }:
                case EarlyFeedIntentStageOutcome { Result: DuplicateEarlyFeedIntentIgnored }:
                case ProcedureActionIntentStageOutcome { Result: ProcedureActionIntentDuplicateIgnored }:
                case ConfirmationTestIntentStageOutcome { Result: ConfirmationTestIntentDuplicateIgnored }:
                case LineRepairIntentStageOutcome { Result: LineRepairIntentDuplicateIgnored }:
                case ContainmentRitualIntentStageOutcome { Result: ContainmentRitualIntentDuplicateIgnored }:
                    terminal = Rejected(_shiftId, intentId, authoritativeReceiveTick, stateVersion, "INTENT_ALREADY_PROCESSED");
                    return true;
                case UnsupportedIntentStageOutcome:
                    terminal = Rejected(_shiftId, intentId, authoritativeReceiveTick, stateVersion, "UNSUPPORTED_ACTION");
                    return true;
                case ManualRoutingIntentStageOutcome { Result: ManualLogIntentAccepted }:
                case EarlyFeedIntentStageOutcome { Result: EarlyFeedScheduled }:
                case ProcedureActionIntentStageOutcome { Result: ProcedureActionIntentHoldStarted }:
                case ProcedureActionIntentStageOutcome { Result: ProcedureActionIntentCompletedImmediately }:
                case ConfirmationTestIntentStageOutcome { Result: ConfirmationTestIntentStarted }:
                case LineRepairIntentStageOutcome { Result: LineRepairIntentStarted }:
                case ContainmentRitualIntentStageOutcome { Result: ContainmentRitualIntentStarted }:
                    terminal = Applied(_shiftId, intentId, authoritativeReceiveTick, stateVersion);
                    return true;
                default:
                    terminal = null;
                    return false;
            }
        }

        private bool TryMapTypedRejection(IntentId intentId, ServerTick tick, StateVersion stateVersion, RejectionReason reason, out Gate3ClientIntentDisposition terminal)
        {
            if (!TryMapStageTwoRejection(reason, out var code))
            {
                terminal = null;
                return false;
            }

            terminal = Rejected(_shiftId, intentId, tick, stateVersion, code);
            return true;
        }

        /// <summary>
        /// The exact retained rejections D-024 produced without consuming the IntentId. `ACTOR_NOT_BOUND` never
        /// reached D-024 at all, and D-024's frozen ordering rejects `SHIFT_MISMATCH` before its seen-IntentId add.
        /// Every other retained result corresponds to a consumed IntentId and stays D-024's duplicate decision.
        /// </summary>
        private static bool IsPreD024RetainedRejection(string rejectionCode) =>
            rejectionCode == "ACTOR_NOT_BOUND" || rejectionCode == "SHIFT_MISMATCH";

        private bool IsValidNewEvidence(IntentEnvelope envelope, Gate3NetworkOrigin origin, ServerTick tick) =>
            envelope != null
            && !envelope.ShiftId.IsDefault
            && !envelope.IntentId.IsDefault
            && origin.IsValid
            && !tick.IsDefault
            && tick.Value >= 0;

        private static Gate3ClientIntentDisposition Pending(ShiftId shiftId, IntentId intentId, ServerTick tick) =>
            new Gate3ClientIntentDisposition(shiftId, intentId, Gate3ClientIntentDispositionKind.PENDING, tick, null, null);

        private static Gate3ClientIntentDisposition Applied(ShiftId shiftId, IntentId intentId, ServerTick tick, StateVersion stateVersion) =>
            new Gate3ClientIntentDisposition(shiftId, intentId, Gate3ClientIntentDispositionKind.APPLIED, tick, stateVersion, null);

        private static Gate3ClientIntentDisposition Rejected(ShiftId shiftId, IntentId intentId, ServerTick tick, StateVersion? stateVersion, string code) =>
            new Gate3ClientIntentDisposition(shiftId, intentId, Gate3ClientIntentDispositionKind.REJECTED, tick, stateVersion, code);

        private sealed class Record
        {
            internal Record(Gate3NetworkOrigin origin, Gate3ClientIntentDisposition disposition)
            {
                Origin = origin;
                Disposition = disposition;
                DeliveryAuthorized = true;
            }

            internal Gate3NetworkOrigin Origin { get; set; }
            internal Gate3ClientIntentDisposition Disposition { get; set; }
            internal bool DeliveryAuthorized { get; set; }
        }
    }

    /// <summary>One retained result and its original delivery authorization, produced only by the session ledger.</summary>
    public readonly struct Gate3ClientIntentDispositionDelivery
    {
        internal Gate3ClientIntentDispositionDelivery(Gate3NetworkOrigin origin, Gate3ClientIntentDisposition disposition, bool deliveryAuthorized)
        {
            Origin = origin;
            Disposition = disposition;
            DeliveryAuthorized = deliveryAuthorized;
        }

        public Gate3NetworkOrigin Origin { get; }
        public Gate3ClientIntentDisposition Disposition { get; }
        public bool DeliveryAuthorized { get; }
    }
}
