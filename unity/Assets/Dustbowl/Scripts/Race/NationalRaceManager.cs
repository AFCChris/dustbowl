using System;
using System.Collections.Generic;
using System.Linq;
using Dustbowl.Bike;
using Dustbowl.Camera;
using Dustbowl.Course;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dustbowl.Race
{
    public enum NationalRaceState
    {
        PreRace,
        Countdown,
        Racing,
        Results
    }

    [DisallowMultipleComponent]
    public sealed class NationalRaceManager : MonoBehaviour
    {
        private const float CountdownDuration = 3.4f;
        private const string AutoThrottlePreference = "dustbowl.autoThrottle";
        public const float PlayableRadius = 340f;
        public const float BoundaryGuardRadius = PlayableRadius + 90f;
        public const float BoundaryReturnRadius = PlayableRadius + 88f;
        public const float BoundaryVelocityRetention = .3f;

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
        [SerializeField] private bool autoThrottleEnabled = true;
        [SerializeField] private bool quitRequested;
        [SerializeField] private int boundaryGuardCount;

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
        public bool AutoThrottleEnabled => autoThrottleEnabled;
        public bool QuitRequested => quitRequested;
        public int BoundaryGuardCount => boundaryGuardCount;

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

        private void Start()
        {
            autoThrottleEnabled = PlayerPrefs.GetInt(AutoThrottlePreference, 1) != 0;
            ApplyThrottlePreference();
            PrepareRace();
        }

        private void Update()
        {
            if (course == null || player == null)
            {
                return;
            }

            if (QuitPressed())
            {
                RequestQuit();
                return;
            }

            if (ThrottleModePressed())
            {
                ToggleAutoThrottle();
            }

            if (state == NationalRaceState.PreRace)
            {
                if (StartPressed())
                {
                    BeginRace();
                }
                return;
            }

            if (state == NationalRaceState.Results && RestartPressed())
            {
                RestartRace();
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

            if (player.ApplyPlayableAreaGuard(
                    BoundaryGuardRadius,
                    BoundaryReturnRadius,
                    BoundaryVelocityRetention))
            {
                boundaryGuardCount++;
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

        public void RestartRace()
        {
            ResetRace(NationalRaceState.Countdown, CountdownDuration);
        }

        public void PrepareRace()
        {
            ResetRace(NationalRaceState.PreRace, 0f);
        }

        public void BeginRace()
        {
            if (state == NationalRaceState.PreRace)
            {
                RestartRace();
            }
        }

        public void ToggleAutoThrottle()
        {
            SetAutoThrottle(!autoThrottleEnabled);
        }

        public void SetAutoThrottle(bool enabled)
        {
            autoThrottleEnabled = enabled;
            PlayerPrefs.SetInt(AutoThrottlePreference, enabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplyThrottlePreference();
        }

        public void CycleCamera()
        {
            cameraModes?.Next();
        }

        public void RequestQuit()
        {
            quitRequested = true;
#if !UNITY_EDITOR
            Application.Quit();
#endif
        }

        private void ResetRace(NationalRaceState nextState, float countdown)
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
            boundaryGuardCount = 0;
            countdownRemaining = countdown;
            state = nextState;
        }

        private void ApplyThrottlePreference()
        {
            player?.SetAutoThrottle(autoThrottleEnabled);
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

        private static bool RestartPressed()
        {
            return (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        }

        private static bool StartPressed()
        {
            return (Keyboard.current != null
                    && (Keyboard.current.enterKey.wasPressedThisFrame
                        || Keyboard.current.spaceKey.wasPressedThisFrame))
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        }

        private static bool ThrottleModePressed()
        {
            return (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
        }

        private static bool QuitPressed()
        {
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
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
