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
