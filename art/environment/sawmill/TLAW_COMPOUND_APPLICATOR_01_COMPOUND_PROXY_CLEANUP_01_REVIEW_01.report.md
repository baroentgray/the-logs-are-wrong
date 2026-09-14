# TLAW_COMPOUND_APPLICATOR_01_COMPOUND_PROXY_CLEANUP_01_REVIEW_01

**Role:** independent production review only  
**Review date:** 2026-09-14  
**Production mutation:** NONE  
**Blender save/re-export:** NONE  
**Proxy restoration/further cleanup:** NONE  
**Canonical mutation:** NONE  
**Unity/Pipeline/Astra:** NOT RUN  
**Git mutation:** NONE  

---

# FINAL VERDICT

**VERDICT: APPROVE WITH NON-BLOCKING NOTES**

**PROMOTION_ELIGIBLE=YES**

Cleanup candidate:

- `.blend` `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a`
- GLB `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65`

is technically eligible for a **later, separately owner-authorized canonical-promotion gate**.

Eligibility is not promotion authorization.

The previous INT01 promotion blocker is resolved:

`CompoundProxy = REMOVED`

The independent review found no second production-object delta, no applicator regression, no anchor movement, no Procedure regression, and no new scene-level route conflict.

---

# 1. Package custody and identity

Required review ZIP:

`TLAW_COMPOUND_APPLICATOR_PROXYCLEAN01__be48b091__94ffb12f.zip`

Expected:

- SHA-256 `94ffb12fdc94cf8dc41a4d7707f89863738af2c674c37212113327d656bb302f`
- size `2118182 bytes`

Independently recomputed:

- SHA-256 `94ffb12fdc94cf8dc41a4d7707f89863738af2c674c37212113327d656bb302f`
- size `2118182 bytes`

**ZIP_CUSTODY=PASS**

The package was extracted to review sandbox storage and treated read-only.

All included production-artifact hashes were independently recomputed:

| role | SHA-256 | size | result |
|---|---|---:|---|
| input INT01 `.blend` | `0d9a66777a28b949e910885082ae3c4a4c54382a4957c331508e9c10fb226c1a` | 615378 | MATCH |
| input INT01 GLB | `04966c05f08e94a88409b526d4c3b529e450a70f2171b270057f79df1d3e1d0f` | 2336208 | MATCH |
| cleanup `.blend` | `be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a` | 614872 | MATCH |
| cleanup GLB | `eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65` | 2332388 | MATCH |

Manifest identities are consistent with the exact files in the ZIP.

No visually equivalent/re-exported substitute was used.

---

# 2. Lineage / prior independent evidence

Exact input INT01 identity is the candidate previously independently reviewed:

`0d9a6677... / 04966c05...`

The cleanup bundle itself records:

`PRIOR_REVIEW_EVIDENCE_MISSING_LOCALLY=YES`

That is not a geometry failure.

In the current Review runtime, the exact previous independent review report is available and was independently rehashed:

- filename `TLAW_COMPOUND_APPLICATOR_01_CORR01_CANONICAL_INTEGRATION_01_REVIEW_01.report.md`
- SHA-256 `8d358fed4b41d4ec3b5a001da1c3695ecba416b5eec06076b13508b5b5756beb`
- size `20716`

This matches the owner-gate expected prior-review identity.

Previous independent disposition:

`APPROVE WITH NON-BLOCKING NOTES`

with:

`CompoundProxy = NON-BLOCKING_CLEANUP_REQUIRED_BEFORE_PROMOTION`

and:

`PROMOTION_ELIGIBLE=NO`

The current cleanup directly addresses that identified blocker-to-promotion while preserving the exact reviewed INT01 input.

**LINEAGE_CUSTODY=PASS**

---

# 3. Review methods

Independent checks performed against exact input/output bytes:

