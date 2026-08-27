# Gate-3 D-023 intent wire codec — TLAW-078

## Authority and scope

TLAW-078 consumes GitHub Issue #177 and implements exactly D-023: one
Gate-3-owned, plain-C# V1 encoder and decoder/materializer at
`Assets/Gate3/IntentWire/Gate3IntentWireV1Codec.cs`.  It has no UnityEngine or
transport dependency.  A successful decode ends at ordinary
PortableAuthority `IntentEnvelope` client evidence; it does not admit, order,
execute, or publish that evidence.

The frozen V1 layout is, in order:

```text
schema_version u16 LE (exactly 1)
shift_id, intent_id, actor_id_hint, target_id, action (u16 LE byte length + strict UTF-8)
expected_state_version i64 LE
client_observed_tick i64 LE
parameter_kind u8
parameter_payload
```

The complete payload is bounded to 2048 bytes.  Every identifier is encoded
as one to 256 UTF-8 bytes, is decoded with strict UTF-8 and **no leading UTF-8
BOM**, and must remain a
valid existing PortableAuthority identifier without normalization, trimming,
or case folding.  Nonnegative numeric fields materialize to existing
`StateVersion` and `ServerTick` values.  V1 parameter kind `1` is `NONE` with
an exactly empty payload and materializes `NoIntentParameters.Instance`; kind
`2` is `PROCEDURE_ACTION` with exactly one attempted `ItemId` and materializes
`ProcedureActionIntentParameters`.  The discriminators, field order, and
little-endian representation are D-023 contract, not a new policy.

Failures are bounded local outcomes, never on-wire numeric ABI:

- `MESSAGE_TOO_LARGE`
- `TRUNCATED_OR_MALFORMED_FRAME`
- `INVALID_UTF8`
- `UNSUPPORTED_SCHEMA_VERSION`
- `INVALID_IDENTIFIER`
- `INVALID_NUMERIC_FIELD`
- `UNSUPPORTED_PARAMETER_KIND`
- `PARAMETER_PAYLOAD_MISMATCH`
- `TRAILING_DATA`

On failure the decoder produces no `IntentEnvelope`.  Structurally valid but
unknown actions, including action/parameter combinations that gameplay may
later reject, intentionally remain evidence only and pass on to the existing
future Stage-Two authority boundary.

## Executable proof

Repository TLAW-078 architecture contracts passed **2/2**.  They guard the
single plain-C# codec/materializer, V1 values, strict UTF-8, explicit
little-endian helpers, and an explicit shared `EF BB BF` leading-BOM guard;
they also guard all failure categories and the absence of FishNet,
FishySteamworks, Steamworks, RPC/Broadcast, connection/actor binding,
receive-tick/sequence, accepted-batch, HostSession, replication, and transport
start/stop coupling.  They also prove preceding Gate-2 and Gate-3 seams do
not couple to this codec.

Pinned Unity `6000.3.21f1 (c02631ffc030)` executed
`Tlaw078IntentWireCodecTests`: **10/10 passed**.  The class proves exact byte
order and little-endian values, both parameter shapes, deterministic encoding,
identifier 1/256-byte and multibyte UTF-8 boundaries, malformed UTF-8,
oversized frames, versions, numeric fields, reserved/unknown discriminator,
parameter-shape mismatch, trailing data, truncation, no decoded envelope on
failure, and preservation of unknown action/gameplay compatibility for the
later authority.  Its CC-078-01 contract explicitly rejects leading `EF BB BF`
on decode with no `IntentEnvelope`, and leading U+FEFF on encode with no
payload, through each outer identifier and `attempted_item`; each uses the
frozen `INVALID_UTF8` outcome.

The full pinned Unity EditMode suite passed **85/85**.  The solution Release
build completed with zero warnings and zero errors; the full .NET suite passed
**1669/1669**.  Preserved repository slices passed: D-014/TLAW-046 **87/87**;
TLAW-067 **6/6**; TLAW-068 **10/10**; TLAW-070 **5/5**; TLAW-071 **2/2**;
TLAW-072 **2/2**; TLAW-073 **2/2**; TLAW-074 **2/2**; TLAW-075 **2/2**;
TLAW-076 **2/2**.  C1 export freshness passed.

PortableAuthority was rebuilt with the existing deployment properties using
`-t:Rebuild`; it completed with zero warnings/errors and remained byte
identical to the committed plugin:
`BD1E5DDA62192587B12737CCE9BBBB272FB75C4B309BA173AF2AA7684E2A7085`.

The Windows x64 Development build succeeded with zero warnings/errors and
reported `153609140` bytes.  Its ordinary `-tlaw-bootstrap-smoke` player
exited `0`, emitted `TLAW073_TRANSPORT_INERT`, and emitted no TLAW-074,
TLAW-075, TLAW-076, or TLAW-078 marker.

## Preserved identities

| Item | Verified identity |
| --- | --- |
| Unity | `6000.3.21f1 (c02631ffc030)` |
| Unity plugin inventory | exactly three DLLs |
| PortableAuthority plugin | `BD1E5DDA62192587B12737CCE9BBBB272FB75C4B309BA173AF2AA7684E2A7085` |
| C1 artifact | 2326 bytes, `94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541` |
| C1 canonical projection | `4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1` |

## Explicitly not changed

PortableAuthority gameplay semantics; `HostSession`; cadence and tick
execution; C1/YAML/configuration material; D-017/vendor/packages and the
three-plugin inventory; TLAW-074 transport Start/Stop ownership; actor
binding/allocation; FishNet gameplay ingress, RPC, and Broadcast; authoritative
receive tick; `ServerReceiveSequence`; dedupe/result cache;
`AuthoritativeAcceptedIntent`; accepted batches; HostSession submission;
replication, snapshots, resync, reconnect, or prediction; scenes/prefabs;
controls, gameplay, presentation, and D-016.  No review, Ready, merge,
cleanup, or next Gate-3 increment was performed.
