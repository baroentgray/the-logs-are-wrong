# Gate 2 production portable authority migration

Schema: `tlaw.gate2-production-portable-authority-migration/v1`.

## Scope and identity

| Field | Value |
| --- | --- |
| GitHub contract | [Issue #141](https://github.com/baroentgray/the-logs-are-wrong/issues/141) |
| Owner implementation-start authorization | [Issue #141 comment 5301713137](https://github.com/baroentgray/the-logs-are-wrong/issues/141#issuecomment-5301713137) |
| Exact baseline | `b6b3a39cb30b22da026a826028075c15c78dea0c` |
| Branch | `task/TLAW-061-production-portable-authority-core` |
| Worktree | `C:\Projects\TheLogsAreWrong-worktrees\TLAW-061` |
| Candidate exact head | This commit; its immutable SHA is bound by the Draft PR and exact-head verification because a commit cannot contain its own resulting object ID. |
| Production source ownership after migration | `TheLogsAreWrong.Domain -> TheLogsAreWrong.PortableAuthority` |

This is only the first D-019 Candidate-B production extraction increment. It
establishes one Unity-free portable authority implementation consumed by the
existing net10 Domain composition. It does not import that assembly into
production Unity or authorize host/tick, gameplay, D-016, networking, Ready,
merge, or cleanup.

## Phase 0 — fail-closed baseline inventory

`origin/main` was fetched and resolved exactly to the authorized baseline.
The 26 accepted TLAW-060 source blobs were compared against both the TLAW-060
baseline `8c0d7d7be4af330aa209b60f9912c49718e3e5df` and this authorized
baseline before any source transformation: `26/26` byte-identical, `0`
mismatches. The production Domain inventory was exactly 60 C# files, split
into the contracted 26-file move and 34-file remaining outer composition.

### Exact pre-move source hashes

| Original Domain path | SHA-256 |
| --- | --- |
| `Anomalies/AnomalyResolutionContracts.cs` | `D8BE6023FF5E74E6E2172EA142ABAB3B6D355D12BF6D174E75493DD9235C8294` |
| `Anomalies/ConfirmationTestContracts.cs` | `04683B46EAA570A63F208925110A7867227E4F9436B8F8CB4454726AC4D48668` |
| `Configuration/ValidatedConfiguration.cs` | `03107EE2C13507F45EAD36A7CC150E6033D39D0AC9840C71356F8773648AAF01` |
| `Containment/ContainmentLifecycleContracts.cs` | `6635DA9237BAA4984EF1F9485223E8E0327FA7E4D6F06F30A3F2F7C865EFB68F` |
| `Enums/DomainEnums.cs` | `7ABFD16889C7360F48B9FA22D79BF3EBF05DC1FCBA74D035B3B95DD2EBE7BB0D` |
| `Events/EventContracts.cs` | `659AF314844E9E48BE6A38DE98E0041E528053AC4F8AF0661A6BB18D61C0AD19` |
| `Identifiers/Identifiers.cs` | `546CDCC2D56927D54210667AAEB7A3E43B4BA355D7CEF6DAAE20C99BAEA60E13` |
| `Intents/IntentContracts.cs` | `40BBDE66D5A0726F73E69B199E70105C642CD70DA79333D9D1810CFA4B0CED3A` |
| `Line/LineJamRepairContracts.cs` | `520E0546C3467DBFFB3EAFBB2B364FB30A0D833987510F04C631E5943C82AA18` |
| `Line/LineNoiseRuntimeContracts.cs` | `276730D64D3A2C8A0ED940DB80CABA634C1C602699CBE3682EA70AACC4FDCE07` |
| `Line/MovementNoiseRuntimeContracts.cs` | `DAA0EA512ABBCBD03D3BB15B7A41AB5C65B8AE5A55094D6E4F6F03BEA41FF4F7` |
| `Logs/LogTransitionPolicy.cs` | `77D1C8CA79293FD716099688E5CC011903178011CA0B2B360E8F85C72F62BE56` |
| `Primitives/Primitives.cs` | `8E697E87DADB079F42DE28848C3CFEF356BC18B9681BAEBC8BD0B09BEDD4F314` |
| `Quota/QuotaContracts.cs` | `E62C0A762733B1FD1585635D469B4BBDB4FBFFD746710C5CFD38269699CC14E2` |
| `Runtime/ConfirmationTestLifecycleContracts.cs` | `9EBDBBB9E014D143FF151F62E64EF434313320CAC0FA2BB79C31D5CE2D4CBF36` |
| `Runtime/LogTransitionServices.cs` | `D9A3975BE6B0CDFC6EC4D7CAEB6F246185690151DB1D0C1F449BD6A9A6A81C70` |
| `Runtime/ProcedureActionLifecycleContracts.cs` | `DA17587F2BE562AFD607EA90AA9AB416AB11F6525D121605C6B4B6A83F659C99` |
| `Runtime/ProcedureCompletionContracts.cs` | `D05A5C97785E26E643A4180AAE83A3EBA72B8A249790529A9B29B57511E617E5` |
| `Runtime/ShiftRuntimeState.cs` | `57847BE0267F35BB076C5D3D4D4D3F975A81D9F5430D9F4ACE97C19C3F63E6B4` |
| `Scheduler/DefaultIntakeAutoRouteContracts.cs` | `FA3AFAECC62C24568BAAEDF016436BCE2CFDBB3B90687F286C0F8BB82842C111` |
| `Scheduler/FeedDueResolutionContracts.cs` | `9525EA833A0B825B9A7F82795E1A922880C77957900A01670382E5EEE4E98790` |
| `Scheduler/FeedPlanningContracts.cs` | `F8033B105DB7D838391B992FA835D62E65A5D636CBF16BD11CC46B3EE166C939` |
| `Scheduler/IntakeDeadlineContracts.cs` | `EDF2207E74D82F643BCDBB3B82045BA6C006C1937EC4AA1ADCCC7C036CF111A6` |
| `Scheduler/RepairPendingTransitionExecutionContracts.cs` | `E181040A59A24162239E4742AF2ADAD3A4FF99F4EC5D1EE4C2DACE06AF947A4F` |
| `Scheduler/SawCycleContracts.cs` | `44AF4711F578FCE8EA5E605D46BC9D60D4D6393ABAF6F7FC36EC9E60D3EC4D73` |
| `Time/SimulationTime.cs` | `8F9BA4BA9C76F1A2422058C96BE1A93C1A72964F096352422ABF8BAF15F40A4C` |

The remaining outer-Domain inventory is exactly 34 files:

```text
Configuration/Diagnostics/ConfigurationDiagnostics.cs
Intents/AcceptedIntentBatchContracts.cs
Intents/ConfirmationTestIntentContracts.cs
Intents/ContainmentRitualIntentContracts.cs
Intents/LineRepairIntentContracts.cs
Intents/ProcedureActionIntentContracts.cs
Journal/EventJournal.cs
Journal/JournaledMutationCommitContracts.cs
Journal/ReplayContracts.cs
Journal/ShiftReplayReducerContracts.cs
Journal/ShiftReplayReductionState.cs
Journal/ShiftSnapshotCaptureContracts.cs
Journal/ShiftSnapshotContracts.cs
Journal/ShiftSnapshotRestoreContracts.cs
Runtime/AcceptedIntentStageExecutionContracts.cs
Runtime/ConfirmationTestIntentHandler.cs
Runtime/ContainmentRitualIntentHandler.cs
Runtime/HostStageFiveFeedExecutionContracts.cs
Runtime/HostStageFourSawExecutionContracts.cs
Runtime/HostStageOneCompletionExecutionContracts.cs
Runtime/HostStageSevenEventExecutionContracts.cs
Runtime/HostStageSixDerivedExecutionContracts.cs
Runtime/HostStageThreeDeadlineExecutionContracts.cs
Runtime/HostTickCompletionCheckpointContracts.cs
Runtime/HostTickExecutionContracts.cs
Runtime/LineRepairIntentHandler.cs
Runtime/ProcedureActionIntentHandler.cs
Runtime/SawQuotaApplicationContracts.cs
Runtime/ShiftCompletionContracts.cs
Scheduler/FeedGateJamDerivationContracts.cs
Scheduler/IntakeAutoFeedJamDerivationContracts.cs
Scheduler/RepairAutoFeedNormalFeedPlanningContracts.cs
Scheduler/RepairFeedGateIntakeDeadlineContracts.cs
Sequencing/SequencingContracts.cs
```

`unity/**` remained byte-identical between the TLAW-060 and authorized
baselines. The current Gate-2 `Packages/manifest.json` and
`Packages/packages-lock.json` contain no FishNet, FishySteamworks, or
Steamworks package. The pre-move `Directory.Build.props`, `global.json`,
`package_manifest.json`, Domain project, and solution matched the authorized
baseline exactly.

## Atomic extraction and transform manifest

Each contracted file was moved, never copied, from
`src/TheLogsAreWrong.Domain/<relative-path>` to
`src/TheLogsAreWrong.PortableAuthority/<relative-path>`:

```text
Anomalies/AnomalyResolutionContracts.cs
Anomalies/ConfirmationTestContracts.cs
Configuration/ValidatedConfiguration.cs
Containment/ContainmentLifecycleContracts.cs
Enums/DomainEnums.cs
Events/EventContracts.cs
Identifiers/Identifiers.cs
Intents/IntentContracts.cs
Line/LineJamRepairContracts.cs
Line/LineNoiseRuntimeContracts.cs
Line/MovementNoiseRuntimeContracts.cs
Logs/LogTransitionPolicy.cs
Primitives/Primitives.cs
Quota/QuotaContracts.cs
Runtime/ConfirmationTestLifecycleContracts.cs
Runtime/LogTransitionServices.cs
Runtime/ProcedureActionLifecycleContracts.cs
Runtime/ProcedureCompletionContracts.cs
Runtime/ShiftRuntimeState.cs
Scheduler/DefaultIntakeAutoRouteContracts.cs
Scheduler/FeedDueResolutionContracts.cs
Scheduler/FeedPlanningContracts.cs
Scheduler/IntakeDeadlineContracts.cs
Scheduler/RepairPendingTransitionExecutionContracts.cs
Scheduler/SawCycleContracts.cs
Time/SimulationTime.cs
```

The compatibility validator reconstructs each portable file from its exact
baseline Domain source using only the three accepted transforms, then compares
the reconstructed bytes to the moved result. It reports `26/26` mapped files,
`19` transformed files, and `0` unapproved file deltas.

| Relative path; before → after | Null guards | `Enum.IsDefined` | `Enum.GetValues` |
| --- | ---: | ---: | ---: |
| `Anomalies/AnomalyResolutionContracts.cs` | 22 | 2 | 0 |
| `Anomalies/ConfirmationTestContracts.cs` | 4 | 1 | 0 |
| `Containment/ContainmentLifecycleContracts.cs` | 8 | 1 | 0 |
| `Intents/IntentContracts.cs` | 1 | 0 | 0 |
| `Line/LineJamRepairContracts.cs` | 2 | 4 | 0 |
| `Line/LineNoiseRuntimeContracts.cs` | 6 | 7 | 0 |
| `Line/MovementNoiseRuntimeContracts.cs` | 11 | 3 | 0 |
| `Quota/QuotaContracts.cs` | 6 | 0 | 0 |
| `Runtime/ConfirmationTestLifecycleContracts.cs` | 10 | 0 | 0 |
| `Runtime/LogTransitionServices.cs` | 3 | 0 | 0 |
| `Runtime/ProcedureActionLifecycleContracts.cs` | 5 | 0 | 0 |
| `Runtime/ProcedureCompletionContracts.cs` | 8 | 0 | 0 |
| `Runtime/ShiftRuntimeState.cs` | 21 | 0 | 1 |
| `Scheduler/DefaultIntakeAutoRouteContracts.cs` | 2 | 0 | 0 |
| `Scheduler/FeedDueResolutionContracts.cs` | 3 | 2 | 0 |
| `Scheduler/FeedPlanningContracts.cs` | 3 | 1 | 0 |
| `Scheduler/IntakeDeadlineContracts.cs` | 7 | 0 | 0 |
| `Scheduler/RepairPendingTransitionExecutionContracts.cs` | 4 | 4 | 0 |
| `Scheduler/SawCycleContracts.cs` | 5 | 0 | 0 |
| **Total** | **131** | **25** | **1** |

The exact replacements are semantic-equivalent and apply only to the one
moved implementation:

- 131 `ArgumentNullException.ThrowIfNull(expression)` calls became explicit
  null-only guards with the original caller-expression parameter string;
- 25 inferred `Enum.IsDefined(value)` calls became
  `Enum.IsDefined(typeof(TEnum), value)` after resolving the existing enum
  type; and
- one `Enum.GetValues<NodeId>()` call became the type-preserving
  `(NodeId[])Enum.GetValues(typeof(NodeId))`.

The resolved 25 enum replacements are: `ContainmentState` 1, `EffectType` 1,
`FeedDueDisposition` 1, `FeedDueFollowUpRequirement` 1, `FeedScheduleKind` 1,
`JamCause` 2, `LineNoise` 4, `LineState` 2, `LogState` 9,
`MovementNoiseAcceptedSource` 2, and `RepairPendingTransitionFollowUp` 1.
There is no unchanged net10 authority copy, scratch authority promotion, or
target-specific authority algorithm.

## Project, package, and dependency proof

`src/TheLogsAreWrong.PortableAuthority/TheLogsAreWrong.PortableAuthority.csproj`
is the production portable owner with `netstandard2.1`, `LangVersion=latest`,
warnings as errors, and assembly name `TheLogsAreWrong.PortableAuthority`.
It owns exactly these four compiler metadata compatibility definitions:

1. `System.Runtime.CompilerServices.IsExternalInit`.
2. `System.Runtime.CompilerServices.RequiredMemberAttribute`.
3. `System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute`.
4. `System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute`.

Its sole direct PackageReference is `System.Collections.Immutable` `8.0.0`.
Fresh restore resolved exactly the accepted official closure:

| Package | Version | Direct |
| --- | ---: | --- |
| `System.Collections.Immutable` | `8.0.0` | yes |
| `System.Memory` | `4.5.5` | no |
| `System.Buffers` | `4.5.1` | no |
| `System.Numerics.Vectors` | `4.4.0` | no |
| `System.Runtime.CompilerServices.Unsafe` | `6.0.0` | no |

`TheLogsAreWrong.Domain` has one project reference to the portable core; the
portable project has no project reference and therefore cannot reference
Domain. Runtime architecture assertions prove Domain references
`TheLogsAreWrong.PortableAuthority`, the portable assembly does not reference
Domain, and its production sources contain no UnityEngine, FishNet, or
Steamworks dependency. The test source inventory is wired to retain the logical
60-file Domain scan, while the physical ownership proof asserts the exact
portable 26 and outer-Domain 34 file sets.

## Determinism and D-014 regression evidence

The production regression executes the required real chain directly through the
moved implementation:

```text
ShiftRuntimeState.Create
-> HostLogTransitionService.Apply (SCHEDULED -> AT_FEED_GATE)
-> HostLogTransitionService.Apply (AT_FEED_GATE -> AT_INTAKE)
-> HostLogTransitionService.Apply (AT_INTAKE -> QUEUED_FOR_SAW)
-> SawCycleStartService.Start
-> LineNoiseDerivationService.Evaluate
```

Its canonical no-final-LF projection is unchanged and hashes to:

```text
CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F
```

The D-014 trait slice covering snapshot capture, restore, journal, and replay
passes `87/87`; the broader snapshot/replay name slice passes `77/77`.
The full existing Release suite passes `1633/1633`, with `0` failures and `0`
skips, retaining the full D-014 behavior through the new Domain-to-core
boundary.

## Not performed

No production Unity import or change under `unity/**`; no host/tick behavior
change; no gameplay; no D-016; no networking, FishNet, FishySteamworks, or
Steamworks; no migration of the remaining 34 outer Domain files; no duplicate
authority implementation; no namespace/API redesign; no `DECISIONS.md` change;
no Ready, merge, or cleanup.

PRODUCTION_PORTABLE_AUTHORITY_MIGRATION_PASS
NO_PRODUCTION_UNITY_IMPORT