1. ZIP SHA-256 and size verification.
2. Production-artifact SHA-256 and size verification.
3. Manifest consistency review.
4. Raw binary GLB parsing of exact INT01 and cleanup GLBs.
5. Node-set comparison.
6. Parent/hierarchy comparison by node name.
7. Local transform comparison by node name.
8. Exported mesh POSITION/NORMAL/index comparison.
9. UV numeric-delta comparison.
10. Material-definition and assignment comparison.
11. Exact applicator-subtree comparison.
12. Exact anchor enumeration/transform comparison.
13. Exact proxy mesh/name/material-remnant review.
14. Exact GLB integrity review.
15. Procedure sweep against exact cleanup GLB using independent triangle-vs-AABB volume SAT.
16. Negative controls against exact cleanup GLB.
17. Negative-control reproduction using triangle-surface intersection kernel for comparison with worker counts.
18. Full applicator-vs-non-applicator static intersection scan.
19. `AP_Procedure` local working-box check.
20. west-side `red_tape` working-volume check.
21. Review of Blender dependency-audit / cleanup / delta scripts and their emitted evidence as secondary Blender-only evidence.
22. Review of official validator output as secondary evidence, not as sole proof.

### Environment limitation

The Review runtime does not contain Blender/bpy.

The exact `.blend` files were therefore **not reopened** by the reviewer.

This means Blender-only facts such as constraints, modifiers, drivers, collection membership, hidden-object state, and scene/world datablocks could not be independently re-executed from the `.blend` files.

This is classified **NON-BLOCKING** for this narrowly bounded cleanup because:

- exact input and output `.blend` custody is established;
- the exact input is the previously independently reviewed INT01 identity;
- exact input/output production GLBs independently demonstrate a one-node/one-mesh export delta;
- every surviving exported production node keeps identical parent and transform;
- all surviving POSITION/NORMAL/index/material data are unchanged;
- the applicator subtree is exactly unchanged;
- the bundle includes a Blender-side dependency audit that explicitly checks children, constraint targets, modifier targets, drivers, animation, physics, mesh users, material users and required-object binds;
- that audit reports `SAFE_DIRECT_REMOVAL=YES` and `consequential_dependencies=[]`;
- the cleanup script is narrowly written to remove `CompoundProxy` and its unique zero-user mesh only;
- worker Blender delta evidence reports no other object/anchor/material/collection change;
- post-clean exact GLB and independent spatial review show no missing or substituted production content.

No Blender-only claim is presented as independently re-executed when it was not.

---

# 4. Exact production delta audit

## Node/object set at export level

INT01 GLB:

`324 nodes`

Cleanup GLB:

`323 nodes`

Exact name-set delta:

- only in INT01: `CompoundProxy`
- only in cleanup: none

There are no duplicate node names in the cleanup GLB.

**EXPORTED_OBJECT_DELTA=REMOVE CompoundProxy ONLY**

## Hierarchy and transforms

For every one of the 323 shared nodes:

- parent relationship: unchanged
- local translation: unchanged
- local rotation: unchanged
- local scale: unchanged

**SHARED_NODE_PARENT_DELTA=0**

**SHARED_NODE_TRANSFORM_DELTA=0**

## Mesh geometry

For every shared mesh node:

- POSITION arrays: unchanged
- NORMAL arrays: unchanged
- indices: unchanged
- material assignment: unchanged

No surviving production geometry changed.

The only numeric re-export variation found is `TEXCOORD_0` float noise on 149 shared mesh nodes.

Maximum absolute UV delta:

`5.960464477539063e-08`

This is export-level floating-point noise and not a meaningful UV/layout mutation.

The applicator subtree has **no UV delta at all**.

Classification:

`FALSE POSITIVE / NOT AN ISSUE`

## Materials

Material definitions are identical input -> cleanup.

Material count:

`25 -> 25`

No material was added or removed.

`TLAW_MechanicalMetal` remains:

`roughness = 0.4699999988079071`

before and after cleanup.

The previously accepted standalone `0.48 -> integrated 0.47` remap was not changed by this gate.

## Scene roots

