using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Runtime;

public enum ShiftCompletionReason
{
    AllLogsTerminal,
    HardDeadline,
    AllLogsTerminalAtHardDeadline
}

/// <summary>Separate immutable lifecycle state for one configured shift and selected profile.</summary>
public sealed class ShiftLifecycleRuntimeState
{
    private ShiftLifecycleRuntimeState(
        ShiftId shiftId,
        ProfileId selectedProfileId,
        ServerTick startedAt,
        SimulationDuration hardDeadlineDuration,
        ServerTick hardDeadlineAt,
        ShiftCompletionEvidence? completion)
    {
        ShiftId = shiftId;
        SelectedProfileId = selectedProfileId;
        StartedAt = startedAt;
        HardDeadlineDuration = hardDeadlineDuration;
        HardDeadlineAt = hardDeadlineAt;
        Completion = completion;
    }

    public ShiftId ShiftId { get; }
    public ProfileId SelectedProfileId { get; }
    public ServerTick StartedAt { get; }
    public SimulationDuration HardDeadlineDuration { get; }
    public ServerTick HardDeadlineAt { get; }
    public bool IsCompleted => Completion is not null;
    public ShiftCompletionEvidence? Completion { get; }

    public static ShiftLifecycleRuntimeState Create(ShiftConfiguration configuration, ProfileId selectedProfileId)
    {
        var profile = ShiftCompletionValidation.ValidateConfiguration(configuration, selectedProfileId);
        var duration = ShiftCompletionValidation.DeriveDuration(profile);
        var startedAt = ServerTick.Zero;
        var hardDeadlineAt = checked(startedAt + duration);
        return new ShiftLifecycleRuntimeState(configuration.ShiftId, selectedProfileId, startedAt, duration, hardDeadlineAt, null);
    }

    public bool ValueEquals(ShiftLifecycleRuntimeState? other) =>
        other is not null &&
        ShiftId == other.ShiftId &&
        SelectedProfileId == other.SelectedProfileId &&
        StartedAt == other.StartedAt &&
        HardDeadlineDuration == other.HardDeadlineDuration &&
        HardDeadlineAt == other.HardDeadlineAt &&
        (Completion is null ? other.Completion is null : Completion.ValueEquals(other.Completion!));

    internal ShiftLifecycleRuntimeState Complete(ShiftCompletionEvidence completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        if (Completion is not null)
        {
            throw new InvalidOperationException("A completed shift lifecycle cannot be completed again.");
        }

        return new ShiftLifecycleRuntimeState(ShiftId, SelectedProfileId, StartedAt, HardDeadlineDuration, HardDeadlineAt, completion);
    }
}

/// <summary>Source-derived quota target and progress values retained with a completed lifecycle.</summary>
public sealed class ShiftQuotaProgressSummary
{
    internal ShiftQuotaProgressSummary(QuotaRuntimeState quota)
    {
        ArgumentNullException.ThrowIfNull(quota);
        TargetTotal = quota.TargetTotal;
        TargetBySpecies = quota.TargetBySpecies;
        MinimumCorrectlyProcessedAnomalies = quota.MinimumCorrectlyProcessedAnomalies;
        TotalCreditedUnits = quota.TotalCreditedUnits;
        CreditedBySpecies = quota.CreditedBySpecies;
        CorrectlyProcessedAnomalies = quota.CorrectlyProcessedAnomalies;
    }

    public int TargetTotal { get; }
    public ImmutableDictionary<SpeciesId, int> TargetBySpecies { get; }
    public int MinimumCorrectlyProcessedAnomalies { get; }
    public int TotalCreditedUnits { get; }
    public ImmutableDictionary<SpeciesId, int> CreditedBySpecies { get; }
    public int CorrectlyProcessedAnomalies { get; }

    internal bool ValueEquals(ShiftQuotaProgressSummary other) =>
        TargetTotal == other.TargetTotal &&
        MinimumCorrectlyProcessedAnomalies == other.MinimumCorrectlyProcessedAnomalies &&
        TotalCreditedUnits == other.TotalCreditedUnits &&
        CorrectlyProcessedAnomalies == other.CorrectlyProcessedAnomalies &&
        ShiftCompletionValidation.DictionariesEqual(TargetBySpecies, other.TargetBySpecies) &&
        ShiftCompletionValidation.DictionariesEqual(CreditedBySpecies, other.CreditedBySpecies);
}

