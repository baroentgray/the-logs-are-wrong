using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Logs;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Tests.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Logs;

[Trait("Scope", "TLAW-003")]
public sealed class LogTransitionPolicyTests
{
    [Fact]
    public void Every_log_state_maps_to_its_single_frozen_node_or_no_active_node()
    {
        Assert.Equal(NodeId.SUPPLY_QUEUE, LogStateNodes.GetNode(LogState.SCHEDULED));
        Assert.Equal(NodeId.FEED_GATE, LogStateNodes.GetNode(LogState.AT_FEED_GATE));
        Assert.Equal(NodeId.INTAKE, LogStateNodes.GetNode(LogState.AT_INTAKE));
        Assert.Equal(NodeId.PROCEDURE, LogStateNodes.GetNode(LogState.AT_PROCEDURE));
        Assert.Equal(NodeId.SAW_QUEUE, LogStateNodes.GetNode(LogState.QUEUED_FOR_SAW));
        Assert.Equal(NodeId.SAW, LogStateNodes.GetNode(LogState.IN_SAW));
        Assert.Equal(NodeId.CONTAINMENT, LogStateNodes.GetNode(LogState.HELD_WRITTEN_OFF));
        Assert.Null(LogStateNodes.GetNode(LogState.PROCESSED));
    }

    public static TheoryData<LogState, LogState> LegalTransitions => new()
    {
        { LogState.SCHEDULED, LogState.AT_FEED_GATE },
        { LogState.AT_FEED_GATE, LogState.AT_INTAKE },
        { LogState.AT_INTAKE, LogState.AT_PROCEDURE },
        { LogState.AT_PROCEDURE, LogState.AT_INTAKE },
        { LogState.AT_INTAKE, LogState.QUEUED_FOR_SAW },
        { LogState.QUEUED_FOR_SAW, LogState.IN_SAW },
        { LogState.IN_SAW, LogState.PROCESSED },
        { LogState.AT_INTAKE, LogState.HELD_WRITTEN_OFF },
        { LogState.AT_PROCEDURE, LogState.HELD_WRITTEN_OFF }
    };

    [Theory]
    [MemberData(nameof(LegalTransitions))]
    public void Every_frozen_legal_edge_is_accepted_by_the_shared_host_transition_seam(LogState from, LogState to)
    {
        var state = StateWithFirstLogAt(from);
        var result = new HostLogTransitionService().Apply(state, LogId.From("log_01"), to);

        var accepted = Assert.IsType<HostLogTransitionAccepted>(result);
        Assert.Equal(LogId.From("log_01"), accepted.Descriptor.LogId);
        Assert.Equal(from, accepted.Descriptor.FromState);
        Assert.Equal(to, accepted.Descriptor.ToState);
        Assert.Equal(state.StateVersion, accepted.Descriptor.PriorStateVersion);
        Assert.Equal(state.StateVersion.Next(), accepted.Descriptor.CurrentStateVersion);
        Assert.Equal(to, accepted.State.Logs[0].State);
    }

    [Fact]
    public void All_non_edges_fail_closed_without_mutating_the_original_state()
    {
        var host = new HostLogTransitionService();

        foreach (var from in Enum.GetValues<LogState>())
        {
            foreach (var to in Enum.GetValues<LogState>())
            {
                if (LogTransitionPolicy.IsAllowed(from, to))
                {
                    continue;
                }

                var state = StateWithFirstLogAt(from);
                var result = host.Apply(state, LogId.From("log_01"), to);

                var rejected = Assert.IsType<HostLogTransitionRejected>(result);
                Assert.Equal(HostLogTransitionFailure.IllegalTransition, rejected.Failure);
                Assert.Same(state, rejected.State);
                Assert.Equal(from, state.Logs[0].State);
            }
        }
    }

    [Fact]
    public void Terminal_states_are_irreversible_and_saw_queue_is_a_point_of_no_return()
    {
        var host = new HostLogTransitionService();
        var processed = StateWithFirstLogAt(LogState.PROCESSED);
        var writtenOff = StateWithFirstLogAt(LogState.HELD_WRITTEN_OFF);
        var queuedForSaw = StateWithFirstLogAt(LogState.QUEUED_FOR_SAW);

        Assert.IsType<HostLogTransitionRejected>(host.Apply(processed, LogId.From("log_01"), LogState.AT_INTAKE));
        Assert.IsType<HostLogTransitionRejected>(host.Apply(writtenOff, LogId.From("log_01"), LogState.AT_INTAKE));
        Assert.IsType<HostLogTransitionRejected>(host.Apply(queuedForSaw, LogId.From("log_01"), LogState.AT_PROCEDURE));
        Assert.IsType<HostLogTransitionRejected>(host.Apply(queuedForSaw, LogId.From("log_01"), LogState.AT_INTAKE));
        Assert.IsType<HostLogTransitionRejected>(host.Apply(queuedForSaw, LogId.From("log_01"), LogState.HELD_WRITTEN_OFF));
        Assert.IsType<HostLogTransitionAccepted>(host.Apply(queuedForSaw, LogId.From("log_01"), LogState.IN_SAW));
    }