Scene-root names are identical before/after:

- `TLAW_SAWMILL_LAYOUT_V2`
- `TLAW_PALLET_DISPATCH_01`
- `TLAW_INSPECTION_GATE_01`
- `TLAW_SAW_01`
- `TLAW_OUTPUT_TABLE_01`

**UNEXPLAINED_EXPORTED_DELTA=0**

Blender-side worker delta evidence additionally reports:

`APPLICATOR_CHANGED_OBJECTS=0`

`OTHER_CANONICAL_CHANGED_OBJECTS=0`

`UNEXPLAINED_DELTA=0`

No independent evidence contradicts those Blender-side results.

---

# 5. CompoundProxy removal

## Object

`CompoundProxy` exists in exact INT01 GLB and is absent from exact cleanup GLB.

## Mesh

INT01 proxy mesh name:

`Cube.198`

Evaluated GLB triangles:

`44`

`Cube.198` is absent from the cleanup GLB.

No cleanup mesh has the same exact geometry signature as the removed proxy mesh.

Therefore the proxy mesh was not silently renamed or duplicated.

## Route_Ochre

`Route_Ochre` remains present.

Objects using it:

- INT01: 32
- cleanup: 31

Exact removed material user:

`CompoundProxy`

No other `Route_Ochre` user is removed.

No cleanup `Route_Ochre` geometry overlaps the former proxy world AABB.

Therefore:

- shared material preserved: **YES**
- visible ochre proxy pedestal remnant: **NO evidence**
- duplicate proxy geometry under another name: **NO**
- proxy/applicator physical overlap after cleanup: **REMOVED**

## Blender dependency evidence

The bundled Blender-side pre-removal audit reports:

- children: none
- constraints on proxy: none
- external constraint users: none
- external modifier users: none
- driver refs: none
- animation/NLA: none
- rigid body/collision: none
- custom properties: none
- mesh `Cube.198` users: 1
- `Route_Ochre` shared
- official required-object bind: absent
- Unity/Domain `CompoundProxy` name bind: none
- consequential dependencies: none
- `SAFE_DIRECT_REMOVAL=YES`

Review of the audit script confirms it actually checks the dependency categories claimed above; it is not merely a hardcoded PASS emitter.

Because Blender is unavailable in the reviewer runtime, these Blender-only dependency findings are accepted as corroborating evidence rather than claimed as independently re-executed.

**COMPOUND_PROXY_REMOVAL=PASS**

---

# 6. Applicator integrity

Exact cleanup applicator subtree:

`17 nodes`

Production meshes:

`13`

Triangles:

`4576`

Input and cleanup subtrees have exactly the same node names.

For all applicator nodes:

- parent relationships: unchanged
- transforms: unchanged
- mesh POSITION: unchanged
- mesh NORMAL: unchanged
- UV: unchanged
- indices: unchanged
- materials: unchanged

Root:

`TLAW_COMPOUND_APPLICATOR_01`

Parent:

`TLAW_SAWMILL_LAYOUT_V2`

Exported root translation corresponds to Blender-space accepted pose:

`(0.600000024, 3.25, 0)`

Rotation:

identity / 0

Scale:

1

**APPLICATOR_CHANGED_OBJECTS=0 independently supported at production-export level**

**APPLICATOR_INTEGRITY=PASS**

---

# 7. Canonical integrity

The cleanup operation starts from exact independently reviewed INT01 and removes one canonical placeholder object.

Every surviving production node in the cleanup export has the same:

- parent;
- transform;
- POSITION geometry;
- NORMAL geometry;
- indices;
- material assignment

as INT01.

Therefore no accommodation edit is present in:

- `ProcedureGuide`
- `ProcedureGuide.001`
- `ProcedureCradle`
- `ProcedureCradleSide`
- `ProcedureCradleSide.001`
- `ProcedureTransferBed`
- floor
- saw/process line
- Output Table
- pallet/dispatch
- disposal
- overhead feed
- route geometry
- anchors

