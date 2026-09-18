# TLAW_REPAIR_FACE_01_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01

Git/LFS custody + disposition record for the reviewed Repair Face production/integration lineage.

**Git/LFS presence does not grant ACCEPTED or CANONICAL status.**

Disposition remains controlled by identified owner / Control Center decisions.

This file is the current-state authoritative custody + disposition record for
these versioned Repair Face production bytes. Historical custody-gate facts below
are preserved. Imported immutable review reports are not altered.

This Repair Face-specific record does **not** rewrite Compound Applicator
historical lineage in `TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md`.
After merge of this promotion PR it supersedes that file's claim that `be48b091…`
/ `eba529c0…` are the effective active canonical.

---

## Current disposition — TLAW_REPAIR_FACE_01_CANONICAL_PROMOTION_01

Owner gate: `TLAW_REPAIR_FACE_01_CANONICAL_PROMOTION_01`

Promotion changeset branch: `task/TLAW-repair-face-canonical-promotion-01`

Repository base: `origin/main` `e0e4073f10fae5058a0059d66c17ced5cc7b46f9` (PR #196 durable Repair Face artifact custody merge)

`PROMOTION_IS_DISPOSITION_ONLY=YES`

`PRODUCTION_BINARY_MUTATION=NO`

`PROMOTION_EFFECTIVE_ON_MERGE_ONLY=YES`

This promotion changeset does **not** modify production binaries. Exact reviewed
integrated Repair Face bytes already versioned on `main` become the new canonical
production baseline **only when this promotion PR is separately owner-authorized
for merge**. Until that merge, effective active canonical on `main` remains:

- `.blend` `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a`
- scene-scoped GLB `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65`

Conditional on merge of this promotion PR:

| role | SHA-256 | disposition |
|---|---|---|
| new active canonical `.blend` | `b940f515e42795aaab785662067da1b39ef26226ddbc76410398faf4be683a5b` | **CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN** |
| new active canonical scene-scoped GLB | `68912b8b8862daea523ba5d8532a63451de4cd1a6b0a3f0416160020d5090ca7` | **CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN** |
| previous canonical `.blend` | `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a` | **PREVIOUS CANONICAL BASELINE / PARENT** |
| previous canonical scene-scoped GLB | `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65` | **PREVIOUS CANONICAL BASELINE / PARENT** |

Independent review authority:

`TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01_REVIEW_01`

- `VERDICT: APPROVE WITH NON-BLOCKING NOTES`
- `PROMOTION_ELIGIBLE=YES`
- reviewed identities `b940f515…` / `68912b8b…`

Until merge:

`EFFECTIVE_ACTIVE_CANONICAL=be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a`

`PR_MERGED=NO`

`RepairFaceProxy = REMOVED`. Do not restore.

`RepairPanelProxy = PRESENT`.

Accepted scope = **CLOSED STATIC PRODUCTION POSE**. This promotion does **not**
approve opening angle, opening animation, swept volume, moving collision, repair
interaction timing, or repair gameplay/procedure.

Durable warnings retained (not converted to PASS):

- W-01 inherited Compound Applicator clearance `LocalSplashGuard <-> log ~= 0.025000052 m` — `DURABLE WARN`
- W-02 inherited Procedure margin `ProcedureGuide <-> log ~= 0.005000019 m` — `DURABLE INHERITED WARN`
- W-03 Unity / engine import for `b940f515…` / `68912b8b…` = `UNTESTED` — `NON-BLOCKING WARN`

RepairStanding separation `≈ 0.110 m` is standing-zone separation, **not** generic
player-route clearance. `ACTUAL_PLAYER_ROUTE_INTRUSION=NO`. Dedicated repair route
remains `NOT_DEFINED`.

---

## Lineage (required)

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

No intermediate candidate is substituted. Previous canonical binaries are not deleted.

Until merge of this promotion PR, Repair Face remains:

`PROMOTION-ELIGIBLE REVIEWED INTEGRATED CANDIDATE — NOT CANONICAL`

---

## Adopted production binaries

Identity is SHA-256. Filenames are stable production names, not SHA embeddings.

SOURCE_SHA == REPO_PATH_SHA == OWNER_REQUIRED_SHA for every row. No Blender save. No re-export. No binary normalization. This promotion gate does not mutate any production binary.

| logical role | repository path | SHA-256 | size | disposition / status | parent / source lineage | review report reference | canonical? |
|---|---|---|---:|---|---|---|---|
| Previous canonical parent `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.blend` | `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a` | 614872 | PREVIOUS CANONICAL BASELINE / PARENT after merge of this promotion PR; still effective active canonical on `main` until merge | Compound Applicator PROXYCLEAN01 after PR #195 | prior canonical promotion record; not re-opened by this gate | **NO** (previous; still effective on `main` until merge) |
| Previous canonical parent scene-scoped GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.glb` | `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65` | 2332388 | PREVIOUS CANONICAL BASELINE / PARENT after merge; still effective until merge | paired export of `be48b091…` | same | **NO** (previous; still effective on `main` until merge) |
| Reviewed standalone Repair Face `.blend` | `art/assets/TLAW_REPAIR_FACE_01.blend` | `65631fa9c309902865dbcf9c71ff259fd89cfb0a652b3ede3d3c744e4e24235f` | 109331 | REVIEWED STANDALONE; `INTEGRATION_ELIGIBLE=YES`; not canonical | standalone production packet `TLAW_REPAIR_FACE_01_PRODUCTION_01` | `TLAW_REPAIR_FACE_01_REVIEW_01.report.md` | NO |
| Reviewed standalone Repair Face GLB | `art/assets/TLAW_REPAIR_FACE_01.glb` | `ebdce71930b1f61b2c22680640f275b94514e8285c3836cb225b9e794c24d0aa` | 89824 | REVIEWED STANDALONE; `INTEGRATION_ELIGIBLE=YES`; not canonical | paired standalone GLB for `65631fa9…` | same | NO |
| New canonical production baseline `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.blend` | `b940f515e42795aaab785662067da1b39ef26226ddbc76410398faf4be683a5b` | 629460 | CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN after merge of this promotion PR; `APPROVE WITH NON-BLOCKING NOTES`; `PROMOTION_ELIGIBLE=YES`; **NOT CANONICAL until merge** | standalone `65631fa9…` integrated into canonical parent `be48b091…` | `TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01.report.md` + `TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01_REVIEW_01.report.md` + `TLAW_REPAIR_FACE_01_CANONICAL_PROMOTION_01.report.md` | **YES** (active after merge only) |
| New canonical production baseline GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.glb` | `68912b8b8862daea523ba5d8532a63451de4cd1a6b0a3f0416160020d5090ca7` | 2415104 | CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN after merge of this promotion PR; paired scene-scoped GLB | paired export of `b940f515…` | same | **YES** (active after merge only) |

---

## Review / provenance files versioned with this gate

| file | SHA-256 | size | notes |
|---|---|---:|---|
| `art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01.report.md` | `2efa931652bf2fdba026345fc57c8629d50d7ccc897ad9170f99b31508994bab` | 8747 | Worker integration report. Exact persistent sawmill / handoff copy. Immutable. |
| `art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01_REVIEW_01.report.md` | `98795bc19526c74cc801c04423f8005e05ef630c2aac9bbab54f022cbe699d94` | 21378 | Independent integration review. Verdict `APPROVE WITH NON-BLOCKING NOTES`. `PROMOTION_ELIGIBLE=YES`. Immutable. |
| `art/environment/sawmill/TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01_REVIEW_01.validation.json` | `c465d8ed019090afcab5123b710c5de2d03f5ba0546bdc3f6a1221c779dbd12b` | 4072 | Machine-readable companion to the independent integration review. Exact bytes. Immutable. |
| `art/environment/sawmill/TLAW_REPAIR_FACE_01_REVIEW_01.report.md` | `c1b4056947d484263953a2992d637d8187b951668b91aa03e7562e48e851249d` | 18505 | Independent standalone review. Verdict `APPROVE WITH NON-BLOCKING NOTES`. `INTEGRATION_ELIGIBLE=YES`. Immutable. |

No JSON reserialization. No Markdown rewriting. No line-ending normalization of imported evidence.

---

## External custody reference (not Git-versioned)

Handoff ZIP lineage (external supporting custody, not a repository object in this gate):

- path `C:\Projects\TLAW_Handoff\CURRENT\TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01.zip`
- SHA-256 `2f9cf0ae1f0496294f554414e292796c40b0b48f3782c72e00fa7cad39306128`
- size `7639338`

---

## Optional provenance not Git-versioned

Standalone worker production report identity is established locally from the reviewed production packet:

- path `C:\Projects\TLAW_Handoff\CURRENT\TLAW_REPAIR_FACE_01_PRODUCTION_01\TLAW_REPAIR_FACE_01_PRODUCTION_01.report.md`
- SHA-256 `e62929f9fde2c43fc0a8183840c62b120d34784fc392a719870a553529c0abd5`
- size `11062`
- encoding: CRLF

Not staged. Existing repository rule `* text=auto eol=lf` would rewrite CRLF to LF and change the SHA-256. Exact-byte policy forbids that substitution. File was not reconstructed.

---

## LFS rules

Existing `.gitattributes` LFS rules preserved:

```
*.blend filter=lfs diff=lfs merge=lfs -text
*.glb filter=lfs diff=lfs merge=lfs -text
```

No duplicate global LFS rules added.

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

Staged `.blend` / `.glb` must appear as three-line LFS pointers, never raw binary blobs in Git history.

---

## Historical custody-gate non-effects (TLAW_REPAIR_FACE_01_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01)

The original custody gate did not promote. Those historical non-effects remain true of that gate.

This promotion overlay does **not** mutate production binaries, restore `RepairFaceProxy`, or merge the promotion PR. Canonical identity change is **conditional on a later separately owner-authorized merge**.
