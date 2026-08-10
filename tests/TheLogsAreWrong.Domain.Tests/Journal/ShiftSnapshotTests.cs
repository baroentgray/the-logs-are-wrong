using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Tests.Determinism;

namespace TheLogsAreWrong.Domain.Tests.Journal;

/// <summary>
/// TLAW-046 evidence for the frozen snapshot shape, the pristine initial snapshot, the single capture boundary and the
/// restore round-trip across every runtime family the P0 host owns.
/// </summary>
[Trait("Scope", "TLAW-046")]
public sealed class ShiftSnapshotTests
{
    private static readonly ShiftSnapshotCaptureService Capture = new();
    private static readonly ShiftSnapshotRestoreService Restore = new();

    /// <summary>The exact top-level fields frozen by docs/LOG_STATE_MACHINE.md, in document order.</summary>
    private static readonly string[] FrozenTopLevelFields =
    [
        "ShiftId", "ServerTick", "StateVersion", "LastEventSequence", "SchedulerState",
        "Logs", "LineState", "ContainmentState", "Inventory", "Quota", "Objectives"
    ];

    private static (ValidatedConfiguration Configuration, FullP0HostScenarioScript Script, FullP0HostScenarioRun Run) Execute(Func<FullP0HostScenarioScript> factory)
    {
        var configuration = Fixture.LoadP0();
        var script = factory();
        return (configuration, script, new FullP0HostScenarioDriver().Run(configuration, script));
    }

    // ----- shape and immutability -----

