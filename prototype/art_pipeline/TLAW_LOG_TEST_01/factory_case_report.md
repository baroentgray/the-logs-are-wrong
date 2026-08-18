# TLAW_LOG_TEST_01 — Factory v0 Case Report

Scope: postmortem of the first asset to run the whole Factory v0 loop.

Sources used: `TLAW_ASSET_FACTORY_V0_WORKFLOW.md` at `d87ffbe1` (current main,
i.e. **after** the binary-handoff stage was added), the package files in this
folder, and my own record of the work.

Reconstruction note: the prototype working copy
(`TheLogsAreWrong-prototypes/opus6-unity`) is **not** a Git repository, so there
is no commit history for the build side. The sequence below is reconstructed
from the session, the build logs in `_logs/`, and the files themselves. Where I
cannot confirm something I say so.

---

## 1. Result

**What was built.** One parameterised normal production log, in Blender, with no
Hyper3D:

| | |
|---|---|
| Finished size | 2.400 m × 0.380 m — matches the card exactly |
| Proportion | length/width 6.3, out-of-round 6.8% |
| Triangles | 3 248 |
| Hierarchy | `AssetRoot → VisualRoot → { Body_Bark, End_A, End_B }` |
| Materials | 2 — `TLAW_LOG_TEST_01_Bark`, `TLAW_LOG_TEST_01_CutWood` |
| Source | package revision `R01`, consumed at commit `35e8216` |

Also produced: a cropped albedo and a derived normal map in the prototype's
texture folder, four Blender preview renders, and a prototype hall in the Unity
scene to view exhibits in even light.

**Production route actually used.** The default route in §1 —
workshop → maps/reference → Claude + Blender → prototype review. Hyper3D was not
used and was not needed.

**Visual bar.** Reached. After the last fix the owner said the result was
correct and, in a later message, `принято`. That is **owner acceptance of the
visual result**. It is *not*:

- a promotion decision (no §11 status such as `TRANSFER_READY` was assigned);
- approval to convert the production line — the owner explicitly ruled the
  opposite, that prototype dimensions stay as they are and assets adapt later.

**What the experiment proved.** That Claude + Blender can build a convincing
parameterised TLAW production log from an approved package without Hyper3D, to
the card's exact dimensions, with a structure the contract asks for.

**What it did not prove.** Nothing about: assets with moving parts, pivots or
sockets; anomaly signs as child meshes; characters; or promotion into
production. It also did not test the new BINARY INBOX / CLAUDE SYNC stages —
those were added to the workflow after this asset was already in progress.

---

## 2. Actual workflow

What actually happened, in order. It is not the ideal loop.

1. **Pre-gate work (out of contract).** The bark map and reference first arrived
   as chat images. I used them immediately, from the browser download folder, and
   built a log. This was before any package existed in GitHub. Everything
   produced in this phase had no revision and no provenance.
2. **A proportion problem surfaced by arithmetic, not by looking.** The card
   available at that time gave 3.0–4.0 m × 45–60 cm — a length/width near 6.7 —
   against the shop's 1.24 m × 0.66 m, which is 1.9. Three rounds had been spent
   on bark while the silhouette was the actual fault.
3. **Package published.** The owner created
   `prototype/art_pipeline/TLAW_LOG_TEST_01/` with card, provenance, handoff and
   both approved PNGs, marked `APPROVED_READY_FOR_CLAUDE`, revision `R01`.
4. **Gate opened, sources re-pulled.** I discarded the chat copies and pulled
   from GitHub, pinning the commit (`35e8216`) into the prototype's `_art/`
   folder. The approved bark map is **1254 × 1254**; the chat copy had been
   853 × 399. Different files.
5. **Map preparation.** Cropped the Asset ID banner off the map, derived a normal
   from luminance, wrote both, and verified them by reading the written files
   back.
6. **Geometry.** Rewrote the builder against the handoff: three objects, two
   materials, cylindrical UV with one seam, procedural end grain, dimensions
   normalised to the card after relief was applied.
7. **Engine integration and a long failure.** The asset went into the Unity
   scene and looked wrong. Roughly a dozen builds followed, testing one
   hypothesis at a time.
8. **Root cause, eventually.** Two independent faults, neither of them in the
   asset:
   - the FBX put the centimetre unit conversion on the new `AssetRoot` empty as a
     scale of `0.01`, and the engine compensates meshes but not that parent, so
     the asset arrived at one hundredth of its size;
   - the engine-side attach step hid the greybox stand-in only for assets whose
     name contained `trunk`, which `TLAW_LOG_TEST_01` does not, so the greybox
     stayed visible.

   Together these rendered a smooth grey-boxed cylinder with a plain procedural
   material, with the real asset invisible inside it. Every map fix along the way
   was correct and none of them could possibly show.
