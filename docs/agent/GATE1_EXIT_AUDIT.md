# Gate 1 exit audit

Schema: `tlaw.gate1-exit-audit/v2`.

## A. Identity

| Field | Value |
|---|---|
| Audited `main` | `972461310d50d4538311652b36d26a9ee9157c61` |
| Verification workflow | `Repository verification`, run `31509749346`, run #182, event `push`, branch `main` |
| Job | `93840266841` — `Deterministic verification`, success |
| Artifact | `verification-31509749346`, ID `9108595403`, `426755` bytes |
| Digest | `sha256:0d441c8bbfe5e23e08fa2ccf54f486326bad3cb6a043ffb2fc84cfe0d2d82a6d`, independently recomputed and matched |
| Checkout | detached clean checkout; expected/actual base and head all equal the exact accepted `main` SHA; restore PASS |
| Build | 0 warnings / 0 errors |
| Tests | 1631 passed / 0 failed / 0 skipped |
| Gate 0 | PASS; Git object reader 52/52 PASS |
| Architecture / Domain dependencies | PASS; `packageReferences: []` |
| Verdict | PASS, `failureReasons: []` |

This is a **fresh re-audit of that exact SHA**, produced by TLAW-048 / Issue #116. Git history retains the earlier
historical audit of `main@6e4ed1e1a9337af2e5149cbd16f3b971f274a0ab`; that artifact is not deleted and remains valid as a
record of what was true at its own SHA. This refreshed artifact supersedes it **only** for the question of current
Gate-1 exit readiness. Any commit later than the audited SHA above requires fresh revalidation before this audit may be
relied upon again.

This document is not a second volatile current-state cache (`CURRENT_STATE.md` remains the only one, per D-008) and it
is not a generated projection (D-009). Its authority is repository-operational under D-002; it does not modify frozen
Gate 0 and it records no new accepted decision — `DECISIONS.md` stays unchanged, because the three questions the
previous audit left open are already resolved as D-014, D-015 and D-016 (D-012).

Evidence used here is repository-native only: frozen Gate 0 documents, `data/*.yaml`, `docs/agent/DECISIONS.md`, merged
code and tests at the audited SHA, merged pull requests, and the exact-main CI evidence above. Chat and model memory are
not authority (D-007).

## B. Gate-1 scope matrix

Frozen Gate-1 scope is `docs/PROTOTYPE_SCOPE.md` § "Gate 1 — чистая C# domain simulation", refined by
`docs/FIRST_SHIFT_SPEC.md`, `docs/ANOMALY_MATRIX.md`, `docs/LOG_STATE_MACHINE.md`, `docs/INTAKE_SCHEDULER.md` and the
Gate-1 test list in `docs/NETWORK_RULES.md`.

Rows 1–18 were `SATISFIED` at the historical audit SHA. They are **not** carried over on that basis. Each is
revalidated here against current evidence: the exact-main full suite `1631 / 0 / 0`, the current architecture and Gate-0
verifier PASS, the current Domain `packageReferences: []`, and the delta review in section E, which shows that the
seventeen commits since the historical audit changed no frozen data, no configuration, and no pre-existing gameplay
semantics. The named merged evidence below was re-checked to still exist and still assert the same thing at the audited
SHA.

