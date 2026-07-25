using System.Reflection;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-007")]
public sealed class Tlaw007ArchitectureTests
{
    [Fact]
    public void TLAW_007_lifecycle_source_is_cross_platform_non_vacuous_and_dependency_free()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var source = Directory.GetFiles(sourceRoot, "ProcedureActionLifecycleContracts.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        var forbidden = new[]
        {
            "Yaml", "UnityEngine", "FishNet", "Steamworks", "System.IO", "DateTime", "DateTimeOffset", "Stopwatch", "Timer", "Task", "Thread.Sleep", "Environment.",
            "QuotaSettlementService.Apply", "HostLogTransitionService", "LogTransitionExecutor", "ManualLogIntentHandler", "IEventJournal", "EffectExecutor"
        };

        Assert.Single(source);
        Assert.All(forbidden, token => Assert.DoesNotContain(source, file => file.Contains(token, StringComparison.Ordinal)));
    }

    [Fact]
    public void Lifecycle_boundaries_expose_no_authority_event_quota_or_transition_parameter()
    {
        var methods = new[]
        {
            typeof(ProcedureActionStartService),
            typeof(ProcedureActionCancellationService),
            typeof(ProcedureActionDueCompletionService)
        }.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        var forbiddenTypes = new[] { typeof(ActorId), typeof(IntentEnvelope), typeof(IEventJournal), typeof(QuotaRuntimeState), typeof(EventEnvelope) };

        Assert.NotEmpty(methods);
        Assert.All(forbiddenTypes, type => Assert.DoesNotContain(methods, method => method.GetParameters().Any(parameter => parameter.ParameterType == type)));
    }

    [Fact]
    public void Due_completion_accepts_no_caller_supplied_log_or_item_identity()
    {
        var method = typeof(ProcedureActionDueCompletionService).GetMethod(nameof(ProcedureActionDueCompletionService.CompleteDue));

        Assert.NotNull(method);
        Assert.DoesNotContain(method!.GetParameters(), parameter => parameter.ParameterType == typeof(LogId) || parameter.ParameterType == typeof(ItemId));
    }
}
