# Gate 2 — Production host-tick PortableAuthority migration (TLAW-064)

This records the separately authorized production H2 migration increment selected
by D-020. It establishes production H2 source ownership and refreshed derived
Unity plugin parity only. It does **not** establish a running Unity host loop.

## Execution identity

| Item | Value |
| --- | --- |
| Issue | [#147](https://github.com/baroentgray/the-logs-are-wrong/issues/147) |
| Pull request | [#148](https://github.com/baroentgray/the-logs-are-wrong/pull/148) (Draft) |
| Authoritative baseline / `origin/main` | `e3805aaa612acaa7e23c2a8bcb4a21c8331cd51a` |
| Branch | `task/TLAW-064-production-host-tick-portable-authority-migration` |
| Worktree | `C:\Projects\TheLogsAreWrong-worktrees\TLAW-064` |
| Unity | `6000.3.21f1`, changeset `c02631ffc030` |
| .NET SDK | `10.0.103` |

## Phase 0 — fail-closed production inventory

All twelve Phase-0 conditions passed before any production modification.

`git fetch origin --prune` resolved `origin/main` to the exact baseline
`e3805aaa612acaa7e23c2a8bcb4a21c8331cd51a`. The new worktree was clean and on the
required branch.

The accepted TLAW-062 production plugin directory remained exactly three DLLs:

```text
System.Collections.Immutable.dll
System.Runtime.CompilerServices.Unsafe.dll
TheLogsAreWrong.PortableAuthority.dll
```

Production references remained `TheLogsAreWrong.Domain -> TheLogsAreWrong.PortableAuthority`
with no reverse project reference. The host root existed at
`src/TheLogsAreWrong.Domain/Runtime/HostTickExecutionContracts.cs`, and its declared
and invoked frozen order was read directly from source as stages one through seven.

The exact transitive cut was re-verified against the accepted TLAW-063 inventory
table: **54 logical source files = 26 PortableAuthority (A) + 28 outer Domain (B)**.
Every one of the 54 files was byte-identical to the accepted inventory — all 54
recorded Git blob IDs and SHA-256 values matched the baseline and the worktree with
**zero drift**.

The regenerated compatibility manifest over the exact 28-file move set produced
exactly:

| Replacement | Count |
| --- | ---: |
| `ArgumentNullException.ThrowIfNull` | 193 |
| generic/inferred `Enum.IsDefined` | 2 |
| `Enum.GetValues` | 0 |
| **Total** | **195** |

The existing 26-file PortableAuthority accepted surface showed no drift at
`131 + 25 + 1 = 157` (131 explicit `ArgumentNullException` guard sites, 25
`Enum.IsDefined(typeof(..))`, 1 `Enum.GetValues(typeof(..))`). Its project/target/
package graph remained the accepted TLAW-061/TLAW-062 boundary: `netstandard2.1`,
exactly one direct `PackageReference` `System.Collections.Immutable 8.0.0`, no
`ProjectReference`. The resolved closure was exactly the accepted five packages.

Pinned Unity version and changeset matched. No FishNet, FishySteamworks, Steamworks
or other networking dependency existed in Gate 2; the only textual matches were the
existing forbidden-list assertions in `Gate2BootstrapSmokeTests`, which prove
absence.

### Recorded baseline regression state

| Evidence | Baseline |
| --- | --- |
| Release build | PASS, 0 warnings, 0 errors |
| Full tests | `1633 passed / 0 failed / 0 skipped` |
| D-014 `Scope=TLAW-046` | `87 / 87` |

## Phase 1 — atomic production H2 migration

The exact accepted 28 files were moved with `git mv` (move, not copy) from
`src/TheLogsAreWrong.Domain/**` to the corresponding relative paths under
`src/TheLogsAreWrong.PortableAuthority/**`:

```text
Intents/AcceptedIntentBatchContracts.cs
Intents/ConfirmationTestIntentContracts.cs
Intents/ContainmentRitualIntentContracts.cs
Intents/LineRepairIntentContracts.cs
Intents/ProcedureActionIntentContracts.cs
Journal/EventJournal.cs
Journal/JournaledMutationCommitContracts.cs
Journal/ReplayContracts.cs
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

Git recorded all 28 as renames. Existing `TheLogsAreWrong.Domain.*` namespaces,
public/internal semantics, type names and orchestration behavior were preserved;
this was an ownership move, not an API or namespace redesign. The friend-only
PortableAuthority members that TLAW-063 identified as the H1 blocker
(`IntakeDeadlineStartService.StartFromRepairedAdmission`,
`LineRuntimeState.TryGetActiveCause`, the four `StartForAuthoritativeIntent`
members, `ShiftRuntimeState.TryGetLog`) became same-assembly accesses, so no
friend/public-API boundary change was needed. `[assembly: InternalsVisibleTo("TheLogsAreWrong.Domain")]`
continues to give the retained outer-Domain files their existing access.

The six explicitly excluded outer-Domain files remain outer-Domain-owned and were
not moved or transformed:

```text
Configuration/Diagnostics/ConfigurationDiagnostics.cs
Journal/ShiftReplayReducerContracts.cs
Journal/ShiftReplayReductionState.cs
Journal/ShiftSnapshotCaptureContracts.cs
Journal/ShiftSnapshotContracts.cs
Journal/ShiftSnapshotRestoreContracts.cs
```

### Compatibility transformation manifest

Exactly the accepted 195 semantic-equivalent replacements were applied to the moved
production source, and nothing else:

| Transformation | Applied |
| --- | ---: |
| `ArgumentNullException.ThrowIfNull(x);` to `if (x is null) { throw new ArgumentNullException("x"); }` | 193 |
| inferred `Enum.IsDefined(v)` to `Enum.IsDefined(typeof(LogState), v)` | 2 |
| `Enum.GetValues` | 0 |
| **Total** | **195** |

Every replaced call was single-argument, so each guard preserves the exact
`paramName` that `CallerArgumentExpression` produced. Both `Enum.IsDefined` sites
were `LogState` operands on one line of
`Runtime/HostStageSevenEventExecutionContracts.cs`.

Post-migration validator result over `src/TheLogsAreWrong.PortableAuthority/**`:

| Measure | Value |
| --- | ---: |
| explicit `ArgumentNullException` guard sites | 324 (131 existing + 193 moved) |
| `Enum.IsDefined(typeof(..))` | 27 (25 + 2) |
| `Enum.GetValues(typeof(..))` | 1 |
| **Combined accepted surface** | **352** |
| non-portable API residue (`ThrowIfNull`, `Enum.IsDefined<>`, `Enum.GetValues<>`) | **0** |

There were zero unapproved deltas. The 29 remaining `ThrowIfNull` occurrences in
the six retained outer-Domain files were intentionally left untouched: those files
stay net10 Domain-owned and are outside the portable cut.

## Phase 2 — production .NET and architecture proof

| Check | Result |
| --- | --- |
| `TheLogsAreWrong.PortableAuthority` independent `netstandard2.1` build | PASS, 0 warnings, 0 errors |
| Direct `PackageReference` count | exactly 1 — `System.Collections.Immutable 8.0.0` |
| Resolved closure | `System.Collections.Immutable 8.0.0`, `System.Memory 4.5.5`, `System.Buffers 4.5.1`, `System.Numerics.Vectors 4.4.0`, `System.Runtime.CompilerServices.Unsafe 6.0.0` |
| Dependency direction | `TheLogsAreWrong.Domain -> TheLogsAreWrong.PortableAuthority` only, no reverse |
| Duplicate moved source in outer Domain | 0 |
| Production host-tick root owner | `src/TheLogsAreWrong.PortableAuthority/Runtime/HostTickExecutionContracts.cs` |
| UnityEngine / FishNet / Steamworks / networking in PortableAuthority | none |
| Six excluded Domain files outside PortableAuthority | confirmed |

That the migrated PortableAuthority compiles standalone for `netstandard2.1` is
itself the transitive-closure proof: the 54-file boundary needs nothing from outer
Domain.

Post-move ownership: `src/TheLogsAreWrong.PortableAuthority` holds **56** `.cs`
files (the 54 logical authority files plus `Support/AssemblyInfo.cs` and
`Support/CompilerCompatibility.cs`); `src/TheLogsAreWrong.Domain` holds **6**.

Zero duplicate orchestration exists. Across `src/**` and
`unity/TheLogsAreWrong/Assets/**` there is exactly one declaration of
`HostTickExecutionService` and exactly one declaration of each of the seven stage
executors.

## Phase 3 — deterministic regressions

| Evidence | Baseline | Candidate | Result |
| --- | --- | --- | --- |
| Full Release build | 0 / 0 | 0 warnings, 0 errors | PASS |
| Full .NET tests | `1633 / 0 / 0` | `1633 passed / 0 failed / 0 skipped` | PASS, no regression |
| D-014 `Scope=TLAW-046` | `87 / 87` | `87 passed / 0 failed / 0 skipped` | PASS |
| Canonical PortableAuthority authority regression | — | `CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F` | PASS |
| Frozen seven-stage order | — | unchanged, read from source | PASS |
| Gate 0 / object reader | `52 / 52` | `52 / 52` | PASS |
| Architecture / Domain dependency guards | PASS | PASS | PASS |
| `git diff --check` | — | clean | PASS |

The total test count is unchanged at 1633: no committed .NET test was added or
removed, only ownership wiring was repointed.

### Production net10 host-tick parity

A fresh production net10 invocation of the accepted TLAW-063 host-tick fixture
through the moved, shared `HostTickExecutionService.Execute` produced the exact
canonical projection and SHA:

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

SHA-256 `287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949`,
repeat-deterministic across two runs in one process.

The TLAW-063 scratch fixture was not retained by that task's accepted cleanup, so
the fixture was reconstructed from the accepted projection contract. The
reconstruction is pinned rather than fitted: the projection is invariant across
every positive `InitialAdmissionDelaySeconds` (verified at 1, 2, 5 and 30), and the
supplied event-identity count is fixed at one by the executor's own plan
validation. The reconstruction was run as a non-committed scratch console harness
outside the repository, so it adds no test and does not alter the 1633 floor.

## Phase 4 — derived Unity plugin refresh and bounded parity proof

The three-DLL production binary boundary was preserved exactly.

| Plugin | Before | After | Status |
| --- | --- | --- | --- |
| `TheLogsAreWrong.PortableAuthority.dll` | `57ECAB9DA135DE17147F03272DD8429535FF68022194DB8A092BC43B2B14ECBB` | `F51EA9509EC280F3B9C930B58419144A517477A0E26D9844905E5B3175B487CB` | refreshed |
| `System.Collections.Immutable.dll` | `5B1B1C83BA3D135C2FDFE425842FBE9C7432878B7E468623ACB554C69B4C130F` | unchanged | byte-identical |
| `System.Runtime.CompilerServices.Unsafe.dll` | `01748200F2400C742AA689F1F5101BD6298EFDFD92C00C18F4FA473847235BA9` | unchanged | byte-identical |

The refreshed plugin is byte-identical to the fresh candidate deployment build of
the migrated source, produced with the accepted deployment properties
(`-p:IncludeSourceRevisionInInformationalVersion=false -p:DebugSymbols=false`);
that build is reproducible, re-emitting the identical `F51EA950…` output. Assembly
identity remained `TheLogsAreWrong.PortableAuthority`, version `1.0.0.0`,
informational version `1.0.0`, size 544768 bytes. No `.meta` file required or
received any change. `TheLogsAreWrong.Domain.dll` was not imported, and no
`System.Memory.dll`, `System.Buffers.dll`, `System.Numerics.Vectors.dll` or fourth
plugin dependency was added.

One bounded EditMode proof file was added,
`unity/TheLogsAreWrong/Assets/Gate2/Tests/Editor/HostTickImportParityTests.cs`
(plus its `.meta`). It invokes the shared imported production
`HostTickExecutionService.Execute` once per run with the accepted fixture and
supplies only input values and projection formatting; it recreates no host-stage
decision and no orchestration. The existing test asmdef already referenced the
PortableAuthority plugin, so no assembly-definition change was needed.

Pinned Unity EditMode result: **13 / 13 passed, 0 failed, 0 skipped**, including
the existing TLAW-062 import/parity contracts and both new host-tick contracts. The
imported host tick reproduced
`287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949` and was
repeat-deterministic.

Windows x64 Development player build: `BUILD_RESULT=Succeeded`,
`BUILD_ERRORS=0 BUILD_WARNINGS=0`, size 146406146. The packaged
`TheLogsAreWrongGate2Bootstrap_Data/Managed/TheLogsAreWrong.PortableAuthority.dll`
hashed `F51EA950…`, equal to the refreshed candidate. The bootstrap/PortableAuthority
smoke passed with the refreshed DLL:

```text
TLAW062_PLAYER_PORTABLE_LOAD_PASS
TLAW062_PLAYER_AUTHORITY_PASS
TLAW062_PLAYER_AUTHORITY_SHA=CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F
```

No fourth dependency, setting change or workaround was required to load and execute
the moved host composition inside the exact three-plugin boundary.

## Changed-path contract

The expected baseline-to-candidate tracked change set is exactly the 28 renames plus:

```text
docs/agent/GATE2_PRODUCTION_HOST_TICK_PORTABLE_AUTHORITY_MIGRATION.md
tests/TheLogsAreWrong.Domain.Tests/Architecture/ArchitectureGuardTests.cs
tests/TheLogsAreWrong.Domain.Tests/Architecture/Tlaw013ArchitectureTests.cs
tests/TheLogsAreWrong.Domain.Tests/Architecture/Tlaw015ArchitectureTests.cs
tests/TheLogsAreWrong.Domain.Tests/Architecture/Tlaw018ArchitectureTests.cs
tests/TheLogsAreWrong.Domain.Tests/Architecture/Tlaw020ArchitectureTests.cs
tests/TheLogsAreWrong.Domain.Tests/Architecture/Tlaw021ArchitectureTests.cs
tests/TheLogsAreWrong.Domain.Tests/Architecture/Tlaw046ArchitectureTests.cs
unity/TheLogsAreWrong/Assets/Gate2/Plugins/PortableAuthority/TheLogsAreWrong.PortableAuthority.dll
unity/TheLogsAreWrong/Assets/Gate2/Tests/Editor/HostTickImportParityTests.cs
unity/TheLogsAreWrong/Assets/Gate2/Tests/Editor/HostTickImportParityTests.cs.meta
```

Counts against the exact baseline: `Packages/** = 0`, `ProjectSettings/** = 0`,
scenes `= 0`, prefabs `= 0`, `*.csproj|*.props|*.targets = 0`, existing `.meta`
changes `= 0`. No networking dependency was introduced. The protected pre-existing
untracked paths `.claude/`, `.review-artifact-016/`, `.review-artifact-017/`,
`.review-artifact-018/` and `prototype/` were not touched.

### Test wiring changed by the ownership move

Seven existing architecture/source-inventory guards asserted the old file
ownership and were repointed mechanically, with assertions preserved or widened and
no architecture or behavior change:

| Guard | Mechanical change |
| --- | --- |
| `Tlaw015`, `Tlaw018`, `Tlaw020`, `Tlaw021` | moved-source path repointed from `TheLogsAreWrong.Domain/Scheduler` to `TheLogsAreWrong.PortableAuthority/Scheduler` |
| `Tlaw013` | non-vacuity probe repointed to the `PortableAuthority/Runtime` directory that now owns those files |
| `Tlaw046` | exported-type scan widened to span both production owners, so it stays non-vacuous |
| `ArchitectureGuardTests` | accepted owned-cut list extended from 26 to 54 entries; outer-Domain source count expectation moved from 34 to 6; method renamed to `..._accepted_54_file_cut` so the name stays factual |

## Deviations and retries

Two environment/process deviations occurred. Neither is a candidate defect and
neither required a scope change.

1. The first Unity EditMode attempt reported `12/13`, failing only
   `Committed_portable_plugin_is_byte_identical_to_the_fresh_candidate_release_output`.
   Cause: the scratch net10 parity harness holds a project reference to Domain, so
   running it transitively rebuilt PortableAuthority *without* the deployment
   properties and overwrote the canonical `bin/Release/netstandard2.1` output that
   the test compares against. Re-running the deployment build restored a
   byte-identical output, and attempt 2 passed `13/13`. Both host-tick contracts
   passed on both attempts.
2. The first Windows player build attempt died with an internal
   `bee_backend` error (`ExitCode: -1`, "The backend process appears to still be
   running"), the known process-kill flakiness of this machine. Attempt 2 succeeded
   with 0 errors and 0 warnings. This is environment evidence, not a defect.

## Work not performed

This increment performed no production Unity host driver, no `MonoBehaviour` or
frame-driven tick loop, no persistent Unity simulation/host state, no gameplay,
input, interaction or presentation binding, no D-016 execution, no FishNet,
FishySteamworks, Steamworks, transport, networking or Gate 3 work, no `Packages/**`
or `ProjectSettings/**` change, no scene or prefab change, no import of outer
`TheLogsAreWrong.Domain.dll`, no fourth Unity plugin dependency, no move of the six
excluded outer-Domain files, no namespace/API redesign, no new project or package
architecture, no unrelated refactor, and no rewrite of D-019 or D-020. Ready, merge
and cleanup were not requested or performed; they remain separate owner gates.

## Terminal

```text
PRODUCTION_HOST_TICK_PORTABLE_AUTHORITY_MIGRATION_PASS
SINGLE_HOST_TICK_AUTHORITY_PRESERVED
NO_HOST_LOOP_GAMEPLAY_OR_NETWORKING
```

## Sources

[Issue #147](https://github.com/baroentgray/the-logs-are-wrong/issues/147),
[PR #148](https://github.com/baroentgray/the-logs-are-wrong/pull/148),
[D-019](DECISIONS.md#d-019--owner-selects-extracted-portable-authoritative-core-for-domainunity),
[D-020](DECISIONS.md#d-020--owner-selects-h2-for-the-unity-host-tick-composition-boundary),
[TLAW-063 host-tick architecture proof](GATE2_UNITY_HOST_TICK_ARCHITECTURE_PROOF.md),
[TLAW-061 production portable authority migration](GATE2_PRODUCTION_PORTABLE_AUTHORITY_MIGRATION.md), and
[TLAW-062 production Unity portable authority import](GATE2_PRODUCTION_UNITY_PORTABLE_AUTHORITY_IMPORT.md).
