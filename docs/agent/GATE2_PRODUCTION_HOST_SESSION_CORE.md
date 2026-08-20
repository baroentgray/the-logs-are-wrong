# Gate 2 — production HostSession core (TLAW-067)

## Candidate binding and Phase 0

| Item | Evidence |
| --- | --- |
| GitHub issue | #156 — production HostSession core |
| Exact authorized baseline | `8e5252ab18146efb217e3b4e138baffc66cb86d8` |
| Branch / worktree | `task/TLAW-067-production-host-session-core` / `C:\Projects\TheLogsAreWrong-worktrees\TLAW-067` |
| First implementation commit / Draft PR | `9cc06f6092718179489613cca835f25f3b0bbbee` / #157, base `main`, Draft, body exactly `Closes #156` |
| Editor used | `6000.3.21f1 (c02631ffc030)` only |

Phase 0 was completed read-only before implementation. `origin/main` and the
existing remote task branch both resolved to the exact authorized baseline.
The existing PortableAuthority core contained the one seven-stage HostTick
implementation, referenced no Domain assembly, and Domain consumed it. The
frozen HostTick carried-state fields and stage order were inspected unchanged.
The accepted three-plugin Unity deployment was checked before refresh:
PortableAuthority, System.Collections.Immutable, and
System.Runtime.CompilerServices.Unsafe only. No Domain DLL, extra closure,
network package, or forbidden networking reference was present.

## Production ownership boundary

`src/TheLogsAreWrong.PortableAuthority/Runtime/HostSessionContracts.cs` adds a
plain, sealed, non-static `HostSession` in the existing
`TheLogsAreWrong.Domain.Runtime` namespace. It owns exactly one configured
shift's carried authoritative state:

- `ShiftRuntimeState`, `QuotaRuntimeState`, `MovementNoiseRuntimeState`,
  `LineNoiseRuntimeState`, `HostTickProgressionEvidence`, and
  `ShiftLifecycleRuntimeState`;
- the selected immutable `ShiftConfiguration` and `AnomalyCatalog`;
- one exact-shift `IEventJournal`, initialized empty for a new session;
- the successful authoritative-tick cursor and invocation count.

Its only public execution boundary is:

```text
ExecuteTick(ServerTick, AcceptedIntentTickBatch, ImmutableHashSet<ItemId>)
```

It accepts an already admitted batch and active-tool evidence only. It does
not accept an `EventId`, event count, scheduler, wall-clock duration,
Unity object, network message, client authority, or generated tick.

For the first request it requires `ServerTick.Zero`; every later request must
be exactly the immediately following tick for the exact configured shift.
Disposed and reentrant invocations fail before a second session carry.
`HostTickExecutionService.Execute` remains the one semantic seven-stage
implementation and is invoked once for each valid HostSession request. The
session carries a result only when its checkpoint is
`HostTickCheckpointAdvanced`; a rejection or exception leaves the session's
carried references and successful cursor unchanged. Existing journal atomicity
is not widened or replaced.

This is a per-session authority boundary, not a process-wide live-host
ownership registry, persistence/resume system, scheduler, Unity MonoBehaviour,
or host loop. Those integrations remain deferred and separately authorized.

## Host-owned event identities

The caller-provided `ImmutableArray<EventId>` parameter was removed from the
public `HostTickExecutionService` and `HostStageSevenEventExecutor` authority
surfaces. Stage Seven first computes and validates its private planned
publication list. For a new publication, it preflights contiguous sequence
capacity, then derives each identity internally from the exact shift and the
next contiguous journal sequence:

```text
host:{shift-id}:{event-sequence-invariant-culture}
```

Zero publications produce no identities and no journal append. One or many
publications receive exactly the one or many identities corresponding to their
contiguous journal sequences. Sequence exhaustion fails before append. Replay
or already-published validation deterministically reconstructs the same IDs
from the committed tail sequence and validates the existing envelopes; it does
not allocate a new identity.

There is no GUID/random ID generator, wall-clock input, static mutable counter,
or target-specific authority path. The journal still owns the monotonic
sequence; HostSession owns the carried continuity cursor; Stage Seven owns
mapping a validated plan to those sequences and identities.

## Bounded tests and deterministic evidence

