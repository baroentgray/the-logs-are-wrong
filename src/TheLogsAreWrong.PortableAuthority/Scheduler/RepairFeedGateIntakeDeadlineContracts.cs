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
        if (state is null) { throw new ArgumentNullException("state"); }
        if (repairedAdmission is null) { throw new ArgumentNullException("repairedAdmission"); }
        if (selectedProfile is null) { throw new ArgumentNullException("selectedProfile"); }
        return IntakeDeadlineStartService.StartFromRepairedAdmission(state, repairedAdmission, selectedProfile);
    }
}
