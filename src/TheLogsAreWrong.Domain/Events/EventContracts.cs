using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Events;

public interface IDomainEventPayload;

public readonly record struct EventTypeId
{
    public string? Value { get; }
    public bool IsDefault => Value is null;

    private EventTypeId(string value) => Value = value;

    public static EventTypeId From(string value) => TryFrom(value, out var result)
        ? result
        : throw new ArgumentException("Event type cannot be null, empty, or whitespace.", nameof(value));

    public static bool TryFrom(string? value, out EventTypeId result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }

        result = new EventTypeId(value);
        return true;
    }

    public override string ToString() => Value ?? string.Empty;
}

public sealed record EventEnvelope
{
    public required ShiftId ShiftId { get; init; }
    public required EventId EventId { get; init; }
    public required EventSequence Sequence { get; init; }
    public IntentId? CausedByIntentId { get; init; }
    public required ServerTick ServerTick { get; init; }
    public required StateVersion StateVersionAfter { get; init; }
    public required EventTypeId EventType { get; init; }
    public required IDomainEventPayload Payload { get; init; }
}

public enum RejectionReason
{
    SHIFT_MISMATCH,
    ACTOR_NOT_BOUND,
    STALE_STATE_VERSION,
    TARGET_NOT_FOUND,
    TARGET_NOT_IN_STATE,
    TARGET_OCCUPIED,
    MISSING_ITEM,
    HOLD_NOT_COMPLETE,
    FEED_ALREADY_PENDING,
    FEED_GATE_OCCUPIED,
    LINE_NOT_CLEAR,
    BLOCKING_CONDITION_REMAINS,
    NO_ACTIVE_REQUEST,
    NO_MORE_LOGS,
    MALFORMED_PROCEDURE_PARAMETERS,
    PROCEDURE_HOLD_ACTIVE,
    PROCEDURE_NO_PLAN,
    PROCEDURE_OUT_OF_ORDER_ITEM,
    PROCEDURE_REPEATED_STEP,
    PROCEDURE_UNCONFIGURED_ITEM
}

public sealed record RejectionEvent
{
    public required ShiftId ShiftId { get; init; }
    public required IntentId IntentId { get; init; }
    public required ServerTick ServerTick { get; init; }
    public required StateVersion CurrentStateVersion { get; init; }
    public required RejectionReason Reason { get; init; }
}
