using System.Collections.Immutable;
using System.Globalization;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Tests.Determinism;

/// <summary>
/// TLAW-042 test-only closed classification of the exact stage-2 outcome one scripted intent must produce.
/// The script declares it up front so the driver never infers gameplay from whatever the host happened to return.
/// </summary>
internal enum FullP0ScriptedIntentOutcome
{
    ManualRoutingAccepted,
    EarlyFeedScheduled,
    ProcedureActionHoldStarted,
    ProcedureActionCompletedImmediately,
    ConfirmationTestStarted,
    LineRepairStarted,
    ContainmentRitualStarted
}

/// <summary>
/// One immutable scripted authoritative intent. <see cref="ExpectedStateVersionOffset"/> is the exact number of
/// state versions the script asserts the host will already have advanced when stage 2 evaluates this receipt: zero
/// when no earlier same-tick work mutates state, and one per preceding accepted same-tick mutation (including a due
/// stage-1 completion). The driver therefore never has to observe gameplay to build a valid expected version.
/// </summary>
internal sealed record FullP0ScriptedIntent(
    IntentActionId Action,
    TargetId Target,
    IIntentParameters Parameters,
    int ExpectedStateVersionOffset,
    FullP0ScriptedIntentOutcome ExpectedOutcome);

/// <summary>One immutable scripted host tick: its ordered intents, its exact active tools, and its exact expected stage-7 publications.</summary>
internal sealed record FullP0ScriptedTick(
    ServerTick Tick,
    ImmutableArray<FullP0ScriptedIntent> Intents,
    ImmutableHashSet<ItemId> ActiveTools,
    ImmutableArray<EventTypeId> ExpectedEvents);

/// <summary>
/// One immutable frozen TLAW-042 scenario script. Every tick from zero through <see cref="FinalTick"/> is executed
/// sequentially through the authoritative host; ticks absent from <see cref="ScriptedTicks"/> carry no intent, no
/// active tool and exactly zero stage-7 publications.
/// </summary>
internal sealed class FullP0HostScenarioScript
{
    private FullP0HostScenarioScript(
        string scenarioId,
        string identityNamespace,
        ProfileId profile,
        ShiftSeed seed,
        ServerTick finalTick,
        bool requiresLifecycleCompletion,
        ImmutableDictionary<long, FullP0ScriptedTick> scriptedTicks)
    {
        ScenarioId = scenarioId;
        IdentityNamespace = identityNamespace;
        Profile = profile;
        Seed = seed;
        FinalTick = finalTick;
        RequiresLifecycleCompletion = requiresLifecycleCompletion;
        ScriptedTicks = scriptedTicks;
    }

    public string ScenarioId { get; }

    /// <summary>
    /// The frozen namespace every deterministic event and intent identity is derived from, together with the exact tick
    /// and ordinal. A sensitivity variant deliberately shares its baseline's namespace so that the unchanged prefix of
    /// the two traces is identity-identical and can be compared for exact structural equality.
    /// </summary>
    public string IdentityNamespace { get; }

    public ProfileId Profile { get; }
    public ShiftSeed Seed { get; }
    public ServerTick FinalTick { get; }
    public bool RequiresLifecycleCompletion { get; }
    public ImmutableDictionary<long, FullP0ScriptedTick> ScriptedTicks { get; }

    public FullP0ScriptedTick TickAt(ServerTick tick) =>
        ScriptedTicks.TryGetValue(tick.Value, out var scripted)
            ? scripted
            : new FullP0ScriptedTick(tick, [], ImmutableHashSet<ItemId>.Empty, []);

    // ----- Frozen identifiers used by every script -----

    internal static readonly ShiftSeed P0Seed = new(47001);
    internal static readonly ProfileId Learning = ProfileId.From("learning");
    internal static readonly ProfileId Pressure = ProfileId.From("pressure");

    internal static readonly ItemId HolyWater = ItemId.From("holy_water");
    internal static readonly ItemId Salt = ItemId.From("salt");
    internal static readonly ItemId RedTape = ItemId.From("red_tape");
    internal static readonly ItemId RelabelStamp = ItemId.From("relabel_stamp");
    internal static readonly ItemId SoundMeter = ItemId.From("sound_meter");
    internal static readonly ItemId ChoirCassette = ItemId.From("choir_cassette");
    internal static readonly ItemId Scale = ItemId.From("scale");
    internal static readonly ItemId Caliper = ItemId.From("caliper");

