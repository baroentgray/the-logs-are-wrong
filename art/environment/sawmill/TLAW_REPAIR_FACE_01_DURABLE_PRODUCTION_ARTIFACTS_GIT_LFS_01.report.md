# TLAW_REPAIR_FACE_01_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01

Custody / versioning gate. Not canonical promotion.

**RESULT=PASS**

Executor: Grok Build in dedicated worktree `C:\Projects\TheLogsAreWrong-worktrees\TLAW-repair-face-durable-production-artifacts-lfs-01` (Control Center authorized Claude Code executor; this session executed the gate).

---

## Final fields

```
RESULT=PASS
BASE_MAIN_SHA=65fa89c5179798eedf248590b4f1ed87c9b90905
BRANCH=task/TLAW-repair-face-durable-production-artifacts-lfs-01
ARTIFACTS_COMMIT_SHA=7bce40d33d0f77a2a2b0020928b7f104e8643f2d
PR_NUMBER=196
PR_URL=https://github.com/baroentgray/the-logs-are-wrong/pull/196
PR_MERGED=NO
INTEGRATED_BLEND_SHA256=b940f515e42795aaab785662067da1b39ef26226ddbc76410398faf4be683a5b
INTEGRATED_GLB_SHA256=68912b8b8862daea523ba5d8532a63451de4cd1a6b0a3f0416160020d5090ca7
INTEGRATION_REPORT_SHA256=2efa931652bf2fdba026345fc57c8629d50d7ccc897ad9170f99b31508994bab
REVIEW_REPORT_SHA256=98795bc19526c74cc801c04423f8005e05ef630c2aac9bbab54f022cbe699d94
REVIEW_VALIDATION_SHA256=c465d8ed019090afcab5123b710c5de2d03f5ba0546bdc3f6a1221c779dbd12b
STANDALONE_BLEND_SHA256=65631fa9c309902865dbcf9c71ff259fd89cfb0a652b3ede3d3c744e4e24235f
STANDALONE_GLB_SHA256=ebdce71930b1f61b2c22680640f275b94514e8285c3836cb225b9e794c24d0aa
GLB_LFS_RULE=PASS
LFS_UPLOAD=PASS
REMOTE_RECHECK=PASS
LOCAL_VERIFY=PASS
GITHUB_CI=SUCCESS
ACTIVE_CANONICAL_BLEND_SHA256=be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a
ACTIVE_CANONICAL_GLB_SHA256=eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65
ACTIVE_CANONICAL_UNCHANGED=YES
PROMOTION_PERFORMED=NO
MANIFEST_PATH=art/environment/sawmill/TLAW_REPAIR_FACE_01_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md
MANIFEST_SHA256=80eb60b7dab5c78bc2d962e1e0f0f6e66c99f516cc13b804a1de1811d37b7e81
READY_FOR_CONTROL_CENTER_REVIEW=YES
```

`ARTIFACTS_COMMIT_SHA` is the Git/LFS artifacts commit. This report is a follow-up docs commit on the same branch/PR. Final head SHA is recorded after this file is committed.

---

## Live baseline preflight

| check | result |
|---|---|
| `git fetch origin` | PASS |
| `origin/main` | `65fa89c5179798eedf248590b4f1ed87c9b90905` |
| expected main | `65fa89c5179798eedf248590b4f1ed87c9b90905` |
| drift | NONE |
| dedicated worktree | `C:\Projects\TheLogsAreWrong-worktrees\TLAW-repair-face-durable-production-artifacts-lfs-01` |
| dedicated branch created from | exact `origin/main` |
| branch | `task/TLAW-repair-face-durable-production-artifacts-lfs-01` |

Did not work on `main`. Upstream tracking to `origin/main` was unset before any push.

---

## `.gitattributes`

Existing `main` already had:

```
*.blend filter=lfs diff=lfs merge=lfs -text
*.glb filter=lfs diff=lfs merge=lfs -text
```

No duplicate global LFS rules added.

