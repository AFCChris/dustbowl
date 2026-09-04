using Dustbowl.Course;
using Dustbowl.Input;
using Dustbowl.Telemetry;
using UnityEngine;

namespace Dustbowl.Bike
{
    public enum BehaviourLabScenario
    {
        AuthoredTabletop,
        NaturalRoundedCrest
    }

    [DisallowMultipleComponent]
    public sealed class BehaviourLabScenarioController : MonoBehaviour
    {
        [SerializeField] private ArcadeBikeController bike;
        [SerializeField] private DustbowlInputReader inputReader;
        [SerializeField] private DevelopmentTelemetryRecorder telemetry;
        [SerializeField] private CourseSurface authoredTabletop;
        [SerializeField] private CourseSurface naturalRoundedCrest;
        [SerializeField] private BehaviourLabScenario currentScenario;

        public BehaviourLabScenario CurrentScenario => currentScenario;
        public string CurrentScenarioName => currentScenario == BehaviourLabScenario.AuthoredTabletop
            ? "D Authored tabletop"
            : "C Natural rounded crest";

        public void Configure(
            ArcadeBikeController controller,
            DustbowlInputReader reader,
            DevelopmentTelemetryRecorder recorder,
            CourseSurface tabletop,
            CourseSurface crest)
        {
            bike = controller;
            inputReader = reader;
            telemetry = recorder;
            authoredTabletop = tabletop;
            naturalRoundedCrest = crest;
        }

        public void Select(BehaviourLabScenario scenario)
        {
            currentScenario = scenario;
            if (bike == null)
            {
                return;
            }

            if (scenario == BehaviourLabScenario.AuthoredTabletop)
            {
                bike.SetSurfaceAndSpawn(authoredTabletop, 334.221629f);
            }
            else
            {
                bike.SetSurfaceAndSpawn(naturalRoundedCrest, RoundedCrestReference.SpawnAlong);
            }

            telemetry?.BeginCapture($"Stage3-{CurrentScenarioName}");
        }

        private void Start()
        {
            Select(currentScenario);
        }

        private void Update()
        {
            if (inputReader != null && inputReader.Current.scenarioNextPressed)
            {
                Select(currentScenario == BehaviourLabScenario.AuthoredTabletop
                    ? BehaviourLabScenario.NaturalRoundedCrest
                    : BehaviourLabScenario.AuthoredTabletop);
            }
        }
    }
}
