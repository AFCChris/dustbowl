using System.Collections;
using System.Linq;
using Dustbowl.Bike;
using Dustbowl.Camera;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dustbowl.Tests
{
    public sealed class BehaviourLabPlayModeTests
    {
        [UnityTest]
        public IEnumerator BehaviourLabIsPlayableAcrossCrestCamerasAndRecovery()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Dustbowl_BehaviourLab", LoadSceneMode.Single);
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            ArcadeBikeController bike = Object.FindFirstObjectByType<ArcadeBikeController>();
            BehaviourLabScenarioController scenarios = Object.FindFirstObjectByType<BehaviourLabScenarioController>();
            CameraModeController modes = Object.FindFirstObjectByType<CameraModeController>();
            Assert.That(bike, Is.Not.Null);
            Assert.That(scenarios, Is.Not.Null);
            Assert.That(modes, Is.Not.Null);
            Assert.That(
                Object.FindObjectsByType<Component>(FindObjectsSortMode.None)
                    .Any(component => component != null && component.GetType().Name == "WheelCollider"),
                Is.False,
                "The Stage 3 player must not use WheelCollider components.");
            Assert.That(Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None), Is.Empty);

            scenarios.Select(BehaviourLabScenario.NaturalRoundedCrest);
            Vector3 start = bike.State.position;
            int initialLandings = bike.LandingCount;
            float maximumAirTime = 0f;
            for (int tick = 0; tick < 600; tick++)
            {
                yield return new WaitForFixedUpdate();
                maximumAirTime = Mathf.Max(maximumAirTime, bike.State.airborneSeconds);
                if (maximumAirTime > 0.3f
                    && bike.LandingCount > initialLandings
                    && bike.State.motionState != BikeMotionState.Airborne)
                {
                    break;
                }
            }

            Assert.That(Vector3.Distance(start, bike.State.position), Is.GreaterThan(20f));
            Assert.That(maximumAirTime, Is.GreaterThan(0.3f));
            Assert.That(bike.LastTakeoffTrigger,
                Is.EqualTo(TakeoffTrigger.RoundedCrest).Or.EqualTo(TakeoffTrigger.LipAndRoundedCrest));

            modes.Set(CameraMode.Chase);
            modes.Next();
            Assert.That(modes.CurrentMode, Is.EqualTo(CameraMode.Close));
            modes.Next();
            Assert.That(modes.CurrentMode, Is.EqualTo(CameraMode.Overhead));
            yield return null;
            Assert.That(UnityEngine.Camera.main, Is.Not.Null);

            bike.ForceWipeout();
            for (int tick = 0; tick < 150 && bike.State.motionState == BikeMotionState.Wipeout; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(bike.State.motionState, Is.EqualTo(BikeMotionState.Grounded));
            Assert.That(bike.State.controlsEnabled, Is.True);
            Assert.That(bike.LastRecoveryDuration, Is.InRange(1.7f, 1.72f));
        }
    }
}
