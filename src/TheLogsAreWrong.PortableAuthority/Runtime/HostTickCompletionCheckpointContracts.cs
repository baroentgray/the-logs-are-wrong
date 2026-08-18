using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Runtime;

public enum HostTickCheckpointRejectionReason
{
    BackwardTick,
    SkippedTick,
    ContradictoryReplay,
    ShiftCompleted
}

/// <summary>Immutable one-tick progression evidence with only the immediately preceding replay receipt.</summary>
public sealed class HostTickProgressionEvidence
{
    private HostTickProgressionEvidence(
        ShiftId shiftId,
        ServerTick initialTick,
        ServerTick? lastCompletedTick,
        HostTickCompletionReceipt? lastReceipt)
    {
        ShiftId = shiftId;
        InitialTick = initialTick;
        LastCompletedTick = lastCompletedTick;
        LastReceipt = lastReceipt;
        Validate();
    }

    public ShiftId ShiftId { get; }
    public ServerTick InitialTick { get; }
    public bool HasCompletedTick => LastCompletedTick is not null;
    public ServerTick? LastCompletedTick { get; }
    public HostTickCompletionReceipt? LastReceipt { get; }

    public static HostTickProgressionEvidence Create(ShiftId shiftId)
    {
        if (shiftId.IsDefault)
        {
            throw new ArgumentException("Progression evidence requires an initialized shift identity.", nameof(shiftId));
        }

        return new HostTickProgressionEvidence(shiftId, ServerTick.Zero, null, null);
    }

    internal HostTickProgressionEvidence Advance(HostTickCompletionReceipt receipt)
    {
        if (receipt is null) { throw new ArgumentNullException("receipt"); }
        Validate();
        receipt.Validate();
        if (receipt.Lifecycle.ShiftId != ShiftId || receipt.ShiftState.ShiftId != ShiftId)
        {
            throw new InvalidOperationException("Checkpoint receipt identity must match its progression shift.");
        }

        return new HostTickProgressionEvidence(ShiftId, InitialTick, receipt.CompletedTick, receipt);
    }

    internal void Validate()
    {
        if (ShiftId.IsDefault || InitialTick.IsDefault || InitialTick != ServerTick.Zero)
        {
            throw new InvalidOperationException("Progression evidence requires an initialized shift and the exact zero initial tick.");
        }

        var hasTick = LastCompletedTick is not null;
        var hasReceipt = LastReceipt is not null;
        if (hasTick != hasReceipt)
        {
            throw new InvalidOperationException("Progression evidence must retain both the last completed tick and its receipt, or neither.");
        }

        if (!hasTick)
        {
            return;
        }

        var completedTick = LastCompletedTick!.Value;
        if (completedTick.IsDefault)
        {
            throw new InvalidOperationException("Progression evidence must retain an initialized completed tick.");
        }

        LastReceipt!.Validate();
        if (LastReceipt.CompletedTick != completedTick || LastReceipt.Lifecycle.ShiftId != ShiftId || LastReceipt.ShiftState.ShiftId != ShiftId)
        {
            throw new InvalidOperationException("Progression evidence and its retained receipt must agree exactly.");
        }
    }
}

/// <summary>Immutable, service-created evidence for exactly one successfully evaluated checkpoint.</summary>
public sealed class HostTickCompletionReceipt
{
    private HostTickCompletionReceipt(ServerTick completedTick, ShiftCompletionEvaluationResult evaluation)
    {
        CompletedTick = completedTick;
        Evaluation = evaluation;
        Validate();
    }

    public ServerTick CompletedTick { get; }
    public ShiftCompletionEvaluationResult Evaluation { get; }
    public ShiftLifecycleRuntimeState Lifecycle => Evaluation.Lifecycle;
    public ShiftRuntimeState ShiftState => Evaluation.ShiftState;
    public QuotaRuntimeState QuotaState => Evaluation.QuotaState;
    public bool ShiftCompleted => Lifecycle.IsCompleted;

    internal static HostTickCompletionReceipt Create(ServerTick completedTick, ShiftCompletionEvaluationResult evaluation) =>
        new(completedTick, evaluation);

