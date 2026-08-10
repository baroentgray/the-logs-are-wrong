# Gate 1 exit audit

Schema: `tlaw.gate1-exit-audit/v1`.

## A. Identity

| Field | Value |
|---|---|
| Audited `main` | `6e4ed1e1a9337af2e5149cbd16f3b971f274a0ab` |
| Verification workflow | `Repository verification`, run `31363490177`, run #171, event `push`, branch `main` |
| Job | `93376925232` — `Deterministic verification`, success |
| Artifact | `verification-31363490177`, ID `9053274210`, `395198` bytes |
| Digest | `sha256:50fd6cfef5cc776479391cffaa0a67abd6d6cdd4567dc3b6e12ad804f5f85b2a`, independently recomputed and matched |
| Build | 0 warnings / 0 errors |
| Tests | 1519 passed / 0 failed / 0 skipped |
| Gate 0 | PASS; Git object reader 52/52 PASS |
| Architecture / Domain dependencies | PASS; `packageReferences: []` |
| Verdict | PASS, `failureReasons: []` |

This document is a **historical audit of that exact SHA**. It is not a second volatile current-state cache
(`CURRENT_STATE.md` remains the only one, per D-008) and it is not a generated projection (D-009). Any later commit
requires revalidation before this audit may be relied upon again. Its authority is repository-operational under D-002;
it does not modify frozen Gate 0 and it records no accepted decision — `DECISIONS.md` stays unchanged because the open
questions below are the owner's to resolve (D-012).

Evidence used here is repository-native only: frozen Gate 0 documents, `data/*.yaml`, merged code at the audited SHA,
and merged pull requests. Chat and model memory are not authority (D-007).

## B. Gate-1 scope matrix

Frozen Gate-1 scope is `docs/PROTOTYPE_SCOPE.md` § "Gate 1 — чистая C# domain simulation", refined by
`docs/FIRST_SHIFT_SPEC.md`, `docs/ANOMALY_MATRIX.md`, `docs/LOG_STATE_MACHINE.md`, `docs/INTAKE_SCHEDULER.md` and the
Gate-1 test list in `docs/NETWORK_RULES.md`.

