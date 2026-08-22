using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLogsAreWrong.Gate2.Tests
{
    /// <summary>
    /// TLAW-052 bootstrap smoke. Proves the committed production bootstrap scene loads with its one
    /// HostSession owner. TLAW-073 verifies the accepted inert transport composition separately; these
    /// tests still touch no Domain input, network session, or external service.
    /// </summary>
    public sealed class Gate2BootstrapSmokeTests
    {
        private const string ScenePath = "Assets/Gate2/Bootstrap/Gate2Bootstrap.unity";
        private const string RootName = "Gate2BootstrapRoot";
        private const string RootComponentTypeName = "Gate2BootstrapRoot";
        private const string ProductionOwnerTypeName = "Gate2ProductionHostDriver";

        [Test]
        public void Bootstrap_scene_asset_exists()
        {
            Assert.IsTrue(File.Exists(ScenePath), $"Bootstrap scene asset missing at {ScenePath}.");
            var guid = AssetDatabase.AssetPathToGUID(ScenePath);
            Assert.IsFalse(string.IsNullOrEmpty(guid), "Bootstrap scene is not registered in the AssetDatabase.");
        }

        [Test]
        public void Bootstrap_scene_opens_and_contains_the_root()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "Bootstrap scene did not open as a valid scene.");
            Assert.IsTrue(scene.isLoaded, "Bootstrap scene opened but is not loaded.");

            var root = FindRoot(scene);
            Assert.IsNotNull(root, $"'{RootName}' was not found in the bootstrap scene.");

            var hasMarker = root.GetComponents<Component>()
                .Where(c => c != null)
                .Any(c => c.GetType().Name == RootComponentTypeName);
            Assert.IsTrue(hasMarker, $"'{RootName}' does not carry the {RootComponentTypeName} component.");
        }

        [Test]
        public void Bootstrap_objects_have_no_missing_scripts()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = FindRoot(scene);
            Assert.IsNotNull(root, $"'{RootName}' was not found in the bootstrap scene.");

            foreach (var transform in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                var components = transform.gameObject.GetComponents<Component>();
                for (var i = 0; i < components.Length; i++)
                {
                    Assert.IsNotNull(
                        components[i],
                        $"Missing script on '{transform.name}' at component index {i}.");
                }
            }
        }

        [Test]
        public void Bootstrap_scene_wires_exactly_one_production_owner_with_tracked_C1_text_assets()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = FindRoot(scene);
            Assert.IsNotNull(root);

            var owners = root.GetComponents<Component>()
                .Where(component => component != null && component.GetType().Name == ProductionOwnerTypeName)
                .ToArray();
            Assert.AreEqual(1, owners.Length, "The bootstrap scene must wire exactly one production HostSession owner.");
            Assert.AreEqual(ProductionOwnerTypeName, owners[0].GetType().Name);

            var serialized = new SerializedObject(owners[0]);
            var artifact = serialized.FindProperty("_c1ArtifactBase64").objectReferenceValue;
            var manifest = serialized.FindProperty("_c1Manifest").objectReferenceValue;
            Assert.IsNotNull(artifact);
            Assert.IsNotNull(manifest);
            Assert.AreEqual("Assets/Gate2/Configuration/validated-configuration-c1-v1.base64", AssetDatabase.GetAssetPath(artifact));
            Assert.AreEqual("Assets/Gate2/Configuration/validated-configuration-c1-v1.manifest", AssetDatabase.GetAssetPath(manifest));
            Assert.AreEqual("learning", serialized.FindProperty("_selectedProfileId").stringValue);
        }

        private static GameObject FindRoot(Scene scene)
        {
            return scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
        }
    }
}
