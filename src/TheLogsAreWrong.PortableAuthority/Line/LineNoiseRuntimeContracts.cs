using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Line;

public sealed record LineNoiseSourceSnapshot
{
    internal LineNoiseSourceSnapshot(bool sawActive, bool movementNoiseActive, bool repairActive)
    {
        SawActive = sawActive;
        MovementNoiseActive = movementNoiseActive;
        RepairActive = repairActive;
    }

    public static LineNoiseSourceSnapshot Quiet { get; } = new(false, false, false);
    public bool SawActive { get; }
    public bool MovementNoiseActive { get; }
    public bool RepairActive { get; }
    public LineNoise DerivedValue => SawActive || MovementNoiseActive || RepairActive ? LineNoise.LOUD : LineNoise.QUIET;
}

public sealed record LineNoiseChanged
{
    public LineNoiseChanged(ShiftId shiftId, LineNoise previous, LineNoise current, ServerTick changedAt, LineNoiseSourceSnapshot sources)
    {
        if (sources is null) { throw new ArgumentNullException("sources"); }
        if (shiftId.IsDefault || !Enum.IsDefined(typeof(LineNoise), previous) || !Enum.IsDefined(typeof(LineNoise), current) || previous == current || changedAt.IsDefault || sources.DerivedValue != current)
        {
            throw new ArgumentException("A line-noise change requires initialized, different, and source-consistent values.");
        }

        ShiftId = shiftId;
        Previous = previous;
        Current = current;
        ChangedAt = changedAt;
        Sources = sources;
    }

    public ShiftId ShiftId { get; }
    public LineNoise Previous { get; }
    public LineNoise Current { get; }
    public ServerTick ChangedAt { get; }
    public LineNoiseSourceSnapshot Sources { get; }
}

public sealed class LineNoiseRuntimeState
{
    private readonly LineNoiseEvaluationEvidence? _latestEvidence;

    private LineNoiseRuntimeState(
        ShiftId shiftId,
        LineNoise current,
        ServerTick? lastEvaluatedAt,
        LineNoiseSourceSnapshot latestSources,
        ServerTick? lastChangedAt,
        LineNoiseEvaluationEvidence? latestEvidence)
    {
        ShiftId = shiftId;
        Current = current;
        LastEvaluatedAt = lastEvaluatedAt;
        LatestSources = latestSources;
        LastChangedAt = lastChangedAt;
        _latestEvidence = latestEvidence;
    }

    public ShiftId ShiftId { get; }
    public LineNoise Current { get; }
    public ServerTick? LastEvaluatedAt { get; }
    public LineNoiseSourceSnapshot LatestSources { get; }
    public ServerTick? LastChangedAt { get; }

    public static LineNoiseRuntimeState Create(ShiftId shiftId)
    {
        if (shiftId.IsDefault)
        {
            throw new ArgumentException("Line-noise runtime requires an initialized shift identifier.", nameof(shiftId));
        }

        return new LineNoiseRuntimeState(shiftId, LineNoise.QUIET, null, LineNoiseSourceSnapshot.Quiet, null, null);
    }

    public bool ValueEquals(LineNoiseRuntimeState? other) =>
        other is not null &&
        ShiftId == other.ShiftId &&
        Current == other.Current &&
        LastEvaluatedAt == other.LastEvaluatedAt &&
        LatestSources == other.LatestSources &&
        LastChangedAt == other.LastChangedAt &&
        ((_latestEvidence is null && other._latestEvidence is null) ||
         (_latestEvidence is not null && other._latestEvidence is not null && _latestEvidence.ValueEquals(other._latestEvidence)));

    internal LineNoiseEvaluationEvidence? LatestEvidence => _latestEvidence;

    internal LineNoiseRuntimeState Apply(LineNoise next, ServerTick evaluatedAt, LineNoiseSourceSnapshot sources, LineNoiseEvaluationEvidence evidence, ServerTick? changedAt) =>
        new(ShiftId, next, evaluatedAt, sources, changedAt, evidence);
}

public abstract record LineNoiseEvaluationResult
{
    private protected LineNoiseEvaluationResult(LineNoiseRuntimeState state)
    {
        if (state is null) { throw new ArgumentNullException("state"); }
        State = state;
    }

    public LineNoiseRuntimeState State { get; }
}

public sealed record LineNoiseEvaluatedWithoutChange : LineNoiseEvaluationResult
{
    internal LineNoiseEvaluatedWithoutChange(LineNoiseRuntimeState state) : base(state) { }
}