| # | Gate-1 lane | Status | Merged evidence at the audited SHA |
|---|---|---|---|
| 1 | Typed Domain + YAML configuration adapter | `SATISFIED` | PR #3; `src/TheLogsAreWrong.Domain/Configuration/ValidatedConfiguration.cs`, `src/TheLogsAreWrong.Config.Yaml/YamlConfigurationLoader.cs`; YAML stays an external adapter and the Domain exports no YAML type (`ArchitectureGuardTests`). |
| 2 | Host clock, deterministic tick, sequencing | `SATISFIED` | PR #4; `Time/SimulationTime.cs`, `Primitives/Primitives.cs`, `Sequencing/SequencingContracts.cs`. `EventSequence.None` vs initialized zero `StateVersion`/`ServerTick` preserved per D-003. |
| 3 | Event journal and state version | `SATISFIED` | PR #4 and PR #38; `Journal/EventJournal.cs`, `Journal/JournaledMutationCommitContracts.cs`. Append is fail-closed on sequence gap, tick regression and version skip. |
| 4 | Immutable shift and log state machine | `SATISFIED` | PR #22; `Runtime/ShiftRuntimeState.cs`, `Logs/LogTransitionPolicy.cs`. Terminal states are irreversible and node capacities are enforced. |
| 5 | Quota ledger and objective predicate | `SATISFIED` | PR #24; `Quota/QuotaContracts.cs`. `ObjectivesSatisfied` is exactly `pine ≥ 5 AND oak ≥ 4 AND correctly_processed_anomalies ≥ 2` from `data/shift_p0.yaml`, matching `FIRST_SHIFT_SPEC` § "Success predicate". |
| 6 | Anomaly processing, procedures, inventory, flags | `SATISFIED` | PRs #26, #28, #30; `Anomalies/AnomalyResolutionContracts.cs`, `Runtime/ProcedureCompletionContracts.cs`, `Runtime/ProcedureActionLifecycleContracts.cs`. Consumables are debited on completion for correct and configured-wrong actions alike, per `ANOMALY_MATRIX` § "Item consumption contract". |
| 7 | Confirmation lifecycle and line-noise integration | `SATISFIED` | PRs #32 and #71; `Runtime/ConfirmationTestLifecycleContracts.cs`. Penitent requires 4 continuous `QUIET` seconds, `LOUD` resets progress, and the intake timer is not paused — exactly `FIRST_SHIFT_SPEC` § "Line noise". |
| 8 | Containment state machine and ritual | `SATISFIED` | PR #34; `Containment/ContainmentLifecycleContracts.cs`. `STABLE → SERVICE_REQUESTED → OVERDUE → INCIDENT`, danger-weight intervals 90/75/60, 20 s grace, 10 s overdue, 4 s ritual hold. Incident *execution* is a separate row — see C.2. |
| 9 | Jam and repair lifecycle | `SATISFIED` | PRs #36, #56; `Line/LineJamRepairContracts.cs`, `Scheduler/RepairPendingTransitionExecutionContracts.cs`. One active cause; repair completion refuses while the blocker remains. |
| 10 | Feed, intake deadline, auto-route, saw scheduler | `SATISFIED` | PRs #42, #44, #46, #50, #52, #54, #58, #61, #63, #65; `Scheduler/**`. Matches the P0 parameters in `INTAKE_SCHEDULER.md`. |
| 11 | Movement noise and derived line noise | `SATISFIED` | PRs #67, #69; `Line/MovementNoiseRuntimeContracts.cs`, `Line/LineNoiseRuntimeContracts.cs`. `LOUD` iff saw, movement or repair is active. |
| 12 | Shift completion and exact hard-deadline checkpoint | `SATISFIED` | PRs #73, #75; `Runtime/ShiftCompletionContracts.cs`, `Runtime/HostTickCompletionCheckpointContracts.cs`. Sequential ticks are enforced; backward, skipped and post-completion ticks are rejected. |
| 13 | Accepted-intent ordering and every current P0 stage-2 action family | `SATISFIED` | PRs #79, #81, #97, #99, #102, #104; `Intents/AcceptedIntentBatchContracts.cs`, `Runtime/AcceptedIntentStageExecutionContracts.cs`. Nine action IDs: four manual routing plus early feed, procedure action, confirmation test, line repair and containment ritual. |
| 14 | Frozen seven-stage host composer | `SATISFIED` | PRs #83, #85, #87, #89, #91, #93, #95; `Runtime/HostTickExecutionContracts.cs`. Stage order equals `HostTickStages.CanonicalOrder` and `data/shift_p0.yaml` `same_tick_order`. |
| 15 | Stage-7 publication and journal evidence | `SATISFIED` | PR #93; `Runtime/HostStageSevenEventExecutionContracts.cs`. Twenty-four frozen event types; contiguous sequence and exact causation. |
| 16 | Full P0 Learning/Pressure scenario and repeatability evidence | `SATISFIED` | PR #106; `tests/…/Determinism/FullP0/**`. Learning correct path completes at tick 172 with the objective met; the cautious full-timeout policy completes at 782 < 840 under `learning` and fails to finish at exactly 600 under `pressure`; write-off-all-suspicious fails the objective; containment overlaps an intake task; ten independent runs are structurally equal. |
| 17 | Domain zero-package, Unity/FishNet/Steam/network independence | `SATISFIED` | `src/TheLogsAreWrong.Domain/TheLogsAreWrong.Domain.csproj` has no `PackageReference`; `ArchitectureGuardTests` and `Tlaw043ArchitectureTests` assert it, and exact-main `domainDependencies` reports `packageReferences: []`. |
| 18 | Consequences of processing errors — terminal state and quota | `SATISFIED` | Wrong Penitent/Resin reach `PROCESSED` with zero credit and zero anomaly delta; wrong False Species credits the declared species once. Proven end-to-end through the real host in PR #106. |
| 19 | Consequences of processing errors — the three timed/targeted effects | see C | `time_penalty`, `lock` and `forced_line_pause` are retained as descriptors only. Section C dispositions each one. |
| 20 | Snapshot and replay | `BLOCKED_REQUIRES_OWNER_DECISION` | See B.1 below. |

