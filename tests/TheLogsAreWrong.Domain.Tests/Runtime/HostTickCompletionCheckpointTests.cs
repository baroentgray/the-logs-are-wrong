using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-028")]
public sealed class HostTickCompletionCheckpointTests
{
    private static readonly HostTickCompletionCheckpointService Service = new();

    [Fact]
    public void Progression_starts_at_zero_without_a_checkpoint_or_receipt()
    {
        var configuration = Fixture.LoadP0().Shift;

        var progression = HostTickProgressionEvidence.Create(configuration.ShiftId);

        Assert.Equal(configuration.ShiftId, progression.ShiftId);
        Assert.Equal(ServerTick.Zero, progression.InitialTick);
        Assert.False(progression.HasCompletedTick);
        Assert.Null(progression.LastCompletedTick);
        Assert.Null(progression.LastReceipt);
        Assert.Throws<ArgumentException>(() => HostTickProgressionEvidence.Create(default));
    }

    [Fact]
    public void Initial_tick_zero_evaluates_once_and_publishes_exact_active_references()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = ShiftRuntimeState.Create(configuration);
        var quota = QuotaRuntimeState.Create(configuration);
        var progression = HostTickProgressionEvidence.Create(configuration.ShiftId);

        var result = Advance(progression, lifecycle, shift, quota, ServerTick.Zero, configuration);

