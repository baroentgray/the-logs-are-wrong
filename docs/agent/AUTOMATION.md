# Automation and routing blueprint

This is a reviewed repository copy of the routing blueprint, not a dispatcher implementation.

1. Resolve the authoritative task source and exact main SHA.
2. Build a compact `tlaw.agent-task/v1` packet with links and paths, not copied Issue bodies.
3. Validate the restricted YAML before dispatch.
4. Route a temporary executor from configured availability/quota and role suitability.
5. Require branch, deterministic evidence, Draft PR, and independent review before a human merge decision.
6. Validate result/review/handoff packets on ingestion; unknown versions or missing evidence stop visibly.

Codex implements; Claude plans/reviews and can be a fallback executor; local tools prepare read-only evidence; Grok console/CLI supports experiments, research, red-team, and alternative review. No entry here launches an agent, locks a provider, changes authority, or grants implementation permission. Future `tlaw packet`, `dispatch`, `review`, and `handoff` workflows may consume the manifest and schemas after separate approval.
