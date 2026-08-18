# TLAW_DISPOSAL_LEVER_01 — Asset Card

Status: **CONFIRMED**

Factory experiment: stationary mechanical asset + separate moving part + exact pivot.

1. **Asset ID:** `TLAW_DISPOSAL_LEVER_01`
2. **Name:** Heavy Disposal Lever — Irreversible Control
3. **Function:** Stationary manual control for irreversible disposal routing. Factory v0.1 test for a mechanical asset with one separate rigid moving part and an exact deterministic pivot. Gameplay logic is outside this asset experiment.
4. **Use location:** Disposal-route control area beside the production line. Exact production-scene placement is not part of this asset contract.
5. **Baseline size:** footprint approximately `0.42 m × 0.36 m`; pivot height `0.68 m`; neutral total height approximately `1.20 m`. Finished engine-import dimensions must remain within declared tolerance.
6. **Primary silhouette:** Heavy dark stationary base with one long highly readable red lever handle. A large hinge housing should visually explain the rotation center. Avoid clusters of small controls.
7. **Materials:** Dark painted steel body; worn/bare steel only on hinge/contact surfaces; muted red painted metal lever; matte rubber grip may be used. No exotic material family is required.
8. **Functional color:** Muted safety red is reserved for the moving lever. Body remains charcoal / very dark desaturated industrial paint.
9. **Interactive / movable parts:** `Lever_Handle` is mandatory as a separate rigid object/assembly. It rotates around one exact hinge center. Do not weld it into the stationary body.
10. **Portability:** Not portable. Floor-mounted stationary equipment. `AssetRoot` remains stationary; only the lever assembly moves.
11. **Generation strategy:** Claude + Blender procedural/manual modeling. Deterministic mechanical construction is preferred over generative geometry.
12. **Hyper3D suitability:** N/A for this experiment. Hyper3D is intentionally not used because exact dimensions, hierarchy and pivot control are the focus.
13. **Visual reference required:** Yes. Existing disposal-lever concept is art direction only, not a mandatory geometry source. The approved reference should establish silhouette, proportion, material grouping, hinge readability and lever states.
14. **Blender requirements:** Metric dimensions; predictable `AssetRoot -> VisualRoot` hierarchy; stationary body and moving lever remain separate; `Lever_Handle` origin/pivot is placed at the real geometric hinge center; transforms normalized where safe; pivot and travel must survive export/import; engine-space dimensions and motion are verified after import.
15. **Additional constraints / gameplay readability:** Lever must look physically operable before UI. Pivot must be visually legible. Base must read as stationary and floor-mounted. No decorative pipes, extra gauges, redundant buttons, sci-fi detail or random greebles. Irreversible semantics may be reinforced by signage/UI but readable text is not required in the mesh itself. Follow TLAW chunky tactile industrial style and 70/20/10 detail hierarchy.

## Machine-readable dimensions

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

## Visual package decision

- `MAP`: **N/A** — separate source map is objectively unnecessary for this experiment; painted steel, worn steel and matte rubber should be built from standard/shared/procedural TLAW materials.
- `TLAW_DISPOSAL_LEVER_01 · REFERENCE · R01` — **APPROVED** by owner in workshop.

The existing disposal-lever concept remains an art-direction reference only and must not be treated as mandatory source geometry.
