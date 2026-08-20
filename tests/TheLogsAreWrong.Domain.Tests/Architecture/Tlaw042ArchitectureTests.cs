using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Sequencing;
using TheLogsAreWrong.Domain.Tests.Determinism;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>
/// TLAW-042 scope guard. The increment is test-only: it must add no production behaviour, no production file, no
/// package, no effect runtime and no host aggregate, and must leave the frozen seven-stage host and the frozen
/// Gate 0 configuration exactly as they are.
/// </summary>
[Trait("Scope", "TLAW-042")]
public sealed class Tlaw042ArchitectureTests
{
    private static readonly string[] Tlaw042Markers = ["TLAW-042", "Tlaw042", "FullP0Host"];

    private static readonly string[] ForbiddenRuntimeConcepts =
    [
        "EffectExecutor", "EffectDispatcher", "EffectRuntime", "ButtonLock", "ButtonLockRuntime",
        "NearestLineButton", "ForcedLinePause", "ForcedPause", "TimePenaltyRuntime", "MiscreditApplication",
        "HostGameState", "HostRuntimeAggregate", "HostAggregate", "ReplayReducer", "SnapshotSerializer"
    ];

    [Fact]
    public void Every_tlaw042_implementation_file_lives_only_under_the_test_project()
    {
        var root = FindRepositoryRoot();
        var sources = EnumerateRepositoryCSharpSources(root);
        Assert.True(sources.Length > 120, $"The cross-platform source scan is vacuous: {sources.Length} files.");

        var testProjectPrefix = Path.Combine(root, "tests") + Path.DirectorySeparatorChar;
        var owning = sources
            .Where(path => Tlaw042Markers.Any(marker => File.ReadAllText(path).Contains(marker, StringComparison.Ordinal)))
            .ToArray();

        Assert.NotEmpty(owning);
        Assert.All(owning, path => Assert.StartsWith(testProjectPrefix, path, StringComparison.Ordinal));

        var determinism = FullP0HostDeterminismTests.Tlaw042SourceDirectory;
        Assert.All(FullP0HostDeterminismTests.Tlaw042SourceFiles, fileName => Assert.True(File.Exists(Path.Combine(determinism, fileName)), fileName));
        Assert.True(File.Exists(Path.Combine(root, "tests", "TheLogsAreWrong.Domain.Tests", "Architecture", "Tlaw042ArchitectureTests.cs")));
        Assert.Equal(
            FullP0HostDeterminismTests.Tlaw042SourceFiles.Length,
            Directory.GetFiles(determinism, "*.cs", SearchOption.AllDirectories).Length);
    }

    /// <summary>
    /// The accepted TLAW-042 range must contain no production edit at all, whether or not the edit mentions this
    /// increment. The range is a fixed historical pair — the authorized Gate-1 baseline and the accepted TLAW-042
    /// merge — so this proof stays true for the accepted history and does not depend on wherever <c>HEAD</c> later
    /// moves. Neither endpoint may be replaced by a dynamic revision.
    /// </summary>
    [Fact]
    public void Accepted_tlaw042_historical_range_changed_only_test_project_paths()
    {
        var root = FindRepositoryRoot();
        var changed = RunGit(root, "diff", "--name-only", AuthorizedBaseline, AcceptedMergeCommit);

        // Non-vacuous: the range really is the exact six accepted TLAW-042 files.
        Assert.Equal(AcceptedTlaw042Paths, changed);
        Assert.All(changed, path => Assert.StartsWith(AuthorizedPathPrefix, path, StringComparison.Ordinal));
        Assert.All(
            UnauthorizedRootPrefixes,
            prefix => Assert.DoesNotContain(changed, path => path.StartsWith(prefix, StringComparison.Ordinal)));

        // The baseline really is an ancestor of the accepted merge, so the range above is the whole increment.
        Assert.Equal([AuthorizedBaseline], RunGit(root, "merge-base", AuthorizedBaseline, AcceptedMergeCommit));
    }

