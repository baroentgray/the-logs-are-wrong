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

## Deterministic local routing

BAR-34 adds a local selection step. It is not dispatch: it does not contact or launch an agent, read or write a lease, alter a task packet claim, probe a provider, or change Linear/GitHub/task state.

```powershell
dotnet run --configuration Release --project tools/Tlaw.Dispatcher -- route --task <unclaimed-task-v2.yaml> --agents <agent-snapshot.json> --output <selection.json> [--executor-override <agent>] [--availability-override <agent>=<STATE>]...
```

`--task`, `--agents`, and `--output` are required exactly once. `--executor-override` is optional and may appear once. Only `--availability-override` may repeat, and each agent may be overridden at most once. Invalid, unknown, duplicate, malformed, or empty options fail with a concise non-zero `FAIL:` diagnostic. Successful routing prints only `SELECTED: <agent> (<STATE>)`.

The closed local agent snapshot is strict UTF-8 JSON:

```json
{
  "schema": "tlaw.dispatcher-agent-snapshot/v1",
  "agents": [
    {
      "agent": "codex",
      "capabilities": ["dotnet", "yaml_protocol"],
      "availability": "AVAILABLE"
    }
  ]
}
```

Agent names are exactly `codex`, `claude`, `grok`, or `local`; availability is exactly `AVAILABLE`, `DEGRADED`, `QUOTA_EXHAUSTED`, `OFFLINE`, or `UNKNOWN`. The root and agent objects are closed, duplicate JSON properties/agent records/capabilities fail closed, and every task-eligible agent requires one snapshot record. No capability or availability is inferred.

Only `AVAILABLE` and `DEGRADED` candidates can be selected. `AVAILABLE` ranks above `DEGRADED`; within one rank, the still-selectable `preferred_agent` wins, then the task's declared `eligible_agents` order breaks ties. A candidate must be eligible, capable, and policy-permitted. An executor override must meet the same checks and cannot bypass an excluded availability state; a separate valid availability override is required. Overrides alter only effective availability and are recorded in the output.

The output is an internal `tlaw.dispatcher-selection/v1` JSON record, not an AgentProtocol envelope. It has stable property order: `schema`, `task_id`, `selected_agent`, `effective_availability`, `executor_override_applied`, and ordered `availability_overrides`. It is UTF-8 without BOM, LF-terminated, deterministic for identical effective input, and atomically replaces the requested output only after validation and selection succeed.

The reviewed human flow is: validate an unclaimed task, run and inspect `selection.json`, then explicitly invoke `lease acquire` with its `selected_agent`. Routing never calls `FileLeaseStore`.

## Correlated local result ingestion

BAR-35 adds a local evidence-ingestion step. It is not lease finalization, execution, launch, dispatch, a Linear transition, a GitHub write, or a merge.

```powershell
dotnet run --configuration Release --project tools/Tlaw.Dispatcher -- ingest-result --task <claimed-task-v2.yaml> --result <result-v1.yaml> --lease-store <absolute-lease-store> --output <ingestion.json>
```

Every option is required exactly once. Before it reads either packet, the command rejects an output path that aliases the task, result, or any lease-store path after full-path normalization and available symbolic-link/junction resolution. It validates the strict UTF-8 task and result packets through the repository-native protocol validator; requires task/v2 to be fully claimed and result/v1 to have exactly the same `task_id`; and reads the existing lease record without changing it. The task id, `claimed_by`, and fencing `claim_id` must match one active, unexpired lease. The existing per-task lease lock is held while that evidence is checked and rechecked immediately before publication. Missing, held, expired, corrupt, duplicate-property, or contradictory lease evidence fails closed.

Only after those checks succeed does the command atomically replace `ingestion.json`. The UTF-8/no-BOM, LF-terminated internal `tlaw.dispatcher-ingestion/v1` record has stable fields in this order: `schema`, `task_id`, `claimed_by`, `claim_id`, `result_status`, `result_sha256`, `human_required`, `projection`. `result_sha256` is the lowercase SHA-256 of the exact validated result bytes. It preserves task, result, lease-store, and any prior output bytes on failure.

Stdout is only the existing concise `ResultProjector` output. A required human pause exposes only summary, question, evidence references, and safe options; a non-human result exposes only status and summary. If stdout fails after the durable record is published, the command reports a non-zero result and does not roll back that record. Ingestion never changes a failed or blocked result to success, releases/renews/replaces a lease, chooses another executor, or moves a task status. BAR-35 is Increment 4 only; BAR-26 remains incomplete.

