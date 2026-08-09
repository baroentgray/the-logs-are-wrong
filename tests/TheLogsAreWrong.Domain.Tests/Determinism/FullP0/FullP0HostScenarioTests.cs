using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Tests.Determinism;

/// <summary>
/// TLAW-042 full P0 scenario acceptance. Every scenario executes the real seven-stage
/// <see cref="HostTickExecutionService"/> once per sequential tick and asserts only host-produced evidence.
/// </summary>
[Trait("Scope", "TLAW-042")]
public sealed class FullP0HostScenarioTests
{
    private static readonly SpeciesId Pine = SpeciesId.From("pine");
    private static readonly SpeciesId Oak = SpeciesId.From("oak");
    private static readonly ItemId HolyWater = ItemId.From("holy_water");

    private static FullP0HostScenarioRun Execute(FullP0HostScenarioScript script) =>
        new FullP0HostScenarioDriver().Run(Fixture.LoadP0(), script);

    // ----- §8.1 Learning correct-path completion -----

    [Fact]
    public void Learning_correct_path_completes_the_whole_shift_through_the_authoritative_host()
    {
        var run = Execute(FullP0HostScenarioScript.LearningCorrectPath());
        var completion = run.Completion;

        AssertSequentialTicksFromZero(run);
        Assert.Equal(172, completion.CompletedAt.Value);
        Assert.True(completion.CompletedAt < run.FinalLifecycle.HardDeadlineAt, "Learning completion must precede the frozen hard deadline.");
        Assert.Equal(840, run.FinalLifecycle.HardDeadlineAt.Value);
        Assert.Equal(ShiftCompletionReason.AllLogsTerminal, completion.Reason);
        Assert.True(completion.AllLogsTerminal);
        Assert.False(completion.HardDeadlineReached);

        Assert.Equal(12, run.FinalShiftState.Logs.Length);
        Assert.All(run.FinalShiftState.Logs, log => Assert.True(log.State is LogState.PROCESSED or LogState.HELD_WRITTEN_OFF, $"{log.LogId} is {log.State}."));
        Assert.Equal(11, completion.ProcessedCount);
        Assert.Equal(1, completion.WrittenOffCount);

        // Success comes only from the existing quota objective predicate.
        Assert.True(completion.ObjectivesSatisfied);
        Assert.Equal(run.FinalQuotaState.ObjectivesSatisfied, completion.ObjectivesSatisfied);
        Assert.True(run.FinalQuotaState.GetCreditedUnits(Pine) >= 5);
        Assert.True(run.FinalQuotaState.GetCreditedUnits(Oak) >= 4);
        Assert.True(run.FinalQuotaState.CorrectlyProcessedAnomalies >= 2);
        Assert.Equal(5, run.FinalQuotaState.GetCreditedUnits(Pine));
        Assert.Equal(4, run.FinalQuotaState.GetCreditedUnits(Oak));
        Assert.Equal(2, run.FinalQuotaState.CorrectlyProcessedAnomalies);
        Assert.Equal(9, run.FinalQuotaState.TotalCreditedUnits);

        AssertExactSettlementParity(run);
        AssertOnlyFrozenEventTypes(run);
    }

