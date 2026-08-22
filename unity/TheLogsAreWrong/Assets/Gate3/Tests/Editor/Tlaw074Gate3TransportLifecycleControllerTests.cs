using System.Collections.Generic;
using FishNet.Transporting;
using NUnit.Framework;

namespace TheLogsAreWrong.Gate3.Tests
{
    /// <summary>Deterministic state-machine contracts for request ordering and bounded rollback.</summary>
    public sealed class Tlaw074Gate3TransportLifecycleControllerTests
    {
        [Test]
        public void Listen_host_starts_and_stops_only_after_observed_transport_states()
        {
            var transport = new TestTransport();
            var controller = new Gate3TransportLifecycleController(transport, 5f);

            Assert.AreEqual(Gate3TransportLifecycleRequestResult.RequestAccepted, controller.RequestListenHostStart());
            CollectionAssert.AreEqual(new[] { "start-server" }, transport.Calls);
            Assert.AreEqual(Gate3TransportLifecyclePhase.StartingServer, controller.Phase);

            transport.ServerState = LocalConnectionState.Started;
            controller.ObserveServerConnectionState(LocalConnectionState.Started);
            CollectionAssert.AreEqual(new[] { "start-server", "start-client" }, transport.Calls);
            Assert.AreEqual(Gate3TransportLifecyclePhase.StartingHostClient, controller.Phase);

            // Fishy's host-local client reports its authoritative state through the client callback;
            // its ordinary non-host client getter remains Stopped for this path.
            controller.ObserveClientConnectionState(LocalConnectionState.Started);
            Assert.AreEqual(Gate3TransportLifecyclePhase.ListenHostStarted, controller.Phase);

            Assert.AreEqual(Gate3TransportLifecycleRequestResult.RequestAccepted, controller.RequestListenHostStop());
            CollectionAssert.AreEqual(new[] { "start-server", "start-client", "stop-client" }, transport.Calls);

            transport.ClientState = LocalConnectionState.Stopped;
            controller.ObserveClientConnectionState(LocalConnectionState.Stopped);
            CollectionAssert.AreEqual(new[] { "start-server", "start-client", "stop-client", "stop-server" }, transport.Calls);

            transport.ServerState = LocalConnectionState.Stopped;
            controller.ObserveServerConnectionState(LocalConnectionState.Stopped);
            Assert.AreEqual(Gate3TransportLifecyclePhase.Offline, controller.Phase);
            Assert.AreEqual(Gate3TransportLifecycleRole.Offline, controller.Role);
        }

        [Test]
        public void Duplicate_conflicting_and_invalid_lifecycle_requests_fail_closed()
        {
            var transport = new TestTransport();
            var controller = new Gate3TransportLifecycleController(transport, 5f);

            Assert.AreEqual(Gate3TransportLifecycleRequestResult.AlreadyStopped, controller.RequestClientOnlyStop());
            Assert.AreEqual(Gate3TransportLifecycleRequestResult.RequestAccepted, controller.RequestClientOnlyStart());
            Assert.AreEqual(Gate3TransportLifecycleRequestResult.DuplicateStart, controller.RequestClientOnlyStart());
            Assert.AreEqual(Gate3TransportLifecycleRequestResult.ConflictingRole, controller.RequestListenHostStart());

            var hostTransport = new TestTransport();
            var host = new Gate3TransportLifecycleController(hostTransport, 5f);
            Assert.AreEqual(Gate3TransportLifecycleRequestResult.RequestAccepted, host.RequestListenHostStart());
            Assert.AreEqual(Gate3TransportLifecycleRequestResult.InvalidStopOrdering, host.RequestListenHostStop());
            hostTransport.ServerState = LocalConnectionState.Stopped;
            host.ObserveServerConnectionState(LocalConnectionState.Stopped);
            Assert.AreEqual(Gate3TransportLifecyclePhase.Offline, host.Phase);
        }