## Deterministic local result finalization

BAR-36 adds the explicit local closeout step; it is not a Linear transition, provider action, launch, GitHub write, merge, or dispatch.

```powershell
dotnet run --configuration Release --project tools/Tlaw.Dispatcher -- finalize-result --task <claimed-task-v2.yaml> --result <result-v1.yaml> --ingestion <ingestion.json> --lease-store <absolute-lease-store> --output <finalization.json>
```

The command strictly validates all three evidence files and requires exact task id, claimed agent, fencing token, result status, human flag, and result-byte SHA-256 agreement with the active unexpired lease. It rejects output aliases to every input and any lease-store path before release. `success` with no human pause releases with `completion` and records `in_review`; `failed` with no human pause releases with `error` and records `todo`. `blocked` or human-required results leave the lease and output unchanged.

The UTF-8/no-BOM, LF-terminated `tlaw.dispatcher-finalization/v1` JSON record orders `schema`, `task_id`, `claimed_by`, `claim_id`, `result_status`, `result_sha256`, `release_reason`, and `next_state`. It is rendered before release, then the exact lease is released before atomic publication. This is not transactional: if publication fails after release, the command returns non-zero, states that the lease was already released and the record was not published, and never recreates the lease. BAR-36 is Increment 5 only; BAR-26 remains incomplete.

## Deterministic review ingestion

BAR-37 adds a local review-evidence step. It is not a lease operation, a live GitHub or pull-request lookup, a merge, a launch, a Linear transition, or a dispatcher.

```powershell
dotnet run --configuration Release --project tools/Tlaw.Dispatcher -- ingest-review --task <claimed-task-v2.yaml> --finalization <finalization.json> --review <review-v1.yaml> --expected-head <40-lowercase-hex> --output <decision.json>
```

All five options are required exactly once. The command validates a fully claimed task/v2, a closed successful `tlaw.dispatcher-finalization/v1` completion record, and a strict UTF-8 review/v1 packet. Task id, claimed agent, and fencing token must agree across the task and finalization; the review task id and its 40-character lowercase `reviewed_head` must agree with the explicit `--expected-head`. The command reads the review bytes once and records their lowercase SHA-256. It rejects output aliases to task, finalization, or review after normalizing available symbolic links and junctions.

The internal LF/no-BOM `tlaw.dispatcher-review-decision/v1` record orders `schema`, `task_id`, `reviewed_head`, `review_sha256`, `verdict`, `highest_severity`, `blocking_findings`, `decision`, and `next_state`. `approve` without blocker/high/medium findings emits `merge`/`in_review`; `request_changes` with one or more blocker/high/medium findings emits `correction`/`todo`; `comment` always emits `human`/`in_review`. Contradictory verdict/severity evidence fails closed. It atomically publishes the decision before printing only `REVIEW: <decision>`; a stdout failure after publication remains non-zero and explicitly says the record was already published. The record is evidence for a future human or adapter action, never an actual status transition or merge.

## Deterministic handoff preparation

BAR-38 adds `prepare-handoff --task <claimed-task-v2.yaml> --snapshot <handoff-input.json> --output <handoff-v2.yaml>`. It validates a fully claimed task/v2 and a closed UTF-8 `tlaw.dispatcher-handoff-input/v1` snapshot, derives task identity/base/branch only from the task, and atomically writes validated `tlaw.agent-handoff/v2` YAML. Snapshot evidence supplies only status, head/commits, ordered work and paths, commands, evidence, failures/questions, summary, and next action. It does not inspect live Git, use a lease, select a successor, launch anything, mutate Linear/GitHub, ingest a handoff, or merge. BAR-38 is Increment 7 only; BAR-26 remains incomplete.

## Deliberate limitations

This tool does not contact or mutate Linear, probe live availability or quota, launch Codex/Claude/Grok/Qwen, ingest handoff packets, write GitHub, merge, or update task state. It performs no network write. Packet preparation, local routing, local lease acquisition, result ingestion, result finalization, and review ingestion are not dispatch; provider adapters, agent launch, review routing, and merge behavior need separately approved increments. BAR-37 does not complete BAR-26.
