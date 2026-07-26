using System.Reflection;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-011")]
public sealed class Tlaw011ArchitectureTests
{
    [Fact]
    public void TLAW_011_sources_are_cross_platform_non_vacuous_and_dependency_free()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var source = Directory.GetFiles(sourceRoot, "*JournaledMutationCommitContracts.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        var forbidden = new[]
        {
            "Yaml", "UnityEngine", "FishNet", "Steamworks", "System.IO", "DateTime", "DateTimeOffset", "Stopwatch", "Timer", "Task", "Thread.Sleep", "Environment.",
            "HostLogTransitionService", "LogTransitionExecutor", "ManualLogIntentHandler", "LineJamEntryService", "LineRepair", "ProcedureAction", "ConfirmationTest", "Containment", "QuotaSettlementService", "SawCycle", "IntakeDeadline"
        };

        Assert.Single(source);
        Assert.All(forbidden, token => Assert.DoesNotContain(source, file => file.Contains(token, StringComparison.Ordinal)));
    }

    [Fact]
    public void Commit_boundary_accepts_only_the_authoritative_contract_inputs_and_draft_cannot_supply_envelope_authority()
    {
        var method = typeof(JournaledMutationCommitService).GetMethod(nameof(JournaledMutationCommitService.Commit), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;

        Assert.Equal(
            new[] { typeof(IEventJournal), typeof(ShiftRuntimeState), typeof(ShiftRuntimeState), typeof(ServerTick), typeof(DomainEventDraft) },
            method.GetParameters().Select(static parameter => parameter.ParameterType));
        Assert.DoesNotContain(typeof(DomainEventDraft).GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
            property.PropertyType == typeof(ShiftId) ||
            property.PropertyType == typeof(EventSequence) ||
            property.PropertyType == typeof(ServerTick) ||
            property.PropertyType == typeof(StateVersion));
    }
}
