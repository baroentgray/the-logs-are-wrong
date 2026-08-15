using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;

namespace TheLogsAreWrong.Domain.Quota;

/// <summary>Immutable quota targets, monotonic credited progress, and settled-log evidence.</summary>
public sealed class QuotaRuntimeState
{
    private QuotaRuntimeState(
        int targetTotal,
        ImmutableDictionary<SpeciesId, int> targetBySpecies,
        int minimumCorrectlyProcessedAnomalies,
        ImmutableDictionary<SpeciesId, int> creditedBySpecies,
        int totalCreditedUnits,
        int correctlyProcessedAnomalies,
        ImmutableHashSet<LogId> settledLogIds)
    {
        TargetTotal = targetTotal;
        TargetBySpecies = targetBySpecies;
        MinimumCorrectlyProcessedAnomalies = minimumCorrectlyProcessedAnomalies;
        CreditedBySpecies = creditedBySpecies;
        TotalCreditedUnits = totalCreditedUnits;
        CorrectlyProcessedAnomalies = correctlyProcessedAnomalies;
        SettledLogIds = settledLogIds;
    }

    public int TargetTotal { get; }
    public ImmutableDictionary<SpeciesId, int> TargetBySpecies { get; }
    public int MinimumCorrectlyProcessedAnomalies { get; }
    public ImmutableDictionary<SpeciesId, int> CreditedBySpecies { get; }
    public int TotalCreditedUnits { get; }
    public int CorrectlyProcessedAnomalies { get; }
    public ImmutableHashSet<LogId> SettledLogIds { get; }

    public bool ObjectivesSatisfied =>
        TargetBySpecies.All(target => GetCreditedUnits(target.Key) >= target.Value) &&
        CorrectlyProcessedAnomalies >= MinimumCorrectlyProcessedAnomalies;

    public static QuotaRuntimeState Create(ShiftConfiguration configuration)
    {
        if (configuration is null) { throw new ArgumentNullException("configuration"); }
        return Create(configuration.Objectives);
    }

    public static QuotaRuntimeState Create(ObjectivesDefinition objectives)
    {
        if (objectives is null) { throw new ArgumentNullException("objectives"); }
        if (objectives.Quota is null) { throw new ArgumentNullException("objectives.Quota"); }
        if (objectives.Quota.BySpecies is null) { throw new ArgumentNullException("objectives.Quota.BySpecies"); }

        if (objectives.Quota.Total < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(objectives), "Quota target total cannot be negative.");
        }

        if (objectives.MinCorrectlyProcessedAnomalies < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(objectives), "Minimum correctly processed anomalies cannot be negative.");
        }

        var targets = ImmutableDictionary.CreateBuilder<SpeciesId, int>();
        var computedTotal = 0;
        foreach (var target in objectives.Quota.BySpecies)
        {
            if (target.Key.IsDefault)
            {
                throw new ArgumentException("Quota targets must use initialized species identifiers.", nameof(objectives));
            }

            if (target.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(objectives), "Quota target units cannot be negative.");
            }

            computedTotal = checked(computedTotal + target.Value);
            targets.Add(target.Key, target.Value);
        }

        if (computedTotal != objectives.Quota.Total)
        {
            throw new ArgumentException("Quota target total must equal the sum of species targets.", nameof(objectives));
        }

        return new QuotaRuntimeState(
            objectives.Quota.Total,
            targets.ToImmutable(),
            objectives.MinCorrectlyProcessedAnomalies,
            ImmutableDictionary<SpeciesId, int>.Empty,
            0,
            0,
            ImmutableHashSet<LogId>.Empty);
    }

    public int GetCreditedUnits(SpeciesId speciesId)
    {
        if (speciesId.IsDefault)
        {
            throw new ArgumentException("Species identifier must be initialized.", nameof(speciesId));
        }

        return CreditedBySpecies.GetValueOrDefault(speciesId);
    }

    public bool IsSettled(LogId logId)
    {
        if (logId.IsDefault)
        {
            throw new ArgumentException("Log identifier must be initialized.", nameof(logId));
        }

        return SettledLogIds.Contains(logId);
    }

    public bool ValueEquals(QuotaRuntimeState? other) =>
        other is not null &&
        TargetTotal == other.TargetTotal &&
        MinimumCorrectlyProcessedAnomalies == other.MinimumCorrectlyProcessedAnomalies &&
        TotalCreditedUnits == other.TotalCreditedUnits &&
        CorrectlyProcessedAnomalies == other.CorrectlyProcessedAnomalies &&
        DictionariesEqual(TargetBySpecies, other.TargetBySpecies) &&
        DictionariesEqual(CreditedBySpecies, other.CreditedBySpecies) &&
        SettledLogIds.SetEquals(other.SettledLogIds);

    internal QuotaRuntimeState WithSettlement(
        ImmutableDictionary<SpeciesId, int> creditedBySpecies,
        int totalCreditedUnits,
        int correctlyProcessedAnomalies,
        ImmutableHashSet<LogId> settledLogIds) =>
        new(
            TargetTotal,
            TargetBySpecies,
            MinimumCorrectlyProcessedAnomalies,
            creditedBySpecies,
            totalCreditedUnits,
            correctlyProcessedAnomalies,
            settledLogIds);

    private static bool DictionariesEqual(ImmutableDictionary<SpeciesId, int> first, ImmutableDictionary<SpeciesId, int> second) =>
        first.Count == second.Count && first.All(pair => second.TryGetValue(pair.Key, out var value) && value == pair.Value);
}