/// <summary>Immutable final evidence retained only after a source-derived completion decision.</summary>
public sealed class ShiftCompletionEvidence
{
    internal ShiftCompletionEvidence(
        ServerTick completedAt,
        ServerTick hardDeadlineAt,
        ShiftCompletionReason reason,
        bool allLogsTerminal,
        bool hardDeadlineReached,
        bool objectivesSatisfied,
        int processedCount,
        int writtenOffCount,
        ShiftRuntimeState finalShiftState,
        QuotaRuntimeState finalQuotaState)
    {
        ArgumentNullException.ThrowIfNull(finalShiftState);
        ArgumentNullException.ThrowIfNull(finalQuotaState);
        if (completedAt.IsDefault || hardDeadlineAt.IsDefault || processedCount < 0 || writtenOffCount < 0)
        {
            throw new ArgumentException("Completion evidence requires initialized timing and non-negative terminal counts.");
        }

        CompletedAt = completedAt;
        HardDeadlineAt = hardDeadlineAt;
        Reason = reason;
        AllLogsTerminal = allLogsTerminal;
        HardDeadlineReached = hardDeadlineReached;
        ObjectivesSatisfied = objectivesSatisfied;
        ProcessedCount = processedCount;
        WrittenOffCount = writtenOffCount;
        FinalShiftState = finalShiftState;
        FinalQuotaState = finalQuotaState;
        Quota = new ShiftQuotaProgressSummary(finalQuotaState);
    }

    public ServerTick CompletedAt { get; }
    public ServerTick HardDeadlineAt { get; }
    public ShiftCompletionReason Reason { get; }
    public bool AllLogsTerminal { get; }
    public bool HardDeadlineReached { get; }
    public bool ObjectivesSatisfied { get; }
    public int ProcessedCount { get; }
    public int WrittenOffCount { get; }
    public ShiftRuntimeState FinalShiftState { get; }
    public QuotaRuntimeState FinalQuotaState { get; }
    public ShiftQuotaProgressSummary Quota { get; }

    internal bool ValueEquals(ShiftCompletionEvidence other) =>
        CompletedAt == other.CompletedAt &&
        HardDeadlineAt == other.HardDeadlineAt &&
        Reason == other.Reason &&
        AllLogsTerminal == other.AllLogsTerminal &&
        HardDeadlineReached == other.HardDeadlineReached &&
        ObjectivesSatisfied == other.ObjectivesSatisfied &&
        ProcessedCount == other.ProcessedCount &&
        WrittenOffCount == other.WrittenOffCount &&
        FinalShiftState.ValueEquals(other.FinalShiftState) &&
        FinalQuotaState.ValueEquals(other.FinalQuotaState) &&
        Quota.ValueEquals(other.Quota);
}

public abstract record ShiftCompletionEvaluationResult
{
    protected internal ShiftCompletionEvaluationResult(
        ShiftLifecycleRuntimeState lifecycle,
        ShiftRuntimeState shiftState,
        QuotaRuntimeState quotaState)
    {
        Lifecycle = lifecycle;
        ShiftState = shiftState;
        QuotaState = quotaState;
    }

    public ShiftLifecycleRuntimeState Lifecycle { get; }
    public ShiftRuntimeState ShiftState { get; }
    public QuotaRuntimeState QuotaState { get; }
}

public sealed record ShiftCompletionActive : ShiftCompletionEvaluationResult
{
    internal ShiftCompletionActive(
        ShiftLifecycleRuntimeState lifecycle,
        ShiftRuntimeState shiftState,
        QuotaRuntimeState quotaState,
        bool allLogsTerminal,
        bool hardDeadlineReached)
        : base(lifecycle, shiftState, quotaState)
    {
        AllLogsTerminal = allLogsTerminal;
        HardDeadlineReached = hardDeadlineReached;
    }

    public bool AllLogsTerminal { get; }
    public bool HardDeadlineReached { get; }
}

public sealed record ShiftCompletionNewlyCompleted : ShiftCompletionEvaluationResult
{
    internal ShiftCompletionNewlyCompleted(ShiftLifecycleRuntimeState lifecycle, ShiftRuntimeState shiftState, QuotaRuntimeState quotaState)
        : base(lifecycle, shiftState, quotaState)
    {
    }

    public ShiftCompletionEvidence Completion => Lifecycle.Completion ?? throw new InvalidOperationException("A newly completed result requires completion evidence.");
}

public sealed record ShiftCompletionAlreadyCompleted : ShiftCompletionEvaluationResult
{
    internal ShiftCompletionAlreadyCompleted(ShiftLifecycleRuntimeState lifecycle, ShiftCompletionEvidence completion)
        : base(lifecycle, completion.FinalShiftState, completion.FinalQuotaState)
    {
    }

    public ShiftCompletionEvidence Completion => Lifecycle.Completion ?? throw new InvalidOperationException("An already-completed result requires completion evidence.");
}

