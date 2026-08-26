# Gate-3 server connection-to-actor binding — TLAW-075

## Authority and scope

TLAW-075 consumes Issue #172. It adds one transient, server-owned registry at
the transport boundary. The registry records a server-observed Fishy connection
identity as live, and it can later hold one explicitly supplied trusted
`ActorId`. It does not choose, allocate, persist, or otherwise create an actor
assignment.

The one `Gate3ServerConnectionActorBindingBridge` is composed by the existing
`Gate2Bootstrap` authoring path beside the accepted TLAW-074 transport
lifecycle owner. It listens to the existing Fishy remote-connection and local
server-state callbacks. A remote `Started` callback registers an unbound live
connection. A remote `Stopped`, local server `Stopped`, disable, or destroy
removes the corresponding transient state. It never starts or stops a
transport.

`Gate3ServerConnectionId` deliberately permits server-observed integer zero:
Fishy's first remote connection can use zero. Its separate validity bit makes
the C# default value invalid instead of conflating it with that valid remote
connection identity.

## Typed fail-closed boundary

The registry has no gameplay message or tick API. It exposes only these
bounded typed outcomes:

- an unbound live connection resolves as `ActorNotBound`;
- an invalid or non-live connection resolves without an actor;
- a trusted supplied actor can bind only once to one live connection;
- a conflicting actor or connection binding is rejected before either map is
  changed;
- a caller-provided actor hint is explicitly ignored; only the stored trusted
  binding can resolve;
- disconnect and server teardown remove all associated transient state, so a
  recycled connection begins unbound.

There is intentionally no actor-allocation or roster policy, no network
`IntentEnvelope` ingress, no server-receive sequencing, no accepted-batch
creation, no `HostSession` reference, and no replication/snapshot/reconnect or
prediction path in this increment. The registry is not yet gameplay wiring.

## Executable proof

The TLAW-075 repository architecture contracts prove that one production
registry/bridge owns this seam, that it observes the two existing Fishy
callbacks, that the only non-vendor production `StartConnection`/`StopConnection`
owner remains TLAW-074, and that the C1 identities remain unchanged.

Pinned Unity `6000.3.21f1 (c02631ffc030)` EditMode executed
`Tlaw075ServerConnectionActorBindingTests`: **7/7 passed**. The class covers
live-unbound registration, trusted binding, ignored actor hints, both binding
conflicts with atomic rejection, invalid identities, disconnect/recycle,
server teardown, and the actual production bridge subscription. The last
contract dispatches the real Fishy `HandleRemoteConnectionState(Started, …)`
callback into the bridge, then the real Fishy local-server stopped callback,
and observes registry creation followed by teardown.

The full pinned EditMode suite passed **69/69**. The TLAW-074 focused
regression class passed **9/9**, preserving the only transport lifecycle
start/stop owner. The Release solution build completed with zero warnings and
zero errors; the full .NET suite passed **1665/1665** and the TLAW-072,
TLAW-074, and TLAW-075 architecture slices each passed **2/2**.
The D-014/TLAW-046 slice passed **87/87**; TLAW-067 and TLAW-068 passed
**6/6** and **10/10** respectively; and the preserved TLAW-073 architecture
guard passed **2/2**. The C1 export freshness check passed and the exact-head
local repository verifier passed.

The Windows x64 Development build succeeded. Its ordinary player smoke exited
zero and emitted `TLAW073_TRANSPORT_INERT` without a TLAW-074 or TLAW-075
transport marker. With the accepted test-only App-ID `480` sidecar and a
running Steam client, the explicit non-gameplay real listen-host probe exited
zero, preserved the TLAW-074 server/client callback lifecycle and stop order,
and emitted `TLAW075_SERVER_CONNECTION_REGISTERED=32767`. This is one local
listen-host callback observation only; it does not introduce a remote peer,
lobby, intent, or gameplay message.

## Preserved composition and D-017 material

The TLAW-074 lifecycle remains the sole TLAW-owned transport start/stop owner.
The new bridge merely observes its existing transport. `Gate2Bootstrap` still
uses the existing Fishy asset with `_peerToPeer=true`; it adds no second
network manager, transport, lifecycle, host owner, or session.

| Item | Required identity |
| --- | --- |
| Unity | `6000.3.21f1 (c02631ffc030)` |
| FishNet | `4.7.2` / `de19b5d66459f60400ffd0edc443c4da173a01e7` |
| Steamworks.NET | `2025.164.1` / `c21a8f0e31c56ae8707130967faf491f7dd7c0d8` |
| FishySteamworks release | 4.1.1, SHA-256 `5698D16BD29B8B08D35E12A9B817CE69992F70D7C14B64810961691ECD9AFC57` |
| Fishy imported non-meta tree | `FBB559519669296F3E2676FAE011CDD9E9EDC906E5A967D9576E164C34C81C2D` |
| C1 artifact | 2326 bytes, SHA-256 `94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541` |
| C1 canonical projection | `4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1` |

## Explicitly not changed

PortableAuthority; HostSession; cadence and tick execution; accepted-batch and
Stage-2 semantics; C1/YAML material; TLAW-072 local admission; the TLAW-074
controller; FishySteamworks vendor source; package pins; actor allocation and
roster policy; network gameplay ingress; server receive sequence; replication;
snapshots; resync; reconnect; prediction; lobby/discovery; controls;
presentation; D-016; scenes other than mechanically regenerated
`Gate2Bootstrap`; and any Ready, review, merge, or Gate-3 follow-up decision.
