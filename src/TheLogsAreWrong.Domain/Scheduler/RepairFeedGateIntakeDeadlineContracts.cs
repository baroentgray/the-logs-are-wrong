using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Scheduler;

public sealed class RepairFeedGateIntakeDeadlineStartService
{
    public IntakeDeadlineStartResult Start(
        ShiftRuntimeState state,
        RepairPendingTransitionExecuted repairedAdmission,
        ShiftProfile selectedProfile)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(repairedAdmission);
        ArgumentNullException.ThrowIfNull(selectedProfile);
        return IntakeDeadlineStartService.StartFromRepairedAdmission(state, repairedAdmission, selectedProfile);
    }
}
