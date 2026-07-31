using System.Reflection;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-025")]
public sealed class Tlaw025ArchitectureTests
{
    [Fact]
    public void Line_noise_derivation_is_non_vacuous_cross_platform_domain_pure_and_has_no_broadened_dependencies()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var sourcePath = Directory.GetFiles(sourceRoot, "LineNoiseRuntimeContracts.cs", SearchOption.AllDirectories).Single();
        var source = File.ReadAllText(sourcePath);
        var forbidden = new[]
        {
            "ConfirmationTestConditionService", "ConfirmationTestLifecycle", "IEventJournal", "EventEnvelope", "EventSequence", "Append", "Commit", "Dispatch",
            "SawCycleStartService", "SawCycleCompletionService", "MovementNoiseApplicationService", "LineRepairStartService", "LineRepairDueCompletionService",
            "DateTime", "Stopwatch", "Timer", "Random", "Task", "Thread", "Environment.", "File.", "Directory.", "Unity", "FishNet", "Steam", "Yaml"
        };

        Assert.Contains("LineNoiseDerivationService", source, StringComparison.Ordinal);
        Assert.Contains("LineNoiseRuntimeState", source, StringComparison.Ordinal);
        Assert.Contains("LineNoiseChanged", source, StringComparison.Ordinal);
        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));

        var project = File.ReadAllText(Path.Combine(sourceRoot, "TheLogsAreWrong.Domain.csproj"));
        Assert.DoesNotContain("<PackageReference", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_derivation_surface_is_closed_to_exact_runtime_state_and_authoritative_tick_inputs()
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var evaluates = typeof(LineNoiseDerivationService).GetMethods(flags).Where(method => method.Name == "Evaluate").ToArray();
        var expected = new[] { typeof(LineNoiseRuntimeState), typeof(ShiftRuntimeState), typeof(MovementNoiseRuntimeState), typeof(ServerTick) };

        Assert.Single(evaluates);
        Assert.Equal(typeof(LineNoiseEvaluationResult), evaluates[0].ReturnType);
        Assert.Equal(expected, evaluates[0].GetParameters().Select(parameter => parameter.ParameterType));
        var forbidden = new[] { typeof(LineNoise), typeof(bool), typeof(int), typeof(string), typeof(object), typeof(StateVersion), typeof(TheLogsAreWrong.Domain.Time.SimulationDuration) };
        Assert.DoesNotContain(evaluates[0].GetParameters(), parameter =>
            forbidden.Contains(parameter.ParameterType) ||
            (parameter.ParameterType.IsGenericType && parameter.ParameterType.GetGenericTypeDefinition() is var generic &&
                (generic == typeof(Dictionary<,>) || generic == typeof(IDictionary<,>) || generic == typeof(IReadOnlyDictionary<,>))));
        Assert.DoesNotContain(typeof(LineNoiseDerivationService).GetMethods(flags), method => method.Name is "Append" or "Commit" or "Dispatch" or "Apply" or "Start" or "Complete");
        Assert.All(typeof(LineNoiseRuntimeState).GetProperties(BindingFlags.Public | BindingFlags.Instance), property => Assert.Null(property.SetMethod));
        Assert.All(typeof(LineNoiseChanged).GetProperties(BindingFlags.Public | BindingFlags.Instance), property => Assert.Null(property.SetMethod));
        Assert.All(typeof(LineNoiseSourceSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance), property => Assert.Null(property.SetMethod));
    }
}