The only removed production geometry is the explicitly authorized `CompoundProxy`.

**OTHER_CANONICAL_CHANGED_OBJECTS=0 independently supported at export level**

**CANONICAL_INTEGRITY=PASS**

---

# 8. Anchor audit

Input anchors:

`19`

Cleanup anchors:

`19`

Anchor name set:

unchanged

For all 19 anchors:

- parent unchanged
- local transform unchanged

`AP_Procedure` remains at Blender-space:

`(-0.30, 4.00, 0)`

with parent:

`TLAW_SAWMILL_LAYOUT_V2`

**ANCHOR_TRANSFORM_DELTA=0**

**ANCHOR_AUDIT=PASS**

---

# 9. Procedure sweep — exact cleanup candidate

Accepted log:

`2.60 x 0.52 x 0.52 m`

Accepted path:

`(-1.15, 1.00, 1.16) -> (-1.15, 3.00, 1.16)`

Sampling:

`41 stations @ 0.05 m`

Constraints:

- rigid
- unrotated
- no lift
- no route deviation

## Independent volume-overlap result

All 13 applicator production meshes:

`REAL=0`

Across the complete 41-station route.

Procedure meshes independently checked:

- `ProcedureCradle`
- `ProcedureCradleSide`
- `ProcedureCradleSide.001`
- `ProcedureGuide`
- `ProcedureGuide.001`
- `ProcedureTransferBed`

Result:

`REAL=0`

Ingress:

`PASS`

Treatment:

`PASS`

Return/egress:

`PASS`

This check was performed from exact cleanup GLB geometry and does not rely on the worker `validate.json`.

**PROCEDURE_SWEEP=PASS**

---

# 10. Clearances

## LocalSplashGuard <-> log

Independent cleanup measurement:

`0.025000052452087473 m`

This is materially identical to the independently reviewed INT01 relationship.

Classification:

**DURABLE WARN**

Acceptable for the fixed unrotated/no-lift treatment geometry.

Not a generic project clearance threshold.

## FanNozzle treatment clearance

Independent cleanup measurement:

`0.1399999427795411 m`

## Canonical ProcedureGuide <-> log

Independent minimum positive AABB relationship:

`0.005000019073486239 m`

This is the separate inherited canonical Procedure clearance.

It is not applicator clearance and was not consumed by proxy cleanup.

Classification:

**DURABLE INHERITED WARN**

---

# 11. Negative controls

Controls were executed in memory against exact cleanup GLB-derived geometry.

No production artifact was saved or mutated.

## A — equivalent original splash intrusion

`Applicator_LocalSplashGuard` shifted down `0.135 m`, reproducing the removed pre-CORR01 vertical intrusion.

Triangle-surface detector:

`FAIL — 11 route stations`

First/last affected station:

`Y=2.30 .. 2.80`

Independent volumetric SAT also fails at 11 stations.

## B — forbidden vertical-band intrusion

Synthetic geometry inserted into the old forbidden splash band.

Triangle-surface detector:

`FAIL — 15 route stations`

First/last affected station:

`Y=2.30 .. 3.00`

Independent volumetric SAT also fails at 15 stations.

## C — treatment head/nozzle intrusion

`Applicator_FanNozzle` shifted downward `0.20 m`.

Triangle-surface detector:

`FAIL — 4 route stations`

First/last affected station:

`Y=2.65 .. 2.80`

Independent volumetric SAT also fails and is intentionally more conservative because it detects contained volume overlap after surface crossings.

The clean candidate returns no applicator/log overlap.

The controls therefore discriminate real geometry changes and are not hardcoded expected outcomes.

**NEGATIVE_CONTROLS=PASS**

---

# 12. Static intersections / proxy postcheck

Independent exact cleanup GLB scan tested every applicator production mesh against every non-applicator mesh.

Only one static surface contact remains:

`Applicator_Foot x MainShopFloor = 16 triangle intersections`

This is the inherited intended floor support.

