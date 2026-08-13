# Gate 2 portable authority runtime/parity proof

Schema: `tlaw.gate2-portable-authority-runtime-parity-proof/v1`.

Date: `2026-08-13`.

## Scope and identity

| Field | Value |
| --- | --- |
| GitHub contract | [Issue #133](https://github.com/baroentgray/the-logs-are-wrong/issues/133) |
| Exact baseline | `f9bc1821037259a61da8c70c715c33061f5ad113` |
| Branch | `task/TLAW-057-portable-authority-runtime-parity-proof` |
| Worktree | `C:\Projects\TheLogsAreWrong-worktrees\TLAW-057` |
| Scratch root | `C:\Temp\TLAW-057` |
| .NET SDK | `10.0.103` |
| Unity executable | `C:\Program Files\Unity 6000.3.21f1\Editor\Unity.exe` |
| Unity identity | `6000.3.21f1_c02631ffc030` / changeset `c02631ffc030` |

D-018 authorizes this separately scoped, scratch-only portability proof over the
already-known 26-file authoritative cut. It does not accept Candidate A or B,
does not accept any Domain↔Unity production architecture, and does not authorize
production source, target, project, package, Unity, bridge, facade, adapter,
host/tick, gameplay, D-016, or networking migration.

## Exact authoritative source cut and byte-identity proof

The cut was reconstructed directly from the accepted TLAW-055 and TLAW-056
evidence, then copied from the exact baseline before any transformation. It is
exactly 26 files:

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

`source-copy-manifest.json` records the SHA-256 of every baseline source and
scratch copy before transformation: `26/26` byte-identical, `0` mismatches.

| Scratch evidence | SHA-256 |
| --- | --- |
| `evidence/source-copy-manifest.json` | `416491849A6E4A7AB577592F84025816DB24E2C336DC446D6128680A1CCC3804` |
| `evidence/enum-is-defined-semantic-inventory.json` | `5DDF2B7713770C686BF0353575B75192F4FCE78A382CE1995A00FC9CC97EB20D` |
| `evidence/transformation-manifest.json` | `573A4201C22C2CE08DF83064E373E4BCF72CA83051ED8A0E86B7EE4A56F20B96` |
| `evidence/transformation-validator.json` | `F485BF573AE73BA9A4C48AE7B0CEFF0AA90643B0E74E9158701C0FF8D39DE71E` |

## Fixed portable ingredients

The scratch project is `netstandard2.1` with `LangVersion=latest`, nullable and
implicit usings enabled, warnings treated as errors, and mechanical explicit
source inclusion. Its sole direct `PackageReference` is
`System.Collections.Immutable` `8.0.0`.

| Package evidence | Value |
| --- | --- |
| Requested / resolved package | `System.Collections.Immutable` `8.0.0` / `8.0.0` |
| NuGet provenance | `https://api.nuget.org/v3/index.json` |
| NuGet content hash | `AurL6Y5BA1WotzlEvVaIDpqzpIPvYnnldxru8oXJU2yFxFUy3+pNXjXd1ymO+RA0rq0+590Q8gaz2l3Sr7fmqg==` |
| Selected asset | `lib\netstandard2.0\System.Collections.Immutable.dll` |
| Selected asset identity | `System.Collections.Immutable, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a` |
| Selected asset SHA-256 | `5B1B1C83BA3D135C2FDFE425842FBE9C7432878B7E468623ACB554C69B4C130F` |

`obj/project.assets.json` selects that exact `lib/netstandard2.0` asset. No
other direct package was added.

Exactly these four scratch-only compiler metadata definitions are in
`portable/Support/CompilerCompatibility.cs`:

1. `System.Runtime.CompilerServices.IsExternalInit`.
2. `System.Runtime.CompilerServices.RequiredMemberAttribute`.
3. `System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute`.
4. `System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute`.

No collection replacement, helper library, shim beyond those four metadata
types, source generator, target-specific behavior, runtime/framework copy, or
additional package was used.

## Mechanical compatibility transformations

The transformation manifest records source path, original offset/line span,
before text, after text, class, and deterministic validation for every delta.
The validator reconstructs each transformed scratch source from its exact
baseline file and the recorded spans; it passed with `0` unapproved deltas.

| Authorized class | Count | Mechanical proof |
| --- | ---: | --- |
| `ArgumentNullException.ThrowIfNull` | 131 | Each direct statement became a null-only `if` guard throwing `ArgumentNullException` with the captured caller-expression parameter name. |
| `Enum.IsDefined<TEnum>` | 25 | A scratch Roslyn semantic inventory resolved each argument's enum type, then replaced the inferred generic form with `Enum.IsDefined(typeof(TEnum), value)`. |
| `Enum.GetValues<TEnum>` | 1 | Replaced with `(TEnum[])Enum.GetValues(typeof(TEnum))`; no unrelated object/enum edit was needed. |

The final validator result is `PASS`: 157 transformations across 19 of the 26
files, all within the three authorized classes. The full machine-readable
manifest remains only in `C:\Temp\TLAW-057` at the hashed paths above.

## Portable compile gate

Command:

~~~text
dotnet build C:\Temp\TLAW-057\portable\PortableAuthority.csproj --configuration Release --nologo --verbosity minimal
~~~

Result: `PASS` — restore completed, the exact portable assembly was produced,
and the full compiler summary reports `0` warnings and `0` errors.

| Portable output | Value |
| --- | --- |
| DLL | `portable/bin/Release/netstandard2.1/TheLogsAreWrong.PortableAuthority.Tlaw057.dll` |
| Identity | `TheLogsAreWrong.PortableAuthority.Tlaw057, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null` |
| SHA-256 | `00CF182E37451031AE2F16F950EB75E6102A6FC96FB3AB06B6CB4F4271334108` |
| Full build log SHA-256 | `329AD32E12B4E5F62A0B18D834FA4A850411FFFBA57F3230D1933E002BF02AAA` |

## Unity Editor load gate

The scratch harness is a mechanical copy of the exact-baseline accepted Gate-2
bootstrap at `unity/TheLogsAreWrong`. Its copied `ProjectVersion.txt` hash is
`C996A482FD926D9F094CFB99105C9319B992208C119A36427312AB8A04D25A42`
and its copied `Packages/manifest.json` hash is
`56494B2AC7B2B44A4A7A886A465803B99E65E57AFA7F21604A1D81A88E71E30B`;
both equal their exact-baseline sources. No production Unity file, Project
Settings value, API compatibility level, backend, reference-validation setting,
or package graph was changed.

The ordinary scratch layout contained only:

- the portable authority DLL above;
- the one selected `System.Collections.Immutable.dll` asset above;
- a minimal `[InitializeOnLoad]` hook that requires
  `ShiftRuntimeState`, `HostLogTransitionService`, and
  `LineNoiseDerivationService` to resolve and would emit
  `TLAW057_PORTABLE_LOAD_PASS` only after successful type load;
- a minimal EditMode assembly/test fixture, not executed after load failure.

The first import exposed only a scratch asmdef wiring omission: the test assembly
with explicit precompiled references had not listed the already-selected
`System.Collections.Immutable.dll`. The same selected DLL was added to that
scratch asmdef; no asset, version, package, Unity setting, or compatibility
workaround was added. Attempt-one log SHA-256:
`43248A1B11E994D1742284F9B48B5212AC485916A905A8F48719EA336F9DEB2A`.

The corrected pinned-Editor command was:

~~~text
C:\Program Files\Unity 6000.3.21f1\Editor\Unity.exe -batchmode -nographics -quit
  -projectPath C:\Temp\TLAW-057\UnityHarness
  -logFile C:\Temp\TLAW-057\evidence\unity-load-attempt-2.log
~~~

Its process exit code was `0`, but that is not accepted as load evidence. The
positive hook marker did not occur. Unity instead recorded this first material
loader blocker:

~~~text
Assembly 'Assets/Tlaw057/Plugins/System.Collections.Immutable.dll'
will not be loaded due to errors:
Unable to resolve reference 'System.Runtime.CompilerServices.Unsafe'.
Is the assembly missing or incompatible with the current platform?
~~~

Consequently Unity also reported references-with-errors for the portable DLL,
the scratch Editor assembly, and the EditMode test assembly. No
`System.Runtime.CompilerServices.Unsafe` DLL was copied, no further dependency
was chased, and no compatibility setting was changed. Attempt-two log SHA-256:
`2DEE2DC72472172BEE66425CDF547E3C6ECFA837306BAB439D86939B9C153067`.

## Downstream stages

| Stage | Result |
| --- | --- |
| Portable compile | RUN — PASS. |
| Pinned Unity Editor load | RUN — blocked at the first material dependency loader error above. |
| EditMode real authority chain | NOT RUN — load gate did not prove the assembly or required types loadable. |
| Fresh exact-baseline net10 vector | NOT RUN — contract sequences it only after successful EditMode authority execution. |
| Exact net10/Unity parity comparison | NOT RUN — no Unity authoritative execution exists. |

The prepared but unexecuted scratch fixture invokes only the required real
authority chain, with no mock or facade substitute:

~~~text
ShiftRuntimeState.Create
-> HostLogTransitionService.Apply
-> SawCycleStartService.Start
-> LineNoiseDerivationService.Evaluate
~~~

Its canonical target projection is retained only as the contract fixture; it
was not claimed as fresh net10 or Unity evidence in this stopped run.

## Repository scope, verification, and deviations

The exact tracked changed-path set is:

~~~text
docs/agent/GATE2_PORTABLE_AUTHORITY_RUNTIME_PARITY_PROOF.md
~~~

All sources, transformed copies, projects, package artifacts, logs, Unity
harness files, and fixture files are scratch-only under `C:\Temp\TLAW-057`.
No tracked production Domain, tests, Unity, project/target/package,
`Directory.Build.props`, `global.json`, `tools/**`, `.github/**`, Gate-0, or
`DECISIONS.md` path changed. No production migration or architecture decision is
authorized. The sole deviation is the documented scratch asmdef reference-list
correction before the valid load attempt; it added no dependency or setting.

UNITY_LOAD_BLOCKED
NO_ARCHITECTURE_DECISION_ACCEPTED
