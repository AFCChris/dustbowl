using Dustbowl.Bike;
using Dustbowl.Camera;
using Dustbowl.Telemetry;
using UnityEngine;

namespace Dustbowl.Core
{
    [DisallowMultipleComponent]
    public sealed class BehaviourLabDevelopmentHud : MonoBehaviour
    {
        [SerializeField] private ArcadeBikeController bike;
        [SerializeField] private BehaviourLabScenarioController scenarios;
        [SerializeField] private CameraModeController cameraModes;
        [SerializeField] private DevelopmentTelemetryRecorder telemetry;

        public void Configure(
            ArcadeBikeController controller,
            BehaviourLabScenarioController scenarioController,
            CameraModeController modes,
            DevelopmentTelemetryRecorder recorder)
        {
            bike = controller;
            scenarios = scenarioController;
            cameraModes = modes;
            telemetry = recorder;
        }

        private void OnGUI()
        {
            if ((!Application.isEditor && !Debug.isDebugBuild)
                || bike == null
                || scenarios == null
                || cameraModes == null)
            {
                return;
            }

            BikeControllerState state = bike.State;
            Rect panel = new(16f, 16f, 470f, 178f);
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(28f, 24f, 448f, 160f));
            GUILayout.Label($"DUSTBOWL STAGE 3 — {scenarios.CurrentScenarioName}");
            GUILayout.Label(
                $"{state.motionState}  |  {state.velocity.magnitude:F1} m/s  |  "
                + $"climb {state.recentClimbRate:F1} m/s  |  air {state.airborneSeconds:F2}s");
            GUILayout.Label(
                $"Camera: {cameraModes.CurrentMode}  |  surface {state.surfaceAmount:F2}  |  "
                + $"last landing {state.lastLandingOutcome}");
            GUILayout.Label("Gamepad: RT throttle, LT brake, left stick steer, right stick air, shoulders camera");
            GUILayout.Label("Keyboard: W/S throttle-brake, A/D steer, F/R pitch, Q/E whip, C/V camera");
            GUILayout.Label("Tab/Select scenario  |  Backspace/North reset  |  K/R3 wipeout  |  F9/Start telemetry");
            if (telemetry != null)
            {
                GUILayout.Label(telemetry.IsRecording
                    ? $"Telemetry recording: {telemetry.CaptureName}"
                    : $"Telemetry saved: {telemetry.LastSavedPath}");
            }

            GUILayout.EndArea();
        }
    }
}