/// <summary>Already-resolved, concrete quota credit for exactly one log.</summary>
public sealed record QuotaSettlement
{
    public QuotaSettlement(LogId logId, SpeciesId? creditedSpecies, int creditedUnits, int correctAnomalyDelta)
    {
        if (logId.IsDefault)
        {
            throw new ArgumentException("Settlement log identifier must be initialized.", nameof(logId));
        }

        if (creditedSpecies is { } species && species.IsDefault)
        {
            throw new ArgumentException("Credited species must be initialized when present.", nameof(creditedSpecies));
        }

        if (creditedUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(creditedUnits), "Credited units cannot be negative.");
        }

        if (correctAnomalyDelta < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(correctAnomalyDelta), "Correct anomaly delta cannot be negative.");
        }

        if (creditedSpecies is null && creditedUnits != 0)
        {
            throw new ArgumentException("A settlement without a credited species must have zero credited units.", nameof(creditedUnits));
        }

        LogId = logId;
        CreditedSpecies = creditedSpecies;
        CreditedUnits = creditedUnits;
        CorrectAnomalyDelta = correctAnomalyDelta;
    }

    public LogId LogId { get; }
    public SpeciesId? CreditedSpecies { get; }
    public int CreditedUnits { get; }
    public int CorrectAnomalyDelta { get; }
}

public sealed record QuotaSettlementDescriptor(
    LogId LogId,
    SpeciesId? CreditedSpecies,
    int CreditedUnits,
    int CorrectAnomalyDelta,
    int PriorSpeciesCredit,
    int CurrentSpeciesCredit,
    int PriorTotalCreditedUnits,
    int CurrentTotalCreditedUnits,
    int PriorCorrectAnomalyCount,
    int CurrentCorrectAnomalyCount);

public abstract record QuotaSettlementResult(QuotaRuntimeState State);

public sealed record QuotaSettlementAccepted(QuotaRuntimeState State, QuotaSettlementDescriptor Descriptor)
    : QuotaSettlementResult(State);

public sealed record QuotaSettlementDuplicate(QuotaRuntimeState State, LogId LogId)
    : QuotaSettlementResult(State);

/// <summary>Applies concrete settlements without resolving anomaly selectors or mutating log runtime state.</summary>
public sealed class QuotaSettlementService
{
    public QuotaSettlementResult Apply(QuotaRuntimeState state, QuotaSettlement settlement)
    {
        if (state is null) { throw new ArgumentNullException("state"); }
        if (settlement is null) { throw new ArgumentNullException("settlement"); }

        if (state.IsSettled(settlement.LogId))
        {
            return new QuotaSettlementDuplicate(state, settlement.LogId);
        }

        var priorTotal = state.TotalCreditedUnits;
        var currentTotal = checked(priorTotal + settlement.CreditedUnits);
        var priorCorrectAnomalyCount = state.CorrectlyProcessedAnomalies;
        var currentCorrectAnomalyCount = checked(priorCorrectAnomalyCount + settlement.CorrectAnomalyDelta);
        var creditedBySpecies = state.CreditedBySpecies;
        var priorSpeciesCredit = 0;
        var currentSpeciesCredit = 0;

        if (settlement.CreditedSpecies is { } creditedSpecies)
        {
            priorSpeciesCredit = state.GetCreditedUnits(creditedSpecies);
            currentSpeciesCredit = checked(priorSpeciesCredit + settlement.CreditedUnits);
            creditedBySpecies = creditedBySpecies.SetItem(creditedSpecies, currentSpeciesCredit);
        }

        var updatedState = state.WithSettlement(
            creditedBySpecies,
            currentTotal,
            currentCorrectAnomalyCount,
            state.SettledLogIds.Add(settlement.LogId));
        var descriptor = new QuotaSettlementDescriptor(
            settlement.LogId,
            settlement.CreditedSpecies,
            settlement.CreditedUnits,
            settlement.CorrectAnomalyDelta,
            priorSpeciesCredit,
            currentSpeciesCredit,
            priorTotal,
            currentTotal,
            priorCorrectAnomalyCount,
            currentCorrectAnomalyCount);

        return new QuotaSettlementAccepted(updatedState, descriptor);
    }
}
