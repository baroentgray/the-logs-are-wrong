---
schema: tlaw.current-state/v1
non_authoritative: true
snapshot_kind: prepared_target
stage: MERGED_AND_VERIFIED
verified_main:
  sha: e13b439d2929b969d179b012b2cfee05f66467c5
  workflow: https://github.com/baroentgray/the-logs-are-wrong/actions/runs/30252521683
  job: https://github.com/baroentgray/the-logs-are-wrong/actions/runs/30252521683/job/89933564902
  artifact: verification-30252521683
  artifact_id: 8647559612
  artifact_digest: sha256:fde729a98e10c1d556c6a58d14a30781a4564a21686bcceeab748667bd0a6977
  tests_passed: 940
  tests_failed: 0
  tests_skipped: 0
  build_warnings: 0
  build_errors: 0
  clean_tree: true
  diff_check: true
  gate_0: true
  architecture: true
  domain_dependencies: true
  git_object_reader: true
completed_tasks:
  - task_id: TLAW-015
    github_issue: https://github.com/baroentgray/the-logs-are-wrong/issues/45
    pull_request: https://github.com/baroentgray/the-logs-are-wrong/pull/46
    linear_issue: https://linear.app/baronet/issue/BAR-54
    status: completed
active_task: null
active_pr: null
candidate_sha: null
open_blockers: []
next_action: After merging and live-verifying this checkpoint, start a new control chat and select or define the next task.
source_links:
  - https://github.com/baroentgray/the-logs-are-wrong/actions/runs/30252521683
  - https://github.com/baroentgray/the-logs-are-wrong/pull/46
  - https://linear.app/baronet/issue/BAR-54
staleness_rule: Validate live GitHub, Linear, and CI before relying on or updating this cache; stale or missing cache is never permission to guess.
prepared_target_note: This prepared target describes the intended post-merge checkpoint. It is not a claim that the TLAW-AUTO-009 candidate is already merged.
---

# Current state — non-authoritative prepared target

This is the single volatile current-state cache. It records the verified
`main` baseline immediately before this checkpoint and the intended clean
state after the checkpoint has been merged and live-verified. It does not
grant authority and does not report the still-unmerged TLAW-AUTO-009 candidate
as merged.

## Verified baseline

`e13b439d2929b969d179b012b2cfee05f66467c5` was verified by workflow
`30252521683`, job `89933564902`, and artifact `verification-30252521683`
(ID `8647559612`). The artifact reported 940 passed, zero failed/skipped,
zero build warnings/errors, clean tree, diff check, Gate 0, architecture,
Domain dependency, Git object reader, and final verifier passes.

TLAW-015 is complete: [PR #46](https://github.com/baroentgray/the-logs-are-wrong/pull/46)
merged, [Issue #45](https://github.com/baroentgray/the-logs-are-wrong/issues/45)
closed, and [BAR-54](https://linear.app/baronet/issue/BAR-54) is Done.

## Staleness rule

Before any write, validate live GitHub, Linear, and CI. A stale or missing
cache is visible evidence only and is never permission to infer a task, a
merge, reviewer authority, or a baseline.
