using System;
using FishNet.Managing;
using UnityEngine;

namespace TheLogsAreWrong.Gate3
{
    /// <summary>
    /// Serialized, inert composition marker for the accepted D-017 transport stack. It does not
    /// start a network connection, create gameplay authority, or accept gameplay input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Gate3TransportBootstrap : MonoBehaviour
    {
        /// <summary>FishySteamworks' serialized property that must be explicitly true in the committed scene.</summary>
        public const string PeerToPeerSerializedProperty = "_peerToPeer";

        /// <summary>Runtime marker proving startup leaves the transport composition offline.</summary>
        public const string InertMarker = "TLAW073_TRANSPORT_INERT";

        [SerializeField]
        private NetworkManager _networkManager;

        [SerializeField]
        private FishySteamworks.FishySteamworks _transport;

        private void Awake()
        {
            if (_networkManager == null || _transport == null)
            {
                throw new InvalidOperationException("The accepted Gate-3 transport composition is incomplete.");
            }
        }

        private void Start()
        {
            if (!_networkManager.IsOffline)
            {
                throw new InvalidOperationException("The Gate-3 transport bootstrap must remain offline until a later authorized increment.");
            }

            Debug.Log(InertMarker);
        }
    }
}
