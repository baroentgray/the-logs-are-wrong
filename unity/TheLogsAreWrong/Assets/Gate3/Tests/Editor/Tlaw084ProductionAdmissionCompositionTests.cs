using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Sequencing;
using TheLogsAreWrong.Gate2;
using UnityEditor;
using UnityEngine;

namespace TheLogsAreWrong.Gate3.Tests
{
    /// <summary>Executable D-025 contracts for one networked-production shared admission/order owner.</summary>
    public sealed class Tlaw084ProductionAdmissionCompositionTests
    {
        private const string ArtifactPath = "Assets/Gate2/Configuration/validated-configuration-c1-v1.base64";
        private const string ManifestPath = "Assets/Gate2/Configuration/validated-configuration-c1-v1.manifest";
        private const string CurrentShift = "P0_SHIFT_A";
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
        public void Local_then_resolved_network_evidence_share_one_zero_based_sequence_domain_before_receipts_exist()
        {
            var fixture = CreateFixture(new long[] { 1000 }, new long[] { 0, 1001 });

            var local = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("local_first"), ActorId.From("trusted_local"));
            RaiseResolved(fixture.ActorResolution, Resolved(22, 0, Envelope("network_second"), "trusted_network"));

            Assert.AreEqual(Gate3NetworkedLocalIntentSubmissionStatus.Admitted, local.Status);
            Assert.AreEqual(ServerReceiveSequence.Zero, local.Admission.AcceptedIntent.ReceiveSequence);
            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.Admitted, fixture.Composition.LastNetworkAdmission.Status);
            Assert.AreEqual(ServerReceiveSequence.From(1), fixture.Composition.LastNetworkAdmission.AcceptedIntent.ReceiveSequence);
            Assert.AreSame(local.Admission.AcceptedIntent.Envelope, local.Admission.AcceptedIntent.Envelope);

