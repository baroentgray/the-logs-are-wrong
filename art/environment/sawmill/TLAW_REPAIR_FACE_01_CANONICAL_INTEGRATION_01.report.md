# TLAW_REPAIR_FACE_01_CANONICAL_INTEGRATION_01

Canonical **integration candidate** only. Not promotion. Not active canonical. No Git mutation. No Unity. No gameplay.

**WORKER_RECOMMENDATION: PASS WITH WARN**

`INTEGRATED_CANDIDATE ≠ ACTIVE_CANONICAL`

No promotion authorization exists.

---

```
RESULT=PASS_WITH_WARN
BASE_MAIN_SHA=65fa89c5179798eedf248590b4f1ed87c9b90905
INPUT_CANONICAL_BLEND_SHA256=be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a
INPUT_CANONICAL_GLB_SHA256=eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65
INPUT_REPAIR_BLEND_SHA256=65631fa9c309902865dbcf9c71ff259fd89cfb0a652b3ede3d3c744e4e24235f
INPUT_REPAIR_GLB_SHA256=ebdce71930b1f61b2c22680640f275b94514e8285c3836cb225b9e794c24d0aa
REVIEW_SHA256=c1b4056947d484263953a2992d637d8187b951668b91aa03e7562e48e851249d
BLENDER_PREFLIGHT=PASS
REPAIR_FACE_PROXY=REMOVED
INTEGRATED_REPAIR_ROOT=TLAW_REPAIR_FACE_01
INTEGRATED_REPAIR_MESHES=6
INTEGRATED_REPAIR_TRIANGLES=1248
INTEGRATED_REPAIR_MATERIALS=3
OTHER_CANONICAL_CHANGED_OBJECTS=0
UNEXPLAINED_DELTA=0
UNEXPLAINED_STATIC_INTERSECTIONS=0
REPAIRSTANDING_SEPARATION=0.110
ACTUAL_PLAYER_ROUTE_INTRUSION=NO
ANCHORS=19/19
ANCHOR_TRANSFORM_DELTA=0
VALIDATOR_PASS=25
VALIDATOR_FAIL=0
NEGATIVE_CONTROLS=PASS
OUTPUT_BLEND_PATH=art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.blend
OUTPUT_BLEND_SHA256=b940f515e42795aaab785662067da1b39ef26226ddbc76410398faf4be683a5b
OUTPUT_GLB_PATH=art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.glb
OUTPUT_GLB_SHA256=68912b8b8862daea523ba5d8532a63451de4cd1a6b0a3f0416160020d5090ca7
SOURCE_IMMUTABILITY=PASS
PRODUCTION_SOURCE_MUTATION=NO
ACTIVE_CANONICAL_MUTATION=NO
GIT_MUTATION=NO
READY_FOR_INDEPENDENT_REVIEW=YES
```

---

## Lineage

active canonical `be48b091… / eba529c0…`  
+ reviewed standalone `65631fa9… / ebdce719…`  
+ review evidence `c1b40569…` (18505 bytes)  
→ integrated candidate

`TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.blend`  
sha256 `b940f515e42795aaab785662067da1b39ef26226ddbc76410398faf4be683a5b` size `629460`

`TLAW_SAWMILL_LAYOUT_V2_REPAIR_FACE_01_INT01.glb`  
sha256 `68912b8b8862daea523ba5d8532a63451de4cd1a6b0a3f0416160020d5090ca7` size `2415104`

Work was performed on a byte-copy of the canonical `.blend`. The active canonical path was never opened for save.

Live `origin/main`=`65fa89c5179798eedf248590b4f1ed87c9b90905`. Drift: NONE.

---

## 1. Inputs (hashed before mutation)

| artifact | sha256 | size | result |
|---|---|---:|---|
| canonical blend | `be48b091…` | 614872 | MATCH |
| canonical GLB | `eba529c0…` | 2332388 | MATCH |
| standalone blend | `65631fa9…` | 109331 | MATCH |
| standalone GLB | `ebdce719…` | 89824 | MATCH |
| production ZIP | `8f1ee89f…` | 5121779 | MATCH |
| review report (project + handoff) | `c1b40569…` | 18505 | MATCH |

Review disposition: `APPROVE WITH NON-BLOCKING NOTES`. `INTEGRATION_ELIGIBLE=YES`.

---

## 2. Blender preflight (standalone, read-only)

Blender 5.0.1. Standalone `.blend` opened; **not saved** (post-hash still `65631fa9…`).

- root `TLAW_REPAIR_FACE_01` EMPTY
- exactly 6 production meshes, 1248 triangles
- materials `TLAW_TableContactSteel`, `TLAW_MechanicalSteel`, `TLAW_SawPaint`
- no extra/hidden mesh, camera, light, REVIEW, animation/NLA, constraints, drivers
- unit scale
- hierarchy: root children MountFrame / MainPanel / FixedHinges; MainPanel children PanelHingeLeaves / PullMounts / FoldFlatPull

`BLENDER_PREFLIGHT=PASS`

This closes the independent review's `BLENDER_AUDIT=WARN_UNAVAILABLE`.

---

## 3. Source placeholder contract (measured on working copy before removal)

`RepairFaceProxy`

- parent `TLAW_SAWMILL_LAYOUT_V2`
- loc `(2.0499999523, -0.8199999928, 0.9900000095)`
- rotation identity, scale 1
- material `Route_Ochre`
- world bbox min `(1.6999999285, -0.8399999738, 0.875)` max `(2.3999998569, -0.8000000119, 1.1050000191)`
- no children, no constraints, no animation
- live modifier `Proxy bevel` (applied on export only)

Matches reviewed contract. `RepairPanelProxy` remains.

