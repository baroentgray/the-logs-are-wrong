# Gate 2 Domain–Unity compatibility audit

Schema: `tlaw.gate2-domain-unity-compatibility/v1`.

Date: `2026-08-13`.

## Scope and identity

| Field | Value |
| --- | --- |
| GitHub contract | [Issue #125](https://github.com/baroentgray/the-logs-are-wrong/issues/125) |
| Exact baseline | `71f88b2423f199cffbc70bf9ddf005e718838f80` |
| Candidate | This commit, the head of `task/TLAW-053-domain-unity-compatibility-audit`. Its exact SHA is recorded in the Draft PR and executor handoff because a commit cannot contain its own object ID. |
| Branch | `task/TLAW-053-domain-unity-compatibility-audit` |
| Worktree | `C:\Projects\TheLogsAreWrong-worktrees\TLAW-053` |
| Scratch root | `C:\Temp\TLAW-053` |
| .NET SDK | `10.0.103` |

This is an evidence-only audit. It neither accepts nor implements a Domain–Unity
bridge architecture. It implements no host, gameplay, intent routing, event
projection, networking, FishNet, Steamworks, D-016 Resin button behavior, or
containment `forced_line_pause` behavior.

## Baseline facts

The baseline `Directory.Build.props` supplies `TargetFramework=net10.0` to
`TheLogsAreWrong.Domain`. The Domain project only declares its assembly and root
namespace; it has no `PackageReference`, Unity reference, or Unity dependency.
Domain remains pure Gate-1 C# and has no `UnityEngine` dependency.

The tested editor was:

| Field | Value |
| --- | --- |
| Executable | `C:\Program Files\Unity 6000.3.21f1\Editor\Unity.exe` |
| Product version | `6000.3.21f1_c02631ffc030` |
| File version | `6000.3.21.12592689` |
| Project pin | `6000.3.21f1 (c02631ffc030)` from `ProjectVersion.txt` |

The Unity project used for every Unity attempt was a fresh archive copy of
`unity/TheLogsAreWrong` from the exact baseline. It remains the D-017-pinned,
single-process, network-free bootstrap shell.

## Scratch integrity and procedure

The executor created `baseline-71f88b2423f199cffbc70bf9ddf005e718838f80.zip`
under the scratch root using:

```text
git archive --format=zip --output C:\Temp\TLAW-053\baseline-71f88b2423f199cffbc70bf9ddf005e718838f80.zip 71f88b2423f199cffbc70bf9ddf005e718838f80
```

Its SHA-256 is
`B8923EB6319626D1F76B403220661C3C2E086013DE25E2C869ACEDE5F22EA150`.
The archive was expanded independently into `ProbeA\repo`, `ProbeB\repo`, and
`ProbeC\repo`.

After Probe B, all 60 baseline Domain `.cs` files compared byte-for-byte equal
to the corresponding scratch files (`0` mismatches). The copied
`Directory.Build.props` SHA-256 was
`6F8C3BA0B862CCBB6D970DC9FC871FE8A3841130DE0FD84EEB83077A68A736AD` in both
locations. The copied Domain project SHA-256 was
`12132D6A568A93FF5B10C7A8B331E79EB72C630DA52A794AED2B477CB5957E60` in both
locations. No scratch Domain `.cs` file, production targeting input, or
dependency was edited.

All DLLs, hooks, Unity assets, generated project state, builds, and logs remain
outside the repository under `C:\Temp\TLAW-053`. The production candidate's
tracked-input integrity is additionally established by the final exact
baseline-to-head changed-path and object-verifier checks recorded below.

## Probe A — existing `net10.0` binary

### Procedure

The exact baseline Domain was built in the scratch copy:

```text
dotnet build C:\Temp\TLAW-053\ProbeA\repo\src\TheLogsAreWrong.Domain\TheLogsAreWrong.Domain.csproj --configuration Release
```

Only the resulting
`bin\Release\net10.0\TheLogsAreWrong.Domain.dll` was copied to
`ProbeA\repo\unity\TheLogsAreWrong\Assets\Tlaw053ProbeA\Plugins`. A
scratch-only regular Unity compilation hook statically referenced exactly:

```text
TheLogsAreWrong.Domain.Runtime.ShiftRuntimeState
```

The Unity compile command was:

```text
Unity.exe -batchmode -nographics -quit -projectPath C:\Temp\TLAW-053\ProbeA\repo\unity\TheLogsAreWrong -logFile C:\Temp\TLAW-053\Logs\probe-a-unity-compile-valid.log
```

### Result

| Field | Result |
| --- | --- |
| Scratch Domain Release build | PASS — 0 warnings, 0 errors |
| Assembly identity | `TheLogsAreWrong.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null` |
| Assembly SHA-256 | `1464E613AB6CE54073010EEDFDB90001F17BC16E008C768AA024768836C84104` |
| Unity batch compile | PASS — process exit `0`; no C# compiler error or script-compilation-error record |
| Representative type | `TheLogsAreWrong.Domain.Runtime.ShiftRuntimeState` |

The Unity compiler response includes the imported DLL as
`-r:"Assets/Tlaw053ProbeA/Plugins/TheLogsAreWrong.Domain.dll"` and successfully
compiled the static `ShiftRuntimeState` reference. This proves the contracted
Editor batch-compile boundary for the existing binary only. Probe A did not
include an EditMode execution test or player build/launch; neither is claimed.

#### Scratch correction

The first Probe A hook mistakenly used `TheLogsAreWrong.Runtime` instead of the
actual `TheLogsAreWrong.Domain.Runtime` namespace and failed with `CS0234`. It
was not accepted as compatibility evidence. The hook was corrected in scratch
only and the valid run above was repeated. The malformed-hook logs are retained
at `Logs\probe-a-unity-compile.log` and
`Logs\probe-a-unity-compile-rerun.log`; the valid evidence is
`Logs\probe-a-unity-compile-valid.log` and its matching exit file.

## Probe B — source-identical `netstandard2.1` feasibility

### Procedure

No source, project file, package, shim, conditional compilation symbol, or
source generator was changed. Targeting metadata was overridden only at the
command line:

```text
dotnet restore C:\Temp\TLAW-053\ProbeB\repo\src\TheLogsAreWrong.Domain\TheLogsAreWrong.Domain.csproj -p:TargetFramework=netstandard2.1
dotnet build C:\Temp\TLAW-053\ProbeB\repo\src\TheLogsAreWrong.Domain\TheLogsAreWrong.Domain.csproj --configuration Release --no-restore -p:TargetFramework=netstandard2.1
```

The target-only compiler language version resolved to `8.0`:

```text
dotnet msbuild ...TheLogsAreWrong.Domain.csproj -getProperty:LangVersion -p:TargetFramework=netstandard2.1
```

That target-only build exited `1` with `631` syntax/language errors. To separate
language selection from framework compatibility, one additional scratch-only
metadata-only inspection used `-p:LangVersion=latest`; it introduced no source
or dependency change. That build also exited `1`, reporting `575` errors.

### Result

**DISPROVEN.** A clean source-identical `netstandard2.1` Domain DLL was not
produced, so no portable assembly identity or SHA-256 exists.

The exact blocking categories from the preserved `LangVersion=latest` compiler
output are:

- `CS0234`: `System.Collections.Immutable` is unavailable;
- `CS0246`: `ImmutableArray<>`, `ImmutableDictionary<,>`, and
  `ImmutableHashSet<>` are unavailable;
- `CS0518`: `System.Runtime.CompilerServices.IsExternalInit` is unavailable;
- `CS0656`: required compiler members are unavailable:
  `RequiredMemberAttribute..ctor`, `CompilerFeatureRequiredAttribute..ctor`, and
  `SetsRequiredMembersAttribute..ctor`.

The immutable collection requirement needs an additional compatibility package;
the record/init and required-member requirements need shims/polyfills and/or
source-level compatibility work. Both actions are prohibited by this audit.
The probe therefore stopped without adding either. The target-only language
result also means a future multitarget proposal would have to make modern
language selection explicit in its production metadata before it reached these
framework/dependency blockers.

The initial `dotnet build` with an implicit restore did not propagate the target
override into `project.assets.json` and returned `NETSDK1005`; the explicit
targeted restore above corrected that probe setup without changing any input.
That setup error is retained in `Logs\probe-b-netstandard21-build.log` and is
not used as the compatibility result.

## Probe C — Unity Editor/runtime compatibility for a portable candidate

**NOT RUN, as required.** Probe B did not produce a clean, source-identical
`netstandard2.1` DLL. Consequently no portable DLL was imported, no Probe C
representative types were selected, no Unity batch compile/EditMode test ran,
and no Windows `StandaloneWindows64` Development build or player launch was
attempted. The untouched fresh copy remains at `C:\Temp\TLAW-053\ProbeC\repo`.

This audit's only Unity representative type is Probe A's
`TheLogsAreWrong.Domain.Runtime.ShiftRuntimeState`; it is not a Probe C result.

## Option matrix

| Option | Empirical status | Production changes required in a later task | Existing `net10.0` tests/tooling | Duplication / Unity coupling / deterministic-semantics risk | Expected next-task size |
| --- | --- | --- | --- | --- | --- |
| Direct existing `net10.0` binary reference | **PROVEN for Unity Editor batch compile only**; runtime/player behavior remains unprobed | Unity-side import/reference and a bounded runtime validation hook; no Domain source change is evidenced by this audit | Existing Domain tests/tooling remain on the same binary | None / low-to-medium / low if the exact binary is used, but runtime compatibility is not yet proven | Small bounded runtime-and-player validation task before any adoption decision |
| Additive Domain multitargeting (`net10.0` + Unity-compatible target) with unchanged source | **DISPROVEN** for clean `netstandard2.1` under this contract | At minimum targeting metadata plus explicit language metadata; currently also an immutable-collections dependency and record/required-member shims or source work, none of which this task may add | Must retain and run the `net10.0` suite plus target-specific compatibility coverage | None / low / medium-to-high because alternate framework behavior and shims create a second semantic surface | Large; requires an explicit owner decision and a separately scoped compatibility plan |
| Source inclusion/linking into a Unity asmdef | **UNPROBED** | Unity asmdef/project changes and linked/copied Domain source selection; likely build metadata changes | Requires a way to preserve current .NET compilation and tests independently | High / high / high due divergence and Unity compilation context | Large architecture task |
| Extract a portable Domain/core boundary plus adapters | **UNPROBED** | New portable project(s), Domain reference changes, Unity adapter(s), and expanded tests/tooling | Requires regression/replay parity across the extracted boundary | Low-to-medium / managed but nonzero / medium-to-high during extraction | Large architecture task |
| Unity-facing portable contract/facade with adapters | **UNPROBED**; does not itself prove that the authoritative Domain runs in Unity | New contract/facade and adapter projects plus Unity-side consumer code | Requires compatibility, contract, and deterministic parity tests | Medium / medium / medium-to-high | Large architecture task |

An out-of-process host, network stack, FishNet transport, Steamworks, or
separate executable topology is outside scope and is not an option adopted or
recommended here.

## Recommendation

`NO_RECOMMENDATION`.

The direct `net10.0` binary has positive Editor compile evidence, while the
source-identical portable `netstandard2.1` path is blocked by dependencies and
compiler/runtime support that this audit expressly forbids adding. That evidence
does not establish a production runtime/player boundary or select a bridge.
Any future bridge choice requires an explicit owner decision and a bounded
implementation task. No architecture decision is accepted by this task.

## Repository verification

The final candidate was verified after committing with the exact baseline and
head command required by Issue #125. The exact candidate SHA, verifier output,
GitHub workflow/job/artifact/digest, and Draft PR identity are recorded in the
executor handoff and PR evidence because the commit's own SHA cannot be written
inside this object. All required repository checks passed:

| Check | Result |
| --- | --- |
| `git diff --name-only 71f88b2423f199cffbc70bf9ddf005e718838f80...HEAD` | Exactly `docs/agent/GATE2_DOMAIN_UNITY_COMPATIBILITY.md` |
| `git diff --check` | PASS |
| `dotnet build TheLogsAreWrong.sln --configuration Release` | PASS |
| `dotnet test TheLogsAreWrong.sln --configuration Release` | PASS — `1631` passed, `0` failed, `0` skipped |
| `dotnet run --configuration Release --project tools/Tlaw.Verify -- --expected-base 71f88b2423f199cffbc70bf9ddf005e718838f80 --expected-head <candidate>` | PASS |
| Gate 0 / Git object reader | PASS |
| Production `Directory.Build.props`, Domain project/source, and `unity/TheLogsAreWrong/**` | Byte-identical to baseline |

## Changed paths, deviations, and retained evidence

The exact tracked changed-path set is:

```text
docs/agent/GATE2_DOMAIN_UNITY_COMPATIBILITY.md
```

Deviations were limited to the two scratch-only setup corrections described in
the Probe A and Probe B sections. No production source, project, package,
Unity asset, scene, prefab, manifest, tool, decision log, authority model, or
state machine changed.

Retained local evidence includes:

```text
C:\Temp\TLAW-053\baseline-71f88b2423f199cffbc70bf9ddf005e718838f80.zip
C:\Temp\TLAW-053\Logs\probe-a-domain-build.log
C:\Temp\TLAW-053\Logs\probe-a-unity-compile-valid.log
C:\Temp\TLAW-053\Logs\probe-a-unity-compile-valid-exit.txt
C:\Temp\TLAW-053\Logs\probe-b-netstandard21-restore.log
C:\Temp\TLAW-053\Logs\probe-b-netstandard21-build-rerun.log
C:\Temp\TLAW-053\Logs\probe-b-netstandard21-langversion-latest-build.log
C:\Temp\TLAW-053\Logs\probe-b-netstandard21-langversion-latest-build-exit.txt
```

No cleanup is performed pending the separate owner gates.
