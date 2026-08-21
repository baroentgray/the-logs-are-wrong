# Gate 2 production HostSession owner / thin Unity driver — TLAW-071

## Run identity

- Authorized baseline / `origin/main`: `e2e2686fb90f918b1f422e2e01c06bc4f9d35733`.
- Implementation commit: `7091b368216f17b534663b73847df401fb805806`.
- Branch: `task/TLAW-071-single-host-owner-unity-driver`.
- Isolated worktree: `C:\Projects\TheLogsAreWrong-worktrees\TLAW-071`.
- Draft PR: #165, targeting `main`, body exactly `Closes #164`.

## Preserved decision constraints

This increment implements D-021 H1/U2/U3/U4 only. The plain-C# PortableAuthority `HostSession` remains the sole holder of carried shift, quota, noise, lifecycle, journal, event identity and host-stage semantic state. Unity owns lifecycle and scheduling only; it does not recreate the seven-stage `HostTickExecutionService` composition.

`HostTickCadence` remains the existing U2 integer-cadence implementation. C1 remains the D-022 PortableAuthority codec/manifest handoff: canonical YAML, Config.Yaml and YamlDotNet stay outside Unity.

## One owner and lifecycle

`Gate2ProductionHostDriver` is the one production Gate-2 component that creates, holds, resets and disposes the running `HostSession`.

- `Gate2ProductionHostLease` is a process-local lock-protected lease containing only a `Guid` owner identity and synchronization object. It contains no session, configuration, cadence, tick cursor, journal, or authoritative runtime state.
- Acquiring the lease precedes construction. A second owner faults before a second `HostSession` is created; it does not disturb the first owner.
- Startup failure disposes any partially created session and releases an acquired lease. Teardown does the same. A new owner can then acquire.
- Reset disposes the old session and releases its lease before it constructs the replacement. The replacement receives a new cadence and session, so it starts at tick zero and cannot reuse the previous journal/runtime state.
- `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` resets the identity-only lease for Unity domain reload after teardown. The contract tests invoke that hook after disposal.
- A fault disposes/releases and leaves any current due tick unretired. Disposed and faulted owners never pump again.

## Exact clock and cadence pump

`StopwatchElapsedTimeSource` samples monotonic timestamps and converts delta ticks using only integer quotient/remainder arithmetic. It retains the sub-millisecond numerator remainder between samples, preventing repeated truncation loss. `IAuthoritativeElapsedTimeSource` is the narrow injectable time seam used by deterministic tests.

Every production `Update` uses this fixed ordering:

```text
sample integer elapsed
-> HostTickCadence.Accumulate
-> next due tick
-> IAlreadyAdmittedHostInputSource.GetInput
-> HostSession.ExecuteTick exactly once
-> HostTickCadence.RetireNextDueTick for that same tick
-> repeat until due backlog is empty
```

There is no catch-up cap. A long elapsed sample retains then drains the entire range in consecutive order. Input faults or session failures fault the owner; the currently due tick is not retired and the driver does not retry/spin in the frame. Any successful Stage Seven result is accepted without Unity-side stage/result semantics; the existing `HostStageSevenNoNewPublication` HostSession vector remains a successful authoritative result.

### Bounded correction evidence

The production-owner EditMode contracts exercise both required outcomes without introducing a second executor or input-admission path:

- The deterministic empty-input sequence executes tick zero first (`HostStageSevenPublished`) and then executes tick one as the exact `HostStageSevenNoNewPublication` result. The owner stays `Running`; the second successful tick increases the execution count by exactly one; its due cadence entry is retired.
- The value-only test seam can return an intentionally wrong, otherwise constructed already-admitted tick batch. The driver records the non-null evidence delivery before calling the real `HostSession.ExecuteTick`. `HostSession.ValidateContinuity` then throws `ArgumentException` with parameter `acceptedIntents`; the driver becomes `Faulted`, credits no successful tick, and retains the current due tick. This is distinct from the retained existing input-source-throw contract.

## Input and C1 startup

`IAlreadyAdmittedHostInputSource` passes only an `AcceptedIntentTickBatch` plus active-tool evidence to `HostSession`. It performs no gameplay admission, actor authority, ordering, controls, or networking. `EmptyAlreadyAdmittedHostInputSource` is the explicit no-input Gate-2 bootstrap provider.

The existing tracked C1 artifact and manifest remain byte-for-byte unchanged:

- Artifact length: `2326`.
- Artifact SHA-256: `94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541`.
- Canonical projection SHA-256: `4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1`.

