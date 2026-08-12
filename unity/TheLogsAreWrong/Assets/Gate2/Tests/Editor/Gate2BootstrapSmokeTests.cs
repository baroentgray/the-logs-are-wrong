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
    /// TLAW-052 Gate-2 bootstrap smoke. Proves the committed bootstrap scene loads and that Gate 2
    /// carries no networking dependency. It touches no Domain code, no network session and no external
    /// service, so it runs offline in batch mode.
    /// </summary>
    public sealed class Gate2BootstrapSmokeTests
    {
        private const string ScenePath = "Assets/Gate2/Bootstrap/Gate2Bootstrap.unity";
        private const string RootName = "Gate2BootstrapRoot";
        private const string RootComponentTypeName = "Gate2BootstrapRoot";

        /// <summary>Package identifiers that must never appear in a Gate-2 project.</summary>
        private static readonly string[] ForbiddenPackageIds =
        {
            "com.firstgeargames.fishnet",
            "com.firstgeargames.fishysteamworks",
            "com.rlabrecque.steamworks.net",
            "com.unity.netcode",
            "com.unity.transport",
            "com.mirror"
        };

        /// <summary>Assembly-name fragments that would indicate a networking stack was linked in.</summary>
        private static readonly string[] ForbiddenAssemblyFragments =
        {
            "FishNet",
            "FishySteamworks",
            "Steamworks",
            "Netcode",
            "Mirror"
        };

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
        public void Gate2_project_declares_no_networking_package()
        {
            var manifest = File.ReadAllText("Packages/manifest.json");
            var lockPath = "Packages/packages-lock.json";
            var locked = File.Exists(lockPath) ? File.ReadAllText(lockPath) : string.Empty;

            foreach (var id in ForbiddenPackageIds)
            {
                Assert.IsFalse(
                    manifest.Contains(id, System.StringComparison.OrdinalIgnoreCase),
                    $"Networking package '{id}' present in Packages/manifest.json.");
                Assert.IsFalse(
                    locked.Contains(id, System.StringComparison.OrdinalIgnoreCase),
                    $"Networking package '{id}' present in Packages/packages-lock.json.");
            }
        }

        [Test]
        public void Gate2_loads_no_networking_assembly()
        {
            var names = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name ?? string.Empty)
                .ToArray();

            Assert.IsNotEmpty(names, "Assembly scan is vacuous.");

            foreach (var fragment in ForbiddenAssemblyFragments)
            {
                Assert.IsFalse(
                    names.Any(n => n.Contains(fragment, System.StringComparison.OrdinalIgnoreCase)),
                    $"Networking assembly containing '{fragment}' is loaded in the Gate-2 project.");
            }
        }

        private static GameObject FindRoot(Scene scene)
        {
            return scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
        }
    }
}
