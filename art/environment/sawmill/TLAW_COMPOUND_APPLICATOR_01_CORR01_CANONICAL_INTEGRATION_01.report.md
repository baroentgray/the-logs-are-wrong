# TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01

Bounded transplant of independently reviewed applicator CORR01 into canonical **3ccf**. Integration only. No applicator remodel. No canonical mesh edit. No promotion. No Astra. No Unity bind. No git add/commit/PR.

**WORKER_VERDICT: PASS WITH WARN**

Integrated **candidate only**. Not canonical. Not promoted.

---

## Lineage

SOURCE_ASSEMBLY=`9c39aaa75de76c13827c736f2653ce30405fb04e1680139bc64133870796a059`  
SOURCE_GLB=`a6b3c135d1c079528663c6ab894aede4489d8b8d6cc4818b7e0d8e9b1d6ea4dc`  
Independent review: `APPROVE WITH NON-BLOCKING NOTES` / `INTEGRATION_ELIGIBLE=YES`

CANONICAL_TARGET=`3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5`  
CANONICAL_GLB=`48b1884ad81c7f6aebc7cb75c6e46da5e279bb7c7810052d91ab6931e925d665`

INTEGRATED_CANDIDATE=`0d9a66777a28b949e910885082ae3c4a4c54382a4957c331508e9c10fb226c1a`  
INTEGRATED_GLB=`04966c05f08e94a88409b526d4c3b529e450a70f2171b270057f79df1d3e1d0f`

```
9c39aaa7… (reviewed CORR01)
+ 3ccf52e2… (canonical 3ccf)
= 0d9a6677… (INT01 candidate)
```

---

## Pre-integration identity check

Hashed **before** any copy/transplant. All four MATCH. Not HOLD.

