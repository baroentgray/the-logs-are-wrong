using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class ExecutorSelectorTests
{
    [Fact]
    public void Preferred_agent_wins_among_equally_available_candidates()
    {
        var selection = Select(
            preferredAgent: "codex",
            eligibleAgents: ["claude", "codex"],
            snapshots: [Agent("claude", ExecutorAvailability.Available), Agent("codex", ExecutorAvailability.Available)]);

        Assert.Equal("codex", selection.SelectedAgent);
        Assert.Equal(ExecutorAvailability.Available, selection.EffectiveAvailability);
    }

    [Fact]
    public void Preferred_agent_wins_among_equally_degraded_candidates()
    {
        var selection = Select(
            preferredAgent: "codex",
            eligibleAgents: ["claude", "codex"],
            snapshots: [Agent("claude", ExecutorAvailability.Degraded), Agent("codex", ExecutorAvailability.Degraded)]);

        Assert.Equal("codex", selection.SelectedAgent);
        Assert.Equal(ExecutorAvailability.Degraded, selection.EffectiveAvailability);
    }

    [Fact]
    public void Available_fallback_beats_a_degraded_preferred_agent()
    {
        var selection = Select(
            preferredAgent: "codex",
            eligibleAgents: ["codex", "claude"],
            snapshots: [Agent("codex", ExecutorAvailability.Degraded), Agent("claude", ExecutorAvailability.Available)]);

        Assert.Equal("claude", selection.SelectedAgent);
        Assert.Equal(ExecutorAvailability.Available, selection.EffectiveAvailability);
    }

    [Fact]
    public void Declared_eligible_agent_order_breaks_remaining_ties()
    {
        var selection = Select(
            preferredAgent: "codex",
            eligibleAgents: ["claude", "grok"],
            snapshots: [Agent("claude", ExecutorAvailability.Available), Agent("grok", ExecutorAvailability.Available)]);

        Assert.Equal("claude", selection.SelectedAgent);
    }

    [Fact]
    public void Missing_required_capability_excludes_a_candidate()
    {
        var selection = Select(
            preferredAgent: "codex",
            eligibleAgents: ["codex", "claude"],
            snapshots: [
                new AgentSnapshot("codex", ["dotnet"], ExecutorAvailability.Available),
                Agent("claude", ExecutorAvailability.Available)
            ]);

        Assert.Equal("claude", selection.SelectedAgent);
    }

    [Fact]
    public void Capability_comparison_is_ordinal()
    {
        var selection = Select(
            preferredAgent: "codex",
            eligibleAgents: ["codex", "claude"],
            snapshots: [
                new AgentSnapshot("codex", ["DotNet", "yaml_protocol"], ExecutorAvailability.Available),
                Agent("claude", ExecutorAvailability.Available)
            ]);

        Assert.Equal("claude", selection.SelectedAgent);
    }

    [Theory]
    [InlineData(ExecutorAvailability.QuotaExhausted)]
    [InlineData(ExecutorAvailability.Offline)]
    [InlineData(ExecutorAvailability.Unknown)]
    public void Non_selectable_availability_states_are_excluded(ExecutorAvailability availability)
    {
        var selection = Select(
            preferredAgent: "codex",
            eligibleAgents: ["codex", "claude"],
            snapshots: [Agent("codex", availability), Agent("claude", ExecutorAvailability.Available)]);

        Assert.Equal("claude", selection.SelectedAgent);
    }

    [Fact]
    public void No_selectable_candidate_fails_explicitly()
    {
        var exception = Assert.Throws<ExecutorSelectionException>(() => Select(
            preferredAgent: "codex",
            eligibleAgents: ["codex", "claude"],
            snapshots: [Agent("codex", ExecutorAvailability.Offline), Agent("claude", ExecutorAvailability.Unknown)]));

        Assert.StartsWith("no selectable executor", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Availability_override_changes_the_effective_selection_and_is_recorded_in_eligible_order()
    {
        var selection = Select(
            preferredAgent: "codex",
            eligibleAgents: ["codex", "claude"],
            snapshots: [Agent("codex", ExecutorAvailability.Degraded), Agent("claude", ExecutorAvailability.Available)],
            overrides: new Dictionary<string, ExecutorAvailability>(StringComparer.Ordinal) { ["codex"] = ExecutorAvailability.Available });

        Assert.Equal("codex", selection.SelectedAgent);
        var applied = Assert.Single(selection.AvailabilityOverrides);
        Assert.Equal("codex", applied.Agent);
        Assert.Equal(ExecutorAvailability.Degraded, applied.Original);
        Assert.Equal(ExecutorAvailability.Available, applied.Effective);
    }

    [Fact]
    public void Executor_override_requires_a_fully_selectable_candidate()
    {
        var selection = Select(
            preferredAgent: "codex",
            eligibleAgents: ["codex", "claude"],
            snapshots: [Agent("codex", ExecutorAvailability.Available), Agent("claude", ExecutorAvailability.Degraded)],
            executorOverride: "claude");

        Assert.Equal("claude", selection.SelectedAgent);
        Assert.True(selection.ExecutorOverrideApplied);
    }

    [Fact]
    public void Executor_override_cannot_bypass_policy_or_unavailable_state()
    {
        var policyException = Assert.Throws<ExecutorSelectionException>(() => ExecutorSelector.Select(new ExecutorSelectionRequest(
            "implementation",
            "branch_write",
            "codex",
            ["codex", "grok"],
            ["dotnet", "yaml_protocol"],
            [Agent("codex", ExecutorAvailability.Available), Agent("grok", ExecutorAvailability.Available)],
            new Dictionary<string, ExecutorAvailability>(StringComparer.Ordinal),
            "grok")));
        Assert.Contains("policy", policyException.Message, StringComparison.Ordinal);

        var availabilityException = Assert.Throws<ExecutorSelectionException>(() => Select(
            preferredAgent: "codex",
            eligibleAgents: ["codex", "claude"],
            snapshots: [Agent("codex", ExecutorAvailability.Available), Agent("claude", ExecutorAvailability.QuotaExhausted)],
            executorOverride: "claude"));
        Assert.Contains("selectable", availabilityException.Message, StringComparison.Ordinal);
    }

    private static ExecutorSelection Select(
        string preferredAgent,
        IReadOnlyList<string> eligibleAgents,
        IReadOnlyList<AgentSnapshot> snapshots,
        IReadOnlyDictionary<string, ExecutorAvailability>? overrides = null,
        string? executorOverride = null) => ExecutorSelector.Select(new ExecutorSelectionRequest(
            "implementation",
            "branch_write",
            preferredAgent,
            eligibleAgents,
            ["dotnet", "yaml_protocol"],
            snapshots,
            overrides ?? new Dictionary<string, ExecutorAvailability>(StringComparer.Ordinal),
            executorOverride));

    private static AgentSnapshot Agent(string agent, ExecutorAvailability availability) => new(agent, ["dotnet", "yaml_protocol"], availability);
}