9. **Review environment.** A lit hall was added to the prototype so exhibits are
   judged in even light rather than in a deliberately gloomy shop.
10. **Acceptance.** Owner viewed the corrected exhibit and accepted the visual.

Rounds through the owner: roughly a dozen. Rounds that were actually about the
asset: three or four. The rest were spent on the two faults in step 8 and on my
own faulty diagnostics.

---

## 3. What worked well

Confirmed, not assumed.

**The approved-package gate (§7) paid for itself on first use.** The approved
bark map and the chat copy were genuinely different files — 1254 × 1254 against
853 × 399. Without the gate the asset would have been built from the wrong
source and nobody would have known.

**Asset ID and revision in filenames.** `TLAW_LOG_TEST_01_BARK_R01_albedo.png`
carries its own identity. Combined with pinning the source commit into the
prototype, the build states exactly what it consumed.

**The card's hard numbers.** Everything that landed first time landed because a
number was written down. The dimension line let me add a build-time compliance
check that immediately caught the asset at 2.42 × 0.41 instead of 2.40 × 0.38 —
bark, knots and bow all add to the bounding box after the nominal radius is
chosen.

**Reference as art target rather than geometry to copy.** Explicitly stated in
provenance, and correct. The reference informed bark scale, cut ends and
proportion; nothing was traced.

**Procedural construction.** Length and diameter are arguments and everything
else derives from them. Nothing here needed generation.

**Separating Bark from End_A / End_B as objects.** Cheap at build time and
already useful: the ends take a different material and will take ring, moisture
and inclusion states later without touching the body.

**The division of labour between mesh and map.** Geometry carries silhouette and
large form; the photographed map carries the close-range plates. Established by
failure — when the geometry also drew plates, it printed a second set of them
underneath the photographed ones, in different places.

**Preview + owner review as the only acceptance test.** §13's rule that automated
inspection must not claim an asset looks right held up exactly. Every mechanical
check passed while the thing on screen was wrong.

---

## 4. Friction / weak points

### 4.1 Root-empty scale is lost in the DCC → engine handoff — `HIGH`

**Problem.** §8 requires `AssetRoot → VisualRoot`. This was the first asset built
that way. The FBX exporter parks the centimetre unit conversion on the outermost
object as a scale of `0.01`; the engine importer compensates mesh nodes but not
that empty, so the asset arrived at 1/100 scale.

**Impact.** The largest single cost in this case. It is invisible in the DCC —
every Blender-side measurement was correct — and in the engine it does not look
like a scale error, it looks like a material that will not take.

**Current workaround.** Export with the unit conversion placed in the file's own
units rather than on objects, then verify by re-importing the FBX and reading
the root scale.

**Recommended fix.** Factory v0 should require that an asset's size is confirmed
**after import in the consuming engine**, not only in the DCC, and that the
figure is recorded. A one-line assertion catches this class permanently.

### 4.2 Greybox replacement is matched by name fragment — `HIGH`

**Problem.** The prototype's attach step disabled the placeholder only when the
asset name contained `trunk`. `TLAW_LOG_TEST_01` does not.

**Impact.** Combined with 4.1 this produced a convincing third explanation — a
texture problem — that survived many rounds. Every correct fix appeared to fail.

**Current workaround.** The placeholder is now hidden whenever any model attaches
successfully.

**Recommended fix.** Prototype tooling, not the contract. But the contract could
usefully say that a greybox and its replacement must never be able to render at
the same time.

### 4.3 The Asset ID banner is part of the delivered map — `HIGH`

**Problem.** §3 says the marker sits outside the useful area, and it does — as
black bands with white text across the top and bottom of the map. Nothing in the
card, provenance or handoff says the consumer must remove it, and no usable
rectangle is given. I mapped the whole image onto the log, banner included.

**Impact.** Wrong colours and wrong tiling, plus a false measurement — see 4.4.

**Current workaround.** Detect the banner by row brightness and crop it. On this
map: 76 px above, 78 px below, leaving 1254 × 1100.

**Recommended fix.** State in §3 that visual deliverables carry the banner and
that consumers must crop before use; either publish the usable rectangle in the
card or require automatic detection.

