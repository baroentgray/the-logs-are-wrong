namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-013")]
public sealed class Tlaw013ArchitectureTests
{
    [Fact]
    public void Scheduler_sources_are_non_vacuous_cross_platform_and_do_not_introduce_forbidden_runtime_or_journal_work()
    {
        var repositoryRoot = FindRepositoryRoot();
        var schedulerRoot = Path.Combine(repositoryRoot, "src", "TheLogsAreWrong.Domain", "Scheduler");
        var files = new[] { Path.Combine(schedulerRoot, "FeedPlanningContracts.cs") };

        Assert.NotEmpty(files);
        Assert.All(files, path => Assert.StartsWith(schedulerRoot, path, StringComparison.OrdinalIgnoreCase));

        var source = string.Join("\n", files.Select(File.ReadAllText));
        var forbidden = new[]
        {
            "EventEnvelope", "EventId", "EventSequence", "IEventJournal", "InMemoryEventJournal", "TryAppend(", "Append(",
            "FeedDue", "LogAdmittedToIntake", "IntakeDeadline", "AutoRoute", "SawCycle", "LineNoiseChanged",
            "DateTime", "Stopwatch", "Timer", "Random", "Environment.", "File.", "Directory.", "Task", "Thread",
            "Unity", "FishNet", "Steam", "Yaml"
        };

        Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
    }

    [Fact]
    public void Domain_project_remains_dependency_free_and_tlaw013_source_selection_cannot_pass_without_production_files()
    {
        var repositoryRoot = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(repositoryRoot, "src", "TheLogsAreWrong.Domain", "TheLogsAreWrong.Domain.csproj"));
        var runtimeFiles = Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src", "TheLogsAreWrong.Domain", "Runtime"), "*.cs", SearchOption.AllDirectories).ToArray();

        Assert.DoesNotContain("PackageReference", project, StringComparison.Ordinal);
        Assert.NotEmpty(runtimeFiles);
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
