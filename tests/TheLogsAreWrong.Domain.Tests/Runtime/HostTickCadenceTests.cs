using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Domain.Tests.Runtime;

[Trait("Scope", "TLAW-068")]
public sealed class HostTickCadenceTests
{
    private const string CanonicalCadenceProjectionSha = "A3CFED2906266153792A1B9FFFB2CBE6EE48F450342EF933B9DAD515DD0BADA0";

    [Fact]
    public void One_second_contract_maps_999_1000_and_2000_milliseconds_without_rounding()
    {
        var cadence = new HostTickCadence();

        cadence.Accumulate(ElapsedMilliseconds(999));
        Assert.Equal(999, cadence.RemainderMilliseconds.Value);
        Assert.Equal(0, cadence.DueTickCount);
        Assert.False(cadence.TryGetDueTicks(out _));

        cadence.Accumulate(ElapsedMilliseconds(1));
        AssertDue(cadence, first: 0, last: 0, count: 1);
        Assert.Equal(0, cadence.RemainderMilliseconds.Value);

        var twoSeconds = new HostTickCadence();
        twoSeconds.Accumulate(ElapsedMilliseconds(2000));
        AssertDue(twoSeconds, first: 0, last: 1, count: 2);
        Assert.Equal(0, twoSeconds.RemainderMilliseconds.Value);
    }

    [Fact]
    public void Sub_second_remainder_is_retained_exactly_across_calls()
    {
        var cadence = new HostTickCadence();

        cadence.Accumulate(ElapsedMilliseconds(400));
        cadence.Accumulate(ElapsedMilliseconds(400));
        Assert.Equal(800, cadence.RemainderMilliseconds.Value);
        Assert.False(cadence.TryGetDueTicks(out _));

        cadence.Accumulate(ElapsedMilliseconds(200));

        Assert.Equal(0, cadence.RemainderMilliseconds.Value);
        AssertDue(cadence, first: 0, last: 0, count: 1);
    }

    [Fact]
    public void Zero_one_and_many_due_ticks_are_all_first_class_results()
    {
        var cadence = new HostTickCadence();

        cadence.Accumulate(ElapsedMilliseconds(0));
        Assert.False(cadence.TryGetDueTicks(out _));
        Assert.Equal(0, cadence.DueTickCount);

        cadence.Accumulate(ElapsedMilliseconds(1000));
        AssertDue(cadence, first: 0, last: 0, count: 1);

        var many = new HostTickCadence();
        many.Accumulate(ElapsedMilliseconds(5000));
        AssertDue(many, first: 0, last: 4, count: 5);
    }

    [Fact]
    public void Equal_elapsed_history_has_identical_due_progression_under_materially_different_partitions()
    {
        var coarse = RunWithoutRetiring(SplitEvenly(totalMilliseconds: 20_000, parts: 20));
        var fine = RunWithoutRetiring(SplitEvenly(totalMilliseconds: 20_000, parts: 120));

        Assert.Equal(Snapshot(coarse), Snapshot(fine));
        AssertDue(coarse, first: 0, last: 19, count: 20);
        Assert.Equal(0, coarse.RemainderMilliseconds.Value);
    }

    [Fact]
    public void Long_stall_exposes_the_complete_due_range_without_allocating_one_entry_per_tick()
    {
        var cadence = new HostTickCadence();

        cadence.Accumulate(ElapsedMilliseconds(10_000_000));

        AssertDue(cadence, first: 0, last: 9_999, count: 10_000);
        Assert.Equal(0, cadence.RemainderMilliseconds.Value);
    }

    [Fact]
    public void Backlog_is_retired_only_by_explicit_ordered_acknowledgement_and_is_never_silently_discarded()
    {
        var cadence = new HostTickCadence();
        cadence.Accumulate(ElapsedMilliseconds(3000));
        AssertDue(cadence, first: 0, last: 2, count: 3);

        Assert.Equal(ServerTick.Zero, cadence.RetireNextDueTick());
        AssertDue(cadence, first: 1, last: 2, count: 2);

        cadence.Accumulate(ElapsedMilliseconds(500));
        AssertDue(cadence, first: 1, last: 2, count: 2);
        Assert.Equal(500, cadence.RemainderMilliseconds.Value);

        Assert.Equal(ServerTick.From(1), cadence.RetireNextDueTick());
        Assert.Equal(ServerTick.From(2), cadence.RetireNextDueTick());
        Assert.Equal(0, cadence.DueTickCount);
        Assert.False(cadence.TryGetDueTicks(out _));
        Assert.Equal(500, cadence.RemainderMilliseconds.Value);
    }

    [Fact]
    public void No_per_call_retirement_cap_or_unbounded_due_collection_is_required_for_large_backlog()
    {
        var cadence = new HostTickCadence();

        cadence.Accumulate(ElapsedMilliseconds(1_000_000_000));

        AssertDue(cadence, first: 0, last: 999_999, count: 1_000_000);
    }

