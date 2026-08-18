using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

/// <summary>
/// TLAW-046 boundary guards. Snapshot capture, restore and replay are pure Domain reconstruction: they add no engine,
/// serialization or network dependency, no generic authoritative host aggregate, no effect execution and no new
/// stage-7 event semantics, and they never call gameplay to reproduce a shift.
/// </summary>
[Trait("Scope", "TLAW-046")]
public sealed class Tlaw046ArchitectureTests
{
    /// <summary>The production files this increment owns.</summary>
    private static readonly string[] Tlaw046SourceFiles =
    [
        "ShiftSnapshotContracts.cs",
        "ShiftSnapshotCaptureContracts.cs",
        "ShiftSnapshotRestoreContracts.cs",
        "ShiftReplayReducerContracts.cs",
        "ShiftReplayReductionState.cs"
    ];

    /// <summary>Identifiers that would exist if snapshot/replay had grown a host aggregate or an effect runtime.</summary>
    private static readonly string[] ForbiddenConcepts =
    [
        "HostAggregate", "AuthoritativeHostState", "ShiftAggregate", "HostState",
        "EffectExecutor", "EffectDispatcher", "EffectRuntime", "EffectApplier",
        "ButtonLock", "ForcedLinePause", "TimePenalty"
    ];

    [Fact]
    public void Domain_remains_zero_package_and_free_of_engine_serialization_or_network_dependencies()
    {
        Assert.DoesNotContain(
            "<PackageReference",
            File.ReadAllText(Path.Combine(DomainSourceRoot(), "TheLogsAreWrong.Domain.csproj")),
            StringComparison.Ordinal);

        var references = typeof(ShiftReplayService).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
        Assert.NotEmpty(references);
        Assert.DoesNotContain(references, reference =>
            reference.Contains("Unity", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Fish", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Steam", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Sockets", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Net.Http", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Yaml", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Json", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Xml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_snapshot_and_replay_sources_contain_no_ambient_nondeterminism_or_persistence()
    {
        var sources = Tlaw046Sources();
        Assert.Equal(Tlaw046SourceFiles.Length, sources.Length);

        var forbidden = new[]
        {
            "DateTime", "DateTimeOffset", "Stopwatch", "Environment.TickCount",
            "Random", "Guid.NewGuid", "Task", "Thread", "Timer", "lock (",
            "File.", "Directory.", "Stream", "Serializ", "JsonSerializer", "XmlWriter",
            "UnityEngine", "FishNet", "Steamworks", "System.Net", "HttpClient", "Socket"
        };

        foreach (var token in forbidden)
        {
            Assert.DoesNotContain(sources, source => source.Text.Contains(token, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void No_generic_authoritative_host_aggregate_and_no_effect_runtime_was_introduced()
    {
        var sources = Tlaw046Sources();
        foreach (var concept in ForbiddenConcepts)
        {
            Assert.DoesNotContain(sources, source => source.Text.Contains(concept, StringComparison.Ordinal));
        }

        // TLAW-064 / D-020 H2: the host-tick cut now lives in TheLogsAreWrong.PortableAuthority, so the
        // authority surface this guard scans spans both production owners.
        var exported = typeof(ShiftReplayService).Assembly.GetExportedTypes()
            .Concat(typeof(HostTickExecutionService).Assembly.GetExportedTypes())
            .ToArray();
        Assert.True(exported.Length > 50, $"The exported-type scan is vacuous: {exported.Length} types.");
        foreach (var concept in ForbiddenConcepts)
        {
            Assert.DoesNotContain(exported, type => type.Name.Contains(concept, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void The_host_tick_still_consumes_separate_runtime_states_exactly_as_D_013_requires()
    {
        var execute = typeof(HostTickExecutionService).GetMethod(nameof(HostTickExecutionService.Execute));
        Assert.NotNull(execute);

        var parameters = execute!.GetParameters();
        Assert.Equal(15, parameters.Length);
        Assert.Equal(typeof(ShiftRuntimeState), parameters[0].ParameterType);
        Assert.Equal(typeof(QuotaRuntimeState), parameters[1].ParameterType);

        // No parameter is a snapshot, a restoration result or any composite aggregate introduced by TLAW-046.
        Assert.DoesNotContain(parameters, parameter =>
            parameter.ParameterType == typeof(ShiftSnapshot) ||
            parameter.ParameterType == typeof(ShiftSnapshotRestored));
    }

    [Fact]
    public void Restore_returns_separate_runtime_values_and_is_never_a_host_input_aggregate()
    {
        // The restoration result is a result type only: nothing in the Domain accepts one as a parameter.
        var consumers = typeof(ShiftReplayService).Assembly.GetTypes()
            .Where(type => type != typeof(ShiftSnapshotRestored))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => !method.IsSpecialName && method.DeclaringType != typeof(ShiftSnapshotCaptureService))
            .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ShiftSnapshotRestored)))
            .Select(method => $"{method.DeclaringType?.Name}.{method.Name}")
            .ToArray();

        Assert.Empty(consumers);
    }

    [Fact]
    public void The_frozen_stage_seven_event_catalog_is_unchanged_and_carries_no_replay_only_addition()
    {
        var catalog = typeof(HostStageSevenEventTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(EventTypeId))
            .ToArray();

        Assert.Equal(24, catalog.Length);
        Assert.DoesNotContain(
            typeof(HostStageSevenEventTypes).GetFields(BindingFlags.Public | BindingFlags.Static),
            field => field.FieldType != typeof(EventTypeId));

        // TLAW-046 owns no part of the catalog file.
        var catalogSource = File.ReadAllText(Path.Combine(DomainSourceRoot(), "Runtime", "HostStageSevenEventExecutionContracts.cs"));
        foreach (var identifier in new[] { "ShiftSnapshot", "ShiftReplay", "SnapshotLog", "ReplayEventKind", "ReductionContext" })
        {
            Assert.DoesNotContain(identifier, catalogSource, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_reducer_never_calls_gameplay_to_reproduce_a_shift()
    {
        var reducer = Tlaw046Sources()
            .Where(source => source.Name is "ShiftReplayReducerContracts.cs" or "ShiftReplayReductionState.cs")
            .ToArray();
        Assert.Equal(2, reducer.Length);

        var gameplayEntryPoints = new[]
        {
            // No stage of the authoritative tick is executed to reproduce a shift.
            "HostTickExecutionService", "IntentAcceptance", ".Execute(",
            // Replay is driven purely by journalled evidence, so it never reruns an original intent.
            "AcceptedIntentTickBatch", "AuthoritativeAcceptedIntent", "IntentEnvelope"
        };

        foreach (var entryPoint in gameplayEntryPoints)
        {
            Assert.DoesNotContain(reducer, source => source.Text.Contains(entryPoint, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Replay_reuses_the_existing_snapshot_boundary_and_ordering_validator()
    {
        var reducer = Tlaw046Sources().Single(source => source.Name == "ShiftReplayReducerContracts.cs").Text;

        Assert.Contains("ReplayValidator", reducer, StringComparison.Ordinal);
        Assert.Contains("SnapshotBoundary", File.ReadAllText(Path.Combine(DomainSourceRoot(), "Journal", "ShiftSnapshotContracts.cs")), StringComparison.Ordinal);

        // The ordering taxonomy stays the pre-existing one; TLAW-046 adds only a separate semantic taxonomy.
        Assert.Equal(10, Enum.GetValues<ReplayAnomaly>().Length);
        Assert.Equal(9, Enum.GetValues<ShiftReplaySemanticFailure>().Length);
    }

    [Fact]
    public void Snapshot_capture_is_the_only_projection_boundary_and_appends_nothing()
    {
        var capture = Tlaw046Sources().Single(source => source.Name == "ShiftSnapshotCaptureContracts.cs").Text;

        Assert.DoesNotContain("Append", capture, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAppend", capture, StringComparison.Ordinal);
        Assert.DoesNotContain("IEventJournal", capture, StringComparison.Ordinal);

        var publicProjections = typeof(ShiftSnapshotCaptureService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["Capture", "CaptureRestored", "CreateInitial"], publicProjections);
    }

    [Fact]
    public void Only_one_production_seam_was_added_outside_the_new_TLAW_046_files()
    {
        var shiftState = File.ReadAllText(Path.Combine(DomainSourceRoot(), "Runtime", "ShiftRuntimeState.cs"));

        // The single seam is internal, reuses the existing pristine construction and validates the manifest.
        Assert.Contains("internal static ShiftRuntimeState RestoreForSnapshot(", shiftState, StringComparison.Ordinal);
        Assert.DoesNotContain("public static ShiftRuntimeState RestoreForSnapshot(", shiftState, StringComparison.Ordinal);
        Assert.Contains("var pristine = Create(configuration);", shiftState, StringComparison.Ordinal);
    }

    private sealed record SourceFile(string Name, string Text);

    private static ImmutableArray<SourceFile> Tlaw046Sources() => Directory
        .GetFiles(Path.Combine(DomainSourceRoot(), "Journal"), "*.cs", SearchOption.TopDirectoryOnly)
        .Where(path => Tlaw046SourceFiles.Contains(Path.GetFileName(path), StringComparer.Ordinal))
        .Select(path => new SourceFile(Path.GetFileName(path), File.ReadAllText(path)))
        .ToImmutableArray();

    private static string DomainSourceRoot() => Path.Combine(AppContext.BaseDirectory, "DomainSources");
}