        [Test]
        public void Failed_or_partial_start_rolls_back_to_a_tracked_stopped_state()
        {
            var serverFailure = new TestTransport { StartServerResult = false };
            var serverController = new Gate3TransportLifecycleController(serverFailure, 5f);
            Assert.AreEqual(Gate3TransportLifecycleRequestResult.StartRequestRejected, serverController.RequestListenHostStart());
            Assert.AreEqual(Gate3TransportLifecycleFailure.ServerStartRequestRejected, serverController.LastFailure);
            Assert.AreEqual(Gate3TransportLifecyclePhase.Offline, serverController.Phase);

            var clientFailure = new TestTransport { StartClientResult = false };
            var clientController = new Gate3TransportLifecycleController(clientFailure, 5f);
            Assert.AreEqual(Gate3TransportLifecycleRequestResult.RequestAccepted, clientController.RequestListenHostStart());
            clientFailure.ServerState = LocalConnectionState.Started;
            clientController.ObserveServerConnectionState(LocalConnectionState.Started);
            CollectionAssert.AreEqual(new[] { "start-server", "start-client", "stop-server" }, clientFailure.Calls);
            clientFailure.ServerState = LocalConnectionState.Stopped;
            clientController.ObserveServerConnectionState(LocalConnectionState.Stopped);
            Assert.AreEqual(Gate3TransportLifecycleFailure.ClientStartRequestRejected, clientController.LastFailure);
            Assert.AreEqual(Gate3TransportLifecyclePhase.Offline, clientController.Phase);
        }

        [Test]
        public void Server_start_timeout_without_a_callback_requests_stop_and_waits_for_the_actual_stopped_callback()
        {
            var transport = new TestTransport();
            var controller = new Gate3TransportLifecycleController(transport, 1f);

            Assert.AreEqual(Gate3TransportLifecycleRequestResult.RequestAccepted, controller.RequestListenHostStart());
            controller.AdvanceTime(1f);

            CollectionAssert.AreEqual(new[] { "start-server", "stop-server" }, transport.Calls);
            Assert.AreEqual(Gate3TransportLifecycleFailure.StartTimedOut, controller.LastFailure);
            Assert.AreEqual(Gate3TransportLifecyclePhase.RollingBackServer, controller.Phase);
            Assert.AreEqual(Gate3TransportLifecycleRole.ListenHost, controller.Role);
            Assert.AreEqual(LocalConnectionState.Stopping, transport.ServerState);

            controller.ObserveServerConnectionState(LocalConnectionState.Stopped);
            Assert.AreEqual(Gate3TransportLifecyclePhase.Offline, controller.Phase);
            Assert.AreEqual(Gate3TransportLifecycleRole.Offline, controller.Role);
        }

        [Test]
        public void Client_only_start_timeout_without_a_callback_requests_stop_and_waits_for_the_actual_stopped_callback()
        {
            var transport = new TestTransport();
            var controller = new Gate3TransportLifecycleController(transport, 1f);

            Assert.AreEqual(Gate3TransportLifecycleRequestResult.RequestAccepted, controller.RequestClientOnlyStart());
            controller.AdvanceTime(1f);

            CollectionAssert.AreEqual(new[] { "start-client", "stop-client" }, transport.Calls);
            Assert.AreEqual(Gate3TransportLifecycleFailure.StartTimedOut, controller.LastFailure);
            Assert.AreEqual(Gate3TransportLifecyclePhase.RollingBackClient, controller.Phase);
            Assert.AreEqual(Gate3TransportLifecycleRole.ClientOnly, controller.Role);
            Assert.AreEqual(LocalConnectionState.Stopping, transport.ClientState);

            controller.ObserveClientConnectionState(LocalConnectionState.Stopped);
            Assert.AreEqual(Gate3TransportLifecyclePhase.Offline, controller.Phase);
            Assert.AreEqual(Gate3TransportLifecycleRole.Offline, controller.Role);
        }

        [Test]
        public void Host_client_start_timeout_stops_client_before_server_and_waits_for_both_stopped_callbacks()
        {
            var transport = new TestTransport();
            var controller = new Gate3TransportLifecycleController(transport, 1f);

            Assert.AreEqual(Gate3TransportLifecycleRequestResult.RequestAccepted, controller.RequestListenHostStart());
            controller.ObserveServerConnectionState(LocalConnectionState.Started);
            controller.AdvanceTime(1f);

            CollectionAssert.AreEqual(new[] { "start-server", "start-client", "stop-client" }, transport.Calls);
            Assert.AreEqual(Gate3TransportLifecyclePhase.RollingBackClient, controller.Phase);
            Assert.AreEqual(Gate3TransportLifecycleRole.ListenHost, controller.Role);

            controller.ObserveClientConnectionState(LocalConnectionState.Stopped);
            CollectionAssert.AreEqual(new[] { "start-server", "start-client", "stop-client", "stop-server" }, transport.Calls);
            Assert.AreEqual(Gate3TransportLifecyclePhase.RollingBackServer, controller.Phase);
            Assert.AreEqual(Gate3TransportLifecycleRole.ListenHost, controller.Role);

            controller.ObserveServerConnectionState(LocalConnectionState.Stopped);
            Assert.AreEqual(Gate3TransportLifecyclePhase.Offline, controller.Phase);
            Assert.AreEqual(Gate3TransportLifecycleRole.Offline, controller.Role);
        }