        Assert.Same(progression, result.OriginalProgression);
        Assert.Equal(ServerTick.Zero, result.Progression.LastCompletedTick);
        Assert.Same(result.Receipt, result.Progression.LastReceipt);
        Assert.IsType<ShiftCompletionActive>(result.Receipt.Evaluation);
        Assert.Same(lifecycle, result.Receipt.Lifecycle);
        Assert.Same(shift, result.Receipt.ShiftState);
        Assert.Same(quota, result.Receipt.QuotaState);
        Assert.False(result.Receipt.ShiftCompleted);
    }

    [Fact]
    public void Initial_tick_one_is_rejected_without_replacing_progression()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = ShiftRuntimeState.Create(configuration);
        var quota = QuotaRuntimeState.Create(configuration);
        var progression = HostTickProgressionEvidence.Create(configuration.ShiftId);

        var result = Assert.IsType<HostTickCheckpointRejected>(Service.Complete(progression, lifecycle, shift, quota, ServerTick.From(1), configuration));

        Assert.Equal(HostTickCheckpointRejectionReason.SkippedTick, result.Reason);
        Assert.Same(progression, result.Progression);
        Assert.Equal(ServerTick.From(1), result.RequestedTick);
    }

    [Fact]
    public void Default_tick_fails_loudly_before_publication()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = ShiftRuntimeState.Create(configuration);
        var quota = QuotaRuntimeState.Create(configuration);
        var progression = HostTickProgressionEvidence.Create(configuration.ShiftId);

        Assert.Throws<ArgumentOutOfRangeException>(() => Service.Complete(progression, lifecycle, shift, quota, default, configuration));

        Assert.False(progression.HasCompletedTick);
        Assert.Null(progression.LastReceipt);
    }

    [Fact]
    public void Sequential_ticks_accept_only_the_exact_next_tick_and_preserve_the_old_progression()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = ShiftRuntimeState.Create(configuration);
        var quota = QuotaRuntimeState.Create(configuration);
        var initial = HostTickProgressionEvidence.Create(configuration.ShiftId);
        var zero = Advance(initial, lifecycle, shift, quota, ServerTick.Zero, configuration);
        var one = Advance(zero.Progression, zero.Receipt.Lifecycle, zero.Receipt.ShiftState, zero.Receipt.QuotaState, ServerTick.From(1), configuration);

        var backward = Assert.IsType<HostTickCheckpointRejected>(Service.Complete(one.Progression, one.Receipt.Lifecycle, one.Receipt.ShiftState, one.Receipt.QuotaState, ServerTick.Zero, configuration));
        var skipped = Assert.IsType<HostTickCheckpointRejected>(Service.Complete(zero.Progression, zero.Receipt.Lifecycle, zero.Receipt.ShiftState, zero.Receipt.QuotaState, ServerTick.From(2), configuration));

        Assert.Equal(ServerTick.From(1), one.Progression.LastCompletedTick);
        Assert.Equal(HostTickCheckpointRejectionReason.BackwardTick, backward.Reason);
        Assert.Same(one.Progression, backward.Progression);
        Assert.Equal(HostTickCheckpointRejectionReason.SkippedTick, skipped.Reason);
        Assert.Same(zero.Progression, skipped.Progression);
    }

    [Fact]
    public void Exact_same_tick_replay_returns_the_original_progression_and_receipt()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = ShiftRuntimeState.Create(configuration);
        var quota = QuotaRuntimeState.Create(configuration);
        var advanced = Advance(HostTickProgressionEvidence.Create(configuration.ShiftId), lifecycle, shift, quota, ServerTick.Zero, configuration);

        var replay = Assert.IsType<HostTickCheckpointReplayed>(Service.Complete(advanced.Progression, advanced.Receipt.Lifecycle, advanced.Receipt.ShiftState, advanced.Receipt.QuotaState, ServerTick.Zero, configuration));

        Assert.Same(advanced.Progression, replay.Progression);
        Assert.Same(advanced.Receipt, replay.Receipt);
        Assert.Same(lifecycle, replay.Receipt.Lifecycle);
    }

    [Fact]
    public void Same_tick_with_any_different_runtime_reference_is_a_contradictory_replay()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = ShiftRuntimeState.Create(configuration);
        var quota = QuotaRuntimeState.Create(configuration);
        var advanced = Advance(HostTickProgressionEvidence.Create(configuration.ShiftId), lifecycle, shift, quota, ServerTick.Zero, configuration);

        var lifecycleMismatch = Assert.IsType<HostTickCheckpointRejected>(Service.Complete(advanced.Progression, ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning")), shift, quota, ServerTick.Zero, configuration));
        var shiftMismatch = Assert.IsType<HostTickCheckpointRejected>(Service.Complete(advanced.Progression, lifecycle, ShiftRuntimeState.Create(configuration), quota, ServerTick.Zero, configuration));
        var quotaMismatch = Assert.IsType<HostTickCheckpointRejected>(Service.Complete(advanced.Progression, lifecycle, shift, QuotaRuntimeState.Create(configuration), ServerTick.Zero, configuration));

        Assert.All(new[] { lifecycleMismatch, shiftMismatch, quotaMismatch }, result =>
        {
            Assert.Equal(HostTickCheckpointRejectionReason.ContradictoryReplay, result.Reason);
            Assert.Same(advanced.Progression, result.Progression);
        });
    }

    [Fact]
    public void Pressure_cannot_skip_its_exact_deadline_and_can_then_complete_at_tick_600()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("pressure"));
        var shift = ShiftRuntimeState.Create(configuration);
        var quota = QuotaRuntimeState.Create(configuration);
        var advanced = AdvanceThrough(HostTickProgressionEvidence.Create(configuration.ShiftId), lifecycle, shift, quota, 599, configuration);

        var skipped = Assert.IsType<HostTickCheckpointRejected>(Service.Complete(advanced.Progression, advanced.Receipt.Lifecycle, advanced.Receipt.ShiftState, advanced.Receipt.QuotaState, ServerTick.From(601), configuration));
        var deadline = Advance(advanced.Progression, advanced.Receipt.Lifecycle, advanced.Receipt.ShiftState, advanced.Receipt.QuotaState, ServerTick.From(600), configuration);

        Assert.Equal(HostTickCheckpointRejectionReason.SkippedTick, skipped.Reason);
        var completion = Assert.IsType<ShiftCompletionNewlyCompleted>(deadline.Receipt.Evaluation);
        Assert.True(deadline.Receipt.ShiftCompleted);
        Assert.Equal(ShiftCompletionReason.HardDeadline, completion.Completion.Reason);
        Assert.Equal(ServerTick.From(600), completion.Completion.CompletedAt);
    }

    [Fact]
    public void Learning_completes_at_its_exact_tick_840()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = ShiftRuntimeState.Create(configuration);
        var quota = QuotaRuntimeState.Create(configuration);

        var deadline = AdvanceThrough(HostTickProgressionEvidence.Create(configuration.ShiftId), lifecycle, shift, quota, 840, configuration);

        var completion = Assert.IsType<ShiftCompletionNewlyCompleted>(deadline.Receipt.Evaluation);
        Assert.Equal(ShiftCompletionReason.HardDeadline, completion.Completion.Reason);
        Assert.Equal(ServerTick.From(840), deadline.Receipt.CompletedTick);
    }

    [Fact]
    public void Early_completion_replays_exactly_and_blocks_all_later_ticks()
    {
        var configuration = Fixture.LoadP0().Shift;
        var active = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var finalShift = WithAllStates(configuration, LogState.PROCESSED);
        var finalQuota = SettleProcessed(configuration, finalShift, objectivesSatisfied: true);
        var completed = Advance(HostTickProgressionEvidence.Create(configuration.ShiftId), active, finalShift, finalQuota, ServerTick.Zero, configuration);

        var replay = Assert.IsType<HostTickCheckpointReplayed>(Service.Complete(completed.Progression, completed.Receipt.Lifecycle, completed.Receipt.ShiftState, completed.Receipt.QuotaState, ServerTick.Zero, configuration));
        var later = Assert.IsType<HostTickCheckpointRejected>(Service.Complete(completed.Progression, completed.Receipt.Lifecycle, ShiftRuntimeState.Create(configuration), QuotaRuntimeState.Create(configuration), ServerTick.From(1), configuration));

        Assert.Same(completed.Receipt, replay.Receipt);
        Assert.Equal(HostTickCheckpointRejectionReason.ShiftCompleted, later.Reason);
        Assert.Same(completed.Progression, later.Progression);
        Assert.Same(finalShift, completed.Receipt.Lifecycle.Completion!.FinalShiftState);
        Assert.Same(finalQuota, completed.Receipt.Lifecycle.Completion.FinalQuotaState);
    }

    [Fact]
    public void Processed_logs_without_settlement_fail_without_a_receipt()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = WithStates(configuration, LogState.PROCESSED, LogState.AT_INTAKE);
        var quota = QuotaRuntimeState.Create(configuration);
        var progression = HostTickProgressionEvidence.Create(configuration.ShiftId);

        Assert.Throws<InvalidOperationException>(() => Service.Complete(progression, lifecycle, shift, quota, ServerTick.Zero, configuration));

        Assert.False(progression.HasCompletedTick);
        Assert.Null(progression.LastReceipt);
    }

    private static HostTickCheckpointAdvanced Advance(
        HostTickProgressionEvidence progression,
        ShiftLifecycleRuntimeState lifecycle,
        ShiftRuntimeState shift,
        QuotaRuntimeState quota,
        ServerTick tick,
        ShiftConfiguration configuration) =>
        Assert.IsType<HostTickCheckpointAdvanced>(Service.Complete(progression, lifecycle, shift, quota, tick, configuration));

    private static HostTickCheckpointAdvanced AdvanceThrough(
        HostTickProgressionEvidence progression,
        ShiftLifecycleRuntimeState lifecycle,
        ShiftRuntimeState shift,
        QuotaRuntimeState quota,
        long finalTick,
        ShiftConfiguration configuration)
    {
        HostTickCheckpointAdvanced? latest = null;
        for (var tick = 0L; tick <= finalTick; tick++)
        {
            latest = Advance(
                latest?.Progression ?? progression,
                latest?.Receipt.Lifecycle ?? lifecycle,
                latest?.Receipt.ShiftState ?? shift,
                latest?.Receipt.QuotaState ?? quota,
                ServerTick.From(tick),
                configuration);
        }

        return latest ?? throw new InvalidOperationException("At least one checkpoint must be advanced.");
    }

    private static ShiftRuntimeState WithAllStates(ShiftConfiguration configuration, LogState state) =>
        WithStates(configuration, Enumerable.Repeat(state, configuration.Manifest.Length).ToArray());

    private static ShiftRuntimeState WithStates(ShiftConfiguration configuration, params LogState[] states)
    {
        var original = ShiftRuntimeState.Create(configuration);
        var logs = original.Logs;
        for (var index = 0; index < states.Length; index++)
        {
            var log = logs[index];
            logs = logs.SetItem(index, new LogRuntimeState(log.LogId, log.TrueSpecies, log.DeclaredSpecies, log.Anomaly, states[index], log.Flags));
        }

        return CloneWith(original, nameof(ShiftRuntimeState.Logs), logs);
    }

    private static QuotaRuntimeState SettleProcessed(ShiftConfiguration configuration, ShiftRuntimeState shift, bool objectivesSatisfied)
    {
        var quota = QuotaRuntimeState.Create(configuration);
        var processed = shift.Logs.Where(log => log.State == LogState.PROCESSED).ToArray();
        for (var index = 0; index < processed.Length; index++)
        {
            var settlement = objectivesSatisfied
                ? index switch
                {
                    0 => new QuotaSettlement(processed[index].LogId, SpeciesId.From("pine"), 5, 0),
                    1 => new QuotaSettlement(processed[index].LogId, SpeciesId.From("oak"), 4, 0),
                    2 => new QuotaSettlement(processed[index].LogId, null, 0, 2),
                    _ => new QuotaSettlement(processed[index].LogId, null, 0, 0)
                }
                : new QuotaSettlement(processed[index].LogId, null, 0, 0);
            quota = Assert.IsType<QuotaSettlementAccepted>(new QuotaSettlementService().Apply(quota, settlement)).State;
        }

        return quota;
    }

    private static T CloneWith<T>(T source, string name, object? value) where T : class
    {
        var clone = Assert.IsType<T>(typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(source, null));
        FindField(typeof(T), name).SetValue(clone, value);
        return clone;
    }

    private static FieldInfo FindField(Type type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? current.GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null)
            {
                return field;
            }
        }

        throw new MissingFieldException(type.FullName, name);
    }
}
