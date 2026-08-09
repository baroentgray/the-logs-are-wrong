using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-039")]
public sealed class Tlaw039ArchitectureTests
{
    [Fact]
    public void Confirmation_intent_result_family_is_closed_to_external_derivation_and_immutable()
    {
        var publicInstance = BindingFlags.Public | BindingFlags.Instance;
        var allInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var root = typeof(ConfirmationTestIntentResult);
        var constructor = Assert.Single(root.GetConstructors(allInstance));
        var kinds = new[]
        {
            typeof(ConfirmationTestIntentStarted),
            typeof(ConfirmationTestIntentRejected),
            typeof(ConfirmationTestIntentUnderlyingRejected),
            typeof(ConfirmationTestIntentDuplicateIgnored),
            typeof(ConfirmationTestIntentUnsupported)
        };

        Assert.True(root.IsClass);
        Assert.True(root.IsAbstract);
        Assert.True(constructor.IsFamilyAndAssembly, "The root constructor must be private protected so another assembly cannot derive a result kind.");
        Assert.False(constructor.IsPublic);
        Assert.False(constructor.IsFamily);
        Assert.Empty(root.GetConstructors(publicInstance));
        Assert.Equal(typeof(ShiftRuntimeState), root.GetProperty(nameof(ConfirmationTestIntentResult.State))!.PropertyType);
        Assert.All(root.GetProperties(publicInstance), property => Assert.Null(property.SetMethod));

        Assert.All(kinds, type =>
        {
            Assert.True(type.IsSealed);
            Assert.Same(root, type.BaseType);
            Assert.Empty(type.GetConstructors(publicInstance));
        });
    }

    [Fact]
    public void Confirmation_handler_and_stage_two_receive_only_explicit_authoritative_evidence()
    {
        var handle = Assert.Single(typeof(ConfirmationTestIntentHandler).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), method => method.Name == "Handle");
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(IntentEnvelope), typeof(ActorId?), typeof(ServerTick), typeof(ImmutableHashSet<ItemId>), typeof(LineNoiseRuntimeState), typeof(AnomalyCatalog) },
            handle.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(typeof(ConfirmationTestIntentHandler).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), method =>
            method.Name is "Dispatch" or "Register" or "Configure" or "Replay");

        var execute = Assert.Single(typeof(AcceptedIntentStageExecutor).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), method => method.Name == "Execute");
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(AcceptedIntentTickBatch), typeof(SchedulerConfiguration), typeof(ImmutableHashSet<ItemId>), typeof(LineNoiseRuntimeState), typeof(AnomalyCatalog) },
            execute.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(execute.GetParameters(), parameter => parameter.ParameterType == typeof(object) || typeof(Delegate).IsAssignableFrom(parameter.ParameterType));

        var source = ReadSource("HostTickExecutionContracts.cs");
        Assert.Contains("_stageTwo.Execute(stageOne.FinalState, acceptedIntents, schedulerConfiguration, activeTools, initialLineNoise, anomalyCatalog)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LineNoiseDerivationService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmationTestConditionService", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage_six_remains_the_later_confirmation_condition_authority_and_stage_seven_closes_new_outcomes()
    {
        var stageSix = ReadSource("HostStageSixDerivedExecutionContracts.cs");
        Assert.Contains("ConfirmationTestConditionService", stageSix, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmationTestIntentHandler", stageSix, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmationIntentActions", stageSix, StringComparison.Ordinal);

        var stageSeven = ReadSource("HostStageSevenEventExecutionContracts.cs");
        Assert.Contains("HostStageSevenEventTypes.ConfirmationTestStarted", stageSeven, StringComparison.Ordinal);
        Assert.Contains("ConfirmationTestIntentStageOutcome", stageSeven, StringComparison.Ordinal);
        Assert.Contains("ConfirmationTestIntentStarted", stageSeven, StringComparison.Ordinal);
        Assert.Contains("ConfirmationTestIntentUnderlyingRejected", stageSeven, StringComparison.Ordinal);
        Assert.Contains("case ConfirmationTestIntentStageOutcome:", stageSeven, StringComparison.Ordinal);
        Assert.DoesNotContain("EffectExecution", stageSeven, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayReducer", stageSeven, StringComparison.Ordinal);
        Assert.DoesNotContain("Snapshot", stageSeven, StringComparison.Ordinal);
        Assert.DoesNotContain("Unity", stageSeven, StringComparison.Ordinal);
        Assert.DoesNotContain("FishNet", stageSeven, StringComparison.Ordinal);
    }

    private static string ReadSource(string fileName)
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var files = Directory.GetFiles(sourceRoot, fileName, SearchOption.AllDirectories);
        return File.ReadAllText(Assert.Single(files));
    }
}
