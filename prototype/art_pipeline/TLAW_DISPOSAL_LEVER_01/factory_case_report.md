# TLAW_DISPOSAL_LEVER_01 — Factory Case Report

Second asset through the Factory. First one with a moving part, an exact pivot,
and more than one material family.

Status at the time of writing: **provisionally accepted by the owner** on form,
mechanism, colour and detail. Not promoted; no §11 classification assigned.

This report extends the one for `TLAW_LOG_TEST_01`. Where a fault there is
repeated here it is marked **(recurring)** — a fault that comes back is a
process signal, not bad luck.

---

## 1. What the asset is

| | |
|---|---|
| Footprint | 0.94 × 0.81 m |
| Pivot height | 0.484 m |
| Actuated height | 1.200 m — the card's `neutral_total_height` |
| Travel | −20° to +50° about local X, 70° |
| Hierarchy | `AssetRoot → VisualRoot → { StationaryBody, Lever_Handle }`, each an empty over one mesh per material |
| Materials | 4 — dark paint, muted red, worn steel, matte rubber |
| Triangles | ~2 100 |
| Source | package revision `R01`, four approved views |

Shape from the art, scale from the card: the two disagreed by a factor of three
on the main dimension, the owner ruled the art governs, and the card's own
total-height figure was then used to size a floor-standing control.

---

## 2. Problems encountered

### 2.1 Baking the space transform destroys a pivot; not baking it loses the axis conversion — `HIGH`

The exporter's `bake_space_transform` folds an object's rotation into its mesh.
For an asset whose contract *is* a rotation that must survive import, baking
silently deletes the thing under test. Switching it off keeps the pivot — and
loses the Z-up to Y-up conversion, so the asset arrived lying on its back.

Rotating the root in the DCC did not help: the exporter writes its own axis
metadata over it.

**Avoid:** for any asset with a functional transform, export unbaked and correct
the axis with **one rotation on the placed root in the engine**. Never with a
rotation baked into meshes, and never by rotating the source geometry.

### 2.2 Submesh order is not preserved through the handoff — `HIGH`

Materials were assigned in the engine by slot index, because the importer hands
back materials with **no names**. Measuring the imported submeshes showed the
order is not the exporter's: the lever's three arrive as red, dark, rubber while
the file lists dark, rubber, red. The arm wore the boot's colour and the boot
wore the arm's, which reads as one flat object rather than as a mix-up — so it
was reported as "no red on the lever" and cost several rounds.

Object names **do** survive.

**Avoid:** one material per mesh, and map by object name. Never by slot index.
This is how the log was always built, which is exactly why the log never had
this failure — a working pattern that was treated as a quirk of that asset
instead of as the rule.

### 2.3 Blender's own slot order is not stable either — `MEDIUM`

Replacing nine stacked boxes with one frustum changed which part was welded
first and silently permuted the lever's material slots. Nothing failed; the
asset would simply have worn its materials in the wrong places.

**Avoid:** covered by 2.2. Where a slot order must exist, pin it explicitly and
print it.

### 2.4 Numbers derived by the build go stale in the consumer — `HIGH`

Turning the grip from stacked tubes into one profile moved the uniform scale
factor from 2.280 to 2.241 — the target is the actuated height and the new dome
is slightly longer — so the pivot moved to 0.484 while the engine's import gate
still held the literal 0.493 and began failing a correct asset.

**Avoid:** the build writes what it produced (`*.contract.txt` beside the model:
pivot height, arm length, both angles) and the gate reads it. No derived number
is copied into a second file by hand.

### 2.5 A check that cannot fail — `HIGH`

The pivot gate reported `TRUE_PIVOT` because "the reach stayed constant". That
proves nothing: *any* point rigidly attached to a body keeps a constant distance
from its origin, whichever direction it lies in. The probe was also taking the
wrong local axis, so the numbers were meaningless as well as unfalsifiable.

**Avoid:** before trusting a check, ask **what would make this print WRONG**. If
there is no such state, it is decoration. The replacement compares the grip's
rise against what the card's angle and arm length imply, and can fail.

### 2.6 Measurements taken in stale units after a scale change — `MEDIUM`

After the uniform scale the report still probed the drawing-board arm length and
measured reach from the drawing-board pivot height, reporting two different
reaches for one rigid arm — physically impossible, and purely a units error.
Caught one per build instead of all at once.

**Avoid:** when a global transform is introduced, re-derive **every** dependent
constant in the same pass, and re-read the report for internally impossible
values.

### 2.7 Placement landed inside a loop — `MEDIUM`

The review stand call was inserted inside a two-lamp `foreach`, so the asset was
built twice in the same spot. The demo rotated one and the other stayed at rest,
which reads as one lever leaving a copy of itself behind.

The evidence was in the log from the first build — every `TLAW_LEVER_*` line
printed twice while `TLAW scene built at` printed once — and was dismissed as
log noise.

**Avoid:** count instances, not just placement. A duplicated diagnostic line is
a duplicated call until proven otherwise.

