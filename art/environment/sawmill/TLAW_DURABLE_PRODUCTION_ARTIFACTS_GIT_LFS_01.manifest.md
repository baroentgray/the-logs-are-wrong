# TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01

Git/LFS custody manifest for the reviewed Compound Applicator production lineage.

**This file records custody only. It does not promote any candidate.**

**Git/LFS presence does not grant ACCEPTED or CANONICAL status.**

Disposition remains controlled by identified owner / Control Center decisions.

Gate: `TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01`  
Live base: `origin/main` `8bd02c8f21a3efe7425186f55f8e3cb9030021fe`  
Branch: `task/TLAW-durable-production-artifacts-lfs-01`  
Canonical promotion performed by this gate: **NO**  
Active canonical identity changed by this gate: **NO**

---

## Lineage (required)

```
3ccf52e2…  current canonical parent  (CANONICAL PRODUCTION BASELINE)
     +
9c39aaa7…  reviewed standalone CORR01  (REVIEWED CORRECTED STANDALONE; not canonical)
     =
0d9a6677…  reviewed integrated INT01
           independently reviewed; PROMOTION_ELIGIBLE=NO until CompoundProxy cleanup
           status: REVIEWED INTEGRATED CANDIDATE — SUPERSEDED FOR PROMOTION BY PROXY CLEANUP
     ->
be48b091…  bounded CompoundProxy cleanup candidate
           independently reviewed: APPROVE WITH NON-BLOCKING NOTES
           PROMOTION_ELIGIBLE=YES
           still PROMOTION-ELIGIBLE CANDIDATE only; not canonical
     +
eba529c0…  paired scene-scoped cleanup GLB
```

Active canonical remains:

- `.blend` `3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5`
- scene-scoped GLB `48b1884ad81c7f6aebc7cb75c6e46da5e279bb7c7810052d91ab6931e925d665`

`be48b091…` does **not** become canonical from this Git commit.

---

## Adopted production binaries

Identity is SHA-256. Filenames are stable production names, not SHA embeddings.

SOURCE_SHA == REPO_PATH_SHA == OWNER_REQUIRED_SHA for every row. No Blender save. No re-export. No binary normalization.

