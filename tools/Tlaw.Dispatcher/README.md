# TLAW deterministic packet preparation

`Tlaw.Dispatcher` implements BAR-26 preparation plus Increment 2's local lease boundary. It can read one local normalized JSON snapshot and emit a validated `tlaw.agent-task/v2` YAML file, then atomically prepare one claimed copy under a local persistent lease. It is not a live dispatcher.

Run from the repository root:

```powershell
dotnet run --configuration Release --project tools/Tlaw.Dispatcher -- packet --input <normalized-input.json> --output <task.yaml>
```

The command returns `0` and prints `PASS` only after the generated YAML validates through `Tlaw.AgentProtocol`. It returns non-zero with a concise `FAIL:` diagnostic for malformed input, invalid packet policy, invalid generated YAML, or I/O failure. It writes a UTF-8, no-BOM temporary file in the target directory and atomically replaces the requested output only after validation. Generated packet bytes always use LF line endings, including on Windows. It never treats a failed generation as a dispatch.

For a subsequent standalone `Tlaw.AgentProtocol validate` command, place the output under this checkout (for example `artifacts/task.yaml`); that existing CLI discovers the repository schemas by walking up from the packet path. The generator itself validates before writing and may write to any existing local directory.

## Normalized input

Input is local JSON, not a live Linear or GitHub response. Its top-level object is closed: unknown fields, including copied issue-body fields, are rejected. The required `schema` value is `tlaw.dispatcher-input/v1`; the remaining fields map directly to task v2.

```json
{
  "schema": "tlaw.dispatcher-input/v1",
  "task_id": "BAR-26-increment-1",
  "source_id": "BAR-26",
  "sources": ["https://linear.app/baronet/issue/BAR-26/tlaw-auto-001-linear-driven-agent-dispatcher-mvp", "docs/agent/AGENT_PROTOCOL.md"],
  "objective": "Generate a validated task packet.",
  "work_type": "implementation",
  "preferred_agent": "codex",
  "eligible_agents": ["codex", "claude"],
  "required_capabilities": ["dotnet", "yaml_protocol"],
  "autonomy_level": "branch_write",
  "forbidden_operations": ["Merge pull requests.", "Write main.", "Contact Linear."],
  "claimed_by": "unclaimed",
  "claim_id": "unclaimed",
  "claim_started_at": "unclaimed",
  "claim_expires_at": "unclaimed",
  "base_sha": "c4ad144fac76584faf7948956c172e20df9a5a79",
  "handoff_required": true,
  "worktree": "task/BAR-26-dispatcher-packet-generation",
  "verification": {
    "required": true,
    "commands": ["dotnet test --configuration Release"]
  },
  "delivery": {
    "branch_required": true,
    "draft_pr_required": true,
    "merge_forbidden": true
  }
}
```

Array order is supplied by the normalized snapshot and preserved. YAML keys are always emitted in the v2 schema order, values are safely quoted, and identical input produces byte-identical output. `sources` must be HTTPS references or repository-relative paths; they are references, not copied source bodies. Do not put secrets or credentials in the snapshot.

## Policy boundary

The v2 contract validates the closed work-type, agent, and autonomy enums. It records one shared autonomy policy for all eligible agents, so a future fallback cannot grant more autonomy. The current policy allows Codex and Claude for implementation, keeps Grok out of implementation, and permits the local model only for read-only analysis with `read_only` autonomy. `merge_forbidden` is required to be true.

The four claim fields have exactly two states. An unclaimed task uses `unclaimed` for every field. A claimed task names one eligible agent, has a non-sentinel unique fencing token, and uses canonical UTC RFC 3339 timestamps in `yyyy-MM-dd'T'HH:mm:ss.fffffffZ` form with a strictly later expiry. Implementation claims by the local model or Grok remain invalid under the existing routing policy.

## Local lease lifecycle

The Increment 2 lease store is an explicitly supplied absolute local directory. It is authoritative only for the local lease lifecycle; it never stores repository, GitHub, Linear, provider, or other credentials. State is one atomic JSON record per task identity, protected by an exclusive per-task filesystem lock. The record survives process restart, different task identities use independent locks, and a malformed or unreadable record fails closed.

Run these commands from the repository root:

```powershell
dotnet run --configuration Release --project tools/Tlaw.Dispatcher -- lease acquire --task <unclaimed-task-v2.yaml> --store <absolute-lease-store> --executor <eligible-agent> --ttl 00:05:00 --output <claimed-task-v2.yaml>
dotnet run --configuration Release --project tools/Tlaw.Dispatcher -- lease status --task-id <task-id> --store <absolute-lease-store>
dotnet run --configuration Release --project tools/Tlaw.Dispatcher -- lease release --task-id <task-id> --store <absolute-lease-store> --claim-id <claim-id> --reason <completion|error|timeout|quota_exhaustion|manual_cancel>
```

`lease acquire` first validates an unclaimed v2 packet, requires the explicitly named executor to be eligible, atomically reserves the task, creates a unique `claim_id` fencing token and canonical UTC timestamps, validates the claimed packet, then atomically publishes its LF/no-BOM output. It returns success only when both the lease and output are valid. If output publication fails after reservation, it releases that exact active claim; if that rollback cannot succeed, it reports the active `claim_id` and an explicit recovery command.

An active lease rejects a second acquisition. A lease that is expired may be atomically taken over, but its old fencing token can no longer release or affect the replacement. Release requires the exact active token and one closed reason; completion release does not mark any task Done. The store is runtime state outside the repository and must never be committed.

## Deliberate limitations

This tool does not contact or mutate Linear, dynamically choose a live executor, probe availability or quota, launch Codex/Claude/Grok/Qwen, ingest result/review/handoff packets, write GitHub, merge, or update task state. It performs no network write. Packet preparation and local lease acquisition are not dispatch; any routing, provider adapter, agent launch, result ingestion, review routing, or merge behavior needs a separately approved increment. BAR-26 is not complete after this increment.
