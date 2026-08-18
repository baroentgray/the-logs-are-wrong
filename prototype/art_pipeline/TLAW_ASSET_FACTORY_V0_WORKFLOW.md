# THE LOGS ARE WRONG — Asset Factory v0 Workflow

Status: **working production workflow**  
Workflow revision: **v0.1**  
Purpose: experimental 3D asset production and preparation in the parallel prototype before possible promotion into production.

## 1. Core principle

Visual source, geometry, and gameplay implementation are separate layers.

Default route:

**Workshop → maps/reference → Claude + Blender → prototype review → production candidate**

Hyper3D is used selectively when Claude cannot reach the required shape efficiently or when generation is materially cheaper than manual/procedural construction.

Production gameplay code, colliders, and interactions must not become dependent on experimental prototype asset structure.

## 2. Visual package workflow

For a new asset, the workshop proceeds in this order:

**MAP → REVIEW → REFERENCE → REVIEW → APPROVAL → TEXT COMMIT → BINARY INBOX → CLAUDE SYNC → PACKAGE VERIFY → READY**

Do not automatically skip a review or handoff step.

### MAP

Create only the material/texture source needed for the current experiment.

Owner review statuses:

- `DRAFT`
- `REVISE`
- `APPROVED`

Only an `APPROVED` map is considered valid input for the next stage.

### REFERENCE

After the map is approved, create the visual asset reference.

The reference is an **art target**, not mandatory geometry to copy literally.

The owner reviews the reference separately.

### APPROVAL

Visuals are approved only after explicit owner approval.

Approval of the visuals does **not** mean the GitHub package is complete. If approved binaries are not yet stored in the repository, use the intermediate package status:

`APPROVED_VISUALS_PENDING_BINARY_UPLOAD`

### TEXT COMMIT

The workshop may create or update text package files in GitHub immediately after visual approval, including:

- `asset_card.md`;
- `provenance.md`;
- optional handoff/notes files when they are actually needed.

If the approved PNG/GLB/etc. binaries are not yet present in GitHub, `provenance.md` must remain in `APPROVED_VISUALS_PENDING_BINARY_UPLOAD` state. This is a valid intermediate state, not a failed package.

### BINARY INBOX

When the workshop cannot directly store an approved binary in GitHub, the owner performs the minimum manual bridge:

1. download the approved binary from the chat/tool that produced it;
2. copy it into the local Asset Factory inbox;
3. do not manually sort or rename it unless convenient.

Recommended local inbox:

```text
prototype/art_pipeline/_incoming/
```

`_incoming/` is a temporary local staging area, **not** an authoritative package location. It should be excluded from normal Git commits so arbitrary browser filenames and unverified binaries are not accidentally promoted.

Downloaded filenames are not trusted. The embedded marker `ASSET_ID · TYPE · REVISION` is the preferred machine-readable recovery key.

If a binary is directly available to a GitHub-writing tool as bytes/base64/file input, the inbox step may be skipped, but package verification is still required.

### CLAUDE SYNC

Local Claude is the default binary-ingestion worker when an inbox handoff is required.

Claude should:

1. inspect files in `_incoming/`;
2. identify approved binaries from their embedded Asset ID / type / revision and the existing `provenance.md`;
3. refuse to guess if identity is ambiguous;
4. rename identified files to the canonical package filenames;
5. move/copy them into the correct `prototype/art_pipeline/<ASSET_ID>/` package;
6. verify that the expected files are present;
7. commit and push the classified binaries;
8. update `provenance.md` only after verification succeeds.

The owner should not have to manually sort browser-generated filenames.

### PACKAGE VERIFY

Before a package becomes usable by Claude for production work, verify at minimum:

- expected binary files physically exist in the asset package;
- Asset ID matches the package folder and metadata;
- TYPE matches the declared visual source (`BARK`, `REFERENCE`, etc.);
- REVISION matches the approved revision;
- `provenance.md` names the files that actually exist;
- no ambiguous or unapproved binary was promoted from `_incoming/`.

