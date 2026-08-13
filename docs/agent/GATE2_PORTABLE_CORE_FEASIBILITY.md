# Gate 2 portable authoritative core feasibility probe

Schema: tlaw.gate2-portable-core-feasibility/v1.

Date: 2026-08-13.

## Scope and identity

| Field | Value |
| --- | --- |
| GitHub contract | [Issue #129](https://github.com/baroentgray/the-logs-are-wrong/issues/129) |
| Exact baseline | 4986990a61d5f672172c45d8f0a7a35b81eeac1d |
| Candidate | This commit, the head of task/TLAW-055-portable-core-feasibility. Its exact SHA is bound externally by the Draft PR, exact-head verification, and executor handoff because a commit cannot contain its own resulting object ID. |
| Branch | task/TLAW-055-portable-core-feasibility |
| Worktree | C:\Projects\TheLogsAreWrong-worktrees\TLAW-055 |
| Scratch root | C:\Temp\TLAW-055 |
| .NET SDK | 10.0.103 |
| Unity executable | C:\Program Files\Unity 6000.3.21f1\Editor\Unity.exe |
| Unity product / file version | 6000.3.21f1_c02631ffc030 / 6000.3.21.12592689 |
| Unity project pin | 6000.3.21f1 (c02631ffc030) |

This is scratch-only feasibility evidence. It neither accepts nor implements a
portable-core, bridge, facade, adapter, host, tick loop, intent routing, event
projection, gameplay, D-016 behavior, networking, FishNet, or Steamworks.
The production Domain and Unity projects remain byte-identical to the baseline.

## Baseline and scratch provenance

The baseline Domain is a pure Gate-1 net10.0 project: it has no
PackageReference, Unity reference, or Unity dependency. Its production source
was read only from the exact-baseline worktree above. Scratch contains a
semantic inventory tool, copied source, scratch project metadata, and compiler
logs only; no scratch artifact is tracked.

The retained scratch evidence is:

~~~text
C:\Temp\TLAW-055\analysis\SourceInventory\SourceInventory.csproj
C:\Temp\TLAW-055\analysis\SourceInventory\Program.cs
C:\Temp\TLAW-055\evidence\domain-source-inventory.json
C:\Temp\TLAW-055\portable-core\PortableCore.csproj
C:\Temp\TLAW-055\portable-core\Source\
C:\Temp\TLAW-055\evidence\portable-core-netstandard-build.log
~~~

domain-source-inventory.json is the retained machine-readable detail output:
it records every source file's declared types, semantic references to production
Domain types and source files, compiler-feature flags, the selected closure,
source cycles, semantic-analysis diagnostics, and the per-file SHA-256 copy
comparison.

## Probe A — full Domain portability/blocker inventory

The scratch analyzer parsed all 60 production src/TheLogsAreWrong.Domain/**
.cs files with the installed compiler's CSharpCompilation and framework
metadata. It obtained direct Domain source/type edges semantically, not by text
search alone. The full-Domain semantic compilation had 0 errors under the
current net10.0 framework references.

| Inventory surface | Full Domain files | Selected closure files | Compiler evidence in the portable compile |
| --- | ---: | ---: | --- |
| System.Collections.Immutable use | 23 | 9 | 8 CS0234 namespace errors and 80 CS0246 immutable-type errors |
| Record declarations | 39 | 25 | Contribute to missing IsExternalInit support |
| Explicit init accessors | 2 | 1 | Included in missing IsExternalInit support |
| required members | 2 | 1 | 6 CS0656 compiler-required support-member errors |
| Semantic Domain source graph | 60 files | 26 files | 0 semantic-analysis errors before portable target substitution |

The portable compiler evidence is decisive for the selected closure. The
netstandard2.1 reference set does not provide System.Collections.Immutable,
System.Runtime.CompilerServices.IsExternalInit, RequiredMemberAttribute,
CompilerFeatureRequiredAttribute, or SetsRequiredMembersAttribute. No package,
shim, polyfill, source generator, conditional source path, or source rewrite
was added to change that result.

## Probe B — smallest representative authoritative execution closure

### Seed selection

The seed is the existing production operation:

~~~text
TheLogsAreWrong.Domain.Line.LineNoiseDerivationService.Evaluate(
    LineNoiseRuntimeState runtime,
    ShiftRuntimeState shiftState,
    MovementNoiseRuntimeState movementNoiseRuntime,
    ServerTick currentTick)
~~~

This is not a DTO/envelope-only path. It is the deterministic stage-6
authoritative derivation used by HostStageSixDerivedExecutor: it validates the
exact authoritative ShiftRuntimeState, derives saw/movement/repair noise
sources, and returns the next immutable LineNoiseRuntimeState plus its typed
evaluation result. The current focused tests prove its three-source truth table,
same-tick identity behavior, failure-closed validation, and deterministic
equivalent sequences; the stage-6 tests retain it in the frozen authoritative
host ordering.

The selected operation itself neither accepts an intent contract nor emits an
event contract. Therefore no intent/event contract is omitted from an operation
that uses one; its current authority participation is state derivation from the
post-stage-5 state and movement evidence.

### Exact transitive source closure

The semantic source/type graph reaches exactly these 26 production files from
the seed. All copies under C:\Temp\TLAW-055\portable-core\Source are
byte-identical to their production counterparts: 26/26 SHA-256 and byte
comparisons matched, with 0 mismatches.

~~~text
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
~~~

The retained JSON supplies the exact type-level edges. In particular, the
closure includes LineNoiseDerivationService, LineNoiseRuntimeState,
LineNoiseEvaluationResult, ShiftRuntimeState, LogRuntimeState,
MovementNoiseRuntimeState, LineRuntimeState, active saw/repair evidence,
configuration, identifiers, enums, and simulation-time primitives actually
referenced by the seed's production declarations and method bodies.

### Source cycles and excluded files

Two source strongly connected components prevent a smaller exact source cut:

~~~text
Anomalies/AnomalyResolutionContracts.cs
Anomalies/ConfirmationTestContracts.cs
Containment/ContainmentLifecycleContracts.cs
Line/LineJamRepairContracts.cs
Line/LineNoiseRuntimeContracts.cs
Line/MovementNoiseRuntimeContracts.cs
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

Primitives/Primitives.cs
Time/SimulationTime.cs
~~~

The remaining 34 production files are not semantically reachable from the
selected operation, so they are excluded for these exact reasons:

| Excluded group | Files | Why not in the seed closure |
| --- | --- | --- |
| Caller composition and alternate runtime handlers | Runtime/AcceptedIntentStageExecutionContracts.cs, ConfirmationTestIntentHandler.cs, ContainmentRitualIntentHandler.cs, HostStageFiveFeedExecutionContracts.cs, HostStageFourSawExecutionContracts.cs, HostStageOneCompletionExecutionContracts.cs, HostStageSevenEventExecutionContracts.cs, HostStageSixDerivedExecutionContracts.cs, HostStageThreeDeadlineExecutionContracts.cs, HostTickCompletionCheckpointContracts.cs, HostTickExecutionContracts.cs, LineRepairIntentHandler.cs, ProcedureActionIntentHandler.cs, SawQuotaApplicationContracts.cs, ShiftCompletionContracts.cs | They call, compose around, or handle other host/intent paths; the selected direct derivation does not reference their declarations. |
| Intent ingress contracts | Intents/AcceptedIntentBatchContracts.cs, ConfirmationTestIntentContracts.cs, ContainmentRitualIntentContracts.cs, LineRepairIntentContracts.cs, ProcedureActionIntentContracts.cs | The selected Evaluate signature and body use no intent contract. |
| Journal, replay, and snapshot machinery | Journal/EventJournal.cs, Journal/JournaledMutationCommitContracts.cs, Journal/ReplayContracts.cs, Journal/ShiftReplayReducerContracts.cs, Journal/ShiftReplayReductionState.cs, Journal/ShiftSnapshotCaptureContracts.cs, Journal/ShiftSnapshotContracts.cs, Journal/ShiftSnapshotRestoreContracts.cs | Persistence/replay/snapshot paths do not participate in direct line-noise derivation. |
| Independent diagnostics, derivation, planning, and sequencing | Configuration/Diagnostics/ConfigurationDiagnostics.cs, Scheduler/FeedGateJamDerivationContracts.cs, Scheduler/IntakeAutoFeedJamDerivationContracts.cs, Scheduler/RepairAutoFeedNormalFeedPlanningContracts.cs, Scheduler/RepairFeedGateIntakeDeadlineContracts.cs, Sequencing/SequencingContracts.cs | They are separate diagnostics or stage families; the selected operation consumes retained state rather than invoking them. |

The result is meaningfully bounded (26/60 source files) and contains real
authoritative state derivation, but the exact-source portable compile below
still decides feasibility.

## Probe C — exact-source portable compile

The scratch project metadata was exactly:

~~~xml
<TargetFramework>netstandard2.1</TargetFramework>
<LangVersion>latest</LangVersion>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<EnableDefaultCompileItems>false</EnableDefaultCompileItems>
~~~

It includes exactly Source\**\*.cs, the 26 byte-identical production source
copies. EnableDefaultCompileItems=false is mechanical inclusion metadata that
avoids SDK duplicate inclusion; it is not a source or portability workaround.
dotnet list ... package reported no packages for netstandard2.1; the project
contains no PackageReference.

The contracted build command was:

~~~text
dotnet build C:\Temp\TLAW-055\portable-core\PortableCore.csproj --nologo --verbosity minimal
~~~

Result: FAIL — 363 distinct compiler errors, 0 warnings. The exact blocker
surface is:

| Category | Error count | Representative exact compiler excerpt |
| --- | ---: | --- |
| Missing immutable namespace | 8 CS0234 | AnomalyResolutionContracts.cs(1,26): error CS0234: The type or namespace name 'Immutable' does not exist in the namespace 'System.Collections' |
| Missing immutable types | 80 CS0246 | ShiftRuntimeState.cs(61,9): error CS0246: The type or namespace name 'ImmutableArray<>' could not be found |
| Missing record/init compiler support | 269 CS0518 | ValidatedConfiguration.cs(11,13): error CS0518: Predefined type 'System.Runtime.CompilerServices.IsExternalInit' is not defined or imported |
| Missing required-member compiler support | 6 CS0656 | EventContracts.cs(34,22): error CS0656: Missing compiler required member 'System.Runtime.CompilerServices.RequiredMemberAttribute..ctor' |

The full native compiler output is retained at
C:\Temp\TLAW-055\evidence\portable-core-netstandard-build.log. No package,
System.Collections.Immutable reference, shim, polyfill, target change, source
edit, or additional candidate was attempted after this material blocker surface.

## Probe D — Unity load, EditMode operation, and parity

**NOT RUN.** Probe C did not produce a clean portable DLL. Therefore no fresh
Unity project was created, no Editor load was attempted, and no EditMode test
ran. The selected baseline operation and a baseline-versus-Unity canonical
state/result/event projection comparison are also **NOT RUN**.

## Repository verification

The committed candidate was checked against the exact baseline with the
required commands and all passed:

| Check | Result |
| --- | --- |
| git diff --name-only 4986990a61d5f672172c45d8f0a7a35b81eeac1d...HEAD | Exactly docs/agent/GATE2_PORTABLE_CORE_FEASIBILITY.md |
| git diff --check | PASS |
| dotnet build TheLogsAreWrong.sln --configuration Release | PASS |
| dotnet test TheLogsAreWrong.sln --configuration Release | PASS — at least 1631 passed, no failures or skip regression |
| Exact-head Tlaw.Verify | PASS |
| Gate 0 / Git object reader | PASS |
| Production Directory.Build.props, Domain source/project, tests, and unity/TheLogsAreWrong/** | Byte-identical to baseline |

The exact candidate SHA, verifier output, CI workflow/job/artifact/digest, and
Draft PR identity are retained outside this self-referential commit in the
executor handoff and PR evidence.

## Changed path, deviations, and boundaries

The exact tracked changed-path set is:

~~~text
docs/agent/GATE2_PORTABLE_CORE_FEASIBILITY.md
~~~

Deviation: the scratch project's first build exposed duplicate SDK Compile item
enumeration. The metadata-only EnableDefaultCompileItems=false setting was added
before the single actual portable source compile. It changed no source input and
did not repair a portability blocker. No other deviation occurred.

Not performed: production Domain/Unity/Directory.Build.props/test/decision-log
edits; multitargeting; packages; immutable binaries; shims; polyfills; source
generators; source rewrites; bridge/facade/adapter work; host/tick integration;
gameplay; D-016; networking; FishNet; Steamworks; Unity settings or reference
validation changes; direct-net10.0 dependency chasing; Grok; Ready; merge; or
cleanup.

NO_ARCHITECTURE_DECISION_ACCEPTED

PORTABLE_CORE_COMPILE_FAIL
