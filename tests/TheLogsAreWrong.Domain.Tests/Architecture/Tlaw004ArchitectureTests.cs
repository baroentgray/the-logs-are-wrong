using TheLogsAreWrong.Domain.Quota;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-004")]
public sealed class Tlaw004ArchitectureTests
{
    [Fact]
    public void TLAW_004_quota_sources_are_cross_platform_non_vacuous_and_dependency_free()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var source = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(sourceRoot, path)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment == "Quota"))
            .Select(File.ReadAllText)
            .ToArray();
        var forbidden = new[] { "Yaml", "UnityEngine", "FishNet", "Steamworks", "System.IO", "DateTime", "DateTimeOffset", "Stopwatch", "Timer", "Task", "Thread.Sleep", "Environment." };

        Assert.NotEmpty(source);
        Assert.All(forbidden, token => Assert.DoesNotContain(source, file => file.Contains(token, StringComparison.Ordinal)));
        Assert.NotNull(typeof(QuotaRuntimeState));
    }
}
