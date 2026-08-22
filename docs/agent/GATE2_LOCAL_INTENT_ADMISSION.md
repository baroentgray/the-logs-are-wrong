# Gate 2 local authoritative gameplay-intent admission — TLAW-072

## Scope and authority boundary

TLAW-072 adds one plain-C# `Gate2LocalIntentAdmissionAdapter` to the existing Gate-2 production owner. Its public ingress is `SubmitLocalIntent(IntentEnvelope envelope, ActorId authoritativeActor)`. The caller cannot provide receive tick, receive sequence, session metadata, or transport metadata.

The adapter owns only local pre-Stage-Two evidence for one live owner:

```text
exact IntentEnvelope reference + separately trusted local ActorId
-> exact open ServerTick + zero-based contiguous ServerReceiveSequence
-> AuthoritativeAcceptedIntent
-> AcceptedIntentTickBatchFactory.Create
-> IAlreadyAdmittedHostInputSource
-> HostSession.ExecuteTick Stage Two through Stage Seven
```

`IntentEnvelope.ActorIdHint` remains untrusted metadata. The adapter retains the submitted envelope and its parameter object exactly; it does not rewrite either. It does not validate gameplay action, target, state version, parameter semantics, active tools, or any Stage-Two rule.

## Open-tick lifecycle

- A fresh adapter starts at `ServerTick.Zero` with receive sequence zero.
- Same-open-tick duplicate `IntentId` is rejected locally and does not consume a receive sequence.
- Null envelope, shift mismatch, unbound authoritative actor, disposed adapter, and sequence exhaustion are local ingress outcomes only; they do not manufacture domain rejection events.
- `GetInput` permits only the matching adapter shift and exact open tick. It invokes the existing `AcceptedIntentTickBatchFactory` once, returns empty active-tool evidence, then opens exactly the next tick and resets the receive sequence to zero.
- A reset, fault, or disposal removes the old adapter. A replacement session creates a fresh adapter at tick zero; no pending evidence crosses the lifecycle boundary.
- A long cadence backlog asks the adapter once for every due tick. After the open tick is materialized, later catch-up ticks are empty unless separately admitted; queued input is never copied into them.

## Preserved production driver ordering

The existing production driver remains the sole owner and its cadence loop remains fixed:

```text
HostTickCadence.Accumulate
-> IAlreadyAdmittedHostInputSource.GetInput
-> HostSession.ExecuteTick
-> HostTickCadence.RetireNextDueTick
```

The runtime owner constructs the local adapter only after C1 materialization, explicit profile selection, cadence construction, and the one `HostSession` construction have succeeded. Existing test-only input sources remain test seams; production runtime uses the adapter.

## Preserved C1 and plugin identities

No C1 artifact, manifest, codec, YAML behavior, or PortableAuthority source changes are part of this increment:

- C1 bytes: `2326`.
- C1 SHA-256: `94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541`.
- Canonical configuration projection SHA-256: `4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1`.
- PortableAuthority plugin SHA-256: `BD1E5DDA62192587B12737CCE9BBBB272FB75C4B309BA173AF2AA7684E2A7085`.

## Explicit non-goals

No PortableAuthority accepted-batch, HostSession, cadence, HostTickExecutionService, C1/YAML, decision, package/project-setting, prefab, scene, gameplay controls, active-tool mechanics, UI/audio, D-016, network transport/RPC, connection binding, Gate-3 policy, replication, prediction, or second runtime-owner path is added or changed.
