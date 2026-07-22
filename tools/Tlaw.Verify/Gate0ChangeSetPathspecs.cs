namespace Tlaw.Verify;

public static class Gate0ChangeSetPathspecs
{
    public const string DocsAgentExclusionPathspec = ":(exclude,top)docs/agent/**";

    private static readonly string[] BroadProtectedRoots = ["docs", "data", "reviews", "scripts", "source"];

    public static IReadOnlyList<string> Build(Gate0Baseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        return baseline.Files
            .Select(file => NormalizeRepositoryPath(file.Path))
            .Concat(BroadProtectedRoots)
            .Distinct(StringComparer.Ordinal)
            .Append(DocsAgentExclusionPathspec)
            .ToArray();
    }

    public static Gate0ChangeSet CreateChangeSet(IEnumerable<string> paths, bool succeeded)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var protectedPaths = paths
            .Select(NormalizeRepositoryPath)
            .Where(path => !IsDocsAgentPath(path))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new Gate0ChangeSet(protectedPaths, succeeded);
    }

    private static bool IsDocsAgentPath(string path) => path.StartsWith("docs/agent/", StringComparison.Ordinal);

    private static string NormalizeRepositoryPath(string path) => path.Replace('\\', '/');
}
