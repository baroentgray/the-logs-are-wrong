using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using UnityEditor;
using UnityEngine;

namespace TheLogsAreWrong.Gate2.Tests
{
    /// <summary>
    /// Executable TLAW-072 contracts invoke the one production adapter and driver by reflection because this
    /// EditMode assembly intentionally has no reference to Unity's default runtime assembly.
    /// </summary>
    public sealed class Tlaw072LocalIntentAdmissionTests
    {
        private const string ArtifactPath = "Assets/Gate2/Configuration/validated-configuration-c1-v1.base64";
        private const string ManifestPath = "Assets/Gate2/Configuration/validated-configuration-c1-v1.manifest";
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
            ResetLeaseAfterTeardown();
        }

        [Test]
        public void Adapter_binds_exact_envelopes_and_trusted_actors_to_open_tick_zero_with_contiguous_sequences()
        {
            var adapter = CreateAdapter();
            var first = Envelope("intent-a", "untrusted-a");
            var second = Envelope("intent-b", "untrusted-b");

            var firstResult = Submit(adapter, first, ActorId.From("host-a"));
            var secondResult = Submit(adapter, second, ActorId.From("host-b"));
            Assert.IsTrue(AdmissionAccepted(firstResult));
            Assert.IsTrue(AdmissionAccepted(secondResult));

            var input = GetInput(adapter, ServerTick.Zero);
            var batch = Batch(input);
            Assert.AreEqual(2, batch.Intents.Length);
            Assert.AreSame(first, batch.Intents[0].Envelope);
            Assert.AreSame(second, batch.Intents[1].Envelope);
            Assert.AreEqual(ActorId.From("host-a"), batch.Intents[0].AuthoritativeActor);
            Assert.AreEqual(ActorId.From("host-b"), batch.Intents[1].AuthoritativeActor);
            Assert.AreEqual(0L, batch.Intents[0].ReceivedAtTick.Value);
            Assert.AreEqual(0L, batch.Intents[0].ReceiveSequence.Value);
            Assert.AreEqual(1L, batch.Intents[1].ReceiveSequence.Value);
            Assert.IsEmpty((ICollection)Property(input, "ActiveTools"));

            var next = GetInput(adapter, ServerTick.From(1));
            Assert.IsEmpty(Batch(next).Intents);
        }

        [Test]
        public void Duplicate_malformed_and_wrong_shift_submissions_are_local_rejections_that_do_not_consume_sequence()
        {
            var adapter = CreateAdapter();
            var first = Envelope("intent-a", "untrusted-a");
            Assert.IsTrue(AdmissionAccepted(Submit(adapter, first, ActorId.From("host-a"))));

            AssertAdmissionRejected(Submit(adapter, first, ActorId.From("host-a")), "DuplicateIntentId");
            AssertAdmissionRejected(Submit(adapter, Envelope("wrong-shift", "untrusted-b", "OTHER_SHIFT"), ActorId.From("host-b")), "ShiftMismatch");
            AssertAdmissionRejected(Submit(adapter, null, ActorId.From("host-c")), "NullEnvelope");
            AssertAdmissionRejected(Submit(adapter, Envelope("no-actor", "untrusted-d"), default(ActorId)), "AuthoritativeActorUnbound");

            var second = Envelope("intent-b", "untrusted-b");
            Assert.IsTrue(AdmissionAccepted(Submit(adapter, second, ActorId.From("host-b"))));
            var input = GetInput(adapter, ServerTick.Zero);
            var batch = Batch(input);
            Assert.AreEqual(2, batch.Intents.Length);
            Assert.AreEqual(0L, batch.Intents[0].ReceiveSequence.Value);
            Assert.AreEqual(1L, batch.Intents[1].ReceiveSequence.Value);
        }