/// <summary>Pure, fail-closed completion evaluator. The future host owns when to call it.</summary>
public sealed class ShiftCompletionEvaluationService
{
    public ShiftCompletionEvaluationResult Evaluate(
        ShiftLifecycleRuntimeState lifecycle,
        ShiftRuntimeState shift,
        QuotaRuntimeState quota,
        ServerTick currentTick,
        ShiftConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(shift);
        ArgumentNullException.ThrowIfNull(quota);
        ArgumentNullException.ThrowIfNull(configuration);

        if (lifecycle.Completion is { } existing)
        {
            return new ShiftCompletionAlreadyCompleted(lifecycle, existing);
        }

        ShiftCompletionValidation.ValidateActiveCorrelation(lifecycle, shift, quota, currentTick, configuration);
        if (currentTick > lifecycle.HardDeadlineAt)
        {
            throw new InvalidOperationException("An active shift lifecycle cannot be evaluated after its hard deadline.");
        }

        var processedCount = 0;
        var writtenOffCount = 0;
        foreach (var log in shift.Logs)
        {
            if (log.State == LogState.PROCESSED)
            {
                processedCount++;
            }
            else if (log.State == LogState.HELD_WRITTEN_OFF)
            {
                writtenOffCount++;
            }
        }

        var allLogsTerminal = processedCount + writtenOffCount == shift.Logs.Length;
        var hardDeadlineReached = currentTick == lifecycle.HardDeadlineAt;
        if (!allLogsTerminal && !hardDeadlineReached)
        {
            return new ShiftCompletionActive(lifecycle, shift, quota, false, false);
        }

        var reason = hardDeadlineReached
            ? allLogsTerminal ? ShiftCompletionReason.AllLogsTerminalAtHardDeadline : ShiftCompletionReason.HardDeadline
            : ShiftCompletionReason.AllLogsTerminal;
        var evidence = new ShiftCompletionEvidence(
            currentTick,
            lifecycle.HardDeadlineAt,
            reason,
            allLogsTerminal,
            hardDeadlineReached,
            quota.ObjectivesSatisfied,
            processedCount,
            writtenOffCount,
            shift,
            quota);
        var completedLifecycle = lifecycle.Complete(evidence);
        return new ShiftCompletionNewlyCompleted(completedLifecycle, shift, quota);
    }
}

