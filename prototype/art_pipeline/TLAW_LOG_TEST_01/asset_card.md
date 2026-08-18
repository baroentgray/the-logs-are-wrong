# TLAW_LOG_TEST_01 — Asset Card

Status: **CONFIRMED**

1. **Asset ID:** `TLAW_LOG_TEST_01`
2. **Name:** Normal Production Log — Claude Test
3. **Function:** Base normal production log. First Asset Factory v0 experiment to determine whether Claude can build a convincing parameterized TLAW production log in Blender without Hyper3D.
4. **Use location:** Sawmill production line; base object for the production/inspection flow. This asset experiment does not add gameplay interactions.
5. **Baseline size:** approximately 2.40 m length × 0.38 m diameter. Geometry should remain parameterizable in length and diameter.
6. **Primary silhouette:** Compact, heavy production log; broadly cylindrical but not mathematically perfect. Mild taper, restrained natural irregularity, slightly uneven cut ends. Large silhouette must remain readable without visual noise.
7. **Materials:** Rough bark and exposed cut wood on both ends. No additional material families in this experiment. Bark is matte and tactile, not wet or glossy.
8. **Functional color:** Muted natural wood palette. Medium-to-dark brown bark with restrained variation; cut ends clearly lighter. Avoid saturated orange/cartoon wood color.
9. **Interactive / movable parts:** None. `Body/Bark`, `End_A`, and `End_B` may remain separate visual objects/material regions for structure and materials, but are not separate gameplay mechanisms.
10. **Portability:** The log is conceptually a movable production object, but physics, colliders, and runtime behavior are outside this Asset Factory visual experiment.
11. **Generation strategy:** Claude + Blender procedural/manual build. Bark map is used as the approved visual/material source. Hyper3D is not used.
12. **Hyper3D suitability:** Technically possible, but intentionally not used because the purpose of the experiment is to test Claude/Blender without Hyper3D.
13. **Visual reference required:** Yes. One normal-log reference, created only after approval of the bark map.
14. **Blender requirements:** Metric dimensions; minimum hierarchy `AssetRoot -> VisualRoot`; visual structure should support `Body/Bark`, `End_A`, `End_B`; deliberate functional origin; normalized transforms where safe; parameterizable length/diameter; no `Signs` or `Internal` on the normal base log.
15. **Additional constraints / gameplay readability:** This is a normal log, not a diagnostic showcase. No anomaly signs, resin, scars, split bark, hollow areas, embedded objects, or extra variants. The silhouette should establish a common visual family for later anomalous versions rather than making anomalies a separate obvious object class. Follow TLAW chunky tactile industrial style and the 70/20/10 detail hierarchy.

## Factory v0 visual package

Approved visual deliverables:

- `TLAW_LOG_TEST_01 · BARK · R01` — **APPROVED**
- `TLAW_LOG_TEST_01 · REFERENCE · R01` — **APPROVED**

The visual package is not available to Claude until the approved image files are physically stored in this GitHub package and `provenance.md` marks the package ready.
