# TLAW_LOG_TEST_01 — Blender Handoff

Status: **READY_FOR_CLAUDE BUILD**

Asset ID: `TLAW_LOG_TEST_01`
Approved source revision: `R01`

## Goal

Build one convincing parameterized normal TLAW production log in Blender without Hyper3D.

This is the first Asset Factory v0 geometry experiment. The result is an experimental prototype asset and still requires owner review after build.

## Authoritative inputs

Use the files in this GitHub package:

- `asset_card.md`
- `provenance.md`
- `TLAW_LOG_TEST_01_BARK_R01.png`
- `TLAW_LOG_TEST_01_REFERENCE_R01.png`

Do not reconstruct the art target from chat history.

The bark PNG is the approved visual/material source. The reference PNG is the approved art target; it is not mandatory geometry to copy literally.

## Visual target

Follow TLAW chunky tactile industrial style:

- compact and heavy;
- tactile;
- stylized low-to-mid poly;
- large readable forms;
- restrained natural irregularity;
- matte rough bark;
- lighter exposed cut wood;
- no photorealism, glossy toy plastic, excessive micro-detail, or decorative horror.

Detail priority: approximately 70% primary form / 20% functional and structural detail / 10% small accents.

## Baseline dimensions

- Length: approximately `2.40 m`
- Diameter: approximately `0.38 m`

Length and diameter must remain deliberately adjustable rather than being accidental fixed dimensions.

The body should be broadly cylindrical, but not mathematically perfect. Use mild taper and restrained radial/longitudinal irregularity. Avoid a perfectly straight pipe-like silhouette and avoid exaggerated fantasy deformation.

## Minimum Blender structure

```text
AssetRoot
└── VisualRoot
    ├── Body_Bark
    ├── End_A
    └── End_B
```

Equivalent naming is acceptable if the same separation is preserved.

Do not add `Signs`, `Internal`, gameplay components, colliders, interactions, sockets, or anomaly-specific objects for this experiment.

## Geometry and transform requirements

- Use metric dimensions.
- Keep a deliberate functional origin suitable for a movable production log.
- Normalize transforms where safe.
- Keep both cut ends structurally separable from the bark/body if practical.
- The visual build should tolerate changes to length and diameter without needing a full manual rebuild.
- Do not make gameplay logic dependent on experimental mesh hierarchy.

## Materials

Create only the materials required for this normal log:

1. rough bark;
2. exposed cut wood.

Use `TLAW_LOG_TEST_01_BARK_R01.png` as the approved bark source. Preserve the bark's coarse scale and directional breakup without turning it into extreme displacement noise.

No separate cut-end texture map is approved for this experiment. Build the cut-end treatment in Blender from simple procedural/manual material and geometry cues guided by the approved reference. Do not generate or introduce an additional external map.

## Explicit exclusions

Do not add:

- anomaly signs;
- resin;
- scars;
- split bark as a diagnostic feature;
- hollow sections;
- embedded/internal objects;
- multiple log variants;
- extra bark maps;
- cut-end maps;
- Hyper3D geometry;
- Unity gameplay implementation.

## Experimental deliverable

Produce one Blender asset candidate and enough preview material for owner review.

At minimum report:

- resulting dimensions;
- hierarchy;
- material list;
- how length/diameter are adjusted;
- important modeling/material decisions;
- any limitations or places where the approved reference could not be matched efficiently.

Provide clear preview views showing the main silhouette, bark treatment, and both cut ends.

Do not label the result production-ready or visually accepted. Owner review is the next gate.
