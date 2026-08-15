using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Intents;

public readonly record struct TargetId
{
    public string? Value { get; }
    public bool IsDefault => Value is null;

    private TargetId(string value) => Value = value;

    public static TargetId From(string value) => TryFrom(value, out var result)
        ? result
        : throw new ArgumentException("Target identifier cannot be null, empty, or whitespace.", nameof(value));

    public static bool TryFrom(string? value, out TargetId result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }

        result = new TargetId(value);
        return true;
    }

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct IntentActionId
{
    public string? Value { get; }
    public bool IsDefault => Value is null;

    private IntentActionId(string value) => Value = value;

    public static IntentActionId From(string value) => TryFrom(value, out var result)
        ? result
        : throw new ArgumentException("Intent action cannot be null, empty, or whitespace.", nameof(value));

    public static bool TryFrom(string? value, out IntentActionId result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }

        result = new IntentActionId(value);
        return true;
    }

    public override string ToString() => Value ?? string.Empty;
}

public interface IIntentParameters;

public sealed record NoIntentParameters : IIntentParameters
{
    private NoIntentParameters()
    {
    }

    public static NoIntentParameters Instance { get; } = new();
}

public sealed record IntentEnvelope
{
    public IntentEnvelope(
        ShiftId shiftId,
        IntentId intentId,
        ActorId actorIdHint,
        TargetId targetId,
        IntentActionId action,
        StateVersion expectedStateVersion,
        ServerTick clientObservedTick,
        IIntentParameters parameters)
    {
        if (shiftId.IsDefault)
        {
            throw new ArgumentException("Shift identifier must be initialized.", nameof(shiftId));
        }

        if (intentId.IsDefault)
        {
            throw new ArgumentException("Intent identifier must be initialized.", nameof(intentId));
        }

        if (actorIdHint.IsDefault)
        {
            throw new ArgumentException("Actor hint must be initialized.", nameof(actorIdHint));
        }

        if (targetId.IsDefault)
        {
            throw new ArgumentException("Target identifier must be initialized.", nameof(targetId));
        }

        if (action.IsDefault)
        {
            throw new ArgumentException("Intent action must be initialized.", nameof(action));
        }

        if (expectedStateVersion.IsDefault)
        {
            throw new ArgumentException("Expected state version must be initialized.", nameof(expectedStateVersion));
        }

        if (clientObservedTick.IsDefault)
        {
            throw new ArgumentException("Client-observed tick must be initialized.", nameof(clientObservedTick));
        }

        if (parameters is null) { throw new ArgumentNullException("parameters"); }

        ShiftId = shiftId;
        IntentId = intentId;
        ActorIdHint = actorIdHint;
        TargetId = targetId;
        Action = action;
        ExpectedStateVersion = expectedStateVersion;
        ClientObservedTick = clientObservedTick;
        Parameters = parameters;
    }

    public ShiftId ShiftId { get; }
    public IntentId IntentId { get; }

    /// <summary>Client-provided metadata only; a trusted host binding supplies authority separately.</summary>
    public ActorId ActorIdHint { get; }

    public TargetId TargetId { get; }
    public IntentActionId Action { get; }
    public StateVersion ExpectedStateVersion { get; }
    public ServerTick ClientObservedTick { get; }
    public IIntentParameters Parameters { get; }
}

public static class LogIntentActions
{
    public static readonly IntentActionId RouteToProcedure = IntentActionId.From("route_to_procedure");
    public static readonly IntentActionId ReturnFromProcedure = IntentActionId.From("return_from_procedure");
    public static readonly IntentActionId RouteToSawQueue = IntentActionId.From("route_to_saw_queue");
    public static readonly IntentActionId WriteOff = IntentActionId.From("write_off");
}
