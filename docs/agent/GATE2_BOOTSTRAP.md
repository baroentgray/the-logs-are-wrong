# Gate 2 bootstrap evidence

Schema: `tlaw.gate2-bootstrap/v1`.

Date: `2026-08-12`.

## Identity

| Field | Value |
| --- | --- |
| GitHub contract | [Issue #123](https://github.com/baroentgray/the-logs-are-wrong/issues/123) |
| Baseline exact main | `d046f89b851a83a6c2abd7eee8784adea528529c` |
| Candidate | this commit — the head of `task/TLAW-052-gate2-unity-bootstrap`; the exact SHA is recorded in the PR and the executor return, since a commit cannot contain its own hash |
| Superseded candidates | `9c77831fbc117811bdb66033b53cf3db3da48309` — pre-amendment, failed the verifier's diff-check lane; `38d5ddf4b4f8375b493578b0c24428809cebb288` — amended and fully green, superseded only by the docs-only changed-path-count correction below |
| Branch | `task/TLAW-052-gate2-unity-bootstrap` |
| Worktree | `C:\Projects\TheLogsAreWrong-worktrees\TLAW-052` |
| Unity project | `unity/TheLogsAreWrong` |
| Host OS | Windows 11 Pro 24H2, `10.0.26100.9168`, x64 |

This is the first Gate-2 implementation increment. It establishes a committable, openable, testable and
buildable Unity shell. It claims no Gate-2 acceptance criterion is complete.

## Unity editor identity

| Field | Value |
| --- | --- |
| Executable | `C:\Program Files\Unity 6000.3.21f1\Editor\Unity.exe` |
| Reported product version | `6000.3.21f1_c02631ffc030` |
| `ProjectSettings/ProjectVersion.txt` | `m_EditorVersion: 6000.3.21f1` / `m_EditorVersionWithRevision: 6000.3.21f1 (c02631ffc030)` |

This matches the D-017 accepted editor pin `6000.3.21f1` / `c02631ffc030` exactly. The identity file was written
by the editor during project creation and was not hand-edited.

## Project creation

A bare project was created with the pinned editor directly:

```text
Unity.exe -batchmode -nographics -quit -createProject unity/TheLogsAreWrong -logFile <log>
```

Exit code `0`. No render-pipeline or art template was selected: the resulting manifest contains no
`com.unity.render-pipelines.*`, no URP, no HDRP and no Shader Graph, so this increment makes no rendering
decision.

## Bootstrap scene

One task-owned scene: `Assets/Gate2/Bootstrap/Gate2Bootstrap.unity`, authored by the pinned editor rather than
hand-written YAML (`Gate2BootstrapAuthoring.CreateBootstrapScene`, exit code `0`, `SCENE_SAVED=True`).

Contents are deliberately primitive and non-gameplay:

| Object | Purpose |
| --- | --- |
| `Gate2BootstrapRoot` | scene root, carries the `Gate2BootstrapRoot` marker component |
| `Gate2BootstrapCamera` | one camera |
| `Gate2BootstrapLight` | one directional light |
| `Gate2BootstrapFloor` | one primitive plane as reference surface |

`Gate2BootstrapRoot.cs` is a marker only: it logs a startup line and, when the built player is launched with
`-tlaw-bootstrap-smoke`, quits after 60 frames so the build smoke can exit cleanly instead of being killed. It
holds no simulation state, reads no gameplay input, and references no Domain, networking or presentation
contract.

No station layout, production machinery, interaction logic, input mapping, art, UI or audio is present.

## Package boundary

| Snapshot | SHA-256 |
| --- | --- |
| `Packages/manifest.json` | `56494B2AC7B2B44A4A7A886A465803B99E65E57AFA7F21604A1D81A88E71E30B` |
| `Packages/packages-lock.json` | `94A75F033C00CA18D15A247C79CBC3C7786BA11D232D0910771B1EEB98DC5D02` |

Both files use LF line endings, so these digests match the committed blobs as well as the working tree. The
Unity EditMode suite was re-run after the normalization and still reports 5/5 passed with the manifest
unchanged by the editor.

Non-module manifest dependencies — exactly one:

```text
com.unity.test-framework   1.6.0
```

Lockfile: 38 resolved entries; the only non-module entries are `com.unity.test-framework` `1.6.0` and its
transitive `com.unity.ext.nunit` `2.0.5`. Everything else is a built-in `com.unity.modules.*` module.

Two deliberate, contract-driven package changes were made to the bare template:

1. **Added `com.unity.test-framework`** — §5.3 explicitly requires Unity Test Framework coverage; the bare
   template does not include it.
2. **Removed `com.unity.multiplayer.center` `1.0.1`** — the bare template's only non-module dependency. It is a
   Unity editor recommendation surface rather than a transport, but §6 requires Gate 2 to remain network-free
   and §6 also limits the project to the minimal packages required by the bare project, tests and
   editor/build operation. Nothing in this increment needs it.

Neither change selects a rendering, networking or architecture direction.

```text
NETWORK_PACKAGES_PRESENT = false
```

No FishNet / `com.firstgeargames.fishnet`, FishySteamworks, Steamworks.NET /
`com.rlabrecque.steamworks.net`, `com.unity.netcode`, `com.unity.transport`, Mirror or any other gameplay
networking or transport package appears in either file. Nothing was copied from the TLAW-049 smoke project.

Note for reviewers: `ProjectSettings/MultiplayerManager.asset` is a stock Unity 6 project-settings asset
emitted by the editor for every project. It is editor-generated settings state under an authorized tracked
path, not a package, and its presence does not indicate a networking dependency.

## Unity compile

```text
Unity.exe -batchmode -nographics -quit -projectPath unity/TheLogsAreWrong -logFile <log>
```

| Field | Result |
| --- | --- |
| Exit code | `0` |
| C# compile errors | `0` |
| C# compile warnings | `0` |
| Native/environment failures | none — no `0xC0000005`, `Access is denied`, `Tundra build failed`, or `Scripts have compiler errors` |

Assemblies produced: `Assembly-CSharp.dll`, `Assembly-CSharp-Editor.dll`,
`TheLogsAreWrong.Gate2.Tests.Editor.dll`, plus the test-framework assemblies.

## Unity automated test

```text
Unity.exe -batchmode -nographics -runTests -projectPath unity/TheLogsAreWrong
          -testPlatform EditMode -testResults <xml> -logFile <log>
```

Exit code `0`. Result `Passed` — **5 passed / 0 failed / 0 skipped / 0 inconclusive**.

| Test | Proves |
| --- | --- |
| `Bootstrap_scene_asset_exists` | the scene asset exists on disk and is registered in the AssetDatabase |
| `Bootstrap_scene_opens_and_contains_the_root` | the scene opens as a valid loaded scene and `Gate2BootstrapRoot` is present with its marker component |
| `Bootstrap_objects_have_no_missing_scripts` | every component on every task-owned bootstrap object resolves (no missing scripts) |
| `Gate2_project_declares_no_networking_package` | neither `manifest.json` nor `packages-lock.json` declares a networking package |
| `Gate2_loads_no_networking_assembly` | no FishNet/FishySteamworks/Steamworks/Netcode/Mirror assembly is loaded in the editor domain |

The suite is a narrowly scoped EditMode assembly. PlayMode was not required to prove the accepted bootstrap
contract. The tests depend on no FishNet, Steamworks, network session or external service and run offline.

## Windows x64 Development build

```text
Unity.exe -batchmode -nographics -quit -projectPath unity/TheLogsAreWrong
          -executeMethod TheLogsAreWrong.Gate2.EditorTools.Gate2BuildEntry.BuildWindows64Development
```

| Field | Value |
| --- | --- |
| Target | `StandaloneWindows64` |
| Options | `BuildOptions.Development` |
| Build result | `Succeeded` |
| Build errors / warnings | `0` / `0` |
| Build size | `145,571,347` bytes |
| Output | `unity/TheLogsAreWrong/Build/TheLogsAreWrongGate2Bootstrap.exe` (ignored path) |

## Player launch/exit smoke

The built player was launched with `-tlaw-bootstrap-smoke`.

| Field | Result |
| --- | --- |
| Player exit code | `0` |
| Wall time | ~5 s |
| Startup marker | `TLAW052_BOOTSTRAP_STARTED scene=Gate2Bootstrap smokeMode=True unity=6000.3.21f1` |
| Clean-exit marker | `TLAW052_BOOTSTRAP_QUIT frames=60` |
| Errors / exceptions in player log | none |
| Steam client required | no |
| Networking package required | no |

## Repository hygiene

`.gitignore` required **no change**. The existing rules already cover everything this project generates:

| Generated path | Ignored by |
| --- | --- |
| `unity/TheLogsAreWrong/Library` | `.gitignore:25` `[Ll]ibrary/` |
| `unity/TheLogsAreWrong/Temp` | `.gitignore:26` `[Tt]emp/` |
| `unity/TheLogsAreWrong/UserSettings` | `.gitignore:27` `UserSettings/` |
| `unity/TheLogsAreWrong/Logs` | `.gitignore:32` `[Ll]ogs/` |
| `unity/TheLogsAreWrong/Build` | `.gitignore:21` `[Bb]uild/` |

Because no `.gitignore` rule was added, there is no risk of a new rule shadowing the tracked root
`TheLogsAreWrong.sln`; that file remains tracked and visible.

### Unity YAML whitespace exemption (contract amendment)

The first candidate `9c77831f` failed the verifier's diff-check lane with **296** trailing-whitespace findings:
274 in `.asset`, 21 in `.meta`, 1 in `.unity`, and **0 in any hand-written file**. All of them are Unity's own
canonical serialization of empty keys (`userData: `, `assetBundleName: `, `assetBundleVariant: `, `m_Name: `),
which the editor regenerates on every save. Stripping them would be undone by the next editor run and would
mean hand-editing editor-generated files, which §5.1 forbids.

The control-center amendment on Issue #123 added `.gitattributes` to the authorized paths for exactly three
scoped rules, and explicitly refused the broader `unity/TheLogsAreWrong/** -whitespace` form because it would
also silence future hand-written sources:

```gitattributes
unity/TheLogsAreWrong/**/*.asset -whitespace
unity/TheLogsAreWrong/**/*.meta -whitespace
unity/TheLogsAreWrong/**/*.unity -whitespace
```

Resolution proof via `git check-attr whitespace`:

| Path | `whitespace` |
| --- | --- |
| `unity/…/ProjectSettings/ProjectSettings.asset` | `unset` (exempt) |
| `unity/…/Assets/Gate2.meta` | `unset` (exempt) |
| `unity/…/Assets/Gate2/Bootstrap/Gate2Bootstrap.unity` | `unset` (exempt) |
| `unity/…/Assets/Gate2/Bootstrap/Gate2BootstrapRoot.cs` | `unspecified` (still checked) |
| `unity/…/Assets/Gate2/Tests/Editor/TheLogsAreWrong.Gate2.Tests.Editor.asmdef` | `unspecified` (still checked) |
| `unity/…/Packages/manifest.json` | `unspecified` (still checked) |
| `src/TheLogsAreWrong.Domain/Runtime/ShiftRuntimeState.cs` | `unspecified` (still checked) |

Only whitespace linting is affected. No `text`/`eol`/LFS behaviour changed, no other `.gitattributes` entry was
touched, `tools/**` was not modified, and `Tlaw.Verify` was not weakened or replaced.

No Unity-generated IDE solution or project files exist under `unity/**`: the project carries no
`com.unity.ide.*` package, so the editor generated none. No crash dumps or editor-local state were produced.
Nothing under `Library/`, `Temp/`, `Logs/`, `UserSettings/` or `Build/` is tracked.

Editor, test, build and player logs were kept outside the repository; their local paths are returned in the
executor handoff rather than pasted here.

## Changed paths

```text
.gitattributes
docs/agent/GATE2_BOOTSTRAP.md
unity/TheLogsAreWrong/Assets/Gate2.meta
unity/TheLogsAreWrong/Assets/Gate2/Bootstrap.meta
unity/TheLogsAreWrong/Assets/Gate2/Bootstrap/Gate2Bootstrap.unity
unity/TheLogsAreWrong/Assets/Gate2/Bootstrap/Gate2Bootstrap.unity.meta
unity/TheLogsAreWrong/Assets/Gate2/Bootstrap/Gate2BootstrapRoot.cs
unity/TheLogsAreWrong/Assets/Gate2/Bootstrap/Gate2BootstrapRoot.cs.meta
unity/TheLogsAreWrong/Assets/Gate2/Editor.meta
unity/TheLogsAreWrong/Assets/Gate2/Editor/Gate2BootstrapAuthoring.cs
unity/TheLogsAreWrong/Assets/Gate2/Editor/Gate2BootstrapAuthoring.cs.meta
unity/TheLogsAreWrong/Assets/Gate2/Editor/Gate2BuildEntry.cs
unity/TheLogsAreWrong/Assets/Gate2/Editor/Gate2BuildEntry.cs.meta
unity/TheLogsAreWrong/Assets/Gate2/Tests.meta
unity/TheLogsAreWrong/Assets/Gate2/Tests/Editor.meta
unity/TheLogsAreWrong/Assets/Gate2/Tests/Editor/Gate2BootstrapSmokeTests.cs
unity/TheLogsAreWrong/Assets/Gate2/Tests/Editor/Gate2BootstrapSmokeTests.cs.meta
unity/TheLogsAreWrong/Assets/Gate2/Tests/Editor/TheLogsAreWrong.Gate2.Tests.Editor.asmdef
unity/TheLogsAreWrong/Assets/Gate2/Tests/Editor/TheLogsAreWrong.Gate2.Tests.Editor.asmdef.meta
unity/TheLogsAreWrong/Packages/manifest.json
unity/TheLogsAreWrong/Packages/packages-lock.json
unity/TheLogsAreWrong/ProjectSettings/AudioManager.asset
unity/TheLogsAreWrong/ProjectSettings/ClusterInputManager.asset
unity/TheLogsAreWrong/ProjectSettings/DynamicsManager.asset
unity/TheLogsAreWrong/ProjectSettings/EditorBuildSettings.asset
unity/TheLogsAreWrong/ProjectSettings/EditorSettings.asset
unity/TheLogsAreWrong/ProjectSettings/GraphicsSettings.asset
unity/TheLogsAreWrong/ProjectSettings/InputManager.asset
unity/TheLogsAreWrong/ProjectSettings/MemorySettings.asset
unity/TheLogsAreWrong/ProjectSettings/MultiplayerManager.asset
unity/TheLogsAreWrong/ProjectSettings/NavMeshAreas.asset
unity/TheLogsAreWrong/ProjectSettings/Physics2DSettings.asset
unity/TheLogsAreWrong/ProjectSettings/PresetManager.asset
unity/TheLogsAreWrong/ProjectSettings/ProjectSettings.asset
unity/TheLogsAreWrong/ProjectSettings/QualitySettings.asset
unity/TheLogsAreWrong/ProjectSettings/TagManager.asset
unity/TheLogsAreWrong/ProjectSettings/TimeManager.asset
unity/TheLogsAreWrong/ProjectSettings/UnityConnectSettings.asset
unity/TheLogsAreWrong/ProjectSettings/VFXManager.asset
unity/TheLogsAreWrong/ProjectSettings/VersionControlSettings.asset
unity/TheLogsAreWrong/ProjectSettings/ProjectVersion.txt
unity/TheLogsAreWrong/ProjectSettings/SceneTemplateSettings.json
```

That is **42** changed paths in total: `.gitattributes`, this document, 17 under `Assets/`, 2 under `Packages/`
and 21 under `ProjectSettings/` (19 `.asset` plus `ProjectVersion.txt` and `SceneTemplateSettings.json`).

All are inside the §4 authorized set as amended — `.gitattributes` is authorized only for the three scoped
Unity-serialization rules above. `src/**`, `tests/**`, `data/**`, `Directory.Build.props`,
`TheLogsAreWrong.sln`, `global.json`, `tools/**`, `.github/**` and every frozen Gate-0 file are unchanged.

## Existing .NET solution

| Check | Result |
| --- | --- |
| `dotnet build TheLogsAreWrong.sln --configuration Release` | 0 warnings / 0 errors |
| `dotnet test TheLogsAreWrong.sln --configuration Release` | `1631` passed / `0` failed / `0` skipped |
| `git diff --check` | clean, exit 0 |
| `Tlaw.Verify` exact base/head | PASS |
| Gate 0 | PASS; Git object reader `52/52` PASS |

The Domain test count did not decrease from the accepted baseline. `TheLogsAreWrong.Domain` is untouched and
remains `net10.0`.

## Deviations

- `.gitignore` was **not** modified. §4 authorizes scoped Unity exclusions "required by this task"; the existing
  rules already cover every generated path, so no rule was required and none was added.
- Two package changes were made relative to the bare template — adding `com.unity.test-framework` and removing
  `com.unity.multiplayer.center` — both justified above and recorded rather than made silently.
- `.gitattributes` was changed under an explicit **contract amendment** to §4 (Issue #123, control-center
  comment `5271997049`), limited to the three scoped Unity-serialization rules. The first candidate
  `9c77831f` was reported `BLOCKED` rather than resolving this silently; the broader rule form was refused by
  the control centre and is not used.

- Control-center pre-review returned `PRE_REVIEW_BLOCKED_EVIDENCE_PATH_COUNT` against candidate `38d5ddf4`: this
  document recorded `ProjectSettings/*.asset (18 files)` while the changed-path set contains **19**. The 19 paths
  are now enumerated explicitly instead of summarised by count, which removes the ambiguity that caused the
  defect. The total of 42 changed paths was correct throughout and is unchanged. This correction is documentation
  only — no Unity input, package, `.gitattributes`, code, verifier or scope change accompanied it.

### Evidence reuse after the amendment

The amended commit changes only `.gitattributes` and this document. Neither is an input to the Unity editor
lanes: no file under `unity/TheLogsAreWrong/Assets/**`, `Packages/**` or `ProjectSettings/**` changed, and
`.gitattributes` affects only Git's whitespace linting, not Unity's compile, test, build or player behaviour.
The Unity compile, EditMode test, Windows build and player launch/exit evidence above is therefore **reused
unchanged from candidate `9c77831f`**, and this reuse is stated explicitly as the amendment requires. The
`git diff --check`, `Tlaw.Verify`, and .NET build/test lanes were re-run in full against the amended candidate.

## Explicitly not performed

- No Domain retargeting, no Domain source referenced or copied into Unity, no Domain/Unity bridge architecture
  decision.
- No `Directory.Build.props` or target-framework change.
- No FishNet, FishySteamworks, Steamworks.NET, NGO, Mirror or any networking stack.
- No local authoritative host, no actor-intent emulation.
- No Resin `nearest_line_button` locking; no containment `forced_line_pause`. Both remain open Gate-2
  obligations under D-016 and the Gate-1 exit audit, and are outside this bootstrap increment.
- No intake/saw/jam/repair presentation.
- No URP/HDRP or rendering architecture choice.
- No art, imported production assets, final UI/audio or gameplay input.
- No frozen Gate-0 or design-contract modification.
- No Grok invocation, no Ready, no merge, no branch/worktree/evidence cleanup.
