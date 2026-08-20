using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>
/// The narrow authoritative composition boundary for one frozen seven-stage host tick. It accepts only the existing
/// separate host-owned runtime/configuration evidence, resolves the selected profile from lifecycle and configuration,
/// and returns the exact stage-seven execution result without reconstructing any stage evidence.
/// </summary>
public sealed class HostTickExecutionService
{
    private readonly HostStageOneCompletionExecutor _stageOne = new();
    private readonly AcceptedIntentStageExecutor _stageTwo = new();
    private readonly HostStageThreeDeadlineExecutor _stageThree = new();
    private readonly HostStageFourSawExecutor _stageFour = new();
    private readonly HostStageFiveFeedExecutor _stageFive = new();
    private readonly HostStageSixDerivedExecutor _stageSix = new();
    private readonly HostStageSevenEventExecutor _stageSeven = new();

    /// <summary>Executes the established authoritative host stages once each in the immutable order one through seven.</summary>
    public HostStageSevenEventExecution Execute(
        ShiftRuntimeState initialShiftState,
        QuotaRuntimeState initialQuotaState,
        MovementNoiseRuntimeState initialMovementNoise,
        LineNoiseRuntimeState initialLineNoise,
        HostTickProgressionEvidence progression,
        ShiftLifecycleRuntimeState lifecycle,
        AcceptedIntentTickBatch acceptedIntents,
        ImmutableHashSet<ItemId> activeTools,
        IAtomicEventJournal journal,
        ServerTick currentTick,
        SchedulerConfiguration schedulerConfiguration,
        ShiftConfiguration shiftConfiguration,
        ContainmentConfiguration containmentConfiguration,
        AnomalyCatalog anomalyCatalog)
    {
        var selectedProfile = ValidateInputs(
            initialShiftState,
            initialQuotaState,
            initialMovementNoise,
            initialLineNoise,
            progression,
            lifecycle,
            acceptedIntents,
            activeTools,
            journal,
            currentTick,
            schedulerConfiguration,
            shiftConfiguration,
            containmentConfiguration,
            anomalyCatalog);

        var stageOne = _stageOne.Execute(initialShiftState, currentTick, anomalyCatalog, containmentConfiguration);
        var stageTwo = _stageTwo.Execute(stageOne.FinalState, acceptedIntents, schedulerConfiguration, activeTools, initialLineNoise, anomalyCatalog, containmentConfiguration);
        var stageThree = _stageThree.Execute(stageTwo.FinalState, currentTick, containmentConfiguration, anomalyCatalog);
        var stageFour = _stageFour.Execute(stageThree.FinalState, initialQuotaState, currentTick, schedulerConfiguration, anomalyCatalog);
        var stageFive = _stageFive.Execute(stageOne, stageTwo, stageThree, stageFour, currentTick, schedulerConfiguration, selectedProfile);
        var stageSix = _stageSix.Execute(
            stageOne,
            stageTwo,
            stageThree,
            stageFour,
            stageFive,
            initialMovementNoise,
            initialLineNoise,
            progression,
            lifecycle,
            activeTools,
            currentTick,
            schedulerConfiguration,
            shiftConfiguration,
            anomalyCatalog);
        return _stageSeven.Execute(stageOne, stageTwo, stageThree, stageFour, stageFive, stageSix, journal, currentTick);
    }

    private static ShiftProfile ValidateInputs(
        ShiftRuntimeState initialShiftState,
        QuotaRuntimeState initialQuotaState,
        MovementNoiseRuntimeState initialMovementNoise,
        LineNoiseRuntimeState initialLineNoise,
        HostTickProgressionEvidence progression,
        ShiftLifecycleRuntimeState lifecycle,
        AcceptedIntentTickBatch acceptedIntents,
        ImmutableHashSet<ItemId> activeTools,
        IAtomicEventJournal journal,
        ServerTick currentTick,
        SchedulerConfiguration schedulerConfiguration,
        ShiftConfiguration shiftConfiguration,
        ContainmentConfiguration containmentConfiguration,
        AnomalyCatalog anomalyCatalog)
    {
        if (initialShiftState is null) { throw new ArgumentNullException("initialShiftState"); }
        if (initialQuotaState is null) { throw new ArgumentNullException("initialQuotaState"); }
        if (initialMovementNoise is null) { throw new ArgumentNullException("initialMovementNoise"); }
        if (initialLineNoise is null) { throw new ArgumentNullException("initialLineNoise"); }
        if (progression is null) { throw new ArgumentNullException("progression"); }
        if (lifecycle is null) { throw new ArgumentNullException("lifecycle"); }
        if (acceptedIntents is null) { throw new ArgumentNullException("acceptedIntents"); }
        if (activeTools is null) { throw new ArgumentNullException("activeTools"); }
        if (journal is null) { throw new ArgumentNullException("journal"); }
        if (schedulerConfiguration is null) { throw new ArgumentNullException("schedulerConfiguration"); }
        if (shiftConfiguration is null) { throw new ArgumentNullException("shiftConfiguration"); }
        if (containmentConfiguration is null) { throw new ArgumentNullException("containmentConfiguration"); }
        if (anomalyCatalog is null) { throw new ArgumentNullException("anomalyCatalog"); }

        if (currentTick.IsDefault)
        {
            throw new ArgumentException("Current tick must be initialized.", nameof(currentTick));
        }

        if (activeTools.Any(tool => tool.IsDefault))
        {
            throw new ArgumentException("Active-tool evidence cannot contain a default item.", nameof(activeTools));
        }

        var shiftId = initialShiftState.ShiftId;
        if (shiftId.IsDefault ||
            initialMovementNoise.ShiftId != shiftId ||
            initialLineNoise.ShiftId != shiftId ||
            progression.ShiftId != shiftId ||
            lifecycle.ShiftId != shiftId ||
            acceptedIntents.ShiftId != shiftId ||
            shiftConfiguration.ShiftId != shiftId ||
            journal.Shift != shiftId)
        {
            throw new ArgumentException("All host-tick evidence must belong to one initialized shift.");
        }

        if (acceptedIntents.CurrentTick != currentTick)
        {
            throw new ArgumentException("The accepted-intent batch tick must equal the current tick.", nameof(currentTick));
        }

        if (shiftConfiguration.Profiles is null) { throw new ArgumentNullException("shiftConfiguration.Profiles"); }
        if (!shiftConfiguration.Profiles.TryGetValue(lifecycle.SelectedProfileId, out var selectedProfile) || selectedProfile is null)
        {
            throw new ArgumentException("Lifecycle selected profile must exist in the supplied shift configuration.", nameof(shiftConfiguration));
        }

        return selectedProfile;
    }
}
