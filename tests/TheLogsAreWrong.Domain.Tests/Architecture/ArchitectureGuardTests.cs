using System.Reflection;
using TheLogsAreWrong.Config.Yaml;
using TheLogsAreWrong.Domain.Configuration.Diagnostics;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Journal;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

public sealed class ArchitectureGuardTests
{
    [Fact]
    public void Domain_assembly_references_no_yaml_unity_or_network_assemblies()
    {
        var references = typeof(ConfigurationLoadResult).Assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty);

        Assert.DoesNotContain(references, reference => reference.Contains("Yaml", StringComparison.OrdinalIgnoreCase) || reference.Contains("Unity", StringComparison.OrdinalIgnoreCase) || reference.Contains("Fish", StringComparison.OrdinalIgnoreCase) || reference.Contains("Steam", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Yaml_dtos_are_internal_and_public_domain_surface_does_not_expose_yaml()
    {
        var yamlAssembly = typeof(YamlConfigurationLoader).Assembly;
        var rawDto = yamlAssembly.GetType("TheLogsAreWrong.Config.Yaml.Dto.RawYamlDocument");
        var domainAssembly = typeof(ConfigurationLoadResult).Assembly;

        Assert.NotNull(rawDto);
        Assert.False(rawDto!.IsPublic);
        Assert.DoesNotContain(domainAssembly.GetExportedTypes(), type => type.FullName?.Contains("Yaml", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Domain_sources_contain_no_wall_clock_timer_or_filesystem_dependencies()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var source = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        var forbidden = new[] { "DateTime", "DateTimeOffset", "Stopwatch", "Environment.TickCount", "Task", "Thread.Sleep", "System.IO", "UnityEngine", "FishNet", "Steamworks" };

        Assert.All(forbidden, forbiddenToken => Assert.DoesNotContain(source, file => file.Contains(forbiddenToken, StringComparison.Ordinal)));
        Assert.DoesNotContain("PackageReference", File.ReadAllText(Path.Combine(sourceRoot, "TheLogsAreWrong.Domain.csproj")), StringComparison.Ordinal);
    }

    [Fact]
    public void Event_and_journal_public_apis_do_not_expose_bare_ordering_longs()
    {
        var types = new[] { typeof(EventEnvelope), typeof(RejectionEvent), typeof(IEventJournal), typeof(InMemoryEventJournal), typeof(SnapshotBoundary), typeof(ReplayValidator) };

        Assert.All(types, type => Assert.DoesNotContain(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly), method =>
            method.GetParameters().Any(parameter => parameter.ParameterType == typeof(long)) || method.ReturnType == typeof(long)));
    }
}
