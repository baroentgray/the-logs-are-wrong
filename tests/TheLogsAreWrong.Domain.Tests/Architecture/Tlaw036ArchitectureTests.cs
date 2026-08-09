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
                "RepairStarted", "RepairCompleted", "SawCycleStarted", "SawCycleCompleted", "LineNoiseChanged", "LogRouted",
                "LogWrittenOff", "ProcedureActionStarted", "ProcedureActionCompleted", "ConfirmationTestStarted", "ConfirmationTestCompleted", "ContainmentRitualCompleted",
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
        Assert.True(typeof(HostStageSevenProcedureActionStartedPayload).IsSealed);
        Assert.True(typeof(HostStageSevenConfirmationTestStartedPayload).IsSealed);
        Assert.True(typeof(HostStageSevenRepairStartedPayload).IsSealed);
        Assert.True(typeof(HostStageSevenSawCompletedPayload).IsSealed);
        Assert.True(typeof(HostStageSevenShiftCompletedPayload).IsSealed);
        Assert.True(typeof(HostStageSevenNoNewPublication).IsSealed);
        Assert.Equal(typeof(ServerTick), typeof(HostStageSevenAutoRoutePayload).GetProperty(nameof(HostStageSevenAutoRoutePayload.AttemptedAt))!.PropertyType);
        Assert.Equal(typeof(System.Collections.Immutable.ImmutableDictionary<SpeciesId, int>), typeof(HostStageSevenShiftCompletedPayload).GetProperty(nameof(HostStageSevenShiftCompletedPayload.TargetBySpecies))!.PropertyType);
        Assert.Equal(typeof(System.Collections.Immutable.ImmutableDictionary<SpeciesId, int>), typeof(HostStageSevenShiftCompletedPayload).GetProperty(nameof(HostStageSevenShiftCompletedPayload.CreditedBySpecies))!.PropertyType);

        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var source = File.ReadAllText(Directory.GetFiles(sourceRoot, "HostStageSevenEventExecutionContracts.cs", SearchOption.AllDirectories).Single());
        Assert.Contains("JournaledMutationCommitService", source, StringComparison.Ordinal);
        Assert.Contains("CommitObservation", source, StringComparison.Ordinal);
        Assert.Contains("RequireNoNewPublicationCursor", source, StringComparison.Ordinal);
        Assert.Contains("HasExactPayloadSemantics", source, StringComparison.Ordinal);
        Assert.Contains("HostStageSevenSawQuotaOutcome", source, StringComparison.Ordinal);
        Assert.Contains("DuplicateQuotaSettlementLogId", source, StringComparison.Ordinal);
        Assert.Contains("ValidateSequenceCapacity", source, StringComparison.Ordinal);
        Assert.Contains("left.QuotaApplicationOutcome == right.QuotaApplicationOutcome", source, StringComparison.Ordinal);
        Assert.Contains("left.DuplicateQuotaSettlementLogId == right.DuplicateQuotaSettlementLogId", source, StringComparison.Ordinal);
        Assert.Contains("left.AttemptedAt == right.AttemptedAt", source, StringComparison.Ordinal);
        Assert.Contains("SameSpeciesValues(left.TargetBySpecies, right.TargetBySpecies)", source, StringComparison.Ordinal);
        Assert.Contains("SameSpeciesValues(left.CreditedBySpecies, right.CreditedBySpecies)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultIntakeAutoRouteContracts", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Guid.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Random", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HostStageOneCompletionExecutor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptedIntentStageExecutor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HostStageSixDerivedExecutor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderBy", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Sort(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Unity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FishNet", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Json", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetHashCode", source, StringComparison.Ordinal);
    }
}
