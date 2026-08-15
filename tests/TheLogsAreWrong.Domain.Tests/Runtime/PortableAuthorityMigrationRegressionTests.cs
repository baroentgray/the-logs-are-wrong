using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Domain.Scheduler;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-061")]
public sealed class PortableAuthorityMigrationRegressionTests
{
    [Fact]
    public void Accepted_real_authority_chain_retains_the_canonical_projection_hash()
    {
        var configuration = ProbeConfiguration();
        var logId = LogId.From("probe_log");
        var created = ShiftRuntimeState.Create(configuration);
        var transitions = new HostLogTransitionService();
        var atFeedGate = Assert.IsType<HostLogTransitionAccepted>(transitions.Apply(created, logId, LogState.AT_FEED_GATE)).State;
        var atIntake = Assert.IsType<HostLogTransitionAccepted>(transitions.Apply(atFeedGate, logId, LogState.AT_INTAKE)).State;
        var queuedForSaw = Assert.IsType<HostLogTransitionAccepted>(transitions.Apply(atIntake, logId, LogState.QUEUED_FOR_SAW)).State;
        var sawStarted = Assert.IsType<SawCycleStarted>(new SawCycleStartService().Start(queuedForSaw, ServerTick.From(10), configuration.Scheduler));
        var lineNoise = Assert.IsType<LineNoiseEvaluatedWithChange>(new LineNoiseDerivationService().Evaluate(
            LineNoiseRuntimeState.Create(sawStarted.State.ShiftId),
            sawStarted.State,
            MovementNoiseRuntimeState.Create(sawStarted.State.ShiftId),
            ServerTick.From(10)));
        Assert.True(sawStarted.State.TryGetLog(logId, out var log));

        var projection = string.Join("\n", new[]
        {
            "operation_chain=ShiftRuntimeState.Create>HostLogTransitionService.Apply>HostLogTransitionService.Apply>HostLogTransitionService.Apply>SawCycleStartService.Start>LineNoiseDerivationService.Evaluate",
            $"shift_id={created.ShiftId}",
            $"created_state_version={created.StateVersion}",
            $"queued_state_version={queuedForSaw.StateVersion}",
            $"saw_state_version={sawStarted.State.StateVersion}",
            $"log_id={logId}",
            $"log_state={log.State}",
            $"saw_started_at={sawStarted.Cycle.StartedAt}",
            $"saw_due_at={sawStarted.Cycle.DueAt}",
            $"line_noise={lineNoise.State.Current}",
            $"line_noise_evaluated_at={lineNoise.State.LastEvaluatedAt}",
            $"line_noise_changed_at={lineNoise.State.LastChangedAt}"
        });

        Assert.Equal("CB58349E77C6F85970D64DE3610B6B4FEC6CD4AB6C3A383B0B9513E1FDEECA5F", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(projection))));
    }

    private static ShiftConfiguration ProbeConfiguration()
    {
        var baseline = Fixture.LoadP0().Shift;
        return baseline with
        {
            ShiftId = ShiftId.From("TLAW058_PROBE_SHIFT"),
            Supply = new SupplyDefinition(1, 0),
            Objectives = new ObjectivesDefinition(new QuotaDefinition(1, ImmutableDictionary<SpeciesId, int>.Empty.Add(SpeciesId.From("pine"), 1)), 0),
            Scheduler = baseline.Scheduler with { SawCycleSeconds = 4 },
            Manifest = ImmutableArray.Create(new ManifestLogDefinition(LogId.From("probe_log"), SpeciesId.From("pine"), SpeciesId.From("pine"), null))
        };
    }
}
