# TLAW_REPAIR_FACE_01_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01

Git/LFS custody manifest for the reviewed Repair Face production/integration lineage.

**This file records custody only. It does not promote any candidate.**

**Git/LFS presence does not grant ACCEPTED or CANONICAL status.**

Disposition remains controlled by identified owner / Control Center decisions.

Gate: `TLAW_REPAIR_FACE_01_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01`
Live base: `origin/main` `65fa89c5179798eedf248590b4f1ed87c9b90905`
Branch: `task/TLAW-repair-face-durable-production-artifacts-lfs-01`
Canonical promotion performed by this gate: **NO**
Active canonical identity changed by this gate: **NO**

This is a Repair Face-specific durable record. It does **not** overwrite historical Compound Applicator custody semantics in `TLAW_DURABLE_PRODUCTION_ARTIFACTS_GIT_LFS_01.manifest.md`.

---

## Lineage (required)

```text
active canonical parent:
be48b091… / eba529c0…
CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN

+

standalone Repair Face:
65631fa9… / ebdce719…
REVIEWED STANDALONE
INTEGRATION_ELIGIBLE=YES

->

integrated Repair Face:
b940f515… / 68912b8b…
REVIEWED INTEGRATED CANDIDATE
APPROVE WITH NON-BLOCKING NOTES
PROMOTION_ELIGIBLE=YES
NOT CANONICAL
```

Active canonical remains:

- `.blend` `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a`
- scene-scoped GLB `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65`

`b940f515…` / `68912b8b…` do **not** become canonical from this Git commit.

Repair Face remains:

`REVIEWED INTEGRATED CANDIDATE — PROMOTION_ELIGIBLE — NOT CANONICAL`

until a later explicit promotion disposition.

---

## Adopted production binaries

Identity is SHA-256. Filenames are stable production names, not SHA embeddings.

SOURCE_SHA == REPO_PATH_SHA == OWNER_REQUIRED_SHA for every row. No Blender save. No re-export. No binary normalization.

| logical role | repository path | SHA-256 | size | disposition / status | parent / source lineage | review report reference | canonical? |
|---|---|---|---:|---|---|---|---|
| Active canonical parent `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.blend` | `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a` | 614872 | CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN | Compound Applicator PROXYCLEAN01 after PR #195 | prior canonical promotion record; not re-opened by this gate | **YES** (active) |
| Active canonical parent scene-scoped GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.glb` | `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65` | 2332388 | CANONICAL PRODUCTION BASELINE — ACCEPT WITH WARN | paired export of `be48b091…` | same | **YES** (active) |
| Reviewed standalone Repair Face `.blend` | `art/assets/TLAW_REPAIR_FACE_01.blend` | `65631fa9c309902865dbcf9c71ff259fd89cfb0a652b3ede3d3c744e4e24235f` | 109331 | REVIEWED STANDALONE; `INTEGRATION_ELIGIBLE=YES`; not canonical | standalone production packet `TLAW_REPAIR_FACE_01_PRODUCTION_01` | `TLAW_REPAIR_FACE_01_REVIEW_01.report.md` | NO |
| Reviewed standalone Repair Face GLB | `art/assets/TLAW_REPAIR_FACE_01.glb` | `ebdce71930b1f61b2c22680640f275b94514e8285c3836cb225b9e794c24d0aa` | 89824 | REVIEWED STANDALONE; `INTEGRATION_ELIGIBLE=YES`; not canonical | paired standalone GLB for `65631fa9…` | same | NO |
| Reviewed integrated Repair Face candidate `.blend` | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.blend` | `b940f515e42795aaab785662067da1b39ef26226ddbc76410398faf4be683a5b` | 629460 | REVIEWED INTEGRATED CANDIDATE; `APPROVE WITH NON-BLOCKING NOTES`; `PROMOTION_ELIGIBLE=YES`; **NOT CANONICAL** | standalone `65631fa9…` integrated into active canonical `be48b091…` | `TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01.report.md` + `TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01_REVIEW_01.report.md` | NO |
| Reviewed integrated Repair Face candidate GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.glb` | `68912b8b8862daea523ba5d8532a63451de4cd1a6b0a3f0416160020d5090ca7` | 2415104 | same as integrated blend | paired scene-scoped GLB for `b940f515…` | same | NO |

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

## Explicit non-effects

- No canonical promotion.
- No active-canonical identity change.
- No Blender edit / re-export / geometry / material change.
- No Unity / Pipeline / Astra / gameplay change.
- No Repair Face correction.
- No PR merge.
- Local recovery copies and `TLAW_Handoff\CURRENT` were not deleted.
- After successful Git/LFS adoption, Git/LFS is the durable source for these versioned milestones. Handoff CURRENT remains only for active owner uploads.
- Git presence does **not** make the Repair Face candidate canonical.
