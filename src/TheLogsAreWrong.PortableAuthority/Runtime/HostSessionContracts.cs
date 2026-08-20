using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Anomalies;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Journal;
using TheLogsAreWrong.Domain.Line;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Quota;
using TheLogsAreWrong.Domain.Scheduler;
using TheLogsAreWrong.Domain.Time;

namespace TheLogsAreWrong.Domain.Runtime;

/// <summary>
/// Plain-C# authoritative continuity boundary for one explicitly requested host tick at a time.
/// It transports only state returned by the single shared <see cref="HostTickExecutionService"/>;
/// it does not schedule time or recreate any host-stage semantics.
/// </summary>
public sealed class HostSession : IDisposable
{
    private readonly HostTickExecutionService _hostTick = new();
    private readonly ShiftConfiguration _shiftConfiguration;
    private readonly AnomalyCatalog _anomalyCatalog;
    private readonly IAtomicEventJournal _journal;
    private ShiftRuntimeState _shiftState;
    private QuotaRuntimeState _quotaState;
    private MovementNoiseRuntimeState _movementNoise;
    private LineNoiseRuntimeState _lineNoise;
    private HostTickProgressionEvidence _progression;
    private ShiftLifecycleRuntimeState _lifecycle;
    private ServerTick? _lastSuccessfulTick;
    private bool _isExecuting;
    private bool _isDisposed;

    public HostSession(
        ShiftConfiguration shiftConfiguration,
        AnomalyCatalog anomalyCatalog,
        ProfileId selectedProfileId)
        : this(
            shiftConfiguration,
            anomalyCatalog,
            selectedProfileId,
            CreateJournal(shiftConfiguration))
    {
    }

    /// <summary>
    /// Creates a new session over an empty journal for the supplied initialized shift. The journal is
    /// owned by the session after construction; restore/resume composition is intentionally outside this increment.
    /// </summary>
    public HostSession(
        ShiftConfiguration shiftConfiguration,
        AnomalyCatalog anomalyCatalog,
        ProfileId selectedProfileId,
        IAtomicEventJournal journal)
    {
        if (shiftConfiguration is null) { throw new ArgumentNullException(nameof(shiftConfiguration)); }
        if (anomalyCatalog is null) { throw new ArgumentNullException(nameof(anomalyCatalog)); }
        if (selectedProfileId.IsDefault) { throw new ArgumentException("Selected profile must be initialized.", nameof(selectedProfileId)); }
        if (journal is null) { throw new ArgumentNullException(nameof(journal)); }
        if (shiftConfiguration.ShiftId.IsDefault || journal.Shift != shiftConfiguration.ShiftId ||
            journal.Count != 0 || journal.LastSequence != EventSequence.None ||
            journal.LastTick != ServerTick.Zero || journal.LastStateVersion != StateVersion.Zero)
        {
            throw new ArgumentException("A new host session requires an empty journal for the exact configured shift.", nameof(journal));
        }

        _shiftConfiguration = shiftConfiguration;
        _anomalyCatalog = anomalyCatalog;
        _journal = journal;
        _shiftState = ShiftRuntimeState.Create(shiftConfiguration);
        _quotaState = QuotaRuntimeState.Create(shiftConfiguration);
        _movementNoise = MovementNoiseRuntimeState.Create(shiftConfiguration.ShiftId);
        _lineNoise = LineNoiseRuntimeState.Create(shiftConfiguration.ShiftId);
        _progression = HostTickProgressionEvidence.Create(shiftConfiguration.ShiftId);
        _lifecycle = ShiftLifecycleRuntimeState.Create(shiftConfiguration, selectedProfileId);
    }

    public ShiftRuntimeState ShiftState => _shiftState;
    public QuotaRuntimeState QuotaState => _quotaState;
    public MovementNoiseRuntimeState MovementNoise => _movementNoise;
    public LineNoiseRuntimeState LineNoise => _lineNoise;
    public HostTickProgressionEvidence Progression => _progression;
    public ShiftLifecycleRuntimeState Lifecycle => _lifecycle;
    public IAtomicEventJournal Journal => _journal;
    public int SuccessfulTickCount { get; private set; }
    public bool IsDisposed => _isDisposed;

