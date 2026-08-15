using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>
/// TLAW-043 audit evidence. The original descriptor-only audit remains historical. TLAW-047 later authorizes the one
/// narrow Penitent saw-owned interval; Resin and containment descriptors remain data-only and no generic effect runtime
/// is introduced here.
/// </summary>
[Trait("Scope", "TLAW-043")]
public sealed class Tlaw043ArchitectureTests
{
    private static readonly AnomalyId Penitent = AnomalyId.From("PENITENT_TRUNK");
    private static readonly AnomalyId Resin = AnomalyId.From("RESIN_BLASPHEMER");
    private static readonly AnomalyId FalseSpecies = AnomalyId.From("FALSE_SPECIES");

    /// <summary>Identifiers that would exceed the later-authorized narrow Penitent saw-owned window.</summary>
    private static readonly string[] ForbiddenEffectRuntimeConcepts =
    [
        "EffectExecutor", "EffectDispatcher", "EffectRuntime", "EffectApplier", "EffectScheduler",
        "ButtonLock", "NearestLineButton", "ForcedLinePause", "ForcedPause",
        "LineButtonState", "PenaltyRuntime", "MiscreditApplication", "MiscreditExecutor"
    ];

    [Fact]
    public void Domain_remains_zero_package_and_free_of_engine_or_network_dependencies()
    {
        var sourceRoot = DomainSourceRoot();
        Assert.DoesNotContain("<PackageReference", File.ReadAllText(Path.Combine(sourceRoot, "TheLogsAreWrong.Domain.csproj")), StringComparison.Ordinal);

        var references = typeof(HostTickExecutionService).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
        Assert.NotEmpty(references);
        Assert.DoesNotContain(references, reference =>
            reference.Contains("Unity", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Fish", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Steam", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Sockets", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Net.Http", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Yaml", StringComparison.OrdinalIgnoreCase));

        var domainSources = DomainSources();
        Assert.All(
            new[] { "UnityEngine", "FishNet", "Steamworks", "System.Net", "HttpClient", "Socket" },
            token => Assert.DoesNotContain(domainSources, source => source.Contains(token, StringComparison.Ordinal)));
    }

    [Fact]
    public void No_generic_effect_runtime_or_resin_or_containment_executor_exists()
    {
        var domainSources = DomainSources();
        Assert.True(domainSources.Length > 30, $"The domain source scan is vacuous: {domainSources.Length} files.");
        Assert.All(
            ForbiddenEffectRuntimeConcepts,
            concept => Assert.DoesNotContain(domainSources, source => source.Contains(concept, StringComparison.Ordinal)));

        var exported = typeof(HostTickExecutionService).Assembly.GetExportedTypes();
        Assert.True(exported.Length > 50, $"The exported-type scan is vacuous: {exported.Length} types.");
        Assert.All(
            ForbiddenEffectRuntimeConcepts,
            concept => Assert.DoesNotContain(exported, type => type.Name.Contains(concept, StringComparison.Ordinal)));

        // D-015 may branch only in the saw-owned completion/replay boundary; it does not authorize a generic dispatcher.
        var effectTypeConsumers = Directory.GetFiles(PortableAuthoritySourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("EffectType.", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["SawCycleContracts.cs"], effectTypeConsumers);
        Assert.Contains("EffectType.time_penalty", File.ReadAllText(YamlLoaderSourcePath()), StringComparison.Ordinal);
    }

    [Fact]
    public void Effect_type_and_p0_descriptors_retain_their_exact_frozen_values()
    {
        Assert.Equal(new[] { EffectType.time_penalty, EffectType.@lock, EffectType.miscredit }, Enum.GetValues<EffectType>());

        var configuration = Fixture.LoadP0();
        var definitions = configuration.Anomalies.Definitions;
        Assert.Equal(3, definitions.Count);

        // Penitent: unflagged processing rejects the output and retains the eight-second time-penalty descriptor.
        var penitent = definitions[Penitent];
        AssertCorrectOutcome(penitent.Processing.OnCorrect);
        Assert.Equal(LogState.PROCESSED, penitent.Processing.OnIncorrect.TerminalState);
        Assert.Equal(SpeciesCreditRule.none, penitent.Processing.OnIncorrect.QuotaCredit.Species);
        Assert.Equal(0, penitent.Processing.OnIncorrect.QuotaCredit.Units);
        Assert.Equal(0, penitent.Processing.OnIncorrect.CorrectAnomalyDelta);
        var timePenalty = Assert.Single(penitent.Processing.OnIncorrect.Effects);
        Assert.Equal(EffectType.time_penalty, timePenalty.Type);
        Assert.Equal(EffectEventId.From("FALSE_PA_ANNOUNCEMENT"), timePenalty.Event);
        Assert.Equal(8, timePenalty.DurationSeconds);
        Assert.Null(timePenalty.Target);

        // Resin: unflagged processing and the configured wrong holy-water action retain the same ten-second lock.
        var resin = definitions[Resin];
        AssertCorrectOutcome(resin.Processing.OnCorrect);
        Assert.Equal(LogState.PROCESSED, resin.Processing.OnIncorrect.TerminalState);
        Assert.Equal(SpeciesCreditRule.none, resin.Processing.OnIncorrect.QuotaCredit.Species);
        Assert.Equal(0, resin.Processing.OnIncorrect.QuotaCredit.Units);
        Assert.Equal(0, resin.Processing.OnIncorrect.CorrectAnomalyDelta);
        AssertResinLock(Assert.Single(resin.Processing.OnIncorrect.Effects));

        var wrongHolyWater = resin.WrongActions[ItemId.From("holy_water")];
        Assert.True(wrongHolyWater.LeavesStateUnchanged);
        Assert.Null(wrongHolyWater.TerminalState);
        Assert.True(wrongHolyWater.Consumes);
        AssertResinLock(Assert.Single(wrongHolyWater.Effects));

        // False Species: unflagged processing credits the declared species once and retains the miscredit descriptor.
        var falseSpecies = definitions[FalseSpecies];
        AssertCorrectOutcome(falseSpecies.Processing.OnCorrect);
        Assert.Equal(LogState.PROCESSED, falseSpecies.Processing.OnIncorrect.TerminalState);
        Assert.Equal(SpeciesCreditRule.declared_species, falseSpecies.Processing.OnIncorrect.QuotaCredit.Species);
        Assert.Equal(1, falseSpecies.Processing.OnIncorrect.QuotaCredit.Units);
        Assert.Equal(0, falseSpecies.Processing.OnIncorrect.CorrectAnomalyDelta);
        var miscredit = Assert.Single(falseSpecies.Processing.OnIncorrect.Effects);
        Assert.Equal(EffectType.miscredit, miscredit.Type);
        Assert.Equal(EffectEventId.From("CREDIT_TO_DECLARED_SPECIES"), miscredit.Event);
        Assert.Null(miscredit.DurationSeconds);
        Assert.Null(miscredit.Target);
    }

    [Fact]
    public void Containment_prototype_incident_remains_the_frozen_forced_line_pause_placeholder()
    {
        var containment = Fixture.LoadP0().Shift.Containment;

        Assert.Equal("forced_line_pause", containment.PrototypeIncident.Type);
        Assert.Equal(8, containment.PrototypeIncident.DurationSeconds);
        Assert.True(containment.PrototypeIncident.RemainsIncidentUntilRitual);
        Assert.False(containment.PrototypeIncident.RepeatBeforeResolution);

        // The incident descriptor is carried as data; nothing in the Domain pauses the line or holds a pause state.
        var domainSources = DomainSources();
        Assert.All(
            new[] { "forced_line_pause", "ForcedLinePause", "PauseLine", "LinePauseState", "PausedUntil" },
            token => Assert.DoesNotContain(domainSources, source => source.Contains(token, StringComparison.Ordinal)));
        Assert.Equal(
            new[] { ContainmentState.STABLE, ContainmentState.SERVICE_REQUESTED, ContainmentState.OVERDUE, ContainmentState.INCIDENT },
            Enum.GetValues<ContainmentState>());
        Assert.Equal(new[] { LineState.LINE_CLEAR, LineState.LINE_JAMMED, LineState.REPAIRING }, Enum.GetValues<LineState>());
    }

    [Fact]
    public void False_species_declared_credit_is_represented_once_with_no_second_application_boundary()
    {
        var configuration = Fixture.LoadP0();
        var state = ShiftRuntimeState.Create(configuration.Shift);
        Assert.True(state.TryGetLog(LogId.From("log_05"), out var log));
        Assert.Equal(FalseSpecies, log.Anomaly);
        Assert.Equal(SpeciesId.From("oak"), log.TrueSpecies);
        Assert.Equal(SpeciesId.From("pine"), log.DeclaredSpecies);

        // The resolver produces exactly one settlement, and it already carries the whole declared-species consequence.
        var resolution = AnomalyProcessingResolver.Resolve(log, configuration.Anomalies);
        Assert.False(resolution.AllRequiredFlagsPresent);
        Assert.Equal(SpeciesId.From("pine"), resolution.Settlement.CreditedSpecies);
        Assert.Equal(1, resolution.Settlement.CreditedUnits);
        Assert.Equal(0, resolution.Settlement.CorrectAnomalyDelta);
        Assert.Equal(EffectType.miscredit, Assert.Single(resolution.Effects).Type);

        // Applying it is idempotent per log: a second application is refused rather than crediting twice.
        var settlements = new QuotaSettlementService();
        var quota = QuotaRuntimeState.Create(configuration.Shift);
        var first = Assert.IsType<QuotaSettlementAccepted>(settlements.Apply(quota, resolution.Settlement));
        Assert.Equal(1, first.State.GetCreditedUnits(SpeciesId.From("pine")));
        Assert.Equal(1, first.State.TotalCreditedUnits);
        Assert.Equal(0, first.State.CorrectlyProcessedAnomalies);

        var second = Assert.IsType<QuotaSettlementDuplicate>(settlements.Apply(first.State, resolution.Settlement));
        Assert.Same(first.State, second.State);
        Assert.Equal(1, second.State.GetCreditedUnits(SpeciesId.From("pine")));
        Assert.Equal(1, second.State.TotalCreditedUnits);

        // There is exactly one settlement per resolution: no production surface applies a separate miscredit.
        Assert.Equal(typeof(QuotaSettlement), typeof(ProcessingResolution).GetProperty(nameof(ProcessingResolution.Settlement))!.PropertyType);
        Assert.DoesNotContain(
            typeof(QuotaSettlementService).GetMethods().Select(method => method.Name),
            name => name.Contains("Miscredit", StringComparison.Ordinal) || name.Contains("Effect", StringComparison.Ordinal));
    }

    [Fact]
    public void Tlaw042_historical_range_proof_is_pinned_to_the_accepted_merge_rather_than_dynamic_head()
    {
        Assert.Equal("71aee1cc4138c2996e974afc9008eb3536b98ff9", Tlaw042ArchitectureTests.AuthorizedBaseline);
        Assert.Equal("6e4ed1e1a9337af2e5149cbd16f3b971f274a0ab", Tlaw042ArchitectureTests.AcceptedMergeCommit);

        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "tests",
            "TheLogsAreWrong.Domain.Tests",
            "Architecture",
            "Tlaw042ArchitectureTests.cs"));

        Assert.Contains("RunGit(root, \"diff\", \"--name-only\", AuthorizedBaseline, AcceptedMergeCommit)", source, StringComparison.Ordinal);
        Assert.Contains("RunGit(root, \"merge-base\", AuthorizedBaseline, AcceptedMergeCommit)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"HEAD\"", source, StringComparison.Ordinal);

        // The argument-safe invocation is retained: git is started from an argument list, never a parsed command line.
        Assert.Contains("startInfo.ArgumentList.Add(argument)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UseShellExecute = true", source, StringComparison.Ordinal);
    }

    private static void AssertCorrectOutcome(ProcessingOutcome outcome)
    {
        Assert.Equal(LogState.PROCESSED, outcome.TerminalState);
        Assert.Equal(SpeciesCreditRule.true_species, outcome.QuotaCredit.Species);
        Assert.Equal(1, outcome.QuotaCredit.Units);
        Assert.Equal(1, outcome.CorrectAnomalyDelta);
        Assert.Empty(outcome.Effects);
    }

    private static void AssertResinLock(EffectDefinition effect)
    {
        Assert.Equal(EffectType.@lock, effect.Type);
        Assert.Equal(EffectEventId.From("RESIN_BUTTON_LOCK"), effect.Event);
        Assert.Equal(10, effect.DurationSeconds);
        Assert.Equal("nearest_line_button", effect.Target);
    }

    private static string DomainSourceRoot() => Path.Combine(AppContext.BaseDirectory, "DomainSources");

    private static string PortableAuthoritySourceRoot() => Path.Combine(RepositoryRoot(), "src", "TheLogsAreWrong.PortableAuthority");

    private static ImmutableArray<string> DomainSources() => Directory
        .GetFiles(DomainSourceRoot(), "*.cs", SearchOption.AllDirectories)
        .Select(File.ReadAllText)
        .ToImmutableArray();

    private static string YamlLoaderSourcePath() => Path.Combine(AppContext.BaseDirectory, "ProductionSources", "YamlConfigurationLoader.cs");

    private static string RepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root containing AGENTS.md was not found.");
    }
}
