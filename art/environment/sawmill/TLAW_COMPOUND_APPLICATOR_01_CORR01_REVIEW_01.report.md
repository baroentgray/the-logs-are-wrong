# TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01

**Gate:** `TLAW_COMPOUND_APPLICATOR_01_CORR01_REVIEW_01`  
**Role:** independent production review only  
**Review date:** 2026-09-11  
**Mutation performed:** NO  
**Blender edits/save/re-export:** NO  
**Canonical mutation/integration:** NO  
**Git mutation:** NO  

---

## FINAL VERDICT

**APPROVE WITH NON-BLOCKING NOTES**

The corrected standalone candidate is independently supported as technically fit for
a later, separately owner-authorized bounded canonical-integration gate.

**INTEGRATION_ELIGIBLE=YES**

This is eligibility only. It is **not** authorization to integrate, transplant,
promote, bind in Unity, modify canonical, or begin any following gate.

No BLOCKER or IMPORTANT finding remains.

---

## 1. Exact identities reviewed

The recovered review package contained all six required production artifacts.
SHA-256 was recomputed directly from the recovered bytes.

| role | independently recomputed SHA-256 | result |
|---|---|---|
| corrected `.blend` | `9c39aaa75de76c13827c736f2653ce30405fb04e1680139bc64133870796a059` | **MATCH** |
| corrected scene-scoped GLB | `a6b3c135d1c079528663c6ab894aede4489d8b8d6cc4818b7e0d8e9b1d6ea4dc` | **MATCH** |
| original `.blend` | `712c310fcc45656d4f33b59b7d580cf9d7c14469c3d92b4da0b8847ea94293c9` | **MATCH** |
| original GLB | `0796a126c05933878067640a5befbb11898e4fa897fdd9d05290ea29442d47ad` | **MATCH** |
| current canonical `.blend` | `3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5` | **MATCH** |
| current canonical GLB | `48b1884ad81c7f6aebc7cb75c6e46da5e279bb7c7810052d91ab6931e925d665` | **MATCH** |

The review therefore operated on the exact owner-gated candidate, original, and
canonical identities. No derivative/re-export was substituted.

---

## 2. Review methods

Independent review used the recovered immutable artifacts and bundled tooling/evidence.

Performed:

1. direct SHA-256 recomputation of all six production artifacts;
2. raw GLB JSON/buffer inspection independent of the worker GLB report;
3. exact original-vs-candidate GLB node/transform/hierarchy/material/geometry diff;
4. exhaustive independent comparison of original vs corrected Blender dumps across
   all dumped object fields, including REVIEW objects;
5. independent rerun of the CORR01 overlay validator on the recovered triangle dumps;
6. reproduction of all three negative controls;
7. direct coordinate-set binding between recovered exact GLBs and the triangle dumps
   used by the sweep;
8. geometry integrity checks on the exact corrected GLB:
   positions, normals, UVs, indices, degenerate triangles and normal orientation;
9. code audit of correction/remesh/validation scripts;
10. independent assessment of the accepted salt-only stateless semantic contract.

### Environment limitation

The independent review runtime did not contain a Blender executable, so the recovered
`.blend` was not reopened directly by this reviewer.

This is **NON-BLOCKING** because:

- exact `.blend` identity was independently recomputed;
- original/corrected Blender dumps expose the scene/object state used for the change;
- an exhaustive dump comparison found exactly four changed objects;
- the exact candidate GLB geometry matches the corrected dump triangle-for-triangle;
- the exact original GLB geometry matches the original dump for the changed source objects;
- key exact canonical GLB geometry matches the canonical dump triangle-for-triangle;
- the spatial validator was independently rerun on those dump triangles;
- correction scripts were audited and target only the authorized correction surface.

No production mutation was required to perform this review.

---

## 3. Delta audit

Owner-required claim:

`UPDATE=4 / ADD=0 / REMOVE=0 / UNEXPLAINED=0`

### Independent result

