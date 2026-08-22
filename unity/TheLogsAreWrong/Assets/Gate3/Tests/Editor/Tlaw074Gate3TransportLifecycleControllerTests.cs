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

            var partial = new TestTransport();
            var partialController = new Gate3TransportLifecycleController(partial, 1f);
            Assert.AreEqual(Gate3TransportLifecycleRequestResult.RequestAccepted, partialController.RequestListenHostStart());
            partialController.AdvanceTime(1f);
            Assert.AreEqual(Gate3TransportLifecycleFailure.StartTimedOut, partialController.LastFailure);
            partial.ServerState = LocalConnectionState.Stopped;
            partialController.ObserveServerConnectionState(LocalConnectionState.Stopped);
            Assert.AreEqual(Gate3TransportLifecyclePhase.Offline, partialController.Phase);
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
