---
schema: tlaw.active-runs/v1
non_authoritative: true
generated: true
snapshot_kind: prepared_target
generated_at: 2026-07-27T09:07:39Z
runs: []
staleness_rule: Validate live GitHub, Linear, and CI before relying on or updating this projection; a stale entry fails visibly and is never permission to guess.
refresh_triggers:
  - TASK_CREATED
  - IMPLEMENTATION_CANDIDATE_READY
  - AUTHORITATIVE_REVIEW_COMPLETE
  - CORRECTION_CANDIDATE_READY
  - MERGED_AND_VERIFIED
  - CHAT_HANDOFF_CREATED
prepared_target_note: This prepared target describes the intended clean post-merge checkpoint. It does not say that the TLAW-AUTO-009 candidate is already merged.
---

# Active runs — generated, non-authoritative projection

This projection contains only unfinished operations and must be ordered by
ascending `identity`. Duplicate identities are invalid. The prepared target is
empty because its post-merge chat checkpoint has no active implementation,
authoritative review, correction, or merge operation.

## Refresh and staleness rule

Refresh only at these safe workflow checkpoints: `TASK_CREATED`,
`IMPLEMENTATION_CANDIDATE_READY`, `AUTHORITATIVE_REVIEW_COMPLETE`,
`CORRECTION_CANDIDATE_READY`, `MERGED_AND_VERIFIED`, and
`CHAT_HANDOFF_CREATED`. Validate live GitHub, Linear, and CI before any write.
If this file is stale or missing, it is not permission to invent an active run
or to treat an unmerged candidate as complete.