**PASS**

All original and corrected Blender-dump object names are identical:

`28 -> 28`

An exhaustive comparison of **all** dumped objects, including REVIEW objects, found
changes in exactly four objects:

- `Applicator_FanNozzle`
- `Applicator_HeadSeal`
- `Applicator_LocalSplashGuard`
- `Applicator_ProtectedDelivery`

No fifth object differed in any dumped field.

### Authorized-object changes

#### `Applicator_FanNozzle`

Changed:

- mesh datablock `Cube.014 -> Applicator_FanNozzle_CORR01`;
- geometry/bounds;
- `540 -> 576` evaluated triangles;
- `280 -> 296` evaluated vertices.

Unchanged:

- parent;
- object transform;
- scale/rotation;
- material;
- collection membership;
- modifiers;
- hide/render state;
- UV presence;
- one-user mesh ownership.

#### `Applicator_HeadSeal`

Changed:

- mesh datablock `Cube.013 -> Applicator_HeadSeal_CORR01`;
- geometry/bounds;
- `108 -> 288` evaluated triangles;
- `56 -> 144` evaluated vertices.

Unchanged:

- parent;
- object transform;
- scale/rotation;
- material;
- collection membership;
- modifiers;
- hide/render state;
- UV presence;
- one-user mesh ownership.

#### `Applicator_LocalSplashGuard`

Changed only in geometry-derived fields/bounds.

Object parent, transform, material, modifiers, visibility, collection and mesh identity
remain unchanged.

#### `Applicator_ProtectedDelivery`

Changed only in geometry-derived fields/bounds.

Object parent, transform, material, modifiers, visibility, collection and mesh identity
remain unchanged.

### Top-level Blender-dump state

Independent comparison:

- scenes: **identical**
- collections: **identical**
- materials: **identical**
- unit scale: **identical**
- Blender version: **identical**
- object set: **identical**
- REVIEW object set/state: **identical**

The top-level mesh datablock list differs only because the two authorized remeshes
replace `Cube.014` and `Cube.013` with the two CORR01 datablocks. All datablocks are
reported with `users=1`.

**DELTA_DISPOSITION: `UPDATE=4 / ADD=0 / REMOVE=0 / UNEXPLAINED=0` independently supported.**

---

## 4. Exact GLB delta audit

Raw glTF inspection of original and corrected GLBs produced:

- identical node set: **17 / 17**
- identical hierarchy: **PASS**
- identical transforms on all 17 nodes: **PASS**
- identical scene root: `TLAW_COMPOUND_APPLICATOR_01`
- identical material set: **4 / 4**
- geometry changed at exactly four production nodes.

Exact changed nodes:

1. `Applicator_FanNozzle`
2. `Applicator_HeadSeal`
3. `Applicator_LocalSplashGuard`
4. `Applicator_ProtectedDelivery`

All other GLB node geometry signatures are unchanged.

This independently corroborates the Blender-dump delta audit.

---

## 5. Functional sweep review

Accepted pose:

- location `(0.60, 3.25, 0)`
- rotation `0`
- scale `1`

Accepted log route used by the recovered validator:

- rigid log `2.60 x 0.52 x 0.52 m`
- unrotated;
- no lift;
- mill pose `(-1.15, 1.00, 1.16)`
- Procedure pose `(-1.15, 3.00, 1.16)`
- straight Y-only ingress/return;
- 41 stations at 0.05 m spacing.

The candidate `LogReference` resolves to the accepted Procedure pose with only
floating-point noise:

`alignment_error = 3.34e-08 m`

### Independent rerun

The recovered validator was rerun independently on the recovered triangle dumps.

Result:

- canonical-only sweep: `REAL=0`
- corrected applicator sweep: `REAL=0`
- treatment pose: no unintended applicator/log triangle hits
- egress/return: `REAL=0`
- `AP_Procedure`: 0 hits
- west red-tape volume: 0 hits