        [Test]
        public void Exact_tick_boundary_rejects_mismatched_materialization_without_advancing_the_open_window()
        {
            var adapter = CreateAdapter();
            Assert.IsTrue(AdmissionAccepted(Submit(adapter, Envelope("intent-a", "untrusted-a"), ActorId.From("host-a"))));

            var mismatch = Assert.Throws<TargetInvocationException>(() => GetInput(adapter, ServerTick.From(1)));
            Assert.IsInstanceOf<ArgumentException>(mismatch.InnerException);

            var input = GetInput(adapter, ServerTick.Zero);
            Assert.AreEqual(1, Batch(input).Intents.Length);
            Assert.AreEqual(0L, Batch(input).CurrentTick.Value);
        }

        [Test]
        public void Real_driver_delivers_local_admission_to_hostsession_stage_two_and_stage_seven_then_retires_the_due_tick()
        {
            var driver = CreateProductionAdmissionDriver(new long[] { 1000 });
            Start(driver);
            var envelope = Envelope("driver-intent", "untrusted-client");
            var accepted = SubmitDriver(driver, envelope, ActorId.From("trusted-local-actor"));
            Assert.IsTrue(AdmissionAccepted(accepted));

            Pump(driver);

            Assert.AreEqual("Running", Property(driver, "Lifecycle").ToString());
            Assert.AreEqual(1, Property<int>(driver, "ExecutedTickCount"));
            Assert.AreEqual(1, Property<int>(driver, "DeliveredAlreadyAdmittedInputCount"));
            Assert.AreEqual(0L, Property<long>(driver, "PendingDueTickCount"));
            Assert.AreEqual("HostStageSevenPublished", Property<string>(driver, "LastSuccessfulTickResultType"));

            var session = (HostSession)PrivateField(driver, "_session");
            Assert.AreEqual(1, session.SuccessfulTickCount);
            Assert.Greater(session.Journal.Count, 0, "The real Stage Seven must have published the stage output.");
        }

        [Test]
        public void Long_backlog_does_not_clone_tick_zero_admission_into_later_catchup_ticks()
        {
            var driver = CreateProductionAdmissionDriver(new long[] { 3000 });
            Start(driver);
            Assert.IsTrue(AdmissionAccepted(SubmitDriver(driver, Envelope("backlog-intent", "untrusted"), ActorId.From("trusted"))));

            Pump(driver);

            Assert.AreEqual("Running", Property(driver, "Lifecycle").ToString());
            Assert.AreEqual(3, Property<int>(driver, "ExecutedTickCount"));
            Assert.AreEqual(3, Property<int>(driver, "DeliveredAlreadyAdmittedInputCount"));
            Assert.AreEqual(0L, Property<long>(driver, "PendingDueTickCount"));

            var session = (HostSession)PrivateField(driver, "_session");
            Assert.AreEqual(3, session.SuccessfulTickCount);
            var afterBacklog = SubmitDriver(driver, Envelope("after-backlog", "untrusted"), ActorId.From("trusted"));
            Assert.IsTrue(AdmissionAccepted(afterBacklog));
            var receipt = (AuthoritativeAcceptedIntent)Property(afterBacklog, "AcceptedIntent");
            Assert.AreEqual(3L, receipt.ReceivedAtTick.Value,
                "Every due tick must obtain a fresh adapter input; a pending tick-zero batch cannot be cloned into catch-up ticks.");
            Assert.AreEqual(0L, receipt.ReceiveSequence.Value);
        }

        [Test]
        public void Reset_creates_a_fresh_tick_zero_admission_window_and_non_running_owner_rejects_ingress()
        {
            var driver = CreateProductionAdmissionDriver(Array.Empty<long>());
            Start(driver);
            Assert.IsTrue(AdmissionAccepted(SubmitDriver(driver, Envelope("before-reset", "untrusted"), ActorId.From("trusted"))));
            Invoke(driver, "ResetForTesting");

            var afterReset = SubmitDriver(driver, Envelope("after-reset", "untrusted"), ActorId.From("trusted"));
            Assert.IsTrue(AdmissionAccepted(afterReset));
            var receipt = (AuthoritativeAcceptedIntent)Property(afterReset, "AcceptedIntent");
            Assert.AreEqual(0L, receipt.ReceivedAtTick.Value);
            Assert.AreEqual(0L, receipt.ReceiveSequence.Value);

            Invoke(driver, "DisposeForTesting");
            AssertAdmissionRejected(SubmitDriver(driver, Envelope("after-dispose", "untrusted"), ActorId.From("trusted")), "OwnerNotRunning");
        }

