# Gate 2 — Unity host-tick architecture proof (TLAW-063)

| Field | Evidence |
| --- | --- |
| GitHub contract | Issue #145 — TLAW-063 |
| Linear | BAR-106 |
| Exact baseline | `5692f9200b191c2c56d1e119b4d6b5ae3003c673` |
| Branch | `task/TLAW-063-unity-host-tick-architecture-proof` |
| Worktree | `C:\Projects\TheLogsAreWrong-worktrees\TLAW-063` |
| Scratch only | `C:\Temp\TLAW-063` |
| GitHub owner authorization | comment `5309021141` |
| Linear owner authorization | comment `c7305c53-9311-4932-a0c1-d6550c187778` |
| Unity | `6000.3.21f1`, changeset `c02631ffc030` |

This is a scratch/non-production architecture proof. It does not select H1, H2, or H3; authorize a production host migration; append `DECISIONS.md`; or authorize a Unity host loop, gameplay, D-016, networking, Ready, merge, or cleanup.

## Phase 0 — fail-closed baseline inventory

`git fetch origin --prune` resolved both `origin/main` and
`origin/task/TLAW-063-unity-host-tick-architecture-proof` to the exact baseline.
The new task worktree was clean and on the required branch.

The pinned executable existed at
`C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe`; its command
version was `6000.3.21f1` and file product version was
`6000.3.21f1_c02631ffc030`.

The accepted TLAW-062 production plugin directory remained exactly:

```text
System.Collections.Immutable.dll
System.Runtime.CompilerServices.Unsafe.dll
TheLogsAreWrong.PortableAuthority.dll
```

There was no Domain/host DLL below production Unity `Assets`, and no FishNet,
FishySteamworks, Steamworks, or networking package/reference hit. Production
references remained `TheLogsAreWrong.Domain -> TheLogsAreWrong.PortableAuthority`;
PortableAuthority did not reference outer Domain.

The host root exists at
`src/TheLogsAreWrong.Domain/Runtime/HostTickExecutionContracts.cs`.
Its declared and invoked frozen order was independently read from source:

```text
HostStageOneCompletionExecutor
-> AcceptedIntentStageExecutor
-> HostStageThreeDeadlineExecutor
-> HostStageFourSawExecutor
-> HostStageFiveFeedExecutor
-> HostStageSixDerivedExecutor
-> HostStageSevenEventExecutor
```

## Exact physical source dependency cut

The scratch Roslyn analyzer started at
`HostTickExecutionService.Execute`, selected its physical source file, and
reached a fixed point by resolved source symbol/type edges. A selected physical
file was fully scanned before adding its outgoing source edges; this is the
relevant cut for an exact-source copy rather than a DTO-only list.

Result: **54 logical source files**: **26** already owned by PortableAuthority
(A) and **28** outer-Domain-owned (B). The host root has 18 direct source edges;
the remaining 35 non-root files are transitive. Every listed file is required
because it declares a source symbol referenced from the root (direct) or from a
previously selected physical source file (transitive). `root` is the execution
entry source itself.

