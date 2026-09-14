# TLAW_COMPOUND_APPLICATOR_01_CORR01

Bounded standalone-asset correction of `TLAW_COMPOUND_APPLICATOR_01` against accepted PREFLIGHT_03 envelope.

Canonical **3ccf** was not opened for edit, not saved, not re-exported. No integration. No Astra. No Unity bind. No git add / commit / PR. No following owner gate.

**WORKER_VERDICT: PASS WITH WARN**

Technically eligible for Control Center review as a standalone candidate. Not integrated. Not promoted.

Role remains `RESIN_SALT_APPLICATOR` / `SIDE_NODE_FIXED_LOCAL_ZONE` / `attempted_item=salt` only. Stateless physical/presentation adapter. No machine-owned gameplay state, fill-state, generic compound, continuous coating, or new items.

TEXT_TO_CAD_USED=`NO`  
UNITY_RUNTIME=`UNTESTED` (not this gate)

---

## Original source identities

| artifact | path | SHA-256 | result |
|---|---|---|---|
| canonical blend | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_SAW01_OT01_PROCSPUR_CORR01.blend` | `3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5` | MATCH, unchanged after gate |
| canonical GLB | `art/environment/sawmill/TLAW_SAWMILL_LAYOUT_V2_SAW01_OT01_PROCSPUR_CORR01.glb` | `48b1884ad81c7f6aebc7cb75c6e46da5e279bb7c7810052d91ab6931e925d665` | MATCH, unchanged after gate |
| standalone source (before) | `TLAW_Artifact_Recovery/TLAW_3D_CANONICAL_ARTIFACT_RECOVERY_01/applicator/TLAW_COMPOUND_APPLICATOR_01.blend` | `712c310fcc45656d4f33b59b7d580cf9d7c14469c3d92b4da0b8847ea94293c9` | MATCH, **never overwritten** |
| standalone GLB (before) | `art/assets/TLAW_COMPOUND_APPLICATOR_01.glb` | `0796a126c05933878067640a5befbb11898e4fa897fdd9d05290ea29442d47ad` | MATCH, **never overwritten** |

Work was done on a copy. 712c source SHA after all work is still `712c310f…`.

---

## Corrected source identities

| artifact | path | SHA-256 | size |
|---|---|---|---|
| corrected `.blend` | `art/assets/TLAW_COMPOUND_APPLICATOR_01_CORR01.blend` | `9c39aaa75de76c13827c736f2653ce30405fb04e1680139bc64133870796a059` | 178 676 B |
| corrected GLB | `art/assets/TLAW_COMPOUND_APPLICATOR_01_CORR01.glb` | `a6b3c135d1c079528663c6ab894aede4489d8b8d6cc4818b7e0d8e9b1d6ea4dc` | 198 520 B |

Blender `5.0.1` (`a3db93c5b259`).

---

## Placement transform used (canonical-fit validation)

Exact accepted PREFLIGHT_03 placement. Overlay only; applicator root in its own file remains origin.

| | |
|---|---|
| location | `(0.60, 3.25, 0)` |
| rotation | `0` |
| scale | `1` |
| LogReference world | `(−1.15, 3.00, 1.16)` |
| LOG_REFERENCE_ALIGNMENT_ERROR | `3.3e-8` m (numeric zero) |
| operator world | `+Y` `(0.60, 3.80, 1.10)` |

Canonical dump used for overlay: `_procspur_corr01/dumps/cand.json` / `cand.npz` (3ccf). Canonical `.blend` was not saved.

---

## Exact objects changed

UPDATE=`4`  
ADD=`0`  
REMOVE=`0`  
UNEXPLAINED_OBJECT_DELTA=`0`

Optional one-dose receptacle: **not added**. Existing `Applicator_ServiceCoupling` / `Applicator_ServiceCover` at local +Y already read as a one-action dock. A hopper would have implied persistent inventory.

| object | kind | change |
|---|---|---|
| `Applicator_LocalSplashGuard` | B functional | bottom lip translated `+0.135 m` in Z so hood sits above log top |
| `Applicator_FanNozzle` | A readability | liquid fan face replaced with open-bottom gravity distributor + 3 metering vanes |
| `Applicator_HeadSeal` | A readability | solid spray gasket replaced with thick open rectangular dry skirt (`TLAW_RubberSeal`) |
| `Applicator_ProtectedDelivery` | A mouth only | west-end floor sloped down `0.040 m` into a gravity chute; east pipe body unchanged |

Unauthorized production meshes compared vertex-for-vertex / transform-for-transform against the 712c dump: **13/13 identical** (9 unchanged meshes + 4 empties). `Foot`, `Pedestal`, `Manifold`, `ProtectedFluidBody`, `HeadSupport`, `HeadBody`, `ServiceCover`, `ServiceCoupling`, `ApplicationIndex`, root, `LogReference`, `ApplicationPoint`, `OperatorSide` untouched.

---

## Exact geometry / material changes

Materials: still exactly `TLAW_MechanicalMetal`, `TLAW_PaintedSteel`, `TLAW_RubberSeal`, `TLAW_TreatmentOchre`. No new material. No null slot.

### `Applicator_LocalSplashGuard` (Cube.019, users=1)

- 28 bottom-lip verts (world Z `1.310…1.318`, including bevel) translated by `dz = 0.135 m`.
- XY footprint unchanged: local/world X `−1.010…−0.550`, Y `−0.700…−0.420`.
- Z `1.310…1.658` → **`1.445…1.658`**.
- Clearance vs unrotated log top (`1.42 m`): **`0.025 m`**.
- Topology unchanged: 112 verts, 108 quads, 216 tris, non-manifold 0, boundary 0.
- Still a south-side hood around the treatment patch; no longer occupies the log Z-band during Y-sweep.

### `Applicator_FanNozzle`

- Object origin / parent / rotation / scale / material **unchanged** `(−0.78, −0.25, 1.56)`, `TLAW_MechanicalMetal`.
- Was: thin 27 mm spray fan, 340×150 mm, 280 verts / 540 tris.
- Now: thick-walled rectangular gravity mouth, open downward, roofed, 3 inset metering vanes.
  - local outer `X ±0.160, Y ±0.110, Z 0…0.042` (world Z `1.560…1.602`)
  - wall `0.012 m`; vane thickness `0.006 m`
- 296 verts / 288 faces / 576 tris. Non-manifold 0, boundary 0, degenerate 0 (closed wall-shell around an open cavity).
- Treatment Zmin remains **`1.560 m`** → log clearance **`0.140 m`** (same as 712c).

### `Applicator_HeadSeal`

- Origin / parent / `TLAW_RubberSeal` unchanged `(−0.78, −0.25, 1.584)`.
- Was: solid 18 mm spray gasket (56 verts / 108 tris).
- Now: thick open rectangular cuff (both ends open): outer `X ±0.185, Y ±0.135`, inner `X ±0.162, Y ±0.108`, Z local `−0.016…0.014` (world `1.568…1.598`).
- 144 verts / 144 faces / 288 tris. Non-manifold 0, boundary 0.
- Reads as a dry-material dust skirt around the drop mouth, not a pressurized spray face.

### `Applicator_ProtectedDelivery` mouth

- Object origin / east pipe / material unchanged.
- 42 verts with world `X ≤ −0.62` and `Z < 1.63` sloped: drop `0` at `X=−0.62` to **`0.040 m`** at `X=−0.76`.
- World Zmin `1.605` → **`1.572`**. XY max unchanged.
- 112 verts / 216 tris (same counts). Gravity-chute lip into the salt head; remaining run still a protected duct, not a hopper.

Production bounds remain **`1.250 × 0.990 × 1.795 m`** (`[−1.010, −0.700, 0] … [0.240, 0.290, 1.795]`).

---

## Unchanged objects / invariants

Must-remain / measured:

| invariant | result |
|---|---|
| applicator root placement contract | PASS — validation uses `(0.60, 3.25, 0)` / R=`I` / scale 1 |
| operator side `+Y` | PASS |
| west-side red-tape volume `X −2.55…−1.20` | PASS — 0 applicator triangle hits |
| `AP_Procedure` | PASS — player box 0 hits; anchor not moved |
| canonical anchors | 19/19 unused as edit targets; 3ccf SHA unchanged |
| canonical rails / guides / cradle | untouched |
| Intake ↔ Procedure topology | untouched; Y-only route, 41 samples @ 0.05 m |
| Procedure endpoint / canonical log pose | PASS (`PROCEDURE_LOG_POSE_DELTA=0`) |
| player circulation topology | local +9 cells = machine footprint only (same as PREFLIGHT_03) |
| saw / Output Table / pallet / disposal | no overlay hits; AABB stays north of mill Y=1, west of saw X=2 |
| canonical 3ccf SHA | immutable this gate |

No receptacle, no new object names, no Review objects in production scene.

---

## Procedure sweep results before / after

Unrotated log `2.60×0.52×0.52`, mill `(−1.15, 1.00, 1.16)` → cradle `(−1.15, 3.00, 1.16)`, 41 stations, triangle–triangle. Rollers = `INTENDED_SUPPORT`.

| | PREFLIGHT_03 (712c as-placed) | **CORR01** |
|---|---|---|
| canon-only REAL | 0 | **0** |
| with applicator REAL | **11** | **0** |
| limiting object | `Applicator_LocalSplashGuard` Y `2.30…2.80` | none |
| raw stations (incl. rollers) | 27 | **16** (rollers only, same as canon-only) |
| ingress | FAIL | **PASS** |
| treatment pose | PASS (0 log tri hits) | **PASS** (0 log tri hits) |
| return / egress | FAIL (same 11) | **PASS** (`REAL=0`) |

---

## Minimum relevant clearances

| pair | before | after | note |
|---|---|---|---|
| log ↔ `Applicator_FanNozzle` (treatment) | 0.140 m | **0.140 m** | preserved |
| log ↔ `Applicator_HeadSeal` | 0.155 m | 0.148 m | skirt slightly larger; CLEAR |
| log ↔ `Applicator_ProtectedDelivery` | 0.185 m | 0.152 m | mouth drop; CLEAR |
| log ↔ `Applicator_LocalSplashGuard` | AABB overlap, 0 tri | **0.025 m** AABB and tri CLEAR | hood above log top |
| log ↔ `ProcedureGuide` (canon sweep min_pos) | 0.005 m at `(−1.15, 1.80, 1.16)` | **0.005 m same object, same station** | **not spent** |
| cradle seat | 0.005 m | 0.005 m | unchanged |
| `AP_Procedure` vs applicator | 0 hits | 0 hits | |
| west red-tape vs applicator | 0 hits | 0 hits | |

Canonical `PROCEDURE_SPUR_CLEARANCE_005` margin was **not** used to clear the splash guard.

---

## Static / circulation findings

Stationary overlay vs 3ccf near set:

| pair | tris | class |
|---|---|---|
| Foot / ProtectedFluidBody / Manifold / ServiceCoupling × `CompoundProxy` | 132+195+34+39 | INTENDED replacement occupancy |
| `Applicator_Foot` × `MainShopFloor` | 16 | INTENDED support; coincident WARN inherited |

No new contacts vs CORR01 guides / rails / cradle. `UNINTENDED_STATIC_INTERSECTIONS=0`.

Circulation window `(−2…3, 1.5…5.5)` @ 0.10 m: blocked 882 → 891, **NEW=9, FREED=0**. Same local footprint as PREFLIGHT_03. No new island / cul-de-sac.

---

## Salt-readability rationale

PREFLIGHT_03 class A: liquid fan / gasket / fluid mouth vs granular salt.

After CORR01:

- **Outlet** is a short, wide, downward-open rectangular mouth (`0.32 × 0.22 × 0.042 m`) with three metering vanes — gravity/metered dry delivery, not a 27 mm spray slit.
- **Seal** is a rubber cuff around that mouth (open through), a dust/dry skirt, not a crushed spray gasket.
- **Delivery** west end drops 40 mm into the head as a chute lip; the protected duct remains, but the mouth is no longer a pipe orifice aimed like a liquid jet.
- **No hopper / no fill window / no quantity cue.** Service +Y coupling is unchanged as a one-dose player dock. Persistent machine inventory is not implied.

Machine topology is the same standing side-node: foot, pedestal, manifold, overhead head over `ApplicationPoint`. Not a tunnel, not a pass-through, not a bench redesign.

SALT_SEMANTIC_READABILITY=`BOUNDED_CORRECTION_APPLIED`

---

## Negative-control evidence

Same overlay validator. Discriminating: a clean REAL=0 is not accepted without a fail path.

| ID | setup | REAL | result |
|---|---|---|---|
| A | corrected meshes, **original 712c splash restored** in the forbidden envelope | **11** | FAIL as required. Limiter `Applicator_LocalSplashGuard`, stations Y `2.30…2.80` — **identical to PREFLIGHT_03**. Proves the test still catches the defect this gate removed. |
| B | synthetic box filling the **trimmed splash Z-band** (`Z 1.31…1.42` in old splash XY) | **15** | FAIL. `NEG_ForbiddenSplashBand` Y `2.30…3.00`. |
| C | salt mouth dropped `−0.20 m` into the log Z envelope | **4** | FAIL. `Applicator_FanNozzle` Y `2.65…2.85`. |

All in-memory on dumps. Candidate `.blend` not mutated by negatives.

---

## Asset-local validation

| check | result |
|---|---|
| Production_Asset objects | **17** (13 mesh + 3 empties + root) |
| Review_Only extras | 11, not in production scene, not in GLB |
| duplicate names | 0 |
| parentage | all 13 meshes + 3 empties → `TLAW_COMPOUND_APPLICATOR_01` |
| scale | all unit `(1,1,1)` |
| rotation | all 0 |
| null materials | 0 |
| UV | 13/13 |
| shared mesh datablocks | none (`users=1`) |
| actions / armatures | 0 |
| loose verts / degenerates | 0 on changed meshes |
| non-manifold on changed meshes | **0** |
| hierarchy | sane; no duplicate scene content |

---

## Scene-scoped GLB verification

Export: `Production_Asset` only, `export_yup=True`, `export_apply=True`, cameras/lights off. Review_Only not exported.

| | 712c GLB | **CORR01 GLB** |
|---|---|---|
| SHA-256 | `0796a126…` | `a6b3c135d1c079528663c6ab894aede4489d8b8d6cc4818b7e0d8e9b1d6ea4dc` |
| size | 162 596 B | 198 520 B |
| objects | 17 | **17** |
| meshes | 13 | **13** |
| triangles | 4 360 | **4 576** (+216 = Fan +36, Seal +180) |
| materials | 4 | **4** (same names) |
| cameras / lights / REVIEW | 0 | **0** |
| null materials | 0 | **0** |
| required names (splash, fan, seal, delivery, 3 empties, root) | present | **present** |

Roundtrip import (factory settings, no save): hierarchy and segmentation survive. Triangle count matches evaluated blend (4 576).

---

## Unexplained delta count

`UNEXPLAINED_OBJECT_DELTA=0`  
`ADD=0` `REMOVE=0` `UPDATE=4` (authorized set only)

Tri delta +216 fully attributed to FanNozzle and HeadSeal remesh. Splash tri count unchanged (vert move only). Delivery tri count unchanged (mouth vert move only).

---

## Inherited / new WARNs

1. **`PROCEDURE_SPUR_CLEARANCE_005`** — canonical, 0.005 m vs trimmed `ProcedureGuide`. Inherited. Overlay min_pos identical. Not spent.
2. **`SAW_TABLE_GUIDE_CLEARANCE_005` / disposal 0.055 vs 0.050** — canonical, unrelated, not spent.
3. **Foot × MainShopFloor coincident** — inherited intended support.
4. **`COMPOUND_APPLICATION_COVERAGE_UNDEFINED`** — inherited; head is still a local patch (~0.32 m of a 2.60 m log). No spray bar / tunnel added.
5. **Splash-to-log 0.025 m** — new positive hood clearance. Tight by design (local shield above the patch). Not a penetration.
6. **UNITY_RUNTIME unbound** — not this gate.
7. **No one-dose receptacle** — by choice; +Y service dock already exists. Not a defect.

NON-BLOCKING: n-gons remain on some **unchanged** parts (e.g. Foot). Changed Fan/Seal/Splash have 0 n-gons.

---

## Final worker recommendation

**PASS WITH WARN**

Procedure Y-sweep with the corrected standalone candidate at the accepted pose is `REAL=0` (ingress / treatment / egress). Salt head is a gravity/metered mouth rather than a liquid fan. Canonical 3ccf geometry, SHA, 0.005 m ProcedureGuide clearance, 19 anchors, routes, and endpoints are unchanged. Negatives A–C FAIL if the splash lip or delivery head re-enter the forbidden envelope.

Do **not** integrate in this gate. Candidate is ready for a later Control Center integration/transplant gate if authorized.

STOP.