    internal void Validate()
    {
        if (CompletedTick.IsDefault || Evaluation is null || Lifecycle is null || ShiftState is null || QuotaState is null)
        {
            throw new InvalidOperationException("Checkpoint receipt requires initialized completion and runtime evidence.");
        }

        switch (Evaluation)
        {
            case ShiftCompletionActive active:
                if (active.Lifecycle.IsCompleted || active.AllLogsTerminal || active.HardDeadlineReached)
                {
                    throw new InvalidOperationException("An active checkpoint receipt cannot retain completion evidence.");
                }

                break;

            case ShiftCompletionNewlyCompleted completed:
                if (!completed.Lifecycle.IsCompleted || completed.Completion.CompletedAt != CompletedTick ||
                    !ReferenceEquals(completed.Completion.FinalShiftState, completed.ShiftState) ||
                    !ReferenceEquals(completed.Completion.FinalQuotaState, completed.QuotaState))
                {
                    throw new InvalidOperationException("A completed checkpoint receipt must retain exact final completion evidence.");
                }

                break;

            default:
                throw new InvalidOperationException("A new checkpoint receipt cannot retain an already-completed evaluation result.");
        }
    }
}

public abstract class HostTickCheckpointResult
{
    private protected HostTickCheckpointResult()
    {
    }
}

public sealed class HostTickCheckpointAdvanced : HostTickCheckpointResult
{
    internal HostTickCheckpointAdvanced(
        HostTickProgressionEvidence originalProgression,
        HostTickProgressionEvidence progression,
        HostTickCompletionReceipt receipt)
    {
        OriginalProgression = originalProgression;
        Progression = progression;
        Receipt = receipt;
    }

    public HostTickProgressionEvidence OriginalProgression { get; }
    public HostTickProgressionEvidence Progression { get; }
    public HostTickCompletionReceipt Receipt { get; }
}

public sealed class HostTickCheckpointReplayed : HostTickCheckpointResult
{
    internal HostTickCheckpointReplayed(HostTickProgressionEvidence progression, HostTickCompletionReceipt receipt)
    {
        Progression = progression;
        Receipt = receipt;
    }

    public HostTickProgressionEvidence Progression { get; }
    public HostTickCompletionReceipt Receipt { get; }
}

public sealed class HostTickCheckpointRejected : HostTickCheckpointResult
{
    internal HostTickCheckpointRejected(
        HostTickProgressionEvidence progression,
        ServerTick requestedTick,
        HostTickCheckpointRejectionReason reason)
    {
        Progression = progression;
        RequestedTick = requestedTick;
        Reason = reason;
    }

    public HostTickProgressionEvidence Progression { get; }
    public ServerTick RequestedTick { get; }
    public HostTickCheckpointRejectionReason Reason { get; }
}

/// <summary>Validates one exact post-stage-six host tick before event sequencing can proceed.</summary>
public sealed class HostTickCompletionCheckpointService
{
    private readonly ShiftCompletionEvaluationService _evaluationService = new();

    /// <summary>
    /// Completes the checkpoint for the exact <paramref name="currentTick"/> using the shift and quota states
    /// produced after all applicable host work in stages one through six for that tick.
    /// </summary>
    public HostTickCheckpointResult Complete(
        HostTickProgressionEvidence progression,
        ShiftLifecycleRuntimeState lifecycle,
        ShiftRuntimeState postStageShift,
        QuotaRuntimeState postStageQuota,
        ServerTick currentTick,
        ShiftConfiguration configuration)
    {
        if (progression is null) { throw new ArgumentNullException("progression"); }
        if (lifecycle is null) { throw new ArgumentNullException("lifecycle"); }
        if (postStageShift is null) { throw new ArgumentNullException("postStageShift"); }
        if (postStageQuota is null) { throw new ArgumentNullException("postStageQuota"); }
        if (configuration is null) { throw new ArgumentNullException("configuration"); }
        if (currentTick.IsDefault)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Checkpoint tick must be initialized.");
        }

        progression.Validate();
        ValidateRuntimeIdentity(progression, lifecycle, postStageShift, configuration);

        if (progression.LastReceipt is { } previous)
        {
            var previousTick = progression.LastCompletedTick!.Value;
            if (currentTick == previousTick)
            {
                return ReferenceEquals(lifecycle, previous.Lifecycle) &&
                    ReferenceEquals(postStageShift, previous.ShiftState) &&
                    ReferenceEquals(postStageQuota, previous.QuotaState)
                    ? new HostTickCheckpointReplayed(progression, previous)
                    : new HostTickCheckpointRejected(progression, currentTick, HostTickCheckpointRejectionReason.ContradictoryReplay);
            }

            if (currentTick < previousTick)
            {
                return new HostTickCheckpointRejected(progression, currentTick, HostTickCheckpointRejectionReason.BackwardTick);
            }

            if (previous.ShiftCompleted)
            {
                return new HostTickCheckpointRejected(progression, currentTick, HostTickCheckpointRejectionReason.ShiftCompleted);
            }

            var expectedNext = NextTick(previousTick);
            if (currentTick > expectedNext)
            {
                return new HostTickCheckpointRejected(progression, currentTick, HostTickCheckpointRejectionReason.SkippedTick);
            }

            if (currentTick != expectedNext)
            {
                throw new InvalidOperationException("Checkpoint progression cannot accept an internally ambiguous tick.");
            }

            if (!ReferenceEquals(lifecycle, previous.Lifecycle))
            {
                throw new InvalidOperationException("A new checkpoint must continue from the exact prior published lifecycle.");
            }
        }
        else if (currentTick != ServerTick.Zero)
        {
            return new HostTickCheckpointRejected(progression, currentTick, HostTickCheckpointRejectionReason.SkippedTick);
        }