| Repository path | Owner | Edge | Baseline Git blob | SHA-256 |
| --- | --- | --- | --- | --- |
| `src/TheLogsAreWrong.Domain/Intents/AcceptedIntentBatchContracts.cs` | B / outer Domain | direct | `6f98798c8d1747b31583a7defbeba56955d813fb` | `6D3BA257FF492A258A2694DA4714A9809500DB576713368844771D9AAF747755` |
| `src/TheLogsAreWrong.Domain/Intents/ConfirmationTestIntentContracts.cs` | B / outer Domain | transitive | `31beff774ba07b6a1661b9638e950404f48ebe92` | `A18797C864A6BDAF21E0FB9CC05901AC5F4044E0E8DE8FCE0FDE55C5C663B6F7` |
| `src/TheLogsAreWrong.Domain/Intents/ContainmentRitualIntentContracts.cs` | B / outer Domain | transitive | `f2344023208a4c8e66fa16a7c5866fe777acac71` | `8E2B75B00D46789AA1176C17422058817402CDFC1C5F41137478658711D4DF74` |
| `src/TheLogsAreWrong.Domain/Intents/LineRepairIntentContracts.cs` | B / outer Domain | transitive | `49fcf3ebe0eedd389d7ee0b581045fdea72c6bbf` | `726B6C20D402161C9A88A1DF9C757229B79BB3E9B6DB19FCF53939C49D8DB20C` |
| `src/TheLogsAreWrong.Domain/Intents/ProcedureActionIntentContracts.cs` | B / outer Domain | transitive | `375b6903cb30f22609ce8eff3b914824f8348a76` | `0650033F407168B5087E2DAC7BD146AC7E19287B565BFA81E46D159D9F33786B` |
| `src/TheLogsAreWrong.Domain/Journal/EventJournal.cs` | B / outer Domain | direct | `ce199229b4601b5721e01dbc0f0e9afdddc29e1d` | `C02C2737F2799BE8FC4E28C16C8264A0E6768C065DFA8E3D65B3C20190F8DC18` |
| `src/TheLogsAreWrong.Domain/Journal/JournaledMutationCommitContracts.cs` | B / outer Domain | transitive | `a855ba1390305354e754c6c32a0820e150c2f1e8` | `53C64D5A5A63DDB2634B758F02DA8EC7FC8A4A75DB92E10EADF6F7A4AB2D90EC` |
| `src/TheLogsAreWrong.Domain/Journal/ReplayContracts.cs` | B / outer Domain | transitive | `85c555ea7f0c79250dbd1bbde405ccbaad38e505` | `6B36213877357E63F7901EDFE59E0D00FACCB374F833829FD093389DD01C80F0` |
| `src/TheLogsAreWrong.Domain/Runtime/AcceptedIntentStageExecutionContracts.cs` | B / outer Domain | direct | `29f83e607e3448eab1783bd7d24f736085272628` | `9A946702AEAB7B103F363E4C08A1439CA3003FFEE789F817BF79ECFD22AECDE5` |
| `src/TheLogsAreWrong.Domain/Runtime/ConfirmationTestIntentHandler.cs` | B / outer Domain | transitive | `c4b48d0dd183c9ea3bb9ffa7e300e5634a19ca3a` | `ECB0AEB0C3BBDDADF4C546B8EADFEA1A23D1E3BF0A48EC0DCFF3568F49ED766B` |
| `src/TheLogsAreWrong.Domain/Runtime/ContainmentRitualIntentHandler.cs` | B / outer Domain | transitive | `057cb2fabb684b70eb6edd9833442287be6b7a17` | `A9E6C54BFD79282BD56626DF209D1DDAEC4E3E41990442D3C1BC3BB07A97C431` |
| `src/TheLogsAreWrong.Domain/Runtime/HostStageFiveFeedExecutionContracts.cs` | B / outer Domain | direct | `d63630738d3d72ee9f7cba3674fd9eb8c206342f` | `75D43E7C7A5066C02686E7BCCC4BDEE483B68D131766988830669214F081AEB7` |
| `src/TheLogsAreWrong.Domain/Runtime/HostStageFourSawExecutionContracts.cs` | B / outer Domain | direct | `634cd498ed8ee1a1b614a65e4314d906952d3faf` | `BF41BF11D08D2982E7DDF00233EA0F2E3CB6502C192A15560F3550BD7C731EAD` |
| `src/TheLogsAreWrong.Domain/Runtime/HostStageOneCompletionExecutionContracts.cs` | B / outer Domain | direct | `656f7416bf4d4026b961caee2b6e2cc290495fe9` | `0B4519177AF3FC4E308C6BB47A20A9EBDD08853BF61E008CDE73D518E70AEEBA` |
| `src/TheLogsAreWrong.Domain/Runtime/HostStageSevenEventExecutionContracts.cs` | B / outer Domain | direct | `1c3a38c7ae08fb7f28a8c3a280d633c9a436e9be` | `AB7B4ED2181E20428E707B7AFC19141F1F6FD5B7856BA6AC8A9EA82EFF005092` |
| `src/TheLogsAreWrong.Domain/Runtime/HostStageSixDerivedExecutionContracts.cs` | B / outer Domain | direct | `6e9a07611db137283053ee19ec60a8348c4c744f` | `579095D8FD6E8EC73EF84E287C264F141ACE3348BDA4D485EFBDA3950D725CD6` |
| `src/TheLogsAreWrong.Domain/Runtime/HostStageThreeDeadlineExecutionContracts.cs` | B / outer Domain | direct | `626f8097e4b0007bd56820bc7a2518ca14521f91` | `645718A4BCD29F17B33D93879BEE37911FC84859FC02C8D2D7320D9D63F56367` |
| `src/TheLogsAreWrong.Domain/Runtime/HostTickCompletionCheckpointContracts.cs` | B / outer Domain | direct | `904641dd4dcc6c65689dfc812cca0ae459867b57` | `92FE34FB6737E240EBB5B13FDF0389BBD5C5F0347128D0359EF8904894459BEA` |
| `src/TheLogsAreWrong.Domain/Runtime/HostTickExecutionContracts.cs` | B / outer Domain | root | `66efe7e8566076546885721d924de56c697ad02b` | `69CC7357A70B5266438E35D39284EE89DEC1680C7A232D02F424E8E6FFC51725` |
| `src/TheLogsAreWrong.Domain/Runtime/LineRepairIntentHandler.cs` | B / outer Domain | transitive | `646f622dad5a0fca89ccdb884ca3d2b2ac5fb2e4` | `05407A90427949FC134D052E2E886318D393226A27CFB3AB1D1E5F294DC7D8B0` |
| `src/TheLogsAreWrong.Domain/Runtime/ProcedureActionIntentHandler.cs` | B / outer Domain | transitive | `0bc7daea6f6be8a8cdfd6c8fef14bdddb3280ef2` | `9318A174A1DD73AEF3C6A7C4E41A091FA880374DA316A5E596E7FDF14DB96A6E` |
| `src/TheLogsAreWrong.Domain/Runtime/SawQuotaApplicationContracts.cs` | B / outer Domain | transitive | `47ee62b2f3e849df5bdf82de5c28100cfcb4a4c8` | `08FD2A1F28E17EF99106FD4A56FDD830DE589FF1BF8F4215B89569FCEECF8E35` |
| `src/TheLogsAreWrong.Domain/Runtime/ShiftCompletionContracts.cs` | B / outer Domain | direct | `c07ef8f7d39bcb07ca3d67bf7198e7f1f5fb4522` | `BFC4B4ADAD4F64125C5AC6270CD6AE4C23F6567BB0527CAA441B250ECF7320AB` |
| `src/TheLogsAreWrong.Domain/Scheduler/FeedGateJamDerivationContracts.cs` | B / outer Domain | transitive | `004217506afeb2ad1fc176886585f38fe7ed2342` | `D28AB3FF5E31A90F982EB7E34EC76CE492A7611528C4E19CF5530D28DB643FD5` |
| `src/TheLogsAreWrong.Domain/Scheduler/IntakeAutoFeedJamDerivationContracts.cs` | B / outer Domain | transitive | `46c2b897c5792a975ad3440439f25698a6fa4872` | `607BACD4098942C5FDFD739F90C1B334C261CAF68FD51519F2099F0388C21AFA` |
| `src/TheLogsAreWrong.Domain/Scheduler/RepairAutoFeedNormalFeedPlanningContracts.cs` | B / outer Domain | transitive | `d978ba11ad5a6ea468da168e1e966b0985aaf88f` | `9F6641ABD98D7AFAF8BEA1D4F75BD7F50EE2E3D26B4C38EB937627FFC786A30D` |
| `src/TheLogsAreWrong.Domain/Scheduler/RepairFeedGateIntakeDeadlineContracts.cs` | B / outer Domain | transitive | `b6ceffcfefed4b2982e7889ba41db0b74b16f3a4` | `E398E533ADC27DD876FB9DB22EDE30BA37B6DB305A11A42560EBE1BD4E9E1CCD` |
| `src/TheLogsAreWrong.Domain/Sequencing/SequencingContracts.cs` | B / outer Domain | transitive | `5853cb64f67eb7b202dcc6dd182d2de09d735486` | `FB822CB7D93F317F160454A06820DB3045D92588EEE64DF510DF72DC92A5409E` |
| `src/TheLogsAreWrong.PortableAuthority/Anomalies/AnomalyResolutionContracts.cs` | A / PortableAuthority | transitive | `eb68a399a2df13be858e17ab264a7943425682dd` | `EB5E10E0DCDDFE4635BC4EAE8006220249A4A9441B8099EE1B1846441D364A5C` |
| `src/TheLogsAreWrong.PortableAuthority/Anomalies/ConfirmationTestContracts.cs` | A / PortableAuthority | transitive | `eeeeae9600d906fd48424c6520d4eaeadd3d1da3` | `F807CFD35CB97A64419EB36D87F5B481E8C8E75ABE0E2FD9B65F518589EC9259` |
| `src/TheLogsAreWrong.PortableAuthority/Configuration/ValidatedConfiguration.cs` | A / PortableAuthority | direct | `1bee9ad8f3763e2e0be895c4abdb169ae932b897` | `03107EE2C13507F45EAD36A7CC150E6033D39D0AC9840C71356F8773648AAF01` |
| `src/TheLogsAreWrong.PortableAuthority/Containment/ContainmentLifecycleContracts.cs` | A / PortableAuthority | transitive | `f8e99a904b3f82ab73821730e8be5643fce5fbb7` | `22C417E73970A2AA918E4459710E540230F78FBAAC8846E8BD1B1C04BD34F1A2` |
| `src/TheLogsAreWrong.PortableAuthority/Enums/DomainEnums.cs` | A / PortableAuthority | transitive | `22de301bc210dd3a9dd6de78e8e7033b2222f72b` | `7ABFD16889C7360F48B9FA22D79BF3EBF05DC1FCBA74D035B3B95DD2EBE7BB0D` |
| `src/TheLogsAreWrong.PortableAuthority/Events/EventContracts.cs` | A / PortableAuthority | transitive | `0a192272360087719d4de331affb7b5564c9001f` | `659AF314844E9E48BE6A38DE98E0041E528053AC4F8AF0661A6BB18D61C0AD19` |
| `src/TheLogsAreWrong.PortableAuthority/Identifiers/Identifiers.cs` | A / PortableAuthority | direct | `735c4c7822d0de6b4efed0a93e0f72602e186427` | `546CDCC2D56927D54210667AAEB7A3E43B4BA355D7CEF6DAAE20C99BAEA60E13` |
| `src/TheLogsAreWrong.PortableAuthority/Intents/IntentContracts.cs` | A / PortableAuthority | transitive | `34f762243faccfd06f89f08cac1f12105cb5254b` | `E55A26CC0C70D65868698D34548E649A9904E62EFD4E647050F793C61F783E20` |
| `src/TheLogsAreWrong.PortableAuthority/Line/LineJamRepairContracts.cs` | A / PortableAuthority | transitive | `1539df4b03536fb0c65630a0dab0c44f3a34b194` | `443F67EC6B7A3D8D65BC0A8A8C10D8AB0FDD088E1D0301972E69A15D6E329CFA` |
| `src/TheLogsAreWrong.PortableAuthority/Line/LineNoiseRuntimeContracts.cs` | A / PortableAuthority | direct | `c6691c0f6dd0a89300f0a818a44f6b8c1d35dfcc` | `0FD34451BE7AE7EED4D32DB67B6E482DE38872D0B284A8601C830811E6EA1F2D` |
| `src/TheLogsAreWrong.PortableAuthority/Line/MovementNoiseRuntimeContracts.cs` | A / PortableAuthority | direct | `2f46d4349a16b5dac986ee6861afc3c87fa31a05` | `BAF361D24E60415A0A768FD55DF5495181E4326751AB3A5FFF4D21B3B58CA43B` |
| `src/TheLogsAreWrong.PortableAuthority/Logs/LogTransitionPolicy.cs` | A / PortableAuthority | transitive | `9cfb0b57144639ee4b909e54c096c025f8f80b6f` | `77D1C8CA79293FD716099688E5CC011903178011CA0B2B360E8F85C72F62BE56` |
| `src/TheLogsAreWrong.PortableAuthority/Primitives/Primitives.cs` | A / PortableAuthority | direct | `db0984d8c0119a16a1909f04f1ebb4c3e0b3decd` | `8E697E87DADB079F42DE28848C3CFEF356BC18B9681BAEBC8BD0B09BEDD4F314` |
| `src/TheLogsAreWrong.PortableAuthority/Quota/QuotaContracts.cs` | A / PortableAuthority | direct | `65d1073f5fe1e4193036311127af6b4bc9c44b81` | `F097673A6CE2A6A46EEEE9E48A902BA4A9458FB37954D1AD84500117697E1344` |
| `src/TheLogsAreWrong.PortableAuthority/Runtime/ConfirmationTestLifecycleContracts.cs` | A / PortableAuthority | transitive | `51e49e4b200575baf4ad6f4cb3533b7cf7112e28` | `19662393EBEBEDB8A8B843BA90EC78FF15122963F6F9C7DC313E53B1EAA3D05D` |
| `src/TheLogsAreWrong.PortableAuthority/Runtime/LogTransitionServices.cs` | A / PortableAuthority | transitive | `dbed7abb1986977ba9903224aa0891a089d6cf46` | `324CA6D1F49978B033A3ADDEA26EC4DFA5AC40077DA8AA8AF3A8BB62EEC07FF6` |
| `src/TheLogsAreWrong.PortableAuthority/Runtime/ProcedureActionLifecycleContracts.cs` | A / PortableAuthority | transitive | `a385de71d94fa5cddd5864f5b646f6fafe0ff794` | `6E49812DEEC601A8874DC66CFFF7386DED35F4996879ACCBB6028599EF835515` |
| `src/TheLogsAreWrong.PortableAuthority/Runtime/ProcedureCompletionContracts.cs` | A / PortableAuthority | transitive | `51582e372f1f27fc0851502c6978de5defaf99ea` | `91A210ACB18B738337211953E42C04329EC6AC7243B611FA3D41C8E35C39244E` |
| `src/TheLogsAreWrong.PortableAuthority/Runtime/ShiftRuntimeState.cs` | A / PortableAuthority | direct | `143b6de9371a1c20812e156a99d2d2f0867e55f1` | `2126EB75FB3758057BE6647B158B7FD8469054402FC33D4612B186361E302A06` |
| `src/TheLogsAreWrong.PortableAuthority/Scheduler/DefaultIntakeAutoRouteContracts.cs` | A / PortableAuthority | transitive | `9411336f9954734bbbe231a1468f46cb1ab6d8b6` | `A02181950E314C8CAEBB2EB0753FB8E7479573E8FBEBA7CF5916970C86CCCEEE` |
| `src/TheLogsAreWrong.PortableAuthority/Scheduler/FeedDueResolutionContracts.cs` | A / PortableAuthority | transitive | `60d0d272af6b90b69b0386eb2d5cc6ba7ec75a00` | `4C5E892D3F22D15BB7FDA15EF5DA02B4E4EF80EAEA41B38515AAA3AED41DB0AB` |
| `src/TheLogsAreWrong.PortableAuthority/Scheduler/FeedPlanningContracts.cs` | A / PortableAuthority | transitive | `4f104ec36d32370493362cc5551674b584fbaee2` | `9605EF31F75E75F6D072D9945BBE93F94217D96B6233FD65D5042695B456B28B` |
| `src/TheLogsAreWrong.PortableAuthority/Scheduler/IntakeDeadlineContracts.cs` | A / PortableAuthority | transitive | `cb1bd7fdab285b8c82d36dd6c97985d784af4c04` | `E3757BA789398A7F840F8B3239D89709DC472228D9E58DE9F55F090731090320` |
| `src/TheLogsAreWrong.PortableAuthority/Scheduler/RepairPendingTransitionExecutionContracts.cs` | A / PortableAuthority | transitive | `b836fe4c6b706fa6887082c2d02cc3318562b69b` | `CC95F6B42E4E279250EC52F08F7AACA4698FBF63CEA9DA4A506F2CB58B2F8C26` |
| `src/TheLogsAreWrong.PortableAuthority/Scheduler/SawCycleContracts.cs` | A / PortableAuthority | transitive | `d178d8ee1e5f7b86d00884301ba1523831b78dc3` | `6FAC15D141BFEBA407F68AA4D76D1159F3387E87A0F7487341DDA73261697B43` |
| `src/TheLogsAreWrong.PortableAuthority/Time/SimulationTime.cs` | A / PortableAuthority | transitive | `f659420fd9d18d59030ffbdfda0b8b96e4850072` | `8F9BA4BA9C76F1A2422058C96BE1A93C1A72964F096352422ABF8BAF15F40A4C` |