The independently generated validation JSON is semantically identical to the worker
validation JSON.

### Canonical limiter

Canonical minimum positive sweep clearance remains:

`ProcedureGuide ~= 0.005000019 m`

at:

`(-1.15, 1.80, 1.16)`

The same limiter and value appear with and without the corrected applicator overlay.

Therefore the applicator correction did **not** consume the canonical Procedure margin.

---

## 6. Dump-to-exact-GLB binding

A critical review concern was whether the recovered dumps used by the sweep actually
represent the exact GLBs under review.

This was checked independently by converting GLB coordinates back to Blender axes and
comparing unordered triangle-coordinate sets.

### Corrected candidate

All 13 production mesh objects match the corrected dump triangle-for-triangle at
`1e-5` coordinate precision, with identical bounds.

This includes all four corrected objects.

### Original source

The four source objects used for the correction/negative control match the original
GLB dump triangle-for-triangle:

- `Applicator_ProtectedDelivery`
- `Applicator_HeadSeal`
- `Applicator_FanNozzle`
- `Applicator_LocalSplashGuard`

### Canonical

Key Procedure geometry matches the exact canonical GLB dump triangle-for-triangle:

- `ProcedureGuide`
- `ProcedureGuide.001`
- `CompoundProxy`
- `ProcedureCradle`
- `ProcedureCradleSide`
- `ProcedureCradleSide.001`

Canonical GLB node translations also match Blender-dump transforms after the expected
Blender Z-up -> glTF Y-up axis conversion.

This materially closes the risk that a valid negative-control run was executed against
a stale or unrelated geometry dump.

---

## 7. Negative-control review

All three owner-required controls were independently rerun.

### A — original splash restored

Corrected overlay with exact original splash geometry restored:

`REAL=11`

Failure stations:

`Y = 2.30 ... 2.80`

Limiter:

`Applicator_LocalSplashGuard`

**PASS AS NEGATIVE CONTROL**

This reproduces the original defect signature.

### B — synthetic forbidden Z-band

Synthetic box inserted into the removed splash band:

`REAL=15`

Failure stations:

`Y = 2.30 ... 3.00`

**PASS AS NEGATIVE CONTROL**

### C — delivery head/nozzle dropped into log

`Applicator_FanNozzle` lowered by `0.20 m` in-memory:

`REAL=4`

Failure stations:

`Y = 2.65 ... 2.80`

**PASS AS NEGATIVE CONTROL**

### Assessment

The controls discriminate three relevant failure classes:

- regression to the exact prior guard defect;
- arbitrary re-entry into the forbidden vertical band;
- independent head/mouth intrusion.

The main plausible untested failure mode would have been stale/wrong sweep geometry or
route reconstruction. The exact-GLB-to-dump binding above substantially addresses that
risk.

No further negative control is required for this gate.

---

## 8. Clearance assessment

### LocalSplashGuard -> log

Corrected guard Z-min:

`1.445000052 m`

Accepted log top:

`1.420000000 m`

Result:

`~0.025000052 m`

### Review classification

**ACCEPTABLE WITH DURABLE WARN**

25 mm is tight but technically credible for this accepted relationship because:

- the payload is a fixed rigid envelope;
- rotation/lift are not part of the accepted mechanic;
- ingress, treatment and egress are independently clean;
- the GLB preserves the geometry exactly;
- 25 mm is large relative to export floating-point noise;
- the guard is static presentation geometry, not a moving swept part.

It must not become a generic TLAW clearance precedent.

### Canonical 5 mm clearance

The canonical `~0.005 m` `ProcedureGuide` clearance is a separate inherited canonical
relationship.

It is **not** applicator clearance.

It remains unchanged and unconsumed.

---

## 9. Salt-semantics assessment

Accepted role:

`TLAW_COMPOUND_APPLICATOR_01 = RESIN_SALT_APPLICATOR`

Accepted spatial contract:

`SIDE_NODE_FIXED_LOCAL_ZONE`

