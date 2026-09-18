# TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01

Git/LFS custody manifest for the reviewed Compound Applicator production lineage.

**Git/LFS presence does not grant ACCEPTED or CANONICAL status.**

Disposition remains controlled by identified owner / Control Center decisions.

This file remains the Compound Applicator lineage custody record. Historical
custody-gate and PR #195 promotion facts below are preserved and are not
rewritten as though they were wrong. Imported immutable review reports are not
altered.

After merge of the Repair Face promotion PR, this file no longer claims that
`be48b091…` / `eba529c0…` remain the effective active 3D canonical.

---

## Current disposition — TLAW_REPAIR_FACE_01_CANONICAL_PROMOTION_01 (overlay)

Owner gate: `TLAW_REPAIR_FACE_01_CANONICAL_PROMOTION_01`

Promotion changeset branch: `task/TLAW-repair-face-canonical-promotion-01`

Repository base: `origin/main` `e0e4073f10fae5058a0059d66c17ced5cc7b46f9` (PR #196 durable Repair Face artifact custody merge)

`PROMOTION_IS_DISPOSITION_ONLY=YES`

`PRODUCTION_BINARY_MUTATION=NO`

`PROMOTION_EFFECTIVE_ON_MERGE_ONLY=YES`

`INTEGRATED_CANDIDATE ≠ ACTIVE_CANONICAL UNTIL MERGE`

Until merge of the Repair Face promotion PR, effective active canonical on
`main` remains:

- `.blend` `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a`
- scene-scoped GLB `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65`

Conditional on merge of the Repair Face promotion PR:

| role | SHA-256 | disposition |
|---|---|---|
| new active canonical `.blend` | `b940f515e42795aaab785662067da1b39ef26226ddbc76410398faf4be683a5b` | **CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN** |
| new active canonical scene-scoped GLB | `68912b8b8862daea523ba5d8532a63451de4cd1a6b0a3f0416160020d5090ca7` | **CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN** |
| previous canonical `.blend` | `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a` | **PREVIOUS CANONICAL BASELINE / PARENT** |
| previous canonical scene-scoped GLB | `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65` | **PREVIOUS CANONICAL BASELINE / PARENT** |

`be48b091…` / `eba529c0…` are not deleted. They become previous canonical parent
after merge only.

The Compound Applicator promotion section below is **historical** (PR #195,
merged). It records how `be48b091…` became canonical. It must not be read as
claiming that pair remains active after the Repair Face promotion PR merges.

---

## Historical disposition — TLAW_COMPOUND_APPLICATOR_01_CANONICAL_PROMOTION_01

Owner gate: `TLAW_COMPOUND_APPLICATOR_01_CANONICAL_PROMOTION_01`

Promotion changeset branch: `task/TLAW-compound-applicator-canonical-promotion-01`

Repository base: `origin/main` `93a87f625269b4e5cff304065eb6dc876c3944ad` (PR #194 custody-only merge)

`PROMOTION_IS_DISPOSITION_ONLY=YES`

`PRODUCTION_BINARY_MUTATION=NO`

`PROMOTION_EFFECTIVE_ON_MERGE_ONLY=YES`

This promotion changeset does **not** modify production binaries. Exact reviewed
cleanup candidate bytes already versioned on `main` become the new canonical
production baseline **only when this promotion PR is separately owner-authorized
for merge**. Until that merge, effective active canonical on `main` remains:

- `.blend` `3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5`
- scene-scoped GLB `48b1884ad81c7f6aebc7cb75c6e46da5e279bb7c7810052d91ab6931e925d665`

Conditional on merge of this promotion PR:

| role | SHA-256 | disposition |
|---|---|---|
| new active canonical `.blend` | `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a` | **CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN** |
| new active canonical scene-scoped GLB | `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65` | **CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN** |
| previous canonical `.blend` | `3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5` | **PREVIOUS CANONICAL BASELINE / PARENT** |
| previous canonical scene-scoped GLB | `48b1884ad81c7f6aebc7cb75c6e46da5e279bb7c7810052d91ab6931e925d665` | **PREVIOUS CANONICAL BASELINE / PARENT** |

Independent review authority:

`TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01`

- `VERDICT: APPROVE WITH NON-BLOCKING NOTES`
- `PROMOTION_ELIGIBLE=YES`
- reviewed identities `be48b091…` / `eba529c0…`

`CompoundProxy = REMOVED`. Promotion-blocking condition resolved. Do not restore.

Applicator accepted role carried forward unchanged:

- `RESIN_SALT_APPLICATOR`
- spatial contract `SIDE_NODE_FIXED_LOCAL_ZONE`
- gameplay adapter `attempted_item=salt`

No new applicator state/semantics are authorized.

Durable warnings retained (not converted to PASS):

- `LocalSplashGuard <-> log ~= 0.025000052 m` — `DURABLE WARN`
- inherited canonical Procedure margin `ProcedureGuide <-> log ~= 0.005000019 m` — `DURABLE INHERITED WARN`
- Unity / engine import for `be48b091…` / `eba529c0…` = `UNTESTED` — `NON-BLOCKING WARN`

---

## Lineage (required)

```
3ccf52e2…  PREVIOUS CANONICAL BASELINE / PARENT
           (was CANONICAL PRODUCTION BASELINE before this promotion)
     +
9c39aaa7…  reviewed standalone CORR01  (REVIEWED CORRECTED STANDALONE; not canonical)
     =
0d9a6677…  reviewed integrated INT01
           independently reviewed; PROMOTION_ELIGIBLE=NO until CompoundProxy cleanup
           status: REVIEWED INTEGRATED CANDIDATE — SUPERSEDED FOR PROMOTION BY PROXY CLEANUP
     ->
be48b091…  bounded CompoundProxy cleanup
           independently reviewed: APPROVE WITH NON-BLOCKING NOTES
           PROMOTION_ELIGIBLE=YES
           after merge of this promotion PR:
           CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN
     +
eba529c0…  paired scene-scoped cleanup GLB
           after merge of this promotion PR: paired canonical GLB
```

SHA values and lineage are preserved. Prior canonical binaries are not deleted.

---

## Adopted production binaries

Identity is SHA-256. Filenames are stable production names, not SHA embeddings.

SOURCE_SHA == REPO_PATH_SHA == OWNER_REQUIRED_SHA for every row. No Blender save. No re-export. No binary normalization. This promotion gate does not mutate any production binary.

| logical role | repository path | SHA-256 | size | disposition / status | parent / source lineage | review report reference | canonical? |
|---|---|---|---:|---|---|---|---|
| Previous canonical parent `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_SAW01_OT01_PROCSPUR_CORR01.blend` | `3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5` | 531039 | PREVIOUS CANONICAL BASELINE / PARENT (effective until this promotion PR merges; remains durable history after merge) | Procedure-spur unrotated-log CORR01 production baseline | prior canonical promotion record; not re-opened by this gate | **NO** (previous; still effective on `main` until merge) |
| Previous canonical parent scene-scoped GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_SAW01_OT01_PROCSPUR_CORR01.glb` | `48b1884ad81c7f6aebc7cb75c6e46da5e279bb7c7810052d91ab6931e925d665` | 2138052 | PREVIOUS CANONICAL BASELINE / PARENT | paired export of `3ccf52e2…` | same | **NO** (previous; still effective on `main` until merge) |
| Reviewed corrected standalone applicator `.blend` | `art/assets/TLAW_COMPOUND_APPLICATOR_01_CORR01.blend` | `9c39aaa75de76c13827c736f2653ce30405fb04e1680139bc64133870796a059` | 178676 | REVIEWED CORRECTED STANDALONE; not canonical | corrected from original standalone `712c310f…` | `TLAW_COMPOUND_APPLICATOR_01_CORR01.report.md` + `TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01.report.md` | NO |
| Reviewed corrected standalone applicator GLB | `art/assets/TLAW_COMPOUND_APPLICATOR_01_CORR01.glb` | `a6b3c135d1c079528663c6ab894aede4489d8b8d6cc4818b7e0d8e9b1d6ea4dc` | 198520 | REVIEWED CORRECTED STANDALONE; not canonical | paired scene-scoped GLB for `9c39aaa7…` | same | NO |
| Reviewed integrated INT01 candidate `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01.blend` | `0d9a66777a28b949e910885082ae3c4a4c54382a4957c331508e9c10fb226c1a` | 615378 | REVIEWED INTEGRATED CANDIDATE — SUPERSEDED FOR PROMOTION BY PROXY CLEANUP; keep for durable lineage; `PROMOTION_ELIGIBLE=NO` | `9c39aaa7…` transplanted into canonical `3ccf52e2…` | `TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01.report.md`; independent review bytes **missing locally** (see below) | NO |
| Reviewed integrated INT01 candidate GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01.glb` | `04966c05f08e94a88409b526d4c3b529e450a70f2171b270057f79df1d3e1d0f` | 2336208 | same as INT01 blend | paired scene-scoped GLB for `0d9a6677…` | same | NO |
| New canonical production baseline `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.blend` | `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a` | 614872 | CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN after merge of this promotion PR; `PROMOTION_ELIGIBLE=YES`; independent review `APPROVE WITH NON-BLOCKING NOTES`; `CompoundProxy = REMOVED` | lineage from INT01 `0d9a6677…`; `CompoundProxy` removed | `TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01.report.md` + `TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01.report.md` + `TLAW_COMPOUND_APPLICATOR_01_CANONICAL_PROMOTION_01.report.md` | **YES** (active after merge only) |
| New canonical production baseline GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.glb` | `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65` | 2332388 | CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN after merge of this promotion PR; paired scene-scoped GLB | paired export of `be48b091…` | same | **YES** (active after merge only) |

---

## Review / provenance files versioned with the custody gate (immutable)

These rows record the exact imported reports versioned by
`TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01`. This promotion gate does **not**
alter those files.

| file | SHA-256 | size | notes |
|---|---|---:|---|
| `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_CORR01.report.md` | `1d1d3848e1bbab98976516518e6b987e3c794569801efc48ed60573e63cb928d` | 14249 | Worker report. LF. Exact persistent sawmill copy. |
| `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01.report.md` | `3243e3ab28a22e7665631acd63661892743bc723e4b4374f43ec5e17f0186647` | 18692 | Independent review. Verdict `APPROVE WITH NON-BLOCKING NOTES`. `INTEGRATION_ELIGIBLE=YES`. Exact bytes copied from Downloads filename `TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_0101.report.md` (download-name collision). Document heading remains `TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01`. |
| `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01.report.md` | `871ca600e02d5d61cf09ba4c682a80ac5a46d149caf2f488ca4648883e0244d8` | 12143 | Worker report. Exact current sawmill / UPLOAD_READY copy. |
| `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01.report.md` | `641e4deb7d0dfd072cd3f9c9b7b3050c0baa7dd37765d92069d69ccdf40b7d30` | 11391 | Worker report. Exact persistent sawmill copy. |
| `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01.report.md` | `55c9d7e77fc85e220b2d59983944530cacbd7ece39e16909654fc2e557ffeba7` | 22082 | Independent cleanup review. Authoritative evidence for `be48b091…` + `eba529c0…` + `PROMOTION_ELIGIBLE=YES`. Verdict `APPROVE WITH NON-BLOCKING NOTES`. |
| `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01.validation.json` | `13e25fa22e7acbacad29fb7cfbebf412150cc53719614fe25897dc2719aec936` | 3423 | Machine-readable companion to the cleanup independent review. LF. Exact bytes. |

Worker `validate.json` files under `_app_corr01/`, `_app_int01/`, and `_app_proxyclean01/dumps/` remain on the local working tree and in recovery trees. They were **not** staged: they are CRLF, and existing `* text=auto eol=lf` would silently rewrite them. Exact-byte policy forbids that substitution.

Promotion report added by this gate (not an imported immutable review):

- `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_CANONICAL_PROMOTION_01.report.md`

---

## Missing / discrepant evidence (not silently substituted)

Historical custody-gate record. Not re-opened. Not used to block this
disposition-only promotion of already independently reviewed cleanup bytes.

### Independent INT01 review — MISSING locally

Expected exact file:

`TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01_REVIEW_01.report.md`

Referenced by the cleanup independent review as:

- SHA-256 `8d358fed4b41d4ec3b5a001da1c3695ecba416b5eec06076b13508b5b5756beb`
- size `20716`

Not found under `TheLogsAreWrong\art`, `TLAW_Artifact_Recovery`, `TLAW_Handoff`, or owner Downloads at custody-gate execution time.

Cleanup independent review records that this prior review was available in that review runtime and stated:

- `APPROVE WITH NON-BLOCKING NOTES`
- `CompoundProxy = NON-BLOCKING_CLEANUP_REQUIRED_BEFORE_PROMOTION`
- `PROMOTION_ELIGIBLE=NO`

The custody gate therefore **recorded** that INT01 was independently reviewed `PROMOTION_ELIGIBLE=NO` until proxy cleanup, but **did not version the exact prior-review bytes**. This promotion gate does not invent those bytes.

### CORR01 independent review — two persistent copies, different verdicts

| source filename | SHA-256 | size | verdict |
|---|---|---:|---|
| Downloads `TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_0101.report.md` | `3243e3ab28a22e7665631acd63661892743bc723e4b4374f43ec5e17f0186647` | 18692 | `APPROVE WITH NON-BLOCKING NOTES` / `INTEGRATION_ELIGIBLE=YES` — **versioned** as `…REVIEW_01.report.md` |
| Downloads `TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01.report.md` | `b14b3a1815db9be9bafdffc240850a9ca01465a50a360936457008bd604e26e2` | 19033 | earlier `REQUEST CHANGES` / `INTEGRATION_ELIGIBLE=NO` (bytes unavailable to that reviewer) |

The REQUEST CHANGES copy is **not** stored under the owner-requested filename. It is a different verdict, not a serialization of the same review. Owner/registry status for CORR01 is the APPROVE copy.

### INT01 worker report — ZIP vs current persistent copy

| copy | SHA-256 | size |
|---|---|---:|
| current sawmill + recovery UPLOAD_READY | `871ca600e02d5d61cf09ba4c682a80ac5a46d149caf2f488ca4648883e0244d8` | 12143 |
| INT01 review ZIP `d06a1ec1…` member (extracted `_tmp_int01_bundle_verify`) | `01ac221fae87f2d970e7d11c4fa7cb7a90f0bed51b934f17cde6f7a8192c5a1b` | 11598 |

The custody gate versioned the current persistent sawmill copy. The ZIP member is a different serialization and was not substituted.

### Cleanup worker report — Downloads vs production

Downloads copies named `TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01.report.md` (and `…011` / `…012`) hash `08374376…` / 5564 bytes. Production sawmill copy hashes `641e4deb…` / 11391 bytes. The custody gate versioned the production sawmill copy only.

---

## LFS rules

Existing `.gitattributes` LFS rules preserved by the custody gate.

Added by the custody gate exactly:

```
*.glb filter=lfs diff=lfs merge=lfs -text
```

`.blend` was already LFS-tracked. `.glb` is now LFS-tracked. This promotion gate does not change LFS rules.

---

## Historical custody-gate non-effects (PR #194)

`TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01` itself performed no canonical
promotion. Active canonical identity was unchanged by that custody merge.
Those facts remain historically true of PR #194 / merge `93a87f62…`.

This file's **current** disposition is the promotion overlay above, effective
only when the promotion PR merges.