After successful verification, package status becomes:

`APPROVED_READY`

Only `APPROVED_READY` opens the Claude production gate.

## 3. Asset ID

Every asset receives a stable `Asset ID`.

The ID should be present in:

- the Asset Card;
- documentation;
- the GitHub package/folder;
- visual deliverables where practical.

Recommended embedded marker:

`ASSET_ID · TYPE · REVISION`

Examples:

`TLAW_LOG_TEST_01 · REFERENCE · R01`

`TLAW_LOG_TEST_01 · BARK · R01`

A downloaded filename is **not** a reliable identifier.

If a browser gives an image an arbitrary filename, the embedded Asset ID must allow Claude/Grok to identify, rename, and sort it later.

The marker should be small, technical, consistently positioned, outside the useful object/texture area, and clearly readable.

The marker is outside the usable area but **inside the file**. Any consumer that maps a deliverable onto geometry must remove it first:

- the Asset Card states the usable rectangle where it is known;
- where it is not, the consumer detects the marker band and reports the crop it applied;
- a map is never used with the marker still on it.

If image generation cannot guarantee exact text, do not invent or alter the Asset ID. Add the marker as a technical overlay after generation.

## 4. Revisions

Approved visual deliverables receive a revision:

`R01`, `R02`, `R03`, ...

A materially changed variant receives a new revision.

Claude should work from a specific approved revision, not from “the latest image in chat.”

Derived maps — normal, roughness, height and similar, produced from an approved source rather than delivered — are **implementation artifacts by default**. They inherit the revision of the source they were derived from, and are named so that this is visible.

A derived map may be promoted to an approved deliverable in its own right, with its own review and revision, when it is worth maintaining independently of its source.

### Data map handling

Data maps carry values rather than colour, and are imported as non-colour/linear.

Compression is **not** forbidden. It is a declared choice, and the declared policy is validated against the uncompressed source for the signal the map actually carries: block compression can quantise a low-amplitude map to nothing while leaving a file that looks correct.

## 5. GitHub package

Recommended structure:

```text
prototype/art_pipeline/
  <ASSET_ID>/
    asset_card.md
    provenance.md
    <ASSET_ID>_REFERENCE_R01.png
    <ASSET_ID>_<MAP_TYPE>_R01.png
```

Additional files are created only when a real need appears:

```text
blender_handoff.md
notes_for_claude.md
*_CUT_END_R01.png
*_MASK_R01.png
```

Do not create a full speculative package “just in case.”

The asset package directory is authoritative only for files that have passed package verification. `_incoming/` is never an authoritative source.

### Asset Card machine-readable dimensions

`asset_card.md` carries the asset's hard numbers in a fenced block as well as in prose, so a built object can be compared with them without anyone reading:

```text
dimensions:
  length_m: 2.40
  diameter_m: 0.38
  tolerance_m: 0.02
```

Prose stays authoritative for intent. The block exists so a dimension can be checked rather than remembered.

## 6. provenance.md

Minimum contents:

```text
Asset ID:
Status:
Revision:
Purpose:

Approved visual sources:
- ...

Intended consumer:
- Claude / Blender

Usage:
- ...

Source:
- workshop image generation / Hyper3D / prototype salvage / other

Notes:
- ...
```

Relevant package statuses:

- `APPROVED_VISUALS_PENDING_BINARY_UPLOAD` — visuals approved, required binaries not yet verified in GitHub;
- `APPROVED_READY` — text + required binaries are present, verified, and ready for Claude production use.

Do not mark a package `APPROVED_READY` merely because the visuals were approved in chat.

## 7. Claude gate

Claude begins production work from a visual package only after the package is `APPROVED_READY`.

Claude should consume the approved source from the verified GitHub package rather than reconstructing it from chat history.

If the package is `APPROVED_VISUALS_PENDING_BINARY_UPLOAD`, Claude may perform the binary-ingestion/sync work described above, but must not start the asset-production task that depends on those visuals until verification completes.

