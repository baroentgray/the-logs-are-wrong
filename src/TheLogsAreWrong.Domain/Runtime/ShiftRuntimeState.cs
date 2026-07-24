using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Logs;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Runtime;

public sealed record LogRuntimeState
{
    public LogRuntimeState(
        LogId logId,
        SpeciesId trueSpecies,
        SpeciesId declaredSpecies,
        AnomalyId? anomaly,
        LogState state,
        ImmutableHashSet<FlagId> flags)
    {
        if (logId.IsDefault || trueSpecies.IsDefault || declaredSpecies.IsDefault || (anomaly is { } anomalyId && anomalyId.IsDefault))
        {
            throw new ArgumentException("A runtime log must retain initialized manifest values.");
        }

        ArgumentNullException.ThrowIfNull(flags);

        LogId = logId;
        TrueSpecies = trueSpecies;
        DeclaredSpecies = declaredSpecies;
        Anomaly = anomaly;
        State = state;
        Flags = flags;
    }

    public LogId LogId { get; }
    public SpeciesId TrueSpecies { get; }
    public SpeciesId DeclaredSpecies { get; }
    public AnomalyId? Anomaly { get; }
    public LogState State { get; }
    public ImmutableHashSet<FlagId> Flags { get; }

    internal LogRuntimeState WithState(LogState state) => new(LogId, TrueSpecies, DeclaredSpecies, Anomaly, state, Flags);
}

public sealed class ShiftRuntimeState
{
    private readonly ImmutableDictionary<LogId, int> _logIndexes;
    private readonly ImmutableDictionary<NodeId, NodeCapacity> _capacities;

    private ShiftRuntimeState(
        ShiftId shiftId,
        ShiftSeed shiftSeed,
        StateVersion stateVersion,
        ImmutableArray<LogRuntimeState> logs,
        ImmutableDictionary<LogId, int> logIndexes,
        ImmutableDictionary<NodeId, NodeCapacity> capacities,
        ImmutableHashSet<IntentId> processedIntentIds)
    {
        ShiftId = shiftId;
        ShiftSeed = shiftSeed;
        StateVersion = stateVersion;
        Logs = logs;
        _logIndexes = logIndexes;
        _capacities = capacities;
        ProcessedIntentIds = processedIntentIds;
    }

    public ShiftId ShiftId { get; }
    public ShiftSeed ShiftSeed { get; }
    public StateVersion StateVersion { get; }
    public ImmutableArray<LogRuntimeState> Logs { get; }
    public ImmutableHashSet<IntentId> ProcessedIntentIds { get; }

    public static ShiftRuntimeState Create(ShiftConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.ShiftId.IsDefault)
        {
            throw new ArgumentException("Shift configuration must contain an initialized shift identifier.", nameof(configuration));
        }

        ArgumentNullException.ThrowIfNull(configuration.Scheduler);
        ArgumentNullException.ThrowIfNull(configuration.Scheduler.Capacities);
        if (configuration.Manifest.IsDefaultOrEmpty)
        {
            throw new ArgumentException("Shift configuration must contain a manifest.", nameof(configuration));
        }

        var capacities = configuration.Scheduler.Capacities;
        foreach (var node in Enum.GetValues<NodeId>())
        {
            if (node == NodeId.SUPPLY_QUEUE)
            {
                continue;
            }

            if (!capacities.TryGetValue(node, out var capacity))
            {
                throw new ArgumentException($"Shift configuration is missing capacity for {node}.", nameof(configuration));
            }

            if ((node == NodeId.CONTAINMENT && !capacity.IsUnlimited) || (node != NodeId.CONTAINMENT && capacity.IsUnlimited))
            {
                throw new ArgumentException($"Shift configuration has an invalid capacity for {node}.", nameof(configuration));
            }
        }

