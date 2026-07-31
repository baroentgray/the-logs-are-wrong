using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Tests.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Line;

[Trait("Scope", "TLAW-024")]
public sealed class MovementNoiseRuntimeTests
{
    private static readonly MovementNoiseApplicationService Service = new();

    [Fact]
    public void Create_requires_an_initialized_shift_and_starts_inactive()
    {
        Assert.Throws<ArgumentException>(() => MovementNoiseRuntimeState.Create(default));

        var runtime = MovementNoiseRuntimeState.Create(Fixture.LoadP0().Shift.ShiftId);

        Assert.Equal(Fixture.LoadP0().Shift.ShiftId, runtime.ShiftId);
        Assert.False(runtime.HasAcceptedMovement);
        Assert.False(runtime.IsActiveAt(ServerTick.Zero));
    }

    [Fact]
    public void First_accepted_manual_movement_uses_the_configured_interval_with_inclusive_start_and_exclusive_due()
    {
        var accepted = ManualAccepted();
        var runtime = MovementNoiseRuntimeState.Create(accepted.State.ShiftId);
        var applied = AssertApplied(Service.Apply(runtime, accepted, ServerTick.From(10), Configuration()));

        Assert.Equal(MovementNoiseAcceptedSource.ManualLogIntent, applied.State.LastAcceptedMovement!.Source);
        Assert.Equal(LogId.From("log_01"), applied.State.LastAcceptedMovement!.LogId);
        Assert.Equal(LogState.AT_INTAKE, applied.State.LastAcceptedMovement!.SourceState);
        Assert.Equal(LogState.AT_PROCEDURE, applied.State.LastAcceptedMovement!.DestinationState);
        Assert.Equal((ServerTick.From(10), ServerTick.From(12)), (applied.State.StartedAt, applied.State.DueAt));
        Assert.True(applied.State.IsActiveAt(ServerTick.From(10)));
        Assert.True(applied.State.IsActiveAt(ServerTick.From(11)));
        Assert.False(applied.State.IsActiveAt(ServerTick.From(12)));
        Assert.False(applied.State.IsActiveAt(ServerTick.From(99)));
    }

    [Fact]
    public void Time_query_does_not_mutate_or_clear_the_runtime()
    {
        var applied = AssertApplied(Service.Apply(MovementNoiseRuntimeState.Create(Fixture.LoadP0().Shift.ShiftId), ManualAccepted(), ServerTick.From(10), Configuration()));
        var before = applied.State;

        _ = before.IsActiveAt(ServerTick.From(12));
        _ = before.IsActiveAt(ServerTick.From(100));

        Assert.Same(before, applied.State);
        Assert.True(before.HasAcceptedMovement);
        Assert.Equal(ServerTick.From(12), before.DueAt);
    }

    [Fact]
    public void Configuration_duration_is_consumed_not_hard_coded()
    {
        var accepted = ManualAccepted();
        var applied = AssertApplied(Service.Apply(MovementNoiseRuntimeState.Create(accepted.State.ShiftId), accepted, ServerTick.From(10), Configuration(5)));

        Assert.Equal(ServerTick.From(15), applied.State.DueAt);
        Assert.True(applied.State.IsActiveAt(ServerTick.From(14)));
        Assert.False(applied.State.IsActiveAt(ServerTick.From(15)));
    }

    [Fact]
    public void Every_closed_accepted_source_retains_its_exact_movement_evidence()
    {
        AssertSource(ManualAccepted(), MovementNoiseAcceptedSource.ManualLogIntent, ServerTick.From(10));
        AssertSource(HostAccepted(), MovementNoiseAcceptedSource.HostLogTransition, ServerTick.From(11));
        AssertSource(FeedDueAdmitted(), MovementNoiseAcceptedSource.FeedDueResolved, ServerTick.Zero);
        AssertSource(FeedDueAtGate(), MovementNoiseAcceptedSource.FeedDueResolved, ServerTick.From(12));
        AssertSource(DefaultAutoRouteAccepted(), MovementNoiseAcceptedSource.DefaultIntakeAutoRoute, ServerTick.From(20));
        AssertSource(RepairPendingTransitionAccepted(), MovementNoiseAcceptedSource.RepairPendingTransition, ServerTick.From(21));
        AssertSource(SawStarted(), MovementNoiseAcceptedSource.SawCycleStarted, ServerTick.From(30));
        AssertSource(SawCompleted(), MovementNoiseAcceptedSource.SawCycleCompleted, SawCompleted().CompletedAt);
    }