## 8. Blender / asset structure

For new assets, prefer a predictable structure.

Minimum principle:

```text
AssetRoot
└── VisualRoot
```

Gameplay-addressable or movable parts remain separate objects.

Do not weld a part into the body if it may later require:

- a pivot;
- animation;
- swapping;
- interaction;
- a separate material/state;
- gameplay addressing.

Asset-specific structure is defined by the Asset Card.

Example for logs:

```text
AssetRoot
└── VisualRoot
    ├── Body / Bark
    ├── End_A
    ├── End_B
    ├── Signs
    └── Internal
```

`Signs` and `Internal` may be absent on a normal base log.

## 9. Transform discipline

For a new production candidate:

- dimensions are deliberate and metric;
- origin has an explicit functional purpose;
- movable-part pivots are placed at the real movement/hinge center;
- final transforms are normalized where doing so does not break required structure;
- changing the visual asset must not require changing gameplay logic;
- the Asset Card's dimensions govern the asset; an existing prototype scene is **not** rebuilt to match them, and a scene-sized variant is produced through the asset's authoritative modeling/generation route rather than by editing the delivered asset;
- declared dimensions are verified on the finished object after all surface relief is applied, not on the nominal shape it was grown from.

## 10. Prototype role

The parallel prototype is the **R&D environment for the Asset Factory**.

It may be used to:

- experiment with Claude/Blender;
- test procedural geometry;
- test materials;
- normalize old assets;
- evaluate Hyper3D;
- develop preview/validation tooling;
- break and rebuild experimental art assets.

Prototype gameplay code does not automatically promote into production.

When an asset is promoted, only the independently reviewed production-candidate visual/material/pivot/socket information should cross that boundary unless separately approved.

## 11. Existing prototype assets

Existing assets may later receive one of these statuses:

`TRANSFER_READY` — nearly ready for promotion.

`SALVAGE_VISUAL` — useful geometry/materials, normalization required.

`REFERENCE_ONLY` — useful design/reference, asset itself should not be promoted.

`DISCARD` — experiment no longer useful.

Do not perform a mass classification in advance. Classify when the asset becomes relevant.

### Acceptance is a separate axis

The statuses above classify **promotion readiness**. They do not record whether anyone has accepted the asset. The two axes are independent and are tracked separately.

Acceptance states:

`PROTOTYPE_ACCEPTED` — the owner has accepted the asset visually in the prototype.

Acceptance implies no promotion and no production integration. An accepted asset may still be classified `SALVAGE_VISUAL`; an asset may be promotion-ready and never accepted.

## 12. Factory automation

Factory v0 begins as a **discipline**, not a large infrastructure project.

First, several different assets should manually pass through:

**source → build → dimensions → hierarchy → materials → preview → owner review**

After repeated operations are observed, Claude may automate them with Blender/Python tooling.

Binary ingestion from `_incoming/` is a legitimate early automation target because it removes manual sorting without changing the artistic or gameplay contract.

Do not pre-emptively add:

- a universal asset queue;
- Meshy/Sloyd/Tripo API integrations;
- LOD automation;
- a Substance pipeline;
- paid retopology;
- complex Unity validation infrastructure.

Automate work that is actually repeated.

## 13. Responsibility split

### 3D Workshop

Responsible for:

- Asset Cards;
- art direction;
- maps;
- visual references/art targets;
- anomaly-sign readability;
- source maps, with their usable area stated where known;
- image prompts;
- Blender handoff;
- visual consistency with TLAW;
- text package creation/update after visual approval where GitHub access permits it.

The workshop does not falsely claim that a binary was stored in GitHub when the image-generation environment did not expose that binary for upload.

### Claude

Primary Blender production worker for:

- procedural/manual modeling;
- materials and UV work;
- derived maps (normal/roughness/height) built from approved sources;
- hierarchy;
- pivots and sockets;
- normalization;
- previews;
- binary inbox ingestion/sync when required;
- later Blender-side automation.

