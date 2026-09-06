using System.Linq;
using System.Collections.Generic;
using Dustbowl.Course;
using Dustbowl.Race;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dustbowl.Tests
{
    public sealed class PlayableParityTests
    {
        private const string DefinitionPath = "Assets/Dustbowl/Courses/DustbowlFlats_National.asset";
        private const string ScenePath = "Assets/Dustbowl/Scenes/Dustbowl_National_Flats.unity";
        private const string DesertMeshPath = "Assets/Dustbowl/Courses/DustbowlFlats_NationalDesert.asset";

        [Test]
        public void CompleteNationalCourseMatchesCanonicalWebShape()
        {
            CourseDefinition definition = AssetDatabase.LoadAssetAtPath<CourseDefinition>(DefinitionPath);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.CourseId, Is.EqualTo("flats"));
            Assert.That(definition.LinePoints.Count, Is.EqualTo(685));
            Assert.That(definition.EndAlong, Is.EqualTo(1510.540817f).Within(.01f));
            Assert.That(definition.Features.Count, Is.EqualTo(9));
            Assert.That(definition.SpawnHints.Count, Is.EqualTo(8));
            Assert.That(definition.Features.Count(value => value.kind == CourseFeatureKind.Table), Is.EqualTo(4));
            Assert.That(definition.Features.Count(value => value.kind == CourseFeatureKind.Tabletop), Is.EqualTo(1));
            Assert.That(definition.Features.Count(value => value.kind == CourseFeatureKind.Whoops), Is.EqualTo(3));
            Assert.That(definition.Features.Count(value => value.kind == CourseFeatureKind.Ripples), Is.EqualTo(1));
        }

        [Test]
        public void NationalDirectionPreservesTheWebClockwiseLapAcrossCoordinateHandedness()
        {
            CourseDefinition definition = AssetDatabase.LoadAssetAtPath<CourseDefinition>(DefinitionPath);
            float unityArea = SignedArea(definition.LinePoints, false);
            float reconstructedWebArea = SignedArea(definition.LinePoints, true);

            Assert.That(unityArea, Is.LessThan(0f),
                "The Unity X/Z loop must be mirrored so its traversal is not anti-clockwise.");
            Assert.That(reconstructedWebArea, Is.GreaterThan(0f),
                "Mapping Unity Z back to web Z must reproduce the reviewed web trackPts order.");
        }

        [Test]
        public void DesertRenderMeshCannotBridgeAcrossOrRiseAboveTheRideableCourse()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            CourseSurface surface = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CourseSurface>(true)).Single();
            Mesh desert = AssetDatabase.LoadAssetAtPath<Mesh>(DesertMeshPath);
            Assert.That(desert, Is.Not.Null);

            Vector3[] vertices = desert.vertices;
            float highestDesertGap = float.NegativeInfinity;
            foreach (Vector3 vertex in vertices)
            {
                highestDesertGap = Mathf.Max(highestDesertGap, vertex.y - surface.SampleHeight(vertex));
            }
            Assert.That(highestDesertGap, Is.LessThanOrEqualTo(-.10f),
                "The visual desert must remain below the shared authoritative height.");

            int[] triangles = desert.triangles;
            float nearestTriangleToCourse = float.PositiveInfinity;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 centroid = (vertices[triangles[index]]
                    + vertices[triangles[index + 1]]
                    + vertices[triangles[index + 2]]) / 3f;
                nearestTriangleToCourse = Mathf.Min(
                    nearestTriangleToCourse,
                    surface.SampleCourse(centroid).distanceFromCenter);
            }
            Assert.That(nearestTriangleToCourse, Is.GreaterThanOrEqualTo(26.5f),
                "A desert triangle intrudes into the protected course corridor.");
        }

        [Test]
        public void MinimapTracksTheCoursePlayerAndAllSevenOpponents()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            NationalRaceHud hud = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NationalRaceHud>(true)).Single();
            var positions = new List<Vector2>();
            hud.CollectMinimapRiderPositions(positions);

            Assert.That(hud.MinimapCoursePointCount, Is.EqualTo(685));
            Assert.That(hud.MinimapTrackedRiderCount, Is.EqualTo(8));
            Assert.That(positions.Count, Is.EqualTo(8));
            Assert.That(positions.All(point =>
                point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f), Is.True);
        }

        [Test]
        public void MinimapCourseBakeStaysInsideItsPanelAndDrawsTheWholeLoop()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            NationalRaceHud hud = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NationalRaceHud>(true)).Single();

            const int resolution = 256;
            Texture2D map = hud.BuildCourseMapTexture(resolution);
            try
            {
                Color32[] pixels = map.GetPixels32();
                int borderAlpha = 0;
                int routePixels = 0;
                int minX = resolution, maxX = -1, minY = resolution, maxY = -1;
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        Color32 pixel = pixels[y * resolution + x];
                        bool border = x == 0 || y == 0 || x == resolution - 1 || y == resolution - 1;
                        if (border) borderAlpha = Mathf.Max(borderAlpha, pixel.a);
                        if (pixel.a > 200)
                        {
                            routePixels++;
                            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                        }
                    }
                }

                Assert.That(borderAlpha, Is.EqualTo(0), "Nothing on the baked minimap may touch the panel edge.");
                Assert.That(routePixels, Is.GreaterThan(resolution * 4), "The course stroke must be visible.");
                Assert.That(maxX - minX, Is.GreaterThan(resolution / 2), "The loop must fill the map horizontally.");
                Assert.That(maxY - minY, Is.GreaterThan(resolution / 2), "The loop must fill the map vertically.");
            }
            finally
            {
                Object.DestroyImmediate(map);
            }
        }

        [Test]
        public void DustRoostUsesSoftTexturedTransparentParticlesOnAllEightBikes()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ParticleSystem[] trails = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ParticleSystem>(true))
                .Where(system => system.gameObject.name == "DustTrail")
                .ToArray();
            Assert.That(trails.Length, Is.EqualTo(8));

            var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline
                as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            Assert.That(pipeline, Is.Not.Null);
            Assert.That(pipeline.supportsCameraDepthTexture, Is.True, "Soft particles need the camera depth texture.");

            foreach (ParticleSystem trail in trails)
            {
                Material material = trail.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Particles/Unlit"));
                Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null, "Dust needs the soft puff sprite sheet.");
                Assert.That(material.GetFloat("_Surface"), Is.EqualTo(1f), "Dust must be alpha blended, not opaque.");
                Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
                Assert.That(material.IsKeywordEnabled("_SOFTPARTICLES_ON"), Is.True);
                Assert.That(trail.colorOverLifetime.enabled, Is.True, "Puffs must fade in and out.");
                Assert.That(trail.sizeOverLifetime.enabled, Is.True, "Puffs must expand as they thin.");
                Assert.That(trail.textureSheetAnimation.enabled, Is.True);
                Assert.That(trail.textureSheetAnimation.numTilesY, Is.EqualTo(4));
                Assert.That(trail.main.maxParticles, Is.LessThanOrEqualTo(128), "Keep the per-bike particle budget modest.");
            }
        }

        [Test]
        public void PresentationPassWiresSkyLightingPostProcessingTerrainTexturesAndHudFonts()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            GameObject[] roots = scene.GetRootGameObjects();

            Assert.That(PlayerSettings.colorSpace, Is.EqualTo(ColorSpace.Linear));
            Assert.That(RenderSettings.skybox, Is.Not.Null, "The National scene needs the gradient sky material.");
            Assert.That(RenderSettings.skybox.shader.name, Is.EqualTo("Dustbowl/Sky Gradient"));
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(UnityEngine.Rendering.AmbientMode.Trilight));

            Light sun = roots.SelectMany(root => root.GetComponentsInChildren<Light>(true))
                .Single(light => light.type == LightType.Directional);
            Assert.That(sun.intensity, Is.InRange(1f, 2f), "Linear-space sun intensity must not blow out the sand.");
            Assert.That(sun.transform.forward.y, Is.LessThan(-.5f).And.GreaterThan(-.7f));

            UnityEngine.Camera camera = roots.SelectMany(root => root.GetComponentsInChildren<UnityEngine.Camera>(true)).Single();
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
            Assert.That(camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing, Is.True);

            UnityEngine.Rendering.Volume volume = roots.SelectMany(root => root.GetComponentsInChildren<UnityEngine.Rendering.Volume>(true)).Single();
            Assert.That(volume.isGlobal, Is.True);
            Assert.That(volume.sharedProfile, Is.Not.Null);
            Assert.That(volume.sharedProfile.Has<UnityEngine.Rendering.Universal.Tonemapping>(), Is.True);

            foreach (string objectName in new[] { "Dustbowl_Desert_Landform", "Authoritative_DustbowlFlats_Surface", "Packed_Dirt_Racing_Surface" })
            {
                MeshRenderer renderer = roots.SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true))
                    .Single(value => value.gameObject.name == objectName);
                Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null, $"{objectName} must use a terrain albedo texture.");
            }

            NationalRaceHud hud = roots.SelectMany(root => root.GetComponentsInChildren<NationalRaceHud>(true)).Single();
            Assert.That(hud.HasCustomFonts, Is.True, "The HUD must reference the display and label fonts.");
        }

        [Test]
        public void LapGateRejectsStartLineFarmingAndFinishesAfterThreeRealLaps()
        {
            const float length = 1000f;
            var tracker = new RaceLapTracker();
            tracker.Reset(990f);

            Assert.That(tracker.Update(5f, length, 3, out _), Is.False, "Grid crossing must not count.");
            Assert.That(tracker.Lap, Is.EqualTo(1));
            for (int lap = 1; lap <= 3; lap++)
            {
                tracker.Update(500f, length, 3, out _);
                bool crossed = tracker.Update(990f, length, 3, out _);
                Assert.That(crossed, Is.False);
                Assert.That(tracker.Update(5f, length, 3, out bool finished), Is.True);
                Assert.That(finished, Is.EqualTo(lap == 3));
            }

            Assert.That(tracker.Finished, Is.True);
            Assert.That(tracker.Lap, Is.EqualTo(3));
        }

        [Test]
        public void SceneContainsOnePlayerSevenAiAndGamePresentation()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            NationalRaceManager race = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NationalRaceManager>(true)).Single();
            Assert.That(race.Opponents.Count, Is.EqualTo(7));
            Assert.That(race.TotalRiders, Is.EqualTo(8));
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NationalRaceHud>(true)), Is.Not.Empty);
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BikePresentation>(true)).Count(), Is.EqualTo(8));
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ParticleSystem>(true)).Count(), Is.EqualTo(8));
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WheelCollider>(true)), Is.Empty);
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Rigidbody>(true)), Is.Empty);
        }

        [Test]
        public void FrontEndFlowIsWiredIntoTheNationalSceneWithTheEventCatalogue()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            GameObject[] roots = scene.GetRootGameObjects();
            GameFlowController flow = roots.SelectMany(root => root.GetComponentsInChildren<GameFlowController>(true)).Single();
            NationalRaceHud hud = roots.SelectMany(root => root.GetComponentsInChildren<NationalRaceHud>(true)).Single();
            NationalRaceManager race = roots.SelectMany(root => root.GetComponentsInChildren<NationalRaceManager>(true)).Single();

            Assert.That(hud.HasFlowController, Is.True, "HUD must draw the setup/pause/results plates for the flow controller.");
            Assert.That(flow.Race, Is.SameAs(race));
            Assert.That(flow.Screen, Is.EqualTo(GameFlowScreen.Setup), "Launch must land on the setup screen, not in the race.");
            Assert.That(flow.QuitRequested, Is.False);

            Assert.That(RaceEventCatalog.Count, Is.EqualTo(1));
            RaceEventDefinition flats = flow.SelectedEvent;
            Assert.That(flats.Id, Is.EqualTo(DustbowlFlatsNationalCourse.CourseId));
            Assert.That(flats.Name, Is.EqualTo(DustbowlFlatsNationalCourse.CourseName));
            Assert.That(flats.Laps, Is.EqualTo(race.TotalLaps));
            Assert.That(flats.Riders, Is.EqualTo(race.TotalRiders));
            Assert.That(RaceEventCatalog.Get(-1), Is.SameAs(flats), "Selection wraps so future rounds cycle with left/right.");
        }

        [Test]
        public void AllSevenAiCanCompleteTheNational()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            NationalRaceManager race = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NationalRaceManager>(true)).Single();
            foreach (NationalAIRider opponent in race.Opponents)
            {
                opponent.ResetRider();
                float elapsed = 0f;
                for (int tick = 0; tick < 900 && !opponent.Finished; tick++)
                {
                    elapsed += .25f;
                    opponent.Tick(.25f, true, 0f, elapsed);
                }

                Assert.That(opponent.Finished, Is.True, $"{opponent.RiderName} did not finish.");
                Assert.That(opponent.FinishTime, Is.GreaterThan(0f));
            }
        }

        private static float SignedArea(System.Collections.Generic.IReadOnlyList<CourseLinePoint> points, bool webSpace)
        {
            float twiceArea = 0f;
            for (int index = 0; index < points.Count - 1; index++)
            {
                Vector3 current = points[index].center;
                Vector3 next = points[index + 1].center;
                float currentZ = webSpace ? DustbowlFlatsNationalCourse.WebZFromUnity(current.z) : current.z;
                float nextZ = webSpace ? DustbowlFlatsNationalCourse.WebZFromUnity(next.z) : next.z;
                twiceArea += current.x * nextZ - next.x * currentZ;
            }

            return twiceArea * .5f;
        }
    }
}