    [Fact]
    public void Each_source_rejects_independently_corrupted_evidence_before_runtime_mutation()
    {
        var manual = ManualAccepted();
        var manualRuntime = NewRuntime(manual.State);
        AssertRejectsWithoutRuntimeMutation(manualRuntime, candidate => Service.Apply(candidate, manual with { Transition = manual.Transition with { ToState = LogState.AT_INTAKE } }, ServerTick.From(10), Configuration()));

        var host = HostAccepted();
        var hostRuntime = NewRuntime(host.State);
        AssertRejectsWithoutRuntimeMutation(hostRuntime, candidate => Service.Apply(candidate, host with { Descriptor = host.Descriptor with { LogId = LogId.From("log_02") } }, ServerTick.From(11), Configuration()));

        var feed = FeedDueAtGate();
        var feedRuntime = NewRuntime(feed.State);
        AssertRejectsWithoutRuntimeMutation(feedRuntime, candidate => Service.Apply(candidate, new FeedDueResolved(feed.State, feed.ConsumedSchedule, ServerTick.From(11), feed.Disposition, feed.FollowUpRequirement, feed.PriorStateVersion, feed.CurrentStateVersion), Configuration()));

        var defaultRoute = DefaultAutoRouteAccepted();
        var defaultRuntime = NewRuntime(defaultRoute.State);
        AssertRejectsWithoutRuntimeMutation(defaultRuntime, candidate => Service.Apply(candidate, defaultRoute with { Destination = LogState.AT_INTAKE }, Configuration()));

        var repair = RepairPendingTransitionAccepted();
        var repairRuntime = NewRuntime(repair.State);
        var mismatchedPending = new PendingLineTransitionDescriptor(LogId.From("log_01"), LogState.AT_FEED_GATE, LogState.AT_INTAKE, JamCause.FEED_GATE_BLOCKED);
        AssertRejectsWithoutRuntimeMutation(repairRuntime, candidate => Service.Apply(candidate, new RepairPendingTransitionExecuted(repair.State, mismatchedPending, repair.AppliedAt, repair.PriorStateVersion, repair.CurrentStateVersion, repair.FollowUpRequirement), Configuration()));

        var started = SawStarted();
        var startedRuntime = NewRuntime(started.State);
        AssertRejectsWithoutRuntimeMutation(startedRuntime, candidate => Service.Apply(candidate, started with { CurrentStateVersion = started.PriorStateVersion }, Configuration()));

        var completed = SawCompleted();
        var completedRuntime = NewRuntime(completed.State);
        AssertRejectsWithoutRuntimeMutation(completedRuntime, candidate => Service.Apply(candidate, completed with { CompletedAt = completed.Cycle.StartedAt }, Configuration()));
    }

    [Fact]
    public void Same_tick_sequential_movements_are_ordered_by_version_and_extend_without_shortening()
    {
        var first = HostAccepted();
        var second = HostAcceptedFrom(first.State, "log_01", LogState.AT_PROCEDURE, LogState.AT_INTAKE);
        var initial = MovementNoiseRuntimeState.Create(first.State.ShiftId);
        var once = AssertApplied(Service.Apply(initial, first, ServerTick.From(10), Configuration(5))).State;
        var twice = AssertApplied(Service.Apply(once, second, ServerTick.From(10), Configuration(2))).State;

        Assert.Equal(ServerTick.From(10), twice.StartedAt);
        Assert.Equal(ServerTick.From(15), twice.DueAt);
        Assert.Equal(second.Descriptor.CurrentStateVersion, twice.LastAcceptedMovement!.CurrentStateVersion);
    }