internal static class ShiftCompletionValidation
{
    internal static ShiftProfile ValidateConfiguration(ShiftConfiguration configuration, ProfileId selectedProfileId)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.ShiftId.IsDefault || selectedProfileId.IsDefault)
        {
            throw new ArgumentException("Shift and selected-profile identities must be initialized.");
        }

        ArgumentNullException.ThrowIfNull(configuration.Profiles);
        if (configuration.Profiles.IsEmpty)
        {
            throw new ArgumentException("Shift configuration must contain profiles.", nameof(configuration));
        }

        foreach (var pair in configuration.Profiles)
        {
            if (pair.Key.IsDefault || pair.Value is null)
            {
                throw new ArgumentException("Shift profiles must retain initialized identities and values.", nameof(configuration));
            }

            if (pair.Value.IntakeTimeoutSeconds <= 0 || pair.Value.HardShiftDeadlineSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration), "Shift profile durations must be positive.");
            }
        }

        if (!configuration.Profiles.TryGetValue(selectedProfileId, out var selectedProfile) || selectedProfile is null)
        {
            throw new ArgumentException("The selected profile must exist exactly in the configuration.", nameof(selectedProfileId));
        }

        ArgumentNullException.ThrowIfNull(configuration.Supply);
        if (configuration.Manifest.IsDefaultOrEmpty || configuration.Supply.Total != configuration.Manifest.Length || configuration.Supply.FreeWriteoffBuffer < 0)
        {
            throw new ArgumentException("Shift supply evidence must agree with the exact manifest.", nameof(configuration));
        }

        var manifestIds = ImmutableHashSet.CreateBuilder<LogId>();
        foreach (var log in configuration.Manifest)
        {
            if (log is null || log.Id.IsDefault || log.TrueSpecies.IsDefault || log.DeclaredSpecies.IsDefault || (log.Anomaly is { } anomaly && anomaly.IsDefault) || !manifestIds.Add(log.Id))
            {
                throw new ArgumentException("Shift manifest evidence is malformed.", nameof(configuration));
            }
        }

        _ = ShiftRuntimeState.Create(configuration);
        _ = QuotaRuntimeState.Create(configuration);
        return selectedProfile;
    }

    internal static SimulationDuration DeriveDuration(ShiftProfile profile)
    {
        if (profile.HardShiftDeadlineSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(profile), "Hard deadline must be positive.");
        }

        return SimulationDuration.FromTicks(checked((long)profile.HardShiftDeadlineSeconds));
    }

    internal static void ValidateActiveCorrelation(
        ShiftLifecycleRuntimeState lifecycle,
        ShiftRuntimeState shift,
        QuotaRuntimeState quota,
        ServerTick currentTick,
        ShiftConfiguration configuration)
    {
        if (currentTick.IsDefault)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Current completion tick must be initialized.");
        }

        if (lifecycle.StartedAt.IsDefault || lifecycle.HardDeadlineDuration.IsDefault || lifecycle.HardDeadlineAt.IsDefault || currentTick < lifecycle.StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Completion tick must not precede an initialized lifecycle start.");
        }

        var selectedProfile = ValidateConfiguration(configuration, lifecycle.SelectedProfileId);
        var expectedDuration = DeriveDuration(selectedProfile);
        var expectedDeadline = checked(lifecycle.StartedAt + expectedDuration);
        if (lifecycle.ShiftId.IsDefault || lifecycle.ShiftId != shift.ShiftId || lifecycle.ShiftId != configuration.ShiftId || lifecycle.HardDeadlineDuration != expectedDuration || lifecycle.HardDeadlineAt != expectedDeadline)
        {
            throw new InvalidOperationException("Lifecycle, runtime, and configuration completion evidence must agree exactly.");
        }

        if (shift.StateVersion.IsDefault)
        {
            throw new InvalidOperationException("Shift runtime state must retain an initialized state version.");
        }

        ValidateManifestCorrelation(shift, configuration);
        ValidateQuotaCorrelation(shift, quota, configuration);
        ValidateActiveSawCorrelation(shift);
    }

    internal static bool DictionariesEqual(ImmutableDictionary<SpeciesId, int> first, ImmutableDictionary<SpeciesId, int> second) =>
        first.Count == second.Count && first.All(pair => second.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static void ValidateManifestCorrelation(ShiftRuntimeState shift, ShiftConfiguration configuration)
    {
        if (shift.Logs.IsDefault || shift.Logs.Length != configuration.Manifest.Length)
        {
            throw new InvalidOperationException("Runtime logs must match the exact configuration manifest length.");
        }

        for (var index = 0; index < configuration.Manifest.Length; index++)
        {
            var manifest = configuration.Manifest[index];
            var runtime = shift.Logs[index];
            if (runtime is null || runtime.LogId != manifest.Id || runtime.TrueSpecies != manifest.TrueSpecies || runtime.DeclaredSpecies != manifest.DeclaredSpecies || runtime.Anomaly != manifest.Anomaly)
            {
                throw new InvalidOperationException("Runtime log evidence must match the exact configuration manifest order and identity.");
            }
        }
    }

    private static void ValidateQuotaCorrelation(ShiftRuntimeState shift, QuotaRuntimeState quota, ShiftConfiguration configuration)
    {
        var objectives = configuration.Objectives;
        if (objectives is null || objectives.Quota is null || objectives.Quota.BySpecies is null || quota.TargetTotal != objectives.Quota.Total || quota.MinimumCorrectlyProcessedAnomalies != objectives.MinCorrectlyProcessedAnomalies || !DictionariesEqual(quota.TargetBySpecies, objectives.Quota.BySpecies))
        {
            throw new InvalidOperationException("Quota targets must match configuration objectives exactly.");
        }

        var processed = ImmutableHashSet.CreateBuilder<LogId>();
        foreach (var log in shift.Logs)
        {
            if (log.State == LogState.PROCESSED)
            {
                processed.Add(log.LogId);
                if (!quota.SettledLogIds.Contains(log.LogId))
                {
                    throw new InvalidOperationException("Every processed log must have settled quota evidence.");
                }
            }
            else if (quota.SettledLogIds.Contains(log.LogId))
            {
                throw new InvalidOperationException("Only processed manifest logs may have settled quota evidence.");
            }
        }

        if (!quota.SettledLogIds.SetEquals(processed))
        {
            throw new InvalidOperationException("Settled quota identities must exactly equal processed runtime identities.");
        }
    }

    private static void ValidateActiveSawCorrelation(ShiftRuntimeState shift)
    {
        var sawOwners = shift.Logs.Count(log => log.State == LogState.IN_SAW);
        if (shift.ActiveSawCycle is not { } active)
        {
            if (sawOwners != 0)
            {
                throw new InvalidOperationException("An in-saw runtime log requires active saw ownership evidence.");
            }

            return;
        }

        if (active.LogId.IsDefault || active.StartedAt.IsDefault || active.Duration.IsDefault || active.Duration <= SimulationDuration.Zero || active.DueAt != active.StartedAt + active.Duration || !shift.TryGetLog(active.LogId, out var owner) || owner.State != LogState.IN_SAW || sawOwners != 1)
        {
            throw new InvalidOperationException("Active saw ownership must retain exactly one in-saw runtime log.");
        }
    }
}