    [Fact]
    public void No_production_source_configuration_or_documentation_file_was_changed_for_tlaw042()
    {
        var root = FindRepositoryRoot();
        var productionRoots = new[] { "src", "data", "docs", "source", "tools" };

        foreach (var relative in productionRoots)
        {
            var directory = Path.Combine(root, relative);
            Assert.True(Directory.Exists(directory), directory);
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .ToArray();
            Assert.NotEmpty(files);
            Assert.All(files, path => Assert.DoesNotContain("Tlaw042", Path.GetFileName(path), StringComparison.Ordinal));
            Assert.All(files, path => Assert.DoesNotContain("FullP0Host", Path.GetFileName(path), StringComparison.Ordinal));
        }

        // No production C# source mentions this increment at all.
        var productionSources = EnumerateRepositoryCSharpSources(root)
            .Where(path => !path.StartsWith(Path.Combine(root, "tests") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .ToArray();
        Assert.True(productionSources.Length > 40, $"The production source scan is vacuous: {productionSources.Length} files.");
        Assert.All(productionSources, path => Assert.All(Tlaw042Markers, marker => Assert.DoesNotContain(marker, File.ReadAllText(path), StringComparison.Ordinal)));
    }

    [Fact]
    public void Frozen_gate_zero_configuration_still_loads_to_its_exact_approved_values()
    {
        var configuration = Fixture.LoadP0();

        Assert.Equal(ShiftId.From("P0_SHIFT_A"), configuration.Shift.ShiftId);
        Assert.Equal(new ShiftSeed(47001), configuration.Shift.Seed);
        Assert.Equal(new ShiftProfile(60, 840), configuration.Shift.Profiles[ProfileId.From("learning")]);
        Assert.Equal(new ShiftProfile(45, 600), configuration.Shift.Profiles[ProfileId.From("pressure")]);
        Assert.Equal(9, configuration.Shift.Objectives.Quota.Total);
        Assert.Equal(5, configuration.Shift.Objectives.Quota.BySpecies[SpeciesId.From("pine")]);
        Assert.Equal(4, configuration.Shift.Objectives.Quota.BySpecies[SpeciesId.From("oak")]);
        Assert.Equal(2, configuration.Shift.Objectives.MinCorrectlyProcessedAnomalies);
        Assert.Equal(12, configuration.Shift.Manifest.Length);
        Assert.Equal(6, configuration.Shift.Scheduler.SawCycleSeconds);
        Assert.Equal(6, configuration.Shift.Scheduler.RepairHoldSeconds);
        Assert.Equal(5, configuration.Shift.Scheduler.NormalFeedDelaySeconds);
        Assert.Equal(2, configuration.Shift.Scheduler.EarlyFeedDelaySeconds);
        Assert.Equal(2, configuration.Shift.Scheduler.MovementNoiseSeconds);
        Assert.Equal(4, configuration.Shift.Containment.RitualHoldSeconds);
        Assert.Equal(20, configuration.Shift.Containment.ServiceRequestedGraceSeconds);
        Assert.Equal(10, configuration.Shift.Containment.OverdueSeconds);
        Assert.Equal(90, configuration.Shift.Containment.IntervalByDangerWeight["1"]);

        // The Gate-2 placeholder incident remains configured data only.
        Assert.Equal("forced_line_pause", configuration.Shift.Containment.PrototypeIncident.Type);
        Assert.Equal(8, configuration.Shift.Containment.PrototypeIncident.DurationSeconds);
        Assert.Equal(3, configuration.Anomalies.Definitions.Count);
    }

    [Fact]
    public void Domain_remains_zero_package_and_free_of_engine_or_network_dependencies()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        Assert.True(Directory.Exists(sourceRoot));
        Assert.DoesNotContain("<PackageReference", File.ReadAllText(Path.Combine(sourceRoot, "TheLogsAreWrong.Domain.csproj")), StringComparison.Ordinal);

        var references = typeof(HostTickExecutionService).Assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty).ToArray();
        Assert.NotEmpty(references);
        Assert.DoesNotContain(references, reference =>
            reference.Contains("Unity", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Fish", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Steam", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Net.Http", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Sockets", StringComparison.OrdinalIgnoreCase));

        var testProject = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tests", "TheLogsAreWrong.Domain.Tests", "TheLogsAreWrong.Domain.Tests.csproj"));
        Assert.Equal(3, testProject.Split("<PackageReference").Length - 1);
        Assert.Contains("Microsoft.NET.Test.Sdk", testProject, StringComparison.Ordinal);
        Assert.Contains("xunit.v3", testProject, StringComparison.Ordinal);
        Assert.Contains("xunit.runner.visualstudio", testProject, StringComparison.Ordinal);
    }

