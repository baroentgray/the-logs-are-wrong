# Decision log

Append-only ADR-lite log. Corrections add a new entry that references the older one; they do not silently rewrite history.

## D-001 — Gate 0 keeps object-byte and fail-closed checks

Gate 0 compares canonical Git-object content and separately rejects committed, staged, unstaged, and untracked protected changes. No checkout-byte fallback or retry makes a partial read pass.

Sources: [BAR-30](https://linear.app/baronet/issue/BAR-30/tlaw-auto-005-deterministic-repository-verification-and-github-ci), [PR #5](https://github.com/baroentgray/the-logs-are-wrong/pull/5), `tools/Tlaw.Verify/README.md`.

## D-002 — `docs/agent/**` is repository-operational, outside frozen Gate 0

The approved knowledge-pack namespace is exactly `docs/agent/**`. It is excluded from broad Gate change-set discovery only; sibling `docs/**` paths and all frozen object checks remain protected.

Source: [BAR-31 execution packet v3](https://linear.app/baronet/document/bar-31-codex-execution-packet-v3-approved-docsagent-gate-boundary-34f77d112606).

## D-003 — Event sequence and initialized-zero values have distinct semantics

`EventSequence` zero is `None`; `StateVersion` and `ServerTick` zero are valid initialized values. Do not conflate their defaults.

Sources: [Issue #2](https://github.com/baroentgray/the-logs-are-wrong/issues/2), [PR #4](https://github.com/baroentgray/the-logs-are-wrong/pull/4).

## D-004 — Human approval remains the merge default

The user performs normal merges while present. Automation may prepare evidence and a Draft PR but does not silently merge or launch another agent.

Source: `AGENTS.md` and [BAR-31](https://linear.app/baronet/issue/BAR-31/tlaw-auto-006-agent-knowledge-pack-and-context-manifest).

## D-005 — Local routing uses availability-first deterministic selection

BAR-34 selects only from a supplied local snapshot. `AVAILABLE` ranks above `DEGRADED`; within one rank, a selectable preferred agent wins, then declared eligible-agent order breaks ties. `QUOTA_EXHAUSTED`, `OFFLINE`, and `UNKNOWN` are not selectable. Explicit overrides remain constrained by eligibility, capability, autonomy, and availability; selection is not a lease or dispatch.

Source: [BAR-34](https://linear.app/baronet/issue/BAR-34/bar-26-increment-3-deterministic-availability-and-executor-selection).

## D-006 — Result ingestion is evidence correlation, not workflow finalization

BAR-35 accepts a result/v1 packet only when its task identity and a fully claimed task/v2 packet match the exact currently active local lease, including agent and fencing token. It records concise projected evidence but does not release the lease, select fallback, transition Linear, launch an agent, write GitHub, merge, or complete BAR-26.

Source: [BAR-35](https://linear.app/baronet/issue/BAR-35/bar-26-increment-4-correlated-result-ingestion-and-human-pause).

## D-007 — Chat and model memory are not project authority

Chat history and model memory are lower than repository evidence and cannot
establish a baseline, task status, reviewer policy, or mutation permission.

Source: [BAR-55](https://linear.app/baronet/issue/BAR-55) and [Issue #47](https://github.com/baroentgray/the-logs-are-wrong/issues/47).

## D-008 — CURRENT_STATE is the single volatile current-state cache

`CURRENT_STATE.md` replaces `STATUS.md`. It is non-authoritative and volatile;
there must not be two competing current-state caches.

Source: [Issue #47](https://github.com/baroentgray/the-logs-are-wrong/issues/47).

## D-009 — ACTIVE_RUNS and HANDOFF are generated projections

`ACTIVE_RUNS.md` records only unfinished operations and `HANDOFF.md` supports a
new control chat. Both are generated, non-authoritative projections that require
live validation before any write.

Source: [Issue #47](https://github.com/baroentgray/the-logs-are-wrong/issues/47).

## D-010 — Operational snapshots refresh only at safe workflow checkpoints

The allowed refresh triggers are `TASK_CREATED`,
`IMPLEMENTATION_CANDIDATE_READY`, `AUTHORITATIVE_REVIEW_COMPLETE`,
`CORRECTION_CANDIDATE_READY`, `MERGED_AND_VERIFIED`, and
`CHAT_HANDOFF_CREATED`. The triggers document checkpoints; they do not launch
agents or write GitHub or Linear automatically.

Source: [Issue #47](https://github.com/baroentgray/the-logs-are-wrong/issues/47).

## D-011 — Grok is the sole authoritative reviewer under current policy

From TLAW-013 until an explicit user policy change, Grok is the sole
authoritative reviewer. There is no dual authoritative review and no automatic
fallback to Claude; implementation and merge roles remain constrained by their
exact handoffs.

Source: [Issue #47](https://github.com/baroentgray/the-logs-are-wrong/issues/47).

## D-012 — One append-only log records accepted, rejected, and superseded decisions

`DECISIONS.md` remains the single append-only log. Accepted decisions are added
here; rejected and superseded options are represented by new references rather
than rewriting earlier entries. At most one bounded correction round is allowed.

Source: [Issue #47](https://github.com/baroentgray/the-logs-are-wrong/issues/47).

## D-013 — Saw completion applies quota through separate immutable state

For the bounded TLAW-023 composition boundary, `ShiftRuntimeState` and
`QuotaRuntimeState` remain separate immutable states. The host-owned boundary
accepts completed saw evidence and applies its resolved settlement to quota; this
does not pre-approve a generic host aggregate or dispatcher.

Sources: [Issue #64](https://github.com/baroentgray/the-logs-are-wrong/issues/64) and [BAR-63](https://linear.app/baronet/issue/BAR-63/tlaw-023-apply-completed-saw-settlement-to-quota-runtime).

## D-014 — Gate 1 implements the frozen full ShiftSnapshot/replay contract

The owner accepted TLAW-043 option B for the snapshot/replay blocker: the already
frozen Gate-1 requirement is implemented rather than deferred. The accepted scope
is exactly the `docs/LOG_STATE_MACHINE.md` contract — a full `ShiftSnapshot`
carrying `shift_id`, `server_tick`, `state_version`, `last_event_sequence`,
`scheduler_state`, `logs[]`, `line_state`, `containment_state`, `inventory`,
`quota` and `objectives`; snapshot capture; restore from a snapshot plus the
events after `last_event_sequence`; and full replay from the initial manifest
producing the same final snapshot.

This remains pure Gate-1 Domain work and introduces no Unity, network or package
dependency. Gate 3 may later consume the same contract for snapshot/resync under
`docs/NETWORK_RULES.md`, but that future use does not defer the Gate-1
implementation. It is a separate bounded increment and is not combined with
D-015.

Sources: [Issue #109 comment 5238424632](https://github.com/baroentgray/the-logs-are-wrong/issues/109#issuecomment-5238424632), `docs/LOG_STATE_MACHINE.md`, `docs/agent/GATE1_EXIT_AUDIT.md`, and [Issue #107](https://github.com/baroentgray/the-logs-are-wrong/issues/107).

## D-015 — Incorrect Penitent processing opens an 8-second saw-only failure window

The owner accepted TLAW-043 option B for `time_penalty` and froze its exact
deterministic mechanics. The trigger stays the frozen incorrect-saw path: a
`PENITENT_TRUNK` completes the saw path without `SANITIZED_PENITENT`.

Accepted semantics:

- the incorrect saw cycle completes and retains the already-frozen rejected,
  no-credit outcome;
- `FALSE_PA_ANNOUNCEMENT` remains the causal domain event identifier, and Gate 1
  requires no audio playback;
- an 8-second saw-only failure window begins at that incorrect completion;
- while the window is active the saw is unavailable and no new saw cycle may
  start;
- intake, feed progression and other line-admission work continue under their
  existing contracts;
- intake deadlines continue;
- the hard shift clock and tick progression continue, and the hard deadline is
  neither moved nor extended;
- existing non-saw player work remains governed by its ordinary contracts;
- no manual repair action and no `LINE_JAMMED`/`REPAIRING` substitution is
  introduced for this effect;
- after exactly 8 seconds the saw automatically becomes available again, and
  ordinary automatic saw-start behaviour may resume once its normal
  preconditions are met.

This decision authorizes no audio, UI, scoring, actor-scoped penalty, whole-line
pause, deadline modification or manual repair, and it does not authorize
contextual or differentiated repair mechanics; those remain separate design
backlog work. It replaces the earlier ambiguity, recorded as blocked by the
TLAW-043 audit, that `time_penalty` might stop the whole line or modify the
shift clock. It is a separate bounded increment and is not combined with D-014.

Sources: [Issue #109 comment 5238424632](https://github.com/baroentgray/the-logs-are-wrong/issues/109#issuecomment-5238424632), `docs/FIRST_SHIFT_SPEC.md`, `docs/ANOMALY_MATRIX.md`, `data/anomalies.prototype.yaml`, `docs/agent/GATE1_EXIT_AUDIT.md`, and [Issue #107](https://github.com/baroentgray/the-logs-are-wrong/issues/107).

## D-016 — Resin nearest-line-button lock execution is deferred to Gate 2

The owner accepted TLAW-043 option A for `lock` / `nearest_line_button`:
Gate 1 keeps descriptor-only treatment and mechanical execution is deferred to
the Gate 2 control-surface and spatial representation.

The accepted fiction is unchanged: unsealed Resin reaching the saw, or holy
water applied to Resin before the saw, activates anomalous resin that blocks the
nearest physical line-control button for ten seconds. The retained effect stays
exactly `lock` / `RESIN_BUTTON_LOCK` / target `nearest_line_button` / duration
10 seconds.

For Gate 1 this means the descriptor and the existing wrong-action semantics are
retained as they are — the holy water is still consumed and the object remains
processable — and Gate 1 must not invent an abstract substitute target and must
not add a lock executor. Gate 1 has no button, control-surface or actor-position
model, so `nearest_line_button` has no Gate-1 referent. Gate 2 resolves physical
nearest-button selection and presentation against the actual control surfaces.

Sources: [Issue #109 comment 5238424632](https://github.com/baroentgray/the-logs-are-wrong/issues/109#issuecomment-5238424632), `docs/PROTOTYPE_SCOPE.md`, `docs/FIRST_SHIFT_SPEC.md`, `docs/ANOMALY_MATRIX.md`, `data/anomalies.prototype.yaml`, and `docs/agent/GATE1_EXIT_AUDIT.md`.

## D-017 — Exact Unity/network package pins are accepted

The owner accepted the exact matrix that TLAW-049 proved, after that task closed
`PACKAGE_MATRIX_SMOKE_PASS` and was merged and exact-main verified. The accepted
pins are exactly:

- Unity Editor `6000.3.21f1`, changeset `c02631ffc030`;
- FishNet `4.7.2`, upstream tag resolving to commit
  `de19b5d66459f60400ffd0edc443c4da173a01e7`;
- Steamworks.NET `2025.164.1`, annotated tag object
  `d6930827976de076964a97f713fea0b557783a54` peeling to commit
  `c21a8f0e31c56ae8707130967faf491f7dd7c0d8`;
- FishySteamworks `4.1.1`, upstream tag resolving to commit
  `21e858249249e2c322365fe9fefbe865f290b0d9`, installed from the official release
  asset `FishySteamworks.4.1.1.unitypackage`, `17,188` bytes, independently
  computed SHA-256
  `5698D16BD29B8B08D35E12A9B817CE69992F70D7C14B64810961691ECD9AFC57`.

The smoke evidence is the prerequisite, not a formality: the exact matrix
imported and compiled with zero C# errors and zero warnings, the FishySteamworks
transport attached and was selected on a FishNet `NetworkManager`, Steamworks.NET
initialized under Steam App ID `480`, a same-process host started and stopped
cleanly, and a Windows x64 Development build loaded the native Steamworks plugin.

For the accepted Steam P2P path, FishySteamworks `_peerToPeer=true` is part of
this acceptance and must be carried explicitly into Gate-3 setup and handoff. The
shipped default `_peerToPeer=false` is **not** the accepted configuration: it
takes the IP listen-socket path, which failed server start during TLAW-049 with
no diagnostic output at all, because `ServerSocket.StartConnection` swallows its
exception in a bare `catch`. This is configuration, not a package patch; no
package source is forked or modified.

Accepting these pins does not start any gate. Gate 2 remains a single local Unity
process with one local authoritative host and **no FishNet, Steamworks or other
networking dependency**; `docs/PROTOTYPE_SCOPE.md` and frozen
`docs/NETWORK_RULES.md` keep networking out of Gate 1 and Gate 2. FishNet,
FishySteamworks and Steamworks.NET are for Gate 3+ networking work only.

Frozen Gate-0 files are deliberately left byte-for-byte unchanged, including the
two unchecked post-Gate-1 network rows in `GATE_0_EXIT_CHECKLIST.md` and the
`PROPOSED` stack wording in `docs/NETWORK_RULES.md`. Both are protected by exact
SHA-256 in `tools/Tlaw.Verify/Gate0/gate0-baseline.json`. Their text is historical
Gate-0 baseline, not a reversal of this later acceptance; the acceptance lives
append-only under `docs/agent/**` per D-002.

Any later change to an accepted version or resolved identity requires a new
explicit owner decision recorded as a new entry, not a silent rewrite of D-017
(D-012).

Sources: [Issue #121](https://github.com/baroentgray/the-logs-are-wrong/issues/121), [Issue #118](https://github.com/baroentgray/the-logs-are-wrong/issues/118), [PR #120](https://github.com/baroentgray/the-logs-are-wrong/pull/120), `docs/agent/PACKAGE_SMOKE_TEST.md`, and `docs/agent/PACKAGE_PIN_ACCEPTANCE.md`.

## D-018 — Owner rejects Domain↔Unity architecture candidates for now

After TLAW-056 Phase A, the owner deliberately decided **REJECT ALL FOR NOW**.
No Domain↔Unity production architecture is accepted.

- Candidate A, an additive portable target on the existing Domain, is not
  accepted now.
- Candidate B, an extracted portable authoritative core shared by net10 Domain
  and Unity, is not accepted now.
- Candidate C, direct source linking / Unity asmdef compilation, is not selected
  under the current evidence.
- Candidate D remains supplementary only; contracts or a facade are not an
  executable authority architecture.

This is a deliberate rejection for now, not a conclusion that A or B is
technically impossible. TLAW-056 Phase A showed that A and B remained blocked at
portable compilation, before Unity load, by the exposed `netstandard2.1`
framework API surface. It authorizes no production source, target, project, or
package migration.

The owner-approved next direction is a separately scoped, scratch-only
portability proof over the already-known 26-file authoritative cut. That later
proof may use semantic-equivalent compatibility replacements for the currently
exposed framework API blockers, then attempt this bounded sequence:

~~~text
portable compile
-> pinned Unity Editor load
-> EditMode authoritative execution
-> exact net10/Unity parity
~~~

It must stop at the first material blocker. This later proof does not pre-accept
Candidate A or Candidate B and must not become a production migration or
architecture acceptance.

Sources: [Issue #131](https://github.com/baroentgray/the-logs-are-wrong/issues/131), [PR #132 owner decision comment 5280644387](https://github.com/baroentgray/the-logs-are-wrong/pull/132#issuecomment-5280644387), `docs/agent/GATE2_DOMAIN_UNITY_ARCHITECTURE_DECISION.md`, and [Grok authoritative PASS record 4926080180](https://github.com/baroentgray/the-logs-are-wrong/pull/132#issuecomment-4926080180).

## D-019 — Owner selects extracted portable authoritative core for Domain↔Unity

After the accepted TLAW-057/TLAW-058 evidence, control-center pre-review PASS,
and D-011 Grok authoritative PASS, the owner explicitly selected Candidate B
for the Domain↔Unity production architecture direction.

Candidate B is an extracted coherent Unity-free portable authoritative core. It
preserves **one semantic authoritative implementation**: net10 Domain
composition and tests consume that core, and later Unity consumes that same
core. There must be no duplicated net10-versus-portable authority
implementation, promoted scratch authority copy, or target-specific gameplay
algorithm fork.

### D-018 history and supersession

D-018 remains an unmodified historical record. Its **REJECT ALL FOR NOW**
decision applied to the earlier evidence state, when the bounded portable path
was still blocked before Unity load. It is superseded by this entry only for the
architecture selection of Candidate B; it is not erased or rewritten.

The evidence state materially changed:

- TLAW-057 compiled the exact 26-file authoritative cut for netstandard2.1
  after exactly 157 accepted semantic-equivalent compatibility replacements:
  131 ArgumentNullException.ThrowIfNull replacements, 25 generic
  Enum.IsDefined<TEnum> replacements, and 1 generic Enum.GetValues<TEnum>
  replacement.
- TLAW-058 established pinned-Editor load, one real authoritative EditMode
  execution chain, and exact fresh net10/Unity parity for the bounded tested
  vector.

These results support selecting Candidate B as the production architecture
direction. They do not make the bounded proof a production migration.

### Bounds retained after selection

The accepted proof remains limited to the proven 26-file cut and tested vector.
This decision does not claim universal parity, full portability of all 60
current Domain files, or a proven production extraction/reference graph. Player
authority execution/parity remains unproven. No host/tick integration, D-016
implementation, networking, FishNet, or Steamworks is authorized by this
decision.

### Required post-selection order

1. The first post-selection implementation is a separately scoped
   scratch/non-production Player authority/load/parity proof using the
   already-proven 26-file portable authority cut and official dependency
   closure.
2. That Player proof must PASS before **any** production migration.
3. Only under a later separate owner implementation-start authorization may a
   production Candidate-B migration atomically establish:
   - single-source extraction of the proven 26-file cut;
   - the 157 accepted semantic-equivalent compatibility replacements on that
     one moved production source implementation: 131 ThrowIfNull replacements,
     25 Enum.IsDefined<TEnum> replacements, and 1 Enum.GetValues<TEnum>
     replacement;
   - an auditable compatibility manifest/count inventory and mandatory
     stop-and-review on unexpected count drift;
   - the portable authoritative-core target;
   - compiler compatibility-definition ownership;
   - System.Collections.Immutable and resolved dependency-closure policy;
   - the existing net10 Domain consuming the core;
   - deterministic regressions; and
   - D-014 snapshot/replay regressions.
4. A later separately gated production Unity import consumes that same core.
5. Host/tick integration and later gameplay/networking work remain separate
   gates and require their own authorization.

This Phase-B decision-log append does not authorize the Player proof,
production source compatibility edits, core extraction, any production
migration, Unity import, host/tick work, D-016, networking, Ready, merge, or
cleanup.

Sources: [Issue #137](https://github.com/baroentgray/the-logs-are-wrong/issues/137), [owner SELECT_B comment 5298433797](https://github.com/baroentgray/the-logs-are-wrong/issues/137#issuecomment-5298433797), [Phase-B implementation-start comment 5298463080](https://github.com/baroentgray/the-logs-are-wrong/issues/137#issuecomment-5298463080), [control-center pre-review PASS](https://github.com/baroentgray/the-logs-are-wrong/pull/138#pullrequestreview-4941178892), [D-011 Grok authoritative PASS](https://github.com/baroentgray/the-logs-are-wrong/pull/138#pullrequestreview-4941343853), [TLAW-059 Phase-A dossier](GATE2_DOMAIN_UNITY_ARCHITECTURE_REFRESH.md), [TLAW-057 portable authority runtime/parity proof](GATE2_PORTABLE_AUTHORITY_RUNTIME_PARITY_PROOF.md), [TLAW-058 portable dependency-closure probe](GATE2_PORTABLE_DEPENDENCY_CLOSURE_PROBE.md), and [D-018](#d-018--owner-rejects-domainunity-architecture-candidates-for-now).

## D-020 — Owner selects H2 for the Unity host-tick composition boundary

After the TLAW-063 scratch/non-production host-tick architecture proof, the
owner explicitly selected **H2** as the production direction for the Unity
host-tick composition boundary. Production migration is **not** performed or
authorized by this entry.

### Evidence and decision context

The selection followed, in order:

- the TLAW-063 scratch/non-production architecture proof;
- control-center Phase-A pre-review PASS 4947116130;
- D-011 authoritative Phase-A review PASS 4947130838.

It was taken against the exact Phase-A candidate
`37c76d3a9f8122be5ae9a380f9e16b4da568370b` over the exact baseline
`5692f9200b191c2c56d1e119b4d6b5ae3003c673`.

TLAW-063 established:

- the exact `HostTickExecutionService.Execute` cut is 54 logical source files:
  the 26 existing PortableAuthority files plus 28 outer-Domain files;
- H2 scratch achieved a `netstandard2.1` compile PASS, a pinned Unity
  6000.3.21f1 / `c02631ffc030` PASS, and an exact host-tick parity PASS;
- the canonical host-tick parity SHA-256 was
  `287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949`;
- H2 required 195 additional known semantic-equivalent compatibility
  replacements: 193 `ArgumentNullException.ThrowIfNull` plus 2
  `Enum.IsDefined`;
- the existing PortableAuthority accepted compatibility surface is
  `131 + 25 + 1 = 157`;
- the known combined H2 compatibility surface is therefore 352.

These counts are evidence bounds measured in scratch. They are not an
authorization to perform those production edits inside TLAW-063, and none of
them have been applied to production source.

### Selected architecture

The measured host-tick composition joins `TheLogsAreWrong.PortableAuthority`.

The intended production ownership boundary becomes 54 logical authoritative
files: the existing 26 PortableAuthority files plus the measured 28-file
`HostTickExecutionService.Execute` cut.

`TheLogsAreWrong.PortableAuthority` remains the shared portable semantic
authority consumed by net10 Domain composition and tests, and later by Unity.

The existing production `HostTickExecutionService` remains the single semantic
seven-stage orchestration authority. No second Unity-side orchestration
implementation is allowed.

### Frozen seven-stage semantic order

H2 preserves one authoritative composition with the existing order:

~~~text
1. HostStageOneCompletionExecutor
2. AcceptedIntentStageExecutor
3. HostStageThreeDeadlineExecutor
4. HostStageFourSawExecutor
5. HostStageFiveFeedExecutor
6. HostStageSixDerivedExecutor
7. HostStageSevenEventExecutor
~~~

Those stages are not redefined by this decision.

### Treatment of alternatives

- **H1** is not selected for this increment because a separate portable host
  assembly requires a separately reviewed friend/public-API boundary change
  under the current production internals arrangement. H1 is not claimed to be
  technically impossible.
- **H3** is not selected because it is wider than the proven execution cut: 60
  logical files versus H2's 54. H3 was scratch-technically viable and is not
  invalid.
- **H4** is not selected because independently recreating the seven-stage
  orchestration inside Unity would violate the one-semantic-authority rule of
  D-019. A future thin Unity driver that invokes the one shared portable
  `HostTickExecutionService` is explicitly **not** H4.

### Relationship to D-019

D-019 remains intact and authoritative. D-020 does not supersede or replace it.

D-019 established the extracted portable authoritative-core architecture. D-020
extends D-019 for the separately gated host/tick composition boundary by
selecting how the subsequently proven host-tick composition joins that
architecture.

### Production migration is still NOT authorized

This decision-log append does **not** authorize:

- moving the 28 production files;
- applying the 195 additional compatibility replacements;
- changing PortableAuthority production ownership;
- editing `src/**`;
- changing csproj/props/targets;
- changing package or dependency policy;
- rebuilding or deploying a new Unity plugin;
- a production Unity host driver;
- a MonoBehaviour or frame-driven host loop;
- gameplay, input, or presentation work;
- D-016 implementation;
- FishNet, FishySteamworks, Steamworks, or any networking;
- Gate 3;
- `Packages/**`, `ProjectSettings/**`, scenes, or prefabs;
- Ready, merge, or cleanup.

The architecture is selected; the production migration is not performed. A
production H2 migration requires a new, separately scoped owner
implementation-start authorization after TLAW-063 is accepted.

Sources: [Issue #145](https://github.com/baroentgray/the-logs-are-wrong/issues/145), [owner H2 selection comment 5309295556](https://github.com/baroentgray/the-logs-are-wrong/issues/145#issuecomment-5309295556), [Phase-B implementation-start comment 5309314299](https://github.com/baroentgray/the-logs-are-wrong/issues/145#issuecomment-5309314299), [authoritative handoff comment 5309325502](https://github.com/baroentgray/the-logs-are-wrong/issues/145#issuecomment-5309325502), [control-center Phase-A pre-review PASS](https://github.com/baroentgray/the-logs-are-wrong/pull/146#pullrequestreview-4947116130), [D-011 Phase-A authoritative review PASS](https://github.com/baroentgray/the-logs-are-wrong/pull/146#pullrequestreview-4947130838), [TLAW-063 Phase-A dossier](GATE2_UNITY_HOST_TICK_ARCHITECTURE_PROOF.md), and [D-019](#d-019--owner-selects-extracted-portable-authoritative-core-for-domainunity).

## D-021 — Owner selects the plain-C# host session and low-desync multiplayer boundary

After TLAW-065 proved the Unity host-runtime boundary on exact candidate
`213c592a5bd454e537b181140253e2e1c6860e92`, D-011 authoritative review
`4968866370` returned PASS / APPROVE with zero findings, and the owner selected
the lowest-complexity, lowest-desync production direction for the later host
runtime and Gate-3 networking boundary.

### Relationship to D-019 and D-020

D-019 and D-020 remain intact and authoritative. This entry does not reverse the
D-020 H2 selection: the candidate labels refer to different architecture
questions.

D-020 H2 selected how the one semantic `HostTickExecutionService` composition
joins `TheLogsAreWrong.PortableAuthority`. TLAW-065 H1 selects the outer runtime
session boundary that owns carried authoritative state and invokes that already
shared composer. The existing `HostTickExecutionService` remains the sole
semantic seven-stage host-tick authority.

### H1 — plain C# authoritative host session

The production direction is one plain C# `HostSession`-shaped boundary owning
the authoritative runtime-state lifetime. Unity lifecycle objects may hold or
schedule that session, but `MonoBehaviour`, static state, networking adapters,
and clients do not become simulation authorities and do not recreate host-stage
semantics.

A running production host process has exactly one explicit owner of that
session. Duplicate production host authority fails closed. The authoritative
session is not a static/global singleton.

### U1 — deterministic host-owned event identity

Authoritative `EventId` values are host-owned and deterministic from
host-authoritative shift/event sequencing. Unity and clients do not manufacture
authoritative event identities.

A later implementation must resolve the TLAW-065 event-identity blocker inside
the authoritative boundary without exposing a Unity-side publication-plan copy
or requiring an external plan-size query. The exact API and representation are
left to that separately reviewed implementation, but replay-safe deterministic
identity from authoritative sequencing is frozen here.

### U2 — exact integer authoritative time

One simulation tick represents one second under the existing integer
`ServerTick` / `SimulationDuration` contract. Render FPS is not authoritative
time. Production scheduling must use exact/integer time and must not use a
float/double simulation accumulator.

Due authoritative ticks must not be silently dropped. This decision deliberately
does not freeze an arbitrary per-frame catch-up cap; any later performance cap
or stall policy requires separate measured evidence and must preserve the
host-authoritative timeline.

### U3 — one explicit production host owner

Exactly one explicit production owner creates, owns, resets, and disposes the
running `HostSession`. A second production session for the same running host is
an invariant violation and fails closed. Single ownership is enforced around the
plain C# session rather than by making authoritative state static/global.

### U4 — validated portable configuration enters Unity

YAML remains outside the accepted three-plugin Unity runtime boundary. Existing
configuration loading/validation remains outside Unity runtime; Unity consumes
already validated PortableAuthority-owned configuration data.

This decision does not authorize `TheLogsAreWrong.Config.Yaml`, `YamlDotNet`,
`TheLogsAreWrong.Domain.dll`, or a fourth plugin inside Gate 2, and it does not
create a second Unity-specific configuration semantics path.

### Gate-3 desync policy

The later Gate-3 networking implementation keeps one simulation truth on the
host:

- clients send intents; the host validates and orders them;
- clients consume authoritative events and snapshots rather than independently
  advancing logs, scheduler, deadlines, quota, containment, procedures, or saw
  state;
- authoritative event/state sequence gaps are resync conditions rather than a
  reason to continue from divergent client state;
- gameplay-state client prediction is not required for the initial Gate-3
  implementation;
- locally responsive actor movement remains compatible with host validation of
  interactions.

The goal is deliberately low-complexity and low-desync: deterministic host-owned
state, ordering, identity, and time, plus recovery through authoritative
snapshot/event boundaries instead of peer simulation convergence.

### Work not authorized by this decision record

This entry records an already-made owner selection only. It does not authorize
or implement the production `HostSession`, EventId generation, Unity runtime
owner, clock/catch-up code, configuration ingestion code, FishNet/
FishySteamworks/Steamworks integration, prediction/reconciliation, Gate 3,
gameplay, D-016, Ready, merge, or cleanup. Each production increment remains
separately owner-gated.

Sources: [Issue #151](https://github.com/baroentgray/the-logs-are-wrong/issues/151), [PR #152](https://github.com/baroentgray/the-logs-are-wrong/pull/152), [D-011 authoritative review 4968866370](https://github.com/baroentgray/the-logs-are-wrong/pull/152#pullrequestreview-4968866370), [Issue #153](https://github.com/baroentgray/the-logs-are-wrong/issues/153), [BAR-109](https://linear.app/baronet/issue/BAR-109/tlaw-066-record-d-021-host-runtime-and-low-desync-multiplayer-boundary), `docs/NETWORK_RULES.md`, and [D-020](#d-020--owner-selects-h2-for-the-unity-host-tick-composition-boundary).

## D-022 — Owner selects C1 for the validated configuration deployment handoff

The owner selected C1, the versioned deterministic artifact plus
PortableAuthority materializer, after the TLAW-069 proof established C1 and C2
as viable alternatives. This records the already-made selection; it does not
reopen candidate evaluation or alter D-019, D-020, or D-021.

C1 remains one PortableAuthority-owned codec/materializer for already validated
configuration data. Canonical YAML loading and validation remain outside Unity.
The deployment boundary carries a deterministic artifact and a trusted binding
to the canonical shift YAML, anomalies YAML, loader identity, artifact identity,
and canonical projection identity.

This selection authorizes only the separately gated TLAW-070 C1 production
handoff. It does not authorize a Unity production owner/driver, U3 ownership
implementation, YAML or validation semantic changes, a Unity-native schema,
networking/Gate 3, gameplay, D-016, Ready, merge, or cleanup.

Sources: [TLAW-069 owner C1 selection](https://github.com/baroentgray/the-logs-are-wrong/pull/161#issuecomment-5372446356), [Issue #162](https://github.com/baroentgray/the-logs-are-wrong/issues/162), and [TLAW-070 implementation handoff](https://github.com/baroentgray/the-logs-are-wrong/issues/162#issuecomment-5373173295).

## D-023 — Gate-3 intent wire contract is versioned, tagged, and fail-closed

For TLAW-077 the owner selected Candidate A and froze one explicit Gate3-owned
wire contract for client gameplay intents. The codec/materializer is plain C#
and independent of FishNet runtime types. PortableAuthority continues to own
`IntentEnvelope` and gameplay semantics; FishNet is only a later carrier around
the frozen bytes.

V1 is one deterministic binary payload with a maximum size of **2048 bytes**,
excluding outer FishNet framing. All fixed-width integers are little-endian. The
exact logical field order is:

~~~text
schema_version          u16
shift_id                string
intent_id               string
actor_id_hint           string
target_id               string
action                  string
expected_state_version  i64
client_observed_tick    i64
parameter_kind          u8
parameter_payload       kind-dependent
~~~

`schema_version=0` is reserved/invalid and V1 is exactly `1`; a V1 decoder
accepts no other version. Every identifier string is encoded as `u16 byte_length`
followed by strict UTF-8, with no BOM, normalization, trimming, or case folding.
Each identifier is bounded to **1..256 UTF-8 bytes** at the network boundary and
must still satisfy the existing PortableAuthority identifier validity rules.
`expected_state_version` and `client_observed_tick` are non-negative signed i64
values and materialize exactly to `StateVersion` and `ServerTick`; neither is
host-authoritative merely because the client transmitted it.

The permanent V1 parameter discriminator is:

~~~text
0 = RESERVED_INVALID
1 = NONE
2 = PROCEDURE_ACTION
~~~

`NONE` has zero parameter-payload bytes and materializes to
`NoIntentParameters.Instance`. `PROCEDURE_ACTION` carries exactly one
`attempted_item` identifier under the same strict UTF-8 / 1..256-byte rules and
materializes to `ProcedureActionIntentParameters(ItemId)`.

A structurally valid but unknown `IntentActionId` is not a wire error. The wire
materializer also does not validate action-to-parameter gameplay compatibility;
those remain Stage-2 responsibilities. Successful decode therefore produces
only decoded client evidence, not admission or authority.

Decode/materialization fails closed and returns no `IntentEnvelope` for malformed
or abusive input. The frozen local Gate-3 failure taxonomy is:

~~~text
MESSAGE_TOO_LARGE
TRUNCATED_OR_MALFORMED_FRAME
INVALID_UTF8
UNSUPPORTED_SCHEMA_VERSION
INVALID_IDENTIFIER
INVALID_NUMERIC_FIELD
UNSUPPORTED_PARAMETER_KIND
PARAMETER_PAYLOAD_MISMATCH
TRAILING_DATA
~~~

These codes are local codec/materialization outcomes only; D-023 does not define
a client-visible rejection protocol or stable on-wire numeric representation for
them. Failure manufactures no Domain event.

V1's payload schema and parameter-kind set are closed. Assigned discriminator
values never change meaning or get reused. A new parameter shape, changed field
encoding, or changed limits requires an explicit new schema version; future V2
support must never reinterpret V1 bytes. V1's action vocabulary remains open so
new structurally compatible actions can still reach Stage 2.

Wire decode/materialization explicitly does **not** resolve actor authority,
trust `ActorIdHint`, choose authoritative receive tick, assign or consume
`ServerReceiveSequence`, deduplicate `IntentId`, decide retransmission semantics,
construct accepted intents/batches, validate shift/session membership, perform
Stage-2 gameplay validation, submit `HostSession`, or replicate state/events.
Those remain later separately owner-gated Gate-3 layers.

This decision record authorizes no FishNet gameplay handler, serialization
implementation, receive sequencing, admission path, replication, snapshot/resync,
reconnect, prediction, Ready, merge, cleanup, or next Gate-3 implementation.

Sources: [Issue #176](https://github.com/baroentgray/the-logs-are-wrong/issues/176), [owner D-023 exact contract freeze comment 5443037732](https://github.com/baroentgray/the-logs-are-wrong/issues/176#issuecomment-5443037732), and [BAR-120](https://linear.app/baronet/issue/BAR-120/tlaw-077-architecture-desk-freeze-gate-3-intentenvelope-wire-contract).
