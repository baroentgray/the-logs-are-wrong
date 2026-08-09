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
        IEventJournal journal,
        ImmutableArray<EventId> eventIds,
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
            eventIds,
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
        return _stageSeven.Execute(stageOne, stageTwo, stageThree, stageFour, stageFive, stageSix, journal, eventIds, currentTick);
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
        IEventJournal journal,
        ImmutableArray<EventId> eventIds,
        ServerTick currentTick,
        SchedulerConfiguration schedulerConfiguration,
        ShiftConfiguration shiftConfiguration,
        ContainmentConfiguration containmentConfiguration,
        AnomalyCatalog anomalyCatalog)
    {
        ArgumentNullException.ThrowIfNull(initialShiftState);
        ArgumentNullException.ThrowIfNull(initialQuotaState);
        ArgumentNullException.ThrowIfNull(initialMovementNoise);
        ArgumentNullException.ThrowIfNull(initialLineNoise);
        ArgumentNullException.ThrowIfNull(progression);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(acceptedIntents);
        ArgumentNullException.ThrowIfNull(activeTools);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(schedulerConfiguration);
        ArgumentNullException.ThrowIfNull(shiftConfiguration);
        ArgumentNullException.ThrowIfNull(containmentConfiguration);
        ArgumentNullException.ThrowIfNull(anomalyCatalog);

        if (currentTick.IsDefault)
        {
            throw new ArgumentException("Current tick must be initialized.", nameof(currentTick));
        }

        if (eventIds.IsDefault)
        {
            throw new ArgumentException("Event identities must be an initialized immutable array.", nameof(eventIds));
        }

        if (eventIds.Any(eventId => eventId.IsDefault))
        {
            throw new ArgumentException("Every event identity must be initialized.", nameof(eventIds));
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

        ArgumentNullException.ThrowIfNull(shiftConfiguration.Profiles);
        if (!shiftConfiguration.Profiles.TryGetValue(lifecycle.SelectedProfileId, out var selectedProfile) || selectedProfile is null)
        {
            throw new ArgumentException("Lifecycle selected profile must exist in the supplied shift configuration.", nameof(shiftConfiguration));
        }

        return selectedProfile;
    }
}