        private static object CreateAdapter()
        {
            return Activator.CreateInstance(AdapterType, ShiftId.From("P0_SHIFT_A"));
        }

        private static object GetInput(object adapter, ServerTick tick)
        {
            var method = AdapterType.GetMethod("GetInput", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method);
            return method.Invoke(adapter, new object[] { ShiftId.From("P0_SHIFT_A"), tick });
        }

        private static AcceptedIntentTickBatch Batch(object input) => (AcceptedIntentTickBatch)Property(input, "AcceptedIntents");

        private static object Submit(object adapter, IntentEnvelope envelope, ActorId actor)
        {
            var method = AdapterType.GetMethod("SubmitLocalIntent", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method);
            return method.Invoke(adapter, new object[] { envelope, actor });
        }

        private Component CreateProductionAdmissionDriver(long[] elapsed)
        {
            var root = new GameObject("TLAW072_ProductionAdmissionOwner");
            root.SetActive(false);
            _roots.Add(root);
            var driver = root.AddComponent(DriverType);
            Invoke(driver, "ConfigureProductionLocalAdmissionForTesting", Artifact(), Manifest(), elapsed, "learning");
            root.SetActive(true);
            return driver;
        }

        private static object SubmitDriver(Component driver, IntentEnvelope envelope, ActorId actor) =>
            Invoke(driver, "SubmitLocalIntent", envelope, actor);

        private static IntentEnvelope Envelope(string intentId, string actorHint, string shiftId = "P0_SHIFT_A") =>
            new IntentEnvelope(
                ShiftId.From(shiftId),
                IntentId.From(intentId),
                ActorId.From(actorHint),
                TargetId.From("log_01"),
                LogIntentActions.RouteToProcedure,
                StateVersion.Zero,
                ServerTick.Zero,
                NoIntentParameters.Instance);

        private static bool AdmissionAccepted(object result) => (bool)Property(result, "Accepted");

        private static void AssertAdmissionRejected(object result, string rejection)
        {
            Assert.IsFalse(AdmissionAccepted(result));
            Assert.AreEqual(rejection, Property(result, "Rejection").ToString());
            Assert.IsNull(Property(result, "AcceptedIntent"));
        }

        private static UnityEngine.Object Artifact() => AssetDatabase.LoadMainAssetAtPath(ArtifactPath);

        private static UnityEngine.Object Manifest() => AssetDatabase.LoadMainAssetAtPath(ManifestPath);

        private static Type AdapterType => DriverType.Assembly.GetType("TheLogsAreWrong.Gate2.Gate2LocalIntentAdmissionAdapter")
            ?? throw new TypeLoadException("The production Gate-2 local admission adapter did not compile.");

        private static Type DriverType => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("TheLogsAreWrong.Gate2.Gate2ProductionHostDriver", false))
            .FirstOrDefault(type => type != null)
            ?? throw new TypeLoadException("The production Gate-2 host driver did not compile.");

        private static void Start(Component driver) => Invoke(driver, "StartForTesting");

        private static void Pump(Component driver) => Invoke(driver, "PumpForTesting");

        private static object Invoke(Component driver, string name, params object[] arguments)
        {
            var methods = DriverType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length)
                .ToArray();
            Assert.AreEqual(1, methods.Length, "Required production driver method is missing or ambiguous: " + name);
            return methods[0].Invoke(driver, arguments);
        }

        private static object Property(object target, string name)
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, "Required property is missing: " + name);
            return property.GetValue(target, null);
        }

        private static T Property<T>(object target, string name) => (T)Property(target, name);

        private static object PrivateField(Component driver, string name)
        {
            var field = DriverType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Required production driver field is missing: " + name);
            return field.GetValue(driver);
        }

        private static void ResetLeaseAfterTeardown()
        {
            var reset = DriverType.GetMethod("ResetProcessLeaseAtSubsystemRegistration", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(reset, "The Unity domain-reload lease reset hook is required.");
            reset.Invoke(null, null);
        }
    }
}
