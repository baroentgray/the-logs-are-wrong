using System.Collections.Immutable;
using System.Reflection;
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

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-037")]
public sealed class Tlaw037ArchitectureTests
{
    private static readonly string[] FrozenCallSites =
    {
        "_stageOne.Execute",
        "_stageTwo.Execute",
        "_stageThree.Execute",
        "_stageFour.Execute",
        "_stageFive.Execute",
        "_stageSix.Execute",
        "_stageSeven.Execute"
    };

    [Fact]
    public void Canonical_host_order_remains_exactly_the_frozen_seven_stages()
    {
        Assert.Equal(
            [
                HostTickStage.hold_and_procedure_completions,
                HostTickStage.accepted_intents_by_server_receive_sequence,
                HostTickStage.deadline_expirations,
                HostTickStage.saw_transitions,
                HostTickStage.feed_and_auto_routes,
                HostTickStage.derived_states,
                HostTickStage.event_emission
            ],
            HostTickStages.CanonicalOrder);
        Assert.Equal(7, HostTickStages.CanonicalOrder.Count);
    }

    [Fact]
    public void Composer_is_the_single_narrow_orchestration_boundary_with_no_caller_supplied_stage_or_profile()
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var execute = Assert.Single(typeof(HostTickExecutionService).GetMethods(flags), method => method.Name == "Execute");
        var parameters = execute.GetParameters();

        Assert.Equal(typeof(HostStageSevenEventExecution), execute.ReturnType);
        Assert.Equal(14, parameters.Length);
        Assert.DoesNotContain(parameters, parameter =>
            parameter.ParameterType == typeof(ShiftProfile) ||
            parameter.ParameterType == typeof(IntentEnvelope) ||
            parameter.ParameterType == typeof(AuthoritativeAcceptedIntent) ||
            parameter.ParameterType == typeof(EventTypeId) ||
            parameter.ParameterType == typeof(HostStageOneCompletionExecution) ||
            parameter.ParameterType == typeof(AcceptedIntentStageExecution) ||
            parameter.ParameterType == typeof(HostStageThreeDeadlineExecution) ||
            parameter.ParameterType == typeof(HostStageFourSawExecution) ||
            parameter.ParameterType == typeof(HostStageFiveFeedExecution) ||
            parameter.ParameterType == typeof(HostStageSixDerivedExecution) ||
            parameter.ParameterType == typeof(object) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
        Assert.DoesNotContain(typeof(HostTickExecutionService).GetMethods(flags), method =>
            method.Name is "Dispatch" or "Register" or "Configure" or "Replay" or "Restore");
    }

    [Fact]
    public void Composer_source_is_non_vacuously_scanned_for_exact_order_and_absence_of_low_level_gameplay_calls()
    {
        var source = ReadSource();
        var indices = FrozenCallSites.Select(callSite => source.IndexOf(callSite, StringComparison.Ordinal)).ToArray();

        Assert.All(indices, index => Assert.True(index >= 0));
        Assert.All(FrozenCallSites, callSite => Assert.Equal(1, CountOccurrences(source, callSite)));
        for (var index = 1; index < indices.Length; index++)
        {
            Assert.True(indices[index - 1] < indices[index], $"{FrozenCallSites[index]} must follow {FrozenCallSites[index - 1]}.");
        }

        Assert.Contains("TryGetValue(lifecycle.SelectedProfileId, out var selectedProfile)", source, StringComparison.Ordinal);
        Assert.Contains("return _stageSeven.Execute", source, StringComparison.Ordinal);
        Assert.All(
            new[]
            {
                "ProcedureActionDueCompletionService", "ConfirmationTestDueCompletionService", "ContainmentRitualCompletionService",
                "LineRepairDueCompletionService", "ManualLogIntentHandler", "EarlyFeedIntentHandler",
                "IntakeDeadlineExpirationService", "ContainmentAdvanceService", "SawCycleCompletionService",
                "SawQuotaApplicationService", "SawCycleStartService", "InitialFeedPlanningService",
                "DefaultIntakeAutoRouteService", "MovementNoiseApplicationService", "HostTickCompletionCheckpointService",
                "JournaledMutationCommitService", "EventEnvelope", "Append(", "Snapshot", "Replay", "Persistence",
                "HostGameState", "HostRuntimeAggregate", "Dictionary<", "Registry", "IServiceProvider", "Activator",
                "OrderBy", ".Sort(", "Guid.", "Random", "DateTime", "Stopwatch", "Timer", "Thread", "Task",
                "Environment.", "File.", "Directory.", "Yaml", "Unity", "FishNet", "Steam", "Network"
            },
            forbidden => Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal));
    }

    [Fact]
    public void Result_reuses_existing_stage_seven_taxonomy_and_domain_remains_zero_package()
    {
        Assert.True(typeof(HostStageSevenPublished).IsSealed);
        Assert.True(typeof(HostStageSevenBlocked).IsSealed);
        Assert.True(typeof(HostStageSevenAlreadyPublished).IsSealed);
        Assert.True(typeof(HostStageSevenNoNewPublication).IsSealed);

        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var contracts = Directory.GetFiles(sourceRoot, "HostTickExecutionContracts.cs", SearchOption.AllDirectories);
        Assert.Single(contracts);
        var project = File.ReadAllText(Path.Combine(sourceRoot, "TheLogsAreWrong.Domain.csproj"));
        Assert.DoesNotContain("<PackageReference", project, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string token) => source.Split(token).Length - 1;

    private static string ReadSource()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var sourcePath = Directory.GetFiles(sourceRoot, "HostTickExecutionContracts.cs", SearchOption.AllDirectories).Single();
        return File.ReadAllText(sourcePath);
    }
}
