namespace Tlaw.Dispatcher;

public enum ExecutorAvailability
{
    Available,
    Degraded,
    QuotaExhausted,
    Offline,
    Unknown
}

public sealed record AgentSnapshot(
    string Agent,
    IReadOnlyList<string> Capabilities,
    ExecutorAvailability Availability);

public sealed record ExecutorSelectionRequest(
    string WorkType,
    string AutonomyLevel,
    string PreferredAgent,
    IReadOnlyList<string> EligibleAgents,
    IReadOnlyList<string> RequiredCapabilities,
    IReadOnlyList<AgentSnapshot> Snapshots,
    IReadOnlyDictionary<string, ExecutorAvailability> AvailabilityOverrides,
    string? ExecutorOverride);

public sealed record AppliedAvailabilityOverride(
    string Agent,
    ExecutorAvailability Original,
    ExecutorAvailability Effective);

public sealed record ExecutorSelection(
    string SelectedAgent,
    ExecutorAvailability EffectiveAvailability,
    bool ExecutorOverrideApplied,
    IReadOnlyList<AppliedAvailabilityOverride> AvailabilityOverrides);

public sealed class ExecutorSelectionException(string message) : Exception(message);

public static class ExecutorSelector
{
    public static ExecutorSelection Select(ExecutorSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkType);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AutonomyLevel);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PreferredAgent);
        ArgumentNullException.ThrowIfNull(request.EligibleAgents);
        ArgumentNullException.ThrowIfNull(request.RequiredCapabilities);
        ArgumentNullException.ThrowIfNull(request.Snapshots);
        ArgumentNullException.ThrowIfNull(request.AvailabilityOverrides);

        var snapshotsByAgent = IndexSnapshots(request.Snapshots);
        var candidates = BuildCandidates(request, snapshotsByAgent);
        var appliedOverrides = BuildAppliedOverrides(request, snapshotsByAgent);

        if (request.ExecutorOverride is not null)
        {
            return SelectOverride(request, candidates, appliedOverrides);
        }

        var selected = candidates
            .Where(candidate => candidate.IsSelectable)
            .OrderBy(candidate => AvailabilityRank(candidate.EffectiveAvailability))
            .ThenBy(candidate => string.Equals(candidate.Agent, request.PreferredAgent, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(candidate => candidate.EligibleIndex)
            .FirstOrDefault();
        if (selected is null)
        {
            throw new ExecutorSelectionException("no selectable executor satisfies the task policy, capabilities, and effective availability.");
        }

        return new ExecutorSelection(selected.Agent, selected.EffectiveAvailability, false, appliedOverrides);
    }

    private static IReadOnlyDictionary<string, AgentSnapshot> IndexSnapshots(IReadOnlyList<AgentSnapshot> snapshots)
    {
        var indexed = new Dictionary<string, AgentSnapshot>(StringComparer.Ordinal);
        foreach (var snapshot in snapshots)
        {
            if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.Agent) || snapshot.Capabilities is null)
            {
                throw new ExecutorSelectionException("agent snapshot is incomplete.");
            }

            if (!indexed.TryAdd(snapshot.Agent, snapshot))
            {
                throw new ExecutorSelectionException($"agent snapshot contains duplicate agent '{snapshot.Agent}'.");
            }
        }

        return indexed;
    }

    private static IReadOnlyList<Candidate> BuildCandidates(ExecutorSelectionRequest request, IReadOnlyDictionary<string, AgentSnapshot> snapshotsByAgent)
    {
        var candidates = new List<Candidate>();
        for (var index = 0; index < request.EligibleAgents.Count; index++)
        {
            var agent = request.EligibleAgents[index];
            if (string.IsNullOrWhiteSpace(agent) || !snapshotsByAgent.TryGetValue(agent, out var snapshot))
            {
                throw new ExecutorSelectionException($"eligible agent '{agent}' has no snapshot record.");
            }

            var effectiveAvailability = request.AvailabilityOverrides.TryGetValue(agent, out var overrideAvailability)
                ? overrideAvailability
                : snapshot.Availability;
            var hasCapabilities = request.RequiredCapabilities.All(capability => snapshot.Capabilities.Contains(capability, StringComparer.Ordinal));
            var isPolicyPermitted = IsPolicyPermitted(request.WorkType, request.AutonomyLevel, agent);
            candidates.Add(new Candidate(agent, index, effectiveAvailability, hasCapabilities, isPolicyPermitted));
        }

        foreach (var agent in request.AvailabilityOverrides.Keys)
        {
            if (!request.EligibleAgents.Contains(agent, StringComparer.Ordinal) || !snapshotsByAgent.ContainsKey(agent))
            {
                throw new ExecutorSelectionException($"availability override target '{agent}' is not an eligible agent with a snapshot record.");
            }
        }

        return candidates;
    }

    private static IReadOnlyList<AppliedAvailabilityOverride> BuildAppliedOverrides(ExecutorSelectionRequest request, IReadOnlyDictionary<string, AgentSnapshot> snapshotsByAgent)
    {
        var applied = new List<AppliedAvailabilityOverride>();
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var agent in request.EligibleAgents)
        {
            if (emitted.Add(agent) && request.AvailabilityOverrides.TryGetValue(agent, out var effective))
            {
                applied.Add(new AppliedAvailabilityOverride(agent, snapshotsByAgent[agent].Availability, effective));
            }
        }

        return applied;
    }

    private static ExecutorSelection SelectOverride(ExecutorSelectionRequest request, IReadOnlyList<Candidate> candidates, IReadOnlyList<AppliedAvailabilityOverride> appliedOverrides)
    {
        var overrideAgent = request.ExecutorOverride!;
        var candidate = candidates.FirstOrDefault(item => string.Equals(item.Agent, overrideAgent, StringComparison.Ordinal));
        if (candidate is null)
        {
            throw new ExecutorSelectionException($"executor override target '{overrideAgent}' is not eligible for the task.");
        }

        if (!candidate.HasCapabilities)
        {
            throw new ExecutorSelectionException($"executor override target '{overrideAgent}' does not satisfy every required capability.");
        }

        if (!candidate.IsPolicyPermitted)
        {
            throw new ExecutorSelectionException($"executor override target '{overrideAgent}' is not permitted by the task policy.");
        }

        if (!IsSelectableAvailability(candidate.EffectiveAvailability))
        {
            throw new ExecutorSelectionException($"executor override target '{overrideAgent}' is not selectable with effective availability {ToWireValue(candidate.EffectiveAvailability)}.");
        }

        return new ExecutorSelection(candidate.Agent, candidate.EffectiveAvailability, true, appliedOverrides);
    }

    private static bool IsPolicyPermitted(string workType, string autonomyLevel, string agent)
    {
        if (string.Equals(workType, "implementation", StringComparison.Ordinal) && agent is "local" or "grok")
        {
            return false;
        }

        return agent != "local" ||
            (string.Equals(workType, "read_only_analysis", StringComparison.Ordinal) && string.Equals(autonomyLevel, "read_only", StringComparison.Ordinal));
    }

    internal static bool IsSelectableAvailability(ExecutorAvailability availability) => availability is ExecutorAvailability.Available or ExecutorAvailability.Degraded;

    internal static string ToWireValue(ExecutorAvailability availability) => availability switch
    {
        ExecutorAvailability.Available => "AVAILABLE",
        ExecutorAvailability.Degraded => "DEGRADED",
        ExecutorAvailability.QuotaExhausted => "QUOTA_EXHAUSTED",
        ExecutorAvailability.Offline => "OFFLINE",
        ExecutorAvailability.Unknown => "UNKNOWN",
        _ => throw new ArgumentOutOfRangeException(nameof(availability))
    };

    private static int AvailabilityRank(ExecutorAvailability availability) => availability switch
    {
        ExecutorAvailability.Available => 0,
        ExecutorAvailability.Degraded => 1,
        _ => int.MaxValue
    };

    private sealed record Candidate(
        string Agent,
        int EligibleIndex,
        ExecutorAvailability EffectiveAvailability,
        bool HasCapabilities,
        bool IsPolicyPermitted)
    {
        internal bool IsSelectable => HasCapabilities && IsPolicyPermitted && IsSelectableAvailability(EffectiveAvailability);
    }
}
