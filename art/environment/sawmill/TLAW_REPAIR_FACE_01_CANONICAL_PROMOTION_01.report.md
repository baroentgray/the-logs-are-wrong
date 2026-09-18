# TLAW_REPAIR_FACE_01_CANONICAL_PROMOTION_01

Canonical disposition / promotion changeset.

**This is not geometry work. This is not a Blender pass. This is not a new integration.**

Exact independently reviewed and Git/LFS-custodied Repair Face integrated
candidate bytes already on `main` are recorded as the new canonical production
baseline **conditional on merge of this promotion PR**.

`PROMOTION_IS_DISPOSITION_ONLY=YES`

`PRODUCTION_BINARY_MUTATION=NO`

`PROMOTION_EFFECTIVE_ON_MERGE_ONLY=YES`

Do **not** merge this PR as part of this gate. Effective active canonical remains
`be48b091…` / `eba529c0…` until Control Center separately authorizes merge.

---

## Required summary

```
RESULT=PASS WITH WARN
OWNER_GATE_ID=TLAW_REPAIR_FACE_01_CANONICAL_PROMOTION_01
BASE_MAIN_SHA=e0e4073f10fae5058a0059d66c17ced5cc7b46f9
BRANCH=task/TLAW-repair-face-canonical-promotion-01
PREVIOUS_CANONICAL_BLEND_SHA256=be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a
PREVIOUS_CANONICAL_GLB_SHA256=eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65
TARGET_CANONICAL_BLEND_SHA256=b940f515e42795aaab785662067da1b39ef26226ddbc76410398faf4be683a5b
TARGET_CANONICAL_GLB_SHA256=68912b8b8862daea523ba5d8532a63451de4cd1a6b0a3f0416160020d5090ca7
PRODUCTION_BINARY_MUTATION=NO
PROMOTION_ELIGIBILITY=CONFIRMED
PROMOTION_ELIGIBLE=YES
PR_MERGED=NO
EFFECTIVE_ACTIVE_CANONICAL=be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a
PROMOTION_CHANGESET_READY=YES
PROMOTION_PERFORMED=NO
```

Commit SHA, PR number/URL, local verify, and GitHub CI are recorded after commit
and PR on this same branch. A follow-up docs commit may record those identities;
it does not change production bytes.

---

## 1 — Owner gate

Gate: `TLAW_REPAIR_FACE_01_CANONICAL_PROMOTION_01`

Executor: Grok Build in dedicated worktree
`C:\Projects\TheLogsAreWrong-worktrees\TLAW-repair-face-canonical-promotion-01`
(Control Center authorized Claude Code executor; this session executed the gate).

Repository: `baroentgray/the-logs-are-wrong`

Authorized mutation: current-state canonical metadata, Repair Face durable
custody manifest status, this promotion report, external registry, commit, push,
PR creation.

Not authorized: Blender, geometry, materials, re-export, `RepairFaceProxy`
restoration, Unity, Pipeline, Astra, gameplay/domain redesign, historical
evidence edits, merge, next production task.

---

## 2 — Repository baseline

| check | result |
|---|---|
| `git fetch origin` | PASS |
| expected live `main` | `e0e4073f10fae5058a0059d66c17ced5cc7b46f9` |
| `origin/main` | `e0e4073f10fae5058a0059d66c17ced5cc7b46f9` |
| drift | NONE |
| `origin/main` subject | `Merge PR #196: durable Repair Face artifact custody` |
| promotion worktree | created from exact `origin/main`; clean |
| branch | `task/TLAW-repair-face-canonical-promotion-01` created from `e0e4073f…` |
| work on `main` | NO |

`HOLD — REPOSITORY DRIFT` was not triggered.

---

## 3 — Required byte preflight

LFS objects materialized from exact `main`. SHA-256 recomputed on working-tree
files (not pointer files).

