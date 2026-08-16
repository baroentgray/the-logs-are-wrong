using System;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using UnityEngine;

namespace TheLogsAreWrong.Gate2
{
    /// <summary>
    /// Command-line-gated production-plugin smoke. This owns no simulation state and delegates every
    /// transition and derivation to the imported PortableAuthority assembly.
    /// </summary>
    public static class PortableAuthoritySmoke
    {
        private const string SmokeArgument = "-tlaw-bootstrap-smoke";
        private const string ExpectedSha = "CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunDuringSmokeStartup()
        {
            if (!HasSmokeArgument())
            {
                return;
            }

            try
            {
                // Direct type materialization proves the imported assembly is present before the operation.
                var types = new[]
                {
                    typeof(ShiftRuntimeState),
                    typeof(HostLogTransitionService),
                    typeof(SawCycleStartService),
                    typeof(LineNoiseDerivationService)
                };

                if (types.Length != 4 || types[0].Assembly.GetName().Name != "TheLogsAreWrong.PortableAuthority")
                {
                    throw new InvalidOperationException("Imported PortableAuthority types did not resolve from the expected assembly.");
                }

                Debug.Log("TLAW062_PLAYER_PORTABLE_LOAD_PASS");

                var projection = ExecuteAcceptedAuthorityChain();
                var hash = Sha256(projection);
                if (!string.Equals(hash, ExpectedSha, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("PortableAuthority projection did not match the accepted canonical SHA.");
                }

                Debug.Log("TLAW062_PLAYER_AUTHORITY_PASS");
                Debug.Log("TLAW062_PLAYER_AUTHORITY_SHA=" + hash);
            }
            catch (Exception exception)
            {
                Debug.LogError("TLAW062_PLAYER_AUTHORITY_FAIL " + exception.GetType().Name + ": " + exception.Message);
                Application.Quit(2);
            }
        }

        private static bool HasSmokeArgument()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], SmokeArgument, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ExecuteAcceptedAuthorityChain()
        {
            var configuration = CreateProbeConfiguration();
            var logId = LogId.From("probe_log");
            var created = ShiftRuntimeState.Create(configuration);
            var transitions = new HostLogTransitionService();
            var atFeedGate = RequireTransition(transitions.Apply(created, logId, LogState.AT_FEED_GATE)).State;
            var atIntake = RequireTransition(transitions.Apply(atFeedGate, logId, LogState.AT_INTAKE)).State;
            var queuedForSaw = RequireTransition(transitions.Apply(atIntake, logId, LogState.QUEUED_FOR_SAW)).State;
            var sawStarted = RequireSawStart(new SawCycleStartService().Start(queuedForSaw, ServerTick.From(10), configuration.Scheduler));
            var lineNoise = RequireLineNoiseChange(new LineNoiseDerivationService().Evaluate(
                LineNoiseRuntimeState.Create(sawStarted.State.ShiftId),
                sawStarted.State,
                MovementNoiseRuntimeState.Create(sawStarted.State.ShiftId),
                ServerTick.From(10)));

            LogRuntimeState log;
            if (!sawStarted.State.TryGetLog(logId, out log))
            {
                throw new InvalidOperationException("The authoritative saw state did not retain the probe log.");
            }

            return string.Join("\n", new[]
            {
                "operation_chain=ShiftRuntimeState.Create>HostLogTransitionService.Apply>HostLogTransitionService.Apply>HostLogTransitionService.Apply>SawCycleStartService.Start>LineNoiseDerivationService.Evaluate",
                "shift_id=" + created.ShiftId,
                "created_state_version=" + created.StateVersion,
                "queued_state_version=" + queuedForSaw.StateVersion,
                "saw_state_version=" + sawStarted.State.StateVersion,
                "log_id=" + logId,
                "log_state=" + log.State,
                "saw_started_at=" + sawStarted.Cycle.StartedAt,
                "saw_due_at=" + sawStarted.Cycle.DueAt,
                "line_noise=" + lineNoise.State.Current,
                "line_noise_evaluated_at=" + lineNoise.State.LastEvaluatedAt,
                "line_noise_changed_at=" + lineNoise.State.LastChangedAt
            });
        }

        private static HostLogTransitionAccepted RequireTransition(HostLogTransitionResult result)
        {
            var accepted = result as HostLogTransitionAccepted;
            if (accepted == null)
            {
                throw new InvalidOperationException("The required host log transition was rejected.");
            }

            return accepted;
        }

        private static SawCycleStarted RequireSawStart(SawCycleStartResult result)
        {
            var started = result as SawCycleStarted;
            if (started == null)
            {
                throw new InvalidOperationException("The required saw start was rejected.");
            }

            return started;
        }

        private static LineNoiseEvaluatedWithChange RequireLineNoiseChange(LineNoiseEvaluationResult result)
        {
            var changed = result as LineNoiseEvaluatedWithChange;
            if (changed == null)
            {
                throw new InvalidOperationException("The required line-noise derivation did not produce its authoritative change.");
            }

            return changed;
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
                ShiftId.From("TLAW058_PROBE_SHIFT"),
                new ShiftSeed(58),
                ImmutableDictionary<ProfileId, ShiftProfile>.Empty.Add(ProfileId.From("probe"), new ShiftProfile(1, 1)),
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
                ImmutableArray.Create(new ManifestLogDefinition(LogId.From("probe_log"), SpeciesId.From("pine"), SpeciesId.From("pine"), null)));
        }

        private static string Sha256(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
            }
        }
    }
}