    [Fact]
    public void Occupancy_is_derived_from_states_and_each_limited_p0_node_rejects_when_full()
    {
        var host = new HostLogTransitionService();

        var feed = RuntimeFixture.CreateInitialState();
        feed = RuntimeFixture.MoveHost(feed, "log_01", LogState.AT_FEED_GATE);
        AssertOccupied(host, feed, "log_02", LogState.AT_FEED_GATE, NodeId.FEED_GATE);

        var intake = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        intake = RuntimeFixture.MoveHost(intake, "log_02", LogState.AT_FEED_GATE);
        AssertOccupied(host, intake, "log_02", LogState.AT_INTAKE, NodeId.INTAKE);

        var procedure = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        procedure = RuntimeFixture.MoveHost(procedure, "log_01", LogState.AT_PROCEDURE);
        procedure = RuntimeFixture.MoveToIntake(procedure, "log_02");
        AssertOccupied(host, procedure, "log_02", LogState.AT_PROCEDURE, NodeId.PROCEDURE);

        var sawQueue = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        sawQueue = RuntimeFixture.MoveHost(sawQueue, "log_01", LogState.QUEUED_FOR_SAW);
        sawQueue = RuntimeFixture.MoveToIntake(sawQueue, "log_02");
        AssertOccupied(host, sawQueue, "log_02", LogState.QUEUED_FOR_SAW, NodeId.SAW_QUEUE);

        var saw = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        saw = RuntimeFixture.MoveHost(saw, "log_01", LogState.QUEUED_FOR_SAW);
        saw = RuntimeFixture.MoveHost(saw, "log_01", LogState.IN_SAW);
        saw = RuntimeFixture.MoveToIntake(saw, "log_02");
        saw = RuntimeFixture.MoveHost(saw, "log_02", LogState.QUEUED_FOR_SAW);
        AssertOccupied(host, saw, "log_02", LogState.IN_SAW, NodeId.SAW);
    }

    [Fact]
    public void Configured_capacity_and_unlimited_containment_are_respected_without_a_second_occupancy_store()
    {
        var shift = Fixture.LoadP0().Shift;
        var capacities = shift.Scheduler.Capacities.SetItem(NodeId.INTAKE, NodeCapacity.Limited(2));
        var configured = shift with { Scheduler = shift.Scheduler with { Capacities = capacities } };
        var host = new HostLogTransitionService();
        var state = ShiftRuntimeState.Create(configured);

        state = RuntimeFixture.MoveHost(state, "log_01", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_01", LogState.AT_INTAKE);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.AT_INTAKE);
        Assert.Equal(2, state.GetNodeOccupancy(NodeId.INTAKE));

        state = RuntimeFixture.MoveHost(state, "log_01", LogState.HELD_WRITTEN_OFF);
        state = RuntimeFixture.MoveHost(state, "log_02", LogState.HELD_WRITTEN_OFF);
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_FEED_GATE);
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.AT_INTAKE);
        state = RuntimeFixture.MoveHost(state, "log_03", LogState.HELD_WRITTEN_OFF);

        Assert.Equal(3, state.GetNodeOccupancy(NodeId.CONTAINMENT));
        Assert.Equal(9, state.GetNodeOccupancy(NodeId.SUPPLY_QUEUE));
        Assert.Equal(0, state.GetNodeOccupancy(NodeId.INTAKE));
    }

    private static void AssertOccupied(HostLogTransitionService host, ShiftRuntimeState state, string logId, LogState to, NodeId node)
    {
        var result = host.Apply(state, LogId.From(logId), to);
        var rejected = Assert.IsType<HostLogTransitionRejected>(result);
        Assert.Equal(HostLogTransitionFailure.TargetOccupied, rejected.Failure);
        Assert.Equal(1, state.GetNodeOccupancy(node));
        Assert.Same(state, rejected.State);
    }

    private static ShiftRuntimeState StateWithFirstLogAt(LogState state)
    {
        var runtime = RuntimeFixture.CreateInitialState();
        return state switch
        {
            LogState.SCHEDULED => runtime,
            LogState.AT_FEED_GATE => RuntimeFixture.MoveHost(runtime, "log_01", LogState.AT_FEED_GATE),
            LogState.AT_INTAKE => RuntimeFixture.MoveToIntake(runtime, "log_01"),
            LogState.AT_PROCEDURE => RuntimeFixture.MoveHost(RuntimeFixture.MoveToIntake(runtime, "log_01"), "log_01", LogState.AT_PROCEDURE),
            LogState.QUEUED_FOR_SAW => RuntimeFixture.MoveHost(RuntimeFixture.MoveToIntake(runtime, "log_01"), "log_01", LogState.QUEUED_FOR_SAW),
            LogState.IN_SAW => RuntimeFixture.MoveHost(RuntimeFixture.MoveHost(RuntimeFixture.MoveToIntake(runtime, "log_01"), "log_01", LogState.QUEUED_FOR_SAW), "log_01", LogState.IN_SAW),
            LogState.PROCESSED => RuntimeFixture.MoveHost(RuntimeFixture.MoveHost(RuntimeFixture.MoveHost(RuntimeFixture.MoveToIntake(runtime, "log_01"), "log_01", LogState.QUEUED_FOR_SAW), "log_01", LogState.IN_SAW), "log_01", LogState.PROCESSED),
            LogState.HELD_WRITTEN_OFF => RuntimeFixture.MoveHost(RuntimeFixture.MoveToIntake(runtime, "log_01"), "log_01", LogState.HELD_WRITTEN_OFF),
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
    }
}
