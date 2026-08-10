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