    [Fact]
    public void Learning_correct_path_processes_two_anomalies_correctly_through_real_confirmation_and_procedure_intents()
    {
        var run = Execute(FullP0HostScenarioScript.LearningCorrectPath());

        var confirmed = run.EventsOfType(HostStageSevenEventTypes.ConfirmationTestCompleted)
            .Select(envelope => ((HostStageSevenConfirmationPayload)envelope.Payload).Result.LogId.ToString())
            .ToArray();
        Assert.Equal(["log_03", "log_05"], confirmed);

        var startedConfirmations = run.EventsOfType(HostStageSevenEventTypes.ConfirmationTestStarted).ToArray();
        Assert.Equal(2, startedConfirmations.Length);
        Assert.All(startedConfirmations, envelope => Assert.NotNull(envelope.CausedByIntentId));

        foreach (var logId in new[] { "log_03", "log_05" })
        {
            var saw = run.SawCompletionFor(logId);
            Assert.True(saw.Resolution.IsAnomalous);
            Assert.True(saw.Resolution.AllRequiredFlagsPresent);
            Assert.Equal(1, saw.QuotaSettlement.CorrectAnomalyDelta);
            Assert.Equal(1, saw.QuotaSettlement.CreditedUnits);
            Assert.Empty(saw.Resolution.Effects);
            Assert.Equal(HostStageSevenSawQuotaOutcome.Accepted, saw.QuotaApplicationOutcome);
        }

        Assert.Equal(Pine, run.SawCompletionFor("log_03").QuotaSettlement.CreditedSpecies);
        Assert.Equal(Oak, run.SawCompletionFor("log_05").QuotaSettlement.CreditedSpecies);

        // The real procedure boundary granted the required flags; nothing was fabricated.
        var procedures = run.EventsOfType(HostStageSevenEventTypes.ProcedureActionCompleted)
            .Select(envelope => ((HostStageSevenProcedurePayload)envelope.Payload).Descriptor)
            .ToArray();
        Assert.Equal(2, procedures.Length);
        Assert.Contains(procedures, descriptor => descriptor.LogId == LogId.From("log_03") && descriptor.NewlyGrantedFlags.Contains(FlagId.From("SANITIZED_PENITENT")));
        Assert.Contains(procedures, descriptor => descriptor.LogId == LogId.From("log_05") && descriptor.NewlyGrantedFlags.Contains(FlagId.From("CORRECTLY_RELABELED")));
        Assert.All(procedures, descriptor => Assert.Empty(descriptor.Effects));

        // The exact saw completions really ran: one per processed log.
        Assert.Equal(11, run.EventsOfType(HostStageSevenEventTypes.SawCycleCompleted).Count());
        Assert.Equal(11, run.EventsOfType(HostStageSevenEventTypes.SawCycleStarted).Count());
    }

    // ----- §10 player-action coverage and mixed accepted batches -----

    [Fact]
    public void Learning_correct_path_exercises_every_composed_stage_two_action_family_through_the_host()
    {
        var run = Execute(FullP0HostScenarioScript.LearningCorrectPath());

        var actions = run.Executions
            .SelectMany(execution => execution.StageTwo.Steps)
            .Select(step => step.Receipt.Envelope.Action)
            .ToImmutableHashSet();

        Assert.Contains(LogIntentActions.RouteToSawQueue, actions);
        Assert.Contains(LogIntentActions.RouteToProcedure, actions);
        Assert.Contains(LogIntentActions.ReturnFromProcedure, actions);
        Assert.Contains(LogIntentActions.WriteOff, actions);
        Assert.Contains(FeedPlanningIntentActions.RequestEarlyFeed, actions);
        Assert.Contains(ProcedureIntentActions.StartProcedureAction, actions);
        Assert.Contains(ConfirmationIntentActions.StartConfirmationTest, actions);
        Assert.Contains(LineRepairIntentActions.StartLineRepair, actions);
        Assert.Contains(ContainmentRitualIntentActions.StartContainmentRitual, actions);
        Assert.Equal(9, actions.Count);
    }

    [Fact]
    public void Mixed_accepted_intent_batch_evolves_the_exact_state_version_between_receipts()
    {
        var run = Execute(FullP0HostScenarioScript.LearningCorrectPath());
        var mixed = run.Executions.Single(execution => execution.CurrentTick == ServerTick.From(26));

        Assert.Equal(2, mixed.StageTwo.Steps.Length);
        var first = mixed.StageTwo.Steps[0];
        var second = mixed.StageTwo.Steps[1];

        Assert.IsType<ManualLogIntentAccepted>(Assert.IsType<ManualRoutingIntentStageOutcome>(first.Outcome).Result);
        Assert.IsType<ProcedureActionIntentHoldStarted>(Assert.IsType<ProcedureActionIntentStageOutcome>(second.Outcome).Result);
        Assert.Equal(first.BeforeState.StateVersion.Next(), first.AfterState.StateVersion);
        Assert.Same(first.AfterState, second.BeforeState);
        Assert.Equal(second.BeforeState.StateVersion.Next(), second.AfterState.StateVersion);
        Assert.Equal(first.BeforeState.StateVersion, first.Receipt.Envelope.ExpectedStateVersion);
        Assert.Equal(second.BeforeState.StateVersion, second.Receipt.Envelope.ExpectedStateVersion);
        Assert.True(first.Receipt.ReceiveSequence < second.Receipt.ReceiveSequence);

        var publications = Assert.IsType<HostStageSevenPublished>(mixed).Publications;
        Assert.Equal(first.Receipt.Envelope.IntentId, publications[0].Envelope.CausedByIntentId);
        Assert.Equal(second.Receipt.Envelope.IntentId, publications[1].Envelope.CausedByIntentId);
    }

