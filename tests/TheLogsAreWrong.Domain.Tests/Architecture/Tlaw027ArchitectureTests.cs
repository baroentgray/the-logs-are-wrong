using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-027")]
public sealed class Tlaw027ArchitectureTests
{
    [Fact]
    public void Completion_lifecycle_is_non_vacuous_dependency_free_and_has_no_host_or_wall_clock_expansion()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var sourcePath = Directory.GetFiles(sourceRoot, "ShiftCompletionContracts.cs", SearchOption.AllDirectories).Single();
        var source = File.ReadAllText(sourcePath);
        var forbidden = new[]
        {
            "IEventJournal", "EventEnvelope", "EventSequence", "Append", "Commit", "Dispatch", "Effect", "Yaml", "Unity", "FishNet", "Steam",
            "DateTime", "Stopwatch", "Timer", "Random", "Task", "Thread", "Environment.", "File.", "Directory.", "Scheduler", "LineNoise"
        };

        Assert.Contains("ShiftLifecycleRuntimeState", source, StringComparison.Ordinal);
        Assert.Contains("ShiftCompletionEvaluationService", source, StringComparison.Ordinal);
        Assert.Contains("SetEquals", source, StringComparison.Ordinal);
        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
        Assert.DoesNotContain("<PackageReference", File.ReadAllText(Path.Combine(sourceRoot, "TheLogsAreWrong.Domain.csproj")), StringComparison.Ordinal);
    }

    [Fact]
    public void Public_surface_accepts_only_source_derived_runtime_and_configuration_evidence()
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var create = Assert.Single(typeof(ShiftLifecycleRuntimeState).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly), method => method.Name == "Create");
        var evaluate = Assert.Single(typeof(ShiftCompletionEvaluationService).GetMethods(flags), method => method.Name == "Evaluate");

        Assert.Equal(new[] { typeof(ShiftConfiguration), typeof(ProfileId) }, create.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(ShiftCompletionEvaluationResult), evaluate.ReturnType);
        Assert.Equal(
            new[] { typeof(ShiftLifecycleRuntimeState), typeof(ShiftRuntimeState), typeof(QuotaRuntimeState), typeof(ServerTick), typeof(ShiftConfiguration) },
            evaluate.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(evaluate.GetParameters(), parameter => parameter.ParameterType == typeof(bool) || parameter.ParameterType == typeof(string));
        Assert.DoesNotContain(typeof(ShiftCompletionEvaluationService).GetMethods(flags), method => method.Name is "Dispatch" or "Commit" or "Append" or "Apply");
    }

    [Fact]
    public void Lifecycle_evidence_and_result_types_are_immutable_and_cannot_be_publicly_fabricated()
    {
        var publicInstance = BindingFlags.Public | BindingFlags.Instance;
        var resultTypes = new[] { typeof(ShiftCompletionActive), typeof(ShiftCompletionNewlyCompleted), typeof(ShiftCompletionAlreadyCompleted) };

        Assert.Empty(typeof(ShiftLifecycleRuntimeState).GetConstructors(publicInstance));
        Assert.Empty(typeof(ShiftCompletionEvidence).GetConstructors(publicInstance));
        Assert.All(resultTypes, type => Assert.Empty(type.GetConstructors(publicInstance)));
        Assert.All(
            new[] { typeof(ShiftLifecycleRuntimeState), typeof(ShiftCompletionEvidence), typeof(ShiftQuotaProgressSummary), typeof(ShiftCompletionEvaluationResult) },
            type => Assert.All(type.GetProperties(publicInstance), property => Assert.Null(property.SetMethod)));
    }
}
