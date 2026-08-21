# Gate 2 C1 validated configuration production handoff

TLAW-070 productionizes only the owner-selected C1 deployment transport from
the TLAW-069 proof. It starts from formalization baseline
`4c92d7b529ffc26aac6072c5e70fc1478eac2b4d` and preserves D-021 U4: YAML and
its validation remain outside Unity; Unity receives already validated
PortableAuthority data only.

## Ownership and data path

`TheLogsAreWrong.PortableAuthority` owns the single C1 v1 semantic
implementation:

- `Configuration/ValidatedConfigurationC1Codec.cs` encodes and materializes
  the full PortableAuthority configuration graph. It contains no YAML parsing,
  schema, defaults, coercion, or cross-document validation.
- `Configuration/ValidatedConfigurationC1DeploymentManifest.cs` carries the
  trusted deployment identity and verifies artifact length/SHA, source binding,
  and canonical projection before materialization is accepted.

Outside Unity, `ValidatedConfigurationC1Exporter` invokes the real
`YamlConfigurationLoader`, refuses failed validation, and hands only its
successful `ValidatedConfiguration` result to the C1 codec. The bounded
`Tlaw.ValidatedConfig.Export` tool supplies `--check` for reproducible tracked
material freshness and `--write` for an intentional artifact refresh.

Tracked Unity deployment material:

- `unity/TheLogsAreWrong/Assets/Gate2/Configuration/validated-configuration-c1-v1.base64`
- `unity/TheLogsAreWrong/Assets/Gate2/Configuration/validated-configuration-c1-v1.manifest`

The manifest is deterministic LF text and binds these frozen v1 facts:

| Field | Value |
| --- | --- |
| Format/version | `TLAW-CFG-U4-C1` / `1` |
| Artifact bytes/SHA-256 | `2326` / `94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541` |
| Canonical projection SHA-256 | `4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1` |
| Shift YAML SHA-256 | `CD08DDFC6F354A1FDDEC7EE751007C95920CDBD26AFA6350A068C350D88277E7` |
| Anomalies YAML SHA-256 | `6517C145AD41410131FF50BF691FE9C37FB33E1CB8E065E42ADB97364F4785D7` |
| Loader source blob | `23651feb72bfa432685f8ef1850648d355baed57` |

## Contracts and failure closure

The .NET C1 contracts run the real canonical YAML loader, regenerate the exact
artifact and manifest byte-for-byte, verify the loader source identity, and
check repeated deterministic encoding. They require the current full 24-record
inventory, including `LineNoiseConfiguration`, and direct `HostSession`
construction from materialized PortableAuthority records in test evidence.

The codec/manifest reject corruption, truncation, unsupported version, trailing
data, malformed payload lengths, malformed collection counts, stale source
binding, invalid manifest framing, and a payload changed with its internal
self-hash recomputed. The last case is accepted by neither the independently
trusted artifact SHA nor the trusted projection identity.

The test-only TLAW-069 C1 codec/projection and Unity codec were retired. No C2
generation path remains. The pinned Unity test reads the exact tracked bytes,
uses only the production PortableAuthority codec/materializer, verifies its
independently trusted artifact/source/projection values, then constructs
`HostSession` directly as test evidence.

## Unity boundary

Only the existing PortableAuthority plugin was mechanically refreshed. The
runtime plugin inventory remains exactly:

1. `TheLogsAreWrong.PortableAuthority.dll`
2. `System.Collections.Immutable.dll`
3. `System.Runtime.CompilerServices.Unsafe.dll`

No Domain DLL, Config.Yaml, YamlDotNet, Unity configuration schema,
ScriptableObject, production U3 owner, Unity driver, scheduler, networking,
scene, prefab, package, or ProjectSettings change is part of this handoff.

## Terminal

```text
VALIDATED_CONFIG_C1_PRODUCTION_HANDOFF_PASS
C1_ARTIFACT_FORMAT_V1_PRESERVED
YAML_REMAINS_OUTSIDE_UNITY_RUNTIME
PORTABLE_CONFIG_SINGLE_SEMANTICS_PRESERVED
PRODUCTION_CONFIG_C1_HANDOFF_IMPLEMENTED
U3_PRODUCTION_HOST_OWNER_NOT_IMPLEMENTED
UNITY_PRODUCTION_DRIVER_NOT_IMPLEMENTED
NETWORKING_NOT_STARTED
```