    private static readonly EventTypeId FeedScheduled = HostStageSevenEventTypes.FeedScheduled;
    private static readonly EventTypeId EarlyFeedRequested = HostStageSevenEventTypes.EarlyFeedRequested;
    private static readonly EventTypeId LogPlacedAtFeedGate = HostStageSevenEventTypes.LogPlacedAtFeedGate;
    private static readonly EventTypeId LogAdmittedToIntake = HostStageSevenEventTypes.LogAdmittedToIntake;
    private static readonly EventTypeId IntakeDeadlineStarted = HostStageSevenEventTypes.IntakeDeadlineStarted;
    private static readonly EventTypeId IntakeDeadlineExpired = HostStageSevenEventTypes.IntakeDeadlineExpired;
    private static readonly EventTypeId AutoRouteAttempted = HostStageSevenEventTypes.AutoRouteAttempted;
    private static readonly EventTypeId LineJammed = HostStageSevenEventTypes.LineJammed;
    private static readonly EventTypeId RepairStarted = HostStageSevenEventTypes.RepairStarted;
    private static readonly EventTypeId RepairCompleted = HostStageSevenEventTypes.RepairCompleted;
    private static readonly EventTypeId SawCycleStarted = HostStageSevenEventTypes.SawCycleStarted;
    private static readonly EventTypeId SawCycleCompleted = HostStageSevenEventTypes.SawCycleCompleted;
    private static readonly EventTypeId LineNoiseChanged = HostStageSevenEventTypes.LineNoiseChanged;
    private static readonly EventTypeId LogRouted = HostStageSevenEventTypes.LogRouted;
    private static readonly EventTypeId LogWrittenOff = HostStageSevenEventTypes.LogWrittenOff;
    private static readonly EventTypeId ProcedureActionStarted = HostStageSevenEventTypes.ProcedureActionStarted;
    private static readonly EventTypeId ProcedureActionCompleted = HostStageSevenEventTypes.ProcedureActionCompleted;
    private static readonly EventTypeId ConfirmationTestStarted = HostStageSevenEventTypes.ConfirmationTestStarted;
    private static readonly EventTypeId ConfirmationTestCompleted = HostStageSevenEventTypes.ConfirmationTestCompleted;
    private static readonly EventTypeId ContainmentRitualStarted = HostStageSevenEventTypes.ContainmentRitualStarted;
    private static readonly EventTypeId ContainmentRitualCompleted = HostStageSevenEventTypes.ContainmentRitualCompleted;
    private static readonly EventTypeId ContainmentStateChanged = HostStageSevenEventTypes.ContainmentStateChanged;
    private static readonly EventTypeId ShiftCompleted = HostStageSevenEventTypes.ShiftCompleted;

    // ----- Scripted intent factories -----

    private static FullP0ScriptedIntent Route(string logId, IntentActionId action, int offset = 0) =>
        new(action, TargetId.From(logId), NoIntentParameters.Instance, offset, FullP0ScriptedIntentOutcome.ManualRoutingAccepted);

    private static FullP0ScriptedIntent EarlyFeed(int offset = 0) =>
        new(FeedPlanningIntentActions.RequestEarlyFeed, FeedPlanningTargets.FeedGate, NoIntentParameters.Instance, offset, FullP0ScriptedIntentOutcome.EarlyFeedScheduled);

    private static FullP0ScriptedIntent StartRepair(int offset = 0) =>
        new(LineRepairIntentActions.StartLineRepair, LineRepairIntentTargets.Line, NoIntentParameters.Instance, offset, FullP0ScriptedIntentOutcome.LineRepairStarted);

    private static FullP0ScriptedIntent StartRitual(int offset = 0) =>
        new(ContainmentRitualIntentActions.StartContainmentRitual, ContainmentRitualIntentTargets.Containment, NoIntentParameters.Instance, offset, FullP0ScriptedIntentOutcome.ContainmentRitualStarted);

    private static FullP0ScriptedIntent StartConfirmation(string logId, int offset = 0) =>
        new(ConfirmationIntentActions.StartConfirmationTest, TargetId.From(logId), NoIntentParameters.Instance, offset, FullP0ScriptedIntentOutcome.ConfirmationTestStarted);

    private static FullP0ScriptedIntent Procedure(string logId, ItemId item, int offset, FullP0ScriptedIntentOutcome outcome) =>
        new(ProcedureIntentActions.StartProcedureAction, TargetId.From(logId), new ProcedureActionIntentParameters(item), offset, outcome);

