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

**MAP → REVIEW → REFERENCE → REVIEW → APPROVAL → GITHUB → CLAUDE**

Do not automatically skip a review step.

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

The package becomes usable by Claude only after explicit owner approval.

Before that point, generated materials and references are not authoritative production sources.

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

## 7. Claude gate

Claude begins production work from a visual package only after explicit confirmation that the package is approved and stored.

Claude should consume the approved source from GitHub rather than reconstructing it from chat history.

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
- visual consistency with TLAW.

### Claude

Primary Blender production worker for:

- procedural/manual modeling;
- materials and UV work;
- hierarchy;
- pivots and sockets;
- normalization;
- previews;
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
3. explicit approval;
4. save approved package in GitHub;
5. owner tells Claude the package is ready for use.

Do not expand this first experiment with extra maps, multiple log variants, or anomaly signs until the minimal test is complete.