### B.1 Snapshot and replay — the one non-effect gap

Three frozen sources place snapshot/replay inside Gate 1:

- `docs/PROTOTYPE_SCOPE.md` § Gate 1 lists «Event journal, state version, snapshots и replay»;
- `docs/NETWORK_RULES.md` § "Тесты по gates → Gate 1" lists `Snapshot/replay`;
- `docs/LOG_STATE_MACHINE.md` § "Snapshot/replay" defines `ShiftSnapshot { shift_id, server_tick, state_version, last_event_sequence, scheduler_state, logs[], line_state, containment_state, inventory, quota, objectives }` and states that Gate 1 «умеет восстановить state из snapshot + events после `last_event_sequence`» and that «полный replay от начального manifest должен давать тот же итоговый snapshot».

What the audited SHA actually contains:

- `Journal/ReplayContracts.cs` defines `SnapshotBoundary` with exactly four fields — `ShiftId`, `ServerTick`, `StateVersion`, `LastEventSequence` — and `ReplayValidator`, which validates envelope **ordering** after a boundary and returns a typed `ReplayAnomaly`;
- `RuntimeCheckpointFactory.Capture` produces that same four-field boundary;
- PR #40 (TLAW-012) and PR #106 exercise it: the complete TLAW-042 journal validates from the zero boundary and from a mid-journal tail boundary;
- there is **no** `ShiftSnapshot` type carrying scheduler state, logs, line, containment, inventory, quota or objectives, and **no** reducer or reconstruction path. Issue #105 explicitly excluded a replay reducer from TLAW-042, and no earlier increment supplied one.

