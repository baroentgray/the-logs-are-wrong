using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-021")]
public sealed class Tlaw021ArchitectureTests
{
    [Fact]
    public void Repaired_auto_feed_normal_planning_source_is_non_vacuous_cross_platform_and_domain_pure()
    {
        var root = FindRoot();
        var sourcePath = Path.Combine(root, "src", "TheLogsAreWrong.PortableAuthority", "Scheduler", "RepairAutoFeedNormalFeedPlanningContracts.cs");
        Assert.True(File.Exists(sourcePath));
        var source = File.ReadAllText(sourcePath);

        Assert.False(string.IsNullOrWhiteSpace(source));
        Assert.Contains("RepairAutoFeedNormalFeedPlanningService", source, StringComparison.Ordinal);
        Assert.Contains("RepairPendingTransitionExecuted", source, StringComparison.Ordinal);
        Assert.Contains("NormalFeedPlanningService", source, StringComparison.Ordinal);
        Assert.All(new[]
        {
            "InitialFeedPlanningService", "EarlyFeedIntentHandler", "FeedDue", "IntakeDeadlineStartService", "IntakeDeadlineExpirationService",
            "DefaultIntakeAutoRouteService", "IntakeAutoFeedJamDerivationService", "LineRepairStartService",
            "LineRepairDueCompletionService", "SawCycle", "LineNoise", "Dispatcher", "EventEnvelope",
            "EventId", "EventSequence", "IEventJournal", "Append(", "TryAppend(", "DateTime", "Stopwatch",
            "Timer", "Random", "Task", "Thread", "Environment.", "File.", "Directory.", "Unity", "FishNet", "Steam", "Yaml"
        }, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));

        var project = File.ReadAllText(Path.Combine(root, "src", "TheLogsAreWrong.Domain", "TheLogsAreWrong.Domain.csproj"));
        Assert.DoesNotContain("<PackageReference", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Normal_planning_composition_exposes_only_the_complete_repaired_descriptor()
    {
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var approved = new[] { typeof(ShiftRuntimeState), typeof(RepairPendingTransitionExecuted), typeof(SchedulerConfiguration) };
        var forbidden = new[]
        {
            typeof(LogId), typeof(ServerTick), typeof(StateVersion), typeof(PendingLineTransitionDescriptor),
            typeof(PendingFeedSchedule), typeof(FeedScheduleKind), typeof(object)
        };

        foreach (var method in typeof(RepairAutoFeedNormalFeedPlanningService).GetMethods(flags).Where(method => !method.IsPrivate))
        {
            var parameters = method.GetParameters();
            Assert.True(parameters.Select(parameter => parameter.ParameterType).SequenceEqual(approved),
                $"{method.Name} must accept only state, the complete repaired descriptor, and configuration.");
            Assert.DoesNotContain(parameters, parameter => forbidden.Contains(parameter.ParameterType));
        }
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
