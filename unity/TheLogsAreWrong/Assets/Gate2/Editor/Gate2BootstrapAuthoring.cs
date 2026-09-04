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
        private const string ConnectionBindingScriptPath = "Assets/Gate3/Connection/Gate3ServerConnectionActorBindingBridge.cs";
        private const string IntentCarrierIngressScriptPath = "Assets/Gate3/IntentCarrier/Gate3IntentCarrierIngress.cs";
        private const string ActorResolutionCompositionScriptPath = "Assets/Gate3/ActorResolution/Gate3ActorResolutionComposition.cs";
        private const string ProductionAdmissionCompositionScriptPath = "Assets/Gate3/Admission/Gate3ProductionAdmissionComposition.cs";
        private const string ClientIntentResultCarrierScriptPath = "Assets/Gate3/Results/Gate3ClientIntentResultCarrier.cs";
        private const string ClientIntentDispositionCompositionScriptPath = "Assets/Gate3/Results/Gate3ClientIntentDispositionComposition.cs";

        public static void CreateBootstrapScene()
        {
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                AssetDatabase.SetImporterOverride<Gate2DeploymentTextImporter>(C1ManifestPath);
                AssetDatabase.ImportAsset(C1ManifestPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(ConnectionBindingScriptPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(IntentCarrierIngressScriptPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(ActorResolutionCompositionScriptPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(ProductionAdmissionCompositionScriptPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(ClientIntentResultCarrierScriptPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(ClientIntentDispositionCompositionScriptPath, ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<MonoScript>(ConnectionBindingScriptPath) == null)
                {
                    throw new FileNotFoundException("The TLAW-075 connection binding script must be an imported Unity asset before bootstrap scene authoring.");
                }

                if (AssetDatabase.LoadAssetAtPath<MonoScript>(IntentCarrierIngressScriptPath) == null)
                {
                    throw new FileNotFoundException("The TLAW-079 intent carrier ingress script must be an imported Unity asset before bootstrap scene authoring.");
                }

                if (AssetDatabase.LoadAssetAtPath<MonoScript>(ActorResolutionCompositionScriptPath) == null)
                {
                    throw new FileNotFoundException("The TLAW-080 actor-resolution composition script must be an imported Unity asset before bootstrap scene authoring.");
                }

                if (AssetDatabase.LoadAssetAtPath<MonoScript>(ProductionAdmissionCompositionScriptPath) == null)
                {
                    throw new FileNotFoundException("The TLAW-084 production admission composition script must be an imported Unity asset before bootstrap scene authoring.");
                }

                if (AssetDatabase.LoadAssetAtPath<MonoScript>(ClientIntentResultCarrierScriptPath) == null
                    || AssetDatabase.LoadAssetAtPath<MonoScript>(ClientIntentDispositionCompositionScriptPath) == null)
                {
                    throw new FileNotFoundException("The TLAW-086 result carrier and disposition composition scripts must be imported Unity assets before bootstrap scene authoring.");
                }

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

                var connectionBinding = root.AddComponent<Gate3ServerConnectionActorBindingBridge>();
                var connectionBindingSerialized = new SerializedObject(connectionBinding);
                connectionBindingSerialized.FindProperty("_transport").objectReferenceValue = transport;
                connectionBindingSerialized.ApplyModifiedPropertiesWithoutUndo();

                var intentCarrierIngress = root.AddComponent<Gate3IntentCarrierIngress>();
                var intentCarrierIngressSerialized = new SerializedObject(intentCarrierIngress);
                intentCarrierIngressSerialized.FindProperty("_networkManager").objectReferenceValue = networkManager;
                intentCarrierIngressSerialized.FindProperty("_hostDriver").objectReferenceValue = owner;
                intentCarrierIngressSerialized.ApplyModifiedPropertiesWithoutUndo();

                var actorResolution = root.AddComponent<Gate3ActorResolutionComposition>();
                var actorResolutionSerialized = new SerializedObject(actorResolution);
                actorResolutionSerialized.FindProperty("_carrierIngress").objectReferenceValue = intentCarrierIngress;
                actorResolutionSerialized.FindProperty("_connectionBinding").objectReferenceValue = connectionBinding;
                actorResolutionSerialized.ApplyModifiedPropertiesWithoutUndo();

                var productionAdmission = root.AddComponent<Gate3ProductionAdmissionComposition>();
                var productionAdmissionSerialized = new SerializedObject(productionAdmission);
                productionAdmissionSerialized.FindProperty("_hostDriver").objectReferenceValue = owner;
                productionAdmissionSerialized.FindProperty("_actorResolution").objectReferenceValue = actorResolution;
                productionAdmissionSerialized.ApplyModifiedPropertiesWithoutUndo();

                var resultCarrier = root.AddComponent<Gate3ClientIntentResultCarrier>();
                var resultCarrierSerialized = new SerializedObject(resultCarrier);
                resultCarrierSerialized.FindProperty("_networkManager").objectReferenceValue = networkManager;
                resultCarrierSerialized.FindProperty("_connectionBinding").objectReferenceValue = connectionBinding;
                resultCarrierSerialized.ApplyModifiedPropertiesWithoutUndo();

                var disposition = root.AddComponent<Gate3ClientIntentDispositionComposition>();
                var dispositionSerialized = new SerializedObject(disposition);
                dispositionSerialized.FindProperty("_hostDriver").objectReferenceValue = owner;
                dispositionSerialized.FindProperty("_actorResolution").objectReferenceValue = actorResolution;
                dispositionSerialized.FindProperty("_admission").objectReferenceValue = productionAdmission;
                dispositionSerialized.FindProperty("_connectionBinding").objectReferenceValue = connectionBinding;
                dispositionSerialized.FindProperty("_resultCarrier").objectReferenceValue = resultCarrier;
                dispositionSerialized.ApplyModifiedPropertiesWithoutUndo();

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
