# TLAW_COMPOUND_APPLICATOR_01_CANONICAL_PROMOTION_01

Canonical disposition / promotion changeset.

**This is not geometry work. This is not a Blender pass. This is not a new integration.**

Exact independently reviewed cleanup candidate bytes already on `main` are
recorded as the new canonical production baseline **conditional on merge of this
promotion PR**.

`PROMOTION_IS_DISPOSITION_ONLY=YES`

`PRODUCTION_BINARY_MUTATION=NO`

`PROMOTION_EFFECTIVE_ON_MERGE_ONLY=YES`

Do **not** merge this PR as part of this gate. Effective active canonical remains
`3ccf52e2…` / `48b1884a…` until Control Center separately authorizes merge.

---

## Required summary

```
RESULT=PASS WITH WARN
OWNER_GATE_ID=TLAW_COMPOUND_APPLICATOR_01_CANONICAL_PROMOTION_01
BASE_MAIN_SHA=93a87f625269b4e5cff304065eb6dc876c3944ad
BRANCH=task/TLAW-compound-applicator-canonical-promotion-01
COMMIT_SHA=PENDING_THIS_CHANGESET
PR_NUMBER=PENDING
PREVIOUS_CANONICAL_BLEND_SHA256=3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5
PREVIOUS_CANONICAL_GLB_SHA256=48b1884ad81c7f6aebc7cb75c6e46da5e279bb7c7810052d91ab6931e925d665
TARGET_CANONICAL_BLEND_SHA256=be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a
TARGET_CANONICAL_GLB_SHA256=eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65
PRODUCTION_BINARY_MUTATION=NO
PROMOTION_ELIGIBILITY=CONFIRMED
PROMOTION_ELIGIBLE=YES
LOCAL_VERIFY=PENDING
GITHUB_CI=PENDING
PR_MERGED=NO
EFFECTIVE_ACTIVE_CANONICAL=3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5
PROMOTION_CHANGESET_READY=YES
```

`COMMIT_SHA` / `PR_NUMBER` / local verify / CI are filled on the same branch after
commit, PR open, and verification. They do not change production bytes.

---

## 1 — Owner gate

Gate: `TLAW_COMPOUND_APPLICATOR_01_CANONICAL_PROMOTION_01`

Executor: Claude Code / Grok Build agent in local repository
`C:\Projects\TheLogsAreWrong` (clean worktree
`C:\Projects\TheLogsAreWrong-worktrees\TLAW-compound-applicator-canonical-promotion-01`).

Repository: `baroentgray/the-logs-are-wrong`

Authorized mutation: current-state canonical metadata, durable custody manifest
status, this promotion report, external registry, commit, push, PR creation.

Not authorized: Blender, geometry, materials, re-export, proxy restoration,
Unity, Pipeline, Astra, gameplay/domain redesign, historical evidence edits,
merge, next production task.

---

## 2 — Repository baseline

| check | result |
|---|---|
| `git fetch origin` | PASS |
| expected live `main` | `93a87f625269b4e5cff304065eb6dc876c3944ad` |
| `origin/main` | `93a87f625269b4e5cff304065eb6dc876c3944ad` |
| drift | NONE |
| `origin/main` subject | `Merge PR #194: durable production artifact custody` |
| primary working tree | dirty / untracked local artifacts (understood; excluded) |
| promotion worktree | created from exact `origin/main`; clean |
| branch | `task/TLAW-compound-applicator-canonical-promotion-01` created from `93a87f62…` |
| work on `main` | NO |

`STOP / HOLD` for unexpected `origin/main` drift was not triggered.

---

## 3 — Required byte preflight

LFS objects materialized from exact `main`. SHA-256 recomputed on working-tree
files (not pointer files).

