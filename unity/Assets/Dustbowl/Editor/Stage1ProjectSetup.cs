using System;
using System.Linq;
using Dustbowl.Camera;
using Dustbowl.Core;
using Dustbowl.Input;
using Dustbowl.Telemetry;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Dustbowl.Editor
{
    public static class Stage1ProjectSetup
    {
        private const string Root = "Assets/Dustbowl";
        private const string ScenePath = Root + "/Scenes/Dustbowl_BehaviourLab.unity";
        private const string InputPath = Root + "/Settings/DustbowlInputActions.inputactions";
        private const string RendererPath = Root + "/Settings/Dustbowl_UniversalRenderer.asset";
        private const string PipelinePath = Root + "/Settings/Dustbowl_URP.asset";
        private const string MaterialPath = Root + "/Materials/BehaviourLabGround.mat";

        [MenuItem("Dustbowl/Stage 1/Configure foundation")]
        public static void Configure()
        {
            ConfigureProjectSettings();
            UniversalRenderPipelineAsset pipeline = CreateRenderPipeline();
            CreateBehaviourLabScene(pipeline);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Verify();
            Debug.Log("Dustbowl Stage 1 foundation configured and verified.");
        }

        [MenuItem("Dustbowl/Stage 1/Verify foundation")]
        public static void Verify()
        {
            if (Mathf.Abs(Time.fixedDeltaTime - SimulationTiming.FixedDeltaSeconds) > 0.000001f)
            {
                throw new InvalidOperationException("Fixed timestep is not 60 Hz.");
            }

            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (actions == null)
            {
                throw new InvalidOperationException("Semantic input asset is missing.");
            }

            string[] requiredActions =
            {
                "Throttle", "Brake", "Steer", "AirPitch", "AirWhip", "Reset",
                "CameraNext", "CameraPrevious"
            };
            InputActionMap player = actions.FindActionMap("Player", true);
            foreach (string action in requiredActions)
            {
                player.FindAction(action, true);
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            BehaviourLabBootstrap bootstrap = roots
                .SelectMany(root => root.GetComponentsInChildren<BehaviourLabBootstrap>(true))
                .SingleOrDefault();
            if (bootstrap == null || bootstrap.InputReader == null || bootstrap.Telemetry == null || bootstrap.CameraModes == null)
            {
                throw new InvalidOperationException("Behaviour lab foundation references are incomplete.");
            }

            if (roots.SelectMany(root => root.GetComponentsInChildren<WheelCollider>(true)).Any())
            {
                throw new InvalidOperationException("Stage 1 must not contain WheelColliders.");
            }

            if (roots.SelectMany(root => root.GetComponentsInChildren<Rigidbody>(true)).Any())
            {
                throw new InvalidOperationException("Stage 1 must not contain bike physics bodies.");
            }

            Debug.Log("Dustbowl Stage 1 verification passed.");
        }

        private static void ConfigureProjectSettings()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            ConfigureVisibleMetaFiles();
            PlayerSettings.companyName = "Dustbowl";
            PlayerSettings.productName = "Dustbowl Behaviour Lab";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            ConfigureTimeSettings();

            UnityEngine.Object[] settingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingsAssets.Length == 0)
            {
                throw new InvalidOperationException("Could not load ProjectSettings.asset.");
            }

            var serializedSettings = new SerializedObject(settingsAssets[0]);
            SerializedProperty inputHandler = serializedSettings.FindProperty("activeInputHandler");
            if (inputHandler == null)
            {
                throw new InvalidOperationException("Could not configure the Input System backend.");
            }

            inputHandler.intValue = 1;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureVisibleMetaFiles()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/VersionControlSettings.asset");
            if (assets.Length == 0)
            {
                throw new InvalidOperationException("Could not load VersionControlSettings.asset.");
            }

            var settings = new SerializedObject(assets[0]);
            SerializedProperty mode = settings.FindProperty("m_Mode");
            if (mode == null)
            {
                throw new InvalidOperationException("Could not configure visible Unity meta files.");
            }

            mode.stringValue = "Visible Meta Files";
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureTimeSettings()
        {
            UnityEngine.Object[] timeAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset");
            if (timeAssets.Length == 0)
            {
                throw new InvalidOperationException("Could not load TimeManager.asset.");
            }

            var timeSettings = new SerializedObject(timeAssets[0]);
            SerializedProperty fixedTimestep = timeSettings.FindProperty("Fixed Timestep");
            SerializedProperty maximumTimestep = timeSettings.FindProperty("Maximum Allowed Timestep");
            if (fixedTimestep == null || maximumTimestep == null)
            {
                throw new InvalidOperationException("Could not configure deterministic timing settings.");
            }

            SerializedProperty tickCount = fixedTimestep.FindPropertyRelative("m_Count");
            SerializedProperty rate = fixedTimestep.FindPropertyRelative("m_Rate");
            SerializedProperty rateDenominator = rate?.FindPropertyRelative("m_Denominator");
            SerializedProperty rateNumerator = rate?.FindPropertyRelative("m_Numerator");
            if (tickCount == null || rateDenominator == null || rateNumerator == null)
            {
                throw new InvalidOperationException("Unity's fixed-time serialization layout is unsupported.");
            }

            tickCount.longValue = 1;
            rateDenominator.longValue = 1;
            rateNumerator.longValue = SimulationTiming.FixedHz;
            maximumTimestep.doubleValue = 0.1d;
            timeSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static UniversalRenderPipelineAsset CreateRenderPipeline()
        {
            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            return pipeline;
        }

        private static void CreateBehaviourLabScene(UniversalRenderPipelineAsset pipeline)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Dustbowl_BehaviourLab";

            var root = new GameObject("Dustbowl_BehaviourLab");

            var infrastructure = new GameObject("FoundationInfrastructure");
            infrastructure.transform.SetParent(root.transform);
            DustbowlInputReader input = infrastructure.AddComponent<DustbowlInputReader>();
            DevelopmentTelemetryRecorder telemetry = infrastructure.AddComponent<DevelopmentTelemetryRecorder>();
            CameraModeController modes = infrastructure.AddComponent<CameraModeController>();
            BehaviourLabBootstrap bootstrap = infrastructure.AddComponent<BehaviourLabBootstrap>();
            input.Configure(AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath));
            bootstrap.Configure(input, telemetry, modes);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "GroundReference";
            ground.transform.SetParent(root.transform);
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.GetComponent<MeshRenderer>().sharedMaterial = CreateGroundMaterial();

            var bikeMount = new GameObject("FutureBikeMount_NoController");
            bikeMount.transform.SetParent(root.transform);
            bikeMount.transform.position = new Vector3(0f, 0.55f, 0f);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "BikePlaceholder_PrimitiveOnly";
            marker.transform.SetParent(bikeMount.transform);
            marker.transform.localScale = new Vector3(0.45f, 0.35f, 1.4f);
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());

            var cameraModes = new GameObject("CameraModeAnchors");
            cameraModes.transform.SetParent(root.transform);
            CreateAnchor(cameraModes.transform, "Chase", new Vector3(0f, 3f, -6f));
            CreateAnchor(cameraModes.transform, "Close", new Vector3(0f, 1.8f, -3.5f));
            CreateAnchor(cameraModes.transform, "Overhead", new Vector3(0f, 18f, -2f));

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.position = new Vector3(0f, 5f, -8f);
            cameraObject.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            cameraObject.AddComponent<UnityEngine.Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();

            var sun = new GameObject("Sun");
            sun.transform.SetParent(root.transform);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;

            RenderSettings.sun = light;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static Material CreateGroundMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Lit shader is unavailable.");
            }

            material = new Material(shader) { color = new Color(0.52f, 0.38f, 0.22f) };
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void CreateAnchor(Transform parent, string name, Vector3 position)
        {
            var anchor = new GameObject(name);
            anchor.transform.SetParent(parent);
            anchor.transform.localPosition = position;
        }
    }
}
