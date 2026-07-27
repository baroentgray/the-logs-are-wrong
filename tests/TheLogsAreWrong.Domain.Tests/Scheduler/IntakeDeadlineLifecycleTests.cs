using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Tests.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Scheduler;

[Trait("Scope", "TLAW-016")]
public sealed class IntakeDeadlineLifecycleTests
{
    [Fact]
    public void Initial_admission_starts_exact_learning_deadline_and_matching_retry_is_an_exact_no_op()
    {
        var admission = AdmitInitial();
        var profile = Profile("learning");
        var started = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, profile));

        Assert.Equal((LogId.From("log_01"), ServerTick.Zero, 60L, ServerTick.From(60)), (started.Deadline.LogId, started.Deadline.StartedAt, started.Deadline.Duration.Value, started.Deadline.DueAt));
        Assert.Equal(admission.State.StateVersion.Next(), started.State.StateVersion);
        Assert.True(admission.State.PendingFeed is null && started.State.PendingFeed is null);

        var retry = Assert.IsType<IntakeDeadlineAlreadyActive>(new IntakeDeadlineStartService().Start(started.State, admission, profile));
        Assert.Same(started.State, retry.State);
        Assert.Equal(started.Deadline, retry.Deadline);
    }

    [Fact]
    public void Normal_and_early_admissions_derive_owner_and_duration_only_from_descriptor_and_selected_profile()
    {
        var normalBase = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        normalBase = RuntimeFixture.MoveHost(normalBase, "log_01", LogState.QUEUED_FOR_SAW);
        var normalPlan = Assert.IsType<NormalFeedScheduled>(new NormalFeedPlanningService().Plan(normalBase, ServerTick.From(10), Fixture.LoadP0().Shift.Scheduler));
        var normalAdmission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(normalPlan.State, ServerTick.From(15)));
        var normal = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(normalAdmission.State, normalAdmission, Profile("pressure")));
        Assert.Equal((LogId.From("log_02"), ServerTick.From(15), 45L, ServerTick.From(60)), (normal.Deadline.LogId, normal.Deadline.StartedAt, normal.Deadline.Duration.Value, normal.Deadline.DueAt));

        var earlyBase = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        earlyBase = RuntimeFixture.MoveHost(earlyBase, "log_01", LogState.QUEUED_FOR_SAW);
        var earlyIntent = new IntentEnvelope(earlyBase.ShiftId, IntentId.From("early_deadline"), ActorId.From("hint"), FeedPlanningTargets.FeedGate, FeedPlanningIntentActions.RequestEarlyFeed, earlyBase.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var earlyPlan = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(earlyBase, earlyIntent, RuntimeFixture.BoundActor, ServerTick.From(20), Fixture.LoadP0().Shift.Scheduler));
        var earlyAdmission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(earlyPlan.State, ServerTick.From(22)));
        var early = Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(earlyAdmission.State, earlyAdmission, Profile("learning")));
        Assert.Equal((LogId.From("log_02"), ServerTick.From(22), ServerTick.From(82)), (early.Deadline.LogId, early.Deadline.StartedAt, early.Deadline.DueAt));
    }

    [Fact]
    public void Null_or_invalid_profile_and_non_exact_admission_state_fail_loudly()
    {
        var admission = AdmitInitial();
        var service = new IntakeDeadlineStartService();
        Assert.Throws<ArgumentNullException>(() => service.Start(admission.State, admission, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Start(admission.State, admission, new ShiftProfile(0, 1)));
        Assert.Throws<ArgumentException>(() => service.Start(RuntimeFixture.MoveHost(admission.State, "log_02", LogState.AT_FEED_GATE), admission, Profile("learning")));
    }

    [Fact]
    public void Accepted_routes_away_from_owner_clear_deadline_in_the_same_version_step_and_unrelated_routes_preserve_it()
    {
        var started = StartInitial();
        var unrelated = RuntimeFixture.MoveHost(started.State, "log_02", LogState.AT_FEED_GATE);
        Assert.Equal(started.Deadline, unrelated.ActiveIntakeDeadline);

        var procedure = Assert.IsType<HostLogTransitionAccepted>(new HostLogTransitionService().Apply(unrelated, LogId.From("log_01"), LogState.AT_PROCEDURE));
        Assert.Null(procedure.State.ActiveIntakeDeadline);
        Assert.Equal(unrelated.StateVersion.Next(), procedure.State.StateVersion);

        var saw = Assert.IsType<HostLogTransitionAccepted>(new HostLogTransitionService().Apply(StartInitial().State, LogId.From("log_01"), LogState.QUEUED_FOR_SAW));
        Assert.Null(saw.State.ActiveIntakeDeadline);
        var writeOff = Assert.IsType<HostLogTransitionAccepted>(new HostLogTransitionService().Apply(StartInitial().State, LogId.From("log_01"), LogState.HELD_WRITTEN_OFF));
        Assert.Null(writeOff.State.ActiveIntakeDeadline);
    }

    [Fact]
    public void Expiration_is_exact_due_or_catch_up_once_and_leaves_owner_at_intake()
    {
        var started = StartInitial();
        var expiration = new IntakeDeadlineExpirationService();
        var beforeDue = Assert.IsType<IntakeDeadlineNotDueYet>(expiration.Expire(started.State, ServerTick.From(59)));
        Assert.Same(started.State, beforeDue.State);

        var expired = Assert.IsType<IntakeDeadlineExpired>(expiration.Expire(started.State, ServerTick.From(60)));
        Assert.Equal((started.Deadline, ServerTick.From(60), started.State.StateVersion, started.State.StateVersion.Next()), (expired.ExpiredDeadline, expired.ExpiredAt, expired.PriorStateVersion, expired.CurrentStateVersion));
        Assert.Null(expired.State.ActiveIntakeDeadline);
        Assert.Equal(LogState.AT_INTAKE, Log(expired.State, "log_01").State);
        Assert.Equal(new DefaultAutoRouteRequired(LogId.From("log_01"), ServerTick.From(60)), expired.FollowUp);

        var repeated = Assert.IsType<IntakeDeadlineNoActiveDeadline>(expiration.Expire(expired.State, ServerTick.From(61)));
        Assert.Same(expired.State, repeated.State);
    }

    [Fact]
    public void Same_tick_route_before_expiration_observes_no_active_deadline_and_value_equality_observes_deadline()
    {
        var first = StartInitial();
        var second = StartInitial();
        Assert.True(first.State.ValueEquals(second.State));
        Assert.False(AdmitInitial().State.ValueEquals(first.State));

        var routed = Assert.IsType<HostLogTransitionAccepted>(new HostLogTransitionService().Apply(first.State, LogId.From("log_01"), LogState.QUEUED_FOR_SAW));
        var noActive = Assert.IsType<IntakeDeadlineNoActiveDeadline>(new IntakeDeadlineExpirationService().Expire(routed.State, ServerTick.From(60)));
        Assert.Same(routed.State, noActive.State);
    }

    [Fact]
    public void Accepted_start_and_expiration_commit_once_through_tlaw011_and_no_ops_do_not_append()
    {
        var commit = new JournaledMutationCommitService();
        var initial = RuntimeFixture.CreateInitialState();
        var journal = new InMemoryEventJournal(initial.ShiftId);
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        Commit(commit, journal, initial, planned.State, ServerTick.Zero, "deadline_plan");
        var admission = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
        Commit(commit, journal, planned.State, admission.State, ServerTick.Zero, "deadline_admission");
        var started = Start(admission);

        var startCommit = Assert.IsType<JournaledMutationCommitted>(commit.Commit(journal, admission.State, started.State, admission.ResolvedAt, Draft("deadline_start")));
        Assert.Same(admission.State, startCommit.Before);
        Assert.Same(started.State, startCommit.After);
        Assert.Equal((3, started.State.StateVersion, admission.ResolvedAt), (journal.Events.Count, journal.LastStateVersion, journal.LastTick));
        Assert.Equal((admission.ResolvedAt, started.State.StateVersion), (startCommit.Envelope.ServerTick, startCommit.Envelope.StateVersionAfter));

        var snapshot = (journal.Events.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion);
        var retry = Assert.IsType<IntakeDeadlineAlreadyActive>(new IntakeDeadlineStartService().Start(started.State, admission, Profile("learning")));
        var rejected = Assert.IsType<JournaledMutationCommitRejected>(commit.Commit(journal, retry.State, retry.State, ServerTick.Zero, Draft("deadline_retry")));
        Assert.Equal(JournaledMutationCommitRejectionReason.StateVersionNotNext, rejected.Reason);
        Assert.Equal(snapshot, (journal.Events.Count, journal.LastSequence, journal.LastTick, journal.LastStateVersion));

        var expiration = Assert.IsType<IntakeDeadlineExpired>(new IntakeDeadlineExpirationService().Expire(started.State, ServerTick.From(60)));
        var expirationCommit = Assert.IsType<JournaledMutationCommitted>(commit.Commit(journal, started.State, expiration.State, ServerTick.From(60), Draft("deadline_expiration")));
        Assert.Same(started.State, expirationCommit.Before);
        Assert.Same(expiration.State, expirationCommit.After);
        Assert.Equal((4, expiration.State.StateVersion, ServerTick.From(60)), (journal.Events.Count, journal.LastStateVersion, journal.LastTick));
        Assert.Equal(LogState.AT_INTAKE, Log(expiration.State, "log_01").State);
        Assert.Null(expiration.State.ActiveIntakeDeadline);

        Assert.Same(started.State, Assert.IsType<IntakeDeadlineNotDueYet>(new IntakeDeadlineExpirationService().Expire(started.State, ServerTick.From(59))).State);
        Assert.Same(expiration.State, Assert.IsType<IntakeDeadlineNoActiveDeadline>(new IntakeDeadlineExpirationService().Expire(expiration.State, ServerTick.From(61))).State);
    }

    [Fact]
    public void Start_rejects_feed_gate_descriptor_null_inputs_different_profile_and_non_exact_state()
    {
        var admission = AdmitInitial();
        var service = new IntakeDeadlineStartService();
        Assert.Throws<ArgumentNullException>(() => service.Start(null!, admission, Profile("learning")));
        Assert.Throws<ArgumentNullException>(() => service.Start(admission.State, null!, Profile("learning")));
        var started = Start(admission);
        Assert.Throws<InvalidOperationException>(() => service.Start(started.State, admission, Profile("pressure")));

        var intake = RuntimeFixture.MoveToIntake(RuntimeFixture.CreateInitialState(), "log_01");
        var intent = new IntentEnvelope(intake.ShiftId, IntentId.From("gate_descriptor"), ActorId.From("hint"), FeedPlanningTargets.FeedGate, FeedPlanningIntentActions.RequestEarlyFeed, intake.StateVersion, ServerTick.Zero, NoIntentParameters.Instance);
        var plan = Assert.IsType<EarlyFeedScheduled>(new EarlyFeedIntentHandler().Handle(intake, intent, RuntimeFixture.BoundActor, ServerTick.From(10), Fixture.LoadP0().Shift.Scheduler));
        var gate = Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(plan.State, ServerTick.From(12)));
        Assert.Equal(FeedDueDisposition.PlacedAtFeedGate, gate.Disposition);
        Assert.Throws<ArgumentException>(() => service.Start(gate.State, gate, Profile("learning")));
    }

    [Fact]
    public void Expiration_no_active_default_tick_and_late_catch_up_are_exact_and_preserve_unrelated_runtime()
    {
        var expiration = new IntakeDeadlineExpirationService();
        var initial = RuntimeFixture.CreateInitialState();
        Assert.Same(initial, Assert.IsType<IntakeDeadlineNoActiveDeadline>(expiration.Expire(initial, ServerTick.Zero)).State);
        Assert.Throws<ArgumentOutOfRangeException>(() => expiration.Expire(initial, default));

        var started = StartInitial();
        var before = (started.State.PendingFeed, started.State.Inventory, started.State.Line, started.State.Containment, started.State.ProcessedIntentIds);
        var late = Assert.IsType<IntakeDeadlineExpired>(expiration.Expire(started.State, ServerTick.From(61)));
        Assert.Equal((ServerTick.From(60), ServerTick.From(61)), (late.ExpiredDeadline.DueAt, late.ExpiredAt));
        Assert.Equal(before, (late.State.PendingFeed, late.State.Inventory, late.State.Line, late.State.Containment, late.State.ProcessedIntentIds));
        Assert.Same(late.State, Assert.IsType<IntakeDeadlineNoActiveDeadline>(expiration.Expire(late.State, ServerTick.From(62))).State);
    }

    [Fact]
    public void Rejected_and_transition_into_intake_preserve_or_do_not_create_deadlines()
    {
        var started = StartInitial();
        var rejected = Assert.IsType<HostLogTransitionRejected>(new HostLogTransitionService().Apply(started.State, LogId.From("log_01"), LogState.AT_FEED_GATE));
        Assert.Same(started.State, rejected.State);
        Assert.Equal(started.Deadline, rejected.State.ActiveIntakeDeadline);

        var fresh = RuntimeFixture.MoveHost(RuntimeFixture.CreateInitialState(), "log_01", LogState.AT_FEED_GATE);
        var returned = RuntimeFixture.MoveHost(fresh, "log_01", LogState.AT_INTAKE);
        Assert.Null(returned.ActiveIntakeDeadline);
    }

    private static IntakeDeadlineStarted StartInitial()
    {
        var admission = AdmitInitial();
        return Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, Profile("learning")));
    }

    private static IntakeDeadlineStarted Start(FeedDueResolved admission) =>
        Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, Profile("learning")));

    private static FeedDueResolved AdmitInitial()
    {
        var initial = RuntimeFixture.CreateInitialState();
        var planned = Assert.IsType<InitialFeedScheduled>(new InitialFeedPlanningService().Plan(initial, ServerTick.Zero, Fixture.LoadP0().Shift.Scheduler));
        return Assert.IsType<FeedDueResolved>(new FeedDueResolutionService().Resolve(planned.State, ServerTick.Zero));
    }

    private static ShiftProfile Profile(string id) => Fixture.LoadP0().Shift.Profiles[ProfileId.From(id)];

    private static LogRuntimeState Log(ShiftRuntimeState state, string id)
    {
        Assert.True(state.TryGetLog(LogId.From(id), out var log));
        return log;
    }

    private static DomainEventDraft Draft(string id) => new(EventId.From(id), EventTypeId.From("test.tlaw016"), new DeadlinePayload(id), null);

    private static void Commit(JournaledMutationCommitService service, IEventJournal journal, ShiftRuntimeState before, ShiftRuntimeState after, ServerTick tick, string id) =>
        Assert.IsType<JournaledMutationCommitted>(service.Commit(journal, before, after, tick, Draft(id)));

    private sealed record DeadlinePayload(string Value) : IDomainEventPayload;
}
