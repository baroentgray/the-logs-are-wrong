# THE LOGS ARE WRONG — Asset Factory v0 Workflow

Status: **working production workflow**  
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

If image generation cannot guarantee exact text, do not invent or alter the Asset ID. Add the marker as a technical overlay after generation.

## 4. Revisions

Approved visual deliverables receive a revision:

`R01`, `R02`, `R03`, ...

A materially changed variant receives a new revision.

Claude should work from a specific approved revision, not from “the latest image in chat.”

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
- changing the visual asset must not require changing gameplay logic.

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
- image prompts;
- Blender handoff;
- visual consistency with TLAW;
- text package creation/update after visual approval where GitHub access permits it.

The workshop does not falsely claim that a binary was stored in GitHub when the image-generation environment did not expose that binary for upload.

### Claude

Primary Blender production worker for:

- procedural/manual modeling;
- materials and UV work;
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

## 15. Current first Factory v0 experiment

Asset:

`TLAW_LOG_TEST_01`

Goal: determine whether Claude can build a convincing parameterized TLAW production log in Blender without Hyper3D.

First visual package:

1. one `BARK` map → review;
2. one normal-log `REFERENCE` → review;
3. explicit visual approval;
4. commit/update `asset_card.md` and `provenance.md`;
5. if direct binary upload is unavailable, owner downloads both approved PNG files and places them in `_incoming/`;
6. Claude identifies, canonically renames, moves, commits and pushes both PNG files into `prototype/art_pipeline/TLAW_LOG_TEST_01/`;
7. Claude verifies the package and updates `provenance.md` from `APPROVED_VISUALS_PENDING_BINARY_UPLOAD` to `APPROVED_READY`;
8. only then does Claude begin building the parameterized log from the approved GitHub package.

Expected canonical visual filenames for R01:

```text
TLAW_LOG_TEST_01_BARK_R01.png
TLAW_LOG_TEST_01_REFERENCE_R01.png
```

Do not expand this first experiment with extra maps, multiple log variants, or anomaly signs until the minimal test is complete.
