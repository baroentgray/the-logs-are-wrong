using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Tests.Determinism;

[Trait("Scope", "TLAW-012")]
public sealed class ScenarioDeterminismTests
{
    private static readonly DeterministicScenarioDriver Driver = new();

    [Fact]
    public void Ten_independent_identical_runs_match_state_journal_boundaries_and_canonical_events_without_cross_mutation()
    {
        var configuration = Fixture.LoadP0();
        var script = DeterministicScenarioScript.Baseline;
        var baseline = Driver.Run(configuration, script);
        var baselineEvents = baseline.Events.ToArray();
        var runs = Enumerable.Range(0, 10).Select(_ => Driver.Run(configuration, script)).ToArray();

        Assert.All(runs, run => AssertTraceEqual(baseline, run));
        Assert.NotSame(baseline.FinalState, runs[0].FinalState);
        Assert.NotSame(baseline.Journal, runs[0].Journal);
        Assert.Equal(baselineEvents, baseline.Events);
        Assert.Equal(ImmutableArray<CanonicalScenarioEvent>.Empty.GetType(), baseline.Events.GetType());
    }

    [Fact]
    public void Fixed_trace_has_exact_contiguous_sequence_version_tick_and_same_tick_script_order()
    {
        var trace = RunBaseline();

        Assert.Equal(9, trace.AcceptedOperationCount);
        Assert.Equal(trace.AcceptedOperationCount, trace.Events.Length);
        Assert.Equal(EventSequence.First, trace.Events[0].Sequence);
        Assert.Equal(StateVersion.From(1), trace.Events[0].StateVersionAfter);
        for (var index = 1; index < trace.Events.Length; index++)
        {
            Assert.Equal(trace.Events[index - 1].Sequence.Next(), trace.Events[index].Sequence);
            Assert.Equal(trace.Events[index - 1].StateVersionAfter.Next(), trace.Events[index].StateVersionAfter);
            Assert.True(trace.Events[index - 1].ServerTick <= trace.Events[index].ServerTick);
        }

        Assert.Equal((ServerTick.From(1), ServerTick.From(1)), (trace.Events[0].ServerTick, trace.Events[1].ServerTick));
        Assert.Equal((EventTypeId.From("HOST_LOG_TRANSITION"), EventTypeId.From("HOST_LOG_TRANSITION")), (trace.Events[0].EventType, trace.Events[1].EventType));
        Assert.Equal((1, 2), (trace.Events[0].Payload.ScriptOrder, trace.Events[1].Payload.ScriptOrder));
        Assert.Equal(IntentId.From("scenario_intent_01"), trace.Events[0].CausedByIntentId);
        Assert.Null(trace.Events[2].CausedByIntentId);
        Assert.Equal(ServerTick.From(2), trace.Events[2].ServerTick);
        Assert.True(trace.Events[2].ServerTick > trace.Events[1].ServerTick);
        Assert.Equal(EventSequence.From(trace.Journal.Count), trace.Journal.LastSequence);
        Assert.Equal(trace.Events[^1].StateVersionAfter, trace.FinalState.StateVersion);
        Assert.Equal(trace.Events[^1].Sequence, trace.FinalBoundary.LastEventSequence);
        Assert.Equal(trace.FinalState.StateVersion, trace.FinalBoundary.StateVersion);
        Assert.Equal(trace.Journal.LastTick, trace.FinalBoundary.ServerTick);
    }

    [Fact]
    public void Trace_composes_host_jam_repair_and_timed_procedure_mutations_only_through_commits()
    {
        var trace = RunBaseline();
        var types = trace.Events.Select(static item => item.EventType).ToArray();

        Assert.Contains(EventTypeId.From("HOST_LOG_TRANSITION"), types);
        Assert.Contains(EventTypeId.From("LINE_JAM_ENTERED"), types);
        Assert.Contains(EventTypeId.From("REPAIR_STARTED"), types);
        Assert.Contains(EventTypeId.From("REPAIR_COMPLETED"), types);
        Assert.Contains(EventTypeId.From("PROCEDURE_HOLD_STARTED"), types);
        Assert.Contains(EventTypeId.From("PROCEDURE_HOLD_COMPLETED"), types);
        Assert.Equal(2, trace.RejectedNoOpOperationCount);
        Assert.Equal(9, trace.Journal.Count);
    }