No `CompoundProxy` contact remains.

No applicator contact with unrelated repair proxy geometry remains.

No new Procedure, saw, output, pallet, disposal, overhead-feed, anchor or route-geometry intersection is present.

**STATIC_INTERSECTIONS=PASS WITH INTENDED FLOOR SUPPORT**

---

# 13. Circulation and red-tape relationship

## West red-tape volume

Independent exact cleanup GLB check:

`0 applicator hits`

The salt applicator does not spatially consume the separate west-side `red_tape` affordance.

## AP_Procedure local player box

Independent exact cleanup GLB check:

`0 applicator hits`

## Circulation reasoning

The cleanup exact export differs from the independently reviewed INT01 only by **removing** the old obstacle proxy.

No surviving geometry moves or grows.

Therefore proxy cleanup cannot create a new geometric blockage in a collision/occupancy-based circulation model.

The bundled official validator result is consistent with that monotonic result:

- circulation probes connected: `8/8`
- one region
- free cells `2674`
- reachable cells `2517`
- same as INT01

The official circulation grid was not independently rerun because Blender is unavailable, but exact production geometry provides no contradictory evidence and no mechanism for a new blockage exists.

**CIRCULATION=PASS WITH EXISTING LOCAL-FOOTPRINT WARN**

**WEST_RED_TAPE_RELATIONSHIP=PASS**

---

# 14. Full scene regression

Independent exact GLB comparison establishes:

- all 323 surviving node transforms unchanged;
- all surviving production POSITION geometry unchanged;
- all surviving NORMAL geometry unchanged;
- all indices unchanged;
- material assignments unchanged;
- scene roots unchanged;
- anchors unchanged;
- applicator unchanged;
- only `CompoundProxy`/`Cube.198` removed.

The previous independently reviewed INT01 scene therefore retains its accepted:

- saw/process line;
- Procedure route;
- disposal route;
- Output Table;
- pallet/dispatch;
- overhead feed;
- anchor layout;
- player-route geometry.

Removal of the old replacement proxy cannot create a new obstruction.

Bundled official validator:

`23 PASS / 0 FAIL`

with:

- `1 UNTESTED` (`export.engine_import`)
- `4 SKIP`

is secondary corroboration, not the sole basis for this conclusion.

**FULL_SCENE_REGRESSION=PASS WITH WARN**

---

# 15. Exact cleanup GLB review

Exact cleanup GLB:

`eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65`

Independent raw GLB results:

- scenes: `1`
- nodes: `323`
- meshes: `276`
- triangles: `36048`
- materials: `25`
- REVIEW nodes: `0`
- cameras: `0`
- lights: `0`

Delta from exact INT01 GLB:

- nodes: `324 -> 323` = `-1`
- meshes: `277 -> 276` = `-1`
- triangles: `36092 -> 36048` = `-44`
- materials: `25 -> 25`

The `-44` evaluated triangles are exactly the removed GLB `CompoundProxy` mesh.

Integrity:

- finite POSITION data: PASS
- finite NORMAL data: PASS
- finite UV data: PASS
- missing UV primitives: `0`
- missing NORMAL primitives: `0`
- invalid indices: `0`
- zero-area triangles: `0`
- zero-length normals: `0`
- detected face/vertex normal opposition: `0`
- duplicate scene content: none identified
- missing applicator geometry: none
- duplicate applicator: none

**CLEANUP_GLB=PASS**

---

# 16. Material note

`TLAW_MechanicalMetal`

cleanup:

`roughness = 0.4699999988079071`

INT01:

`roughness = 0.4699999988079071`

No material change occurred in this cleanup.

The previously reviewed standalone `0.48` vs integrated `0.47` difference remains an inherited explained/non-blocking condition and is not reopened.

**MATERIAL_DELTA=0**

---

# 17. Salt semantics

The actual applicator geometry is exactly unchanged from independently reviewed INT01.

Therefore the accepted dry/granular salt reading remains:

- gravity distributor;
- metering vanes;
- dry/open skirt;
- chute lip;
- no persistent hopper/fill affordance.

Removing the opaque ochre replacement proxy **reduces** semantic ambiguity rather than adding any new machine state.

No new:

- generic compound semantics;
- liquid semantics;
- machine inventory;
- fill state;
- coverage state;
- item ID;
- Procedure state;
- machine gameplay authority

is introduced.

**SALT_SEMANTICS=PASS**

---

# 18. Unity / engine import

Unity was not run.

`export.engine_import = UNTESTED`

`UNITY_BINDING = UNTESTED`

Classification:

**NON-BLOCKING WARN**

No export incompatibility is visible in the exact GLB.

This review does not claim Unity-side proof.

---

# 19. Classified findings

## BLOCKER

None.

## IMPORTANT

None.

## NON-BLOCKING

### N-01 — LocalSplashGuard/log clearance

`~0.025000052 m`

Inherited durable WARN. Unchanged.

### N-02 — canonical Procedure clearance

`~0.005000019 m`

Inherited durable WARN. Separate from applicator clearance and unchanged.

### N-03 — Unity / engine import

UNTESTED by gate definition.

### N-04 — Blender executable unavailable in independent review runtime

Blender-only dependency/collection/scene-world checks could not be independently re-executed.

This does not block the narrow cleanup disposition because exact custody, exact production-export delta, prior independent INT01 custody, inspected cleanup/audit scripts, bundled Blender-side audit evidence, and independent spatial/GLB checks all converge on the same single-object cleanup.

## POLISH

None required before promotion eligibility.

## FALSE POSITIVE / NOT AN ISSUE

### F-01 — UV float noise

149 shared exported mesh nodes show maximum TEXCOORD numeric difference:

`5.960464477539063e-08`

Positions, normals, indices, transforms and material assignments are unchanged.

Not a meaningful UV mutation.

### F-02 — raw Blender `-12 tris` vs exported GLB `-44 tris`

Not inconsistent.

The raw `CompoundProxy` mesh is a 12-triangle box with a local bevel modifier; evaluated/exported geometry is 44 triangles.

### F-03 — prior independent report absent from worker bundle

Not a cleanup geometry failure.

The exact prior independent report is available in the current Review runtime and matches the required SHA/size.

---

# 20. Promotion readiness

**Question:** Is cleanup candidate
`be48b0910205b2a6135a131e49d2b3f099b75e1cbccea449d4cd98639968527a`
/
`eba529c0ebd04086d5fe4c4defb731e3580ca408a31e140f341a4da730a8cd65`
technically eligible for a separately owner-authorized canonical-promotion gate?

**Answer: `PROMOTION_ELIGIBLE=YES`**

The prior promotion-blocking cleanup item has been resolved:

- `CompoundProxy` absent;
- proxy mesh absent;
- no renamed/duplicate proxy geometry found;
- shared `Route_Ochre` preserved;
- applicator exact geometry preserved;
- all other exported canonical content preserved;
- anchors preserved;
- Procedure sweep remains clean;
- clearances unchanged;
- negative controls discriminate;
- circulation/red-tape relationships remain valid;
- GLB integrity passes.

This is **eligibility only**.

No promotion is authorized by this review.

---

# 21. Persistent project copy

The review environment does not expose the persistent Windows project filesystem:

`C:\Projects\TheLogsAreWrong\art\environment\sawmill\`

Therefore:

`PERSISTENT_PROJECT_COPY_REQUIRED=YES`

The owner/custody workflow must place the exact report in persistent project storage if required.

---

# 22. STOP boundary

STOP.

No Blender edits.  
No save/re-export.  
No proxy restoration.  
No further cleanup.  
No material change.  
No canonical mutation.  
No anchor/route change.  
No gameplay work.  
No Unity.  
No Pipeline.  
No Astra.  
No git add/commit/push/PR/merge.  
No promotion.  
No following gate.
