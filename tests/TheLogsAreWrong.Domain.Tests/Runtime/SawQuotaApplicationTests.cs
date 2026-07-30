using System.Collections.Immutable;
using System.Reflection;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Tests.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-023")]
public sealed class SawQuotaApplicationTests
{
    private static readonly SawQuotaApplicationService Service = new();

    [Fact]
    public void Apply_rejects_null_inputs()
    {
        var completion = Completed("log_01");
        var quota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);

        Assert.Throws<ArgumentNullException>(() => Service.Apply(null!, quota));
        Assert.Throws<ArgumentNullException>(() => Service.Apply(completion, null!));
    }

    [Fact]
    public void Apply_rejects_mismatched_cycle_resolution_and_settlement_identities_before_mutating_quota()
    {
        var completion = Completed("log_01");
        var quota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);
        var resolutionMismatch = CloneWith(completion.Resolution, nameof(ProcessingResolution.LogId), LogId.From("log_02"));
        var cycleMismatch = CloneWith(completion.Cycle, nameof(ActiveSawCycle.LogId), LogId.From("log_02"));

        AssertRejectsWithoutQuotaMutation(new SawCycleCompleted(completion.State, completion.Cycle, resolutionMismatch, completion.CompletedAt, completion.PriorStateVersion, completion.CurrentStateVersion), quota);
        AssertRejectsWithoutQuotaMutation(new SawCycleCompleted(completion.State, cycleMismatch, completion.Resolution, completion.CompletedAt, completion.PriorStateVersion, completion.CurrentStateVersion), quota);
    }

    [Fact]
    public void Apply_rejects_non_processed_terminal_resolution_before_mutating_quota()
    {
        var completion = Completed("log_01");
        var malformed = CloneWith(completion.Resolution, nameof(ProcessingResolution.TerminalState), LogState.HELD_WRITTEN_OFF);

        AssertRejectsWithoutQuotaMutation(new SawCycleCompleted(completion.State, completion.Cycle, malformed, completion.CompletedAt, completion.PriorStateVersion, completion.CurrentStateVersion), QuotaRuntimeState.Create(Fixture.LoadP0().Shift));
    }

    [Fact]
    public void Apply_rejects_completion_state_that_still_retains_active_cycle_before_mutating_quota()
    {
        var completion = Completed("log_01");
        var malformedState = CloneWith(completion.State, nameof(ShiftRuntimeState.ActiveSawCycle), completion.Cycle);

        AssertRejectsWithoutQuotaMutation(new SawCycleCompleted(malformedState, completion.Cycle, completion.Resolution, completion.CompletedAt, completion.PriorStateVersion, completion.CurrentStateVersion), QuotaRuntimeState.Create(Fixture.LoadP0().Shift));
    }

    [Fact]
    public void Apply_rejects_missing_or_non_processed_completed_owner_before_mutating_quota()
    {
        var completion = Completed("log_01");
        var missingOwner = RemoveLog(completion.State, "log_01");
        var nonProcessedOwner = ReplaceLogState(completion.State, "log_01", LogState.HELD_WRITTEN_OFF);
        var quota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);

        AssertRejectsWithoutQuotaMutation(new SawCycleCompleted(missingOwner, completion.Cycle, completion.Resolution, completion.CompletedAt, completion.PriorStateVersion, completion.CurrentStateVersion), quota);
        AssertRejectsWithoutQuotaMutation(new SawCycleCompleted(nonProcessedOwner, completion.Cycle, completion.Resolution, completion.CompletedAt, completion.PriorStateVersion, completion.CurrentStateVersion), quota);
    }

    [Fact]
    public void Apply_rejects_state_version_mismatches_before_mutating_quota()
    {
        var completion = Completed("log_01");
        var currentMismatch = new SawCycleCompleted(completion.State, completion.Cycle, completion.Resolution, completion.CompletedAt, completion.PriorStateVersion, completion.PriorStateVersion);
        var nonSequential = new SawCycleCompleted(completion.State, completion.Cycle, completion.Resolution, completion.CompletedAt, completion.CurrentStateVersion, completion.CurrentStateVersion);
        var quota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);

        AssertRejectsWithoutQuotaMutation(currentMismatch, quota);
        AssertRejectsWithoutQuotaMutation(nonSequential, quota);
    }

    [Fact]
    public void Apply_rejects_completion_before_due_and_corrupted_cycle_timing_before_mutating_quota()
    {
        var completion = Completed("log_01");
        var early = new SawCycleCompleted(completion.State, completion.Cycle, completion.Resolution, completion.Cycle.StartedAt, completion.PriorStateVersion, completion.CurrentStateVersion);
        var malformedCycle = CloneWith(completion.Cycle, nameof(ActiveSawCycle.DueAt), ServerTick.From(completion.Cycle.DueAt.Value + 1));
        var malformed = new SawCycleCompleted(completion.State, malformedCycle, completion.Resolution, completion.CompletedAt, completion.PriorStateVersion, completion.CurrentStateVersion);
        var quota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);

        AssertRejectsWithoutQuotaMutation(early, quota);
        AssertRejectsWithoutQuotaMutation(malformed, quota);
    }

    [Theory]
    [InlineData("log_01", "pine")]
    [InlineData("log_02", "oak")]
    public void Apply_accepts_normal_log_and_returns_the_existing_quota_descriptor(string logId, string species)
    {
        var completion = Completed(logId);
        var quota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);
        var expected = Assert.IsType<QuotaSettlementAccepted>(new QuotaSettlementService().Apply(quota, completion.Resolution.Settlement));

        var result = AssertAccepted(Service.Apply(completion, quota), completion);

        Assert.Equal(expected.Descriptor, result.AcceptedSettlement.Descriptor);
        Assert.True(expected.State.ValueEquals(result.QuotaState));
        Assert.Equal(1, result.QuotaState.GetCreditedUnits(SpeciesId.From(species)));
        Assert.Equal(1, result.QuotaState.TotalCreditedUnits);
        Assert.Equal(0, result.QuotaState.CorrectlyProcessedAnomalies);
        Assert.True(result.QuotaState.IsSettled(LogId.From(logId)));
        Assert.True(quota.ValueEquals(QuotaRuntimeState.Create(Fixture.LoadP0().Shift)));
    }

    [Fact]
    public void Apply_accepts_correctly_prepared_anomaly_with_its_resolved_credit_and_delta()
    {
        var completion = Completed("log_03", prepared: true);
        var quota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);

        var result = AssertAccepted(Service.Apply(completion, quota), completion);

        Assert.Equal(completion.Resolution.Settlement.CreditedSpecies, result.AcceptedSettlement.Descriptor.CreditedSpecies);
        Assert.Equal(completion.Resolution.Settlement.CreditedUnits, result.AcceptedSettlement.Descriptor.CreditedUnits);
        Assert.Equal(completion.Resolution.Settlement.CorrectAnomalyDelta, result.AcceptedSettlement.Descriptor.CorrectAnomalyDelta);
        Assert.Equal(1, result.QuotaState.CorrectlyProcessedAnomalies);
        Assert.True(result.QuotaState.IsSettled(LogId.From("log_03")));
    }

    [Theory]
    [InlineData("log_03", "FALSE_PA_ANNOUNCEMENT")]
    [InlineData("log_06", "RESIN_BUTTON_LOCK")]
    public void Apply_preserves_incorrect_anomaly_effect_data_without_granting_credit_or_executing_it(string logId, string effectEvent)
    {
        var completion = Completed(logId);
        var quota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);

        var result = AssertAccepted(Service.Apply(completion, quota), completion);

        Assert.Equal(0, result.QuotaState.TotalCreditedUnits);
        Assert.Equal(0, result.QuotaState.CorrectlyProcessedAnomalies);
        Assert.NotEmpty(result.Resolution.Effects);
        Assert.Contains(result.Resolution.Effects, effect => effect.Event.Value == effectEvent);
        Assert.Equal(completion.Resolution.Effects, result.Resolution.Effects);
    }

    [Fact]
    public void Apply_keeps_incorrect_false_species_declared_species_credit_with_zero_anomaly_delta()
    {
        var completion = Completed("log_05");
        var quota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);

        var result = AssertAccepted(Service.Apply(completion, quota), completion);

        Assert.Equal(SpeciesId.From("pine"), result.AcceptedSettlement.Descriptor.CreditedSpecies);
        Assert.Equal(1, result.AcceptedSettlement.Descriptor.CreditedUnits);
        Assert.Equal(0, result.AcceptedSettlement.Descriptor.CorrectAnomalyDelta);
        Assert.Equal(1, result.QuotaState.GetCreditedUnits(SpeciesId.From("pine")));
        Assert.Equal(0, result.QuotaState.GetCreditedUnits(SpeciesId.From("oak")));
        Assert.Equal(0, result.QuotaState.CorrectlyProcessedAnomalies);
    }

    [Fact]
    public void Apply_changes_objectives_satisfied_only_through_existing_quota_rules()
    {
        var completion = Completed("log_03", prepared: true);
        var quota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);
        var quotaService = new QuotaSettlementService();
        quota = Assert.IsType<QuotaSettlementAccepted>(quotaService.Apply(quota, new QuotaSettlement(LogId.From("prior_pine"), SpeciesId.From("pine"), 4, 0))).State;
        quota = Assert.IsType<QuotaSettlementAccepted>(quotaService.Apply(quota, new QuotaSettlement(LogId.From("prior_oak"), SpeciesId.From("oak"), 4, 1))).State;
        Assert.False(quota.ObjectivesSatisfied);

        var result = AssertAccepted(Service.Apply(completion, quota), completion);

        Assert.True(result.QuotaState.ObjectivesSatisfied);
        Assert.Equal(9, result.QuotaState.TotalCreditedUnits);
        Assert.Equal(2, result.QuotaState.CorrectlyProcessedAnomalies);
    }

    [Fact]
    public void Apply_preserves_exact_shift_reference_and_complete_shift_value_while_leaving_quota_input_unchanged()
    {
        var completion = Completed("log_01");
        var quota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);
        var expectedShift = completion.State;
        var expectedQuota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);

        var result = AssertAccepted(Service.Apply(completion, quota), completion);

        Assert.Same(expectedShift, result.ShiftState);
        Assert.True(expectedShift.ValueEquals(result.ShiftState));
        Assert.True(quota.ValueEquals(expectedQuota));
        Assert.NotSame(quota, result.QuotaState);
    }

    [Fact]
    public void Repeated_application_returns_typed_already_applied_result_without_double_credit()
    {
        var completion = Completed("log_03", prepared: true);
        var initialQuota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);
        var first = AssertAccepted(Service.Apply(completion, initialQuota), completion);

        var repeated = Assert.IsType<SawQuotaApplicationAlreadyApplied>(Service.Apply(completion, first.QuotaState));

        Assert.Same(first.QuotaState, repeated.QuotaState);
        Assert.Same(completion.State, repeated.ShiftState);
        Assert.Same(completion.Resolution, repeated.Resolution);
        Assert.Equal(completion.Cycle.LogId, repeated.CompletedLogId);
        Assert.Same(first.QuotaState, repeated.DuplicateSettlement.State);
        Assert.Equal(first.QuotaState.TotalCreditedUnits, repeated.QuotaState.TotalCreditedUnits);
        Assert.Equal(first.QuotaState.CorrectlyProcessedAnomalies, repeated.QuotaState.CorrectlyProcessedAnomalies);
    }

    [Fact]
    public void Contradictory_retained_duplicate_cannot_replace_first_settlement()
    {
        var completion = Completed("log_01");
        var initialQuota = QuotaRuntimeState.Create(Fixture.LoadP0().Shift);
        var first = AssertAccepted(Service.Apply(completion, initialQuota), completion);
        var contradictoryResolution = new ProcessingResolution(
            completion.Resolution.LogId,
            completion.Resolution.IsAnomalous,
            completion.Resolution.AllRequiredFlagsPresent,
            completion.Resolution.TerminalState,
            new QuotaSettlement(completion.Cycle.LogId, SpeciesId.From("oak"), 7, 3),
            completion.Resolution.Effects);
        var contradictory = new SawCycleCompleted(
            completion.State,
            completion.Cycle,
            contradictoryResolution,
            completion.CompletedAt,
            completion.PriorStateVersion,
            completion.CurrentStateVersion);

        var repeated = Assert.IsType<SawQuotaApplicationAlreadyApplied>(Service.Apply(contradictory, first.QuotaState));

        Assert.Same(first.QuotaState, repeated.QuotaState);
        Assert.Equal(1, repeated.QuotaState.GetCreditedUnits(SpeciesId.From("pine")));
        Assert.Equal(0, repeated.QuotaState.GetCreditedUnits(SpeciesId.From("oak")));
        Assert.Equal(1, repeated.QuotaState.TotalCreditedUnits);
        Assert.Equal(0, repeated.QuotaState.CorrectlyProcessedAnomalies);
    }

    [Fact]
    public void Malformed_completion_cannot_hide_behind_an_already_settled_log()
    {
        var completion = Completed("log_01");
        var accepted = AssertAccepted(Service.Apply(completion, QuotaRuntimeState.Create(Fixture.LoadP0().Shift)), completion);
        var malformedState = CloneWith(completion.State, nameof(ShiftRuntimeState.ActiveSawCycle), completion.Cycle);
        var malformed = new SawCycleCompleted(malformedState, completion.Cycle, completion.Resolution, completion.CompletedAt, completion.PriorStateVersion, completion.CurrentStateVersion);

        AssertRejectsWithoutQuotaMutation(malformed, accepted.QuotaState);
    }

    [Fact]
    public void Independent_value_equivalent_completion_and_quota_inputs_produce_value_equivalent_results()
    {
        var firstCompletion = Completed("log_05");
        var secondCompletion = Completed("log_05");
        var first = AssertAccepted(Service.Apply(firstCompletion, QuotaRuntimeState.Create(Fixture.LoadP0().Shift)), firstCompletion);
        var second = AssertAccepted(Service.Apply(secondCompletion, QuotaRuntimeState.Create(Fixture.LoadP0().Shift)), secondCompletion);

        Assert.Equal(first.CompletedLogId, second.CompletedLogId);
        Assert.Equal(first.AcceptedSettlement.Descriptor, second.AcceptedSettlement.Descriptor);
        Assert.True(first.QuotaState.ValueEquals(second.QuotaState));
        Assert.True(first.Resolution.ValueEquals(second.Resolution));
    }

    private static SawQuotaApplicationAccepted AssertAccepted(SawQuotaApplicationResult result, SawCycleCompleted completion)
    {
        var accepted = Assert.IsType<SawQuotaApplicationAccepted>(result);
        Assert.Same(completion.State, accepted.ShiftState);
        Assert.Same(completion.Resolution, accepted.Resolution);
        Assert.Equal(completion.Cycle.LogId, accepted.CompletedLogId);
        Assert.Same(accepted.QuotaState, accepted.AcceptedSettlement.State);
        return accepted;
    }

    private static void AssertRejectsWithoutQuotaMutation(SawCycleCompleted completion, QuotaRuntimeState quota)
    {
        var total = quota.TotalCreditedUnits;
        var correctlyProcessed = quota.CorrectlyProcessedAnomalies;
        var creditedBySpecies = quota.CreditedBySpecies;
        var settledLogIds = quota.SettledLogIds;
        var exception = Record.Exception(() => Service.Apply(completion, quota));

        Assert.NotNull(exception);
        Assert.Equal(total, quota.TotalCreditedUnits);
        Assert.Equal(correctlyProcessed, quota.CorrectlyProcessedAnomalies);
        Assert.Equal(creditedBySpecies, quota.CreditedBySpecies);
        Assert.Equal(settledLogIds, quota.SettledLogIds);
    }

    private static SawCycleCompleted Completed(string logId, bool prepared = false)
    {
        var fixture = Fixture.LoadP0();
        var state = Queued(logId);
        if (prepared)
        {
            Assert.True(state.TryGetLog(LogId.From(logId), out var log));
            Assert.True(new ProcedurePlanResolver(fixture.Anomalies).TryGetPlan(log, out var plan));
            var preparedLog = new LogRuntimeState(log.LogId, log.TrueSpecies, log.DeclaredSpecies, log.Anomaly, log.State, plan!.GrantedFlags);
            state = CloneWith(state, nameof(ShiftRuntimeState.Logs), state.Logs.SetItem(LogIndex(state, logId), preparedLog));
        }

        var started = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(state, ServerTick.From(10), fixture.Shift.Scheduler));
        return Assert.IsType<SawCycleCompleted>(new SawCycleCompletionService().Complete(started.State, started.Cycle.DueAt, fixture.Anomalies));
    }

    private static ShiftRuntimeState Queued(string logId)
    {
        var state = ShiftRuntimeState.Create(Fixture.LoadP0().Shift);
        state = RuntimeFixture.MoveHost(state, logId, LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, logId, LogState.AT_INTAKE);
        return RuntimeFixture.MoveHost(state, logId, LogState.QUEUED_FOR_SAW);
    }

    private static ShiftRuntimeState ReplaceLogState(ShiftRuntimeState state, string logId, LogState stateValue)
    {
        Assert.True(state.TryGetLog(LogId.From(logId), out var log));
        var replacement = new LogRuntimeState(log.LogId, log.TrueSpecies, log.DeclaredSpecies, log.Anomaly, stateValue, log.Flags);
        return CloneWith(state, nameof(ShiftRuntimeState.Logs), state.Logs.SetItem(LogIndex(state, logId), replacement));
    }

    private static ShiftRuntimeState RemoveLog(ShiftRuntimeState state, string logId)
    {
        var id = LogId.From(logId);
        var indexes = (ImmutableDictionary<LogId, int>)FindField(typeof(ShiftRuntimeState), "_logIndexes").GetValue(state)!;
        return CloneWith(
            CloneWith(state, nameof(ShiftRuntimeState.Logs), state.Logs.RemoveAt(LogIndex(state, logId))),
            "_logIndexes",
            indexes.Remove(id));
    }

    private static int LogIndex(ShiftRuntimeState state, string logId)
    {
        var id = LogId.From(logId);
        for (var index = 0; index < state.Logs.Length; index++)
        {
            if (state.Logs[index].LogId == id)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Missing fixture log {logId}.");
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
