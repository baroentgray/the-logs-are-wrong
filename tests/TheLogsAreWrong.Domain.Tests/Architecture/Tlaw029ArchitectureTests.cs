using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-029")]
public sealed class Tlaw029ArchitectureTests
{
    [Fact]
    public void Accepted_intent_batch_boundary_is_immutable_and_exposes_only_adapter_accepted_evidence()
    {
        var publicInstance = BindingFlags.Public | BindingFlags.Instance;
        var receipt = typeof(AuthoritativeAcceptedIntent);
        var batch = typeof(AcceptedIntentTickBatch);
        var create = Assert.Single(typeof(AcceptedIntentTickBatchFactory).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly), method => method.Name == "Create");

        Assert.True(receipt.IsSealed);
        Assert.True(batch.IsSealed);
        Assert.Empty(batch.GetConstructors(publicInstance));
        Assert.Equal(typeof(AcceptedIntentTickBatch), create.ReturnType);
        Assert.Equal(
            new[] { typeof(ShiftId), typeof(ServerTick), typeof(IEnumerable<AuthoritativeAcceptedIntent>) },
            create.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(ImmutableArray<AuthoritativeAcceptedIntent>), batch.GetProperty(nameof(AcceptedIntentTickBatch.Intents))!.PropertyType);
        Assert.All(new[] { receipt, batch }, type =>
        {
            Assert.Empty(type.GetFields(publicInstance));
            Assert.All(type.GetProperties(publicInstance), property => Assert.Null(property.SetMethod));
        });
        Assert.DoesNotContain(receipt.GetProperties(publicInstance), property => property.PropertyType.Name.Contains("Connection", StringComparison.Ordinal) || property.PropertyType.Namespace?.Contains("Net", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Accepted_intent_batch_preserves_the_frozen_seven_host_stages_and_stage_two_ordering()
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
    public void Accepted_intent_batch_source_is_dependency_free_and_non_vacuously_scanned()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var sourcePath = Directory.GetFiles(sourceRoot, "AcceptedIntentBatchContracts.cs", SearchOption.AllDirectories).Single();
        var source = File.ReadAllText(sourcePath);
        var forbidden = new[]
        {
            "Connection", "Network", "FishNet", "Steam", "Unity", "Yaml", "DateTime", "Stopwatch", "Timer", "Random", "Task", "Thread", "Environment.", "File.", "Directory.",
            "IEventJournal", "EventEnvelope", "Journal", "Append", "Commit", "Dispatch", "ShiftRuntimeState", "QuotaRuntimeState", "ShiftLifecycleRuntimeState", "HostTickCompletion", "Scheduler", "Dictionary<", "object"
        };

        Assert.Contains("AuthoritativeAcceptedIntent", source, StringComparison.Ordinal);
        Assert.Contains("AcceptedIntentTickBatchFactory", source, StringComparison.Ordinal);
        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
        Assert.DoesNotContain("<PackageReference", File.ReadAllText(Path.Combine(sourceRoot, "TheLogsAreWrong.Domain.csproj")), StringComparison.Ordinal);
    }
}
