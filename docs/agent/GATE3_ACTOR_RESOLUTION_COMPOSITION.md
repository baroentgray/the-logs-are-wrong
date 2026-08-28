# Gate-3 authoritative actor-resolution composition — TLAW-080

## Authority and bounded scope

TLAW-080 implements GitHub Issue #181 only. It composes the successful
TLAW-079 `Gate3DecodedNetworkIntentEvidence` with the existing TLAW-075
server-owned `ResolveAuthoritativeActor(connection_id, actor_id_hint)` seam,
then stops at server-local `Gate3ResolvedNetworkIntentEvidence`.

The resolved evidence preserves the exact TLAW-079 connection id,
authoritative receive tick, and materialized `IntentEnvelope`; it adds only
the separately authoritative `ActorId` returned from the existing TLAW-075
binding registry. `ActorIdHint` is passed unchanged as untrusted client
evidence. It is never compared, normalized, substituted, or elevated to
authority.

The single production `Gate3ActorResolutionComposition` subscribes only to the
successful decoded output exposed by `Gate3IntentCarrierIngress`; it delegates
only to the committed `Gate3ServerConnectionActorBindingBridge` resolver. The
carrier remains the owner of authenticated Reliable FishNet registration,
server-observed connection extraction, receive-tick capture, and D-023 decode.
The composition owns neither transport lifecycle nor connection binding.

## Fail-closed result boundary

The bounded result retains the existing TLAW-075 local statuses exactly:

- `InvalidConnection`;
- `ConnectionNotLive`;
- `ActorNotBound`;
- `Resolved`.

Only `Resolved` carries resolved evidence. All failures carry no such evidence
and manufacture neither a Domain event nor a client-visible response.

## Phase 0

Before tracked edits, `origin/main`, the task branch, and the task worktree
were verified against exact base
`dc9be24bd3ca7d7f45af758d26ce6b6821be45bb` (tree
`2d70afec817e9083b13ef1e2a4e2caa3348e9ad1`). The worktree was clean.
`Tlaw.Verify` with that exact base/head and `git diff --check` both passed.
The frozen pillars, scope, state-machine, network rules, D-021/D-023, Issue
#181, and owner handoff `5456804696` were read before implementation.

## Executable contracts

The focused pinned Unity suite is `Tlaw080ActorResolutionCompositionTests`. It
proves exact connection and untrusted-hint forwarding, exact receive-tick and
envelope preservation, registry authority over a different or absent hint, all
three existing failure outcomes without resolved evidence, and real production
carrier-success-event composition through the existing bridge.

The .NET `Scope=TLAW-080` architecture guards require one composition/source
of resolved evidence, no second registry/resolver or actor assignment, no
carrier decode/connection re-read/time re-observation, and no sequencing,
dedupe, accepted-intent, batch, stage-two, HostSession, response, replication,
snapshot, reconnect, prediction, or transport lifecycle code. They also guard
the bootstrap wiring and preserve the predecessor owners.

## Verification evidence

Pinned Unity `6000.3.21f1 (c02631ffc030)` passed the focused
`Tlaw080ActorResolutionCompositionTests` suite **7/7** and the full EditMode
suite **102/102**. The latter includes the existing production-plugin parity
contract after its prescribed deterministic deployment build
(`IncludeSourceRevisionInInformationalVersion=false`, `DebugSymbols=false`).

PortableAuthority Release and the full solution Release builds completed with
zero warnings and zero errors. The full .NET suite passed **1673/1673**; the
combined retained TLAW-046/067/068/070/071/072/073/074/075/076/078/079/080
architecture slices passed **119/119**. C1 freshness reported
`VALIDATED_CONFIG_C1_EXPORT_FRESH`, and exact-base/head `Tlaw.Verify` passed.

The Windows x64 Development build reported `BUILD_RESULT=Succeeded`,
`BUILD_ERRORS=0 BUILD_WARNINGS=0`, and `BUILD_SIZE=153620539`. Its ordinary
`-tlaw-bootstrap-smoke` player exited `0`, emitted the existing
PortableAuthority, TLAW-071 owner, `TLAW073_TRANSPORT_INERT`, and bootstrap
quit markers, and emitted no TLAW-074 through TLAW-080 marker.

The C1 deployment material remains 2326 decoded bytes with SHA-256
`94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541` and
canonical projection SHA-256
`4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1`.
The Unity plugin inventory remains exactly three DLLs; the PortableAuthority
plugin and fresh deployment build both hash to
`BD1E5DDA62192587B12737CCE9BBBB272FB75C4B309BA173AF2AA7684E2A7085`.
D-017 remains FishNet `4.7.2` / `de19b5d66459f60400ffd0edc443c4da173a01e7`
and Steamworks.NET `2025.164.1` /
`c21a8f0e31c56ae8707130967faf491f7dd7c0d8`.

## Explicitly not changed

TLAW-074 transport start/stop ownership; TLAW-075 binding registry, binding
policy, and lifecycle; TLAW-076 receive-time mapping; D-023/TLAW-078 codec;
PortableAuthority; C1/YAML/configuration; D-017 package/vendor identities;
actor allocation/selection/roster/lobby; `ServerReceiveSequence`; network
ordering/admission; dedupe/retransmission/idempotency; accepted intent/batch
construction; Stage Two; HostSession; client-visible response protocol;
replication, snapshot/gap/resync/reconnect/late join/prediction; host-loss
gameplay; D-016; gameplay, UI, audio; and any next Gate-3 increment.