---

## 4. Integration method

`bpy.data.libraries.load` of the seven reviewed objects from the exact standalone `.blend` into the INT01 working copy.

Root parented under `TLAW_SAWMILL_LAYOUT_V2`. Local pose set to the measured proxy transform. Child local transforms **not** baked.

Appended `*.001` materials rebound to existing canonical datablocks. Principled Base Color / Metallic / Roughness compared equal. No visual tweak.

`RepairFaceProxy` removed after placement. No consequential dependency.

Closed static pose only. No opening animation.

---

## 5. Delta

| | canonical | INT01 |
|---|---:|---:|
| objects | 329 | 335 |
| mesh objects | 276 | 281 |
| official triangles | 31088 | 32324 |
| materials | 25 | 25 |

Object-set delta:

- remove `RepairFaceProxy`
- add `TLAW_REPAIR_FACE_01` + six meshes

`OTHER_CANONICAL_CHANGED_OBJECTS=0`  
`UNEXPLAINED_DELTA=0`

Standalone vs integrated: parent/local loc/vert/tri/material identity unchanged for all six meshes. World placement differs as required.

---

## 6. Envelope / static

Integrated repair world bbox:

- min `(1.70499992, -0.83999997, 0.87800002)`
- max `(2.39499998, -0.80000001, 1.10199999)`

Inside proxy envelope (X/Z inset, Y coincident).

Triangle static:

- intended: `RepairFace_MountFrame ↔ RepairPanelProxy` 36 hits, AABB overlap ~`3e-9` (flush/tangent mount)
- unexplained: **0**

`UNEXPLAINED_STATIC_INTERSECTIONS=0`

---

## 7. RepairStanding / player routes / anchors

Centerline reserved-zone gap: **`0.110000027 m`** (reviewed ~0.110). No standing-volume triangle hits.

Physical painted line `RepairStanding_Boundary.003` planar gap: **`0.087500014 m`** (reviewed ~0.0875). Distinct from the 0.110 m zone.

Official player routes still PASS (`inspection_to_documentation` 0.049 m vs Gate; `pallet_push_walk` 0.025 m vs LoadingEndStop). Tightest points unchanged class.

`ACTUAL_PLAYER_ROUTE_INTRUSION=NO`  
`DEDICATED_REPAIR_ROUTE=NOT_DEFINED`

Anchors **19/19**, `ANCHOR_TRANSFORM_DELTA=0`. `AP_Repair` still `(2.05, -1.40, 0)`.

---

## 8. Official validator

`tlaw_validator.py` + live ACTIVE config against INT01 blend. **Not** retargeted.

Verdict **WARN** — 25 PASS, 0 FAIL, 1 UNTESTED (Unity), 4 SKIP.

Carried:

- W-01 `LocalSplashGuard ↔ log ≈ 0.025000052 m` (not consumed)
- W-02 `ProcedureGuide ↔ log ≈ 0.005 m` official OBB vs ProcedureCradleSide (not consumed)
- Unity `export.engine_import` UNTESTED

Circulation free_cells still `2674`. Procedure REAL remains 0 vs required 0.

---

## 9. Negative controls (in-memory copies; INT01 not saved)

Independent triangle copies:

| ID | setup | result |
|---|---|---|
| A | repair tris Y `-= 0.15` into standing zone | **FAIL** 36 hits |
| B | repair tris Y `+= 0.02` into `RepairPanelProxy` front skin | **FAIL** 138 hits |

`NEGATIVE_CONTROLS=PASS`

Post-control INT01 blend SHA unchanged (`b940f515…`).

---

## 10. Export

Scene-scoped GLB, selection excluding cameras/lights/REVIEW. `export_apply=True`, `export_yup=True`.

| | blend official | GLB export |
|---|---:|---:|
| nodes/objects | 335 (incl. 1 camera + 5 lights) | **329** |
| meshes | 281 | 281 |
| triangles | 32324 (unevaluated) | 37252 (modifiers applied) |
| materials | 25 | 25 |
| cameras/lights | in .blend | 0 / 0 |
| REVIEW | 0 | 0 |
| RepairFaceProxy | absent | absent |
| RepairPanelProxy | present | present |
| anchors | 19 | 19 |

Triangle count difference is applied bevels on remaining proxy/shop meshes, same class as canonical GLB 36048 vs blend 31088. Repair production tris remain 1248.

---

## 11. Visual evidence (integrated candidate, not standalone)

`art/environment/sawmill/_repair_int01/evidence/`

- `01_player_facing.png` — service face on saw cabinet, saw behind, no ochre plate
- `02_side_envelope.png`
- `03_repairstanding.png`
- `04_saw_context.png`
- `05_hinge_pull_close.png` — hinges + fold-flat pull
- `06_no_ochre_proxy.png` — steel frame / saw-paint panel / contact-steel hardware

---

## 12. Immutability

End-of-gate rehash:

- active canonical blend `be48b091…` MATCH
- active canonical GLB `eba529c0…` MATCH
- standalone blend `65631fa9…` MATCH
- standalone GLB `ebdce719…` MATCH
- review `c1b40569…` MATCH
- `git diff --quiet` tracked files clean

`SOURCE_IMMUTABILITY=PASS`  
`PRODUCTION_SOURCE_MUTATION=NO`  
`ACTIVE_CANONICAL_MUTATION=NO`  
`GIT_MUTATION=NO`

---

## 13. Handoff

New package (previous Repair Face production/review custody **not** destroyed):

See `HANDOFF_ZIP_PATH` in the final fields after packaging.

---

This candidate is eligible for a later independent Review of the **integrated** scene. It is **not** the active canonical. Do not promote from this worker.

STOP.
