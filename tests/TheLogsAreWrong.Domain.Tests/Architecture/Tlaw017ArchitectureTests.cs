namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-017")]
public sealed class Tlaw017ArchitectureTests
{
    [Fact]
    public void Auto_route_source_is_non_vacuous_cross_platform_and_remains_host_pure()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "TheLogsAreWrong.PortableAuthority", "Scheduler", "DefaultIntakeAutoRouteContracts.cs");
        Assert.True(File.Exists(path));
        var source = File.ReadAllText(path);
        Assert.False(string.IsNullOrWhiteSpace(source));
        Assert.Contains("DefaultIntakeAutoRouteService", source, StringComparison.Ordinal);
        Assert.Contains("HostLogTransitionService", source, StringComparison.Ordinal);
        Assert.All(new[]
        {
            "EventEnvelope", "EventId", "EventSequence", "IEventJournal", "TryAppend(", "Append(", "AutoRouteAttempted",
            "LineJamEntryService", "PendingLineTransitionDescriptor", "SawCycle", "LineNoise", "Dispatcher", "DateTime",
            "Stopwatch", "Timer", "Random", "Environment.", "File.", "Directory.", "Task", "Thread", "Unity", "FishNet", "Steam", "Yaml"
        }, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git"))) return current.FullName;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
