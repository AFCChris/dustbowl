using System.Collections;
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
            Assert.That(race, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(cameras, Is.Not.Null);
            Assert.That(race.Opponents.Count, Is.EqualTo(7));
            Assert.That(race.State, Is.EqualTo(NationalRaceState.Countdown));
            Assert.That(player.State.controlsEnabled, Is.False);

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

                cameras.Set(CameraMode.Chase);
                cameras.Next();
                cameras.Next();
                Assert.That(cameras.CurrentMode, Is.EqualTo(CameraMode.Overhead));

                player.ForceWipeout();
                while (player.State.motionState == BikeMotionState.Wipeout) yield return null;
                Assert.That(player.LastRecoveryDuration, Is.InRange(1.7f, 1.75f));
                Assert.That(player.State.controlsEnabled, Is.True);
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