Because the two established deployment extensions were not both serializable as Unity `TextAsset`s, the Editor-only `Gate2DeploymentTextImporter` imports their exact raw text as `Gate2DeploymentTextAsset` Unity data. It does not decode, validate, alter, or synthesize C1 content. Runtime `Gate2C1DeploymentStartupSource` calls only `ValidatedConfigurationC1DeploymentManifest.Parse` and `VerifyAndMaterialize`; the PortableAuthority codec remains the sole decoder/materializer. The bootstrap scene serializes the two imported data assets and explicit `learning` profile selection. Missing, malformed/tampered, mismatched, or unknown-profile startup input faults before a session exists.

## Scene and player evidence

Only the existing `Assets/Gate2/Bootstrap/Gate2Bootstrap.unity` was wired, through `Gate2BootstrapAuthoring`. The root has exactly one `Gate2ProductionHostDriver` and exact references to:

- `Assets/Gate2/Configuration/validated-configuration-c1-v1.base64`;
- `Assets/Gate2/Configuration/validated-configuration-c1-v1.manifest`.

The corrected Windows x64 Development build logged `BUILD_RESULT=Succeeded`, `BUILD_ERRORS=0 BUILD_WARNINGS=0`, and build size `146464833`. Player smoke exited after the existing 60-frame marker and logged:

```text
TLAW071_OWNER_ACQUIRED
TLAW071_OWNER_SESSION_CREATED profile=learning
TLAW071_OWNER_START_PASS shift=P0_SHIFT_A
TLAW071_BOOTSTRAP_OWNER_RUNNING shift=P0_SHIFT_A profile=learning
TLAW052_BOOTSTRAP_QUIT frames=60
TLAW071_OWNER_TEARDOWN_PASS
```

The runtime plugin inventory remains exactly three:

1. `TheLogsAreWrong.PortableAuthority.dll` — `BD1E5DDA62192587B12737CCE9BBBB272FB75C4B309BA173AF2AA7684E2A7085`;
2. `System.Collections.Immutable.dll` — `5B1B1C83BA3D135C2FDFE425842FBE9C7432878B7E468623ACB554C69B4C130F`;
3. `System.Runtime.CompilerServices.Unsafe.dll` — `01748200F2400C742AA689F1F5101BD6298EFDFD92C00C18F4FA473847235BA9`.

## Executable evidence

- PortableAuthority Release deployment build: 0 warnings, 0 errors; fresh/plugin SHA identical.
- Full solution Release build: 0 warnings, 0 errors.
- Full .NET suite: `1657/1657` passed.
- D-014 TLAW-046: `87/87` passed.
- TLAW-067 HostSession/EventId slice: `6/6` passed.
- TLAW-068 cadence slice: `10/10` passed.
- TLAW-070 C1 slice: `5/5` passed.
- Repository U3 source/ownership/C1 inventory guard: `2/2` passed.
- Corrected TLAW-071 production-owner EditMode class: `16/16` passed, including the exact no-publication and HostSession-continuity-rejection vectors.
- Unity `6000.3.21f1 (c02631ffc030)` full EditMode suite: `40/40` passed.
- Canonical preserved assertions remain: one tick `287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949`; four tick `C7FEC7BD00DE7D5A92DA0A89A09F61D4B7E4DC905A4F7D35687A8E6460029411`; cadence `A3CFED2906266153792A1B9FFFB2CBE6EE48F450342EF933B9DAD515DD0BADA0`.

## Work deliberately not performed

No PortableAuthority source, HostSession semantics, HostTickCadence semantics, C1 payload/manifest content or codec semantics, PortableAuthority plugin, decision record, package/project setting, prefab, other scene, gameplay input admission/control, UI/audio/presentation, D-016, FishNet/FishySteamworks/Steamworks, RPC/transport/networking, U3 second owner path, Ready, merge, or cleanup was implemented.

```text
PRODUCTION_HOST_OWNER_DRIVER_PASS
D021_U3_SINGLE_OWNER_ENFORCED
PLAIN_CSHARP_HOSTSESSION_REMAINS_AUTHORITY
EXACT_INTEGER_CADENCE_PRESERVED
C1_PRODUCTION_CONFIG_CONSUMED
DUPLICATE_HOST_AUTHORITY_FAILS_CLOSED
GAMEPLAY_INPUT_PLUMBING_NOT_IMPLEMENTED
NETWORKING_NOT_STARTED
```