### Hyper3D

Specialized external generator for shapes that are difficult or inefficient to produce with Claude/Blender.

### Grok

Challenger/reviewer and optional worker for visual/technical experiments, especially character work where useful.

### Codex

Production software engineering:

- Unity runtime/editor tooling;
- gameplay prefab integration;
- production validation;
- CI;
- production contracts.

### Owner

Final visual and experiential acceptance.

For binary handoff, the owner's required manual action should be limited to downloading an approved file and placing it into the local inbox when direct binary upload is unavailable.

Automated inspection may validate measurable properties, but it must not claim that an asset looks, reads, or feels correct in the game unless the owner has confirmed it.

## 14. Character fallback

The character pipeline is not yet considered fully proven.

Claude and Grok should first demonstrate how far they can take the existing character, rig, animation, equipment, and variation workflow.

**Mixamo remains an available fallback**, but is not adopted by default and is not ruled out.

## 15. Completed reference case: TLAW_LOG_TEST_01

The first asset to run the whole Factory v0 loop.

Goal: determine whether Claude could build a convincing parameterized TLAW production log in Blender without Hyper3D.

Outcome: yes. The asset was built from the approved `R01` package to the Asset Card's exact dimensions, and the owner accepted it visually in the prototype. Package status `APPROVED_READY`, asset acceptance `PROTOTYPE_ACCEPTED`. It was not promoted and the prototype line was not converted to the card's dimensions.

The full postmortem — the route as it actually went, the faults that cost the most, the manual steps that repeat, and the changes that produced revision v0.1 of this workflow — is in:

`prototype/art_pipeline/TLAW_LOG_TEST_01/factory_case_report.md`

Read it before starting the next asset. Most of the rules added in v0.1 are only intelligible next to the failure that produced them.

## 16. Engine import gate and review parity

An asset is not finished when it exports. Between the DCC and the screen sit an exporter and an importer, and both rewrite data silently.

### Dimension check after import

The deliverable report states the asset's measured size **in the consuming engine**, not only in the DCC. Disagreement with the Asset Card is a defect.

An asset can be correct to three decimals in the DCC and arrive in the engine at one hundredth of its size: the unit conversion is parked on the outermost object, and importers commonly compensate mesh nodes without compensating that parent. No DCC-side measurement catches this.

### Mandatory import sanity gate

The gate is mandatory and deliberately narrow. It holds checks for failure modes already observed in a completed case, and it grows only when a new one is observed.

Before an imported asset is judged, confirm:

- measured size matches the Asset Card within tolerance;
- every mesh that samples a texture has a UV set;
- tangents exist wherever a tangent-space map is used;
- the expected materials landed on the expected parts;
- data maps are imported as non-colour/linear, and any compression applied to them matches the declared, validated policy;
- no placeholder is rendering alongside the asset that replaced it.

### Visual replacement is explicit, never by name

When a modelled asset replaces a greybox or placeholder, the placeholder is disabled through an explicit reference — never by matching a fragment of the asset's name.

A name-fragment match silently fails for the first asset whose name lacks the fragment, and it fails by rendering both objects at once. That reads as a material problem rather than a wiring problem, and it can absorb an unlimited number of correct fixes without showing any of them.

### Preview source parity

Preview renders supplied for owner review are made from the same prepared maps the engine consumes, and state that they are.

A review comparison whose two sides differ in more than the thing under test answers nothing.

### Geometry and material: default division

Unless the Asset Card says otherwise:

- geometry carries silhouette and large form;
- maps carry surface.

Geometry that also draws what the map draws prints a second, contradictory set of the same features.

For **tiling or world-scale materials**, surface detail is specified in metres of the finished asset, so a larger asset receives more detail cells rather than larger ones. This does not apply to uniquely unwrapped or hand-authored maps, where detail follows the layout rather than the world.
