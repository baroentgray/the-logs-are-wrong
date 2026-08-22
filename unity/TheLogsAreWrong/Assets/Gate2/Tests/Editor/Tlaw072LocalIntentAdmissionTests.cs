using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

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
        public void GetInput_wrong_shift_fails_closed_without_clearing_or_advancing_the_valid_open_window()
        {
            var adapter = CreateAdapter();
            var envelope = Envelope("wrong-shift-input", "untrusted");
            Assert.IsTrue(AdmissionAccepted(Submit(adapter, envelope, ActorId.From("trusted"))));

            var mismatch = Assert.Throws<TargetInvocationException>(() => GetInput(adapter, ShiftId.From("OTHER_SHIFT"), ServerTick.Zero));
            Assert.IsInstanceOf<ArgumentException>(mismatch.InnerException);
            Assert.AreEqual(0L, ((ServerTick)Property(adapter, "OpenAdmissionTick")).Value);

            var valid = GetInput(adapter, ServerTick.Zero);
            Assert.AreSame(envelope, Batch(valid).Intents.Single().Envelope);
            Assert.AreEqual(0L, Batch(valid).CurrentTick.Value);
            Assert.AreEqual(1L, ((ServerTick)Property(adapter, "OpenAdmissionTick")).Value);
        }

        [Test]
        public void Materialized_tick_cannot_reopen_and_skipped_or_future_ticks_remain_fail_closed()
        {
            var adapter = CreateAdapter();
            Assert.IsTrue(AdmissionAccepted(Submit(adapter, Envelope("materialized", "untrusted"), ActorId.From("trusted"))));
            Assert.AreEqual(0L, Batch(GetInput(adapter, ServerTick.Zero)).CurrentTick.Value);

            var reopened = Assert.Throws<TargetInvocationException>(() => GetInput(adapter, ServerTick.Zero));
            Assert.IsInstanceOf<ArgumentException>(reopened.InnerException);
            var skipped = Assert.Throws<TargetInvocationException>(() => GetInput(adapter, ServerTick.From(2)));
            Assert.IsInstanceOf<ArgumentException>(skipped.InnerException);
            Assert.AreEqual(1L, ((ServerTick)Property(adapter, "OpenAdmissionTick")).Value);

            var tickOne = GetInput(adapter, ServerTick.From(1));
            Assert.IsEmpty(Batch(tickOne).Intents);
            Assert.AreEqual(2L, ((ServerTick)Property(adapter, "OpenAdmissionTick")).Value);
        }

        [Test]
        public void Checked_tick_exhaustion_fails_closed_without_clearing_or_wrapping_the_open_window()
        {
            var tickExhaustion = CreateAdapter();
            Assert.IsTrue(AdmissionAccepted(Submit(tickExhaustion, Envelope("pending-at-tick-limit", "untrusted"), ActorId.From("trusted"))));
            SetPrivateField(tickExhaustion, "_openAdmissionTick", ServerTick.From(long.MaxValue));

            Assert.Throws<OverflowException>(() => GetInput(tickExhaustion, ServerTick.From(long.MaxValue)));
            Assert.AreEqual(long.MaxValue, ((ServerTick)Property(tickExhaustion, "OpenAdmissionTick")).Value);
            Assert.AreNotEqual(long.MinValue, ((ServerTick)Property(tickExhaustion, "OpenAdmissionTick")).Value);
            Assert.AreEqual(1, ((ICollection)PrivateField(tickExhaustion, "_accepted")).Count);
        }

        [Test]
        public void Receive_sequence_exhaustion_assigns_the_terminal_value_once_without_wrapping_or_duplicates()
        {
            var sequenceExhaustion = CreateAdapter();
            SetPrivateField(sequenceExhaustion, "_nextReceiveSequence", TheLogsAreWrong.Domain.Sequencing.ServerReceiveSequence.From(long.MaxValue));
            var terminal = Submit(sequenceExhaustion, Envelope("terminal-sequence", "untrusted"), ActorId.From("trusted"));
            Assert.IsTrue(AdmissionAccepted(terminal));
            Assert.AreEqual(long.MaxValue, ((AuthoritativeAcceptedIntent)Property(terminal, "AcceptedIntent")).ReceiveSequence.Value);
            AssertAdmissionRejected(Submit(sequenceExhaustion, Envelope("wrapped-sequence", "untrusted"), ActorId.From("trusted")), "ReceiveSequenceExhausted");
            Assert.AreEqual(1, ((ICollection)PrivateField(sequenceExhaustion, "_accepted")).Count);
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
        public void Real_driver_admits_gameplay_invalid_envelopes_and_the_real_stage_two_classifies_them()
        {
            var driver = CreateProductionAdmissionDriver(new long[] { 1000 });
            Start(driver);
            var stale = Envelope("stale-state", "untrusted", expectedStateVersion: StateVersion.From(1));
            var missingTarget = Envelope("missing-target", "untrusted", targetId: TargetId.From("missing_log"));
            var unsupported = Envelope("unsupported-action", "untrusted", action: IntentActionId.From("unsupported_action"));

            Assert.IsTrue(AdmissionAccepted(SubmitDriver(driver, stale, ActorId.From("trusted"))));
            Assert.IsTrue(AdmissionAccepted(SubmitDriver(driver, missingTarget, ActorId.From("trusted"))));
            Assert.IsTrue(AdmissionAccepted(SubmitDriver(driver, unsupported, ActorId.From("trusted"))));
            Pump(driver);

            Assert.AreEqual("Running", Property(driver, "Lifecycle").ToString());
            Assert.AreEqual(1, Property<int>(driver, "DeliveredAlreadyAdmittedInputCount"));
            Assert.AreEqual(1, Property<int>(driver, "ExecutedTickCount"));
            Assert.AreEqual(0L, Property<long>(driver, "PendingDueTickCount"));

            var execution = (HostStageSevenEventExecution)Property(driver, "LastSuccessfulTickResultForTesting");
            Assert.AreEqual(3, execution.StageTwo.Steps.Length);
            Assert.AreSame(stale, execution.StageTwo.Steps[0].Receipt.Envelope);
            Assert.AreSame(missingTarget, execution.StageTwo.Steps[1].Receipt.Envelope);
            Assert.AreSame(unsupported, execution.StageTwo.Steps[2].Receipt.Envelope);
            Assert.AreSame(execution.StageTwo.InitialState, execution.StageTwo.FinalState);

            Assert.IsInstanceOf<ManualRoutingIntentStageOutcome>(execution.StageTwo.Steps[0].Outcome);
            var staleOutcome = (ManualRoutingIntentStageOutcome)execution.StageTwo.Steps[0].Outcome;
            Assert.IsInstanceOf<ManualLogIntentRejected>(staleOutcome.Result);
            Assert.AreEqual(RejectionReason.STALE_STATE_VERSION, ((ManualLogIntentRejected)staleOutcome.Result).Reason);
            Assert.IsInstanceOf<ManualRoutingIntentStageOutcome>(execution.StageTwo.Steps[1].Outcome);
            var missingOutcome = (ManualRoutingIntentStageOutcome)execution.StageTwo.Steps[1].Outcome;
            Assert.IsInstanceOf<ManualLogIntentRejected>(missingOutcome.Result);
            Assert.AreEqual(RejectionReason.TARGET_NOT_FOUND, ((ManualLogIntentRejected)missingOutcome.Result).Reason);
            Assert.IsInstanceOf<UnsupportedIntentStageOutcome>(execution.StageTwo.Steps[2].Outcome);
            Assert.AreEqual(IntentActionId.From("unsupported_action"), ((UnsupportedIntentStageOutcome)execution.StageTwo.Steps[2].Outcome).Action);

            Assert.IsInstanceOf<HostStageSevenPublished>(execution);
            var published = (HostStageSevenPublished)execution;
            CollectionAssert.AreEquivalent(
                new[] { RejectionReason.STALE_STATE_VERSION, RejectionReason.TARGET_NOT_FOUND },
                published.Rejections.Select(rejection => rejection.Reason).ToArray());
        }

        [Test]
        public void Running_owner_local_rejection_is_isolated_and_the_next_valid_tick_executes_and_retires()
        {
            var driver = CreateProductionAdmissionDriver(new long[] { 1000 });
            Start(driver);

            AssertAdmissionRejected(SubmitDriver(driver, Envelope("wrong-shift-owner", "untrusted", shiftId: "OTHER_SHIFT"), ActorId.From("trusted")), "ShiftMismatch");
            Assert.AreEqual("Running", Property(driver, "Lifecycle").ToString());
            Assert.IsNull(Property(driver, "Fault"));
            Assert.AreEqual(0, Property<int>(driver, "DeliveredAlreadyAdmittedInputCount"));

            Pump(driver);

            Assert.AreEqual("Running", Property(driver, "Lifecycle").ToString());
            Assert.IsNull(Property(driver, "Fault"));
            Assert.AreEqual(1, Property<int>(driver, "DeliveredAlreadyAdmittedInputCount"));
            Assert.AreEqual(1, Property<int>(driver, "ExecutedTickCount"));
            Assert.AreEqual(0L, Property<long>(driver, "PendingDueTickCount"));
            Assert.IsEmpty(((HostStageSevenEventExecution)Property(driver, "LastSuccessfulTickResultForTesting")).StageTwo.Batch.Intents);
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

        [Test]
        public void Faulted_owner_disposes_its_retained_adapter_and_pending_evidence_cannot_escape()
        {
            var driver = CreateProductionAdmissionDriver(new long[] { 1000 });
            Start(driver);
            var staleAdapter = PrivateField(driver, "_localIntentAdmission");
            Assert.IsTrue(AdmissionAccepted(SubmitDriver(driver, Envelope("pending-before-fault", "untrusted"), ActorId.From("trusted"))));
            SetPrivateField(staleAdapter, "_openAdmissionTick", ServerTick.From(1));

            ExpectOwnerError("TLAW071_OWNER_FAULT");
            Pump(driver);

            Assert.AreEqual("Faulted", Property(driver, "Lifecycle").ToString());
            Assert.IsNotNull(Property(driver, "Fault"));
            Assert.AreEqual(0, Property<int>(driver, "ExecutedTickCount"));
            Assert.AreEqual(1L, Property<long>(driver, "PendingDueTickCount"));
            AssertAdmissionRejected(SubmitDriver(driver, Envelope("after-fault", "untrusted"), ActorId.From("trusted")), "OwnerNotRunning");
            AssertAdmissionRejected(Submit(staleAdapter, Envelope("stale-adapter", "untrusted"), ActorId.From("trusted")), "AdapterDisposed");
            Assert.AreEqual(0, ((ICollection)PrivateField(staleAdapter, "_accepted")).Count);

            var staleRead = Assert.Throws<TargetInvocationException>(() => GetInput(staleAdapter, ServerTick.From(1)));
            Assert.IsInstanceOf<ObjectDisposedException>(staleRead.InnerException);
            Pump(driver);
            Assert.AreEqual(0, Property<int>(driver, "ExecutedTickCount"));
            AssertAdmissionRejected(SubmitDriver(driver, Envelope("after-fault-pump", "untrusted"), ActorId.From("trusted")), "OwnerNotRunning");
        }

        private static object CreateAdapter()
        {
            return Activator.CreateInstance(AdapterType, ShiftId.From("P0_SHIFT_A"));
        }

        private static object GetInput(object adapter, ServerTick tick)
            => GetInput(adapter, ShiftId.From("P0_SHIFT_A"), tick);

        private static object GetInput(object adapter, ShiftId shiftId, ServerTick tick)
        {
            var method = AdapterType.GetMethod("GetInput", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method);
            return method.Invoke(adapter, new object[] { shiftId, tick });
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

        private static IntentEnvelope Envelope(
            string intentId,
            string actorHint,
            string shiftId = "P0_SHIFT_A",
            TargetId? targetId = null,
            IntentActionId? action = null,
            StateVersion? expectedStateVersion = null) =>
            new IntentEnvelope(
                ShiftId.From(shiftId),
                IntentId.From(intentId),
                ActorId.From(actorHint),
                targetId ?? TargetId.From("log_01"),
                action ?? LogIntentActions.RouteToProcedure,
                expectedStateVersion ?? StateVersion.Zero,
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

        private static object PrivateField(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Required private field is missing: " + name);
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Required private field is missing: " + name);
            field.SetValue(target, value);
        }

        private static void ExpectOwnerError(string marker)
        {
            LogAssert.Expect(LogType.Error, new Regex(marker, RegexOptions.CultureInvariant));
        }

        private static void ResetLeaseAfterTeardown()
        {
            var reset = DriverType.GetMethod("ResetProcessLeaseAtSubsystemRegistration", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(reset, "The Unity domain-reload lease reset hook is required.");
            reset.Invoke(null, null);
        }
    }
}
