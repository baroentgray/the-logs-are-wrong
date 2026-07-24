using TheLogsAreWrong.Domain.Anomalies;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-005")]
public sealed class Tlaw005ArchitectureTests
{
    [Fact]
    public void TLAW_005_anomaly_sources_are_cross_platform_non_vacuous_and_dependency_free()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var source = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(sourceRoot, path)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment == "Anomalies"))
            .Select(File.ReadAllText)
            .ToArray();
        var forbidden = new[] { "Yaml", "UnityEngine", "FishNet", "Steamworks", "System.IO", "DateTime", "DateTimeOffset", "Stopwatch", "Timer", "Task", "Thread.Sleep", "Environment.", "QuotaSettlementService.Apply", "WithState", "ApplyTransition" };

        Assert.NotEmpty(source);
        Assert.All(forbidden, token => Assert.DoesNotContain(source, file => file.Contains(token, StringComparison.Ordinal)));
        Assert.NotNull(typeof(AnomalyProcessingResolver));
    }
}
