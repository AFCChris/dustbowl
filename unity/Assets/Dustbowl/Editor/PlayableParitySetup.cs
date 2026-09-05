using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dustbowl.Bike;
using Dustbowl.Camera;
using Dustbowl.Course;
using Dustbowl.Input;
using Dustbowl.Race;
using Dustbowl.Telemetry;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Dustbowl.Editor
{
    public static class PlayableParitySetup
    {
        private const string Root = "Assets/Dustbowl";
        private const string ScenePath = Root + "/Scenes/Dustbowl_National_Flats.unity";
        private const string DefinitionPath = Root + "/Courses/DustbowlFlats_National.asset";
        private const string SurfaceMeshPath = Root + "/Courses/DustbowlFlats_NationalSurface.asset";
        private const string DesertMeshPath = Root + "/Courses/DustbowlFlats_NationalDesert.asset";
        private const string RacingLineMeshPath = Root + "/Courses/DustbowlFlats_NationalRacingLine.asset";
        private const string SoftDustTexturePath = Root + "/Materials/NationalDustSoft.asset";
        private const string SkyMaterialPath = Root + "/Materials/NationalSkyGradient.mat";
        private const string TuningPath = Root + "/Settings/WebReferenceArcadeBikeTuning.asset";
        private const string InputPath = Root + "/Settings/DustbowlInputActions.inputactions";
        private const string BehaviourLabPath = Root + "/Scenes/Dustbowl_BehaviourLab.unity";

        private static readonly Color[] RiderColors =
        {
            new(.88f, .12f, .055f), new(.08f, .36f, .82f), new(.08f, .65f, .20f),
            new(.92f, .68f, .06f), new(.50f, .08f, .78f), new(.82f, .06f, .48f),
            new(.02f, .63f, .77f), new(.91f, .27f, .05f)
        };

        [MenuItem("Dustbowl/Playable parity/Configure Dustbowl Flats National")]
        public static void Configure()
        {
            CourseDefinition definition = CreateDefinition();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Dustbowl_National_Flats";
            var root = new GameObject("Dustbowl_National_DustbowlFlats");

            // Unity's Lambert/PBR response is brighter than the web vertex colours.
            // These are presentation-compensated values from the same Dustbowl Flats palette.
            Material sand = Material("NationalSand", Hex(0xB58F60), 0f);
            Material track = Material("NationalTrack", Hex(0x63351F), 0f);
            Material trackShoulder = Material("NationalTrackShoulder", Hex(0x85512E), 0f);
            Material dark = Material("NationalBikeDark", Hex(0x1B1D22), .12f);
            Material chrome = Material("NationalBikeMetal", Hex(0x9BA1A8), .58f);
            Material white = Material("NationalBikeWhite", Hex(0xEDE7DA), 0f);
            Material skin = Material("NationalRiderSkin", Hex(0xB97550), 0f);
            Material dust = DustMaterial();

            var surfaceObject = new GameObject("Authoritative_DustbowlFlats_Surface");
            surfaceObject.transform.SetParent(root.transform, false);
            CourseSurface surface = surfaceObject.AddComponent<CourseSurface>();
            surface.Configure(definition, null, CourseSurfaceModel.DustbowlFlatsNational);
            Mesh surfaceMesh = SaveMesh(CourseSurfaceMeshBuilder.Build(surface), SurfaceMeshPath);
            surface.Configure(definition, surfaceMesh, CourseSurfaceModel.DustbowlFlatsNational);
            surfaceObject.GetComponent<MeshRenderer>().sharedMaterial = trackShoulder;

            var desertObject = new GameObject("Dustbowl_Desert_Landform");
            desertObject.transform.SetParent(root.transform, false);
            desertObject.AddComponent<MeshFilter>().sharedMesh = SaveMesh(BuildDesertMesh(surface), DesertMeshPath);
            desertObject.AddComponent<MeshRenderer>().sharedMaterial = sand;

            var racingLineObject = new GameObject("Packed_Dirt_Racing_Surface");
            racingLineObject.transform.SetParent(root.transform, false);
            racingLineObject.AddComponent<MeshFilter>().sharedMesh = SaveMesh(BuildRacingLineMesh(surface), RacingLineMeshPath);
            racingLineObject.AddComponent<MeshRenderer>().sharedMaterial = track;

            var infrastructure = new GameObject("RaceInfrastructure");
            infrastructure.transform.SetParent(root.transform, false);
            DustbowlInputReader input = infrastructure.AddComponent<DustbowlInputReader>();
            input.Configure(AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath));
            DevelopmentTelemetryRecorder telemetry = infrastructure.AddComponent<DevelopmentTelemetryRecorder>();
            telemetry.ConfigureCapacity(36000);
            CameraModeController cameraModes = infrastructure.AddComponent<CameraModeController>();

            ArcadeBikeTuning tuning = AssetDatabase.LoadAssetAtPath<ArcadeBikeTuning>(TuningPath);
            GameObject playerObject = CreateBike(root.transform, "Player_Motocross_Bike", RiderColors[0], dark, chrome, white, skin, dust);
            ArcadeBikeController player = playerObject.AddComponent<ArcadeBikeController>();
            player.Configure(tuning, input, telemetry, cameraModes);
            CourseSpawnHint playerSpawn = definition.SpawnHints[0];
            player.SetSurfaceAndSpawn(surface, playerSpawn.along, playerSpawn.lateralOffset);
            ConfigurePresentation(playerObject, player, null);

            var ai = new NationalAIRider[7];
            for (int index = 0; index < ai.Length; index++)
            {
                CourseSpawnHint spawn = definition.SpawnHints[index + 1];
                GameObject aiObject = CreateBike(
                    root.transform,
                    $"AI_{index + 1}_Motocross_Bike",
                    RiderColors[index + 1],
                    dark,
                    chrome,
                    white,
                    skin,
                    dust);
                ai[index] = aiObject.AddComponent<NationalAIRider>();
                ai[index].Configure(
                    $"Rider {index + 1}",
                    index,
                    surface,
                    .78f + index * (.18f / 6f),
                    spawn.along,
                    spawn.lateralOffset);
                ConfigurePresentation(aiObject, null, ai[index]);
            }

            NationalRaceManager race = infrastructure.AddComponent<NationalRaceManager>();
            race.Configure(surface, player, cameraModes, ai);
            NationalRaceHud hud = infrastructure.AddComponent<NationalRaceHud>();
            hud.Configure(race, player);

            CreateCamera(root.transform, player, cameraModes);
            CreateLighting(root.transform);
            CreateStartFinish(root.transform, surface, track, dark, chrome);
            CreateCourseMarkers(root.transform, surface, RiderColors[0], chrome);
            CreateScenery(root.transform, surface);

            RenderSettings.fog = true;
            RenderSettings.fogColor = Hex(0xC18A61);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 280f;
            RenderSettings.fogEndDistance = 1180f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex(0x7086A7);
            RenderSettings.ambientEquatorColor = Hex(0x8B5B50);
            RenderSettings.ambientGroundColor = Hex(0x493525);
            RenderSettings.ambientIntensity = .72f;
            RenderSettings.reflectionIntensity = .28f;
            RenderSettings.skybox = SkyMaterial();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(BehaviourLabPath, true)
            };
            PlayerSettings.companyName = "Dustbowl";
            PlayerSettings.productName = "Dustbowl";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Verify();
            Debug.Log("Playable Unity parity scene configured: Dustbowl Flats National.");
        }

        [MenuItem("Dustbowl/Playable parity/Verify Dustbowl Flats National")]
        public static void Verify()
        {
            CourseDefinition definition = AssetDatabase.LoadAssetAtPath<CourseDefinition>(DefinitionPath);
            if (definition == null || definition.LinePoints.Count < 650 || definition.Features.Count != 9)
            {
                throw new InvalidOperationException("The complete Dustbowl Flats course data is missing or incomplete.");
            }

            if (Mathf.Abs(definition.EndAlong - DustbowlFlatsNationalCourse.ExpectedLapLength) > .75f)
            {
                throw new InvalidOperationException(
                    $"Dustbowl Flats lap length drifted: {definition.EndAlong:F6}m.");
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            NationalRaceManager race = roots.SelectMany(value => value.GetComponentsInChildren<NationalRaceManager>(true)).SingleOrDefault();
            CourseSurface surface = roots.SelectMany(value => value.GetComponentsInChildren<CourseSurface>(true))
                .SingleOrDefault(value => value.SurfaceModel == CourseSurfaceModel.DustbowlFlatsNational);
            if (race == null || surface == null || race.Course != surface || race.Player == null
                || race.Opponents.Count != 7 || race.TotalRiders != 8)
            {
                throw new InvalidOperationException("The National race wiring is incomplete.");
            }

            if (surface.GetComponent<MeshFilter>().sharedMesh != surface.GetComponent<MeshCollider>().sharedMesh)
            {
                throw new InvalidOperationException("Course rendering and collision do not share the authoritative surface mesh.");
            }

            if (roots.SelectMany(value => value.GetComponentsInChildren<WheelCollider>(true)).Any()
                || roots.SelectMany(value => value.GetComponentsInChildren<Rigidbody>(true)).Any())
            {
                throw new InvalidOperationException("Playable parity must remain code-controlled without WheelColliders or bike rigid bodies.");
            }

            CameraModeController modes = roots.SelectMany(value => value.GetComponentsInChildren<CameraModeController>(true)).Single();
            modes.Set(CameraMode.Chase);
            modes.Next();
            modes.Next();
            if (modes.CurrentMode != CameraMode.Overhead)
            {
                throw new InvalidOperationException("Overhead is not reachable as a first-class gameplay camera.");
            }
            modes.Set(CameraMode.Chase);

            if (!EditorBuildSettings.scenes.Any(value => value.enabled && value.path == ScenePath))
            {
                throw new InvalidOperationException("The National scene is not enabled in build settings.");
            }

            NationalRaceHud hud = roots.SelectMany(value => value.GetComponentsInChildren<NationalRaceHud>(true)).Single();
            if (!NationalRaceHud.ReferenceLayoutFits(1280f, 720f)
                || !NationalRaceHud.ReferenceLayoutFits(1280f, 800f))
            {
                throw new InvalidOperationException("The National HUD does not fit the Windows or Steam Deck reference frames.");
            }

            BikePresentation[] presentations = roots
                .SelectMany(value => value.GetComponentsInChildren<BikePresentation>(true)).ToArray();
            ParticleSystem[] dustTrails = roots
                .SelectMany(value => value.GetComponentsInChildren<ParticleSystem>(true)).ToArray();
            if (hud.MinimapTrackedRiderCount != 8 || hud.MinimapCoursePointCount != 685
                || presentations.Length != 8 || dustTrails.Length != 8
                || dustTrails.Any(value => value.GetComponent<ParticleSystemRenderer>().sharedMaterial == null
                    || value.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture == null))
            {
                throw new InvalidOperationException("The National HUD, minimap or eight-rider soft-roost presentation is incomplete.");
            }

            if (RenderSettings.skybox == null
                || RenderSettings.skybox.shader == null
                || RenderSettings.skybox.shader.name != "Dustbowl/SkyGradient")
            {
                throw new InvalidOperationException("The Dustbowl gradient sky is not configured.");
            }

            Debug.Log(
                $"Playable parity verification passed: {definition.LinePoints.Count - 1} course samples, "
                + $"{definition.EndAlong:F3}m, {definition.Features.Count} features, 1 player + 7 AI.");
        }

        public static void VerifyAllStages()
        {
            Stage1ProjectSetup.Verify();
            Stage2CourseSetup.Verify();
            Stage3ControllerSetup.Verify();
            Verify();
            Debug.Log("Stage 1-3 and playable parity verification passed.");
        }

        [MenuItem("Dustbowl/Playable parity/Build Windows development player")]
        public static void BuildWindowsDevelopment()
        {
            Verify();
            string output = GetArgument("-parityBuildPath");
            if (string.IsNullOrWhiteSpace(output))
            {
                output = Path.GetFullPath(Path.Combine(
                    Application.dataPath,
                    "../../../../Dustbowl Builds/PlayableParity/Dustbowl.exe"));
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
                throw new InvalidOperationException($"Playable parity Windows build failed: {report.summary.result}.");
            }

            Debug.Log($"Dustbowl playable parity Windows development build: {output}");
        }

        public static void CaptureGridPreview()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CourseSurface surface = scene.GetRootGameObjects()
                .SelectMany(value => value.GetComponentsInChildren<CourseSurface>(true))
                .Single(value => value.SurfaceModel == CourseSurfaceModel.DustbowlFlatsNational);
            UnityEngine.Camera camera = scene.GetRootGameObjects()
                .SelectMany(value => value.GetComponentsInChildren<UnityEngine.Camera>(true)).Single();
            ArcadeBikeCamera follow = camera.GetComponent<ArcadeBikeCamera>();
            if (follow != null) follow.enabled = false;
            surface.Definition.TrySampleLine(surface.Definition.EndAlong - 2f, out CourseLinePoint start);
            surface.Definition.TrySampleLine(10f, out CourseLinePoint ahead);
            Vector3 forward = ahead.center - start.center;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = new(forward.z, 0f, -forward.x);
            Vector3 target = start.center + forward * 4f + Vector3.up * 1.4f;
            camera.transform.position = target - forward * 18f + right * 11f + Vector3.up * 8f;
            camera.transform.LookAt(target);
            camera.fieldOfView = 58f;

            const int width = 1280;
            const int height = 720;
            var renderTarget = new RenderTexture(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = renderTarget;
            camera.Render();
            RenderTexture.active = renderTarget;
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            string output = GetArgument("-parityCapturePath");
            if (string.IsNullOrWhiteSpace(output))
            {
                output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/PlayableParityPreview.png"));
            }

            File.WriteAllBytes(output, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(renderTarget);
            UnityEngine.Object.DestroyImmediate(image);
            Debug.Log($"Playable parity preview captured: {output}");
        }

        private static CourseDefinition CreateDefinition()
        {
            CourseDefinition definition = AssetDatabase.LoadAssetAtPath<CourseDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CourseDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            DustbowlFlatsNationalCourse.Populate(definition);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static Mesh BuildDesertMesh(CourseSurface surface)
        {
            const int resolution = 160;
            const float size = 900f;
            const float renderOffset = .12f;
            // The authoritative course strip reaches 32 m from centre. Removing
            // desert quads whose centres are inside 28 m leaves a small overlap
            // at the seam, but no coarse uncut triangle can bridge over the road.
            const float exclusionHalfWidth = 28f;
            int width = resolution + 1;
            var vertices = new Vector3[width * width];
            var uv = new Vector2[vertices.Length];
            var triangles = new List<int>(resolution * resolution * 6);
            for (int z = 0; z < width; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float worldX = Mathf.Lerp(-size * .5f, size * .5f, x / (float)resolution);
                    float worldZ = Mathf.Lerp(-size * .5f, size * .5f, z / (float)resolution);
                    int index = z * width + x;
                    Vector3 world = new(worldX, 0f, worldZ);
                    vertices[index] = new Vector3(worldX, surface.SampleHeight(world) - renderOffset, worldZ);
                    uv[index] = new Vector2(x / (float)resolution, z / (float)resolution) * 18f;
                }
            }

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float centerX = Mathf.Lerp(-size * .5f, size * .5f, (x + .5f) / resolution);
                    float centerZ = Mathf.Lerp(-size * .5f, size * .5f, (z + .5f) / resolution);
                    CourseSample sample = surface.SampleCourse(new Vector3(centerX, 0f, centerZ));
                    if (sample.isValid && sample.distanceFromCenter < exclusionHalfWidth)
                    {
                        continue;
                    }

                    int current = z * width + x;
                    triangles.Add(current);
                    triangles.Add(current + width);
                    triangles.Add(current + 1);
                    triangles.Add(current + 1);
                    triangles.Add(current + width);
                    triangles.Add(current + width + 1);
                }
            }

            var mesh = new Mesh { name = "DustbowlFlats_NationalDesert", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildRacingLineMesh(CourseSurface surface)
        {
            IReadOnlyList<CourseLinePoint> points = surface.Definition.LinePoints;
            const int rows = 4;
            var vertices = new Vector3[points.Count * rows];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(points.Count - 1) * (rows - 1) * 6];
            for (int index = 0; index < points.Count; index++)
            {
                int previous = Mathf.Max(0, index - 1);
                int next = Mathf.Min(points.Count - 1, index + 1);
                Vector3 forward = points[next].center - points[previous].center;
                forward.y = 0f;
                forward.Normalize();
                Vector3 right = new(forward.z, 0f, -forward.x);
                for (int row = 0; row < rows; row++)
                {
                    float lateralT = row / (float)(rows - 1);
                    float lateral = Mathf.Lerp(-8.35f, 8.35f, lateralT);
                    Vector3 point = points[index].center + right * lateral;
                    point.y = surface.SampleHeight(point) + .035f;
                    int vertex = index * rows + row;
                    vertices[vertex] = point;
                    uv[vertex] = new Vector2(lateralT, points[index].along * .055f);
                }
            }

            int triangle = 0;
            for (int index = 0; index < points.Count - 1; index++)
            {
                for (int row = 0; row < rows - 1; row++)
                {
                    int current = index * rows + row;
                    triangles[triangle++] = current;
                    triangles[triangle++] = current + rows;
                    triangles[triangle++] = current + 1;
                    triangles[triangle++] = current + 1;
                    triangles[triangle++] = current + rows;
                    triangles[triangle++] = current + rows + 1;
                }
            }

            var mesh = new Mesh { name = "DustbowlFlats_NationalRacingLine", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject CreateBike(
            Transform parent,
            string name,
            Color color,
            Material dark,
            Material chrome,
            Material white,
            Material skin,
            Material dustMaterial)
        {
            var bike = new GameObject(name);
            bike.transform.SetParent(parent, false);
            Material body = Material(name + "_Paint", color, .08f);
            Material jersey = Material(name + "_Jersey", Color.Lerp(color, Color.white, .18f), 0f);
            Transform rear = CreateWheel(bike.transform, "RearWheel", new Vector3(0f, -.12f, -.78f), dark, chrome);
            Transform front = CreateWheel(bike.transform, "FrontWheel", new Vector3(0f, -.12f, .82f), dark, chrome);

            Tube(bike.transform, "FrameTop", new Vector3(0f, .12f, -.53f), new Vector3(0f, .37f, .42f), .065f, body);
            Tube(bike.transform, "FrameLower", new Vector3(0f, -.01f, -.50f), new Vector3(0f, .24f, .39f), .055f, chrome);
            Tube(bike.transform, "RearSwingarm", new Vector3(0f, -.08f, -.75f), new Vector3(0f, .10f, .18f), .05f, chrome);
            Tube(bike.transform, "FrontForkLeft", new Vector3(-.075f, -.10f, .79f), new Vector3(-.075f, .53f, .57f), .045f, chrome);
            Tube(bike.transform, "FrontForkRight", new Vector3(.075f, -.10f, .79f), new Vector3(.075f, .53f, .57f), .045f, chrome);
            Primitive(bike.transform, PrimitiveType.Cube, "Engine", new Vector3(0f, .12f, -.05f),
                new Vector3(.34f, .31f, .34f), Quaternion.Euler(-4f, 0f, 0f), dark);
            Primitive(bike.transform, PrimitiveType.Sphere, "FuelTank", new Vector3(0f, .42f, .10f),
                new Vector3(.31f, .24f, .45f), Quaternion.Euler(-8f, 0f, 0f), body);
            Primitive(bike.transform, PrimitiveType.Cube, "Seat", new Vector3(0f, .44f, -.42f),
                new Vector3(.29f, .10f, .58f), Quaternion.Euler(4f, 0f, 0f), dark);
            Primitive(bike.transform, PrimitiveType.Cube, "LeftSidePanel", new Vector3(-.20f, .31f, -.25f),
                new Vector3(.055f, .28f, .44f), Quaternion.Euler(2f, 0f, -8f), body);
            Primitive(bike.transform, PrimitiveType.Cube, "RightSidePanel", new Vector3(.20f, .31f, -.25f),
                new Vector3(.055f, .28f, .44f), Quaternion.Euler(2f, 0f, 8f), body);
            Primitive(bike.transform, PrimitiveType.Cube, "FrontNumberPlate", new Vector3(0f, .58f, .63f),
                new Vector3(.35f, .36f, .065f), Quaternion.Euler(-12f, 0f, 0f), white);
            Primitive(bike.transform, PrimitiveType.Cube, "FrontFender", new Vector3(0f, .29f, .76f),
                new Vector3(.30f, .055f, .62f), Quaternion.Euler(-7f, 0f, 0f), body);
            Primitive(bike.transform, PrimitiveType.Cube, "RearFender", new Vector3(0f, .43f, -.72f),
                new Vector3(.30f, .055f, .56f), Quaternion.Euler(10f, 0f, 0f), body);
            Tube(bike.transform, "Handlebar", new Vector3(-.36f, .68f, .55f), new Vector3(.36f, .68f, .55f), .03f, chrome);
            Tube(bike.transform, "Exhaust", new Vector3(.18f, .16f, -.10f), new Vector3(.22f, .35f, -.67f), .045f, chrome);

            var rider = new GameObject("Rider");
            rider.transform.SetParent(bike.transform, false);
            rider.transform.localPosition = new Vector3(0f, .48f, -.10f);
            Primitive(rider.transform, PrimitiveType.Capsule, "Torso", new Vector3(0f, .35f, .06f),
                new Vector3(.31f, .34f, .22f), Quaternion.Euler(22f, 0f, 0f), jersey);
            Primitive(rider.transform, PrimitiveType.Cube, "ChestPanel", new Vector3(0f, .43f, .24f),
                new Vector3(.30f, .28f, .05f), Quaternion.Euler(-18f, 0f, 0f), white);
            Primitive(rider.transform, PrimitiveType.Cube, "Hips", new Vector3(0f, .08f, -.10f),
                new Vector3(.30f, .18f, .24f), Quaternion.Euler(5f, 0f, 0f), dark);
            Primitive(rider.transform, PrimitiveType.Sphere, "Helmet", new Vector3(0f, .82f, .20f),
                new Vector3(.24f, .26f, .25f), Quaternion.identity, jersey);
            Primitive(rider.transform, PrimitiveType.Cube, "HelmetVisor", new Vector3(0f, .85f, .43f),
                new Vector3(.24f, .08f, .09f), Quaternion.Euler(-8f, 0f, 0f), dark);
            Primitive(rider.transform, PrimitiveType.Cube, "HelmetPeak", new Vector3(0f, .99f, .35f),
                new Vector3(.26f, .035f, .20f), Quaternion.Euler(-10f, 0f, 0f), white);
            Primitive(rider.transform, PrimitiveType.Sphere, "Face", new Vector3(0f, .78f, .415f),
                new Vector3(.13f, .10f, .07f), Quaternion.identity, skin);
            CapsuleLimb(rider.transform, "LeftArm", new Vector3(-.18f, .55f, .10f), new Vector3(-.29f, .20f, .57f), .065f, jersey);
            CapsuleLimb(rider.transform, "RightArm", new Vector3(.18f, .55f, .10f), new Vector3(.29f, .20f, .57f), .065f, jersey);
            CapsuleLimb(rider.transform, "LeftLeg", new Vector3(-.14f, .12f, -.10f), new Vector3(-.20f, -.26f, -.38f), .085f, dark);
            CapsuleLimb(rider.transform, "RightLeg", new Vector3(.14f, .12f, -.10f), new Vector3(.20f, -.26f, -.38f), .085f, dark);
            Primitive(rider.transform, PrimitiveType.Cube, "LeftBoot", new Vector3(-.20f, -.30f, -.31f),
                new Vector3(.13f, .13f, .29f), Quaternion.Euler(-12f, 0f, 0f), dark);
            Primitive(rider.transform, PrimitiveType.Cube, "RightBoot", new Vector3(.20f, -.30f, -.31f),
                new Vector3(.13f, .13f, .29f), Quaternion.Euler(-12f, 0f, 0f), dark);

            ParticleSystem particles = CreateDust(bike.transform, dustMaterial);
            BikePresentation presentation = bike.AddComponent<BikePresentation>();
            presentation.Configure(null, null, front, rear, rider.transform, particles);
            return bike;
        }

        private static Transform CreateWheel(
            Transform parent,
            string name,
            Vector3 position,
            Material tyre,
            Material metal)
        {
            GameObject wheel = Primitive(
                parent,
                PrimitiveType.Cylinder,
                name,
                position,
                new Vector3(.46f, .12f, .46f),
                Quaternion.Euler(0f, 0f, 90f),
                tyre);
            Primitive(wheel.transform, PrimitiveType.Cylinder, "Rim",
                Vector3.zero, new Vector3(.31f, 1.08f, .31f), Quaternion.identity, metal);
            Primitive(wheel.transform, PrimitiveType.Cylinder, "Hub",
                Vector3.zero, new Vector3(.12f, 1.16f, .12f), Quaternion.identity, tyre);
            Primitive(wheel.transform, PrimitiveType.Cube, "SpokeVertical",
                Vector3.zero, new Vector3(.055f, .035f, .68f), Quaternion.identity, metal);
            Primitive(wheel.transform, PrimitiveType.Cube, "SpokeHorizontal",
                Vector3.zero, new Vector3(.68f, .035f, .055f), Quaternion.identity, metal);
            return wheel.transform;
        }

        private static GameObject CapsuleLimb(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float radius,
            Material material)
        {
            Vector3 delta = end - start;
            return Primitive(
                parent,
                PrimitiveType.Capsule,
                name,
                (start + end) * .5f,
                new Vector3(radius * 2f, delta.magnitude * .5f, radius * 2f),
                Quaternion.FromToRotation(Vector3.up, delta.normalized),
                material);
        }

        private static void ConfigurePresentation(GameObject bike, ArcadeBikeController player, NationalAIRider ai)
        {
            BikePresentation presentation = bike.GetComponent<BikePresentation>();
            presentation.Configure(
                player,
                ai,
                bike.transform.Find("FrontWheel"),
                bike.transform.Find("RearWheel"),
                bike.transform.Find("Rider"),
                bike.GetComponentInChildren<ParticleSystem>());
        }

        private static ParticleSystem CreateDust(Transform parent, Material material)
        {
            var dust = new GameObject("Soft_Roost_Trail");
            dust.transform.SetParent(parent, false);
            dust.transform.localPosition = new Vector3(0f, -.27f, -.83f);
            ParticleSystem particles = dust.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.55f, 1.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(.42f, 1.15f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-.55f, .55f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(.67f, .48f, .29f, .34f), new Color(.91f, .76f, .55f, .18f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 260;
            main.gravityModifier = -.035f;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 21f;
            shape.radius = .11f;
            shape.length = .42f;
            dust.transform.localRotation = Quaternion.Euler(-28f, 180f, 0f);

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-.55f, .55f);
            velocity.y = new ParticleSystem.MinMaxCurve(.35f, 1.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(-.45f, .25f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(.25f, .70f);
            noise.frequency = .32f;
            noise.scrollSpeed = .18f;
            noise.damping = true;

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, .45f),
                new Keyframe(.35f, 1.05f),
                new Keyframe(1f, 1.65f)));

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(.83f, .70f, .52f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(.72f, 0f),
                    new GradientAlphaKey(.30f, .48f),
                    new GradientAlphaKey(0f, 1f)
                });
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;

            ParticleSystemRenderer renderer = dust.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
            renderer.sortingFudge = 1.5f;
            return particles;
        }

        private static void CreateCamera(Transform parent, ArcadeBikeController player, CameraModeController modes)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = Hex(0x1D3A63);
            camera.nearClipPlane = .2f;
            camera.farClipPlane = 1800f;
            camera.fieldOfView = 62f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraObject.AddComponent<ArcadeBikeCamera>().Configure(player, modes);
            cameraObject.transform.position = player.transform.position + new Vector3(0f, 4f, -8f);
            cameraObject.transform.LookAt(player.transform.position + Vector3.up);
        }

        private static void CreateLighting(Transform parent)
        {
            var sunObject = new GameObject("DesertSun");
            sunObject.transform.SetParent(parent, false);
            sunObject.transform.rotation = Quaternion.Euler(54f, -138f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex(0xFFD6A0);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .72f;
            RenderSettings.sun = sun;
        }

        private static void CreateStartFinish(Transform parent, CourseSurface surface, Material track, Material dark, Material chrome)
        {
            surface.Definition.TrySampleLine(0f, out CourseLinePoint line);
            surface.Definition.TrySampleLine(3f, out CourseLinePoint ahead);
            Vector3 forward = (ahead.center - line.center).normalized;
            Vector3 right = new(forward.z, 0f, -forward.x);
            Vector3 center = line.center;
            center.y = surface.SampleHeight(center);
            var gate = new GameObject("Dustbowl_StartFinish_Gate");
            gate.transform.SetParent(parent, false);
            gate.transform.SetPositionAndRotation(center, Quaternion.LookRotation(forward, Vector3.up));
            Tube(gate.transform, "LeftPost", new Vector3(-10.5f, 0f, 0f), new Vector3(-10.5f, 6.2f, 0f), .20f, chrome);
            Tube(gate.transform, "RightPost", new Vector3(10.5f, 0f, 0f), new Vector3(10.5f, 6.2f, 0f), .20f, chrome);
            Primitive(gate.transform, PrimitiveType.Cube, "DUSTBOWL_NATIONAL_Banner", new Vector3(0f, 5.7f, 0f),
                new Vector3(21f, 1.25f, .35f), Quaternion.identity, track);
            for (int tile = 0; tile < 12; tile++)
            {
                Primitive(gate.transform, PrimitiveType.Cube, $"FinishTile_{tile}", new Vector3(-8.25f + tile * 1.5f, .025f, 0f),
                    new Vector3(1.5f, .05f, 1.25f), Quaternion.identity, tile % 2 == 0 ? chrome : dark);
            }
        }

        private static void CreateCourseMarkers(Transform parent, CourseSurface surface, Color accent, Material chrome)
        {
            Material flag = Material("NationalCourseMarker", accent, .18f);
            var markers = new GameObject("Course_Markers_And_Banners");
            markers.transform.SetParent(parent, false);
            float length = surface.Definition.EndAlong;
            for (int index = 0; index < 48; index++)
            {
                float along = index / 48f * length;
                surface.Definition.TrySampleLine(along, out CourseLinePoint line);
                surface.Definition.TrySampleLine(Mathf.Repeat(along + 2f, length), out CourseLinePoint ahead);
                Vector3 forward = (ahead.center - line.center).normalized;
                Vector3 right = new(forward.z, 0f, -forward.x);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 position = line.center + right * side * 13.3f;
                    position.y = surface.SampleHeight(position);
                    var marker = new GameObject($"Marker_{index}_{side}");
                    marker.transform.SetParent(markers.transform, false);
                    marker.transform.position = position;
                    Tube(marker.transform, "Stake", Vector3.zero, Vector3.up * 1.55f, .035f, chrome);
                    Primitive(marker.transform, PrimitiveType.Cube, "Flag", new Vector3(side * .24f, 1.34f, 0f),
                        new Vector3(.48f, .28f, .045f), Quaternion.identity, flag);
                }
            }
        }

        private static void CreateScenery(Transform parent, CourseSurface surface)
        {
            Material rock = Material("NationalRock", Hex(0x9A8A7C), 0f);
            Material scrub = Material("NationalScrub", Hex(0x8B8A4E), 0f);
            Material trunk = Material("NationalScrubStem", Hex(0x6D4A2C), 0f);
            var scenery = new GameObject("Desert_Scenery");
            scenery.transform.SetParent(parent, false);
            var random = new System.Random(1805);
            int made = 0;
            for (int attempt = 0; attempt < 500 && made < 110; attempt++)
            {
                float x = Mathf.Lerp(-405f, 405f, (float)random.NextDouble());
                float z = Mathf.Lerp(-405f, 405f, (float)random.NextDouble());
                Vector3 query = new(x, 0f, z);
                CourseSample course = surface.SampleCourse(query);
                if (course.distanceFromCenter < 37f || new Vector2(x, z).magnitude > 430f)
                {
                    continue;
                }

                float y = DustbowlFlatsNationalCourse.SampleNaturalHeight(x, z);
                float scale = Mathf.Lerp(.55f, 2.4f, (float)random.NextDouble());
                if (made % 3 == 0)
                {
                    GameObject boulder = Primitive(scenery.transform, PrimitiveType.Sphere, $"Rock_{made}",
                        new Vector3(x, y + scale * .28f, z), new Vector3(scale, scale * .62f, scale * .8f),
                        Quaternion.Euler(0f, (float)random.NextDouble() * 180f, 0f), rock);
                    boulder.transform.localScale *= Mathf.Lerp(.7f, 1.4f, (float)random.NextDouble());
                }
                else
                {
                    var shrub = new GameObject($"Scrub_{made}");
                    shrub.transform.SetParent(scenery.transform, false);
                    shrub.transform.position = new Vector3(x, y, z);
                    Tube(shrub.transform, "Stem", Vector3.zero, Vector3.up * scale, .035f * scale, trunk);
                    for (int branch = 0; branch < 3; branch++)
                    {
                        float angle = branch * 120f + (float)random.NextDouble() * 25f;
                        Vector3 end = Quaternion.Euler(0f, angle, 0f) * new Vector3(.38f * scale, .65f * scale, 0f);
                        Tube(shrub.transform, $"Branch_{branch}", Vector3.up * .25f * scale, end, .055f * scale, scrub);
                    }
                }

                made++;
            }
        }

        private static GameObject Primitive(
            Transform parent, PrimitiveType type, string name, Vector3 position,
            Vector3 scale, Quaternion rotation, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.transform.localRotation = rotation;
            primitive.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(primitive.GetComponent<Collider>());
            return primitive;
        }

        private static GameObject Tube(Transform parent, string name, Vector3 start, Vector3 end, float radius, Material material)
        {
            Vector3 delta = end - start;
            return Primitive(parent, PrimitiveType.Cylinder, name, (start + end) * .5f,
                new Vector3(radius, delta.magnitude * .5f, radius),
                Quaternion.FromToRotation(Vector3.up, delta.normalized), material);
        }

        private static Material Material(string name, Color color, float metallic)
        {
            string path = $"{Root}/Materials/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", metallic > .5f ? .62f : .18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material DustMaterial()
        {
            const string path = Root + "/Materials/NationalDust.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP particle shader is unavailable.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "NationalDust" };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            Texture2D texture = SoftDustTexture();
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", Color.white);
            material.color = Color.white;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D SoftDustTexture()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftDustTexturePath);
            if (texture == null)
            {
                const int size = 64;
                texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "NationalDustSoft",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float px = (x + .5f) / size * 2f - 1f;
                        float py = (y + .5f) / size * 2f - 1f;
                        float alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(px * px + py * py)), 1.55f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply();
                AssetDatabase.CreateAsset(texture, SoftDustTexturePath);
            }

            return texture;
        }

        private static Material SkyMaterial()
        {
            Shader shader = Shader.Find("Dustbowl/SkyGradient");
            if (shader == null)
            {
                throw new InvalidOperationException("The Dustbowl gradient sky shader is unavailable.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "NationalSkyGradient" };
                AssetDatabase.CreateAsset(material, SkyMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_TopColor", Hex(0x1D3A63));
            material.SetColor("_MidColor", Hex(0xC07A72));
            material.SetColor("_HazeColor", Hex(0xF6B077));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Color Hex(uint rgb)
        {
            Color srgb = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? srgb.linear : srgb;
        }

        private static Mesh SaveMesh(Mesh generated, string path)
        {
            generated.name = Path.GetFileNameWithoutExtension(path);
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static string GetArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }
    }
}
