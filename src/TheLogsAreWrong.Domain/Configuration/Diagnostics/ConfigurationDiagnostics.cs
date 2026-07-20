using System.Collections.Immutable;

namespace TheLogsAreWrong.Domain.Configuration.Diagnostics;

public enum DiagnosticSeverity { Warning, Error }
public enum ConfigurationDocument { Shift = 0, Anomalies = 1, CrossDocument = 2 }

public sealed record ConfigurationDiagnostic(
    string Code,
    DiagnosticSeverity Severity,
    ConfigurationDocument Document,
    string Path,
    string Message,
    int? Line = null,
    int? Column = null);

public sealed record ConfigurationLoadResult
{
    private ConfigurationLoadResult(ValidatedConfiguration? configuration, ImmutableArray<ConfigurationDiagnostic> diagnostics)
    {
        var hasErrors = diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        if ((configuration is null) == !hasErrors)
        {
            throw new ArgumentException("Configuration results must contain a configuration exactly when diagnostics contain no errors.", nameof(configuration));
        }

        Configuration = configuration;
        Diagnostics = diagnostics;
        IsSuccess = configuration is not null;
    }

    public bool IsSuccess { get; }
    public ValidatedConfiguration? Configuration { get; }
    public ImmutableArray<ConfigurationDiagnostic> Diagnostics { get; }

    public static ConfigurationLoadResult Success(ValidatedConfiguration configuration, IEnumerable<ConfigurationDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var ordered = (diagnostics ?? []).ToImmutableArray();
        return new ConfigurationLoadResult(configuration, ordered);
    }

    public static ConfigurationLoadResult Failure(IEnumerable<ConfigurationDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var ordered = diagnostics.ToImmutableArray();
        return new ConfigurationLoadResult(null, ordered);
    }
}
