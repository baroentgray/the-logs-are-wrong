# Gate 2 local authoritative gameplay-intent admission — TLAW-072

## Scope and authority boundary

TLAW-072 adds one plain-C# `Gate2LocalIntentAdmissionAdapter` to the existing Gate-2 production owner. Its public ingress is `SubmitLocalIntent(IntentEnvelope envelope, ActorId authoritativeActor)`. The caller cannot provide receive tick, receive sequence, session metadata, or transport metadata.

The adapter owns only local pre-Stage-Two evidence for one live owner:

```text
exact IntentEnvelope reference + separately trusted local ActorId
-> exact open ServerTick + zero-based contiguous ServerReceiveSequence
-> AuthoritativeAcceptedIntent
-> AcceptedIntentTickBatchFactory.Create
-> IAlreadyAdmittedHostInputSource
-> HostSession.ExecuteTick Stage Two through Stage Seven
```

`IntentEnvelope.ActorIdHint` remains untrusted metadata. The adapter retains the submitted envelope and its parameter object exactly; it does not rewrite either. It does not validate gameplay action, target, state version, parameter semantics, active tools, or any Stage-Two rule.

## Open-tick lifecycle

- A fresh adapter starts at `ServerTick.Zero` with receive sequence zero.
- Same-open-tick duplicate `IntentId` is rejected locally and does not consume a receive sequence.
- Null envelope, shift mismatch, unbound authoritative actor, disposed adapter, and sequence exhaustion are local ingress outcomes only; they do not manufacture domain rejection events.
- `GetInput` permits only the matching adapter shift and exact open tick. It invokes the existing `AcceptedIntentTickBatchFactory` once, returns empty active-tool evidence, then opens exactly the next tick and resets the receive sequence to zero.
- A reset, fault, or disposal removes the old adapter. A replacement session creates a fresh adapter at tick zero; no pending evidence crosses the lifecycle boundary.
- A long cadence backlog asks the adapter once for every due tick. After the open tick is materialized, later catch-up ticks are empty unless separately admitted; queued input is never copied into them.

## Preserved production driver ordering

The existing production driver remains the sole owner and its cadence loop remains fixed:

```text
HostTickCadence.Accumulate
-> IAlreadyAdmittedHostInputSource.GetInput
-> HostSession.ExecuteTick
-> HostTickCadence.RetireNextDueTick
```

The runtime owner constructs the local adapter only after C1 materialization, explicit profile selection, cadence construction, and the one `HostSession` construction have succeeded. Existing test-only input sources remain test seams; production runtime uses the adapter.

## Preserved C1 and plugin identities

No C1 artifact, manifest, codec, YAML behavior, or PortableAuthority source changes are part of this increment:

- C1 bytes: `2326`.
- C1 SHA-256: `94FCBE2B0E08662E9E45DDFC4D310A1E3063F6A765FE36B596409021D930B541`.
- Canonical configuration projection SHA-256: `4837EF28FC0480DC133B72A024110E3569E2CB2973E206A4542A7C70949F7AB1`.
- PortableAuthority plugin SHA-256: `BD1E5DDA62192587B12737CCE9BBBB272FB75C4B309BA173AF2AA7684E2A7085`.

## Explicit non-goals

No PortableAuthority accepted-batch, HostSession, cadence, HostTickExecutionService, C1/YAML, decision, package/project-setting, prefab, scene, gameplay controls, active-tool mechanics, UI/audio, D-016, network transport/RPC, connection binding, Gate-3 policy, replication, prediction, or second runtime-owner path is added or changed.

## Executable evidence

Phase 0 passed against base `32ac403b395f910691f739fec2e801aa14fd639c`: `origin/main`, the isolated branch and its initial `HEAD` matched exactly; the worktree was clean; prior TLAW-071 merged/closed state and stale-branch absence were confirmed; no TLAW-072 PR existed; decisions D-019 through D-022 and the existing accepted-batch/driver/C1 seams were read before editing.

The focused tests exercise the real adapter and production driver:

- exact reference retention for two envelopes and their parameter objects, independently trusted actors, open tick zero, sequences zero/one, and empty active tools;
- duplicate, null, wrong-shift, and unbound-actor local rejections without a sequence gap;
- exact `GetInput` tick enforcement without advancing a rejected materialization;
- the real `Gate2ProductionHostDriver` path from local admission through `HostSession` to a non-empty Stage Seven publication and a retired due tick;
- a three-tick backlog: the adapter advances to tick three with sequence zero after draining, proving that its tick-zero input was not cloned into later catch-up ticks;
- reset creating a new adapter window at tick zero, and disposed-owner ingress returning `OwnerNotRunning`.

The repository source guards also require one plain-C# adapter, one use of `AcceptedIntentTickBatchFactory.Create`, no Unity/gameplay/transport/host-tick implementation in the adapter, and retained production pump ordering.