    [Fact]
    public void Overlapping_later_movement_extends_and_shorter_candidate_never_shortens()
    {
        var first = HostAccepted();
        var second = HostAcceptedFrom(first.State, "log_01", LogState.AT_PROCEDURE, LogState.AT_INTAKE);
        var runtime = AssertApplied(Service.Apply(NewRuntime(first.State), first, ServerTick.From(10), Configuration(5))).State;

        var extended = AssertApplied(Service.Apply(runtime, second, ServerTick.From(13), Configuration(5))).State;
        Assert.Equal(ServerTick.From(18), extended.DueAt);

        var noShorten = AssertApplied(Service.Apply(extended, HostAcceptedFrom(second.State, "log_01", LogState.AT_INTAKE, LogState.AT_PROCEDURE), ServerTick.From(14), Configuration(2))).State;
        Assert.Equal(ServerTick.From(18), noShorten.DueAt);
    }

    [Fact]
    public void Movement_after_expiration_starts_a_new_window()
    {
        var first = HostAccepted();
        var second = HostAcceptedFrom(first.State, "log_01", LogState.AT_PROCEDURE, LogState.AT_INTAKE);
        var prior = AssertApplied(Service.Apply(NewRuntime(first.State), first, ServerTick.From(10), Configuration(2))).State;

        var restarted = AssertApplied(Service.Apply(prior, second, ServerTick.From(20), Configuration(3))).State;

        Assert.Equal((ServerTick.From(20), ServerTick.From(23)), (restarted.StartedAt, restarted.DueAt));
    }

    [Fact]
    public void Exact_duplicate_is_a_typed_same_instance_no_op()
    {
        var accepted = HostAccepted();
        var first = AssertApplied(Service.Apply(NewRuntime(accepted.State), accepted, ServerTick.From(10), Configuration()));

        var duplicate = Assert.IsType<MovementNoiseAlreadyApplied>(Service.Apply(first.State, accepted, ServerTick.From(10), Configuration()));
        Assert.Same(first.State, duplicate.State);
        Assert.Equal(first.State.DueAt, duplicate.State.DueAt);

    }

    [Fact]
    public void Same_version_changed_authoritative_tick_fails_closed_without_mutating_the_active_runtime()
    {
        var accepted = HostAccepted();
        var active = AssertApplied(Service.Apply(NewRuntime(accepted.State), accepted, ServerTick.From(10), Configuration())).State;

        AssertRejectsWithoutRuntimeMutation(active, candidate => Service.Apply(candidate, accepted, ServerTick.From(11), Configuration()));
    }

    [Fact]
    public void Same_version_changed_log_identity_fails_closed_without_mutating_the_active_runtime()
    {
        var accepted = HostAccepted();
        var active = AssertApplied(Service.Apply(NewRuntime(accepted.State), accepted, ServerTick.From(10), Configuration())).State;
        var changedLog = accepted with { Descriptor = accepted.Descriptor with { LogId = LogId.From("log_02") } };

        AssertRejectsWithoutRuntimeMutation(active, candidate => Service.Apply(candidate, changedLog, ServerTick.From(10), Configuration()));
    }