Accepted semantic boundary:

- existing `attempted_item=salt`;
- stateless physical/presentation adapter;
- no generic compound state;
- no machine inventory;
- no fill state;
- no hopper state;
- no coverage state;
- no continuous treatment simulation;
- no new item semantics.

### Independent geometry/script assessment

The final remesh script explicitly constructs:

#### FanNozzle

A thick rectangular cup/mouth with:

- downward-open cavity;
- roof;
- three internal closed metering vanes;
- no narrow liquid spray slit.

#### HeadSeal

A thick rectangular tube/cuff:

- both ends open;
- rubber material retained;
- no solid liquid-style gasket face.

#### ProtectedDelivery

The west mouth is sloped downward by up to `0.040 m`, forming a gravity chute lip.

#### Receptacle/state implication

No new object was added and no one-dose receptacle/hopper/fill indicator appears.

### Verdict

**SALT_SEMANTICS=PASS**

The corrected head reads materially more like gravity/metered dry granular delivery
than liquid spraying.

The three vanes give a plausible one-action distribution cue without requiring a
continuous material simulation.

The correction does not imply persistent hopper/fill-state gameplay.

The legacy internal name `Applicator_ProtectedFluidBody` remains in the asset, but no
geometry/state delta was introduced there and it is not a player-facing semantic
contract. Renaming it is not required by this gate.

---

## 10. Production asset quality

Recovered Blender evidence reports:

- 17 Production_Asset objects;
- 13 production meshes;
- all production meshes parented to `TLAW_COMPOUND_APPLICATOR_01`;
- all production scales `(1,1,1)`;
- all production rotations `0`;
- no null/empty material slots;
- UV on 13/13 meshes;
- all mesh datablocks `users=1`;
- no duplicate object names;
- no actions;
- no armatures;
- no asset-local validation issues.

Changed meshes:

### `Applicator_ProtectedDelivery`

- 112 verts
- 216 tris
- loose verts: 0
- non-manifold edges: 0
- boundary edges: 0
- degenerate faces: 0
- n-gons: 0

### `Applicator_HeadSeal`

- 144 verts
- 288 tris
- loose verts: 0
- non-manifold edges: 0
- boundary edges: 0
- degenerate faces: 0
- n-gons: 0

### `Applicator_FanNozzle`

- 296 verts
- 576 tris
- loose verts: 0
- non-manifold edges: 0
- boundary edges: 0
- degenerate faces: 0
- n-gons: 0

### `Applicator_LocalSplashGuard`

- 112 verts
- 216 tris
- loose verts: 0
- non-manifold edges: 0
- boundary edges: 0
- degenerate faces: 0
- n-gons: 0

### Fragmentation / budget

Production mesh count is unchanged at 13.

Corrected total:

`4576 tris`

Original:

`4360 tris`

Delta:

`+216 tris`

The increase is fully attributable to `FanNozzle` and `HeadSeal` and remains
comfortably within the established low-to-mid-poly production character of this asset.

**ASSET_QUALITY=PASS**

---

## 11. Exact corrected GLB review

Raw corrected GLB result:

- scenes: 1
- nodes/objects: **17**
- meshes: **13**
- triangles: **4576**
- materials: **4**
- cameras: **0**
- lights: **0**
- REVIEW nodes: **0**
- scene root: `TLAW_COMPOUND_APPLICATOR_01`

Materials:

- `TLAW_TreatmentOchre`
- `TLAW_MechanicalMetal`
- `TLAW_PaintedSteel`
- `TLAW_RubberSeal`

Every mesh primitive contains:

- `POSITION`
- `NORMAL`
- `TEXCOORD_0`

Independent exact-GLB integrity checks found:

- finite positions: PASS
- finite UVs: PASS
- finite normals: PASS
- zero-area triangles: 0
- face/vertex normal opposition detected: 0
- missing UV primitives: 0
- missing normal primitives: 0

The scene-scoped export contains no doubled multi-scene production content.

