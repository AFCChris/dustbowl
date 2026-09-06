using System;
using System.Collections.Generic;
using System.Linq;
using Dustbowl.Bike;
using Dustbowl.Camera;
using Dustbowl.Course;
using UnityEngine;

namespace Dustbowl.Race
{
    public enum NationalRaceState
    {
        Countdown,
        Racing,
        Results,
        /// <summary>Riders parked on the grid behind the front-end; no clocks run.</summary>
        Idle
    }

    [DisallowMultipleComponent]
    public sealed class NationalRaceManager : MonoBehaviour
    {
        private const float CountdownDuration = 3.4f;

        [SerializeField] private CourseSurface course;
        [SerializeField] private ArcadeBikeController player;
        [SerializeField] private CameraModeController cameraModes;
        [SerializeField] private NationalAIRider[] opponents = Array.Empty<NationalAIRider>();
        [SerializeField] private NationalRaceState state;
        [SerializeField] private float countdownRemaining;
        [SerializeField] private float raceTime;
        [SerializeField] private float currentLapTime;
        [SerializeField] private float bestLapTime;
        [SerializeField] private int playerPosition = 1;

        private readonly RaceLapTracker playerLaps = new();
        private readonly List<float> playerLapTimes = new();
        private float playerAlong;

        public NationalRaceState State => state;
        public float CountdownRemaining => countdownRemaining;
        public float RaceTime => raceTime;
        public float CurrentLapTime => currentLapTime;
        public float BestLapTime => bestLapTime;
        public float PlayerAlong => playerAlong;
        public int PlayerLap => playerLaps.Lap;
        public int PlayerPosition => playerPosition;
        public IReadOnlyList<float> PlayerLapTimes => playerLapTimes;
        public IReadOnlyList<NationalAIRider> Opponents => opponents;
        public CameraMode CameraMode => cameraModes != null ? cameraModes.CurrentMode : CameraMode.Chase;
        public int TotalLaps => DustbowlFlatsNationalCourse.Laps;
        public int TotalRiders => opponents.Length + 1;
        public float LapLength => course != null && course.Definition != null ? course.Definition.EndAlong : 0f;
        public CourseSurface Course => course;
        public ArcadeBikeController Player => player;

        public string CountdownText
        {
            get
            {
                if (state != NationalRaceState.Countdown)
                {
                    return string.Empty;
                }

                if (countdownRemaining > 2.5f) return "3";
                if (countdownRemaining > 1.6f) return "2";
                if (countdownRemaining > .7f) return "1";
                return "GO!";
            }
        }

        public void Configure(
            CourseSurface raceCourse,
            ArcadeBikeController playerController,
            CameraModeController modes,
            NationalAIRider[] aiRiders)
        {
            course = raceCourse;
            player = playerController;
            cameraModes = modes;
            opponents = aiRiders ?? Array.Empty<NationalAIRider>();
        }

        // The scene opens on the grid behind the setup screen; GameFlowController
        // starts, restarts and abandons events. Without a flow controller (older
        // scenes, tests) the manager still races on its own from Start.
        private void Start()
        {
            if (state != NationalRaceState.Idle)
            {
                RestartRace();
            }
        }

        private void Update()
        {
            if (course == null || player == null || state == NationalRaceState.Idle)
            {
                return;
            }

            if (state == NationalRaceState.Countdown)
            {
                countdownRemaining -= Time.deltaTime;
                if (countdownRemaining <= 0f)
                {
                    countdownRemaining = 0f;
                    state = NationalRaceState.Racing;
                    player.SetRaceControlEnabled(true);
                }
            }

            CourseSample playerSample = course.SampleCourse(player.State.position);
            playerAlong = playerSample.isValid ? playerSample.along : playerAlong;
            if (state == NationalRaceState.Racing)
            {
                raceTime += Time.deltaTime;
                currentLapTime += Time.deltaTime;
                if (player.State.motionState != BikeMotionState.Wipeout
                    && playerLaps.Update(playerAlong, LapLength, TotalLaps, out bool finished))
                {
                    CommitPlayerLap(finished);
                }
            }

            float playerDistance = playerLaps.EffectiveDistance(playerAlong, LapLength, TotalLaps, raceTime);
            foreach (NationalAIRider opponent in opponents)
            {
                opponent.Tick(Time.deltaTime, state == NationalRaceState.Racing, playerDistance, raceTime);
            }

            playerPosition = 1 + opponents.Count(opponent =>
                opponent.EffectiveDistance(LapLength, TotalLaps) > playerDistance);
        }

