using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dustbowl.Bike;
using Dustbowl.Camera;
using Dustbowl.Race;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dustbowl.Tests
{
    public sealed class PlayableParityPlayModeTests
    {
        [UnityTest]
        public IEnumerator NationalRaceStartsMovesEightRidersCyclesCamerasAndRecovers()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Dustbowl_National_Flats", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;

            NationalRaceManager race = Object.FindFirstObjectByType<NationalRaceManager>();
            ArcadeBikeController player = Object.FindFirstObjectByType<ArcadeBikeController>();
            CameraModeController cameras = Object.FindFirstObjectByType<CameraModeController>();
            NationalRaceHud hud = Object.FindFirstObjectByType<NationalRaceHud>();
            Assert.That(race, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(cameras, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            Assert.That(race.Opponents.Count, Is.EqualTo(7));
            Assert.That(race.State, Is.EqualTo(NationalRaceState.PreRace));
            Assert.That(player.State.controlsEnabled, Is.False);
            race.SetAutoThrottle(false);
            Assert.That(race.AutoThrottleEnabled, Is.False);
            Assert.That(player.AutoThrottleEnabled, Is.False);
            race.SetAutoThrottle(true);
            Assert.That(race.AutoThrottleEnabled, Is.True);
            Assert.That(player.AutoThrottleEnabled, Is.True);
            race.BeginRace();
            Assert.That(race.State, Is.EqualTo(NationalRaceState.Countdown));

            Time.timeScale = 20f;
            try
            {
                while (race.State == NationalRaceState.Countdown) yield return null;
                Assert.That(player.State.controlsEnabled, Is.True);
                float[] starts = race.Opponents.Select(value => value.Along).ToArray();
                for (int frame = 0; frame < 20; frame++) yield return null;
                Assert.That(race.Opponents.Where((value, index) =>
                    Mathf.Abs(Mathf.DeltaAngle(starts[index] / race.LapLength * 360f, value.Along / race.LapLength * 360f)) > .1f).Count(),
                    Is.EqualTo(7));

                ParticleSystem[] roost = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
                Assert.That(roost.Length, Is.EqualTo(8));
                Assert.That(roost.All(value => value.emission.rateOverTime.constantMax > 0f
                    || value.emission.rateOverDistance.constantMax > 0f), Is.True);

                var mapPositions = new List<Vector2>();
                hud.CollectMinimapRiderPositions(mapPositions);
                Assert.That(mapPositions.Count, Is.EqualTo(8));
                Vector2 firstOpponentMapPosition = mapPositions[1];
                for (int frame = 0; frame < 10; frame++) yield return null;
                hud.CollectMinimapRiderPositions(mapPositions);
                Assert.That(mapPositions[1], Is.Not.EqualTo(firstOpponentMapPosition));

                cameras.Set(CameraMode.Chase);
                cameras.Next();
                cameras.Next();
                Assert.That(cameras.CurrentMode, Is.EqualTo(CameraMode.Overhead));

                player.ForceWipeout();
                while (player.State.motionState == BikeMotionState.Wipeout) yield return null;
                Assert.That(player.LastRecoveryDuration, Is.InRange(1.7f, 1.75f));
                Assert.That(player.State.controlsEnabled, Is.True);
                race.RequestQuit();
                Assert.That(race.QuitRequested, Is.True);
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }

        [UnityTest]
        public IEnumerator ThreeValidLapsProduceACompleteResultsBoard()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Dustbowl_National_Flats", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;
            NationalRaceManager race = Object.FindFirstObjectByType<NationalRaceManager>();
            Assert.That(race.State, Is.EqualTo(NationalRaceState.PreRace));
            race.BeginRace();
            Time.timeScale = 20f;
            try
            {
                while (race.State == NationalRaceState.Countdown) yield return null;
                race.DevelopmentCompletePlayerLap(45f);
                race.DevelopmentCompletePlayerLap(44f);
                race.DevelopmentCompletePlayerLap(43f);
                Assert.That(race.State, Is.EqualTo(NationalRaceState.Results));
                Assert.That(race.PlayerLapTimes.Count, Is.EqualTo(3));
                Assert.That(race.BestLapTime, Is.EqualTo(43f));
                Assert.That(race.BuildResults().Count, Is.EqualTo(8));
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }
    }
}