    [Fact]
    public void Due_stage_one_completion_is_followed_by_accepted_stage_two_work_in_the_same_host_tick()
    {
        var run = Execute(FullP0HostScenarioScript.LearningCorrectPath());
        var tick = run.Executions.Single(execution => execution.CurrentTick == ServerTick.From(29));

        var completion = Assert.IsType<ProcedureActionDueCompleted>(tick.StageOne.Procedure.Result);
        Assert.Equal(LogId.From("log_03"), completion.Descriptor.LogId);
        Assert.Equal(tick.StageOne.InitialState.StateVersion.Next(), tick.StageOne.FinalState.StateVersion);

        var step = Assert.Single(tick.StageTwo.Steps);
        Assert.Same(tick.StageOne.FinalState, step.BeforeState);
        Assert.Equal(tick.StageOne.FinalState.StateVersion, step.Receipt.Envelope.ExpectedStateVersion);
        Assert.IsType<ManualLogIntentAccepted>(Assert.IsType<ManualRoutingIntentStageOutcome>(step.Outcome).Result);

        var publications = Assert.IsType<HostStageSevenPublished>(tick).Publications;
        Assert.Equal(HostStageSevenEventTypes.ProcedureActionCompleted, publications[0].Envelope.EventType);
        Assert.Equal(HostStageSevenEventTypes.LogRouted, publications[1].Envelope.EventType);
        Assert.Null(publications[0].Envelope.CausedByIntentId);
        Assert.Equal(step.Receipt.Envelope.IntentId, publications[1].Envelope.CausedByIntentId);
    }

    // ----- §8.5 containment / intake overlap -----

    [Fact]
    public void Containment_service_request_overlaps_an_active_intake_task_reached_only_through_host_execution()
    {
        var run = Execute(FullP0HostScenarioScript.LearningCorrectPath());

        var overlaps = run.Ticks
            .Where(record => record.ContainmentState != ContainmentState.STABLE && record.ActiveIntakeDeadlineLogId is not null)
            .ToArray();

        Assert.NotEmpty(overlaps);
        var first = overlaps[0];
        Assert.Equal(153, first.Tick.Value);
        Assert.Equal(ContainmentState.SERVICE_REQUESTED, first.ContainmentState);
        Assert.Equal(LogId.From("log_11"), first.ActiveIntakeDeadlineLogId);
        Assert.Equal(201, first.ActiveIntakeDeadlineDueAt!.Value.Value);

        // The danger-bearing write-off is what armed containment timing, and the real ritual intent resolved it.
        var writtenOff = Assert.Single(run.FinalShiftState.Logs.Where(log => log.State == LogState.HELD_WRITTEN_OFF));
        Assert.Equal(LogId.From("log_08"), writtenOff.LogId);
        Assert.Equal(AnomalyId.From("PENITENT_TRUNK"), writtenOff.Anomaly);

        var ritualStart = Assert.Single(run.EventsOfType(HostStageSevenEventTypes.ContainmentRitualStarted));
        Assert.Equal(154, ritualStart.ServerTick.Value);
        Assert.NotNull(ritualStart.CausedByIntentId);
        var ritualCompleted = Assert.Single(run.EventsOfType(HostStageSevenEventTypes.ContainmentRitualCompleted));
        Assert.Equal(158, ritualCompleted.ServerTick.Value);
        Assert.Null(ritualCompleted.CausedByIntentId);

        // The Gate-2 placeholder incident is never reached, so no forced line pause can be executed.
        Assert.DoesNotContain(run.Ticks, record => record.ContainmentState == ContainmentState.INCIDENT);
        Assert.All(
            run.EventsOfType(HostStageSevenEventTypes.ContainmentStateChanged),
            envelope => Assert.Null(((HostStageSevenContainmentPayload)envelope.Payload).Incident));
    }

    // ----- §8.2 Learning full-timeout -----

