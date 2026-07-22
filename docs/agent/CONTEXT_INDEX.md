# Agent context index

`AGENTS.md` is the binding operational entry point. This index is the required second read for a new agent; it does not replace a frozen Gate document, a merged GitHub contract, or a Linear issue.

Read in this order:

1. `AGENTS.md`.
2. This file, then `CONTEXT.md` for stable project facts.
3. Frozen Gate 0 documents named by `context-manifest.json` when the work touches their subject.
4. Merged GitHub code, contracts, PR evidence, CI, and the exact SHA for executable truth.
5. The relevant Linear issue for current queue and status.
6. `STATUS.md` only as a timestamped, non-authoritative cache.
7. `AGENT_PROTOCOL.md` and its v1 schema before producing or ingesting a YAML packet.

Source hierarchy is strict: frozen Gate documents and approved architecture → merged GitHub code/contracts/PRs/CI/exact SHA → Linear queue/status → this repository knowledge pack → generated status cache → chat/model memory. A lower source cannot override a higher one.

## Maintenance policy

Repository maintainers own stable context and protocol evolution; an agent may update them only through a reviewed task branch. Update stable files after an approved architecture decision or merged executable-contract change. Refresh only `STATUS.md` after a deterministic verification snapshot. Keep volatile facts in that one cache, not duplicated in the other files. Add corrections to `DECISIONS.md` as a new entry. Introduce a new schema identifier for incompatible protocol changes; do not silently edit a v1 contract. Missing or stale generated status is visible evidence, never current state by default.
