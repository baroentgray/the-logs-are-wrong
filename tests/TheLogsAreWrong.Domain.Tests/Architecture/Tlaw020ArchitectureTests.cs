namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-020")]
public sealed class Tlaw020ArchitectureTests
{
    [Fact]
    public void Repaired_feed_gate_deadline_source_is_non_vacuous_cross_platform_and_domain_pure()
    {
        var root = FindRoot();
        var sourcePath = Path.Combine(root, "src", "TheLogsAreWrong.Domain", "Scheduler", "RepairFeedGateIntakeDeadlineContracts.cs");
        var deadlinePath = Path.Combine(root, "src", "TheLogsAreWrong.Domain", "Scheduler", "IntakeDeadlineContracts.cs");
        Assert.True(File.Exists(sourcePath));
        Assert.True(File.Exists(deadlinePath));
        var source = File.ReadAllText(sourcePath);
        var deadline = File.ReadAllText(deadlinePath);

        Assert.False(string.IsNullOrWhiteSpace(source));
        Assert.Contains("RepairPendingTransitionExecuted", source, StringComparison.Ordinal);
        Assert.Contains("RepairFeedGateIntakeDeadlineStartService", source, StringComparison.Ordinal);
        Assert.Contains("IntakeDeadlineStartService", source, StringComparison.Ordinal);
        Assert.Contains("ActiveIntakeDeadline", deadline, StringComparison.Ordinal);
        Assert.Contains("WithActiveIntakeDeadline", deadline, StringComparison.Ordinal);
        Assert.All(new[] { "EventEnvelope", "EventId", "EventSequence", "IEventJournal", "Append(", "TryAppend(", "NormalFeedPlanningService", "IntakeDeadlineExpirationService", "DefaultIntakeAutoRouteService", "IntakeAutoFeedJamDerivationService", "LineJamEntryService", "LineRepairStartService", "LineRepairDueCompletionService", "SawCycle", "LineNoise", "Dispatcher", "DateTime", "Stopwatch", "Timer", "Random", "Task", "Thread", "Environment.", "File.", "Directory.", "Unity", "FishNet", "Steam", "Yaml" }, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
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
