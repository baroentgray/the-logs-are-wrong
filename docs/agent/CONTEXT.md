# TLAW stable context

Public title: **THE LOGS ARE WRONG**. Tagline: **Quota still applies.** Namespace: `TheLogsAreWrong`; internal shorthand: `TLAW`.

## Authority and Gates

Gate 0 is closed and protected from accidental edits. Its frozen documents and approved architecture are the highest repository authority. Gate 1 Domain remains independent of `UnityEngine`; no knowledge-pack file grants gameplay, networking, scheduler, clock, journal, state-machine, or provider permission.

The canonical source hierarchy is recorded in `CONTEXT_INDEX.md`. Treat GitHub merged code and the exact reviewed SHA as the executable contract; use Linear for work queue/status; treat this pack as operational context and `STATUS.md` as a non-authoritative cache.

## Preserved merged semantics

`EventSequence` uses zero as `None`/unassigned, while `StateVersion` and `ServerTick` retain initialized zero as a real value. Preserve that distinction; it was clarified for the time/event-journal increment in [Issue #2](https://github.com/baroentgray/the-logs-are-wrong/issues/2) and [PR #4](https://github.com/baroentgray/the-logs-are-wrong/pull/4).

## Execution model

Temporary executors are routed dynamically; provider availability and quota are configuration, not a repository permission expansion. Current roles are: Codex for implementation; Claude for planning/review and fallback implementation; local tools for read-only preparation; and Grok console/CLI for model or asset experiments, research, red-team work, and alternative review until the implementation benchmark is met. Grok is console/CLI-capable; an adapter or doctor check owns exact invocation details.

Manual merge is the default while the user is present. Deterministic automated merge is a future mode; agent merge is only an optional unattended fallback after explicit authorization.

## Stable non-goals

Do not duplicate the full backlog, launch agents automatically, claim generated snapshots are authoritative, rewrite frozen Gate 0, implement gameplay, or expand local-model/Grok implementation permissions. Humans use ordinary language; agents exchange the compact v1 YAML envelopes described in `AGENT_PROTOCOL.md`.
