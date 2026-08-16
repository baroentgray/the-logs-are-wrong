# Gate 2 — production Unity PortableAuthority import (TLAW-062)

## Candidate binding and boundaries

| Item | Evidence |
| --- | --- |
| GitHub issue / Linear | #143 / BAR-105 |
| Exact base and authoritative `main` | `5aa9922594688402e430ca1a8f8a6d4bed92ecc7` |
| Branch / worktree | `task/TLAW-062-production-unity-portable-authority-import` / `C:\Projects\TheLogsAreWrong-worktrees\TLAW-062` |
| Original implementation candidate | `f270338da76455409edc40576c098acfa660cb9b` |
| Draft PR | #144, base `main`, body exactly `Closes #143` |
| Original exact-head external binding | Repository verification run `31938047046` / job `95142966127` / artifact `9261226401`, digest `sha256:9c79ba7db259b46fe7f57365f2071b1d0c28202e9542e746d199c12aab583e97` |
| First control-center blocker | `5307516999` — revision-stamped artifact required a build-contract correction |
| First owner authorization | GitHub `5307536081`; Linear `05afb8ed-8c62-4162-b9e3-63562e5a265b` |
| Unpushed first correction | `d48206b880a6e7fd90b7764ef9c59abf44bc9ef3` — revision stamp removed, but cross-commit byte equality still failed |
| Second owner authorization | GitHub `5308423546`; Linear `7c35fcac-80fe-4ff3-b901-435bc6d9b85b` |

The correction candidate is bound by its immutable commit SHA after commit and
by the Draft PR plus exact-head control-center/repository verification after
push. A commit SHA is deliberately not embedded into its own tracked dossier.
The original run above remains evidence for `f270338…` only; it is not claimed
as exact-head CI evidence for the correction commit.

This is a Unity consumer import and a bounded deployment-artifact correction.
There remains one semantic authoritative implementation in
`TheLogsAreWrong.PortableAuthority`. No `src/**` source, project, package,
props, targets, authority algorithm, Domain composition, host/tick, gameplay,
D-016, networking, FishNet, Steamworks, scene, prefab, Package manifest, or
ProjectSettings change is authorized or made.

## Exact-base Phase-0 reconstruction

The following baseline record was reconstructed read-only from exact Git object
bytes at `5aa992…` after the original Phase-0 logs were no longer retained.
For each directory, `inventory SHA-256` means SHA-256 of sorted
`repository-path|blob-SHA-256` records joined with LF. The Git tree IDs and
file counts provide a second independent inventory anchor.

| Exact-base scope | Files | Git tree | Inventory SHA-256 |
| --- | ---: | --- | --- |
| `unity/TheLogsAreWrong` | 40 | `55a550c4316809ab27160c68b98cc07e578576cd` | `67CFCD073181EF132D817E5653A6FC2AC113A9A6238B375C50E0680EF1ACB403` |
| `Packages` | 2 | `18ca1c427ad307dc5d9c49b4799de7659cc3c47b` | `91509B252F83077EC31BDEF35C54AD44355D193F99EC536768B62C85C76F8C3F` |
| `ProjectSettings` | 21 | `19f0cbaaf58c938bfbffbb9b9bb0a877202a1b12` | `B98A08FECF5BB6DB9136F5B6C224C39CD1D60E00C2711632A339501BD8FD1146` |
| `Assets/Gate2` | 16 | `4b2dc4f416ab469f3f5f3337eae931806d6278a5` | `3AC67C5150BA6801672DFC19D7B42D32CEDF6D0A496B49284BD3C343D8826CB6` |

The exact-base file hashes are:

```text
Packages/manifest.json       56494B2AC7B2B44A4A7A886A465803B99E65E57AFA7F21604A1D81A88E71E30B
Packages/packages-lock.json  94A75F033C00CA18D15A247C79CBC3C7786BA11D232D0910771B1EEB98DC5D02
Assets/Gate2/Bootstrap/Gate2Bootstrap.unity
                             E523088207A4B4DCD00B98E46ABB84E8436E905C0206467221BA14D871896959
```

The baseline has exactly one bootstrap scene and zero `.prefab` files. The
following target plugin paths were absent at baseline:

```text
Assets/Gate2/Plugins
Assets/Gate2/Plugins/PortableAuthority
Assets/Gate2/Plugins/PortableAuthority/TheLogsAreWrong.PortableAuthority.dll
```

