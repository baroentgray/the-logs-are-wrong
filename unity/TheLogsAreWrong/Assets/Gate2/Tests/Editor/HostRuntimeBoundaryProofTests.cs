using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Gate2.Tests
{
    /// <summary>
    /// TLAW-065 Unity host-runtime boundary architecture proof. Test-only and non-production.
    /// <para>
    /// The probes below own no simulation semantics: every authoritative decision comes from the one
    /// shared imported <see cref="HostSession"/>. The scheduler probe only decides when a tick is due.
    /// Nothing here is a production host loop.
    /// </para>
    /// </summary>
    public sealed class HostRuntimeBoundaryProofTests
    {
        private const string CanonicalOneTickSha = "287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949";
        private const string MultiTickSha = "C7FEC7BD00DE7D5A92DA0A89A09F61D4B7E4DC905A4F7D35687A8E6460029411";

        private static readonly LogId ProbeLogId = LogId.From("probe_log");
        private static readonly ProfileId ProbeProfileId = ProfileId.From("probe");
        private const int SequentialTickCount = 4;

        [Test]
        public void Shared_authority_is_the_only_host_tick_implementation_reachable_from_unity()
        {
            var assembly = typeof(ShiftRuntimeState).Assembly;
            Assert.AreEqual("TheLogsAreWrong.PortableAuthority", assembly.GetName().Name);
            Assert.AreEqual(assembly, typeof(HostTickExecutionService).Assembly);
            Assert.AreEqual(assembly, typeof(HostTickProgressionEvidence).Assembly);
            Assert.AreEqual(assembly, typeof(InMemoryEventJournal).Assembly);
            Assert.AreEqual(assembly, typeof(AcceptedIntentTickBatchFactory).Assembly);
        }

        [Test]
        public void Canonical_one_tick_parity_is_unchanged_under_the_session_boundary()
        {
            var session = new HostSessionProbe(ProbeConfiguration(1, 1));
            var result = session.ExecuteTick(ServerTick.Zero);

            Assert.AreEqual(CanonicalOneTickSha, Sha256(session.ProjectCanonical(ServerTick.Zero, result)));
            Assert.AreEqual(1, session.Invocations);
        }

        [Test]
        public void Consecutive_ticks_carry_authoritative_state_without_a_unity_side_copy()
        {
            var first = RunSequence();
            var second = RunSequence();

            Assert.AreEqual(MultiTickSha, Sha256(first.Projection), "Imported multi-tick projection drifted.");
            Assert.AreEqual(first.Projection, second.Projection, "The tick sequence was not repeat-deterministic.");
            Assert.AreEqual(SequentialTickCount, first.Invocations, "Exactly one semantic invocation per authoritative tick is required.");
        }

        [Test]
        public void Exact_clock_scheduler_is_independent_of_frame_count()
        {
            var coarse = new ExactTickSchedulerProbe(1000);
            var fine = new ExactTickSchedulerProbe(1000);

            var coarseTotal = 0;
            for (var i = 0; i < 20; i++) coarseTotal += coarse.DrainDueTicks(2000 / 20 + (i < 2000 % 20 ? 1 : 0));
            var fineTotal = 0;
            for (var i = 0; i < 120; i++) fineTotal += fine.DrainDueTicks(2000 / 120 + (i < 2000 % 120 ? 1 : 0));

            Assert.AreEqual(2, coarseTotal);
            Assert.AreEqual(coarseTotal, fineTotal, "Authoritative cadence must not depend on frame count.");
        }

        [Test]
        public void One_frame_can_yield_zero_one_or_many_due_ticks()
        {
            var scheduler = new ExactTickSchedulerProbe(1000);
            var due = new[] { 400L, 400L, 400L, 2500L, 0L }.Select(scheduler.DrainDueTicks).ToArray();

            CollectionAssert.Contains(due, 0);
            CollectionAssert.Contains(due, 1);
            Assert.IsTrue(due.Any(d => d > 1), "A single frame must be able to retire multiple due ticks.");
            Assert.AreEqual(3, due.Sum());
        }

        [Test]
        public void Disposed_production_session_ticks_are_rejected()
        {
            var disposed = new HostSessionProbe(ProbeConfiguration(30, 600));
            disposed.Dispose();
            Assert.Throws<ObjectDisposedException>(() => disposed.ExecuteTick(ServerTick.Zero));
        }

        [Test]
        public void Two_sessions_are_independent_and_no_ownership_guard_exists_at_this_boundary()
        {
            var left = new HostSessionProbe(ProbeConfiguration(30, 600));
            var right = new HostSessionProbe(ProbeConfiguration(30, 600));

            left.ExecuteTick(ServerTick.Zero);
            right.ExecuteTick(ServerTick.Zero);

            // Both succeed: nothing in the shared authority prevents a duplicate host session.
            // Single-owner enforcement is therefore an unresolved production policy, recorded in the dossier.
            Assert.AreEqual(1, left.Invocations);
            Assert.AreEqual(1, right.Invocations);
        }

        private static SequenceResult RunSequence()
        {
            var session = new HostSessionProbe(ProbeConfiguration(30, 600));
            var parts = new List<string>();
            for (var i = 0; i < SequentialTickCount; i++)
            {
                var tick = ServerTick.From(i);
                var result = session.ExecuteTick(tick);
                parts.Add("--- tick " + i + " ---\n" + session.Project(tick, result));
            }

            return new SequenceResult(string.Join("\n", parts.ToArray()), session.Invocations);
        }

        private static ShiftConfiguration ProbeConfiguration(int intakeTimeoutSeconds, int hardDeadlineSeconds)
        {
            var capacities = ImmutableDictionary<NodeId, NodeCapacity>.Empty
                .Add(NodeId.FEED_GATE, NodeCapacity.Limited(1))
                .Add(NodeId.INTAKE, NodeCapacity.Limited(1))
                .Add(NodeId.PROCEDURE, NodeCapacity.Limited(1))
                .Add(NodeId.SAW_QUEUE, NodeCapacity.Limited(1))
                .Add(NodeId.SAW, NodeCapacity.Limited(1))
                .Add(NodeId.CONTAINMENT, NodeCapacity.Unlimited);

            return new ShiftConfiguration(
                ShiftId.From("TLAW063_PROBE_SHIFT"),
                new ShiftSeed(63),
                ImmutableDictionary<ProfileId, ShiftProfile>.Empty
                    .Add(ProbeProfileId, new ShiftProfile(intakeTimeoutSeconds, hardDeadlineSeconds)),
                new ObjectivesDefinition(new QuotaDefinition(1, ImmutableDictionary<SpeciesId, int>.Empty.Add(SpeciesId.From("pine"), 1)), 0),
                new SupplyDefinition(1, 0),
                new SchedulerConfiguration(capacities, 1, 1, 1, 4, 1, 1, "probe", ImmutableArray<HostTickStage>.Empty),
                new LineNoiseConfiguration(ImmutableArray<string>.Empty, 0, false, false),
                new ResourcesConfiguration(ImmutableDictionary<ItemId, int>.Empty, ImmutableHashSet<ItemId>.Empty),
                new ContainmentConfiguration(
                    true,
                    1,
                    1,
                    1,
                    ImmutableDictionary<string, int>.Empty,
                    new AfterSuccessfulRitualDefinition(ContainmentState.STABLE, false, false, false),
                    new PrototypeIncidentDefinition("probe", 0, false, false),
                    new PostFactoIntervalInferenceDefinition(false, "probe", false)),
                ImmutableArray<string>.Empty,
                ImmutableArray.Create(new ManifestLogDefinition(ProbeLogId, SpeciesId.From("pine"), SpeciesId.From("pine"), null)));
        }

        private static string Sha256(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
            }
        }

        private sealed class SequenceResult
        {
            public SequenceResult(string projection, int invocations)
            {
                Projection = projection;
                Invocations = invocations;
            }

            public string Projection { get; }
            public int Invocations { get; }
        }

        /// <summary>
        /// Test adapter around the imported production <see cref="HostSession"/>. It owns no copied
        /// state, service, event identity, or tick orchestration.
        /// </summary>
        private sealed class HostSessionProbe
        {
            private readonly AnomalyCatalog _anomalies = new AnomalyCatalog(ImmutableDictionary<AnomalyId, AnomalyDefinition>.Empty);
            private readonly HostSession _session;

            public int Invocations { get; private set; }
            public bool Disposed => _session.IsDisposed;

            public HostSessionProbe(ShiftConfiguration shift)
            {
                _session = new HostSession(shift, _anomalies, ProbeProfileId);
            }

            public HostStageSevenEventExecution ExecuteTick(ServerTick tick)
            {
                var result = _session.ExecuteTick(
                    tick,
                    AcceptedIntentTickBatchFactory.Create(_session.ShiftState.ShiftId, tick, ImmutableArray<AuthoritativeAcceptedIntent>.Empty),
                    ImmutableHashSet<ItemId>.Empty);
                Invocations++;
                return result;
            }

            public void Dispose()
            {
                _session.Dispose();
            }

            public string Project(ServerTick tick, HostStageSevenEventExecution result)
            {
                LogRuntimeState log;
                _session.ShiftState.TryGetLog(ProbeLogId, out log);
                var events = _session.Journal.Events.ToArray();
                var lines = new List<string>
                {
                    "operation=HostTickExecutionService.Execute",
                    "result=" + result.GetType().Name,
                    "tick=" + tick,
                    "shift_id=" + _session.ShiftState.ShiftId,
                    "state_version=" + _session.ShiftState.StateVersion,
                    "log_state=" + log.State,
                    "line_state=" + _session.ShiftState.Line.State,
                    "containment_state=" + _session.ShiftState.Containment.State,
                    "line_noise=" + _session.LineNoise.Current,
                    "checkpoint=" + result.Checkpoint.GetType().Name,
                    "journal_count=" + events.Length
                };
                AppendJournal(lines, events);
                return string.Join("\n", lines.ToArray());
            }

            public string ProjectCanonical(ServerTick tick, HostStageSevenEventExecution result)
            {
                LogRuntimeState log;
                _session.ShiftState.TryGetLog(ProbeLogId, out log);
                var events = _session.Journal.Events.ToArray();
                var stageOrder = string.Join(">", new[]
                {
                    result.StageOne.GetType().Name,
                    result.StageTwo.GetType().Name,
                    result.StageThree.GetType().Name,
                    result.StageFour.GetType().Name,
                    result.StageFive.GetType().Name,
                    result.StageSix.GetType().Name,
                    result.GetType().Name
                });

                var lines = new List<string>
                {
                    "operation=HostTickExecutionService.Execute",
                    "stage_order=" + stageOrder,
                    "tick=" + tick,
                    "shift_id=" + _session.ShiftState.ShiftId,
                    "state_version=" + _session.ShiftState.StateVersion,
                    "log_id=" + ProbeLogId,
                    "log_state=" + log.State,
                    "line_state=" + _session.ShiftState.Line.State,
                    "containment_state=" + _session.ShiftState.Containment.State,
                    "quota_target_total=" + result.FinalQuotaState.TargetTotal,
                    "quota_credited_total=" + result.FinalQuotaState.TotalCreditedUnits,
                    "quota_correct_anomalies=" + result.FinalQuotaState.CorrectlyProcessedAnomalies,
                    "line_noise=" + _session.LineNoise.Current,
                    "journal_count=" + events.Length
                };
                AppendJournal(lines, events);
                lines.Add("checkpoint=" + result.Checkpoint.GetType().Name);
                return string.Join("\n", lines.ToArray());
            }

            private static void AppendJournal(List<string> lines, EventEnvelope[] events)
            {
                foreach (var envelope in events)
                {
                    var payload = (HostStageSevenVersionedPayload)envelope.Payload;
                    lines.Add("journal=" + envelope.Sequence + "|" + envelope.EventType + "|" + payload.PriorStateVersion
                        + "|" + payload.CurrentStateVersion + "|"
                        + (envelope.CausedByIntentId == null ? "-" : envelope.CausedByIntentId.ToString()));
                }
            }
        }

        /// <summary>
        /// Exact-clock scheduler probe. Authoritative elapsed time is an integer quantity, so due-tick
        /// count depends only on elapsed authoritative time and never on frame count. It performs no
        /// semantics and never calls the shared service.
        /// </summary>
        private sealed class ExactTickSchedulerProbe
        {
            private readonly long _millisecondsPerTick;
            private long _elapsedMilliseconds;
            private long _emitted;

            public ExactTickSchedulerProbe(long millisecondsPerTick)
            {
                _millisecondsPerTick = millisecondsPerTick;
            }

            public int DrainDueTicks(long deltaMilliseconds)
            {
                _elapsedMilliseconds += deltaMilliseconds;
                var total = _elapsedMilliseconds / _millisecondsPerTick;
                var due = (int)(total - _emitted);
                _emitted = total;
                return due;
            }
        }
    }
}
