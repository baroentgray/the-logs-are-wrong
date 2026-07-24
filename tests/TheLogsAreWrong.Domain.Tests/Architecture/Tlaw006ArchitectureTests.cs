using System.Reflection;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-006")]
public sealed class Tlaw006ArchitectureTests
{
    [Fact]
    public void TLAW_006_runtime_sources_are_cross_platform_non_vacuous_and_dependency_free()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var source = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(sourceRoot, path)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment == "Runtime") &&
                Path.GetFileName(path) is "ShiftRuntimeState.cs" or "ProcedureCompletionContracts.cs")
            .Select(File.ReadAllText)
            .ToArray();
        var forbidden = new[]
        {
            "Yaml", "UnityEngine", "FishNet", "Steamworks", "System.IO", "DateTime", "DateTimeOffset", "Stopwatch", "Timer", "Task", "Thread.Sleep", "Environment.",
            "QuotaSettlementService.Apply", "HostLogTransitionService", "LogTransitionExecutor", "ManualLogIntentHandler", "IEventJournal", "EffectExecutor"
        };

        Assert.Equal(2, source.Length);
        Assert.All(forbidden, token => Assert.DoesNotContain(source, file => file.Contains(token, StringComparison.Ordinal)));
    }

    [Fact]
    public void Completion_boundary_has_no_authority_event_quota_or_transition_service_parameter()
    {
        var methods = typeof(ItemActionCompletionService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var forbiddenTypes = new[] { typeof(ActorId), typeof(IntentEnvelope), typeof(IEventJournal), typeof(QuotaRuntimeState) };

        Assert.Single(methods);
        Assert.All(forbiddenTypes, type => Assert.DoesNotContain(methods, method => method.GetParameters().Any(parameter => parameter.ParameterType == type)));
    }
}
