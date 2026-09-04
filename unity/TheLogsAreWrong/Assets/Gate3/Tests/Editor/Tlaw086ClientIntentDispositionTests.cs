using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Gate2;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TheLogsAreWrong.Gate3.Tests
{
    /// <summary>Executable D-026 V1 codec and session-ledger contracts.</summary>
    public sealed class Tlaw086ClientIntentDispositionTests
    {
        private const string ArtifactPath = "Assets/Gate2/Configuration/validated-configuration-c1-v1.base64";
        private const string ManifestPath = "Assets/Gate2/Configuration/validated-configuration-c1-v1.manifest";
        private static readonly ShiftId Shift = ShiftId.From("P0_SHIFT_A");
        private readonly List<GameObject> _roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = _roots.Count - 1; index >= 0; index--)
            {
                if (_roots[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_roots[index]);
                }
            }

            _roots.Clear();
            var reset = typeof(Gate2ProductionHostDriver).GetMethod("ResetProcessLeaseAtSubsystemRegistration", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(reset, "The existing production owner lease reset hook is required.");
            reset.Invoke(null, null);
        }

        [Test]
        public void Result_v1_golden_vector_is_little_endian_and_round_trips_exactly()
        {
            var result = Rejected("intent_01", 42, StateVersion.From(9), "STALE_STATE_VERSION");

            Assert.IsTrue(Gate3ClientIntentResultV1Codec.TryEncode(result, out var payload, out var encodeFailure), encodeFailure.ToString());
            CollectionAssert.AreEqual(new byte[]
            {
                1, 0,
                10, 0, (byte)'P', (byte)'0', (byte)'_', (byte)'S', (byte)'H', (byte)'I', (byte)'F', (byte)'T', (byte)'_', (byte)'A',
                9, 0, (byte)'i', (byte)'n', (byte)'t', (byte)'e', (byte)'n', (byte)'t', (byte)'_', (byte)'0', (byte)'1',
                3,
                42, 0, 0, 0, 0, 0, 0, 0,
                1,
                9, 0, 0, 0, 0, 0, 0, 0,
                19, 0, (byte)'S', (byte)'T', (byte)'A', (byte)'L', (byte)'E', (byte)'_', (byte)'S', (byte)'T', (byte)'A', (byte)'T', (byte)'E', (byte)'_', (byte)'V', (byte)'E', (byte)'R', (byte)'S', (byte)'I', (byte)'O', (byte)'N'
            }, payload);

            Assert.IsTrue(Gate3ClientIntentResultV1Codec.TryDecode(payload, out var decoded, out var decodeFailure), decodeFailure.ToString());
            AssertDisposition(result, decoded);
        }

        [TestCase(Gate3ClientIntentDispositionKind.PENDING)]
        [TestCase(Gate3ClientIntentDispositionKind.APPLIED)]
        [TestCase(Gate3ClientIntentDispositionKind.REJECTED)]
        public void Result_v1_round_trips_every_non_reserved_disposition(Gate3ClientIntentDispositionKind kind)
        {
            var result = kind switch
            {
                Gate3ClientIntentDispositionKind.PENDING => Pending("pending", 3),
                Gate3ClientIntentDispositionKind.APPLIED => Applied("applied", 4, StateVersion.From(2)),
                Gate3ClientIntentDispositionKind.REJECTED => Rejected("rejected", 5, null, "ACTOR_NOT_BOUND"),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };

            Assert.IsTrue(Gate3ClientIntentResultV1Codec.TryEncode(result, out var payload, out var encodeFailure), encodeFailure.ToString());
            Assert.IsTrue(Gate3ClientIntentResultV1Codec.TryDecode(payload, out var decoded, out var decodeFailure), decodeFailure.ToString());
            AssertDisposition(result, decoded);
        }

        [Test]
        public void Result_v1_fails_closed_for_malformed_version_utf8_tags_lengths_state_rejection_and_trailing_data()
        {
            AssertDecodeFails(null, Gate3ClientIntentResultV1Failure.TRUNCATED_OR_MALFORMED_FRAME);
            AssertDecodeFails(new byte[] { 1 }, Gate3ClientIntentResultV1Failure.TRUNCATED_OR_MALFORMED_FRAME);
            AssertDecodeFails(new byte[] { 2, 0 }, Gate3ClientIntentResultV1Failure.UNSUPPORTED_SCHEMA_VERSION);

            Assert.IsTrue(Gate3ClientIntentResultV1Codec.TryEncode(Pending("malformed", 1), out var valid, out var encodeFailure), encodeFailure.ToString());
            var invalidTag = (byte[])valid.Clone();
            invalidTag[2 + 2 + Shift.Value.Length + 2 + "malformed".Length] = 0;
            AssertDecodeFails(invalidTag, Gate3ClientIntentResultV1Failure.INVALID_DISPOSITION);

            var badStateFlag = (byte[])valid.Clone();
            badStateFlag[valid.Length - 3] = 2;
            AssertDecodeFails(badStateFlag, Gate3ClientIntentResultV1Failure.INVALID_STATE_VERSION_FLAG);

            var trailing = new byte[valid.Length + 1];
            Buffer.BlockCopy(valid, 0, trailing, 0, valid.Length);
            AssertDecodeFails(trailing, Gate3ClientIntentResultV1Failure.TRAILING_DATA);

            var bomIdentifier = new byte[]
            {
                1, 0,
                3, 0, 0xef, 0xbb, 0xbf,
                1, 0, (byte)'i',
                1,
                0, 0, 0, 0, 0, 0, 0, 0,
                0,
                0, 0
            };
            AssertDecodeFails(bomIdentifier, Gate3ClientIntentResultV1Failure.INVALID_UTF8);
        }

        [Test]
        public void Result_v1_encode_rejects_no_bom_and_inconsistent_disposition_shapes_without_payload()
        {
            Assert.IsFalse(Gate3ClientIntentResultV1Codec.TryEncode(
                Pending("\uFEFFpending", 1), out var bomPayload, out var bomFailure));
            Assert.IsNull(bomPayload);
            Assert.AreEqual(Gate3ClientIntentResultV1Failure.INVALID_UTF8, bomFailure);

            Assert.IsFalse(Gate3ClientIntentResultV1Codec.TryEncode(
                new Gate3ClientIntentDisposition(Shift, IntentId.From("bad"), Gate3ClientIntentDispositionKind.PENDING, ServerTick.From(1), StateVersion.Zero, null),
                out var pendingPayload,
                out var pendingFailure));
            Assert.IsNull(pendingPayload);
            Assert.AreEqual(Gate3ClientIntentResultV1Failure.DISPOSITION_PAYLOAD_MISMATCH, pendingFailure);

            Assert.IsFalse(Gate3ClientIntentResultV1Codec.TryEncode(
                new Gate3ClientIntentDisposition(Shift, IntentId.From("bad2"), Gate3ClientIntentDispositionKind.REJECTED, ServerTick.From(1), null, ""),
                out var rejectionPayload,
                out var rejectionFailure));
            Assert.IsNull(rejectionPayload);
            Assert.AreEqual(Gate3ClientIntentResultV1Failure.INVALID_REJECTION_CODE, rejectionFailure);
        }

        [Test]
        public void Every_existing_typed_stage_two_rejection_has_one_explicit_stable_protocol_code()
        {
            foreach (RejectionReason reason in Enum.GetValues(typeof(RejectionReason)))
            {
                Assert.IsTrue(Gate3ClientIntentDispositionLedger.TryMapStageTwoRejection(reason, out var code), reason.ToString());
                Assert.AreEqual(reason.ToString(), code);
            }
        }

        [Test]
        public void Ledger_reserves_pending_before_admission_and_replays_only_the_same_authorized_origin()
        {
            using var ledger = new Gate3ClientIntentDispositionLedger(Shift);
            var original = Origin(14, 1);
            var different = Origin(15, 2);
            var envelope = Envelope("same");

            var reserved = ledger.Reserve(envelope, original, ServerTick.From(8));
            var sameOrigin = ledger.Reserve(envelope, original, ServerTick.From(9));
            var differentOrigin = ledger.Reserve(envelope, different, ServerTick.From(9));

            Assert.AreEqual(Gate3ClientIntentDispositionReservationStatus.ReservedPending, reserved.Status);
            Assert.AreEqual(Gate3ClientIntentDispositionKind.PENDING, reserved.Disposition.Kind);
            Assert.AreEqual(Gate3ClientIntentDispositionReservationStatus.ReplaySameOrigin, sameOrigin.Status);
            Assert.AreEqual(ServerTick.From(8), sameOrigin.Disposition.AuthoritativeReceiveTick);
            Assert.AreEqual(Gate3ClientIntentDispositionReservationStatus.IntentIdAlreadyUsed, differentOrigin.Status);
            Assert.AreEqual("INTENT_ID_ALREADY_USED", differentOrigin.Disposition.RejectionCode);
            Assert.AreEqual(1, ledger.Count);
        }

        [Test]
        public void Ledger_capacity_boundary_rejects_the_4097th_new_record_before_it_can_be_admitted()
        {
            using var ledger = new Gate3ClientIntentDispositionLedger(Shift);
            var origin = Origin(22, 1);
            for (var index = 0; index < Gate3ClientIntentDispositionLedger.Capacity; index++)
            {
                var result = ledger.Reserve(Envelope("capacity_" + index), origin, ServerTick.From(1));
                Assert.AreEqual(Gate3ClientIntentDispositionReservationStatus.ReservedPending, result.Status, index.ToString());
            }

            var exhausted = ledger.Reserve(Envelope("capacity_exhausted"), origin, ServerTick.From(1));

            Assert.AreEqual(Gate3ClientIntentDispositionLedger.Capacity, ledger.Count);
            Assert.AreEqual(Gate3ClientIntentDispositionReservationStatus.ResultCapacityExhausted, exhausted.Status);
            Assert.AreEqual(Gate3ClientIntentDispositionKind.REJECTED, exhausted.Disposition.Kind);
            Assert.AreEqual("RESULT_CAPACITY_EXHAUSTED", exhausted.Disposition.RejectionCode);
        }

        [Test]
        public void Disconnect_revokes_delivery_and_a_reused_transport_id_cannot_replay_the_old_record()
        {
            using var ledger = new Gate3ClientIntentDispositionLedger(Shift);
            var oldOrigin = Origin(31, 7);
            var replacement = Origin(31, 8);
            var envelope = Envelope("disconnect");

            Assert.AreEqual(Gate3ClientIntentDispositionReservationStatus.ReservedPending, ledger.Reserve(envelope, oldOrigin, ServerTick.From(2)).Status);
            ledger.RevokeDelivery(oldOrigin);

            Assert.IsFalse(ledger.IsDeliveryAuthorized(envelope.IntentId, oldOrigin));
            Assert.AreEqual(Gate3ClientIntentDispositionReservationStatus.IntentIdAlreadyUsed,
                ledger.Reserve(envelope, replacement, ServerTick.From(3)).Status);
        }

        [Test]
        public void Dispose_clears_capacity_and_stale_session_records_cannot_be_replayed()
        {
            var origin = Origin(42, 1);
            var envelope = Envelope("reset");
            var old = new Gate3ClientIntentDispositionLedger(Shift);
            Assert.AreEqual(Gate3ClientIntentDispositionReservationStatus.ReservedPending, old.Reserve(envelope, origin, ServerTick.From(1)).Status);
            old.Dispose();

            using var fresh = new Gate3ClientIntentDispositionLedger(Shift);
            Assert.AreEqual(Gate3ClientIntentDispositionReservationStatus.ReservedPending, fresh.Reserve(envelope, origin, ServerTick.From(1)).Status);
            Assert.AreEqual(1, fresh.Count);
        }

        [Test]
        public void Same_origin_terminal_replay_returns_byte_equivalent_result_without_creating_a_second_record()
        {
            using var ledger = new Gate3ClientIntentDispositionLedger(Shift);
            var origin = Origin(51, 1);
            var envelope = Envelope("terminal_replay");
            Assert.AreEqual(Gate3ClientIntentDispositionReservationStatus.ReservedPending,
                ledger.Reserve(envelope, origin, ServerTick.From(12)).Status);
            Assert.IsTrue(ledger.TryTerminalizeAdmission(envelope.IntentId, "ACTOR_NOT_BOUND"));
            Assert.IsTrue(ledger.TryGetDisposition(envelope.IntentId, out var terminal));
            Assert.IsTrue(Gate3ClientIntentResultV1Codec.TryEncode(terminal, out var originalBytes, out var firstFailure), firstFailure.ToString());

            var replay = ledger.Reserve(envelope, origin, ServerTick.From(99));
            Assert.AreEqual(Gate3ClientIntentDispositionReservationStatus.ReplaySameOrigin, replay.Status);
            Assert.AreEqual(1, ledger.Count);
            Assert.IsTrue(Gate3ClientIntentResultV1Codec.TryEncode(replay.Disposition, out var replayBytes, out var replayFailure), replayFailure.ToString());
            CollectionAssert.AreEqual(originalBytes, replayBytes);
        }

        [Test]
        public void Real_authenticated_decoded_live_unbound_ingress_creates_actor_not_bound_only_and_non_live_ingress_creates_no_record()
        {
            var fixture = CreateProductionFixture(Array.Empty<long>(), new[] { 0L, 0L }, 61);
            var unbound = Envelope("unbound_live");

            SendDecodedCarrier(fixture, 61, unbound);

            var ledger = CurrentLedger(fixture.Disposition);
            Assert.IsTrue(ledger.TryGetDisposition(unbound.IntentId, out var result));
            Assert.AreEqual(Gate3ClientIntentDispositionKind.REJECTED, result.Kind);
            Assert.AreEqual("ACTOR_NOT_BOUND", result.RejectionCode);
            Assert.AreEqual(1, ledger.Count);
            Assert.AreEqual(Gate3AuthoritativeActorResolutionStatus.ActorNotBound, fixture.ActorResolution.LastResult.Status);

            SendDecodedCarrier(fixture, 62, Envelope("non_live"));

            Assert.AreEqual(1, ledger.Count, "A valid D-023 envelope with no live authorized recipient must remain server-local and create no ordinary result.");
            Assert.AreEqual(Gate3AuthoritativeActorResolutionStatus.ActorNotBound, fixture.ActorResolution.LastResult.Status,
                "The non-live packet must not enter actor resolution after D-026 fails closed.");
        }

        [Test]
        public void Real_d023_d025_driver_path_keeps_admission_pending_until_the_exact_successful_stage_two_step_projects_terminal_result()
        {
            var fixture = CreateProductionFixture(new[] { 1000L }, new[] { 0L, 1001L }, 71);
            Assert.AreEqual(Gate3ServerConnectionActorBindingResult.Bound,
                fixture.Binding.BindTrustedServerActor(Connection(71), ActorId.From("authoritative_actor")));
            var envelope = Envelope("pending_then_stage_two");

            SendDecodedCarrier(fixture, 71, envelope);

            var ledger = CurrentLedger(fixture.Disposition);
            Assert.IsTrue(ledger.TryGetDisposition(envelope.IntentId, out var pending));
            Assert.AreEqual(Gate3ClientIntentDispositionKind.PENDING, pending.Kind);
            Assert.AreEqual(0, fixture.Driver.ExecutedTickCount);
            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.Admitted, fixture.Admission.LastNetworkAdmission.Status);

            fixture.Driver.PumpForTesting();

            Assert.AreEqual(1, fixture.Driver.ExecutedTickCount);
            Assert.AreEqual(0L, fixture.Driver.PendingDueTickCount);
            Assert.IsTrue(ledger.TryGetDisposition(envelope.IntentId, out var terminal));
            Assert.AreEqual(Gate3ClientIntentDispositionKind.REJECTED, terminal.Kind);
            Assert.AreEqual("UNSUPPORTED_ACTION", terminal.RejectionCode);
            Assert.IsTrue(terminal.StateVersion.HasValue);
        }

        [Test]
        public void A_real_hostsession_continuity_fault_emits_no_success_projection_and_retained_pending_result_stays_pending()
        {
            var root = new GameObject("Tlaw086HostFault");
            root.SetActive(false);
            _roots.Add(root);
            var driver = root.AddComponent<Gate2ProductionHostDriver>();
            using var ledger = new Gate3ClientIntentDispositionLedger(Shift);
            var origin = Origin(81, 1);
            var envelope = Envelope("fault_pending");
            Assert.AreEqual(Gate3ClientIntentDispositionReservationStatus.ReservedPending,
                ledger.Reserve(envelope, origin, ServerTick.Zero).Status);

            var projections = 0;
            var successfulTick = typeof(Gate2ProductionHostDriver).GetField("AuthoritativeTickSucceeded", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(successfulTick, "D-026 may observe only the server-local post-HostSession success signal.");
            Action<ServerTick, HostStageSevenEventExecution> onSuccessfulTick = (_, execution) =>
            {
                projections = checked(projections + 1);
                ledger.ProjectSuccessfulTick(execution.StageTwo);
            };
            successfulTick.SetValue(driver, Delegate.Combine((Delegate)successfulTick.GetValue(driver), onSuccessfulTick));
            driver.ConfigureForTesting(Artifact(), Manifest(), new[] { 1000L }, -1, "learning", 0);
            driver.StartForTesting();
            LogAssert.Expect(LogType.Error, new Regex("TLAW071_OWNER_FAULT ArgumentException"));
            driver.PumpForTesting();

            Assert.AreEqual(ProductionHostOwnerLifecycle.Faulted, driver.Lifecycle);
            Assert.AreEqual(0, projections, "The driver must not publish a result projection when HostSession rejected the invalid continuity evidence.");
            Assert.IsTrue(ledger.TryGetDisposition(envelope.IntentId, out var pending));
            Assert.AreEqual(Gate3ClientIntentDispositionKind.PENDING, pending.Kind);
        }

        private static Gate3ClientIntentDisposition Pending(string intent, long receiveTick) =>
            new Gate3ClientIntentDisposition(Shift, IntentId.From(intent), Gate3ClientIntentDispositionKind.PENDING, ServerTick.From(receiveTick), null, null);

        private static Gate3ClientIntentDisposition Applied(string intent, long receiveTick, StateVersion stateVersion) =>
            new Gate3ClientIntentDisposition(Shift, IntentId.From(intent), Gate3ClientIntentDispositionKind.APPLIED, ServerTick.From(receiveTick), stateVersion, null);

        private static Gate3ClientIntentDisposition Rejected(string intent, long receiveTick, StateVersion? stateVersion, string rejectionCode) =>
            new Gate3ClientIntentDisposition(Shift, IntentId.From(intent), Gate3ClientIntentDispositionKind.REJECTED, ServerTick.From(receiveTick), stateVersion, rejectionCode);

        private static IntentEnvelope Envelope(string intent) =>
            new IntentEnvelope(Shift, IntentId.From(intent), ActorId.From("client_hint"), TargetId.From("target"), IntentActionId.From("unsupported_action"), StateVersion.Zero, ServerTick.Zero, NoIntentParameters.Instance);

        private ProductionFixture CreateProductionFixture(long[] elapsedMilliseconds, long[] observedElapsedMilliseconds, int connectionId)
        {
            var root = new GameObject("Tlaw086ProductionComposition");
            root.SetActive(false);
            _roots.Add(root);

            var networkManager = root.AddComponent<NetworkManager>();
            var serverManager = root.AddComponent<ServerManager>();
            SetAutoProperty(networkManager, "ServerManager", serverManager);
            SetAutoProperty(serverManager, "NetworkManager", networkManager);
            var transportManager = root.AddComponent<TransportManager>();
            var transport = root.AddComponent<FishySteamworks.FishySteamworks>();
            transportManager.Transport = transport;
            var driver = root.AddComponent<Gate2ProductionHostDriver>();
            var ingress = root.AddComponent<Gate3IntentCarrierIngress>();
            var binding = root.AddComponent<Gate3ServerConnectionActorBindingBridge>();
            var actorResolution = root.AddComponent<Gate3ActorResolutionComposition>();
            var admission = root.AddComponent<Gate3ProductionAdmissionComposition>();
            var resultCarrier = root.AddComponent<Gate3ClientIntentResultCarrier>();
            var disposition = root.AddComponent<Gate3ClientIntentDispositionComposition>();

            SetPrivateField(ingress, "_networkManager", networkManager);
            SetPrivateField(ingress, "_hostDriver", driver);
            SetPrivateField(binding, "_transport", transport);
            SetPrivateField(actorResolution, "_carrierIngress", ingress);
            SetPrivateField(actorResolution, "_connectionBinding", binding);
            SetPrivateField(admission, "_hostDriver", driver);
            SetPrivateField(admission, "_actorResolution", actorResolution);
            SetPrivateField(resultCarrier, "_networkManager", networkManager);
            SetPrivateField(resultCarrier, "_connectionBinding", binding);
            SetPrivateField(disposition, "_hostDriver", driver);
            SetPrivateField(disposition, "_actorResolution", actorResolution);
            SetPrivateField(disposition, "_admission", admission);
            SetPrivateField(disposition, "_connectionBinding", binding);
            SetPrivateField(disposition, "_resultCarrier", resultCarrier);

            InvokeLifecycle(binding, "Awake");
            InvokeLifecycle(ingress, "Awake");
            InvokeLifecycle(actorResolution, "Awake");
            InvokeLifecycle(resultCarrier, "Awake");
            InvokeLifecycle(disposition, "Awake");
            InvokeLifecycle(admission, "Awake");
            InvokeLifecycle(actorResolution, "OnEnable");
            InvokeLifecycle(admission, "OnEnable");
            InvokeLifecycle(disposition, "OnEnable");
            driver.ConfigureNetworkedProductionAdmissionForTesting(Artifact(), Manifest(), elapsedMilliseconds, observedElapsedMilliseconds, "learning");
            driver.StartForTesting();
            Assert.AreEqual(ProductionHostOwnerLifecycle.Running, driver.Lifecycle, driver.Fault?.ToString());
            transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionState.Started, connectionId, 0));
            Assert.AreEqual(1, binding.LiveConnectionCount);

            return new ProductionFixture(driver, ingress, binding, actorResolution, admission, disposition);
        }

        private static void SendDecodedCarrier(ProductionFixture fixture, int connectionId, IntentEnvelope envelope)
        {
            Assert.IsTrue(Gate3IntentWireV1Codec.TryEncode(envelope, out var payload, out var failure), failure.ToString());
            var callback = typeof(Gate3IntentCarrierIngress).GetMethod("OnCarrierBroadcast", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(callback, "TLAW-079 must retain the one authenticated carrier callback.");
            callback.Invoke(fixture.Ingress, new object[]
            {
                new NetworkConnection { ClientId = connectionId },
                new Gate3IntentCarrierBroadcast { Payload = payload },
                Channel.Reliable
            });
        }

        private static Gate3ClientIntentDispositionLedger CurrentLedger(Gate3ClientIntentDispositionComposition disposition)
        {
            var ledger = PrivateField(disposition, "_ledger") as Gate3ClientIntentDispositionLedger;
            Assert.IsNotNull(ledger, "The D-026 ledger must be created with the current D-025 host session.");
            return ledger;
        }

        private static Gate2DeploymentTextAsset Artifact() => AssetDatabase.LoadAssetAtPath<Gate2DeploymentTextAsset>(ArtifactPath);
        private static Gate2DeploymentTextAsset Manifest() => AssetDatabase.LoadAssetAtPath<Gate2DeploymentTextAsset>(ManifestPath);

        private static Gate3ServerConnectionId Connection(int id)
        {
            Assert.IsTrue(Gate3ServerConnectionId.TryFromServerObservedTransportId(id, out var connection));
            return connection;
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            var field = target.GetType().GetField("<" + propertyName + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Expected auto-property backing field for " + target.GetType().Name + "." + propertyName + ".");
            field.SetValue(target, value);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Expected private field " + target.GetType().Name + "." + fieldName + ".");
            field.SetValue(target, value);
        }

        private static object PrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Expected private field " + target.GetType().Name + "." + fieldName + ".");
            return field.GetValue(target);
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Expected lifecycle method " + target.GetType().Name + "." + methodName + ".");
            method.Invoke(target, null);
        }

        private readonly struct ProductionFixture
        {
            internal ProductionFixture(
                Gate2ProductionHostDriver driver,
                Gate3IntentCarrierIngress ingress,
                Gate3ServerConnectionActorBindingBridge binding,
                Gate3ActorResolutionComposition actorResolution,
                Gate3ProductionAdmissionComposition admission,
                Gate3ClientIntentDispositionComposition disposition)
            {
                Driver = driver;
                Ingress = ingress;
                Binding = binding;
                ActorResolution = actorResolution;
                Admission = admission;
                Disposition = disposition;
            }

            internal Gate2ProductionHostDriver Driver { get; }
            internal Gate3IntentCarrierIngress Ingress { get; }
            internal Gate3ServerConnectionActorBindingBridge Binding { get; }
            internal Gate3ActorResolutionComposition ActorResolution { get; }
            internal Gate3ProductionAdmissionComposition Admission { get; }
            internal Gate3ClientIntentDispositionComposition Disposition { get; }
        }

        private static Gate3NetworkOrigin Origin(int connectionId, long lifetime)
        {
            Assert.IsTrue(Gate3ServerConnectionId.TryFromServerObservedTransportId(connectionId, out var connection));
            return Gate3NetworkOrigin.From(connection, Gate3ServerConnectionLifetime.From(lifetime));
        }

        private static void AssertDecodeFails(byte[] payload, Gate3ClientIntentResultV1Failure expected)
        {
            Assert.IsFalse(Gate3ClientIntentResultV1Codec.TryDecode(payload, out var result, out var failure));
            Assert.IsNull(result);
            Assert.AreEqual(expected, failure);
        }

        private static void AssertDisposition(Gate3ClientIntentDisposition expected, Gate3ClientIntentDisposition actual)
        {
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.ShiftId, actual.ShiftId);
            Assert.AreEqual(expected.IntentId, actual.IntentId);
            Assert.AreEqual(expected.Kind, actual.Kind);
            Assert.AreEqual(expected.AuthoritativeReceiveTick, actual.AuthoritativeReceiveTick);
            Assert.AreEqual(expected.StateVersion, actual.StateVersion);
            Assert.AreEqual(expected.RejectionCode, actual.RejectionCode);
        }
    }
}