    [Fact]
    public void Stale_cross_shift_default_version_and_invalid_duration_evidence_fail_without_mutating_the_input_runtime()
    {
        var accepted = HostAccepted();
        var first = AssertApplied(Service.Apply(NewRuntime(accepted.State), accepted, ServerTick.From(10), Configuration())).State;
        var newerAccepted = HostAcceptedFrom(accepted.State, "log_01", LogState.AT_PROCEDURE, LogState.AT_INTAKE);
        var newer = AssertApplied(Service.Apply(first, newerAccepted, ServerTick.From(11), Configuration())).State;
        var defaultVersion = accepted with { Descriptor = accepted.Descriptor with { PriorStateVersion = default } };

        AssertRejectsWithoutRuntimeMutation(newer, candidate => Service.Apply(candidate, accepted, ServerTick.From(10), Configuration()));
        AssertRejectsWithoutRuntimeMutation(first, candidate => Service.Apply(candidate, defaultVersion, ServerTick.From(11), Configuration()));
        AssertRejectsWithoutRuntimeMutation(first, candidate => Service.Apply(candidate, accepted, ServerTick.From(11), Configuration(0)));

        var otherShift = MovementNoiseRuntimeState.Create(ShiftId.From("other_shift"));
        AssertRejectsWithoutRuntimeMutation(otherShift, candidate => Service.Apply(candidate, accepted, ServerTick.From(10), Configuration()));
    }

    [Fact]
    public void Independent_value_equivalent_sequences_produce_value_equivalent_runtime()
    {
        var firstAccepted = HostAccepted();
        var firstNext = HostAcceptedFrom(firstAccepted.State, "log_01", LogState.AT_PROCEDURE, LogState.AT_INTAKE);
        var secondAccepted = HostAccepted();
        var secondNext = HostAcceptedFrom(secondAccepted.State, "log_01", LogState.AT_PROCEDURE, LogState.AT_INTAKE);

        var first = AssertApplied(Service.Apply(AssertApplied(Service.Apply(NewRuntime(firstAccepted.State), firstAccepted, ServerTick.From(10), Configuration())).State, firstNext, ServerTick.From(11), Configuration())).State;
        var second = AssertApplied(Service.Apply(AssertApplied(Service.Apply(NewRuntime(secondAccepted.State), secondAccepted, ServerTick.From(10), Configuration())).State, secondNext, ServerTick.From(11), Configuration())).State;

        Assert.True(first.ValueEquals(second));
    }

    private static void AssertSource(ManualLogIntentAccepted accepted, MovementNoiseAcceptedSource source, ServerTick tick)
    {
        var applied = AssertApplied(Service.Apply(NewRuntime(accepted.State), accepted, tick, Configuration()));
        AssertRetainedMovement(applied.State, source, accepted.Transition.LogId, accepted.Transition.FromState, accepted.Transition.ToState, accepted.Transition.PriorStateVersion, accepted.Transition.CurrentStateVersion, tick);
    }

    private static void AssertSource(HostLogTransitionAccepted accepted, MovementNoiseAcceptedSource source, ServerTick tick)
    {
        var applied = AssertApplied(Service.Apply(NewRuntime(accepted.State), accepted, tick, Configuration()));
        AssertRetainedMovement(applied.State, source, accepted.Descriptor.LogId, accepted.Descriptor.FromState, accepted.Descriptor.ToState, accepted.Descriptor.PriorStateVersion, accepted.Descriptor.CurrentStateVersion, tick);
    }

    private static void AssertSource(FeedDueResolved accepted, MovementNoiseAcceptedSource source, ServerTick tick)
    {
        var applied = AssertApplied(Service.Apply(NewRuntime(accepted.State), accepted, Configuration()));
        var destination = accepted.Disposition == FeedDueDisposition.AdmittedToIntake ? LogState.AT_INTAKE : LogState.AT_FEED_GATE;
        Assert.Equal(accepted.ResolvedAt, tick);
        AssertRetainedMovement(applied.State, source, accepted.ConsumedSchedule.LogId, LogState.SCHEDULED, destination, accepted.PriorStateVersion, accepted.CurrentStateVersion, accepted.ResolvedAt);
    }

    private static void AssertSource(DefaultIntakeAutoRouteApplied accepted, MovementNoiseAcceptedSource source, ServerTick tick)
    {
        var applied = AssertApplied(Service.Apply(NewRuntime(accepted.State), accepted, Configuration()));
        AssertRetainedMovement(applied.State, source, accepted.LogId, accepted.Source, accepted.Destination, accepted.PriorStateVersion, accepted.CurrentStateVersion, tick);
    }

