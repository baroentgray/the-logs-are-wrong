using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLogsAreWrong.Gate2.Tests
{
    /// <summary>Executable pinned-Unity contracts for the inert, accepted D-017 transport composition.</summary>
    public sealed class Tlaw073Gate3TransportBootstrapTests
    {
        private const string ScenePath = "Assets/Gate2/Bootstrap/Gate2Bootstrap.unity";
        private const string RootName = "Gate2BootstrapRoot";

        [Test]
        public void Accepted_package_manifest_and_lockfile_resolve_to_the_exact_D017_identities()
        {
            var manifest = File.ReadAllText("Packages/manifest.json");
            var packageLock = File.ReadAllText("Packages/packages-lock.json");

            StringAssert.Contains("\"com.firstgeargames.fishnet\": \"https://github.com/FirstGearGames/FishNet.git?path=Assets/FishNet#4.7.2\"", manifest);
            StringAssert.Contains("\"com.rlabrecque.steamworks.net\": \"https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1\"", manifest);
            StringAssert.Contains("\"hash\": \"de19b5d66459f60400ffd0edc443c4da173a01e7\"", packageLock);
            StringAssert.Contains("\"hash\": \"c21a8f0e31c56ae8707130967faf491f7dd7c0d8\"", packageLock);
        }

        [Test]
        public void Production_bootstrap_has_one_explicit_P2P_transport_and_does_not_start_a_network_session()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().Single(gameObject => gameObject.name == RootName);
            var components = root.GetComponents<Component>().Where(component => component != null).ToArray();

            var networkManagers = components.Where(component => component.GetType().FullName == "FishNet.Managing.NetworkManager").ToArray();
            var transports = components.Where(component => component.GetType().FullName == "FishySteamworks.FishySteamworks").ToArray();
            var bootstraps = components.Where(component => component.GetType().FullName == "TheLogsAreWrong.Gate3.Gate3TransportBootstrap").ToArray();

            Assert.AreEqual(1, networkManagers.Length, "The production bootstrap must compose exactly one FishNet NetworkManager.");
            Assert.AreEqual(1, transports.Length, "The production bootstrap must compose exactly one FishySteamworks transport.");
            Assert.AreEqual(1, bootstraps.Length, "The production bootstrap must expose exactly one inert transport marker.");
            Assert.IsFalse(components.Any(component => component.GetType().FullName == "FishNet.Object.NetworkObject"),
                "TLAW-073 composes transport only; it must not introduce replicated gameplay objects.");

            var serializedNetworkManager = new SerializedObject(networkManagers[0]);
            var spawnablePrefabs = serializedNetworkManager.FindProperty("_spawnablePrefabs");
            Assert.IsNotNull(spawnablePrefabs);
            Assert.IsNotNull(spawnablePrefabs.objectReferenceValue);
            Assert.AreEqual("Assets/DefaultPrefabObjects.asset", AssetDatabase.GetAssetPath(spawnablePrefabs.objectReferenceValue));
            Assert.IsFalse(serializedNetworkManager.FindProperty("_dontDestroyOnLoad").boolValue,
                "The inert transport manager must not move the existing production bootstrap or its HostSession owner between scenes.");

            var serializedTransport = new SerializedObject(transports[0]);
            var peerToPeer = serializedTransport.FindProperty("_peerToPeer");
            Assert.IsNotNull(peerToPeer, "FishySteamworks must expose its accepted serialized P2P configuration.");
            Assert.IsTrue(peerToPeer.boolValue, "D-017 requires explicit _peerToPeer=true; the shipped false default is forbidden.");

            var bootstrapSource = File.ReadAllText("Assets/Gate3/Transport/Gate3TransportBootstrap.cs");
            StringAssert.DoesNotContain("StartConnection", bootstrapSource);
            StringAssert.DoesNotContain("StopConnection", bootstrapSource);
            StringAssert.DoesNotContain("HostSession", bootstrapSource);
            StringAssert.DoesNotContain("IntentEnvelope", bootstrapSource);
            StringAssert.Contains("TLAW073_TRANSPORT_INERT", bootstrapSource);
        }
    }
}
