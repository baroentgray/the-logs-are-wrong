# Gate 2 validated PortableAuthority configuration → Unity handoff proof

Schema: `tlaw.gate2-validated-config-unity-handoff-proof/v1`.

This is TLAW-069's bounded architecture/transport proof. It is not production
configuration ingestion, a Unity host driver, a change to PortableAuthority,
or an architecture selection. The exact resulting candidate SHA is bound by
the first proof commit, Draft PR, and exact-head CI evidence; this document
cannot name its own commit object ID.

## Identity and Phase 0 fail-closed result

| Field | Evidence |
| --- | --- |
| GitHub contract | Issue #160; closed implementation handoff comment `5369075075` |
| Authorized baseline / `origin/main` | `55da4b91572da292f5004e5728284783852282a4` |
| Branch | `task/TLAW-069-validated-config-unity-handoff-proof` |
| Worktree | `C:\Projects\TheLogsAreWrong-worktrees\TLAW-069` |
| Proof-start HEAD / remote task branch | `55da4b91572da292f5004e5728284783852282a4` |
| Proof-start worktree | clean |
| Execution profile | `CODEX_STANDARD`, medium reasoning; quota unknown is treated as YELLOW per `docs/MODEL_ROUTING.md` |

Phase 0 was run before any tracked edit. `git fetch origin --prune` completed,
then `origin/main`, the remote task branch, and worktree `HEAD` were all
required to equal the authorized baseline. Any mismatch would have stopped the
proof; none occurred.

| Phase-0 contract | Exact evidence | Result |
| --- | --- | --- |
| D-021 U4 remains unchanged | `docs/agent/DECISIONS.md` blob `46218faae6aa015c8d5ae63c65ec78ab73426005`; no diff against baseline | PASS |
| PortableAuthority owns validated graph | `src/TheLogsAreWrong.PortableAuthority/Configuration/ValidatedConfiguration.cs`, blob `1bee9ad8f3763e2e0be895c4abdb169ae932b897` | PASS |
| Canonical adapter remains external | `src/TheLogsAreWrong.Config.Yaml/YamlConfigurationLoader.cs`, blob `23651feb72bfa432685f8ef1850648d355baed57`; success returns `new ValidatedConfiguration(shift!, anomalies!)` only after document and cross-document validation | PASS |
| Real input source | `data/shift_p0.yaml` blob `761a37912c7876eabcf5a647ae7d113afd86674d`, SHA-256 `CD08DDFC6F354A1FDDEC7EE751007C95920CDBD26AFA6350A068C350D88277E7`; `data/anomalies.prototype.yaml` blob `4afc3101986c9a8cb8fd4221a3d05ae22e8725bb`, SHA-256 `6517C145AD41410131FF50BF691FE9C37FB33E1CB8E065E42ADB97364F4785D7` | PASS |
| Project direction | Config.Yaml project blob `2bcaef5a301cd3a9d7cdae70edec3c4193482540` references Domain; Domain project blob `08b07b13c0dab88c6e6763eb05754c55ee854ac1` references PortableAuthority; PortableAuthority project blob `88d68682ecac483457c7a7fcd527f8a5715c2a52` has neither reverse reference | PASS |
| Portable target/package closure | `netstandard2.1`; exactly direct `System.Collections.Immutable 8.0.0` | PASS |
| Direct host consumption | `HostSessionContracts.cs`, blob `27e10b4aa9ed22705de56e2c19813e88cedeaeec`: public constructor `(ShiftConfiguration, AnomalyCatalog, ProfileId)` | PASS |
| Pinned Unity | `6000.3.21f1 (c02631ffc030)` from `ProjectVersion.txt`, blob `bed7306bacdbabb221b9f7c55acbc410fb0e7644` | PASS |
| Unity plugin boundary | exactly PortableAuthority, `System.Collections.Immutable`, and `System.Runtime.CompilerServices.Unsafe`; PortableAuthority SHA-256 `067F7C6B2D499F37828E7AF5AB32F64A3638CC63BD211588D573320AED4BE5DA` | PASS |
| Existing Unity configuration path | Gate-2 inventory contained only synthetic test probe configurations; no YAML/Config.Yaml/decoder/generated-real-config/ScriptableObject production path | PASS |
| Gate-2 networking exclusion | package manifest blob `e94299d0c5615ce98f00b1d2c5f5dc52900fc001` and source inventory contain no FishNet, FishySteamworks, Steamworks, Netcode, Mirror, or Unity Transport dependency | PASS |

