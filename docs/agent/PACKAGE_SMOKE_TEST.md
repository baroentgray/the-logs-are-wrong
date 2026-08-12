# TLAW-049 package matrix smoke test

Schema: `tlaw.package-smoke-test/v2`.

Dates: first attempt `2026-08-11` (blocked); resumed and completed `2026-08-12`.

## Scope and identity

| Field | Evidence |
| --- | --- |
| GitHub contract | [Issue #118](https://github.com/baroentgray/the-logs-are-wrong/issues/118) — read in full before execution |
| Resume authorization | [comment 5267165674](https://github.com/baroentgray/the-logs-are-wrong/issues/118#issuecomment-5267165674) |
| Execution resume packet | [comment 5267209964](https://github.com/baroentgray/the-logs-are-wrong/issues/118#issuecomment-5267209964) |
| Baseline required by the contract | `56cd92613b1d8889af652cd8faa056dba05fdf5c` |
| Remote baseline proof | GitHub API `commits/main` and local `git rev-parse origin/main` both returned `56cd92613b1d8889af652cd8faa056dba05fdf5c` |
| Task branch | `task/TLAW-049-network-package-smoke` |
| Task worktree | `C:\Projects\TheLogsAreWrong-worktrees\TLAW-049` |
| Temporary Unity project | `C:\Projects\TheLogsAreWrong-smoke\TLAW-049` |
| Host OS | Windows 11 Pro 24H2, `10.0.26100.9168`, x64 |
| Production diff | `NONE` |
| Frozen Gate-0 files | `UNCHANGED` |

This is an isolated compatibility investigation only. The repository has no Unity, FishNet, FishySteamworks, or
Steamworks.NET production dependency, and no Gate 1, Gate 2, or Gate 3 production code was changed.

## Candidate-only matrix

| Component | Exact candidate | Official source and resolved identity | Installation method | Result |
| --- | --- | --- | --- | --- |
| Unity Editor | `6000.3.21f1` | [Unity release](https://unity.com/releases/editor/whats-new/6000.3.21f1), changeset `c02631ffc030` | Official Windows installer | Installed; `Unity.exe` reports `6000.3.21f1_c02631ffc030` |
| FishNet | `4.7.2` | [FirstGearGames/FishNet](https://github.com/FirstGearGames/FishNet), lightweight tag `4.7.2` → commit `de19b5d66459f60400ffd0edc443c4da173a01e7` | Pinned UPM Git URL `https://github.com/FirstGearGames/FishNet.git?path=Assets/FishNet#4.7.2` | Imported, zero compile errors |
| Steamworks.NET | `2025.164.1` | [rlabrecque/Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET), annotated tag object `d6930827976de076964a97f713fea0b557783a54` → peeled commit `c21a8f0e31c56ae8707130967faf491f7dd7c0d8` | Pinned UPM Git URL `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1` | Imported, zero compile errors |
| FishySteamworks | `4.1.1` | [FishySteamworks release 4.1.1](https://github.com/FirstGearGames/FishySteamworks/releases/tag/4.1.1), tag `4.1.1` → commit `21e858249249e2c322365fe9fefbe865f290b0d9` | Official `FishySteamworks.4.1.1.unitypackage` release asset | Imported, zero compile errors |

The candidates remain **unaccepted**. This table is evidence of the requested matrix, not a pin decision.

## Artifact and tag evidence

- Tag resolution was re-verified against the GitHub API on resume; all three resolutions match the first attempt.
- FishNet `4.7.2` is a lightweight tag pointing directly at commit `de19b5d66459f60400ffd0edc443c4da173a01e7`.
- Steamworks.NET `2025.164.1` is an annotated tag; the tag object is `d6930827976de076964a97f713fea0b557783a54` and the
  peeled commit is `c21a8f0e31c56ae8707130967faf491f7dd7c0d8`. `git ls-remote` reports the tag object, and the Unity
  lockfile records the peeled commit; both are recorded so the two values are not confused later.
- FishySteamworks official release asset `FishySteamworks.4.1.1.unitypackage`, `17,188` bytes, SHA-256
  `5698D16BD29B8B08D35E12A9B817CE69992F70D7C14B64810961691ECD9AFC57`. The size matches the GitHub release asset record
  (`17188`); the release exposes no digest field, so the hash is the independently computed local value.
- The `package.json` carried inside the imported FishySteamworks asset declares `"version": "4.1.1"`, `unity: 2021.3`
  and an empty `dependencies` object. The first-attempt record noted `4.1.0` for the repository-tagged metadata; the
  value recorded here is what the imported release artifact actually contains. Neither value was normalized or edited.
- No floating `main`, `master`, `latest`, or unpinned Git URL appears in the final smoke state.

## Environment blocker on the first attempt (retained incident history)

The `2026-08-11` attempt **stopped before any candidate package was installed**. It is retained here because it is the
reason this task has two execution dates, and because it establishes that the packages were not the cause.

| Step | Outcome on 2026-08-11 |
| --- | --- |
| Fresh project creation | Project created, but the initial `bee_backend.exe` ScriptAssemblies process ended `-1073741819` (`0xC0000005`) after `2s169ms`. |
| Exact-editor retry | Reproducible pre-package failure: `ApiUpdater.MovedFromExtractor` reported `Access is denied.` reading `UnityEngine.UnityWebRequestTextureModule.dll`; a further run exited `0xC0000005` on `UnityEditor.ShaderFoundryModule.dll`. Unity recorded `*** Tundra build failed`, `Scripts have compiler errors.`, `Application will terminate with return code 1`. |
| FishNet / Steamworks.NET / FishySteamworks | Not started — the package-free compile gate had failed, so any package compile evidence would have been non-diagnostic. |

Retained first-attempt logs: `Unity-project-create.log`, `Unity-baseline-retry.log`. The contaminated first-attempt
project tree (whose `ProjectVersion.txt` still read `UnknownUnityVersion`) was preserved intact under
`_prior-blocked-attempt/` rather than deleted, and a clean project was created for the resumed run.

That blocker was diagnosed and remediated separately under TLAW-050 / [Issue #119](https://github.com/baroentgray/the-logs-are-wrong/issues/119),
which closed as `TLAW_050_ENVIRONMENT_REMEDIATED`. No workstation or security diagnosis was repeated in this task.

## Resumed preflight

| Check | Result |
| --- | --- |
| `origin/main` equals the contract baseline | PASS — `56cd92613b1d8889af652cd8faa056dba05fdf5c` |
| Retained worktree on `task/TLAW-049-network-package-smoke` | PASS — HEAD at baseline, only the untracked evidence document present |
| No colliding TLAW-049 remote branch or PR | PASS — no `049` remote branch; newest PR was #117 (TLAW-048, merged) |
| Exact Unity `6000.3.21f1` installed | PASS — `C:\Program Files\Unity 6000.3.21f1\Editor\Unity.exe`, `6000.3.21f1_c02631ffc030` |
| Windows x64 build support | PASS — `windowsstandalonesupport` playback engine present |
| Steam client | PASS — installed and running |
| Clean pre-package Unity compile | PASS — see below |

The pre-package compile gate is the exact step that failed on 2026-08-11. On resume it completed with exit code `0`,
produced `Assembly-CSharp.dll`, and the previously failing steps `MovedFromExtractor-Combine` and
`Csc …/Assembly-CSharp.dll` both succeeded. No `0xC0000005`, `Access is denied`, `Tundra build failed`, or
`Scripts have compiler errors` signature appeared. Log: `Unity-resume-create.log`, `Unity-resume-baseline-compile.log`.

## Package installation and compile results

Installed strictly in the contract order. The zero-C#-compile-error gate was required and met before each next package.

| # | Package | Import | Compile errors | Compile warnings | Native/environment failures |
| --- | --- | --- | --- | --- | --- |
| 1 | FishNet `4.7.2` | Resolved to `Library/PackageCache/com.firstgeargames.fishnet@0728292d8339` | **0** | **0** | none |
| 2 | Steamworks.NET `2025.164.1` | Resolved to `Library/PackageCache/com.rlabrecque.steamworks.net@6fb66c768572` | **0** | **0** | none |
| 3 | FishySteamworks `4.1.1` | Imported from the official `.unitypackage` into `Assets/FishNet/Plugins/FishySteamworks/` | **0** | **0** | none |

Assemblies produced after step 3 include `FishNet.Runtime.dll` (921,600 bytes),
`com.rlabrecque.steamworks.net.dll` (425,984 bytes), and `Assembly-CSharp.dll` (25,600 bytes; FishySteamworks ships
without an assembly definition and therefore compiles into `Assembly-CSharp`).

**The anticipated API incompatibility did not occur.** FishySteamworks `4.1.1` (upstream unchanged since 2024-08-26)
compiles cleanly against FishNet `4.7.2` and Steamworks.NET `2025.164.1` on Unity `6000.3.21f1`, with zero errors and
zero warnings. Logs: `Unity-install-1-fishnet.log`, `Unity-install-2-steamworks.log`,
`Unity-install-3-fishysteamworks.log`, `Unity-compile-3-fishysteamworks.log`.

## Final package state

| Snapshot | SHA-256 |
| --- | --- |
| `Packages/manifest.json` | `BDFEC1535D42C223CCA6BA3A438673B9B15A961495F159A9116154D8FC988D0D` |
| `Packages/packages-lock.json` | `D055DE1F936D17FD5D88D0D9380362BAAB33545C225629AF54040928B9F1CB11` |

Manifest candidate entries (both pinned to an exact tag):

```text
com.firstgeargames.fishnet     = https://github.com/FirstGearGames/FishNet.git?path=Assets/FishNet#4.7.2
com.rlabrecque.steamworks.net  = https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1
```

Lockfile resolution (41 locked packages total):

| Package | Source | Resolved hash |
| --- | --- | --- |
| `com.firstgeargames.fishnet` | git | `de19b5d66459f60400ffd0edc443c4da173a01e7` |
| `com.rlabrecque.steamworks.net` | git | `c21a8f0e31c56ae8707130967faf491f7dd7c0d8` |
| `com.unity.nuget.newtonsoft-json` | registry | `3.2.2` (transitive FishNet dependency) |

Both git hashes equal the independently resolved upstream tag commits recorded above. FishySteamworks is an
`Assets/`-imported `.unitypackage` and therefore correctly has no lockfile entry.

## Component wiring

Performed through the editor with a minimal generated scene (`Assets/Smoke/Smoke.unity`); no game code was copied in.

| Requirement | Result |
| --- | --- |
| FishNet `NetworkManager` can be created | PASS |
| `TransportManager` present | PASS |
| `FishySteamworks.FishySteamworks` type resolves | PASS |
| FishySteamworks component attaches | PASS — no serialization or missing-script error |
| FishySteamworks selected as active transport | PASS — `TransportManager.Transport` = `FishySteamworks.FishySteamworks` |

### Transport configuration finding

With the transport left at its shipped defaults (`_peerToPeer = false`, `_serverBindAddress = ""`),
`ServerManager.StartConnection()` returned `false` and **no error surfaced**: `ServerSocket.StartConnection` catches its
exception and returns `false` silently, and FishNet's `LogError` path produced no Unity log entry. Enabling the
transport's documented Steam P2P mode (`_peerToPeer = true`) made the server start immediately.

This is a configuration requirement, not a package defect — in the default IP path FishySteamworks calls
`SteamNetworkingSockets.CreateListenSocketIP`, which is not the supported Steam client-app path. No package source was
patched, forked, downgraded, or substituted; only a serialized Inspector field on the transport component was set.

The silent failure mode is recorded here because it is a real ergonomics hazard for the later Gate-3 work: a
misconfigured FishySteamworks server fails with no diagnostic output at all.

## Runtime smoke — Windows x64 Development build, Steam running, App ID `480`

Executed in the built player (not the editor), which exercises the native plugin path at the same time.

| Requirement | Result |
| --- | --- |
| Steamworks.NET initializes with App ID `480` | PASS — `SteamAPI.InitEx` returned `k_ESteamAPIInitResult_OK`, empty error message |
| Reported App ID | PASS — `480` |
| Steam user logged on | PASS |
| Native `steam_api64.dll` load | PASS — `NATIVE_STEAM_API_LOADED=True`; the plugin shipped to `TLAW049Smoke_Data/Plugins/x86_64/steam_api64.dll` (317,080 bytes) |
| `NetworkManager` found and initialized | PASS |
| Active transport at runtime | PASS — `FishySteamworks.FishySteamworks` |
| Server starts | PASS — `StartConnection` returned `true`, `ServerManager.Started = true` within 0 frames, transport server state `Started` |
| Same-process client/host starts | PASS — `ClientManager.Started = true` within 1 frame, `SAME_PROCESS_HOST = true` |
| Short run without error spam | PASS — `ERRORS_DURING_STEADY_RUN = 0` over a 3-second steady run |
| Clean stop/shutdown | PASS — client and server both stopped, `CLEAN_SHUTDOWN = true`, `SteamAPI.Shutdown` ok |
| Total runtime errors/exceptions | PASS — `TOTAL_ERRORS = 0` |
| Player exit code | PASS — `0` |

Recorded nuance: `Transport.GetConnectionState(false)` reported `Stopped` while `ClientManager.Started` was `true`. This
is correct client-host behaviour, not a defect — `GetConnectionState(false)` returns the *remote* Steam client socket
(`_client`), while in host mode FishySteamworks routes the local client through `ClientHostSocket` via
`_clientHost.StartConnection(_server)`. FishNet's `ClientManager.Started` is the authoritative host-client state.

Logs: `TLAW049-smoke-result.log` (structured smoke result), `Player-runtime.log`.

## Windows build smoke

| Field | Value |
| --- | --- |
| Target | `StandaloneWindows64` |
| Options | `BuildOptions.Development` |
| Scripting backend | `Mono2x` — the fresh project's default; **not** overridden to obtain a pass |
| Build result | `Succeeded` |
| Build errors / warnings | `0` / `0` |
| Build size | `152,651,854` bytes |
| Output | `C:\Projects\TheLogsAreWrong-smoke\TLAW-049\Build\TLAW049Smoke.exe` |

Log: `Unity-build-windows.log`.

## Warnings and known limitations

- One unrelated non-fatal player log line, `d3d12: failed to query info queue interface (0x80004002)`, is a headless
  D3D12 debug-layer message on this machine and is not package-related.
- The smoke exercised a **single process**. No two-process, two-account, lobby, invite, latency, reconnect, or host
  migration behaviour was tested; those are Gate-3 concerns and are explicitly out of scope.
- FishySteamworks `4.1.1` has had no substantive upstream release since 2024-08-26. This smoke proves the exact matrix
  compiles and runs today; it does not promise future maintenance.
- The silent server-start failure under default transport configuration (above) should be treated as a known trap.

## Verdict

All required rows — package import and compile, component wiring, one-process host lifecycle, and Windows x64
build with native Steamworks load — passed on the exact candidate matrix.

This verdict **does not accept the package pins**. It only makes the exact matrix eligible for a separate owner
pin-acceptance step. Neither frozen `GATE_0_EXIT_CHECKLIST.md` network row was edited. Gate 2 and Gate 3 were not
started, and `docs/agent/DECISIONS.md` was not modified.

PACKAGE_MATRIX_SMOKE_PASS
