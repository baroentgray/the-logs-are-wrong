# Gate 2 Domain–Unity in-process architecture decision dossier

Schema: tlaw.gate2-domain-unity-architecture-decision/v1.

Date: 2026-08-13.

## Phase A scope and identity

| Field | Value |
| --- | --- |
| GitHub contract | [Issue #131](https://github.com/baroentgray/the-logs-are-wrong/issues/131) |
| Exact baseline | edbb0d4cfcccec59fd6f3a5426b7c5f6d154508d |
| Candidate | This commit, the head of task/TLAW-056-domain-unity-architecture-decision. Its exact SHA is bound by the Draft PR, exact-head verification, and executor handoff because a commit cannot contain its own resulting object ID. |
| Branch | task/TLAW-056-domain-unity-architecture-decision |
| Worktree | C:\Projects\TheLogsAreWrong-worktrees\TLAW-056 |
| Scratch root | C:\Temp\TLAW-056 |
| .NET SDK | 10.0.103 |
| Unity executable | C:\Program Files\Unity 6000.3.21f1\Editor\Unity.exe |
| Unity product / file version | 6000.3.21f1_c02631ffc030 / 6000.3.21.12592689 |
| Unity project pin | 6000.3.21f1 (c02631ffc030) |

This is a Phase-A decision dossier, not production implementation or
architecture acceptance. It creates no production bridge, portable/core/facade
project, host, tick loop, gameplay, D-016 control surface, networking,
FishNet, Steamworks, or Unity setting change. D-018 is not appended: no owner
architecture selection has occurred.

Preserved: one local Unity process and one local authoritative host for Gate 2;
pure deterministic authority testable outside Unity; separate immutable
ShiftRuntimeState and QuotaRuntimeState under D-013; full snapshot/replay under
D-014; unchanged D-015; deferred D-016; and no independently reimplemented
Unity gameplay algorithm.

## Accepted evidence entering Phase A

| Source | Accepted evidence and implication |
| --- | --- |
| TLAW-052 / GATE2_BOOTSTRAP.md | Pinned Unity 6000.3.21f1_c02631ffc030 compiles cleanly, passes 5 EditMode tests, builds and launches a Windows x64 Development bootstrap, and has no networking stack. |
| TLAW-053 / GATE2_DOMAIN_UNITY_COMPATIBILITY.md | Unity CSC/Tundra statically compiled a reference to the current net10 Domain DLL, but the same Editor session failed to load it because System.Collections.Immutable was unresolved. Static compile is not Editor consumption. |
| TLAW-054 / GATE2_DIRECT_DOMAIN_RUNTIME_PROBE.md | The one exact .NETCoreApp 10 immutable binary did not solve direct consumption: Unity rejected it with Invalid data directory 3. Direct loader/dependency chasing is not retried. |
| TLAW-055 / GATE2_PORTABLE_CORE_FEASIBILITY.md | A meaningful current authoritative closure exists: 26/60 files rooted at frozen-stage-6 LineNoiseDerivationService.Evaluate. Exact-source netstandard2.1 compilation without compatibility ingredients failed: immutable 88, IsExternalInit 269, required-member support 6. |

The Phase-A scratch proof adds only the ingredients Issue #131 permits, to
determine whether those earlier blockers were the whole migration surface. It
does not broaden the direct-net10 experiment.

## Candidate definitions

| Candidate | Definition |
| --- | --- |
| A — additive portable target | Keep current net10 Domain authority/tests and add a Unity-compatible target from the same production source. |
| B — extracted portable authoritative core | Move a coherent Unity-free authoritative implementation into a portable assembly consumed by both net10 Domain composition/tests and Unity, without algorithm duplication. |
| C — direct source linking / Unity asmdef | Compile or link production Domain source in Unity's asmdef compilation context. |
| D — facade/contracts-only | Expose Unity-facing contracts without moving executable authority. |

Excluded non-candidates remain direct .NETCoreApp 10 loader chasing, out-of-process
or localhost authority, networking/transport, independent Unity logic, and
weakened determinism/replay.

## Scratch provenance and permitted ingredients

The scratch baseline was created only from the exact baseline:

~~~text
git archive --format=zip --output C:\Temp\TLAW-056\baseline-edbb0d4cfcccec59fd6f3a5426b7c5f6d154508d.zip edbb0d4cfcccec59fd6f3a5426b7c5f6d154508d
~~~

Archive SHA-256:

~~~text
A62142133FE4EA8644073DE20223898AAEFD3229F14DE2CCC6683C747218F54C
~~~

TLAW-055's semantic closure was mechanically copied twice from that archive:
once in current-Directory layout for A and once in PortableCore\Source layout
for B. Both copy sets contain exactly 26 files and both compared byte-identical
to baseline, with 0 mismatches.

### One selected package

Exactly one package was selected, once, with no version cycling:

| Field | Value |
| --- | --- |
| Package | System.Collections.Immutable |
| Requested / resolved version | 8.0.0 / 8.0.0 |
| Package source/provenance | https://api.nuget.org/v3/index.json |
| NuGet content hash | AurL6Y5BA1WotzlEvVaIDpqzpIPvYnnldxru8oXJU2yFxFUy3+pNXjXd1ymO+RA0rq0+590Q8gaz2l3Sr7fmqg== |
| Selected package asset | lib\netstandard2.0\System.Collections.Immutable.dll |
| Resolved assembly identity | System.Collections.Immutable, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a |
| Asset SHA-256 | 5B1B1C83BA3D135C2FDFE425842FBE9C7432878B7E468623ACB554C69B4C130F |

Each scratch project targets netstandard2.1, explicitly uses LangVersion=latest,
includes the selected package, and uses mechanical source-inclusion metadata
only.

### Explicit scratch compatibility definitions

Each candidate added only these permitted metadata-support definitions, in
scratch-only CompilerCompatibility.cs:

1. System.Runtime.CompilerServices.IsExternalInit.
2. System.Runtime.CompilerServices.RequiredMemberAttribute.
3. System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute.
4. System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute.

They do not replace any collection or gameplay algorithm. No ArgumentNullException,
Enum, framework API, collection, algorithm, source generator, conditional
behavior, runtime-framework copy, Unity compatibility or backend change, or
reference-validation setting was added.

## Representative authoritative vector

The proof uses the real TLAW-055 seed, LineNoiseDerivationService.Evaluate,
rather than contracts alone. Scratch fixture setup invokes current authoritative
services unchanged:

1. ShiftRuntimeState.Create constructs a valid one-log runtime.
2. HostLogTransitionService.Apply performs SCHEDULED → AT_FEED_GATE →
   AT_INTAKE → QUEUED_FOR_SAW.
3. SawCycleStartService.Start creates a valid active saw cycle at tick 10.
4. LineNoiseDerivationService.Evaluate derives the stage-6 loud result from
   the resulting ShiftRuntimeState and MovementNoiseRuntimeState.

The same fixture source ran successfully against exact-baseline net10 Domain and
produced this canonical materially affected projection:

~~~text
result=LineNoiseEvaluatedWithChange|shift=tlaw056-shift|stateVersion=4|line=LOUD|lastEvaluatedAt=10|lastChangedAt=10|sawActive=True|movementActive=False|repairActive=False|sawOwner=log-01|sawDueAt=15
~~~

This establishes a valid baseline operation and the intended parity projection.
It does not create a portable result, because both portable candidates fail
before a DLL exists.

## Candidate A — additive portable target on existing Domain

### Scratch result

A compiled the exact 26 closure files in current relative source layout, plus
the one package and four permitted metadata definitions:

~~~text
dotnet build C:\Temp\TLAW-056\A\ExistingDomainPortable.csproj --configuration Release
~~~

Result: FAIL — 161 unique compiler errors, 1 warning.

| Compiler blocker | Count | Representative source evidence |
| --- | ---: | --- |
| ArgumentNullException.ThrowIfNull absent from netstandard2.1 | 131 CS0117 | LineNoiseRuntimeContracts.cs(30,31): CS0117: ArgumentNullException does not contain a definition for ThrowIfNull. |
| Generic Enum.IsDefined unavailable | 25 CS7036 | LineNoiseRuntimeContracts.cs(31,40): CS7036: no argument corresponds to required parameter value of Enum.IsDefined(Type, object). |
| Generic Enum.GetValues unavailable | 1 CS0308 | ShiftRuntimeState.cs(135,35): CS0308: non-generic Enum.GetValues(Type) cannot be used with type arguments. |
| Consequent object-typed enum errors | 3 CS0019, 1 CS1503 | ShiftRuntimeState.cs(137,17): CS0019: operator == cannot be applied to object and NodeId. |

The selected immutable package and explicit compiler metadata definitions remove
the earlier immutable, IsExternalInit, and required-member blocker classes. The
remaining framework API surface is material. Repairing it would require source
semantic edits, extra framework/API compatibility code, different targeting, or
target-specific behavior—none is authorized by this Phase-A proof. A therefore
produces no portable DLL.

### Production migration surface if an owner later selects A

A real additive target would at least require changes to shared targeting
metadata (Directory.Build.props and/or the Domain project), a deliberate package
reference policy, compatibility definitions ownership, a source-wide audit for
ThrowIfNull and generic Enum APIs, and target-specific CI. Because current
Domain is one 60-file production project, the 26-file failure is a lower bound,
not a full migration estimate.

A could preserve one source tree only if every target difference is made
semantically equivalent and replay/snapshot tests run on both targets. That is
substantial permanent multi-target and test-matrix burden. Unity Editor load and
EditMode execution are NOT RUN: there is no candidate DLL. Baseline-versus-
portable parity is likewise NOT RUN.

## Candidate B — extracted portable authoritative core

### Scratch result

B mechanically moved the same byte-identical 26 closure files to a separate
portable-core source root, retaining namespace and source content unchanged. It
used exactly the same one package, definitions, target, and language version:

~~~text
dotnet build C:\Temp\TLAW-056\B\PortableCore.csproj --configuration Release
~~~

Result: FAIL — the same 161 unique compiler errors, 1 warning, with the same
category counts and framework-API blocker surface. The difference between A and
B was only source organization; byte comparison reconfirmed 0 source-copy
mismatches.

B has no portable DLL, therefore Unity Editor load, EditMode execution, and
baseline-versus-portable canonical parity are NOT RUN.

### Production migration surface if an owner later selects B

TLAW-055 shows initial extraction is not arbitrarily small: it reaches 26/60
files and includes a 17-file strongly connected component spanning runtime
state, line, scheduler, containment, anomaly, and transition services, plus the
Primitives/Time cycle. A production B would need a new Unity-free core project,
mechanically moved or refactored dependency boundaries, current net10 Domain
retained as composition/replay/snapshot owner, references from Domain and Unity,
compatibility policy, and parity/replay regression coverage across the new
boundary.

B has the clearest eventual dependency direction—portable authority below net10
composition and Unity presentation—and can avoid duplicated algorithms. However,
the exact-source cut is not portable even with the allowed ingredients. It
cannot prove single semantic implementation, Unity viability, or replay
preservation in a working portable assembly. An owner would need a separately
scoped migration decision that explicitly permits and reviews source/framework
compatibility work before extraction begins.

## Candidate C — direct source linking / Unity asmdef compilation

C is not a credible safe execution architecture under current evidence, and no
runtime PoC was forced. TLAW-054 recorded that this Unity compiler rejected a
scratch C# 10 file-scoped namespace with CS8773; current Domain source also uses
C# 11 required members. Directly compiling production source in Unity would
therefore expose a distinct language/API context before considering the same
ThrowIfNull and generic Enum surface that blocked A/B.

C would require Unity asmdef/source-linking metadata and a second build context
for production algorithms. That creates high divergence risk from net10 Domain
compiler, tests, snapshot/replay authority, and CI. Preserving one semantic
implementation would require a broad portability migration first, after which
direct source linking still couples authority to Unity build topology. Unity
load/EditMode/parity are NOT RUN because existing compiler/language and
portability evidence already dominate this option.

## Candidate D — Unity-facing contracts/facade only

D can be supplementary presentation/interface work, but it is not sufficient
to execute authority in-process. Contracts can describe intents, events, or
projections; they cannot make non-loadable net10 implementation execute in the
Unity Editor, nor can they replace authoritative ShiftRuntimeState derivation
without introducing a second algorithm. A facade becomes a complete architecture
only when it delegates to viable executable authority such as A or B, neither
of which has passed the bounded portability proof.

D has low standalone migration cost and may later reduce Unity coupling, but its
authority, replay, and Unity-load answers are inherited from the missing
execution boundary. Compile/load/EditMode/parity are NOT APPLICABLE as an
independent solution.

## Decision matrix

| Criterion | A — additive portable target | B — extracted portable core | C — direct Unity source link | D — contracts/facade only |
| --- | --- | --- | --- | --- |
| In-process Unity viability | Not established: portable compile fails before Editor load. | Not established: same portable compile failure. | Strongly dominated by Unity C# context and API divergence. | Insufficient: no executable authority. |
| One semantic implementation | Possible only with sustained exact-target equivalence work. | Best eventual dependency direction if a core becomes portable without duplication. | High build-context divergence risk. | Cannot execute semantics alone. |
| Determinism/replay preservation | Net10 authority retained, but target parity and replay must be proven. | Requires migration/parity/replay tests around core and composition. | High risk from compiling authority in a different toolchain. | Preserves nothing by itself; depends on A/B. |
| Production migration size | Broad: current Domain becomes multi-targeted; 26-file failure is only a lower bound. | Broad but bounded conceptually: 26-file start, 17-file SCC, new ownership/reference surfaces. | Broad Unity asmdef/linking plus language/API migration. | Small as a supplement, but not an execution solution. |
| Package / compatibility burden | Selected package/metadata is insufficient; source/framework decisions remain. | Same immediate burden, then extraction burden. | Same portability burden inside Unity plus Unity compiler constraints. | Low itself, but defers rather than solves executable authority. |
| Unity coupling | Moderate: precompiled import remains a Unity artifact boundary. | Lower after successful extraction: core stays Unity-free. | High: authority source becomes Unity build input. | Low, but no authority execution. |
| Outside-Unity testability | Existing suite remains; add portable target matrix. | Retains net10 composition/tests with core test suites. | Weakest: Unity compile context becomes material. | Contracts testable, authority remains elsewhere. |
| Expected later Gate-2 increments | Targeting, compatibility, parity, Unity import, then local host integration. | Migration/extraction, adapters, parity/replay, Unity import, then local host integration. | asmdef/source topology and broad portability work before any host. | Only projection/contract layer after viable A/B boundary. |
| Rollback / reversibility | Metadata/source migration spreads across current Domain. | Reversible by project/reference rollback but move review is significant. | High rollback risk because Unity build topology owns source compilation. | Reversible but does not advance authority execution. |

No numerical score is assigned: B has the most coherent eventual authority
direction, but the admissible proof does not show a viable portable
implementation for either A or B. Selecting B on conceptual preference alone
would overstate the evidence.

## Migration and authority conclusions

- The direct existing net10 binary route remains excluded; no loader or runtime
  dependency chase was repeated.
- The Phase-A package and compiler metadata ingredients are not enough for
  either exact source layout. Remaining framework API errors are not merely
  loader configuration.
- There is no portable DLL, Unity assembly load, EditMode execution, or
  cross-runtime parity result to claim.
- The baseline vector confirms selected path is genuine deterministic
  authoritative execution, not a DTO-only test.
- Any source/API compatibility migration, target choice, package policy, or
  extraction boundary requires explicit owner decision and separately authorized
  implementation scope.

## Repository verification

The committed candidate is checked against the exact baseline with the required
commands:

| Check | Result |
| --- | --- |
| git diff --name-only edbb0d4cfcccec59fd6f3a5426b7c5f6d154508d...HEAD | Exactly docs/agent/GATE2_DOMAIN_UNITY_ARCHITECTURE_DECISION.md |
| git diff --check | PASS |
| dotnet build TheLogsAreWrong.sln --configuration Release | PASS |
| dotnet test TheLogsAreWrong.sln --configuration Release | PASS — at least 1631 passed, no failures or skip regression |
| Exact-head Tlaw.Verify | PASS |
| Gate 0 / Git object reader | PASS |
| Production Domain, tests, Unity, solution/project/target/package state | Byte-identical to baseline |

The exact candidate SHA, verifier output, CI workflow/job/artifact/digest, and
Draft PR identity are retained outside this self-referential commit in executor
handoff and PR evidence.

## Changed path, deviations, and retained scratch evidence

The exact tracked changed path is:

~~~text
docs/agent/GATE2_DOMAIN_UNITY_ARCHITECTURE_DECISION.md
~~~

One scratch build of A was repeated unchanged solely to retain terminal compiler
output after the first build exposed the material blocker. This was not a new
candidate, dependency, version, target, source change, or workaround. B was
built once. No other deviation occurred.

Retained scratch evidence:

~~~text
C:\Temp\TLAW-056\baseline-edbb0d4cfcccec59fd6f3a5426b7c5f6d154508d.zip
C:\Temp\TLAW-056\A\ExistingDomainPortable.csproj
C:\Temp\TLAW-056\A\Support\CompilerCompatibility.cs
C:\Temp\TLAW-056\B\PortableCore.csproj
C:\Temp\TLAW-056\B\Support\CompilerCompatibility.cs
C:\Temp\TLAW-056\evidence-a-build.log
C:\Temp\TLAW-056\evidence-b-build.log
C:\Temp\TLAW-056\baseline-vector.log
C:\Temp\TLAW-056\shared\AuthoritativeLineNoiseVector.cs
~~~

Not performed: production source/project/target/package/solution changes;
production Unity source/settings/package changes; Unity reference-validation
changes; runtime/framework copying; framework/API shim or polyfill additions
beyond the four explicit permitted metadata definitions; semantic source
rewrites; Unity Editor load; EditMode tests; player build; host/tick integration;
D-016 or containment presentation; networking, FishNet, or Steamworks; Grok;
D-018; Ready transition; merge; or cleanup.

NO_RECOMMENDATION
OWNER_ARCHITECTURE_DECISION_REQUIRED

## Phase B — owner decision finalization

The Phase-A evidence and its historical conclusion immediately above remain
unchanged: `NO_RECOMMENDATION` and
`OWNER_ARCHITECTURE_DECISION_REQUIRED` recorded that Phase A did not itself make
an owner architecture choice. This separate finalization records the subsequent
owner decision; it does not rewrite Phase A as a recommendation to reject an
architecture.

### Owner decision and decision-log record

The owner decision is **REJECT ALL FOR NOW**, recorded in [PR #132 owner decision
comment 5280644387](https://github.com/baroentgray/the-logs-are-wrong/pull/132#issuecomment-5280644387).
It is appended as [D-018](DECISIONS.md#d-018--owner-rejects-domainunity-architecture-candidates-for-now).

- Candidate A is not accepted now, but is not disproven.
- Candidate B is not accepted now, but is not disproven.
- Candidate C is not selected under the current evidence.
- Candidate D remains supplementary only and is not an executable authority
  architecture.

Accordingly, no Domain↔Unity production architecture is accepted. No production
source, target, project, package, portable-core, bridge, facade, adapter, Unity,
host/tick, gameplay, D-016, or networking implementation is authorized by this
Phase-B finalization.

### Owner-approved later investigation direction

The next direction is the separately scoped, scratch-only portability proof
recorded by D-018. It is limited to the already-known 26-file authoritative cut
and may make semantic-equivalent compatibility replacements for the currently
exposed framework API blockers. It must attempt, in order:

~~~text
portable compile
-> pinned Unity Editor load
-> EditMode authoritative execution
-> exact net10/Unity parity
~~~

It must stop at the first material blocker. That later proof does not pre-accept
Candidate A or Candidate B, must not silently become a production migration, and
does not select a Domain↔Unity architecture.

PR #132 remains Draft pending verification and review of this amended exact-head
candidate, followed by a separate owner Ready gate.

Sources: [Issue #131](https://github.com/baroentgray/the-logs-are-wrong/issues/131), [PR #132 owner decision comment 5280644387](https://github.com/baroentgray/the-logs-are-wrong/pull/132#issuecomment-5280644387), [D-018](DECISIONS.md#d-018--owner-rejects-domainunity-architecture-candidates-for-now), and [Grok authoritative PASS record 4926080180](https://github.com/baroentgray/the-logs-are-wrong/pull/132#issuecomment-4926080180).
