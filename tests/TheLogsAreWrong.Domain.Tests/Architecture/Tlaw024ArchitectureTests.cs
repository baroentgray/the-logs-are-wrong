using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-024")]
public sealed class Tlaw024ArchitectureTests
{
    [Fact]
    public void Movement_noise_source_is_non_vacuous_cross_platform_domain_pure_and_has_no_broadened_dependencies()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var sourcePath = Directory.GetFiles(sourceRoot, "MovementNoiseRuntimeContracts.cs", SearchOption.AllDirectories).Single();
        var source = File.ReadAllText(sourcePath);
        var forbidden = new[]
        {
            "LineNoiseChanged", "Confirmation", "EffectExecutor", "ShiftCompletion", "Dispatcher", "IEventJournal", "EventEnvelope", "Snapshot", "Replay",
            "DateTime", "Stopwatch", "Timer", "Random", "Task", "Thread", "Environment.", "File.", "Directory.", "Unity", "FishNet", "Steam", "Yaml"
        };

        Assert.Contains("MovementNoiseApplicationService", source, StringComparison.Ordinal);
        Assert.Contains("MovementNoiseRuntimeState", source, StringComparison.Ordinal);
        Assert.Contains("SchedulerConfiguration", source, StringComparison.Ordinal);
        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));

        var project = File.ReadAllText(Path.Combine(sourceRoot, "TheLogsAreWrong.Domain.csproj"));
        Assert.DoesNotContain("<PackageReference", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_application_surface_is_closed_to_exact_accepted_results_runtime_and_configuration()
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var applies = typeof(MovementNoiseApplicationService).GetMethods(flags).Where(method => method.Name == "Apply").ToArray();
        var expectedSignatures = new[]
        {
            new[] { typeof(MovementNoiseRuntimeState), typeof(ManualLogIntentAccepted), typeof(ServerTick), typeof(SchedulerConfiguration) },
            new[] { typeof(MovementNoiseRuntimeState), typeof(HostLogTransitionAccepted), typeof(ServerTick), typeof(SchedulerConfiguration) },
            new[] { typeof(MovementNoiseRuntimeState), typeof(FeedDueResolved), typeof(SchedulerConfiguration) },
            new[] { typeof(MovementNoiseRuntimeState), typeof(DefaultIntakeAutoRouteApplied), typeof(SchedulerConfiguration) },
            new[] { typeof(MovementNoiseRuntimeState), typeof(RepairPendingTransitionExecuted), typeof(SchedulerConfiguration) },
            new[] { typeof(MovementNoiseRuntimeState), typeof(SawCycleStarted), typeof(SchedulerConfiguration) },
            new[] { typeof(MovementNoiseRuntimeState), typeof(SawCycleCompleted), typeof(SchedulerConfiguration) }
        };
        var actualSignatures = applies.Select(method => method.GetParameters().Select(parameter => parameter.ParameterType).ToArray()).ToArray();

        Assert.Equal(7, applies.Length);
        Assert.All(applies, method => Assert.Equal(typeof(MovementNoiseApplicationResult), method.ReturnType));
        Assert.All(expectedSignatures, expected => Assert.Contains(actualSignatures, actual => actual.SequenceEqual(expected)));
        Assert.All(actualSignatures, actual => Assert.Contains(expectedSignatures, expected => actual.SequenceEqual(expected)));
        var forbiddenRawParameters = new[] { typeof(TheLogsAreWrong.Domain.Identifiers.LogId), typeof(TheLogsAreWrong.Domain.Enums.LogState), typeof(StateVersion), typeof(int), typeof(string), typeof(object) };
        Assert.DoesNotContain(actualSignatures.SelectMany(signature => signature), parameterType =>
            forbiddenRawParameters.Contains(parameterType) ||
            (parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() is var genericType &&
                (genericType == typeof(Dictionary<,>) || genericType == typeof(IDictionary<,>) || genericType == typeof(IReadOnlyDictionary<,>))));
        Assert.DoesNotContain(typeof(MovementNoiseApplicationService).GetMethods(flags), method => method.Name is "Commit" or "Append" or "Dispatch");
        Assert.All(typeof(MovementNoiseRuntimeState).GetProperties(BindingFlags.Public | BindingFlags.Instance), property => Assert.Null(property.SetMethod));
    }
}