| role | path | size | SHA-256 | required | result |
|---|---|---:|---|---|---|
| current canonical `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.blend` | 614872 | `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a` | `be48b091…` | MATCH |
| current canonical GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.glb` | 2332388 | `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65` | `eba529c0…` | MATCH |
| promotion target `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.blend` | 629460 | `b940f515e42795aaab785662067da1b39ef26226ddbc76410398faf4be683a5b` | `b940f515…` | MATCH |
| promotion target GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.glb` | 2415104 | `68912b8b8862daea523ba5d8532a63451de4cd1a6b0a3f0416160020d5090ca7` | `68912b8b…` | MATCH |

`ALL_MATCH=True`

No regeneration. No re-export. `HOLD — CANONICAL/PROMOTION TARGET IDENTITY MISMATCH` was not triggered.

---

## 4 — Review authority

Durable independent integration review stored in Git:

`art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01_REVIEW_01.report.md`

SHA-256 `98795bc19526c74cc801c04423f8005e05ef630c2aac9bbab54f022cbe699d94` size `21378`

Companion validation JSON:

`art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01_REVIEW_01.validation.json`

SHA-256 `c465d8ed019090afcab5123b710c5de2d03f5ba0546bdc3f6a1221c779dbd12b` size `4072`

Worker integration report:

`art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01.report.md`

SHA-256 `2efa931652bf2fdba026345fc57c8629d50d7ccc897ad9170f99b31508994bab`

Confirmed statements:

- `VERDICT: APPROVE WITH NON-BLOCKING NOTES`
- `PROMOTION_ELIGIBLE=YES`

Confirmed reviewed identities:

- `.blend` `b940f515e42795aaab785662067da1b39ef26226ddbc76410398faf4be683a5b`
- GLB `68912b8b8862daea523ba5d8532a63451de4cd1a6b0a3f0416160020d5090ca7`

`PROMOTION_ELIGIBILITY=CONFIRMED`

That review is eligibility authority only. This gate is the owner-authorized
disposition/promotion changeset. Merge remains a later Control Center action.

Imported review/evidence bytes are not modified.

---

## 5 — Custody authority

Repair Face durable custody was merged by PR #196.

Current `main` contains:

- `art/environment/sawmill/TLAW_REPAIR_FACE_01_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md`
- `art/environment/sawmill/TLAW_REPAIR_FACE_01_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.report.md`

Required custody state before this promotion:

- exact target BLEND/GLB present through Git LFS — YES
- reviewed integrated candidate — YES
- `PROMOTION_ELIGIBLE=YES` — YES
- `NOT CANONICAL` — YES (until merge of this PR)
- active canonical still `be48b091…` / `eba529c0…` — YES

Custody record does not contradict this gate.

---

## 6 — Canonical promotion meaning

After eventual merge of this promotion changeset:

| | before merge (still effective on `main`) | after merge |
|---|---|---|
| active canonical `.blend` | `be48b091…` | `b940f515…` |
| paired scene-scoped GLB | `eba529c0…` | `68912b8b…` |
| status of target | PROMOTION-ELIGIBLE REVIEWED INTEGRATED CANDIDATE — NOT CANONICAL | **CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN** |
| status of `be48b091…` / `eba529c0…` | CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN | **PREVIOUS CANONICAL BASELINE / PARENT** |

Previous canonical remains durable history. Files are not deleted.

Until merge:

`EFFECTIVE_ACTIVE_CANONICAL=be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a`

`PROMOTION_CHANGESET_READY=YES`

`PR_MERGED=NO`

---

## 7 — Lineage

```text
be48b091… / eba529c0…
CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN
Compound Applicator canonical parent
        +
65631fa9… / ebdce719…
TLAW_REPAIR_FACE_01
REVIEWED STANDALONE
INTEGRATION_ELIGIBLE=YES
        ->
b940f515… / 68912b8b…
TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01
APPROVE WITH NON-BLOCKING NOTES
PROMOTION_ELIGIBLE=YES
        ->
after promotion PR merge only:
CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN
```

No intermediate candidate is substituted.

---

## 8 — Repair Face contract carried forward

Promotion accepts only the already reviewed bounded production state.

