using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Tlaw.Verify;

public sealed record TestCounts(int Passed, int Failed, int Skipped, int Total);

public sealed record BuildDiagnosticCounts(int Warnings, int Errors);

public sealed record Gate0Baseline(string BaselineId, string SourceSha, IReadOnlyList<Gate0FileHash> Files);

public sealed record Gate0FileHash(string Path, string Sha256);

public static class TrxCounterParser
{
    public static TestCounts Parse(string trxPath)
    {
        var document = XDocument.Load(trxPath);
        var counters = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Counters")
            ?? throw new InvalidDataException("TRX counters are missing.");

        return new TestCounts(
            ReadCounter(counters, "passed"),
            ReadCounter(counters, "failed"),
            ReadCounter(counters, "notExecuted") + ReadCounter(counters, "inconclusive"),
            ReadCounter(counters, "total"));
    }

    private static int ReadCounter(XElement counters, string name)
    {
        var value = counters.Attribute(name)?.Value ?? "0";
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException($"TRX counter '{name}' is invalid.");
    }
}

public static class BuildDiagnosticParser
{
    private static readonly Regex WarningPattern = new(@"(?im)^.*\b(?:warning|предупреждение)\s+[A-Z]+\d{3,}\b", RegexOptions.CultureInvariant);
    private static readonly Regex ErrorPattern = new(@"(?im)^.*\b(?:error|ошибка)\s+[A-Z]+\d{3,}\b", RegexOptions.CultureInvariant);

    public static BuildDiagnosticCounts Parse(string output) => new(WarningPattern.Count(output), ErrorPattern.Count(output));
}

public static class Sha256Hasher
{
    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

public static class Gate0BaselineLoader
{
    public static Gate0Baseline Load(string path)
    {
        var json = File.ReadAllText(path);
        return System.Text.Json.JsonSerializer.Deserialize<Gate0Baseline>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })
            ?? throw new InvalidDataException("Gate 0 baseline manifest is empty.");
    }
}

public static class Gate0Verifier
{
    public static Gate0Evidence Verify(string repositoryRoot, Gate0Baseline baseline)
    {
        var mismatches = new List<string>();
        foreach (var file in baseline.Files)
        {
            var path = Path.Combine(repositoryRoot, file.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) || !string.Equals(Sha256Hasher.HashFile(path), file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                mismatches.Add(file.Path);
            }
        }

        return new Gate0Evidence(
            mismatches.Count == 0 ? EvidenceStatus.PASS : EvidenceStatus.FAIL,
            baseline.BaselineId,
            baseline.SourceSha,
            baseline.Files.Select(file => file.Path).ToArray(),
            mismatches);
    }
}

public static class DomainDependencyVerifier
{
    public static DomainDependenciesEvidence Verify(string domainProjectPath)
    {
        var document = XDocument.Load(domainProjectPath);
        var packageReferences = document.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return new DomainDependenciesEvidence(packageReferences.Length == 0 ? EvidenceStatus.PASS : EvidenceStatus.FAIL, packageReferences);
    }
}

public static class ArchitectureEvidenceExtractor
{
    public static ArchitectureEvidence FromTrx(string trxPath)
    {
        var document = XDocument.Load(trxPath);
        var checks = document.Descendants()
            .Where(element => element.Name.LocalName == "UnitTestResult")
            .Where(element => (element.Attribute("testName")?.Value ?? string.Empty).Contains("ArchitectureGuardTests", StringComparison.Ordinal))
            .Select(element => $"{element.Attribute("testName")?.Value}: {element.Attribute("outcome")?.Value}")
            .ToArray();

        var status = checks.Length > 0 && checks.All(check => check.EndsWith(": Passed", StringComparison.Ordinal))
            ? EvidenceStatus.PASS
            : EvidenceStatus.FAIL;
        return new ArchitectureEvidence(status, checks);
    }
}