    [Fact]
    public void Identical_initial_state_and_elapsed_evidence_replay_to_the_recorded_canonical_projection()
    {
        var first = CadenceProjection();
        var second = CadenceProjection();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first)));

        Assert.Equal(first, second);
        Assert.Equal(CanonicalCadenceProjectionSha, hash);
    }

    [Fact]
    public void Invalid_evidence_and_overflow_fail_closed_without_partial_cadence_mutation()
    {
        var cadence = new HostTickCadence();
        cadence.Accumulate(ElapsedMilliseconds(999));
        var beforeElapsedOverflow = Snapshot(cadence);

        Assert.Throws<OverflowException>(() => cadence.Accumulate(ElapsedMilliseconds(long.MaxValue)));
        Assert.Equal(beforeElapsedOverflow, Snapshot(cadence));
        Assert.Throws<InvalidOperationException>(() => cadence.Accumulate(default));
        Assert.Equal(beforeElapsedOverflow, Snapshot(cadence));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthoritativeElapsedMilliseconds.FromMilliseconds(-1));
        Assert.Throws<ArgumentException>(() => new HostTickCadence(default));

        var nearMaximumTick = new HostTickCadence(ServerTick.From(long.MaxValue - 1));
        var beforeTickOverflow = Snapshot(nearMaximumTick);
        Assert.Throws<OverflowException>(() => nearMaximumTick.Accumulate(ElapsedMilliseconds(3000)));
        Assert.Equal(beforeTickOverflow, Snapshot(nearMaximumTick));
    }

    [Fact]
    public void Cadence_source_has_no_host_execution_journal_state_configuration_or_engine_dependency()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "DomainSources");
        var sourcePath = Path.Combine(sourceRoot, "Runtime", "HostTickCadenceContracts.cs");
        Assert.True(File.Exists(sourcePath), sourcePath);
        var source = File.ReadAllText(sourcePath);

        Assert.All(
            new[]
            {
                "HostSession", "HostTickExecutionService", "HostStage", "IEventJournal", "ShiftRuntimeState",
                "QuotaRuntimeState", "AnomalyCatalog", "ShiftConfiguration", "UnityEngine", "Time.deltaTime",
                "Time.unscaledDeltaTime", "DateTime", "Stopwatch", "FishNet", "Steamworks", "Yaml"
            },
            forbidden => Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal));
        Assert.Contains("MillisecondsPerServerTick = 1000", source, StringComparison.Ordinal);
        Assert.Contains("checked", source, StringComparison.Ordinal);
        Assert.DoesNotContain("List<", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ImmutableArray", source, StringComparison.Ordinal);
    }

    private static AuthoritativeElapsedMilliseconds ElapsedMilliseconds(long value) => AuthoritativeElapsedMilliseconds.FromMilliseconds(value);

    private static HostTickCadence RunWithoutRetiring(IEnumerable<long> elapsedHistory)
    {
        var cadence = new HostTickCadence();
        foreach (var elapsed in elapsedHistory)
        {
            cadence.Accumulate(ElapsedMilliseconds(elapsed));
        }

        return cadence;
    }

    private static long[] SplitEvenly(long totalMilliseconds, int parts)
    {
        var quotient = totalMilliseconds / parts;
        var remainder = totalMilliseconds % parts;
        return Enumerable.Range(0, parts).Select(index => quotient + (index < remainder ? 1 : 0)).ToArray();
    }

    private static void AssertDue(HostTickCadence cadence, long first, long last, long count)
    {
        Assert.Equal(count, cadence.DueTickCount);
        var due = cadence.GetDueTickRange();
        Assert.True(due.HasValue);
        Assert.Equal(ServerTick.From(first), due.Value.First);
        Assert.Equal(ServerTick.From(last), due.Value.Last);
        Assert.Equal(count, due.Value.Count);
    }

    private static CadenceSnapshot Snapshot(HostTickCadence cadence)
    {
        var due = cadence.GetDueTickRange();
        return new CadenceSnapshot(
            cadence.RemainderMilliseconds,
            cadence.DueTickCount,
            due?.First,
            due?.Last,
            due?.Count ?? 0);
    }

    private static string CadenceProjection()
    {
        var cadence = new HostTickCadence();
        var projection = new StringBuilder();
        foreach (var elapsed in new long[] { 400, 599, 1, 2000, 2500, 0, 1000 })
        {
            cadence.Accumulate(ElapsedMilliseconds(elapsed));
            var due = cadence.GetDueTickRange();
            projection.Append(elapsed.ToString(CultureInfo.InvariantCulture));
            projection.Append('|');
            projection.Append(cadence.RemainderMilliseconds.Value.ToString(CultureInfo.InvariantCulture));
            projection.Append('|');
            projection.Append(cadence.DueTickCount.ToString(CultureInfo.InvariantCulture));
            projection.Append('|');
            projection.Append(due is null
                ? "-"
                : $"{due.Value.First.Value.ToString(CultureInfo.InvariantCulture)}-{due.Value.Last.Value.ToString(CultureInfo.InvariantCulture)}");
            projection.Append('\n');
        }

        return projection.ToString();
    }

    private sealed record CadenceSnapshot(
        AuthoritativeElapsedMilliseconds Remainder,
        long DueTickCount,
        ServerTick? FirstDueTick,
        ServerTick? LastDueTick,
        long DueRangeCount);
}