- `RepairFaceProxy = REMOVED`
- `RepairPanelProxy = PRESENT`
- `TLAW_REPAIR_FACE_01` present
- 6 repair production meshes
- 1248 repair triangles
- 3 intended canonical materials
- `OTHER_CANONICAL_CHANGED_OBJECTS=0`
- `UNEXPLAINED_DELTA=0`
- `UNEXPLAINED_STATIC_INTERSECTIONS=0`
- RepairStanding separation `≈ 0.110 m` (standing-zone separation, **not** generic player-route clearance)
- `ACTUAL_PLAYER_ROUTE_INTRUSION=NO`
- dedicated repair route remains `NOT_DEFINED`
- anchors `19/19`
- anchor transform delta `0`
- accepted scope = **CLOSED STATIC PRODUCTION POSE**

Promotion does **not** approve:

- opening angle
- opening animation
- swept volume
- moving collision
- repair interaction timing
- repair gameplay/procedure

Do not restore `RepairFaceProxy`.

---

## 9 — Durable warnings — must carry forward

New canonical disposition retains these WARNs. They are not converted to PASS.

### W-01 — inherited Compound Applicator clearance

`LocalSplashGuard <-> log ~= 0.025000052 m`

Classification: `DURABLE WARN`

Independent integration review records this relationship on an unchanged
canonical subtree. Not consumed by Repair Face integration.

### W-02 — inherited canonical Procedure margin

`ProcedureGuide <-> log ~= 0.005000019 m`

Classification: `DURABLE INHERITED WARN`

Official OBB vs ProcedureCradleSide. Inherited from prior canonical parent.
Not consumed by Repair Face integration.

### W-03 — Unity / engine import

`Unity / engine import for b940f515… / 68912b8b… = UNTESTED`

Classification: `NON-BLOCKING WARN`

Independent integration review N-05. This promotion gate did not run Unity.

---

## 10 — Discover existing canonical records

Repository authority on live `origin/main` `e0e4073f…`:

Tracked current-state canonical identity records:

| path | action |
|---|---|
| `art/environment/sawmill/TLAW_REPAIR_FACE_01_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md` | **UPDATED** — current-state disposition overlay + table |
| `art/environment/sawmill/TLAW_REPAIR_FACE_01_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.report.md` | NOT updated — historical custody-gate report |
| `art/environment/sawmill/TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md` | NOT updated — Compound Applicator historical custody/disposition; Repair Face overlay supersedes its active-canonical claim after merge of this PR |
| imported worker/review reports listed in the Repair Face manifest | NOT updated — immutable evidence |

Not in Git on `origin/main` (local untracked; not added by this gate):

- `art/environment/sawmill/CANONICAL.md` — stale historical filename/SHA document
- `tools/3d_validation/` — local validator tree, not tracked on `main`

Established Git canonical-promotion mechanism: durable custody manifest
disposition + promotion report + external registry. Compatible with this gate.
No competing mechanism was invented.

Validator-config migration historically lived in a **separate** local/untracked
sync gate and is **not tracked** on `main`. This disposition-only gate does not
add that tree or migrate validator bindings.

External registry (outside repo):

- `C:\Projects\TLAW_Handoff\registry\ARTIFACT_REGISTRY.csv` — updated to record
  this pending promotion PR and lineage, distinguishing
  `PROMOTION_CHANGESET_READY` from `ACTIVE_CANONICAL`

---

## 11 — Authoritative current-state files updated

| file | change |
|---|---|
| `art/environment/sawmill/TLAW_REPAIR_FACE_01_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md` | disposition: `b940f515…` / `68912b8b…` become `CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN` conditional on merge; `be48b091…` / `eba529c0…` become `PREVIOUS CANONICAL BASELINE / PARENT` |
| `art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_PROMOTION_01.report.md` | this report (new) |
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
- modify standalone Repair Face
- modify Compound Applicator binaries

Existing candidate bytes become canonical by authoritative disposition after
merge.

---

## 13 — Git / PR / verify

Filled after commit + PR on this same branch.

Suggested commit message:

`chore(art): record Repair Face canonical promotion`

Suggested PR title:

`TLAW: promote reviewed Repair Face integration to canonical`

---

## 14 — STOP boundary

Promotion changeset prepared. Do not merge.

Return to Control Center after PR/CI.

No next production task.
No Astra.
No Unity.
No Blender.
No geometry edits.
