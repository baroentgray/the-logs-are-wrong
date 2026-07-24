using System.Reflection;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-003")]
public sealed class Tlaw003ArchitectureTests
{
    [Fact]
    public void TLAW_003_domain_sources_remain_free_of_yaml_unity_network_io_and_timer_dependencies()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var source = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(sourceRoot, path)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "Intents" or "Runtime" or "Logs"))
            .Select(File.ReadAllText)
            .ToArray();
        var forbidden = new[] { "Yaml", "UnityEngine", "FishNet", "Steamworks", "System.IO", "DateTime", "DateTimeOffset", "Stopwatch", "Timer", "Task", "Thread.Sleep", "File.", "Environment." };

        Assert.NotEmpty(source);
        Assert.All(forbidden, token => Assert.DoesNotContain(source, file => file.Contains(token, StringComparison.Ordinal)));
    }

    [Fact]
    public void Intent_and_host_transition_seams_cannot_append_events_or_accept_an_actor_hint_as_authority()
    {
        var handlerMethods = typeof(ManualLogIntentHandler).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var hostMethods = typeof(HostLogTransitionService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(handlerMethods, method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(IEventJournal)));
        Assert.DoesNotContain(hostMethods, method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(IEventJournal) || parameter.ParameterType == typeof(ActorId)));
        Assert.DoesNotContain(typeof(RejectionReason).GetEnumNames(), name => name == "UNSUPPORTED_ACTION");
    }
}