Path-specific whitespace exceptions added only for three immutable imported Markdown evidence files that actually trigger `git diff --check` trailing-whitespace diagnostics (PR #194 precedent):

```
art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01.report.md whitespace=-trailing-space
art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01_REVIEW_01.report.md whitespace=-trailing-space
art/environment/sawmill/TLAW_REPAIR_FACE_01_REVIEW_01.report.md whitespace=-trailing-space
```

Did not use `*.md -whitespace`. Did not disable whitespace checking for `art/`. Imported evidence bytes were not edited.

JSON exact bytes remained exact and did not require an exception.

Verification:

```
git check-attr filter -- art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.blend
→ filter: lfs

git check-attr filter -- art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.glb
→ filter: lfs

git check-attr filter -- art/assets/TLAW_REPAIR_FACE_01.blend
→ filter: lfs

git check-attr filter -- art/assets/TLAW_REPAIR_FACE_01.glb
→ filter: lfs
```

`GLB_LFS_RULE=PASS`

---

## Adopted artifact table

SOURCE_SHA == REPO_PATH_SHA == OWNER_REQUIRED_SHA. No Blender save. No re-export. No binary normalization.

| role | path | SHA-256 | size | LFS | canonical? | status |
|---|---|---|---:|---|---|---|
| active canonical parent `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.blend` | `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a` | 614872 | YES | **YES (active)** | CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN; unchanged |
| active canonical parent GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.glb` | `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65` | 2332388 | YES | **YES (active)** | unchanged |
| reviewed standalone `.blend` | `art/assets/TLAW_REPAIR_FACE_01.blend` | `65631fa9c309902865dbcf9c71ff259fd89cfb0a652b3ede3d3c744e4e24235f` | 109331 | YES | NO | REVIEWED STANDALONE; `INTEGRATION_ELIGIBLE=YES` |
| reviewed standalone GLB | `art/assets/TLAW_REPAIR_FACE_01.glb` | `ebdce71930b1f61b2c22680640f275b94514e8285c3836cb225b9e794c24d0aa` | 89824 | YES | NO | REVIEWED STANDALONE |
| reviewed integrated candidate `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.blend` | `b940f515e42795aaab785662067da1b39ef26226ddbc76410398faf4be683a5b` | 629460 | YES | NO | REVIEWED INTEGRATED CANDIDATE; `PROMOTION_ELIGIBLE=YES`; **NOT CANONICAL** |
| reviewed integrated candidate GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.glb` | `68912b8b8862daea523ba5d8532a63451de4cd1a6b0a3f0416160020d5090ca7` | 2415104 | YES | NO | paired scene-scoped GLB; **NOT CANONICAL** |

Staged Git LFS pointers used `oid sha256:<owner SHA>` and matching sizes. No raw large binary was committed outside LFS.

---

## Review / provenance versioned

| file | SHA-256 | size |
|---|---|---:|
| `art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01.report.md` | `2efa931652bf2fdba026345fc57c8629d50d7ccc897ad9170f99b31508994bab` | 8747 |
| `art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01_REVIEW_01.report.md` | `98795bc19526c74cc801c04423f8005e05ef630c2aac9bbab54f022cbe699d94` | 21378 |
| `art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01_REVIEW_01.validation.json` | `c465d8ed019090afcab5123b710c5de2d03f5ba0546bdc3f6a1221c779dbd12b` | 4072 |
| `art/environment/sawmill/TLAW_REPAIR_FACE_01_REVIEW_01.report.md` | `c1b4056947d484263953a2992d637d8187b951668b91aa03e7562e48e851249d` | 18505 |
| `art/environment/sawmill/TLAW_REPAIR_FACE_01_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md` | `80eb60b7dab5c78bc2d962e1e0f0f6e66c99f516cc13b804a1de1811d37b7e81` | 7695 |

Independent integration review authority: `VERDICT=APPROVE_WITH_NON_BLOCKING_NOTES`, `PROMOTION_ELIGIBLE=YES`.

Imported evidence files are immutable. No JSON reserialization. No Markdown rewriting. No line-ending normalization.

---

## Optional provenance not Git-versioned

Standalone worker production report identity is established locally from the reviewed production packet:

- `C:\Projects\TLAW_Handoff\CURRENT\TLAW_REPAIR_FACE_01_PRODUCTION_01\TLAW_REPAIR_FACE_01_PRODUCTION_01.report.md`
- SHA-256 `e62929f9fde2c43fc0a8183840c62b120d34784fc392a719870a553529c0abd5`
- size `11062`
- CRLF

Not staged. Existing `* text=auto eol=lf` would rewrite CRLF to LF and change the SHA-256. Exact-byte policy forbids that substitution. Not reconstructed.

Handoff ZIP remains external custody reference (not Git-versioned):

- `C:\Projects\TLAW_Handoff\CURRENT\TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01.zip`
- SHA-256 `2f9cf0ae1f0496294f554414e292796c40b0b48f3782c72e00fa7cad39306128`
- size `7639338`

---

## Staged-diff audit (artifacts commit)

`git diff --cached --name-only` contained only:

- `.gitattributes` (three path-specific whitespace exceptions)
- `art/assets/TLAW_REPAIR_FACE_01.blend`
- `art/assets/TLAW_REPAIR_FACE_01.glb`
- `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.blend`
- `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.glb`
- `art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01.report.md`
- `art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01_REVIEW_01.report.md`
- `art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01_REVIEW_01.validation.json`
- `art/environment/sawmill/TLAW_REPAIR_FACE_01_REVIEW_01.report.md`
- `art/environment/sawmill/TLAW_REPAIR_FACE_01_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md`

No C#. No Unity. No validator semantic changes. No Blender scripts. No gameplay/domain files. No unrelated 3D artifacts. No current canonical binary modifications.

Active canonical LFS pointer/blob SHA did not change.

Each `.blend`/`.glb` showed as a 3-line LFS pointer.

Commit message: `chore(art): version reviewed Repair Face artifacts with Git LFS`

---

## Local verification (artifacts commit)

- `git diff --check 65fa89c5179798eedf248590b4f1ed87c9b90905...HEAD` PASS (exit 0)
- `Tlaw.Verify --expected-head 7bce40d33d0f77a2a2b0020928b7f104e8643f2d --expected-base 65fa89c5179798eedf248590b4f1ed87c9b90905` PASS

`LOCAL_VERIFY=PASS` for the artifacts commit. Re-run on the docs follow-up commit is recorded below after that commit exists.

---

## Push / PR / LFS upload

- Branch pushed: `origin/task/TLAW-repair-face-durable-production-artifacts-lfs-01`
- LFS upload: `Uploading LFS objects: 100% (4/4), 3.2 MB`
- PR: [#196](https://github.com/baroentgray/the-logs-are-wrong/pull/196) against `main`
- PR title: `TLAW: version reviewed Repair Face production artifacts`
- PR body states `CUSTODY ONLY — NOT CANONICAL PROMOTION`, `PROMOTION_PERFORMED=NO`, `ACTIVE_CANONICAL_UNCHANGED=YES`
- PR state: OPEN, not merged
- `origin/main` after push still `65fa89c5179798eedf248590b4f1ed87c9b90905`

`LFS_UPLOAD=PASS`

---

## GitHub CI (artifacts commit)

Repository verification on `7bce40d33d0f77a2a2b0020928b7f104e8643f2d`:

- run `35325551018`
- job `Deterministic verification` SUCCESS (1m8s)
- https://github.com/baroentgray/the-logs-are-wrong/actions/runs/35325551018/job/105537637498
- conclusion `SUCCESS`

`GITHUB_CI=SUCCESS` on the artifacts head. Docs follow-up CI is required on the final head.

---

## Remote LFS recheck

Temporary clone:

`C:\Projects\_tmp_tlaw_repair_face_lfs_recheck_01\repo`

- branch `task/TLAW-repair-face-durable-production-artifacts-lfs-01`
- HEAD `7bce40d33d0f77a2a2b0020928b7f104e8643f2d`
- `Filtering content: 100% (12/12)`
- integrated BLEND materialized to `b940f515e42795aaab785662067da1b39ef26226ddbc76410398faf4be683a5b` / 629460
- integrated GLB materialized to `68912b8b8862daea523ba5d8532a63451de4cd1a6b0a3f0416160020d5090ca7` / 2415104
- standalone BLEND materialized to `65631fa9c309902865dbcf9c71ff259fd89cfb0a652b3ede3d3c744e4e24235f` / 109331
- standalone GLB materialized to `ebdce71930b1f61b2c22680640f275b94514e8285c3836cb225b9e794c24d0aa` / 89824
- active canonical BLEND/GLB unchanged `be48b091…` / `eba529c0…`
- materialized integrated GLB starts with `glTF`
- `git lfs ls-files` shows `*` (full object) for all four Repair Face binaries

`REMOTE_RECHECK=PASS`

---

## Candidate / canonical status

- Active canonical **unchanged**: `be48b091…` / `eba529c0…`
- Repair Face remains `REVIEWED INTEGRATED CANDIDATE — PROMOTION_ELIGIBLE — NOT CANONICAL`
- `PROMOTION_PERFORMED=NO`
- Git/LFS presence does **not** grant ACCEPTED or CANONICAL status

---

## Handoff / registry

`C:\Projects\TLAW_Handoff\registry\ARTIFACT_REGISTRY.csv` updated with Repair Face integrated-candidate status:

`REVIEWED / PROMOTION_ELIGIBLE / GIT_LFS_CUSTODIED / NOT_CANONICAL`

The registry is external supporting custody, not canonical authority. Historical rows were not overwritten.

Local recovery copies and `TLAW_Handoff\CURRENT` were **not** deleted.

---

## Hard boundaries honored

No Blender save/re-export. No geometry/material mutation. No Repair Face correction. No Unity. No Astra. No gameplay/domain changes. No canonical disposition change. No promotion. No PR merge. No next gate.

The active canonical remains `be48b091…` / `eba529c0…` throughout this gate.
