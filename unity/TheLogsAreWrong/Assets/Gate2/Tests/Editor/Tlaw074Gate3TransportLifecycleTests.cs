using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLogsAreWrong.Gate2.Tests
{
    /// <summary>Assembly-boundary-safe scene and source contracts for TLAW-074.</summary>
    public sealed class Tlaw074Gate3TransportLifecycleTests
    {
        private const string ScenePath = "Assets/Gate2/Bootstrap/Gate2Bootstrap.unity";
        private const string RootName = "Gate2BootstrapRoot";

        [Test]
        public void Ordinary_production_bootstrap_remains_offline_with_one_lifecycle_seam()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().Single(gameObject => gameObject.name == RootName);
            var steamRuntime = scene.GetRootGameObjects().Single(gameObject => gameObject.name == "Gate3SteamRuntime");
            var components = root.GetComponents<Component>().Where(component => component != null).ToArray();
            var lifecycle = components.Where(component => component.GetType().FullName == "TheLogsAreWrong.Gate3.Gate3TransportLifecycle").ToArray();
            var transport = components.Single(component => component.GetType().FullName == "FishySteamworks.FishySteamworks");

            Assert.AreEqual(1, lifecycle.Length);
            Assert.IsFalse((bool)lifecycle[0].GetType().GetProperty("IsLifecycleActive").GetValue(lifecycle[0]));
            Assert.IsNotNull(transport, "The lifecycle must use the one serialized Fishy transport; no second transport is allowed.");
            Assert.IsFalse(steamRuntime.activeSelf, "Steam runtime initialization must remain opt-in with the transport lifecycle request.");
            Assert.AreEqual(1, steamRuntime.GetComponents<Component>().Count(component => component != null && component.GetType().Name == "SteamManager"));
        }

        [Test]
        public void Lifecycle_source_is_transport_only_and_preserves_the_explicit_p2p_configuration()
        {
            var lifecycleSource = File.ReadAllText("Assets/Gate3/Transport/Gate3TransportLifecycle.cs");
            foreach (var forbidden in new[]
            {
                "HostSession", "HostTickCadence", "HostTickExecutionService", "ActorId", "IntentEnvelope",
                "SubmitLocalIntent", "NetworkObject", "NetworkBehaviour", "Rpc", "Snapshot"
            })
            {
                StringAssert.DoesNotContain(forbidden, lifecycleSource);
            }

            var transportSource = File.ReadAllText("Assets/Gate3/Transport/Gate3TransportBootstrap.cs");
            StringAssert.Contains("TLAW073_TRANSPORT_INERT", transportSource);
            StringAssert.DoesNotContain("StartConnection", transportSource);
        }
    }
}
