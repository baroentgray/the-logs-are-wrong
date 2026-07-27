# Agent protocol v1, task v2, and handoff v2

Agent packets are UTF-8 YAML interchange/audit records. A human speaks ordinary language; an agent serializes a compact, validated packet only when a task, result, review, or handoff needs transfer.

## Envelopes

| Envelope | Schema | Closed status/verdict |
| --- | --- | --- |
| Legacy task | `tlaw.agent-task/v1` | N/A |
| Prepared task | `tlaw.agent-task/v2` | `work_type`, agent and autonomy policy enums |
| Result | `tlaw.agent-result/v1` | `success`, `blocked`, `failed` |
| Review | `tlaw.agent-review/v1` | `approve`, `request_changes`, `comment` |
| Handoff | `tlaw.agent-handoff/v1` | `ready`, `blocked` |
| Prepared handoff | `tlaw.agent-handoff/v2` | `ready`, `blocked` |

The JSON schemas and positive/negative examples live under `schemas/`. Validate before dispatch and after ingestion with:

```powershell
dotnet run --configuration Release --project tools/Tlaw.AgentProtocol -- validate docs/agent/schemas/examples/result.valid.yaml
dotnet run --configuration Release --project tools/Tlaw.AgentProtocol -- project-result docs/agent/schemas/examples/result.valid.yaml
```

## Safe YAML subset and evidence

Only one mapping-root document is accepted. Anchors, aliases, custom tags, merge keys, duplicate keys, unknown schema versions, unknown fields, and missing required evidence fail visibly. The tool performs no object deserialization. Emit keys in the schema property order and use source URLs or repository paths instead of copied Issue bodies.

Every result, review, and handoff has `human_summary` with at most five non-empty lines. A result needs at least one evidence item (`command`, `source`, `file`, or `ci`); a `success` claim without it is invalid.

Every review has `reviewed_head`: exactly 40 hexadecimal characters naming the reviewed Git commit. v1 validates this recorded evidence structurally; a later merge policy may compare it with a live pull-request head.

## Operational checkpoint projections

`CURRENT_STATE.md`, `ACTIVE_RUNS.md`, and `HANDOFF.md` are repository-backed
operational projections, not agent envelopes and not sources of authority.
Their YAML front matter uses the closed schemas `tlaw.current-state/v1`,
`tlaw.active-runs/v1`, and `tlaw.chat-handoff/v1`. Validate a document with:

```powershell
dotnet run --configuration Release --project tools/Tlaw.AgentProtocol -- validate-document docs/agent/HANDOFF.md
```

The same safe YAML subset applies. Active-run identities must be unique and in
ascending ordinal order. A prepared-target snapshot must say so explicitly and
never claim that an unmerged candidate is merged. `CURRENT_STATE.md` is the one
volatile current-state cache; the other two documents are generated projections.

The documented refresh triggers are `TASK_CREATED`,
`IMPLEMENTATION_CANDIDATE_READY`, `AUTHORITATIVE_REVIEW_COMPLETE`,
`CORRECTION_CANDIDATE_READY`, `MERGED_AND_VERIFIED`, and
`CHAT_HANDOFF_CREATED`. They are safe workflow checkpoints only: this tooling
does not automatically write GitHub or Linear, launch agents, monitor
continuously, or create chats.

## Handoff v2 preparation

`tlaw.agent-handoff/v1` is immutable. `tlaw.agent-handoff/v2` is an explicitly required compatible envelope for complete reassignment/continuation evidence; there is no v1/v2 conversion. It records task-derived `task_id`, `source_id`, claim identity, `base_sha`, and branch, plus snapshot-derived status, head/ordered commits, work, normalized changed paths, commands, evidence, failures/questions, summary, and next action.

`head_sha` and every commit are lowercase non-sentinel 40-character SHAs. An empty commit list requires `head_sha == base_sha`; otherwise its final entry equals `head_sha`. Changed paths are unique repository-relative forward-slash paths. A blocked handoff requires a known failure or open question. Preparation validates and atomically publishes LF/no-BOM YAML, but never inspects Git, leases, a successor, providers, Linear, or GitHub. BAR-38 is Increment 7 only: Increment 8 separately ingests and correlates handoff evidence; BAR-26 remains incomplete.