| role | path | size | SHA-256 | required | result |
|---|---|---:|---|---|---|
| current canonical `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_SAW01_OT01_PROCSPUR_CORR01.blend` | 531039 | `3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5` | `3ccf52e2…` | MATCH |
| current canonical GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_SAW01_OT01_PROCSPUR_CORR01.glb` | 2138052 | `48b1884ad81c7f6aebc7cb75c6e46da5e279bb7c7810052d91ab6931e925d665` | `48b1884a…` | MATCH |
| promotion candidate `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.blend` | 614872 | `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a` | `be48b091…` | MATCH |
| promotion candidate GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.glb` | 2332388 | `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65` | `eba529c0…` | MATCH |

`ALL_MATCH=True`

No regeneration. No re-export. `STOP / HOLD` for identity mismatch was not triggered.

---

## 4 — Review authority

Durable cleanup independent-review report now stored in Git:

`art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01.report.md`

Confirmed statements:

- `VERDICT: APPROVE WITH NON-BLOCKING NOTES`
- `PROMOTION_ELIGIBLE=YES`

Confirmed reviewed identities:

- `.blend` `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a`
- GLB `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65`

`PROMOTION_ELIGIBILITY=CONFIRMED`

That review is eligibility authority only. This gate is the owner-authorized
disposition/promotion changeset. Merge remains a later Control Center action.

Imported review bytes are not modified.

---

## 5 — Canonical promotion meaning

After eventual merge of this promotion changeset:

| | before merge (still effective on `main`) | after merge |
|---|---|---|
| active canonical `.blend` | `3ccf52e2…` | `be48b091…` |
| paired scene-scoped GLB | `48b1884a…` | `eba529c0…` |
| status of target | PROMOTION-ELIGIBLE CANDIDATE | **CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN** |
| status of `3ccf52e2…` / `48b1884a…` | CANONICAL PRODUCTION BASELINE | **PREVIOUS CANONICAL BASELINE / PARENT** |

Previous canonical remains durable history. Files are not deleted.

Until merge:

`EFFECTIVE_ACTIVE_CANONICAL=3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5`

`PROMOTION_CHANGESET_READY=YES`

`PR_MERGED=NO`

---

## 6 — Lineage

```
3ccf52e2…  previous canonical parent
           TLAW_SAWMILL_LAYOUT_V2_SAW01_OT01_PROCSPUR_CORR01.blend
           paired GLB 48b1884a…
     *
9c39aaa7…  reviewed corrected standalone applicator
           TLAW_COMPOUND_APPLICATOR_01_CORR01.blend
     ->
0d9a6677…  reviewed integrated INT01
           TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01.blend
           paired GLB 04966c05…
           PROMOTION_ELIGIBLE=NO (CompoundProxy blocker)
     ->
be48b091…  bounded CompoundProxy cleanup
           TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.blend
           paired GLB eba529c0…
           independent review:
             APPROVE WITH NON-BLOCKING NOTES
             PROMOTION_ELIGIBLE=YES
           after merge of this PR:
             CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN
```

No intermediate candidate is substituted. INT01 remains durable lineage with
`PROMOTION_ELIGIBLE=NO`. Cleanup candidate is the promotion target.

---

## 7 — CompoundProxy status

`CompoundProxy = REMOVED`

Its promotion-blocking condition is resolved.

Do not restore it.

---

## 8 — Applicator status

Carried forward unchanged. No new state/semantics authorized.

- role: `RESIN_SALT_APPLICATOR`
- spatial contract: `SIDE_NODE_FIXED_LOCAL_ZONE`
- gameplay adapter: `attempted_item=salt`

Standalone applicator `9c39aaa7…` is not modified.
INT01 `0d9a6677…` is not modified.

---

## 9 — Durable warnings — must carry forward

New canonical disposition retains these WARNs. They are not converted to PASS.

### W-01 — LocalSplashGuard <-> log

`LocalSplashGuard <-> log ~= 0.025000052 m`

Classification: `DURABLE WARN`

Independent cleanup review N-01. Unchanged from reviewed INT01 relationship.

### W-02 — inherited canonical Procedure margin

`ProcedureGuide <-> log ~= 0.005000019 m`

Classification: `DURABLE INHERITED WARN`