### 4.4 A seam check that was passed by the wrong thing — `MEDIUM`

**Problem.** I measured how well the map tiles by comparing opposite edges. It
reported the vertical wrap as nearly seamless. It was — the top and bottom rows
matched beautifully because both were black banner. After cropping, the real
figure was more than twice as bad across the length.

**Impact.** A seam would have run across the middle of every trunk.

**Current workaround.** Measure the cropped bark only, and mirror the wrap along
the log's axis.

**Recommended fix.** No contract change. This belongs in the lesson recorded in
§6 below: verify the artifact, not a neighbour of it.

### 4.5 No contract for non-colour maps — `MEDIUM/HIGH`

**Problem.** The workshop delivers colour. Nothing says who produces normal or
roughness, what colour space they must be in, or whether they may be compressed.
I derived a normal from luminance; the engine then imported it as an ordinary
colour texture with sRGB, which turns a flat 0.5 into 0.214 and tilts every
vertex — and separately compressed it to a format whose quantisation step was a
quarter of the whole signal.

**Impact.** Two more rounds. Both faults look like "the texture is not working".

**Current workaround.** Derive the normal in the map-prep step, import it linear
and uncompressed, and check the achieved amplitude against a target.

**Recommended fix.** Factory v0 should name the owner of derived maps and state
the required colour space and compression policy for data maps.

### 4.6 Card dimensions conflict with the existing scene — `MEDIUM`

**Problem.** The card specifies 2.40 × 0.38. The prototype line runs 2.60 × 0.52.
Factory v0 does not say which wins.

**Impact.** I built to the card, which is right for the asset and leaves the asset
unusable in the line without conversion work.

**Current workaround.** Owner ruling: prototype dimensions are frozen; the asset
stands as an exhibit; a shop-sized variant is a second call to the generator, not
a second file.

**Recommended fix.** Write that ruling into the contract as a general rule.

### 4.7 DCC preview and engine used different maps — `MEDIUM`

**Problem.** My Blender previews loaded the raw approved source while the engine
loaded the prepared crop. I asked the owner to compare two pictures that differed
in more than the thing under test.

**Impact.** Invalidated the one comparison that would have isolated the problem
early. The owner spotted it.

**Current workaround.** The preview now loads the same prepared file the engine
uses.

**Recommended fix.** Require that review previews are rendered from the same
prepared maps the engine consumes.

### 4.8 Cut ends have no approved map and cannot be finished without one — `MEDIUM`

**Problem.** The handoff explicitly excludes a cut-end map and asks for
procedural end grain. Rings are geometry — a shallow ripple, two radial checks,
a slight dish and tilt — but the light and dark of growth rings is colour, and
one flat material cannot show it.

**Impact.** The ends are the weakest part of the asset, and they are the surface
the entire shift's diagnosis is read from.

**Current workaround.** Geometry only.

**Recommended fix.** Treat a fresh-cut end map as the highest-value next
deliverable. It is also the cheapest: a flat disc needs no unwrapping work.

### 4.9 Tooling that fails silently — `MEDIUM`

**Problem.** Two DCC-side operations failed without error: writing a large image
through the DCC's own image API produced a black file, and my diagnostic
reported success because it measured the in-memory array rather than the file.

**Impact.** A confirmed-looking failure, which is worse than an obvious one.

**Current workaround.** Write images with an explicit encoder and verify by
reading the written file back.

**Recommended fix.** Tooling rule, not contract: a check must read the artifact
it claims to check.

### 4.10 Preview harness accumulated scene furniture — `LOW`

**Problem.** Each preview call added a floor, camera, lamps and a scale staff,
and framed the shot on every mesh present — so from the second call it framed on
a 20 m floor. Two of four end-on previews came out blank white while the script
reported four files written.

**Current workaround.** Clear everything but the asset before each shot.

---

## 5. Manual work that repeated

