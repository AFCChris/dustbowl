using System;
using System.IO;
using System.Linq;
using Dustbowl.Bike;
using Dustbowl.Camera;
using Dustbowl.Core;
using Dustbowl.Course;
using Dustbowl.Input;
using Dustbowl.Telemetry;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dustbowl.Editor
{
    public static class Stage3ControllerSetup
    {
        private const string Root = "Assets/Dustbowl";
        private const string ScenePath = Root + "/Scenes/Dustbowl_BehaviourLab.unity";
        private const string CrestDefinitionPath = Root + "/Courses/BehaviourLab_NaturalRoundedCrest.asset";
        private const string CrestMeshPath = Root + "/Courses/BehaviourLab_NaturalRoundedCrestMesh.asset";
        private const string TuningPath = Root + "/Settings/WebReferenceArcadeBikeTuning.asset";
        private const string MaterialPath = Root + "/Materials/BehaviourLabGround.mat";

        [MenuItem("Dustbowl/Stage 3/Configure arcade controller")]
        public static void Configure()
        {
            ArcadeBikeTuning tuning = CreateTuning();
            CourseDefinition crestDefinition = CreateCrestDefinition();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = scene.GetRootGameObjects().Single(gameObject => gameObject.name == "Dustbowl_BehaviourLab");
            CourseSurface tabletop = root.GetComponentsInChildren<CourseSurface>(true)
                .Single(surface => surface.SurfaceModel == CourseSurfaceModel.DustbowlFlatsReference);
            CourseSurface crest = CreateCrestSurface(root.transform, crestDefinition);

            Transform infrastructure = FindDirectChild(root.transform, "FoundationInfrastructure");
            DustbowlInputReader input = infrastructure.GetComponent<DustbowlInputReader>();
            DevelopmentTelemetryRecorder telemetry = infrastructure.GetComponent<DevelopmentTelemetryRecorder>();
            CameraModeController modes = infrastructure.GetComponent<CameraModeController>();
            telemetry.ConfigureCapacity(36000);

            DestroyNamed(root.transform, "FutureBikeMount_NoController");
            DestroyNamed(root.transform, "PlayerBike_ArcadeController");
            DestroyNamed(infrastructure, "BehaviourLabScenarioController");
            DestroyNamed(infrastructure, "BehaviourLabDevelopmentHud");

            GameObject bikeObject = CreatePlaceholderBike(root.transform);
            ArcadeBikeController bike = bikeObject.AddComponent<ArcadeBikeController>();
            bike.Configure(tuning, input, telemetry, modes);

            GameObject scenarioObject = new("BehaviourLabScenarioController");
            scenarioObject.transform.SetParent(infrastructure, false);
            BehaviourLabScenarioController scenarios = scenarioObject.AddComponent<BehaviourLabScenarioController>();
            scenarios.Configure(bike, input, telemetry, tabletop, crest);
            scenarios.Select(BehaviourLabScenario.AuthoredTabletop);

            GameObject hudObject = new("BehaviourLabDevelopmentHud");
            hudObject.transform.SetParent(infrastructure, false);
            hudObject.AddComponent<BehaviourLabDevelopmentHud>().Configure(bike, scenarios, modes, telemetry);

            Transform cameraTransform = FindDirectChild(root.transform, "Main Camera");
            ArcadeBikeCamera bikeCamera = cameraTransform.GetComponent<ArcadeBikeCamera>();
            if (bikeCamera == null)
            {
                bikeCamera = cameraTransform.gameObject.AddComponent<ArcadeBikeCamera>();
            }

            bikeCamera.Configure(bike, modes);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Stage1ProjectSetup.Verify();
            Stage2CourseSetup.Verify();
            Verify();
            Debug.Log("Dustbowl Stage 3 arcade controller configured and verified.");
        }

        [MenuItem("Dustbowl/Stage 3/Verify arcade controller")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            ArcadeBikeController bike = roots
                .SelectMany(root => root.GetComponentsInChildren<ArcadeBikeController>(true))
                .SingleOrDefault();
            BehaviourLabScenarioController scenarios = roots
                .SelectMany(root => root.GetComponentsInChildren<BehaviourLabScenarioController>(true))
                .SingleOrDefault();
            ArcadeBikeCamera bikeCamera = roots
                .SelectMany(root => root.GetComponentsInChildren<ArcadeBikeCamera>(true))
                .SingleOrDefault();
            CourseSurface[] surfaces = roots
                .SelectMany(root => root.GetComponentsInChildren<CourseSurface>(true))
                .ToArray();
            if (bike == null || scenarios == null || bikeCamera == null || surfaces.Length != 2)
            {
                throw new InvalidOperationException("The Stage 3 BehaviourLab wiring is incomplete.");
            }

            if (roots.SelectMany(root => root.GetComponentsInChildren<WheelCollider>(true)).Any()
                || roots.SelectMany(root => root.GetComponentsInChildren<Rigidbody>(true)).Any())
            {
                throw new InvalidOperationException("Stage 3 must remain code-controlled without vehicle rigid bodies.");
            }

            CourseSurface crest = surfaces.Single(surface =>
                surface.SurfaceModel == CourseSurfaceModel.RoundedCrestDevelopment);
            if (crest.Definition.Features.Count != 0
                || crest.GetComponent<MeshFilter>().sharedMesh != crest.GetComponent<MeshCollider>().sharedMesh)
            {
                throw new InvalidOperationException("The natural crest must be unfeatured and share render/collision data.");
            }

            VerificationRun authored = RunScenario(scenarios, bike, BehaviourLabScenario.AuthoredTabletop);
            VerificationRun natural = RunScenario(scenarios, bike, BehaviourLabScenario.NaturalRoundedCrest);
            if (natural.trigger != TakeoffTrigger.RoundedCrest
                && natural.trigger != TakeoffTrigger.LipAndRoundedCrest)
            {
                throw new InvalidOperationException(
                    $"Natural crest did not release through recent climb rate: {natural.trigger}.");
            }

            bike.ForceWipeout();
            for (int index = 0; index < 150 && bike.State.motionState == BikeMotionState.Wipeout; index++)
            {
                bike.SimulateTick(default, 1f / 60f);
            }

            if (bike.State.motionState != BikeMotionState.Grounded
                || bike.LastRecoveryDuration < 1.7f
                || bike.LastRecoveryDuration > 1.72f)
            {
                throw new InvalidOperationException(
                    $"Wipeout recovery did not restore control in the web envelope: {bike.LastRecoveryDuration:F3}s.");
            }

            CameraModeController modes = roots
                .SelectMany(root => root.GetComponentsInChildren<CameraModeController>(true))
                .Single();
            modes.Set(CameraMode.Chase);
            modes.Next();
            if (modes.CurrentMode != CameraMode.Close)
            {
                throw new InvalidOperationException("Chase-to-close camera cycling failed.");
            }

            modes.Next();
            if (modes.CurrentMode != CameraMode.Overhead)
            {
                throw new InvalidOperationException("Close-to-overhead camera cycling failed.");
            }

            Debug.Log(
                $"Stage 3 scripted verification passed. "
                + $"Tabletop: trigger={authored.trigger}, takeoff={authored.takeoffSpeed:F2}m/s, "
                + $"climb={authored.climbRate:F2}m/s, air={authored.airTime:F2}s, landing={authored.landing}. "
                + $"Natural crest: trigger={natural.trigger}, takeoff={natural.takeoffSpeed:F2}m/s, "
                + $"climb={natural.climbRate:F2}m/s, air={natural.airTime:F2}s, landing={natural.landing}. "
                + $"Recovery={bike.LastRecoveryDuration:F3}s.");
        }

        [MenuItem("Dustbowl/Stage 3/Build Windows development player")]
        public static void BuildWindowsDevelopment()
        {
            string output = GetCommandLineArgument("-stage3BuildPath");
            if (string.IsNullOrWhiteSpace(output))
            {
                output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../DustbowlStage3Build/DustbowlBehaviourLab.exe"));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Stage 3 Windows build failed: {report.summary.result}.");
            }

            Debug.Log($"Dustbowl Stage 3 Windows development build: {output}");
        }

        private static VerificationRun RunScenario(
            BehaviourLabScenarioController scenarios,
            ArcadeBikeController bike,
            BehaviourLabScenario scenario)
        {
            scenarios.Select(scenario);
            var input = new PlayerInputSnapshot { throttle = 1f };
            int startTakeoffs = bike.TakeoffCount;
            int startLandings = bike.LandingCount;
            float takeoffSpeed = 0f;
            float climbRate = 0f;
            float maximumAir = 0f;
            TakeoffTrigger trigger = TakeoffTrigger.None;
            int observedTakeoffs = startTakeoffs;
            for (int tick = 0; tick < 900; tick++)
            {
                bike.SimulateTick(input, 1f / 60f);
                if (bike.TakeoffCount > observedTakeoffs)
                {
                    observedTakeoffs = bike.TakeoffCount;
                    trigger = bike.LastTakeoffTrigger;
                    takeoffSpeed = bike.State.velocity.magnitude;
                    climbRate = bike.State.recentClimbRate;
                }

                maximumAir = Mathf.Max(maximumAir, bike.State.airborneSeconds);
                if (bike.LandingCount > startLandings
                    && maximumAir > 0.3f
                    && bike.State.motionState != BikeMotionState.Airborne)
                {
                    break;
                }
            }

            if (bike.TakeoffCount <= startTakeoffs
                || bike.LandingCount <= startLandings
                || maximumAir <= 0.3f)
            {
                throw new InvalidOperationException(
                    $"{scenario} did not complete a meaningful takeoff and landing (max air {maximumAir:F3}s).");
            }

            return new VerificationRun
            {
                trigger = trigger,
                takeoffSpeed = takeoffSpeed,
                climbRate = climbRate,
                airTime = maximumAir,
                landing = bike.State.lastLandingOutcome
            };
        }

        private static ArcadeBikeTuning CreateTuning()
        {
            ArcadeBikeTuning tuning = AssetDatabase.LoadAssetAtPath<ArcadeBikeTuning>(TuningPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<ArcadeBikeTuning>();
                AssetDatabase.CreateAsset(tuning, TuningPath);
            }

            return tuning;
        }

        private static CourseDefinition CreateCrestDefinition()
        {
            CourseDefinition definition = AssetDatabase.LoadAssetAtPath<CourseDefinition>(CrestDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CourseDefinition>();
                AssetDatabase.CreateAsset(definition, CrestDefinitionPath);
            }

            RoundedCrestReference.Populate(definition);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static CourseSurface CreateCrestSurface(Transform root, CourseDefinition definition)
        {
            DestroyNamed(root, "NaturalRoundedCrest_DevelopmentOnly");
            var surfaceObject = new GameObject("NaturalRoundedCrest_DevelopmentOnly");
            surfaceObject.transform.SetParent(root, false);
            CourseSurface surface = surfaceObject.AddComponent<CourseSurface>();
            surface.Configure(definition, null, CourseSurfaceModel.RoundedCrestDevelopment);
            Mesh mesh = CreateOrReplaceMesh(CourseSurfaceMeshBuilder.Build(surface), CrestMeshPath);
            surface.Configure(definition, mesh, CourseSurfaceModel.RoundedCrestDevelopment);
            surfaceObject.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            surfaceObject.AddComponent<CourseSurfaceDebugGizmos>().Configure(surface);
            return surface;
        }

        private static Mesh CreateOrReplaceMesh(Mesh generated, string assetPath)
        {
            generated.name = Path.GetFileNameWithoutExtension(assetPath);
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, assetPath);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static GameObject CreatePlaceholderBike(Transform root)
        {
            var bike = new GameObject("PlayerBike_ArcadeController");
            bike.transform.SetParent(root, false);
            Material bodyMaterial = CreateMaterial("Stage3BikeBody", new Color(0.13f, 0.42f, 0.82f));
            Material frontMaterial = CreateMaterial("Stage3BikeFront", new Color(1f, 0.34f, 0.08f));
            Material wheelMaterial = CreateMaterial("Stage3BikeWheel", new Color(0.06f, 0.06f, 0.07f));

            CreatePrimitive(bike.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 0.15f, 0f),
                new Vector3(0.65f, 0.38f, 1.55f), Quaternion.identity, bodyMaterial);
            CreatePrimitive(bike.transform, PrimitiveType.Cube, "FrontDirection", new Vector3(0f, 0.22f, 1.05f),
                new Vector3(0.28f, 0.28f, 0.65f), Quaternion.identity, frontMaterial);
            CreatePrimitive(bike.transform, PrimitiveType.Cylinder, "FrontWheel", new Vector3(0f, -0.25f, 0.82f),
                new Vector3(0.44f, 0.12f, 0.44f), Quaternion.Euler(0f, 0f, 90f), wheelMaterial);
            CreatePrimitive(bike.transform, PrimitiveType.Cylinder, "RearWheel", new Vector3(0f, -0.25f, -0.75f),
                new Vector3(0.44f, 0.12f, 0.44f), Quaternion.Euler(0f, 0f, 90f), wheelMaterial);
            CreatePrimitive(bike.transform, PrimitiveType.Sphere, "RiderMarker", new Vector3(0f, 0.8f, -0.1f),
                new Vector3(0.42f, 0.58f, 0.42f), Quaternion.identity, frontMaterial);
            return bike;
        }

        private static void CreatePrimitive(
            Transform parent,
            PrimitiveType type,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.transform.localRotation = localRotation;
            primitive.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(primitive.GetComponent<Collider>());
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"{Root}/Materials/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = name, color = color };
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }

        private static string GetCommandLineArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }

        private static void DestroyNamed(Transform parent, string name)
        {
            Transform child = FindDirectChild(parent, name);
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private struct VerificationRun
        {
            public TakeoffTrigger trigger;
            public float takeoffSpeed;
            public float climbRate;
            public float airTime;
            public Stage3LandingOutcome landing;
        }
    }
}
