# TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01

Custody / versioning gate. Not canonical promotion.

**RESULT=PASS WITH WARN**

Executor: Grok Build in local repository `C:\Projects\TheLogsAreWrong` (Control Center authorized Claude Code executor; this session executed the gate).

---

## Final fields

```
RESULT=PASS WITH WARN
BASE_MAIN_SHA=8bd02c8f21a3efe7425186f55f8e3cb9030021fe
BRANCH=task/TLAW-durable-production-artifacts-lfs-01
COMMIT_SHA=faf01fe4bb273a388bcb00a6d28f120226445cdf
PR_NUMBER=194
PR_URL=https://github.com/baroentgray/the-logs-are-wrong/pull/194
GLB_LFS_RULE=PASS
LFS_UPLOAD=PASS
REMOTE_RECHECK=PASS
CANONICAL_BLEND_SHA256=3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5
CANDIDATE_BLEND_SHA256=be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a
CANDIDATE_GLB_SHA256=eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65
ACTIVE_CANONICAL_UNCHANGED=YES
PROMOTION_PERFORMED=NO
READY_FOR_CONTROL_CENTER_REVIEW=YES
```

`COMMIT_SHA` above is the artifacts/LFS commit. This report is a follow-up docs commit on the same branch/PR.

---

## Live baseline preflight

| check | result |
|---|---|
| `git fetch origin` | PASS |
| `origin/main` | `8bd02c8f21a3efe7425186f55f8e3cb9030021fe` |
| expected main | `8bd02c8f21a3efe7425186f55f8e3cb9030021fe` |
| drift | NONE |
| working tree at fetch | dirty / untracked (see exclusions) |
| starting local branch | `task/TLAW-disposal-lever-01-reference-views` @ `10a99b7df61463d57ed8b841fa656671085984c9` |
| dedicated branch created from | `origin/main` (exact) |

Pre-existing untracked paths were identified and **excluded** from this gate:

- `.claude/`
- `.review-artifact-016/` `.review-artifact-017/` `.review-artifact-018/`
- remainder of untracked `art/` (historical scenes, `_app_*` scratch, other assemblies)
- `prototype/.shots/`
- extra `prototype/art_pipeline/` and `prototype/layout_editor/`
- `tools/3d_validation/`

No unrelated source/code/config/UI/gameplay files were staged.

---

## `.gitattributes` before / after

**Before** (live `origin/main`):

```
*.blend filter=lfs diff=lfs merge=lfs -text
*.fbx filter=lfs diff=lfs merge=lfs -text
```

plus existing audio/video/image/archive LFS rules and Unity whitespace exemptions. No `*.glb` rule.

**After** (this branch): added exactly one line after `*.blend`:

```
*.glb filter=lfs diff=lfs merge=lfs -text
```

No existing LFS rule removed or normalized.

Verification:

```
git check-attr filter -- art/assets/TLAW_COMPOUND_APPLICATOR_01_CORR01.blend
→ filter: lfs

git check-attr filter -- art/assets/TLAW_COMPOUND_APPLICATOR_01_CORR01.glb
→ filter: lfs
```

`git lfs track` lists both `*.blend` and `*.glb`.

---

## Adopted artifact table

SOURCE_SHA == REPO_PATH_SHA == OWNER_REQUIRED_SHA. No Blender save. No re-export.

