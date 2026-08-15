namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-019")]
public sealed class Tlaw019ArchitectureTests
{
    [Fact]
    public void Pending_transition_execution_source_is_non_vacuous_cross_platform_and_host_pure()
    {
        var root = FindRoot();
        var path = Path.Combine(root, "src", "TheLogsAreWrong.PortableAuthority", "Scheduler", "RepairPendingTransitionExecutionContracts.cs");
        Assert.True(File.Exists(path));
        var source = File.ReadAllText(path);
        Assert.False(string.IsNullOrWhiteSpace(source));
        Assert.Contains("HostLogTransitionService", source, StringComparison.Ordinal);
        Assert.All(new[] { "EventEnvelope", "EventId", "EventSequence", "IEventJournal", "Append(", "TryAppend(", "LineRepairStartService", "LineRepairDueCompletionService", "IntakeDeadlineStartService", "NormalFeedPlanningService", "ConfirmationTestCancellationService", "ConfirmationTestConditionService", "ConfirmationTestDueCompletionService", "SawCycle", "LineNoise", "Dispatcher", "DateTime", "Stopwatch", "Timer", "Random", "Environment.", "File.", "Directory.", "Task", "Thread", "Unity", "FishNet", "Steam", "Yaml" }, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
        var project = File.ReadAllText(Path.Combine(root, "src", "TheLogsAreWrong.Domain", "TheLogsAreWrong.Domain.csproj"));
        Assert.DoesNotContain("<PackageReference", project, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git"))) return current.FullName;
        }

        throw new DirectoryNotFoundException();
    }
}