The complete validated graph remains the 23 PortableAuthority records in
`ValidatedConfiguration.cs`: the root, shift/profile/objective/quota/supply/
manifest/scheduler/noise/resources/containment records and the anomaly/
confirm/processing/outcome/effect/procedure/wrong-action records. Namespace
text remains `TheLogsAreWrong.Domain.*` by the accepted extraction strategy;
the source assembly owns every listed type.

## Preserved U4 contract and current dependency path

D-021 U4 is preserved exactly: canonical YAML and its validation remain outside
the three-plugin Unity runtime boundary; Unity consumes already validated
PortableAuthority-owned data; Config.Yaml, YamlDotNet, Domain.dll, a fourth
plugin, and a second Unity configuration-semantic path are not allowed.

```text
data/*.yaml
  -> Config.Yaml / YamlConfigurationLoader (YamlDotNet + frozen validation)
  -> ValidatedConfiguration { ShiftConfiguration, AnomalyCatalog }
  -> [candidate transport boundary]
  -> existing three-plugin Unity test/runtime boundary
  -> new HostSession(shift, anomalies, selectedProfile)
```

The proof's source binding is the exact pair of YAML SHA-256 values above plus
the exact canonical-loader source blob ID
`23651feb72bfa432685f8ef1850648d355baed57`. It is a source/result identity,
not a second schema or validator.

## Deterministic full-graph projection and proof harness

The proof harness defines an explicitly ordered binary projection of every
property in the current `ValidatedConfiguration` object graph. Dictionaries and
sets are ordered ordinally by their portable identifier/string representation;
arrays retain configured order; enum values and optional values have explicit
tags. It contains no YAML parsing, defaults, coercion, document rules,
cross-document rules, anomaly rules, or shift validation.

| Projection / material | SHA-256 | Evidence |
| --- | --- | --- |
| Canonical complete configuration projection | `4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1` | real `YamlConfigurationLoader` → full graph → projection |
| C1 deterministic v1 artifact | `94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541` (2,326 bytes) | real YAML/loader input, repeated output equality |
| C2 deterministic generated source | `07DB882E9AB082D515B5D906E233C604E773CAC540F7DCB0B241A68393AD9A90` | exact source equality to the test-only emitter |

The tracked C1 codec reference implementations live only in the bounded .NET
and Unity Editor proof harnesses. They are duplicate-free with respect to
configuration semantics: both only read/write typed values and construct the
already-owned PortableAuthority records. A production C1 task, if selected,
must own one implementation in PortableAuthority rather than promoting either
test reference copy. The C2 factory is generated test material and is verified
against the real loader result; it is not a runtime generator or loader.

## Candidate matrix

| Candidate | Executable proof | U4 / dependency result | Failure behavior | Status |
| --- | --- | --- | --- | --- |
| C1 — deterministic versioned data artifact + PortableAuthority materializer | Real YAML is loaded and validated by the existing loader; versioned binary artifact repeatability, full-graph materialization, direct HostSession construction, net10/Unity projection parity, and negative cases are all executed. | The reference uses only PortableAuthority types plus BCL/Immutable. Unity test assembly references no Domain, Config.Yaml, or YamlDotNet; no plugin/project/package change. A later owner-selected implementation can place one equivalent decoder in PortableAuthority without a new package or plugin. | Magic, version, source binding, exact length/trailing-data, payload SHA-256, malformed reader state, and re-projected payload agreement all fail closed. Corrupt, truncated, wrong-version, and stale-binding tests pass. | Viable; owner selection required. |
| C2 — deterministic generated C# construction source | Real loader result emits the committed factory exactly; source carries source binding and canonical projection SHA. Pinned Unity compiles it, materializes the full graph, reproduces the same SHA, and directly constructs HostSession. | Pure construction of PortableAuthority records; no YAML API, parser, Domain DLL, fourth plugin, package, scene, prefab, or ProjectSettings change. | Regeneration/staleness is fail-closed because the .NET proof compares the complete generated source byte-for-byte. The generated source carries the input/validator binding and projection SHA. | Viable; owner selection required. |
| C3 — Unity-native serialized asset / ScriptableObject | Evaluated, not implemented. An opaque byte payload inside an asset is only C1 with an asset container, not a materially distinct C3. A native field-by-field asset requires a second Unity schema and mapping/materialization surface. | A distinct native representation would duplicate the portable configuration shape and requires Unity-specific field mapping/default/defaulting/validation policy, contrary to D-021 U4. | No compliant distinct fail-closed representation exists without reducing to C1. | Rejected: non-compliant if distinct; otherwise not a separate candidate. |
| C4 — runtime YAML / Config.Yaml / YamlDotNet / Domain import | Rejected without implementation. | Directly violates D-021 U4 and the exact three-plugin inventory; Config.Yaml depends on Domain and YamlDotNet. | N/A: forbidden boundary, not a permitted fallback. | Rejected. |

