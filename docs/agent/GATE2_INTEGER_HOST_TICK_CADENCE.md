# Gate 2 — deterministic integer host tick cadence core (TLAW-068)

## Candidate binding and Phase 0

| Item | Evidence |
| --- | --- |
| GitHub issue | [#158](https://github.com/baroentgray/the-logs-are-wrong/issues/158) |
| Implementation-start authorization | Issue #158 comment `5361573265` |
| Exact baseline / `origin/main` before edits | `0d1e922472a39c32751b3058880b95f0c3c4719a` |
| Branch / worktree | `task/TLAW-068-integer-host-tick-cadence` / `C:\Projects\TheLogsAreWrong-worktrees\TLAW-068` |
| First implementation candidate | `d4c321bcb402dd159af4717e879de6eb02d811ed` |
| Draft PR | [#159](https://github.com/baroentgray/the-logs-are-wrong/pull/159), base `main`, body exactly `Closes #158` |
| Pinned Unity | `6000.3.21f1`, changeset `c02631ffc030` |

Phase 0 completed before any production edit. The isolated worktree path,
branch, `HEAD`, `origin/main`, and clean status all matched the authorization;
there was no earlier TLAW-068 implementation commit.

The inventory found:

1. `HostSession` remains plain C# in PortableAuthority, owns its carried state
   and atomic journal, and retains host-owned Stage-7 event identity supply.
2. There is exactly one `HostTickExecutionService`, with the frozen order:
   completion, admitted intents, deadlines, saw, feed, derived state, event
   publication.
3. `ServerTick` and `SimulationDuration` remain initialized non-negative,
   checked-`long` primitives; their meanings were not changed.
4. TLAW-065's `ExactTickSchedulerProbe` remains a nested Unity **test-only**
   integer-millisecond probe. `ManualSimulationClock` remains an explicit
   simulation-duration clock, not elapsed-time cadence or a host loop.
5. No production cadence boundary or production Unity host loop existed.
6. The Unity plugin inventory was exactly PortableAuthority,
   `System.Collections.Immutable`, and
   `System.Runtime.CompilerServices.Unsafe`; no Domain DLL or fourth plugin
   existed.
7. PortableAuthority had one direct package reference
   (`System.Collections.Immutable` `8.0.0`), no project reference, and no
   Unity, networking, FishNet, FishySteamworks, Steamworks, HTTP, socket, or
   configuration dependency. Gate-2 package manifests contained no networking
   package.

The pre-edit TLAW-067 focused regression slice passed `66 / 66`.

## Production cadence boundary

Before this increment, no production boundary answered when a tick became due.
The former exact scheduler was only a Unity test probe.

After this increment, one PortableAuthority file adds the full plain-C# U2
boundary:

```text
AuthoritativeElapsedMilliseconds (initialized non-negative long milliseconds)
    -> HostTickCadence.Accumulate(elapsed delta)
       -> remainder milliseconds + compact DueServerTickRange
       -> external owner may explicitly RetireNextDueTick()
```

`HostTickCadence` owns only three scheduling-continuity values:

- the exact sub-second remainder (`0..999` milliseconds);
- the count of due but unretired ticks; and
- the next due `ServerTick` cursor.

It owns no shift, quota, journal, configuration, catalog, gameplay, network,
or client state. It invokes no HostSession, host-tick composer, stage executor,
or any simulation semantics. It therefore answers only **which / how many
ticks are due**, never what happens in a tick.

## Exact integer semantics

`AuthoritativeElapsedMilliseconds` rejects a default value on use and rejects
negative construction. `Accumulate` accepts deltas only, so no absolute-time
regression path exists. It computes with checked `long` arithmetic:

```text
total = checked(prior remainder + elapsed milliseconds)
new ticks = total / 1000
remainder = total % 1000
due backlog = checked(prior backlog + new ticks)
```

One `ServerTick` is exactly one second / `1000` milliseconds. Integer addition
and division make the result depend only on the total elapsed integer evidence,
not on render-frame or call partition. A due range gives first tick, last tick,
and count without allocating a collection proportional to a stall. Backlog is
not retired by `Accumulate`; only an explicit ordered
`RetireNextDueTick()` acknowledgement removes exactly one due tick.

All additions and the proposed due range are preflighted before any cadence
field changes. Elapsed accumulation overflow, backlog overflow, invalid
evidence, and a due range beyond `ServerTick` capacity therefore fail closed
with the prior cadence continuity unchanged. No catch-up cap, frame budget,
adaptive skipping, or dropped-tick policy exists.

## Deterministic cadence evidence

The bounded .NET TLAW-068 contract suite passed `10 / 10` and proves:

- `999ms -> 0`, `1000ms -> 1`, and `2000ms -> 2` due ticks;
- exact sub-second remainder preservation across calls;
- zero, one, and many due ticks;
- identical `20,000ms` authoritative histories split into 20 versus 120 calls
  result in the same cursor, remainder, backlog and range;
- a `10,000s` stall exposes all `10,000` due ticks, while a `1,000,000s`
  stress call exposes one million ticks with one compact range;
- pending backlog survives unrelated sub-tick evidence and only explicit,
  ordered retirement changes it;
- deterministic replay from identical initial state and elapsed evidence;
- invalid/default/negative evidence, elapsed overflow, and `ServerTick`
  overflow fail before partial cadence mutation; and
- a direct source guard rejects HostSession, composer, stages, journal,
  state/configuration, Unity, wall-clock, YAML, and networking dependencies.

The canonical projection uses elapsed milliseconds:

```text
400, 599, 1, 2000, 2500, 0, 1000
```

Its exact projection SHA-256 is:

```text
A3CFED2906266153792A1B9FFFB2CBE6EE48F450342EF933B9DAD515DD0BADA0
```

## Cross-runtime proof and derived plugin

The PortableAuthority deployment DLL was rebuilt mechanically with the existing
non-persisted deployment properties and the resulting DLL alone replaced the
existing Unity PortableAuthority plugin. Its SHA-256 is:

```text
067F7C6B2D499F37828E7AF5AB32F64A3638CC63BD211588D573320AED4BE5DA
```

The fresh deployment output equals the committed plugin byte-for-byte and has
no PortableAuthority PDB. The other two plugin hashes are unchanged:

| Plugin | SHA-256 |
| --- | --- |
| `TheLogsAreWrong.PortableAuthority.dll` | `067F7C6B2D499F37828E7AF5AB32F64A3638CC63BD211588D573320AED4BE5DA` |
| `System.Collections.Immutable.dll` | `5B1B1C83BA3D135C2FDFE425842FBE9C7432878B7E468623ACB554C69B4C130F` |
| `System.Runtime.CompilerServices.Unsafe.dll` | `01748200F2400C742AA689F1F5101BD6298EFDFD92C00C18F4FA473847235BA9` |

One bounded Unity EditMode test invokes the imported `HostTickCadence` assembly
using the exact same vector and serialization. Pinned Unity Mono completed
`21 passed / 0 failed / 0 skipped`, including equality with the canonical
net10 cadence SHA above. This is parity of one implementation, not a second
Unity cadence implementation.

The Windows x64 Development build succeeded with `0` errors and `0` warnings.
The bootstrap smoke exited `0`, reported the unchanged PortableAuthority
load/authority markers, and retained the existing player authority SHA:

```text
CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F
```

## Regression and scope evidence

Pre-final-candidate local evidence passed:

| Evidence | Result |
| --- | --- |
| Standalone PortableAuthority Release build | `0` warnings / `0` errors |
| Full repository Release build | `0` warnings / `0` errors |
| Full .NET suite | `1650 passed / 0 failed / 0 skipped` |
| D-014 `Scope=TLAW-046` | `87 / 87` passed |
| TLAW-067 HostSession/EventId slice | `6 / 6` passed |
| Existing HostTick execution slice | `15 / 15` passed |
| Architecture guards | `5 / 5` passed |

The retained canonical host values are:

```text
one tick:  287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949
four tick: C7FEC7BD00DE7D5A92DA0A89A09F61D4B7E4DC905A4F7D35687A8E6460029411
```

The source-inventory guard now records the frozen 54-file host-tick cut plus
`Runtime/HostSessionContracts.cs` and exactly one adjacent U2 cadence file.
No project, package, target, props, Domain source, reverse dependency,
configuration, or networking wiring changed.

## Retry, edge cases, and work not performed

The first Unity import found only a bounded test-harness compatibility issue:
the Unity Mono framework does not provide `SHA256.HashData` or
`Convert.ToHexString`. The parity test was changed to its profile-compatible
`SHA256.Create` / `BitConverter` equivalent. No production code, package, or
Unity compatibility setting was changed; the rerun passed.

Covered edge cases include initialized zero versus default time evidence,
negative factory input, sub-second accumulation, exact second boundaries, long
stall, a large compact backlog, explicit retirement, elapsed overflow, and
cursor overflow.

Not performed: production Unity driver or `MonoBehaviour` scheduler; Update or
FixedUpdate simulation; cadence-to-HostSession execution; U3 ownership;
configuration/YAML ingestion; Domain import; fourth plugin; scenes, prefabs,
Packages, ProjectSettings; gameplay, D-016, FishNet, FishySteamworks,
Steamworks, transport, networking, Ready, merge, or cleanup.

Final exact candidate identity, clean-tree verification, and Repository CI
artifact are recorded after the final candidate is committed and pushed; this
dossier does not claim those future-head results in advance.

```text
INTEGER_HOST_TICK_CADENCE_PASS
ONE_SECOND_SERVER_TICK_CONTRACT_PRESERVED
DUE_TICK_BACKLOG_LOSSLESS
HOSTSESSION_NOT_DRIVEN_BY_CADENCE
UNITY_PRODUCTION_DRIVER_NOT_IMPLEMENTED
NETWORKING_NOT_STARTED
```
