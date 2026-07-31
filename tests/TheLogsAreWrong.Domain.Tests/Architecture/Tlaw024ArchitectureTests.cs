using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Line;
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

        Assert.Equal(7, applies.Length);
        Assert.All(applies, method =>
        {
            Assert.Equal(typeof(MovementNoiseApplicationResult), method.ReturnType);
            Assert.Contains(method.GetParameters(), parameter => parameter.ParameterType == typeof(MovementNoiseRuntimeState));
            Assert.Contains(method.GetParameters(), parameter => parameter.ParameterType == typeof(SchedulerConfiguration));
        });
        Assert.Contains(applies, method => method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(new[] { typeof(MovementNoiseRuntimeState), typeof(ManualLogIntentAccepted), typeof(TheLogsAreWrong.Domain.Primitives.ServerTick), typeof(SchedulerConfiguration) }));
        Assert.Contains(applies, method => method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(new[] { typeof(MovementNoiseRuntimeState), typeof(HostLogTransitionAccepted), typeof(TheLogsAreWrong.Domain.Primitives.ServerTick), typeof(SchedulerConfiguration) }));
        Assert.DoesNotContain(typeof(MovementNoiseApplicationService).GetMethods(flags), method => method.Name is "Commit" or "Append" or "Dispatch");
        Assert.All(typeof(MovementNoiseRuntimeState).GetProperties(BindingFlags.Public | BindingFlags.Instance), property => Assert.Null(property.SetMethod));
    }
}
