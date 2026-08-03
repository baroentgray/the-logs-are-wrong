using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-028")]
public sealed class Tlaw028ArchitectureTests
{
    [Fact]
    public void Checkpoint_boundary_is_dependency_free_and_has_no_host_or_event_expansion()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var sourcePath = Directory.GetFiles(sourceRoot, "HostTickCompletionCheckpointContracts.cs", SearchOption.AllDirectories).Single();
        var source = File.ReadAllText(sourcePath);
        var forbidden = new[]
        {
            "IEventJournal", "EventEnvelope", "EventSequence", "Journal", "Append", "Commit", "Dispatch", "Intent", "ServerReceiveSequence",
            "Effect", "Yaml", "Unity", "FishNet", "Steam", "DateTime", "Stopwatch", "Timer", "Random", "Task", "Thread", "Environment.",
            "File.", "Directory.", "Scheduler", "LineNoise", "Movement", "Containment", "Dictionary<", "object"
        };

        Assert.Contains("HostTickCompletionCheckpointService", source, StringComparison.Ordinal);
        Assert.Contains("ShiftCompletionEvaluationService", source, StringComparison.Ordinal);
        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
        Assert.DoesNotContain("HostTickStage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", File.ReadAllText(Path.Combine(sourceRoot, "TheLogsAreWrong.Domain.csproj")), StringComparison.Ordinal);
    }

    [Fact]
    public void Checkpoint_preserves_the_existing_seven_frozen_host_stages()
    {
        Assert.Equal(new[]
        {
            HostTickStage.hold_and_procedure_completions,
            HostTickStage.accepted_intents_by_server_receive_sequence,
            HostTickStage.deadline_expirations,
            HostTickStage.saw_transitions,
            HostTickStage.feed_and_auto_routes,
            HostTickStage.derived_states,
            HostTickStage.event_emission
        }, HostTickStages.CanonicalOrder);
    }

    [Fact]
    public void Public_surface_accepts_only_post_stage_runtime_evidence_and_configuration()
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var create = Assert.Single(typeof(HostTickProgressionEvidence).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly), method => method.Name == "Create");
        var complete = Assert.Single(typeof(HostTickCompletionCheckpointService).GetMethods(flags), method => method.Name == "Complete");

        Assert.Equal(new[] { typeof(ShiftId) }, create.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(HostTickCheckpointResult), complete.ReturnType);
        Assert.Equal(
            new[] { typeof(HostTickProgressionEvidence), typeof(ShiftLifecycleRuntimeState), typeof(ShiftRuntimeState), typeof(QuotaRuntimeState), typeof(ServerTick), typeof(ShiftConfiguration) },
            complete.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(complete.GetParameters(), parameter => parameter.ParameterType == typeof(bool) || parameter.ParameterType == typeof(string) || parameter.ParameterType == typeof(object));
        Assert.DoesNotContain(typeof(HostTickCompletionCheckpointService).GetMethods(flags), method => method.Name is "Dispatch" or "Commit" or "Append" or "Apply");
    }

    [Fact]
    public void Progression_receipt_and_closed_results_are_immutable_and_cannot_be_publicly_fabricated()
    {
        var publicInstance = BindingFlags.Public | BindingFlags.Instance;
        var resultTypes = new[] { typeof(HostTickCheckpointAdvanced), typeof(HostTickCheckpointReplayed), typeof(HostTickCheckpointRejected) };

        Assert.Empty(typeof(HostTickProgressionEvidence).GetConstructors(publicInstance));
        Assert.Empty(typeof(HostTickCompletionReceipt).GetConstructors(publicInstance));
        Assert.All(resultTypes, type => Assert.Empty(type.GetConstructors(publicInstance)));
        Assert.All(
            new[] { typeof(HostTickProgressionEvidence), typeof(HostTickCompletionReceipt), typeof(HostTickCheckpointResult) }.Concat(resultTypes),
            type => Assert.All(type.GetProperties(publicInstance), property => Assert.Null(property.SetMethod)));
    }
}