            fixture.Driver.PumpForTesting();
            Assert.AreEqual(1, fixture.Driver.ExecutedTickCount);
            Assert.AreEqual(0L, fixture.Driver.PendingDueTickCount);
        }

        [Test]
        public void Resolved_network_then_local_evidence_share_the_same_one_sequence_domain()
        {
            var fixture = CreateFixture(Array.Empty<long>(), new long[] { 0 });

            RaiseResolved(fixture.ActorResolution, Resolved(23, 0, Envelope("network_first"), "network_actor"));
            var local = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("local_second"), ActorId.From("local_actor"));

            Assert.AreEqual(ServerReceiveSequence.Zero, fixture.Composition.LastNetworkAdmission.AcceptedIntent.ReceiveSequence);
            Assert.AreEqual(ServerReceiveSequence.From(1), local.Admission.AcceptedIntent.ReceiveSequence);
        }

        [Test]
        public void Mixed_burst_uses_one_serialized_admission_order_and_preserves_exact_network_actor_and_receive_tick()
        {
            var fixture = CreateFixture(Array.Empty<long>(), new long[] { 0, 0 });
            var localFirst = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("local_first"), ActorId.From("local_actor"));
            RaiseResolved(fixture.ActorResolution, Resolved(31, 0, Envelope("network_middle", "forged_hint"), "resolved_actor"));
            var localLast = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("local_last"), ActorId.From("local_actor"));

            Assert.AreEqual(ServerReceiveSequence.Zero, localFirst.Admission.AcceptedIntent.ReceiveSequence);
            Assert.AreEqual(ServerReceiveSequence.From(1), fixture.Composition.LastNetworkAdmission.AcceptedIntent.ReceiveSequence);
            Assert.AreEqual(ServerReceiveSequence.From(2), localLast.Admission.AcceptedIntent.ReceiveSequence);
            Assert.AreEqual(ActorId.From("resolved_actor"), fixture.Composition.LastNetworkAdmission.AcceptedIntent.AuthoritativeActor);
            Assert.AreEqual(ServerTick.Zero, fixture.Composition.LastNetworkAdmission.AcceptedIntent.ReceivedAtTick);
            Assert.AreEqual("forged_hint", fixture.Composition.LastNetworkAdmission.AcceptedIntent.Envelope.ActorIdHint.Value);
        }

        [Test]
        public void Cross_source_duplicate_is_terminal_for_the_session_and_consumes_no_additional_sequence()
        {
            var fixture = CreateFixture(Array.Empty<long>(), new long[] { 0 });
            var local = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("same"), ActorId.From("local_actor"));
            RaiseResolved(fixture.ActorResolution, Resolved(33, 0, Envelope("same"), "network_actor"));
            var later = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("later"), ActorId.From("local_actor"));

            Assert.AreEqual(Gate3NetworkedLocalIntentSubmissionStatus.Admitted, local.Status);
            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.DuplicateIntentId, fixture.Composition.LastNetworkAdmission.Status);
            Assert.AreEqual(ServerReceiveSequence.From(1), later.Admission.AcceptedIntent.ReceiveSequence);
        }

        [Test]
        public void Exact_next_receive_tick_restarts_sequence_but_session_lifetime_duplicate_disposition_survives()
        {
            var fixture = CreateFixture(Array.Empty<long>(), new long[] { 0, 1001, 1001 });
            var first = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("first"), ActorId.From("local_actor"));
            var nextTick = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("next"), ActorId.From("local_actor"));
            var duplicate = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("first"), ActorId.From("local_actor"));

            Assert.AreEqual(ServerTick.Zero, first.Admission.AcceptedIntent.ReceivedAtTick);
            Assert.AreEqual(ServerReceiveSequence.Zero, first.Admission.AcceptedIntent.ReceiveSequence);
            Assert.AreEqual(ServerTick.From(1), nextTick.Admission.AcceptedIntent.ReceivedAtTick);
            Assert.AreEqual(ServerReceiveSequence.Zero, nextTick.Admission.AcceptedIntent.ReceiveSequence);
            Assert.AreEqual(Gate3NetworkedLocalIntentSubmissionStatus.AdmissionRejected, duplicate.Status);
            Assert.AreEqual(Gate3NetworkIntentAdmissionStatus.DuplicateIntentId, duplicate.Admission.Status);
        }

        [Test]
        public void At_exactly_1000ms_due_tick_zero_stays_open_for_later_same_frame_ingress_then_seals_after_1001ms()
        {
            var fixture = CreateFixture(new long[] { 1000, 0 }, new long[] { 1000, 1000, 1001 });

            fixture.Driver.PumpForTesting();
            Assert.AreEqual("Running", fixture.Driver.Lifecycle.ToString());
            Assert.AreEqual(0, fixture.Driver.ExecutedTickCount);
            Assert.AreEqual(1L, fixture.Driver.PendingDueTickCount);

            var admittedAtInclusiveBoundary = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("at_1000"), ActorId.From("local_actor"));
            Assert.AreEqual(Gate3NetworkedLocalIntentSubmissionStatus.Admitted, admittedAtInclusiveBoundary.Status);
            Assert.AreEqual(ServerTick.Zero, admittedAtInclusiveBoundary.Admission.AcceptedIntent.ReceivedAtTick);

            fixture.Driver.PumpForTesting();
            Assert.AreEqual("Running", fixture.Driver.Lifecycle.ToString());
            Assert.AreEqual(1, fixture.Driver.ExecutedTickCount);
            Assert.AreEqual(0L, fixture.Driver.PendingDueTickCount);
        }

        [Test]
        public void Pump_then_ingress_and_ingress_then_pump_at_the_inclusive_boundary_materialize_the_same_tick_zero_evidence()
        {
            var pumpFirst = RunInclusiveBoundaryPermutation(true);
            var ingressFirst = RunInclusiveBoundaryPermutation(false);

            Assert.AreEqual(ServerTick.Zero, pumpFirst.ReceivedAtTick);
            Assert.AreEqual(ServerTick.Zero, ingressFirst.ReceivedAtTick);
            Assert.AreEqual(ServerReceiveSequence.Zero, pumpFirst.ReceiveSequence);
            Assert.AreEqual(ServerReceiveSequence.Zero, ingressFirst.ReceiveSequence);
        }

        [Test]
        public void Due_backlog_waits_for_receive_window_closure_without_rewriting_a_future_network_tick()
        {
            var fixture = CreateFixture(new long[] { 3000, 1000 }, new long[] { 3001, 3001, 3001, 3001 });
            RaiseResolved(fixture.ActorResolution, Resolved(41, 3, Envelope("future"), "network_actor"));

            fixture.Driver.PumpForTesting();
            fixture.Driver.PumpForTesting();

            Assert.AreEqual(1L, fixture.Driver.PendingDueTickCount, "At receive tick three, tick three remains open after the older due backlog drains.");
            Assert.AreEqual(3, fixture.Driver.ExecutedTickCount);
            Assert.AreEqual(ServerTick.From(3), fixture.Composition.LastNetworkAdmission.AcceptedIntent.ReceivedAtTick);
        }

        [Test]
        public void One_final_input_source_has_no_batch_merge_and_keeps_active_tools_empty()
        {
            var fixture = CreateFixture(Array.Empty<long>(), new long[] { 1001 });
            var inputSource = (IAlreadyAdmittedHostInputSource)PrivateField(fixture.Driver, "_inputSource");

            var input = inputSource.GetInput(ShiftId.From(CurrentShift), ServerTick.Zero);

            Assert.IsEmpty(input.AcceptedIntents.Intents);
            Assert.IsEmpty(input.ActiveTools);
            Assert.Throws<InvalidOperationException>(() => inputSource.GetInput(ShiftId.From(CurrentShift), ServerTick.Zero));
        }

        [Test]
        public void Wrong_shift_materialization_fails_closed_without_clearing_the_valid_shared_tick()
        {
            var fixture = CreateFixture(new long[] { 1000 }, new long[] { 0, 1001 });
            var admitted = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("retained"), ActorId.From("local_actor"));
            var inputSource = (IAlreadyAdmittedHostInputSource)PrivateField(fixture.Driver, "_inputSource");

            Assert.Throws<InvalidOperationException>(() => inputSource.GetInput(ShiftId.From("other_shift"), ServerTick.Zero));
            fixture.Driver.PumpForTesting();

            Assert.AreEqual(Gate3NetworkedLocalIntentSubmissionStatus.Admitted, admitted.Status);
            Assert.AreEqual("Running", fixture.Driver.Lifecycle.ToString());
            Assert.AreEqual(1, fixture.Driver.ExecutedTickCount);
        }

        [Test]
        public void Reset_disposes_retained_old_input_and_stale_prior_session_evidence_cannot_poison_a_fresh_shared_owner()
        {
            var fixture = CreateFixture(Array.Empty<long>(), new long[] { 0, 0 });
            var first = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("reused_after_reset"), ActorId.From("local_actor"));
            var oldInput = (IAlreadyAdmittedHostInputSource)PrivateField(fixture.Driver, "_inputSource");

            fixture.Driver.ResetForTesting();
            var fresh = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("reused_after_reset"), ActorId.From("local_actor"));

            Assert.AreEqual(Gate3NetworkedLocalIntentSubmissionStatus.Admitted, first.Status);
            Assert.Throws<ObjectDisposedException>(() => oldInput.GetInput(ShiftId.From(CurrentShift), ServerTick.Zero));
            Assert.AreEqual(Gate3NetworkedLocalIntentSubmissionStatus.Admitted, fresh.Status);
            Assert.AreEqual(ServerReceiveSequence.Zero, fresh.Admission.AcceptedIntent.ReceiveSequence);
        }

        [Test]
        public void Disposed_or_not_running_owner_refuses_later_local_ingress_without_creating_a_receipt()
        {
            var fixture = CreateFixture(Array.Empty<long>(), new long[] { 0 });
            fixture.Driver.DisposeForTesting();

            var result = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("after_dispose"), ActorId.From("local_actor"));

            Assert.AreEqual(Gate3NetworkedLocalIntentSubmissionStatus.OwnerNotRunning, result.Status);
            Assert.IsFalse(result.HasAcceptedIntent);
        }

        private AuthoritativeAcceptedIntent RunInclusiveBoundaryPermutation(bool pumpFirst)
        {
            var fixture = CreateFixture(new long[] { 1000, 0 }, new long[] { 1000, 1000, 1001 });
            if (pumpFirst)
            {
                fixture.Driver.PumpForTesting();
            }

            var local = fixture.Driver.SubmitNetworkedLocalIntent(Envelope("same_permutation"), ActorId.From("local_actor"));
            if (!pumpFirst)
            {
                fixture.Driver.PumpForTesting();
            }

            fixture.Driver.PumpForTesting();
            Assert.AreEqual(1, fixture.Driver.ExecutedTickCount);
            fixture.Driver.DisposeForTesting();
            return local.Admission.AcceptedIntent;
        }

        private Fixture CreateFixture(long[] samples, long[] observations)
        {
            var root = new GameObject("Tlaw084ProductionAdmission");
            root.SetActive(false);
            _roots.Add(root);
            var driver = root.AddComponent<Gate2ProductionHostDriver>();
            var actorResolution = root.AddComponent<Gate3ActorResolutionComposition>();
            var composition = root.AddComponent<Gate3ProductionAdmissionComposition>();
            SetPrivateField(composition, "_hostDriver", driver);
            SetPrivateField(composition, "_actorResolution", actorResolution);
            InvokeLifecycle(composition, "Awake");
            InvokeLifecycle(composition, "OnEnable");
            driver.ConfigureNetworkedProductionAdmissionForTesting(
                Artifact(),
                Manifest(),
                samples,
                observations,
                "learning");
            driver.StartForTesting();
            Assert.AreEqual(ProductionHostOwnerLifecycle.Running, driver.Lifecycle, driver.Fault?.ToString());
            return new Fixture(driver, actorResolution, composition);
        }

        private static Gate2DeploymentTextAsset Artifact() => AssetDatabase.LoadAssetAtPath<Gate2DeploymentTextAsset>(ArtifactPath);
        private static Gate2DeploymentTextAsset Manifest() => AssetDatabase.LoadAssetAtPath<Gate2DeploymentTextAsset>(ManifestPath);

        private static Gate3ResolvedNetworkIntentEvidence Resolved(int connection, long tick, IntentEnvelope envelope, string actor)
        {
            Assert.IsTrue(Gate3ServerConnectionId.TryFromServerObservedTransportId(connection, out var connectionId));
            var constructor = typeof(Gate3ResolvedNetworkIntentEvidence).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Gate3ServerConnectionId), typeof(ServerTick), typeof(IntentEnvelope), typeof(ActorId) },
                null);
            Assert.IsNotNull(constructor);
            return (Gate3ResolvedNetworkIntentEvidence)constructor.Invoke(new object[] { connectionId, ServerTick.From(tick), envelope, ActorId.From(actor) });
        }

        private static void RaiseResolved(Gate3ActorResolutionComposition composition, Gate3ResolvedNetworkIntentEvidence evidence)
        {
            var field = typeof(Gate3ActorResolutionComposition).GetField("Resolved", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "TLAW-080 must retain the bounded success event backing field.");
            var callback = field.GetValue(composition) as Delegate;
            Assert.IsNotNull(callback, "The D-025 composition must subscribe exactly once to resolved network evidence.");
            callback.DynamicInvoke(evidence);
        }

        private static IntentEnvelope Envelope(string intent, string actorHint = "client_hint")
        {
            return new IntentEnvelope(
                ShiftId.From(CurrentShift),
                IntentId.From(intent),
                ActorId.From(actorHint),
                TargetId.From("target"),
                IntentActionId.From("unsupported_action"),
                StateVersion.Zero,
                ServerTick.Zero,
                NoIntentParameters.Instance);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Expected private field: " + fieldName);
            field.SetValue(target, value);
        }

        private static object PrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Expected private field: " + fieldName);
            return field.GetValue(target);
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Expected lifecycle method: " + methodName);
            method.Invoke(target, null);
        }

        private readonly struct Fixture
        {
            internal Fixture(Gate2ProductionHostDriver driver, Gate3ActorResolutionComposition actorResolution, Gate3ProductionAdmissionComposition composition)
            {
                Driver = driver;
                ActorResolution = actorResolution;
                Composition = composition;
            }

            internal Gate2ProductionHostDriver Driver { get; }
            internal Gate3ActorResolutionComposition ActorResolution { get; }
            internal Gate3ProductionAdmissionComposition Composition { get; }
        }
    }
}
