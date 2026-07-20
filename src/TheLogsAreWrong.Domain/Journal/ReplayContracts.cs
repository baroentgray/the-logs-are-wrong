using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Journal;

public sealed record SnapshotBoundary
{
    public required ShiftId ShiftId { get; init; }
    public required ServerTick ServerTick { get; init; }
    public required StateVersion StateVersion { get; init; }
    public required EventSequence LastEventSequence { get; init; }
}

public enum ReplayAnomaly
{
    DefaultValue,
    ShiftMismatch,
    DuplicateOfSnapshot,
    GapAfterSnapshot,
    Duplicate,
    OutOfOrder,
    SequenceGap,
    TickRegression,
    StateVersionRegression,
    StateVersionSkip
}

public sealed record ReplayValidationResult
{
    private ReplayValidationResult(bool isValid, ReplayAnomaly? anomaly, int? position)
    {
        IsValid = isValid;
        Anomaly = anomaly;
        Position = position;
    }

    public bool IsValid { get; }
    public ReplayAnomaly? Anomaly { get; }
    public int? Position { get; }

    public static ReplayValidationResult Valid() => new(true, null, null);
    public static ReplayValidationResult Invalid(ReplayAnomaly anomaly, int position) => new(false, anomaly, position);
}

public sealed class ReplayValidator
{
    public ReplayValidationResult Validate(SnapshotBoundary boundary, IReadOnlyList<EventEnvelope> events)
    {
        var lastSequence = boundary.LastEventSequence;
        var lastTick = boundary.ServerTick;
        var lastStateVersion = boundary.StateVersion;

        for (var position = 0; position < events.Count; position++)
        {
            var envelope = events[position];
            var anomaly = ValidateEnvelope(boundary.ShiftId, envelope, lastSequence, lastTick, lastStateVersion, position == 0);
            if (anomaly is not null)
            {
                return ReplayValidationResult.Invalid(anomaly.Value, position);
            }

            lastSequence = envelope.Sequence;
            lastTick = envelope.ServerTick;
            lastStateVersion = envelope.StateVersionAfter;
        }

        return ReplayValidationResult.Valid();
    }

    private static ReplayAnomaly? ValidateEnvelope(
        ShiftId shift,
        EventEnvelope envelope,
        EventSequence lastSequence,
        ServerTick lastTick,
        StateVersion lastStateVersion,
        bool isFirst)
    {
        if (InMemoryEventJournal.HasDefaultField(envelope))
        {
            return ReplayAnomaly.DefaultValue;
        }

        if (envelope.ShiftId != shift)
        {
            return ReplayAnomaly.ShiftMismatch;
        }

        var expectedSequence = lastSequence.Next();
        if (envelope.Sequence < expectedSequence)
        {
            if (isFirst && envelope.Sequence <= lastSequence)
            {
                return ReplayAnomaly.DuplicateOfSnapshot;
            }

            return envelope.Sequence == lastSequence ? ReplayAnomaly.Duplicate : ReplayAnomaly.OutOfOrder;
        }

        if (envelope.Sequence > expectedSequence)
        {
            return isFirst ? ReplayAnomaly.GapAfterSnapshot : ReplayAnomaly.SequenceGap;
        }

        if (envelope.ServerTick < lastTick)
        {
            return ReplayAnomaly.TickRegression;
        }

        if (envelope.StateVersionAfter < lastStateVersion)
        {
            return ReplayAnomaly.StateVersionRegression;
        }

        if (envelope.StateVersionAfter > lastStateVersion && (!lastStateVersion.TryNext(out var nextVersion) || envelope.StateVersionAfter != nextVersion))
        {
            return ReplayAnomaly.StateVersionSkip;
        }

        return null;
    }
}