public sealed record LineNoiseEvaluatedWithChange : LineNoiseEvaluationResult
{
    internal LineNoiseEvaluatedWithChange(LineNoiseRuntimeState state, LineNoiseChanged change) : base(state) => Change = change;
    public LineNoiseChanged Change { get; }
}

public sealed record LineNoiseAlreadyEvaluated : LineNoiseEvaluationResult
{
    internal LineNoiseAlreadyEvaluated(LineNoiseRuntimeState state) : base(state) { }
}

public sealed class LineNoiseDerivationService
{
    public LineNoiseEvaluationResult Evaluate(
        LineNoiseRuntimeState runtime,
        ShiftRuntimeState shiftState,
        MovementNoiseRuntimeState movementNoiseRuntime,
        ServerTick currentTick)
    {
        if (runtime is null) { throw new ArgumentNullException("runtime"); }
        if (shiftState is null) { throw new ArgumentNullException("shiftState"); }
        if (movementNoiseRuntime is null) { throw new ArgumentNullException("movementNoiseRuntime"); }
        if (currentTick.IsDefault)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Line-noise evaluation requires an initialized authoritative tick.");
        }

        ValidateRuntime(runtime);
        ValidateShiftOwnership(runtime, shiftState, movementNoiseRuntime);
        var sawActive = ValidateSawSource(shiftState, currentTick);
        var repairActive = ValidateRepairSource(shiftState.Line);
        ValidateMovementSource(movementNoiseRuntime);
        var sources = new LineNoiseSourceSnapshot(sawActive, movementNoiseRuntime.IsActiveAt(currentTick), repairActive);
        var evidence = new LineNoiseEvaluationEvidence(shiftState, movementNoiseRuntime, currentTick, sources);