The analyzer excluded six outer files as not reachable from this execution root:
`Configuration/Diagnostics/ConfigurationDiagnostics.cs`,
`Journal/ShiftReplayReducerContracts.cs`, `Journal/ShiftReplayReductionState.cs`,
`Journal/ShiftSnapshotCaptureContracts.cs`, `Journal/ShiftSnapshotContracts.cs`, and
`Journal/ShiftSnapshotRestoreContracts.cs`. It also correctly excludes the two
PortableAuthority support files from the logical source cut; compiler support is
recorded separately below.

## Candidate results

| Candidate | Source boundary | netstandard2.1 result | Unity / parity | Verdict |
| --- | --- | --- | --- | --- |
| H1 — separate portable host composition | 28 B files referencing existing A assembly | **FAIL under the current boundary** | Not run; no viable portable assembly exists without an API/friend-boundary change | Rejected for current evidence, not technically disproven |
| H2 — expand PortableAuthority | 26 A + 28 B = 54 logical authority files | PASS, 0 warnings/errors | Exact pinned Unity load + EditMode PASS; parity PASS | Technically viable scratch option only |
| H3 — portable/multi-target outer Domain | all 34 B files + existing 26 A = 60 logical files | PASS, 0 warnings/errors | Exact pinned Unity load + EditMode PASS; parity PASS | Technically viable scratch option only; wider than the cut |
| H4 — Unity-side orchestration copy | recreate stages 1 through 7 in Unity | Not implemented | N/A | **D-019 non-compliant / rejected**: creates a second semantic authoritative composition |