    private static void AssertSource(RepairPendingTransitionExecuted accepted, MovementNoiseAcceptedSource source, ServerTick tick)
    {
        var applied = AssertApplied(Service.Apply(NewRuntime(accepted.State), accepted, Configuration()));
        AssertRetainedMovement(applied.State, source, accepted.LogId, accepted.Source, accepted.Destination, accepted.PriorStateVersion, accepted.CurrentStateVersion, tick);
    }

    private static void AssertSource(SawCycleStarted accepted, MovementNoiseAcceptedSource source, ServerTick tick)
    {
        var applied = AssertApplied(Service.Apply(NewRuntime(accepted.State), accepted, Configuration()));
        AssertRetainedMovement(applied.State, source, accepted.Cycle.LogId, LogState.QUEUED_FOR_SAW, LogState.IN_SAW, accepted.PriorStateVersion, accepted.CurrentStateVersion, tick);
    }

    private static void AssertSource(SawCycleCompleted accepted, MovementNoiseAcceptedSource source, ServerTick tick)
    {
        var applied = AssertApplied(Service.Apply(NewRuntime(accepted.State), accepted, Configuration()));
        AssertRetainedMovement(applied.State, source, accepted.Cycle.LogId, LogState.IN_SAW, LogState.PROCESSED, accepted.PriorStateVersion, accepted.CurrentStateVersion, tick);
    }

    private static void AssertRetainedMovement(
        MovementNoiseRuntimeState runtime,
        MovementNoiseAcceptedSource source,
        LogId logId,
        LogState sourceState,
        LogState destinationState,
        StateVersion priorStateVersion,
        StateVersion currentStateVersion,
        ServerTick acceptedAt)
    {
        var retained = Assert.IsType<MovementNoiseAcceptedMovement>(runtime.LastAcceptedMovement);
        Assert.Equal((source, logId, sourceState, destinationState, priorStateVersion, currentStateVersion, acceptedAt), (retained.Source, retained.LogId, retained.SourceState, retained.DestinationState, retained.PriorStateVersion, retained.CurrentStateVersion, retained.AcceptedAt));
    }

    private static MovementNoiseApplied AssertApplied(MovementNoiseApplicationResult result) => Assert.IsType<MovementNoiseApplied>(result);

    private static MovementNoiseRuntimeState NewRuntime(ShiftRuntimeState state) => MovementNoiseRuntimeState.Create(state.ShiftId);

    private static SchedulerConfiguration Configuration(int duration = 2) => Fixture.LoadP0().Shift.Scheduler with { MovementNoiseSeconds = duration };

    private static ManualLogIntentAccepted ManualAccepted()
    {
        var before = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var after = RuntimeFixture.MoveHost(before, "log_01", LogState.AT_PROCEDURE);
        return new ManualLogIntentAccepted(after, new LogTransitionDescriptor(LogId.From("log_01"), LogState.AT_INTAKE, LogState.AT_PROCEDURE, before.StateVersion, after.StateVersion, RuntimeFixture.BoundActor));
    }

    private static HostLogTransitionAccepted HostAccepted()
    {
        var before = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        return Assert.IsType<HostLogTransitionAccepted>(new HostLogTransitionService().Apply(before, LogId.From("log_01"), LogState.AT_PROCEDURE));
    }

    private static HostLogTransitionAccepted HostAcceptedFrom(ShiftRuntimeState before, string id, LogState source, LogState destination)
    {
        Assert.True(before.TryGetLog(LogId.From(id), out var owner));
        Assert.Equal(source, owner.State);
        return Assert.IsType<HostLogTransitionAccepted>(new HostLogTransitionService().Apply(before, LogId.From(id), destination));
    }

