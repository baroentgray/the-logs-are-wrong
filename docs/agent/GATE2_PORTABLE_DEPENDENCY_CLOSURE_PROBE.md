# Gate 2 portable dependency-closure probe

Schema: tlaw.gate2-portable-dependency-closure-probe/v1.

Date: 2026-08-14.

## Scope and authority

| Field | Value |
| --- | --- |
| GitHub contract | [Issue #135](https://github.com/baroentgray/the-logs-are-wrong/issues/135) |
| Exact baseline | 77eeb26308b047abc53fd399a36ad2e0d39e7c6f |
| Branch | task/TLAW-058-portable-dependency-closure-probe |
| Worktree | C:\Projects\TheLogsAreWrong-worktrees\TLAW-058 |
| Scratch root | C:\Temp\TLAW-058 |
| .NET SDK | 10.0.103 |
| Unity | 6000.3.21f1, changeset c02631ffc030 |
| Unity executable | C:\Program Files\Unity 6000.3.21f1\Editor\Unity.exe |

This is a scratch-only bounded compatibility probe. It does not accept
Candidate A or B, a production Domain-to-Unity architecture, migration, bridge,
facade, adapter, host/tick integration, gameplay work, D-016, networking,
FishNet, Steamworks, Ready, merge, or cleanup.

## Exact authoritative cut and mechanical reconstruction

The reconstruction copied these exact 26 baseline Domain files before any
change:

~~~text
src/TheLogsAreWrong.Domain/Anomalies/AnomalyResolutionContracts.cs
src/TheLogsAreWrong.Domain/Anomalies/ConfirmationTestContracts.cs
src/TheLogsAreWrong.Domain/Configuration/ValidatedConfiguration.cs
src/TheLogsAreWrong.Domain/Containment/ContainmentLifecycleContracts.cs
src/TheLogsAreWrong.Domain/Enums/DomainEnums.cs
src/TheLogsAreWrong.Domain/Events/EventContracts.cs
src/TheLogsAreWrong.Domain/Identifiers/Identifiers.cs
src/TheLogsAreWrong.Domain/Intents/IntentContracts.cs
src/TheLogsAreWrong.Domain/Line/LineJamRepairContracts.cs
src/TheLogsAreWrong.Domain/Line/LineNoiseRuntimeContracts.cs
src/TheLogsAreWrong.Domain/Line/MovementNoiseRuntimeContracts.cs
src/TheLogsAreWrong.Domain/Logs/LogTransitionPolicy.cs
src/TheLogsAreWrong.Domain/Primitives/Primitives.cs
src/TheLogsAreWrong.Domain/Quota/QuotaContracts.cs
src/TheLogsAreWrong.Domain/Runtime/ConfirmationTestLifecycleContracts.cs
src/TheLogsAreWrong.Domain/Runtime/LogTransitionServices.cs
src/TheLogsAreWrong.Domain/Runtime/ProcedureActionLifecycleContracts.cs
src/TheLogsAreWrong.Domain/Runtime/ProcedureCompletionContracts.cs
src/TheLogsAreWrong.Domain/Runtime/ShiftRuntimeState.cs
src/TheLogsAreWrong.Domain/Scheduler/DefaultIntakeAutoRouteContracts.cs
src/TheLogsAreWrong.Domain/Scheduler/FeedDueResolutionContracts.cs
src/TheLogsAreWrong.Domain/Scheduler/FeedPlanningContracts.cs
src/TheLogsAreWrong.Domain/Scheduler/IntakeDeadlineContracts.cs
src/TheLogsAreWrong.Domain/Scheduler/RepairPendingTransitionExecutionContracts.cs
src/TheLogsAreWrong.Domain/Scheduler/SawCycleContracts.cs
src/TheLogsAreWrong.Domain/Time/SimulationTime.cs
~~~

The scratch source-copy manifest records 26 of 26 matching baseline and
pre-transform SHA-256 values, with zero mismatches. Its SHA-256 is
3AD9BD0AA479989FDFCA336CC8FC0D4FE62B52A4D107D64BAAEE16A55BE0E80C.

The mandatory transformation manifest records source path, original offset,
line, before text, and after text. Its replay validator rebuilds every
transformed scratch file from its exact baseline content and passed with zero
unapproved deltas.

| Authorized source delta | Expected | Actual |
| --- | ---: | ---: |
| ArgumentNullException.ThrowIfNull to explicit equivalent null guard | 131 | 131 |
| generic Enum.IsDefined to Enum.IsDefined(Type, object) | 25 | 25 |
| generic Enum.GetValues to type-preserving non-generic cast | 1 | 1 |
| Total | 157 | 157 |

The 157 transformations span 19 of the 26 files. Transformation manifest
SHA-256: 46092A222600DE7E64CD030E9FFF656E5EE9F0309F15981C328B5C1C4F061059.
Validator SHA-256:
E5469658455D8A8868EE5042504C1848784BB44C7E9301602276A76E5AD7EC5B.

The scratch project targets netstandard2.1, uses LangVersion=latest, and has
exactly one direct PackageReference:

~~~text
System.Collections.Immutable 8.0.0
~~~

Its only scratch compiler metadata definitions are:

1. System.Runtime.CompilerServices.IsExternalInit.
2. System.Runtime.CompilerServices.RequiredMemberAttribute.
3. System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute.
4. System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute.

No other portability transformation, semantic rewrite, source shim, source
generator, conditional source behavior, or production source change occurred.

## Fresh restore and resolved official closure

The portable project was freshly restored from
https://api.nuget.org/v3/index.json with --force-evaluate --no-cache before any
Unity dependency copy. The netstandard2.1 project.assets.json resolved exactly
five packages:

| Package/version | Declared dependency edges | Selected runtime asset | Copied to Unity |
| --- | --- | --- | --- |
| System.Collections.Immutable 8.0.0 | System.Memory 4.5.5; System.Runtime.CompilerServices.Unsafe 6.0.0 | lib/netstandard2.0/System.Collections.Immutable.dll | Yes, fixed root |
| System.Buffers 4.5.1 | none | lib/netstandard2.0/System.Buffers.dll | No |
| System.Memory 4.5.5 | System.Buffers 4.5.1; System.Numerics.Vectors 4.4.0; System.Runtime.CompilerServices.Unsafe 4.5.3 | lib/netstandard2.0/System.Memory.dll | No |
| System.Numerics.Vectors 4.4.0 | none | lib/netstandard2.0/System.Numerics.Vectors.dll | No |
| System.Runtime.CompilerServices.Unsafe 6.0.0 | none | lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll | Yes, one justified addition |

The fixed root asset had the required identity:

~~~text
System.Collections.Immutable, Version=8.0.0.0, Culture=neutral,
PublicKeyToken=b03f5f7f11d50a3a
~~~

Its SHA-256 was the required
5B1B1C83BA3D135C2FDFE425842FBE9C7432878B7E468623ACB554C69B4C130F.
The full resolved graph, versions, runtime assets, identities, hashes, and
edges are in scratch evidence:

~~~text
C:\Temp\TLAW-058\evidence\resolved-dependency-graph.json
SHA-256: 1A1D3E7DC76E97F272FFAA902DB60A6DDDBEFA8D7BC5743BD7AEEA78D7734A68
~~~

The portable build passed with zero warnings and zero errors. Its output
identity is TheLogsAreWrong.PortableAuthority.Tlaw058, Version=1.0.0.0,
Culture=neutral, PublicKeyToken=null. Its SHA-256 is
4D81928BE17EF7860B5F5643504B7D07CA43DCA52DD30A8AEB37EC75068A7BB9.

## Pinned Unity load gate

The harness is a scratch mechanical copy of the exact-baseline Gate-2
bootstrap. ProjectVersion.txt was byte-identical with SHA-256
C996A482FD926D9F094CFB99105C9319B992208C119A36427312AB8A04D25A42.
Packages/manifest.json was byte-identical with SHA-256
56494B2AC7B2B44A4A7A886A465803B99E65E57AFA7F21604A1D81A88E71E30B.
No Unity project setting, API compatibility setting, backend, reference
validation, package graph, or tracked Unity source was changed.

The scratch InitializeOnLoad hook emits its positive marker only after the
portable assembly and all required authority types resolve:

~~~text
TheLogsAreWrong.Domain.Runtime.ShiftRuntimeState
TheLogsAreWrong.Domain.Runtime.HostLogTransitionService
TheLogsAreWrong.Domain.Scheduler.SawCycleStartService
TheLogsAreWrong.Domain.Line.LineNoiseDerivationService
~~~

### Attempt 1 — fixed root only

The first harness contained only the portable assembly and the fixed root asset.
It reproduced the expected first material loader result and emitted no positive
marker:

~~~text
Assembly 'Assets/Tlaw058/Plugins/System.Collections.Immutable.dll'
will not be loaded due to errors:
Unable to resolve reference 'System.Runtime.CompilerServices.Unsafe'. Is the assembly missing or incompatible with the current platform?
~~~

The portable assembly and hook assembly then had references-with-errors.
Attempt-one log SHA-256:
518AC54AC0AB50C288353FDB932EC8F70D53FBD83561D40D841A7E696B39B8FA.

### Attempt 2 — one evidence-justified closure addition

The preceding concrete loader error authorized exactly one addition:

| Field | Value |
| --- | --- |
| Package | System.Runtime.CompilerServices.Unsafe |
| Resolved version | 6.0.0 |
| Asset | lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll |
| Identity | System.Runtime.CompilerServices.Unsafe, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a |
| SHA-256 | 01748200F2400C742AA689F1F5101BD6298EFDFD92C00C18F4FA473847235BA9 |
| Closure edge | System.Collections.Immutable/8.0.0 -> System.Runtime.CompilerServices.Unsafe/6.0.0 |
| Additional external DLLs used / hard maximum | 1 / 4 |

Unity internal tool directories contain several Unsafe DLLs, including one whose
identity and SHA-256 match the NuGet asset, but attempt one proves that identity
was not supplied to the ordinary project-plugin resolver. No Unity
internal/framework file was copied; the attempt-two asset came from the
official resolved NuGet closure. That single addition produced:

~~~text
TLAW058_PORTABLE_LOAD_PASS required_authority_types_resolved
~~~

No material loader error followed. Attempt-two log SHA-256:
AD0C4E1EE48070DB66F73FE3C651EB223B95F887672CD0001EEB71EAA8781101.
No System.Memory, System.Buffers, System.Numerics.Vectors, framework runtime
asset, arbitrary SDK DLL, or DLL outside the resolved closure was copied or
chased.

## Real EditMode authority gate

After positive load evidence, one scratch EditMode test executed the actual
authority chain without a mock, facade-only test, Unity rewrite, or copied
gameplay implementation:

~~~text
ShiftRuntimeState.Create
-> HostLogTransitionService.Apply (SCHEDULED -> AT_FEED_GATE)
-> HostLogTransitionService.Apply (AT_FEED_GATE -> AT_INTAKE)
-> HostLogTransitionService.Apply (AT_INTAKE -> QUEUED_FOR_SAW)
-> SawCycleStartService.Start
-> LineNoiseDerivationService.Evaluate
~~~

The test passed 1 of 1, with 0 failed and 0 skipped, and emitted
TLAW058_EDITMODE_AUTHORITY_PASS. Results XML SHA-256:
6EE4000B900B1AA511C8960DF51F8B066BC2380B790CC79B16F32A97ECF7EBA8.
Final Unity test log SHA-256:
8D09F22E43521D11BCF7DE8A47FECDCE7FE1534775EF69EB97BAD32DCA25F0CA.

## Fresh exact net10 parity

The same scratch fixture was copied byte-identically into the Unity EditMode
and exact-baseline net10 runners. Fixture SHA-256:
491F1905BBE01B06B773C40F030998F4642C78A1B441B9D5AE1A71EB18F40CA1.
The net10 runner referenced the Domain project at the exact baseline and
emitted TLAW058_NET10_AUTHORITY_PASS.

Both fresh executions produced this canonical projection:

~~~text
operation_chain=ShiftRuntimeState.Create>HostLogTransitionService.Apply>HostLogTransitionService.Apply>HostLogTransitionService.Apply>SawCycleStartService.Start>LineNoiseDerivationService.Evaluate
shift_id=TLAW058_PROBE_SHIFT
created_state_version=0
queued_state_version=3
saw_state_version=4
log_id=probe_log
log_state=IN_SAW
saw_started_at=10
saw_due_at=14
line_noise=LOUD
line_noise_evaluated_at=10
line_noise_changed_at=10
~~~

The projections have identical UTF-8 bytes, identical text, and the same
SHA-256:
CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F.
The scratch comparison record is:

~~~text
C:\Temp\TLAW-058\evidence\exact-parity.json
SHA-256: E77C8EBB8855DC7F2664CC28824A4AC74CB8B8C44FC6A1C1A5EB9028DBAC579D
~~~

## Scope, deviations, and work not performed

The tracked repository change for this task is exactly this evidence document.
All copied source, transformed source, compiler metadata, projects, NuGet
assets, DLLs, Unity harness content, fixture code, logs, XML, and projections
remain under C:\Temp\TLAW-058.

Two scratch harness-only corrections preceded the valid EditMode run: fixture
and test namespaces changed from file-scoped to C# 9 block syntax because the
pinned Unity test compiler reports C# 9, and the test command was rerun without
-quit so Unity's requested test runner could execute. Neither changed the
26-file portable source cut, its 157 transformations, the NuGet graph, Unity
settings, or production files.

Not performed: production Domain/tests/Unity/project/target/package/tooling
changes; production source linking or multitargeting; any extra package, shim,
polyfill, source generator, source transformation, framework/runtime copy,
binding workaround, reference-validation disablement, backend or ProjectSettings
change; player build or player smoke; host/tick integration; D-016; networking,
FishNet, Steamworks; architecture acceptance; Ready; merge; or cleanup.

PORTABLE_DEPENDENCY_PARITY_PASS
NO_ARCHITECTURE_DECISION_ACCEPTED
