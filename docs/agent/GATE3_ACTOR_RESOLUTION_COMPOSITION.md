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

## Explicitly not changed

TLAW-074 transport start/stop ownership; TLAW-075 binding registry, binding
policy, and lifecycle; TLAW-076 receive-time mapping; D-023/TLAW-078 codec;
PortableAuthority; C1/YAML/configuration; D-017 package/vendor identities;
actor allocation/selection/roster/lobby; `ServerReceiveSequence`; network
ordering/admission; dedupe/retransmission/idempotency; accepted intent/batch
construction; Stage Two; HostSession; client-visible response protocol;
replication, snapshot/gap/resync/reconnect/late join/prediction; host-loss
gameplay; D-016; gameplay, UI, audio; and any next Gate-3 increment.