- PortableAuthority standalone deterministic Release build: 0 warnings, 0 errors; fresh SHA equals the pinned plugin SHA `BD1E5DDA62192587B12737CCE9BBBB272FB75C4B309BA173AF2AA7684E2A7085`.
- Full solution Release build: 0 warnings, 0 errors.
- Full .NET suite: `1659/1659` passed.
- D-014 Scope=TLAW-046: `87/87` passed.
- TLAW-067 HostSession/EventId slice: `6/6` passed.
- TLAW-068 cadence slice: `10/10` passed.
- Existing TLAW-070 C1 architecture slice: `5/5` passed.
- Existing TLAW-071 owner architecture slice: `2/2` passed.
- TLAW-072 source architecture slice: `2/2` passed.
- Post-D011 corrected TLAW-072 real-adapter EditMode class: `13/13` passed.
- Post-D011 corrected Unity `6000.3.21f1 (c02631ffc030)` full EditMode suite: `53/53` passed.
- `Tlaw.ValidatedConfig.Export --check`: `VALIDATED_CONFIG_C1_EXPORT_FRESH`.
- Windows x64 Development player build: `BUILD_RESULT=Succeeded`, `BUILD_ERRORS=0 BUILD_WARNINGS=0`, size `146470037`; bootstrap smoke exited `0` after its 60-frame marker with the required TLAW-071 owner-start and teardown markers.

The canonical regression identities remain unchanged: one tick `287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949`; four tick `C7FEC7BD00DE7D5A92DA0A89A09F61D4B7E4DC905A4F7D35687A8E6460029411`; cadence `A3CFED2906266153792A1B9FFFB2CBE6EE48F450342EF933B9DAD515DD0BADA0`.

```text
LOCAL_HOST_INTENT_ADMISSION_PASS
LOCAL_INTENT_TICK_AND_SEQUENCE_OWNED_BY_ADAPTER
ACCEPTED_BATCH_FACTORY_REMAINS_SOLE_BATCH_VALIDATOR
HOSTSESSION_REMAINS_STAGE_TWO_AND_STAGE_SEVEN_AUTHORITY
LONG_BACKLOG_DOES_NOT_CLONE_LOCAL_INPUT
C1_AND_PORTABLE_PLUGIN_IDENTITIES_PRESERVED
GATE3_NETWORKING_NOT_STARTED
```

## Bounded correction evidence

The authorized evidence-only correction closes all four Control Center findings without changing admission, host, cadence, C1, or gameplay semantics.

- The adapter contracts now prove wrong-`ShiftId` `GetInput` fails before clearing/advancing the valid window; a materialized tick cannot reopen; skipped/future ticks fail; the actual pinned-runner checked `ServerTick` exhaustion is asserted directly as `OverflowException` while preserving the open tick and pending evidence; and an independent terminal `ServerReceiveSequence` test retains its one terminal receipt then refuses a successor rather than wrapping or assigning a duplicate.
- The real production-local-admission driver now exposes its exact successful stage result only under `UNITY_EDITOR`. This non-semantic, player-excluded observation lets the EditMode contract prove three structurally valid but gameplay-invalid envelopes are admitted unchanged into the real `HostSession`: stale state version and missing target produce the existing Stage-Two rejection outcomes, while an unsupported action produces the existing `UnsupportedIntentStageOutcome`. The Stage-Seven rejection evidence remains owned by PortableAuthority.
- A running production owner rejects a cross-shift ingress locally, remains running and fault-free, then executes the next empty authoritative tick once and retires it. The observed real Stage-Two batch is empty, so rejected ingress cannot poison the session.
- A retained live adapter with pending evidence is faulted through its existing `GetInput` exact-tick boundary using test reflection. The real owner becomes `Faulted`, retains the due cadence tick, disposes and clears the old adapter, returns `OwnerNotRunning` for driver ingress, and cannot pump or admit later input. Direct stale-adapter ingress returns `AdapterDisposed`; direct materialization throws `ObjectDisposedException`.

### Post-D011 runner discrepancy and final proof

D-011 predicted that `MethodInfo.Invoke` would wrap checked tick exhaustion in `TargetInvocationException`. The prescribed wrapper probe was executed on pinned Unity `6000.3.21f1` and produced `12/13`: its only failure observed `OverflowException` directly from `Gate2LocalIntentAdmissionAdapter.GetInput`, with no wrapper to inspect. The independent receive-sequence exhaustion test passed. No production change or reflection-helper normalization, wrapping, unwrapping, translation, or fabrication was made. The final overflow proof follows the actual pinned-runner behavior with direct `Assert.Throws<OverflowException>` while retaining the open-tick and pending-evidence assertions; its focused result is `13/13` and the full pinned EditMode result is `53/53`.