### 2.8 The wrong object reviewed — `MEDIUM` **(recurring)**

An old chat-era lever, built before the Factory existed, stood twelve metres
from the Factory build and next to the canister — so it was the first lever
anyone walked up to. Two rounds of verdicts applied to it. The same class as the
log's greybox rendering in front of its own replacement.

**Avoid:** the owner's rule, now implemented — a post with a green lamp beside
anything newly put out for review. Superseded exhibits are removed from the
hall, not left standing beside their replacements.

### 2.9 A greybox rendering alongside its replacement — `HIGH` **(recurring)**

Same fault as the log: the placeholder was hidden by matching the name fragment
`trunk`, which this asset does not contain.

**Avoid:** already fixed — the placeholder is disabled whenever any model
attaches, by reference rather than by name.

### 2.10 Many small reference panels support several readings — `MEDIUM`

The orthographic sheet supported three different silhouettes — tower, buttress,
truncated pyramid — and all three were built. Front and side elevations of those
three shapes are nearly identical; the dimensions printed on them made them look
authoritative for form, which they are not.

One large view per file, three angles, settled it in a single pass.

**Avoid:** read form from a volumetric view; use orthographic views and figures
only to verify it. Request one view per file. Recorded in full in §4.

### 2.11 Card and art disagreed by 3× — `HIGH`

The card's 0.68 pivot on a 0.42 × 0.36 foot is a waist-high pedestal; the art is
a block bolted to the floor at about 0.22. No reshaping reconciles them.

**Avoid:** ask. That is the standing rule the owner set, and it was the right
call here — the answer ("art governs, but scale it up") was not derivable from
either document.

### 2.12 Stacked primitives where a surface was needed — `MEDIUM`, twice

Nine boxes faking a taper produced a staircase under raking light. Stacked tubes
faking a grip produced bands at every radius step, though the section was round.

**Avoid:** if a form has a profile, revolve or extrude it. A surface of
revolution is *cheaper* than the stack it replaces — the grip came out at fewer
triangles with no joints at all.

### 2.13 Shop weathering applied to a control — `LOW`

The shader's default dust of 0.28 settles on upward-facing surfaces. On the
domed grip and the ribs it read as pale blotches on red paint, i.e. as a texture
fault rather than as dirt.

**Avoid:** weathering defaults are tuned for machinery that has stood in the
shop for years. An object the operator must read at a glance gets it turned
down deliberately.

### 2.14 Parts fighting for the same space — `LOW`

The bolt pads sat under the body's own corners. Arithmetic after the fact:
bolt at (0.166, 0.138), body corner at (0.168, 0.150) — overlapping. Now (0.178,
0.150) against (0.146, 0.126), a 40 mm clearance.

**Avoid:** where two features are placed from different formulas, print the
clearance between them rather than eyeballing the render.

### 2.15 Patch tooling failing silently — `HIGH`

Three separate incidents, all in the editing layer rather than the asset:

- a replacement searched with `\n` against a CRLF file, matched nothing, and was
  written without an assertion — the four lever materials were never created, so
  every part came back `<none>` and the whole asset rendered in fallback steel;
- escaped newlines collapsed passing through a heredoc, producing a source file
  with a string literal split across lines;
- a splice deleted an adjacent line and broke an unrelated lamp call.

**Avoid:** every patch asserts its anchor matched, reads and writes with explicit
newline handling, and is verified by rebuilding — not by assuming the write
succeeded. This is the same rule as for assets, applied to the tools that edit
them.

---

## 3. What worked

- **The pivot contract survived the handoff intact**, verified numerically at
  three angles with zero error, and needed no correction after the export
  settings were right.
- **Splitting by material into named meshes** removed a whole class of failure
  rather than patching an instance of it.
- **The contract sidecar** removed a second class: derived numbers now travel
  with the asset.
- **Owner ruling on card-versus-art** unblocked in one exchange what three
  rebuilds had not.
- **One view per file** was the single largest improvement in the whole case.
- **The green beacon** is small and removes a failure that had already happened
  twice.

---

## 4. Standing rules this case produced

1. One material per mesh; map by object name. Never by slot index.
2. Numbers a build derives are written by the build and read by the consumer.
3. A check must be able to fail — ask what would make it print WRONG.
4. After a scale or unit change, re-derive every dependent constant in one pass.
5. Read form from a volumetric view; orthographic views and figures verify it.
6. If a form has a profile, revolve or extrude it rather than stacking parts.
7. When the card and the art disagree, ask.
8. Mark anything newly put out for review; remove superseded exhibits.
9. Count instances, not just placements; a doubled log line means a doubled call.
10. Weathering defaults are for background machinery, not for controls.
11. Every patch asserts, handles newlines explicitly, and is verified by rebuild.

Rules 1, 2, 3 and 11 are candidates for Factory v0.2. The rest are prototype
practice and are recorded in the build scripts themselves.

---

## Confidence

Dimensions, angles, material assignments and clearances are taken from build and
import logs; they are facts. Round counts are recollection. The owner's
acceptance is provisional and covers appearance and mechanism only.
