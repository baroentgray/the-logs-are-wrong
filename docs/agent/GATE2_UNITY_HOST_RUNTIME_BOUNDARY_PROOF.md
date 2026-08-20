# Gate 2 — Unity host runtime boundary proof (TLAW-065)

This is an **architecture proof only**. It determines and measures the narrowest Unity-side
host runtime/session boundary that can own authoritative runtime state and invoke the one
shared `HostTickExecutionService`. It does **not** implement or authorize a production host
loop.

## Execution identity

| Item | Value |
| --- | --- |
| Issue | [#151](https://github.com/baroentgray/the-logs-are-wrong/issues/151) |
| Baseline / `origin/main` | `33728019add3bcc6e7e7cebe4ef46a09be938db9` |
| TLAW-064 merge ancestor | `de3bfc03bee95334f5c14afd9d04b3a6703b9f47` |
| Branch | `task/TLAW-065-unity-host-runtime-boundary-proof` |
| Worktree | `C:\Projects\TheLogsAreWrong-worktrees\TLAW-065` |
| Unity | `6000.3.21f1`, changeset `c02631ffc030` |
| .NET SDK | `10.0.103` |

## Phase 0 — fail-closed inventory

All ten conditions passed before any proof work.

1. `origin/main` resolved to exactly `33728019add3bcc6e7e7cebe4ef46a09be938db9`; the worktree
   was clean and on the required branch. Drift from the TLAW-064 merge ancestor over 13
   commits was confined to `prototype/art_pipeline/**` and exactly three added `.gitignore`
   lines. No `src/**`, Gate-2 authority/bootstrap, project/package or production architecture
   drift occurred.
2. TLAW-064 H2 ownership intact: `src/TheLogsAreWrong.PortableAuthority` holds 56 `.cs` files
   (54 logical authority files plus two `Support/` files); `src/TheLogsAreWrong.Domain` holds 6.
3. Exactly one `HostTickExecutionService` declaration exists across `src/**` and
   `unity/TheLogsAreWrong/Assets/**`.
4. The frozen seven-stage order was read directly from source and is unchanged.
5. The Unity boundary is exactly three plugin DLLs with the accepted hashes:

   | Plugin | SHA-256 |
   | --- | --- |
   | `TheLogsAreWrong.PortableAuthority.dll` | `F51EA9509EC280F3B9C930B58419144A517477A0E26D9844905E5B3175B487CB` |
   | `System.Collections.Immutable.dll` | `5B1B1C83BA3D135C2FDFE425842FBE9C7432878B7E468623ACB554C69B4C130F` |
   | `System.Runtime.CompilerServices.Unsafe.dll` | `01748200F2400C742AA689F1F5101BD6298EFDFD92C00C18F4FA473847235BA9` |

6. No `TheLogsAreWrong.Domain.dll`, FishNet, FishySteamworks, Steamworks, transport or
   networking dependency exists in Gate 2. The only textual matches are the existing
   forbidden-list assertions that prove absence.
7. / 8. **Every type required to carry state across two consecutive `Execute` calls is
   PortableAuthority-owned.** All 22 boundary types were classified; zero are outer-Domain-owned:

   `ShiftRuntimeState`, `QuotaRuntimeState`, `MovementNoiseRuntimeState`,
   `LineNoiseRuntimeState`, `HostTickProgressionEvidence`, `ShiftLifecycleRuntimeState`,
   `AcceptedIntentTickBatch`, `AcceptedIntentTickBatchFactory`, `AuthoritativeAcceptedIntent`,
   `ItemId`, `IEventJournal`, `InMemoryEventJournal`, `EventId`, `ServerTick`,
   `SchedulerConfiguration`, `ShiftConfiguration`, `ContainmentConfiguration`, `AnomalyCatalog`,
   `HostStageSevenEventExecution`, `HostStageSevenPublished`, `HostStageSevenBlocked`,
   `HostTickCheckpointResult`.

   The single ingestion gap is recorded in **U4** below: the *type* `ShiftConfiguration` is
   PortableAuthority-owned and constructible in Unity, but the YAML loader that produces it in
   production (`src/TheLogsAreWrong.Config.Yaml/YamlConfigurationLoader.cs`) is outside the
   three-plugin boundary. This is not a blocker for the boundary itself and did not require
   importing Domain or widening packages.
9. Gate-2 production sources have not acquired simulation ownership. `Gate2BootstrapRoot`
   contains zero simulation types; `Gate2/Authority` still holds only the existing
   PortableAuthority smoke boundary, not a host session or driver.
10. No semantic, project, package or networking drift was found.

## Current shared host-tick input/output/state-continuity graph

### Input envelope (15 parameters, all PortableAuthority-owned)

| Class | Members |
| --- | --- |
| Authoritative state carried between ticks | `ShiftRuntimeState`, `QuotaRuntimeState`, `MovementNoiseRuntimeState`, `LineNoiseRuntimeState`, `HostTickProgressionEvidence`, `IEventJournal` |
| Session-stable inputs | `ShiftLifecycleRuntimeState`, `ShiftConfiguration`, `SchedulerConfiguration`, `ContainmentConfiguration`, `AnomalyCatalog` |
| Per-tick admitted inputs | `AcceptedIntentTickBatch`, `ImmutableHashSet<ItemId>` active tools |
| Per-tick identities/time | `ImmutableArray<EventId>`, `ServerTick` |

### Output and continuity

`Execute` returns `HostStageSevenEventExecution` with four concrete results:
`HostStageSevenPublished`, `HostStageSevenNoNewPublication`, `HostStageSevenAlreadyPublished`
and `HostStageSevenBlocked`. All continuity values hang off the base type, so a runtime can
carry state without inspecting the subtype:

```text
FinalShiftState        <- StageSix.FinalShiftState
FinalQuotaState        <- StageSix.FinalQuotaState
FinalLineNoise         <- StageSix.FinalLineNoise
FinalMovementNoise     <- StageSix.FinalMovementNoise
Checkpoint             <- StageSix.Checkpoint
Progression (next tick)<- ((HostTickCheckpointAdvanced)Checkpoint).Progression
Journal                <- the same IEventJournal instance, appended in place
```

**`HostTickProgressionEvidence.Advance` is `internal`.** A Unity-side runtime therefore
*cannot* advance progression itself; it must take the advanced instance the checkpoint
returned. The boundary structurally enforces "transport, do not reinterpret", which is exactly
the D-019 property this proof needed to establish.

## Candidates

### H1 — Plain C# host session + thin Unity scheduler — **PROVEN VIABLE, smallest**

A Unity-independent session object owns the carried state above and exposes one explicit
`ExecuteTick(...)` boundary. A separate scheduler decides only *when* a tick is due.

- **Ownership/lifetime:** plain C# object, created explicitly, reset by constructing a new
  instance, disposed explicitly. No Unity type is involved, so domain reload, scene load and
  Play-mode transitions cannot silently mutate or drop authoritative state.
- **Scheduling:** the scheduler converts elapsed authoritative time into a due-tick count and
  calls the session that many times. The session performs the semantic tick; the scheduler
  never touches the shared service.
- **Dependencies:** PortableAuthority only. No `UnityEngine` reference is required by the
  session, which is why the identical harness runs unchanged on net10 and in Unity.
- **Reentrancy/duplicate/reset:** proven below.

### H2 — MonoBehaviour owns authoritative session state directly — viable but strictly wider

Not structurally invalid and it does not duplicate semantics, but it is rejected as the
recommendation because it couples authoritative lifetime to Unity object lifetime: a
`MonoBehaviour` is destroyed and recreated by scene loads, domain reload and Play-mode
transitions, and Unity serialization cannot represent the immutable PortableAuthority records,
so state loss would be silent rather than loud. It also cannot be executed outside Unity, which
would have made the cross-runtime hash equality below impossible to establish. H1 subsumes it:
a `MonoBehaviour` may *hold a reference to* an H1 session without owning the state.

### H3 — Static/singleton host runtime — viable but strictly wider

Also not semantically invalid. It trades one real problem (duplicate ownership, see **U3**)
for two others: static state survives Play-mode exit under domain-reload-disabled editor
settings, producing cross-session bleed, and it makes deterministic test isolation harder — the
duplicate-session and reset proofs below would not be expressible. Single-ownership can instead
be enforced *around* an H1 session by an explicit owner, without making the session global.

### H4 — Frame-driven semantic recreation — **REJECTED**

Reproducing or partially inlining host-stage decisions in Unity would create a second semantic
authority, violating D-019 and D-020. The measurement confirms this is unnecessary, not merely
forbidden: the entire envelope is reachable from the three-plugin boundary, so there is no
technical pressure to recreate anything. No H4 probe was written.

### No additional candidate

No materially distinct, narrower family was found. H1 is already "own the carried values and
call the one service once".

## Executable proof

Two harnesses run the **same** logic against the **same** shared authority: a non-committed
net10 scratch console harness referencing `TheLogsAreWrong.PortableAuthority` directly, and a
bounded Gate-2 EditMode test file exercising the imported three-plugin boundary. Both are
test-only and neither is a second semantic implementation.

Discovered publication-plan sizes for the four-tick sequence: `[1, 3, 1, 0]`.

| # | Requirement | Result |
| --- | --- | --- |
| 1 | 2+ consecutive ticks over real shared service | PASS — 4 ticks executed |
| 2 | State continuity without a Unity-side semantic copy | PASS — session stores only returned references; `Advance` is `internal` so no recomputation is possible |
| 3 | Zero/one/multiple due ticks independent of frame count | PASS — see cadence finding below |
| 4 | Repeat determinism for identical sequences | PASS — identical projection across repeated runs |
| 5 | Exactly one semantic invocation per authoritative tick | PASS — 4 ticks, 4 invocations |
| 6 | Reentrancy / duplicate owner behavior | PASS (measured, see **U3**) |
| 7 | TLAW-064 canonical one-tick SHA retained | PASS — `287BD370…C25949` |

Multi-tick sequence projection SHA-256:
`C7FEC7BD00DE7D5A92DA0A89A09F61D4B7E4DC905A4F7D35687A8E6460029411`, produced **identically by
net10 and by pinned Unity/Mono**. Cross-runtime equality of a four-tick carried-state sequence
is the strongest available evidence that the session boundary transports rather than
reinterprets.

Observed authoritative progression across the sequence (long-horizon probe configuration):

```text
tick 0  HostStageSevenPublished          state_version=1  journal=1  QUIET
tick 1  HostStageSevenPublished          state_version=3  journal=4  LOUD
tick 2  HostStageSevenPublished          state_version=3  journal=5  QUIET
tick 3  HostStageSevenNoNewPublication   state_version=3  journal=5  QUIET
```

A zero-event tick is a first-class authoritative result, not an error, so the runtime boundary
must accept `HostStageSevenNoNewPublication` without treating it as a failure.

### Cadence finding

A naive `double` accumulator is **not** frame-rate independent. Two seconds of authoritative
time delivered as 120 frames retired 2 ticks, while the same two seconds delivered as 20 frames
retired only 1 — floating-point residue silently swallowed a tick:

```text
naive_double_accumulator  120f=2  20f=1  equal=False
exact_integer_clock       120f=2  20f=2  equal=True
```

An exact integer authoritative clock (elapsed integer time divided by tick period, emitting the
delta) is frame-count independent. A single frame correctly yielded `[0,0,1,2,0]` due ticks.
**Any production host runtime must derive cadence from an exact integer clock; a float
accumulator is disqualified.**

### Reentrancy, duplicate owner, reset

```text
reentrancy                  TLAW065_REENTRANT_TICK_REJECTED
disposed session            TLAW065_DISPOSED_SESSION_REJECTED
duplicate sessions          both succeeded independently, 1 invocation each
duplicate owner prevented   false — no ownership guard exists at this boundary
```

A re-entrant tick and a tick on a reset/disposed session are both rejected by the session probe
itself. Two independent sessions over the same configuration both execute successfully: nothing
in the shared authority prevents a duplicate host session. That is recorded as **U3**, not
silently solved here.

If the shared service throws before a result is accepted, the session's carried state is left
untouched, because `Carry(...)` runs only after `Execute` returns. `ValidatePlan` runs before
any journal append, so a rejected tick also leaves the journal unmutated.

## Regressions

| Evidence | Result |
| --- | --- |
| Full Release build | 0 warnings, 0 errors |
| Full .NET tests | `1633 passed / 0 failed / 0 skipped` — unchanged |
| D-014 `Scope=TLAW-046` | `87 / 87` — unchanged |
| Pinned Unity EditMode | `20 / 20 passed, 0 failed, 0 skipped` (13 pre-existing + 7 new proof contracts) |
| Windows x64 Development player build | `BUILD_RESULT=Succeeded`, `0` errors, `0` warnings |
| Player bootstrap/PortableAuthority smoke | PASS — `CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F` |
| Packaged plugin hash | `F51EA950…B487CB`, equal to the committed plugin |
| Plugin inventory | exactly 3 DLLs, unchanged and uncommitted-to |

No production source, project, package, plugin, scene, prefab, `Packages/**` or
`ProjectSettings/**` file was modified.

## Unresolved policy questions requiring owner / Architecture Desk selection

**U1 — Event-identity supply policy (hard blocker for any production host loop).**
`HostStageSevenEventExecutor.ValidatePlan` requires `eventIds.Length` to equal the publication
plan size exactly, and throws otherwise. A caller cannot know that size before executing the
tick. This proof sidestepped it by discovering plan sizes with throwaway replayed sessions and
then supplying exact counts in the measured run, which keeps one invocation per tick — but that
is a proof device, not a production mechanism. A production runtime needs an accepted policy
(for example a deterministic identity generator passed into the service, a two-phase
plan/commit, or a published plan-size query). **None exists today; one must be selected.**

**U2 — Tick cadence and catch-up policy.** The repository defines no authoritative tick
frequency, no maximum catch-up per frame, and no behavior for a long stall. The proof therefore
measured cadence mechanics without freezing a value. Required decisions: tick period, whether a
frame may retire unbounded due ticks or a bounded number, and what happens to the remainder.

**U3 — Single-owner enforcement.** Nothing prevents a second host session from being created
and ticked; both duplicates ran successfully. Whether single ownership is enforced by an
explicit owner object, a registry, an assertion, or deliberately left to the caller is an owner
decision.

**U4 — Configuration ingestion into Unity.** `ShiftConfiguration` is PortableAuthority-owned and
constructible in Unity, but the production YAML loader is in `TheLogsAreWrong.Config.Yaml`,
outside the three-plugin boundary. A production host session needs an accepted configuration
source (in-code, a Unity asset, a serialized payload, or a separately reviewed boundary change).
This proof used in-code configuration exactly as the accepted TLAW-062/TLAW-064 EditMode
fixtures do, and did **not** import Domain or widen packages.

## Recommendation

**H1 is the smallest viable candidate** and is the only one proven executable in both runtimes.
H2 and H3 remain semantically valid but strictly wider and lifetime-coupled; H4 is rejected as a
D-019 violation.

Because U1–U4 remain open, this proof stops at the owner selection gate rather than choosing
silently inside implementation.

## Work not performed

No production Unity host driver or session implementation, no `MonoBehaviour` simulation loop,
no persistent Unity simulation state, no gameplay/input/interaction/presentation binding, no
D-016 mechanical execution, no FishNet/FishySteamworks/Steamworks/transport/networking/Gate 3
work, no `TheLogsAreWrong.Domain.dll` import, no fourth plugin, no namespace/API redesign, no
further Domain-to-PortableAuthority moves, no project/package architecture change, no
scene/prefab/`Packages/**`/`ProjectSettings/**` change, and no timing or catch-up policy
change. Ready, merge and cleanup were not requested or performed.

## Terminal

```text
UNITY_HOST_RUNTIME_BOUNDARY_PROOF_AWAITING_OWNER_SELECTION=H1-recommended-smallest;H2-H3-viable-but-wider;U1-event-identity-supply;U2-tick-cadence-and-catch-up;U3-single-owner-enforcement;U4-unity-configuration-ingestion
SINGLE_HOST_TICK_AUTHORITY_PRESERVED
PRODUCTION_HOST_LOOP_NOT_IMPLEMENTED
NETWORKING_NOT_STARTED
```

## Sources

[Issue #151](https://github.com/baroentgray/the-logs-are-wrong/issues/151),
[D-019](DECISIONS.md#d-019--owner-selects-extracted-portable-authoritative-core-for-domainunity),
[D-020](DECISIONS.md#d-020--owner-selects-h2-for-the-unity-host-tick-composition-boundary),
[TLAW-063 host-tick architecture proof](GATE2_UNITY_HOST_TICK_ARCHITECTURE_PROOF.md),
[TLAW-064 production host-tick migration](GATE2_PRODUCTION_HOST_TICK_PORTABLE_AUTHORITY_MIGRATION.md), and
[TLAW-062 production Unity portable authority import](GATE2_PRODUCTION_UNITY_PORTABLE_AUTHORITY_IMPORT.md).