        /// <summary>Resets the whole field to the grid and runs the normal countdown.</summary>
        public void RestartRace() => ResetField(NationalRaceState.Countdown);

        /// <summary>Resets the whole field to the grid and holds it there for the front-end.</summary>
        public void HoldOnGrid() => ResetField(NationalRaceState.Idle);

        private void ResetField(NationalRaceState nextState)
        {
            if (course == null || player == null || course.Definition.SpawnHints.Count == 0)
            {
                return;
            }

            CourseSpawnHint playerSpawn = course.Definition.SpawnHints[0];
            player.SetSurfaceAndSpawn(course, playerSpawn.along, playerSpawn.lateralOffset);
            player.SetRaceControlEnabled(false);
            foreach (NationalAIRider opponent in opponents)
            {
                opponent.ResetRider();
            }

            playerAlong = playerSpawn.along;
            playerLaps.Reset(playerAlong);
            playerLapTimes.Clear();
            bestLapTime = 0f;
            raceTime = 0f;
            currentLapTime = 0f;
            playerPosition = 1;
            countdownRemaining = nextState == NationalRaceState.Countdown ? CountdownDuration : 0f;
            state = nextState;
        }

        public IReadOnlyList<string> BuildResults()
        {
            var entries = new List<ResultEntry>
            {
                new("YOU", playerLaps.EffectiveDistance(playerAlong, LapLength, TotalLaps, raceTime), raceTime)
            };
            entries.AddRange(opponents.Select(opponent => new ResultEntry(
                opponent.RiderName,
                opponent.EffectiveDistance(LapLength, TotalLaps),
                opponent.FinishTime)));
            return entries.OrderByDescending(entry => entry.distance)
                .Select((entry, index) => $"{index + 1}.  {entry.name,-10}  {FormatTime(entry.finishTime)}")
                .ToArray();
        }

        public static string FormatTime(float seconds)
        {
            if (seconds <= 0f) return "RACING";
            int minutes = Mathf.FloorToInt(seconds / 60f);
            return $"{minutes}:{seconds - minutes * 60f:00.000}";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DevelopmentCompletePlayerLap(float lapTime)
        {
            if (state != NationalRaceState.Racing) return;
            currentLapTime = lapTime;
            playerLaps.Update(LapLength * .55f, LapLength, TotalLaps, out _);
            playerLaps.Update(LapLength * .95f, LapLength, TotalLaps, out _);
            playerAlong = LapLength * .05f;
            if (playerLaps.Update(playerAlong, LapLength, TotalLaps, out bool finished))
            {
                CommitPlayerLap(finished);
            }
        }
#endif

        private void CommitPlayerLap(bool finished)
        {
            playerLapTimes.Add(currentLapTime);
            bestLapTime = bestLapTime <= 0f ? currentLapTime : Mathf.Min(bestLapTime, currentLapTime);
            currentLapTime = 0f;
            if (finished)
            {
                state = NationalRaceState.Results;
                player.SetRaceControlEnabled(false);
            }
        }

        private readonly struct ResultEntry
        {
            public ResultEntry(string riderName, float raceDistance, float time)
            {
                name = riderName;
                distance = raceDistance;
                finishTime = time;
            }

            public readonly string name;
            public readonly float distance;
            public readonly float finishTime;
        }
    }
}