        if (lifecycle.IsCompleted)
        {
            throw new InvalidOperationException("A completed lifecycle requires its original checkpoint receipt.");
        }

        ShiftCompletionValidation.ValidateActiveCorrelation(lifecycle, postStageShift, postStageQuota, currentTick, configuration);
        if (currentTick > lifecycle.HardDeadlineAt)
        {
            throw new InvalidOperationException("An active lifecycle cannot publish a checkpoint after its hard deadline.");
        }

        var evaluation = _evaluationService.Evaluate(lifecycle, postStageShift, postStageQuota, currentTick, configuration);
        ValidateEvaluation(evaluation, lifecycle, postStageShift, postStageQuota, currentTick);
        var receipt = HostTickCompletionReceipt.Create(currentTick, evaluation);
        var advanced = progression.Advance(receipt);
        return new HostTickCheckpointAdvanced(progression, advanced, receipt);
    }

    private static ServerTick NextTick(ServerTick currentTick) =>
        checked(currentTick + SimulationDuration.FromTicks(1));

    private static void ValidateRuntimeIdentity(
        HostTickProgressionEvidence progression,
        ShiftLifecycleRuntimeState lifecycle,
        ShiftRuntimeState postStageShift,
        ShiftConfiguration configuration)
    {
        if (lifecycle.ShiftId.IsDefault || postStageShift.ShiftId.IsDefault || configuration.ShiftId.IsDefault ||
            lifecycle.ShiftId != progression.ShiftId || postStageShift.ShiftId != progression.ShiftId || configuration.ShiftId != progression.ShiftId)
        {
            throw new InvalidOperationException("Checkpoint progression, lifecycle, runtime, and configuration shifts must agree exactly.");
        }

        var profile = ShiftCompletionValidation.ValidateConfiguration(configuration, lifecycle.SelectedProfileId);
        var duration = ShiftCompletionValidation.DeriveDuration(profile);
        if (lifecycle.StartedAt.IsDefault || lifecycle.HardDeadlineDuration.IsDefault || lifecycle.HardDeadlineAt.IsDefault ||
            lifecycle.HardDeadlineDuration != duration || lifecycle.HardDeadlineAt != checked(lifecycle.StartedAt + duration))
        {
            throw new InvalidOperationException("Checkpoint lifecycle timing must agree exactly with the selected configuration profile.");
        }
    }

    private static void ValidateEvaluation(
        ShiftCompletionEvaluationResult evaluation,
        ShiftLifecycleRuntimeState inputLifecycle,
        ShiftRuntimeState postStageShift,
        QuotaRuntimeState postStageQuota,
        ServerTick currentTick)
    {
        if (evaluation is null) { throw new ArgumentNullException("evaluation"); }
        if (!ReferenceEquals(evaluation.ShiftState, postStageShift) || !ReferenceEquals(evaluation.QuotaState, postStageQuota))
        {
            throw new InvalidOperationException("Checkpoint evaluation must retain the exact supplied post-stage runtime references.");
        }

        switch (evaluation)
        {
            case ShiftCompletionActive active:
                if (!ReferenceEquals(active.Lifecycle, inputLifecycle) || active.Lifecycle.IsCompleted || active.AllLogsTerminal || active.HardDeadlineReached || currentTick >= inputLifecycle.HardDeadlineAt)
                {
                    throw new InvalidOperationException("An active checkpoint evaluation is invalid at this tick.");
                }

                return;

            case ShiftCompletionNewlyCompleted completed:
                if (ReferenceEquals(completed.Lifecycle, inputLifecycle) || !completed.Lifecycle.IsCompleted ||
                    completed.Completion.CompletedAt != currentTick ||
                    !ReferenceEquals(completed.Completion.FinalShiftState, postStageShift) ||
                    !ReferenceEquals(completed.Completion.FinalQuotaState, postStageQuota))
                {
                    throw new InvalidOperationException("A completed checkpoint evaluation must retain exact current-tick evidence.");
                }

                return;

            default:
                throw new InvalidOperationException("A new checkpoint cannot accept an already-completed evaluation result.");
        }
    }
}