    [Fact]
    public void Conservative_learning_full_timeout_policy_still_completes_before_the_frozen_hard_deadline()
    {
        var run = Execute(FullP0HostScenarioScript.LearningFullTimeout());
        var completion = run.Completion;

        AssertSequentialTicksFromZero(run);
        Assert.All(run.Executions, execution => Assert.Empty(execution.StageTwo.Steps));
        Assert.Equal(840, run.FinalLifecycle.HardDeadlineAt.Value);
        Assert.Equal(782, completion.CompletedAt.Value);
        Assert.True(completion.CompletedAt < run.FinalLifecycle.HardDeadlineAt);
        Assert.Equal(ShiftCompletionReason.AllLogsTerminal, completion.Reason);
        Assert.Equal(12, completion.ProcessedCount);
        Assert.Equal(0, completion.WrittenOffCount);

        // Every release used the exact configured 60-second intake timeout and the frozen default auto-route.
        var expirations = run.EventsOfType(HostStageSevenEventTypes.IntakeDeadlineExpired)
            .Select(envelope => (HostStageSevenIntakeDeadlinePayload)envelope.Payload)
            .ToArray();
        Assert.Equal(12, expirations.Length);
        Assert.All(expirations, payload => Assert.Equal(60, payload.Duration.Value));
        Assert.All(expirations, payload => Assert.Equal(payload.DueAt, payload.OccurredAt));
        Assert.Equal(
            12,
            run.EventsOfType(HostStageSevenEventTypes.AutoRouteAttempted)
                .Count(envelope => ((HostStageSevenAutoRoutePayload)envelope.Payload).Outcome == HostStageSevenAutoRouteOutcome.Applied));

        // The frozen policy is capable of completion but not of satisfying the objective without acceleration.
        Assert.False(completion.ObjectivesSatisfied);
        AssertExactSettlementParity(run);
    }

    // ----- §8.3 Pressure full-timeout -----

    [Fact]
    public void Conservative_pressure_full_timeout_policy_fails_at_the_exact_frozen_hard_deadline()
    {
        var run = Execute(FullP0HostScenarioScript.PressureFullTimeout());
        var completion = run.Completion;

        AssertSequentialTicksFromZero(run);
        Assert.All(run.Executions, execution => Assert.Empty(execution.StageTwo.Steps));
        Assert.Equal(600, run.FinalLifecycle.HardDeadlineAt.Value);
        Assert.Equal(600, completion.CompletedAt.Value);
        Assert.Equal(601, run.HostTickCount);
        Assert.Equal(600, run.Ticks[^1].Tick.Value);
        Assert.Equal(599, run.Ticks[^2].Tick.Value);
        Assert.DoesNotContain(run.Ticks.SkipLast(1), record => record.LifecycleCompleted);

        Assert.Equal(ShiftCompletionReason.HardDeadline, completion.Reason);
        Assert.True(completion.HardDeadlineReached);
        Assert.False(completion.AllLogsTerminal);
        Assert.False(completion.ObjectivesSatisfied);
        Assert.Equal(11, completion.ProcessedCount);
        Assert.Equal(0, completion.WrittenOffCount);
        Assert.Equal(45, ((HostStageSevenIntakeDeadlinePayload)run.EventsOfType(HostStageSevenEventTypes.IntakeDeadlineExpired).First().Payload).Duration.Value);

        var shiftCompleted = Assert.Single(run.EventsOfType(HostStageSevenEventTypes.ShiftCompleted));
        var payload = Assert.IsType<HostStageSevenShiftCompletedPayload>(shiftCompleted.Payload);
        Assert.Equal(600, shiftCompleted.ServerTick.Value);
        Assert.Equal(600, payload.HardDeadlineAt.Value);
        Assert.Equal(7, payload.TotalCreditedUnits);
        Assert.Equal(0, payload.CorrectlyProcessedAnomalies);
        AssertExactSettlementParity(run);
    }

    // ----- §8.4 write off every suspicious log -----

