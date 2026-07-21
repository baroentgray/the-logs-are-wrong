# Agent protocol v1

Agent packets are UTF-8 YAML interchange/audit records. A human speaks ordinary language; an agent serializes a compact, validated packet only when a task, result, review, or handoff needs transfer.

## Envelopes

| Envelope | Schema | Closed status/verdict |
| --- | --- | --- |
| Task | `tlaw.agent-task/v1` | N/A |
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

## Human pause

When `human.required: true`, a result must be `blocked`, provide `question`, `evidence`, and non-empty `safe_options`. Projection returns only the summary, question, evidence references, and safe options. It does not append task metadata or free-form trailing prose. Automation pauses for that payload; it does not guess, dispatch, merge, or expand provider permissions.

## Evolution

v1 identifiers are immutable. Add a new versioned identifier and migration note for incompatible changes. Schema files, examples, and validator tests change together in a reviewed branch.