## Task v2 preparation

`tlaw.agent-task/v2` is a new task envelope because v1 cannot represent the distinct BAR-26 routing and execution-policy fields. `tlaw.agent-task/v1` remains immutable and its fixture continues to validate unchanged; result, review, and handoff remain v1 envelopes.

Task v2 records `task_id`, `source_id`, repository-relative or HTTPS `sources`, `objective`, `work_type`, `preferred_agent`, `eligible_agents`, `required_capabilities`, `autonomy_level`, `forbidden_operations`, four claim fields, exact `base_sha`, `handoff_required`, `worktree`, `verification`, and `delivery`. The four claim fields are either all `unclaimed`, or all one active claim: `claimed_by` must be eligible, `claim_id` is a non-sentinel fencing token, and timestamps are canonical UTC RFC 3339 `yyyy-MM-dd'T'HH:mm:ss.fffffffZ` with expiry strictly after start. This packet records preparation and local claim evidence; neither state is an agent launch or dispatch.

The contract contains one autonomy level shared by every eligible agent, so a fallback cannot raise it. The accepted routing policy keeps implementation with Codex/Claude, keeps Grok out of implementation, and restricts the local model to read-only analysis. `delivery.merge_forbidden` is always true.

Generate v2 only with the repository-native commands documented in [`tools/Tlaw.Dispatcher/README.md`](../../tools/Tlaw.Dispatcher/README.md). Packet generation accepts a closed local normalized JSON snapshot, emits UTF-8/no-BOM YAML with LF line endings in schema order, validates it through this implementation before atomically replacing the output, and never copies an Issue body into a packet. The separately explicit local `lease acquire` command can convert only an unclaimed valid v2 packet into a validated claimed packet; it does not dynamically select, contact, or launch its named executor.

The separately explicit local `route` command consumes a validated unclaimed task v2 packet and an auditable local agent snapshot, then emits an internal `tlaw.dispatcher-selection/v1` JSON record. Selection is deterministic and does not alter any claim field, acquire a lease, launch an agent, or create a new AgentProtocol envelope. A human reviews that record before separately invoking `lease acquire`.

The separately explicit local `ingest-result` command consumes one validated result/v1 packet only when it exactly matches a validated, fully claimed task/v2 packet and its currently active local lease. Before packet reads, it rejects an output path that aliases either input or any lease-store path after full-path normalization and available symbolic-link/junction resolution. It holds the existing per-task lease lock while it verifies the matching, unexpired claim and rechecks that evidence immediately before atomically publishing its internal `tlaw.dispatcher-ingestion/v1` record. The record includes `result_sha256`, the lowercase SHA-256 of the exact validated result bytes. It then projects the existing concise result content. A stdout failure after durable publication is reported as a non-zero command result and does not roll back the record. It does not release or renew the lease, alter either packet, transition Linear, launch an agent, write GitHub, merge, or complete the parent dispatcher MVP.

The separately explicit local `finalize-result` command verifies a fully claimed task/v2, result/v1, correlated ingestion record, and exact active lease before closing a non-human terminal result. Only `success` maps to `completion` and future state `in_review`; only `failed` maps to `error` and future state `todo`. A `blocked` or human-required result fails closed without releasing the lease or writing output. The command releases the exact lease before publishing its deterministic `tlaw.dispatcher-finalization/v1` record; if publication then fails, it reports that the lease is already released and does not recreate it. It records no actual Linear transition, launch, provider action, GitHub write, merge, or parent completion.

The separately explicit local `ingest-review` command consumes a fully claimed task/v2, a closed successful `tlaw.dispatcher-finalization/v1` completion record, and review/v1 evidence. It requires exact task, agent, fencing-token, and explicit lowercase expected-head agreement, hashes the exact review bytes, and writes a deterministic internal `tlaw.dispatcher-review-decision/v1` record. `approve` without blocker/high/medium findings produces the future decision `merge`/`in_review`; `request_changes` with such findings produces `correction`/`todo`; and `comment` produces `human`/`in_review`. It checks packet aliases before input reads, performs no lease operation or live PR lookup, and records no actual merge or task-state transition.