Independent cleanup review N-02. Separate from applicator clearance. Inherited
from Procedure-spur CORR01 canonical parent `3ccf52e2…`. Not consumed by proxy
cleanup.

### W-03 — Unity / engine import

`Unity / engine import for be48b091… / eba529c0… = UNTESTED`

Classification: `NON-BLOCKING WARN`

Independent cleanup review N-03 / section 18. This promotion gate did not run
Unity.

---

## 10 — Discover existing canonical records

Repository authority on live `origin/main` `93a87f62…`:

Tracked current-state canonical identity records:

| path | action |
|---|---|
| `art/environment/sawmill/TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md` | **UPDATED** — current-state disposition overlay + table |
| `art/environment/sawmill/TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.report.md` | NOT updated — historical custody-gate report |
| imported worker/review reports listed in the manifest | NOT updated — immutable evidence |
| `docs/agent/CURRENT_STATE.md` | NOT updated — domain Gate-1 checkpoint cache; does not record 3D SHA |

Not in Git on `origin/main` (local untracked; not added by this gate):

- `art/environment/sawmill/CANONICAL.md` — stale historical filename/SHA document
- `tools/3d_validation/configs/tlaw_sawmill_layout_v2.json` — local validator ACTIVE_CANONICAL config
- `tools/3d_validation/README.md`
- prior Procedure-spur promotion packet files

Established Git canonical-promotion mechanism: durable custody manifest
disposition + promotion report + external registry. Compatible with this gate.
No competing mechanism was invented.

Validator-config migration historically lived in a **separate** local/untracked
sync gate (`TLAW_CANONICAL_3CCF_VALIDATOR_SYNC_01`) and is **not tracked** on
`main`. This disposition-only gate does not add that tree or migrate validator
bindings.

External registry (outside repo):

- `C:\Projects\TLAW_Handoff\registry\ARTIFACT_REGISTRY.csv` — updated to record
  this pending promotion PR and lineage, distinguishing
  `PROMOTION_CHANGESET_READY` from `ACTIVE_CANONICAL`

---

## 11 — Authoritative current-state files updated

| file | change |
|---|---|
| `art/environment/sawmill/TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md` | disposition: `be48b091…` / `eba529c0…` become `CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN` conditional on merge; `3ccf52e2…` / `48b1884a…` become `PREVIOUS CANONICAL BASELINE / PARENT` |
| `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_CANONICAL_PROMOTION_01.report.md` | this report (new) |
| `C:\Projects\TLAW_Handoff\registry\ARTIFACT_REGISTRY.csv` | pending promotion PR / lineage; not a repo file |

No production binary paths staged.
No LFS pointer modifications.
No imported review-report modifications.

---

## 12 — Zero production-binary mutation

`PRODUCTION_BINARY_MUTATION=NO`

Not done:

- open/save in Blender
- re-export GLB
- modify candidate binaries
- normalize binaries
- duplicate under speculative filenames
- delete prior canonical
- modify standalone applicator
- modify INT01

Existing candidate bytes become canonical by authoritative disposition after
merge.

---

## 13 — Git / PR / verify

Filled after commit + PR on this same branch.

| field | value |
|---|---|
| BRANCH | `task/TLAW-compound-applicator-canonical-promotion-01` |
| BASE_MAIN_SHA | `93a87f625269b4e5cff304065eb6dc876c3944ad` |
| COMMIT_SHA | PENDING_THIS_CHANGESET |
| PR_NUMBER | PENDING |
| PR_MERGED | NO |
| LOCAL_VERIFY | PENDING (`git diff --check` + `Tlaw.Verify`) |
| GITHUB_CI | PENDING |

Suggested commit message:

`chore(art): record Compound Applicator canonical promotion`

Suggested PR title:

`TLAW: promote reviewed Compound Applicator integration to canonical`

---

## 14 — STOP boundary

Promotion changeset prepared. Do not merge.

Return to Control Center after PR/CI.

No next production task.
No Astra.
No Unity.
No Blender.
No geometry edits.