| Operation | Times | Verdict |
|---|---|---|
| Pull approved package from GitHub, pin the source commit | 1 (every future asset) | `AUTOMATION CANDIDATE` |
| Crop the Asset ID banner from a delivered map | 1 (every future map) | `AUTOMATION CANDIDATE` |
| Derive a normal from an albedo and verify amplitude | 2 | `AUTOMATION CANDIDATE` |
| Set engine import settings: linear, uncompressed, wrap, no rescale | 3 | `AUTOMATION CANDIDATE` |
| Assert finished size against the card | 2 | `AUTOMATION CANDIDATE` |
| Assert size **after** engine import | 0 — missing, and its absence caused 4.1 | `AUTOMATION CANDIDATE` |
| Confirm UVs, tangents, material assignment reached the engine | 4 | `AUTOMATION CANDIDATE` |
| Render preview set from a clean scene | 3 | `AUTOMATION CANDIDATE` |
| Choose bark cell size, relief amount, smoothing threshold | many | `KEEP MANUAL` |
| Decide what geometry carries and what the map carries | 3 | `KEEP MANUAL` |
| Judge whether the result looks right | every round | `KEEP MANUAL` — contractual, §13 |
| Read the card and translate it into build parameters | 1 | `KEEP MANUAL` for now |

Per §12 these are recorded, not built.

---

## 6. Missing information / contract ambiguity

Places where I had to decide for myself:

- **Usable area of a delivered map.** Not stated. I detect and crop.
- **Who makes the normal map, and to what spec.** Not stated. I derive it.
- **Colour space and compression for data maps.** Not stated. Linear,
  uncompressed.
- **Tiling intent.** Nothing says how many times a map should wrap. I chose one
  wrap around the girth and two along the length, then mirrored the length
  because the measured wrap quality along it was poor.
- **Which wins when card and scene disagree on size.** Not stated; resolved by
  owner ruling during this case.
- **Origin convention.** §9 asks for a "deliberate functional origin" without
  saying what that is per type. I used the centre of the axis, on the grounds
  that a log is carried, clamped and rolled about it. Not recorded anywhere.
- **What a preview set must contain.** The handoff asks for silhouette, bark and
  both ends; it does not say the previews must use the prepared maps, which is
  exactly where I went wrong.

### Local practice that worked but is not in the contract

These are mine, they held up, and they are currently written only in prototype
code comments:

1. **Bark cell size is specified in metres of finished asset**, and facet and
   ring counts are derived from it. This is what makes the generator genuinely
   parametric: a longer log gets *more* cells, not bigger ones.
2. **Dimensions are normalised after relief.** Bark, knots and bow all add to the
   bounding box, so the finished object is scaled back to the card's figures.
   This is how "exact dimensions independently of the bark relief" is actually
   kept.
3. **Geometry carries silhouette, the map carries plates**, and the smoothing
   threshold is set from that decision rather than by eye. The rule depends on
   who is drawing the bark, and it has been got wrong in both directions.
4. **Verify the artifact, not a neighbour of it.** Every diagnostic in this case
   returned a true number about the wrong object at least once.

---

## 7. Factory v0 change proposals

Proposals only. I have not edited the workflow.

### MUST CHANGE

**M1 — Size must be confirmed after engine import.**
*Weak point:* 4.1. *Proposed rule:* the deliverable report must state the asset's
measured size **in the consuming engine**, not only in the DCC, and a mismatch
against the card is a defect. *Basis:* the asset was correct in Blender to three
decimals and arrived at 1/100 scale; no DCC-side check could have caught it.

**M2 — Delivered maps carry a banner and must be cropped.**
*Weak point:* 4.3. *Proposed rule:* §3 states that the `ASSET_ID · TYPE ·
REVISION` marker is part of the delivered image and outside the usable area; the
consumer must remove it before use; the card should give the usable rectangle, or
the consumer must detect it and report the crop. *Basis:* the banner was mapped
onto the log and also silently corrupted a tiling measurement.

**M3 — Name the owner and spec of non-colour maps.**
*Weak point:* 4.5. *Proposed rule:* state who produces normal/roughness maps; that
data maps are linear and uncompressed in the engine; and that derived maps are
recorded as derived, with their source revision. *Basis:* two separate rounds
lost to colour-space and compression handling of a map nobody owned.

### SHOULD CHANGE

**S1 — Rule for card-versus-scene dimension conflict.**
*Weak point:* 4.6. *Proposed rule:* the card governs the asset; the existing
prototype scene is not rebuilt to match it; a scene-sized variant is produced by
re-running the generator. *Basis:* the owner's ruling in this case.

**S2 — Review previews must use the prepared maps.**
*Weak point:* 4.7. *Proposed rule:* preview renders supplied for owner review must
be made from the same prepared textures the engine consumes, and must say so.
*Basis:* the one comparison that would have isolated the fault early was invalid
because the two sides used different images.

**S3 — Record the geometry/map division of labour.**
*Weak point:* §6 local practice items 1–3. *Proposed rule:* add a short paragraph
to §8 or §9: geometry carries silhouette and large form, maps carry surface;
detail size is specified in metres of finished asset; dimensions are normalised
after relief. *Basis:* all three were arrived at by failing the other way first.

