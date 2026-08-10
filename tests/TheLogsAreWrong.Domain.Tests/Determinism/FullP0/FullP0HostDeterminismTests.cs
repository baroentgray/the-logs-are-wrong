using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Determinism;

/// <summary>
/// TLAW-042 repeatability, replay and ambient-nondeterminism guards for the canonical full-host trace projection.
/// </summary>
[Trait("Scope", "TLAW-042")]
public sealed class FullP0HostDeterminismTests
{
    private const int IndependentRunCount = 10;

    /// <summary>Every run builds its own configuration, runtime states and journal from scratch; nothing is shared.</summary>
    private static FullP0HostScenarioRun IndependentRun(Func<FullP0HostScenarioScript> script) =>
        new FullP0HostScenarioDriver().Run(Fixture.LoadP0(), script());

    [Fact]
    public void Canonical_learning_correct_path_projection_is_structurally_equal_across_ten_independent_runs()
    {
        var runs = Enumerable.Range(0, IndependentRunCount)
            .Select(_ => IndependentRun(FullP0HostScenarioScript.LearningCorrectPath))
            .ToArray();

        var baseline = runs[0].Projection;
        Assert.Equal(IndependentRunCount, runs.Length);

        // Non-vacuous: the compared projection really carries a full shift of ordered host evidence.
        Assert.Equal(115, baseline.Events.Length);
        Assert.Equal(173, baseline.HostTickCount);
        Assert.Equal(12, baseline.Logs.Length);
        Assert.True(baseline.LifecycleCompleted);
        Assert.Equal(172, baseline.CompletedAt);

        foreach (var run in runs.Skip(1))
        {
            Assert.Null(baseline.FirstDifference(run.Projection));
            Assert.True(baseline.StructurallyEquals(run.Projection));
        }

        // Independent runs really are independent object graphs.
        Assert.All(runs.Skip(1), run => Assert.NotSame(runs[0].Journal, run.Journal));
        Assert.All(runs.Skip(1), run => Assert.NotSame(runs[0].FinalShiftState, run.FinalShiftState));
        Assert.All(runs.Skip(1), run => Assert.True(runs[0].FinalShiftState.ValueEquals(run.FinalShiftState)));
        Assert.All(runs.Skip(1), run => Assert.True(runs[0].FinalQuotaState.ValueEquals(run.FinalQuotaState)));
        Assert.All(runs.Skip(1), run => Assert.True(runs[0].FinalMovementNoise.ValueEquals(run.FinalMovementNoise)));
        Assert.All(runs.Skip(1), run => Assert.True(runs[0].FinalLineNoise.ValueEquals(run.FinalLineNoise)));
        Assert.All(runs.Skip(1), run => Assert.True(runs[0].FinalLifecycle.ValueEquals(run.FinalLifecycle)));
    }

    [Fact]
    public void Canonical_journal_is_contiguous_monotonic_and_agrees_with_the_final_runtime_and_checkpoint()
    {
        var run = IndependentRun(FullP0HostScenarioScript.LearningCorrectPath);
        var events = run.Journal.Events;

        Assert.NotEmpty(events);
        Assert.Equal(1, events[0].Sequence.Value);
        for (var index = 1; index < events.Count; index++)
        {
            Assert.Equal(events[index - 1].Sequence.Value + 1, events[index].Sequence.Value);
            Assert.True(events[index - 1].ServerTick <= events[index].ServerTick, "Journal ticks must be nondecreasing.");
            Assert.True(events[index - 1].StateVersionAfter <= events[index].StateVersionAfter, "State versions must never regress.");
            var priorVersion = events[index - 1].StateVersionAfter;
            Assert.True(
                events[index].StateVersionAfter == priorVersion || events[index].StateVersionAfter == priorVersion.Next(),
                "State versions must advance one step at a time or stay put for an observation.");
        }

        // Same-tick publications keep the frozen causal order the host planned.
        foreach (var group in events.GroupBy(envelope => envelope.ServerTick))
        {
            var ordered = group.ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                Assert.True(ordered[index - 1].Sequence < ordered[index].Sequence);
            }
        }

        Assert.Equal(run.FinalShiftState.StateVersion, run.Journal.LastStateVersion);
        Assert.Equal(events.Count, run.Journal.Count);
        Assert.Equal(events[^1].Sequence, run.Journal.LastSequence);