    [Fact]
    public void The_snapshot_exposes_exactly_the_frozen_top_level_fields_and_nothing_speculative()
    {
        var actual = typeof(ShiftSnapshot)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name != "Boundary")
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(FrozenTopLevelFields, actual);
    }

    [Fact]
    public void Every_snapshot_property_is_get_only_and_holds_only_values_or_immutable_collections()
    {
        var visited = new HashSet<Type>();
        var queue = new Queue<Type>();
        queue.Enqueue(typeof(ShiftSnapshot));

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (!visited.Add(type))
            {
                continue;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Boundary is a projection onto the pre-existing SnapshotBoundary contract, not a snapshot field.
                if (type == typeof(ShiftSnapshot) && property.Name == "Boundary")
                {
                    continue;
                }

                Assert.True(
                    property.SetMethod is null || IsInitOnly(property.SetMethod),
                    $"{type.Name}.{property.Name} exposes a mutable setter; the snapshot graph must stay immutable.");

                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (propertyType.IsGenericType)
                {
                    var definition = propertyType.GetGenericTypeDefinition();
                    Assert.True(
                        definition == typeof(ImmutableArray<>),
                        $"{type.Name}.{property.Name} uses {definition.Name}; the snapshot graph may only hold ImmutableArray collections.");
                    propertyType = propertyType.GetGenericArguments()[0];
                }

                if (propertyType.Namespace?.StartsWith("TheLogsAreWrong.Domain", StringComparison.Ordinal) == true && !propertyType.IsEnum)
                {
                    queue.Enqueue(propertyType);
                }
            }
        }

        // Positional records generate init-only setters, which keep the value immutable after construction.
        static bool IsInitOnly(MethodInfo setter) =>
            setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));

        // Non-vacuous: the walk really reached the nested snapshot value records.
        Assert.Contains(typeof(SnapshotLog), visited);
        Assert.Contains(typeof(SnapshotSchedulerState), visited);
        Assert.Contains(typeof(SnapshotConfirmationResult), visited);
    }

    [Fact]
    public void Two_independent_live_states_that_are_equivalent_capture_structurally_equal_snapshots()
    {
        var first = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var second = Execute(FullP0HostScenarioScript.LearningCorrectPath);

        var left = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(first.Run.Executions[^1])).Snapshot;
        var right = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(second.Run.Executions[^1])).Snapshot;

        Assert.NotSame(left, right);
        Assert.True(left.StructurallyEquals(right));
        Assert.Null(left.FirstDifference(right));
    }

    [Fact]
    public void Structural_equality_reports_the_first_difference_rather_than_hiding_it()
    {
        var (_, _, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var final = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(run.Executions[^1])).Snapshot;
        var earlier = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(run.Executions[10])).Snapshot;

        Assert.False(final.StructurallyEquals(earlier));
        Assert.NotNull(final.FirstDifference(earlier));
    }

    [Fact]
    public void The_snapshot_manifest_keeps_the_configured_order_and_a_unique_identity_per_log()
    {
        var configuration = Fixture.LoadP0();
        var snapshot = Capture.CreateInitial(configuration.Shift, ProfileId.From("learning"));

        Assert.Equal(configuration.Shift.Manifest.Select(entry => entry.Id), snapshot.Logs.Select(log => log.LogId));
        Assert.Equal(snapshot.Logs.Length, snapshot.Logs.Select(log => log.LogId).Distinct().Count());
    }

    // ----- the pristine initial snapshot -----

    [Fact]
    public void The_initial_snapshot_is_the_pristine_configuration_projection_with_no_executed_tick()
    {
        var configuration = Fixture.LoadP0();
        var snapshot = Capture.CreateInitial(configuration.Shift, ProfileId.From("learning"));

        Assert.Equal(configuration.Shift.ShiftId, snapshot.ShiftId);
        Assert.Equal(ServerTick.Zero, snapshot.ServerTick);
        Assert.Equal(StateVersion.Zero, snapshot.StateVersion);
        Assert.Equal(EventSequence.None, snapshot.LastEventSequence);

        Assert.All(snapshot.Logs, log => Assert.Equal(LogState.SCHEDULED, log.State));
        Assert.All(snapshot.Logs, log => Assert.Null(log.ProcedureProgress));
        Assert.All(snapshot.Logs, log => Assert.Null(log.ConfirmationResult));

        Assert.Null(snapshot.SchedulerState.PendingFeed);
        Assert.Null(snapshot.SchedulerState.ActiveIntakeDeadline);
        Assert.Null(snapshot.SchedulerState.ActiveProcedureHold);
        Assert.Null(snapshot.SchedulerState.ActiveConfirmationTest);
        Assert.Null(snapshot.SchedulerState.ActiveSawCycle);
        Assert.Empty(snapshot.SchedulerState.ProcessedIntentIds);
        Assert.False(snapshot.SchedulerState.Progression.HasCompletedTick);

        Assert.Equal(LineState.LINE_CLEAR, snapshot.LineState.State);
        Assert.Null(snapshot.LineState.MovementNoise);
        Assert.Equal(0, snapshot.Quota.TotalCreditedUnits);
        Assert.Empty(snapshot.Quota.SettledLogIds);
        Assert.Null(snapshot.Objectives.Completion);

        // Configuration-derived state really is present rather than empty.
        Assert.NotEmpty(snapshot.Inventory.Consumables);
        Assert.True(snapshot.Objectives.TargetTotal > 0);
    }

    [Fact]
    public void The_initial_snapshot_carries_the_selected_profile_objectives()
    {
        var configuration = Fixture.LoadP0();
        var learning = Capture.CreateInitial(configuration.Shift, ProfileId.From("learning"));
        var pressure = Capture.CreateInitial(configuration.Shift, ProfileId.From("pressure"));

        Assert.Equal(ProfileId.From("learning"), learning.Objectives.SelectedProfileId);
        Assert.Equal(ProfileId.From("pressure"), pressure.Objectives.SelectedProfileId);
        Assert.False(learning.StructurallyEquals(pressure));
    }

    // ----- capture -----

    [Fact]
    public void Capture_reads_a_published_checkpoint_and_mutates_nothing()
    {
        var (_, _, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var execution = run.Executions[^1];
        var versionBefore = execution.FinalShiftState.StateVersion;
        var journalCountBefore = run.Journal.Count;

        var first = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(execution)).Snapshot;
        var second = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(execution)).Snapshot;

        Assert.True(first.StructurallyEquals(second));
        Assert.Equal(versionBefore, execution.FinalShiftState.StateVersion);
        Assert.Equal(journalCountBefore, run.Journal.Count);
    }

    [Fact]
    public void Capture_records_the_tick_version_and_journal_cursor_of_the_same_completed_trace()
    {
        var (_, _, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);

        foreach (var execution in run.Executions)
        {
            var snapshot = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(execution)).Snapshot;
            Assert.Equal(execution.CurrentTick, snapshot.ServerTick);
            Assert.Equal(execution.FinalShiftState.StateVersion, snapshot.StateVersion);
            Assert.Equal(execution.AfterCursor.LastSequence, snapshot.LastEventSequence);
            Assert.Equal(snapshot.Boundary.LastEventSequence, snapshot.LastEventSequence);
            Assert.Equal(snapshot.Boundary.StateVersion, snapshot.StateVersion);
        }
    }

    [Fact]
    public void A_tick_that_publishes_nothing_still_captures_a_coherent_snapshot()
    {
        var (_, _, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var quiet = run.Executions.First(execution => execution.AfterCursor.LastSequence == execution.BeforeCursor.LastSequence);

        var snapshot = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(quiet)).Snapshot;
        Assert.Equal(quiet.CurrentTick, snapshot.ServerTick);
        Assert.True(snapshot.SchedulerState.Progression.HasCompletedTick);
    }

    // ----- restore round-trip across the runtime families -----

    public static TheoryData<string> RoundTripFamilies() =>
    [
        "pending feed and intake deadline",
        "procedure hold in flight",
        "procedure progress and consumed inventory",
        "confirmation test in flight",
        "confirmation result recorded",
        "saw cycle in flight",
        "jam and repair timing",
        "containment request and ritual",
        "movement and line noise",
        "non-zero quota and settled logs",
        "completed lifecycle and progression"
    ];

    [Theory]
    [MemberData(nameof(RoundTripFamilies))]
    public void Every_runtime_family_round_trips_through_restore_without_loss(string family)
    {
        foreach (var factory in new[]
                 {
                     FullP0HostScenarioScript.LearningCorrectPath,
                     FullP0HostScenarioScript.PressureFullTimeout,
                     FullP0HostScenarioScript.WriteOffAllSuspicious
                 })
        {
            var (configuration, _, run) = Execute(factory);
            var witness = run.Executions
                .Select(execution => Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(execution)).Snapshot)
                .FirstOrDefault(snapshot => Matches(family, snapshot));

            if (witness is null)
            {
                continue;
            }

            var restored = Assert.IsType<ShiftSnapshotRestored>(Restore.Restore(witness, configuration.Shift));
            var reprojected = Capture.CaptureRestored(restored);

            Assert.Null(witness.FirstDifference(reprojected));
            Assert.True(witness.StructurallyEquals(reprojected));
            return;
        }

        Assert.Fail($"no frozen scenario reached the '{family}' family, so this proof would be vacuous");
    }

    private static bool Matches(string family, ShiftSnapshot snapshot) => family switch
    {
        "pending feed and intake deadline" => snapshot.SchedulerState.PendingFeed is not null && snapshot.SchedulerState.ActiveIntakeDeadline is not null,
        "procedure hold in flight" => snapshot.SchedulerState.ActiveProcedureHold is not null,
        "procedure progress and consumed inventory" => snapshot.Logs.Any(log => log.ProcedureProgress is not null) && snapshot.Inventory.Consumables.Any(consumable => consumable.Quantity == 0),
        "confirmation test in flight" => snapshot.SchedulerState.ActiveConfirmationTest is not null,
        "confirmation result recorded" => snapshot.Logs.Any(log => log.ConfirmationResult is not null),
        "saw cycle in flight" => snapshot.SchedulerState.ActiveSawCycle is not null,
        "jam and repair timing" => snapshot.LineState.State == LineState.REPAIRING && snapshot.LineState.ActiveRepairHold is not null,
        "containment request and ritual" => snapshot.ContainmentState.ActiveRitual is not null,
        "movement and line noise" => snapshot.LineState.MovementNoise is not null && snapshot.LineState.LineNoise.LastEvaluatedAt is not null,
        "non-zero quota and settled logs" => snapshot.Quota.TotalCreditedUnits > 0 && !snapshot.Quota.SettledLogIds.IsEmpty,
        "completed lifecycle and progression" => snapshot.Objectives.Completion is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    [Fact]
    public void Every_captured_tick_of_every_scenario_round_trips_through_restore()
    {
        foreach (var factory in new[]
                 {
                     FullP0HostScenarioScript.LearningCorrectPath,
                     FullP0HostScenarioScript.WriteOffAllSuspicious,
                     FullP0HostScenarioScript.IncorrectResin
                 })
        {
            var (configuration, _, run) = Execute(factory);
            foreach (var execution in run.Executions)
            {
                var snapshot = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(execution)).Snapshot;
                var restored = Assert.IsType<ShiftSnapshotRestored>(Restore.Restore(snapshot, configuration.Shift));
                Assert.Null(snapshot.FirstDifference(Capture.CaptureRestored(restored)));
            }
        }
    }

    [Fact]
    public void Restore_rejects_a_snapshot_that_does_not_belong_to_the_supplied_configuration()
    {
        var configuration = Fixture.LoadP0();
        var snapshot = Capture.CreateInitial(configuration.Shift, ProfileId.From("learning"));
        var other = configuration.Shift with { ShiftId = ShiftId.From("TLAW046_OTHER") };

        var rejected = Assert.IsType<ShiftSnapshotRestoreRejected>(Restore.Restore(snapshot, other));
        Assert.Equal(ShiftSnapshotRestoreRejection.ShiftMismatch, rejected.Reason);
        Assert.NotEmpty(rejected.Detail);
    }

    [Fact]
    public void Restore_rejects_a_snapshot_whose_profile_is_not_configured()
    {
        var configuration = Fixture.LoadP0();
        var snapshot = Capture.CreateInitial(configuration.Shift, ProfileId.From("learning"));
        var tampered = new ShiftSnapshot(
            snapshot.ShiftId, snapshot.ServerTick, snapshot.StateVersion, snapshot.LastEventSequence,
            snapshot.SchedulerState, snapshot.Logs, snapshot.LineState, snapshot.ContainmentState,
            snapshot.Inventory, snapshot.Quota,
            new SnapshotObjectives(
                ProfileId.From("not_a_configured_profile"),
                snapshot.Objectives.TargetTotal,
                snapshot.Objectives.TargetBySpecies,
                snapshot.Objectives.MinimumCorrectlyProcessedAnomalies,
                snapshot.Objectives.StartedAt,
                snapshot.Objectives.HardDeadlineDuration,
                snapshot.Objectives.Completion));

        var rejected = Assert.IsType<ShiftSnapshotRestoreRejected>(Restore.Restore(tampered, configuration.Shift));
        Assert.Equal(ShiftSnapshotRestoreRejection.ProfileMismatch, rejected.Reason);
    }

    [Fact]
    public void Restore_rejects_a_snapshot_whose_manifest_does_not_match_the_configuration()
    {
        var configuration = Fixture.LoadP0();
        var snapshot = Capture.CreateInitial(configuration.Shift, ProfileId.From("learning"));
        var tampered = new ShiftSnapshot(
            snapshot.ShiftId, snapshot.ServerTick, snapshot.StateVersion, snapshot.LastEventSequence,
            snapshot.SchedulerState, snapshot.Logs.RemoveAt(snapshot.Logs.Length - 1), snapshot.LineState,
            snapshot.ContainmentState, snapshot.Inventory, snapshot.Quota, snapshot.Objectives);

        var rejected = Assert.IsType<ShiftSnapshotRestoreRejected>(Restore.Restore(tampered, configuration.Shift));
        Assert.Equal(ShiftSnapshotRestoreRejection.ManifestMismatch, rejected.Reason);
    }

    [Fact]
    public void Restore_produces_separate_runtime_values_rather_than_a_single_host_aggregate()
    {
        var (configuration, _, run) = Execute(FullP0HostScenarioScript.LearningCorrectPath);
        var snapshot = Assert.IsType<ShiftSnapshotCaptured>(Capture.Capture(run.Executions[^1])).Snapshot;
        var restored = Assert.IsType<ShiftSnapshotRestored>(Restore.Restore(snapshot, configuration.Shift));

        // D-013: the shift and quota states stay separate, exactly as HostTickExecutionService consumes them.
        Assert.IsType<ShiftRuntimeState>(restored.ShiftState);
        Assert.IsType<QuotaRuntimeState>(restored.QuotaState);
        Assert.Equal(snapshot.StateVersion, restored.ShiftState.StateVersion);
        Assert.Equal(snapshot.Quota.TotalCreditedUnits, restored.QuotaState.TotalCreditedUnits);
        Assert.Equal(snapshot.ServerTick, restored.ServerTick);
        Assert.Equal(snapshot.Boundary, restored.Boundary);
    }
}