    [Fact]
    public void No_effect_executor_button_lock_or_forced_pause_runtime_exists_in_the_domain()
    {
        var domainSources = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "DomainSources"), "*.cs", SearchOption.AllDirectories);
        Assert.True(domainSources.Length > 30, $"The domain source scan is vacuous: {domainSources.Length} files.");
        var combined = domainSources.Select(File.ReadAllText).ToArray();

        Assert.All(ForbiddenRuntimeConcepts, concept => Assert.DoesNotContain(combined, source => source.Contains(concept, StringComparison.Ordinal)));

        var exported = typeof(HostTickExecutionService).Assembly.GetExportedTypes();
        Assert.True(exported.Length > 50, $"The exported-type scan is vacuous: {exported.Length} types.");
        Assert.All(ForbiddenRuntimeConcepts, concept => Assert.DoesNotContain(exported, type => type.Name.Contains(concept, StringComparison.Ordinal)));

        // The frozen effect descriptors remain configured data with no execution surface.
        Assert.Equal(new[] { EffectType.time_penalty, EffectType.@lock, EffectType.miscredit }, Enum.GetValues<EffectType>());
        Assert.All(
            exported.Where(type => type != typeof(EffectDefinition)),
            type => Assert.DoesNotContain(
                type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly),
                method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(EffectDefinition))));
    }

    [Fact]
    public void Canonical_seven_stage_host_and_separate_immutable_state_families_are_unchanged()
    {
        Assert.Equal(
            [
                HostTickStage.hold_and_procedure_completions,
                HostTickStage.accepted_intents_by_server_receive_sequence,
                HostTickStage.deadline_expirations,
                HostTickStage.saw_transitions,
                HostTickStage.feed_and_auto_routes,
                HostTickStage.derived_states,
                HostTickStage.event_emission
            ],
            HostTickStages.CanonicalOrder);

        var execute = Assert.Single(
            typeof(HostTickExecutionService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => method.Name == "Execute");
        Assert.Equal(14, execute.GetParameters().Length);

        // D-013: shift and quota runtime remain separate immutable state families passed independently to the host.
        var parameterTypes = execute.GetParameters().Select(parameter => parameter.ParameterType).ToImmutableArray();
        Assert.Equal(typeof(ShiftRuntimeState), parameterTypes[0]);
        Assert.Equal(typeof(QuotaRuntimeState), parameterTypes[1]);
        Assert.Equal(typeof(MovementNoiseRuntimeState), parameterTypes[2]);
        Assert.Equal(typeof(LineNoiseRuntimeState), parameterTypes[3]);
        Assert.Equal(typeof(HostTickProgressionEvidence), parameterTypes[4]);
        Assert.Equal(typeof(ShiftLifecycleRuntimeState), parameterTypes[5]);
        Assert.Equal(typeof(IEventJournal), parameterTypes[8]);

        Assert.DoesNotContain(typeof(ShiftRuntimeState).GetProperties(), property => typeof(QuotaRuntimeState).IsAssignableFrom(property.PropertyType));
        Assert.DoesNotContain(typeof(QuotaRuntimeState).GetProperties(), property => typeof(ShiftRuntimeState).IsAssignableFrom(property.PropertyType));
        Assert.All(typeof(ShiftRuntimeState).GetProperties(BindingFlags.Public | BindingFlags.Instance), property => Assert.Null(property.SetMethod));
        Assert.All(typeof(QuotaRuntimeState).GetProperties(BindingFlags.Public | BindingFlags.Instance), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void Tlaw042_driver_uses_the_authoritative_composer_and_introduces_no_production_aggregate()
    {
        var driver = FullP0HostDeterminismTests.ReadTlaw042Source("FullP0HostScenarioDriver.cs");
        Assert.False(string.IsNullOrWhiteSpace(driver));
        Assert.Contains("HostTickExecutionService", driver, StringComparison.Ordinal);
        Assert.All(ForbiddenRuntimeConcepts, concept => Assert.DoesNotContain(concept, driver, StringComparison.Ordinal));

        var driverTypes = typeof(Tlaw042ArchitectureTests).Assembly.GetTypes()
            .Where(type => type.Namespace == "TheLogsAreWrong.Domain.Tests.Determinism"
                && type.Name.StartsWith("FullP0", StringComparison.Ordinal)
                && !type.Name.EndsWith("Tests", StringComparison.Ordinal))
            .ToArray();
        Assert.True(driverTypes.Length >= 8, $"The TLAW-042 driver-type scan is vacuous: {driverTypes.Length} types.");
        Assert.All(driverTypes, type => Assert.False(type.IsPublic, $"{type.Name} must not widen the test surface."));
        Assert.All(driverTypes, type => Assert.Equal(typeof(Tlaw042ArchitectureTests).Assembly, type.Assembly));
    }

    /// <summary>The exact authorized Gate-1 baseline this increment was implemented from.</summary>
    internal const string AuthorizedBaseline = "71aee1cc4138c2996e974afc9008eb3536b98ff9";

    /// <summary>The exact accepted TLAW-042 merge commit. Together with the baseline it fixes the historical range.</summary>
    internal const string AcceptedMergeCommit = "6e4ed1e1a9337af2e5149cbd16f3b971f274a0ab";

    private const string AuthorizedPathPrefix = "tests/TheLogsAreWrong.Domain.Tests/";

    /// <summary>The exact six files the accepted TLAW-042 range changed, in git's own ordering.</summary>
    private static readonly string[] AcceptedTlaw042Paths =
    [
        "tests/TheLogsAreWrong.Domain.Tests/Architecture/Tlaw042ArchitectureTests.cs",
        "tests/TheLogsAreWrong.Domain.Tests/Determinism/FullP0/FullP0HostDeterminismTests.cs",
        "tests/TheLogsAreWrong.Domain.Tests/Determinism/FullP0/FullP0HostScenarioDriver.cs",
        "tests/TheLogsAreWrong.Domain.Tests/Determinism/FullP0/FullP0HostScenarioScript.cs",
        "tests/TheLogsAreWrong.Domain.Tests/Determinism/FullP0/FullP0HostScenarioTests.cs",
        "tests/TheLogsAreWrong.Domain.Tests/Determinism/FullP0/FullP0HostTraceProjection.cs"
    ];

    private static readonly string[] UnauthorizedRootPrefixes = ["src/", "data/", "docs/", "source/", "tools/"];

    /// <summary>
    /// Runs git with an argument list rather than a parsed command line, so no path or revision is ever shell-quoted.
    /// Git always reports repository paths with forward slashes, so the returned values are platform-independent.
    /// </summary>
    private static string[] RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("git could not be started for the TLAW-042 range guard.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {standardError}");
        return standardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
        path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static string[] EnumerateRepositoryCSharpSources(string root) => new[] { "src", "tools", "tests" }
        .Select(relative => Path.Combine(root, relative))
        .Where(Directory.Exists)
        .SelectMany(directory => Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
        .Where(path => !IsBuildOutput(path))
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();

    private static string FindRepositoryRoot()
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
