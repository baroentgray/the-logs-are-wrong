# Gate 2 direct `net10.0` Domain runtime probe

Schema: `tlaw.gate2-direct-domain-runtime-probe/v1`.

Date: `2026-08-13`.

## Scope and identity

| Field | Value |
| --- | --- |
| GitHub contract | [Issue #127](https://github.com/baroentgray/the-logs-are-wrong/issues/127) |
| Exact baseline | `e795fbf2c2a2ca75d728bbe9562f8db0ae40443c` |
| Candidate | This commit, the head of `task/TLAW-054-direct-domain-runtime-probe`. Its exact SHA is bound externally by the Draft PR, exact-head verification, and executor handoff because a commit cannot contain its own resulting object ID. |
| Branch | `task/TLAW-054-direct-domain-runtime-probe` |
| Worktree | `C:\Projects\TheLogsAreWrong-worktrees\TLAW-054` |
| Scratch root | `C:\Temp\TLAW-054` |
| .NET SDK | `10.0.103` |

This is a scratch-only technical evidence probe. It does not accept or
implement a Domain–Unity bridge architecture, host, tick loop, intent routing,
event projection, gameplay, D-016 Resin behavior, containment
`forced_line_pause`, networking, FishNet, or Steamworks.

## Baseline and Unity identity

The baseline retains `TheLogsAreWrong.Domain` as pure Gate-1 C#. Its project has
no `PackageReference`, Unity reference, or Unity dependency, and
`Directory.Build.props` supplies `TargetFramework=net10.0`.

| Field | Value |
| --- | --- |
| Unity executable | `C:\Program Files\Unity 6000.3.21f1\Editor\Unity.exe` |
| Product version | `6000.3.21f1_c02631ffc030` |
| File version | `6000.3.21.12592689` |
| Project pin | `6000.3.21f1 (c02631ffc030)` from `ProjectVersion.txt` |

The scratch Unity project was a fresh archive copy of the baseline
`unity/TheLogsAreWrong` project. Its API compatibility, backend, settings,
packages, and manifest were not changed.

## Scratch provenance and integrity

The executor created the scratch baseline archive with:

```text
git archive --format=zip --output C:\Temp\TLAW-054\baseline-e795fbf2c2a2ca75d728bbe9562f8db0ae40443c.zip e795fbf2c2a2ca75d728bbe9562f8db0ae40443c
```

The archive SHA-256 is
`9C8A9C00C2EE048A54ACA709718C85D21E0D3A12967281232E9FF4862D9C325B`.
It was expanded independently into `DomainBuild\repo` and `ProbeB\repo`.
Every runtime/dependency artifact, Unity import, hook, and log remains under
`C:\Temp\TLAW-054`; no DLL or scratch output is tracked.

## Probe A — exact immutable dependency identity and provenance

### Procedure

The exact-baseline Domain was built under scratch:

```text
dotnet build C:\Temp\TLAW-054\DomainBuild\repo\src\TheLogsAreWrong.Domain\TheLogsAreWrong.Domain.csproj --configuration Release
```

The Domain assembly reference was read from the built binary. One candidate was
selected from the already installed .NET runtime that matched that reference;
no package was downloaded and no variant cycling was performed.

### Domain binary

| Field | Value |
| --- | --- |
| Identity | `TheLogsAreWrong.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null` |
| SHA-256 | `A45CCC6C081DB6F26AE943B1215AA32CD2AAA0E0B5C66C1CA81B85626D54FDDB` |
| Scratch Release build | PASS — 0 warnings, 0 errors |

### `System.Collections.Immutable` AssemblyRef

```text
System.Collections.Immutable, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
```

### One selected dependency candidate

| Field | Value |
| --- | --- |
| Origin | Installed local `Microsoft.NETCore.App 10.0.3` runtime; no download or package restore |
| Source path | `C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.3\System.Collections.Immutable.dll` |
| Identity | `System.Collections.Immutable, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a` |
| SHA-256 | `BBC5F38AA96B57545CF06C322A6720214BEE9087394762C09F0E3B23C14CFE32` |
| File version | `10.0.326.7603` |
| Product version | `10.0.3+c2435c3e0f46de784341ac3ed62863ce77e117b4` |
| Image runtime version | `v4.0.30319` |
| `TargetFrameworkAttribute` | `.NETCoreApp,Version=v10.0` |

The candidate identity exactly matches the Domain AssemblyRef. Its target
framework metadata was inspected by a scratch-only reader that loaded the
candidate from the stated path directly.

## Probe B — Unity Editor load with exactly one explicit dependency

### Procedure

`ProbeB\repo\unity\TheLogsAreWrong` received exactly these imported DLLs:

```text
Assets\Tlaw054Probe\Plugins\TheLogsAreWrong.Domain.dll
Assets\Tlaw054Probe\Plugins\System.Collections.Immutable.dll
```

The only scratch source addition was a static compile hook that used `typeof`
for these production contracts:

```text
TheLogsAreWrong.Domain.Runtime.ShiftRuntimeState
TheLogsAreWrong.Domain.Intents.IntentEnvelope
TheLogsAreWrong.Domain.Events.EventEnvelope
```

It was run with the pinned editor:

```text
Unity.exe -batchmode -nographics -quit -projectPath C:\Temp\TLAW-054\ProbeB\repo\unity\TheLogsAreWrong -logFile C:\Temp\TLAW-054\Logs\probe-b-unity-compile-reload-valid.log
```

No reference validation was disabled. No Unity setting, compatibility level,
scripting backend, manifest, production file, shim, polyfill, package, or
additional framework/runtime assembly was changed or copied.

### Separate results

| Stage | Result |
| --- | --- |
| CSC/Tundra static compilation | PASS — `Assembly-CSharp.dll` compiled with all three `typeof` references; `Tundra build success` |
| Unity Editor assembly reload | FAIL — the single immutable candidate could not load; the Domain was then unloaded as broken |
| Unity batch process | Exit `0`; this does not convert the failed assembly reload into a load pass |

The first material loader blocker after adding the one allowed dependency was:

```text
Could not load image C:\Temp\TLAW-054\ProbeB\repo\unity\TheLogsAreWrong\Assets\Tlaw054Probe\Plugins\System.Collections.Immutable.dll due to Invalid data directory 3
Run the peverify utility against this for more information.
```

The same reload then recorded these dependent failures:

```text
TypeCache is unable to load attribute info on class TheLogsAreWrong.Domain.Time.SimulationDuration. Are you missing a reference?
Unloading broken assembly Assets/Tlaw054Probe/Plugins/TheLogsAreWrong.Domain.dll, this assembly can cause crashes in the runtime
```

The task stops at this first material loader blocker. It did not run `peverify`,
substitute another framework assembly, inspect alternative candidate variants,
or add another dependency.

## Probe C — EditMode type/runtime smoke

**NOT RUN.** Probe B did not reach Unity Editor assembly-load success. Therefore
the three required contracts were only statically referenced by the Probe B
compiler hook; no Editor runtime materialization, construction, factory call,
host operation, intent execution, replay, or adapter operation was attempted.

## Probe D — Windows Development player smoke

**NOT RUN.** Probe C was not eligible. No Windows build, player launch, player
runtime smoke, or player dependency experiment was attempted.

## Direct-binary implication

The exact matching immutable candidate resolves the original missing-assembly
name but cannot itself be loaded by this Unity Editor under the constrained
procedure. The direct existing `net10.0` binary path remains blocked before
EditMode and player execution. The exact meaning of `Invalid data directory 3`
and any permitted remedy are unresolved; evaluating another dependency, a
runtime substitution, a Unity setting change, or an architectural boundary is a
separate explicitly scoped future decision. This task accepts no architecture.

## Repository verification

The committed candidate was checked against the exact baseline with the
required commands and all passed:

| Check | Result |
| --- | --- |
| `git diff --name-only e795fbf2c2a2ca75d728bbe9562f8db0ae40443c...HEAD` | Exactly `docs/agent/GATE2_DIRECT_DOMAIN_RUNTIME_PROBE.md` |
| `git diff --check` | PASS |
| `dotnet build TheLogsAreWrong.sln --configuration Release` | PASS |
| `dotnet test TheLogsAreWrong.sln --configuration Release` | PASS — `1631` passed, `0` failed, `0` skipped |
| Exact-head `Tlaw.Verify` | PASS |
| Gate 0 / Git object reader | PASS |
| Production `Directory.Build.props`, Domain project/source, and `unity/TheLogsAreWrong/**` | Byte-identical to baseline |

The exact candidate SHA, verifier output, CI workflow/job/artifact/digest, and
Draft PR identity are retained outside this self-referential commit in the
executor handoff and PR evidence.

## Changed paths, deviations, and retained scratch evidence

The exact tracked changed-path set is:

```text
docs/agent/GATE2_DIRECT_DOMAIN_RUNTIME_PROBE.md
```

The first cold Unity attempt used a scratch hook with a C# 10 file-scoped
namespace, while this Unity compiler is C# 9; it failed with `CS8773` and was
not accepted as probe evidence. The hook was changed in scratch only to a
block-scoped namespace and the valid two-DLL run above was repeated. A
scratch-only metadata reader also required one source correction to fit its
template's existing `Main` method before inspecting the selected DLL. Neither
setup correction changed a probe input, production file, package, or Unity
setting.

Retained evidence paths include:

```text
C:\Temp\TLAW-054\baseline-e795fbf2c2a2ca75d728bbe9562f8db0ae40443c.zip
C:\Temp\TLAW-054\Logs\probe-a-domain-build.log
C:\Temp\TLAW-054\Logs\immutable-candidate-metadata.log
C:\Temp\TLAW-054\Logs\probe-b-unity-compile-reload.log
C:\Temp\TLAW-054\Logs\probe-b-unity-compile-reload-valid.log
C:\Temp\TLAW-054\Logs\probe-b-unity-compile-reload-valid-exit.txt
```

No production Domain target/source/project change; Unity change; multitargeting;
package, shim, polyfill, bridge, facade, host, gameplay, D-016, networking,
FishNet, Steamworks, decision-log change; merge; or cleanup was performed.

DIRECT_NET10_EDITOR_LOAD_FAIL
