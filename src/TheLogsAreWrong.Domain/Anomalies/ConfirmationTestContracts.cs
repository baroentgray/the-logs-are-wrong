using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Anomalies;

public sealed record ConfirmationTestPlan
{
    public ConfirmationTestPlan(AnomalyId anomalyId, ImmutableHashSet<ItemId> requiredTools, SimulationDuration duration, bool continuous, LineNoise? requiredLineNoise, bool resetWhenConditionLost, string result)
    {
        if (anomalyId.IsDefault) throw new ArgumentException("Confirmation anomaly identifier must be initialized.", nameof(anomalyId));
        ArgumentNullException.ThrowIfNull(requiredTools);
        if (requiredTools.IsEmpty || requiredTools.Any(tool => tool.IsDefault)) throw new ArgumentException("Confirmation tools must be initialized and non-empty.", nameof(requiredTools));
        if (duration.IsDefault || duration.Value <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
        if (requiredLineNoise is { } noise && !Enum.IsDefined(noise)) throw new ArgumentOutOfRangeException(nameof(requiredLineNoise));
        if (string.IsNullOrWhiteSpace(result)) throw new ArgumentException("Confirmation result must be non-blank.", nameof(result));
        AnomalyId = anomalyId; RequiredTools = requiredTools; Duration = duration; Continuous = continuous; RequiredLineNoise = requiredLineNoise; ResetWhenConditionLost = resetWhenConditionLost; Result = result;
    }
    public AnomalyId AnomalyId { get; }
    public ImmutableHashSet<ItemId> RequiredTools { get; }
    public SimulationDuration Duration { get; }
    public bool Continuous { get; }
    public LineNoise? RequiredLineNoise { get; }
    public bool ResetWhenConditionLost { get; }
    public string Result { get; }
}

public sealed class ConfirmationTestPlanResolver
{
    private readonly ImmutableDictionary<AnomalyId, ConfirmationTestPlan> _plans;
    public ConfirmationTestPlanResolver(AnomalyCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(catalog.Definitions);
        var plans = ImmutableDictionary.CreateBuilder<AnomalyId, ConfirmationTestPlan>();
        foreach (var pair in catalog.Definitions)
        {
            var definition = AnomalyDefinitionGuard.RequireDefinition(pair.Key, pair.Value);
            var test = definition.ConfirmTest ?? throw new ArgumentException("Confirmation definition is required.", nameof(catalog));
            if (test.Tools.IsDefaultOrEmpty) throw new ArgumentException("Confirmation tools must be non-empty.", nameof(catalog));
            var tools = ImmutableHashSet.CreateBuilder<ItemId>();
            foreach (var tool in test.Tools) if (tool.IsDefault || !tools.Add(tool)) throw new ArgumentException("Confirmation tools must be distinct initialized identifiers.", nameof(catalog));
            plans.Add(pair.Key, new ConfirmationTestPlan(pair.Key, tools.ToImmutable(), SimulationDuration.FromTicks(test.DurationSeconds), test.Continuous, test.RequiredLineNoise, test.Continuous || test.ResetWhenConditionLost == true, test.Result));
        }
        _plans = plans.ToImmutable();
    }
    public ConfirmationTestPlan GetPlan(AnomalyId anomalyId) => anomalyId.IsDefault ? throw new ArgumentException("Anomaly identifier must be initialized.", nameof(anomalyId)) : _plans.TryGetValue(anomalyId, out var plan) ? plan : throw new AnomalyDefinitionNotFoundException(anomalyId);
    public bool TryGetPlan(LogRuntimeState log, out ConfirmationTestPlan? plan)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (log.Anomaly is not { } anomaly) { plan = null; return false; }
        plan = GetPlan(anomaly); return true;
    }
}
