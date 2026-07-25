using System.Reflection;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-008")]
public sealed class Tlaw008ArchitectureTests
{
    [Fact]
    public void TLAW_008_sources_are_cross_platform_non_vacuous_and_dependency_free()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var source = Directory.GetFiles(sourceRoot, "*ConfirmationTest*Contracts.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        var forbidden = new[]
        {
            "Yaml", "UnityEngine", "FishNet", "Steamworks", "System.IO", "DateTime", "DateTimeOffset", "Stopwatch", "Timer", "Task", "Thread.Sleep", "Environment.",
            "HostLogTransitionService", "LogTransitionExecutor", "ManualLogIntentHandler", "IEventJournal", "QuotaSettlementService.Apply", "EffectExecutor"
        };

        Assert.Equal(2, source.Length);
        Assert.All(forbidden, token => Assert.DoesNotContain(source, file => file.Contains(token, StringComparison.Ordinal)));
    }

    [Fact]
    public void Confirmation_boundaries_expose_no_authority_event_quota_or_caller_supplied_completion_identity()
    {
        var types = new[] { typeof(ConfirmationTestStartService), typeof(ConfirmationTestConditionService), typeof(ConfirmationTestDueCompletionService), typeof(ConfirmationTestCancellationService) };
        var methods = types.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        var forbidden = new[] { typeof(ActorId), typeof(IntentEnvelope), typeof(IEventJournal), typeof(QuotaRuntimeState), typeof(EventEnvelope) };
        var due = typeof(ConfirmationTestDueCompletionService).GetMethod(nameof(ConfirmationTestDueCompletionService.CompleteDue));

        Assert.All(forbidden, type => Assert.DoesNotContain(methods, method => method.GetParameters().Any(parameter => parameter.ParameterType == type)));
        Assert.NotNull(due);
        Assert.DoesNotContain(due!.GetParameters(), parameter => parameter.ParameterType == typeof(LogId) || parameter.ParameterType == typeof(AnomalyId) || parameter.ParameterType == typeof(ItemId) || parameter.ParameterType == typeof(string));
    }
}