### H1 evidence

The 28-file byte-identical scratch `PortableHostComposition` referenced only the
existing PortableAuthority project. Its initial netstandard2.1 build failed with
204 errors: 195 `CS0117` (193 `ArgumentNullException.ThrowIfNull` plus two
friend-only static members), 2 `CS7036` (generic `Enum.IsDefined`), 4 `CS1061`,
2 `CS1503`, and 1 `CS0165`.

The non-framework blockers are intentional friend-boundary access from a
separate assembly. PortableAuthority currently has only
`[assembly: InternalsVisibleTo("TheLogsAreWrong.Domain")]`. The cut needs:

- `IntakeDeadlineStartService.StartFromRepairedAdmission`;
- `LineRuntimeState.TryGetActiveCause`;
- four `StartForAuthoritativeIntent` members (procedure, confirmation, repair,
  containment);
- `ShiftRuntimeState.TryGetLog(TargetId, out LogRuntimeState)`.

Thus an independently named H1 assembly cannot consume the exact current
PortableAuthority binary. It would require a separately reviewed
friend/public-API boundary change plus the 195 source compatibility replacements
listed below. That is outside this proof and no workaround was attempted.

### H2 evidence

H2 mechanically copied all 54 logical source files. The 26 A files were copied
from their current production portable form; the 28 B files were first copied
byte-identically. H2 uses `netstandard2.1`, `LangVersion=latest`, the four
existing compiler-metadata definitions (`IsExternalInit`, `RequiredMember`,
`CompilerFeatureRequired`, `SetsRequiredMembers`), and exactly one direct
package: `System.Collections.Immutable` `8.0.0`.