| # | Gate-1 lane | Status | Evidence revalidated at `972461310d50d4538311652b36d26a9ee9157c61` |
|---|---|---|---|
| 1 | Typed Domain + YAML configuration adapter | `SATISFIED` | `Configuration/ValidatedConfiguration.cs`, `TheLogsAreWrong.Config.Yaml/YamlConfigurationLoader.cs`. YAML stays an external adapter; `Tlaw043ArchitectureTests` asserts no `Yaml` assembly reference from the Domain, and the exact-main `domainDependencies` check is PASS. Unchanged by the delta. |
| 2 | Host clock, deterministic tick, sequencing | `SATISFIED` | `Time/SimulationTime.cs`, `Primitives/Primitives.cs`, `Sequencing/SequencingContracts.cs`. `EventSequence.None` vs initialized zero `StateVersion`/`ServerTick` preserved per D-003; `ShiftSnapshot.LastEventSequence` uses `EventSequence.None` for "no event published yet", which re-exercises that distinction rather than eroding it. |
| 3 | Event journal and state version | `SATISFIED` | `Journal/EventJournal.cs`, `Journal/JournaledMutationCommitContracts.cs`. Append stays fail-closed on sequence gap, tick regression and version skip. TLAW-046 reads the journal only; `Tlaw046ArchitectureTests` asserts the capture boundary contains no `Append`/`TryAppend`/`IEventJournal`. |
| 4 | Immutable shift and log state machine | `SATISFIED` | `Runtime/ShiftRuntimeState.cs`, `Logs/LogTransitionPolicy.cs`. Terminal states remain irreversible and node capacities enforced. The delta added one field and one `internal` restore seam to this file and changed no transition rule — see E.2. |
| 5 | Quota ledger and objective predicate | `SATISFIED` | `Quota/QuotaContracts.cs`. `ObjectivesSatisfied` is still exactly `pine ≥ 5 AND oak ≥ 4 AND correctly_processed_anomalies ≥ 2` from `data/shift_p0.yaml`, matching `FIRST_SHIFT_SPEC` § "Success predicate". `data/**` is untouched by the delta. |
| 6 | Anomaly processing, procedures, inventory, flags | `SATISFIED` | `Anomalies/AnomalyResolutionContracts.cs`, `Runtime/ProcedureCompletionContracts.cs`, `Runtime/ProcedureActionLifecycleContracts.cs`. Consumables are still debited on completion for correct and configured-wrong actions alike, per `ANOMALY_MATRIX` § "Item consumption contract"; `Tlaw043ArchitectureTests` re-asserts the wrong-holy-water `consumes: true` contract at this SHA. |
| 7 | Confirmation lifecycle and line-noise integration | `SATISFIED` | `Runtime/ConfirmationTestLifecycleContracts.cs`. Penitent still requires 4 continuous `QUIET` seconds, `LOUD` resets progress, and the intake timer is not paused — exactly `FIRST_SHIFT_SPEC` § "Line noise". Unchanged by the delta. |
| 8 | Containment state machine and ritual | `SATISFIED` | `Containment/ContainmentLifecycleContracts.cs`. `STABLE → SERVICE_REQUESTED → OVERDUE → INCIDENT`, danger-weight intervals 90/75/60, 20 s grace, 10 s overdue, 4 s ritual hold. `Tlaw043ArchitectureTests` re-asserts the exact `ContainmentState` enum at this SHA. Incident *execution* stays a separate row — see C.2. |
| 9 | Jam and repair lifecycle | `SATISFIED` | `Line/LineJamRepairContracts.cs`, `Scheduler/RepairPendingTransitionExecutionContracts.cs`. One active cause; repair completion still refuses while the blocker remains. D-015 introduced no jam or repair substitution — `PenitentSawFailureWindowContinuationTests` asserts `LINE_CLEAR`, no active repair hold and no repair intent across every tick of the failure-window continuation. |
| 10 | Feed, intake deadline, auto-route, saw scheduler | `SATISFIED` | `Scheduler/**`, matching the P0 parameters in `INTAKE_SCHEDULER.md`. Feed and intake work is proven to continue *during* an active failure window: the continuation scenario schedules an early feed at tick 20 (due 22), admits at 22 and starts a fresh intake deadline, all while the saw is blocked. |
| 11 | Movement noise and derived line noise | `SATISFIED` | `Line/MovementNoiseRuntimeContracts.cs`, `Line/LineNoiseRuntimeContracts.cs`. `LOUD` iff saw, movement or repair is active. The failure window is not a noise source: at tick 26, mid-window, stage six reports `QUIET` with all three sources inactive. |
| 12 | Shift completion and exact hard-deadline checkpoint | `SATISFIED` | `Runtime/ShiftCompletionContracts.cs`, `Runtime/HostTickCompletionCheckpointContracts.cs`. Sequential ticks remain enforced; backward, skipped and post-completion ticks are rejected. The failure-window continuation runs ticks `0..33` contiguously and keeps `HardDeadlineDuration = 840` / `HardDeadlineAt = 840`. |
| 13 | Accepted-intent ordering and every current P0 stage-2 action family | `SATISFIED` | `Intents/AcceptedIntentBatchContracts.cs`, `Runtime/AcceptedIntentStageExecutionContracts.cs`. The same nine action IDs: four manual routing (`RouteToProcedure`, `ReturnFromProcedure`, `RouteToSawQueue`, `WriteOff`) plus early feed, procedure action, confirmation test, line repair and containment ritual. No action family was added or removed by the delta. |
| 14 | Frozen seven-stage host composer | `SATISFIED` | `Runtime/HostTickExecutionContracts.cs`. Stage order still equals `HostTickStages.CanonicalOrder` and `data/shift_p0.yaml` `same_tick_order`. `Tlaw047ArchitectureTests` asserts the composer source contains no `SawFailure` token at all, so D-015 sits inside stage 4 and did not reshape the composer. |
| 15 | Stage-7 publication and journal evidence | `SATISFIED` | `Runtime/HostStageSevenEventExecutionContracts.cs`. Still exactly twenty-four frozen event types, contiguous sequence and exact causation. Both `Tlaw046ArchitectureTests` and `Tlaw047ArchitectureTests` independently re-assert the count as 24 and assert that neither increment added a replay-only or effect-only event type. |
| 16 | Full P0 Learning/Pressure scenario and repeatability evidence | `SATISFIED` | `tests/…/Determinism/FullP0/**`. Learning correct path still completes at tick 172 with the objective met; the cautious full-timeout policy still completes at 782 < 840 under `learning` and still fails to finish at exactly 600 under `pressure`; write-off-all-suspicious still fails the objective; containment still overlaps an intake task; ten independent runs are still structurally equal. The delta added one new scenario and edited none of the frozen ones — see E.2. |
| 17 | Domain zero-package, Unity/FishNet/Steam/network independence | `SATISFIED` | `src/TheLogsAreWrong.Domain/TheLogsAreWrong.Domain.csproj` still has no `PackageReference`. `ArchitectureGuardTests`, `Tlaw043ArchitectureTests`, `Tlaw046ArchitectureTests` and `Tlaw047ArchitectureTests` each assert it independently, `Tlaw046ArchitectureTests` additionally bans serialization/JSON/XML assemblies from the snapshot surface, and exact-main `domainDependencies` reports `packageReferences: []`. |
| 18 | Consequences of processing errors — terminal state and quota | `SATISFIED` | Wrong Penitent/Resin still reach `PROCESSED` with zero credit and zero anomaly delta; wrong False Species still credits the declared species once. Proven end-to-end through the real host and re-asserted from configuration by `Tlaw043ArchitectureTests`. D-015 explicitly preserves this: the incorrect-Penitent completion test asserts `PROCESSED`, `AllRequiredFlagsPresent == false` and `CreditedUnits == 0` alongside the new window. |
| 19 | Consequences of processing errors — the three timed/targeted effects | see C | `time_penalty` is now executed as the exact D-015 saw-only window; `lock` and `forced_line_pause` remain descriptors only. Section C dispositions each one. |
| 20 | Snapshot and replay | `SATISFIED` | See B.1 below. |