The bounded .NET `TLAW-067` suite proves the public HostSession shape has no
caller event-ID argument or static mutable state; invalid inputs do not carry
state; four consecutive ticks are repeat-deterministic with contiguous
journal sequences and Stage-Seven-owned identity prefixes; and real journal
callback reentrancy plus disposal is rejected. Existing HostTick, Stage Seven,
journal, replay, snapshot, and architecture tests were migrated to the
no-caller-identity API without changing authority semantics. The production
PortableAuthority source inventory guard now includes the HostSession file.

The accepted existing direct-authority canonical vector remains exactly:

```text
CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F
```

The allowed existing Unity Gate-2 HostRuntime test no longer transports a
test-owned state copy: it invokes imported production `HostSession` and only
formats the returned result/journal projection. The scheduler probe remains
test-only, performs no authority work, and is not a production scheduler.

## Derived Unity plugin and pinned runtime proof

The only refreshed derived binary is:

```text
unity/TheLogsAreWrong/Assets/Gate2/Plugins/PortableAuthority/TheLogsAreWrong.PortableAuthority.dll
```

It was built from the changed PortableAuthority source with the established
non-persisted deployment recipe:

```text
dotnet build src/TheLogsAreWrong.PortableAuthority/TheLogsAreWrong.PortableAuthority.csproj \
  --configuration Release --no-restore \
  -p:IncludeSourceRevisionInInformationalVersion=false -p:DebugSymbols=false
```

That output and the committed plugin are byte-identical at SHA-256
`90AC8BAE90DCA3B6807BEF6EFCD0BDD608AEFF6EC134FAF485FED79E1B340E7B`;
no PortableAuthority PDB is present. Immutable and Unsafe remain byte-identical
at `5B1B1C83BA3D135C2FDFE425842FBE9C7432878B7E468623ACB554C69B4C130F`
and `01748200F2400C742AA689F1F5101BD6298EFDFD92C00C18F4FA473847235BA9`.
The Assets plugin inventory remains exactly those three DLLs.

Under pinned Unity `6000.3.21f1 (c02631ffc030)`, EditMode completed
`20 passed / 0 failed / 0 skipped`. The Windows x64 Development build reported
`[TLAW052] BUILD_RESULT=Succeeded` and `BUILD_ERRORS=0 BUILD_WARNINGS=0`.
The built player launched with `-tlaw-bootstrap-smoke`, exited `0`, and emitted
the existing PortableAuthority load/pass markers, the exact canonical SHA
above, and both TLAW052 bootstrap markers. The player managed directory uses
the same PortableAuthority DLL hash; task-supplied plugins remain the exact
three Assets DLLs. Unity generated System.Memory/System.Buffers facades are
not imported Assets or task-supplied closure additions.

## Boundaries and final verification

No Domain source, project/package/props/target configuration, Unity scene,
prefab, Package manifest, ProjectSettings, scheduler/host runtime, gameplay,
D-016, networking, FishNet, Steamworks, or DECISIONS.md change is made. There
is no production Unity import architecture change beyond refreshing the already
approved derived PortableAuthority plugin, and no production Unity host/tick
integration is implemented.

Pre-commit local repository regression passed with `git diff --check`; fresh
restore; standalone PortableAuthority Release build `0 warnings / 0 errors`;
full Release build `0 warnings / 0 errors`; full tests `1638 passed / 0 failed
/ 0 skipped`; D-014 `Scope=TLAW-046` `87 passed / 0 failed / 0 skipped`; and
the one-test canonical PortableAuthority vector. After the ordinary repository
build, a clean two-property deployment build again matched the staged plugin
byte-for-byte at `90AC8B…40E7` with no PDB.

Final exact-head repository verification, object reader, full Release build,
full tests, D-014 slice, architecture/dependency checks, and Repository CI
artifact are recorded only after the final candidate is pushed; earlier CI is
not claimed for that future head.

PRODUCTION_HOST_SESSION_CORE_PASS
HOST_OWNED_EVENT_ID_SUPPLY_PASS
SINGLE_HOST_TICK_AUTHORITY_PRESERVED
UNITY_SCHEDULER_NOT_IMPLEMENTED
NETWORKING_NOT_STARTED
