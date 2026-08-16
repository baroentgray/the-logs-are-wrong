using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using UnityEngine;

namespace TheLogsAreWrong.Gate2.Tests
{
    /// <summary>
    /// TLAW-062 proves the derived three-DLL production import directly. The fixture supplies only
    /// input values and projection formatting; PortableAuthority owns every transition and derivation.
    /// </summary>
    public sealed class PortableAuthorityImportParityTests
    {
        private const string ExpectedAuthorityHash = "CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F";
        private const string ImmutableHash = "5B1B1C83BA3D135C2FDFE425842FBE9C7432878B7E468623ACB554C69B4C130F";
        private const string UnsafeHash = "01748200F2400C742AA689F1F5101BD6298EFDFD92C00C18F4FA473847235BA9";

        // Before this test runs, the caller must materialize the fresh candidate deployment output with:
        // dotnet build src/TheLogsAreWrong.PortableAuthority/TheLogsAreWrong.PortableAuthority.csproj
        //     --configuration Release -p:IncludeSourceRevisionInInformationalVersion=false -p:DebugSymbols=false
        // These non-persisted deployment properties remove the revision stamp and PDB/SourceLink debug metadata,
        // making the byte-equality target reproducible across the implementation commit.
        private const string DeploymentInformationalVersion = "1.0.0";

        [Test]
        public void Committed_plugin_set_is_exactly_the_three_authorized_dlls()
        {
            var names = Directory.GetFiles(AssetsRoot(), "*.dll", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "System.Collections.Immutable.dll",
                    "System.Runtime.CompilerServices.Unsafe.dll",
                    "TheLogsAreWrong.PortableAuthority.dll"
                },
                names);
        }

        [Test]
        public void Dependency_plugins_retain_the_accepted_identity_and_hash()
        {
            AssertAssembly(PluginPath("System.Collections.Immutable.dll"), "System.Collections.Immutable", new Version(8, 0, 0, 0), "b03f5f7f11d50a3a", ImmutableHash);
            AssertAssembly(PluginPath("System.Runtime.CompilerServices.Unsafe.dll"), "System.Runtime.CompilerServices.Unsafe", new Version(6, 0, 0, 0), "b03f5f7f11d50a3a", UnsafeHash);
        }

        [Test]
        public void Committed_portable_plugin_is_byte_identical_to_the_fresh_candidate_release_output()
        {
            var committed = PluginPath("TheLogsAreWrong.PortableAuthority.dll");
            var fresh = FreshCandidateDeploymentOutputPath();

            Assert.IsTrue(File.Exists(fresh), "Fresh candidate PortableAuthority deployment output is missing.");
            CollectionAssert.AreEqual(File.ReadAllBytes(fresh), File.ReadAllBytes(committed));
            AssertAssembly(committed, "TheLogsAreWrong.PortableAuthority", new Version(1, 0, 0, 0), null, Sha256(File.ReadAllBytes(fresh)));
            var informationalVersion = typeof(ShiftRuntimeState).Assembly
                .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                .Cast<AssemblyInformationalVersionAttribute>()
                .Single()
                .InformationalVersion;
            Assert.AreEqual(DeploymentInformationalVersion, informationalVersion);
        }

        [Test]
        public void Imported_portable_assembly_loads_and_resolves_the_expected_authority_types()
        {
            var assembly = typeof(ShiftRuntimeState).Assembly;
            Assert.AreEqual("TheLogsAreWrong.PortableAuthority", assembly.GetName().Name);
            Assert.AreEqual(assembly, typeof(HostLogTransitionService).Assembly);
            Assert.AreEqual(assembly, typeof(SawCycleStartService).Assembly);
            Assert.AreEqual(assembly, typeof(LineNoiseDerivationService).Assembly);
        }

        [Test]
        public void Imported_portable_authority_executes_the_accepted_chain_with_the_canonical_projection()
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
            Assert.IsTrue(sawStarted.State.TryGetLog(logId, out log));
            var projection = string.Join("\n", new[]
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

            Assert.AreEqual(ExpectedAuthorityHash, Sha256(Encoding.UTF8.GetBytes(projection)));
        }

