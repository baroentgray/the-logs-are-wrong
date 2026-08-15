namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-014")]
public sealed class Tlaw014ArchitectureTests
{
    [Fact]
    public void Feed_due_source_is_non_vacuous_cross_platform_and_excludes_journal_and_deferred_runtime_dependencies()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourcePath = Path.Combine(repositoryRoot, "src", "TheLogsAreWrong.PortableAuthority", "Scheduler", "FeedDueResolutionContracts.cs");
        Assert.True(File.Exists(sourcePath));

        var source = File.ReadAllText(sourcePath);
        var forbidden = new[]
        {
            "EventEnvelope", "EventId", "EventSequence", "IEventJournal", "InMemoryEventJournal", "TryAppend(", "Append(",
            "IntakeDeadlineService", "LineJamEntryService", "AutoRoute", "SawCycle", "LineNoise", "Dispatcher",
            "DateTime", "Stopwatch", "Timer", "Random", "Environment.", "File.", "Directory.", "Task", "Thread",
            "Unity", "FishNet", "Steam", "Yaml"
        };

        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
    }

    [Fact]
    public void Domain_project_remains_dependency_free()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "src", "TheLogsAreWrong.Domain", "TheLogsAreWrong.Domain.csproj");

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
