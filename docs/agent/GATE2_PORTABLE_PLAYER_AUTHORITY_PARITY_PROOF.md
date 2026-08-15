# Gate 2 portable Player authority/load/parity proof

Schema: tlaw.gate2-portable-player-authority-parity-proof/v1.

Date: 2026-08-15.

## Scope and identity

| Field | Value |
| --- | --- |
| GitHub contract | [Issue #139](https://github.com/baroentgray/the-logs-are-wrong/issues/139) |
| Owner authorization | Issue #139 comment [5301118864](https://github.com/baroentgray/the-logs-are-wrong/issues/139#issuecomment-5301118864); Linear BAR-103 comment 27524eb9-a2e3-4abc-9915-f67ce1410bbc |
| Exact baseline | 8c0d7d7be4af330aa209b60f9912c49718e3e5df |
| Branch | task/TLAW-060-portable-player-authority-parity-proof |
| Worktree | C:\Projects\TheLogsAreWrong-worktrees\TLAW-060 |
| Scratch root | C:\Temp\TLAW-060 |
| .NET SDK | 10.0.103 |
| Unity | 6000.3.21f1, changeset c02631ffc030 |

This is only D-019's separately authorized scratch/non-production standalone
Player authority/load/parity proof. It is evidence for the already-proven
26-file portable cut, not production Candidate-B extraction, an architecture
acceptance, or a production migration.

No production source transform, project/reference migration, package-policy
change, Unity import, host/tick work, gameplay, D-016, networking, FishNet,
Steamworks, Gate 3, decision-log edit, Ready, merge, or cleanup was performed.

## Exact 26-file cut and transform replay

The inventory was rederived from the accepted TLAW-057 and TLAW-058 repository
evidence. It is exactly:

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

All 26 exact-baseline files were mechanically copied to scratch before any
transformation. The source-copy manifest records 26/26 byte-identical copies
and zero mismatches. The scratch semantic inventory recorded original offset,
line, before text, after text, and enum type where needed. The validator
replayed every transformed file from the exact baseline and recorded zero
unapproved deltas.

| Evidence file | SHA-256 |
| --- | --- |
| accepted-source-inventory.txt | 2DA9771C7ECE32B44BB6EC20133D746B015DA6B5155AECB23444D692E0DF69C3 |
| source-copy-manifest.json | 1C1D261C5C346B62D692DBECA51CFCC26A099F6EE58079F392F242BC61699A09 |
| transformation-manifest.json | 439072996CF0F1F61B5FBB871A57E6FD938C84CA01D8F649E83C0B14A739E22F |
| transformation-validator.json | 0D6BF822B2128AD409117363E7865EF4E9ADEE47F38BE68B693D2B4892A3EB0F |

| Authorized semantic-equivalent transformation | Expected | Actual |
| --- | ---: | ---: |
| ArgumentNullException.ThrowIfNull to explicit null-only ArgumentNullException guard | 131 | 131 |
| Generic Enum.IsDefined to Enum.IsDefined(Type, value) | 25 | 25 |
| Generic Enum.GetValues to type-preserving non-generic cast | 1 | 1 |
| Total | 157 | 157 |

The transformations span exactly 19 of 26 files. No source inventory,
transformed-file set, count, source rewrite, or transformation algorithm was
silently adapted.

## Gate-2 and Unity provenance

The production Unity bootstrap was mechanically copied into the scratch
harness. These baseline files equal their scratch copies:

| File | SHA-256 |
| --- | --- |
| ProjectSettings/ProjectVersion.txt | C996A482FD926D9F094CFB99105C9319B992208C119A36427312AB8A04D25A42 |
| Packages/manifest.json | 56494B2AC7B2B44A4A7A886A465803B99E65E57AFA7F21604A1D81A88E71E30B |
| ProjectSettings/ProjectSettings.asset | 8E98FCEE46FEF9242DAEC0745FFCB5D16C67F8B01A3747006B6CE8A9F00ABD31 |

The copied ProjectVersion establishes Unity 6000.3.21f1 and changeset
c02631ffc030. No scratch change was made to copied backend, API compatibility,
package graph, reference validation, production scene, or production asset.
The Gate-2 package manifest contains no FishNet, FishySteamworks, or
Steamworks dependency.

Only scratch additions live below Assets/Tlaw060: the task plugins,
byte-identical fixture, minimal runtime harness, temporary scratch scene, and
scratch Editor build method. Production unity remains unchanged from baseline.

## Fresh portable build and official resolved closure

The portable scratch project uses:

- TargetFramework netstandard2.1;
- LangVersion latest;
- warnings as errors;
- exactly one direct PackageReference, System.Collections.Immutable 8.0.0;
- only these scratch compiler metadata definitions: IsExternalInit,
  RequiredMemberAttribute, CompilerFeatureRequiredAttribute, and
  SetsRequiredMembersAttribute.

Fresh restore used the official NuGet v3 source with force-evaluate and
no-cache before any Unity asset copy. No TLAW-057/TLAW-058 binary was used as
authority. The portable build passed with zero warnings and zero errors.

| Evidence file | SHA-256 |
| --- | --- |
| portable-restore.log | E36AC788A863CF7C4DB85D4931A629CCDDD4744093144E82CD97A028A2482B78 |
| portable-build.log | 4F58C819C577A14565CF46D3845ED5FD5F64064757630AE4B2CA8319906B509B |
| resolved-dependency-graph.json | 8A7292BA79A6B5802691986AC6FD70F9D38047B9512A527BB7ABDA787571FD4C |

Portable output identity: TheLogsAreWrong.PortableAuthority.Tlaw060, Version
1.0.0.0, Culture neutral, PublicKeyToken null.

Portable output SHA-256:
8C7175B31C3FE4D30D634EFF527FFC7683A9423C8F32E17ABDAAD3960F65534E.

The fresh graph contains exactly five official packages:

| Package/version | Resolved dependency edges | Selected netstandard2.0 asset | Identity | SHA-256 |
| --- | --- | --- | --- | --- |
| System.Collections.Immutable 8.0.0 | -> System.Memory 4.5.5; -> System.Runtime.CompilerServices.Unsafe 6.0.0 | System.Collections.Immutable.dll | System.Collections.Immutable 8.0.0.0, PKT b03f5f7f11d50a3a | 5B1B1C83BA3D135C2FDFE425842FBE9C7432878B7E468623ACB554C69B4C130F |
| System.Buffers 4.5.1 | none | System.Buffers.dll | System.Buffers 4.0.3.0, PKT cc7b13ffcd2ddd51 | C65FFF603B283DC966D1A8B730C11D5E5E750E8021BD24640612F6CC3F2C6FB7 |
| System.Memory 4.5.5 | -> System.Buffers 4.5.1; -> System.Numerics.Vectors 4.4.0; -> Unsafe 4.5.3 | System.Memory.dll | System.Memory 4.0.1.2, PKT cc7b13ffcd2ddd51 | 11590D8BB3B12F29F4202B3EF8593229A5CD6DEBB61E76CBA9AC5493A82EE382 |
| System.Numerics.Vectors 4.4.0 | none | System.Numerics.Vectors.dll | System.Numerics.Vectors 4.1.3.0, PKT b03f5f7f11d50a3a | 2324EE5A35674269225E2AA20957CE8830DBCA0CFFB918BD593F7A3222DEE480 |
| System.Runtime.CompilerServices.Unsafe 6.0.0 | none | System.Runtime.CompilerServices.Unsafe.dll | System.Runtime.CompilerServices.Unsafe 6.0.0.0, PKT b03f5f7f11d50a3a | 01748200F2400C742AA689F1F5101BD6298EFDFD92C00C18F4FA473847235BA9 |

## Windows x64 Development Player

The scratch build method generated a temporary scratch-only scene and used:

~~~text
BuildTarget: StandaloneWindows64
BuildOptions: Development
Output: C:\Temp\TLAW-060\PlayerBuild\Tlaw060Player.exe
~~~

The build log records result Succeeded and platform StandaloneWindows64.
Build log SHA-256:
8B68B107D600E23AE3808212F39C7BA45795E116C06AB283ABA073A968FB2C5E.

Player executable SHA-256:
C8B0D73DC40E4F2CDDBF656CFB7257FCB8273DA22E44E12A8694CD8E275C6FB2.

The full scratch player-managed-asset-inventory.json contains 109 managed
assemblies and has SHA-256:
6FF5E182039CCA6AD28A7A8FC0B0672E51DA40DA8A8BCEADBC58A717AFA45E32.
It proves all three task-supplied plugins are packaged:

| Assembly | Identity | SHA-256 |
| --- | --- | --- |
| TheLogsAreWrong.PortableAuthority.Tlaw060.dll | TheLogsAreWrong.PortableAuthority.Tlaw060 1.0.0.0 | 8C7175B31C3FE4D30D634EFF527FFC7683A9423C8F32E17ABDAAD3960F65534E |
| System.Collections.Immutable.dll | System.Collections.Immutable 8.0.0.0 | 5B1B1C83BA3D135C2FDFE425842FBE9C7432878B7E468623ACB554C69B4C130F |
| System.Runtime.CompilerServices.Unsafe.dll | System.Runtime.CompilerServices.Unsafe 6.0.0.0 | 01748200F2400C742AA689F1F5101BD6298EFDFD92C00C18F4FA473847235BA9 |

The ordinary Unity Player output also has its own standard System.Memory and
System.Buffers entries. Neither official closure asset was copied into the
project, added as a plugin, or used as a retry. No Unity-internal DLL,
arbitrary SDK DLL, binding workaround, package downgrade, framework closure,
or reference-validation change was used.

### Player attempt 1

Input plugin assets before build and run were exactly:

1. newly built portable authority DLL;
2. official resolved System.Collections.Immutable 8.0.0 netstandard2.0 asset;
3. official resolved System.Runtime.CompilerServices.Unsafe 6.0.0
   netstandard2.0 asset.

No System.Memory, System.Buffers, or System.Numerics.Vectors asset was added.
The Player exited 0. Its log SHA-256 is:
65CB4421D10657A6774004903C3B4B0AAB66319DC4BAB86166AD67CE95151676.

The running Player directly resolved these actual authority types before its
positive load marker:

~~~text
TheLogsAreWrong.Domain.Runtime.ShiftRuntimeState
TheLogsAreWrong.Domain.Runtime.HostLogTransitionService
TheLogsAreWrong.Domain.Scheduler.SawCycleStartService
TheLogsAreWrong.Domain.Line.LineNoiseDerivationService
~~~

Its log contains:

~~~text
TLAW060_PLAYER_PORTABLE_LOAD_PASS
TLAW060_PLAYER_AUTHORITY_PASS
~~~

There was no failed Player build/load attempt, concrete managed dependency
error, closure addition, or retry.

## Real authority operation and fresh parity

One task-owned fixture was copied byte-identically into the Unity harness and
fresh net10 runner. Fixture SHA-256:
B945EE3DC97C78FD969B72062BA1EFDADE7E3872E4CFBC0C2B02BAE2B20124B4.

It directly invokes:

~~~text
ShiftRuntimeState.Create
-> HostLogTransitionService.Apply: SCHEDULED -> AT_FEED_GATE
-> HostLogTransitionService.Apply: AT_FEED_GATE -> AT_INTAKE
-> HostLogTransitionService.Apply: AT_INTAKE -> QUEUED_FOR_SAW
-> SawCycleStartService.Start
-> LineNoiseDerivationService.Evaluate
~~~

No mock, facade substitute, Unity rewrite, or copied gameplay algorithm was
used. The fresh net10 runner references the exact-baseline Domain and emitted
TLAW060_NET10_AUTHORITY_PASS.

Both raw, UTF-8, no-prefix/no-timestamp projections contain:

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

| Record | Result |
| --- | --- |
| Player raw SHA-256 | CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F |
| Fresh net10 raw SHA-256 | CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F |
| Raw byte length | 426 each |
| Byte-identical | Yes |
| UTF-8 text identical | Yes |
| TLAW-058 oracle match | Yes |
| exact-player-net10-parity.json SHA-256 | 15B84C9EAC349E7456BD31228CCBD511D2129AD557579A92B52AD0AFC7263C08 |

## Deviation and explicitly not performed

Before the valid Player run, the first fresh net10 projection contained a
single final LF and therefore had SHA-256
1F85289D819B094606C1AEDD9AF14C02543BC9DDF0941A7BB373F4603C45DD8E.
The established oracle is the identical canonical values without a terminal
newline. The task-owned raw projection writer was corrected to omit that LF,
then fresh net10 was rerun and matched the oracle before the Player attempt.
No fixture value, authority operation, source-cut transformation, package,
dependency asset, Unity setting, or production file changed.

Not performed: production Candidate-B extraction; production 157 source edits;
production portable-core project; production project-reference migration;
production package policy; production Unity import; host/tick integration;
gameplay; D-016; networking; FishNet; Steamworks; Gate 3; any architecture or
decision-log change; Ready; merge; or cleanup.

PLAYER_AUTHORITY_PARITY_PASS
NO_PRODUCTION_MIGRATION
