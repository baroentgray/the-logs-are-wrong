using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Anomalies;

public sealed class AnomalyDefinitionNotFoundException : InvalidOperationException
{
    public AnomalyDefinitionNotFoundException(AnomalyId anomalyId)
        : base($"Anomaly definition '{anomalyId}' was not found.") => AnomalyId = anomalyId;

    public AnomalyId AnomalyId { get; }
}

public sealed record ProcedurePlanStep
{
    public ProcedurePlanStep(ItemId item, bool consumes, int? holdSeconds)
    {
        if (item.IsDefault)
        {
            throw new ArgumentException("Procedure step item must be initialized.", nameof(item));
        }

        if (holdSeconds is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(holdSeconds), "Procedure step hold seconds cannot be negative.");
        }

        Item = item;
        Consumes = consumes;
        HoldSeconds = holdSeconds;
    }

    public ItemId Item { get; }
    public bool Consumes { get; }
    public int? HoldSeconds { get; }
}

public sealed record ProcedurePlan
{
    public ProcedurePlan(AnomalyId anomalyId, ImmutableArray<ProcedurePlanStep> steps, ImmutableHashSet<FlagId> grantedFlags)
    {
        if (anomalyId.IsDefault)
        {
            throw new ArgumentException("Procedure plan anomaly identifier must be initialized.", nameof(anomalyId));
        }

        if (steps.IsDefault)
        {
            throw new ArgumentException("Procedure plan steps must be initialized.", nameof(steps));
        }

        if (grantedFlags is null) { throw new ArgumentNullException("grantedFlags"); }
        foreach (var step in steps)
        {
            if (step is null) { throw new ArgumentNullException("step"); }
        }

        foreach (var flag in grantedFlags)
        {
            if (flag.IsDefault)
            {
                throw new ArgumentException("Procedure plan granted flags must be initialized.", nameof(grantedFlags));
            }
        }

        AnomalyId = anomalyId;
        Steps = steps;
        GrantedFlags = grantedFlags;
    }

    public AnomalyId AnomalyId { get; }
    public ImmutableArray<ProcedurePlanStep> Steps { get; }
    public ImmutableHashSet<FlagId> GrantedFlags { get; }
}

/// <summary>Immutable procedure plans derived from an already-validated anomaly catalog.</summary>
public sealed class ProcedurePlanResolver
{
    private readonly ImmutableDictionary<AnomalyId, ProcedurePlan> _plans;

    public ProcedurePlanResolver(AnomalyCatalog catalog)
    {
        if (catalog is null) { throw new ArgumentNullException("catalog"); }
        if (catalog.Definitions is null) { throw new ArgumentNullException("catalog.Definitions"); }

        var plans = ImmutableDictionary.CreateBuilder<AnomalyId, ProcedurePlan>();
        foreach (var pair in catalog.Definitions)
        {
            if (pair.Key.IsDefault)
            {
                throw new ArgumentException("Anomaly catalog keys must be initialized.", nameof(catalog));
            }

            var definition = AnomalyDefinitionGuard.RequireDefinition(pair.Key, pair.Value);
            if (definition.Procedure is null) { throw new ArgumentNullException("definition.Procedure"); }
            if (definition.Procedure.Steps.IsDefault)
            {
                throw new ArgumentException("Procedure steps must be initialized.", nameof(catalog));
            }

            if (definition.Procedure.GrantsFlags is null) { throw new ArgumentNullException("definition.Procedure.GrantsFlags"); }
            var steps = ImmutableArray.CreateBuilder<ProcedurePlanStep>(definition.Procedure.Steps.Length);
            foreach (var step in definition.Procedure.Steps)
            {
                if (step.Item.IsDefault)
                {
                    throw new ArgumentException("Procedure step items must be initialized.", nameof(catalog));
                }

                steps.Add(new ProcedurePlanStep(step.Item, step.Consumes, step.HoldSeconds));
            }

            plans.Add(pair.Key, new ProcedurePlan(pair.Key, steps.MoveToImmutable(), definition.Procedure.GrantsFlags));
        }

        _plans = plans.ToImmutable();
    }

