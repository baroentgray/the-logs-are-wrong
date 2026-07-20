using YamlDotNet.RepresentationModel;

namespace TheLogsAreWrong.Config.Yaml.Dto;

// Raw YAML representation is intentionally internal: no YAML-shaped type crosses this adapter boundary.
internal sealed record RawYamlDocument(YamlMappingNode Root);
