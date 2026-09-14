# TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01

Controlled resume of the already-authorized single-object cleanup. Not a new gate. Not canonical. Not promoted.

**WORKER_VERDICT: PASS WITH WARN**

Cleanup **candidate only**. Lineage from independently reviewed INT01 `0d9a6677…`.

---

## Resume context

Previous attempt ended `RESULT=HOLD` with `PRODUCTION_MUTATION=NO` / `OUTPUT_BLEND=NOT_CREATED` / `COMPOUND_PROXY_DISPOSITION=NOT_EXECUTED` due to exact-input/runtime unavailability only.

This resume used the local production workspace `C:\Projects\TheLogsAreWrong` with Blender 5.0.1 (`a3db93c5b259`) and exact on-disk INT01 bytes.

---

## Input exact identity

Hashed **before** Blender open, and rehashed **after** cleanup. Inputs unchanged.

| role | path | size | SHA-256 | result |
|---|---|---|---|---|
| INPUT_BLEND | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01.blend` | 615378 | `0d9a66777a28b949e910885082ae3c4a4c54382a4957c331508e9c10fb226c1a` | MATCH before+after |
| INPUT_GLB | `…CORR01_INT01.glb` | 2336208 | `04966c05f08e94a88409b526d4c3b529e450a70f2171b270057f79df1d3e1d0f` | MATCH before+after |

Recovery copies of the same INT01 pair rehashed identical.

Standalone reviewed applicator (not mutated, identity check only):

| role | SHA-256 | result |
|---|---|---|
| `.blend` | `9c39aaa75de76c13827c736f2653ce30405fb04e1680139bc64133870796a059` | MATCH |
| GLB | `a6b3c135d1c079528663c6ab894aede4489d8b8d6cc4818b7e0d8e9b1d6ea4dc` | MATCH |

`save_version=0`. INT01 never overwritten. No `.blend1` of INT01 written.

---

## Dependency audit (before any mutation)

Opened exact INT01. Did **not** save. `CompoundProxy` present.

| check | evidence |
|---|---|
| type | MESH |
| parent | `TLAW_SAWMILL_LAYOUT_V2` |
| children | **none** |
| collections | `Stations` only |
| constraints on proxy | none |
| other objects constraining to it | none |
| modifiers on proxy | `Proxy bevel` (BEVEL), local to this object |
| other objects using it as modifier target | none |
| drivers (on proxy / file-wide targeting it) | none |
| animation / NLA | none |
| rigid body / collision | none |
| custom properties | none |
| mesh datablock | `Cube.198`, users=1, unique to this object, raw 8 verts / 6 polys / **12 tris**, eval **44 tris** |
| materials | `Route_Ochre` only, **shared** (32 object users) — not unique, not removed |
| official `hierarchy.required_objects` | CompoundProxy **not listed** (31/31 other names present) |
| Unity / Domain C# name bind | **no** `CompoundProxy` references |
| validator config | no required bind on this name |
| exporter | scene-scoped selection by collection/type; no name-special case that would break |

`SAFE_DIRECT_REMOVAL=YES`  
`consequential_dependencies=[]`

Direct removal demonstrated safe. No HOLD. No compensation edits.

---

## Exact proxy disposition

`COMPOUND_PROXY_DISPOSITION=REMOVED`

Removed object `CompoundProxy` with `bpy.data.objects.remove(..., do_unlink=True)`.

Removed unique mesh `Cube.198` after users reached 0. Required to keep `common.unused_data` PASS (INT01 had 0 unused meshes). Not a material/geometry cleanup of remaining content.

`Route_Ochre` retained (shared). No `orphans_purge`. No other object, material, collection, or datablock removed.

Postcheck:

* object absent from production scene
* absent from scene-scoped GLB (`found.CompoundProxy=false`)
* no exported production `CompoundProxy`
* no obsolete visible ochre proxy geometry under that name
* applicator × leftover proxy-named objects (`RepairFaceProxy`, `RepairPanelProxy` — existing repair-station proxies, unrelated) = **0** hits
* no duplicate pedestal geometry introduced
* no proxy/applicator intersections remaining
* no proxy-based validator masking (official procedure/circulation unchanged except explained occupancy)

Metadata-only retention **not** used. Not required by project convention.

---

## Before/after object delta

Compared INT01 dump vs PROXYCLEAN01 dump, every object except the authorized removal.

| | |
|---|---|
| only in INT01 | `CompoundProxy` |
| only in cleanup | none |
| applicator field/mesh/transform deltas | **0** |
| other canonical field/mesh/transform deltas | **0** |
| `UNEXPLAINED_DELTA` | **0** |

Official blend counts:

| | INT01 | PROXYCLEAN01 | delta |
|---|---|---|---|
| objects | 330 | 329 | −1 |
| meshes w/ geometry | 277 | 276 | −1 |
| triangles (raw validator) | 31100 | 31088 | −12 (raw `Cube.198`) |
| vertices | 16584 | 16576 | −8 |
| materials | 25 | 25 | 0 |

GLB import counts:

| | INT01 | PROXYCLEAN01 | delta |
|---|---|---|---|
| nodes | 324 | 323 | −1 |
| meshes | 277 | 276 | −1 |
| tris | 36092 | 36048 | −44 (evaluated bevel) |
| materials | 25 | 25 | 0 |
| REVIEW / cameras / lights | 0 | 0 | 0 |

All count deltas explained solely by `CompoundProxy` removal.

---

## Applicator integrity

`APPLICATOR_CHANGED_OBJECTS=0`

| | required | measured |
|---|---|---|
| production meshes | 13 | 13 |
| tris | 4576 | 4576 (matches reviewed standalone) |
| root loc | `(0.60, 3.25, 0)` | `(0.60, 3.25, 0)` |
| rotation | 0 | 0 |
| scale | 1 | 1 |
| parent | `TLAW_SAWMILL_LAYOUT_V2` | unchanged |
| local child transforms | unchanged | `sub_deltas=[]` |
| materials | unchanged | MechanicalMetal roughness **0.47** (`0.4699999988079071`) before and after |

`TLAW_MechanicalMetal` roughness not altered. Standalone 0.48 remains independently classified explained/non-blocking. This gate did not normalize materials.

---

## Other-canonical integrity

`OTHER_CANONICAL_CHANGED_OBJECTS=0`

Explicitly unchanged vs INT01 (parent/loc/rot/scale/raw_verts/eval_tris):

* `ProcedureGuide` / `ProcedureGuide.001`
* `ProcedureCradle` / both cradle sides / `ProcedureTransferBed`
* floor / saw / `OutputTable_Frame` / pallet / disposal / overhead feed
* all `AP_*` including `AP_Procedure`

Vs canonical 3ccf dump, excluding applicator add and the now-removed proxy: **0** object deltas.

---

## Anchor audit

`19/19`  
Transform delta **0**  
`AP_Procedure` loc `(-0.30, 4.00, 0)` unmoved.

---

## Procedure sweep (cleanup scene, not overlay-only)

Unrotated log `2.60×0.52×0.52`, mill `(−1.15,1.00,1.16)` → cradle `(−1.15,3.00,1.16)`, 41 samples @ 0.05 m, triangle–triangle.

| | result |
|---|---|
| canon-only (3ccf dump) REAL | **0** |
| cleanup without applicator meshes REAL | **0** |
| **cleanup full scene REAL** | **0** |
| ingress | **PASS** |
| treatment | **PASS** (0 log tri hits on all 13 applicator meshes) |
| egress/return | **PASS** |
| official `swept.procedure_spur` | **PASS**, min clearance **0.005 m** vs `ProcedureCradleSide` at `(−1.15, 2.25, 1.16)` |
| overlay min_pos | **0.005 m** vs `ProcedureGuide` at `(−1.15, 1.80, 1.16)` — **not spent** |

Real applicator `REAL=0`. Canonical Procedure geometry `REAL=0`.

---

## Applicator clearance

`Applicator_LocalSplashGuard` ↔ log gap = **`0.025000052452087473 m`**

Materially identical to INT01 `0.02500005 m` / standalone `0.025 m`. Durable WARN. Unchanged. Not converted into a new threshold.

FanNozzle treatment clearance **`0.140 m`**.

---

## Negative controls (in-memory on dumps; blend not mutated)

| ID | setup | REAL | result |
|---|---|---|---|
| A | original 712c splash restored at accepted pose | **11** | FAIL as required |
| B | forbidden Z-band box `1.31…1.42` in old splash XY | **15** | FAIL |
| C | FanNozzle dropped `−0.20 m` | **4** | FAIL |

Cleanup blend SHA after dump/validator/GLB export still `be48b091…`. INT01 still `0d9a6677…`. Controls never saved.

---

## CompoundProxy postcheck

| | |
|---|---|
| production object | absent |
| GLB name | absent |
| ochre `Route_Ochre` unique proxy mesh | gone (`Cube.198` removed) |
| applicator × CompoundProxy contacts | gone (INT01 had Foot/FluidBody/Manifold/ServiceCoupling overlaps; cleanup static set is **Foot × MainShopFloor 16 only**) |
| duplicate pedestal | none |
| validator masking | none; official procedure/circulation identical to INT01 |

---

## Full regression (official suite)

Official validator on PROXYCLEAN01 vs `configs/tlaw_sawmill_layout_v2.json`:

**Verdict WARN** — **23 pass, 0 fail**, 1 untested (`export.engine_import`), 4 skipped.

Same 23/0 as INT01. No unrelated regression.

| check | result |
|---|---|
| hierarchy 31/31 | PASS |
| geometry non-manifold / loose / duplicate faces | PASS |
| transforms | PASS |
| unused data | PASS |
| `swept.procedure_spur` / return | PASS 0.005 m |
| `swept.disposal_log` | PASS 0.055 m (need 0.050) |
| `swept.overhead_feed` | PASS 0.095 m |
| `swept.pallet_dispatch` | PASS 0.110 m |
| terminal dispatch/loading | expected contact |
| player inspection_to_documentation | PASS 0.049 m |
| player pallet_push_walk | PASS 0.025 m |
| circulation 8/8 one region | PASS (free 2674 / reachable 2517) — **identical to INT01**; proxy already occupied applicator XY at 0.25 m |
| opening.disposal_pit | PASS |
| coverage.disposal_rail | PASS 100% |

West red-tape volume `X −2.55…−1.20`: **0** applicator hits. `AP_Procedure` player box: **0** hits.

---

## Output identities

| | path | size | SHA-256 |
|---|---|---|---|
| OUTPUT_BLEND | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_COMPOUND_APPLICATOR_CORR01_INT01_PROXYCLEAN01.blend` | 614872 | `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a` |
| OUTPUT_GLB | `…PROXYCLEAN01.glb` | 2332388 | `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65` |