    public ProcedurePlan GetPlan(AnomalyId anomalyId)
    {
        if (anomalyId.IsDefault)
        {
            throw new ArgumentException("Anomaly identifier must be initialized.", nameof(anomalyId));
        }

        return _plans.TryGetValue(anomalyId, out var plan)
            ? plan
            : throw new AnomalyDefinitionNotFoundException(anomalyId);
    }

    public bool TryGetPlan(LogRuntimeState log, out ProcedurePlan? plan)
    {
        if (log is null) { throw new ArgumentNullException("log"); }
        if (log.Anomaly is not { } anomalyId)
        {
            plan = null;
            return false;
        }

        plan = GetPlan(anomalyId);
        return true;
    }
}

public sealed record ProcessingResolution
{
    public ProcessingResolution(
        LogId logId,
        bool isAnomalous,
        bool? allRequiredFlagsPresent,
        LogState terminalState,
        QuotaSettlement settlement,
        ImmutableArray<EffectDefinition> effects)
    {
        if (logId.IsDefault)
        {
            throw new ArgumentException("Resolution log identifier must be initialized.", nameof(logId));
        }

        if (isAnomalous != allRequiredFlagsPresent.HasValue)
        {
            throw new ArgumentException("Only anomalous resolutions may report required-flag status.", nameof(allRequiredFlagsPresent));
        }

        if (terminalState != LogState.PROCESSED)
        {
            throw new ArgumentException("Current processing outcomes must terminate in PROCESSED.", nameof(terminalState));
        }

        if (settlement is null) { throw new ArgumentNullException("settlement"); }
        if (settlement.LogId != logId)
        {
            throw new ArgumentException("Settlement log identity must equal the resolved log identity.", nameof(settlement));
        }

        if (effects.IsDefault)
        {
            throw new ArgumentException("Resolution effects must be initialized.", nameof(effects));
        }

        LogId = logId;
        IsAnomalous = isAnomalous;
        AllRequiredFlagsPresent = allRequiredFlagsPresent;
        TerminalState = terminalState;
        Settlement = settlement;
        Effects = AnomalyDefinitionGuard.CopyEffects(effects);
    }

    public LogId LogId { get; }
    public bool IsAnomalous { get; }
    public bool? AllRequiredFlagsPresent { get; }
    public LogState TerminalState { get; }
    public QuotaSettlement Settlement { get; }
    public ImmutableArray<EffectDefinition> Effects { get; }

    public bool ValueEquals(ProcessingResolution? other) =>
        other is not null &&
        LogId == other.LogId &&
        IsAnomalous == other.IsAnomalous &&
        AllRequiredFlagsPresent == other.AllRequiredFlagsPresent &&
        TerminalState == other.TerminalState &&
        Settlement == other.Settlement &&
        Effects.Length == other.Effects.Length &&
        Effects.SequenceEqual(other.Effects);
}