So the merged evidence proves *journal integrity and deterministic re-execution*, not *state reconstruction from a
snapshot*. Deterministic re-execution from the frozen configuration and the same scripted inputs (PR #106) is close to
«полный replay… даёт тот же итоговый snapshot» in effect, but it is re-execution rather than event replay and there is
no snapshot artifact to compare against. No repository decision defers the difference, so it is not
`DEFERRED_BY_FROZEN_GATE_BOUNDARY`; it is an open owner question. Options are in E.3.

## C. Effect-disposition matrix

Frozen `data/anomalies.prototype.yaml` and `data/shift_p0.yaml` retain four effect descriptors. `EffectType` is exactly
`{ time_penalty, lock, miscredit }` (`Enums/DomainEnums.cs`). At the audited SHA the Domain never branches on an effect
kind at all: the only production reference to an `EffectType` member is the YAML loader's shape validation of the
`duration_seconds`/`target` fields. Descriptors are carried on `ProcessingResolution.Effects` and
`ItemActionCompletionDescriptor.Effects` and published inside stage-7 payloads, and nothing consumes them.

| # | Effect | Trigger | Status |
|---|---|---|---|
| C.1 | `miscredit` / `CREDIT_TO_DECLARED_SPECIES` | False Species processed without `CORRECTLY_RELABELED` | `SATISFIED` |
| C.2 | `forced_line_pause`, 8 s | Containment reaches `INCIDENT` | `DEFERRED_BY_FROZEN_GATE_BOUNDARY` |
| C.3 | `time_penalty` / `FALSE_PA_ANNOUNCEMENT`, 8 s | Penitent processed without `SANITIZED_PENITENT` | `BLOCKED_REQUIRES_OWNER_DECISION` |
| C.4 | `lock` / `RESIN_BUTTON_LOCK`, target `nearest_line_button`, 10 s | Resin processed without `SEALED_RESIN`, and holy water applied to Resin before the saw | `BLOCKED_REQUIRES_OWNER_DECISION` |

### C.1 False Species `miscredit` — `SATISFIED`

The frozen sources define this effect entirely in quota terms. `FIRST_SHIFT_SPEC` § "Ложная порода" gives, without the
flag: terminal `PROCESSED`, «credit: заявленная порода ×1», `correctly_processed_anomalies +0`, effect type
`miscredit`. `ANOMALY_MATRIX` states «Incorrect saw: credit goes to declared species». `data/anomalies.prototype.yaml`
encodes `quota_credit: {species: declared_species, units: 1}`, `correct_anomaly_delta: 0` and a `miscredit` effect with
no `duration_seconds` and no `target` — the only one of the three anomaly effects that is untimed and untargeted.

Merged behaviour matches exactly and applies it once:

- `AnomalyProcessingResolver.Resolve` maps `declared_species` to `log.DeclaredSpecies` and emits **one**
  `QuotaSettlement(logId, declaredSpecies, 1, 0)`; `ProcessingResolution.Settlement` is a single settlement, not a
  collection;
- `SawQuotaApplicationService` forwards exactly that settlement to `QuotaSettlementService`, which refuses a repeat for
  an already-settled log (`QuotaSettlementDuplicate`) rather than crediting twice;
- PR #106 proves it through the real host: `log_05` processed unflagged yields declared species `pine` ×1, delta 0,
  final quota `pine = 1`, `correctly_processed_anomalies = 0`, accepted settlement `PriorSpeciesCredit 0 →
  CurrentSpeciesCredit 1`.

The descriptor is therefore a **label for a consequence that has already been applied**, not an instruction for a
second one. Nothing further is owed in Gate 1, and adding a separate miscredit executor would double-credit.

### C.2 Containment `forced_line_pause` — `DEFERRED_BY_FROZEN_GATE_BOUNDARY`

`FIRST_SHIFT_SPEC` § "Отстойник" heads this behaviour literally «Placeholder Gate 2 incident», and describes an
8-second forced line pause applied once, with the state remaining `INCIDENT` until the ritual completes and no repeat
incident before resolution. `PROTOTYPE_SCOPE` places «Сигнал/ритуал/placeholder incident отстойника» in the Gate 2
"Входит" list, not Gate 1.

The audited SHA behaves accordingly: `ContainmentAdvanceService` enters `INCIDENT` and builds a
`ContainmentIncidentDescriptor(type, duration, triggeredAt)` from configuration, stage 7 publishes it inside
`HostStageSevenContainmentPayload.Incident`, and no Domain source contains any line-pause state or execution. The
`prototype_incident` block in `data/shift_p0.yaml` still reads `type: forced_line_pause`, `duration_seconds: 8`,
`remains_incident_until_ritual: true`, `repeat_before_resolution: false`.

This row is closed by the frozen gate boundary itself and needs no owner decision. It must **not** be implemented in
Gate 1.

### C.3 Penitent `time_penalty` — `BLOCKED_REQUIRES_OWNER_DECISION`

Every frozen statement about this effect was located and is reproduced in full:

- `FIRST_SHIFT_SPEC` § "Кающийся ствол", without the flag: «распил разрешён; terminal state `PROCESSED`; output
  rejected, quota credit: 0; `FALSE_PA_ANNOUNCEMENT`; effect type: `time_penalty`; penalty: 8 секунд»;
- `ANOMALY_MATRIX`: «Incorrect saw: output rejected, no quota credit, `FALSE_PA_ANNOUNCEMENT`, 8-second time penalty.
  Effect class: `time_penalty`.» and the summary row "time penalty";
- `data/anomalies.prototype.yaml`: `{type: time_penalty, event: FALSE_PA_ANNOUNCEMENT, duration_seconds: 8}`.

That is the complete authoritative record: a name, an event identifier and a duration. None of the questions an
implementation must answer is decided anywhere:

| Question | Source answer |
|---|---|
| Does it move the hard shift deadline (840 / 600)? | not stated |
| Does it advance, skip or stall authoritative `ServerTick` progression? | not stated — and the frozen checkpoint rejects skipped ticks, so a naive "advance the clock" reading contradicts `HostTickCompletionCheckpointContracts` |
| Does it delay feed, intake, saw or repair work, and which of them? | not stated |
| Is it purely report/UI/scoring evidence? | not stated |
| Who is penalised — the shift, an actor, or a node? | not stated; Gate 1 has no actor-scoped runtime to penalise |
| How does it compose with an exact-deadline tick and with same-tick stage ordering? | not stated |

`source/the_logs_are_wrong_design_v5_2.md` cannot supply the answer: it is contradiction-resolution material only, and
its only nearby uses of a time penalty are a Gate-2 *placeholder suggestion* for the containment incident (§ "Для Gate 2
допускается простейшая заглушка… например, штраф времени") and an explicitly excluded death-scoring idea («В прототип
не входит»). `notes/GAMEPLAY_WORKING_HYPOTHESES.md` and Issue #100 / BAR-82 are non-authoritative by Issue #107.

The descriptor name is not semantics. Implementing eight seconds of anything here would be invention, and each
candidate reading produces a materially different `P0_SHIFT_A` outcome — a deadline shift changes the 840/600 results
directly, whereas a report-only reading changes nothing. Owner decision required; options in E.1.

### C.4 Resin `lock` / `nearest_line_button` — `BLOCKED_REQUIRES_OWNER_DECISION`

Every frozen statement was located:

- `FIRST_SHIFT_SPEC` § "Смоляной святотатец", without the flag: «`RESIN_BUTTON_LOCK`; effect type: `lock`; duration:
  10 секунд», plus «Применение holy water до распила также вызывает `RESIN_BUTTON_LOCK`, но объект остаётся в текущем
  состоянии и всё ещё может быть правильно обработан»;
- `ANOMALY_MATRIX`: «Incorrect saw: … `RESIN_BUTTON_LOCK` for 10 seconds. Wrong holy water before saw: same lock,
  object remains processable. Effect class: `lock`.»;
- `data/anomalies.prototype.yaml`: `{type: lock, event: RESIN_BUTTON_LOCK, target: nearest_line_button,
  duration_seconds: 10}`.

Again: a name, an event, a duration and an untyped target string. Undecided:

| Question | Source answer |
|---|---|
| Which button or action is `nearest_line_button`? | not stated. **Gate 1 has no referent at all**: the Domain contains no button entity, no control surface and no actor position — the only `Position` in the Domain is the replay-anomaly index in `ReplayContracts.cs`. "Nearest" cannot be evaluated in a Gate-1 state. |
| Does it block one action family or several? | not stated |
| Is it global or scoped to the acting actor? | not stated; intents carry an `ActorId`, but no per-actor runtime exists to hold a lock |
| When does the interval start and end, in exact ticks? | not stated |
| What happens to an intent already accepted for the same tick? | not stated; would interact with the frozen stage-2 receive-sequence ordering |
| Does it affect automatic host work (auto-route, feed, saw start), or only player intents? | not stated |
| What are recovery/expiry semantics? | not stated |

`source/the_logs_are_wrong_design_v5_2.md` § 7.3 describes distributed shop-floor buttons as a spatial Gate-2+ layer;
it is contradiction-resolution material and cannot introduce a Gate-1 control-surface model. The one genuinely frozen
Gate-1 consequence — that a Resin log remains processable after wrong holy water, with the charge consumed — is already
proven through the real host in PR #106.

Owner decision required; options in E.2.

## D. Exit verdict

```text
GATE1_EXIT_BLOCKED_OWNER_DECISION_REQUIRED
```

Three rows are unresolved: C.3 `time_penalty`, C.4 `lock / nearest_line_button` and B.20 snapshot/replay. Every other
material Gate-1 lane is `SATISFIED` against merged evidence, and C.2 is closed by the frozen gate boundary.

This audit does not declare Gate 1 complete and does not choose any option below. A descriptor-only test is recorded as
proof that the descriptor is *retained*, never as proof that the corresponding mechanical rule is *complete*.

## E. Smallest bounded options for the owner

Only the smallest alternatives supported by the current architecture are listed. This audit selects none of them.

### E.1 Penitent `time_penalty`

- **A.** Freeze descriptor-only treatment as sufficient for Gate 1: the 8-second penalty is recorded evidence
  (retained descriptor plus the existing zero-credit outcome) and its mechanical execution is assigned to a named later
  gate. Cost: no code change; the audit row becomes `DEFERRED_BY_FROZEN_GATE_BOUNDARY` once the gate is named.
- **B.** Freeze exact deterministic mechanics — precisely which of deadline, tick progression, scheduler work or
  report-only is affected, and how it composes with the exact 600/840 evaluation and sequential checkpoint — and
  authorize a separate effect-runtime increment. Cost: one bounded production increment plus rework of the frozen P0
  scenario expectations that the penalty would shift.

### E.2 Resin `lock / nearest_line_button`

- **A.** Freeze descriptor-only treatment as sufficient for Gate 1 and defer the lock to the gate that introduces the
  control-surface/spatial model it needs. Cost: no code change.
- **B.** Freeze an exact Gate-1-expressible substitute target and its full semantics — the action family blocked, the
  scope, the exact tick interval, same-tick and automatic-work behaviour, and expiry — and authorize a separate
  effect-runtime increment. Cost: one bounded production increment; note that `nearest_line_button` itself cannot be
  evaluated until a button/position model exists, so B requires either a substitute target or that model first.

### E.3 Snapshot and replay

- **A.** Freeze the current scope — a four-field `SnapshotBoundary` plus ordering validation and deterministic
  re-execution — as the intended Gate-1 meaning of «snapshots и replay», and assign full `ShiftSnapshot` capture and
  reconstruction to Gate 3, where `NETWORK_RULES` § "Snapshot/resync" first needs it. Cost: no code change.
- **B.** Authorize a separate increment implementing the `ShiftSnapshot` shape from `LOG_STATE_MACHINE`, capture and
  restore, plus the "full replay equals final snapshot" proof. Cost: one bounded production increment; note that
  Issue #105 deliberately excluded a replay reducer, so this would be new production surface.

## F. Post-Gate-1 sequence

Source-backed only, recorded and not started:

1. Gate 1 accepted.
2. Isolated package smoke-test of the Unity/FishNet/FishySteamworks/Steamworks.NET pin matrix —
   `GATE_0_EXIT_CHECKLIST` § Network, «After Gate 1: isolated package smoke-test» (unchecked), and
   `PROTOTYPE_SCOPE` § Gate 3 «Отдельный smoke-test pin-матрицы».
3. Exact package pins accepted — `GATE_0_EXIT_CHECKLIST` § Network, «After smoke-test: exact pins accepted»
   (unchecked). The stack remains `PROPOSED` until then (`NETWORK_RULES` § "Proposed stack").
4. Gate 2 — single Unity process with one local authoritative host, no FishNet (`PROTOTYPE_SCOPE` § Gate 2;
   `NETWORK_RULES`: «FishNet не является зависимостью Gate 1 или Gate 2»).
5. Gate 3 — Steam listen-server for two real players (`PROTOTYPE_SCOPE` § Gate 3).

No package research, install, smoke-test or Gate 2 work is performed by this audit.
