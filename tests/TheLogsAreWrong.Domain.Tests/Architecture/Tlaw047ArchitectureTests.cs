using System.Reflection;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Tests.Architecture;

[Trait("Scope", "TLAW-047")]
public sealed class Tlaw047ArchitectureTests
{
    [Fact]
    public void Domain_remains_zero_package_and_the_frozen_host_and_stage_seven_contracts_are_unchanged()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        Assert.DoesNotContain("<PackageReference", File.ReadAllText(Path.Combine(root, "TheLogsAreWrong.Domain.csproj")), StringComparison.Ordinal);

        var composer = File.ReadAllText(Path.Combine(root, "Runtime", "HostTickExecutionContracts.cs"));
        var stageSeven = File.ReadAllText(Path.Combine(root, "Runtime", "HostStageSevenEventExecutionContracts.cs"));
        Assert.DoesNotContain("SawFailure", composer, StringComparison.Ordinal);
        Assert.DoesNotContain("SawFailure", stageSeven, StringComparison.Ordinal);

        var eventFields = typeof(HostStageSevenEventTypes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(EventTypeId))
            .ToArray();
        Assert.Equal(24, eventFields.Length);
    }

    [Fact]
    public void New_value_is_saw_owned_immutable_and_is_not_a_line_or_generic_effect_runtime()
    {
        Assert.DoesNotContain(typeof(SawFailureWindow).GetProperties(), property => property.SetMethod is not null);
        Assert.DoesNotContain(typeof(ShiftRuntimeState).GetProperties(), property => property.Name == "ActiveSawFailureWindow" && property.SetMethod is not null);

        var root = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var sources = new[]
        {
            File.ReadAllText(Path.Combine(root, "Scheduler", "SawCycleContracts.cs")),
            File.ReadAllText(Path.Combine(root, "Runtime", "ShiftRuntimeState.cs")),
            File.ReadAllText(Path.Combine(root, "Journal", "ShiftSnapshotContracts.cs")),
            File.ReadAllText(Path.Combine(root, "Journal", "ShiftSnapshotCaptureContracts.cs")),
            File.ReadAllText(Path.Combine(root, "Journal", "ShiftSnapshotRestoreContracts.cs")),
            File.ReadAllText(Path.Combine(root, "Journal", "ShiftReplayReductionState.cs")),
            File.ReadAllText(Path.Combine(root, "Journal", "ShiftReplayReducerContracts.cs"))
        };

        foreach (var forbidden in new[] { "EffectDispatcher", "EffectExecutor", "GenericEffect", "ButtonLock", "ForcedLinePause", "UnityEngine", "FishNet", "Steamworks", "System.Net", "Task", "Thread", "Timer" })
        {
            Assert.DoesNotContain(sources, source => source.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Snapshot_shape_remains_the_frozen_eleven_top_level_fields_while_scheduler_state_carries_the_window()
    {
        var topLevel = typeof(ShiftSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name != nameof(ShiftSnapshot.Boundary))
            .ToArray();
        Assert.Equal(11, topLevel.Length);
        Assert.NotNull(typeof(SnapshotSchedulerState).GetProperty(nameof(SnapshotSchedulerState.ActiveSawFailureWindow)));
        Assert.Null(typeof(ShiftSnapshot).GetProperty(nameof(SnapshotSchedulerState.ActiveSawFailureWindow)));
    }
}
