# Automation and routing blueprint

This is a reviewed repository copy of the routing blueprint. BAR-26 Increment 2 implements deterministic task-packet preparation plus an explicitly local, persistent lease lifecycle; it is not a dispatcher implementation.

1. A human or other approved read-only process prepares a local normalized JSON snapshot from authoritative references and an exact base SHA.
2. Run `dotnet run --configuration Release --project tools/Tlaw.Dispatcher -- packet --input <normalized-input.json> --output <task.yaml>` from the repository root.
3. The tool creates a compact `tlaw.agent-task/v2` YAML packet with links and paths, never copied Issue bodies, validates it, and replaces the output atomically only after validation succeeds.
4. A human may explicitly invoke `lease acquire` with an eligible executor and an absolute local store path. The tool atomically prepares a validated claimed packet with a fencing token; it does not contact or launch that executor.
5. A human reviews the prepared or claimed packet before any later, separately approved routing or execution step.

## Current limitations

This increment does not contact or mutate Linear; dynamically select a live executor from availability; probe quota; launch Codex, Claude, Grok, or Qwen; ingest result, review, or handoff packets; write GitHub; or merge anything. It has no provider adapters, no fallback execution, and no task-status transition. A future approved increment may consume a validated packet, but cannot infer authority beyond its declared policy. Preparing a packet or acquiring/releasing a local lease does not complete BAR-26 or transition a card.

Codex implements; Claude plans/reviews and can be a fallback executor; local tools prepare read-only evidence; Grok console/CLI supports experiments, research, red-team, and alternative review. No entry here launches an agent, locks a provider, changes authority, or grants implementation permission. `tlaw packet` is preparation only; `dispatch`, `review`, and `handoff` workflows require separate approval.