        [Test]
        public void Unity_contains_no_copied_authority_source_or_forbidden_dependency_plugin()
        {
            var copiedAuthorityNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "AnomalyResolutionContracts.cs", "ConfirmationTestContracts.cs", "ValidatedConfiguration.cs", "ContainmentLifecycleContracts.cs", "DomainEnums.cs", "EventContracts.cs", "Identifiers.cs", "IntentContracts.cs", "LineJamRepairContracts.cs", "LineNoiseRuntimeContracts.cs", "MovementNoiseRuntimeContracts.cs", "LogTransitionPolicy.cs", "Primitives.cs", "QuotaContracts.cs", "ConfirmationTestLifecycleContracts.cs", "LogTransitionServices.cs", "ProcedureActionLifecycleContracts.cs", "ProcedureCompletionContracts.cs", "ShiftRuntimeState.cs", "DefaultIntakeAutoRouteContracts.cs", "FeedDueResolutionContracts.cs", "FeedPlanningContracts.cs", "IntakeDeadlineContracts.cs", "RepairPendingTransitionExecutionContracts.cs", "SawCycleContracts.cs", "SimulationTime.cs"
            };
            var copied = Directory.GetFiles(AssetsRoot(), "*.cs", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(name => copiedAuthorityNames.Contains(name))
                .ToArray();

            CollectionAssert.IsEmpty(copied);
            Assert.IsFalse(File.Exists(Path.Combine(AssetsRoot(), "TheLogsAreWrong.Domain.dll")));
            Assert.IsFalse(File.Exists(Path.Combine(AssetsRoot(), "System.Memory.dll")));
            Assert.IsFalse(File.Exists(Path.Combine(AssetsRoot(), "System.Buffers.dll")));
            Assert.IsFalse(File.Exists(Path.Combine(AssetsRoot(), "System.Numerics.Vectors.dll")));
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(ProjectRoot(), "..", ".."));
        }

        private static string AssetsRoot()
        {
            return Path.Combine(ProjectRoot(), "Assets");
        }

        private static string PluginPath(string fileName)
        {
            return Path.Combine(AssetsRoot(), "Gate2", "Plugins", "PortableAuthority", fileName);
        }

        private static string FreshCandidateDeploymentOutputPath()
        {
            return Path.Combine(RepositoryRoot(), "src", "TheLogsAreWrong.PortableAuthority", "bin", "Release", "netstandard2.1", "TheLogsAreWrong.PortableAuthority.dll");
        }

        private static void AssertAssembly(string path, string name, Version version, string token, string expectedHash)
        {
            Assert.IsTrue(File.Exists(path), "Plugin is missing: " + path);
            var identity = AssemblyName.GetAssemblyName(path);
            Assert.AreEqual(name, identity.Name);
            Assert.AreEqual(version, identity.Version);
            if (token != null)
            {
                Assert.AreEqual(token, BitConverter.ToString(identity.GetPublicKeyToken()).Replace("-", string.Empty).ToLowerInvariant());
            }

            Assert.AreEqual(expectedHash, Sha256(File.ReadAllBytes(path)));
        }

        private static HostLogTransitionAccepted RequireTransition(HostLogTransitionResult result)
        {
            var accepted = result as HostLogTransitionAccepted;
            Assert.IsNotNull(accepted, "Required host transition was rejected.");
            return accepted;
        }

        private static SawCycleStarted RequireSawStart(SawCycleStartResult result)
        {
            var started = result as SawCycleStarted;
            Assert.IsNotNull(started, "Required saw start was rejected.");
            return started;
        }

        private static LineNoiseEvaluatedWithChange RequireLineNoiseChange(LineNoiseEvaluationResult result)
        {
            var changed = result as LineNoiseEvaluatedWithChange;
            Assert.IsNotNull(changed, "Required line-noise evaluation did not produce a change.");
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

        private static string Sha256(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty);
            }
        }
    }
}
