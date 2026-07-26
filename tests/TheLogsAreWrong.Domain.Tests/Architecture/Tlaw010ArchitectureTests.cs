using System.Reflection;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Quota;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-010")]
public sealed class Tlaw010ArchitectureTests
{
    [Fact]
    public void TLAW_010_sources_are_cross_platform_non_vacuous_and_dependency_free()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var source = Directory.GetFiles(sourceRoot, "*LineJamRepairContracts.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        var forbidden = new[]
        {
            "Yaml", "UnityEngine", "FishNet", "Steamworks", "System.IO", "DateTime", "DateTimeOffset", "Stopwatch", "Timer", "Task", "Thread.Sleep", "Environment.",
            "HostLogTransitionService", "LogTransitionExecutor", "ManualLogIntentHandler", "IEventJournal", "EffectExecutor", "QuotaSettlementService", "SawCycle", "IntakeDeadline"
        };

        Assert.Single(source);
        Assert.All(forbidden, token => Assert.DoesNotContain(source, file => file.Contains(token, StringComparison.Ordinal)));
    }

    [Fact]
    public void Line_boundaries_expose_no_authority_event_quota_or_caller_supplied_log_identity()
    {
        var types = new[]
        {
            typeof(LineJamEntryService),
            typeof(LineRepairStartService),
            typeof(LineRepairDueCompletionService)
        };
        var methods = types.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        var forbidden = new[] { typeof(ActorId), typeof(IntentEnvelope), typeof(IEventJournal), typeof(QuotaRuntimeState), typeof(EventEnvelope), typeof(LogId), typeof(AnomalyId), typeof(ItemId), typeof(string) };

        Assert.All(forbidden, type => Assert.DoesNotContain(methods, method => method.GetParameters().Any(parameter => parameter.ParameterType == type)));
    }
}
