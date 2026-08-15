using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-020")]
public sealed class Tlaw020ArchitectureTests
{
    [Fact]
    public void Repaired_feed_gate_deadline_source_is_non_vacuous_cross_platform_and_domain_pure()
    {
        var root = FindRoot();
        var sourcePath = Path.Combine(root, "src", "TheLogsAreWrong.Domain", "Scheduler", "RepairFeedGateIntakeDeadlineContracts.cs");
        var deadlinePath = Path.Combine(root, "src", "TheLogsAreWrong.PortableAuthority", "Scheduler", "IntakeDeadlineContracts.cs");
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
        Assert.DoesNotContain("StartFromAcceptedAdmission", deadline, StringComparison.Ordinal);
        Assert.All(new[] { "EventEnvelope", "EventId", "EventSequence", "IEventJournal", "Append(", "TryAppend(", "NormalFeedPlanningService", "IntakeDeadlineExpirationService", "DefaultIntakeAutoRouteService", "IntakeAutoFeedJamDerivationService", "LineJamEntryService", "LineRepairStartService", "LineRepairDueCompletionService", "SawCycle", "LineNoise", "Dispatcher", "DateTime", "Stopwatch", "Timer", "Random", "Task", "Thread", "Environment.", "File.", "Directory.", "Unity", "FishNet", "Steam", "Yaml" }, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
        var project = File.ReadAllText(Path.Combine(root, "src", "TheLogsAreWrong.Domain", "TheLogsAreWrong.Domain.csproj"));
        Assert.DoesNotContain("<PackageReference", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Deadline_start_composition_exposes_only_complete_source_descriptors()
    {
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var feedDueShape = new[] { typeof(ShiftRuntimeState), typeof(FeedDueResolved), typeof(ShiftProfile) };
        var repairedShape = new[] { typeof(ShiftRuntimeState), typeof(RepairPendingTransitionExecuted), typeof(ShiftProfile) };
        var forbiddenCallerAssembledTypes = new[]
        {
            typeof(LogId),
            typeof(ServerTick),
            typeof(StateVersion),
            typeof(PendingLineTransitionDescriptor),
            typeof(object)
        };

        foreach (var type in new[] { typeof(IntakeDeadlineStartService), typeof(RepairFeedGateIntakeDeadlineStartService) })
        {
            foreach (var method in type.GetMethods(flags).Where(method => !method.IsPrivate))
            {
                var parameterTypes = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
                Assert.True(
                    parameterTypes.SequenceEqual(feedDueShape) || parameterTypes.SequenceEqual(repairedShape),
                    $"{type.Name}.{method.Name} must accept one complete deadline-admission descriptor.");
                Assert.DoesNotContain(method.GetParameters(), parameter => forbiddenCallerAssembledTypes.Contains(parameter.ParameterType));
            }
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
