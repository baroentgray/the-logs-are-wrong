using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

internal static class RuntimeFixture
{
    internal static readonly ActorId BoundActor = ActorId.From("host_bound_actor");

    internal static ShiftRuntimeState CreateInitialState() => ShiftRuntimeState.Create(Fixture.LoadP0().Shift);

    internal static ShiftRuntimeState MoveHost(ShiftRuntimeState state, string logId, LogState toState)
    {
        var result = new HostLogTransitionService().Apply(state, LogId.From(logId), toState);
        return Assert.IsType<HostLogTransitionAccepted>(result).State;
    }

    internal static ShiftRuntimeState MoveToIntake(ShiftRuntimeState state, string logId)
    {
        state = MoveHost(state, logId, LogState.AT_FEED_GATE);
        return MoveHost(state, logId, LogState.AT_INTAKE);
    }

    internal static IntentEnvelope RoutingIntent(
        string intentId,
        string logId,
        IntentActionId action,
        ShiftRuntimeState state,
        string actorHint = "untrusted_actor") => new(
            state.ShiftId,
            IntentId.From(intentId),
            ActorId.From(actorHint),
            TargetId.From(logId),
            action,
            state.StateVersion,
            TheLogsAreWrong.Domain.Primitives.ServerTick.Zero,
            NoIntentParameters.Instance);
}