Compressed `.blend` (`compress=True`), same encoding family as INT01. Size 615378 → 614872 explained by proxy mesh removal.

---

## Prior independent review evidence

Expected: `TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01_REVIEW_01.report.md`  
SHA-256 `8d358fed4b41d4ec3b5a001da1c3695ecba416b5eec06076b13508b5b5756beb` size 20716.

**PRIOR_REVIEW_EVIDENCE_MISSING_LOCALLY=YES**

Not in `TLAW_Handoff`, `TLAW_Artifact_Recovery`, production sawmill tree, or the INT01 review ZIP. Not recreated. Does not block this already-authorized cleanup.

Worker INT01 report remains in evidence unchanged.

---

## Warnings

1. **`PROCEDURE_SPUR_CLEARANCE_005`** — 0.005 m vs ProcedureGuide / ProcedureCradleSide. Inherited. Not spent.
2. **`SAW_TABLE_GUIDE_CLEARANCE_005` / disposal 0.055 vs 0.050** — inherited, unrelated.
3. **Splash ↔ log 0.025000052 m** — durable reviewed WARN; remeasured identical after cleanup.
4. **Foot × MainShopFloor** coincident support (16 tris). Inherited intended stand.
5. **Official circulation** remains 2674 / 2517 (INT01 footprint, not a new island).
6. **`export.engine_import` UNTESTED** / Unity unbound. This gate forbids Unity.
7. **`COMPOUND_APPLICATION_COVERAGE_UNDEFINED`** — inherited local patch.
8. Existing repair-station objects `RepairFaceProxy` / `RepairPanelProxy` remain; not `CompoundProxy`; no applicator hits.

UNEXPLAINED_DELTA=`0`  
APPLICATOR_CHANGED_OBJECTS=`0`  
OTHER_CANONICAL_CHANGED_OBJECTS=`0`

---

## Final recommendation

**PASS WITH WARN**

Authorized `CompoundProxy` removal only. Applicator and all other canonical content byte-compared unchanged at object/mesh/transform level. Procedure REAL=0. Official 23/0 preserved. Negatives fail. Candidate is **not** canonical.

`NEXT_GATE=TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01`  
`UPLOAD_TO=specialized independent Review chat`

STOP.
