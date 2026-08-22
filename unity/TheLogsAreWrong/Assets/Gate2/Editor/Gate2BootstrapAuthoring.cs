using System;
using System.IO;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using TheLogsAreWrong.Gate3;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLogsAreWrong.Gate2.EditorTools
{
    /// <summary>
    /// Generates the one task-owned Gate-2 bootstrap scene. The scene is committed; this authoring entry
    /// point exists so the scene is produced by the pinned editor rather than hand-written YAML.
    /// </summary>
    public static class Gate2BootstrapAuthoring
    {
        public const string ScenePath = "Assets/Gate2/Bootstrap/Gate2Bootstrap.unity";
        public const string RootName = "Gate2BootstrapRoot";
        private const string C1ArtifactPath = "Assets/Gate2/Configuration/validated-configuration-c1-v1.base64";
        private const string C1ManifestPath = "Assets/Gate2/Configuration/validated-configuration-c1-v1.manifest";

        public static void CreateBootstrapScene()
        {
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                AssetDatabase.SetImporterOverride<Gate2DeploymentTextImporter>(C1ManifestPath);
                AssetDatabase.ImportAsset(C1ManifestPath, ImportAssetOptions.ForceUpdate);

                var root = new GameObject(RootName);
                root.AddComponent<Gate2BootstrapRoot>();
                var owner = root.AddComponent<Gate2ProductionHostDriver>();
                var steamRuntime = new GameObject("Gate3SteamRuntime");
                steamRuntime.SetActive(false);
                steamRuntime.AddComponent<SteamManager>();
                var artifact = AssetDatabase.LoadAssetAtPath<Gate2DeploymentTextAsset>(C1ArtifactPath);
                var manifest = AssetDatabase.LoadAssetAtPath<Gate2DeploymentTextAsset>(C1ManifestPath);
                if (artifact == null || manifest == null)
                {
                    throw new FileNotFoundException("Tracked Gate-2 C1 deployment TextAssets are required before bootstrap scene authoring.");
                }

                var ownerSerialized = new SerializedObject(owner);
                ownerSerialized.FindProperty("_c1ArtifactBase64").objectReferenceValue = artifact;
                ownerSerialized.FindProperty("_c1Manifest").objectReferenceValue = manifest;
                ownerSerialized.FindProperty("_selectedProfileId").stringValue = "learning";
                ownerSerialized.ApplyModifiedPropertiesWithoutUndo();

                var networkManager = root.AddComponent<NetworkManager>();
                var transportManager = root.AddComponent<TransportManager>();
                var transport = root.AddComponent<FishySteamworks.FishySteamworks>();
                transportManager.Transport = transport;

                var spawnablePrefabs = AssetDatabase.LoadMainAssetAtPath("Assets/DefaultPrefabObjects.asset");
                if (spawnablePrefabs == null)
                {
                    throw new FileNotFoundException("FishNet must materialize its empty DefaultPrefabObjects asset before transport bootstrap authoring.");
                }

                var networkManagerSerialized = new SerializedObject(networkManager);
                networkManagerSerialized.FindProperty("_spawnablePrefabs").objectReferenceValue = spawnablePrefabs;
                networkManagerSerialized.FindProperty("_dontDestroyOnLoad").boolValue = false;
                networkManagerSerialized.ApplyModifiedPropertiesWithoutUndo();

                var transportBootstrap = root.AddComponent<Gate3TransportBootstrap>();
                var transportBootstrapSerialized = new SerializedObject(transportBootstrap);
                transportBootstrapSerialized.FindProperty("_networkManager").objectReferenceValue = networkManager;
                transportBootstrapSerialized.FindProperty("_transport").objectReferenceValue = transport;
                transportBootstrapSerialized.ApplyModifiedPropertiesWithoutUndo();

                var lifecycle = root.AddComponent<Gate3TransportLifecycle>();
                var lifecycleSerialized = new SerializedObject(lifecycle);
                lifecycleSerialized.FindProperty("_networkManager").objectReferenceValue = networkManager;
                lifecycleSerialized.FindProperty("_transport").objectReferenceValue = transport;
                lifecycleSerialized.FindProperty("_steamRuntime").objectReferenceValue = steamRuntime;
                lifecycleSerialized.ApplyModifiedPropertiesWithoutUndo();

                var transportSerialized = new SerializedObject(transport);
                var peerToPeer = transportSerialized.FindProperty(Gate3TransportBootstrap.PeerToPeerSerializedProperty);
                if (peerToPeer == null)
                {
                    throw new InvalidOperationException("FishySteamworks no longer exposes the accepted _peerToPeer serialized field.");
                }

                peerToPeer.boolValue = true;
                transportSerialized.ApplyModifiedPropertiesWithoutUndo();

                var cameraGo = new GameObject("Gate2BootstrapCamera");
                cameraGo.transform.SetParent(root.transform, false);
                cameraGo.transform.position = new Vector3(0f, 3f, -8f);
                cameraGo.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
                var camera = cameraGo.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 1f);
                cameraGo.tag = "MainCamera";

                var lightGo = new GameObject("Gate2BootstrapLight");
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;

                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Gate2BootstrapFloor";
                floor.transform.SetParent(root.transform, false);

                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/Gate2/Bootstrap");
                var saved = EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log($"[TLAW052] SCENE_SAVED={saved} path={ScenePath}");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[TLAW052] AUTHORING_OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TLAW052] AUTHORING_EXCEPTION {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                EditorApplication.Exit(2);
            }
        }

        /// <summary>Batch-mode entry point used to regenerate the committed bootstrap scene with the pinned editor.</summary>
        public static void CreateBootstrapSceneAndExit()
        {
            CreateBootstrapScene();
            EditorApplication.Exit(0);
        }
    }
}
