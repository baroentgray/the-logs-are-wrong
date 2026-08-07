using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-035")]
public sealed class Tlaw035ArchitectureTests
{
    private const string MovementCallSite = "_movementNoiseService.Apply";

    private static readonly string[] OrderedSingleCallSites =
    {
        "_intakeAutoFeedJamDerivationService.Derive",
        "_feedGateJamDerivationService.Derive",
        "_lineNoiseDerivationService.Evaluate",
        "_confirmationTestConditionService.Update",
        "_hostTickCompletionCheckpointService.Complete"
    };

    [Fact]
    public void Stage_six_composition_preserves_the_frozen_seven_host_stages_with_derived_states_sixth()
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
        Assert.Equal(HostTickStage.derived_states, HostTickStages.CanonicalOrder[5]);
    }

    [Fact]
    public void Stage_six_source_is_dependency_free_and_non_vacuously_scanned()
    {
        var source = ReadSource();
        var forbidden = new[]
        {
            "StateVersion",
            "ContainmentAdvanceService", "ShiftCompletionEvaluationService", "ConfirmationTestDueCompletionService",
            "HostStageOneCompletionExecutor", "AcceptedIntentStageExecutor", "HostStageThreeDeadlineExecutor",
            "HostStageFourSawExecutor", "HostStageFiveFeedExecutor",
            "EventEnvelope", "EventSequence", "IEventJournal", "Journal", "Append", "Snapshot", "Replay",
            "Sort", "OrderBy", "ThenBy", "GroupBy",
            "IServiceProvider", "ServiceProvider", "Registry", "Activator", "Reflection", "GetMethod",
            "Func<", "Action<", "Dictionary<",
            "HostTickStage",
            "Random", "DateTime", "Stopwatch", "Timer", "Thread", "Task", "Environment.", "File.", "Directory.",
            "Yaml", "Unity", "FishNet", "Steam", "Connection", "Network"
        };

        Assert.Contains("HostStageSixDerivedExecutor", source, StringComparison.Ordinal);
        Assert.All(OrderedSingleCallSites, token => Assert.Contains(token, source, StringComparison.Ordinal));
        Assert.Contains(MovementCallSite, source, StringComparison.Ordinal);
        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
    }

    [Fact]
    public void Stage_six_applies_movement_before_deriving_either_jam_family_in_the_bounded_precedence_order()
    {
        var source = ReadSource();

        // Movement application shares one call site invoked once per accepted evidence item; it must occur
        // at least six times (the six frozen stage 2/4/5 movement sources) and every occurrence must precede
        // the single-occurrence ordered call sites that follow it.
        var movementOccurrences = CountOccurrences(source, MovementCallSite);
        Assert.True(movementOccurrences >= 6, $"Expected at least 6 movement-application call sites, found {movementOccurrences}.");
        var lastMovementIndex = source.LastIndexOf(MovementCallSite, StringComparison.Ordinal);

        var indices = new int[OrderedSingleCallSites.Length];
        for (var i = 0; i < OrderedSingleCallSites.Length; i++)
        {
            Assert.Equal(1, source.Split(OrderedSingleCallSites[i]).Length - 1);
            indices[i] = source.IndexOf(OrderedSingleCallSites[i], StringComparison.Ordinal);
            Assert.True(indices[i] >= 0, $"Missing call site {OrderedSingleCallSites[i]}.");
        }

        Assert.True(lastMovementIndex < indices[0], "All movement application must precede intake-auto-feed jam derivation.");
        for (var i = 1; i < indices.Length; i++)
        {
            Assert.True(indices[i - 1] < indices[i], $"Call site {OrderedSingleCallSites[i]} must follow {OrderedSingleCallSites[i - 1]}.");
        }
    }

    [Fact]
    public void Checkpoint_call_is_textually_last_among_stage_six_service_invocations()
    {
        var source = ReadSource();
        var checkpointIndex = source.IndexOf("_hostTickCompletionCheckpointService.Complete", StringComparison.Ordinal);
        var allCallSites = OrderedSingleCallSites.Concat(new[] { MovementCallSite }).Where(token => token != "_hostTickCompletionCheckpointService.Complete");

        foreach (var token in allCallSites)
        {
            var lastIndex = source.LastIndexOf(token, StringComparison.Ordinal);
            Assert.True(lastIndex < checkpointIndex, $"Checkpoint must be textually last; {token} appears after it.");
        }
    }

    [Fact]
    public void Public_executor_consumes_stage_evidence_without_a_shift_runtime_or_quota_runtime_state_parameter()
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var execute = Assert.Single(typeof(HostStageSixDerivedExecutor).GetMethods(flags), method => method.Name == "Execute");
        var parameterTypes = execute.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.Equal(typeof(HostStageSixDerivedExecution), execute.ReturnType);
        Assert.Contains(typeof(HostStageOneCompletionExecution), parameterTypes);
        Assert.Contains(typeof(AcceptedIntentStageExecution), parameterTypes);
        Assert.Contains(typeof(HostStageThreeDeadlineExecution), parameterTypes);
        Assert.Contains(typeof(HostStageFourSawExecution), parameterTypes);
        Assert.Contains(typeof(HostStageFiveFeedExecution), parameterTypes);
        Assert.DoesNotContain(execute.GetParameters(), parameter =>
            parameter.ParameterType == typeof(ShiftRuntimeState) ||
            parameter.ParameterType == typeof(QuotaRuntimeState) ||
            parameter.ParameterType == typeof(object) ||
            parameter.ParameterType == typeof(bool) ||
            parameter.ParameterType == typeof(string) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
        Assert.DoesNotContain(
            typeof(HostStageSixDerivedExecutor).GetMethods(flags),
            method => method.Name is "Dispatch" or "Register" or "Add" or "Configure");
    }

    [Fact]
    public void Result_and_step_types_are_sealed_immutable_and_non_publicly_constructible()
    {
        var publicInstance = BindingFlags.Public | BindingFlags.Instance;
        var types = new[]
        {
            typeof(HostStageSixDerivedExecution),
            typeof(MovementNoiseApplicationStageStep),
            typeof(IntakeAutoFeedJamStageStep),
            typeof(FeedGateJamStageStep),
            typeof(LineNoiseDerivationStageStep),
            typeof(ConfirmationConditionStageStep),
            typeof(HostTickCheckpointStageStep)
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

    private static int CountOccurrences(string source, string token) => source.Split(token).Length - 1;

    private static string ReadSource()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var sourcePath = Directory.GetFiles(sourceRoot, "HostStageSixDerivedExecutionContracts.cs", SearchOption.AllDirectories).Single();
        return File.ReadAllText(sourcePath);
    }
}
