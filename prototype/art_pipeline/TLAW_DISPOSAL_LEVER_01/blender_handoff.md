# TLAW_DISPOSAL_LEVER_01 — Blender Handoff

Status: **WAITING_FOR_APPROVED_READY**

Asset ID: `TLAW_DISPOSAL_LEVER_01`
Approved visual revision: `R01`

## Gate

Do not begin geometry production while `provenance.md` is `APPROVED_VISUALS_PENDING_BINARY_UPLOAD`.

Claude may perform the Factory v0.1 binary-ingestion/sync step from `prototype/art_pipeline/_incoming/`, verify the canonical reference file, commit it, and update package status to `APPROVED_READY`. Geometry production begins only after that verification succeeds.

## Goal

Build one stationary heavy disposal-lever control in Blender with one separate rigid moving lever and an exact, testable hinge pivot.

This experiment specifically tests the Factory path for:

- stationary mechanical equipment;
- separate gameplay-addressable moving geometry;
- exact pivot placement;
- deterministic angular travel;
- preservation of dimensions and pivot behavior through DCC → engine import.

## Authoritative inputs

After package status becomes `APPROVED_READY`, consume only the verified files in this GitHub package:

- `asset_card.md`
- `provenance.md`
- `TLAW_DISPOSAL_LEVER_01_REFERENCE_R01.png`

`MAP` is explicitly `N/A`; do not invent or request a source texture map for this experiment.

Existing disposal-lever concepts are art-direction context only. Do not trace or salvage their geometry unless separately authorized.

## Visual target

Follow TLAW chunky tactile industrial style:

- compact, heavy, tactile stationary control;
- dark painted steel base;
- one dominant muted-red lever;
- large readable hinge housing;
- restrained wear on genuine contact areas;
- matte rubber grip if used;
- simplified believable machinery;
- 70% primary shape / 20% functional detail / 10% accents.

Avoid photorealism, sci-fi, cyberpunk, steampunk, post-apocalypse, chrome, glossy toy plastic, excessive rust, random pipes, decorative gauges, redundant buttons and greebles.

## Hard mechanical contract

Use the Asset Card's machine-readable block as authoritative:

```text
dimensions:
  width_m: 0.42
  depth_m: 0.36
  pivot_height_m: 0.68
  neutral_total_height_m: 1.20
  tolerance_m: 0.01

lever:
  pivot_to_grip_center_m: 0.56
  grip_length_m: 0.16

pivot:
  rotation_axis: local_X
  rest_angle_deg: -20
  actuated_angle_deg: 50
  travel_deg: 70
```

The finished imported asset must satisfy the declared dimensions/tolerance after all geometry and surface treatment are present.

## Minimum Blender hierarchy

```text
AssetRoot
└── VisualRoot
    ├── StationaryBody
    └── Lever_Handle
```

Additional stationary sub-objects are allowed only when they materially help construction or material separation. The moving lever assembly must remain one clearly addressable rigid branch.

## Pivot requirements

- `Lever_Handle` must have its functional origin/pivot at the real geometric center of the hinge axis.
- Rotation axis is local X.
- Rest state: `-20°`.
- Actuated state: `50°`.
- Total travel: `70°`.
- Do not fake motion by moving the whole asset or by using an offset parent whose actual pivot is elsewhere.
- Rest and actuated states must be reproducible numerically, not eyeballed.
- Pivot semantics must survive export/import into the consuming engine.

## Materials

Create only the material families actually needed:

1. dark painted steel;
2. muted red painted steel;
3. worn/bare steel at hinge/contact surfaces;
4. matte rubber, only if used for the grip.

Use standard/shared/procedural TLAW material construction. No approved source map exists for this asset.

## Engine import sanity gate

Before owner visual review, verify in the consuming engine:

- measured size matches the Asset Card within tolerance;
- `Lever_Handle` remains separately addressable;
- the imported pivot is at the intended hinge center;
- numeric `-20°` and `50°` states produce the expected physical positions;
- the lever rotates around local X and does not orbit an offset point;
- materials landed on the intended parts;
- any texture-bearing mesh has UVs;
- tangents exist if a tangent-space map is introduced as an implementation artifact;
- no greybox/placeholder renders alongside the replacement asset.

Preview renders used for review must represent the same prepared materials/maps used by the engine.

## Explicit exclusions

Do not add:

- extra levers;
- gauges or meters;
- extra buttons;
- decorative pipes;
- anomaly states;
- gameplay scripts or Unity interaction logic;
- Hyper3D geometry;
- unapproved source maps;
- permanent readable signage text baked into geometry unless separately requested.

## Experimental deliverable

Produce one Blender asset candidate and engine-imported preview evidence for owner review.

Report at minimum:

- Blender dimensions and engine-imported dimensions;
- hierarchy;
- material list;
- exact pivot location/orientation;
- lever rest/actuated angles and measured travel;
- confirmation that pivot behavior survived import;
- important modeling decisions;
- any deviation from the Asset Card or approved reference.

Provide clear preview views of the stationary silhouette, hinge/pivot, rest state and actuated state.

Do not label the asset production-ready or visually accepted before owner review.