| logical role | repository path | SHA-256 | size | disposition / status | parent / source lineage | review report reference | canonical? |
|---|---|---|---:|---|---|---|---|
| Current canonical parent `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_SAW01_OT01_PROCSPUR_CORR01.blend` | `3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5` | 531039 | CANONICAL PRODUCTION BASELINE | Procedure-spur unrotated-log CORR01 production baseline | prior canonical promotion record; not re-opened by this gate | **YES** (active) |
| Current canonical parent scene-scoped GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_SAW01_OT01_PROCSPUR_CORR01.glb` | `48b1884ad81c7f6aebc7cb75c6e46da5e279bb7c7810052d91ab6931e925d665` | 2138052 | CANONICAL PRODUCTION BASELINE | paired export of `3ccf52e2…` | same | **YES** (active) |
| Reviewed corrected standalone applicator `.blend` | `art/assets/TLAW_COMPOUND_APPLICATOR_01_CORR01.blend` | `9c39aaa75de76c13827c736f2653ce30405fb04e1680139bc64133870796a059` | 178676 | REVIEWED CORRECTED STANDALONE; not canonical | corrected from original standalone `712c310f…` | `TLAW_COMPOUND_APPLICATOR_01_CORR01.report.md` + `TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01.report.md` | NO |
| Reviewed corrected standalone applicator GLB | `art/assets/TLAW_COMPOUND_APPLICATOR_01_CORR01.glb` | `a6b3c135d1c079528663c6ab894aede4489d8b8d6cc4818b7e0d8e9b1d6ea4dc` | 198520 | REVIEWED CORRECTED STANDALONE; not canonical | paired scene-scoped GLB for `9c39aaa7…` | same | NO |
| Reviewed integrated INT01 candidate `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01.blend` | `0d9a66777a28b949e910885082ae3c4a4c54382a4957c331508e9c10fb226c1a` | 615378 | REVIEWED INTEGRATED CANDIDATE — SUPERSEDED FOR PROMOTION BY PROXY CLEANUP; keep for durable lineage; `PROMOTION_ELIGIBLE=NO` until CompoundProxy cleanup | `9c39aaa7…` transplanted into canonical `3ccf52e2…` | `TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01.report.md`; independent review bytes **missing locally** (see below) | NO |
| Reviewed integrated INT01 candidate GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01.glb` | `04966c05f08e94a88409b526d4c3b529e450a70f2171b270057f79df1d3e1d0f` | 2336208 | same as INT01 blend | paired scene-scoped GLB for `0d9a6677…` | same | NO |
| Promotion-eligible cleanup candidate `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.blend` | `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a` | 614872 | PROMOTION-ELIGIBLE CANDIDATE only; `PROMOTION_ELIGIBLE=YES`; independent review `APPROVE WITH NON-BLOCKING NOTES`; **not canonical** | lineage from INT01 `0d9a6677…`; `CompoundProxy` removed | `TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01.report.md` + `TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01.report.md` | NO |
| Promotion-eligible cleanup candidate GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.glb` | `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65` | 2332388 | PROMOTION-ELIGIBLE CANDIDATE only; paired scene-scoped GLB; **not canonical** | paired export of `be48b091…` | same | NO |

---

## Review / provenance files versioned with this gate

| file | SHA-256 | size | notes |
|---|---|---:|---|
| `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_CORR01.report.md` | `1d1d3848e1bbab98976516518e6b987e3c794569801efc48ed60573e63cb928d` | 14249 | Worker report. LF. Exact persistent sawmill copy. |
| `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01.report.md` | `3243e3ab28a22e7665631acd63661892743bc723e4b4374f43ec5e17f0186647` | 18692 | Independent review. Verdict `APPROVE WITH NON-BLOCKING NOTES`. `INTEGRATION_ELIGIBLE=YES`. Exact bytes copied from Downloads filename `TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_0101.report.md` (download-name collision). Document heading remains `TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01`. |
| `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01.report.md` | `871ca600e02d5d61cf09ba4c682a80ac5a46d149caf2f488ca4648883e0244d8` | 12143 | Worker report. Exact current sawmill / UPLOAD_READY copy. |
| `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01.report.md` | `641e4deb7d0dfd072cd3f9c9b7b3050c0baa7dd37765d92069d69ccdf40b7d30` | 11391 | Worker report. Exact persistent sawmill copy. |
| `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01.report.md` | `55c9d7e77fc85e220b2d59983944530cacbd7ece39e16909654fc2e557ffeba7` | 22082 | Independent cleanup review. Authoritative evidence for `be48b091…` + `eba529c0…` + `PROMOTION_ELIGIBLE=YES`. Verdict `APPROVE WITH NON-BLOCKING NOTES`. |
| `art/environment/sawmill/TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01.validation.json` | `13e25fa22e7acbacad29fb7cfbebf412150cc53719614fe25897dc2719aec936` | 3423 | Machine-readable companion to the cleanup independent review. LF. Exact bytes. |

Worker `validate.json` files under `_app_corr01/`, `_app_int01/`, and `_app_proxyclean01/dumps/` remain on the local working tree and in recovery trees. They were **not** staged: they are CRLF, and existing `* text=auto eol=lf` would silently rewrite them. Exact-byte policy forbids that substitution.

---

## Missing / discrepant evidence (not silently substituted)

### Independent INT01 review — MISSING locally

Expected exact file:

`TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01_REVIEW_01.report.md`

Referenced by the cleanup independent review as:

- SHA-256 `8d358fed4b41d4ec3b5a001da1c3695ecba416b5eec06076b13508b5b5756beb`
- size `20716`

Not found under `TheLogsAreWrong\art`, `TLAW_Artifact_Recovery`, `TLAW_Handoff`, or owner Downloads at execution time.

Cleanup independent review records that this prior review was available in that review runtime and stated:

- `APPROVE WITH NON-BLOCKING NOTES`
- `CompoundProxy = NON-BLOCKING_CLEANUP_REQUIRED_BEFORE_PROMOTION`
- `PROMOTION_ELIGIBLE=NO`

This gate therefore **records** that INT01 was independently reviewed `PROMOTION_ELIGIBLE=NO` until proxy cleanup, but **does not version the exact prior-review bytes**.

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

This gate versions the current persistent sawmill copy. The ZIP member is a different serialization and was not substituted.

### Cleanup worker report — Downloads vs production

Downloads copies named `TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01.report.md` (and `…011` / `…012`) hash `08374376…` / 5564 bytes. Production sawmill copy hashes `641e4deb…` / 11391 bytes. This gate versions the production sawmill copy only.

---

## LFS rules

Existing `.gitattributes` LFS rules preserved.

Added exactly:

```
*.glb filter=lfs diff=lfs merge=lfs -text
```

`.blend` was already LFS-tracked. `.glb` is now LFS-tracked. No existing LFS rule was removed or normalized.

---

## Explicit non-effects

- No canonical promotion.
- No active-canonical identity change.
- No Blender edit / re-export / geometry / material change.
- No Unity / Pipeline / Astra / gameplay change.
- No PR merge.
- Local recovery copies and `TLAW_Handoff\CURRENT` were not deleted.
- After successful Git/LFS adoption, Git/LFS is the durable source for these versioned milestones. Handoff CURRENT remains only for active owner uploads.
