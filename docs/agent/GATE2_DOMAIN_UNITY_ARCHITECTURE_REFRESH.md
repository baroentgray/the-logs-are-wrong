# Gate 2 Domain–Unity architecture refresh after portable parity proof

Schema: tlaw.gate2-domain-unity-architecture-refresh/v1.

Date: 2026-08-14.

## Scope, authority, and identity

| Field | Value |
| --- | --- |
| GitHub contract | [Issue #137](https://github.com/baroentgray/the-logs-are-wrong/issues/137) |
| Exact baseline | 3275ddf2cd997eb74e57216419d3ac81eb09387d |
| Branch | task/TLAW-059-domain-unity-architecture-refresh |
| Worktree | C:\Projects\TheLogsAreWrong-worktrees\TLAW-059 |
| Authorized tracked path | docs/agent/GATE2_DOMAIN_UNITY_ARCHITECTURE_REFRESH.md |
| Evidence period refreshed | TLAW-053 through TLAW-058 |

This is a Phase-A evidence dossier. It rederives the accepted repository
evidence and gives one advisory architecture recommendation to the owner. It is
not an owner architecture decision and does not supersede the historical D-018
decision.

No production Domain, tests, Unity project, project/target/package metadata,
tooling, Gate-0 input, or decision-log entry changes. No runtime or Player
experiment is run or constructed. In particular, this dossier does not create a
portable/core project, migrate or refactor source, accept an architecture,
authorize host/tick integration, implement D-016, introduce networking,
FishNet, or Steamworks, mark a pull request Ready, merge, or clean up.

## Decision context retained from D-013 through D-018

D-013 keeps saw-completion quota work in separate immutable ShiftRuntimeState
and QuotaRuntimeState. D-014 retains the frozen, complete ShiftSnapshot and
replay contract as Gate-1 Domain authority. D-015 retains the deterministic
eight-second saw-only Penitent window, and D-016 remains deferred to a real
Gate-2 control surface. D-017 accepts package pins for later Gate-3 networking
only; it does not authorize a network dependency in Gate 2.

D-018 is the applicable historical owner decision. After TLAW-056 Phase A, the
owner deliberately rejected all Domain-to-Unity production candidates for then:
Candidate A was not accepted, Candidate B was not accepted, Candidate C was not
selected, and Candidate D remained supplementary only. This was not a finding
that A or B was technically impossible. At that time the portable proofs stopped
at the exposed netstandard2.1 framework-API surface before Unity load.

The owner then authorized the bounded scratch-only proof sequence:

~~~text
portable compile
-> pinned Unity Editor load
-> EditMode authoritative execution
-> exact net10/Unity parity
~~~

TLAW-057 and TLAW-058 supplied new evidence for that sequence. They did not
select A or B, and neither this dossier nor the new evidence changes D-018 into
an accepted production architecture.

Sources: [D-013 through D-018](DECISIONS.md), especially
[D-018](DECISIONS.md#d-018--owner-rejects-domainunity-architecture-candidates-for-now);
[TLAW-056 evidence](GATE2_DOMAIN_UNITY_ARCHITECTURE_DECISION.md); and
[Issue #131](https://github.com/baroentgray/the-logs-are-wrong/issues/131).

## Accepted evidence chain

### Earlier negative routes remain negative

| Evidence | Accepted result | Architectural consequence retained |
| --- | --- | --- |
| TLAW-053 | Unity CSC/Tundra statically compiled a reference to the existing net10.0 Domain DLL, but the same valid Editor session could not load it because System.Collections.Immutable was unresolved. | Static C# compilation is not Editor consumption. Direct existing net10.0 DLL use was disproven for that DLL-only Editor-load procedure. |
| TLAW-054 | Adding one exact .NETCoreApp 10 System.Collections.Immutable candidate produced Invalid data directory 3 in the Unity Editor. The probe stopped at that first material blocker. | Direct net10.0 dependency/load chasing remains excluded. There is no direct-Domain EditMode or Player result. |
| TLAW-055 | A real authoritative LineNoiseDerivationService.Evaluate seed reached a 26-file semantic closure from the 60-file Domain, but exact-source netstandard2.1 compilation failed on immutable collections and compiler-support APIs. | The cut was meaningful and bounded, but source-identical portability was not established. |
| TLAW-056 / D-018 | With one Immutable package and allowed compiler metadata definitions, both A and B scratch layouts still failed on ThrowIfNull and generic Enum API surface before Unity load. The owner rejected all candidates for then. | The former portable blocker was framework-API compatibility, not a mere loader configuration issue. No architecture was accepted. |

The direct net10.0 route is not rehabilitated by the later portable evidence:
TLAW-057 and TLAW-058 use a separately compiled netstandard2.1 portable
assembly and its official dependency closure, not the existing net10.0 Domain
binary. No direct loader experiment is repeated here.

Sources: [TLAW-053](GATE2_DOMAIN_UNITY_COMPATIBILITY.md),
[TLAW-054](GATE2_DIRECT_DOMAIN_RUNTIME_PROBE.md),
[TLAW-055](GATE2_PORTABLE_CORE_FEASIBILITY.md), and
[TLAW-056](GATE2_DOMAIN_UNITY_ARCHITECTURE_DECISION.md).

### What changed after D-018

#### TLAW-057 — the compatibility surface became mechanically portable

The accepted TLAW-055 authoritative cut was reconstructed from the exact
baseline as 26 source files. It includes real immutable runtime state and
authoritative services rather than DTOs alone, and includes the accepted
17-file source SCC plus the Primitives/Time SCC evidence that prevents a
smaller exact cut.

Before transformation, all 26 scratch copies were byte-identical to their
baseline counterparts. TLAW-057 then made exactly 157 authorized,
semantic-equivalent mechanical transformations, validated from a manifest:

| Transformation class | Count |
| --- | ---: |
| ArgumentNullException.ThrowIfNull to an explicit null-only guard | 131 |
| generic Enum.IsDefined to Enum.IsDefined(Type, object) | 25 |
| generic Enum.GetValues to a type-preserving non-generic cast | 1 |
| Total | 157 |

The scratch project targeted netstandard2.1 with LangVersion=latest, used
System.Collections.Immutable 8.0.0 as its only direct package, and used only
the four stated compiler-metadata definitions: IsExternalInit,
RequiredMemberAttribute, CompilerFeatureRequiredAttribute, and
SetsRequiredMembersAttribute. The portable compile then passed with zero
warnings and zero errors.

The first Unity load still stopped at the first material blocker:
System.Collections.Immutable could not resolve
System.Runtime.CompilerServices.Unsafe. No additional dependency was chased,
no Unity setting changed, and no EditMode or parity result was claimed in
TLAW-057.

Source: [TLAW-057](GATE2_PORTABLE_AUTHORITY_RUNTIME_PARITY_PROOF.md).

#### TLAW-058 — the bounded portable cut loaded and matched in the Editor

TLAW-058 fresh-restored the official NuGet closure for the same portable cut.
The resolved closure was exactly:

| Package | Version |
| --- | --- |
| System.Collections.Immutable | 8.0.0 |
| System.Memory | 4.5.5 |
| System.Buffers | 4.5.1 |
| System.Numerics.Vectors | 4.4.0 |
| System.Runtime.CompilerServices.Unsafe | 6.0.0 |

Unity received only the fixed Immutable root asset and the one
evidence-justified System.Runtime.CompilerServices.Unsafe 6.0.0 asset. The
other resolved closure assets were not copied; no framework/runtime closure,
arbitrary SDK DLL, binding workaround, reference-validation change, package
graph change, or backend change was used.

Under the pinned Unity 6000.3.21f1 Editor, the portable assembly then loaded
and the hook resolved all four required authority types:

~~~text
TheLogsAreWrong.Domain.Runtime.ShiftRuntimeState
TheLogsAreWrong.Domain.Runtime.HostLogTransitionService
TheLogsAreWrong.Domain.Scheduler.SawCycleStartService
TheLogsAreWrong.Domain.Line.LineNoiseDerivationService
~~~

One real EditMode test passed the non-mocked authority chain:

~~~text
ShiftRuntimeState.Create
-> HostLogTransitionService.Apply (SCHEDULED -> AT_FEED_GATE)
-> HostLogTransitionService.Apply (AT_FEED_GATE -> AT_INTAKE)
-> HostLogTransitionService.Apply (AT_INTAKE -> QUEUED_FOR_SAW)
-> SawCycleStartService.Start
-> LineNoiseDerivationService.Evaluate
~~~

The same byte-identical fixture ran afresh against the exact-baseline net10
Domain. The portable Editor and net10 runners emitted identical UTF-8 canonical
projections, including the operation chain, state versions 0/3/4, the IN_SAW
log state, saw times 10/14, and LOUD line noise. The projection bytes and text
were equal and both had SHA-256
CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F.

This is positive evidence for the bounded transformed portable assembly,
the pinned Editor dependency arrangement, and one actual authoritative
operation. It is not evidence for the existing direct net10.0 binary or for a
production migration.

Source: [TLAW-058](GATE2_PORTABLE_DEPENDENCY_CLOSURE_PROBE.md).

## What the refreshed evidence still does not prove

The positive TLAW-058 result is deliberately bounded. It does not establish:

- universal parity beyond the 26-file cut and one canonical authority vector;
- full portability of all 60 current Domain source files;
- a production portable target, portable-core project, package policy, or
  compiler-compatibility-definition ownership;
- an actual production extraction boundary or compilation/reference graph;
- full snapshot capture, restore, journal reduction, or replay parity across a
  portable boundary required by D-014;
- Windows or any other Player authority execution, dependency resolution, or
  parity;
- a local host/tick composition using the portable code;
- D-016 physical control-surface behavior;
- networking, FishNet, Steamworks, an out-of-process host, or a second
  authoritative implementation.

The 34 Domain files outside the 26-file source closure are neither proven
portable nor shown impossible to port. They include caller composition and
alternate handlers, intent ingress, journal/replay/snapshot machinery, and
independent diagnostics, derivation, planning, and sequencing. That is an
unknown migration surface, not a failure finding about those files.

## Candidate A refresh — additive portable target on the existing Domain

Candidate A keeps the current net10 Domain authority and tests while adding a
Unity-compatible target from the same production source.

### Evidence status and credible migration surface

The TLAW-057/058 result proves that a real 26-file authoritative subset can be
made portable with the stated semantic-equivalent transformations, compiler
metadata support, System.Collections.Immutable 8.0.0, and the observed Unity
plugin dependency arrangement. It removes the previous claim that the exposed
API surface makes portable compilation categorically unavailable.

It does not prove the same for the remaining 34 files. Candidate A would make
their netstandard-compatible compilation a prerequisite because its definition
is a second target over the current production Domain source. The 26-file
result is therefore a lower bound on A's source and verification surface, not a
completion proof for the whole project.

If an owner later selects A, its production migration would credibly include:

- changing shared project/target metadata so current Domain source builds for
  net10.0 and the selected portable target;
- assigning a production owner and locked version policy for
  System.Collections.Immutable and any observed portable-target compiler
  metadata support;
- auditing the full 60-file source tree for the same compatibility classes and
  any additional portable-target framework API dependencies;
- proving every target-specific source difference is semantically equivalent
  rather than silently creating a second algorithm;
- retaining all current net10 tests while adding portable-target compile,
  deterministic-vector, snapshot/replay, pinned-Editor-load, EditMode, and
  Player test coverage; and
- maintaining Unity import/dependency packaging as a target artifact rather
  than allowing Unity source to own the authority implementation.

The package and compatibility burden is shared broadly in A: one production
Domain project would own two framework contracts. It avoids a source move, but
it makes target-specific compiler/API constraints a permanent concern for every
Domain source change. Existing net10 tests remain valuable but cannot establish
portable-target behavior; an explicit dual-target test matrix is required.

D-014 makes snapshot/replay preservation material. A must run the existing
net10 composition/replay suite and an equivalent portable projection/replay
suite wherever the portable target carries that behavior. Passing the one
line-noise vector is insufficient for this requirement.

### Coupling, rollback, and minimum reversible first increment

A leaves a precompiled assembly boundary between Unity and Domain, so Unity
source coupling can remain low. Its rollback is mechanically straightforward
only before widespread shared-source compatibility edits: removing the portable
target returns the project to net10.0, but broad target-conditioned changes
would make that rollback costly to audit.

Under the selected Player-risk classification, an owner selecting A must first
authorize a separately scoped scratch/non-production Player authority/load/parity
proof using the already-proven 26-file portable authority cut and its official
resolved dependency closure. It occurs before any production migration. Only
after that Player gate passes, the minimum reversible first production A
increment should be a non-Unity, no-host additive portable compilation target
over the entire existing Domain source, accompanied by explicit
compatibility/package ownership and a failing-closed full-source inventory. It
must keep the net10.0 target and regression suite intact, add no production
Unity integration, and stop before gameplay work. Its purpose is to establish
the full 60-file compile surface before a production import or host boundary is
attempted.

This is a future conditional description only. It is not authorization to make
the target or any production change now.

## Candidate B refresh — extracted portable authoritative core

Candidate B extracts a coherent Unity-free portable authoritative core that
both the net10 Domain composition/tests and Unity consume. It must preserve one
semantic implementation rather than copy or reimplement gameplay logic.

### Evidence status and likely ownership boundary

The TLAW-055 semantic closure is positive boundary evidence for B. The chosen
seed is current deterministic authority, not an envelope-only contract:
LineNoiseDerivationService.Evaluate derives immutable LineNoiseRuntimeState from
validated ShiftRuntimeState, movement evidence, saw/repair state,
configuration, identifiers, enums, and simulation-time primitives. Its exact
transitive closure is the 26 files later made portable, and it contains the
accepted 17-file SCC plus the Primitives/Time SCC. The cut consequently cannot
be reduced to just DTOs or a superficial facade without losing its existing
authoritative operation.

Because the closure has no recorded semantic outgoing source dependency to the
remaining 34 Domain files, it is credible evidence for an inner, Unity-free
core dependency direction. It is not proof that an extracted production project
will compile unchanged: access, assembly, test, and reference ownership remain
unproven until a separately authorized migration verifies them.

The likely future boundary is:

~~~text
portable authoritative core
  <- net10 Domain composition, host/tick handlers, journal/replay/snapshot
  <- Unity presentation and local Gate-2 composition
~~~

Under that direction, the portable core would own the moved immutable state,
authoritative services, and their portable dependency/compatibility policy.
The net10 Domain would retain the complete current composition, intent
ingress/handlers, host/tick ordering, event journal, snapshot capture/restore,
and replay authority required by D-014, while consuming the same core types and
services. Unity would consume that same compiled core, not a Unity rewrite of
the authoritative algorithms.

This is an architectural boundary hypothesis grounded in the measured closure,
not an accepted design or an assertion that all existing outer code already has
the required one-way references.

### Package, parity, and migration burden

B confines portable package and compiler-compatibility ownership to the
portable core rather than placing a portable framework contract on every
existing Domain file. It would still require an explicit, reproducible policy
for System.Collections.Immutable 8.0.0, its resolved closure, the compiler
metadata definitions, and the Unity plugin artifacts actually required by the
pinned Editor. TLAW-058 proves only Immutable plus the specifically justified
Unsafe 6.0.0 assets for the tested core and Editor; it does not establish a
general Player packaging policy.

B has a real extraction cost. Moving the 26-file interlocked cut changes source
ownership and requires a Domain-to-core reference direction, project build
boundaries, and test references. The cut includes enough core contracts that
outer Domain functionality will need to consume the moved types rather than
retain duplicate definitions. A temporary duplicated source implementation is
not acceptable because it would violate the one-semantic-implementation
requirement.

Production B must also carry forward the exact TLAW-057 compatibility surface
on that one moved production source implementation. Against the accepted current
26-file source baseline, this is exactly 157 semantic-equivalent replacements:

| TLAW-057 class | Expected count | Required production form |
| --- | ---: | --- |
| ArgumentNullException.ThrowIfNull | 131 | Explicit null-only ArgumentNullException guards |
| generic Enum.IsDefined<TEnum> | 25 | Enum.IsDefined(typeof(TEnum), value) |
| generic Enum.GetValues<TEnum> | 1 | Type-preserving non-generic cast |
| Total | 157 | One moved portable-core source implementation |

The replacement manifest/inventory must be auditable against those classes and
counts. Current production Domain source has not changed since the proof, so
157 is the expected baseline. If a later production implementation observes
count drift, it must stop and explain the exact baseline difference for review;
it must not silently alter this compatibility contract.

These semantic-equivalent edits apply only to the one moved production source
implementation. There must not be an unchanged net10 authority copy alongside
a transformed portable copy, a promoted scratch transformed copy as parallel
authority, or target-specific gameplay algorithms. After the edits, the
resulting moved portable-core source is consumed by the existing net10 Domain
composition and, later, by the Unity consumer.

The parity boundary must expand deliberately. At minimum, it must keep
existing net10 Domain tests and add core-level deterministic vectors. It must
also prove that the net10 composition's full snapshot/replay behavior is
unchanged when it consumes the core, then compare selected portable-Unity
operations against their exact net10 counterparts. The TLAW-058 line-noise
projection is a valid first vector, not sufficient replay coverage.

### Coupling, rollback, and minimum reversible first increment

If successful, B keeps the authoritative implementation Unity-free and makes
Unity a compiled consumer. That reduces Unity compiler/build-topology coupling
relative to direct source linking, while preserving ordinary outside-Unity core
and net10 composition tests. Its rollback remains reviewable if the move,
project references, and test wiring are one bounded increment; it is not
free, because the 26-file SCC means the extraction cannot be cherry-picked as
one or two isolated DTOs.

Under the selected Player-risk classification, an owner selecting B must first
authorize a separately scoped scratch/non-production Player authority/load/parity
proof using the already-proven 26-file portable authority cut and its official
resolved dependency closure. It occurs before any production migration and must
prove the portable authority load and the same real-operation projection parity
against the exact net10 Domain. No production source, project, target, package,
or Unity import is part of that Player proof.

Only after that Player gate passes, the minimum reversible first production B
migration increment must atomically establish all of the following:

- one single-source extraction of the proven 26-file closure;
- productionization of the 157 TLAW-057 semantic-equivalent compatibility
  replacements on that one moved source implementation: 131 null-only
  ArgumentNullException guards, 25 Enum.IsDefined(typeof(TEnum), value)
  replacements, and 1 type-preserving Enum.GetValues replacement;
- an auditable transformation manifest/inventory against the accepted
  131 + 25 + 1 = 157 baseline, with mandatory stop-and-review on count drift;
- the portable authoritative-core target;
- explicit ownership of compiler compatibility definitions;
- System.Collections.Immutable package policy and the required resolved
  dependency-closure policy;
- the existing net10 Domain consuming that core;
- deterministic regression coverage; and
- the D-014-required snapshot/replay regression coverage.

That production B increment must not include a production Unity import,
host/tick integration, gameplay, D-016 control-surface work, networking,
FishNet, or Steamworks. Only after the atomic extraction/policy and net10
regression gate passes can a separately authorized production Unity-import
increment begin, consuming the same moved portable-core source after its
compatibility edits. This ordering retains an auditable project/reference
rollback point without allowing a parallel authority implementation.

This is a future conditional description only. It is not authorization to
create the project, move source, or modify any production reference now.

## Qualitative A-versus-B matrix

| Criterion | A — additive portable target on existing Domain | B — extracted portable authoritative core |
| --- | --- | --- |
| In-process Unity viability | The 26-file positive proof supports a subset only. The full 60-file target must compile and load before viability is established. | The proven 26-file real-authority cut compiled, loaded, and executed in the pinned Editor, but no production extraction or wider operation set is proven. |
| One semantic implementation | One shared source tree is possible, but target-specific compatibility differences must be kept equivalent across the entire Domain. | One moved core source implementation is possible if the core is consumed by both net10 composition and Unity; duplicate transitional logic is not acceptable. |
| Determinism and replay preservation | Requires full target-matrix parity, including snapshot/replay where shared source carries it. Existing net10 behavior alone is insufficient. | Requires retained net10 composition/replay ownership plus core and cross-boundary parity. The selected vector is encouraging but is not replay proof. |
| Production migration size | Broad and presently unknown: all 60 files become portable-target candidates; 26-file result is a lower bound. | Concrete but still material: at least the 26-file cut, its SCCs, new project/reference ownership, and outer-Domain consumption must be migrated. |
| Package and compatibility burden | Shared Domain project owns portable-target package/metadata policy and every future source change must honor it. | Core owns portable package/metadata policy; outer net10 Domain can remain on its current framework contract, but project/package boundary work is required. |
| Unity coupling | Low-to-moderate: Unity imports a compiled multi-target Domain artifact, while target policy stays in Domain. | Low after extraction: Unity consumes a Unity-free core artifact; the risk is in initial boundary/reference work, not Unity source ownership. |
| Outside-Unity testability | Current net10 suite remains, with a substantial portable-target companion matrix. | Current net10 composition suite remains; a focused core suite adds to it. Unity need not compile the production authority source. |
| CI and test-matrix burden | Highest breadth: net10 and portable compilation/tests across the whole Domain, plus Editor and Player target gates. | Split but bounded: core compile/tests/parity plus net10 composition/replay and Unity consumer gates. It is not automatically smaller until the boundary is proven. |
| Rollback and reversibility | Simple only before source-wide target compatibility changes spread; later shared-source edits increase rollback review cost. | One bounded move/reference increment is reviewable, but the 26-file SCC makes the first move nontrivial. Atomic source ownership avoids long-lived duplication. |
| Known Player risk | No Player proof exists for a full multi-target Domain. It may have a larger unknown dependency/package surface than the tested cut. | No Player proof exists for the portable core. The tested Editor assets bound one cut's current load arrangement but do not prove Player packaging or execution. |

The matrix does not assign numerical scores. The positive 26-file result is
evidence in favor of a possible B boundary, not a presumption that B is
conceptually cleaner or automatically cheaper. Conversely, it does not make A
unworkable; A's decisive uncertainty is the untested remaining 34-file
portable-target surface.

## Candidate C and Candidate D after TLAW-057/058

### Candidate C — direct source linking or Unity asmdef compilation

TLAW-057/058 do not materially rehabilitate C. They prove that a separately
compiled netstandard2.1 assembly with defined compatibility transformations and
two selected Unity plugin assets can load and execute in the pinned Editor.
They do not compile production source in Unity's asmdef context.

TLAW-054 already recorded a Unity compiler C# 9 limitation for a scratch
file-scoped namespace, while the current Domain uses modern language features.
Direct source linking would additionally put production authority under a
second compiler/build context and require Unity asmdef/source topology changes.
It retains high divergence risk for deterministic behavior, replay authority,
and CI even if the 26-file compatibility work were carried forward. C is not
selected by this evidence.

### Candidate D — contracts or facade only

D can remain a supplementary presentation/interface technique. It does not
independently solve executable authority: contracts describe intents, events,
or projections but cannot run the authoritative state transitions in Unity.
Replacing those operations with facade behavior would create an independent
algorithm, contrary to the single-authority requirement. The new portable
Editor result does not change that conclusion.

## Player unknown — advisory classification

Classification: **A — acceptable residual risk after architecture selection,
with Player proof as the first post-selection increment.**

Reasoning:

- The pinned-Editor result is genuine evidence of in-process portable authority
  compile, load, execution, and exact parity for a real operation; it is not
  merely static compilation.
- The missing Player result is material to rollout and must fail closed before
  any production Unity authority is relied upon. It must cover portable
  dependency packaging/load, the same real authority operation, and exact
  net10/Player projection parity.
- That result does not resolve the present A-versus-B boundary uncertainty.
  Both candidates require a portable Unity consumer artifact, while the
  presently discriminating evidence is A's unknown full-Domain target surface
  versus B's concrete 26-file closure and reference-boundary work.
- Therefore Player evidence should be an immediate, separately scoped,
  scratch/non-production first post-selection increment using the already-proven
  26-file portable authority cut and its official resolved dependency closure.
  It occurs before any production migration, host/tick integration, or gameplay
  work. It is not a license to deploy or integrate without that gate.

No Player proof is run by this task.

## Advisory recommendation for owner decision

The advisory recommendation is RECOMMEND_B. It is not an architecture
acceptance.

The recommendation is based on evidence rather than conceptual preference:

- B has a measured, non-DTO 26-file authoritative closure whose mechanically
  transformed portable form passed netstandard2.1 compile, pinned-Editor load,
  one real EditMode authority execution, and a fresh byte-for-byte net10/Unity
  parity vector.
- TLAW-055's semantic closure and SCC evidence provide a credible starting
  dependency direction for an inner portable authority implementation and an
  outer net10 composition/replay/snapshot owner.
- A has the same positive evidence only for 26 of 60 files. Its defining
  full-Domain portable target remains unproven across the other 34 files and
  would make portable-target compatibility a broad permanent shared-source
  obligation.
- C remains a separate Unity compiler/build-context risk, and D does not
  execute authority independently.

An owner selecting B would still need to authorize separate scopes in this
order:

~~~text
scratch/non-production Player authority/load/parity proof
-> production B single-source extraction plus 157 compatibility replacements,
   auditable manifest, portable-target/package and compiler-compatibility
   policy, net10 Domain consumption, deterministic and D-014 snapshot/replay
   regressions
-> production Unity import
-> local host/tick integration
-> later gameplay and networking gates, each separately authorized
~~~

The selection and this ordering must not be inferred as authorization from this
advisory document. TLAW-058 does not by itself authorize any source move,
package addition, portable-core project, Unity integration, host/tick work,
gameplay, D-016, or networking.

## Sources

- [Issue #137](https://github.com/baroentgray/the-logs-are-wrong/issues/137)
- [TLAW-053 Domain–Unity compatibility audit](GATE2_DOMAIN_UNITY_COMPATIBILITY.md)
- [TLAW-054 direct net10 Domain runtime probe](GATE2_DIRECT_DOMAIN_RUNTIME_PROBE.md)
- [TLAW-055 portable authoritative core feasibility probe](GATE2_PORTABLE_CORE_FEASIBILITY.md)
- [TLAW-056 architecture decision dossier](GATE2_DOMAIN_UNITY_ARCHITECTURE_DECISION.md)
- [TLAW-057 portable authority runtime/parity proof](GATE2_PORTABLE_AUTHORITY_RUNTIME_PARITY_PROOF.md)
- [TLAW-058 portable dependency-closure probe](GATE2_PORTABLE_DEPENDENCY_CLOSURE_PROBE.md)
- [Decision log D-013 through D-018](DECISIONS.md)

ARCHITECTURE_RECOMMENDATION=RECOMMEND_B
OWNER_ARCHITECTURE_DECISION_REQUIRED
NO_ARCHITECTURE_DECISION_ACCEPTED
