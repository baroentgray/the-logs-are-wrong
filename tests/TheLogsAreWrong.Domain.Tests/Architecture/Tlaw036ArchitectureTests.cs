using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-036")]
public sealed class Tlaw036ArchitectureTests
{
    [Fact]
    public void Frozen_stage_seven_catalog_is_exact_and_caller_cannot_supply_its_event_type_or_payload()
    {
        Assert.Equal(
            [
                "FeedScheduled", "EarlyFeedRequested", "LogPlacedAtFeedGate", "LogAdmittedToIntake",
                "IntakeDeadlineStarted", "IntakeDeadlineExpired", "AutoRouteAttempted", "LineJammed",
                "RepairCompleted", "SawCycleStarted", "SawCycleCompleted", "LineNoiseChanged", "LogRouted",
                "LogWrittenOff", "ProcedureActionCompleted", "ConfirmationTestCompleted", "ContainmentRitualCompleted",
                "ContainmentStateChanged", "ConfirmationConditionUpdated", "ShiftCompleted"
            ],
            typeof(HostStageSevenEventTypes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(field => ((EventTypeId)field.GetValue(null)!).Value));

        var execute = Assert.Single(typeof(HostStageSevenEventExecutor).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), method => method.Name == "Execute");
        var parameters = execute.GetParameters();
        Assert.Equal(typeof(HostStageSevenEventExecution), execute.ReturnType);
        Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(ImmutableArray<EventId>));
        Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(IEventJournal));
        Assert.DoesNotContain(parameters, parameter =>
            parameter.ParameterType == typeof(EventTypeId) ||
            parameter.ParameterType == typeof(IDomainEventPayload) ||
            parameter.ParameterType == typeof(EventSequence) ||
            parameter.ParameterType == typeof(StateVersion) ||
            parameter.ParameterType == typeof(ShiftId) ||
            parameter.ParameterType == typeof(object) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
    }

    [Fact]
    public void Payloads_are_closed_immutable_and_stage_seven_is_not_a_dispatcher_or_identity_generator()
    {
        Assert.Empty(typeof(HostStageSevenEventPayload).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.True(typeof(HostStageSevenLogTransitionPayload).IsSealed);
        Assert.True(typeof(HostStageSevenFeedSchedulePayload).IsSealed);
        Assert.True(typeof(HostStageSevenSawCompletedPayload).IsSealed);
        Assert.True(typeof(HostStageSevenShiftCompletedPayload).IsSealed);

        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var source = File.ReadAllText(Directory.GetFiles(sourceRoot, "HostStageSevenEventExecutionContracts.cs", SearchOption.AllDirectories).Single());
        Assert.Contains("JournaledMutationCommitService", source, StringComparison.Ordinal);
        Assert.Contains("CommitObservation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Guid.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Random", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HostStageOneCompletionExecutor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptedIntentStageExecutor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HostStageSixDerivedExecutor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderBy", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Sort(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Unity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FishNet", source, StringComparison.Ordinal);
    }
}
