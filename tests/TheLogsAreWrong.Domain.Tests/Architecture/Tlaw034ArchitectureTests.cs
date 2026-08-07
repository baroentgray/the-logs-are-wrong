using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-034")]
public sealed class Tlaw034ArchitectureTests
{
    private static readonly string[] OrderedCallSites =
    {
        "_initialFeedPlanningService.Plan",
        "_repairPendingTransitionExecutionService.Execute",
        "_repairFeedGateIntakeDeadlineStartService.Start",
        "_repairAutoFeedNormalFeedPlanningService.Plan",
        "_defaultIntakeAutoRouteService.Attempt",
        "_normalFeedPlanningService.Plan",
        "_feedDueResolutionService.Resolve",
        "_intakeDeadlineStartService.Start"
    };

    [Fact]
    public void Stage_five_composition_preserves_the_frozen_seven_host_stages_with_feed_and_auto_routes_fifth()
    {
        Assert.Equal(new[]
        {
            HostTickStage.hold_and_procedure_completions,
            HostTickStage.accepted_intents_by_server_receive_sequence,
            HostTickStage.deadline_expirations,
            HostTickStage.saw_transitions,
            HostTickStage.feed_and_auto_routes,
            HostTickStage.derived_states,
            HostTickStage.event_emission
        }, HostTickStages.CanonicalOrder);
        Assert.Equal(HostTickStage.feed_and_auto_routes, HostTickStages.CanonicalOrder[4]);
    }

    [Fact]
    public void Stage_five_source_is_dependency_free_and_non_vacuously_scanned()
    {
        var source = ReadSource();
        var forbidden = new[]
        {
            "StateVersion",
            "FeedGateJamDerivationService", "IntakeAutoFeedJamDerivationService",
            "MovementNoise", "LineNoise", "ConfirmationTestCondition",
            "HostTickCompletionCheckpoint", "ShiftCompletion", "Checkpoint",
            "EventEnvelope", "EventSequence", "IEventJournal", "Journal", "Append", "Snapshot", "Replay",
            "HostStageOneCompletionExecutor", "AcceptedIntentStageExecutor", "HostStageThreeDeadlineExecutor", "HostStageFourSawExecutor",
            "Sort", "OrderBy", "ThenBy", "GroupBy",
            "IServiceProvider", "ServiceProvider", "Registry", "Activator", "Reflection", "GetMethod",
            "Func<", "Action<", "Dictionary<", "EarlyFeed",
            "HostTickStage",
            "Random", "DateTime", "Stopwatch", "Timer", "Thread", "Task", "Environment.", "File.", "Directory.",
            "Yaml", "Unity", "FishNet", "Steam", "Connection", "Network"
        };

        Assert.Contains("HostStageFiveFeedExecutor", source, StringComparison.Ordinal);
        Assert.All(OrderedCallSites, token => Assert.Contains(token, source, StringComparison.Ordinal));
        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
    }

    [Fact]
    public void Stage_five_source_calls_each_approved_service_once_in_the_bounded_order()
    {
        var source = ReadSource();

        var indices = new int[OrderedCallSites.Length];
        for (var i = 0; i < OrderedCallSites.Length; i++)
        {
            Assert.Equal(1, source.Split(OrderedCallSites[i]).Length - 1);
            indices[i] = source.IndexOf(OrderedCallSites[i], StringComparison.Ordinal);
            Assert.True(indices[i] >= 0, $"Missing call site {OrderedCallSites[i]}.");
        }

        for (var i = 1; i < indices.Length; i++)
        {
            Assert.True(indices[i - 1] < indices[i], $"Call site {OrderedCallSites[i]} must follow {OrderedCallSites[i - 1]}.");
        }
    }

    [Fact]
    public void Public_executor_consumes_stage_evidence_without_a_shift_runtime_state_parameter()
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var execute = Assert.Single(typeof(HostStageFiveFeedExecutor).GetMethods(flags), method => method.Name == "Execute");
        var parameterTypes = execute.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.Equal(typeof(HostStageFiveFeedExecution), execute.ReturnType);
        Assert.Contains(typeof(HostStageOneCompletionExecution), parameterTypes);
        Assert.Contains(typeof(AcceptedIntentStageExecution), parameterTypes);
        Assert.Contains(typeof(HostStageThreeDeadlineExecution), parameterTypes);
        Assert.Contains(typeof(HostStageFourSawExecution), parameterTypes);
        Assert.DoesNotContain(execute.GetParameters(), parameter =>
            parameter.ParameterType == typeof(ShiftRuntimeState) ||
            parameter.ParameterType == typeof(object) ||
            parameter.ParameterType == typeof(bool) ||
            parameter.ParameterType == typeof(string) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
        Assert.DoesNotContain(
            typeof(HostStageFiveFeedExecutor).GetMethods(flags),
            method => method.Name is "Dispatch" or "Register" or "Add" or "Configure");
    }

    [Fact]
    public void Result_is_sealed_immutable_and_non_publicly_constructible()
    {
        var publicInstance = BindingFlags.Public | BindingFlags.Instance;
        var type = typeof(HostStageFiveFeedExecution);

        Assert.True(type.IsSealed);
        Assert.Empty(type.GetConstructors(publicInstance));
        Assert.Empty(type.GetFields(publicInstance));
        Assert.All(type.GetProperties(publicInstance), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void Domain_assembly_remains_zero_package()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var csproj = File.ReadAllText(Path.Combine(sourceRoot, "TheLogsAreWrong.Domain.csproj"));

        Assert.DoesNotContain("<PackageReference", csproj, StringComparison.Ordinal);
    }

    private static string ReadSource()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var sourcePath = Directory.GetFiles(sourceRoot, "HostStageFiveFeedExecutionContracts.cs", SearchOption.AllDirectories).Single();
        return File.ReadAllText(sourcePath);
    }
}
