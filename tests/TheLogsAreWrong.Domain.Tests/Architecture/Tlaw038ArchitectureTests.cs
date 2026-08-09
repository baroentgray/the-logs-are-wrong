using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-038")]
public sealed class Tlaw038ArchitectureTests
{
    [Fact]
    public void Procedure_intent_is_typed_and_handler_has_one_narrow_authoritative_boundary()
    {
        var parameters = typeof(ProcedureActionIntentParameters);
        Assert.True(parameters.IsSealed);
        Assert.Contains(typeof(IIntentParameters), parameters.GetInterfaces());
        Assert.Equal(typeof(ItemId), parameters.GetProperty(nameof(ProcedureActionIntentParameters.AttemptedItem))!.PropertyType);
        Assert.DoesNotContain(parameters.GetProperties(), property => property.PropertyType == typeof(LogId));

        var handle = Assert.Single(typeof(ProcedureActionIntentHandler).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), method => method.Name == "Handle");
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(IntentEnvelope), typeof(ActorId?), typeof(ServerTick), typeof(AnomalyCatalog) },
            handle.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(typeof(ProcedureActionIntentHandler).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), method =>
            method.Name is "Dispatch" or "Register" or "Configure" or "Replay");
    }

    [Fact]
    public void Stage_two_receives_the_explicit_catalog_and_composer_passes_its_exact_argument()
    {
        var execute = Assert.Single(typeof(AcceptedIntentStageExecutor).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), method => method.Name == "Execute");
        Assert.Equal(
            new[] { typeof(ShiftRuntimeState), typeof(AcceptedIntentTickBatch), typeof(SchedulerConfiguration), typeof(AnomalyCatalog) },
            execute.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(execute.GetParameters(), parameter =>
            parameter.ParameterType == typeof(object) || typeof(Delegate).IsAssignableFrom(parameter.ParameterType));

        var source = ReadSource("HostTickExecutionContracts.cs");
        Assert.Contains("_stageTwo.Execute(stageOne.FinalState, acceptedIntents, schedulerConfiguration, anomalyCatalog)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcedureActionStartService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dictionary<", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Registry", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage_seven_closes_all_procedure_results_without_new_replay_or_effect_execution_surface()
    {
        var source = ReadSource("HostStageSevenEventExecutionContracts.cs");
        Assert.Contains("HostStageSevenEventTypes.ProcedureActionStarted", source, StringComparison.Ordinal);
        Assert.Contains("ProcedureActionIntentHoldStarted", source, StringComparison.Ordinal);
        Assert.Contains("ProcedureActionIntentCompletedImmediately", source, StringComparison.Ordinal);
        Assert.Contains("ProcedureActionIntentRejected", source, StringComparison.Ordinal);
        Assert.Contains("ProcedureActionIntentUnderlyingRejected", source, StringComparison.Ordinal);
        Assert.Contains("case ProcedureActionIntentStageOutcome:", source, StringComparison.Ordinal);
        Assert.Contains("HostStageSevenProcedureActionStartedPayload", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EffectExecution", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayReducer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Snapshot", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Unity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FishNet", source, StringComparison.Ordinal);
    }

    private static string ReadSource(string fileName)
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var files = Directory.GetFiles(sourceRoot, fileName, SearchOption.AllDirectories);
        return File.ReadAllText(Assert.Single(files));
    }
}