    [Fact]
    public void Timed_boundaries_produce_no_event_before_due_and_one_event_at_exact_due()
    {
        var trace = RunBaseline();
        var completion = Assert.Single(trace.Events.Where(static item => item.EventType == EventTypeId.From("PROCEDURE_HOLD_COMPLETED")));

        Assert.Equal(ServerTick.From(4), Assert.Single(trace.Events.Where(static item => item.EventType == EventTypeId.From("PROCEDURE_HOLD_STARTED"))).ServerTick);
        Assert.Equal(ServerTick.From(7), completion.ServerTick);
        Assert.DoesNotContain(trace.Events, static item => item.ServerTick == ServerTick.From(6));
        Assert.DoesNotContain(trace.Events, static item => item.ServerTick == ServerTick.From(8));
        Assert.Equal(2, trace.RejectedNoOpOperationCount);
    }

    [Fact]
    public void Initial_intermediate_and_final_checkpoints_validate_full_tail_and_empty_replay()
    {
        var trace = RunBaseline();
        var validator = new ReplayValidator();
        var full = trace.Journal.Events.ToArray();
        var tail = full.Skip(checked((int)trace.IntermediateBoundary.LastEventSequence.Value)).ToArray();

        Assert.Equal((ServerTick.Zero, StateVersion.Zero, EventSequence.None), (trace.InitialBoundary.ServerTick, trace.InitialBoundary.StateVersion, trace.InitialBoundary.LastEventSequence));
        Assert.Equal((ServerTick.From(1), StateVersion.From(2), EventSequence.From(2)), (trace.IntermediateBoundary.ServerTick, trace.IntermediateBoundary.StateVersion, trace.IntermediateBoundary.LastEventSequence));
        Assert.True(validator.Validate(trace.InitialBoundary, full).IsValid);
        Assert.True(validator.Validate(trace.IntermediateBoundary, tail).IsValid);
        Assert.True(validator.Validate(trace.FinalBoundary, Array.Empty<EventEnvelope>()).IsValid);
    }

    [Fact]
    public void Replay_tampering_is_detected_without_mutating_the_canonical_source_trace()
    {
        var trace = RunBaseline();
        var validator = new ReplayValidator();
        var source = trace.Journal.Events.ToArray();
        var duplicate = source.ToArray();
        var gap = source.ToArray();
        var tick = source.ToArray();
        var version = source.ToArray();
        duplicate[1] = duplicate[1] with { Sequence = EventSequence.First };
        gap[1] = gap[1] with { Sequence = EventSequence.From(3) };
        tick[2] = tick[2] with { ServerTick = ServerTick.Zero };
        version[2] = version[2] with { StateVersionAfter = StateVersion.From(4) };

        Assert.Equal(ReplayAnomaly.Duplicate, validator.Validate(trace.InitialBoundary, duplicate).Anomaly);
        Assert.Equal(ReplayAnomaly.SequenceGap, validator.Validate(trace.InitialBoundary, gap).Anomaly);
        Assert.Equal(ReplayAnomaly.TickRegression, validator.Validate(trace.InitialBoundary, tick).Anomaly);
        Assert.Equal(ReplayAnomaly.StateVersionSkip, validator.Validate(trace.InitialBoundary, version).Anomaly);
        Assert.Equal(source, trace.Journal.Events);
        Assert.Equal(source.Select(static item => item.EventId), trace.Events.Select(static item => item.EventId));
    }

