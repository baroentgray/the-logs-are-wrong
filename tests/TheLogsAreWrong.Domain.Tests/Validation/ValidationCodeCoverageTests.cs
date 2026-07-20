using System.Collections.Immutable;
using System.Text.RegularExpressions;
using TheLogsAreWrong.Config.Yaml;
using TheLogsAreWrong.Domain.Configuration.Diagnostics;

namespace TheLogsAreWrong.Domain.Tests.Validation;

public sealed partial class ValidationCodeCoverageTests
{
    [Theory]
    [MemberData(nameof(ValidationCodeCases.TheoryData), MemberType = typeof(ValidationCodeCases))]
    public void Every_approved_validation_code_has_an_explicit_contract_case(ValidationCase testCase)
    {
        var inputs = new YamlInputs(Fixture.ShiftYaml, Fixture.AnomaliesYaml);
        var mutated = testCase.Mutate(inputs);

        var result = new YamlConfigurationLoader().Load(mutated.Shift, mutated.Anomalies);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == testCase.Code &&
            diagnostic.Document == testCase.Document &&
            diagnostic.Path == testCase.Path &&
            diagnostic.Severity == testCase.Severity);
        Assert.Equal(testCase.IsSuccess, result.IsSuccess);
        if (testCase.IsSuccess)
        {
            Assert.NotNull(result.Configuration);
        }
        else
        {
            Assert.Null(result.Configuration);
        }
    }

    [Fact]
    public void Contract_cases_exactly_cover_the_approved_and_production_validation_code_sets()
    {
        var approvedCodes = ValidationCodeCases.ApprovedCodes;
        var caseCodes = ValidationCodeCases.Cases
            .Select(static testCase => testCase.Code)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var productionCodes = ReadProductionCodes();

        Assert.Equal(111, approvedCodes.Count);
        Assert.Equal(ValidationCodeCases.Cases.Length, caseCodes.Count);
        Assert.Empty(approvedCodes.Except(caseCodes));
        Assert.Empty(caseCodes.Except(approvedCodes));
        Assert.Empty(approvedCodes.Except(productionCodes));
        Assert.Empty(productionCodes.Except(approvedCodes));
        Assert.Empty(productionCodes.Except(caseCodes));
    }

    private static ImmutableHashSet<string> ReadProductionCodes()
    {
        var sources = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "ProductionSources", "YamlConfigurationLoader.cs"),
            Path.Combine(AppContext.BaseDirectory, "ProductionSources", "YamlDocumentParser.cs")
        };

        return sources
            .SelectMany(File.ReadAllLines)
            .SelectMany(static line => ValidationCodePattern().Matches(line).Select(static match => match.Value))
            .ToImmutableHashSet(StringComparer.Ordinal);
    }

    [GeneratedRegex("TLAW-CFG-\\d{3}")]
    private static partial Regex ValidationCodePattern();
}
