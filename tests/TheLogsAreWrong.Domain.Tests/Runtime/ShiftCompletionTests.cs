using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-027")]
public sealed class ShiftCompletionTests
{
    private static readonly ShiftCompletionEvaluationService Service = new();

    [Theory]
    [InlineData("learning", 840)]
    [InlineData("pressure", 600)]
    public void Create_derives_the_selected_profile_and_exact_hard_deadline(string profile, long deadline)
    {
        var configuration = Fixture.LoadP0().Shift;

        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From(profile));

        Assert.Equal(configuration.ShiftId, lifecycle.ShiftId);
        Assert.Equal(ProfileId.From(profile), lifecycle.SelectedProfileId);
        Assert.Equal(ServerTick.Zero, lifecycle.StartedAt);
        Assert.Equal(deadline, lifecycle.HardDeadlineDuration.Value);
        Assert.Equal(ServerTick.From(deadline), lifecycle.HardDeadlineAt);
        Assert.False(lifecycle.IsCompleted);
        Assert.Null(lifecycle.Completion);
    }

    [Fact]
    public void Create_rejects_default_missing_or_malformed_configuration_evidence()
    {
        var configuration = Fixture.LoadP0().Shift;
        var missingProfiles = configuration with { Profiles = ImmutableDictionary<ProfileId, ShiftProfile>.Empty };
        var invalidDeadline = configuration with
        {
            Profiles = configuration.Profiles.SetItem(ProfileId.From("learning"), new ShiftProfile(60, 0))
        };
        var contradictoryObjectives = configuration with
        {
            Objectives = configuration.Objectives with
            {
                Quota = configuration.Objectives.Quota with { Total = configuration.Objectives.Quota.Total + 1 }
            }
        };

        Assert.Throws<ArgumentException>(() => ShiftLifecycleRuntimeState.Create(configuration, default));
        Assert.Throws<ArgumentException>(() => ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("missing")));
        Assert.Throws<ArgumentException>(() => ShiftLifecycleRuntimeState.Create(missingProfiles, ProfileId.From("learning")));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShiftLifecycleRuntimeState.Create(invalidDeadline, ProfileId.From("learning")));
        Assert.Throws<ArgumentException>(() => ShiftLifecycleRuntimeState.Create(contradictoryObjectives, ProfileId.From("learning")));
        Assert.Throws<ArgumentNullException>(() => ShiftLifecycleRuntimeState.Create(null!, ProfileId.From("learning")));
    }

    [Fact]
    public void Evaluate_returns_an_exact_reference_active_no_op_before_the_deadline()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = ShiftRuntimeState.Create(configuration);
        var quota = QuotaRuntimeState.Create(configuration);

        var result = Assert.IsType<ShiftCompletionActive>(Service.Evaluate(lifecycle, shift, quota, ServerTick.Zero, configuration));

        Assert.Same(lifecycle, result.Lifecycle);
        Assert.Same(shift, result.ShiftState);
        Assert.Same(quota, result.QuotaState);
        Assert.False(result.AllLogsTerminal);
        Assert.False(result.HardDeadlineReached);
        Assert.Same(lifecycle, result.Lifecycle);
    }

    [Fact]
    public void Evaluate_keeps_partial_processed_and_written_off_inputs_active_before_the_deadline()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = WithStates(configuration, LogState.PROCESSED, LogState.HELD_WRITTEN_OFF, LogState.AT_INTAKE);
        var quota = QuotaFor(configuration, shift, objectivesSatisfied: false);

        var result = Assert.IsType<ShiftCompletionActive>(Service.Evaluate(lifecycle, shift, quota, ServerTick.From(839), configuration));

        Assert.Same(lifecycle, result.Lifecycle);
        Assert.Same(shift, result.ShiftState);
        Assert.Same(quota, result.QuotaState);
        Assert.False(result.AllLogsTerminal);
    }

    [Fact]
    public void Evaluate_completes_all_processed_logs_with_exact_final_references_and_quota_success()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = WithAllStates(configuration, LogState.PROCESSED);
        var quota = QuotaFor(configuration, shift, objectivesSatisfied: true);

        var result = Assert.IsType<ShiftCompletionNewlyCompleted>(Service.Evaluate(lifecycle, shift, quota, ServerTick.From(25), configuration));
        var completion = Assert.IsType<ShiftCompletionEvidence>(result.Lifecycle.Completion);

        Assert.Equal(ShiftCompletionReason.AllLogsTerminal, completion.Reason);
        Assert.Equal(ServerTick.From(25), completion.CompletedAt);
        Assert.True(completion.AllLogsTerminal);
        Assert.False(completion.HardDeadlineReached);
        Assert.True(completion.ObjectivesSatisfied);
        Assert.Equal(12, completion.ProcessedCount);
        Assert.Equal(0, completion.WrittenOffCount);
        Assert.Same(shift, completion.FinalShiftState);
        Assert.Same(quota, completion.FinalQuotaState);
        Assert.Equal(quota.TargetTotal, completion.Quota.TargetTotal);
        Assert.Equal(quota.TotalCreditedUnits, completion.Quota.TotalCreditedUnits);
    }

    [Fact]
    public void Evaluate_completes_a_processed_and_written_off_terminal_mix_but_derives_failure_only_from_quota()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var states = Enumerable.Repeat(LogState.PROCESSED, 8).Concat(Enumerable.Repeat(LogState.HELD_WRITTEN_OFF, 4)).ToArray();
        var shift = WithStates(configuration, states);
        var quota = QuotaFor(configuration, shift, objectivesSatisfied: false);

        var result = Assert.IsType<ShiftCompletionNewlyCompleted>(Service.Evaluate(lifecycle, shift, quota, ServerTick.From(50), configuration));
        var completion = Assert.IsType<ShiftCompletionEvidence>(result.Lifecycle.Completion);

        Assert.Equal(ShiftCompletionReason.AllLogsTerminal, completion.Reason);
        Assert.Equal(8, completion.ProcessedCount);
        Assert.Equal(4, completion.WrittenOffCount);
        Assert.False(completion.ObjectivesSatisfied);
    }

    [Theory]
    [InlineData("learning", 840)]
    [InlineData("pressure", 600)]
    public void Evaluate_completes_at_the_exact_deadline_with_outstanding_logs(string profile, long deadline)
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From(profile));
        var shift = ShiftRuntimeState.Create(configuration);
        var quota = QuotaRuntimeState.Create(configuration);

        var result = Assert.IsType<ShiftCompletionNewlyCompleted>(Service.Evaluate(lifecycle, shift, quota, ServerTick.From(deadline), configuration));
        var completion = Assert.IsType<ShiftCompletionEvidence>(result.Lifecycle.Completion);

        Assert.Equal(ShiftCompletionReason.HardDeadline, completion.Reason);
        Assert.False(completion.AllLogsTerminal);
        Assert.True(completion.HardDeadlineReached);
        Assert.False(completion.ObjectivesSatisfied);
    }

    [Fact]
    public void Evaluate_all_terminal_at_exact_deadline_uses_the_combined_reason()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = WithAllStates(configuration, LogState.PROCESSED);
        var quota = QuotaFor(configuration, shift, objectivesSatisfied: true);

        var result = Assert.IsType<ShiftCompletionNewlyCompleted>(Service.Evaluate(lifecycle, shift, quota, lifecycle.HardDeadlineAt, configuration));

        Assert.Equal(ShiftCompletionReason.AllLogsTerminalAtHardDeadline, result.Lifecycle.Completion!.Reason);
        Assert.True(result.Lifecycle.Completion.AllLogsTerminal);
        Assert.True(result.Lifecycle.Completion.HardDeadlineReached);
        Assert.True(result.Lifecycle.Completion.ObjectivesSatisfied);
    }

    [Fact]
    public void Evaluate_can_succeed_at_the_deadline_with_outstanding_logs_when_the_existing_quota_predicate_is_satisfied()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = WithStates(configuration, LogState.PROCESSED, LogState.PROCESSED, LogState.PROCESSED, LogState.AT_INTAKE);
        var quota = QuotaFor(configuration, shift, objectivesSatisfied: true);

        var result = Assert.IsType<ShiftCompletionNewlyCompleted>(Service.Evaluate(lifecycle, shift, quota, lifecycle.HardDeadlineAt, configuration));

        Assert.Equal(ShiftCompletionReason.HardDeadline, result.Lifecycle.Completion!.Reason);
        Assert.True(result.Lifecycle.Completion.ObjectivesSatisfied);
        Assert.Same(quota, result.Lifecycle.Completion.FinalQuotaState);
    }

    [Fact]
    public void Evaluate_rejects_an_active_lifecycle_after_its_hard_deadline_without_replacing_any_input()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = ShiftRuntimeState.Create(configuration);
        var quota = QuotaRuntimeState.Create(configuration);

        Assert.Throws<InvalidOperationException>(() => Service.Evaluate(lifecycle, shift, quota, ServerTick.From(841), configuration));

        Assert.False(lifecycle.IsCompleted);
        Assert.Same(shift, shift);
        Assert.Same(quota, quota);
    }

    [Fact]
    public void Evaluate_returns_the_exact_completed_lifecycle_without_rewriting_original_final_evidence()
    {
        var configuration = Fixture.LoadP0().Shift;
        var active = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var finalShift = WithAllStates(configuration, LogState.PROCESSED);
        var finalQuota = QuotaFor(configuration, finalShift, objectivesSatisfied: true);
        var completed = Assert.IsType<ShiftCompletionNewlyCompleted>(Service.Evaluate(active, finalShift, finalQuota, ServerTick.From(40), configuration)).Lifecycle;
        var unrelatedShift = ShiftRuntimeState.Create(configuration);
        var unrelatedQuota = QuotaRuntimeState.Create(configuration);

        var result = Assert.IsType<ShiftCompletionAlreadyCompleted>(Service.Evaluate(completed, unrelatedShift, unrelatedQuota, ServerTick.From(900), configuration));

        Assert.Same(completed, result.Lifecycle);
        Assert.Same(finalShift, result.ShiftState);
        Assert.Same(finalQuota, result.QuotaState);
        Assert.Same(finalShift, completed.Completion!.FinalShiftState);
        Assert.Same(finalQuota, completed.Completion.FinalQuotaState);
        Assert.Equal(ServerTick.From(40), completed.Completion.CompletedAt);
    }

    [Fact]
    public void Evaluate_rejects_mismatched_runtime_configuration_and_lifecycle_evidence_before_completion()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = ShiftRuntimeState.Create(configuration);
        var quota = QuotaRuntimeState.Create(configuration);
        var otherConfiguration = configuration with { ShiftId = ShiftId.From("OTHER_SHIFT") };
        var removedProfile = configuration with { Profiles = configuration.Profiles.Remove(ProfileId.From("learning")) };
        var changedDeadline = configuration with { Profiles = configuration.Profiles.SetItem(ProfileId.From("learning"), new ShiftProfile(60, 841)) };

        Assert.Throws<InvalidOperationException>(() => Service.Evaluate(lifecycle, shift, quota, ServerTick.Zero, otherConfiguration));
        Assert.Throws<InvalidOperationException>(() => Service.Evaluate(lifecycle, shift, quota, ServerTick.Zero, removedProfile));
        Assert.Throws<InvalidOperationException>(() => Service.Evaluate(lifecycle, shift, quota, ServerTick.Zero, changedDeadline));
        Assert.False(lifecycle.IsCompleted);
        Assert.Same(shift, shift);
        Assert.Same(quota, quota);
    }

    [Fact]
    public void Evaluate_rejects_default_or_pre_start_ticks()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = ShiftRuntimeState.Create(configuration);
        var quota = QuotaRuntimeState.Create(configuration);
        var beforeStart = CloneWith(lifecycle, nameof(ShiftLifecycleRuntimeState.StartedAt), ServerTick.From(2));

        Assert.Throws<ArgumentOutOfRangeException>(() => Service.Evaluate(lifecycle, shift, quota, default, configuration));
        Assert.Throws<ArgumentOutOfRangeException>(() => Service.Evaluate(beforeStart, shift, quota, ServerTick.Zero, configuration));
    }

    [Fact]
    public void Evaluate_rejects_manifest_and_quota_correlation_mismatches()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = WithStates(configuration, LogState.PROCESSED, LogState.AT_INTAKE);
        var validQuota = QuotaFor(configuration, shift, objectivesSatisfied: false);
        var reorderedManifest = configuration with { Manifest = configuration.Manifest.Reverse().ToImmutableArray() };
        var mismatchedTargets = CloneWith(validQuota, nameof(QuotaRuntimeState.TargetTotal), validQuota.TargetTotal + 1);
        var missingSettlement = QuotaRuntimeState.Create(configuration);
        var unknownSettlement = Apply(QuotaRuntimeState.Create(configuration), new QuotaSettlement(LogId.From("unknown"), null, 0, 0));

        Assert.Throws<InvalidOperationException>(() => Service.Evaluate(lifecycle, shift, validQuota, ServerTick.Zero, reorderedManifest));
        Assert.Throws<InvalidOperationException>(() => Service.Evaluate(lifecycle, shift, mismatchedTargets, ServerTick.Zero, configuration));
        Assert.Throws<InvalidOperationException>(() => Service.Evaluate(lifecycle, shift, missingSettlement, ServerTick.Zero, configuration));
        Assert.Throws<InvalidOperationException>(() => Service.Evaluate(lifecycle, shift, unknownSettlement, ServerTick.Zero, configuration));
    }

    [Fact]
    public void Evaluate_rejects_an_active_saw_cycle_that_owns_a_terminal_log()
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = WithStates(configuration, LogState.PROCESSED);
        var malformed = CloneWith(shift, nameof(ShiftRuntimeState.ActiveSawCycle), new TheLogsAreWrong.Domain.Scheduler.ActiveSawCycle(LogId.From("log_01"), ServerTick.Zero, TheLogsAreWrong.Domain.Time.SimulationDuration.FromTicks(6)));
        var quota = QuotaFor(configuration, malformed, objectivesSatisfied: false);

        Assert.Throws<InvalidOperationException>(() => Service.Evaluate(lifecycle, malformed, quota, ServerTick.Zero, configuration));
        Assert.Same(malformed, malformed);
        Assert.Same(quota, quota);
    }

    [Theory]
    [InlineData(5, 4, 2, true)]
    [InlineData(9, 0, 2, false)]
    [InlineData(5, 4, 1, false)]
    [InlineData(7, 7, 3, true)]
    public void Completion_uses_only_the_existing_quota_objective_predicate(int pine, int oak, int anomalies, bool expected)
    {
        var configuration = Fixture.LoadP0().Shift;
        var lifecycle = ShiftLifecycleRuntimeState.Create(configuration, ProfileId.From("learning"));
        var shift = WithAllStates(configuration, LogState.PROCESSED);
        var quota = QuotaForExplicitProgress(configuration, shift, pine, oak, anomalies);

        var result = Assert.IsType<ShiftCompletionNewlyCompleted>(Service.Evaluate(lifecycle, shift, quota, ServerTick.From(5), configuration));

        Assert.Equal(expected, quota.ObjectivesSatisfied);
        Assert.Equal(expected, result.Lifecycle.Completion!.ObjectivesSatisfied);
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

    private static QuotaRuntimeState QuotaFor(ShiftConfiguration configuration, ShiftRuntimeState shift, bool objectivesSatisfied) =>
        QuotaForExplicitProgress(configuration, shift, objectivesSatisfied ? 5 : 4, objectivesSatisfied ? 4 : 3, objectivesSatisfied ? 2 : 1);

    private static QuotaRuntimeState QuotaForExplicitProgress(ShiftConfiguration configuration, ShiftRuntimeState shift, int pine, int oak, int anomalies)
    {
        var quota = QuotaRuntimeState.Create(configuration);
        var processed = shift.Logs.Where(log => log.State == LogState.PROCESSED).ToArray();
        for (var index = 0; index < processed.Length; index++)
        {
            var settlement = index switch
            {
                0 => new QuotaSettlement(processed[index].LogId, SpeciesId.From("pine"), pine, 0),
                1 => new QuotaSettlement(processed[index].LogId, SpeciesId.From("oak"), oak, 0),
                2 => new QuotaSettlement(processed[index].LogId, null, 0, anomalies),
                _ => new QuotaSettlement(processed[index].LogId, null, 0, 0)
            };
            quota = Apply(quota, settlement);
        }

        return quota;
    }

    private static QuotaRuntimeState Apply(QuotaRuntimeState quota, QuotaSettlement settlement) =>
        Assert.IsType<QuotaSettlementAccepted>(new QuotaSettlementService().Apply(quota, settlement)).State;

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
