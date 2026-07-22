# TLAW deterministic packet preparation

`Tlaw.Dispatcher` implements the BAR-26 Increment 1 preparation boundary. It reads one local normalized JSON snapshot and emits one validated `tlaw.agent-task/v2` YAML file. It is not a live dispatcher.

Run from the repository root:

```powershell
dotnet run --configuration Release --project tools/Tlaw.Dispatcher -- packet --input <normalized-input.json> --output <task.yaml>
```

The command returns `0` and prints `PASS` only after the generated YAML validates through `Tlaw.AgentProtocol`. It returns non-zero with a concise `FAIL:` diagnostic for malformed input, invalid packet policy, invalid generated YAML, or I/O failure. It writes a UTF-8, no-BOM temporary file in the target directory and atomically replaces the requested output only after validation. It never treats a failed generation as a dispatch.

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

The four claim fields must either all be `unclaimed` or all describe one externally prepared claim snapshot. This tool does not acquire, store, renew, release, or otherwise act on a claim.

## Deliberate limitations

This tool does not contact or mutate Linear, acquire or release leases, choose a live executor based on availability, launch Codex/Claude/Grok/Qwen, ingest result/review/handoff packets, write GitHub, merge, or update task state. It performs no network write. Any routing, provider adapter, result ingestion, review routing, or merge behavior needs a separately approved increment.
