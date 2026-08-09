using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-041")]
public sealed class Tlaw041ArchitectureTests
{
    [Fact]
    public void Containment_ritual_intent_result_family_is_closed_to_external_derivation_and_immutable()
    {
        var root = typeof(ContainmentRitualIntentResult);
        var constructor = Assert.Single(root.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        var kinds = new[]
        {
            typeof(ContainmentRitualIntentStarted),
            typeof(ContainmentRitualIntentRejected),
            typeof(ContainmentRitualIntentUnderlyingRejected),
            typeof(ContainmentRitualIntentDuplicateIgnored),
            typeof(ContainmentRitualIntentUnsupportedAction),
            typeof(ContainmentRitualIntentUnsupportedTarget)
        };

        Assert.True(root.IsAbstract);
        Assert.True(constructor.IsFamilyAndAssembly, "The root constructor must be private protected so another assembly cannot derive a result kind.");
        Assert.Empty(root.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(typeof(ShiftRuntimeState), root.GetProperty(nameof(ContainmentRitualIntentResult.State))!.PropertyType);
        Assert.All(root.GetProperties(BindingFlags.Public | BindingFlags.Instance), property => Assert.Null(property.SetMethod));
        Assert.All(kinds, type =>
        {
            Assert.True(type.IsSealed);
            Assert.Same(root, type.BaseType);
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        });
    }

    [Fact]
    public void Public_boundary_accepts_only_existing_authoritative_evidence()
    {
        var handle = Assert.Single(typeof(ContainmentRitualIntentHandler).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), method => method.Name == "Handle");
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(IntentEnvelope), typeof(ActorId?), typeof(ServerTick), typeof(ContainmentConfiguration) },
            handle.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(handle.GetParameters(), parameter =>
            parameter.ParameterType == typeof(ContainmentRuntimeState) ||
            parameter.ParameterType == typeof(ActiveContainmentRitual) ||
            parameter.ParameterType == typeof(ContainmentIncidentDescriptor) ||
            parameter.ParameterType == typeof(LogId) ||
            parameter.ParameterType == typeof(object));

        Assert.Equal(IntentActionId.From("start_containment_ritual"), ContainmentRitualIntentActions.StartContainmentRitual);
        Assert.Equal(TargetId.From("CONTAINMENT"), ContainmentRitualIntentTargets.Containment);
        Assert.Equal(7, HostTickStages.CanonicalOrder.Count);
    }

    [Fact]
    public void Stage_two_and_seven_close_containment_ritual_outcomes_without_later_stage_or_replay_expansion()
    {
        var stageTwo = ReadSource("AcceptedIntentStageExecutionContracts.cs");
        Assert.Contains("nine owned stage-2 action IDs", stageTwo, StringComparison.Ordinal);
        Assert.Contains("ContainmentRitualIntentHandler", stageTwo, StringComparison.Ordinal);
        Assert.Contains("ContainmentRitualIntentStageOutcome", stageTwo, StringComparison.Ordinal);
        Assert.Contains("ContainmentConfiguration", stageTwo, StringComparison.Ordinal);

        var composer = ReadSource("HostTickExecutionContracts.cs");
        Assert.Contains("_stageTwo.Execute", composer, StringComparison.Ordinal);
        Assert.Contains("containmentConfiguration", composer, StringComparison.Ordinal);

        var stageSeven = ReadSource("HostStageSevenEventExecutionContracts.cs");
        Assert.Contains("HostStageSevenEventTypes.ContainmentRitualStarted", stageSeven, StringComparison.Ordinal);
        Assert.Contains("ContainmentRitualIntentStageOutcome", stageSeven, StringComparison.Ordinal);
        Assert.Contains("ContainmentRitualIntentStarted", stageSeven, StringComparison.Ordinal);
        Assert.Contains("ContainmentRitualIntentUnderlyingRejected", stageSeven, StringComparison.Ordinal);
        Assert.Contains("HostStageSevenContainmentRitualStartedPayload", stageSeven, StringComparison.Ordinal);
        Assert.Contains("left.ContainmentDeadlineAt == right.ContainmentDeadlineAt", stageSeven, StringComparison.Ordinal);
        Assert.Contains("left.RitualDueAt == right.RitualDueAt", stageSeven, StringComparison.Ordinal);
        Assert.DoesNotContain("ContainmentRitualCancellationService", ReadSource("ContainmentRitualIntentHandler.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("ContainmentRitualIntentHandler", ReadSource("HostStageOneCompletionExecutionContracts.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("ContainmentRitualIntentHandler", ReadSource("HostStageThreeDeadlineExecutionContracts.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("Effect", stageSeven, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayReducer", stageSeven, StringComparison.Ordinal);
        Assert.DoesNotContain("Snapshot", stageSeven, StringComparison.Ordinal);
        Assert.DoesNotContain("Unity", stageSeven, StringComparison.Ordinal);
        Assert.DoesNotContain("FishNet", stageSeven, StringComparison.Ordinal);
    }

    private static string ReadSource(string fileName)
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        return File.ReadAllText(Assert.Single(Directory.GetFiles(sourceRoot, fileName, SearchOption.AllDirectories)));
    }
}
