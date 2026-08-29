# Gate 3 network admission buffer — TLAW-082

## Scope

TLAW-082 implements the frozen D-024 plain-C# owner at exactly this bounded
server-local boundary:

```text
successful TLAW-080 resolved evidence
  -> current-shift membership
  -> session/shift-lifetime IntentId ledger
  -> per-exact-receive-tick ServerReceiveSequence
  -> existing AuthoritativeAcceptedIntent
  -> pending exact-tick bucket
  -> exact-tick seal/materialize
  -> existing AcceptedIntentTickBatchFactory
```

The owner is
`unity/TheLogsAreWrong/Assets/Gate3/Admission/Gate3NetworkIntentAdmissionBuffer.cs`.
It is one lifecycle-bound, non-static plain-C# object for one authoritative
shift. It consumes only `Gate3ResolvedNetworkIntentEvidence`; it does not
re-read transport identity, server time, wire bytes, or actor binding.

## Frozen behavior retained

- A foreign-shift envelope returns `ShiftMismatch` before it enters the
  current-session `IntentId` ledger or consumes a sequence.
- A current-shift `IntentId` is first-seen only once for the lifetime of the
  owner. Duplicates across connections and receive ticks return
  `DuplicateIntentId`, construct no second receipt, and consume no sequence.
- Every receive-tick bucket starts at `ServerReceiveSequence.Zero`; the one
  serialized admission-call order is retained directly, with no sort by any
  client, connection, actor, or gameplay field.
- The source receive tick remains the receipt's `ReceivedAtTick`. Future
  buckets coexist with older unmaterialized buckets; materializing one exact
  tick cannot roll later evidence forward.
- Materialization seals an exact `(shift, receive tick)` before it returns the
  existing factory batch. A first-seen late intent returns `ReceiveTickClosed`;
  its later retransmission remains terminally remembered and cannot re-enter a
  later tick.
- The last representable sequence is admitted. Its checked successor failure
  marks only that tick exhausted; a later first-seen intent for that tick
  returns `ReceiveSequenceExhausted` without a gap or reuse, and its later
  retransmission cannot resurrect on another tick.
- Structurally valid gameplay-invalid envelopes are retained unchanged. Stage
  2 remains the only gameplay validator.

The exhaustion test uses reflection only to place the one existing pending
bucket's `ServerReceiveSequence` at `long.MaxValue`; it introduces no second
counter, sequence type, or production test seam.

## Explicit exclusions

TLAW-082 does not wire TLAW-080 into the buffer or the buffer into
`HostSession`; it does not change TLAW-075/076/078/079/080, TLAW-072,
PortableAuthority, D-017/D-023/D-024, FishNet carrier behavior, transport
lifecycle, response/ack/rejection ABI, retransmission transport, replication,
snapshot/resync/reconnect, prediction, or gameplay semantics.

## Executable contracts

`Tlaw082NetworkAdmissionBufferTests` is the pinned-Unity focused class. Its
ten contracts cover Zero/contiguous reset behavior, cross-connection and
cross-tick dedupe, resolved-actor/envelope preservation, shift isolation,
exact sealing/no roll-forward, future backlog buckets, checked exhaustion,
factory materialization, no gameplay prevalidation, and deterministic
disposal. The initial pinned Unity `6000.3.21f1 (c02631ffc030)` focused run
passed `10/10` after the source was implemented.

`Tlaw082NetworkAdmissionBufferArchitectureTests` is the .NET repository guard.
It requires the one plain-C# owner and its use of the existing accepted-batch
factory while rejecting Unity/FishNet, codec, carrier, actor-resolution,
HostSession, Stage-2, response, replication, snapshot, prediction, and
transport-lifecycle widening. It also verifies the predecessor files remain
unwired to this owner.
