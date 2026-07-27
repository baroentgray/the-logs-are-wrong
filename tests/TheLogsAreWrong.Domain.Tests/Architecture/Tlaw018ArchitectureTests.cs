namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-018")]
public sealed class Tlaw018ArchitectureTests
{
    [Fact]
    public void Jam_derivation_source_is_non_vacuous_cross_platform_and_host_pure()
    {
        var root = FindRoot();
        var path = Path.Combine(root, "src", "TheLogsAreWrong.Domain", "Scheduler", "IntakeAutoFeedJamDerivationContracts.cs");
        Assert.True(File.Exists(path));
        var source = File.ReadAllText(path);
        Assert.False(string.IsNullOrWhiteSpace(source));
        Assert.Contains("LineJamEntryService", source, StringComparison.Ordinal);
        Assert.All(new[] { "EventEnvelope", "EventId", "EventSequence", "IEventJournal", "Append(", "LineJammed", "AutoRouteAttempted", "PendingLineTransitionDescriptor", "SawCycle", "LineNoise", "Dispatcher", "DateTime", "Stopwatch", "Timer", "Random", "Environment.", "File.", "Directory.", "Task", "Thread", "Unity", "FishNet", "Steam", "Yaml" }, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
    }

    private static string FindRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent) if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git"))) return current.FullName;
        throw new DirectoryNotFoundException();
    }
}
