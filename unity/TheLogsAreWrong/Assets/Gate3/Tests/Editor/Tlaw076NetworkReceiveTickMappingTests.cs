using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TheLogsAreWrong.Gate3.Tests
{
    /// <summary>Executable contracts for the elapsed server-time to exact receive-tick seam.</summary>
    public sealed class Tlaw076NetworkReceiveTickMappingTests
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
        public void Frozen_inclusive_boundary_mapping_is_deterministic_and_cannot_wrap()
        {
            AssertTick(0, 0);
            AssertTick(1, 0);
            AssertTick(999, 0);
            AssertTick(1000, 0);
            AssertTick(1001, 1);
            AssertTick(2000, 1);
            AssertTick(2001, 2);
            AssertTick(5000, 4);
            AssertTick(5000, 4);

            var maximum = Gate3ServerReceiveTickMapper.Map(AuthoritativeElapsedMilliseconds.FromMilliseconds(long.MaxValue));
            Assert.AreEqual((long.MaxValue - 1) / HostTickCadence.MillisecondsPerServerTick, maximum.Value);
            Assert.AreNotEqual(long.MinValue, maximum.Value);
        }

        [Test]
        public void Receive_observation_uses_elapsed_time_not_unretired_or_retired_backlog_and_does_not_mutate_cadence()
        {
            var driver = CreateDriver(Array.Empty<long>(), new long[] { 5000, 5000 });
            Start(driver);
            var cadence = (HostTickCadence)PrivateField(driver, "_cadence");
            cadence.Accumulate(AuthoritativeElapsedMilliseconds.FromMilliseconds(5000));
            var beforeRemainder = cadence.RemainderMilliseconds.Value;
            var beforeDue = cadence.DueTickCount;
            var beforeFirst = cadence.GetDueTickRange().Value.First;

            AssertObservedTick(Observe(driver), 4);
            Assert.AreEqual(beforeRemainder, cadence.RemainderMilliseconds.Value);
            Assert.AreEqual(beforeDue, cadence.DueTickCount);
            Assert.AreEqual(beforeFirst, cadence.GetDueTickRange().Value.First);

            Assert.AreEqual(ServerTick.Zero, cadence.RetireNextDueTick());
            Assert.AreEqual(4L, cadence.DueTickCount);
            Assert.AreEqual(1L, cadence.GetDueTickRange().Value.First.Value);
            AssertObservedTick(Observe(driver), 4);
            Assert.AreEqual("Running", Property(driver, "Lifecycle").ToString());
            Assert.AreEqual(0, Property<int>(driver, "ExecutedTickCount"));
        }

        [Test]
        public void Observation_is_non_consuming_for_the_next_real_cadence_delta()
        {
            var driver = CreateDriver(new long[] { 1000 }, new long[] { 0 });
            Start(driver);
            AssertObservedTick(Observe(driver), 0);
            Assert.AreEqual(0L, Property<long>(driver, "PendingDueTickCount"));

            Pump(driver);

            Assert.AreEqual("Running", Property(driver, "Lifecycle").ToString());
            Assert.AreEqual(1, Property<int>(driver, "ExecutedTickCount"));
            Assert.AreEqual(0L, Property<long>(driver, "PendingDueTickCount"));

            var bridgeEvidence = (long[])InvokeStatic("ObserveThenSampleTimestampMillisecondsForTesting", 1000L, new long[] { 0, 500, 1000 });
            CollectionAssert.AreEqual(new long[] { 500, 1000 }, bridgeEvidence,
                "Observing the real monotonic bridge must not consume its next cadence sample.");
        }

        [Test]
        public void Reset_creates_a_fresh_receive_time_origin_and_non_running_owner_exposes_no_tick()
        {
            var driver = CreateDriver(Array.Empty<long>(), new long[] { 0, 5000 });
            AssertRejected(Observe(driver), "OwnerNotRunning");

            Start(driver);
            AssertObservedTick(Observe(driver), 0);
            AssertObservedTick(Observe(driver), 4);

            Invoke(driver, "ResetForTesting");
            AssertObservedTick(Observe(driver), 0);

            Invoke(driver, "DisposeForTesting");
            AssertRejected(Observe(driver), "OwnerNotRunning");
        }

        [Test]
        public void Backward_and_overflow_monotonic_evidence_fail_closed_without_a_wrapped_receive_tick()
        {
            var backwards = Assert.Throws<TargetInvocationException>(() =>
                InvokeStatic("ObserveTimestampSamplesForTesting", 1000L, new long[] { 100, 99 }));
            Assert.IsInstanceOf<InvalidOperationException>(backwards.InnerException);

            var overflow = Assert.Throws<OverflowException>(() =>
                InvokeStatic("ObserveTimestampSamplesForTesting", 1L, new long[] { long.MinValue, long.MaxValue }));
            Assert.IsNotNull(overflow);

            var driver = CreateDriver(Array.Empty<long>(), new long[] { -1 });
            Start(driver);
            ExpectOwnerError("TLAW071_OWNER_FAULT");
            AssertRejected(Observe(driver), "ClockFaulted");
            Assert.AreEqual("Faulted", Property(driver, "Lifecycle").ToString());
            Assert.AreNotEqual("Observed", Property(Observe(driver), "Status").ToString());
        }

        private static void AssertTick(long elapsedMilliseconds, long expectedTick)
        {
            Assert.AreEqual(ServerTick.From(expectedTick),
                Gate3ServerReceiveTickMapper.Map(AuthoritativeElapsedMilliseconds.FromMilliseconds(elapsedMilliseconds)));
        }

        private Component CreateDriver(long[] elapsedMilliseconds, long[] observedElapsedMilliseconds)
        {
            var root = new GameObject("TLAW076_ReceiveTickOwner");
            root.SetActive(false);
            _roots.Add(root);
            var driver = root.AddComponent(DriverType);
            Invoke(driver, "ConfigureReceiveTickForTesting", Artifact(), Manifest(), elapsedMilliseconds, observedElapsedMilliseconds, "learning");
            root.SetActive(true);
            return driver;
        }

        private static object Observe(Component driver) => Invoke(driver, "ObserveAuthoritativeServerReceiveTick");

        private static void AssertObservedTick(object observation, long expectedTick)
        {
            Assert.AreEqual("Observed", Property(observation, "Status").ToString());
            Assert.IsTrue(Property<bool>(observation, "HasReceiveTick"));
            Assert.AreEqual(expectedTick, ((ServerTick)Property(observation, "ReceiveTick")).Value);
        }

        private static void AssertRejected(object observation, string expectedStatus)
        {
            Assert.AreEqual(expectedStatus, Property(observation, "Status").ToString());
            Assert.IsFalse(Property<bool>(observation, "HasReceiveTick"));
        }

        private static UnityEngine.Object Artifact() => AssetDatabase.LoadMainAssetAtPath(ArtifactPath);
        private static UnityEngine.Object Manifest() => AssetDatabase.LoadMainAssetAtPath(ManifestPath);

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

        private static object InvokeStatic(string name, params object[] arguments)
        {
            var methods = DriverType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length)
                .ToArray();
            Assert.AreEqual(1, methods.Length, "Required production driver static method is missing or ambiguous: " + name);
            return methods[0].Invoke(null, arguments);
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
            Assert.IsNotNull(field, "Required private field is missing: " + name);
            return field.GetValue(driver);
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
