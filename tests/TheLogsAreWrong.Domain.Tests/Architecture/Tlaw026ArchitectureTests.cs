using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-026")]
public sealed class Tlaw026ArchitectureTests
{
    [Fact]
    public void Confirmation_line_noise_integration_is_non_vacuous_domain_pure_and_has_no_orchestration_dependencies()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var sourcePath = Directory.GetFiles(sourceRoot, "ConfirmationTestLifecycleContracts.cs", SearchOption.AllDirectories).Single();
        var source = File.ReadAllText(sourcePath);
        var forbidden = new[]
        {
            "IEventJournal", "EventEnvelope", "EventSequence", "Append", "Commit", "Dispatch", "EffectExecutor", "ShiftCompletion", "Unity", "FishNet", "Steam", "Yaml",
            "DateTime", "Stopwatch", "Timer", "Random", "Task", "Thread", "Environment.", "File.", "Directory."
        };

        Assert.Contains("LineNoiseRuntimeState", source, StringComparison.Ordinal);
        Assert.Contains("LineNoiseEvaluationResult", source, StringComparison.Ordinal);
        Assert.Contains("LineNoiseDerivationService.ValidateRuntime", source, StringComparison.Ordinal);
        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
    }

    [Fact]
    public void Public_confirmation_surface_accepts_only_retained_runtime_or_exact_evaluation_evidence()
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var start = Assert.Single(typeof(ConfirmationTestStartService).GetMethods(flags), method => method.Name == "Start");
        var update = Assert.Single(typeof(ConfirmationTestConditionService).GetMethods(flags), method => method.Name == "Update");

        Assert.Equal(typeof(ConfirmationTestStartResult), start.ReturnType);
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(LogId), typeof(ImmutableHashSet<ItemId>), typeof(ServerTick), typeof(LineNoiseRuntimeState), typeof(AnomalyCatalog) },
            start.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(ConfirmationTestConditionResult), update.ReturnType);
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(ServerTick), typeof(LineNoiseEvaluationResult), typeof(ImmutableHashSet<ItemId>), typeof(AnomalyCatalog) },
            update.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(start.GetParameters().Concat(update.GetParameters()), parameter => parameter.ParameterType == typeof(TheLogsAreWrong.Domain.Enums.LineNoise));
        Assert.DoesNotContain(typeof(ConfirmationTestStartService).GetMethods(flags), method => method.Name is "Dispatch" or "Commit" or "Append");
        Assert.DoesNotContain(typeof(ConfirmationTestConditionService).GetMethods(flags), method => method.Name is "Dispatch" or "Commit" or "Append");
    }

    [Fact]
    public void Public_line_noise_result_types_are_observable_but_cannot_be_constructed_or_fabricated_by_callers()
    {
        var resultTypes = new[]
        {
            typeof(LineNoiseEvaluatedWithoutChange),
            typeof(LineNoiseEvaluatedWithChange),
            typeof(LineNoiseAlreadyEvaluated)
        };
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        Assert.All(resultTypes, type => Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)));
        Assert.All(resultTypes, type => Assert.All(type.GetProperties(flags), property => Assert.Null(property.SetMethod)));
        Assert.Single(typeof(LineNoiseDerivationService).GetMethods(flags), method => method.Name == "Evaluate");
        Assert.DoesNotContain(typeof(LineNoiseDerivationService).GetMethods(flags), method => method.Name is "Create" or "From" or "WithChange" or "WithoutChange");
    }
}
