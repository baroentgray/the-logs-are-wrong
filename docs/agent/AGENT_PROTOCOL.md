# Agent protocol v1 and task v2

Agent packets are UTF-8 YAML interchange/audit records. A human speaks ordinary language; an agent serializes a compact, validated packet only when a task, result, review, or handoff needs transfer.

## Envelopes

| Envelope | Schema | Closed status/verdict |
| --- | --- | --- |
| Legacy task | `tlaw.agent-task/v1` | N/A |
| Prepared task | `tlaw.agent-task/v2` | `work_type`, agent and autonomy policy enums |
| Result | `tlaw.agent-result/v1` | `success`, `blocked`, `failed` |
| Review | `tlaw.agent-review/v1` | `approve`, `request_changes`, `comment` |
| Handoff | `tlaw.agent-handoff/v1` | `ready`, `blocked` |

The JSON schemas and positive/negative examples live under `schemas/`. Validate before dispatch and after ingestion with:

```powershell
dotnet run --configuration Release --project tools/Tlaw.AgentProtocol -- validate docs/agent/schemas/examples/result.valid.yaml
dotnet run --configuration Release --project tools/Tlaw.AgentProtocol -- project-result docs/agent/schemas/examples/result.valid.yaml
```

## Safe YAML subset and evidence

Only one mapping-root document is accepted. Anchors, aliases, custom tags, merge keys, duplicate keys, unknown schema versions, unknown fields, and missing required evidence fail visibly. The tool performs no object deserialization. Emit keys in the schema property order and use source URLs or repository paths instead of copied Issue bodies.

Every result, review, and handoff has `human_summary` with at most five non-empty lines. A result needs at least one evidence item (`command`, `source`, `file`, or `ci`); a `success` claim without it is invalid.

Every review has `reviewed_head`: exactly 40 hexadecimal characters naming the reviewed Git commit. v1 validates this recorded evidence structurally; a later merge policy may compare it with a live pull-request head.

## Task v2 preparation

`tlaw.agent-task/v2` is a new task envelope because v1 cannot represent the distinct BAR-26 routing and execution-policy fields. `tlaw.agent-task/v1` remains immutable and its fixture continues to validate unchanged; result, review, and handoff remain v1 envelopes.

Task v2 records `task_id`, `source_id`, repository-relative or HTTPS `sources`, `objective`, `work_type`, `preferred_agent`, `eligible_agents`, `required_capabilities`, `autonomy_level`, `forbidden_operations`, four claim fields, exact `base_sha`, `handoff_required`, `worktree`, `verification`, and `delivery`. The four claim fields use `unclaimed` together when no claim exists. This packet records a prepared snapshot only; it neither acquires nor persists a lease.

The contract contains one autonomy level shared by every eligible agent, so a fallback cannot raise it. The accepted routing policy keeps implementation with Codex/Claude, keeps Grok out of implementation, and restricts the local model to read-only analysis. `delivery.merge_forbidden` is always true.

Generate v2 only with the repository-native command documented in [`tools/Tlaw.Dispatcher/README.md`](../../tools/Tlaw.Dispatcher/README.md). The command accepts a closed local normalized JSON snapshot, emits UTF-8 YAML in schema order, validates it through this implementation before atomically replacing the output, and never copies an Issue body into a packet.

## Human pause

When `human.required: true`, a result must be `blocked`, provide `question`, `evidence`, and non-empty `safe_options`. Projection returns only the summary, question, evidence references, and safe options. It does not append task metadata or free-form trailing prose. Automation pauses for that payload; it does not guess, dispatch, merge, or expand provider permissions.

## Evolution

v1 identifiers are immutable. Task v2 is the first task-envelope evolution: consumers that only understand v1 must continue using `tlaw.agent-task/v1`; consumers of the richer preparation fields must require `tlaw.agent-task/v2` explicitly. There is no silent conversion between versions. Schema files, examples, and validator tests change together in a reviewed branch.