| role | path | SHA-256 | result |
|---|---|---|---|
| source blend | `art/assets/TLAW_COMPOUND_APPLICATOR_01_CORR01.blend` | `9c39aaa7…059` | MATCH |
| source GLB | `art/assets/TLAW_COMPOUND_APPLICATOR_01_CORR01.glb` | `a6b3c135…4dc` | MATCH |
| canonical blend | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_SAW01_OT01_PROCSPUR_CORR01.blend` | `3ccf52e2…2a5` | MATCH |
| canonical GLB | `…PROCSPUR_CORR01.glb` | `48b1884a…665` | MATCH |

Canonical SHA after transplant/export still `3ccf52e2…`. Never overwritten.

---

## Paths

| | |
|---|---|
| source blend | `C:\Projects\TheLogsAreWrong\art\assets\TLAW_COMPOUND_APPLICATOR_01_CORR01.blend` |
| source GLB | `C:\Projects\TheLogsAreWrong\art\assets\TLAW_COMPOUND_APPLICATOR_01_CORR01.glb` |
| canonical blend (immutable) | `C:\Projects\TheLogsAreWrong\art\environment\sawmill\TLAW_SAWMILL_LAYOUT_V2_SAW01_OT01_PROCSPUR_CORR01.blend` |
| integrated blend | `C:\Projects\TheLogsAreWrong\art\environment\sawmill\TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01.blend` |
| integrated GLB | `C:\Projects\TheLogsAreWrong\art\environment\sawmill\TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01.glb` |

Blender `5.0.1` (`a3db93c5b259`). `save_version=0` on INT01 only.

---

## Transplant method

1. Byte-copy 3ccf → INT01 (copy SHA = `3ccf52e2…`).
2. `bpy.ops.wm.append` **Collection** `TLAW_COMPOUND_APPLICATOR_01` from reviewed source. Not `Review_Setup_NotForExport`. Not whole-file append.
3. Parent root to `TLAW_SAWMILL_LAYOUT_V2` (identity at origin, same parent pattern as `CompoundProxy`).
4. Set root `location (0.60, 3.25, 0)`, rotation 0, scale 1. World translation measured `(0.60, 3.25, 0)`.
5. Nest collection under `Stations` (same collection family as `CompoundProxy`).
6. Remap appended `TLAW_MechanicalMetal.001` / `TLAW_PaintedSteel.001` onto pre-existing canonical materials of those names (saw/OT already use them). New unique `TLAW_RubberSeal` and `TLAW_TreatmentOchre` kept.
7. No Review scene/objects/cameras/lights crossed. No canonical object removed or remeshed.

`CompoundProxy` **left in place, untrimmed** (hard boundary: no canonical-object edits). Overlap classified INTENDED replacement occupancy, same as PREFLIGHT_03.

---

## Object manifest

SOURCE ASSEMBLY → CANONICAL TARGET → INTEGRATED RESULT

ADD=`17` (expected production hierarchy)  
REMOVE=`0`  
CANONICAL_MESH_UPDATE=`0`  
UNEXPLAINED_MANIFEST_DELTA=`0`

| object | type | parent after | world translation | materials | tris | notes |
|---|---|---|---|---|---|---|
| `TLAW_COMPOUND_APPLICATOR_01` | EMPTY | `TLAW_SAWMILL_LAYOUT_V2` | `(0.60, 3.25, 0)` | — | — | **explained**: standalone parent was none; pose is the authorized placement |
| `Applicator_Foot` | MESH | root | `(0.60, 3.25, 0)` | MechanicalMetal, PaintedSteel | 872 | local loc unchanged |
| `Applicator_Pedestal` | MESH | root | | PaintedSteel | | |
| `Applicator_ProtectedFluidBody` | MESH | root | | MechanicalMetal, PaintedSteel | | |
| `Applicator_Manifold` | MESH | root | | MechanicalMetal | | |
| `Applicator_ServiceCoupling` | MESH | root | | MechanicalMetal | | |
| `Applicator_HeadSupport` | MESH | root | | PaintedSteel | | |
| `Applicator_ProtectedDelivery` | MESH | root | | MechanicalMetal | 216 | chute lip preserved |
| `Applicator_HeadBody` | MESH | root | | PaintedSteel | | |
| `Applicator_HeadSeal` | MESH | root | `(−0.180, 3.00, 1.584)` | **TLAW_RubberSeal** | 288 | dry skirt |
| `Applicator_FanNozzle` | MESH | root | `(−0.180, 3.00, 1.560)` | MechanicalMetal | 576 | gravity mouth + vanes |
| `Applicator_LocalSplashGuard` | MESH | root | | MechanicalMetal | 216 | Zmin still 1.445 |
| `Applicator_ServiceCover` | MESH | root | | MechanicalMetal | | |
| `Applicator_ApplicationIndex` | MESH | root | | TreatmentOchre, MechanicalMetal | | |
| `LogReference` | EMPTY | root | `(−1.15, 3.00, 1.16)` | — | — | alignment error `4e-8` m |
| `ApplicationPoint` | EMPTY | root | | — | — | |
| `OperatorSide` | EMPTY | root | `(0.60, 3.80, 1.10)` | — | — | world **+Y** |

All 13 production meshes present. Collection `TLAW_COMPOUND_APPLICATOR_01`.

**Flagged identity differences vs standalone (explained, not unexplained):**

- Root parent `None` → `TLAW_SAWMILL_LAYOUT_V2` (layout parenting).
- Root loc `(0,0,0)` → `(0.60, 3.25, 0)` (authorized pose).
- Some mesh datablock names auto-suffixed on append (`Cube` → `Cube.099` etc.). Geometry `mesh_sha` unchanged. Fan/Seal datablock names `Applicator_*_CORR01` preserved.
- `TLAW_MechanicalMetal` / `TLAW_PaintedSteel` remapped from `.001` copies onto canonical slots. RubberSeal / TreatmentOchre new.

Applicator-subassembly: 13 meshes, **4576 tris** (matches reviewed standalone). Local transforms of children unchanged. `sub_deltas=[]`.

---

## Canonical delta audit

Compared INT01 dump vs 3ccf `cand.json` for every non-applicator object: parent/loc/rot/scale/raw_verts/eval_tris.

`CANONICAL_OBJECT_TRANSFORM_DELTA=0`  
`CANONICAL_MESH_DELTA=0`  
`AP_*` **19/19**, transform delta **0**. `AP_Procedure` unmoved.

ProcedureGuide / .001 / cradle / sides / rails / floor / CompoundProxy / routes: **untouched**.

---

## Procedure sweep (integrated scene, not overlay-only)

Unrotated log `2.60×0.52×0.52`, mill `(−1.15,1.00,1.16)` → cradle `(−1.15,3.00,1.16)`, 41 samples @ 0.05 m, triangle–triangle.

| | result |
|---|---|
| canon-only (3ccf dump) REAL | **0** |
| integrated without applicator meshes REAL | **0** |
| **integrated full scene REAL** | **0** |
| ingress | **PASS** |
| treatment | **PASS** (0 log tri hits on all 13 applicator meshes) |
| egress/return | **PASS** (`procedure_spur_return` official also PASS) |
| official `swept.procedure_spur` | **PASS**, min clearance **0.005 m** vs `ProcedureCradleSide` at `(−1.15, 2.25, 1.16)` |
| overlay min_pos | **0.005 m** vs `ProcedureGuide` at `(−1.15, 1.80, 1.16)` — **not spent** |

---

## 25 mm splash clearance (remeasured after transplant)

`Applicator_LocalSplashGuard` ↔ log AABB/tri gap = **`0.02500005 m`**

Materially equivalent to reviewed standalone `0.025 m`. Positive. No contact. Not converted into a new TLAW threshold.

FanNozzle treatment clearance **`0.140 m`** (unchanged).

---

## Static / scene-level regression

Official validator on INT01 vs `configs/tlaw_sawmill_layout_v2.json`:

**Verdict WARN** — 23 pass, 0 fail, 1 untested (`export.engine_import`), 4 skipped.

| check | result |
|---|---|
| hierarchy 31/31 | PASS |
| geometry non-manifold / loose / duplicate faces | PASS |
| transforms | PASS |
| `swept.procedure_spur` / return | PASS 0.005 m |
| `swept.disposal_log` | PASS 0.055 m (need 0.050) |
| `swept.overhead_feed` | PASS 0.095 m |
| `swept.pallet_dispatch` | PASS 0.110 m |
| terminal dispatch/loading | expected contact |
| player inspection_to_documentation | PASS 0.049 m |
| player pallet_push_walk | PASS 0.025 m |
| circulation 8/8 one region | PASS (free 2674 / reachable 2517) |
| opening.disposal_pit | PASS |
| coverage.disposal_rail | PASS 100% |

3ccf official circulation was free 2675 / reachable 2518. INT01 is **−1 cell** at 0.25 m grid — machine footprint, not a new island. Local 0.10 m window still **+9** cells (same PREFLIGHT_03 footprint).

Applicator × canonical near-set triangle contacts:

| pair | tris | class |
|---|---|---|
| Foot / FluidBody / Manifold / ServiceCoupling × `CompoundProxy` | 132+214+34+36 | INTENDED replacement occupancy (proxy kept) |
| Foot × `MainShopFloor` | 16 | INTENDED support |

No new contacts vs guides/rails/cradle. West red-tape volume `X −2.55…−1.20`: **0** applicator hits. `AP_Procedure` player box: **0** hits.

---

## Negative controls (in-memory on dumps; blend not mutated)

| ID | setup | REAL | result |
|---|---|---|---|
| A | original 712c splash restored at accepted pose | **11** | FAIL as required |
| B | forbidden Z-band box `1.31…1.42` in old splash XY | **15** | FAIL |
| C | FanNozzle dropped `−0.20 m` | **4** | FAIL |

INT01 blend SHA after dump/validator/GLB export still `0d9a6677…` (controls never saved).

---

## Source-asset integrity in situ

Reviewed characteristics present: open gravity mouth, 3 metering vanes, dry rubber skirt, chute lip. No hopper, no one-dose receptacle, no fill-state affordance.

Standalone export reference 17 / 13 / 4576 / 4 — applicator **subassembly** in INT01: 17 objects, 13 meshes, 4576 tris, materials MechanicalMetal + PaintedSteel (canonical slots) + RubberSeal + TreatmentOchre.

---

## GLB export / content

Scene-scoped: current `Scene`, selection excluding `Review` collection and cameras/lights. `export_yup=True`, `export_apply=True`.

| | 3ccf GLB | **INT01 GLB** |
|---|---|---|
| SHA | `48b1884a…` | `04966c05f08e94a88409b526d4c3b529e450a70f2171b270057f79df1d3e1d0f` |
| size | 2 138 052 B | 2 336 208 B |
| objects (import) | 308 (307+wrapper) | **324** (= 307 + 17) |
| meshes | 264 | **277** (+13) |
| triangles (import eval) | 31 516 | **36 092** (= 31516 + 4576) |
| materials | 23 | **25** (+RubberSeal, +TreatmentOchre) |
| cameras / lights / REVIEW | 0 | **0** |
| null materials | 0 | **0** |

Required names present: applicator root/13 meshes/3 empties, `ProcedureGuide`, `AP_Procedure`, `CompoundProxy`.

---

## Warnings

1. **`PROCEDURE_SPUR_CLEARANCE_005`** — 0.005 m vs ProcedureGuide / ProcedureCradleSide. Inherited. Not spent.
2. **`SAW_TABLE_GUIDE_CLEARANCE_005` / disposal 0.055 vs 0.050** — inherited, unrelated.
3. **Splash ↔ log 0.025 m** — durable reviewed WARN; remeasured identical after transplant.
4. **`CompoundProxy` retained** — canonical-object edit forbidden; intended occupancy overlap with applicator.
5. **Foot × MainShopFloor** coincident support.
6. **Official circulation −1 cell** (2675→2674 / 2518→2517) at 0.25 m — footprint, 8/8 still one region.
7. **`export.engine_import` UNTESTED** / `UNITY_RUNTIME` unbound.
8. **`COMPOUND_APPLICATION_COVERAGE_UNDEFINED`** — inherited local patch.
9. Material name-collision remap of MechanicalMetal/PaintedSteel onto canonical slots.

UNEXPLAINED_MANIFEST_DELTA=`0`  
UNEXPLAINED_APPLICATOR_SUBASSEMBLY_DELTA=`0`

---

## Final recommendation

**PASS WITH WARN**

Fit at accepted pose without canonical geometry change. Integrated Procedure REAL=0. Official process/disposal/OT/pallet/anchors/circulation pass. Negatives fail. Candidate is **not** canonical.

---

## Upload bundle

UPLOAD_BUNDLE_PATH=`C:\Projects\TLAW_Artifact_Recovery\TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01\UPLOAD_READY\TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01_REVIEW_BUNDLE.zip`  
UPLOAD_BUNDLE_SHA256=`d06a1ec1a0643576e3597db514c66885b09de8bf81b08da2743d9ec114e1be57`  
UPLOAD_BUNDLE_SIZE=`2221547`  
EXTRACT_VERIFY=`PASS` (six production artifacts rehashed)  
READY_FOR_OWNER_UPLOAD=`YES`

Owner uploads this ZIP unchanged to the independent Review chat. This candidate is **not** promoted.

STOP.
