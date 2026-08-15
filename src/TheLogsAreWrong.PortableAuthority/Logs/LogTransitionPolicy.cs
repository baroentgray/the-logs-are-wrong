using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Enums;

namespace TheLogsAreWrong.Domain.Logs;

public readonly record struct LogTransition(LogState FromState, LogState ToState);

public static class LogTransitionPolicy
{
    public static ImmutableHashSet<LogTransition> AllowedTransitions { get; } = ImmutableHashSet.Create(
        new LogTransition(LogState.SCHEDULED, LogState.AT_FEED_GATE),
        new LogTransition(LogState.AT_FEED_GATE, LogState.AT_INTAKE),
        new LogTransition(LogState.AT_INTAKE, LogState.AT_PROCEDURE),
        new LogTransition(LogState.AT_PROCEDURE, LogState.AT_INTAKE),
        new LogTransition(LogState.AT_INTAKE, LogState.QUEUED_FOR_SAW),
        new LogTransition(LogState.QUEUED_FOR_SAW, LogState.IN_SAW),
        new LogTransition(LogState.IN_SAW, LogState.PROCESSED),
        new LogTransition(LogState.AT_INTAKE, LogState.HELD_WRITTEN_OFF),
        new LogTransition(LogState.AT_PROCEDURE, LogState.HELD_WRITTEN_OFF));

    public static bool IsAllowed(LogState fromState, LogState toState) => AllowedTransitions.Contains(new LogTransition(fromState, toState));
}

public static class LogStateNodes
{
    public static NodeId? GetNode(LogState state) => state switch
    {
        LogState.SCHEDULED => NodeId.SUPPLY_QUEUE,
        LogState.AT_FEED_GATE => NodeId.FEED_GATE,
        LogState.AT_INTAKE => NodeId.INTAKE,
        LogState.AT_PROCEDURE => NodeId.PROCEDURE,
        LogState.QUEUED_FOR_SAW => NodeId.SAW_QUEUE,
        LogState.IN_SAW => NodeId.SAW,
        LogState.HELD_WRITTEN_OFF => NodeId.CONTAINMENT,
        LogState.PROCESSED => null,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}
