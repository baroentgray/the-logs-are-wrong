namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-015")]
public sealed class Tlaw015ArchitectureTests
{
    [Fact]
    public void Feed_gate_jam_derivation_source_is_non_vacuous_cross_platform_and_uses_no_deferred_or_journal_runtime()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourcePath = Path.Combine(repositoryRoot, "src", "TheLogsAreWrong.Domain", "Scheduler", "FeedGateJamDerivationContracts.cs");
        Assert.True(File.Exists(sourcePath));

        var source = File.ReadAllText(sourcePath);
        var forbidden = new[]
        {
            "EventEnvelope", "EventId", "EventSequence", "IEventJournal", "InMemoryEventJournal", "TryAppend(", "Append(",
            "FeedDueResolved", "PendingLineTransitionDescriptor", "LineRepair", "AutoRoute", "IntakeDeadline", "SawCycle", "LineNoise", "Dispatcher",
            "DateTime", "Stopwatch", "Timer", "Random", "Environment.", "File.", "Directory.", "Task", "Thread",
            "Unity", "FishNet", "Steam", "Yaml"
        };

        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
        Assert.Contains("LineJamEntryService", source, StringComparison.Ordinal);
        Assert.Contains("JamCause.FEED_GATE_BLOCKED", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Domain_project_remains_dependency_free()
    {
        var projectPath = Path.Combine(FindRepositoryRoot(), "src", "TheLogsAreWrong.Domain", "TheLogsAreWrong.Domain.csproj");
        Assert.True(File.Exists(projectPath));
        Assert.DoesNotContain("PackageReference", File.ReadAllText(projectPath), StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var gitMarker = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitMarker) || File.Exists(gitMarker))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