        [Test]
        public void Rollback_stop_rejection_fails_closed_for_server_client_and_host_client_cleanup()
        {
            var serverTransport = new TestTransport { StopServerResult = false };
            var serverController = new Gate3TransportLifecycleController(serverTransport, 1f);
            Assert.AreEqual(Gate3TransportLifecycleRequestResult.RequestAccepted, serverController.RequestListenHostStart());
            serverController.AdvanceTime(1f);
            CollectionAssert.AreEqual(new[] { "start-server", "stop-server" }, serverTransport.Calls);
            Assert.AreEqual(Gate3TransportLifecyclePhase.Faulted, serverController.Phase);
            Assert.AreEqual(Gate3TransportLifecycleFailure.StopRequestRejected, serverController.LastFailure);

            var clientTransport = new TestTransport { StopClientResult = false };
            var clientController = new Gate3TransportLifecycleController(clientTransport, 1f);
            Assert.AreEqual(Gate3TransportLifecycleRequestResult.RequestAccepted, clientController.RequestClientOnlyStart());
            clientController.AdvanceTime(1f);
            CollectionAssert.AreEqual(new[] { "start-client", "stop-client" }, clientTransport.Calls);
            Assert.AreEqual(Gate3TransportLifecyclePhase.Faulted, clientController.Phase);
            Assert.AreEqual(Gate3TransportLifecycleFailure.StopRequestRejected, clientController.LastFailure);

            var hostClientTransport = new TestTransport { StopClientResult = false };
            var hostClientController = new Gate3TransportLifecycleController(hostClientTransport, 1f);
            Assert.AreEqual(Gate3TransportLifecycleRequestResult.RequestAccepted, hostClientController.RequestListenHostStart());
            hostClientController.ObserveServerConnectionState(LocalConnectionState.Started);
            hostClientController.AdvanceTime(1f);
            CollectionAssert.AreEqual(new[] { "start-server", "start-client", "stop-client" }, hostClientTransport.Calls);
            Assert.AreEqual(Gate3TransportLifecyclePhase.Faulted, hostClientController.Phase);
            Assert.AreEqual(Gate3TransportLifecycleFailure.StopRequestRejected, hostClientController.LastFailure);

            var hostTransport = new TestTransport { StopServerResult = false };
            var hostController = new Gate3TransportLifecycleController(hostTransport, 1f);
            Assert.AreEqual(Gate3TransportLifecycleRequestResult.RequestAccepted, hostController.RequestListenHostStart());
            hostController.ObserveServerConnectionState(LocalConnectionState.Started);
            hostController.AdvanceTime(1f);
            CollectionAssert.AreEqual(new[] { "start-server", "start-client", "stop-client" }, hostTransport.Calls);
            hostController.ObserveClientConnectionState(LocalConnectionState.Stopped);
            CollectionAssert.AreEqual(new[] { "start-server", "start-client", "stop-client", "stop-server" }, hostTransport.Calls);
            Assert.AreEqual(Gate3TransportLifecyclePhase.Faulted, hostController.Phase);
            Assert.AreEqual(Gate3TransportLifecycleFailure.StopRequestRejected, hostController.LastFailure);
        }

        private sealed class TestTransport : IGate3TransportLifecycleTransport
        {
            public readonly List<string> Calls = new List<string>();
            public LocalConnectionState ServerState { get; set; } = LocalConnectionState.Stopped;
            public LocalConnectionState ClientState { get; set; } = LocalConnectionState.Stopped;
            public bool StartServerResult { get; set; } = true;
            public bool StartClientResult { get; set; } = true;
            public bool StopServerResult { get; set; } = true;
            public bool StopClientResult { get; set; } = true;

            public bool StartServer()
            {
                Calls.Add("start-server");
                if (StartServerResult)
                {
                    ServerState = LocalConnectionState.Starting;
                }

                return StartServerResult;
            }

            public bool StartClient()
            {
                Calls.Add("start-client");
                if (StartClientResult)
                {
                    ClientState = LocalConnectionState.Starting;
                }

                return StartClientResult;
            }

            public bool StopServer()
            {
                Calls.Add("stop-server");
                if (StopServerResult)
                {
                    ServerState = LocalConnectionState.Stopping;
                }

                return StopServerResult;
            }

            public bool StopClient()
            {
                Calls.Add("stop-client");
                if (StopClientResult)
                {
                    ClientState = LocalConnectionState.Stopping;
                }

                return StopClientResult;
            }
        }
    }
}
