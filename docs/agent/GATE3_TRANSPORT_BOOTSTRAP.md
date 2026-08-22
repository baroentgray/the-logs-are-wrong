# TLAW-073 — accepted Gate-3 transport bootstrap

## Authority and bounded scope

This implementation consumes D-017 and preserves D-019, D-020, and D-021.
It materializes only the accepted Unity transport dependencies and an inert
production composition. It does not implement connection identity, actor
binding, `IntentEnvelope` ingress, RPCs, replication, snapshots/resync,
reconnect, prediction, controls, lobby UX, host loss, D-016, or gameplay.

The exact execution baseline is
`1c3cc9895c6fe37e64ea147cbb102fc6d2718804`. Phase 0 ran before tracked
edits: `origin/main`, task-branch `HEAD`, and the isolated worktree all matched
that SHA; the tree was clean; the repository verifier passed; .NET SDK was
`10.0.103`; and the pinned editor reported `6000.3.21f1`.

## Exact D-017 materialization

The production Unity package manifest uses the accepted immutable Git URLs:

| Component | Manifest identity | Resolved lock identity |
| --- | --- | --- |
| FishNet | `https://github.com/FirstGearGames/FishNet.git?path=Assets/FishNet#4.7.2` | `de19b5d66459f60400ffd0edc443c4da173a01e7` |
| Steamworks.NET | `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1` | `c21a8f0e31c56ae8707130967faf491f7dd7c0d8` |

FishySteamworks is imported unchanged from the official
`FishySteamworks.4.1.1.unitypackage` release asset: `17,188` bytes, SHA-256
`5698D16BD29B8B08D35E12A9B817CE69992F70D7C14B64810961691ECD9AFC57`.
The repository attributes preserve this imported subtree as non-text so Git
does not normalize its official CRLF bytes. Its three upstream
trailing-whitespace lines are scoped out of whitespace lint for that subtree;
no vendor source content was edited.
The committed imported non-meta tree has the deterministic ordered-byte SHA-256
`FBB559519669296F3E2676FAE011CDD9E9EDC906E5A967D9576E164C34C81C2D`.
That guard covers its ten official asset payloads, including `package.json`
version `4.1.1`; no package source was patched, forked, or substituted.

## Inert production composition

`Gate2Bootstrap.unity` carries exactly one FishNet `NetworkManager`, one
`TransportManager` selecting exactly one `FishySteamworks.FishySteamworks`, and
one `Gate3TransportBootstrap` marker. The FishySteamworks serialized field is
committed as `_peerToPeer: 1`; this does not depend on the rejected shipped
default of `false`.

The transport composition contains no `NetworkObject`, no transport start/stop
call, no Steam API initialization/shutdown call, no HostSession/tick execution,
and no gameplay ingress. Its empty FishNet `DefaultPrefabObjects` collection is
serialized only to permit an offline `NetworkManager` to initialize without
creating replicated gameplay objects. Its persistence setting is explicitly
off, so it cannot move the existing bootstrap root or its one HostSession owner
to `DontDestroyOnLoad`.

The existing C1 artifact remains `2326` decoded bytes with SHA-256
`94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541` and
canonical projection SHA-256
`4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1`.
The existing three PortableAuthority plugins and their identity remain unchanged.

## Bounded manual transport check

`STEAM_RUNTIME_START_STOP_MANUAL_CHECK_REQUIRED`.

TLAW-073 deliberately does not start a FishySteamworks server/client in
headless verification. The bounded follow-up manual check requires a Steam
client and real Steam runtime context: launch the built player under the
accepted App ID, explicitly start and stop the configured P2P transport, and
record the server/client lifecycle outcome. It must not be replaced by an
invented headless success marker. The automated player smoke proves only the
required inert startup marker, `TLAW073_TRANSPORT_INERT`.

## Candidate verification

- TLAW-073 repository contracts: `2/2` passed.
- Pinned Unity `6000.3.21f1 (c02631ffc030)` focused TLAW-073 EditMode class:
  `2/2` passed.
- Pinned Unity full EditMode suite: `53/53` passed.
- PortableAuthority deterministic Release and full solution Release: zero
  warnings and zero errors; full .NET suite: `1661/1661` passed.
- Preserved architecture slices: D-014 `87/87`, TLAW-067 `6/6`, TLAW-068
  `10/10`, TLAW-070 `5/5`, TLAW-071 `2/2`, TLAW-072 `2/2`.
- Windows x64 Development build succeeded with zero errors/warnings, size
  `153570746` bytes. Its player smoke exited `0` and emitted the existing
  TLAW-071 owner markers, `TLAW073_TRANSPORT_INERT`, and the clean 60-frame
  bootstrap exit marker.

## Explicitly not changed

PortableAuthority, HostSession, HostTickCadence, HostTickExecutionService,
accepted-batch semantics, authoritative clocks/deadlines, EventId/state-version/
event-sequence semantics, C1 artifact/manifest content, the three-plugin
boundary, gameplay admission, actor binding, networking gameplay contracts,
replication, snapshots, reconnect, prediction, controls, UI, audio, D-016, and
decision records are outside this increment.
