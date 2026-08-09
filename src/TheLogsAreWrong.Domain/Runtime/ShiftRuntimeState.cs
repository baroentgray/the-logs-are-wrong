using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Containment;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Logs;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Scheduler;

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

    internal LogRuntimeState WithAdditionalFlags(ImmutableHashSet<FlagId> flags) =>
        new(LogId, TrueSpecies, DeclaredSpecies, Anomaly, State, Flags.Union(flags).ToImmutableHashSet());
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
        ImmutableHashSet<IntentId> processedIntentIds,
        PendingFeedSchedule? pendingFeed,
        RuntimeInventory inventory,
        ImmutableDictionary<LogId, ProcedureProgress> procedureProgressByLog,
        ActiveProcedureHold? activeProcedureHold,
        ActiveConfirmationTest? activeConfirmationTest,
        ImmutableDictionary<LogId, ConfirmationTestResult> confirmationResultsByLog,
        ContainmentRuntimeState containment,
        ActiveContainmentRitual? activeContainmentRitual,
        LineRuntimeState line,
        ActiveIntakeDeadline? activeIntakeDeadline,
        ActiveSawCycle? activeSawCycle)
    {
        ShiftId = shiftId;
        ShiftSeed = shiftSeed;
        StateVersion = stateVersion;
        Logs = logs;
        _logIndexes = logIndexes;
        _capacities = capacities;
        ProcessedIntentIds = processedIntentIds;
        PendingFeed = pendingFeed;
        Inventory = inventory;
        ProcedureProgressByLog = procedureProgressByLog;
        ActiveProcedureHold = activeProcedureHold;
        ActiveConfirmationTest = activeConfirmationTest;
        ConfirmationResultsByLog = confirmationResultsByLog;
        Containment = containment;
        ActiveContainmentRitual = activeContainmentRitual;
        Line = line;
        ActiveIntakeDeadline = activeIntakeDeadline;
        ActiveSawCycle = activeSawCycle;
    }

    public ShiftId ShiftId { get; }
    public ShiftSeed ShiftSeed { get; }
    public StateVersion StateVersion { get; }
    public ImmutableArray<LogRuntimeState> Logs { get; }
    public ImmutableHashSet<IntentId> ProcessedIntentIds { get; }
    public PendingFeedSchedule? PendingFeed { get; }
    public RuntimeInventory Inventory { get; }
    public ImmutableDictionary<LogId, ProcedureProgress> ProcedureProgressByLog { get; }
    public ActiveProcedureHold? ActiveProcedureHold { get; }
    public ActiveConfirmationTest? ActiveConfirmationTest { get; }
    public ImmutableDictionary<LogId, ConfirmationTestResult> ConfirmationResultsByLog { get; }
    public ContainmentRuntimeState Containment { get; }
    public ActiveContainmentRitual? ActiveContainmentRitual { get; }
    public LineRuntimeState Line { get; }
    public ActiveIntakeDeadline? ActiveIntakeDeadline { get; }
    public ActiveSawCycle? ActiveSawCycle { get; }

    public static ShiftRuntimeState Create(ShiftConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.ShiftId.IsDefault)
        {
            throw new ArgumentException("Shift configuration must contain an initialized shift identifier.", nameof(configuration));
        }

        ArgumentNullException.ThrowIfNull(configuration.Scheduler);
        ArgumentNullException.ThrowIfNull(configuration.Scheduler.Capacities);
        ArgumentNullException.ThrowIfNull(configuration.Resources);
        if (configuration.Manifest.IsDefaultOrEmpty)
        {
            throw new ArgumentException("Shift configuration must contain a manifest.", nameof(configuration));
        }

        var capacities = configuration.Scheduler.Capacities;
        var inventory = RuntimeInventory.Create(configuration.Resources);
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
            ImmutableHashSet<IntentId>.Empty,
            null,
            inventory,
            ImmutableDictionary<LogId, ProcedureProgress>.Empty,
            null,
            null,
            ImmutableDictionary<LogId, ConfirmationTestResult>.Empty,
            new ContainmentRuntimeState(ContainmentState.STABLE, ServerTick.Zero, null),
            null,
            new LineRuntimeState(LineState.LINE_CLEAR, ServerTick.Zero, null, null, null),
            null,
            null);
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

    public bool TryGetProcedureProgress(LogId logId, out ProcedureProgress progress)
    {
        if (logId.IsDefault)
        {
            throw new ArgumentException("Log identifier must be initialized.", nameof(logId));
        }

        if (ProcedureProgressByLog.TryGetValue(logId, out progress!))
        {
            return true;
        }

        progress = default!;
        return false;
    }

    public bool ValueEquals(ShiftRuntimeState? other)
    {
        if (other is null || ShiftId != other.ShiftId || ShiftSeed != other.ShiftSeed || StateVersion != other.StateVersion || Logs.Length != other.Logs.Length || !ProcessedIntentIds.SetEquals(other.ProcessedIntentIds) || PendingFeed != other.PendingFeed || _capacities.Count != other._capacities.Count || !Inventory.ValueEquals(other.Inventory) || ProcedureProgressByLog.Count != other.ProcedureProgressByLog.Count || ActiveProcedureHold != other.ActiveProcedureHold || !ConfirmationEqual(ActiveConfirmationTest, other.ActiveConfirmationTest) || ConfirmationResultsByLog.Count != other.ConfirmationResultsByLog.Count || Containment != other.Containment || ActiveContainmentRitual != other.ActiveContainmentRitual || Line != other.Line || ActiveIntakeDeadline != other.ActiveIntakeDeadline || ActiveSawCycle != other.ActiveSawCycle)
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

        return _capacities.All(pair => other._capacities.TryGetValue(pair.Key, out var otherCapacity) && pair.Value == otherCapacity) &&
            ProcedureProgressByLog.All(pair => other.ProcedureProgressByLog.TryGetValue(pair.Key, out var otherProgress) && pair.Value == otherProgress) &&
            ConfirmationResultsByLog.All(pair => other.ConfirmationResultsByLog.TryGetValue(pair.Key, out var otherResult) && ResultEqual(pair.Value, otherResult));
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

    internal bool TryGetNextScheduledLog(out LogRuntimeState log)
    {
        foreach (var candidate in Logs)
        {
            if (candidate.State == LogState.SCHEDULED)
            {
                log = candidate;
                return true;
            }
        }

        log = default!;
        return false;
    }

    internal bool IsPristineForInitialFeedPlanning =>
        StateVersion == StateVersion.Zero &&
        PendingFeed is null &&
        Line.State == LineState.LINE_CLEAR &&
        ActiveProcedureHold is null &&
        ActiveConfirmationTest is null &&
        ActiveContainmentRitual is null &&
        ActiveSawCycle is null &&
        Logs.All(log => log.State == LogState.SCHEDULED);

    internal ShiftRuntimeState WithPendingFeed(PendingFeedSchedule pendingFeed, IntentId? processedIntentId)
    {
        ArgumentNullException.ThrowIfNull(pendingFeed);
        if (PendingFeed is not null)
        {
            throw new InvalidOperationException("Only one pending feed is permitted.");
        }

        if (!TryGetLog(pendingFeed.LogId, out var log) || log.State != LogState.SCHEDULED)
        {
            throw new ArgumentException("A pending feed must reserve a currently scheduled manifest log.", nameof(pendingFeed));
        }

        if (pendingFeed.CausedByIntentId != processedIntentId)
        {
            throw new ArgumentException("Pending-feed causation must match its processed intent identity.", nameof(processedIntentId));
        }

        var processed = processedIntentId is { } intentId ? ProcessedIntentIds.Add(intentId) : ProcessedIntentIds;
        return new ShiftRuntimeState(
            ShiftId,
            ShiftSeed,
            StateVersion.Next(),
            Logs,
            _logIndexes,
            _capacities,
            processed,
            pendingFeed,
            Inventory,
            ProcedureProgressByLog,
            ActiveProcedureHold,
            ActiveConfirmationTest,
            ConfirmationResultsByLog,
            Containment,
            ActiveContainmentRitual,
            Line,
            ActiveIntakeDeadline,
            ActiveSawCycle);
    }

    internal ShiftRuntimeState ConsumePendingFeed(LogState destination)
    {
        if (PendingFeed is not { } pendingFeed)
        {
            throw new InvalidOperationException("A pending feed is required for scheduler-owned resolution.");
        }

        if (destination is not (LogState.AT_INTAKE or LogState.AT_FEED_GATE))
        {
            throw new ArgumentOutOfRangeException(nameof(destination), "Feed resolution accepts only intake or feed-gate destinations.");
        }

        if (!TryGetLog(pendingFeed.LogId, out var log) || log.State != LogState.SCHEDULED)
        {
            throw new InvalidOperationException("The pending feed must reserve an existing scheduled log.");
        }

        if (!CanEnter(destination))
        {
            throw new InvalidOperationException("The scheduler-owned feed destination must have capacity.");
        }

        var updatedLogs = Logs.SetItem(_logIndexes[pendingFeed.LogId], log.WithState(destination));
        return new ShiftRuntimeState(
            ShiftId,
            ShiftSeed,
            StateVersion.Next(),
            updatedLogs,
            _logIndexes,
            _capacities,
            ProcessedIntentIds,
            null,
            Inventory,
            ProcedureProgressByLog,
            ActiveProcedureHold,
            ActiveConfirmationTest,
            ConfirmationResultsByLog,
            Containment,
            ActiveContainmentRitual,
            Line,
            ActiveIntakeDeadline,
            ActiveSawCycle);
    }

    internal ShiftRuntimeState ApplyTransition(LogId logId, LogState toState, IntentId? processedIntentId)
    {
        if (!TryGetLog(logId, out var existing))
        {
            throw new ArgumentException("Transition target must resolve to a manifest log.", nameof(logId));
        }

        var updatedLogs = Logs.SetItem(_logIndexes[logId], existing.WithState(toState));
        var processed = processedIntentId is { } intentId ? ProcessedIntentIds.Add(intentId) : ProcessedIntentIds;
        var activeProcedureHold = ActiveProcedureHold is { } hold && hold.LogId == logId && toState != LogState.AT_PROCEDURE
            ? null
            : ActiveProcedureHold;
        var activeConfirmation = ActiveConfirmationTest is { } test && test.LogId == logId && toState != LogState.AT_INTAKE ? null : ActiveConfirmationTest;
        var activeIntakeDeadline = ActiveIntakeDeadline is { } deadline && deadline.LogId == logId && existing.State == LogState.AT_INTAKE && toState != LogState.AT_INTAKE ? null : ActiveIntakeDeadline;
        return new ShiftRuntimeState(ShiftId, ShiftSeed, StateVersion.Next(), updatedLogs, _logIndexes, _capacities, processed, PendingFeed, Inventory, ProcedureProgressByLog, activeProcedureHold, activeConfirmation, ConfirmationResultsByLog, Containment, ActiveContainmentRitual, Line, activeIntakeDeadline, ActiveSawCycle);
    }

    internal ShiftRuntimeState StartSawCycle(ActiveSawCycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        if (ActiveSawCycle is not null)
        {
            throw new InvalidOperationException("Only one active saw cycle is permitted.");
        }

        if (!TryGetLog(cycle.LogId, out var owner) || owner.State != LogState.QUEUED_FOR_SAW || !CanEnter(LogState.IN_SAW))
        {
            throw new InvalidOperationException("A saw cycle must start from its queued owner while the saw has capacity.");
        }

        var updatedLogs = Logs.SetItem(_logIndexes[cycle.LogId], owner.WithState(LogState.IN_SAW));
        return new ShiftRuntimeState(
            ShiftId,
            ShiftSeed,
            StateVersion.Next(),
            updatedLogs,
            _logIndexes,
            _capacities,
            ProcessedIntentIds,
            PendingFeed,
            Inventory,
            ProcedureProgressByLog,
            ActiveProcedureHold,
            ActiveConfirmationTest,
            ConfirmationResultsByLog,
            Containment,
            ActiveContainmentRitual,
            Line,
            ActiveIntakeDeadline,
            cycle);
    }

    internal ShiftRuntimeState CompleteSawCycle(ActiveSawCycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        if (ActiveSawCycle is null || !ReferenceEquals(ActiveSawCycle, cycle))
        {
            throw new InvalidOperationException("Only the current active saw cycle may complete.");
        }

        if (!TryGetLog(cycle.LogId, out var owner) || owner.State != LogState.IN_SAW)
        {
            throw new InvalidOperationException("The active saw cycle must retain an in-saw owner.");
        }

        var updatedLogs = Logs.SetItem(_logIndexes[cycle.LogId], owner.WithState(LogState.PROCESSED));
        return new ShiftRuntimeState(
            ShiftId,
            ShiftSeed,
            StateVersion.Next(),
            updatedLogs,
            _logIndexes,
            _capacities,
            ProcessedIntentIds,
            PendingFeed,
            Inventory,
            ProcedureProgressByLog,
            ActiveProcedureHold,
            ActiveConfirmationTest,
            ConfirmationResultsByLog,
            Containment,
            ActiveContainmentRitual,
            Line,
            ActiveIntakeDeadline,
            null);
    }

    internal ShiftRuntimeState WithActiveIntakeDeadline(ActiveIntakeDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(deadline);
        if (ActiveIntakeDeadline is not null)
        {
            throw new InvalidOperationException("Only one active intake deadline is permitted.");
        }

        if (!TryGetLog(deadline.LogId, out var log) || log.State != LogState.AT_INTAKE)
        {
            throw new ArgumentException("An active intake deadline must own a log at intake.", nameof(deadline));
        }

        return new ShiftRuntimeState(ShiftId, ShiftSeed, StateVersion.Next(), Logs, _logIndexes, _capacities, ProcessedIntentIds, PendingFeed, Inventory, ProcedureProgressByLog, ActiveProcedureHold, ActiveConfirmationTest, ConfirmationResultsByLog, Containment, ActiveContainmentRitual, Line, deadline, ActiveSawCycle);
    }

    internal ShiftRuntimeState ClearActiveIntakeDeadline()
    {
        if (ActiveIntakeDeadline is null)
        {
            throw new InvalidOperationException("There is no active intake deadline to clear.");
        }

        return new ShiftRuntimeState(ShiftId, ShiftSeed, StateVersion.Next(), Logs, _logIndexes, _capacities, ProcessedIntentIds, PendingFeed, Inventory, ProcedureProgressByLog, ActiveProcedureHold, ActiveConfirmationTest, ConfirmationResultsByLog, Containment, ActiveContainmentRitual, Line, null, ActiveSawCycle);
    }

    internal ShiftRuntimeState ApplyItemAction(
        LogId logId,
        RuntimeInventory inventory,
        ProcedureProgress? progress,
        ImmutableHashSet<FlagId> newlyGrantedFlags)
        => ApplyItemAction(logId, inventory, progress, newlyGrantedFlags, ActiveProcedureHold);

    internal ShiftRuntimeState ApplyItemAction(
        LogId logId,
        RuntimeInventory inventory,
        ProcedureProgress? progress,
        ImmutableHashSet<FlagId> newlyGrantedFlags,
        ActiveProcedureHold? activeProcedureHold)
        => ApplyItemAction(logId, inventory, progress, newlyGrantedFlags, activeProcedureHold, null);

    internal ShiftRuntimeState ApplyItemAction(
        LogId logId,
        RuntimeInventory inventory,
        ProcedureProgress? progress,
        ImmutableHashSet<FlagId> newlyGrantedFlags,
        ActiveProcedureHold? activeProcedureHold,
        IntentId? processedIntentId)
    {
        if (!TryGetLog(logId, out var existing))
        {
            throw new ArgumentException("Item action target must resolve to a manifest log.", nameof(logId));
        }

        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(newlyGrantedFlags);
        var updatedLogs = newlyGrantedFlags.IsEmpty
            ? Logs
            : Logs.SetItem(_logIndexes[logId], existing.WithAdditionalFlags(newlyGrantedFlags));
        var updatedProgress = progress is null
            ? ProcedureProgressByLog
            : ProcedureProgressByLog.SetItem(logId, progress);
        var processed = processedIntentId is { } intentId ? ProcessedIntentIds.Add(intentId) : ProcessedIntentIds;
        return new ShiftRuntimeState(
            ShiftId,
            ShiftSeed,
            StateVersion.Next(),
            updatedLogs,
            _logIndexes,
            _capacities,
            processed,
            PendingFeed,
            inventory,
            updatedProgress,
            activeProcedureHold, ActiveConfirmationTest, ConfirmationResultsByLog, Containment, ActiveContainmentRitual, Line, ActiveIntakeDeadline, ActiveSawCycle);
    }

    internal ShiftRuntimeState StartActiveProcedureHold(ActiveProcedureHold activeProcedureHold)
        => StartActiveProcedureHold(activeProcedureHold, null);

    internal ShiftRuntimeState StartActiveProcedureHold(ActiveProcedureHold activeProcedureHold, IntentId? processedIntentId)
    {
        ArgumentNullException.ThrowIfNull(activeProcedureHold);
        if (ActiveProcedureHold is not null)
        {
            throw new InvalidOperationException("Only one active procedure hold is permitted.");
        }

        if (!TryGetLog(activeProcedureHold.LogId, out var log) || log.State != LogState.AT_PROCEDURE || log.Anomaly != activeProcedureHold.AnomalyId)
        {
            throw new ArgumentException("Active procedure hold must match an anomalous log at procedure.", nameof(activeProcedureHold));
        }

        return new ShiftRuntimeState(
            ShiftId,
            ShiftSeed,
            StateVersion.Next(),
            Logs,
            _logIndexes,
            _capacities,
            processedIntentId is { } intentId ? ProcessedIntentIds.Add(intentId) : ProcessedIntentIds,
            PendingFeed,
            Inventory,
            ProcedureProgressByLog,
            activeProcedureHold, ActiveConfirmationTest, ConfirmationResultsByLog, Containment, ActiveContainmentRitual, Line, ActiveIntakeDeadline, ActiveSawCycle);
    }

    internal ShiftRuntimeState ClearActiveProcedureHold()
    {
        if (ActiveProcedureHold is null)
        {
            throw new InvalidOperationException("There is no active procedure hold to clear.");
        }

        return new ShiftRuntimeState(
            ShiftId,
            ShiftSeed,
            StateVersion.Next(),
            Logs,
            _logIndexes,
            _capacities,
            ProcessedIntentIds,
            PendingFeed,
            Inventory,
            ProcedureProgressByLog,
            null, ActiveConfirmationTest, ConfirmationResultsByLog, Containment, ActiveContainmentRitual, Line, ActiveIntakeDeadline, ActiveSawCycle);
    }

    public bool TryGetConfirmationResult(LogId logId, out ConfirmationTestResult result) => ConfirmationResultsByLog.TryGetValue(logId, out result!);
    internal ShiftRuntimeState WithActiveConfirmation(ActiveConfirmationTest? active, ConfirmationTestResult? result = null)
    {
        var results = result is null ? ConfirmationResultsByLog : ConfirmationResultsByLog.Add(result.LogId, result);
        return new ShiftRuntimeState(ShiftId, ShiftSeed, StateVersion.Next(), Logs, _logIndexes, _capacities, ProcessedIntentIds, PendingFeed, Inventory, ProcedureProgressByLog, ActiveProcedureHold, active, results, Containment, ActiveContainmentRitual, Line, ActiveIntakeDeadline, ActiveSawCycle);
    }

    internal ShiftRuntimeState WithContainment(ContainmentRuntimeState containment, ActiveContainmentRitual? activeContainmentRitual)
    {
        ArgumentNullException.ThrowIfNull(containment);
        return new ShiftRuntimeState(
            ShiftId,
            ShiftSeed,
            StateVersion.Next(),
            Logs,
            _logIndexes,
            _capacities,
            ProcessedIntentIds,
            PendingFeed,
            Inventory,
            ProcedureProgressByLog,
            ActiveProcedureHold,
            ActiveConfirmationTest,
            ConfirmationResultsByLog,
            containment,
            activeContainmentRitual,
            Line,
            ActiveIntakeDeadline,
            ActiveSawCycle);
    }

    internal ShiftRuntimeState WithLine(LineRuntimeState line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new ShiftRuntimeState(
            ShiftId,
            ShiftSeed,
            StateVersion.Next(),
            Logs,
            _logIndexes,
            _capacities,
            ProcessedIntentIds,
            PendingFeed,
            Inventory,
            ProcedureProgressByLog,
            ActiveProcedureHold,
            ActiveConfirmationTest,
            ConfirmationResultsByLog,
            Containment,
            ActiveContainmentRitual,
            line,
            ActiveIntakeDeadline,
            ActiveSawCycle);
    }

    private static bool ConfirmationEqual(ActiveConfirmationTest? left, ActiveConfirmationTest? right) => left is null ? right is null : right is not null && left.LogId == right.LogId && left.AnomalyId == right.AnomalyId && left.AccumulatedValidDuration == right.AccumulatedValidDuration && left.SegmentStartedAt == right.SegmentStartedAt && left.DueAt == right.DueAt && left.IsRunning == right.IsRunning && left.LastConditionBoundaryAt == right.LastConditionBoundaryAt && left.Plan.AnomalyId == right.Plan.AnomalyId && left.Plan.RequiredTools.SetEquals(right.Plan.RequiredTools) && left.Plan.Duration == right.Plan.Duration && left.Plan.Continuous == right.Plan.Continuous && left.Plan.RequiredLineNoise == right.Plan.RequiredLineNoise && left.Plan.ResetWhenConditionLost == right.Plan.ResetWhenConditionLost && left.Plan.Result == right.Plan.Result;
    private static bool ResultEqual(ConfirmationTestResult left, ConfirmationTestResult right) => left.LogId == right.LogId && left.AnomalyId == right.AnomalyId && left.Result == right.Result && left.RequiredTools.SetEquals(right.RequiredTools) && left.Duration == right.Duration && left.CompletedAt == right.CompletedAt;
}