    /// <summary>
    /// Executes exactly one explicitly supplied authoritative tick. The session accepts only an already-admitted
    /// per-tick batch and active-tool evidence; publication identities remain inside the shared authority.
    /// </summary>
    public HostStageSevenEventExecution ExecuteTick(
        ServerTick currentTick,
        AcceptedIntentTickBatch acceptedIntents,
        ImmutableHashSet<ItemId> activeTools)
    {
        ThrowIfDisposed();
        if (_isExecuting)
        {
            throw new InvalidOperationException("A host session cannot execute reentrantly.");
        }

        ValidateContinuity(currentTick, acceptedIntents, activeTools);
        _isExecuting = true;
        try
        {
            var execution = _hostTick.Execute(
                _shiftState,
                _quotaState,
                _movementNoise,
                _lineNoise,
                _progression,
                _lifecycle,
                acceptedIntents,
                activeTools,
                _journal,
                currentTick,
                _shiftConfiguration.Scheduler,
                _shiftConfiguration,
                _shiftConfiguration.Containment,
                _anomalyCatalog);

            if (execution.Checkpoint is not HostTickCheckpointAdvanced advanced)
            {
                throw new HostSessionTickRejectedException(execution.Checkpoint);
            }

            CarryAcceptedResult(execution, advanced, currentTick);
            return execution;
        }
        finally
        {
            _isExecuting = false;
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
    }

    private static IAtomicEventJournal CreateJournal(ShiftConfiguration shiftConfiguration)
    {
        if (shiftConfiguration is null) { throw new ArgumentNullException(nameof(shiftConfiguration)); }
        return new InMemoryEventJournal(shiftConfiguration.ShiftId);
    }

    private void ValidateContinuity(
        ServerTick currentTick,
        AcceptedIntentTickBatch acceptedIntents,
        ImmutableHashSet<ItemId> activeTools)
    {
        if (currentTick.IsDefault) { throw new ArgumentException("Current tick must be initialized.", nameof(currentTick)); }
        if (acceptedIntents is null) { throw new ArgumentNullException(nameof(acceptedIntents)); }
        if (activeTools is null) { throw new ArgumentNullException(nameof(activeTools)); }
        if (acceptedIntents.ShiftId != _shiftState.ShiftId || acceptedIntents.CurrentTick != currentTick)
        {
            throw new ArgumentException("Per-tick input must belong to the current session shift and exact requested tick.", nameof(acceptedIntents));
        }

        if (_lastSuccessfulTick is { } prior)
        {
            if (!prior.TrySubtract(ServerTick.Zero, out var priorDuration) ||
                priorDuration.Value == long.MaxValue ||
                currentTick != prior + SimulationDuration.FromTicks(1))
            {
                throw new InvalidOperationException("A host session requires one exact consecutive authoritative tick after its accepted cursor.");
            }
        }
        else if (currentTick != ServerTick.Zero)
        {
            throw new InvalidOperationException("A new host session must begin at authoritative tick zero.");
        }
    }

    private void CarryAcceptedResult(
        HostStageSevenEventExecution execution,
        HostTickCheckpointAdvanced advanced,
        ServerTick currentTick)
    {
        _shiftState = execution.FinalShiftState;
        _quotaState = execution.FinalQuotaState;
        _movementNoise = execution.StageSix.FinalMovementNoise;
        _lineNoise = execution.FinalLineNoise;
        _progression = advanced.Progression;
        _lifecycle = advanced.Receipt.Lifecycle;
        _lastSuccessfulTick = currentTick;
        SuccessfulTickCount = checked(SuccessfulTickCount + 1);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(HostSession));
        }
    }
}

/// <summary>Reports a shared host-tick rejection without advancing session-carried state.</summary>
public sealed class HostSessionTickRejectedException : InvalidOperationException
{
    internal HostSessionTickRejectedException(HostTickCheckpointResult checkpoint)
        : base("The shared host tick was not accepted by the session continuity boundary.")
    {
        Checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
    }

    public HostTickCheckpointResult Checkpoint { get; }
}