### B.1 Snapshot and replay — closed by D-014 and accepted TLAW-046

The frozen requirement is unchanged and still sits inside Gate 1:

- `docs/PROTOTYPE_SCOPE.md` § Gate 1 lists «Event journal, state version, snapshots и replay»;
- `docs/NETWORK_RULES.md` § "Тесты по gates → Gate 1" lists `Snapshot/replay`;
- `docs/LOG_STATE_MACHINE.md` § "Snapshot/replay" defines the eleven-field `ShiftSnapshot`, requires that Gate 1
  «умеет восстановить state из snapshot + events после `last_event_sequence`», and requires that «полный replay от
  начального manifest должен давать тот же итоговый snapshot».

D-014 recorded the owner's selection of TLAW-043 option B — implement it rather than defer it — and TLAW-046 /
Issue #112 / PR #113 implemented it. The audited SHA now contains all six required elements:

| Required element | Evidence at the audited SHA |
|---|---|
| Exact frozen eleven-field shape | `Journal/ShiftSnapshotContracts.cs` — `ShiftSnapshot` exposes exactly `ShiftId`, `ServerTick`, `StateVersion`, `LastEventSequence`, `SchedulerState`, `Logs`, `LineState`, `ContainmentState`, `Inventory`, `Quota`, `Objectives`, in the frozen order. `Tlaw047ArchitectureTests` asserts the public top-level property count is exactly 11 excluding the derived `Boundary`, and that the D-015 window lives inside `SchedulerState`, never as a twelfth top-level field. Shift seed, node capacities and manifest order are configuration-derived and validated against the exact `ShiftConfiguration` during restore instead of being duplicated into the frozen shape. |
| Capture | `Journal/ShiftSnapshotCaptureContracts.cs` — `ShiftSnapshotCaptureService` exposes exactly `Capture`, `CaptureRestored` and `CreateInitial`, and nothing else. It appends nothing: the guard asserts the source contains no `Append`, `TryAppend` or `IEventJournal`. |
| Restore | `Journal/ShiftSnapshotRestoreContracts.cs` — `ShiftSnapshotRestoreService.Restore` returns separate runtime values, never a composite host aggregate. It reaches the runtime through exactly one `internal static ShiftRuntimeState.RestoreForSnapshot` seam, which rebuilds identity, seed, capacities and manifest indexes from the same validated configuration the live host used and rejects any manifest whose length, order or identity disagrees. `Tlaw046ArchitectureTests` asserts the seam is `internal`, is the only added production seam, and that no Domain method accepts a `ShiftSnapshotRestored` as input. |
| Snapshot + exact journal tail replay | `Journal/ShiftReplayReducerContracts.cs` — `ShiftReplayService.ReplayFrom(snapshot, tail, configuration)`. `ShiftReplayScenarioTests.Mid_shift_snapshot_plus_journal_tail_reconstructs_the_uninterrupted_live_final_snapshot` captures a real mid-shift checkpoint at tick 154 with an active containment ritual, takes only the envelopes with `Sequence > LastEventSequence`, and reconstructs the uninterrupted live final snapshot with no difference. `PenitentSawFailureWindowContinuationTests` repeats the same proof from a checkpoint whose `SchedulerState.ActiveSawFailureWindow` is non-null. |
| Full replay from the initial manifest | `ShiftSnapshotCaptureService.CreateInitial(configuration, profile)` plus `ShiftReplayService.ReplayAll`. `ShiftReplayScenarioTests` proves it for three materially different real scenarios — Learning correct path (tick 172, 115 events, objectives satisfied), Pressure full timeout (tick 600, hard-deadline completion, a log still in the saw) and write-off-all-suspicious (five logs `HELD_WRITTEN_OFF`, objectives unsatisfied) — each reproducing the live final snapshot with `FirstDifference == null`. `Full_replay_needs_no_intents_and_leaves_its_inputs_unchanged` proves reconstruction consumes only the published journal, replays no intent, and mutates neither the input snapshot nor the journal. |
| Deterministic structural equality | `ShiftSnapshot.StructurallyEquals` compares by value, independent of identity and of collection enumeration order; `SnapshotOrdering` gives every snapshot collection one canonical ordinal order. `Ten_independent_full_replays_produce_structurally_equal_snapshots` asserts ten independent replays are structurally equal and `NotSame`. |