The exact current B cut required 195 scratch-only semantic-equivalent source
compatibility replacements:

| Replacement | Count |
| --- | ---: |
| `ArgumentNullException.ThrowIfNull` to explicit null-only guards | 193 |
| generic/inferred `Enum.IsDefined` to `Enum.IsDefined(typeof(TEnum), value)` | 2 |
| generic `Enum.GetValues` replacement | 0 |
| **H2 added total** | **195** |

Combined with the already accepted 26-file PortableAuthority baseline surface
(`131 + 25 + 1 = 157`), an eventual one-source H2 migration would have a known
352 replacement surface. No target-specific gameplay algorithm or second
authority source was created in this proof.

Fresh H2 build:

```text
dotnet build ExpandedPortableAuthority.csproj --configuration Release
Warnings: 0
Errors: 0
```

Resolved closure was exactly `System.Collections.Immutable/8.0.0`,
`System.Memory/4.5.5`, `System.Buffers/4.5.1`,
`System.Numerics.Vectors/4.4.0`, and
`System.Runtime.CompilerServices.Unsafe/6.0.0`; only Immutable was direct.

### H3 evidence

H3 copied all 34 outer Domain files (excluding ignored `bin/obj` generated
files) and referenced existing PortableAuthority under the established Domain
assembly identity. It is consequently 6 outer files wider than the measured
host cut, forcing Unity to consume configuration diagnostics plus replay/snapshot
surface that `HostTickExecutionService.Execute` does not require.