    [Fact]
    public void Writing_off_every_suspicious_log_leaves_the_seven_normals_insufficient_and_fails_the_objective()
    {
        var run = Execute(FullP0HostScenarioScript.WriteOffAllSuspicious());
        var completion = run.Completion;

        AssertSequentialTicksFromZero(run);
        Assert.Equal(73, completion.CompletedAt.Value);
        Assert.Equal(ShiftCompletionReason.AllLogsTerminal, completion.Reason);
        Assert.Equal(7, completion.ProcessedCount);
        Assert.Equal(5, completion.WrittenOffCount);
        Assert.False(completion.ObjectivesSatisfied);

        var writtenOff = run.FinalShiftState.Logs.Where(log => log.State == LogState.HELD_WRITTEN_OFF).Select(log => log.LogId.ToString()).ToArray();
        Assert.Equal(["log_03", "log_05", "log_06", "log_08", "log_10"], writtenOff);
        Assert.All(run.FinalShiftState.Logs.Where(log => log.Anomaly is not null), log => Assert.Equal(LogState.HELD_WRITTEN_OFF, log.State));

        // No written-off log ever settles quota; only the seven processed normals do.
        Assert.All(writtenOff, logId => Assert.False(run.FinalQuotaState.IsSettled(LogId.From(logId))));
        Assert.Equal(7, run.FinalQuotaState.SettledLogIds.Count);
        Assert.Equal(7, run.FinalQuotaState.TotalCreditedUnits);
        Assert.Equal(4, run.FinalQuotaState.GetCreditedUnits(Pine));
        Assert.Equal(3, run.FinalQuotaState.GetCreditedUnits(Oak));
        Assert.Equal(0, run.FinalQuotaState.CorrectlyProcessedAnomalies);
        Assert.True(run.FinalQuotaState.GetCreditedUnits(Pine) < run.FinalQuotaState.TargetBySpecies[Pine]);
        Assert.True(run.FinalQuotaState.GetCreditedUnits(Oak) < run.FinalQuotaState.TargetBySpecies[Oak]);

        // Containment weight follows only the actual written-off anomalies and never reaches the Gate-2 incident.
        Assert.Equal(ContainmentState.STABLE, run.FinalShiftState.Containment.State);
        Assert.DoesNotContain(run.Ticks, record => record.ContainmentState == ContainmentState.INCIDENT);
        AssertExactSettlementParity(run);
        AssertOnlyFrozenEventTypes(run);
    }

    // ----- §9 wrong-outcome host evidence -----

    [Fact]
    public void Incorrect_penitent_processing_retains_the_frozen_time_penalty_descriptor_without_executing_it()
    {
        var run = Execute(FullP0HostScenarioScript.IncorrectPenitent());
        var saw = run.SawCompletionFor("log_03");

        Assert.Equal(LogState.PROCESSED, saw.Resolution.TerminalState);
        Assert.Equal(LogState.PROCESSED, LogStateOf(run, "log_03"));
        Assert.False(saw.Resolution.AllRequiredFlagsPresent);
        Assert.Null(saw.QuotaSettlement.CreditedSpecies);
        Assert.Equal(0, saw.QuotaSettlement.CreditedUnits);
        Assert.Equal(0, saw.QuotaSettlement.CorrectAnomalyDelta);

        var effect = Assert.Single(saw.Resolution.Effects);
        Assert.Equal(EffectType.time_penalty, effect.Type);
        Assert.Equal(EffectEventId.From("FALSE_PA_ANNOUNCEMENT"), effect.Event);
        Assert.Equal(8, effect.DurationSeconds);
        Assert.Null(effect.Target);

        Assert.Equal(0, run.FinalQuotaState.TotalCreditedUnits);
        Assert.Equal(0, run.FinalQuotaState.CorrectlyProcessedAnomalies);
        Assert.True(run.FinalQuotaState.IsSettled(LogId.From("log_03")));
        AssertNoEffectExecution(run);
    }

    [Fact]
    public void Incorrect_resin_processing_retains_the_frozen_button_lock_descriptor_without_a_lock_runtime()
    {
        var run = Execute(FullP0HostScenarioScript.IncorrectResin());
        var saw = run.SawCompletionFor("log_06");

        Assert.Equal(LogState.PROCESSED, saw.Resolution.TerminalState);
        Assert.Equal(LogState.PROCESSED, LogStateOf(run, "log_06"));
        Assert.False(saw.Resolution.AllRequiredFlagsPresent);
        Assert.Null(saw.QuotaSettlement.CreditedSpecies);
        Assert.Equal(0, saw.QuotaSettlement.CreditedUnits);
        Assert.Equal(0, saw.QuotaSettlement.CorrectAnomalyDelta);

        var effect = Assert.Single(saw.Resolution.Effects);
        Assert.Equal(EffectType.@lock, effect.Type);
        Assert.Equal(EffectEventId.From("RESIN_BUTTON_LOCK"), effect.Event);
        Assert.Equal("nearest_line_button", effect.Target);
        Assert.Equal(10, effect.DurationSeconds);

        Assert.Equal(0, run.FinalQuotaState.TotalCreditedUnits);
        AssertNoEffectExecution(run);
    }