## Local LM Studio read-only worker

BAR-27 Increment 1 is a closed local read-only analysis boundary, not an AgentProtocol result producer. `local-worker run` accepts only a claimed local `read_only_analysis` task/v2, its exact active lease, closed `tlaw.local-worker-config/v1`, and closed `tlaw.local-worker-input/v1` material manifest. It sends only a bounded loopback LM Studio model-list probe and a no-tools/no-MCP chat completion request. The output is a stable internal `tlaw.local-worker-artifact/v1` JSON record with untrusted analysis, exact identity and hashes, and explicit non-authoritative/no-command flags. It cannot acquire, renew, release, ingest, finalize, transition, merge, or otherwise change task state.

BAR-27 Increment 2 adds the separately explicit trusted host-side command `local-worker complete --task <claimed-task-v2.yaml> --lease-store <absolute-path> --artifact <local-worker-artifact-v1.json> --output <agent-result-v1.yaml>`. It accepts only the exact schema-validated fully claimed local `read_only_analysis` task/v2 with `read_only` autonomy and `delivery.merge_forbidden: true`, plus a strict Increment 1 artifact. The command is not model execution: it never calls LM Studio, Git, a shell, Linear, GitHub, or another agent, and it never changes the task or lease. It holds the existing per-task lock, requires the exact active lease including canonical start/expiry timestamps, and rechecks that lease immediately before result publication.

The artifact remains untrusted. Mandatory manual flow is `local-worker run -> human/stronger-model inspection of untrusted artifact -> local-worker complete -> ingest-result -> finalize-result -> guarded linear transition to In Review`. Only the host-authored result/v1 summary `Validated local read-only analysis artifact produced.` may be authoritative; detailed model analysis remains solely in the artifact. The result's canonical evidence records the artifact schema, exact artifact-byte SHA-256, closed operation, model key, and explicit `non_authoritative=true` / `commands_executed=false` facts. It is accepted unchanged by existing result ingestion and finalization; successful non-human finalization maps only to `in_review`, and has no route to `Done`.

## Human pause

When `human.required: true`, a result must be `blocked`, provide `question`, `evidence`, and non-empty `safe_options`. Projection returns only the summary, question, evidence references, and safe options. It does not append task metadata or free-form trailing prose. Automation pauses for that payload; it does not guess, dispatch, merge, or expand provider permissions.

Result ingestion preserves a valid `failed` or `blocked` status. It is evidence for a later, separately approved workflow decision and never turns an input result into success.

## Evolution

v1 identifiers are immutable. Task v2 is the first task-envelope evolution: consumers that only understand v1 must continue using `tlaw.agent-task/v1`; consumers of the richer preparation fields must require `tlaw.agent-task/v2` explicitly. There is no silent conversion between versions. Schema files, examples, and validator tests change together in a reviewed branch.

## Live adapter boundary

The dispatcher may create a local `tlaw.dispatcher-input/v1` only from a strict `tlaw.dispatcher-linear-profile/v1` plus an explicit minimal Linear snapshot. The profile supplies objective, work policy, autonomy, verification and delivery; Linear supplies identity and links only. The Linear description is never copied into either artifact. API credentials are accepted only via an explicitly named environment variable and are never packet fields or command values.

`tlaw.dispatcher-linear-transition/v1` is an internal durable receipt, not an AgentProtocol envelope. A guarded adapter re-fetches the supplied snapshot, validates event-specific local evidence, executes the minimal allowed update, refetches, and publishes the receipt. A review decision with `merge` is not a merge and cannot mark Done without independent verifier and Git ancestry/reachability proof.

`tlaw.dispatcher-doctor/v1` is a readiness-only report. Its corresponding `tlaw.dispatcher-agent-snapshot/v1` contains the closed `codex`, `claude`, `grok`, and `local` names with route-compatible availability. Doctor configuration does not contain shell syntax, arbitrary arguments, scripts, credentials, or task prompts; local endpoints must be exact loopback addresses.
