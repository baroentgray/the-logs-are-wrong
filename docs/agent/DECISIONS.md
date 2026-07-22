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