    [Fact]
    public void Incorrect_false_species_processing_credits_the_declared_species_exactly_once()
    {
        var run = Execute(FullP0HostScenarioScript.IncorrectFalseSpecies());
        var saw = run.SawCompletionFor("log_05");

        Assert.Equal(LogState.PROCESSED, saw.Resolution.TerminalState);
        Assert.False(saw.Resolution.AllRequiredFlagsPresent);
        Assert.Equal(SpeciesId.From("pine"), saw.QuotaSettlement.CreditedSpecies);
        Assert.Equal(SpeciesId.From("pine"), LogOf(run, "log_05").DeclaredSpecies);
        Assert.Equal(SpeciesId.From("oak"), LogOf(run, "log_05").TrueSpecies);
        Assert.Equal(1, saw.QuotaSettlement.CreditedUnits);
        Assert.Equal(0, saw.QuotaSettlement.CorrectAnomalyDelta);

        var effect = Assert.Single(saw.Resolution.Effects);
        Assert.Equal(EffectType.miscredit, effect.Type);
        Assert.Equal(EffectEventId.From("CREDIT_TO_DECLARED_SPECIES"), effect.Event);

        // The resolved settlement is the whole miscredit: it is never applied a second time.
        Assert.Equal(HostStageSevenSawQuotaOutcome.Accepted, saw.QuotaApplicationOutcome);
        Assert.Equal(1, saw.AcceptedQuotaSettlement!.CurrentSpeciesCredit);
        Assert.Equal(0, saw.AcceptedQuotaSettlement.PriorSpeciesCredit);
        Assert.Equal(1, run.FinalQuotaState.GetCreditedUnits(Pine));
        Assert.Equal(0, run.FinalQuotaState.GetCreditedUnits(Oak));
        Assert.Equal(1, run.FinalQuotaState.TotalCreditedUnits);
        Assert.Equal(0, run.FinalQuotaState.CorrectlyProcessedAnomalies);
        AssertNoEffectExecution(run);
    }

    [Fact]
    public void Resin_wrong_holy_water_consumes_one_charge_and_leaves_the_log_processable()
    {
        var run = Execute(FullP0HostScenarioScript.ResinWrongHolyWaterRecovery());
        var descriptors = run.EventsOfType(HostStageSevenEventTypes.ProcedureActionCompleted)
            .Select(envelope => ((HostStageSevenProcedurePayload)envelope.Payload).Descriptor)
            .ToArray();

        Assert.Equal(3, descriptors.Length);
        var wrong = descriptors[0];
        Assert.Equal(HolyWater, wrong.AttemptedItem);
        Assert.Equal(ItemActionCompletionKind.ConfiguredWrongAction, wrong.Kind);
        Assert.True(wrong.ItemConsumed);
        Assert.Empty(wrong.NewlyGrantedFlags);
        Assert.Null(wrong.PriorProgress);
        Assert.Null(wrong.CurrentProgress);
        var lockEffect = Assert.Single(wrong.Effects);
        Assert.Equal(EffectType.@lock, lockEffect.Type);
        Assert.Equal(EffectEventId.From("RESIN_BUTTON_LOCK"), lockEffect.Event);
        Assert.Equal("nearest_line_button", lockEffect.Target);
        Assert.Equal(10, lockEffect.DurationSeconds);

        // Exactly one holy-water charge is consumed and no correct progress is fabricated by the wrong action.
        Assert.Equal(1, run.FinalShiftState.Inventory.GetConsumableQuantity(HolyWater));

        // The remaining approved resources still complete the exact configured Resin procedure.
        Assert.Equal(ItemActionCompletionKind.CorrectProcedureStep, descriptors[1].Kind);
        Assert.Equal(ItemId.From("salt"), descriptors[1].AttemptedItem);
        Assert.Equal(1, descriptors[1].CurrentProgress!.CompletedStepCount);
        Assert.False(descriptors[1].CurrentProgress!.IsComplete);
        Assert.Equal(ItemActionCompletionKind.CorrectProcedureStep, descriptors[2].Kind);
        Assert.Equal(ItemId.From("red_tape"), descriptors[2].AttemptedItem);
        Assert.True(descriptors[2].CurrentProgress!.IsComplete);
        Assert.Contains(FlagId.From("SEALED_RESIN"), descriptors[2].NewlyGrantedFlags);

        var saw = run.SawCompletionFor("log_06");
        Assert.True(saw.Resolution.AllRequiredFlagsPresent);
        Assert.Equal(Oak, saw.QuotaSettlement.CreditedSpecies);
        Assert.Equal(1, saw.QuotaSettlement.CreditedUnits);
        Assert.Equal(1, saw.QuotaSettlement.CorrectAnomalyDelta);
        Assert.Empty(saw.Resolution.Effects);
        AssertNoEffectExecution(run);
    }