The four corrected relationships survive export, and the exact exported geometry is
the geometry independently used by the spatial review through the dump binding.

**GLB_REVIEW=PASS**

---

## 12. Canonical-fit findings

Exact canonical identity:

`3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5`

Accepted applicator pose:

`location (0.60, 3.25, 0)`  
`rotation 0`  
`scale 1`

At that exact pose, independent evidence supports:

- Procedure ingress: PASS
- treatment: PASS
- egress/return: PASS
- `AP_Procedure`: clear
- west red-tape volume: clear
- no Procedure guide/cradle interference from applicator
- intended `CompoundProxy` replacement occupancy only
- intended Foot/MainShopFloor support contact
- local circulation delta `+9` cells, matching the already accepted footprint class
- no requirement to move canonical anchors
- no requirement to move Procedure endpoint
- no route change
- no log rotation
- no log lift
- no circulation-topology redesign
- no canonical geometry correction required

The canonical `~0.005 m` ProcedureGuide margin remains independent.

**CANONICAL_FIT=PASS**

---

## 13. Classified findings

### BLOCKER

None.

### IMPORTANT

None.

### NON-BLOCKING

**N-01 — LocalSplashGuard/log margin is tight**

`~0.025 m`

Accepted for the fixed static relationship, but retain it as a durable integration WARN.

**N-02 — inherited canonical ProcedureGuide margin remains tight**

`~0.005000019 m`

This is canonical, not applicator clearance. It is unchanged and must remain unconsumed.

**N-03 — independent runtime had no Blender executable**

The exact `.blend` could not be reopened directly in this review runtime.

This does not block disposition because exact-byte identity, exhaustive Blender-dump
delta, exact GLB delta, dump-to-GLB triangle binding, correction-script audit, and
independent spatial/negative-control reruns all agree.

A later integration gate with Blender available should still use the exact reviewed
SHA, not a re-saved derivative.

### POLISH

None required for integration eligibility.

### FALSE POSITIVE / NOT AN ISSUE

**F-01 — No one-dose receptacle**

Not a defect. A receptacle/hopper could incorrectly imply persistent machine inventory
or fill state.

**F-02 — No generic compound system**

Correct. The accepted content binding is salt-only.

**F-03 — Canonical 5 mm Procedure clearance is not applicator clearance**

Correct. It remains a separate canonical relationship.

**F-04 — Legacy internal `ProtectedFluidBody` name**

Not a gameplay-state contract and not a reason to expand the authorized correction
surface.

---

## 14. Integration-readiness decision

**Question:** Is corrected candidate
`9c39aaa75de76c13827c736f2653ce30405fb04e1680139bc64133870796a059`
/
`a6b3c135d1c079528663c6ab894aede4489d8b8d6cc4818b7e0d8e9b1d6ea4dc`
technically eligible for a separately owner-authorized bounded integration gate into
canonical
`3ccf52e23b4321fb81f651f367253ecd380c09f5bdf3a4ad6b25684c74f982a5`?

**YES.**

**Disposition: APPROVE WITH NON-BLOCKING NOTES.**

The independent review supports:

- exact custody;
- bounded four-object correction;
- no unexplained production-object delta;
- corrected `REAL=0` Procedure sweep;
- discriminating negative controls;
- acceptable-but-tight 25 mm applicator/log clearance;
- preserved independent canonical 5 mm Procedure margin;
- dry/granular salt semantic correction;
- production-quality mesh/export state;
- clean scene-scoped GLB;
- canonical fit without canonical/layout/route changes.

Eligibility is not authorization.

---

## 15. STOP boundary

STOP.

No Blender edits.  
No save/re-export.  
No canonical mutation.  
No integration/transplant.  
No canonical promotion.  
No anchor/route/layout changes.  
No Unity binding.  
No gameplay implementation.  
No Astra.  
No Pipeline.  
No git add/commit/PR/merge.  
No following gate.
