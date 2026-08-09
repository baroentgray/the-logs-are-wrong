using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-040")]
public sealed class Tlaw040ArchitectureTests
{
    [Fact]
    public void Line_repair_intent_result_family_is_closed_to_external_derivation_and_immutable()
    {
        var root = typeof(LineRepairIntentResult);
        var constructor = Assert.Single(root.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        var kinds = new[]
        {
            typeof(LineRepairIntentStarted),
            typeof(LineRepairIntentRejected),
            typeof(LineRepairIntentUnderlyingRejected),
            typeof(LineRepairIntentDuplicateIgnored),
            typeof(LineRepairIntentUnsupportedAction),
            typeof(LineRepairIntentUnsupportedTarget)
        };

        Assert.True(root.IsAbstract);
        Assert.True(constructor.IsFamilyAndAssembly, "The root constructor must be private protected so another assembly cannot derive a result kind.");
        Assert.Empty(root.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(typeof(ShiftRuntimeState), root.GetProperty(nameof(LineRepairIntentResult.State))!.PropertyType);
        Assert.All(root.GetProperties(BindingFlags.Public | BindingFlags.Instance), property => Assert.Null(property.SetMethod));
        Assert.All(kinds, type =>
        {
            Assert.True(type.IsSealed);
            Assert.Same(root, type.BaseType);
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        });
    }

    [Fact]
    public void Line_repair_boundary_accepts_only_existing_authoritative_evidence()
    {
        var handle = Assert.Single(typeof(LineRepairIntentHandler).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), method => method.Name == "Handle");
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(IntentEnvelope), typeof(ActorId?), typeof(ServerTick), typeof(SchedulerConfiguration) },
            handle.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(handle.GetParameters(), parameter =>
            parameter.ParameterType == typeof(LogId) || parameter.ParameterType == typeof(JamCause) || parameter.ParameterType == typeof(ActiveRepairHold));

        Assert.Equal(IntentActionId.From("start_line_repair"), LineRepairIntentActions.StartLineRepair);
        Assert.Equal(TargetId.From("LINE"), LineRepairIntentTargets.Line);
    }

    [Fact]
    public void Stage_two_and_seven_close_line_repair_outcomes_without_later_stage_or_replay_expansion()
    {
        var stageTwo = ReadSource("AcceptedIntentStageExecutionContracts.cs");
        Assert.Contains("nine owned stage-2 action IDs", stageTwo, StringComparison.Ordinal);
        Assert.Contains("LineRepairIntentHandler", stageTwo, StringComparison.Ordinal);
        Assert.Contains("LineRepairIntentStageOutcome", stageTwo, StringComparison.Ordinal);

        var stageSeven = ReadSource("HostStageSevenEventExecutionContracts.cs");
        Assert.Contains("HostStageSevenEventTypes.RepairStarted", stageSeven, StringComparison.Ordinal);
        Assert.Contains("LineRepairIntentStageOutcome", stageSeven, StringComparison.Ordinal);
        Assert.Contains("LineRepairIntentStarted", stageSeven, StringComparison.Ordinal);
        Assert.Contains("LineRepairIntentUnderlyingRejected", stageSeven, StringComparison.Ordinal);
        Assert.Contains("case LineRepairIntentStageOutcome:", stageSeven, StringComparison.Ordinal);
        Assert.Contains("HostStageSevenRepairStartedPayload", stageSeven, StringComparison.Ordinal);
        Assert.Contains("LineRepairStartFailed failed => throw", ReadSource("LineRepairIntentHandler.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("LineRepairIntentHandler", ReadSource("HostStageOneCompletionExecutionContracts.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("LineRepairIntentHandler", ReadSource("HostStageFiveFeedExecutionContracts.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("LineRepairIntentHandler", ReadSource("HostStageSixDerivedExecutionContracts.cs"), StringComparison.Ordinal);
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
