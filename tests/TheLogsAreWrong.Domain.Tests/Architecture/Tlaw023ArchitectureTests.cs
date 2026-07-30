using System.Reflection;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-023")]
public sealed class Tlaw023ArchitectureTests
{
    [Fact]
    public void Saw_quota_application_source_is_non_vacuous_cross_platform_and_has_no_broadened_dependencies()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var sourcePath = Directory.GetFiles(sourceRoot, "SawQuotaApplicationContracts.cs", SearchOption.AllDirectories).Single();
        var source = File.ReadAllText(sourcePath);
        var forbidden = new[]
        {
            "EffectExecutor", "ShiftCompletion", "Dispatcher", "IEventJournal", "EventEnvelope", "Snapshot", "Replay",
            "Unity", "FishNet", "Steam", "Yaml", "DateTime", "Stopwatch", "Timer", "Random", "Task", "Thread", "Environment.", "File.", "Directory."
        };

        Assert.Contains("SawQuotaApplicationService", source, StringComparison.Ordinal);
        Assert.Contains("QuotaSettlementService", source, StringComparison.Ordinal);
        Assert.Contains("ValidateCompletion", source, StringComparison.Ordinal);
        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));

        var project = File.ReadAllText(Path.Combine(sourceRoot, "TheLogsAreWrong.Domain.csproj"));
        Assert.DoesNotContain("<PackageReference", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_application_surface_is_closed_to_accepted_completion_and_separate_quota_state()
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var apply = Assert.Single(typeof(SawQuotaApplicationService).GetMethods(flags));

        Assert.Equal(new[] { typeof(SawCycleCompleted), typeof(QuotaRuntimeState) }, apply.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(SawQuotaApplicationResult), apply.ReturnType);
        Assert.All(typeof(SawQuotaApplicationService).GetProperties(flags), property => Assert.False(property.CanWrite));
        Assert.DoesNotContain(typeof(SawQuotaApplicationService).GetMethods(flags), method => method.Name is "Commit" or "Append" or "Dispatch");
        Assert.Null(typeof(ShiftRuntimeState).GetProperty("QuotaState"));
    }
}
