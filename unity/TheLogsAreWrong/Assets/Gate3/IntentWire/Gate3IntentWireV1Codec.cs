using System;
using System.Collections.Generic;
using System.Text;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Gate3
{
    /// <summary>Local bounded outcomes for D-023 V1 encoding and materialization; these values are not on-wire.</summary>
    public enum Gate3IntentWireV1Failure
    {
        NONE = 0,
        MESSAGE_TOO_LARGE,
        TRUNCATED_OR_MALFORMED_FRAME,
        INVALID_UTF8,
        UNSUPPORTED_SCHEMA_VERSION,
        INVALID_IDENTIFIER,
        INVALID_NUMERIC_FIELD,
        UNSUPPORTED_PARAMETER_KIND,
        PARAMETER_PAYLOAD_MISMATCH,
        TRAILING_DATA
    }

    /// <summary>Permanent V1 parameter tags. Values are frozen by D-023 and are never reused.</summary>
    public enum Gate3IntentWireV1ParameterKind : byte
    {
        NONE = 1,
        PROCEDURE_ACTION = 2
    }

    /// <summary>
    /// Deterministic D-023 V1 byte encoder and materializer. It ends at ordinary client-provided
    /// <see cref="IntentEnvelope"/> evidence and deliberately performs no authority or gameplay work.
    /// </summary>
    public static class Gate3IntentWireV1Codec
    {
        public const ushort SchemaVersion = 1;
        public const int MaxPayloadBytes = 2048;
        public const int MaxIdentifierUtf8Bytes = 256;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static bool TryEncode(IntentEnvelope envelope, out byte[] payload, out Gate3IntentWireV1Failure failure)
        {
            payload = null;
            failure = Gate3IntentWireV1Failure.NONE;
            if (envelope == null)
            {
                failure = Gate3IntentWireV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                return false;
            }

            var output = new List<byte>();
            WriteUInt16LittleEndian(output, SchemaVersion);
            if (!TryWriteIdentifier(envelope.ShiftId.Value, IsShiftId, output, out failure)
                || !TryWriteIdentifier(envelope.IntentId.Value, IsIntentId, output, out failure)
                || !TryWriteIdentifier(envelope.ActorIdHint.Value, IsActorId, output, out failure)
                || !TryWriteIdentifier(envelope.TargetId.Value, IsTargetId, output, out failure)
                || !TryWriteIdentifier(envelope.Action.Value, IsIntentActionId, output, out failure))
            {
                return false;
            }

            if (envelope.ExpectedStateVersion.IsDefault || envelope.ClientObservedTick.IsDefault)
            {
                failure = Gate3IntentWireV1Failure.INVALID_NUMERIC_FIELD;
                return false;
            }

            var expectedStateVersion = envelope.ExpectedStateVersion.Value;
            var clientObservedTick = envelope.ClientObservedTick.Value;
            if (expectedStateVersion < 0 || clientObservedTick < 0)
            {
                failure = Gate3IntentWireV1Failure.INVALID_NUMERIC_FIELD;
                return false;
            }

            WriteInt64LittleEndian(output, expectedStateVersion);
            WriteInt64LittleEndian(output, clientObservedTick);

            if (envelope.Parameters is NoIntentParameters)
            {
                output.Add((byte)Gate3IntentWireV1ParameterKind.NONE);
            }
            else if (envelope.Parameters is ProcedureActionIntentParameters procedureAction)
            {
                output.Add((byte)Gate3IntentWireV1ParameterKind.PROCEDURE_ACTION);
                if (!TryWriteIdentifier(procedureAction.AttemptedItem.Value, IsItemId, output, out failure))
                {
                    return false;
                }
            }
            else
            {
                failure = Gate3IntentWireV1Failure.UNSUPPORTED_PARAMETER_KIND;
                return false;
            }

            if (output.Count > MaxPayloadBytes)
            {
                failure = Gate3IntentWireV1Failure.MESSAGE_TOO_LARGE;
                return false;
            }

            payload = output.ToArray();
            return true;
        }

        public static bool TryDecode(byte[] payload, out IntentEnvelope envelope, out Gate3IntentWireV1Failure failure)
        {
            envelope = null;
            failure = Gate3IntentWireV1Failure.NONE;
            if (payload == null || payload.Length > MaxPayloadBytes)
            {
                failure = payload != null && payload.Length > MaxPayloadBytes
                    ? Gate3IntentWireV1Failure.MESSAGE_TOO_LARGE
                    : Gate3IntentWireV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                return false;
            }

            var reader = new Reader(payload);
            if (!reader.TryReadUInt16LittleEndian(out var schemaVersion))
            {
                failure = Gate3IntentWireV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                return false;
            }

            if (schemaVersion != SchemaVersion)
            {
                failure = Gate3IntentWireV1Failure.UNSUPPORTED_SCHEMA_VERSION;
                return false;
            }

            if (!TryReadIdentifier(reader, IsShiftId, out var shift, out failure)
                || !TryReadIdentifier(reader, IsIntentId, out var intent, out failure)
                || !TryReadIdentifier(reader, IsActorId, out var actorHint, out failure)
                || !TryReadIdentifier(reader, IsTargetId, out var target, out failure)
                || !TryReadIdentifier(reader, IsIntentActionId, out var action, out failure))
            {
                return false;
            }

            if (!reader.TryReadInt64LittleEndian(out var expectedStateVersion)
                || !reader.TryReadInt64LittleEndian(out var clientObservedTick))
            {
                failure = Gate3IntentWireV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                return false;
            }

            if (expectedStateVersion < 0 || clientObservedTick < 0)
            {
                failure = Gate3IntentWireV1Failure.INVALID_NUMERIC_FIELD;
                return false;
            }

            if (!reader.TryReadByte(out var parameterKind))
            {
                failure = Gate3IntentWireV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                return false;
            }

            IIntentParameters parameters;
            switch ((Gate3IntentWireV1ParameterKind)parameterKind)
            {
                case Gate3IntentWireV1ParameterKind.NONE:
                    if (reader.Remaining != 0)
                    {
                        failure = Gate3IntentWireV1Failure.PARAMETER_PAYLOAD_MISMATCH;
                        return false;
                    }

                    parameters = NoIntentParameters.Instance;
                    break;

                case Gate3IntentWireV1ParameterKind.PROCEDURE_ACTION:
                    if (reader.Remaining == 0)
                    {
                        failure = Gate3IntentWireV1Failure.PARAMETER_PAYLOAD_MISMATCH;
                        return false;
                    }

                    if (!TryReadIdentifier(reader, IsItemId, out var attemptedItem, out failure))
                    {
                        return false;
                    }

                    if (reader.Remaining != 0)
                    {
                        failure = Gate3IntentWireV1Failure.TRAILING_DATA;
                        return false;
                    }

                    parameters = new ProcedureActionIntentParameters(ItemId.From(attemptedItem));
                    break;

                default:
                    failure = Gate3IntentWireV1Failure.UNSUPPORTED_PARAMETER_KIND;
                    return false;
            }

            envelope = new IntentEnvelope(
                ShiftId.From(shift),
                IntentId.From(intent),
                ActorId.From(actorHint),
                TargetId.From(target),
                IntentActionId.From(action),
                StateVersion.From(expectedStateVersion),
                ServerTick.From(clientObservedTick),
                parameters);
            return true;
        }

        private static bool TryWriteIdentifier(string value, Func<string, bool> domainValid, List<byte> output, out Gate3IntentWireV1Failure failure)
        {
            failure = Gate3IntentWireV1Failure.NONE;
            if (value == null || !domainValid(value))
            {
                failure = Gate3IntentWireV1Failure.INVALID_IDENTIFIER;
                return false;
            }

            byte[] utf8;
            try
            {
                utf8 = StrictUtf8.GetBytes(value);
            }
            catch (EncoderFallbackException)
            {
                failure = Gate3IntentWireV1Failure.INVALID_UTF8;
                return false;
            }

            if (HasLeadingUtf8Bom(utf8))
            {
                failure = Gate3IntentWireV1Failure.INVALID_UTF8;
                return false;
            }

            if (utf8.Length < 1 || utf8.Length > MaxIdentifierUtf8Bytes)
            {
                failure = Gate3IntentWireV1Failure.INVALID_IDENTIFIER;
                return false;
            }

            WriteUInt16LittleEndian(output, (ushort)utf8.Length);
            output.AddRange(utf8);
            return true;
        }

        private static bool TryReadIdentifier(Reader reader, Func<string, bool> domainValid, out string value, out Gate3IntentWireV1Failure failure)
        {
            value = null;
            failure = Gate3IntentWireV1Failure.NONE;
            if (!reader.TryReadUInt16LittleEndian(out var length))
            {
                failure = Gate3IntentWireV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                return false;
            }

            if (length < 1 || length > MaxIdentifierUtf8Bytes)
            {
                failure = Gate3IntentWireV1Failure.INVALID_IDENTIFIER;
                return false;
            }

            if (!reader.TryReadBytes(length, out var utf8))
            {
                failure = Gate3IntentWireV1Failure.TRUNCATED_OR_MALFORMED_FRAME;
                return false;
            }

            try
            {
                value = StrictUtf8.GetString(utf8);
            }
            catch (DecoderFallbackException)
            {
                failure = Gate3IntentWireV1Failure.INVALID_UTF8;
                return false;
            }

            if (HasLeadingUtf8Bom(utf8))
            {
                value = null;
                failure = Gate3IntentWireV1Failure.INVALID_UTF8;
                return false;
            }

            if (!domainValid(value))
            {
                value = null;
                failure = Gate3IntentWireV1Failure.INVALID_IDENTIFIER;
                return false;
            }

            return true;
        }

        private static bool HasLeadingUtf8Bom(byte[] utf8) =>
            utf8 != null
            && utf8.Length >= 3
            && utf8[0] == 0xef
            && utf8[1] == 0xbb
            && utf8[2] == 0xbf;

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

        private static bool IsShiftId(string value) => ShiftId.TryFrom(value, out _);
        private static bool IsIntentId(string value) => IntentId.TryFrom(value, out _);
        private static bool IsActorId(string value) => ActorId.TryFrom(value, out _);
        private static bool IsTargetId(string value) => TargetId.TryFrom(value, out _);
        private static bool IsIntentActionId(string value) => IntentActionId.TryFrom(value, out _);
        private static bool IsItemId(string value) => ItemId.TryFrom(value, out _);

        private sealed class Reader
        {
            private readonly byte[] _payload;
            private int _offset;

            internal Reader(byte[] payload)
            {
                _payload = payload;
            }

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
}