    private static FeedDueResolved FeedDueAdmitted()
    {
        var fixture = Fixture.LoadP0();
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(RuntimeFixture.CreateInitialState(), ServerTick.Zero, fixture.Shift.Scheduler));
        return Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
    }

    private static FeedDueResolved FeedDueAtGate()
    {
        var fixture = Fixture.LoadP0();
        var occupied = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var intent = new TheLogsAreWrong.Domain.Intents.IntentEnvelope(occupied.ShiftId, TheLogsAreWrong.Domain.Identifiers.IntentId.From("noise_early"), TheLogsAreWrong.Domain.Identifiers.ActorId.From("hint"), FeedPlanningTargets.FeedGate, FeedPlanningIntentActions.RequestEarlyFeed, occupied.StateVersion, ServerTick.Zero, TheLogsAreWrong.Domain.Intents.NoIntentParameters.Instance);
        var planned = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(occupied, intent, RuntimeFixture.BoundActor, ServerTick.From(10), fixture.Shift.Scheduler));
        return Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.From(12)));
    }

    private static DefaultIntakeAutoRouteApplied DefaultAutoRouteAccepted()
    {
        var before = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var after = RuntimeFixture.MoveHost(before, "log_01", LogState.QUEUED_FOR_SAW);
        return new DefaultIntakeAutoRouteApplied(after, LogId.From("log_01"), ServerTick.From(20), LogState.AT_INTAKE, LogState.QUEUED_FOR_SAW, before.StateVersion, after.StateVersion);
    }

    private static RepairPendingTransitionExecuted RepairPendingTransitionAccepted()
    {
        var ownerAtProcedure = RuntimeFixture.MoveHost(RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01"), "log_01", LogState.AT_PROCEDURE);
        var before = RuntimeFixture.MoveHost(ownerAtProcedure, "log_02", LogState.AT_FEED_GATE);
        var after = RuntimeFixture.MoveHost(before, "log_02", LogState.AT_INTAKE);
        var pending = new PendingLineTransitionDescriptor(LogId.From("log_02"), LogState.AT_FEED_GATE, LogState.AT_INTAKE, JamCause.FEED_GATE_BLOCKED);
        return new RepairPendingTransitionExecuted(after, pending, ServerTick.From(21), before.StateVersion, after.StateVersion, RepairPendingTransitionFollowUp.IntakeDeadlineStartRequired);
    }

    private static SawCycleStarted SawStarted()
    {
        var state = RuntimeFixture.MoveHost(RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01"), "log_01", LogState.QUEUED_FOR_SAW);
        return Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(state, ServerTick.From(30), Configuration()));
    }

    private static SawCycleCompleted SawCompleted()
    {
        var started = SawStarted();
        return Assert.IsType<SawCycleCompleted>(new SawCycleCompletionService().Complete(started.State, started.Cycle.DueAt, Fixture.LoadP0().Anomalies));
    }

    private static void AssertRejectsWithoutRuntimeMutation(
        MovementNoiseRuntimeState runtime,
        Func<MovementNoiseRuntimeState, MovementNoiseApplicationResult> apply)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(apply);
        var shiftId = runtime.ShiftId;
        var hasAcceptedMovement = runtime.HasAcceptedMovement;
        var lastAcceptedMovement = runtime.LastAcceptedMovement;
        var startedAt = runtime.StartedAt;
        var dueAt = runtime.DueAt;

        Assert.ThrowsAny<ArgumentException>(() => apply(runtime));

        Assert.Equal(shiftId, runtime.ShiftId);
        Assert.Equal(hasAcceptedMovement, runtime.HasAcceptedMovement);
        Assert.Equal(startedAt, runtime.StartedAt);
        Assert.Equal(dueAt, runtime.DueAt);
        Assert.Equal(lastAcceptedMovement, runtime.LastAcceptedMovement);
        if (lastAcceptedMovement is null)
        {
            Assert.False(runtime.HasAcceptedMovement);
            Assert.Null(runtime.LastAcceptedMovement);
        }
        else
        {
            Assert.True(runtime.HasAcceptedMovement);
            Assert.Same(lastAcceptedMovement, runtime.LastAcceptedMovement);
        }
    }
}