## PortableAuthority build-contract correction

### Former mismatch and root cause

The original committed Unity plugin on `f270338…` had SHA-256
`6FFC47FB5C2BFBFB348542A20C994422A0E0AA46AAABB4ADD0B2408E4AFC2EB4` and
embedded informational version
`1.0.0+5aa9922594688402e430ca1a8f8a6d4bed92ecc7`. A normal fresh Release
build at that same source head produced SHA-256
`549BD487EE0B30BC1B20FCC9121350A1D037C63CE68E069140DDEA1A79DF5FF2` and
embedded `1.0.0+f270338da76455409edc40576c098acfa660cb9b`.

The ordinary build therefore stamps the Git revision into
`AssemblyInformationalVersion`, changing the binary across a commit even when
the PortableAuthority source is unchanged. This was the real byte-equality
mismatch reported by control center, not a semantic authority change.

The first command-line-only property was necessary but insufficient: on the
unpublished `d48206b…` correction, the fresh deployment output was
`4F694A4CF9A067FA027DD8F0DD11CAD22ED77D4D3F69C25841C1A4552A0D5C57`, while
the committed plugin was `996219A790835C8EF68A6334CF7CEB5835847A1531DF231D3B31966CD06F04FD`.
The informational version had no Git suffix, but the generated portable PDB
contained SourceLink for `d48206b…`; its checksum/debug metadata still changed
the DLL. The first correction was never pushed.

The deployment artifact therefore has one explicit, non-persisted build-time
recipe:

```text
dotnet clean src/TheLogsAreWrong.PortableAuthority/TheLogsAreWrong.PortableAuthority.csproj --configuration Release
dotnet build src/TheLogsAreWrong.PortableAuthority/TheLogsAreWrong.PortableAuthority.csproj --configuration Release -p:IncludeSourceRevisionInInformationalVersion=false -p:DebugSymbols=false
```

The property is command-line-only. It is not stored in a csproj, source,
Directory.Build.props, props/targets file, package, or architecture setting.

### Reproducibility proof