Neither C1 nor C2 dominates on every material dimension. C1's deployed data
artifact is compact and auditable but requires a future PortableAuthority codec;
C2 needs no decoder but stores a larger generated construction source and has a
build-generation/staleness workflow. They are materially distinct viable
production directions. Executor preference cannot select between them.

## Regression and Unity evidence

| Check | Result |
| --- | --- |
| Full solution Release build | PASS — 0 warnings, 0 errors |
| Full .NET Release suite | PASS — 1654 passed, 0 failed, 0 skipped (TLAW-068 accepted baseline 1650 plus 4 TLAW-069 proof contracts) |
| D-014 `Scope=TLAW-046` | PASS — 87/87 |
| TLAW-067 HostSession/EventId slice | PASS — 6/6 |
| TLAW-068 cadence slice | PASS — 10/10 |
| TLAW-069 .NET proof contracts | PASS — 4/4 |
| C1 real YAML source binding/repeatability/full graph/direct HostSession | PASS |
| C1 corruption/truncation/wrong-version/stale-binding rejection | PASS |
| C2 exact deterministic source emission | PASS |
| Pinned Unity EditMode | PASS — 25/25, 0 failed, 0 skipped; includes 4 TLAW-069 contracts |
| Unity test results | `C:\Temp\TLAW-069\evidence\editmode-attempt-3.xml`, SHA-256 `EF9171C2F946CCBD3DCB454C094222E7819B4E48A1D22903CBAC5CBBED093E0B` |
| Unity batch log | `C:\Temp\TLAW-069\evidence\editmode-attempt-3.log`, SHA-256 `2A779839CE87F17FFA395DD3EAC811A5915ACB4F9D5BEDC37C23807BDDD1B732` |
| Fresh deterministic portable deployment output | PASS — SHA-256 `067F7C6B2D499F37828E7AF5AB32F64A3638CC63BD211588D573320AED4BE5DA` equals committed plugin |
| Existing canonical host-tick evidence exercised by Unity | one-tick `287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949`; four-tick `C7FEC7BD00DE7D5A92DA0A89A09F61D4B7E4DC905A4F7D35687A8E6460029411`; cadence `A3CFED2906266153792A1B9FFFB2CBE6EE48F450342EF933B9DAD515DD0BADA0` — PASS |

The remaining full repository Release build/test, Tlaw.Verify/Gate 0/
architecture/object-reader checks, and exact-head CI/artifact evidence are
recorded against the first proof commit and its exact candidate head.

## Smallest next ownership boundary and unresolved owner decision

If the owner selects C1, the smallest future production change is one
PortableAuthority-owned versioned transport codec plus an outside-Unity
generator/deployment step and a fixed source-binding manifest. If the owner
selects C2, it is one outside-Unity generator plus a generated
PortableAuthority-record factory compiled into the Unity-side assembly, with
the source-binding check required in CI. Both must retain the exact current
validated object model and direct HostSession constructor; neither authorizes
new validation semantics.

Open owner question: select C1 or C2, including the source-binding deployment
policy. The proof does not answer this by executor preference.

## Work not performed

Not performed: production U4 ingestion/loader/decoder/generator; any source
change under PortableAuthority or Config.Yaml; YAML schema/default/defaulting or
validation change; HostSession/cadence/EventId change; U3 owner/driver;
MonoBehaviour scheduler; gameplay; networking; Gate 3; Domain.dll, Config.Yaml,
or YamlDotNet Unity import; fourth plugin; Packages/ProjectSettings/scenes/
prefabs; decision-log edit; Ready; merge; cleanup; or protected-path changes.

## Terminal

```text
VALIDATED_CONFIG_UNITY_HANDOFF_PROOF_AWAITING_OWNER_SELECTION=C1,C2
YAML_REMAINS_OUTSIDE_UNITY_RUNTIME
PORTABLE_CONFIG_SINGLE_SEMANTICS_PRESERVED
PRODUCTION_CONFIG_INGESTION_NOT_IMPLEMENTED
UNITY_PRODUCTION_DRIVER_NOT_IMPLEMENTED
NETWORKING_NOT_STARTED
```