No network or package dependency was introduced: `Tlaw046ArchitectureTests` bans `Unity`, `Fish`, `Steam`, `Sockets`,
`Net.Http`, `Yaml`, `Json` and `Xml` assembly references from the assembly that owns `ShiftReplayService`, and bans
`DateTime`, `Stopwatch`, `Random`, `Guid.NewGuid`, `Task`, `Thread`, `Timer`, `lock (`, file, stream and serialization
tokens from the five snapshot/replay sources. The reducer is also proven not to re-run gameplay: it references no
`HostTickExecutionService`, no intent envelope and no `.Execute(`.

`docs/NETWORK_RULES.md` § "Snapshot/resync" still says Gate 3 is the first *network* consumer of this contract. That
remains true and is not a deferral: the Gate-1 obligation is implemented here, and Gate 3 may later consume the same
contract over the wire.

Row 20 is therefore `SATISFIED`. The historical `BLOCKED_REQUIRES_OWNER_DECISION` status is closed.

## C. Effect-disposition matrix

Frozen `data/anomalies.prototype.yaml` and `data/shift_p0.yaml` retain four effect descriptors, unchanged at this SHA.
`EffectType` is exactly `{ time_penalty, lock, miscredit }` (`Enums/DomainEnums.cs`). Descriptors are carried on
`ProcessingResolution.Effects` and `ItemActionCompletionDescriptor.Effects` and published inside stage-7 payloads.

The one behavioural change since the historical audit is D-015: the Domain now branches on an effect kind in exactly one
place. `Tlaw043ArchitectureTests` pins that scope — the only Domain file containing `EffectType.` is
`Scheduler/SawCycleContracts.cs`, and the only other production reference is the YAML loader's shape validation. No
generic effect dispatcher, executor, runtime, applier or scheduler exists, and none of `ButtonLock`,
`NearestLineButton`, `ForcedLinePause`, `ForcedPause`, `LineButtonState`, `PenaltyRuntime`, `MiscreditApplication` or
`MiscreditExecutor` appears anywhere in the Domain sources or exported types.

| # | Effect | Trigger | Status |
|---|---|---|---|
| C.1 | `miscredit` / `CREDIT_TO_DECLARED_SPECIES` | False Species processed without `CORRECTLY_RELABELED` | `SATISFIED` |
| C.2 | `forced_line_pause`, 8 s | Containment reaches `INCIDENT` | `DEFERRED_BY_FROZEN_GATE_BOUNDARY` |
| C.3 | `time_penalty` / `FALSE_PA_ANNOUNCEMENT`, 8 s | Penitent processed without `SANITIZED_PENITENT` | `SATISFIED` |
| C.4 | `lock` / `RESIN_BUTTON_LOCK`, target `nearest_line_button`, 10 s | Resin processed without `SEALED_RESIN`, and holy water applied to Resin before the saw | `DEFERRED_BY_FROZEN_GATE_BOUNDARY` |