    [Fact]
    public void One_input_repair_completion_tick_variant_is_valid_repeatable_and_detectably_different_after_its_unchanged_prefix()
    {
        var configuration = Fixture.LoadP0();
        var baselineScript = DeterministicScenarioScript.Baseline;
        var variantScript = baselineScript.WithRepairCompletionTick(ServerTick.From(10));
        var baseline = Driver.Run(configuration, baselineScript);
        var variant = Driver.Run(configuration, variantScript);
        var repeatedVariant = Driver.Run(configuration, variantScript);

        AssertTraceEqual(variant, repeatedVariant);
        Assert.True(new ReplayValidator().Validate(variant.InitialBoundary, variant.Journal.Events).IsValid);
        for (var index = 0; index < baseline.Events.Length - 1; index++)
        {
            Assert.Equal(baseline.Events[index], variant.Events[index]);
        }

        Assert.Equal(ServerTick.From(9), baseline.Events[^1].ServerTick);
        Assert.Equal(ServerTick.From(10), variant.Events[^1].ServerTick);
        Assert.False(baseline.FinalState.ValueEquals(variant.FinalState));
        Assert.Equal(ServerTick.From(9), baselineScript.Operations[^1].Tick);
        Assert.Equal(ServerTick.From(10), variantScript.Operations[^1].Tick);
        Assert.Equal(1, CountScriptDifferences(baselineScript, variantScript));
    }

    [Fact]
    public void Configuration_script_payloads_and_trace_collections_remain_stable_value_based_and_immutable()
    {
        var configuration = Fixture.LoadP0();
        var script = DeterministicScenarioScript.Baseline;
        var seed = configuration.Shift.Seed;
        var manifestCount = configuration.Shift.Manifest.Length;
        var scriptOperations = script.Operations.ToArray();
        var trace = Driver.Run(configuration, script);

        Assert.Equal(seed, configuration.Shift.Seed);
        Assert.Equal(manifestCount, configuration.Shift.Manifest.Length);
        Assert.Equal(scriptOperations.Select(static item => item.Tick), script.Operations.Select(static item => item.Tick));
        Assert.Equal(typeof(ImmutableArray<CanonicalScenarioEvent>), typeof(DeterministicScenarioTrace).GetProperty(nameof(DeterministicScenarioTrace.Events))!.PropertyType);
        Assert.All(trace.Events, item => Assert.Equal(item.Payload, new DeterministicScenarioPayload(item.Payload.ScriptOrder, item.Payload.Operation, item.Payload.Target)));
    }

    private static DeterministicScenarioTrace RunBaseline() => Driver.Run(Fixture.LoadP0(), DeterministicScenarioScript.Baseline);

    private static void AssertTraceEqual(DeterministicScenarioTrace expected, DeterministicScenarioTrace actual)
    {
        Assert.Equal(expected.FinalState.StateVersion, actual.FinalState.StateVersion);
        Assert.True(expected.FinalState.ValueEquals(actual.FinalState));
        Assert.Equal((expected.Journal.Count, expected.Journal.LastSequence, expected.Journal.LastTick, expected.Journal.LastStateVersion), (actual.Journal.Count, actual.Journal.LastSequence, actual.Journal.LastTick, actual.Journal.LastStateVersion));
        Assert.Equal(expected.InitialBoundary, actual.InitialBoundary);
        Assert.Equal(expected.IntermediateBoundary, actual.IntermediateBoundary);
        Assert.Equal(expected.FinalBoundary, actual.FinalBoundary);
        Assert.Equal(expected.AcceptedOperationCount, actual.AcceptedOperationCount);
        Assert.Equal(expected.RejectedNoOpOperationCount, actual.RejectedNoOpOperationCount);
        Assert.Equal(expected.Events.Length, actual.Events.Length);
        for (var index = 0; index < expected.Events.Length; index++)
        {
            Assert.Equal(expected.Events[index], actual.Events[index]);
        }
    }

    private static int CountScriptDifferences(DeterministicScenarioScript left, DeterministicScenarioScript right)
    {
        Assert.Equal(left.Operations.Length, right.Operations.Length);
        var differences = 0;
        for (var index = 0; index < left.Operations.Length; index++)
        {
            var first = left.Operations[index];
            var second = right.Operations[index];
            if (first.Kind != second.Kind || first.Tick != second.Tick || first.EventId != second.EventId || first.EventType != second.EventType || first.CausedByIntentId != second.CausedByIntentId || first.Payload != second.Payload)
            {
                differences++;
            }
        }

        return differences;
    }
}
