using System.Reflection;
using System.Text.RegularExpressions;
using TheLogsAreWrong.Config.Yaml;
using TheLogsAreWrong.Domain.Configuration.Diagnostics;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

public sealed class ArchitectureGuardTests
{
    [Fact]
    public void Domain_assembly_references_no_yaml_unity_or_network_assemblies()
    {
        var references = typeof(ConfigurationLoadResult).Assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty);

        Assert.DoesNotContain(references, reference => reference.Contains("Yaml", StringComparison.OrdinalIgnoreCase) || reference.Contains("Unity", StringComparison.OrdinalIgnoreCase) || reference.Contains("Fish", StringComparison.OrdinalIgnoreCase) || reference.Contains("Steam", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Yaml_dtos_are_internal_and_public_domain_surface_does_not_expose_yaml()
    {
        var yamlAssembly = typeof(YamlConfigurationLoader).Assembly;
        var rawDto = yamlAssembly.GetType("TheLogsAreWrong.Config.Yaml.Dto.RawYamlDocument");
        var domainAssembly = typeof(ConfigurationLoadResult).Assembly;

        Assert.NotNull(rawDto);
        Assert.False(rawDto!.IsPublic);
        Assert.DoesNotContain(domainAssembly.GetExportedTypes(), type => type.FullName?.Contains("Yaml", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Domain_sources_contain_no_wall_clock_timer_or_filesystem_dependencies()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var sourcePaths = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
        Assert.Equal(64, sourcePaths.Length);

        var source = sourcePaths
            .Select(File.ReadAllText)
            .ToArray();
        var forbidden = new[] { "DateTime", "DateTimeOffset", "Stopwatch", "Environment.TickCount", "Task", "Thread.Sleep", "System.IO", "UnityEngine", "FishNet", "Steamworks" };

        Assert.All(forbidden, forbiddenToken => Assert.DoesNotContain(source, file => file.Contains(forbiddenToken, StringComparison.Ordinal)));
        Assert.DoesNotContain("PackageReference", File.ReadAllText(Path.Combine(sourceRoot, "TheLogsAreWrong.Domain.csproj")), StringComparison.Ordinal);
    }

    [Fact]
    public void Event_and_journal_public_apis_do_not_expose_bare_ordering_longs()
    {
        var types = new[] { typeof(EventEnvelope), typeof(RejectionEvent), typeof(IEventJournal), typeof(InMemoryEventJournal), typeof(SnapshotBoundary), typeof(ReplayValidator) };

        Assert.All(types, type => Assert.DoesNotContain(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly), method =>
            method.GetParameters().Any(parameter => parameter.ParameterType == typeof(long)) || method.ReturnType == typeof(long)));
    }

    [Fact]
    public void Portable_authority_core_is_the_single_owner_of_the_accepted_host_tick_cut_and_cadence()
    {
        var root = FindRepositoryRoot();
        var domainRoot = Path.Combine(root, "src", "TheLogsAreWrong.Domain");
        var portableRoot = Path.Combine(root, "src", "TheLogsAreWrong.PortableAuthority");
        var moved = new[]
        {
            "Anomalies/AnomalyResolutionContracts.cs", "Anomalies/ConfirmationTestContracts.cs", "Configuration/ValidatedConfiguration.cs",
            "Containment/ContainmentLifecycleContracts.cs", "Enums/DomainEnums.cs", "Events/EventContracts.cs", "Identifiers/Identifiers.cs",
            "Intents/IntentContracts.cs", "Line/LineJamRepairContracts.cs", "Line/LineNoiseRuntimeContracts.cs",
            "Line/MovementNoiseRuntimeContracts.cs", "Logs/LogTransitionPolicy.cs", "Primitives/Primitives.cs", "Quota/QuotaContracts.cs",
            "Runtime/ConfirmationTestLifecycleContracts.cs", "Runtime/LogTransitionServices.cs", "Runtime/ProcedureActionLifecycleContracts.cs",
            "Runtime/ProcedureCompletionContracts.cs", "Runtime/ShiftRuntimeState.cs", "Scheduler/DefaultIntakeAutoRouteContracts.cs",
            "Scheduler/FeedDueResolutionContracts.cs", "Scheduler/FeedPlanningContracts.cs", "Scheduler/IntakeDeadlineContracts.cs",
            "Scheduler/RepairPendingTransitionExecutionContracts.cs", "Scheduler/SawCycleContracts.cs", "Time/SimulationTime.cs",
            // TLAW-064 / D-020 H2: the measured HostTickExecutionService.Execute cut joins the same portable owner.
            "Intents/AcceptedIntentBatchContracts.cs", "Intents/ConfirmationTestIntentContracts.cs",
            "Intents/ContainmentRitualIntentContracts.cs", "Intents/LineRepairIntentContracts.cs",
            "Intents/ProcedureActionIntentContracts.cs", "Journal/EventJournal.cs",
            "Journal/JournaledMutationCommitContracts.cs", "Journal/ReplayContracts.cs",
            "Runtime/AcceptedIntentStageExecutionContracts.cs", "Runtime/ConfirmationTestIntentHandler.cs",
            "Runtime/ContainmentRitualIntentHandler.cs", "Runtime/HostStageFiveFeedExecutionContracts.cs",
            "Runtime/HostStageFourSawExecutionContracts.cs", "Runtime/HostStageOneCompletionExecutionContracts.cs",
            "Runtime/HostStageSevenEventExecutionContracts.cs", "Runtime/HostStageSixDerivedExecutionContracts.cs",
            "Runtime/HostStageThreeDeadlineExecutionContracts.cs", "Runtime/HostTickCompletionCheckpointContracts.cs",
            "Runtime/HostTickExecutionContracts.cs", "Runtime/LineRepairIntentHandler.cs",
            "Runtime/ProcedureActionIntentHandler.cs", "Runtime/SawQuotaApplicationContracts.cs",
            "Runtime/ShiftCompletionContracts.cs", "Scheduler/FeedGateJamDerivationContracts.cs",
            "Scheduler/IntakeAutoFeedJamDerivationContracts.cs", "Scheduler/RepairAutoFeedNormalFeedPlanningContracts.cs",
            "Scheduler/RepairFeedGateIntakeDeadlineContracts.cs", "Sequencing/SequencingContracts.cs"
        };

        var session = "Runtime/HostSessionContracts.cs";
        // TLAW-068 / D-021 U2 adds only a cadence boundary beside the frozen 54-file HostTick cut.
        var cadence = "Runtime/HostTickCadenceContracts.cs";
        // TLAW-070 / D-022 adds one already-validated configuration transport and its deployment identity.
        var c1Transport = new[] { "Configuration/ValidatedConfigurationC1Codec.cs", "Configuration/ValidatedConfigurationC1DeploymentManifest.cs" };

        var domainSources = RelativeSources(domainRoot);
        var portableSources = RelativeSources(portableRoot).Where(path => !path.StartsWith("Support/", StringComparison.Ordinal)).ToArray();
        Assert.Equal(6, domainSources.Length);
        Assert.Equal(moved.Append(session).Append(cadence).Concat(c1Transport).OrderBy(path => path, StringComparer.Ordinal), portableSources.OrderBy(path => path, StringComparer.Ordinal));
        Assert.All(moved, path => Assert.False(File.Exists(Path.Combine(domainRoot, path))));

        var portableProject = File.ReadAllText(Path.Combine(portableRoot, "TheLogsAreWrong.PortableAuthority.csproj"));
        var domainProject = File.ReadAllText(Path.Combine(domainRoot, "TheLogsAreWrong.Domain.csproj"));
        var compilerCompatibility = File.ReadAllText(Path.Combine(portableRoot, "Support", "CompilerCompatibility.cs"));
        Assert.Contains("<TargetFramework>netstandard2.1</TargetFramework>", portableProject, StringComparison.Ordinal);
        Assert.Contains("<LangVersion>latest</LangVersion>", portableProject, StringComparison.Ordinal);
        Assert.Contains("<AssemblyName>TheLogsAreWrong.PortableAuthority</AssemblyName>", portableProject, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(portableProject, "<PackageReference").Cast<Match>());
        Assert.Contains("PackageReference Include=\"System.Collections.Immutable\" Version=\"8.0.0\"", portableProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<ProjectReference", portableProject, StringComparison.Ordinal);
        Assert.Contains("TheLogsAreWrong.PortableAuthority", domainProject, StringComparison.Ordinal);
        Assert.All(new[] { "IsExternalInit", "RequiredMemberAttribute", "CompilerFeatureRequiredAttribute", "SetsRequiredMembersAttribute" }, definition => Assert.Contains(definition, compilerCompatibility, StringComparison.Ordinal));

        var domainAssembly = typeof(ConfigurationLoadResult).Assembly;
        var portableAssembly = typeof(ShiftRuntimeState).Assembly;
        Assert.Equal("TheLogsAreWrong.PortableAuthority", portableAssembly.GetName().Name);
        Assert.Contains(domainAssembly.GetReferencedAssemblies(), reference => reference.Name == "TheLogsAreWrong.PortableAuthority");
        Assert.DoesNotContain(portableAssembly.GetReferencedAssemblies(), reference => reference.Name == "TheLogsAreWrong.Domain");
        var portableReferences = portableAssembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(portableReferences, reference =>
            reference.Contains("Yaml", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Unity", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Fish", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Steam", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Sockets", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Net.Http", StringComparison.OrdinalIgnoreCase));

        var portableSource = RelativeSources(portableRoot)
            .Select(path => File.ReadAllText(Path.Combine(portableRoot, path)))
            .ToArray();
        Assert.All(portableSource, source =>
        {
            Assert.DoesNotContain("UnityEngine", source, StringComparison.Ordinal);
            Assert.DoesNotContain("FishNet", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Steamworks", source, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Net", source, StringComparison.Ordinal);
            Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Socket", source, StringComparison.Ordinal);
        });
    }

    private static string[] RelativeSources(string root) => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
        .Where(path =>
        {
            var relativePath = Path.GetRelativePath(root, path);
            return !relativePath.StartsWith($"bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                   && !relativePath.StartsWith($"obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
        })
        .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
        .ToArray();

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root containing AGENTS.md was not found.");
    }
}
