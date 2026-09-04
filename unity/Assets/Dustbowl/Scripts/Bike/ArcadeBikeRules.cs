using UnityEngine;

namespace Dustbowl.Bike
{
    public static class ArcadeBikeRules
    {
        public static float EnginePower(float forwardSpeed, ArcadeBikeTuning tuning)
        {
            return tuning.GroundPower
                * (1f - Mathf.Clamp01(forwardSpeed / tuning.EngineCeiling));
        }

        public static float SteeringAuthority(float speed, ArcadeBikeTuning tuning)
        {
            float lowSpeed = Mathf.Clamp01(speed / tuning.FullSteerSpeed);
            float highSpeedLoss = Mathf.Clamp(
                speed / (tuning.EngineCeiling * tuning.HighSpeedSteerScale),
                0f,
                tuning.MaximumSteerReduction);
            return lowSpeed * (1f - highSpeedLoss);
        }

        public static float GripRate(float lateralSpeed, ArcadeBikeTuning tuning)
        {
            float loss = Mathf.Clamp01(Mathf.Abs(lateralSpeed) / tuning.FullGripLossLateralSpeed);
            return Mathf.Lerp(tuning.GripRate, tuning.MinimumGripRate, loss);
        }

        public static float DragCoefficient(bool hasThrottle, float surfaceAmount, ArcadeBikeTuning tuning)
        {
            float loose = (1f - Mathf.Clamp01(surfaceAmount)) * tuning.LooseSandDrag;
            return (hasThrottle ? tuning.ThrottleDrag : tuning.CoastDrag) + loose;
        }

        public static float ExponentialResponse(float rate, float deltaTime)
        {
            return 1f - Mathf.Exp(-rate * deltaTime);
        }

        public static float UpdateClimbRate(
            float current,
            float priorHeight,
            float nextHeight,
            float deltaTime,
            ArcadeBikeTuning tuning)
        {
            float groundRate = Mathf.Clamp(
                (nextHeight - priorHeight) / deltaTime,
                -tuning.ClimbRateLimit,
                tuning.ClimbRateLimit);
            return Mathf.Lerp(
                current,
                groundRate,
                ExponentialResponse(tuning.ClimbRateResponse, deltaTime));
        }

        public static TakeoffTrigger EvaluateTakeoff(
            float chassisHeight,
            float nextGroundHeight,
            float recentClimbRate,
            float groundRate,
            float deltaTime,
            ArcadeBikeTuning tuning)
        {
            bool lip = chassisHeight > nextGroundHeight + tuning.RideHeight + tuning.LipReleaseGap;
            float carried = recentClimbRate - tuning.Gravity * deltaTime;
            bool crest = carried > groundRate + tuning.CrestReleaseMargin;
            if (lip && crest)
            {
                return TakeoffTrigger.LipAndRoundedCrest;
            }

            if (lip)
            {
                return TakeoffTrigger.Lip;
            }

            return crest ? TakeoffTrigger.RoundedCrest : TakeoffTrigger.None;
        }

        public static Stage3LandingResult EvaluateLanding(
            float airborneSeconds,
            float surfaceAlignment,
            float impactDownwardSpeed,
            ArcadeBikeTuning tuning)
        {
            if (airborneSeconds <= tuning.EvaluatedAirTime)
            {
                return new Stage3LandingResult
                {
                    outcome = Stage3LandingOutcome.ShortContact,
                    surfaceAlignment = surfaceAlignment,
                    impactDownwardSpeed = impactDownwardSpeed,
                    speedRetention = 1f
                };
            }

            bool wipeout = surfaceAlignment < tuning.WipeoutAlignment
                || (surfaceAlignment < tuning.HardImpactAlignment
                    && impactDownwardSpeed > tuning.HardImpactSpeed);
            float retention = Mathf.Lerp(
                tuning.MinimumAcceptedRetention,
                1f,
                Mathf.Clamp01(
                    (surfaceAlignment - tuning.WipeoutAlignment)
                    / (tuning.FullRetentionAlignment - tuning.WipeoutAlignment)));
            return new Stage3LandingResult
            {
                outcome = wipeout ? Stage3LandingOutcome.Wipeout : Stage3LandingOutcome.Accepted,
                surfaceAlignment = surfaceAlignment,
                impactDownwardSpeed = impactDownwardSpeed,
                speedRetention = retention
            };
        }
    }
}
