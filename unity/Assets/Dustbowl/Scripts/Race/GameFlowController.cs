using Dustbowl.Bike;
using Dustbowl.Camera;
using Dustbowl.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dustbowl.Race
{
    public enum GameFlowScreen
    {
        /// <summary>Opening event-setup plate; the field idles on the grid behind it.</summary>
        Setup,
        /// <summary>Countdown, racing and results, all owned by <see cref="NationalRaceManager"/>.</summary>
        Race,
        /// <summary>Escape overlay over a frozen race.</summary>
        Paused
    }

    /// <summary>
    /// Lightweight front-end and race-loop state machine mirroring the web shell:
    /// setup screen → countdown → race → results → race again / back to setup, plus
    /// an Escape pause overlay. It owns throttle-mode and camera selection, input
    /// suspension while menus are up, and the only quit path. Keyboard and gamepad
    /// shortcuts are polled here so the standalone build never depends on mouse UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        private const string ThrottlePrefKey = "dustbowl.throttle";

        [SerializeField] private NationalRaceManager race;
        [SerializeField] private ArcadeBikeController player;
        [SerializeField] private CameraModeController cameraModes;
        [SerializeField] private DustbowlInputReader inputReader;
        [SerializeField] private ArcadeBikeCamera followCamera;
        [SerializeField] private int selectedEventIndex;
        [SerializeField] private ThrottleMode throttleMode = ThrottleMode.Auto;
        [SerializeField] private GameFlowScreen screen = GameFlowScreen.Setup;

        private bool ownsTimeScale;

        public GameFlowScreen Screen => screen;
        public int SelectedEventIndex => selectedEventIndex;
        public RaceEventDefinition SelectedEvent => RaceEventCatalog.Get(selectedEventIndex);
        public ThrottleMode ThrottleMode => throttleMode;
        public CameraMode CameraMode => cameraModes != null ? cameraModes.CurrentMode : CameraMode.Chase;
        public NationalRaceManager Race => race;
        public bool IsPaused => screen == GameFlowScreen.Paused;
        public bool ShowsResults => screen == GameFlowScreen.Race && race != null && race.State == NationalRaceState.Results;

        /// <summary>Set once <see cref="Quit"/> has asked the application to close (observable in tests).</summary>
        public bool QuitRequested { get; private set; }

        public void Configure(
            NationalRaceManager raceManager,
            ArcadeBikeController playerController,
            CameraModeController modes,
            DustbowlInputReader reader,
            ArcadeBikeCamera camera)
        {
            race = raceManager;
            player = playerController;
            cameraModes = modes;
            inputReader = reader;
            followCamera = camera;
        }

        private void Awake()
        {
            if (PlayerPrefs.HasKey(ThrottlePrefKey))
            {
                throttleMode = PlayerPrefs.GetInt(ThrottlePrefKey, 0) == 0 ? ThrottleMode.Auto : ThrottleMode.Manual;
            }
        }

        private void Start()
        {
            ApplyThrottleMode();
            ReturnToMenu();
        }

        private void OnDisable()
        {
            RestoreTimeScale();
        }

        private void Update()
        {
            if (race == null)
            {
                return;
            }

            switch (screen)
            {
                case GameFlowScreen.Setup:
                    UpdateSetupInput();
                    break;
                case GameFlowScreen.Race:
                    UpdateRaceInput();
                    break;
                case GameFlowScreen.Paused:
                    UpdatePausedInput();
                    break;
            }
        }

        // ------------------------------------------------------------ actions

        /// <summary>DROP IN: leaves the setup screen and runs the countdown for the selected event.</summary>
        public void StartRace()
        {
            if (race == null)
            {
                return;
            }

            RestoreTimeScale();
            ApplyThrottleMode();
            screen = GameFlowScreen.Race;
            inputReader?.SetSuspended(false);
            race.RestartRace();
        }

        /// <summary>RACE AGAIN / RESTART: same event, whole field back to the grid, normal countdown.</summary>
        public void RestartRace()
        {
            if (race == null)
            {
                return;
            }

            RestoreTimeScale();
            ApplyThrottleMode();
            screen = GameFlowScreen.Race;
            inputReader?.SetSuspended(false);
            race.RestartRace();
            followCamera?.Snap();
        }

        /// <summary>Back to the setup screen with the field parked on the grid.</summary>
        public void ReturnToMenu()
        {
            if (race == null)
            {
                return;
            }

            RestoreTimeScale();
            screen = GameFlowScreen.Setup;
            inputReader?.SetSuspended(true);
            race.HoldOnGrid();
            followCamera?.Snap();
        }

        public void Pause()
        {
            if (screen != GameFlowScreen.Race || race == null || race.State == NationalRaceState.Results)
            {
                return;
            }

            screen = GameFlowScreen.Paused;
            inputReader?.SetSuspended(true);
            Time.timeScale = 0f;
            ownsTimeScale = true;
        }

        public void Resume()
        {
            if (screen != GameFlowScreen.Paused)
            {
                return;
            }

            RestoreTimeScale();
            screen = GameFlowScreen.Race;
            inputReader?.SetSuspended(false);
        }

        public void TogglePause()
        {
            if (screen == GameFlowScreen.Paused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        public void Quit()
        {
            QuitRequested = true;
            RestoreTimeScale();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void SetThrottleMode(ThrottleMode mode)
        {
            throttleMode = mode;
            PlayerPrefs.SetInt(ThrottlePrefKey, mode == ThrottleMode.Auto ? 0 : 1);
            ApplyThrottleMode();
        }

        public void ToggleThrottleMode()
        {
            SetThrottleMode(throttleMode == ThrottleMode.Auto ? ThrottleMode.Manual : ThrottleMode.Auto);
        }

        public void CycleCamera(int direction)
        {
            if (cameraModes == null)
            {
                return;
            }

            if (direction >= 0)
            {
                cameraModes.Next();
            }
            else
            {
                cameraModes.Previous();
            }
        }

        public void SetCameraMode(CameraMode mode)
        {
            cameraModes?.Set(mode);
        }

        public void SelectEvent(int index)
        {
            if (RaceEventCatalog.Count == 0)
            {
                return;
            }

            selectedEventIndex = ((index % RaceEventCatalog.Count) + RaceEventCatalog.Count) % RaceEventCatalog.Count;
        }

        // -------------------------------------------------------------- input

        private void UpdateSetupInput()
        {
            if (Pressed(Key.Enter) || Pressed(Key.NumpadEnter) || Pressed(Key.Space)
                || PadPressed(GamepadButton.South) || PadPressed(GamepadButton.Start))
            {
                StartRace();
            }
            else if (Pressed(Key.T) || PadPressed(GamepadButton.North))
            {
                ToggleThrottleMode();
            }
            else if (Pressed(Key.C) || PadPressed(GamepadButton.West) || PadPressed(GamepadButton.RightShoulder))
            {
                CycleCamera(1);
            }
            else if (Pressed(Key.V) || PadPressed(GamepadButton.LeftShoulder))
            {
                CycleCamera(-1);
            }
            else if (Pressed(Key.RightArrow) || Pressed(Key.D) || PadPressed(GamepadButton.DpadRight))
            {
                SelectEvent(selectedEventIndex + 1);
            }
            else if (Pressed(Key.LeftArrow) || Pressed(Key.A) || PadPressed(GamepadButton.DpadLeft))
            {
                SelectEvent(selectedEventIndex - 1);
            }
            else if (Pressed(Key.Q))
            {
                Quit();
            }
        }

        private void UpdateRaceInput()
        {
            if (race.State == NationalRaceState.Results)
            {
                if (Pressed(Key.Enter) || Pressed(Key.NumpadEnter) || Pressed(Key.Space)
                    || PadPressed(GamepadButton.South) || PadPressed(GamepadButton.Start))
                {
                    RestartRace();
                }
                else if (Pressed(Key.Escape) || Pressed(Key.M) || PadPressed(GamepadButton.East))
                {
                    ReturnToMenu();
                }
                else if (Pressed(Key.Q))
                {
                    Quit();
                }

                return;
            }

            if (Pressed(Key.Escape) || Pressed(Key.P) || PadPressed(GamepadButton.Start))
            {
                Pause();
            }
        }

        private void UpdatePausedInput()
        {
            if (Pressed(Key.Escape) || Pressed(Key.P) || Pressed(Key.Enter) || Pressed(Key.NumpadEnter)
                || PadPressed(GamepadButton.Start) || PadPressed(GamepadButton.South))
            {
                Resume();
            }
            else if (Pressed(Key.R) || PadPressed(GamepadButton.West))
            {
                RestartRace();
            }
            else if (Pressed(Key.M) || PadPressed(GamepadButton.East))
            {
                ReturnToMenu();
            }
            else if (Pressed(Key.Q))
            {
                Quit();
            }
        }

        private static bool Pressed(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }

        private static bool PadPressed(GamepadButton button)
        {
            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad[button].wasPressedThisFrame;
        }

        // ------------------------------------------------------------ helpers

        private void ApplyThrottleMode()
        {
            player?.SetThrottleMode(throttleMode);
        }

        private void RestoreTimeScale()
        {
            if (ownsTimeScale)
            {
                Time.timeScale = 1f;
                ownsTimeScale = false;
            }
        }
    }
}