        var receipt = run.FinalProgression.LastReceipt!;
        Assert.Equal(run.Ticks[^1].Tick, receipt.CompletedTick);
        Assert.True(receipt.ShiftCompleted);
        Assert.True(receipt.ShiftState.ValueEquals(run.FinalShiftState));
        Assert.True(receipt.QuotaState.ValueEquals(run.FinalQuotaState));
        Assert.True(receipt.Lifecycle.ValueEquals(run.FinalLifecycle));

        // The complete journal validates through the existing replay validation contract.
        var validator = new ReplayValidator();
        var initialBoundary = new SnapshotBoundary
        {
            ShiftId = run.FinalShiftState.ShiftId,
            ServerTick = ServerTick.Zero,
            StateVersion = StateVersion.Zero,
            LastEventSequence = EventSequence.None
        };
        Assert.True(validator.Validate(initialBoundary, events).IsValid);

        var split = events.Count / 2;
        var tailBoundary = new SnapshotBoundary
        {
            ShiftId = run.FinalShiftState.ShiftId,
            ServerTick = events[split - 1].ServerTick,
            StateVersion = events[split - 1].StateVersionAfter,
            LastEventSequence = events[split - 1].Sequence
        };
        Assert.True(validator.Validate(tailBoundary, events.Skip(split).ToArray()).IsValid);
    }

    [Fact]
    public void Sensitivity_variant_is_deterministic_and_diverges_only_at_the_expected_semantic_point()
    {
        var baseline = IndependentRun(FullP0HostScenarioScript.LearningCorrectPath).Projection;
        var variant = IndependentRun(FullP0HostScenarioScript.LearningCorrectPathSensitivityVariant).Projection;
        var repeatedVariant = IndependentRun(FullP0HostScenarioScript.LearningCorrectPathSensitivityVariant).Projection;

        // The variant is itself deterministic.
        Assert.Null(variant.FirstDifference(repeatedVariant));
        Assert.True(variant.StructurallyEquals(repeatedVariant));

        // Exactly one bounded routing choice moved, so the trace must differ.
        Assert.False(baseline.StructurallyEquals(variant));
        Assert.Equal(172, baseline.CompletedAt);
        Assert.Equal(173, variant.CompletedAt);
        Assert.Equal(166L, FullP0HostScenarioScript.SensitivityDivergenceTick);

        // The unchanged prefix is exactly identical, identities included, up to the exact divergence tick.
        var baselinePrefix = baseline.Events.Where(projection => projection.ServerTick < FullP0HostScenarioScript.SensitivityDivergenceTick).ToArray();
        var variantPrefix = variant.Events.Where(projection => projection.ServerTick < FullP0HostScenarioScript.SensitivityDivergenceTick).ToArray();
        Assert.True(baselinePrefix.Length >= 100, $"The compared prefix is vacuous: {baselinePrefix.Length} events.");
        Assert.Equal(baselinePrefix.Length, variantPrefix.Length);
        for (var index = 0; index < baselinePrefix.Length; index++)
        {
            Assert.True(baselinePrefix[index].StructurallyEquals(variantPrefix[index]), baselinePrefix[index].Describe());
        }

        // The first divergence is exactly the moved routing choice.
        Assert.Contains(baseline.Events, projection => projection.ServerTick == 166 && projection.EventTypeId == HostStageSevenEventTypes.LogRouted.ToString());
        Assert.DoesNotContain(variant.Events, projection => projection.ServerTick == 166);
        Assert.Contains(variant.Events, projection => projection.ServerTick == 167 && projection.EventTypeId == HostStageSevenEventTypes.LogRouted.ToString());

        // Both traces still reach the same frozen objective outcome; only their timing moved.
        Assert.True(baseline.ObjectivesSatisfied);
        Assert.True(variant.ObjectivesSatisfied);
        Assert.Equal(baseline.QuotaCreditedBySpecies, variant.QuotaCreditedBySpecies);
        Assert.Equal(baseline.Events.Length, variant.Events.Length);
    }

    [Fact]
    public void Canonical_payload_projection_covers_every_frozen_stage_seven_payload_kind()
    {
        var payloadKinds = typeof(HostStageSevenEventPayload).Assembly
            .GetExportedTypes()
            .Where(type => type.IsSealed && typeof(HostStageSevenEventPayload).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(18, payloadKinds.Length);

        var projector = typeof(FullP0HostTraceProjection)
            .GetMethod(nameof(FullP0HostTraceProjection.ProjectPayload), BindingFlags.Static | BindingFlags.NonPublic)!;
        var source = ReadTlaw042Source("FullP0HostTraceProjection.cs");
        Assert.All(payloadKinds, type => Assert.Contains(type.Name, source, StringComparison.Ordinal));
        Assert.NotNull(projector);

        // Non-vacuous: the projections really produced fields for the payload kinds a full shift reaches.
        var run = new FullP0HostScenarioDriver().Run(Fixture.LoadP0(), FullP0HostScenarioScript.LearningCorrectPath());
        var reachedKinds = run.Projection.Events.Select(projection => projection.PayloadKind).ToImmutableHashSet();
        Assert.True(reachedKinds.Count >= 10, $"Only {reachedKinds.Count} payload kinds were reached.");
        Assert.All(run.Projection.Events, projection => Assert.NotEmpty(projection.PayloadFields));
    }

    [Fact]
    public void Tlaw042_test_sources_carry_no_ambient_nondeterminism()
    {
        var forbidden = new[]
        {
            "Guid.", "Random", "DateTime", "DateTimeOffset", "Stopwatch", "Environment.", "Thread", "Task",
            "GetHashCode", "Directory.", "CurrentCulture", "Assembly.Load", "AppDomain", "Unity",
            "FishNet", "Steam", "Socket", "HttpClient", "BindingFlags", "ConstructorInfo", "Activator"
        };

        foreach (var fileName in Tlaw042ImplementationSourceFiles)
        {
            var source = ReadTlaw042Source(fileName);
            Assert.False(string.IsNullOrWhiteSpace(source), fileName);
            Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
        }

        // This guard file names the forbidden tokens itself, so its own call forms are assembled at compile time from
        // fragments that never appear contiguously in this source.
        var guard = ReadTlaw042Source("FullP0HostDeterminismTests.cs");
        Assert.All(
            new[]
            {
                "Guid" + ".NewGuid", "new " + "Random", "DateTime" + ".Now", "DateTime" + ".UtcNow",
                "Stopwatch" + ".StartNew", "Thread" + ".Sleep", "Environment" + ".GetEnvironmentVariable",
                "Activator" + ".CreateInstance", "CultureInfo" + ".CurrentCulture"
            },
            token => Assert.DoesNotContain(token, guard, StringComparison.Ordinal));

        // Non-vacuous: the scanned driver really calls the authoritative composer exactly once per tick.
        var driver = ReadTlaw042Source("FullP0HostScenarioDriver.cs");
        Assert.Equal(1, CountOccurrences(driver, "_host.Execute("));
        Assert.Contains("private readonly HostTickExecutionService _host = new();", driver, StringComparison.Ordinal);
        Assert.All(
            new[] { "HostStageOneCompletionExecutor", "AcceptedIntentStageExecutor", "HostStageThreeDeadlineExecutor", "HostStageFourSawExecutor", "HostStageFiveFeedExecutor", "HostStageSixDerivedExecutor", "HostStageSevenEventExecutor" },
            stageExecutor => Assert.DoesNotContain(stageExecutor, driver, StringComparison.Ordinal));
    }

    internal static readonly string[] Tlaw042ImplementationSourceFiles =
    [
        "FullP0HostScenarioScript.cs",
        "FullP0HostScenarioDriver.cs",
        "FullP0HostTraceProjection.cs",
        "FullP0HostScenarioTests.cs"
    ];

    internal static readonly string[] Tlaw042SourceFiles =
    [
        "FullP0HostScenarioScript.cs",
        "FullP0HostScenarioDriver.cs",
        "FullP0HostTraceProjection.cs",
        "FullP0HostScenarioTests.cs",
        "FullP0HostDeterminismTests.cs"
    ];

    internal static int CountOccurrences(string source, string token) => source.Split(token).Length - 1;

    /// <summary>The exact cross-platform directory that owns every TLAW-042 implementation file.</summary>
    internal static string Tlaw042SourceDirectory =>
        Path.Combine(FindRepositoryRoot(), "tests", "TheLogsAreWrong.Domain.Tests", "Determinism", "FullP0");

    internal static string ReadTlaw042Source(string fileName) =>
        File.ReadAllText(Path.Combine(Tlaw042SourceDirectory, fileName));

    internal static string FindRepositoryRoot()
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
