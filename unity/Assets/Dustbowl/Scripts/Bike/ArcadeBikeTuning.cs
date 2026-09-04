using UnityEngine;

namespace Dustbowl.Bike
{
    [CreateAssetMenu(fileName = "ArcadeBikeTuning", menuName = "Dustbowl/Arcade Bike Tuning")]
    public sealed class ArcadeBikeTuning : ScriptableObject
    {
        [Header("Reference mode")]
        [SerializeField] private bool autoThrottle = true;

        [Header("Ground motion")]
        [SerializeField, Min(0f)] private float gravity = 22f;
        [SerializeField, Min(0f)] private float engineCeiling = 42f;
        [SerializeField, Min(0f)] private float rideHeight = 0.42f;
        [SerializeField, Min(0f)] private float contactTolerance = 0.06f;
        [SerializeField, Min(0f)] private float groundPower = 32f;
        [SerializeField, Min(0f)] private float forwardBrake = 24f;
        [SerializeField, Min(0f)] private float reverseDrive = 12f;
        [SerializeField, Range(0f, 1f)] private float slopeGravityFactor = 0.55f;
        [SerializeField, Min(0f)] private float steerRate = 2.2f;
        [SerializeField, Min(0.01f)] private float fullSteerSpeed = 6f;
        [SerializeField, Min(1f)] private float highSpeedSteerScale = 1.5f;
        [SerializeField, Range(0f, 1f)] private float maximumSteerReduction = 0.55f;
        [SerializeField, Min(0f)] private float gripRate = 14f;
        [SerializeField, Min(0f)] private float minimumGripRate = 7f;
        [SerializeField, Min(0.01f)] private float fullGripLossLateralSpeed = 12f;
        [SerializeField, Min(0f)] private float throttleDrag = 0.1f;
        [SerializeField, Min(0f)] private float coastDrag = 0.85f;
        [SerializeField, Min(0f)] private float looseSandDrag = 0.62f;
        [SerializeField, Min(0f)] private float speedShapedDrag = 0.004f;
        [SerializeField, Min(0f)] private float groundFollowSpring = 90f;
        [SerializeField, Min(0f)] private float groundVerticalDamping = 16f;
        [SerializeField, Min(0f)] private float groundOrientationRate = 11f;
        [SerializeField, Min(0f)] private float visualLeanRate = 8f;
        [SerializeField, Min(0f)] private float visualLeanSpeed = 16f;
        [SerializeField, Min(0f)] private float visualLeanLimit = 0.55f;

        [Header("Takeoff")]
        [SerializeField, Min(0f)] private float climbRateLimit = 40f;
        [SerializeField, Min(0f)] private float climbRateResponse = 11f;
        [SerializeField, Min(0f)] private float lipReleaseGap = 0.25f;
        [SerializeField, Min(0f)] private float crestReleaseMargin = 0.6f;
        [SerializeField, Min(0f)] private float maximumLaunchVerticalSpeed = 26f;

        [Header("Air")]
        [SerializeField, Min(0f)] private float airDrag = 0.06f;
        [SerializeField, Min(0f)] private float airPitchRate = 2.1f;
        [SerializeField, Min(0f)] private float airYawRate = 1.5f;
        [SerializeField, Min(0f)] private float airRollRate = 0.8f;
        [SerializeField, Min(0f)] private float neutralSettleDelay = 0.2f;
        [SerializeField, Min(0f)] private float neutralSettleRate = 1.5f;

        [Header("Landing and temporary recovery")]
        [SerializeField, Min(0f)] private float evaluatedAirTime = 0.3f;
        [SerializeField, Range(-1f, 1f)] private float wipeoutAlignment = 0.35f;
        [SerializeField, Range(-1f, 1f)] private float hardImpactAlignment = 0.62f;
        [SerializeField, Min(0f)] private float hardImpactSpeed = 20f;
        [SerializeField, Range(0f, 1f)] private float minimumAcceptedRetention = 0.5f;
        [SerializeField, Range(-1f, 1f)] private float fullRetentionAlignment = 0.85f;
        [SerializeField, Range(0f, 1f)] private float landingVerticalRebound = 0.12f;
        [SerializeField, Min(0f)] private float wipeoutDuration = 1.7f;
        [SerializeField, Min(0f)] private float wipeoutBounce = 0.25f;
        [SerializeField, Range(0f, 1f)] private float wipeoutGroundDamping = 0.55f;
        [SerializeField, Range(0f, 1f)] private float wipeoutTumbleDamping = 0.6f;
        [SerializeField, Range(0f, 89f)] private float safeResetSlope = 16f;

        public bool AutoThrottle => autoThrottle;
        public float Gravity => gravity;
        public float EngineCeiling => engineCeiling;
        public float RideHeight => rideHeight;
        public float ContactTolerance => contactTolerance;
        public float GroundPower => groundPower;
        public float ForwardBrake => forwardBrake;
        public float ReverseDrive => reverseDrive;
        public float SlopeGravityFactor => slopeGravityFactor;
        public float SteerRate => steerRate;
        public float FullSteerSpeed => fullSteerSpeed;
        public float HighSpeedSteerScale => highSpeedSteerScale;
        public float MaximumSteerReduction => maximumSteerReduction;
        public float GripRate => gripRate;
        public float MinimumGripRate => minimumGripRate;
        public float FullGripLossLateralSpeed => fullGripLossLateralSpeed;
        public float ThrottleDrag => throttleDrag;
        public float CoastDrag => coastDrag;
        public float LooseSandDrag => looseSandDrag;
        public float SpeedShapedDrag => speedShapedDrag;
        public float GroundFollowSpring => groundFollowSpring;
        public float GroundVerticalDamping => groundVerticalDamping;
        public float GroundOrientationRate => groundOrientationRate;
        public float VisualLeanRate => visualLeanRate;
        public float VisualLeanSpeed => visualLeanSpeed;
        public float VisualLeanLimit => visualLeanLimit;
        public float ClimbRateLimit => climbRateLimit;
        public float ClimbRateResponse => climbRateResponse;
        public float LipReleaseGap => lipReleaseGap;
        public float CrestReleaseMargin => crestReleaseMargin;
        public float MaximumLaunchVerticalSpeed => maximumLaunchVerticalSpeed;
        public float AirDrag => airDrag;
        public float AirPitchRate => airPitchRate;
        public float AirYawRate => airYawRate;
        public float AirRollRate => airRollRate;
        public float NeutralSettleDelay => neutralSettleDelay;
        public float NeutralSettleRate => neutralSettleRate;
        public float EvaluatedAirTime => evaluatedAirTime;
        public float WipeoutAlignment => wipeoutAlignment;
        public float HardImpactAlignment => hardImpactAlignment;
        public float HardImpactSpeed => hardImpactSpeed;
        public float MinimumAcceptedRetention => minimumAcceptedRetention;
        public float FullRetentionAlignment => fullRetentionAlignment;
        public float LandingVerticalRebound => landingVerticalRebound;
        public float WipeoutDuration => wipeoutDuration;
        public float WipeoutBounce => wipeoutBounce;
        public float WipeoutGroundDamping => wipeoutGroundDamping;
        public float WipeoutTumbleDamping => wipeoutTumbleDamping;
        public float SafeResetSlope => safeResetSlope;
    }
}
