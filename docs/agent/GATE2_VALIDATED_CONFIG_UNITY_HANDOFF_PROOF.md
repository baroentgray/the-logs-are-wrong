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
| GitHub contract | Issue #160; closed implementation handoff comment `5369075075`; bounded C1 correction authorization in PR #161 comment `5369604609` |
| Authorized baseline / `origin/main` | `55da4b91572da292f5004e5728284783852282a4` |
| Branch | `task/TLAW-069-validated-config-unity-handoff-proof` |
| Worktree | `C:\Projects\TheLogsAreWrong-worktrees\TLAW-069` |
| Proof-start HEAD / remote task branch | `55da4b91572da292f5004e5728284783852282a4` |
| Proof-start worktree | clean |
| C1 correction input head | `94ccb1a0cc039381485c7c45e8bea15fd238f8fd` |
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

The complete validated graph remains the 24 PortableAuthority records in
`ValidatedConfiguration.cs`: the root, shift/profile/objective/quota/supply/
manifest/scheduler/noise/resources/containment records and the anomaly/
confirm/processing/outcome/effect/procedure/wrong-action records. Namespace
text remains `TheLogsAreWrong.Domain.*` by the accepted extraction strategy;
the source assembly owns every listed type.

TLAW-069 C1/C2 payloads already carried `ShiftConfiguration.LineNoise`; only
their named inventory omitted `LineNoiseConfiguration`. TLAW-070 corrects that
named guard without changing the previously proven payload.

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

### Corrected C1 same-handoff binding

The immutable test resource
`unity/TheLogsAreWrong/Assets/Gate2/Tests/Editor/Tlaw069C1Artifact.base64`
is a canonical Base64 representation of the exact C1 v1 artifact, with no
configuration values derived from C2. Its decoded boundary bytes are **2,326
bytes** with SHA-256
`94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541`;
the committed Base64 resource text has SHA-256
`CCBF6F80C7C0C37BE1673DD26241BE5D2B6BDC4EA06C40D39372061D530D727A`.
Those bytes were generated outside Unity from the canonical YAML pair through
the real `YamlConfigurationLoader` and bind the two YAML identities plus the
loader/validator blob in the C1 header.

The .NET C1 contract reruns that real loader, regenerates C1, checks
byte-for-byte equality to the decoded committed resource, and independently
checks both the artifact SHA above and the canonical projection SHA below.
The Unity C1 contract reads those same decoded bytes directly from the resource;
its C1 path does not call `Tlaw069GeneratedValidatedConfiguration` or any C1
encoder. It independently checks the trusted artifact SHA and trusted projection
SHA, decodes/materializes the artifact using the fixed YAML/loader binding, and
passes the resulting `ShiftConfiguration` plus `AnomalyCatalog` straight to
`HostSession`.

The correction also proves why the internal payload self-hash is insufficient
on its own: a test flips the encoded seed and recomputes the internal payload
self-hash. The C1 decoder accepts that internally consistent altered payload,
but the independently trusted artifact/source-result identity rejects it before
acceptance. Corruption, truncation, wrong-version, and stale-binding negative
cases continue to run against this same boundary resource.

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
| C1 deterministic v1 artifact | `94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541` (2,326 bytes) | exact decoded bytes in `Tlaw069C1Artifact.base64`; real loader regeneration is byte-for-byte equal; Base64 resource SHA `CCBF6F80C7C0C37BE1673DD26241BE5D2B6BDC4EA06C40D39372061D530D727A` |
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
| C1 — deterministic versioned data artifact + PortableAuthority materializer | The exact committed 2,326-byte real-loader artifact crosses the Unity boundary. .NET regenerates it from YAML/loader and compares bytes; Unity reads those same bytes, independently checks trusted artifact/projection identities, materializes the full graph, and constructs HostSession directly. | The reference uses only PortableAuthority types plus BCL/Immutable. Unity test assembly references no Domain, Config.Yaml, or YamlDotNet; no plugin/project/package change. A later owner-selected implementation can place one equivalent decoder in PortableAuthority without a new package or plugin. | Magic, version, fixed YAML/loader binding, exact external artifact SHA, fixed canonical projection SHA, length/trailing-data, payload SHA-256, malformed reader state, and re-projected payload agreement fail closed. Corrupt, truncated, wrong-version, stale-binding, and payload-modified-with-recomputed-self-hash tests pass. | Viable; owner selection required. |
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
| PortableAuthority standalone Release deployment build | PASS — 0 warnings, 0 errors; documented non-persisted deployment flags reproduce the committed PortableAuthority plugin SHA `067F7C6B2D499F37828E7AF5AB32F64A3638CC63BD211588D573320AED4BE5DA` |
| Full .NET Release suite | PASS — 1655 passed, 0 failed, 0 skipped (the bounded C1 correction adds the explicit recomputed-self-hash rejection contract) |
| D-014 `Scope=TLAW-046` | PASS — 87/87 |
| TLAW-067 HostSession/EventId slice | PASS — 6/6 |
| TLAW-068 cadence slice | PASS — 10/10 |
| TLAW-069 corrected .NET proof contracts | PASS — 5/5 |
| C1 same-handoff real YAML/loader regeneration, trusted artifact/projection identity, full graph, direct HostSession | PASS |
| C1 corruption/truncation/wrong-version/stale-binding and recomputed-self-hash rejection | PASS |
| C2 exact deterministic source emission | PASS |
| Pinned Unity 6000.3.21f1 EditMode | PASS — 26/26, 0 failed, 0 skipped; includes 5 corrected TLAW-069 contracts |
| Unity test results | `C:\Temp\TLAW-069\evidence\correction-final-editmode.xml`, SHA-256 `8167A5ED4D25725FBB95E869FB875867CD5EDBA5CEA344A08D8709A63B7BA60A` |
| Unity batch log | `C:\Temp\TLAW-069\evidence\correction-final-editmode.log`, SHA-256 `221DC313CF4EC0EE53151216986541ECCF2B43AE62049005583BD62F5114D3B8` |
| Fresh deterministic portable deployment output | PASS — SHA-256 `067F7C6B2D499F37828E7AF5AB32F64A3638CC63BD211588D573320AED4BE5DA` equals committed plugin |
| Existing canonical host-tick evidence exercised by Unity | one-tick `287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949`; four-tick `C7FEC7BD00DE7D5A92DA0A89A09F61D4B7E4DC905A4F7D35687A8E6460029411`; cadence `A3CFED2906266153792A1B9FFFB2CBE6EE48F450342EF933B9DAD515DD0BADA0` — PASS |
| Windows x64 Development build | PASS — result `Succeeded`, 0 errors, 0 warnings, 146,413,247-byte output; log SHA-256 `D626248CFD5DDD6232B8A399D60CDCA5B86D3DAE6F66378366A2E7DA94EABE6B` |
| Windows player bootstrap smoke | PASS — exit 0, startup/60-frame clean-exit markers present; log SHA-256 `174CC66F005E7848A936B8073ABFB81A0DC191BDAD7BEC2814E0B051250E0F51` |

The full repository build/test, `Tlaw.Verify` / Gate 0 / architecture /
object-reader checks, and exact-head CI/artifact evidence are bound to the
correction candidate in the Draft PR and Control Center evidence packet. The
self-referential dossier intentionally does not name its own final commit SHA.

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
