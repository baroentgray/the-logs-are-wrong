using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-030")]
public sealed class Tlaw030ArchitectureTests
{
    [Fact]
    public void Stage_two_execution_preserves_the_frozen_seven_host_stages_with_accepted_intents_at_stage_two()
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
        Assert.Equal(HostTickStage.accepted_intents_by_server_receive_sequence, HostTickStages.CanonicalOrder[1]);
    }

    [Fact]
    public void Stage_two_execution_source_is_dependency_free_and_non_vacuously_scanned()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var sourcePath = Directory.GetFiles(sourceRoot, "AcceptedIntentStageExecutionContracts.cs", SearchOption.AllDirectories).Single();
        var source = File.ReadAllText(sourcePath);
        var forbidden = new[]
        {
            "ServerReceiveSequence", "Sort", "OrderBy", "ThenBy", "GroupBy", "CompareTo",
            "IEventJournal", "EventEnvelope", "EventSequence", "Journal", "Append", "Commit", "Snapshot",
            "Dispatch", "Registry", "IServiceProvider", "ServiceProvider", "delegate", "Func<", "Action<", "Dictionary<",
            "NormalFeedPlanning", "InitialFeedPlanning",
            "Random", "DateTime", "Stopwatch", "Timer", "Thread", "Task", "Environment.", "File.", "Directory.",
            "Yaml", "Unity", "FishNet", "Steam", "Connection", "Network", "object"
        };

        Assert.Contains("AcceptedIntentStageExecutor", source, StringComparison.Ordinal);
        Assert.Contains("AcceptedIntentStageOutcome", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HostTickStage", source, StringComparison.Ordinal);
        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
        Assert.DoesNotContain("<PackageReference", File.ReadAllText(Path.Combine(sourceRoot, "TheLogsAreWrong.Domain.csproj")), StringComparison.Ordinal);
    }

    [Fact]
    public void Public_executor_surface_accepts_only_state_batch_and_configuration_and_offers_no_dispatch_registry()
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var execute = Assert.Single(typeof(AcceptedIntentStageExecutor).GetMethods(flags), method => method.Name == "Execute");

        Assert.Equal(typeof(AcceptedIntentStageExecution), execute.ReturnType);
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(AcceptedIntentTickBatch), typeof(SchedulerConfiguration) },
            execute.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(execute.GetParameters(), parameter =>
            parameter.ParameterType == typeof(object) ||
            parameter.ParameterType == typeof(bool) ||
            parameter.ParameterType == typeof(string) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
        // The only public instance behavior is Execute; there is no host dispatcher, registry or DI seam.
        Assert.DoesNotContain(
            typeof(AcceptedIntentStageExecutor).GetMethods(flags),
            method => method.Name is "Dispatch" or "Register" or "Route" or "Add" or "Configure");
    }

    [Fact]
    public void Outcome_family_is_closed_via_a_private_protected_root_and_sealed_public_kinds()
    {
        var publicInstance = BindingFlags.Public | BindingFlags.Instance;
        var root = typeof(AcceptedIntentStageOutcome);
        var kinds = new[]
        {
            typeof(ManualRoutingIntentStageOutcome),
            typeof(EarlyFeedIntentStageOutcome),
            typeof(UnsupportedIntentStageOutcome)
        };
        var constructor = Assert.Single(root.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));

        Assert.True(root.IsClass);
        Assert.True(root.IsAbstract);
        Assert.False(root.IsSealed);
        Assert.True(constructor.IsFamilyAndAssembly);
        Assert.False(constructor.IsFamily);
        Assert.False(constructor.IsPublic);
        Assert.Equal(typeof(ShiftRuntimeState), root.GetProperty(nameof(AcceptedIntentStageOutcome.State))!.PropertyType);
        Assert.All(kinds, type =>
        {
            Assert.True(type.IsClass);
            Assert.True(type.IsPublic);
            Assert.True(type.IsSealed);
            Assert.Same(root, type.BaseType);
            Assert.Empty(type.GetConstructors(publicInstance));
        });
    }

    [Fact]
    public void Step_and_stage_result_are_immutable_with_non_public_construction()
    {
        var publicInstance = BindingFlags.Public | BindingFlags.Instance;
        var step = typeof(AcceptedIntentStageStep);
        var result = typeof(AcceptedIntentStageExecution);

        Assert.True(step.IsSealed);
        Assert.True(result.IsSealed);
        Assert.Empty(step.GetConstructors(publicInstance));
        Assert.Empty(result.GetConstructors(publicInstance));
        Assert.Equal(
            typeof(ImmutableArray<AcceptedIntentStageStep>),
            result.GetProperty(nameof(AcceptedIntentStageExecution.Steps))!.PropertyType);
        Assert.All(
            new[]
            {
                typeof(AcceptedIntentStageOutcome),
                typeof(ManualRoutingIntentStageOutcome),
                typeof(EarlyFeedIntentStageOutcome),
                typeof(UnsupportedIntentStageOutcome),
                step,
                result
            },
            type =>
            {
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
}