    // ----- Frozen scripts -----

    /// <summary>
    /// §8.1 — the exact Learning correct-path script. It exercises every composed stage-2 action family, reaches a
    /// natural containment/intake overlap, and completes with the frozen quota objective satisfied at tick 172.
    /// </summary>
    internal static FullP0HostScenarioScript LearningCorrectPath() => BuildLearningCorrectPath("TLAW042_LEARNING_CORRECT_PATH", 166);

    /// <summary>§11 — the sensitivity variant: exactly one bounded routing choice (log_12) moves from tick 166 to tick 167.</summary>
    internal static FullP0HostScenarioScript LearningCorrectPathSensitivityVariant() => BuildLearningCorrectPath("TLAW042_LEARNING_CORRECT_PATH_VARIANT", 167);

    /// <summary>The exact tick at and after which the sensitivity variant may legitimately differ from the baseline.</summary>
    internal const long SensitivityDivergenceTick = 166;

    /// <summary>The identity namespace the Learning correct path shares with its one-input sensitivity variant.</summary>
    internal const string LearningCorrectPathIdentityNamespace = "TLAW042_LEARNING_CORRECT_PATH";

    private static FullP0HostScenarioScript BuildLearningCorrectPath(string scenarioId, long finalRoutingTick)
    {
        var soundMeter = ImmutableHashSet.Create(SoundMeter);
        var falseSpeciesTools = ImmutableHashSet.Create(Scale, Caliper);
        var builder = new ScriptBuilder();

        builder.Tick(0, [FeedScheduled, LogAdmittedToIntake, IntakeDeadlineStarted, LineNoiseChanged]);
        builder.Tick(1, [Route("log_01", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted, FeedScheduled]);
        builder.Tick(6, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(7, [SawCycleCompleted]);
        builder.Tick(8, [EarlyFeed()], [EarlyFeedRequested, FeedScheduled]);
        builder.Tick(9, [LineNoiseChanged]);
        builder.Tick(10, [LogPlacedAtFeedGate, LineJammed, LineNoiseChanged]);
        builder.Tick(11, [StartRepair()], [RepairStarted]);
        builder.Tick(12, [Route("log_02", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted]);
        builder.Tick(17, [RepairCompleted, LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(18, [SawCycleCompleted]);
        builder.Tick(20, [LineNoiseChanged]);
        builder.Tools(21, soundMeter, [StartConfirmation("log_03")], [ConfirmationTestStarted]);
        builder.Tools(22, soundMeter, [], []);
        builder.Tools(23, soundMeter, [], []);
        builder.Tools(24, soundMeter, [], []);
        builder.Tools(25, soundMeter, [], [ConfirmationTestCompleted]);
        builder.Tick(
            26,
            [Route("log_03", LogIntentActions.RouteToProcedure), Procedure("log_03", HolyWater, 1, FullP0ScriptedIntentOutcome.ProcedureActionHoldStarted)],
            [LogRouted, ProcedureActionStarted, FeedScheduled, LineNoiseChanged]);
        builder.Tick(28, [LineNoiseChanged]);
        builder.Tick(29, [Route("log_03", LogIntentActions.ReturnFromProcedure, 1)], [ProcedureActionCompleted, LogRouted, LineNoiseChanged]);
        builder.Tick(30, [Route("log_03", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted]);
        builder.Tick(31, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(32, [Route("log_04", LogIntentActions.RouteToSawQueue)], [LogRouted, FeedScheduled]);
        builder.Tick(36, [SawCycleCompleted, SawCycleStarted]);
        builder.Tick(37, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tools(38, falseSpeciesTools, [StartConfirmation("log_05")], [ConfirmationTestStarted]);
        builder.Tools(39, falseSpeciesTools, [], []);
        builder.Tools(40, falseSpeciesTools, [], []);
        builder.Tools(41, falseSpeciesTools, [], []);
        builder.Tools(42, falseSpeciesTools, [], [SawCycleCompleted]);
        builder.Tools(43, falseSpeciesTools, [], []);
        builder.Tools(44, falseSpeciesTools, [], [ConfirmationTestCompleted, LineNoiseChanged]);
        builder.Tick(
            45,
            [Route("log_05", LogIntentActions.RouteToProcedure), Procedure("log_05", RelabelStamp, 1, FullP0ScriptedIntentOutcome.ProcedureActionCompletedImmediately)],
            [LogRouted, ProcedureActionCompleted, FeedScheduled, LineNoiseChanged]);
        builder.Tick(46, [Route("log_05", LogIntentActions.ReturnFromProcedure)], [LogRouted]);
        builder.Tick(47, [Route("log_05", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted]);
        builder.Tick(50, [LogAdmittedToIntake, IntakeDeadlineStarted]);

        // log_06 is RESIN_BLASPHEMER: it is sealed through the real configured procedure so the core success path
        // never produces an incorrect-processing effect descriptor.
        builder.Tick(
            51,
            [
                Route("log_06", LogIntentActions.RouteToProcedure),
                Procedure("log_06", Salt, 1, FullP0ScriptedIntentOutcome.ProcedureActionCompletedImmediately),
                Procedure("log_06", RedTape, 2, FullP0ScriptedIntentOutcome.ProcedureActionCompletedImmediately)
            ],
            [LogRouted, ProcedureActionCompleted, ProcedureActionCompleted, FeedScheduled]);
        builder.Tick(52, [Route("log_06", LogIntentActions.ReturnFromProcedure)], [LogRouted]);
        builder.Tick(53, [Route("log_06", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleCompleted, SawCycleStarted]);
        builder.Tick(56, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(57, [Route("log_07", LogIntentActions.RouteToSawQueue)], [LogRouted, FeedScheduled]);
        builder.Tick(59, [SawCycleCompleted, SawCycleStarted]);
        builder.Tick(62, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(63, [Route("log_08", LogIntentActions.WriteOff)], [LogWrittenOff, ContainmentStateChanged, FeedScheduled]);
        builder.Tick(65, [SawCycleCompleted]);
        builder.Tick(67, [LineNoiseChanged]);
        builder.Tick(68, [LogAdmittedToIntake, IntakeDeadlineStarted, LineNoiseChanged]);
        builder.Tick(70, [LineNoiseChanged]);
        builder.Tick(128, [IntakeDeadlineExpired, AutoRouteAttempted, FeedScheduled, LineNoiseChanged]);
        builder.Tick(129, [SawCycleStarted]);
        builder.Tick(133, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(135, [SawCycleCompleted]);

        // log_10 is the second RESIN_BLASPHEMER: it consumes the remaining salt and red tape through the real
        // configured procedure, so both Resin logs reach the saw sealed and the core path stays effect-independent.
        builder.Tick(
            136,
            [
                Route("log_10", LogIntentActions.RouteToProcedure),
                Procedure("log_10", Salt, 1, FullP0ScriptedIntentOutcome.ProcedureActionCompletedImmediately),
                Procedure("log_10", RedTape, 2, FullP0ScriptedIntentOutcome.ProcedureActionCompletedImmediately)
            ],
            [LogRouted, ProcedureActionCompleted, ProcedureActionCompleted, FeedScheduled]);
        builder.Tick(137, [Route("log_10", LogIntentActions.ReturnFromProcedure)], [LogRouted]);
        builder.Tick(138, [Route("log_10", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted]);
        builder.Tick(141, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(144, [SawCycleCompleted]);
        builder.Tick(146, [LineNoiseChanged]);
        builder.Tick(153, [ContainmentStateChanged]);
        builder.Tick(154, [StartRitual()], [ContainmentRitualStarted]);
        builder.Tick(158, [ContainmentRitualCompleted]);
        builder.Tick(159, [Route("log_11", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted, FeedScheduled, LineNoiseChanged]);
        builder.Tick(164, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(165, [SawCycleCompleted]);
        builder.Tick(finalRoutingTick, [Route("log_12", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted]);
        builder.Tick(finalRoutingTick + 6, [SawCycleCompleted, ShiftCompleted]);

        return new FullP0HostScenarioScript(scenarioId, LearningCorrectPathIdentityNamespace, Learning, P0Seed, ServerTick.From(finalRoutingTick + 6), true, builder.Build());
    }

    /// <summary>§8.2 — the cautious full-timeout policy under the exact learning profile (60-second timeout, deadline 840).</summary>
    internal static FullP0HostScenarioScript LearningFullTimeout() =>
        BuildCautiousFullTimeout("TLAW042_LEARNING_FULL_TIMEOUT", Learning, 60, 782);

    /// <summary>§8.3 — the identical cautious policy under the exact pressure profile (45-second timeout, deadline 600).</summary>
    internal static FullP0HostScenarioScript PressureFullTimeout() =>
        BuildCautiousFullTimeout("TLAW042_PRESSURE_FULL_TIMEOUT", Pressure, 45, 600);

    /// <summary>The one manifest-derived classification the cautious full-timeout policy branches on.</summary>
    private enum CautiousLogKind { Ordinary, Penitent, FalseSpecies, Resin }

    private static CautiousLogKind KindOf(int manifestIndex) => manifestIndex switch
    {
        3 or 8 => CautiousLogKind.Penitent,
        5 => CautiousLogKind.FalseSpecies,
        6 or 10 => CautiousLogKind.Resin,
        _ => CautiousLogKind.Ordinary
    };

    /// <summary>
    /// The one mechanically defined cautious full-timeout policy shared by §8.2 and §8.3. Only the configured profile
    /// timeout differs between the two runs; the decision rule is identical.
    /// <para>
    /// Ordinary logs are never routed by hand: each is released solely by its exact configured intake timeout and the
    /// frozen default auto-route, so the full timeout cost is paid in every cycle.
    /// </para>
    /// <para>
    /// Anomalous logs dwell at intake for as long as the frozen rules allow and then perform exactly the configured
    /// confirmation and procedure work that their correct processing requires. The latest deterministic intervention is
    /// forced by two frozen facts: the configured confirmation test only runs while the log is at intake, and the log
    /// must leave intake during stage 2 of its exact deadline tick, because stage 3 of that same tick would otherwise
    /// expire the deadline and auto-route it to the saw unflagged. The confirmation therefore starts at exactly
    /// <c>deadline - confirmDurationSeconds</c> (the last tick from which it can still complete in stage 1 of the
    /// deadline tick), and the log is routed to procedure in stage 2 of the deadline tick itself. Waiting one tick
    /// longer in either place would force an incorrect Penitent or Resin saw outcome, so this is the maximally cautious
    /// dwell the scenario permits — no timeout, deadline or catalog value is altered to reach it.
    /// </para>
    /// </summary>
    private static FullP0HostScenarioScript BuildCautiousFullTimeout(string scenarioId, ProfileId profile, long intakeTimeout, long finalTick)
    {
        // The normal feed delay follows every vacated intake, so one log enters intake every timeout + 5 ticks.
        var cadence = intakeTimeout + 5;
        var builder = new ScriptBuilder();
        builder.Tick(0, [FeedScheduled, LogAdmittedToIntake, IntakeDeadlineStarted, LineNoiseChanged]);
        builder.Tick(2, [LineNoiseChanged]);

        for (var manifestIndex = 1; manifestIndex <= 12; manifestIndex++)
        {
            var logId = $"log_{manifestIndex.ToString("00", CultureInfo.InvariantCulture)}";
            var deadline = (cadence * (manifestIndex - 1)) + intakeTimeout;
            var isLastLog = manifestIndex == 12;

            switch (KindOf(manifestIndex))
            {
                case CautiousLogKind.Ordinary:
                    builder.Tick(
                        deadline,
                        isLastLog
                            ? [IntakeDeadlineExpired, AutoRouteAttempted, LineNoiseChanged]
                            : [IntakeDeadlineExpired, AutoRouteAttempted, FeedScheduled, LineNoiseChanged]);
                    builder.Tick(deadline + 1, [SawCycleStarted]);
                    if (!isLastLog)
                    {
                        builder.Tick(deadline + 5, [LogAdmittedToIntake, IntakeDeadlineStarted]);
                        builder.Tick(deadline + 7, [SawCycleCompleted]);
                        builder.Tick(deadline + 9, [LineNoiseChanged]);
                    }
                    else if (deadline + 7 <= finalTick)
                    {
                        builder.Tick(deadline + 7, [SawCycleCompleted, ShiftCompleted]);
                    }
                    else
                    {
                        // The hard deadline arrives while the last saw cycle is still running.
                        builder.Tick(finalTick, [ShiftCompleted]);
                    }

                    break;

                case CautiousLogKind.Penitent:
                    // Confirmation: sound_meter, 4 continuous quiet seconds. Procedure: holy_water with a 3-second hold.
                    builder.Tools(deadline - 4, ImmutableHashSet.Create(SoundMeter), [StartConfirmation(logId)], [ConfirmationTestStarted]);
                    Idle(builder, deadline - 3, deadline - 1, ImmutableHashSet.Create(SoundMeter));
                    builder.Tools(
                        deadline,
                        ImmutableHashSet.Create(SoundMeter),
                        [
                            Route(logId, LogIntentActions.RouteToProcedure, 1),
                            Procedure(logId, HolyWater, 2, FullP0ScriptedIntentOutcome.ProcedureActionHoldStarted)
                        ],
                        [ConfirmationTestCompleted, LogRouted, ProcedureActionStarted, FeedScheduled, LineNoiseChanged]);
                    builder.Tick(deadline + 2, [LineNoiseChanged]);
                    builder.Tick(deadline + 3, [Route(logId, LogIntentActions.ReturnFromProcedure, 1)], [ProcedureActionCompleted, LogRouted, LineNoiseChanged]);
                    builder.Tick(deadline + 4, [Route(logId, LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted]);
                    builder.Tick(deadline + 5, [LogAdmittedToIntake, IntakeDeadlineStarted]);
                    builder.Tick(deadline + 10, [SawCycleCompleted]);
                    builder.Tick(deadline + 12, [LineNoiseChanged]);
                    break;

                case CautiousLogKind.FalseSpecies:
                    // Confirmation: scale and caliper, 6 seconds. Procedure: the reusable relabel stamp, no hold.
                    builder.Tools(deadline - 6, ImmutableHashSet.Create(Scale, Caliper), [StartConfirmation(logId)], [ConfirmationTestStarted]);
                    Idle(builder, deadline - 5, deadline - 1, ImmutableHashSet.Create(Scale, Caliper));
                    builder.Tools(
                        deadline,
                        ImmutableHashSet.Create(Scale, Caliper),
                        [
                            Route(logId, LogIntentActions.RouteToProcedure, 1),
                            Procedure(logId, RelabelStamp, 2, FullP0ScriptedIntentOutcome.ProcedureActionCompletedImmediately)
                        ],
                        [ConfirmationTestCompleted, LogRouted, ProcedureActionCompleted, FeedScheduled, LineNoiseChanged]);
                    ImmediateProcedureTail(builder, logId, deadline);
                    break;

                case CautiousLogKind.Resin:
                    // Confirmation: choir cassette, 4 continuous seconds. Procedure: salt then red tape, no hold.
                    builder.Tools(deadline - 4, ImmutableHashSet.Create(ChoirCassette), [StartConfirmation(logId)], [ConfirmationTestStarted]);
                    Idle(builder, deadline - 3, deadline - 1, ImmutableHashSet.Create(ChoirCassette));
                    builder.Tools(
                        deadline,
                        ImmutableHashSet.Create(ChoirCassette),
                        [
                            Route(logId, LogIntentActions.RouteToProcedure, 1),
                            Procedure(logId, Salt, 2, FullP0ScriptedIntentOutcome.ProcedureActionCompletedImmediately),
                            Procedure(logId, RedTape, 3, FullP0ScriptedIntentOutcome.ProcedureActionCompletedImmediately)
                        ],
                        [ConfirmationTestCompleted, LogRouted, ProcedureActionCompleted, ProcedureActionCompleted, FeedScheduled, LineNoiseChanged]);
                    ImmediateProcedureTail(builder, logId, deadline);
                    break;

                default:
                    throw new InvalidOperationException("Unknown cautious log kind.");
            }
        }

        return new FullP0HostScenarioScript(scenarioId, scenarioId, profile, P0Seed, ServerTick.From(finalTick), true, builder.Build());
    }

    /// <summary>The shared tail for an anomaly whose configured procedure completes immediately.</summary>
    private static void ImmediateProcedureTail(ScriptBuilder builder, string logId, long deadline)
    {
        builder.Tick(deadline + 1, [Route(logId, LogIntentActions.ReturnFromProcedure)], [LogRouted]);
        builder.Tick(deadline + 2, [Route(logId, LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted]);
        builder.Tick(deadline + 5, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(deadline + 8, [SawCycleCompleted]);
        builder.Tick(deadline + 10, [LineNoiseChanged]);
    }

    /// <summary>Keeps the configured confirmation tools active across the silent ticks of a running confirmation.</summary>
    private static void Idle(ScriptBuilder builder, long fromTick, long toTick, ImmutableHashSet<ItemId> tools)
    {
        for (var tick = fromTick; tick <= toTick; tick++)
        {
            builder.Tools(tick, tools, [], []);
        }
    }

    /// <summary>§8.4 — every suspicious log is irreversibly written off; the seven normals cannot satisfy the frozen objective.</summary>
    internal static FullP0HostScenarioScript WriteOffAllSuspicious()
    {
        var builder = new ScriptBuilder();
        builder.Tick(0, [FeedScheduled, LogAdmittedToIntake, IntakeDeadlineStarted, LineNoiseChanged]);
        builder.Tick(1, [Route("log_01", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted, FeedScheduled]);
        builder.Tick(6, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(7, [Route("log_02", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleCompleted, SawCycleStarted, FeedScheduled]);
        builder.Tick(12, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(13, [Route("log_03", LogIntentActions.WriteOff)], [LogWrittenOff, ContainmentStateChanged, SawCycleCompleted, FeedScheduled]);
        builder.Tick(15, [LineNoiseChanged]);
        builder.Tick(18, [LogAdmittedToIntake, IntakeDeadlineStarted, LineNoiseChanged]);
        builder.Tick(19, [Route("log_04", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted, FeedScheduled]);
        builder.Tick(24, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(25, [Route("log_05", LogIntentActions.WriteOff)], [LogWrittenOff, SawCycleCompleted, FeedScheduled]);
        builder.Tick(27, [LineNoiseChanged]);
        builder.Tick(30, [LogAdmittedToIntake, IntakeDeadlineStarted, LineNoiseChanged]);
        builder.Tick(31, [Route("log_06", LogIntentActions.WriteOff)], [LogWrittenOff, FeedScheduled]);
        builder.Tick(33, [LineNoiseChanged]);
        builder.Tick(36, [LogAdmittedToIntake, IntakeDeadlineStarted, LineNoiseChanged]);
        builder.Tick(37, [Route("log_07", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted, FeedScheduled]);
        builder.Tick(42, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(43, [Route("log_08", LogIntentActions.WriteOff)], [LogWrittenOff, SawCycleCompleted, FeedScheduled]);
        builder.Tick(45, [LineNoiseChanged]);
        builder.Tick(48, [LogAdmittedToIntake, IntakeDeadlineStarted, LineNoiseChanged]);
        builder.Tick(49, [Route("log_09", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted, FeedScheduled]);
        builder.Tick(54, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(55, [Route("log_10", LogIntentActions.WriteOff)], [LogWrittenOff, SawCycleCompleted, FeedScheduled]);
        builder.Tick(57, [LineNoiseChanged]);
        builder.Tick(60, [LogAdmittedToIntake, IntakeDeadlineStarted, LineNoiseChanged]);
        builder.Tick(61, [Route("log_11", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted, FeedScheduled]);
        builder.Tick(66, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(67, [Route("log_12", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleCompleted, SawCycleStarted]);
        builder.Tick(73, [SawCycleCompleted, ShiftCompleted]);
        return new FullP0HostScenarioScript("TLAW042_WRITE_OFF_ALL_SUSPICIOUS", "TLAW042_WRITE_OFF_ALL_SUSPICIOUS", Learning, P0Seed, ServerTick.From(73), true, builder.Build());
    }

    /// <summary>§9 — the bounded incorrect-Penitent saw scenario. log_03 reaches the saw without SANITIZED_PENITENT.</summary>
    internal static FullP0HostScenarioScript IncorrectPenitent()
    {
        var builder = new ScriptBuilder();
        ClearNormals(builder, 2);
        builder.Tick(13, [Route("log_03", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted, FeedScheduled]);
        builder.Tick(18, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(19, [SawCycleCompleted]);
        return new FullP0HostScenarioScript("TLAW042_INCORRECT_PENITENT", "TLAW042_INCORRECT_PENITENT", Learning, P0Seed, ServerTick.From(19), false, builder.Build());
    }

    /// <summary>§9 — the bounded incorrect-False-Species saw scenario. log_05 reaches the saw without CORRECTLY_RELABELED.</summary>
    internal static FullP0HostScenarioScript IncorrectFalseSpecies()
    {
        var builder = new ScriptBuilder();
        ClearNormals(builder, 4);
        builder.Tick(25, [Route("log_05", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted, FeedScheduled]);
        builder.Tick(30, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(31, [SawCycleCompleted]);
        return new FullP0HostScenarioScript("TLAW042_INCORRECT_FALSE_SPECIES", "TLAW042_INCORRECT_FALSE_SPECIES", Learning, P0Seed, ServerTick.From(31), false, builder.Build());
    }

    /// <summary>§9 — the bounded incorrect-Resin saw scenario. log_06 reaches the saw without SEALED_RESIN.</summary>
    internal static FullP0HostScenarioScript IncorrectResin()
    {
        var builder = new ScriptBuilder();
        ClearNormals(builder, 5);
        builder.Tick(31, [Route("log_06", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted, FeedScheduled]);
        builder.Tick(36, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(37, [SawCycleCompleted]);
        return new FullP0HostScenarioScript("TLAW042_INCORRECT_RESIN", "TLAW042_INCORRECT_RESIN", Learning, P0Seed, ServerTick.From(37), false, builder.Build());
    }

    /// <summary>
    /// §9 — the bounded Resin wrong-holy-water recovery scenario. The configured wrong action is attempted first, then
    /// the exact configured Resin procedure is completed from the remaining approved resources in the same batch.
    /// </summary>
    internal static FullP0HostScenarioScript ResinWrongHolyWaterRecovery()
    {
        var builder = new ScriptBuilder();
        ClearNormals(builder, 5);
        builder.Tick(
            31,
            [
                Route("log_06", LogIntentActions.RouteToProcedure),
                Procedure("log_06", HolyWater, 1, FullP0ScriptedIntentOutcome.ProcedureActionCompletedImmediately),
                Procedure("log_06", Salt, 2, FullP0ScriptedIntentOutcome.ProcedureActionCompletedImmediately),
                Procedure("log_06", RedTape, 3, FullP0ScriptedIntentOutcome.ProcedureActionCompletedImmediately)
            ],
            [LogRouted, ProcedureActionCompleted, ProcedureActionCompleted, ProcedureActionCompleted, FeedScheduled]);
        builder.Tick(32, [Route("log_06", LogIntentActions.ReturnFromProcedure)], [LogRouted]);
        builder.Tick(33, [Route("log_06", LogIntentActions.RouteToSawQueue)], [LogRouted, SawCycleStarted]);
        builder.Tick(36, [LogAdmittedToIntake, IntakeDeadlineStarted]);
        builder.Tick(39, [SawCycleCompleted]);
        return new FullP0HostScenarioScript("TLAW042_RESIN_WRONG_HOLY_WATER_RECOVERY", "TLAW042_RESIN_WRONG_HOLY_WATER_RECOVERY", Learning, P0Seed, ServerTick.From(39), false, builder.Build());
    }

    /// <summary>Writes off the first <paramref name="count"/> manifest logs so a later suspicious log reaches intake in a bounded scenario.</summary>
    private static void ClearNormals(ScriptBuilder builder, int count)
    {
        builder.Tick(0, [FeedScheduled, LogAdmittedToIntake, IntakeDeadlineStarted, LineNoiseChanged]);
        for (var index = 0; index < count; index++)
        {
            var admitted = 6 * index;
            var written = admitted + 1;
            var logId = $"log_{(index + 1).ToString("00", CultureInfo.InvariantCulture)}";
            if (admitted > 0)
            {
                builder.Tick(admitted, [LogAdmittedToIntake, IntakeDeadlineStarted, LineNoiseChanged]);
            }

            // log_03 is the first written-off danger-bearing anomaly, so containment arms its interval in that exact tick.
            builder.Tick(
                written,
                [Route(logId, LogIntentActions.WriteOff)],
                logId == "log_03" ? [LogWrittenOff, ContainmentStateChanged, FeedScheduled] : [LogWrittenOff, FeedScheduled]);
            builder.Tick(written + 2, [LineNoiseChanged]);
        }

        builder.Tick(6 * count, [LogAdmittedToIntake, IntakeDeadlineStarted, LineNoiseChanged]);
    }

    private sealed class ScriptBuilder
    {
        private readonly Dictionary<long, FullP0ScriptedTick> _ticks = [];

        public void Tick(long tick, ImmutableArray<EventTypeId> events) => Tools(tick, ImmutableHashSet<ItemId>.Empty, [], events);

        public void Tick(long tick, ImmutableArray<FullP0ScriptedIntent> intents, ImmutableArray<EventTypeId> events) =>
            Tools(tick, ImmutableHashSet<ItemId>.Empty, intents, events);

        public void Tools(long tick, ImmutableHashSet<ItemId> tools, ImmutableArray<FullP0ScriptedIntent> intents, ImmutableArray<EventTypeId> events)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "A scripted tick cannot be negative.");
            }

            if (!_ticks.TryAdd(tick, new FullP0ScriptedTick(ServerTick.From(tick), intents, tools, events)))
            {
                throw new ArgumentException($"Tick {tick.ToString(CultureInfo.InvariantCulture)} is already scripted.", nameof(tick));
            }
        }

        public ImmutableDictionary<long, FullP0ScriptedTick> Build() => _ticks.ToImmutableDictionary();
    }
}
