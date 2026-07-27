using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
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

    private static IntakeDeadlineStarted StartInitial()
    {
        var admission = AdmitInitial();
        return Assert.IsType<IntakeDeadlineStarted>(new IntakeDeadlineStartService().Start(admission.State, admission, Profile("learning")));
    }

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
}
