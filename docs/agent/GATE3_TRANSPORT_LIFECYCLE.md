# Gate-3 transport lifecycle — TLAW-074

## Authority and scope

TLAW-074 consumes Issue #170. It adds exactly one explicit TLAW-owned
transport lifecycle seam over the existing D-017 FishNet + FishySteamworks
composition. It is transport-only: it neither owns nor touches simulation
authority or gameplay input.

`Gate3TransportLifecycle` is inert unless a caller explicitly requests one of
its bounded paths:

- listen-host: request server, observe server `Started`, request local
  host-client, observe client `Started`;
- listen-host stop: request client stop, observe client `Stopped`, then request
  server stop and observe server `Stopped`;
- client-only: bounded explicit start/stop for a later peer transport check,
  using Fishy's existing serialized client address.

A request result is never treated as connection proof. The controller changes
to either started phase only from the actual Fishy/FishNet local connection
state callbacks. Its timeout, duplicate/conflicting role checks, invalid stop
ordering, immediate request failures, and partial-start rollback all fail
closed. A rollback retains a tracked phase until the relevant stopped callback
arrives.

### CC-074-01 accepted-start rollback responsibility

An accepted `StartServer()` or `StartClient()` request creates cleanup
responsibility even when the latest callback cache still says `Stopped`. The
controller keeps per-side accepted-start bookkeeping until that side emits an
actual `Stopped` callback. Thus, a start timeout with no callback explicitly
requests the corresponding stop and remains in a rollback phase; it never
declares `Offline` from a pre-start cached `Stopped` value. The callback remains
the sole proof that a side reached `Started` or completed stopping.

The deterministic controller contracts cover server-start timeout with no
callback (`start-server`, `stop-server`), client-only timeout with no callback
(`start-client`, `stop-client`), and a listen-host host-client timeout
(`start-server`, `start-client`, `stop-client`, then after the actual client
`Stopped`, `stop-server`). Each keeps the role tracked until the relevant
actual stopped callback. They also prove rejected stop requests fault closed for
server cleanup, client-only cleanup, and both client and server legs of
listen-host partial-start cleanup. Pinned Unity `6000.3.21f1 (c02631ffc030)`
executed this controller class after the correction: `7/7` passed.

The full pinned EditMode suite then passed `62/62`. The same corrected working
tree passed the full .NET suite (`1663/1663`), a zero-warning/error Release
solution build, C1 freshness, a Windows x64 Development build
(`153589131` bytes; zero errors/warnings), the ordinary inert player smoke,
and the explicit real-Steam listen-host lifecycle probe. The latter again
observed server `Started`, host-client `Started`, client `Stopped`, server
`Stopped`, and `TLAW074_LISTEN_HOST_LIFECYCLE_PASS` with process exit `0`.

## Preserved D-017 material

| Item | Required identity | Result |
| --- | --- | --- |
| Unity | `6000.3.21f1 (c02631ffc030)` | preserved |
| FishNet | `4.7.2` / `de19b5d66459f60400ffd0edc443c4da173a01e7` | preserved |
| Steamworks.NET | `2025.164.1` / `c21a8f0e31c56ae8707130967faf491f7dd7c0d8` | preserved |
| FishySteamworks release | 4.1.1, SHA-256 `5698D16BD29B8B08D35E12A9B817CE69992F70D7C14B64810961691ECD9AFC57` | preserved |
| Fishy imported non-meta tree | `FBB559519669296F3E2676FAE011CDD9E9EDC906E5A967D9576E164C34C81C2D` | preserved by TLAW-073 guard |
| Production P2P configuration | `_peerToPeer=true` | explicit and guarded |

The exact official `SteamManager.cs` asset carried inside the accepted
`SteamManager.unitypackage` is imported unchanged at
`Assets/Scripts/Steamworks.NET/SteamManager.cs`. The raw archive asset and the
committed imported source both SHA-256 to
`0CB2C43F2DFEA8C8808D1F086CF4281EF33E1724EC560AB250832BFF8AB8401F`.
It is on a separate inactive `Gate3SteamRuntime` object, so ordinary bootstrap
does not initialize Steam or begin transport work. It becomes active only at an
explicit lifecycle request and never carries the existing production owner.
No file below `Assets/FishNet/Plugins/FishySteamworks` was modified.

## Executable proof

- Repository TLAW-074 architecture contracts prove one TLAW-owned production
  start/stop call-site, preserved inert marker and C1 identities, and no
  simulation/intent/replication coupling.
- Pinned Unity EditMode tests prove ordinary scene composition is inert, the
  callback-driven listen-host ordering, client-first stop ordering, duplicate
  and conflicting role rejection, invalid stop ordering, immediate request
  failure, timeout rollback, accepted-start no-callback cleanup, and stop
  rejection fail-closed behavior.
- The normal player smoke uses `-tlaw-bootstrap-smoke`; it starts the existing
  production owner and emits `TLAW073_TRANSPORT_INERT` with no TLAW-074
  lifecycle state/request marker.

## Real Steam runtime evidence

Steam was running under the accepted App-ID context `480`; the runtime
`steam_appid.txt` was placed beside the ignored built executable for the manual
check. The player was launched with the explicit non-gameplay command-line
probe `-tlaw-gate3-listen-host-lifecycle-smoke`.

Observed callback sequence, with clean process exit `0`:

```text
TLAW074_LISTEN_HOST_START_REQUESTED
TLAW074_SERVER_CONNECTION_STATE=Starting
TLAW074_SERVER_CONNECTION_STATE=Started
TLAW074_CLIENT_CONNECTION_STATE=Starting
TLAW074_CLIENT_CONNECTION_STATE=Started
TLAW074_LISTEN_HOST_STOP_REQUESTED
TLAW074_CLIENT_CONNECTION_STATE=Stopping
TLAW074_CLIENT_CONNECTION_STATE=Stopped
TLAW074_SERVER_CONNECTION_STATE=Stopping
TLAW074_SERVER_CONNECTION_STATE=Stopped
TLAW074_LISTEN_HOST_LIFECYCLE_PASS
```

This is one local listen-host lifecycle check only. It is not a remote peer,
lobby, discovery, game message, or gameplay transport proof.

## Explicitly not changed

PortableAuthority, the production owner and its session lifecycle, cadence,
tick execution, C1 payload/manifest identities, TLAW-072 local admission,
Stage-2 authority, clocks/deadlines, events, versions, replay, connection
identity, network gameplay messages, replication, snapshots, resync, reconnect,
prediction, lobby/discovery UX, controls, presentation, D-016, package pins,
and FishySteamworks vendor source remain outside TLAW-074.