/// <summary>Resolves immutable processing data; it does not apply quota, transition logs, or execute effects.</summary>
public static class AnomalyProcessingResolver
{
    public static ProcessingResolution Resolve(LogRuntimeState log, AnomalyCatalog catalog)
    {
        if (log is null) { throw new ArgumentNullException("log"); }
        if (catalog is null) { throw new ArgumentNullException("catalog"); }

        if (log.Anomaly is not { } anomalyId)
        {
            return new ProcessingResolution(
                log.LogId,
                false,
                null,
                LogState.PROCESSED,
                new QuotaSettlement(log.LogId, log.TrueSpecies, 1, 0),
                ImmutableArray<EffectDefinition>.Empty);
        }

        var definition = AnomalyDefinitionGuard.Find(catalog, anomalyId);
        if (definition.Processing is null) { throw new ArgumentNullException("definition.Processing"); }
        if (definition.Processing.RequiredFlags is null) { throw new ArgumentNullException("definition.Processing.RequiredFlags"); }

        var allRequiredFlagsPresent = definition.Processing.RequiredFlags.IsSubsetOf(log.Flags);
        var outcome = allRequiredFlagsPresent ? definition.Processing.OnCorrect : definition.Processing.OnIncorrect;
        if (outcome is null) { throw new ArgumentNullException("outcome"); }

        var creditedSpecies = ResolveCreditedSpecies(outcome.QuotaCredit, log);
        var effects = AnomalyDefinitionGuard.CopyEffects(outcome.Effects);
        return new ProcessingResolution(
            log.LogId,
            true,
            allRequiredFlagsPresent,
            outcome.TerminalState,
            new QuotaSettlement(log.LogId, creditedSpecies, outcome.QuotaCredit.Units, outcome.CorrectAnomalyDelta),
            effects);
    }

    private static SpeciesId? ResolveCreditedSpecies(QuotaCreditDefinition quotaCredit, LogRuntimeState log)
    {
        if (quotaCredit is null) { throw new ArgumentNullException("quotaCredit"); }
        return quotaCredit.Species switch
        {
            SpeciesCreditRule.true_species => log.TrueSpecies,
            SpeciesCreditRule.declared_species => log.DeclaredSpecies,
            SpeciesCreditRule.none when quotaCredit.Units == 0 => null,
            SpeciesCreditRule.none => throw new ArgumentException("The none species selector requires zero credited units.", nameof(quotaCredit)),
            _ => throw new ArgumentOutOfRangeException(nameof(quotaCredit), "Unknown species credit selector.")
        };
    }
}

public abstract record WrongActionResolution
{
    protected WrongActionResolution(LogId logId, ItemId attemptedItem)
    {
        if (logId.IsDefault || attemptedItem.IsDefault)
        {
            throw new ArgumentException("Wrong-action identities must be initialized.");
        }

        LogId = logId;
        AttemptedItem = attemptedItem;
    }

    public LogId LogId { get; }
    public ItemId AttemptedItem { get; }
}

public sealed record ConfiguredWrongActionResolution : WrongActionResolution
{
    public ConfiguredWrongActionResolution(
        LogId logId,
        AnomalyId anomalyId,
        ItemId attemptedItem,
        bool consumes,
        bool leavesStateUnchanged,
        LogState? terminalState,
        ImmutableArray<EffectDefinition> effects)
        : base(logId, attemptedItem)
    {
        if (anomalyId.IsDefault)
        {
            throw new ArgumentException("Wrong-action anomaly identifier must be initialized.", nameof(anomalyId));
        }

        if (leavesStateUnchanged && terminalState is not null)
        {
            throw new ArgumentException("An unchanged-state wrong action cannot specify a terminal state.", nameof(terminalState));
        }

        if (!leavesStateUnchanged && terminalState is null)
        {
            throw new ArgumentException("A state-changing wrong action must specify a terminal state.", nameof(terminalState));
        }

        if (terminalState is { } state && !Enum.IsDefined(typeof(LogState), state))
        {
            throw new ArgumentOutOfRangeException(nameof(terminalState), "Wrong-action terminal state is not defined.");
        }

        if (effects.IsDefault)
        {
            throw new ArgumentException("Wrong-action effects must be initialized.", nameof(effects));
        }

        AnomalyId = anomalyId;
        Consumes = consumes;
        LeavesStateUnchanged = leavesStateUnchanged;
        TerminalState = terminalState;
        Effects = AnomalyDefinitionGuard.CopyEffects(effects);
    }

    public AnomalyId AnomalyId { get; }
    public bool Consumes { get; }
    public bool LeavesStateUnchanged { get; }
    public LogState? TerminalState { get; }
    public ImmutableArray<EffectDefinition> Effects { get; }
}

