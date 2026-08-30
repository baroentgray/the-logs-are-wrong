# Gate 3 production admission composition — TLAW-084

## Scope and binding

TLAW-084 implements frozen D-025 at one bounded server-local production boundary.
The isolated branch began at `48e4ff07cb8eff82a56c8e4d79e1e6d883dd5116`
(`458f404d259cf182e244a417d8b0b5139618f77c`). Before the first tracked edit,
`origin/main`, the pre-created remote task branch, this worktree `HEAD`, and the
merge-base matched that baseline; the worktree was clean. The protected primary
worktree was only inspected and fetched, never switched, reset, cleaned, or edited.

The selected production path is exactly:

```text
trusted listen-host local envelope + trusted ActorId
  -> TLAW-076 session elapsed-time observation
  -> Gate3ProductionAdmissionComposition
                         \
TLAW-079 -> TLAW-080 resolved evidence -> one Gate3NetworkIntentAdmissionBuffer
                                             -> one final IAlreadyAdmittedHostInputSource
                                             -> Gate2ProductionHostDriver
                                             -> HostSession.ExecuteTick
```

`Gate3NetworkIntentAdmissionBuffer` remains the one D-024 receipt owner. It was
generalized only to accept source-neutral production evidence after either trusted
local normalization or successful network actor resolution. It still owns the sole
session/shift `IntentId` ledger, per-exact-receive-tick `ServerReceiveSequence`,
pending buckets, sealed ticks, checked exhaustion behavior, and the existing
`AcceptedIntentTickBatchFactory` materialization. No second counter, batch merge,
or post-hoc resequencing exists.

## Exact temporal rule

The one final input source implements an ingress-before-seal phase, not a second
admission authority. For a due host tick `T`, it materializes only when the same
TLAW-076 authoritative receive-time observation maps strictly later than `T`.
Thus the frozen mapping remains unchanged:

```text
0/1/999/1000 ms -> tick 0
1001/2000 ms    -> tick 1
2001 ms         -> tick 2
```

At exactly `1000 ms`, cadence may have made tick zero due, but the window remains
open and a later local or resolved-network callback in that frame still enters
tick zero. At `1001 ms`, tick zero seals once, materializes one batch, executes,
and only then the driver retires it. Backlog never rewrites received ticks and
never extends the frozen receive window.

## Lifecycle and production wiring

The scene-owned `Gate3ProductionAdmissionComposition` configures the existing
host driver before `Start`. The driver creates the shared owner only after C1
materialization and the fresh session time origin, receives exactly one final
`IAlreadyAdmittedHostInputSource`, and calls the optional temporal phase before
its existing `GetInput -> HostSession.ExecuteTick -> RetireNextDueTick` ordering.

On reset, fault, dispose, or replacement, the driver ends the composition session;
the input source and buffer are disposed, retained old input cannot materialize,
and the next HostSession creates a fresh ledger/bucket set. TLAW-080 only publishes
successful already-resolved evidence through its bounded event. It still performs
no sequencing, admission, gameplay validation, execution, or response handling.

The committed `Gate2Bootstrap` authoring adds only this composition and its exact
references to the existing host owner and actor-resolution component. No other
scene, prefab, package, project setting, plugin, C1 material, or transport
lifecycle ownership is changed.

## Executable contracts

Pinned Unity `6000.3.21f1 (c02631ffc030)` focused class
`Tlaw084ProductionAdmissionCompositionTests` passed **12/12** during implementation;
the full pinned EditMode suite passed **124/124** after the prescribed fresh
PortableAuthority deployment build. The existing TLAW-082 class remains **10/10**
within that full suite.
It proves:

- local→network and network→local use one zero-based contiguous sequence domain;
- mixed source bursts preserve serialized shared-owner order and exact resolved
  actor/receive tick;
- cross-source duplicates consume no new sequence;
- each next receive tick restarts at zero while session-lifetime duplicate
  disposition remains terminal;
- local networked ingress obtains TLAW-076/session time without FishNet;
- `1000 ms` remains ingress-open and `1001 ms` seals/executed tick zero exactly once;
- pump/ingress callback-order permutations at the inclusive boundary converge;
- backlog preserves future received ticks without extending their window;
- there is one final input source, no batch merge, and empty active tools;
- wrong-shift materialization fails closed without clearing valid evidence;
- reset/disposal rejects retained stale ingress and creates a fresh session owner.

The .NET architecture subset for TLAW-080, TLAW-082, and TLAW-084 passed **7/7**;
the full Release solution build completed with **0 warnings / 0 errors** and the
full .NET suite passed **1678/1678**. The fresh deployment output and the committed
PortableAuthority plugin both hash to
`BD1E5DDA62192587B12737CCE9BBBB272FB75C4B309BA173AF2AA7684E2A7085`.
Its guards require the one shared owner, exactly one temporal input source,
TLAW-080's success-only event, bootstrap scene wiring, and TLAW-072's unchanged
local/offline adapter boundary. Final full regression and exact-head CI evidence
is recorded only after the candidate commit.

## Explicit exclusions

This increment does not add FishNet gameplay ingress/RPC/Broadcast behavior,
actor allocation or roster policy, client results/acks/replay, retransmission,
replication, event-sequence networking, snapshots/resync/reconnect/late join,
prediction, host-loss gameplay, transport start/stop changes, controls, active
tools, gameplay semantics, Stage 2/HostSession changes, D-023 changes,
TLAW-076 remapping, TLAW-075 binding semantics, TLAW-079 carrier semantics,
TLAW-072 behavior changes, PortableAuthority/C1/YAML changes, or D-016/UI/audio.
