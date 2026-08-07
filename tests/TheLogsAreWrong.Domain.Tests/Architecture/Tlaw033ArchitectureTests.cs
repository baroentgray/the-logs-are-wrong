using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-033")]
public sealed class Tlaw033ArchitectureTests
{
    [Fact]
    public void Stage_four_composition_preserves_the_frozen_seven_host_stages_with_saw_transitions_fourth()
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
        Assert.Equal(HostTickStage.saw_transitions, HostTickStages.CanonicalOrder[3]);
    }

    [Fact]
    public void Stage_four_source_is_dependency_free_and_non_vacuously_scanned()
    {
        var source = ReadSource();
        var forbidden = new[]
        {
            "StateVersion", "AnomalyProcessingResolver", ".Resolve(", "QuotaSettlement",
            "Sort", "OrderBy", "ThenBy", "GroupBy",
            "Registry", "IServiceProvider", "ServiceProvider", "Activator", "Reflection", "GetMethod",
            "delegate", "Func<", "Action<", "Dictionary<",
            "HostStageOneCompletionExecutor", "AcceptedIntentStageExecutor", "HostStageThreeDeadlineExecutor",
            "Checkpoint", "ShiftCompletion", "HostTickCompletion",
            "MovementNoise", "LineNoise", "IEventJournal", "EventEnvelope", "EventSequence", "Journal", "Append",
            "FeedDue", "FeedPlanning", "AutoRoute", "JamEntry", "JamDerivation", "RepairPending", "ConfirmationTestCondition",
            "Dispatch", "HostTickStage",
            "Random", "DateTime", "Stopwatch", "Timer", "Thread", "Task", "Environment.", "File.", "Directory.",
            "Yaml", "Unity", "FishNet", "Steam", "Connection", "Network"
        };

        Assert.Contains("HostStageFourSawExecutor", source, StringComparison.Ordinal);
        Assert.Contains("_sawCycleCompletionService.Complete", source, StringComparison.Ordinal);
        Assert.Contains("_sawQuotaApplicationService.Apply", source, StringComparison.Ordinal);
        Assert.Contains("_sawCycleStartService.Start", source, StringComparison.Ordinal);
        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
    }

    [Fact]
    public void Stage_four_source_calls_exactly_the_three_approved_services_in_canonical_order()
    {
        var source = ReadSource();

        Assert.Equal(1, source.Split("_sawCycleCompletionService.Complete").Length - 1);
        Assert.Equal(1, source.Split("_sawQuotaApplicationService.Apply").Length - 1);
        Assert.Equal(1, source.Split("_sawCycleStartService.Start").Length - 1);

        var completion = source.IndexOf("_sawCycleCompletionService.Complete", StringComparison.Ordinal);
        var quota = source.IndexOf("_sawQuotaApplicationService.Apply", StringComparison.Ordinal);
        var start = source.IndexOf("_sawCycleStartService.Start", StringComparison.Ordinal);
        var completedBranch = source.IndexOf("is SawCycleCompleted", StringComparison.Ordinal);

        Assert.True(completion >= 0 && quota >= 0 && start >= 0);
        Assert.True(completion < quota, "Saw completion must run before quota application.");
        Assert.True(quota < start, "Quota application must run before automatic saw start.");
        // The quota call only exists inside the SawCycleCompleted branch.
        Assert.True(completedBranch >= 0 && completedBranch < quota, "Quota application must be guarded by the SawCycleCompleted branch.");
    }

    [Fact]
    public void Public_executor_surface_accepts_only_typed_source_derived_inputs()
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var execute = Assert.Single(typeof(HostStageFourSawExecutor).GetMethods(flags), method => method.Name == "Execute");

        Assert.Equal(typeof(HostStageFourSawExecution), execute.ReturnType);
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(QuotaRuntimeState), typeof(ServerTick), typeof(SchedulerConfiguration), typeof(AnomalyCatalog) },
            execute.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(execute.GetParameters(), parameter =>
            parameter.ParameterType == typeof(object) ||
            parameter.ParameterType == typeof(bool) ||
            parameter.ParameterType == typeof(string) ||
            parameter.ParameterType == typeof(LogId) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
        Assert.DoesNotContain(
            typeof(HostStageFourSawExecutor).GetMethods(flags),
            method => method.Name is "Dispatch" or "Register" or "Add" or "Configure");
    }

    [Fact]
    public void Steps_and_result_are_sealed_immutable_and_non_publicly_constructible()
    {
        var publicInstance = BindingFlags.Public | BindingFlags.Instance;
        var types = new[]
        {
            typeof(HostStageFourSawExecution),
            typeof(SawCycleCompletionStageStep),
            typeof(SawQuotaApplicationStageStep),
            typeof(SawCycleStartStageStep)
        };

        Assert.All(types, type =>
        {
            Assert.True(type.IsSealed);
            Assert.Empty(type.GetConstructors(publicInstance));
            Assert.Empty(type.GetFields(publicInstance));
            Assert.All(type.GetProperties(publicInstance), property => Assert.Null(property.SetMethod));
        });
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
        var sourcePath = Directory.GetFiles(sourceRoot, "HostStageFourSawExecutionContracts.cs", SearchOption.AllDirectories).Single();
        return File.ReadAllText(sourcePath);
    }
}