public sealed record WrongActionNotConfigured : WrongActionResolution
{
    public WrongActionNotConfigured(LogId logId, AnomalyId? anomalyId, ItemId attemptedItem)
        : base(logId, attemptedItem)
    {
        if (anomalyId is { } value && value.IsDefault)
        {
            throw new ArgumentException("Wrong-action anomaly identifier must be initialized when present.", nameof(anomalyId));
        }

        AnomalyId = anomalyId;
    }

    public AnomalyId? AnomalyId { get; }
}

/// <summary>Resolves configured wrong actions as data only; it never consumes inventory or changes a log.</summary>
public static class WrongActionResolver
{
    public static WrongActionResolution Resolve(LogRuntimeState log, ItemId attemptedItem, AnomalyCatalog catalog)
    {
        if (log is null) { throw new ArgumentNullException("log"); }
        if (attemptedItem.IsDefault)
        {
            throw new ArgumentException("Attempted item identifier must be initialized.", nameof(attemptedItem));
        }

        if (catalog is null) { throw new ArgumentNullException("catalog"); }
        if (log.Anomaly is not { } anomalyId)
        {
            return new WrongActionNotConfigured(log.LogId, null, attemptedItem);
        }

        var definition = AnomalyDefinitionGuard.Find(catalog, anomalyId);
        if (definition.WrongActions is null) { throw new ArgumentNullException("definition.WrongActions"); }
        if (!definition.WrongActions.TryGetValue(attemptedItem, out var wrongAction))
        {
            return new WrongActionNotConfigured(log.LogId, anomalyId, attemptedItem);
        }

        if (wrongAction is null) { throw new ArgumentNullException("wrongAction"); }
        return new ConfiguredWrongActionResolution(
            log.LogId,
            anomalyId,
            attemptedItem,
            wrongAction.Consumes,
            wrongAction.LeavesStateUnchanged,
            wrongAction.TerminalState,
            AnomalyDefinitionGuard.CopyEffects(wrongAction.Effects));
    }
}

internal static class AnomalyDefinitionGuard
{
    internal static AnomalyDefinition Find(AnomalyCatalog catalog, AnomalyId anomalyId)
    {
        if (catalog is null) { throw new ArgumentNullException("catalog"); }
        if (catalog.Definitions is null) { throw new ArgumentNullException("catalog.Definitions"); }
        if (anomalyId.IsDefault)
        {
            throw new ArgumentException("Anomaly identifier must be initialized.", nameof(anomalyId));
        }

        return catalog.Definitions.TryGetValue(anomalyId, out var definition)
            ? RequireDefinition(anomalyId, definition)
            : throw new AnomalyDefinitionNotFoundException(anomalyId);
    }

    internal static AnomalyDefinition RequireDefinition(AnomalyId key, AnomalyDefinition? definition)
    {
        if (key.IsDefault)
        {
            throw new ArgumentException("Anomaly catalog keys must be initialized.", nameof(key));
        }

        if (definition is null) { throw new ArgumentNullException("definition"); }
        if (definition.Id.IsDefault || definition.Id != key)
        {
            throw new ArgumentException("Anomaly definition identity must match its catalog key.", nameof(definition));
        }

        return definition;
    }

    internal static ImmutableArray<EffectDefinition> CopyEffects(ImmutableArray<EffectDefinition> effects)
    {
        if (effects.IsDefault)
        {
            throw new ArgumentException("Effects must be initialized.", nameof(effects));
        }

        foreach (var effect in effects)
        {
            if (effect is null) { throw new ArgumentNullException("effect"); }
            if (effect.Event.IsDefault)
            {
                throw new ArgumentException("Effect event identifiers must be initialized.", nameof(effects));
            }

            if (effect.DurationSeconds is < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(effects), "Effect durations cannot be negative.");
            }

            if (!Enum.IsDefined(typeof(EffectType), effect.Type))
            {
                throw new ArgumentOutOfRangeException(nameof(effects), "Effect type is not defined.");
            }
        }

        return effects.ToImmutableArray();
    }
}
