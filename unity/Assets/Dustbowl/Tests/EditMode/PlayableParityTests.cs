using System.Linq;
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
    }
}
