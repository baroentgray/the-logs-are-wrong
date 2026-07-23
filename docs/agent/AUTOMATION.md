# Automation and routing blueprint

This is a reviewed repository copy of the routing blueprint. BAR-34 implements deterministic local executor selection after task-packet preparation and before an explicit local lease lifecycle; it is not a dispatcher implementation.

1. A human or other approved read-only process prepares a local normalized JSON snapshot from authoritative references and an exact base SHA.
2. Run `dotnet run --configuration Release --project tools/Tlaw.Dispatcher -- packet --input <normalized-input.json> --output <task.yaml>` from the repository root.
3. The tool creates a compact `tlaw.agent-task/v2` YAML packet with links and paths, never copied Issue bodies, validates it, and replaces the output atomically only after validation succeeds.
4. A human may explicitly run `route` with a closed local agent snapshot. It validates an unclaimed task v2 packet, selects one eligible/capable/policy-permitted agent by availability-first ordering, and emits an internal `selection.json`; it does not create a claim or launch an agent.
5. A human inspects `selection.json`, then may explicitly invoke `lease acquire` with its `selected_agent` and an absolute local store path. The tool atomically prepares a validated claimed packet with a fencing token; it does not contact or launch that executor.
6. A human may explicitly run `ingest-result` with one claimed task/v2 packet, one result/v1 packet, and the existing local lease store. It accepts evidence only when task id, claimed agent, fencing token, and current lease state match exactly; it emits an internal record and the existing concise result projection without releasing the lease or changing any status.
7. A human may explicitly run `finalize-result` for a non-human `success` or `failed` result after ingestion. It releases only the matching active local lease and writes a deterministic record for a future adapter; it does not move Linear or complete a task.
8. A human reviews the selected, prepared, claimed, ingested, or finalized record before any later, separately approved workflow step.

## Current limitations

This increment does not contact or mutate Linear; probe live availability or quota; launch Codex, Claude, Grok, or Qwen; ingest review or handoff packets; write GitHub; or merge anything. Result ingestion is strictly local evidence correlation: it does not release a lease, select a fallback, transition a task, or imply completion. There are no provider adapters, fallback execution, or task-status transitions. Routing consumes only a provided snapshot: `AVAILABLE` ranks above `DEGRADED`, then a selectable preferred agent, then declared eligible order; `QUOTA_EXHAUSTED`, `OFFLINE`, and `UNKNOWN` are excluded. Overrides are explicit and recorded, cannot add capability or autonomy, and cannot make an excluded agent selectable without an explicit valid availability override. Preparing a packet, selecting an executor, acquiring/releasing a local lease, or ingesting a result does not complete BAR-26 or transition a card.

Codex implements; Claude plans/reviews and can be a fallback executor; local tools prepare read-only evidence; Grok console/CLI supports experiments, research, red-team, and alternative review. No entry here launches an agent, locks a provider, changes authority, or grants implementation permission. `tlaw packet` is preparation only; `dispatch`, `review`, and `handoff` workflows require separate approval.