### C.1 False Species `miscredit` — `SATISFIED`

The frozen sources define this effect entirely in quota terms. `FIRST_SHIFT_SPEC` § "Ложная порода" gives, without the
flag: terminal `PROCESSED`, «credit: заявленная порода ×1», `correctly_processed_anomalies +0`, effect type
`miscredit`. `ANOMALY_MATRIX` states «Incorrect saw: credit goes to declared species». `data/anomalies.prototype.yaml`
encodes `quota_credit: {species: declared_species, units: 1}`, `correct_anomaly_delta: 0` and a `miscredit` effect with
no `duration_seconds` and no `target` — still the only one of the three anomaly effects that is untimed and untargeted.

The declared-species quota settlement already applies the whole consequence exactly once, and this is re-proven at the
audited SHA by `Tlaw043ArchitectureTests.False_species_declared_credit_is_represented_once_with_no_second_application_boundary`:

- `AnomalyProcessingResolver.Resolve` maps `declared_species` to `log.DeclaredSpecies` and emits **one**
  `QuotaSettlement`; `ProcessingResolution.Settlement` is a single `QuotaSettlement`, not a collection — asserted by
  reflection on the property type;
- applying it once credits `pine = 1`, total `1`, `correctly_processed_anomalies = 0`; applying it again returns
  `QuotaSettlementDuplicate` with the same state instance rather than crediting twice;
- `QuotaSettlementService` exposes no method whose name contains `Miscredit` or `Effect`, so no production surface can
  apply a second, separate miscredit.

The descriptor remains a **label for a consequence that has already been applied**, not an instruction for a second one.
Nothing further is owed in Gate 1, and adding a separate miscredit executor would double-credit. Unchanged by the
delta.

### C.2 Containment `forced_line_pause` — `DEFERRED_BY_FROZEN_GATE_BOUNDARY`

`FIRST_SHIFT_SPEC` § "Отстойник" still heads this behaviour literally «Placeholder Gate 2 incident», and describes an
8-second forced line pause applied once, with the state remaining `INCIDENT` until the ritual completes and no repeat
incident before resolution. `PROTOTYPE_SCOPE` still places «Сигнал/ритуал/placeholder incident отстойника» in the
Gate 2 "Входит" list, not Gate 1.

The audited SHA still behaves accordingly: `ContainmentAdvanceService` enters `INCIDENT` and builds a
`ContainmentIncidentDescriptor(type, duration, triggeredAt)` from configuration, stage 7 publishes it inside
`HostStageSevenContainmentPayload.Incident`, and no Domain source contains any line-pause state or execution. The
`prototype_incident` block in `data/shift_p0.yaml` still reads `type: forced_line_pause`, `duration_seconds: 8`,
`remains_incident_until_ritual: true`, `repeat_before_resolution: false`.

`Tlaw043ArchitectureTests.Containment_prototype_incident_remains_the_frozen_forced_line_pause_placeholder` re-asserts
all four configured values at this SHA and additionally proves the Domain sources contain none of `forced_line_pause`,
`ForcedLinePause`, `PauseLine`, `LinePauseState` or `PausedUntil`, and that `LineState` is still exactly
`{ LINE_CLEAR, LINE_JAMMED, REPAIRING }` with no pause member.

D-015 did not touch this row. The saw failure window is explicitly not a line pause: it is saw-owned, and the
continuation evidence proves the line stays `LINE_CLEAR` and feed/intake work continues throughout. This row is closed
by the frozen gate boundary itself, needs no owner decision, and must **not** be implemented in Gate 1.

### C.3 Penitent `time_penalty` — `SATISFIED`

The historical audit blocked this row because the frozen sources gave only a name, an event identifier and a duration,
and none of the questions an implementation must answer was decided anywhere. That gap is now closed by an owner
decision, not by inference: D-015 froze the exact deterministic mechanics, and TLAW-047 / Issue #114 / PR #115
implemented exactly them. The implementation is part of the accepted current main, whose exact-main evidence is in
section A.

The frozen descriptor is unchanged — `FIRST_SHIFT_SPEC` § "Кающийся ствол", `ANOMALY_MATRIX` § PENITENT_TRUNK and
`data/anomalies.prototype.yaml` still give `time_penalty` / `FALSE_PA_ANNOUNCEMENT` / 8 seconds with no target — and
`Tlaw043ArchitectureTests` re-asserts all four configured values at this SHA.