Its 34 B files required 228 scratch-only replacements: 222 null-only guards and
6 generic/inferred `Enum.IsDefined` replacements. Together with the existing
PortableAuthority 157 surface, a full portable outer-Domain approach exposes a
385 replacement surface. The project had no direct package; its PortableAuthority
reference resolved the same five-package Immutable closure.

```text
dotnet build PortableOuterDomain.csproj --configuration Release
Warnings: 0
Errors: 0
```

### H4 structural result

Production Unity source had zero `HostTickExecutionService` or stage-executor
source hits. Recreating `stage1 -> stage2 -> ... -> stage7` there would duplicate
the semantic orchestration that is presently uniquely owned by
`HostTickExecutionService`. A thin future Unity driver invoking one shared
portable host would not be H4; no driver or frame loop was created here.

## Pinned Unity proof and parity

The identical test-only fixture directly invokes
`HostTickExecutionService.Execute` once from a minimal deterministic
configuration, runs it twice, and projects LF-separated state rather than merely
checking a return value. It includes the execution and seven stage-result
identities, current tick, shift/state version, log/line/containment state, quota
target/progress, line noise, journal count/sequence/event/state-version/cause,
and checkpoint identity.

The canonical projection was:

```text
operation=HostTickExecutionService.Execute
stage_order=HostStageOneCompletionExecution>AcceptedIntentStageExecution>HostStageThreeDeadlineExecution>HostStageFourSawExecution>HostStageFiveFeedExecution>HostStageSixDerivedExecution>HostStageSevenPublished
tick=0
shift_id=TLAW063_PROBE_SHIFT
state_version=1
log_id=probe_log
log_state=SCHEDULED
line_state=LINE_CLEAR
containment_state=STABLE
quota_target_total=1
quota_credited_total=0
quota_correct_anomalies=0
line_noise=QUIET
journal_count=1
journal=1|FeedScheduled|0|1|-
checkpoint=HostTickCheckpointAdvanced
```

