# TLAW_DISPOSAL_LEVER_01 — Provenance

Asset ID: `TLAW_DISPOSAL_LEVER_01`

Package status: **APPROVED_READY**

Revision: `R01`

Purpose:
- Second Asset Factory experiment under workflow v0.1.
- Test a stationary mechanical asset with one separate moving part and an exact pivot that survives Blender → engine handoff.

Approved visual sources:
- `MAP`: **N/A** — no separate material/source map is required for this experiment.
- `TLAW_DISPOSAL_LEVER_01_REFERENCE_R01.png` — owner approved in workshop; binary verified and stored in this GitHub package.

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
- Do not add extra controls, gauges, pipes, maps, Hyper3D geometry, gameplay code, or unapproved mechanical states during this experiment.
