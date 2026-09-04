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
