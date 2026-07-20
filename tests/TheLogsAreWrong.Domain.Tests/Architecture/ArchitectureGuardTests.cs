using System.Reflection;
using TheLogsAreWrong.Config.Yaml;
using TheLogsAreWrong.Domain.Configuration.Diagnostics;

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
}
