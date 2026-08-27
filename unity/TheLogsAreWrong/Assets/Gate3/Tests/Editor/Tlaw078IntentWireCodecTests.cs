using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Gate3.Tests
{
    /// <summary>Executable D-023 V1 byte, materialization, and fail-closed contracts.</summary>
    public sealed class Tlaw078IntentWireCodecTests
    {
        [Test]
        public void None_round_trip_has_the_frozen_little_endian_v1_field_order()
        {
            var expectedStateVersion = 0x0102030405060708L;
            var observedTick = 0x1112131415161718L;
            var envelope = CreateEnvelope("s", "i", "a", "t", "x", expectedStateVersion, observedTick, NoIntentParameters.Instance);

            var payload = Encode(envelope);
            var expected = new List<byte>
            {
                1, 0,
                1, 0, (byte)'s',
                1, 0, (byte)'i',
                1, 0, (byte)'a',
                1, 0, (byte)'t',
                1, 0, (byte)'x'
            };
            AppendInt64LittleEndian(expected, expectedStateVersion);
            AppendInt64LittleEndian(expected, observedTick);
            expected.Add(1);
            CollectionAssert.AreEqual(expected, payload);

            var decoded = Decode(payload);
            AssertEnvelope(decoded, envelope);
            Assert.AreSame(NoIntentParameters.Instance, decoded.Parameters);
        }

        [Test]
        public void Procedure_action_round_trip_preserves_exact_attempted_item()
        {
            var envelope = CreateEnvelope(parameters: new ProcedureActionIntentParameters(ItemId.From("holy_water")));

            var decoded = Decode(Encode(envelope));

            AssertEnvelope(decoded, envelope);
            var parameters = decoded.Parameters as ProcedureActionIntentParameters;
            Assert.IsNotNull(parameters);
            Assert.AreEqual("holy_water", parameters.AttemptedItem.Value);
        }

        [Test]
        public void Equivalent_supported_input_encodes_byte_for_byte_deterministically()
        {
            var first = Encode(CreateEnvelope(parameters: new ProcedureActionIntentParameters(ItemId.From("holy_water"))));
            var second = Encode(CreateEnvelope(parameters: new ProcedureActionIntentParameters(ItemId.From("holy_water"))));

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void Identifier_boundaries_and_multibyte_utf8_byte_limits_are_enforced()
        {
            Assert.IsNotEmpty(Encode(CreateEnvelope("s")));
            Assert.IsNotEmpty(Encode(CreateEnvelope(new string('s', 256))));
            Assert.IsNotEmpty(Encode(CreateEnvelope(new string('\u00e9', 128))));

            AssertEncodeFailure(CreateEnvelope(new string('\u00e9', 129)), Gate3IntentWireV1Failure.INVALID_IDENTIFIER);

            var zeroLength = Encode(CreateEnvelope());
            zeroLength[2] = 0;
            zeroLength[3] = 0;
            AssertDecodeFailure(zeroLength, Gate3IntentWireV1Failure.INVALID_IDENTIFIER);

            var maxLength = Encode(CreateEnvelope(new string('s', 256)));
            maxLength[2] = 1;
            maxLength[3] = 1;
            var oversized = new List<byte>(maxLength);
            oversized.Insert(260, (byte)'s');
            AssertDecodeFailure(oversized.ToArray(), Gate3IntentWireV1Failure.INVALID_IDENTIFIER);
        }

        [Test]
        public void Invalid_utf8_and_payloads_larger_than_the_frozen_bound_fail_closed()
        {
            var invalidUtf8 = Encode(CreateEnvelope());
            invalidUtf8[4] = 0xc3;
            AssertDecodeFailure(invalidUtf8, Gate3IntentWireV1Failure.INVALID_UTF8);

            AssertDecodeFailure(new byte[Gate3IntentWireV1Codec.MaxPayloadBytes + 1], Gate3IntentWireV1Failure.MESSAGE_TOO_LARGE);
        }

        [Test]
        public void Leading_utf8_bom_is_rejected_at_every_shared_identifier_encode_decode_boundary()
        {
            var leadingBom = "\ufeff";
            foreach (var envelope in new[]
            {
                CreateEnvelope(shift: leadingBom + "shift"),
                CreateEnvelope(intent: leadingBom + "intent"),
                CreateEnvelope(actor: leadingBom + "actor_hint"),
                CreateEnvelope(target: leadingBom + "target"),
                CreateEnvelope(action: leadingBom + "action"),
                CreateEnvelope(parameters: new ProcedureActionIntentParameters(ItemId.From(leadingBom + "holy_water")))
            })
            {
                AssertEncodeFailure(envelope, Gate3IntentWireV1Failure.INVALID_UTF8);
            }

            var outerIdentifier = PrependUtf8BomToLengthPrefixedIdentifier(Encode(CreateEnvelope()), 2);
            AssertDecodeFailure(outerIdentifier, Gate3IntentWireV1Failure.INVALID_UTF8);

            var procedure = Encode(CreateEnvelope(parameters: new ProcedureActionIntentParameters(ItemId.From("holy_water"))));
            var attemptedItem = PrependUtf8BomToLengthPrefixedIdentifier(procedure, ParameterKindOffset(procedure) + 1);
            AssertDecodeFailure(attemptedItem, Gate3IntentWireV1Failure.INVALID_UTF8);
        }

        [Test]
        public void Unsupported_versions_and_invalid_numeric_fields_fail_closed()
        {
            AssertDecodeFailure(new byte[] { 0, 0 }, Gate3IntentWireV1Failure.UNSUPPORTED_SCHEMA_VERSION);
            AssertDecodeFailure(new byte[] { 2, 0 }, Gate3IntentWireV1Failure.UNSUPPORTED_SCHEMA_VERSION);

            var negativeStateVersion = Encode(CreateEnvelope());
            var numericOffset = FixedNumericOffset(negativeStateVersion);
            for (var index = 0; index < 8; index++) negativeStateVersion[numericOffset + index] = 0xff;
            AssertDecodeFailure(negativeStateVersion, Gate3IntentWireV1Failure.INVALID_NUMERIC_FIELD);

            var negativeObservedTick = Encode(CreateEnvelope());
            numericOffset = FixedNumericOffset(negativeObservedTick) + 8;
            for (var index = 0; index < 8; index++) negativeObservedTick[numericOffset + index] = 0xff;
            AssertDecodeFailure(negativeObservedTick, Gate3IntentWireV1Failure.INVALID_NUMERIC_FIELD);
        }

        [Test]
        public void Parameter_discriminators_payload_shapes_and_trailing_data_are_fail_closed()
        {
            var none = Encode(CreateEnvelope());
            var kindOffset = ParameterKindOffset(none);

            var reserved = (byte[])none.Clone();
            reserved[kindOffset] = 0;
            AssertDecodeFailure(reserved, Gate3IntentWireV1Failure.UNSUPPORTED_PARAMETER_KIND);

            var unknown = (byte[])none.Clone();
            unknown[kindOffset] = 99;
            AssertDecodeFailure(unknown, Gate3IntentWireV1Failure.UNSUPPORTED_PARAMETER_KIND);

            AssertDecodeFailure(none.Concat(new byte[] { 1 }).ToArray(), Gate3IntentWireV1Failure.PARAMETER_PAYLOAD_MISMATCH);

            var procedure = Encode(CreateEnvelope(parameters: new ProcedureActionIntentParameters(ItemId.From("holy_water"))));
            kindOffset = ParameterKindOffset(procedure);
            AssertDecodeFailure(procedure.Take(kindOffset + 1).ToArray(), Gate3IntentWireV1Failure.PARAMETER_PAYLOAD_MISMATCH);
            AssertDecodeFailure(procedure.Concat(new byte[] { 0 }).ToArray(), Gate3IntentWireV1Failure.TRAILING_DATA);
            AssertDecodeFailure(procedure.Take(procedure.Length - 1).ToArray(), Gate3IntentWireV1Failure.TRUNCATED_OR_MALFORMED_FRAME);
        }

        [Test]
        public void Unknown_action_and_action_parameter_gameplay_compatibility_remain_for_stage_two()
        {
            var unknownAction = Decode(Encode(CreateEnvelope(action: "future_structurally_valid_action")));
            Assert.AreEqual("future_structurally_valid_action", unknownAction.Action.Value);

            var gameplayIncompatible = Decode(Encode(CreateEnvelope(
                action: "route_to_procedure",
                parameters: new ProcedureActionIntentParameters(ItemId.From("holy_water")))));
            Assert.IsInstanceOf<ProcedureActionIntentParameters>(gameplayIncompatible.Parameters);
            Assert.AreEqual("route_to_procedure", gameplayIncompatible.Action.Value);
        }

        [Test]
        public void Truncated_outer_fields_produce_no_envelope()
        {
            var complete = Encode(CreateEnvelope());
            AssertDecodeFailure(complete.Take(1).ToArray(), Gate3IntentWireV1Failure.TRUNCATED_OR_MALFORMED_FRAME);
            AssertDecodeFailure(complete.Take(complete.Length - 1).ToArray(), Gate3IntentWireV1Failure.TRUNCATED_OR_MALFORMED_FRAME);
        }

        private static byte[] Encode(IntentEnvelope envelope)
        {
            Assert.IsTrue(Gate3IntentWireV1Codec.TryEncode(envelope, out var payload, out var failure), failure.ToString());
            Assert.AreEqual(Gate3IntentWireV1Failure.NONE, failure);
            Assert.IsNotNull(payload);
            return payload;
        }

        private static IntentEnvelope Decode(byte[] payload)
        {
            Assert.IsTrue(Gate3IntentWireV1Codec.TryDecode(payload, out var envelope, out var failure), failure.ToString());
            Assert.AreEqual(Gate3IntentWireV1Failure.NONE, failure);
            Assert.IsNotNull(envelope);
            return envelope;
        }

        private static void AssertEncodeFailure(IntentEnvelope envelope, Gate3IntentWireV1Failure expected)
        {
            Assert.IsFalse(Gate3IntentWireV1Codec.TryEncode(envelope, out var payload, out var failure));
            Assert.IsNull(payload);
            Assert.AreEqual(expected, failure);
        }

        private static void AssertDecodeFailure(byte[] payload, Gate3IntentWireV1Failure expected)
        {
            Assert.IsFalse(Gate3IntentWireV1Codec.TryDecode(payload, out var envelope, out var failure));
            Assert.IsNull(envelope);
            Assert.AreEqual(expected, failure);
        }

        private static IntentEnvelope CreateEnvelope(
            string shift = "shift",
            string intent = "intent",
            string actor = "actor_hint",
            string target = "target",
            string action = "action",
            long expectedStateVersion = 7,
            long observedTick = 11,
            IIntentParameters parameters = null)
        {
            return new IntentEnvelope(
                ShiftId.From(shift),
                IntentId.From(intent),
                ActorId.From(actor),
                TargetId.From(target),
                IntentActionId.From(action),
                StateVersion.From(expectedStateVersion),
                ServerTick.From(observedTick),
                parameters ?? NoIntentParameters.Instance);
        }

        private static int FixedNumericOffset(byte[] payload)
        {
            var offset = 2;
            for (var index = 0; index < 5; index++)
            {
                offset += 2 + ReadUInt16LittleEndian(payload, offset);
            }

            return offset;
        }

        private static int ParameterKindOffset(byte[] payload) => FixedNumericOffset(payload) + 16;

        private static byte[] PrependUtf8BomToLengthPrefixedIdentifier(byte[] payload, int lengthOffset)
        {
            var withBom = new List<byte>(payload);
            var length = ReadUInt16LittleEndian(payload, lengthOffset);
            withBom[lengthOffset] = (byte)(length + 3);
            withBom[lengthOffset + 1] = (byte)((length + 3) >> 8);
            withBom.InsertRange(lengthOffset + 2, new byte[] { 0xef, 0xbb, 0xbf });
            return withBom.ToArray();
        }

        private static int ReadUInt16LittleEndian(byte[] payload, int offset) => payload[offset] | (payload[offset + 1] << 8);

        private static void AppendInt64LittleEndian(List<byte> output, long value)
        {
            var unsigned = unchecked((ulong)value);
            for (var index = 0; index < 8; index++) output.Add((byte)(unsigned >> (index * 8)));
        }

        private static void AssertEnvelope(IntentEnvelope actual, IntentEnvelope expected)
        {
            Assert.AreEqual(expected.ShiftId, actual.ShiftId);
            Assert.AreEqual(expected.IntentId, actual.IntentId);
            Assert.AreEqual(expected.ActorIdHint, actual.ActorIdHint);
            Assert.AreEqual(expected.TargetId, actual.TargetId);
            Assert.AreEqual(expected.Action, actual.Action);
            Assert.AreEqual(expected.ExpectedStateVersion, actual.ExpectedStateVersion);
            Assert.AreEqual(expected.ClientObservedTick, actual.ClientObservedTick);
        }
    }
}
