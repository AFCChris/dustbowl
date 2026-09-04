using System;
using System.Linq;
using Dustbowl.Bike;
using Dustbowl.Camera;
using Dustbowl.Core;
using Dustbowl.Telemetry;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Dustbowl.Tests
{
    public sealed class FoundationTests
    {
        private const string InputAssetPath = "Assets/Dustbowl/Settings/DustbowlInputActions.inputactions";
        private const string ScenePath = "Assets/Dustbowl/Scenes/Dustbowl_BehaviourLab.unity";

        [Test]
        public void SemanticInputActionsExist()
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            Assert.That(asset, Is.Not.Null);

            string[] expected =
            {
                "Throttle", "Brake", "Steer", "AirPitch", "AirWhip", "Reset",
                "CameraNext", "CameraPrevious"
            };
            string[] actual = asset.FindActionMap("Player", true).actions.Select(action => action.name).ToArray();
            Assert.That(actual, Is.EquivalentTo(expected));
        }

        [Test]
        public void FixedClockAdvancesAtSixtyHertz()
        {
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(SimulationTiming.FixedDeltaSeconds).Within(0.000001f));

            var clock = new SimulationClock();
            for (int i = 0; i < SimulationTiming.FixedHz; i++)
            {
                clock.Advance();
            }

            Assert.That(clock.Tick, Is.EqualTo(60));
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(1d).Within(1e-10));
        }

        [Test]
        public void LandingContractContainsAllFourOutcomes()
        {
            LandingTier[] expected =
            {
                LandingTier.Clean, LandingTier.Sketchy, LandingTier.Ugly, LandingTier.Wipeout
            };
            Assert.That(expected.All(value => Enum.IsDefined(typeof(LandingTier), value)), Is.True);
        }

        [Test]
        public void TelemetrySampleSerializesComparableFields()
        {
            var sample = new LandingEvent
            {
                airborneDuration = 1.25f,
                touchdownPitchDegrees = -8f,
                alignmentErrorDegrees = 12f,
                impactDownwardSpeed = 3.5f,
                classification = LandingTier.Sketchy
            };

            string json = DevelopmentTelemetryRecorder.Serialize(sample);
            StringAssert.Contains("airborneDuration", json);
            StringAssert.Contains("touchdownPitchDegrees", json);
            StringAssert.Contains("alignmentErrorDegrees", json);
            StringAssert.Contains("impactDownwardSpeed", json);
            StringAssert.Contains("\"classification\":" + (int)LandingTier.Sketchy, json);
        }

        [Test]
        public void ChaseAndOverheadRemainFirstClassModes()
        {
            var host = new GameObject("CameraModesTest");
            try
            {
                var modes = host.AddComponent<CameraModeController>();
                Assert.That(modes.Supports(CameraMode.Chase), Is.True);
                Assert.That(modes.Supports(CameraMode.Overhead), Is.True);
                modes.Set(CameraMode.Overhead);
                Assert.That(modes.CurrentMode, Is.EqualTo(CameraMode.Overhead));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void BehaviourLabSceneLoadsWithFoundationOnly()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                BehaviourLabBootstrap bootstrap = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<BehaviourLabBootstrap>(true))
                    .Single();
                Assert.That(bootstrap.InputReader, Is.Not.Null);
                Assert.That(bootstrap.Telemetry, Is.Not.Null);
                Assert.That(bootstrap.CameraModes, Is.Not.Null);
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WheelCollider>(true)), Is.Empty);
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Rigidbody>(true)), Is.Empty);
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
