using System.Linq;
using Dustbowl.Bike;
using Dustbowl.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Dustbowl.Tests
{
    public sealed class ArcadeBikeControllerTests
    {
        private const string TuningPath = "Assets/Dustbowl/Settings/WebReferenceArcadeBikeTuning.asset";
        private const string InputPath = "Assets/Dustbowl/Settings/DustbowlInputActions.inputactions";
        private const string ScenePath = "Assets/Dustbowl/Scenes/Dustbowl_BehaviourLab.unity";

        [Test]
        public void EnginePowerTapersToZeroWithoutHardSpeedClamp()
        {
            ArcadeBikeTuning tuning = LoadTuning();
            Assert.That(ArcadeBikeRules.EnginePower(0f, tuning), Is.EqualTo(32f).Within(0.0001f));
            Assert.That(ArcadeBikeRules.EnginePower(21f, tuning), Is.EqualTo(16f).Within(0.0001f));
            Assert.That(ArcadeBikeRules.EnginePower(42f, tuning), Is.Zero.Within(0.0001f));
            Assert.That(ArcadeBikeRules.EnginePower(50f, tuning), Is.Zero.Within(0.0001f));
        }

        [Test]
        public void SteeringAuthorityMatchesWebSpeedShape()
        {
            ArcadeBikeTuning tuning = LoadTuning();
            Assert.That(ArcadeBikeRules.SteeringAuthority(0f, tuning), Is.Zero);
            Assert.That(ArcadeBikeRules.SteeringAuthority(6f, tuning),
                Is.EqualTo(1f - 6f / 63f).Within(0.0001f));
            Assert.That(ArcadeBikeRules.SteeringAuthority(42f, tuning), Is.EqualTo(0.45f).Within(0.0001f));
        }

        [Test]
        public void LooseSandAddsWebReferenceDrag()
        {
            ArcadeBikeTuning tuning = LoadTuning();
            Assert.That(ArcadeBikeRules.DragCoefficient(true, 1f, tuning), Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(ArcadeBikeRules.DragCoefficient(true, 0f, tuning), Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(ArcadeBikeRules.DragCoefficient(false, 0f, tuning), Is.EqualTo(1.47f).Within(0.0001f));
        }

        [Test]
        public void ClimbRateUsesClampedExponentialHistory()
        {
            ArcadeBikeTuning tuning = LoadTuning();
            float deltaTime = 1f / 60f;
            float expected = 40f * (1f - Mathf.Exp(-11f * deltaTime));
            float actual = ArcadeBikeRules.UpdateClimbRate(0f, 0f, 10f, deltaTime, tuning);
            Assert.That(actual, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void TakeoffRulesDistinguishLipAndRoundedCrest()
        {
            ArcadeBikeTuning tuning = LoadTuning();
            Assert.That(ArcadeBikeRules.EvaluateTakeoff(1f, 0f, 0f, 0f, 1f / 60f, tuning),
                Is.EqualTo(TakeoffTrigger.Lip));
            Assert.That(ArcadeBikeRules.EvaluateTakeoff(0.42f, 0f, 4f, 2f, 1f / 60f, tuning),
                Is.EqualTo(TakeoffTrigger.RoundedCrest));
        }

        [Test]
        public void LandingEvaluatorPreservesCurrentBinaryBoundaries()
        {
            ArcadeBikeTuning tuning = LoadTuning();
            Assert.That(ArcadeBikeRules.EvaluateLanding(0.31f, 0.349f, 0f, tuning).outcome,
                Is.EqualTo(Stage3LandingOutcome.Wipeout));
            Assert.That(ArcadeBikeRules.EvaluateLanding(0.31f, 0.619f, 20.01f, tuning).outcome,
                Is.EqualTo(Stage3LandingOutcome.Wipeout));
            Assert.That(ArcadeBikeRules.EvaluateLanding(0.31f, 0.62f, 21f, tuning).outcome,
                Is.EqualTo(Stage3LandingOutcome.Accepted));
            Assert.That(ArcadeBikeRules.EvaluateLanding(0.3f, -1f, 99f, tuning).outcome,
                Is.EqualTo(Stage3LandingOutcome.ShortContact));
        }

        [Test]
        public void BehaviourLabCompletesBothProtectedTakeoffPaths()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ArcadeBikeController bike = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ArcadeBikeController>(true))
                .Single();
            BehaviourLabScenarioController scenarios = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BehaviourLabScenarioController>(true))
                .Single();

            float tabletopAir = RunUntilMeaningfulLanding(
                scenarios,
                bike,
                BehaviourLabScenario.AuthoredTabletop);
            Assert.That(tabletopAir, Is.GreaterThan(0.3f));
            float crestAir = RunUntilMeaningfulLanding(
                scenarios,
                bike,
                BehaviourLabScenario.NaturalRoundedCrest);
            Assert.That(crestAir, Is.GreaterThan(0.3f));
            Assert.That(bike.LastTakeoffTrigger,
                Is.EqualTo(TakeoffTrigger.RoundedCrest).Or.EqualTo(TakeoffTrigger.LipAndRoundedCrest));
        }

        [Test]
        public void WipeoutRestoresUsefulControlAtWebReferenceTiming()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ArcadeBikeController bike = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ArcadeBikeController>(true))
                .Single();
            BehaviourLabScenarioController scenarios = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BehaviourLabScenarioController>(true))
                .Single();
            scenarios.Select(BehaviourLabScenario.NaturalRoundedCrest);
            bike.ForceWipeout();
            for (int index = 0; index < 150 && bike.State.motionState == BikeMotionState.Wipeout; index++)
            {
                bike.SimulateTick(default, 1f / 60f);
            }

            Assert.That(bike.State.motionState, Is.EqualTo(BikeMotionState.Grounded));
            Assert.That(bike.State.controlsEnabled, Is.True);
            Assert.That(bike.LastRecoveryDuration, Is.InRange(1.7f, 1.72f));
        }

        [Test]
        public void GamepadAndKeyboardBindingsCoverStageThreeActions()
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            InputActionMap player = asset.FindActionMap("Player", true);
            AssertBinding(player, "Steer", "<Gamepad>/leftStick/x");
            AssertBinding(player, "Throttle", "<Gamepad>/rightTrigger");
            AssertBinding(player, "Brake", "<Gamepad>/leftTrigger");
            AssertBinding(player, "AirPitch", "<Gamepad>/rightStick/y");
            AssertBinding(player, "AirWhip", "<Gamepad>/rightStick/x");
            AssertBinding(player, "Reset", "<Keyboard>/backspace");
            AssertBinding(player, "CameraNext", "<Gamepad>/rightShoulder");
            AssertBinding(player, "CameraPrevious", "<Gamepad>/leftShoulder");
        }

        private static float RunUntilMeaningfulLanding(
            BehaviourLabScenarioController scenarios,
            ArcadeBikeController bike,
            BehaviourLabScenario scenario)
        {
            scenarios.Select(scenario);
            int initialLandings = bike.LandingCount;
            float maximumAir = 0f;
            for (int index = 0; index < 900; index++)
            {
                bike.SimulateTick(new PlayerInputSnapshot { throttle = 1f }, 1f / 60f);
                maximumAir = Mathf.Max(maximumAir, bike.State.airborneSeconds);
                if (maximumAir > 0.3f
                    && bike.LandingCount > initialLandings
                    && bike.State.motionState != BikeMotionState.Airborne)
                {
                    break;
                }
            }

            return maximumAir;
        }

        private static ArcadeBikeTuning LoadTuning()
        {
            ArcadeBikeTuning tuning = AssetDatabase.LoadAssetAtPath<ArcadeBikeTuning>(TuningPath);
            Assert.That(tuning, Is.Not.Null);
            return tuning;
        }

        private static void AssertBinding(InputActionMap map, string actionName, string path)
        {
            InputAction action = map.FindAction(actionName, true);
            Assert.That(action.bindings.Any(binding => binding.effectivePath == path), Is.True,
                $"{actionName} is missing {path}.");
        }
    }
}