        var logs = ImmutableArray.CreateBuilder<LogRuntimeState>(configuration.Manifest.Length);
        var indexes = ImmutableDictionary.CreateBuilder<LogId, int>();
        for (var index = 0; index < configuration.Manifest.Length; index++)
        {
            var manifestLog = configuration.Manifest[index];
            var runtimeLog = new LogRuntimeState(
                manifestLog.Id,
                manifestLog.TrueSpecies,
                manifestLog.DeclaredSpecies,
                manifestLog.Anomaly,
                LogState.SCHEDULED,
                ImmutableHashSet<FlagId>.Empty);

            if (!indexes.TryAdd(runtimeLog.LogId, index))
            {
                throw new ArgumentException($"Shift manifest contains duplicate log identifier {runtimeLog.LogId}.", nameof(configuration));
            }

            logs.Add(runtimeLog);
        }

        return new ShiftRuntimeState(
            configuration.ShiftId,
            configuration.Seed,
            StateVersion.Zero,
            logs.MoveToImmutable(),
            indexes.ToImmutable(),
            capacities,
            ImmutableHashSet<IntentId>.Empty);
    }

    public bool TryGetLog(LogId logId, out LogRuntimeState log)
    {
        if (logId.IsDefault)
        {
            throw new ArgumentException("Log identifier must be initialized.", nameof(logId));
        }

        if (_logIndexes.TryGetValue(logId, out var index))
        {
            log = Logs[index];
            return true;
        }

        log = default!;
        return false;
    }

    public int GetNodeOccupancy(NodeId node) => Logs.Count(log => LogStateNodes.GetNode(log.State) == node);

    public bool ValueEquals(ShiftRuntimeState? other)
    {
        if (other is null || ShiftId != other.ShiftId || ShiftSeed != other.ShiftSeed || StateVersion != other.StateVersion || Logs.Length != other.Logs.Length || !ProcessedIntentIds.SetEquals(other.ProcessedIntentIds) || _capacities.Count != other._capacities.Count)
        {
            return false;
        }

        for (var index = 0; index < Logs.Length; index++)
        {
            var left = Logs[index];
            var right = other.Logs[index];
            if (left.LogId != right.LogId || left.TrueSpecies != right.TrueSpecies || left.DeclaredSpecies != right.DeclaredSpecies || left.Anomaly != right.Anomaly || left.State != right.State || !left.Flags.SetEquals(right.Flags))
            {
                return false;
            }
        }

        return _capacities.All(pair => other._capacities.TryGetValue(pair.Key, out var otherCapacity) && pair.Value == otherCapacity);
    }

    internal bool TryGetLog(TargetId targetId, out LogRuntimeState log)
    {
        if (targetId.IsDefault)
        {
            throw new ArgumentException("Target identifier must be initialized.", nameof(targetId));
        }

        if (!LogId.TryFrom(targetId.Value, out var logId))
        {
            log = default!;
            return false;
        }

        return TryGetLog(logId, out log);
    }

    internal bool CanEnter(LogState state)
    {
        var node = LogStateNodes.GetNode(state);
        if (node is null || node == NodeId.SUPPLY_QUEUE || !_capacities.TryGetValue(node.Value, out var capacity) || capacity.IsUnlimited)
        {
            return true;
        }

        return GetNodeOccupancy(node.Value) < capacity.Limit;
    }

    internal ShiftRuntimeState ApplyTransition(LogId logId, LogState toState, IntentId? processedIntentId)
    {
        if (!TryGetLog(logId, out var existing))
        {
            throw new ArgumentException("Transition target must resolve to a manifest log.", nameof(logId));
        }

        var updatedLogs = Logs.SetItem(_logIndexes[logId], existing.WithState(toState));
        var processed = processedIntentId is { } intentId ? ProcessedIntentIds.Add(intentId) : ProcessedIntentIds;
        return new ShiftRuntimeState(ShiftId, ShiftSeed, StateVersion.Next(), updatedLogs, _logIndexes, _capacities, processed);
    }
}
