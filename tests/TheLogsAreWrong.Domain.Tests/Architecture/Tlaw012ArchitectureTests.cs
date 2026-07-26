using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Tests.Determinism;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-012")]
public sealed class Tlaw012ArchitectureTests
{
    [Fact]
    public void TLAW_012_scenario_sources_are_cross_platform_non_vacuous_test_only_and_free_of_nondeterministic_dependencies()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var testRoot = Path.Combine(repositoryRoot, "tests", "TheLogsAreWrong.Domain.Tests");
        var scenarioRoot = Path.Combine(testRoot, "Determinism");
        var scenarioSources = Directory.GetFiles(scenarioRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText)
            .ToArray();
        var taggedSources = Directory.GetFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("TLAW-012", StringComparison.Ordinal))
            .ToArray();
        var forbidden = new[]
        {
            "Guid", "Random", "DateTime", "DateTimeOffset", "Stopwatch", "System.IO", "Environment.", "Process", "Thread", "Timer", "Task", "UnityEngine", "FishNet", "Steamworks",
            "IntakeScheduler", "HostTick", "ReplayReducer", "QuotaSettlementService", ".Append(", ".TryAppend("
        };

        Assert.Equal(typeof(Tlaw012ArchitectureTests).Assembly, typeof(DeterministicScenarioDriver).Assembly);
        Assert.NotEqual(typeof(ShiftRuntimeState).Assembly, typeof(DeterministicScenarioDriver).Assembly);
        Assert.Equal(2, scenarioSources.Length);
        Assert.NotEmpty(taggedSources);
        Assert.All(taggedSources, source => Assert.StartsWith(testRoot, source, StringComparison.OrdinalIgnoreCase));
        Assert.All(forbidden, token => Assert.DoesNotContain(scenarioSources, source => source.Contains(token, StringComparison.Ordinal)));
    }

    [Fact]
    public void Deterministic_trace_exposes_immutable_canonical_values_not_runtime_hash_or_reference_based_projections()
    {
        var traceProperties = typeof(DeterministicScenarioTrace).GetProperties();
        var payloadProperties = typeof(DeterministicScenarioPayload).GetProperties();

        Assert.Contains(traceProperties, property => property.Name == nameof(DeterministicScenarioTrace.Events));
        Assert.DoesNotContain(traceProperties, property => property.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(payloadProperties, property => property.PropertyType == typeof(object));
        Assert.All(traceProperties, property => Assert.False(property.CanWrite));
    }
}
