# Gate-3 authoritative network receive-tick mapping — TLAW-076

## Authority and scope

TLAW-076 consumes Issue #174. It provides exactly one server-owned observation
seam for a future network ingress: exact authoritative elapsed session time to
one `ServerTick`. It is not a network message, admission, or gameplay path.

The production owner establishes a fresh monotonic timestamp origin before it
creates the cadence and `HostSession`. The cadence delta bridge and the
receive-time observer use that same source and origin. Reading the observer is
non-consuming: it neither advances the cadence delta sample nor changes its
remainder, due range, retire cursor, `HostSession`, or gameplay state.

The frozen inclusive-boundary mapping is:

```text
elapsed_ms == 0 => tick 0
elapsed_ms > 0  => floor((elapsed_ms - 1) / 1000)
```

Consequently, `0`, `1`, `999`, and `1000` ms map to tick `0`; `1001` and
`2000` map to tick `1`; and `2001` maps to tick `2`. At 5000 ms, five
unretired cadence ticks still map a new receive observation to tick `4`, and
retiring that backlog without advancing elapsed time cannot alter the result.

The observer fails closed on a non-running owner, a monotonic regression, or
checked arithmetic overflow. Session replacement resets the origin, so its
first observation again maps to tick `0`.

### CC-076-01 monotonic-read correction

The timestamp bridge retains a read guard separate from the cadence delta
cursor. Every successful timestamp read advances that guard; both an
observation and the next cadence sample reject a timestamp lower than that
latest read. Observation still does not alter the cadence `_lastTimestamp` or
its retained sub-millisecond numerator remainder. Reset establishes a new
origin and resets the guard with it.

This closes the prior gap where `100 -> 200 -> 150` could remain above the
origin while moving backward relative to an earlier observation. Pinned Unity
now proves both that observe-to-observe sequence and an observe-to-sample
`0 -> 500 -> 400` regression fail closed, while the preserved
`0 -> 500 -> 1000` probe still yields observe `500` and cadence delta `1000`.

## Executable proof

The repository TLAW-076 architecture contracts passed **2/2**. They enforce
that the mapper consumes only authoritative elapsed evidence, contains no
FishNet/Fishy/Steam, RPC, connection, actor, intent, receive-sequence,
accepted-batch, `HostSession`, cadence, replication, or transport start/stop
coupling; that the production owner creates and clears the bounded observer;
and that TLAW-074 remains the sole TLAW-owned transport start/stop owner.

Pinned Unity `6000.3.21f1 (c02631ffc030)` ran
`Tlaw076NetworkReceiveTickMappingTests`: **6/6 passed**. The tests cover every
frozen boundary, deterministic repetition, a five-tick unretired backlog and
retirement invariance, non-consuming observation followed by the real cadence
sample, fresh reset origin, non-running owner rejection, monotonic regression,
including prior-observation and observe-to-sample regressions, and overflow
fail-closed behavior. The full pinned Unity EditMode suite passed **75/75**.
Preserved focused regressions passed TLAW-072 **13/13**, TLAW-074
**9/9**, and TLAW-075 **7/7**.

The Release solution build completed with zero warnings and zero errors; the
full .NET suite passed **1667/1667**. The D-014/TLAW-046 slice passed **87/87**;
TLAW-067 **6/6**, TLAW-068 **10/10**, TLAW-070 **5/5**, TLAW-071 **2/2**,
TLAW-072 **2/2**, TLAW-073 **2/2**, TLAW-074 **2/2**, TLAW-075 **2/2**, and
TLAW-076 **2/2** passed. C1 export freshness passed. The deterministic
PortableAuthority Release build had zero warnings/errors and SHA-256
`BD1E5DDA62192587B12737CCE9BBBB272FB75C4B309BA173AF2AA7684E2A7085`.

The Windows x64 Development build succeeded with zero warnings/errors
(`153601280` bytes reported by Unity; `153601523` bytes total output). The
ordinary `-tlaw-bootstrap-smoke` player exited `0`, emitted
`TLAW073_TRANSPORT_INERT`, and emitted no TLAW-074, TLAW-075, or TLAW-076
marker.

## Preserved deployment identities

| Item | Verified identity |
| --- | --- |
| Unity | `6000.3.21f1 (c02631ffc030)` |
| FishNet | `4.7.2` / `de19b5d66459f60400ffd0edc443c4da173a01e7` |
| Steamworks.NET | `2025.164.1` / `c21a8f0e31c56ae8707130967faf491f7dd7c0d8` |
| Fishy imported non-meta tree | `FBB559519669296F3E2676FAE011CDD9E9EDC906E5A967D9576E164C34C81C2D` |
| Production P2P configuration | `_peerToPeer=true` |
| Unity plugin inventory | exactly three DLLs |
| PortableAuthority plugin | `BD1E5DDA62192587B12737CCE9BBBB272FB75C4B309BA173AF2AA7684E2A7085` |
| C1 artifact | 2326 bytes, `94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541` |
| C1 canonical projection | `4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1` |

## Explicitly not changed

PortableAuthority; `HostSession`; `HostTickCadence` and its semantics; tick
execution; C1/YAML/configuration material; TLAW-072 local admission; TLAW-074
transport lifecycle start/stop ownership; TLAW-075 connection-to-actor binding;
FishNet gameplay messages/RPC/Broadcast; `IntentEnvelope` wire behavior;
actor allocation; server receive sequencing; accepted-batch construction;
gameplay/result transport; replication, snapshots, resync, reconnect, or
prediction; D-017/vendor/packages; scenes/prefabs; controls/presentation;
D-016; and any Ready, review, merge, cleanup, or subsequent Gate-3 decision.
