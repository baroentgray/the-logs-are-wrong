# Gate-3 authenticated intent carrier ingress — TLAW-079

## Authority and bounded scope

TLAW-079 implements Issue #179 only. `Gate3IntentCarrierIngress` is the one
production FishNet server registration for `Gate3IntentCarrierBroadcast`. The
broadcast has exactly one field: the unmodified `byte[]` D-023 payload. It is
registered with `requireAuthentication: true`, accepted only on
`Channel.Reliable`, and explicitly unregistered when the component is disabled
or destroyed. Registration and unregistration neither start nor stop any
transport.

For each authenticated server callback, the local processor obtains identity
only from the server-supplied `NetworkConnection.ClientId`, materializes the
existing `Gate3ServerConnectionId`, captures the existing TLAW-076
authoritative receive tick, and only then calls the existing
`Gate3IntentWireV1Codec.TryDecode(payload)`. A successful result is bounded
local evidence comprising that connection id, receive tick, and ordinary
PortableAuthority `IntentEnvelope`. It does not cross an admission, gameplay,
or response boundary.

The committed `Gate2Bootstrap` authoring entry point imports the ingress
script, adds exactly one ingress component to the existing bootstrap root, and
binds it to the already-authoritative `NetworkManager` and production host
owner. The committed scene was regenerated with pinned Unity; no transport
Start/Stop behavior was added.

## Executable contracts and results

The two .NET TLAW-079 architecture contracts passed **2/2**. They require the
single-field `IBroadcast`, explicit authenticated registration and
unregistration, reliable-only processing, use of `NetworkConnection.ClientId`,
TLAW-076 receive-tick capture before the D-023 decoder call, and ending at
`Gate3DecodedNetworkIntentEvidence`. They also prove the bootstrap authoring
wiring and reject actor/connection resolution, actor-binding outcomes,
server-receive sequence, accepted intent or batch, HostSession, transport
start/stop, replication, snapshot, resync, reconnect, and prediction coupling.

Pinned Unity `6000.3.21f1 (c02631ffc030)` ran
`Tlaw079IntentCarrierIngressTests`: **9/9 passed**. The class proves one
payload-only FishNet broadcast; a reliable NONE payload preserving the exact
connection id, receive tick, and decoded envelope; PROCEDURE_ACTION payload
preservation; rejection of unreliable traffic and invalid server client ids
before tick capture/decode; receive-tick failure before malformed-payload
decode; D-023 malformed and oversized local failure with no evidence; and
retention of actor hint solely as untrusted client evidence. The full pinned
Unity EditMode suite passed **94/94**.

The Release solution build completed with zero warnings and zero errors; the
full .NET suite passed **1671/1671**. Preserved focused architecture/test
slices passed: D-014/TLAW-046 **87/87**; TLAW-067 **6/6**; TLAW-068 **10/10**;
TLAW-070 **5/5**; TLAW-071 **2/2**; TLAW-072 **2/2**; TLAW-073 **2/2**;
TLAW-074 **2/2**; TLAW-075 **2/2**; TLAW-076 **2/2**; TLAW-078 **2/2**; and
TLAW-079 **2/2**. The C1 export freshness check passed. The deterministic
PortableAuthority Release rebuild completed with zero warnings/errors and
remained byte-identical to the committed plugin.

The Windows x64 Development build completed with `BUILD_RESULT=Succeeded`,
zero errors, zero warnings, and `153615840` reported bytes. Its ordinary
`-tlaw-bootstrap-smoke` player exited `0`, emitted the existing
`TLAW073_TRANSPORT_INERT` marker, and emitted no TLAW-079 marker or transport
lifecycle transition.

## Preserved deployment identities

| Item | Verified identity |
| --- | --- |
| Unity | `6000.3.21f1 (c02631ffc030)` |
| Unity plugin inventory | exactly three DLLs |
| PortableAuthority plugin | `BD1E5DDA62192587B12737CCE9BBBB272FB75C4B309BA173AF2AA7684E2A7085` |
| C1 artifact | 2326 bytes, `94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541` |
| C1 canonical projection | `4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1` |

## Explicitly not changed

PortableAuthority and D-023 semantics; C1/YAML/configuration; D-017/vendor
packages and the three-plugin inventory; TLAW-074 transport lifecycle
ownership; actor allocation, resolution, binding, roster, or lobby policy;
`ACTOR_NOT_BOUND`; `ServerReceiveSequence`; ordering, network admission,
dedupe, retransmission, result cache, or idempotency; accepted intents or
batches; Stage Two; HostSession submission or execution; client-visible
acknowledgement, rejection, or result protocol; replication, snapshots, gap
handling, resync, reconnect, late join, prediction, reconciliation, or
host-loss gameplay; D-016/gameplay/UI/audio; and any next Gate-3 increment.
