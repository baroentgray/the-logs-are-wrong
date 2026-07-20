using System.Diagnostics.CodeAnalysis;

namespace TheLogsAreWrong.Domain.Identifiers;

internal static class IdGuard
{
    internal static bool IsValid([NotNullWhen(true)] string? value) => !string.IsNullOrWhiteSpace(value);
}

public readonly record struct ShiftId
{
    public string? Value { get; }
    public bool IsDefault => Value is null;
    private ShiftId(string value) => Value = value;
    public static ShiftId From(string value) => TryFrom(value, out var result) ? result : throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", nameof(value));
    public static bool TryFrom(string? value, out ShiftId result) { if (!IdGuard.IsValid(value)) { result = default; return false; } result = new ShiftId(value); return true; }
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct LogId
{
    public string? Value { get; }
    public bool IsDefault => Value is null;
    private LogId(string value) => Value = value;
    public static LogId From(string value) => TryFrom(value, out var result) ? result : throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", nameof(value));
    public static bool TryFrom(string? value, out LogId result) { if (!IdGuard.IsValid(value)) { result = default; return false; } result = new LogId(value); return true; }
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct SpeciesId
{
    public string? Value { get; }
    public bool IsDefault => Value is null;
    private SpeciesId(string value) => Value = value;
    public static SpeciesId From(string value) => TryFrom(value, out var result) ? result : throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", nameof(value));
    public static bool TryFrom(string? value, out SpeciesId result) { if (!IdGuard.IsValid(value)) { result = default; return false; } result = new SpeciesId(value); return true; }
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct AnomalyId
{
    public string? Value { get; }
    public bool IsDefault => Value is null;
    private AnomalyId(string value) => Value = value;
    public static AnomalyId From(string value) => TryFrom(value, out var result) ? result : throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", nameof(value));
    public static bool TryFrom(string? value, out AnomalyId result) { if (!IdGuard.IsValid(value)) { result = default; return false; } result = new AnomalyId(value); return true; }
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct FlagId
{
    public string? Value { get; }
    public bool IsDefault => Value is null;
    private FlagId(string value) => Value = value;
    public static FlagId From(string value) => TryFrom(value, out var result) ? result : throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", nameof(value));
    public static bool TryFrom(string? value, out FlagId result) { if (!IdGuard.IsValid(value)) { result = default; return false; } result = new FlagId(value); return true; }
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct ItemId
{
    public string? Value { get; }
    public bool IsDefault => Value is null;
    private ItemId(string value) => Value = value;
    public static ItemId From(string value) => TryFrom(value, out var result) ? result : throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", nameof(value));
    public static bool TryFrom(string? value, out ItemId result) { if (!IdGuard.IsValid(value)) { result = default; return false; } result = new ItemId(value); return true; }
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct ProfileId
{
    public string? Value { get; }
    public bool IsDefault => Value is null;
    private ProfileId(string value) => Value = value;
    public static ProfileId From(string value) => TryFrom(value, out var result) ? result : throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", nameof(value));
    public static bool TryFrom(string? value, out ProfileId result) { if (!IdGuard.IsValid(value)) { result = default; return false; } result = new ProfileId(value); return true; }
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct EffectEventId
{
    public string? Value { get; }
    public bool IsDefault => Value is null;
    private EffectEventId(string value) => Value = value;
    public static EffectEventId From(string value) => TryFrom(value, out var result) ? result : throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", nameof(value));
    public static bool TryFrom(string? value, out EffectEventId result) { if (!IdGuard.IsValid(value)) { result = default; return false; } result = new EffectEventId(value); return true; }
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct IntentId
{
    public string? Value { get; }
    public bool IsDefault => Value is null;
    private IntentId(string value) => Value = value;
    public static IntentId From(string value) => TryFrom(value, out var result) ? result : throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", nameof(value));
    public static bool TryFrom(string? value, out IntentId result) { if (!IdGuard.IsValid(value)) { result = default; return false; } result = new IntentId(value); return true; }
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifies an actor. Client-provided actor_id_hint values are untrusted at the adapter boundary.</summary>
public readonly record struct ActorId
{
    public string? Value { get; }
    public bool IsDefault => Value is null;
    private ActorId(string value) => Value = value;
    public static ActorId From(string value) => TryFrom(value, out var result) ? result : throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", nameof(value));
    public static bool TryFrom(string? value, out ActorId result) { if (!IdGuard.IsValid(value)) { result = default; return false; } result = new ActorId(value); return true; }
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct EventId
{
    public string? Value { get; }
    public bool IsDefault => Value is null;
    private EventId(string value) => Value = value;
    public static EventId From(string value) => TryFrom(value, out var result) ? result : throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", nameof(value));
    public static bool TryFrom(string? value, out EventId result) { if (!IdGuard.IsValid(value)) { result = default; return false; } result = new EventId(value); return true; }
    public override string ToString() => Value ?? string.Empty;
}