**The exact eight-second saw-only window.** `Scheduler/SawCycleContracts.cs` defines an immutable `SawFailureWindow`
with `StartedAt`, `Duration` and `DueAt = StartedAt + Duration`, active on the half-open interval
`StartedAt <= tick < DueAt`. It is installed only by `SawFailureWindowFactory.FromCompletion`, which fires only when the
owner anomaly is exactly `PENITENT_TRUNK`, the resolution is anomalous, `AllRequiredFlagsPresent` is exactly `false`,
and the retained effects contain exactly one `time_penalty` / `FALSE_PA_ANNOUNCEMENT` / 8-second effect with a null
target. Any other shape — none, two, wrong type, wrong event, wrong duration, or a non-null target — throws rather than
producing a window, proven by `PenitentSawFailureWindowTests.Malformed_effect_like_incorrect_penitent_evidence_fails_closed_without_creating_a_window`
across all six malformed cases. Normal, Resin and False Species completions produce no window at all.

**Saw-only.** The window is consumed in exactly two places inside the saw boundary: `SawCycleStartService.Start` returns
the typed no-op `SawCycleStartBlockedByFailureWindow` while it is active, and `SawCycleCompletionService` fails closed
if a completion or an active cycle would ever overlap an active window. `Tlaw047ArchitectureTests` asserts the host
composer source and the stage-7 source contain no `SawFailure` token at all, and that the new value is not a line or
generic effect runtime.

**Unchanged hard deadline, ticks, feed, intake and non-saw work; no jam, repair or global pause; automatic expiry.**
`PenitentSawFailureWindowContinuationTests.Real_host_continuation_keeps_the_saw_blocked_until_exact_expiry_while_non_saw_work_continues`
runs the real full-host driver and asserts, in one scenario:

- the incorrect Penitent completes at tick 19 and the window is exactly `StartedAt = 19`, `DueAt = 27`, `Duration = 8`;
- every tick from 19 through 26 returns `SawCycleStartBlockedByFailureWindow` on the *same* state instance, so the block
  is a typed no-op that mutates nothing and bumps no state version;
- at exactly tick 27 the saw resumes automatically with an ordinary `SawCycleStarted` for the queued successor — no
  player action, no repair intent and no manual clearing step is involved;
- ticks run contiguously `0..33` with no skip, stall or repetition;
- the hard deadline stays `HardDeadlineDuration = 840` and `HardDeadlineAt = 840`, neither moved nor extended;
- non-saw work continues *during* the window: an early-feed intent and a manual route are accepted at tick 20, the feed
  is scheduled for 22, and the log is admitted at 22 with a fresh intake deadline started;
- the line is `LINE_CLEAR` on every tick, with no active repair hold, no `LineRepair` result other than
  `LineRepairNoActive`, and no line-repair intent anywhere in the run;
- at tick 26, mid-window, derived line noise is `QUIET` with saw, movement and repair all inactive — the window is not
  a noise source and not a line state.

`PenitentSawFailureWindowTests.Stage_four_keeps_completion_then_quota_then_typed_blocked_start_for_incorrect_penitent`
proves the stage-4 ordering is unchanged: completion, then quota settlement, then the typed blocked start, with exactly
one state-version increment for the whole stage. The sibling test proves an ordinary completion still starts its
successor in the same tick with no window. `Ten_independent_continuations_have_identical_window_blocking_host_journal_and_snapshot_projections`
proves the whole thing is deterministic across ten independent runs.

`FALSE_PA_ANNOUNCEMENT` remains the causal domain event identifier and Gate 1 requires no audio playback, exactly as
D-015 states. The rejected, zero-credit outcome is unchanged — row 18 above.

Row C.3 is therefore `SATISFIED`. The historical `BLOCKED_REQUIRES_OWNER_DECISION` status is closed by D-015 plus
accepted TLAW-047, not by this audit inventing mechanics.

### C.4 Resin `lock` / `nearest_line_button` — `DEFERRED_BY_FROZEN_GATE_BOUNDARY`

The historical audit classified this as `BLOCKED_REQUIRES_OWNER_DECISION` because no repository decision deferred it.
That is no longer the case: D-016 is that decision. The owner accepted TLAW-043 option A — Gate 1 keeps descriptor-only
treatment and mechanical execution is deferred to the Gate 2 control-surface and spatial representation. The row is
therefore closed by a named gate boundary and is **not** a Gate-1 blocker.

The accepted fiction is unchanged: unsealed Resin reaching the saw, or holy water applied to Resin before the saw,
activates anomalous resin that blocks the nearest physical line-control button for ten seconds. The retained effect
stays exactly `lock` / `RESIN_BUTTON_LOCK` / target `nearest_line_button` / duration 10 seconds.

What D-016 requires of Gate 1, and what the audited SHA proves:

| D-016 requirement | Evidence at the audited SHA |
|---|---|
| Retain the exact descriptor | `data/anomalies.prototype.yaml` still carries `{type: lock, event: RESIN_BUTTON_LOCK, target: nearest_line_button, duration_seconds: 10}` on both `processing.on_incorrect` and `wrong_actions.holy_water`. `Tlaw043ArchitectureTests.Effect_type_and_p0_descriptors_retain_their_exact_frozen_values` asserts all four fields on both paths. |
| Retain the existing wrong-action semantics | The same test asserts the configured wrong holy-water action still has `LeavesStateUnchanged == true`, a null terminal state and `Consumes == true` — the charge is spent and the object remains processable, exactly as `FIRST_SHIFT_SPEC` and `ANOMALY_MATRIX` freeze it. |
| No Gate-1 lock executor | `Tlaw043ArchitectureTests.No_generic_effect_runtime_or_resin_or_containment_executor_exists` asserts no Domain source and no exported type contains `ButtonLock`, `NearestLineButton`, `EffectExecutor`, `EffectDispatcher`, `EffectRuntime`, `EffectApplier` or `EffectScheduler`, over a non-vacuous scan of more than 30 sources and more than 50 exported types. |
| No abstract substitute target | The `EffectType.` branch scope is pinned to exactly one Domain file, `Scheduler/SawCycleContracts.cs`, and that branch matches only `EffectType.time_penalty` with a **null** target. Nothing in the Domain reads, resolves or substitutes `nearest_line_button`: the literal does not occur anywhere under `src/**`, and the target field survives only as an opaque `EffectDefinition.Target` string carried from YAML. |
| The Gate-1 referent genuinely does not exist | The Domain still contains no button entity, no control surface and no actor-position model, so "nearest" cannot be evaluated in a Gate-1 state. This is the substantive reason D-016 names Gate 2, not a convenience. |

D-015 did not weaken this boundary. The one narrow effect branch it authorized is saw-owned and explicitly refuses any
effect carrying a target, which is precisely what a `lock` descriptor carries — so the Resin descriptor cannot
accidentally reach the D-015 path.

Physical nearest-button selection and presentation belong to Gate 2, which resolves them against actual control
surfaces. This row must **not** be classified as `BLOCKED_REQUIRES_OWNER_DECISION`: the owner decision is already
recorded as D-016.

## D. Exit verdict

```text
GATE1_EXIT_READY
```

Every material Gate-1 lane is `SATISFIED` against current exact-main evidence. The two remaining non-satisfied
rows — C.2 containment `forced_line_pause` and C.4 Resin `lock / nearest_line_button` — are
`DEFERRED_BY_FROZEN_GATE_BOUNDARY` to Gate 2, one by the frozen scope documents themselves and one by the recorded
owner decision D-016. No row is `BLOCKED_REQUIRES_OWNER_DECISION`. No owner-decision blocker remains open.

**This verdict is an audit readiness conclusion, not owner acceptance.** The two are deliberately distinct:

- the **audit readiness verdict** above is a source-and-evidence conclusion about the repository at
  `972461310d50d4538311652b36d26a9ee9157c61`: no frozen Gate-1 obligation is outstanding and no unresolved question
  requires the owner;
- **owner acceptance of Gate 1** is a separate, later, explicit workflow gate. It happens only after this audit
  artifact is itself reviewed, merged and exact-main verified, and it is the owner's act — not this document's.

Nothing in this audit authorizes package smoke testing, package-pin selection, Gate 2, Gate 3 or BAR-82 work, and a
ready verdict does not start any of them. A descriptor-only test remains proof that a descriptor is *retained*, never
proof that a corresponding mechanical rule is *complete*.

## E. Delta and regression argument

Historical audited `main@6e4ed1e1a9337af2e5149cbd16f3b971f274a0ab` → current audited
`main@972461310d50d4538311652b36d26a9ee9157c61` is **17 commits ahead, 0 behind**, verified by
`git rev-list --left-right --count`. The full changed-path set is 21 files: 2 documentation, 7 production, 12 test.

### E.1 Decision recording — D-014, D-015, D-016