| Leg | Repeat deterministic | SHA-256 |
| --- | --- | --- |
| A — exact baseline net10 Domain | yes | `287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949` |
| B — H2 portable scratch | yes | `287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949` |
| C — H2 pinned Unity EditMode | yes | `287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949` |
| D — H3 portable scratch | yes | `287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949` |
| E — H3 pinned Unity EditMode | yes | `287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949` |

Each viable Unity project was newly created under `C:\Temp\TLAW-063`, used
baseline Packages/ProjectSettings only as scratch inputs, and had no production
Unity path changed. H2 loaded its one expanded portable authority DLL plus the
accepted Immutable/Unsafe closure; its EditMode test passed `1/1`. H3 loaded
scratch `TheLogsAreWrong.Domain.dll`, the existing
`TheLogsAreWrong.PortableAuthority.dll`, and the accepted Immutable/Unsafe
closure; its EditMode test passed `1/1` and asserted that Domain actually
references/consumes PortableAuthority. Both assembly-reference guards rejected
Unity, Fish, Steam, `Net.Http`, and `Sockets` names. No networking assembly was
required.

## Non-authoritative recommendation

**NON-AUTHORITATIVE UNTIL OWNER DECISION:** H2 is the narrower technically
viable scratch boundary (54 logical files) compared with H3’s 60-file
multi-target outer surface. H1 does not compile under the current internal
boundary, and H4 is D-019 non-compliant. This comparison is not an architecture
selection or acceptance, and does not authorize a production migration.

