namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-016")]
public sealed class Tlaw016ArchitectureTests
{
    [Fact]
    public void Deadline_sources_are_non_vacuous_cross_platform_and_remain_pure_data_only()
    {
        var root = FindRepositoryRoot();
        var sourcePaths = new[]
        {
            Path.Combine(root, "src", "TheLogsAreWrong.PortableAuthority", "Scheduler", "IntakeDeadlineContracts.cs"),
            Path.Combine(root, "src", "TheLogsAreWrong.PortableAuthority", "Runtime", "ShiftRuntimeState.cs"),
            Path.Combine(root, "src", "TheLogsAreWrong.PortableAuthority", "Runtime", "LogTransitionServices.cs")
        };
        Assert.All(sourcePaths, path => Assert.True(File.Exists(path)));
        var source = sourcePaths.Select(File.ReadAllText).ToArray();
        Assert.All(source, text => Assert.False(string.IsNullOrWhiteSpace(text)));

        var deadlineSource = source[0];
        Assert.Contains("IntakeDeadlineStartService", deadlineSource, StringComparison.Ordinal);
        Assert.Contains("IntakeDeadlineExpirationService", deadlineSource, StringComparison.Ordinal);
        Assert.Contains("DefaultAutoRouteRequired", deadlineSource, StringComparison.Ordinal);
        Assert.All(new[]
        {
            "EventEnvelope", "EventId", "EventSequence", "IEventJournal", "TryAppend(", "Append(", "AutoRouteAttempted",
            "INTAKE_AUTOFEED_BLOCKED", "LineJamEntryService", "LineRepair", "SawCycle", "LineNoise", "Dispatcher",
            "DateTime", "Stopwatch", "Timer", "Random", "Environment.", "File.", "Directory.", "Task", "Thread",
            "Unity", "FishNet", "Steam", "Yaml"
        }, token => Assert.DoesNotContain(token, deadlineSource, StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
