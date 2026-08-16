# Gate 2 — production Unity PortableAuthority import (TLAW-062)

## Contract and boundaries

- GitHub Issue: #143 (`[Gate 2] TLAW-062 — Import production PortableAuthority into Unity`)
- Linear: BAR-105
- Exact base: `5aa9922594688402e430ca1a8f8a6d4bed92ecc7`
- Branch: `task/TLAW-062-production-unity-portable-authority-import`
- Worktree: `C:\Projects\TheLogsAreWrong-worktrees\TLAW-062`
- Owner implementation authorization: Issue #143 comment `5303894928`.

This is a constrained Unity consumer import, not a second authority
implementation.  The one authority implementation remains in
`TheLogsAreWrong.PortableAuthority`; no `src/**` or production Domain source
was changed.  No host/tick integration, gameplay, D-016, networking,
FishNet, Steamworks, package-manifest, ProjectSettings, scene, or prefab
change was made.

## Phase 0 fail-closed inventory

`origin/main` and this task branch were both verified at the exact base before
any tracked change.  The accepted 26 moved PortableAuthority files and the
34 outer Domain files remained the accepted `26/34` partition.  The accepted
source-compatibility inventory remained `131 + 25 + 1 = 157`; the project and
package graph was unchanged:

```
TheLogsAreWrong.Domain -> TheLogsAreWrong.PortableAuthority
TheLogsAreWrong.PortableAuthority -X-> TheLogsAreWrong.Domain
```

The fresh candidate build was produced with:

```
dotnet restore src/TheLogsAreWrong.PortableAuthority/TheLogsAreWrong.PortableAuthority.csproj --force-evaluate --no-cache
dotnet build   src/TheLogsAreWrong.PortableAuthority/TheLogsAreWrong.PortableAuthority.csproj --configuration Release --no-restore
```

The result was `netstandard2.1`, `LangVersion=latest`, assembly identity
`TheLogsAreWrong.PortableAuthority, Version=1.0.0.0, Culture=neutral,
PublicKeyToken=null`, and `0 warnings / 0 errors`.

The exact pinned editor was found and used throughout:

```
C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe
6000.3.21f1 (c02631ffc030)
```

Before import, `unity/TheLogsAreWrong/Packages/**`,
`unity/TheLogsAreWrong/ProjectSettings/**`, the Gate-2 bootstrap scene, and
all prefab inventory were recorded.  No Unity networking token was found;
there is one bootstrap scene and zero prefabs.  The production Unity tree was
otherwise byte-identical to the base.

## Exact import set

Only the following three deployment DLLs were added under
`unity/TheLogsAreWrong/Assets/Gate2/Plugins/PortableAuthority/`, with Unity
generated `.meta` assets only.  No Domain DLL, PDB, XML, source copy,
`System.Memory`, `System.Buffers`, `System.Numerics.Vectors`, framework
closure, package, shim, or polyfill was imported.

| DLL | Source | SHA-256 |
| --- | --- | --- |
| `TheLogsAreWrong.PortableAuthority.dll` | fresh candidate `bin/Release/netstandard2.1` output | `6FFC47FB5C2BFBFB348542A20C994422A0E0AA46AAABB4ADD0B2408E4AFC2EB4` |
| `System.Collections.Immutable.dll` | official `System.Collections.Immutable` 8.0.0 `lib/netstandard2.0` | `5B1B1C83BA3D135C2FDFE425842FBE9C7432878B7E468623ACB554C69B4C130F` |
| `System.Runtime.CompilerServices.Unsafe.dll` | official `System.Runtime.CompilerServices.Unsafe` 6.0.0 `lib/netstandard2.0` | `01748200F2400C742AA689F1F5101BD6298EFDFD92C00C18F4FA473847235BA9` |

The direct package remains exactly `System.Collections.Immutable 8.0.0`.
`System.Runtime.CompilerServices.Unsafe 6.0.0` is its accepted resolved
closure member.  The only .NET libraries injected by this task are the three
listed files.

## Unity consumer contracts

`Assets/Gate2/Authority/PortableAuthoritySmoke.cs` is a minimal command-gated
runtime consumer.  It has no MonoBehaviour, scene, host, tick, gameplay, or
network ownership.  Its `RuntimeInitializeOnLoadMethod` returns unless
`-tlaw-bootstrap-smoke` is present, verifies the imported assembly identity,
then invokes the existing authoritative services directly:

```
ShiftRuntimeState.Create
-> HostLogTransitionService.Apply (three accepted transitions)
-> SawCycleStartService.Start
-> LineNoiseDerivationService.Evaluate
```

It emits the required player markers only after the imported assembly has
loaded and the canonical projection has matched.  It contains no replacement
authority algorithm.

`PortableAuthorityImportParityTests` is an EditMode consumer contract.  It
asserts the exact three-file import set, byte equality to the fresh candidate,
the two official dependency identities/hashes, loading of
`ShiftRuntimeState`, `IntentEnvelope`, and `EventEnvelope`, the same direct
authority chain and canonical SHA, no copied accepted authority source, no
Domain DLL, no extra dependency DLL, and no forbidden Unity/network symbols.
The existing test assembly definition was changed only to declare its three
precompiled references.

## Unity verification

A fresh batch import/compile with the exact pinned editor completed with code
`0`; script compilation succeeded and no loader or C# compiler error was
reported.  No additional dependency was added after that import.

EditMode result:

```
total=11, passed=11, failed=0, inconclusive=0, skipped=0
```

This includes all six `PortableAuthorityImportParityTests` contracts and the
pre-existing five bootstrap contracts.

The pre-existing build entry produced a Windows x64 Development player:

```
[TLAW052] BUILD_RESULT=Succeeded
[TLAW052] BUILD_ERRORS=0 BUILD_WARNINGS=0
[TLAW052] BUILD_SIZE=146245310
```

The build output contains the three imported assembly bytes unchanged.  Unity
also emitted its own platform `System.Memory.dll` and `System.Buffers.dll`
facade assemblies in the generated player `Managed/` output; they were not
imported Assets, are not task-supplied dependencies, and are not part of the
three-DLL import inventory.

The generated player was run once with `-tlaw-bootstrap-smoke` and exited
`0`.  Its log contains exactly the requested proof markers:

```
TLAW062_PLAYER_PORTABLE_LOAD_PASS
TLAW062_PLAYER_AUTHORITY_PASS
TLAW062_PLAYER_AUTHORITY_SHA=CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F
TLAW052_BOOTSTRAP_STARTED scene=Gate2Bootstrap smokeMode=True unity=6000.3.21f1
TLAW052_BOOTSTRAP_QUIT frames=60
```

There were no player loader, missing-reference, or runtime failure entries.

## Repository regression evidence before commit

Fresh solution restore and full Release build both passed with `0 warnings / 0
errors`.  The full existing suite passed `1633/1633`, `0 failed`, `0 skipped`.
The explicit frozen D-014 snapshot/capture/restore/journal/replay slice
(`Scope=TLAW-046`) passed `87/87`, and the canonical
`PortableAuthorityMigrationRegressionTests` authority-chain vector passed
`1/1` with SHA-256
`CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F`.

The exact committed candidate must additionally pass `git diff --check`,
`Tlaw.Verify` against the exact base, Gate 0/object reader,
architecture/dependency checks, and exact-head repository CI/artifact
verification.  Those checks bind this dossier to the committed candidate; they
do not authorize host/tick, gameplay, networking, or any subsequent production
work.

PRODUCTION_UNITY_PORTABLE_AUTHORITY_IMPORT_PASS
NO_HOST_TICK_GAMEPLAY_OR_NETWORKING