`docs/agent/DECISIONS.md` gained exactly three appended entries (TLAW-045 / Issue #110 / PR #111). D-001 through D-013
are untouched, preserving the append-only contract of D-012. `docs/agent/GATE1_EXIT_AUDIT.md` was added by TLAW-043 /
PR #108 and is the artifact this document replaces.

### E.2 Production changes

| Path | Nature |
|---|---|
| `Journal/ShiftSnapshotContracts.cs`, `ShiftSnapshotCaptureContracts.cs`, `ShiftSnapshotRestoreContracts.cs`, `ShiftReplayReducerContracts.cs`, `ShiftReplayReductionState.cs` | New files. The whole D-014 / TLAW-046 snapshot and replay surface. |
| `Runtime/ShiftRuntimeState.cs` | One new `SawFailureWindow?` field threaded through the private constructor and every existing copy helper, one added `ValueEquals` comparison, one added clause in the pristine-state predicate, one added parameter on `internal CompleteSawCycle`, and one new `internal static RestoreForSnapshot` seam. **No existing transition rule, capacity check, terminal-state rule or validation was changed**: every other edited line is an argument list gaining one trailing argument. |
| `Scheduler/SawCycleContracts.cs` | The D-015 / TLAW-047 `SawFailureWindow` value, the `SawCycleStartBlockedByFailureWindow` typed no-op, two fail-closed overlap guards, and the `internal SawFailureWindowFactory` trigger derivation. |

There is no change to `data/**`, no change to `docs/**` outside `docs/agent/**`, no change to `source/**`,
`tools/**`, workflows or package manifests, and no change to any frozen Gate-0 file. The only production files touched
outside the new snapshot/replay set are the two named above, and both changes are the direct, minimal expression of
D-014 and D-015.

### E.3 Test changes

New: `Architecture/Tlaw043ArchitectureTests.cs`, `Architecture/Tlaw046ArchitectureTests.cs`,
`Architecture/Tlaw047ArchitectureTests.cs`, `Determinism/SnapshotReplay/ShiftReplayScenarioTests.cs`,
`Journal/ShiftReplayReducerTests.cs`, `Journal/ShiftSnapshotTests.cs`,
`Journal/ShiftSnapshotRestoreCorrelationTests.cs`, `Journal/PenitentSawFailureWindowSnapshotTests.cs`,
`Scheduler/PenitentSawFailureWindowTests.cs`, `Scheduler/PenitentSawFailureWindowContinuationTests.cs`.

Modified: `Determinism/FullP0/FullP0HostScenarioScript.cs` — purely additive. One new scenario builder,
`IncorrectPenitentFailureWindowContinuation`, was appended; no existing frozen scenario was edited, reordered or
retimed, which is why the tick-172, tick-782 and tick-600 expectations in row 16 still hold unchanged.

Modified: `Architecture/Tlaw042ArchitectureTests.cs` — audit maintenance only. The TLAW-042 range proof previously
compared its authorized baseline against dynamic `HEAD`, which would have become false the moment any later increment
touched `src/**`. It now compares the fixed historical pair `71aee1cc…` → `6e4ed1e1…` and asserts the exact six
accepted TLAW-042 paths. This makes a historical claim permanently checkable; it removes no coverage from current main
and changes no gameplay assertion.

### E.4 Reconciliation

The delta reduces exactly to the four groups Issue #116 anticipated: decision recording, snapshot/replay implementation
and tests, Penitent saw-failure implementation and tests, and audit/architecture-test maintenance. The delta contains
**no** frozen Gate-0 or YAML change, **no** package, network or workflow change, **no** Gate-2 runtime, **no** BAR-82
design work and **no** unrelated gameplay semantics. Nothing in it contradicts a previously satisfied lane, so no
`SATISFIED` row from the historical audit is regressed, and the ready verdict in section D stands.

## F. Post-Gate-1 sequence

Source-backed only, recorded and **not started** by this audit:

1. **Explicit owner acceptance of Gate 1**, after this audit artifact is reviewed, merged and exact-main verified. This
   is a separate owner act; the section D verdict does not perform it.
2. **Isolated package smoke-test** of the Unity/FishNet/FishySteamworks/Steamworks.NET pin matrix —
   `GATE_0_EXIT_CHECKLIST` § Network, «After Gate 1: isolated package smoke-test» (still unchecked), and
   `PROTOTYPE_SCOPE` § Gate 3 «Отдельный smoke-test pin-матрицы».
3. **Exact package pins accepted** — `GATE_0_EXIT_CHECKLIST` § Network, «After smoke-test: exact pins accepted» (still
   unchecked). The stack remains `PROPOSED` until then (`NETWORK_RULES` § "Proposed stack").
4. **Gate 2** — a single Unity process with one local authoritative host, no FishNet (`PROTOTYPE_SCOPE` § Gate 2;
   `NETWORK_RULES`: «FishNet не является зависимостью Gate 1 или Gate 2»). Gate 2 also owns the deferred C.2 and C.4
   rows.
5. **Gate 3** — Steam listen-server for two real players (`PROTOTYPE_SCOPE` § Gate 3), the first network consumer of the
   `ShiftSnapshot` contract implemented in B.1.

No package research, install or smoke-test, no Gate 2 or Gate 3 work, no BAR-82 design work and no branch or worktree
cleanup is performed by this audit.
