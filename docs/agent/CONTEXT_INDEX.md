# Agent context index

`AGENTS.md` is the binding operational entry point. This index is the required second read for a new agent; it does not replace a frozen Gate document, a merged GitHub contract, or a Linear issue.

Read in this order:

1. `AGENTS.md`.
2. This file, then `CONTEXT.md` for stable project facts.
3. `HANDOFF.md` for the compact chat-rotation starting point.
4. `CURRENT_STATE.md` and `ACTIVE_RUNS.md` only as non-authoritative volatile projections.
5. Frozen Gate 0 documents named by `context-manifest.json` when the work touches their subject.
6. Merged GitHub code, contracts, PR evidence, CI, and the exact SHA for executable truth.
7. The relevant Linear issue for current queue and status.
8. `DECISIONS.md` for append-only repository-operational decisions.
9. `AGENT_PROTOCOL.md` and its schemas before producing or validating YAML records.

Source hierarchy is strict: frozen Gate documents and approved architecture → merged GitHub code/contracts/PRs/CI/exact SHA → Linear queue/status → this repository knowledge pack → generated volatile projections → chat/model memory. A lower source cannot override a higher one.

## Maintenance policy

Repository maintainers own stable context and protocol evolution; an agent may update them only through a reviewed task branch. Update stable files after an approved architecture decision or merged executable-contract change. `CURRENT_STATE.md` is the single volatile current-state cache; `ACTIVE_RUNS.md` and `HANDOFF.md` are generated non-authoritative projections. Refresh them only at their documented safe workflow checkpoints, after live GitHub, Linear, and CI validation and before no mutation is inferred from them. Add corrections to `DECISIONS.md` as a new entry. Introduce a new schema identifier for incompatible protocol changes; do not silently edit a v1 contract. A missing or stale projection is visible evidence only and is never permission to guess.