**S4 — Origin convention per asset type.**
*Weak point:* 4.6/§9. *Proposed rule:* the card states the origin's functional
purpose. *Basis:* I chose one and it is recorded nowhere.

### OPTIONAL

**O1 — Machine-readable dimensions in the card.** A small fenced block with
length/diameter/tolerance would let compliance be checked automatically instead
of by me reading prose. The most expensive mistake of the whole case — three
rounds spent on bark while the proportion was wrong — was a number that sat in
the card in plain figures and was never compared with the built object.

**O2 — A status for "visually accepted, not promoted".** §11's vocabulary covers
promotion readiness but there is no term for what this asset is now.

### Already addressed — no proposal

The gap I actually hit at the start (working from chat images before a package
existed) has been closed since, by `d87ffbe1`, which adds BINARY INBOX, CLAUDE
SYNC, PACKAGE VERIFY and the `APPROVED_READY` gate. Those stages were not
exercised by this case.

---

## 8. Tooling candidates

Each tied to a bottleneck that actually occurred.

**T1 — Map preparation step.** *Bottleneck:* 4.3, 4.5, 4.9. Crop the banner,
derive the normal, verify the written files, report crop, wrap quality and
amplitude. Exists in the prototype; worth generalising. Highest confidence.

**T2 — Engine-side asset validator.** *Bottleneck:* 4.1, 4.2, and most of the
lost rounds. On import, assert: size against the card; UVs present; tangents
present; expected materials on expected parts; data maps linear and uncompressed;
no placeholder rendering alongside its replacement. This is where both HIGH
faults lived, and both are one-line assertions. Highest value.

*Caveat:* §12 warns against pre-emptive complex engine validation. This is not
that — it is six assertions on one asset, and each corresponds to a fault that
has already happened.

**T3 — Preview harness.** *Bottleneck:* 4.7, 4.10. Fixed shot list, clean scene
per shot, prepared maps only, and a check that each render is not blank.

**T4 — Card → parameters.** *Bottleneck:* O1. Only worth it once the card carries
machine-readable numbers.

**Not recommended now — Blender MCP or an alternative Blender backend.** No
bottleneck in this case was inside Blender. Of the faults above, one was in the
exporter's unit handling, one in engine attach logic, one in map preparation, one
in engine import settings, and the rest in my own diagnostics. A better hand in
the DCC would not have shortened this case.

**Not recommended now — a Factory Skill or plugin.** §12 says automate what has
actually repeated. Two assets is not a pattern yet. T1 and T2 are scripts; if the
next two assets use them unchanged, that is when a Skill has something to wrap.

---

## 9. Recommendation for Factory v0 test #2

Deliberately not another organic hero object. Each of these tests properties this
case did not touch. I am not starting any of them.

**A. A fresh-cut end map for `TLAW_LOG_TEST_01` (map-only, no new geometry).**
Tests the map pipeline end to end with the geometry already settled and accepted:
banner cropping, derived maps, colour space, a flat disc unwrap with no seam
problem. It is the cheapest deliverable on the list and it closes the weakest
part of an otherwise accepted asset (4.8). It also tests whether a *revision* of
an existing package works, which nothing has exercised.

**B. A hard-surface asset with one moving part** — a clamp, a lever or a hatch.
Tests everything §8 and §9 say about pivots, sockets, gameplay-addressable parts
and "do not weld a part that may later need to move", none of which this case
touched. It also tests the promised separation between visual structure and
gameplay logic, since the moving part has to be addressable without the gameplay
depending on the mesh hierarchy.

**C. An anomalous variant of the accepted log** — one sign, as a `Signs` child.
Tests the diagnostic-readability requirement that the card states and this case
deliberately excluded: that an anomalous object must not read as a different
class of object from ten metres. It also tests whether the base asset really is a
family base, which is the claim the whole log family rests on.

If only one is run, A is the best value: smallest, closes a known gap, and
exercises the revision path.

---

## Confidence and gaps

- Sizes, triangle counts, texture figures and the two root-cause faults are taken
  from build logs and file inspection; they are facts.
- Round counts are my recollection and are approximate.
- I cannot confirm anything about how the workshop produced the map or reference,
  or how long that took.
- The BINARY INBOX / CLAUDE SYNC / PACKAGE VERIFY stages were added after this
  asset began and are untested by this case.
