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
    /// TLAW-064 bounded host-tick import proof. After the D-020 H2 migration the imported production
    /// PortableAuthority plugin owns the single seven-stage HostTickExecutionService composition, so this
    /// fixture invokes that one shared orchestration directly. It supplies only input values and projection
    /// formatting; it recreates no host-stage decision and no orchestration.
    /// </summary>
    public sealed class HostTickImportParityTests
    {
        private const string ExpectedHostTickHash = "287BD37030A1F1875B6067D00D0C4EA2B1A3018C8A40490716B4B54987C25949";

        private static readonly LogId ProbeLogId = LogId.From("probe_log");
        private static readonly ProfileId ProbeProfileId = ProfileId.From("probe");

        [Test]
        public void Imported_host_tick_authority_is_owned_by_the_portable_plugin()
        {
            var assembly = typeof(ShiftRuntimeState).Assembly;
            Assert.AreEqual("TheLogsAreWrong.PortableAuthority", assembly.GetName().Name);
            Assert.AreEqual(assembly, typeof(HostTickExecutionService).Assembly);
            Assert.AreEqual(assembly, typeof(HostStageOneCompletionExecutor).Assembly);
            Assert.AreEqual(assembly, typeof(AcceptedIntentStageExecutor).Assembly);
            Assert.AreEqual(assembly, typeof(HostStageThreeDeadlineExecutor).Assembly);
            Assert.AreEqual(assembly, typeof(HostStageFourSawExecutor).Assembly);
            Assert.AreEqual(assembly, typeof(HostStageFiveFeedExecutor).Assembly);
            Assert.AreEqual(assembly, typeof(HostStageSixDerivedExecutor).Assembly);
            Assert.AreEqual(assembly, typeof(HostStageSevenEventExecutor).Assembly);
        }

        [Test]
        public void Imported_host_tick_execution_reproduces_the_canonical_parity_projection()
        {
            var first = ExecuteProbeTick();
            var second = ExecuteProbeTick();

            Assert.AreEqual(first, second, "The imported host tick was not repeat-deterministic.");
            Assert.AreEqual(ExpectedHostTickHash, Sha256(Encoding.UTF8.GetBytes(first)));
        }

        private static string ExecuteProbeTick()
        {
            var shift = CreateProbeConfiguration();
            var anomalies = new AnomalyCatalog(ImmutableDictionary<AnomalyId, AnomalyDefinition>.Empty);
            var tick = ServerTick.Zero;
            var state = ShiftRuntimeState.Create(shift);
            var journal = new InMemoryEventJournal(state.ShiftId);

            var result = new HostTickExecutionService().Execute(
                state,
                QuotaRuntimeState.Create(shift),
                MovementNoiseRuntimeState.Create(state.ShiftId),
                LineNoiseRuntimeState.Create(state.ShiftId),
                HostTickProgressionEvidence.Create(state.ShiftId),
                ShiftLifecycleRuntimeState.Create(shift, ProbeProfileId),
                AcceptedIntentTickBatchFactory.Create(state.ShiftId, tick, ImmutableArray<AuthoritativeAcceptedIntent>.Empty),
                ImmutableHashSet<ItemId>.Empty,
                journal,
                ImmutableArray.Create(EventId.From("event-1")),
                tick,
                shift.Scheduler,
                shift,
                shift.Containment,
                anomalies);

            var published = result as HostStageSevenPublished;
            Assert.IsNotNull(published, "The imported host tick did not publish.");

            var final = published.FinalShiftState;
            LogRuntimeState log;
            Assert.IsTrue(final.TryGetLog(ProbeLogId, out log));
            var quota = published.FinalQuotaState;
            var events = journal.Events.ToArray();

            var stageOrder = string.Join(">", new[]
            {
                published.StageOne.GetType().Name,
                published.StageTwo.GetType().Name,
                published.StageThree.GetType().Name,
                published.StageFour.GetType().Name,
                published.StageFive.GetType().Name,
                published.StageSix.GetType().Name,
                published.GetType().Name
            });

            var lines = new List<string>
            {
                "operation=HostTickExecutionService.Execute",
                "stage_order=" + stageOrder,
                "tick=" + tick,
                "shift_id=" + final.ShiftId,
                "state_version=" + final.StateVersion,
                "log_id=" + ProbeLogId,
                "log_state=" + log.State,
                "line_state=" + final.Line.State,
                "containment_state=" + final.Containment.State,
                "quota_target_total=" + quota.TargetTotal,
                "quota_credited_total=" + quota.TotalCreditedUnits,
                "quota_correct_anomalies=" + quota.CorrectlyProcessedAnomalies,
                "line_noise=" + published.FinalLineNoise.Current,
                "journal_count=" + events.Length
            };

            foreach (var envelope in events)
            {
                var payload = (HostStageSevenVersionedPayload)envelope.Payload;
                lines.Add("journal=" + envelope.Sequence + "|" + envelope.EventType + "|" + payload.PriorStateVersion
                    + "|" + payload.CurrentStateVersion + "|"
                    + (envelope.CausedByIntentId == null ? "-" : envelope.CausedByIntentId.ToString()));
            }

            lines.Add("checkpoint=" + published.Checkpoint.GetType().Name);
            return string.Join("\n", lines.ToArray());
        }

        private static ShiftConfiguration CreateProbeConfiguration()
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
                ImmutableDictionary<ProfileId, ShiftProfile>.Empty.Add(ProbeProfileId, new ShiftProfile(1, 1)),
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

        private static string Sha256(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty);
            }
        }
    }
}
