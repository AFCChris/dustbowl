using Dustbowl.Camera;
using Dustbowl.Input;
using Dustbowl.Telemetry;
using UnityEngine;

namespace Dustbowl.Core
{
    [DisallowMultipleComponent]
    public sealed class BehaviourLabBootstrap : MonoBehaviour
    {
        [SerializeField] private DustbowlInputReader inputReader;
        [SerializeField] private DevelopmentTelemetryRecorder telemetry;
        [SerializeField] private CameraModeController cameraModes;

        private readonly SimulationClock clock = new();

        public SimulationClock Clock => clock;
        public DustbowlInputReader InputReader => inputReader;
        public DevelopmentTelemetryRecorder Telemetry => telemetry;
        public CameraModeController CameraModes => cameraModes;

        private void FixedUpdate()
        {
            clock.Advance();
        }

        public void Configure(
            DustbowlInputReader reader,
            DevelopmentTelemetryRecorder recorder,
            CameraModeController modes)
        {
            inputReader = reader;
            telemetry = recorder;
            cameraModes = modes;
        }
    }
}
