# TLAW_DISPOSAL_LEVER_01 — Provenance

Asset ID: `TLAW_DISPOSAL_LEVER_01`

Package status: **APPROVED_READY**

Asset acceptance: **PROTOTYPE_ACCEPTED**

Revision: `R01`

Purpose:
- Second Asset Factory experiment under workflow v0.1.
- Test a stationary mechanical asset with one separate moving part and an exact pivot that survives Blender → engine handoff.

Approved visual sources:
- `MAP`: **N/A** — no separate material/source map is required for this experiment.
- `TLAW_DISPOSAL_LEVER_01_REFERENCE_R01.png` — owner approved in workshop; binary verified and stored in this GitHub package.
- `TLAW_DISPOSAL_LEVER_01_REFERENCE_R01_SIDE.png` — elevation along the hinge axis.
- `TLAW_DISPOSAL_LEVER_01_REFERENCE_R01_ISO_HINGE.png` — three-quarter from the hinge side.
- `TLAW_DISPOSAL_LEVER_01_REFERENCE_R01_ISO_SIGN.png` — three-quarter showing the sign face.

Existing art direction:
- Previous disposal-lever concept establishes the visual family and intent only.
- It is not an authoritative geometry source and must not override the Asset Card's hard dimensions or pivot contract.

Intended consumer:
- Claude / Blender

Usage:
- Approved reference is the art target for overall silhouette, heavy stationary character, red moving handle, material grouping, visually legible hinge/pivot, and the two lever states.
- Reference is not geometry that must be copied literally.
- Text/signage visible in the concept/reference is not required to be baked into the mesh unless separately approved.

Source:
- Workshop image generation, informed by existing TLAW disposal-lever art direction.

Notes:
- Owner approved `TLAW_DISPOSAL_LEVER_01 · REFERENCE · R01`.
- No map was generated because the Asset Card records `MAP: N/A`.
- Claude production gate is open for revision `R01`.
- Ingested from `_incoming/` on 2026-08-18. Identity confirmed from the embedded marker `TLAW_DISPOSAL_LEVER_01 · REFERENCE · R01`, not from the filename; the stored file is byte-identical to the incoming copy (md5 56678e81435e5e424b76f1c21d45ab1f, 1536x1024 PNG RGB).
- The reference sheet's technical block (rotation axis local X, rest -20 deg, actuated 50 deg, travel 70 deg) agrees with the Asset Card's machine-readable block.
- Package verification passed: expected binary present, Asset ID matches the folder, TYPE and REVISION match the approved revision, and no unapproved binary was promoted from `_incoming/`.
- Three further views ingested from `_incoming/` on 2026-08-19, one view per file. All three carry the marker `TLAW_DISPOSAL_LEVER_01 · REFERENCE · R01`, so asset, type and revision are unambiguous; the `_SIDE` / `_ISO_HINGE` / `_ISO_SIGN` suffixes are Claude's classification of the view, not part of the marker.
- **Open conflict: the reference proportions and the card's dimensions describe different objects.** Measured off the side elevation, whose horizontal extent is the declared 0.36 m depth: the mounting plate is 757 px, the stationary body 557 px tall, and the hinge centre sits about 455 px above the ground — that is a pivot height near 0.22 m. The card declares 0.68 m. The art is roughly three times squatter than the numbers. Reading the plate as the 0.42 m width instead gives 0.25 m, which does not close the gap.
- Resolved: the owner ruled that the art governs for this asset, and that this is a ruling for the case rather than a rule — the standing rule is to ask. The asset takes its shape from the reference and its scale from the card's `neutral_total_height` of 1.20 m, which the art's own size could not reach.
- Built and accepted at: footprint 0.94 x 0.81 m, pivot height 0.484 m, actuated height 1.200 m, travel 70 degrees about local X with zero error at both states.
- `PROTOTYPE_ACCEPTED` records owner acceptance of the built asset on form, mechanism, colour and detail, given 2026-08-19. It implies no promotion and no production integration; the lever is a review exhibit and is not wired to the disposal route.
- The Asset Card's `pivot_height_m`, `neutral_total_height_m` and `pivot_to_grip_center_m` no longer describe the accepted asset. Correcting the card is the workshop's call.
- Case report: `factory_case_report.md`.

- Do not add extra controls, gauges, pipes, maps, Hyper3D geometry, gameplay code, or unapproved mechanical states during this experiment.