## Work not performed

No production `src/**`, csproj, props/targets, PortableHost project, Unity
`Assets/**`, `Packages/**`, `ProjectSettings/**`, scenes, prefabs, tests, or
package policy was changed. No host/frame loop, gameplay/input/presentation,
D-016, FishNet, FishySteamworks, Steamworks, networking, Gate 3 work,
`DECISIONS.md` change, Ready, merge, or cleanup occurred.

Repository regression, exact-head verification, Draft PR, and CI artifact
evidence are recorded in the finalization update to this dossier.

## Repository regression and finalization

The first evidence commit opened Draft PR #146 to `main`; its body is exactly
`Closes #145`. The PR remains Draft.

Before this final dossier commit, the clean evidence worktree passed:

| Gate | Result |
| --- | --- |
| `git diff --check` | PASS |
| `dotnet restore TheLogsAreWrong.sln` | PASS |
| `dotnet build TheLogsAreWrong.sln --configuration Release --no-restore` | PASS — 0 warnings, 0 errors |
| Full Release tests | PASS — 1633 passed, 0 failed, 0 skipped |
| D-014 snapshot/capture/restore/journal/replay (`Scope=TLAW-046`) | PASS — 87 passed, 0 failed, 0 skipped |
| Canonical production PortableAuthority regression | PASS — `CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F` |

Because a commit cannot know its own immutable object ID, exact-head
`Tlaw.Verify`, Gate0/object-reader, architecture/domain dependency checks, and
Repository verification are executed after this finalization commit against its
actual SHA and are reported with the Draft PR evidence. No later source change
is authorized or needed.

The candidate’s changed tracked-path inventory is exactly this dossier. Relative
to the baseline: `src/** = 0`, `unity/** = 0`, `Packages/** = 0`,
`ProjectSettings/** = 0`, scenes = 0, and prefabs = 0.

```text
UNITY_HOST_TICK_ARCHITECTURE_PROOF_PASS
NO_PRODUCTION_HOST_INTEGRATION
NO_GAMEPLAY_OR_NETWORKING
```
