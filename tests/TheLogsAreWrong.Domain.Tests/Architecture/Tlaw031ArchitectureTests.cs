using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-031")]
public sealed class Tlaw031ArchitectureTests
{
    [Fact]
    public void Stage_one_composition_preserves_the_frozen_seven_host_stages_with_hold_and_procedure_completions_first()
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
        Assert.Equal(HostTickStage.hold_and_procedure_completions, HostTickStages.CanonicalOrder[0]);
    }

    [Fact]
    public void Stage_one_source_is_dependency_free_and_non_vacuously_scanned()
    {
        var source = ReadSource();
        var forbidden = new[]
        {
            "ServerReceiveSequence", "AcceptedIntent", "Sort", "OrderBy", "ThenBy", "GroupBy",
            "IServiceProvider", "ServiceProvider", "Registry", "Activator", "Reflection",
            "delegate", "Func<", "Action<", "Dictionary<",
            "StateVersion", "IEventJournal", "EventEnvelope", "EventSequence", "Journal", "Append", "Commit",
            "Checkpoint", "Replay", "Snapshot",
            "Quota", "Scheduler", "ContainmentAdvance", "ConfirmationTestCondition", "Saw",
            "FeedPlanning", "FeedDue", "AutoRoute", "IntakeDeadline", "LineNoise", "MovementNoise", "JamDerivation",
            "Dispatch", "HostTickStage",
            "Random", "DateTime", "Stopwatch", "Timer", "Thread", "Task", "Environment.", "File.", "Directory.",
            "Yaml", "Unity", "FishNet", "Steam", "Connection", "Network", "object"
        };

        Assert.Contains("HostStageOneCompletionExecutor", source, StringComparison.Ordinal);
        Assert.Contains("ProcedureActionDueCompletionService", source, StringComparison.Ordinal);
        Assert.Contains("ConfirmationTestDueCompletionService", source, StringComparison.Ordinal);
        Assert.Contains("ContainmentRitualCompletionService", source, StringComparison.Ordinal);
        Assert.Contains("LineRepairDueCompletionService", source, StringComparison.Ordinal);
        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
    }

    [Fact]
    public void Stage_one_source_calls_exactly_the_four_approved_services_in_canonical_order()
    {
        var source = ReadSource();

        Assert.Equal(4, source.Split("CompleteDue(").Length - 1);

        var procedure = source.IndexOf("_procedureService.CompleteDue", StringComparison.Ordinal);
        var confirmation = source.IndexOf("_confirmationService.CompleteDue", StringComparison.Ordinal);
        var containment = source.IndexOf("_containmentRitualService.CompleteDue", StringComparison.Ordinal);
        var lineRepair = source.IndexOf("_lineRepairService.CompleteDue", StringComparison.Ordinal);

        Assert.True(procedure >= 0 && confirmation >= 0 && containment >= 0 && lineRepair >= 0);
        Assert.True(procedure < confirmation, "Procedure completion must run before confirmation.");
        Assert.True(confirmation < containment, "Confirmation completion must run before containment ritual.");
        Assert.True(containment < lineRepair, "Containment ritual completion must run before line repair.");
    }

    [Fact]
    public void Public_executor_surface_accepts_only_state_tick_catalog_and_configuration()
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var execute = Assert.Single(typeof(HostStageOneCompletionExecutor).GetMethods(flags), method => method.Name == "Execute");

        Assert.Equal(typeof(HostStageOneCompletionExecution), execute.ReturnType);
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(ServerTick), typeof(AnomalyCatalog), typeof(ContainmentConfiguration) },
            execute.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(execute.GetParameters(), parameter =>
            parameter.ParameterType == typeof(object) ||
            parameter.ParameterType == typeof(bool) ||
            parameter.ParameterType == typeof(string) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
        Assert.DoesNotContain(
            typeof(HostStageOneCompletionExecutor).GetMethods(flags),
            method => method.Name is "Dispatch" or "Register" or "Add" or "Configure");
    }

    [Fact]
    public void Steps_and_result_are_sealed_immutable_and_non_publicly_constructible()
    {
        var publicInstance = BindingFlags.Public | BindingFlags.Instance;
        var types = new[]
        {
            typeof(HostStageOneCompletionExecution),
            typeof(ProcedureDueCompletionStageStep),
            typeof(ConfirmationDueCompletionStageStep),
            typeof(ContainmentRitualDueCompletionStageStep),
            typeof(LineRepairDueCompletionStageStep)
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
        var sourcePath = Directory.GetFiles(sourceRoot, "HostStageOneCompletionExecutionContracts.cs", SearchOption.AllDirectories).Single();
        return File.ReadAllText(sourcePath);
    }
}
