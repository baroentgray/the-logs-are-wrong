# TLAW-086 — D-026 client intent disposition/replay

Status: implementation evidence dossier for Issue #191 / D-026. This document records the bounded production boundary only; it does not authorize a client-world-state protocol, reconnect support, replication, prediction, or another Gate-3 increment.

## Single owner and lifecycle

`Gate3ClientIntentDispositionLedger` is the only D-026 plain-C# result owner. It is created by `Gate3ClientIntentDispositionComposition` from the same shift lifecycle as the existing D-025 `Gate3ProductionAdmissionComposition`, and is disposed before the D-025 owner is reset. It owns only result correlation, the origin lifetime token, retained `PENDING`/terminal result state, replay authorization, and its bounded capacity. It does not own a gameplay `IntentId` set, `ServerReceiveSequence`, tick sealing, `HostSession`, Stage 2 validation, or Domain events.

The connection bridge produces a fresh monotonic `Gate3ServerConnectionLifetime` for each live, server-observed connection lifetime. Disconnect or server teardown revokes the old token. The result carrier requires that the exact token still matches the current live connection before attempting delivery, so a reused transport id cannot gain access to an old record.

## Frozen V1 ABI

`Gate3ClientIntentResultV1Codec` is a separate plain-C# codec, independent of FishNet types. Its payload is at most 1024 bytes, all fixed-width integers are little-endian, and schema version is exactly `1`.

| Field | V1 representation |
| --- | --- |
| schema version | `u16`, exactly `1` |
| shift id | `u16` byte length + existing strict Gate-3 identifier UTF-8, 1..256 bytes, no BOM |
| intent id | `u16` byte length + existing strict Gate-3 identifier UTF-8, 1..256 bytes, no BOM |
| disposition | `u8`: `1=PENDING`, `2=APPLIED`, `3=REJECTED`; `0` is reserved/invalid |
| authoritative receive tick | non-negative `i64` from the existing TLAW-076 observation |
| state-version marker/value | `u8` exactly 0/1 and optional `i64` iff marker is 1 |
| rejection code | `u16` byte length + strict UTF-8; empty for pending/applied and 1..64 bytes for rejected |

The codec rejects unsupported/zero schema, malformed or truncated frames, invalid UTF-8 including a leading BOM, illegal identifier/rejection lengths, unknown tags, invalid numeric/marker values, inconsistent disposition shapes, payloads over the cap, and trailing bytes. It performs no normalization, trimming, case folding, replacement fallback, or manufactured valid result.

No V1 payload serializes `ServerReceiveSequence`, event sequence, connection id, actor id, snapshot data, or gameplay state.

## Reservation, admission, and terminal projection

After authenticated TLAW-079 has already produced an ordinary valid D-023 envelope, the production composition obtains the current TLAW-075 connection lifetime and reserves the result record *before* it permits actor resolution or D-025 admission. Capacity is exactly 4096 records for the shift; record 4097 receives the correlatable `REJECTED / RESULT_CAPACITY_EXHAUSTED` result and does not enter D-025.

With a live recipient, actor resolution maps `ActorNotBound` to `REJECTED / ACTOR_NOT_BOUND`. Valid resolved evidence goes unchanged to the existing D-025 owner. Expected D-025 terminal outcomes map to `SHIFT_MISMATCH`, `RECEIVE_TICK_CLOSED`, or `RECEIVE_SEQUENCE_EXHAUSTED`. D-025 admission success is retained and delivered only as `PENDING`.

The production driver emits its server-local D-026 observation only after the real `HostSession.ExecuteTick` returned successfully and before cadence retirement. The ledger reads the exact returned Stage-2 step, then maps known accepted steps to `APPLIED` and typed/duplicate/unsupported outcomes to the explicit stable rejection codes. An owner or HostSession fault has no ordinary client result path and leaves retained pending state uncommitted.

Stable code mapping is an explicit switch over every current `RejectionReason`; production never uses enum formatting, integer serialization, exception text, or an unmapped fallback as a protocol value.

## Replay, privacy, and delivery

Only the uninterrupted exact original live origin may replay its retained pending or terminal result. A different, local, disconnected, revoked, or otherwise unsafe origin learns only `REJECTED / INTENT_ID_ALREADY_USED`; it never sees another origin’s state version or outcome. Disconnect permanently revokes delivery/replay authorization while retaining the record’s gameplay-idempotency correlation until session reset.

The result carrier sends only one `IBroadcast` with `byte[] Payload`, uses `requireAuthenticated=true` and `Channel.Reliable`, and targets the current original connection only. It has no client receiver registration, ACK, timer, retransmission scheduler, standalone query, transport Start/Stop ownership, event replication, snapshot, reconnect, or prediction behavior.

Trusted local networked-production ingress remains off FishNet and does not allocate a fake network origin or D-026 record.

## Focused evidence

`Tlaw086ClientIntentDispositionTests` covers the V1 golden vector, all three canonical disposition round trips, strict malformed failure cases, explicit rejection-code coverage, reservation/replay privacy, exact 4096/4097 capacity behavior, disconnect/reused-connection isolation, and session reset/disposal isolation. `Tlaw086ClientIntentDispositionArchitectureTests` guards the code/scene composition and forbids D-026 authority expansion into D-024/D-025, HostSession, transport lifecycle, sequence, or replication.

The final candidate packet records actual pinned Unity, .NET, verifier, Gate0, and CI results bound to the exact candidate head.