        if (runtime.LastEvaluatedAt is { } lastEvaluatedAt)
        {
            var comparison = currentTick.CompareTo(lastEvaluatedAt);
            if (comparison < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentTick), "Older line-noise evaluations are not accepted.");
            }

            if (comparison == 0)
            {
                return runtime.LatestEvidence is not null && runtime.LatestEvidence.ValueEquals(evidence)
                    ? new LineNoiseAlreadyEvaluated(runtime)
                    : throw new ArgumentException("Same-tick line-noise evidence must be exactly equivalent to the retained evaluation.");
            }
        }

        var next = sources.DerivedValue;
        if (next == runtime.Current)
        {
            return new LineNoiseEvaluatedWithoutChange(runtime.Apply(next, currentTick, sources, evidence, runtime.LastChangedAt));
        }

        var change = new LineNoiseChanged(runtime.ShiftId, runtime.Current, next, currentTick, sources);
        return new LineNoiseEvaluatedWithChange(runtime.Apply(next, currentTick, sources, evidence, currentTick), change);
    }

    internal static void ValidateRuntime(LineNoiseRuntimeState runtime)
    {
        if (runtime.ShiftId.IsDefault || !Enum.IsDefined(typeof(LineNoise), runtime.Current) || runtime.LatestSources is null)
        {
            throw new ArgumentException("Line-noise runtime must retain initialized typed values.", nameof(runtime));
        }

        if (runtime.LastEvaluatedAt is null)
        {
            if (runtime.Current != LineNoise.QUIET || runtime.LastChangedAt is not null || runtime.LatestSources != LineNoiseSourceSnapshot.Quiet || runtime.LatestEvidence is not null)
            {
                throw new ArgumentException("An unevaluated line-noise runtime must retain the exact quiet baseline.", nameof(runtime));
            }

            return;
        }

        if (runtime.LastEvaluatedAt.Value.IsDefault || runtime.LatestEvidence is null || runtime.LatestEvidence.Tick != runtime.LastEvaluatedAt || runtime.LatestSources != runtime.LatestEvidence.Sources || runtime.Current != runtime.LatestSources.DerivedValue)
        {
            throw new ArgumentException("An evaluated line-noise runtime must retain exact evaluation evidence.", nameof(runtime));
        }

        if (runtime.LastChangedAt is { } lastChangedAt && (lastChangedAt.IsDefault || lastChangedAt > runtime.LastEvaluatedAt.Value))
        {
            throw new ArgumentException("Line-noise change timing must be initialized and monotonic.", nameof(runtime));
        }
    }

    private static void ValidateShiftOwnership(LineNoiseRuntimeState runtime, ShiftRuntimeState shiftState, MovementNoiseRuntimeState movementNoiseRuntime)
    {
        if (shiftState.ShiftId.IsDefault || movementNoiseRuntime.ShiftId.IsDefault || runtime.ShiftId != shiftState.ShiftId || runtime.ShiftId != movementNoiseRuntime.ShiftId || shiftState.StateVersion.IsDefault)
        {
            throw new ArgumentException("Every line-noise source must belong to the exact runtime shift.");
        }
    }

    private static bool ValidateSawSource(ShiftRuntimeState shiftState, ServerTick currentTick)
    {
        if (shiftState.ActiveSawCycle is not { } cycle)
        {
            if (shiftState.GetNodeOccupancy(NodeId.SAW) != 0)
            {
                throw new ArgumentException("A saw occupant requires valid retained saw-cycle evidence.", nameof(shiftState));
            }

            return false;
        }

        if (cycle.LogId.IsDefault || cycle.StartedAt.IsDefault || cycle.Duration.IsDefault || cycle.Duration <= SimulationDuration.Zero || cycle.DueAt.IsDefault || cycle.DueAt != cycle.StartedAt + cycle.Duration || currentTick < cycle.StartedAt || currentTick >= cycle.DueAt ||
            !shiftState.TryGetLog(cycle.LogId, out var owner) || owner.State != LogState.IN_SAW || shiftState.GetNodeOccupancy(NodeId.SAW) != 1)
        {
            throw new ArgumentException("Retained active saw evidence is malformed or temporally contradictory.", nameof(shiftState));
        }

        return true;
    }

    private static bool ValidateRepairSource(LineRuntimeState line)
    {
        if (line is null) { throw new ArgumentNullException("line"); }
        if (!Enum.IsDefined(typeof(LineState), line.State) || line.EnteredAt.IsDefault)
        {
            throw new ArgumentException("Retained line evidence is malformed.", nameof(line));
        }

        if (line.State != LineState.REPAIRING)
        {
            if (line.ActiveRepairHold is not null)
            {
                throw new ArgumentException("A non-repairing line cannot retain an active repair hold.", nameof(line));
            }

            return false;
        }

        if (!LineRuntimeState.TryGetActiveCause(line.Cause, out _) || line.PendingLogId is not { } pending || pending.IsDefault || line.ActiveRepairHold is not { } hold ||
            hold.StartedAt.IsDefault || hold.DueAt.IsDefault || hold.Duration.IsDefault || hold.Duration <= SimulationDuration.Zero || hold.DueAt != hold.StartedAt + hold.Duration || hold.StartedAt != line.EnteredAt)
        {
            throw new ArgumentException("A repairing line requires exact active repair evidence.", nameof(line));
        }

        return true;
    }

    private static void ValidateMovementSource(MovementNoiseRuntimeState runtime)
    {
        if (runtime.LastAcceptedMovement is not { } movement)
        {
            if (!runtime.StartedAt.IsDefault || !runtime.DueAt.IsDefault)
            {
                throw new ArgumentException("An idle movement-noise runtime cannot retain timing evidence.", nameof(runtime));
            }

            return;
        }

        if (!Enum.IsDefined(typeof(MovementNoiseAcceptedSource), movement.Source) || movement.LogId.IsDefault || !Enum.IsDefined(typeof(LogState), movement.SourceState) || !Enum.IsDefined(typeof(LogState), movement.DestinationState) ||
            movement.PriorStateVersion.IsDefault || movement.CurrentStateVersion.IsDefault || movement.CurrentStateVersion != movement.PriorStateVersion.Next() || movement.AcceptedAt.IsDefault ||
            runtime.StartedAt.IsDefault || runtime.DueAt.IsDefault || runtime.DueAt <= runtime.StartedAt || movement.AcceptedAt < runtime.StartedAt || movement.AcceptedAt >= runtime.DueAt)
        {
            throw new ArgumentException("Retained movement-noise evidence is malformed.", nameof(runtime));
        }
    }
}

internal sealed class LineNoiseEvaluationEvidence
{
    internal LineNoiseEvaluationEvidence(ShiftRuntimeState shiftState, MovementNoiseRuntimeState movementNoiseRuntime, ServerTick tick, LineNoiseSourceSnapshot sources)
    {
        ShiftState = shiftState;
        MovementNoiseRuntime = movementNoiseRuntime;
        Tick = tick;
        Sources = sources;
    }

    internal ShiftRuntimeState ShiftState { get; }
    internal MovementNoiseRuntimeState MovementNoiseRuntime { get; }
    internal ServerTick Tick { get; }
    internal LineNoiseSourceSnapshot Sources { get; }

    internal bool ValueEquals(LineNoiseEvaluationEvidence other) =>
        ShiftState.ValueEquals(other.ShiftState) &&
        MovementNoiseRuntime.ValueEquals(other.MovementNoiseRuntime) &&
        Tick == other.Tick &&
        Sources == other.Sources;
}
