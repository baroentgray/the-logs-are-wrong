using System;
using System.Collections.Immutable;
using System.IO;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Gate3;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace TheLogsAreWrong.Gate2
{
    /// <summary>Observed lifecycle of the one Unity-held, plain-C# authoritative host session.</summary>
    public enum ProductionHostOwnerLifecycle
    {
        Unstarted,
        Running,
        Faulted,
        Disposed
    }

    /// <summary>Supplies non-negative integer elapsed evidence to the imported PortableAuthority cadence.</summary>
    public interface IAuthoritativeElapsedTimeSource
    {
        AuthoritativeElapsedMilliseconds SampleElapsedMilliseconds();

        /// <summary>Observes total elapsed milliseconds from the current host-session origin without consuming a delta.</summary>
        AuthoritativeElapsedMilliseconds ObserveElapsedMilliseconds();

        /// <summary>Establishes the next HostSession's fresh elapsed-time origin before cadence starts.</summary>
        void ResetSessionOrigin();
    }

    /// <summary>Minimal monotonic timestamp dependency used only by the exact integer bridge.</summary>
    public interface IMonotonicTimestampSource
    {
        long Frequency { get; }

        long GetTimestamp();
    }

    /// <summary>
    /// Converts monotonic timestamp deltas into exact integer milliseconds. The retained numerator remainder
    /// carries sub-millisecond time into later samples, so repeated truncation cannot lose simulation time.
    /// </summary>
    public sealed class StopwatchElapsedTimeSource : IAuthoritativeElapsedTimeSource
    {
        private readonly IMonotonicTimestampSource _timestamps;
        private long _originTimestamp;
        private long _lastTimestamp;
        private long _latestReadTimestamp;
        private long _millisecondNumeratorRemainder;

        public StopwatchElapsedTimeSource()
            : this(new SystemStopwatchTimestampSource())
        {
        }

        public StopwatchElapsedTimeSource(IMonotonicTimestampSource timestamps)
        {
            _timestamps = timestamps ?? throw new ArgumentNullException(nameof(timestamps));
            if (_timestamps.Frequency <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timestamps), "Monotonic timestamp frequency must be positive.");
            }

            ResetSessionOrigin();
        }

        public AuthoritativeElapsedMilliseconds SampleElapsedMilliseconds()
        {
            var current = _timestamps.GetTimestamp();
            if (current < _latestReadTimestamp)
            {
                throw new InvalidOperationException("The monotonic timestamp source moved backwards.");
            }

            var elapsedTicks = checked(current - _lastTimestamp);
            var wholeSeconds = elapsedTicks / _timestamps.Frequency;
            var fractionalTicks = elapsedTicks % _timestamps.Frequency;
            var fractionalMillisecondsNumerator = checked(fractionalTicks * HostTickCadence.MillisecondsPerServerTick + _millisecondNumeratorRemainder);
            var elapsedMilliseconds = checked(wholeSeconds * HostTickCadence.MillisecondsPerServerTick + fractionalMillisecondsNumerator / _timestamps.Frequency);

            _lastTimestamp = current;
            _latestReadTimestamp = current;
            _millisecondNumeratorRemainder = fractionalMillisecondsNumerator % _timestamps.Frequency;
            return AuthoritativeElapsedMilliseconds.FromMilliseconds(elapsedMilliseconds);
        }

        public AuthoritativeElapsedMilliseconds ObserveElapsedMilliseconds()
        {
            var current = _timestamps.GetTimestamp();
            if (current < _latestReadTimestamp)
            {
                throw new InvalidOperationException("The monotonic timestamp source moved backwards from a prior host-session read.");
            }

            var elapsedTicks = checked(current - _originTimestamp);
            var wholeSeconds = elapsedTicks / _timestamps.Frequency;
            var fractionalTicks = elapsedTicks % _timestamps.Frequency;
            var elapsedMilliseconds = checked(wholeSeconds * HostTickCadence.MillisecondsPerServerTick
                + checked(fractionalTicks * HostTickCadence.MillisecondsPerServerTick) / _timestamps.Frequency);
            _latestReadTimestamp = current;
            return AuthoritativeElapsedMilliseconds.FromMilliseconds(elapsedMilliseconds);
        }

        public void ResetSessionOrigin()
        {
            var origin = _timestamps.GetTimestamp();
            _originTimestamp = origin;
            _lastTimestamp = origin;
            _latestReadTimestamp = origin;
            _millisecondNumeratorRemainder = 0;
        }

        private sealed class SystemStopwatchTimestampSource : IMonotonicTimestampSource
        {
            public long Frequency => Stopwatch.Frequency;

            public long GetTimestamp() => Stopwatch.GetTimestamp();
        }
    }

    /// <summary>Already-admitted evidence for one exact tick; Unity neither sorts nor validates its semantics.</summary>
    public sealed class AlreadyAdmittedHostTickInput
    {
        public AlreadyAdmittedHostTickInput(AcceptedIntentTickBatch acceptedIntents, ImmutableHashSet<ItemId> activeTools)
        {
            AcceptedIntents = acceptedIntents ?? throw new ArgumentNullException(nameof(acceptedIntents));
            ActiveTools = activeTools ?? throw new ArgumentNullException(nameof(activeTools));
        }

        public AcceptedIntentTickBatch AcceptedIntents { get; }

        public ImmutableHashSet<ItemId> ActiveTools { get; }
    }

    /// <summary>Boundary for evidence that has already passed the future admission/ordering authority.</summary>
    public interface IAlreadyAdmittedHostInputSource
    {
        AlreadyAdmittedHostTickInput GetInput(ShiftId shiftId, ServerTick tick);
    }

    /// <summary>
    /// Optional TLAW-owned phase boundary for a final already-admitted input source. It has no sequencing,
    /// gameplay, or batch authority: it only prevents a due cadence tick from sealing while its exact
    /// authoritative receive-time window remains open.
    /// </summary>
    public interface IIngressBeforeSealHostInputSource
    {
        bool CanSeal(ShiftId shiftId, ServerTick tick);
    }

    /// <summary>Explicit Gate-2 bootstrap input: no gameplay admission is implemented in this increment.</summary>
    public sealed class EmptyAlreadyAdmittedHostInputSource : IAlreadyAdmittedHostInputSource
    {
        public AlreadyAdmittedHostTickInput GetInput(ShiftId shiftId, ServerTick tick)
        {
            return new AlreadyAdmittedHostTickInput(
                AcceptedIntentTickBatchFactory.Create(shiftId, tick, ImmutableArray<AuthoritativeAcceptedIntent>.Empty),
                ImmutableHashSet<ItemId>.Empty);
        }
    }

    /// <summary>Loads an already-validated configuration; this boundary has no YAML parser or validator.</summary>
    public interface IValidatedConfigurationStartupSource
    {
        ValidatedConfiguration Load();
    }

    /// <summary>
    /// Unity TextAsset adapter for the committed C1 deployment material. It delegates all trust verification and
    /// materialization to the production PortableAuthority deployment manifest and codec.
    /// </summary>
    public sealed class Gate2C1DeploymentStartupSource : IValidatedConfigurationStartupSource
    {
        private readonly Gate2DeploymentTextAsset _artifactBase64;
        private readonly Gate2DeploymentTextAsset _manifest;

        public Gate2C1DeploymentStartupSource(Gate2DeploymentTextAsset artifactBase64, Gate2DeploymentTextAsset manifest)
        {
            _artifactBase64 = artifactBase64 ?? throw new ArgumentNullException(nameof(artifactBase64));
            _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        }

        public ValidatedConfiguration Load()
        {
            return MaterializeDeploymentTexts(_artifactBase64.Text, _manifest.Text);
        }

        /// <summary>Testable production-materialization boundary; no Unity-specific C1 codec exists here.</summary>
        public static ValidatedConfiguration MaterializeDeploymentTexts(string artifactBase64Text, string manifestText)
        {
            if (string.IsNullOrWhiteSpace(artifactBase64Text))
            {
                throw new InvalidDataException("The C1 deployment artifact is missing or empty.");
            }

            if (string.IsNullOrWhiteSpace(manifestText))
            {
                throw new InvalidDataException("The C1 deployment manifest is missing or empty.");
            }

            var artifact = Convert.FromBase64String(artifactBase64Text.Trim());
            var manifest = ValidatedConfigurationC1DeploymentManifest.Parse(manifestText);
            return manifest.VerifyAndMaterialize(artifact);
        }
    }

    /// <summary>
    /// Identity-only process lease for the one production Unity owner. It deliberately stores no session,
    /// cadence, configuration, journal, tick cursor, or authoritative runtime state.
    /// </summary>
    internal static class Gate2ProductionHostLease
    {
        private static readonly object Sync = new object();
        private static Guid? _currentOwnerId;

        internal static IDisposable Acquire(Guid ownerId)
        {
            lock (Sync)
            {
                if (_currentOwnerId.HasValue)
                {
                    throw new InvalidOperationException("A production HostSession owner is already active in this process.");
                }

                _currentOwnerId = ownerId;
                return new Lease(ownerId);
            }
        }

        internal static void ResetForSubsystemRegistration()
        {
            lock (Sync)
            {
                _currentOwnerId = null;
            }
        }

        private sealed class Lease : IDisposable
        {
            private readonly Guid _ownerId;
            private bool _released;

            internal Lease(Guid ownerId)
            {
                _ownerId = ownerId;
            }

            public void Dispose()
            {
                lock (Sync)
                {
                    if (_released)
                    {
                        return;
                    }

                    if (_currentOwnerId == _ownerId)
                    {
                        _currentOwnerId = null;
                    }

                    _released = true;
                }
            }
        }
    }

    /// <summary>
    /// The one explicit Gate-2 production owner and thin Unity driver. Authoritative state remains entirely inside
    /// the imported plain-C# <see cref="HostSession"/>; this component only owns lifecycle, C1 startup, exact
    /// elapsed evidence, already-admitted input, and execute-before-retire pumping.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class Gate2ProductionHostDriver : MonoBehaviour
    {
        public const string OwnerAcquiredMarker = "TLAW071_OWNER_ACQUIRED";
        public const string SessionCreatedMarker = "TLAW071_OWNER_SESSION_CREATED";
        public const string StartupPassMarker = "TLAW071_OWNER_START_PASS";
        public const string TeardownPassMarker = "TLAW071_OWNER_TEARDOWN_PASS";
        public const string StartupFailMarker = "TLAW071_OWNER_START_FAIL";
        public const string FaultMarker = "TLAW071_OWNER_FAULT";

        [SerializeField]
        private Gate2DeploymentTextAsset _c1ArtifactBase64;

        [SerializeField]
        private Gate2DeploymentTextAsset _c1Manifest;

        [SerializeField]
        private string _selectedProfileId = "learning";

        private readonly Guid _ownerId = Guid.NewGuid();
        private IAuthoritativeElapsedTimeSource _elapsedTimeSource;
        private IAlreadyAdmittedHostInputSource _inputSource;
        private IValidatedConfigurationStartupSource _configurationSource;
        private Gate2LocalIntentAdmissionAdapter _localIntentAdmission;
        private Gate3ProductionAdmissionComposition _networkedProductionAdmission;
        private Gate3ServerReceiveTickObservationSource _receiveTickObservationSource;
        private IDisposable _lease;
        private HostSession _session;
        private HostTickCadence _cadence;
        private bool _testingConfigured;
        private bool _useProductionLocalAdmission;

        public ProductionHostOwnerLifecycle Lifecycle { get; private set; } = ProductionHostOwnerLifecycle.Unstarted;

        public int SessionCreationCount { get; private set; }

        public int ExecutedTickCount { get; private set; }

        /// <summary>
        /// Test-visible observability for already-admitted input delivery. This increments only after the input
        /// source has returned a non-null evidence object, before the authoritative HostSession validates it.
        /// </summary>
        internal int DeliveredAlreadyAdmittedInputCount { get; private set; }

        public long PendingDueTickCount => _cadence == null ? 0 : _cadence.DueTickCount;

        public string SelectedProfileId => _selectedProfileId;

        public string RunningShiftId => _session == null ? string.Empty : _session.ShiftState.ShiftId.ToString();

        public string LastSuccessfulTickResultType { get; private set; }

        public Exception Fault { get; private set; }

        /// <summary>
        /// Server-local notification emitted only after the real HostSession has returned a successful exact tick.
        /// It grants no alternative execution path and is used by D-026 solely to project retained client results
        /// from the returned Stage Two trace before cadence retirement.
        /// </summary>
        internal event Action<ServerTick, HostStageSevenEventExecution> AuthoritativeTickSucceeded;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only observation of the exact real driver result for executable boundary tests. This is not
        /// compiled into a player and never participates in owner, session, cadence, or admission semantics.
        /// </summary>
        internal HostStageSevenEventExecution LastSuccessfulTickResultForTesting { get; private set; }
#endif

        /// <summary>
        /// The one production local gameplay-intent ingress. The caller provides only an exact envelope and its
        /// separately trusted local actor; the live adapter owns receive tick and receive sequence evidence.
        /// </summary>
        public LocalIntentAdmissionResult SubmitLocalIntent(IntentEnvelope envelope, ActorId authoritativeActor)
        {
            if (Lifecycle != ProductionHostOwnerLifecycle.Running || _localIntentAdmission == null)
            {
                return LocalIntentAdmissionResult.Reject(LocalIntentAdmissionRejection.OwnerNotRunning);
            }

            return _localIntentAdmission.SubmitLocalIntent(envelope, authoritativeActor);
        }

        /// <summary>
        /// The networked-production local ingress. It uses the same session receive-time observation as network
        /// ingress and stops at the one shared D-025 owner; no transport path or second local sequencer is used.
        /// </summary>
        public Gate3NetworkedLocalIntentSubmissionResult SubmitNetworkedLocalIntent(IntentEnvelope envelope, ActorId authoritativeActor)
        {
            if (Lifecycle != ProductionHostOwnerLifecycle.Running || _networkedProductionAdmission == null)
            {
                return Gate3NetworkedLocalIntentSubmissionResult.OwnerNotRunning();
            }

            return _networkedProductionAdmission.SubmitTrustedLocalIntent(envelope, authoritativeActor);
        }

        /// <summary>
        /// Bounded server-only future Gate-3 ingress dependency. It observes the live host-session time origin and
        /// does not deserialize, admit, order, or execute any gameplay input.
        /// </summary>
        public Gate3ServerReceiveTickObservation ObserveAuthoritativeServerReceiveTick()
        {
            if (Lifecycle != ProductionHostOwnerLifecycle.Running || _receiveTickObservationSource == null)
            {
                return Gate3ServerReceiveTickObservation.Rejected(Gate3ServerReceiveTickObservationStatus.OwnerNotRunning);
            }

            try
            {
                return Gate3ServerReceiveTickObservation.Observed(_receiveTickObservationSource.ObserveReceiveTick());
            }
            catch (Exception exception)
            {
                FaultOwner(exception, FaultMarker);
                return Gate3ServerReceiveTickObservation.Rejected(Gate3ServerReceiveTickObservationStatus.ClockFaulted);
            }
        }

        private void Start()
        {
            StartOwner();
        }

        private void Update()
        {
            Pump();
        }

        private void OnDestroy()
        {
            DisposeOwner();
        }

        /// <summary>Injects only clock/input/C1 startup dependencies so EditMode contracts exercise this exact owner.</summary>
        public void ConfigureForTesting(
            IAuthoritativeElapsedTimeSource elapsedTimeSource,
            IAlreadyAdmittedHostInputSource inputSource,
            IValidatedConfigurationStartupSource configurationSource,
            string selectedProfileId)
        {
            if (Lifecycle != ProductionHostOwnerLifecycle.Unstarted)
            {
                throw new InvalidOperationException("A production owner can be configured only before startup.");
            }

            _elapsedTimeSource = elapsedTimeSource ?? throw new ArgumentNullException(nameof(elapsedTimeSource));
            _inputSource = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
            _configurationSource = configurationSource ?? throw new ArgumentNullException(nameof(configurationSource));
            _selectedProfileId = selectedProfileId ?? throw new ArgumentNullException(nameof(selectedProfileId));
            _testingConfigured = true;
            _useProductionLocalAdmission = false;
        }

        /// <summary>
        /// Establishes the one scene-owned D-025 composition before this owner starts. The composition supplies
        /// exactly one final input source only after the HostSession's C1 configuration and time origin exist.
        /// </summary>
        public void ConfigureNetworkedProductionAdmission(Gate3ProductionAdmissionComposition composition)
        {
            if (Lifecycle != ProductionHostOwnerLifecycle.Unstarted)
            {
                throw new InvalidOperationException("A production owner can be configured only before startup.");
            }

            if (_testingConfigured)
            {
                throw new InvalidOperationException("Networked production admission cannot replace an explicit test input configuration.");
            }

            _networkedProductionAdmission = composition ?? throw new ArgumentNullException(nameof(composition));
        }

        /// <summary>
        /// Value-only test seam for the existing owner. It injects deterministic elapsed evidence and explicit
        /// no-input/failure evidence without exposing a second session factory or tick executor.
        /// </summary>
        public void ConfigureForTesting(
            Gate2DeploymentTextAsset artifactBase64,
            Gate2DeploymentTextAsset manifest,
            long[] elapsedMilliseconds,
            int failInputOnRequest,
            string selectedProfileId,
            int invalidContinuityInputOnRequest)
        {
            ConfigureForTesting(
                new ScriptedElapsedTimeSource(elapsedMilliseconds),
                new ScriptedNoInputSource(failInputOnRequest, invalidContinuityInputOnRequest),
                new Gate2C1DeploymentStartupSource(artifactBase64, manifest),
                selectedProfileId);
        }

        /// <summary>
        /// Value-only seam for exercising the real production local-admission path with deterministic elapsed
        /// evidence and the committed C1 deployment material. It exposes neither a second host nor a tick executor.
        /// </summary>
        public void ConfigureProductionLocalAdmissionForTesting(
            Gate2DeploymentTextAsset artifactBase64,
            Gate2DeploymentTextAsset manifest,
            long[] elapsedMilliseconds,
            string selectedProfileId)
        {
            if (Lifecycle != ProductionHostOwnerLifecycle.Unstarted)
            {
                throw new InvalidOperationException("A production owner can be configured only before startup.");
            }

            _elapsedTimeSource = new ScriptedElapsedTimeSource(elapsedMilliseconds ?? throw new ArgumentNullException(nameof(elapsedMilliseconds)));
            _inputSource = null;
            _configurationSource = new Gate2C1DeploymentStartupSource(
                artifactBase64 ?? throw new ArgumentNullException(nameof(artifactBase64)),
                manifest ?? throw new ArgumentNullException(nameof(manifest)));
            _selectedProfileId = selectedProfileId ?? throw new ArgumentNullException(nameof(selectedProfileId));
            _testingConfigured = true;
            _useProductionLocalAdmission = true;
        }

        /// <summary>
        /// Value-only deterministic seam for the scene-owned D-025 composition. It injects only elapsed evidence
        /// and committed C1 material; the already-admitted source is still created by the configured production
        /// composition during the ordinary owner startup lifecycle.
        /// </summary>
        public void ConfigureNetworkedProductionAdmissionForTesting(
            Gate2DeploymentTextAsset artifactBase64,
            Gate2DeploymentTextAsset manifest,
            long[] elapsedMilliseconds,
            long[] observedElapsedMilliseconds,
            string selectedProfileId)
        {
            if (Lifecycle != ProductionHostOwnerLifecycle.Unstarted)
            {
                throw new InvalidOperationException("A production owner can be configured only before startup.");
            }

            if (_networkedProductionAdmission == null)
            {
                throw new InvalidOperationException("The scene-owned production admission composition must be configured before its deterministic test clock.");
            }

            _elapsedTimeSource = new ScriptedElapsedTimeSource(
                elapsedMilliseconds ?? throw new ArgumentNullException(nameof(elapsedMilliseconds)),
                observedElapsedMilliseconds ?? throw new ArgumentNullException(nameof(observedElapsedMilliseconds)));
            _inputSource = null;
            _configurationSource = new Gate2C1DeploymentStartupSource(
                artifactBase64 ?? throw new ArgumentNullException(nameof(artifactBase64)),
                manifest ?? throw new ArgumentNullException(nameof(manifest)));
            _selectedProfileId = selectedProfileId ?? throw new ArgumentNullException(nameof(selectedProfileId));
            _testingConfigured = true;
            _useProductionLocalAdmission = false;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only value seam for deterministic TLAW-076 receive-time contracts over the real owner.</summary>
        public void ConfigureReceiveTickForTesting(
            Gate2DeploymentTextAsset artifactBase64,
            Gate2DeploymentTextAsset manifest,
            long[] elapsedMilliseconds,
            long[] observedElapsedMilliseconds,
            string selectedProfileId)
        {
            if (Lifecycle != ProductionHostOwnerLifecycle.Unstarted)
            {
                throw new InvalidOperationException("A production owner can be configured only before startup.");
            }

            _elapsedTimeSource = new ScriptedElapsedTimeSource(
                elapsedMilliseconds ?? throw new ArgumentNullException(nameof(elapsedMilliseconds)),
                observedElapsedMilliseconds ?? throw new ArgumentNullException(nameof(observedElapsedMilliseconds)));
            _inputSource = new EmptyAlreadyAdmittedHostInputSource();
            _configurationSource = new Gate2C1DeploymentStartupSource(
                artifactBase64 ?? throw new ArgumentNullException(nameof(artifactBase64)),
                manifest ?? throw new ArgumentNullException(nameof(manifest)));
            _selectedProfileId = selectedProfileId ?? throw new ArgumentNullException(nameof(selectedProfileId));
            _testingConfigured = true;
            _useProductionLocalAdmission = false;
        }
#endif

        /// <summary>Exposes deterministic conversion evidence for the exact production clock bridge only.</summary>
        public static long[] ConvertTimestampSamplesForTesting(long frequency, long[] timestamps)
        {
            var source = new ScriptedTimestampSource(frequency, timestamps);
            var bridge = new StopwatchElapsedTimeSource(source);
            var converted = new long[Math.Max(0, timestamps.Length - 1)];
            for (var index = 0; index < converted.Length; index++)
            {
                converted[index] = bridge.SampleElapsedMilliseconds().Value;
            }

            return converted;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only evidence that the real monotonic bridge rejects backward/overflow observations.</summary>
        public static long[] ObserveTimestampSamplesForTesting(long frequency, long[] timestamps)
        {
            var source = new ScriptedTimestampSource(frequency, timestamps);
            var bridge = new StopwatchElapsedTimeSource(source);
            var observed = new long[Math.Max(0, timestamps.Length - 1)];
            for (var index = 0; index < observed.Length; index++)
            {
                observed[index] = bridge.ObserveElapsedMilliseconds().Value;
            }

            return observed;
        }

        /// <summary>Editor-only proof that observing real total elapsed time does not consume the next cadence delta.</summary>
        public static long[] ObserveThenSampleTimestampMillisecondsForTesting(long frequency, long[] timestamps)
        {
            var source = new ScriptedTimestampSource(frequency, timestamps);
            var bridge = new StopwatchElapsedTimeSource(source);
            return new[]
            {
                bridge.ObserveElapsedMilliseconds().Value,
                bridge.SampleElapsedMilliseconds().Value
            };
        }
#endif

        /// <summary>Executes the same startup lifecycle used by <see cref="Start"/> without waiting for an Editor frame.</summary>
        public void StartForTesting()
        {
            StartOwner();
        }

        /// <summary>Executes the same no-cap cadence pump used by <see cref="Update"/>.</summary>
        public void PumpForTesting()
        {
            Pump();
        }

        /// <summary>Exercises the production reset lifecycle: dispose old session, release, then construct anew.</summary>
        public void ResetForTesting()
        {
            if (Lifecycle != ProductionHostOwnerLifecycle.Running)
            {
                throw new InvalidOperationException("Only a running production owner can reset.");
            }

            DisposeSessionAndReleaseLease();
            Lifecycle = ProductionHostOwnerLifecycle.Unstarted;
            StartOwner();
        }

        /// <summary>Exercises production teardown without needing to destroy the containing GameObject.</summary>
        public void DisposeForTesting()
        {
            DisposeOwner();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetProcessLeaseAtSubsystemRegistration()
        {
            Gate2ProductionHostLease.ResetForSubsystemRegistration();
        }

        private void StartOwner()
        {
            if (Lifecycle != ProductionHostOwnerLifecycle.Unstarted)
            {
                return;
            }

            try
            {
                EnsureSources();
                _lease = Gate2ProductionHostLease.Acquire(_ownerId);
                Debug.Log(OwnerAcquiredMarker);

                var configuration = _configurationSource.Load();
                var profileId = ProfileId.From(_selectedProfileId);
                if (!configuration.Shift.Profiles.ContainsKey(profileId))
                {
                    throw new InvalidDataException("The selected startup profile is not present in the materialized configuration.");
                }

                _elapsedTimeSource.ResetSessionOrigin();
                _cadence = new HostTickCadence();
                _session = new HostSession(configuration.Shift, configuration.Anomalies, profileId);
                _receiveTickObservationSource = new Gate3ServerReceiveTickObservationSource(_elapsedTimeSource);
                if (_networkedProductionAdmission != null)
                {
                    _inputSource = _networkedProductionAdmission.BeginSession(configuration.Shift.ShiftId, ObserveAuthoritativeServerReceiveTick);
                }
                else if (_useProductionLocalAdmission)
                {
                    _localIntentAdmission = new Gate2LocalIntentAdmissionAdapter(configuration.Shift.ShiftId);
                    _inputSource = _localIntentAdmission;
                }

                if (_inputSource == null)
                {
                    throw new InvalidOperationException("A production owner requires already-admitted input evidence.");
                }

                SessionCreationCount = checked(SessionCreationCount + 1);
                Lifecycle = ProductionHostOwnerLifecycle.Running;
                Debug.Log(SessionCreatedMarker + " profile=" + profileId);
                Debug.Log(StartupPassMarker + " shift=" + configuration.Shift.ShiftId);
            }
            catch (Exception exception)
            {
                FaultOwner(exception, StartupFailMarker);
            }
        }

        private void Pump()
        {
            if (Lifecycle != ProductionHostOwnerLifecycle.Running)
            {
                return;
            }

            try
            {
                _cadence.Accumulate(_elapsedTimeSource.SampleElapsedMilliseconds());
                while (_cadence.TryGetDueTicks(out var dueTicks))
                {
                    var tick = dueTicks.First;
                    if (_inputSource is IIngressBeforeSealHostInputSource ingressBeforeSeal
                        && !ingressBeforeSeal.CanSeal(_session.ShiftState.ShiftId, tick))
                    {
                        break;
                    }

                    var input = _inputSource.GetInput(_session.ShiftState.ShiftId, tick);
                    if (input == null)
                    {
                        throw new InvalidOperationException("Already-admitted input evidence cannot be null.");
                    }

                    DeliveredAlreadyAdmittedInputCount = checked(DeliveredAlreadyAdmittedInputCount + 1);
                    var result = _session.ExecuteTick(tick, input.AcceptedIntents, input.ActiveTools);
                    AuthoritativeTickSucceeded?.Invoke(tick, result);
                    var retired = _cadence.RetireNextDueTick();
                    if (retired != tick)
                    {
                        throw new InvalidOperationException("Cadence retired a tick other than the successfully executed tick.");
                    }

                    ExecutedTickCount = checked(ExecutedTickCount + 1);
                    LastSuccessfulTickResultType = result.GetType().Name;
#if UNITY_EDITOR
                    LastSuccessfulTickResultForTesting = result;
#endif
                }
            }
            catch (Exception exception)
            {
                FaultOwner(exception, FaultMarker);
            }
        }

        private void EnsureSources()
        {
            if (_testingConfigured)
            {
                return;
            }

            _elapsedTimeSource = new StopwatchElapsedTimeSource();
            _inputSource = null;
            _configurationSource = new Gate2C1DeploymentStartupSource(_c1ArtifactBase64, _c1Manifest);
            _useProductionLocalAdmission = _networkedProductionAdmission == null;
        }

        private void FaultOwner(Exception exception, string marker)
        {
            Fault = exception ?? throw new ArgumentNullException(nameof(exception));
            DisposeSessionAndReleaseLease();
            Lifecycle = ProductionHostOwnerLifecycle.Faulted;
            Debug.LogError(marker + " " + exception.GetType().Name + ": " + exception.Message);
        }

        private void DisposeOwner()
        {
            if (Lifecycle == ProductionHostOwnerLifecycle.Disposed)
            {
                return;
            }

            var hadSession = _session != null;
            DisposeSessionAndReleaseLease();
            Lifecycle = ProductionHostOwnerLifecycle.Disposed;
            if (hadSession)
            {
                Debug.Log(TeardownPassMarker);
            }
        }

        private void DisposeSessionAndReleaseLease()
        {
            _receiveTickObservationSource = null;

            if (_networkedProductionAdmission != null)
            {
                _networkedProductionAdmission.EndSession();
            }

            if (_localIntentAdmission != null)
            {
                _localIntentAdmission.Dispose();
                _localIntentAdmission = null;
            }

            if (_session != null)
            {
                _session.Dispose();
                _session = null;
            }

            if (_lease != null)
            {
                _lease.Dispose();
                _lease = null;
            }
        }

        private sealed class ScriptedElapsedTimeSource : IAuthoritativeElapsedTimeSource
        {
            private readonly long[] _samples;
            private readonly long[] _observedElapsedMilliseconds;
            private int _nextSample;
            private int _nextObservation;
            private long _lastObservedElapsedMilliseconds;
            private bool _hasObservedElapsedMilliseconds;

            internal ScriptedElapsedTimeSource(long[] samples)
                : this(samples, new long[] { 0 })
            {
            }

            internal ScriptedElapsedTimeSource(long[] samples, long[] observedElapsedMilliseconds)
            {
                _samples = samples ?? throw new ArgumentNullException(nameof(samples));
                _observedElapsedMilliseconds = observedElapsedMilliseconds ?? throw new ArgumentNullException(nameof(observedElapsedMilliseconds));
            }

            public AuthoritativeElapsedMilliseconds SampleElapsedMilliseconds()
            {
                var value = _nextSample < _samples.Length ? _samples[_nextSample++] : 0;
                return AuthoritativeElapsedMilliseconds.FromMilliseconds(value);
            }

            public AuthoritativeElapsedMilliseconds ObserveElapsedMilliseconds()
            {
                var value = _nextObservation < _observedElapsedMilliseconds.Length
                    ? _observedElapsedMilliseconds[_nextObservation++]
                    : _lastObservedElapsedMilliseconds;
                if (_hasObservedElapsedMilliseconds && value < _lastObservedElapsedMilliseconds)
                {
                    throw new InvalidOperationException("The scripted elapsed-time observation moved backwards.");
                }

                _lastObservedElapsedMilliseconds = value;
                _hasObservedElapsedMilliseconds = true;
                return AuthoritativeElapsedMilliseconds.FromMilliseconds(value);
            }

            public void ResetSessionOrigin()
            {
                _nextSample = 0;
                _nextObservation = 0;
                _lastObservedElapsedMilliseconds = 0;
                _hasObservedElapsedMilliseconds = false;
            }
        }

        private sealed class ScriptedNoInputSource : IAlreadyAdmittedHostInputSource
        {
            private readonly int _failInputOnRequest;
            private readonly int _invalidContinuityInputOnRequest;
            private int _requests;

            internal ScriptedNoInputSource(int failInputOnRequest, int invalidContinuityInputOnRequest)
            {
                _failInputOnRequest = failInputOnRequest;
                _invalidContinuityInputOnRequest = invalidContinuityInputOnRequest;
            }

            public AlreadyAdmittedHostTickInput GetInput(ShiftId shiftId, ServerTick tick)
            {
                var request = _requests++;
                if (_failInputOnRequest >= 0 && request >= _failInputOnRequest)
                {
                    throw new InvalidOperationException("Test input evidence intentionally unavailable.");
                }

                var batchTick = _invalidContinuityInputOnRequest >= 0 && request >= _invalidContinuityInputOnRequest
                    ? ServerTick.From(checked(tick.Value + 1))
                    : tick;
                return new AlreadyAdmittedHostTickInput(
                    AcceptedIntentTickBatchFactory.Create(shiftId, batchTick, ImmutableArray<AuthoritativeAcceptedIntent>.Empty),
                    ImmutableHashSet<ItemId>.Empty);
            }
        }

        private sealed class ScriptedTimestampSource : IMonotonicTimestampSource
        {
            private readonly long[] _timestamps;
            private int _next;

            internal ScriptedTimestampSource(long frequency, long[] timestamps)
            {
                if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));
                Frequency = frequency;
                _timestamps = timestamps ?? throw new ArgumentNullException(nameof(timestamps));
                if (_timestamps.Length == 0) throw new ArgumentException("At least one timestamp is required.", nameof(timestamps));
            }

            public long Frequency { get; }

            public long GetTimestamp()
            {
                if (_next >= _timestamps.Length)
                {
                    throw new InvalidOperationException("No timestamp sample remains.");
                }

                return _timestamps[_next++];
            }
        }
    }
}