    // ----- shared assertions -----

    private static LogRuntimeState LogOf(FullP0HostScenarioRun run, string logId)
    {
        Assert.True(run.FinalShiftState.TryGetLog(LogId.From(logId), out var log));
        return log;
    }

    private static LogState LogStateOf(FullP0HostScenarioRun run, string logId) => LogOf(run, logId).State;

    private static void AssertSequentialTicksFromZero(FullP0HostScenarioRun run)
    {
        Assert.NotEmpty(run.Ticks);
        for (var index = 0; index < run.Ticks.Length; index++)
        {
            Assert.Equal(index, run.Ticks[index].Tick.Value);
        }

        Assert.All(run.Ticks, record => Assert.True(record.ExecutionKind is nameof(HostStageSevenPublished) or nameof(HostStageSevenNoNewPublication)));
    }

    private static void AssertExactSettlementParity(FullP0HostScenarioRun run)
    {
        var processed = run.FinalShiftState.Logs.Where(log => log.State == LogState.PROCESSED).Select(log => log.LogId).ToImmutableHashSet();
        Assert.True(run.FinalQuotaState.SettledLogIds.SetEquals(processed));

        var completions = run.EventsOfType(HostStageSevenEventTypes.SawCycleCompleted)
            .Select(envelope => (HostStageSevenSawCompletedPayload)envelope.Payload)
            .ToArray();
        Assert.Equal(processed.Count, completions.Length);
        Assert.All(completions, payload => Assert.Equal(HostStageSevenSawQuotaOutcome.Accepted, payload.QuotaApplicationOutcome));
        Assert.All(completions, payload => Assert.Equal(payload.Cycle.LogId, payload.QuotaSettlement.LogId));
        Assert.Equal(completions.Sum(payload => payload.QuotaSettlement.CreditedUnits), run.FinalQuotaState.TotalCreditedUnits);
        Assert.Equal(completions.Sum(payload => payload.QuotaSettlement.CorrectAnomalyDelta), run.FinalQuotaState.CorrectlyProcessedAnomalies);
        Assert.Equal(completions.Select(payload => payload.Cycle.LogId).Distinct().Count(), completions.Length);
    }

    private static void AssertOnlyFrozenEventTypes(FullP0HostScenarioRun run)
    {
        var frozen = typeof(HostStageSevenEventTypes)
            .GetFields()
            .Where(field => field.FieldType == typeof(EventTypeId))
            .Select(field => (EventTypeId)field.GetValue(null)!)
            .ToImmutableHashSet();
        Assert.Equal(24, frozen.Count);
        Assert.All(run.Journal.Events, envelope => Assert.Contains(envelope.EventType, frozen));
    }

    /// <summary>TLAW-042 never executes a configured effect: retained descriptors change no runtime and publish no event.</summary>
    private static void AssertNoEffectExecution(FullP0HostScenarioRun run)
    {
        AssertOnlyFrozenEventTypes(run);
        Assert.Equal(LineState.LINE_CLEAR, run.FinalShiftState.Line.State);
        Assert.DoesNotContain(run.Ticks, record => record.ContainmentState == ContainmentState.INCIDENT);
        Assert.All(
            run.EventsOfType(HostStageSevenEventTypes.ContainmentStateChanged),
            envelope => Assert.Null(((HostStageSevenContainmentPayload)envelope.Payload).Incident));
    }
}
