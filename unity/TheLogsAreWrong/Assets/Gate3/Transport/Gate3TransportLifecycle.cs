using System;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace TheLogsAreWrong.Gate3
{
    /// <summary>
    /// The one opt-in production seam for the already materialized transport. It owns only the
    /// request/observed-state lifecycle; it has no simulation responsibility.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Gate3TransportLifecycle : MonoBehaviour
    {
        /// <summary>Explicit non-gameplay command-line probe for the required manual Steam lifecycle evidence.</summary>
        public const string ListenHostProbeArgument = "-tlaw-gate3-listen-host-lifecycle-smoke";

        [SerializeField]
        private NetworkManager _networkManager;

        [SerializeField]
        private FishySteamworks.FishySteamworks _transport;

        [SerializeField]
        private GameObject _steamRuntime;

        [SerializeField]
        [Min(1f)]
        private float _transitionTimeoutSeconds = 15f;

        private Gate3TransportLifecycleController _controller;
        private bool _probeRequested;
        private bool _probeStopRequested;
        private bool _probeServerStarted;
        private bool _probeClientStarted;
        private bool _probeClientStopped;
        private bool _probeServerStopped;

        public Gate3TransportLifecyclePhase Phase => _controller == null
            ? Gate3TransportLifecyclePhase.Offline
            : _controller.Phase;

        public Gate3TransportLifecycleRole Role => _controller == null
            ? Gate3TransportLifecycleRole.Offline
            : _controller.Role;

        public Gate3TransportLifecycleFailure LastFailure => _controller == null
            ? Gate3TransportLifecycleFailure.None
            : _controller.LastFailure;

        public bool IsLifecycleActive => Phase != Gate3TransportLifecyclePhase.Offline;

        public Gate3TransportLifecycleRequestResult RequestListenHostStart()
        {
            EnsureSteamRuntime();
            return RequireController().RequestListenHostStart();
        }

        public Gate3TransportLifecycleRequestResult RequestListenHostStop()
        {
            return RequireController().RequestListenHostStop();
        }

        public Gate3TransportLifecycleRequestResult RequestClientOnlyStart()
        {
            EnsureSteamRuntime();
            return RequireController().RequestClientOnlyStart();
        }

        public Gate3TransportLifecycleRequestResult RequestClientOnlyStop()
        {
            return RequireController().RequestClientOnlyStop();
        }

        private void Awake()
        {
            if (_networkManager == null || _transport == null || _steamRuntime == null)
            {
                throw new InvalidOperationException("The Gate-3 transport lifecycle requires the committed NetworkManager and Fishy transport.");
            }

            _controller = new Gate3TransportLifecycleController(new FishyTransportPort(_transport), _transitionTimeoutSeconds);
            _controller.PhaseChanged += OnPhaseChanged;
            _transport.OnServerConnectionState += OnServerConnectionState;
            _transport.OnClientConnectionState += OnClientConnectionState;
        }

        private void Start()
        {
            if (!HasArgument(ListenHostProbeArgument))
            {
                return;
            }

            _probeRequested = true;
            Debug.Log("TLAW074_LISTEN_HOST_START_REQUESTED");
            var result = RequestListenHostStart();
            if (result != Gate3TransportLifecycleRequestResult.RequestAccepted)
            {
                FailProbe("START_REQUEST_" + result);
            }
        }

        private void Update()
        {
            _controller?.AdvanceTime(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.PhaseChanged -= OnPhaseChanged;
            }

            if (_transport != null)
            {
                _transport.OnServerConnectionState -= OnServerConnectionState;
                _transport.OnClientConnectionState -= OnClientConnectionState;
            }
        }

        private void OnServerConnectionState(ServerConnectionStateArgs state)
        {
            Debug.Log("TLAW074_SERVER_CONNECTION_STATE=" + state.ConnectionState);
            if (state.ConnectionState == LocalConnectionState.Started)
            {
                _probeServerStarted = true;
            }
            else if (state.ConnectionState == LocalConnectionState.Stopped && _probeServerStarted)
            {
                _probeServerStopped = true;
            }

            _controller.ObserveServerConnectionState(state.ConnectionState);
        }

        private void OnClientConnectionState(ClientConnectionStateArgs state)
        {
            Debug.Log("TLAW074_CLIENT_CONNECTION_STATE=" + state.ConnectionState);
            if (state.ConnectionState == LocalConnectionState.Started)
            {
                _probeClientStarted = true;
            }
            else if (state.ConnectionState == LocalConnectionState.Stopped && _probeClientStarted)
            {
                _probeClientStopped = true;
            }

            _controller.ObserveClientConnectionState(state.ConnectionState);
        }

        private void OnPhaseChanged(Gate3TransportLifecyclePhase phase, Gate3TransportLifecycleFailure failure)
        {
            Debug.Log("TLAW074_LIFECYCLE_PHASE=" + phase + " failure=" + failure);
            if (!_probeRequested)
            {
                return;
            }

            if (phase == Gate3TransportLifecyclePhase.ListenHostStarted && !_probeStopRequested)
            {
                _probeStopRequested = true;
                Debug.Log("TLAW074_LISTEN_HOST_STOP_REQUESTED");
                var result = RequestListenHostStop();
                if (result != Gate3TransportLifecycleRequestResult.RequestAccepted)
                {
                    FailProbe("STOP_REQUEST_" + result);
                }

                return;
            }

            if (phase == Gate3TransportLifecyclePhase.Faulted)
            {
                FailProbe("FAULTED_" + failure);
                return;
            }

            if (phase == Gate3TransportLifecyclePhase.Offline && _probeStopRequested)
            {
                if (failure != Gate3TransportLifecycleFailure.None)
                {
                    FailProbe("ROLLBACK_" + failure);
                    return;
                }

                if (!_probeServerStarted || !_probeClientStarted || !_probeClientStopped || !_probeServerStopped)
                {
                    FailProbe("INCOMPLETE_STATE_SEQUENCE");
                    return;
                }

                Debug.Log("TLAW074_LISTEN_HOST_LIFECYCLE_PASS");
                Application.Quit(0);
            }
        }

        private Gate3TransportLifecycleController RequireController()
        {
            if (_controller == null)
            {
                throw new InvalidOperationException("The Gate-3 transport lifecycle has not initialized.");
            }

            return _controller;
        }

        private void EnsureSteamRuntime()
        {
            if (!_steamRuntime.activeSelf)
            {
                _steamRuntime.SetActive(true);
            }
        }

        private static bool HasArgument(string argument)
        {
            foreach (var candidate in Environment.GetCommandLineArgs())
            {
                if (string.Equals(candidate, argument, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void FailProbe(string reason)
        {
            Debug.LogError("TLAW074_LISTEN_HOST_LIFECYCLE_FAILED=" + reason);
            Application.Quit(4);
        }

        private sealed class FishyTransportPort : IGate3TransportLifecycleTransport
        {
            private readonly FishySteamworks.FishySteamworks _transport;

            public FishyTransportPort(FishySteamworks.FishySteamworks transport)
            {
                _transport = transport;
            }

            public LocalConnectionState ServerState => _transport.GetConnectionState(true);
            public LocalConnectionState ClientState => _transport.GetConnectionState(false);
            public bool StartServer() => _transport.StartConnection(true);
            public bool StartClient() => _transport.StartConnection(false);
            public bool StopServer() => _transport.StopConnection(true);
            public bool StopClient() => _transport.StopConnection(false);
        }
    }

    /// <summary>Minimal port used by the lifecycle coordinator; production adapts only the selected Fishy transport.</summary>
    public interface IGate3TransportLifecycleTransport
    {
        LocalConnectionState ServerState { get; }
        LocalConnectionState ClientState { get; }
        bool StartServer();
        bool StartClient();
        bool StopServer();
        bool StopClient();
    }

    public enum Gate3TransportLifecycleRole
    {
        Offline,
        ListenHost,
        ClientOnly
    }

    public enum Gate3TransportLifecyclePhase
    {
        Offline,
        StartingServer,
        StartingHostClient,
        ListenHostStarted,
        StartingClientOnly,
        ClientOnlyStarted,
        StoppingClient,
        StoppingServer,
        RollingBackClient,
        RollingBackServer,
        Faulted
    }

    public enum Gate3TransportLifecycleRequestResult
    {
        RequestAccepted,
        DuplicateStart,
        ConflictingRole,
        AlreadyStopped,
        InvalidStopOrdering,
        StartRequestRejected,
        Faulted
    }

    public enum Gate3TransportLifecycleFailure
    {
        None,
        ServerStartRequestRejected,
        ClientStartRequestRejected,
        StartTimedOut,
        InvalidStopOrdering,
        UnexpectedTransportState,
        StopRequestRejected
    }

    /// <summary>
    /// Bounded lifecycle state machine. A successful request is not success evidence: only the
    /// observed FishNet/Fishy state callbacks move either start path to its started phase.
    /// </summary>
    public sealed class Gate3TransportLifecycleController
    {
        private readonly IGate3TransportLifecycleTransport _transport;
        private readonly float _timeoutSeconds;
        private float _remainingSeconds;
        private LocalConnectionState _observedServerState;
        private LocalConnectionState _observedClientState;

        public Gate3TransportLifecycleController(IGate3TransportLifecycleTransport transport, float timeoutSeconds)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            if (timeoutSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            }

            _timeoutSeconds = timeoutSeconds;
            _observedServerState = transport.ServerState;
            _observedClientState = transport.ClientState;
        }

        public event Action<Gate3TransportLifecyclePhase, Gate3TransportLifecycleFailure> PhaseChanged;

        public Gate3TransportLifecycleRole Role { get; private set; } = Gate3TransportLifecycleRole.Offline;
        public Gate3TransportLifecyclePhase Phase { get; private set; } = Gate3TransportLifecyclePhase.Offline;
        public Gate3TransportLifecycleFailure LastFailure { get; private set; } = Gate3TransportLifecycleFailure.None;

        public Gate3TransportLifecycleRequestResult RequestListenHostStart()
        {
            var rejection = BeginStart(Gate3TransportLifecycleRole.ListenHost, Gate3TransportLifecyclePhase.StartingServer);
            if (rejection.HasValue)
            {
                return rejection.Value;
            }

            if (_transport.StartServer())
            {
                return Gate3TransportLifecycleRequestResult.RequestAccepted;
            }

            BeginRollback(Gate3TransportLifecycleFailure.ServerStartRequestRejected);
            return Gate3TransportLifecycleRequestResult.StartRequestRejected;
        }

        public Gate3TransportLifecycleRequestResult RequestClientOnlyStart()
        {
            var rejection = BeginStart(Gate3TransportLifecycleRole.ClientOnly, Gate3TransportLifecyclePhase.StartingClientOnly);
            if (rejection.HasValue)
            {
                return rejection.Value;
            }

            if (_transport.StartClient())
            {
                return Gate3TransportLifecycleRequestResult.RequestAccepted;
            }

            BeginRollback(Gate3TransportLifecycleFailure.ClientStartRequestRejected);
            return Gate3TransportLifecycleRequestResult.StartRequestRejected;
        }

        public Gate3TransportLifecycleRequestResult RequestListenHostStop()
        {
            if (Role == Gate3TransportLifecycleRole.Offline && Phase == Gate3TransportLifecyclePhase.Offline)
            {
                return Gate3TransportLifecycleRequestResult.AlreadyStopped;
            }

            if (Role != Gate3TransportLifecycleRole.ListenHost)
            {
                return Gate3TransportLifecycleRequestResult.ConflictingRole;
            }

            if (Phase != Gate3TransportLifecyclePhase.ListenHostStarted
                || _observedServerState != LocalConnectionState.Started
                || _observedClientState != LocalConnectionState.Started)
            {
                BeginRollback(Gate3TransportLifecycleFailure.InvalidStopOrdering);
                return Gate3TransportLifecycleRequestResult.InvalidStopOrdering;
            }

            SetPhase(Gate3TransportLifecyclePhase.StoppingClient);
            if (_transport.StopClient() || _observedClientState == LocalConnectionState.Stopped)
            {
                if (Phase == Gate3TransportLifecyclePhase.StoppingClient
                    && _observedClientState == LocalConnectionState.Stopped)
                {
                    BeginServerStop();
                }

                return Gate3TransportLifecycleRequestResult.RequestAccepted;
            }

            EnterFaulted(Gate3TransportLifecycleFailure.StopRequestRejected);
            return Gate3TransportLifecycleRequestResult.Faulted;
        }

        public Gate3TransportLifecycleRequestResult RequestClientOnlyStop()
        {
            if (Role == Gate3TransportLifecycleRole.Offline && Phase == Gate3TransportLifecyclePhase.Offline)
            {
                return Gate3TransportLifecycleRequestResult.AlreadyStopped;
            }

            if (Role != Gate3TransportLifecycleRole.ClientOnly)
            {
                return Gate3TransportLifecycleRequestResult.ConflictingRole;
            }

            if (Phase != Gate3TransportLifecyclePhase.ClientOnlyStarted || _observedClientState != LocalConnectionState.Started)
            {
                BeginRollback(Gate3TransportLifecycleFailure.InvalidStopOrdering);
                return Gate3TransportLifecycleRequestResult.InvalidStopOrdering;
            }

            SetPhase(Gate3TransportLifecyclePhase.StoppingClient);
            if (_transport.StopClient() || _observedClientState == LocalConnectionState.Stopped)
            {
                if (Phase == Gate3TransportLifecyclePhase.StoppingClient
                    && _observedClientState == LocalConnectionState.Stopped)
                {
                    SetOffline();
                }

                return Gate3TransportLifecycleRequestResult.RequestAccepted;
            }

            EnterFaulted(Gate3TransportLifecycleFailure.StopRequestRejected);
            return Gate3TransportLifecycleRequestResult.Faulted;
        }

        public void ObserveServerConnectionState(LocalConnectionState state)
        {
            _observedServerState = state;
            if (Phase == Gate3TransportLifecyclePhase.StartingServer)
            {
                if (state == LocalConnectionState.Started)
                {
                    BeginHostClientStart();
                }
                else if (state == LocalConnectionState.Stopped)
                {
                    BeginRollback(Gate3TransportLifecycleFailure.UnexpectedTransportState);
                }

                return;
            }

            if (Phase == Gate3TransportLifecyclePhase.StoppingServer || Phase == Gate3TransportLifecyclePhase.RollingBackServer)
            {
                if (state == LocalConnectionState.Stopped)
                {
                    SetOffline();
                }

                return;
            }

            if (Phase == Gate3TransportLifecyclePhase.Offline && state != LocalConnectionState.Stopped)
            {
                BeginRollback(Gate3TransportLifecycleFailure.UnexpectedTransportState);
            }
        }

        public void ObserveClientConnectionState(LocalConnectionState state)
        {
            _observedClientState = state;
            if (Phase == Gate3TransportLifecyclePhase.StartingHostClient)
            {
                if (state == LocalConnectionState.Started)
                {
                    SetPhase(Gate3TransportLifecyclePhase.ListenHostStarted);
                }
                else if (state == LocalConnectionState.Stopped)
                {
                    BeginRollback(Gate3TransportLifecycleFailure.UnexpectedTransportState);
                }

                return;
            }

            if (Phase == Gate3TransportLifecyclePhase.StartingClientOnly)
            {
                if (state == LocalConnectionState.Started)
                {
                    SetPhase(Gate3TransportLifecyclePhase.ClientOnlyStarted);
                }
                else if (state == LocalConnectionState.Stopped)
                {
                    BeginRollback(Gate3TransportLifecycleFailure.UnexpectedTransportState);
                }

                return;
            }

            if (Phase == Gate3TransportLifecyclePhase.StoppingClient)
            {
                if (state == LocalConnectionState.Stopped)
                {
                    if (Role == Gate3TransportLifecycleRole.ListenHost)
                    {
                        BeginServerStop();
                    }
                    else
                    {
                        SetOffline();
                    }
                }

                return;
            }

            if (Phase == Gate3TransportLifecyclePhase.RollingBackClient && state == LocalConnectionState.Stopped)
            {
                BeginRollbackServer();
                return;
            }

            if (Phase == Gate3TransportLifecyclePhase.Offline && state != LocalConnectionState.Stopped)
            {
                BeginRollback(Gate3TransportLifecycleFailure.UnexpectedTransportState);
            }
        }

        public void AdvanceTime(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f || !IsStartPhase(Phase))
            {
                return;
            }

            _remainingSeconds -= elapsedSeconds;
            if (_remainingSeconds <= 0f)
            {
                BeginRollback(Gate3TransportLifecycleFailure.StartTimedOut);
            }
        }

        private Gate3TransportLifecycleRequestResult? BeginStart(Gate3TransportLifecycleRole requestedRole, Gate3TransportLifecyclePhase startPhase)
        {
            if (Phase == Gate3TransportLifecyclePhase.Faulted)
            {
                return Gate3TransportLifecycleRequestResult.Faulted;
            }

            if (Role != Gate3TransportLifecycleRole.Offline || Phase != Gate3TransportLifecyclePhase.Offline)
            {
                return Role == requestedRole
                    ? Gate3TransportLifecycleRequestResult.DuplicateStart
                    : Gate3TransportLifecycleRequestResult.ConflictingRole;
            }

            if (_observedServerState != LocalConnectionState.Stopped || _observedClientState != LocalConnectionState.Stopped)
            {
                BeginRollback(Gate3TransportLifecycleFailure.UnexpectedTransportState);
                return Gate3TransportLifecycleRequestResult.Faulted;
            }

            Role = requestedRole;
            LastFailure = Gate3TransportLifecycleFailure.None;
            _remainingSeconds = _timeoutSeconds;
            SetPhase(startPhase);
            return null;
        }

        private void BeginHostClientStart()
        {
            SetPhase(Gate3TransportLifecyclePhase.StartingHostClient);
            _remainingSeconds = _timeoutSeconds;
            if (!_transport.StartClient())
            {
                BeginRollback(Gate3TransportLifecycleFailure.ClientStartRequestRejected);
            }
        }

        private void BeginServerStop()
        {
            SetPhase(Gate3TransportLifecyclePhase.StoppingServer);
            if (_transport.StopServer() || _observedServerState == LocalConnectionState.Stopped)
            {
                if (Phase == Gate3TransportLifecyclePhase.StoppingServer
                    && _observedServerState == LocalConnectionState.Stopped)
                {
                    SetOffline();
                }

                return;
            }

            EnterFaulted(Gate3TransportLifecycleFailure.StopRequestRejected);
        }

        private void BeginRollback(Gate3TransportLifecycleFailure failure)
        {
            LastFailure = failure;
            if (_observedClientState != LocalConnectionState.Stopped)
            {
                SetPhase(Gate3TransportLifecyclePhase.RollingBackClient);
                if (_transport.StopClient() || _observedClientState == LocalConnectionState.Stopped)
                {
                    if (Phase == Gate3TransportLifecyclePhase.RollingBackClient
                        && _observedClientState == LocalConnectionState.Stopped)
                    {
                        BeginRollbackServer();
                    }

                    return;
                }

                EnterFaulted(Gate3TransportLifecycleFailure.StopRequestRejected);
                return;
            }

            BeginRollbackServer();
        }

        private void BeginRollbackServer()
        {
            if (_observedServerState == LocalConnectionState.Stopped)
            {
                SetOffline();
                return;
            }

            SetPhase(Gate3TransportLifecyclePhase.RollingBackServer);
            if (_transport.StopServer() || _observedServerState == LocalConnectionState.Stopped)
            {
                if (Phase == Gate3TransportLifecyclePhase.RollingBackServer
                    && _observedServerState == LocalConnectionState.Stopped)
                {
                    SetOffline();
                }

                return;
            }

            EnterFaulted(Gate3TransportLifecycleFailure.StopRequestRejected);
        }

        private void SetOffline()
        {
            Role = Gate3TransportLifecycleRole.Offline;
            _remainingSeconds = 0f;
            SetPhase(Gate3TransportLifecyclePhase.Offline);
        }

        private void EnterFaulted(Gate3TransportLifecycleFailure failure)
        {
            LastFailure = failure;
            _remainingSeconds = 0f;
            SetPhase(Gate3TransportLifecyclePhase.Faulted);
        }

        private void SetPhase(Gate3TransportLifecyclePhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase, LastFailure);
        }

        private static bool IsStartPhase(Gate3TransportLifecyclePhase phase)
        {
            return phase == Gate3TransportLifecyclePhase.StartingServer
                   || phase == Gate3TransportLifecyclePhase.StartingHostClient
                   || phase == Gate3TransportLifecyclePhase.StartingClientOnly;
        }
    }
}