| role | path | SHA-256 | size | LFS | canonical? | status |
|---|---|---|---:|---|---|---|
| canonical parent `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_SAW01_OT01_PROCSPUR_CORR01.blend` | `3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5` | 531039 | YES | **YES (active)** | CANONICAL PRODUCTION BASELINE |
| canonical parent GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_SAW01_OT01_PROCSPUR_CORR01.glb` | `48b1884ad81c7f6aebc7cb75c6e46da5e279bb7c7810052d91ab6931e925d665` | 2138052 | YES | **YES (active)** | CANONICAL PRODUCTION BASELINE |
| reviewed standalone `.blend` | `art/assets/TLAW_COMPOUND_APPLICATOR_01_CORR01.blend` | `9c39aaa75de76c13827c736f2653ce30405fb04e1680139bc64133870796a059` | 178676 | YES | NO | REVIEWED CORRECTED STANDALONE |
| reviewed standalone GLB | `art/assets/TLAW_COMPOUND_APPLICATOR_01_CORR01.glb` | `a6b3c135d1c079528663c6ab894aede4489d8b8d6cc4818b7e0d8e9b1d6ea4dc` | 198520 | YES | NO | REVIEWED CORRECTED STANDALONE |
| reviewed INT01 `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01.blend` | `0d9a66777a28b949e910885082ae3c4a4c54382a4957c331508e9c10fb226c1a` | 615378 | YES | NO | REVIEWED INTEGRATED CANDIDATE — SUPERSEDED FOR PROMOTION BY PROXY CLEANUP; `PROMOTION_ELIGIBLE=NO` until CompoundProxy cleanup |
| reviewed INT01 GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01.glb` | `04966c05f08e94a88409b526d4c3b529e450a70f2171b270057f79df1d3e1d0f` | 2336208 | YES | NO | same |
| promotion-eligible cleanup `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.blend` | `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a` | 614872 | YES | NO | PROMOTION-ELIGIBLE CANDIDATE; independent review `APPROVE WITH NON-BLOCKING NOTES`; `PROMOTION_ELIGIBLE=YES`; **not canonical** |
| promotion-eligible cleanup GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.glb` | `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65` | 2332388 | YES | NO | paired GLB; not canonical |

Staged Git LFS pointers used `oid sha256:<owner SHA>` and matching sizes. No raw large binary was committed outside LFS.

---

## Review / provenance versioned

| file | SHA-256 | size |
|---|---|---:|
| `TLAW_COMPOUND_APPLICATOR_01_CORR01.report.md` | `1d1d3848e1bbab98976516518e6b987e3c794569801efc48ed60573e63cb928d` | 14249 |
| `TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01.report.md` | `3243e3ab28a22e7665631acd63661892743bc723e4b4374f43ec5e17f0186647` | 18692 |
| `TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01.report.md` | `871ca600e02d5d61cf09ba4c682a80ac5a46d149caf2f488ca4648883e0244d8` | 12143 |
| `TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01.report.md` | `641e4deb7d0dfd072cd3f9c9b7b3050c0baa7dd37765d92069d69ccdf40b7d30` | 11391 |
| `TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01.report.md` | `55c9d7e77fc85e220b2d59983944530cacbd7ece39e16909654fc2e557ffeba7` | 22082 |
| `TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01.validation.json` | `13e25fa22e7acbacad29fb7cfbebf412150cc53719614fe25897dc2719aec936` | 3423 |
| `TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md` | `b3c6af2efd5cfb7067801afc7ff07ab7697ca4697cbca5394786add8b45c6895` | 10554 |

CORR01 independent review bytes are the APPROVE copy (`INTEGRATION_ELIGIBLE=YES`). Source Downloads filename was `…REVIEW_0101.report.md` (download-name collision). Stored under the owner-requested filename. Document heading is `TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01`.

---

## Missing evidence / discrepancies (WARN)

1. **INT01 independent review bytes missing locally.** Expected `TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01_REVIEW_01.report.md` SHA-256 `8d358fed4b41d4ec3b5a001da1c3695ecba416b5eec06076b13508b5b5756beb` size `20716`. Cleanup independent review records that file was present in the review runtime with `PROMOTION_ELIGIBLE=NO` until CompoundProxy cleanup. Exact bytes were not found under `art/`, recovery, handoff, or Downloads. Not reconstructed.

2. **Earlier CORR01 independent review (REQUEST CHANGES)** exists in Downloads as `TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01.report.md` SHA-256 `b14b3a1815db9be9bafdffc240850a9ca01465a50a360936457008bd604e26e2` size `19033`. Different verdict, not a serialization of the APPROVE review. Not stored under the owner-requested filename.

3. **INT01 worker report ZIP member differs** from current sawmill copy: ZIP `d06a1ec1…` member SHA `01ac221f…` / 11598 B vs versioned sawmill SHA `871ca600…` / 12143 B. Current persistent copy was versioned. ZIP member was not substituted.

4. **Downloads cleanup worker report** SHA `08374376…` / 5564 B differs from production sawmill SHA `641e4deb…` / 11391 B. Production copy versioned.

5. Worker `validate.json` under `_app_corr01/`, `_app_int01/`, `_app_proxyclean01/dumps/` left untracked. They are CRLF; existing `* text=auto eol=lf` would rewrite them.

---

## Staged diff audit (artifacts commit)

`git diff --cached --name-only` contained only:

- `.gitattributes`
- 8 specified `.blend` / `.glb`
- specified production review `.md` / cleanup independent-review `.json`
- custody manifest

`git diff --cached --stat`: 16 files, 2887 insertions. Each `.blend`/`.glb` showed as a 3-line LFS pointer.

Commit message: `chore(art): version reviewed production artifacts with Git LFS`

---

## Push / PR / LFS upload

- Branch pushed: `origin/task/TLAW-durable-production-artifacts-lfs-01`
- LFS upload: `Uploading LFS objects: 100% (8/8), 8.9 MB`
- PR: [#194](https://github.com/baroentgray/the-logs-are-wrong/pull/194) against `main`
- PR state: OPEN, not merged
- `origin/main` after push still `8bd02c8f21a3efe7425186f55f8e3cb9030021fe`

---

## Remote checkout verification

Temporary clone:

`C:\Projects\_tmp_tlaw_lfs_recheck_01\repo`

- branch `task/TLAW-durable-production-artifacts-lfs-01`
- HEAD `faf01fe4bb273a388bcb00a6d28f120226445cdf`
- `Filtering content: 100% (8/8)`
- all eight binaries rehashed **PASS** against owner-required SHA-256
- materialized `.blend` starts with Blender magic (`BLENDER`), not an LFS pointer
- `git lfs ls-files` shows `*` (full object) for all eight

**REMOTE_RECHECK=PASS**

---

## Candidate / canonical status

- Active canonical **unchanged**: `3ccf52e2…` / `48b1884a…`
- `be48b091…` remains PROMOTION-ELIGIBLE CANDIDATE only
- `PROMOTION_PERFORMED=NO`
- Git/LFS presence does **not** grant ACCEPTED or CANONICAL status

---

## Handoff / registry

`C:\Projects\TLAW_Handoff\registry\ARTIFACT_REGISTRY.csv` updated with repo paths and PR/branch custody.

Local recovery copies and `TLAW_Handoff\CURRENT` were **not** deleted. CURRENT remains owner-upload slot only, not the long-term store. Git/LFS is now the durable source for these versioned milestones.

---

## Hard boundaries honored

No Blender edits. No re-export. No geometry/material changes. No Unity. No Pipeline. No Astra. No gameplay/domain changes. No candidate cleanup. No canonical promotion. No active-canonical identity change. No PR merge. No following gate.

---

## CI remediation (controlled resume)

Not a new gate. Bounded remediation of PR #194 after `Repository verification` failed on head `ac1275340ab3695a4c909e5f099d7439b102c1f3`.

### Prior failing CI

| run | event | conclusion | URL |
|---|---|---|---|
| `34889939932` | pull_request | failure | https://github.com/baroentgray/the-logs-are-wrong/actions/runs/34889939932 |
| `34890120585` | pull_request | failure | https://github.com/baroentgray/the-logs-are-wrong/actions/runs/34890120585 |

Restore/build/tests were otherwise successful. Failure lane: `git diff --check` (Tlaw.Verify `diff-check` / `diff-range-check`).

### Root cause

Trailing whitespace in imported immutable provenance reports (markdown two-space hard line-breaks) plus removable trailing whitespace in the custody-owned manifest.

Exact files from `git diff --check 8bd02c8f…...HEAD`:

- `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_CORR01.report.md`
- `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01.report.md`
- `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01.report.md`
- `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01.report.md`
- `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01.report.md`
- `art/environment/sawmill/TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md`

Production binaries / LFS pointers were not the failure.

### Fix

1. Removed trailing whitespace from custody-owned `TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md` (not historical immutable evidence).
2. Added path-specific `.gitattributes` rules only for the five imported reports:

```
art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_CORR01.report.md whitespace=-trailing-space
art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01.report.md whitespace=-trailing-space
art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01.report.md whitespace=-trailing-space
art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01.report.md whitespace=-trailing-space
art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01.report.md whitespace=-trailing-space
```

Did not use `*.md -whitespace`. Did not disable whitespace checking for `art/`. LFS rules for `*.blend` and `*.glb` unchanged.

### Immutable evidence hashes (BEFORE == AFTER)

| file | SHA-256 | result |
|---|---|---|
| `TLAW_COMPOUND_APPLICATOR_01_CORR01.report.md` | `1d1d3848e1bbab98976516518e6b987e3c794569801efc48ed60573e63cb928d` | PRESERVED |
| `TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01.report.md` | `3243e3ab28a22e7665631acd63661892743bc723e4b4374f43ec5e17f0186647` | PRESERVED |
| `TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01.report.md` | `871ca600e02d5d61cf09ba4c682a80ac5a46d149caf2f488ca4648883e0244d8` | PRESERVED |
| `TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01.report.md` | `641e4deb7d0dfd072cd3f9c9b7b3050c0baa7dd37765d92069d69ccdf40b7d30` | PRESERVED |
| `TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01.report.md` | `55c9d7e77fc85e220b2d59983944530cacbd7ece39e16909654fc2e557ffeba7` | PRESERVED |
| `TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01.validation.json` | `13e25fa22e7acbacad29fb7cfbebf412150cc53719614fe25897dc2719aec936` | PRESERVED |

### Production binaries (spot-check)

| artifact | SHA-256 | result |
|---|---|---|
| canonical `.blend` | `3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5` | PRESERVED |
| canonical GLB | `48b1884ad81c7f6aebc7cb75c6e46da5e279bb7c7810052d91ab6931e925d665` | PRESERVED |
| cleanup candidate `.blend` | `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a` | PRESERVED |
| cleanup candidate GLB | `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65` | PRESERVED |

### Local / CI verification

Recorded after the remediation commit and GitHub run. See follow-up fields in this section after push.

- Previous head: `ac1275340ab3695a4c909e5f099d7439b102c1f3`
- Follow-up commit SHA: pending this remediation commit
- Local `git diff --check` on working-tree fix: PASS (exit 0)
- Local Tlaw.Verify: pending clean worktree run after commit
- GitHub Repository verification: pending
- PR #194 remains OPEN / NOT MERGED
- Active canonical unchanged
- Promotion not performed

Custody-owned manifest SHA after whitespace cleanup (identity intentionally changed):

`TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md` previously `b3c6af2e…` / 10554 B. New hash recorded in the remediation commit.
