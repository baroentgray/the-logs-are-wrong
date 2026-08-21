using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TheLogsAreWrong.Gate2.Tests
{
    /// <summary>
    /// Executable U3/U2/U4 contracts for the one real Gate-2 production owner. The existing test assembly cannot
    /// reference Unity's default runtime assembly directly, so it invokes the component's value-only test seam by
    /// reflection; no duplicate host, codec, cadence, or input executor is introduced here.
    /// </summary>
    public sealed class Tlaw071ProductionHostOwnerDriverTests
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
        public void First_owner_materializes_the_exact_tracked_C1_configuration_and_creates_one_session()
        {
            var driver = CreateDriver();
            Start(driver);

            Assert.AreEqual("Running", Lifecycle(driver));
            Assert.AreEqual(1, Property<int>(driver, "SessionCreationCount"));
            Assert.AreEqual("learning", Property<string>(driver, "SelectedProfileId"));
            Assert.AreEqual("P0_SHIFT_A", Property<string>(driver, "RunningShiftId"));
            Assert.IsNull(Property(driver, "Fault"));
        }

        [Test]
        public void Concurrent_second_owner_fails_before_creating_a_session_and_cannot_corrupt_the_first()
        {
            var first = CreateDriver(new long[] { 1000 });
            var second = CreateDriver();
            Start(first);
            ExpectOwnerError("TLAW071_OWNER_START_FAIL");
            Start(second);

            Assert.AreEqual("Running", Lifecycle(first));
            Assert.AreEqual("Faulted", Lifecycle(second));
            Assert.AreEqual(1, Property<int>(first, "SessionCreationCount"));
            Assert.AreEqual(0, Property<int>(second, "SessionCreationCount"));

            Pump(first);
            Assert.AreEqual("Running", Lifecycle(first));
            Assert.AreEqual(1, Property<int>(first, "ExecutedTickCount"));
        }

        [Test]
        public void Teardown_releases_the_lease_and_a_new_owner_can_acquire_cleanly()
        {
            var first = CreateDriver();
            Start(first);
            Invoke(first, "DisposeForTesting");
            var firstRoot = first.gameObject;
            UnityEngine.Object.DestroyImmediate(firstRoot);
            _roots.Remove(firstRoot);

            var replacement = CreateDriver();
            Start(replacement);

            Assert.AreEqual("Running", Lifecycle(replacement));
            Assert.AreEqual(1, Property<int>(replacement, "SessionCreationCount"));
        }

        [Test]
        public void Failed_startup_releases_its_lease_and_leaves_no_live_session()
        {
            var failed = CreateDriver(Array.Empty<long>(), -1, "does-not-exist");
            ExpectOwnerError("TLAW071_OWNER_START_FAIL");
            Start(failed);

            Assert.AreEqual("Faulted", Lifecycle(failed));
            Assert.AreEqual(0, Property<int>(failed, "SessionCreationCount"));
            Assert.IsNotNull(Property(failed, "Fault"));

            var succeeding = CreateDriver();
            Start(succeeding);
            Assert.AreEqual("Running", Lifecycle(succeeding));
        }

        [Test]
        public void Reset_disposes_the_old_session_before_constructing_a_tick_zero_replacement()
        {
            var driver = CreateDriver(new long[] { 1000, 1000 });
            Start(driver);
            Pump(driver);
            Invoke(driver, "ResetForTesting");
            Pump(driver);

            Assert.AreEqual("Running", Lifecycle(driver));
            Assert.AreEqual(2, Property<int>(driver, "SessionCreationCount"));
            Assert.AreEqual(2, Property<int>(driver, "ExecutedTickCount"));
        }

        [Test]
        public void Disposed_or_faulted_owner_cannot_continue_ticking()
        {
            var disposed = CreateDriver(new long[] { 1000 });
            Start(disposed);
            Invoke(disposed, "DisposeForTesting");
            Pump(disposed);
            Assert.AreEqual("Disposed", Lifecycle(disposed));
            Assert.AreEqual(0, Property<int>(disposed, "ExecutedTickCount"));

            var faulted = CreateDriver(new long[] { 1000 }, 0);
            Start(faulted);
            ExpectOwnerError("TLAW071_OWNER_FAULT");
            Pump(faulted);
            Pump(faulted);
            Assert.AreEqual("Faulted", Lifecycle(faulted));
            Assert.AreEqual(0, Property<int>(faulted, "ExecutedTickCount"));
            Assert.AreEqual(1L, Property<long>(faulted, "PendingDueTickCount"));
        }

        [Test]
        public void Subsystem_registration_reset_is_safe_after_teardown()
        {
            var first = CreateDriver();
            Start(first);
            Invoke(first, "DisposeForTesting");
            ResetLeaseAfterTeardown();

            var replacement = CreateDriver();
            Start(replacement);
            Assert.AreEqual("Running", Lifecycle(replacement));
        }

        [Test]
        public void The_999_then_1_millisecond_boundary_makes_tick_zero_due_once()
        {
            var driver = CreateDriver(new long[] { 999, 1 });
            Start(driver);
            Pump(driver);
            Assert.AreEqual(0, Property<int>(driver, "ExecutedTickCount"));
            Assert.AreEqual(0L, Property<long>(driver, "PendingDueTickCount"));

            Pump(driver);
            Assert.AreEqual(1, Property<int>(driver, "ExecutedTickCount"));
        }

        [Test]
        public void Equivalent_frame_partitions_execute_the_same_consecutive_tick_sequence()
        {
            var coarse = CreateDriver(new long[] { 4000 });
            Start(coarse);
            Pump(coarse);
            Invoke(coarse, "DisposeForTesting");

            var fine = CreateDriver(new long[] { 400, 600, 1000, 1000, 1000 });
            Start(fine);
            for (var frame = 0; frame < 5; frame++) Pump(fine);

            Assert.AreEqual(4, Property<int>(coarse, "ExecutedTickCount"));
            Assert.AreEqual(Property<int>(coarse, "ExecutedTickCount"), Property<int>(fine, "ExecutedTickCount"));
            Assert.AreNotEqual(string.Empty, Property<string>(fine, "LastSuccessfulTickResultType"));
        }

        [Test]
        public void Monotonic_clock_conversion_retains_submillisecond_remainder_across_samples()
        {
            var converted = (long[])InvokeStatic("ConvertTimestampSamplesForTesting", 10_000L, Enumerable.Range(0, 11).Select(value => (long)value).ToArray());
            Assert.AreEqual(1L, converted.Sum());
            CollectionAssert.AreEqual(new long[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }, converted);
        }

        [Test]
        public void Long_stall_drains_the_full_backlog_without_an_arbitrary_catchup_cap()
        {
            var driver = CreateDriver(new long[] { 12_000 });
            Start(driver);
            Pump(driver);

            Assert.AreEqual(12, Property<int>(driver, "ExecutedTickCount"));
            Assert.AreEqual(0L, Property<long>(driver, "PendingDueTickCount"));
        }

        [Test]
        public void Failed_input_leaves_its_due_tick_unretired_after_prior_success_and_faults_the_driver()
        {
            var driver = CreateDriver(new long[] { 2000 }, 1);
            Start(driver);
            ExpectOwnerError("TLAW071_OWNER_FAULT");
            Pump(driver);

            Assert.AreEqual("Faulted", Lifecycle(driver));
            Assert.AreEqual(1, Property<int>(driver, "ExecutedTickCount"));
            Assert.AreEqual(1L, Property<long>(driver, "PendingDueTickCount"));
        }

        [Test]
        public void Empty_already_admitted_input_accepts_a_zero_publication_tick_without_a_driver_fault()
        {
            var driver = CreateDriver(new long[] { 1000, 1000 });
            Start(driver);
            Pump(driver);

            Assert.AreEqual(1, Property<int>(driver, "ExecutedTickCount"));
            Assert.AreEqual("HostStageSevenPublished", Property<string>(driver, "LastSuccessfulTickResultType"));

            var successfulTicksBeforeNoPublication = Property<int>(driver, "ExecutedTickCount");
            Pump(driver);

            Assert.AreEqual("Running", Lifecycle(driver));
            Assert.AreEqual(successfulTicksBeforeNoPublication + 1, Property<int>(driver, "ExecutedTickCount"));
            Assert.AreEqual(0L, Property<long>(driver, "PendingDueTickCount"));
            Assert.AreEqual("HostStageSevenNoNewPublication", Property<string>(driver, "LastSuccessfulTickResultType"));
        }

        [Test]
        public void HostSession_rejection_after_delivered_invalid_continuity_evidence_faults_and_retains_the_due_tick()
        {
            var driver = CreateDriver(new long[] { 1000 }, invalidContinuityInputOnRequest: 0);
            Start(driver);
            ExpectOwnerError("TLAW071_OWNER_FAULT");
            Pump(driver);

            var fault = (Exception)Property(driver, "Fault");
            Assert.AreEqual("Faulted", Lifecycle(driver));
            Assert.AreEqual(1, Property<int>(driver, "SessionCreationCount"));
            Assert.AreEqual(1, Property<int>(driver, "DeliveredAlreadyAdmittedInputCount"));
            Assert.AreEqual(0, Property<int>(driver, "ExecutedTickCount"));
            Assert.AreEqual(1L, Property<long>(driver, "PendingDueTickCount"));
            Assert.IsInstanceOf<ArgumentException>(fault);
            Assert.AreEqual("acceptedIntents", ((ArgumentException)fault).ParamName);
            StringAssert.Contains("Per-tick input must belong to the current session shift and exact requested tick.", fault.Message);
        }

        [Test]
        public void Tampered_missing_or_unknown_profile_startup_input_prevents_session_creation()
        {
            var manifest = Manifest();
            var sourceArtifact = Artifact();
            var sourceText = DeploymentText(sourceArtifact);
            var tampered = CreateDeploymentText(sourceText.Substring(0, sourceText.Length - 8) + "AAAAAAAA");
            var tamperedOwner = CreateDriver(Array.Empty<long>(), -1, "learning", tampered, manifest);
            ExpectOwnerError("TLAW071_OWNER_START_FAIL");
            Start(tamperedOwner);
            Assert.AreEqual("Faulted", Lifecycle(tamperedOwner));
            Assert.AreEqual(0, Property<int>(tamperedOwner, "SessionCreationCount"));

            var missingOwner = CreateDriver(Array.Empty<long>(), -1, "learning", CreateDeploymentText(string.Empty), manifest);
            ExpectOwnerError("TLAW071_OWNER_START_FAIL");
            Start(missingOwner);
            Assert.AreEqual("Faulted", Lifecycle(missingOwner));
            Assert.AreEqual(0, Property<int>(missingOwner, "SessionCreationCount"));

            var profileOwner = CreateDriver(Array.Empty<long>(), -1, "does-not-exist", Artifact(), manifest);
            ExpectOwnerError("TLAW071_OWNER_START_FAIL");
            Start(profileOwner);
            Assert.AreEqual("Faulted", Lifecycle(profileOwner));
            Assert.AreEqual(0, Property<int>(profileOwner, "SessionCreationCount"));
        }

        [Test]
        public void Owner_and_lease_static_fields_cannot_hold_authoritative_runtime_state()
        {
            var forbidden = new[] { "HostSession", "HostTickCadence", "ValidatedConfiguration", "ShiftRuntimeState", "QuotaRuntimeState", "IAtomicEventJournal" };
            var ownerFields = DriverType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsFalse(ownerFields.Any(field => forbidden.Contains(field.FieldType.Name)), "The component must not make session/config/runtime state static.");

            var leaseType = DriverType.Assembly.GetType("TheLogsAreWrong.Gate2.Gate2ProductionHostLease");
            Assert.IsNotNull(leaseType);
            var leaseFields = leaseType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsFalse(leaseFields.Any(field => forbidden.Contains(field.FieldType.Name)), "The process lease may retain identity only.");
        }

        private Component CreateDriver(long[] elapsedMilliseconds = null, int failInputOnRequest = -1, string profileId = "learning", UnityEngine.Object artifact = null, UnityEngine.Object manifest = null, int invalidContinuityInputOnRequest = -1)
        {
            var root = new GameObject("TLAW071_TestOwner");
            root.SetActive(false);
            _roots.Add(root);
            var driver = root.AddComponent(DriverType);
            Invoke(driver, "ConfigureForTesting", artifact ?? Artifact(), manifest ?? Manifest(), elapsedMilliseconds ?? Array.Empty<long>(), failInputOnRequest, profileId, invalidContinuityInputOnRequest);
            root.SetActive(true);
            return driver;
        }

        private static UnityEngine.Object Artifact() => AssetDatabase.LoadMainAssetAtPath(ArtifactPath);

        private static UnityEngine.Object Manifest() => AssetDatabase.LoadMainAssetAtPath(ManifestPath);

        private static string DeploymentText(UnityEngine.Object asset)
        {
            Assert.IsNotNull(asset);
            var property = asset.GetType().GetProperty("Text", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property);
            return (string)property.GetValue(asset, null);
        }

        private static UnityEngine.Object CreateDeploymentText(string text)
        {
            var type = DriverType.Assembly.GetType("TheLogsAreWrong.Gate2.Gate2DeploymentTextAsset");
            Assert.IsNotNull(type);
            var asset = ScriptableObject.CreateInstance(type);
            var setText = type.GetMethod("SetImportedText", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(setText);
            setText.Invoke(asset, new object[] { text });
            return asset;
        }

        private static Type DriverType => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("TheLogsAreWrong.Gate2.Gate2ProductionHostDriver", false))
            .FirstOrDefault(type => type != null)
            ?? throw new TypeLoadException("The production Gate2 host driver did not compile.");

        private static void Start(Component driver) => Invoke(driver, "StartForTesting");

        private static void Pump(Component driver) => Invoke(driver, "PumpForTesting");

        private static object Invoke(Component driver, string name, params object[] arguments)
        {
            var methods = DriverType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length)
                .ToArray();
            Assert.AreEqual(1, methods.Length, "Required production driver method is missing or ambiguous: " + name);
            var method = methods[0];
            return method.Invoke(driver, arguments);
        }

        private static object InvokeStatic(string name, params object[] arguments)
        {
            var method = DriverType.GetMethod(name, BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(method, "Required production driver static method is missing: " + name);
            return method.Invoke(null, arguments);
        }

        private static object Property(Component driver, string name)
        {
            var property = DriverType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, "Required production driver property is missing: " + name);
            return property.GetValue(driver, null);
        }

        private static T Property<T>(Component driver, string name) => (T)Property(driver, name);

        private static string Lifecycle(Component driver) => Property(driver, "Lifecycle").ToString();

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
