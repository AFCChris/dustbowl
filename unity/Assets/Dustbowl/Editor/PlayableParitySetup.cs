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

            Material sand = Material("NationalSand", Hex(0xE3BE86), 0f);
            Material track = Material("NationalTrack", Hex(0x8A4520), 0f);
            Material trackShoulder = Material("NationalTrackShoulder", Hex(0xB4783C), 0f);
            Material dark = Material("NationalBikeDark", new Color(.025f, .03f, .035f), .38f);
            Material chrome = Material("NationalBikeMetal", new Color(.36f, .39f, .41f), .72f);
            Material skin = Material("NationalRiderSkin", new Color(.62f, .35f, .20f), .15f);
            Material dust = Material("NationalDust", Hex(0xD99A52), 0f);

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
            GameObject playerObject = CreateBike(root.transform, "Player_Motocross_Bike", RiderColors[0], dark, chrome, skin, dust);
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
            RenderSettings.fogColor = Hex(0xE8AC78);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 310f;
            RenderSettings.fogEndDistance = 1200f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex(0x9DBBE8);
            RenderSettings.ambientEquatorColor = Hex(0xC07A72);
            RenderSettings.ambientGroundColor = Hex(0x7D5230);
            RenderSettings.skybox = null;

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
            Material skin,
            Material dustMaterial)
        {
            var bike = new GameObject(name);
            bike.transform.SetParent(parent, false);
            Material body = Material(name + "_Paint", color, .32f);
            Transform rear = Primitive(bike.transform, PrimitiveType.Cylinder, "RearWheel",
                new Vector3(0f, -.12f, -.78f), new Vector3(.46f, .13f, .46f), Quaternion.Euler(0f, 0f, 90f), dark).transform;
            Transform front = Primitive(bike.transform, PrimitiveType.Cylinder, "FrontWheel",
                new Vector3(0f, -.12f, .82f), new Vector3(.46f, .13f, .46f), Quaternion.Euler(0f, 0f, 90f), dark).transform;
            Primitive(rear, PrimitiveType.Cylinder, "RearHub", Vector3.zero, new Vector3(.38f, 1.05f, .38f), Quaternion.identity, chrome);
            Primitive(front, PrimitiveType.Cylinder, "FrontHub", Vector3.zero, new Vector3(.38f, 1.05f, .38f), Quaternion.identity, chrome);
            Tube(bike.transform, "FrameTop", new Vector3(0f, .14f, -.52f), new Vector3(0f, .37f, .45f), .075f, body);
            Tube(bike.transform, "FrameLower", new Vector3(0f, -.01f, -.53f), new Vector3(0f, .26f, .43f), .065f, chrome);
            Tube(bike.transform, "RearSwingarm", new Vector3(0f, -.08f, -.76f), new Vector3(0f, .12f, .22f), .055f, chrome);
            Tube(bike.transform, "FrontFork", new Vector3(0f, -.10f, .79f), new Vector3(0f, .52f, .58f), .055f, chrome);
            Primitive(bike.transform, PrimitiveType.Sphere, "FuelTank", new Vector3(0f, .42f, .10f),
                new Vector3(.30f, .25f, .46f), Quaternion.Euler(-8f, 0f, 0f), body);
            Primitive(bike.transform, PrimitiveType.Cube, "Seat", new Vector3(0f, .43f, -.43f),
                new Vector3(.28f, .11f, .55f), Quaternion.Euler(4f, 0f, 0f), dark);
            Primitive(bike.transform, PrimitiveType.Cube, "FrontNumberPlate", new Vector3(0f, .56f, .62f),
                new Vector3(.34f, .38f, .07f), Quaternion.Euler(-12f, 0f, 0f), body);
            Tube(bike.transform, "Handlebar", new Vector3(-.34f, .66f, .54f), new Vector3(.34f, .66f, .54f), .035f, chrome);

            var rider = new GameObject("Rider");
            rider.transform.SetParent(bike.transform, false);
            rider.transform.localPosition = new Vector3(0f, .48f, -.10f);
            Primitive(rider.transform, PrimitiveType.Sphere, "Torso", new Vector3(0f, .34f, .04f),
                new Vector3(.33f, .46f, .24f), Quaternion.Euler(-16f, 0f, 0f), body);
            Primitive(rider.transform, PrimitiveType.Sphere, "Helmet", new Vector3(0f, .82f, .20f),
                new Vector3(.23f, .25f, .24f), Quaternion.identity, body);
            Primitive(rider.transform, PrimitiveType.Sphere, "Face", new Vector3(0f, .82f, .41f),
                new Vector3(.14f, .12f, .08f), Quaternion.identity, skin);
            Tube(rider.transform, "LeftArm", new Vector3(-.18f, .52f, .10f), new Vector3(-.28f, .20f, .58f), .055f, body);
            Tube(rider.transform, "RightArm", new Vector3(.18f, .52f, .10f), new Vector3(.28f, .20f, .58f), .055f, body);
            Tube(rider.transform, "LeftLeg", new Vector3(-.15f, .13f, -.09f), new Vector3(-.18f, -.27f, -.38f), .075f, body);
            Tube(rider.transform, "RightLeg", new Vector3(.15f, .13f, -.09f), new Vector3(.18f, -.27f, -.38f), .075f, body);

            ParticleSystem particles = CreateDust(bike.transform, dustMaterial);
            BikePresentation presentation = bike.AddComponent<BikePresentation>();
            presentation.Configure(null, null, front, rear, rider.transform, particles);
            return bike;
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
            var dust = new GameObject("DustTrail");
            dust.transform.SetParent(parent, false);
            dust.transform.localPosition = new Vector3(0f, -.32f, -.86f);
            ParticleSystem particles = dust.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(.18f, .58f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(.67f, .39f, .17f, .65f), new Color(.92f, .70f, .40f, .3f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 26f;
            shape.radius = .14f;
            dust.transform.localRotation = Quaternion.Euler(-20f, 180f, 0f);
            ParticleSystemRenderer renderer = dust.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return particles;
        }

        private static void CreateCamera(Transform parent, ArcadeBikeController player, CameraModeController modes)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
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
            sun.intensity = 2.5f;
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

                float y = DustbowlFlatsHeight.Sample(x, z);
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
