using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dustbowl.Bike;
using Dustbowl.Camera;
using Dustbowl.Input;
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
            GameFlowController flow = Object.FindFirstObjectByType<GameFlowController>();
            ArcadeBikeController player = Object.FindFirstObjectByType<ArcadeBikeController>();
            CameraModeController cameras = Object.FindFirstObjectByType<CameraModeController>();
            NationalRaceHud hud = Object.FindFirstObjectByType<NationalRaceHud>();
            Assert.That(race, Is.Not.Null);
            Assert.That(flow, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(cameras, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            Assert.That(race.Opponents.Count, Is.EqualTo(7));
            Assert.That(flow.Screen, Is.EqualTo(GameFlowScreen.Setup));
            Assert.That(race.State, Is.EqualTo(NationalRaceState.Idle));
            flow.StartRace();
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
            GameFlowController flow = Object.FindFirstObjectByType<GameFlowController>();
            flow.StartRace();
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
                Assert.That(flow.ShowsResults, Is.True);
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }

        [UnityTest]
        public IEnumerator FrontEndFlowStartsOnSetupPausesRacesAgainAndReturnsToMenu()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Dustbowl_National_Flats", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;

            NationalRaceManager race = Object.FindFirstObjectByType<NationalRaceManager>();
            GameFlowController flow = Object.FindFirstObjectByType<GameFlowController>();
            ArcadeBikeController player = Object.FindFirstObjectByType<ArcadeBikeController>();
            DustbowlInputReader input = Object.FindFirstObjectByType<DustbowlInputReader>();
            Assert.That(flow, Is.Not.Null);
            Assert.That(input, Is.Not.Null);

            // Launch: setup screen, field parked on the grid, bike input suspended.
            Assert.That(flow.Screen, Is.EqualTo(GameFlowScreen.Setup));
            Assert.That(race.State, Is.EqualTo(NationalRaceState.Idle));
            Assert.That(race.RaceTime, Is.Zero);
            Assert.That(player.State.controlsEnabled, Is.False);
            Assert.That(input.Suspended, Is.True);
            float[] gridAlongs = race.Opponents.Select(value => value.Along).ToArray();
            Vector3 playerGrid = player.State.position;

            // Throttle selection reaches the controller in both directions.
            flow.SetThrottleMode(ThrottleMode.Manual);
            Assert.That(player.ThrottleMode, Is.EqualTo(ThrottleMode.Manual));
            flow.SetThrottleMode(ThrottleMode.Auto);
            Assert.That(player.ThrottleMode, Is.EqualTo(ThrottleMode.Auto));

            try
            {
                flow.StartRace();
                Assert.That(flow.Screen, Is.EqualTo(GameFlowScreen.Race));
                Assert.That(race.State, Is.EqualTo(NationalRaceState.Countdown));
                Assert.That(input.Suspended, Is.False);
                Time.timeScale = 20f;
                while (race.State == NationalRaceState.Countdown) yield return null;
                Assert.That(player.State.controlsEnabled, Is.True);
                for (int frame = 0; frame < 10; frame++) yield return null;

                // Escape overlay freezes the race and its clocks.
                flow.Pause();
                Assert.That(flow.IsPaused, Is.True);
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(input.Suspended, Is.True);
                float pausedRaceTime = race.RaceTime;
                float[] pausedAlongs = race.Opponents.Select(value => value.Along).ToArray();
                for (int frame = 0; frame < 5; frame++) yield return null;
                Assert.That(race.RaceTime, Is.EqualTo(pausedRaceTime));
                Assert.That(race.Opponents.Select(value => value.Along).ToArray(), Is.EqualTo(pausedAlongs));
                flow.Resume();
                Assert.That(flow.IsPaused, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(input.Suspended, Is.False);
                Time.timeScale = 20f;
                for (int frame = 0; frame < 5; frame++) yield return null;
                Assert.That(race.RaceTime, Is.GreaterThan(pausedRaceTime));

                // Finish, then Race Again resets the whole field through a fresh countdown.
                race.DevelopmentCompletePlayerLap(45f);
                race.DevelopmentCompletePlayerLap(44f);
                race.DevelopmentCompletePlayerLap(43f);
                Assert.That(race.State, Is.EqualTo(NationalRaceState.Results));
                Assert.That(flow.ShowsResults, Is.True);
                flow.RestartRace();
                Assert.That(flow.Screen, Is.EqualTo(GameFlowScreen.Race));
                Assert.That(race.State, Is.EqualTo(NationalRaceState.Countdown));
                Assert.That(race.RaceTime, Is.Zero);
                Assert.That(race.CurrentLapTime, Is.Zero);
                Assert.That(race.BestLapTime, Is.Zero);
                Assert.That(race.PlayerLapTimes, Is.Empty);
                Assert.That(race.PlayerLap, Is.EqualTo(1));
                Assert.That(race.PlayerPosition, Is.EqualTo(1));
                Assert.That(player.State.controlsEnabled, Is.False);
                Assert.That(Vector3.Distance(player.State.position, playerGrid), Is.LessThan(.01f));
                Assert.That(race.Opponents.Select(value => value.Along).ToArray(), Is.EqualTo(gridAlongs));
                Assert.That(race.Opponents.All(value => value.Lap == 1 && !value.Finished && value.FinishTime == 0f), Is.True);
                while (race.State == NationalRaceState.Countdown) yield return null;
                Assert.That(race.State, Is.EqualTo(NationalRaceState.Racing));
                Assert.That(player.State.controlsEnabled, Is.True);

                // Return to the setup screen parks everyone again.
                flow.ReturnToMenu();
                Assert.That(flow.Screen, Is.EqualTo(GameFlowScreen.Setup));
                Assert.That(race.State, Is.EqualTo(NationalRaceState.Idle));
                Assert.That(input.Suspended, Is.True);
                Assert.That(player.State.controlsEnabled, Is.False);
                Assert.That(race.Opponents.Select(value => value.Along).ToArray(), Is.EqualTo(gridAlongs));
                yield return null;
                Assert.That(race.State, Is.EqualTo(NationalRaceState.Idle), "Idle field must not start a countdown by itself.");
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }
    }
}
