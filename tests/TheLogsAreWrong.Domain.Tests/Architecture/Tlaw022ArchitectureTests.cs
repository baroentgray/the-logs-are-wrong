using System.Reflection;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-022")]
public sealed class Tlaw022ArchitectureTests
{
    [Fact]
    public void Saw_cycle_source_is_non_vacuous_domain_pure_and_has_no_orchestration_dependencies()
    {
        var sourcePath = Path.Combine(FindRoot(), "src", "TheLogsAreWrong.PortableAuthority", "Scheduler", "SawCycleContracts.cs");
        Assert.True(File.Exists(sourcePath));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("SawCycleStartService", source, StringComparison.Ordinal);
        Assert.Contains("SawCycleCompletionService", source, StringComparison.Ordinal);
        Assert.Contains("AnomalyProcessingResolver", source, StringComparison.Ordinal);
        Assert.Contains("ProcessingResolution", source, StringComparison.Ordinal);
        Assert.Contains("if (state is null) { throw new ArgumentNullException(\"state\"); }", source, StringComparison.Ordinal);
        Assert.Contains("if (configuration is null) { throw new ArgumentNullException(\"configuration\"); }", source, StringComparison.Ordinal);
        Assert.Contains("if (catalog is null) { throw new ArgumentNullException(\"catalog\"); }", source, StringComparison.Ordinal);
        Assert.Contains("resolution.LogId != active.LogId || resolution.TerminalState != LogState.PROCESSED", source, StringComparison.Ordinal);
        Assert.Contains("The active saw cycle must own the only saw occupant.", source, StringComparison.Ordinal);
        Assert.All(new[]
        {
            "InitialFeedPlanningService", "NormalFeedPlanningService", "EarlyFeedIntentHandler", "FeedDue", "IntakeDeadline",
            "DefaultIntakeAutoRouteService", "IntakeAutoFeedJamDerivationService", "LineRepair", "ProcedureAction",
            "ConfirmationTest", "Containment", "LineNoise", "QuotaSettlementService", "EffectExecutor", "ShiftCompletion",
            "Dispatcher", "EventEnvelope", "IEventJournal", "Append(", "TryAppend(", "Snapshot", "Replay", "DateTime",
            "Stopwatch", "Timer", "Random", "Task", "Thread", "Environment.", "File.", "Directory.", "Unity", "FishNet", "Steam", "Yaml"
        }, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));

        var project = File.ReadAllText(Path.Combine(FindRoot(), "src", "TheLogsAreWrong.Domain", "TheLogsAreWrong.Domain.csproj"));
        Assert.DoesNotContain("<PackageReference", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_service_inputs_are_closed_to_current_state_tick_configuration_and_catalog()
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var start = Assert.Single(typeof(SawCycleStartService).GetMethods(flags));
        var completion = Assert.Single(typeof(SawCycleCompletionService).GetMethods(flags));

        Assert.Equal(new[] { typeof(ShiftRuntimeState), typeof(ServerTick), typeof(SchedulerConfiguration) }, start.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(new[] { typeof(ShiftRuntimeState), typeof(ServerTick), typeof(AnomalyCatalog) }, completion.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(typeof(ActiveSawCycle).GetProperties(BindingFlags.Public | BindingFlags.Instance), property => property.SetMethod is not null);
        Assert.Null(typeof(ShiftRuntimeState).GetProperty(nameof(ShiftRuntimeState.ActiveSawCycle))!.SetMethod);
        Assert.All(typeof(SawCycleStartService).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), property => Assert.False(property.CanWrite));
        Assert.All(typeof(SawCycleCompletionService).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), property => Assert.False(property.CanWrite));
        Assert.DoesNotContain(typeof(SawCycleStartService).GetMethods(flags), method => method.Name is "Commit" or "Append" or "Dispatch");
        Assert.DoesNotContain(typeof(SawCycleCompletionService).GetMethods(flags), method => method.Name is "Commit" or "Append" or "Dispatch");
    }

    private static string FindRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git"))) return current.FullName;
        }

        throw new DirectoryNotFoundException();
    }
}