Two clean builds from the unchanged local correction state used that exact
two-property recipe. External
evidence is retained under
`C:\Temp\TLAW-062\build-contract-second-reproducibility\` as
`build-a-clean.log`, `build-a.log`, `build-b-clean.log`, `build-b.log`, and
the separately captured `TheLogsAreWrong.PortableAuthority.build-{a,b}.dll`.

| Requirement | Build A | Build B | Result |
| --- | --- | --- | --- |
| SHA-256 | `57ECAB9DA135DE17147F03272DD8429535FF68022194DB8A092BC43B2B14ECBB` | `57ECAB9DA135DE17147F03272DD8429535FF68022194DB8A092BC43B2B14ECBB` | PASS; byte-for-byte equal |
| Identity | `TheLogsAreWrong.PortableAuthority, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null` | same | PASS |
| Informational version | `1.0.0`, no 40-hex Git suffix | same | PASS |
| PDB and commit-specific debug/SourceLink entry | absent | absent | PASS |
| Build diagnostics | 0 warnings, 0 errors | 0 warnings, 0 errors | PASS |

The derived committed `TheLogsAreWrong.PortableAuthority.dll` is replaced only
with that reproducible output and has the same `57ECAB…ECBB` hash.

After the recreated correction commit, a separate clean cross-commit build
using the same two properties again produced
`57ECAB9DA135DE17147F03272DD8429535FF68022194DB8A092BC43B2B14ECBB` and was
byte-identical to the committed plugin. It again produced no PDB, no embedded
40-hex Git identifier, and no `RSDS`, `.pdb`, or SourceLink/debug entry in the
DLL. The final candidate repeats that decisive proof after this evidence update.

## Exact dependency and plugin evidence

The Assets plugin inventory is exactly three DLLs; no fourth task plugin was
added. `System.Memory`, `System.Buffers`, `System.Numerics.Vectors`, a Domain
DLL, PDBs, XML, source copies, shims, or framework closure are not imported
under `Assets`.

| Imported asset | Identity | Official source asset | SHA-256 |
| --- | --- | --- | --- |
| `TheLogsAreWrong.PortableAuthority.dll` | `TheLogsAreWrong.PortableAuthority, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null` | exact deployment recipe above | `57ECAB9DA135DE17147F03272DD8429535FF68022194DB8A092BC43B2B14ECBB` |
| `System.Collections.Immutable.dll` | `System.Collections.Immutable, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a` | `C:\Users\Sergei\.nuget\packages\system.collections.immutable\8.0.0\lib\netstandard2.0\System.Collections.Immutable.dll` | `5B1B1C83BA3D135C2FDFE425842FBE9C7432878B7E468623ACB554C69B4C130F` |
| `System.Runtime.CompilerServices.Unsafe.dll` | `System.Runtime.CompilerServices.Unsafe, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a` | `C:\Users\Sergei\.nuget\packages\system.runtime.compilerservices.unsafe\6.0.0\lib\netstandard2.0\System.Runtime.CompilerServices.Unsafe.dll` | `01748200F2400C742AA689F1F5101BD6298EFDFD92C00C18F4FA473847235BA9` |

Each DLL `.meta` remains GUID-only; Unity serialized no explicit
`PluginImporter` fields. The exact inventory is:

```text
System.Collections.Immutable.dll.meta             guid: 5caa87a8185efb249b2cad967e74f8e3
System.Runtime.CompilerServices.Unsafe.dll.meta   guid: b9df694aa649b4c42a2af5f9d1a349eb
TheLogsAreWrong.PortableAuthority.dll.meta        guid: 94701842084bc7c4c87dac60a8a0020e
```

Thus Unity's default effective managed-plugin import behavior is the observed
behavior: the exact pinned Editor compiles the project, loads the portable
assembly, runs its direct authority contracts in EditMode, packages the DLL in
the Windows player, and executes the smoke operation. No meta drift occurred
during this correction.

## Changed-path contract

The expected old-head-to-correction change set is exactly:

```text
docs/agent/GATE2_PRODUCTION_UNITY_PORTABLE_AUTHORITY_IMPORT.md
unity/TheLogsAreWrong/Assets/Gate2/Plugins/PortableAuthority/TheLogsAreWrong.PortableAuthority.dll
unity/TheLogsAreWrong/Assets/Gate2/Tests/Editor/PortableAuthorityImportParityTests.cs
```

Counts against exact base remain: `src/** = 0`, `Packages/** = 0`,
`ProjectSettings/** = 0`, scenes = 0, prefabs = 0. The two third-party DLLs
and all `.meta` files remain byte-identical.

## Actual test ownership

`PortableAuthorityImportParityTests` directly proves the contracts it contains:
the exact three-DLL Assets inventory; Immutable/Unsafe identity and hash; strict
byte equality between the committed plugin and the fresh two-property
deployment-recipe output (including informational version `1.0.0`); resolution from the imported
assembly of `ShiftRuntimeState`, `HostLogTransitionService`,
`SawCycleStartService`, and `LineNoiseDerivationService`; the direct authority
operation; no copied 26-file authority source; and no Asset Domain/Memory/
Buffers/Vectors plugin.

It does **not** directly resolve `IntentEnvelope` or `EventEnvelope`, and it
does **not** own package or loaded-network-assembly guards.
`Gate2BootstrapSmokeTests` owns the pre-existing assertions that no forbidden
networking package is declared and no assembly whose name contains `FishNet`,
`FishySteamworks`, or `Steamworks` is loaded.

## Unity Editor proof

The only editor used is:

```text
C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe
6000.3.21f1 (c02631ffc030)
```

One fresh batch import/compile attempt completed successfully with no C# or
managed-loader error. One EditMode attempt passed `11/11`, `0 failed`,
`0 skipped`, including all six portable import/parity contracts. The retained
final-recipe logs/results are under
`C:\Temp\TLAW-062\build-contract-second-unity\` as
`import-compile-attempt-1.log`, `editmode-attempt-1.log`, and
`editmode-attempt-1.xml`.

The direct authoritative operation is exactly:

```text
ShiftRuntimeState.Create
> HostLogTransitionService.Apply (SCHEDULED -> AT_FEED_GATE)
> HostLogTransitionService.Apply (AT_FEED_GATE -> AT_INTAKE)
> HostLogTransitionService.Apply (AT_INTAKE -> QUEUED_FOR_SAW)
> SawCycleStartService.Start (ServerTick.From(10))
> LineNoiseDerivationService.Evaluate (ServerTick.From(10))
```

Its auditable canonical projection is the ordered LF-joined record in
`PortableAuthorityImportParityTests`: operation chain, `shift_id`, created/
queued/saw state versions, `log_id`, log state, saw start/due ticks, and line
noise current/evaluated/changed ticks. The test and the runtime smoke use the
same projection construction with the `TLAW058_PROBE_SHIFT`, seed `58`, and
`probe_log` input fixture. Its raw UTF-8 SHA-256 is exactly:

```text
CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F
```

## Windows player proof

One `StandaloneWindows64` `Development` build attempt completed:

```text
[TLAW052] BUILD_RESULT=Succeeded
[TLAW052] BUILD_ERRORS=0 BUILD_WARNINGS=0
```

The output managed directory contains 109 DLLs. Its sorted
`name|SHA-256` inventory digest is
`BFD90CC74C0B35578826A4522FDBB67E01B8D8D0127A320F99B19222B055F41C`.
The task-supplied subset is exactly the three Assets plugins above, with their
same identities and hashes. There is no Domain DLL or
`System.Numerics.Vectors.dll`. Unity generated player facades are distinct
from task-supplied Assets: `System.Memory.dll`
(`4.0.99.0`, PKT `cc7b13ffcd2ddd51`, SHA
`C4F030A2CBA7DA7CDCF493257C24560E203D355904AEE490D645A935842F834A`) and
`System.Buffers.dll` (`4.0.99.0`, PKT `cc7b13ffcd2ddd51`, SHA
`762F8FDBE975E05B76BE5FE996C53CE7C75E4A2830F2F50B02A5948EF6BA0AEB`).
Neither is an imported Asset or a task-supplied plugin.

One player run with `-tlaw-bootstrap-smoke` exited `0` and emitted:

```text
TLAW062_PLAYER_PORTABLE_LOAD_PASS
TLAW062_PLAYER_AUTHORITY_PASS
TLAW062_PLAYER_AUTHORITY_SHA=CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F
TLAW052_BOOTSTRAP_STARTED scene=Gate2Bootstrap smokeMode=True unity=6000.3.21f1
TLAW052_BOOTSTRAP_QUIT frames=60
```

It uses the same canonical projection representation and canonical SHA as the
EditMode proof. No loader or runtime failure was recorded. The one build and
one smoke logs are retained as `player-build-attempt-1.log` and
`player-smoke-attempt-1.log` under
`C:\Temp\TLAW-062\build-contract-second-unity\`.

## Deviations, history, and remaining repository binding

The first original Phase-0 attempt stopped before production/Unity changes
because exact Unity `6000.3.21f1 (c02631ffc030)` was not installed; other
installed editor versions were not substituted. The exact editor was later
installed, Phase 0 was repeated, and implementation then continued. This is
historical environment evidence, not a candidate defect.

The original f270 exact-head repository artifact recorded restore PASS, build
`0/0`, tests `1633/0/0`, diff check PASS, Gate 0/object reader `52/52` PASS,
architecture PASS, Domain dependency PASS, and verdict PASS. The correction
commit must receive a **new** exact-head repository verification/artifact after
push; that immutable external PR/CI evidence is deliberately not replaced by
the stale f270 run in this dossier.

For the two-property correction content, local repository regression passed:
fresh restore; Release build `0 warnings / 0 errors`; full tests `1633/1633`,
`0 failed`, `0 skipped`; frozen D-014/TLAW-046 slice `87/87`; canonical
PortableAuthority migration vector `1/1` with SHA
`CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F`; and
`Tlaw.Verify` against the exact base with diff check, Gate 0/object reader
`52/52`, architecture, and Domain dependencies all PASS. As expected, the
ordinary Release build can emit its normal revision/debug output, so the exact
two-property deployment recipe was rerun after repository regression and again
matched the committed plugin byte-for-byte with no PDB or SourceLink/debug
entry. A new exact-head CI artifact is still required after the final push.

Not performed: persistent build-property configuration; PortableAuthority or
Domain source/project/package changes; Immutable/Unsafe replacement; extra
plugin import; production Unity scene/prefab/settings/package change; host,
tick, gameplay, D-016, networking, FishNet, Steamworks; Ready, merge, or
cleanup.

PRODUCTION_UNITY_PORTABLE_AUTHORITY_IMPORT_PASS
NO_HOST_TICK_GAMEPLAY_OR_NETWORKING
