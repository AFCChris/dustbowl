using Dustbowl.Course;
using UnityEngine;

namespace Dustbowl.Race
{
    [DisallowMultipleComponent]
    public sealed class NationalAIRider : MonoBehaviour
    {
        [SerializeField] private string riderName = "Rider";
        [SerializeField] private CourseSurface surface;
        [SerializeField] private float basePace = .82f;
        [SerializeField] private float gridAlong;
        [SerializeField] private float gridLateral;
        [SerializeField] private float along;
        [SerializeField] private float speed;
        [SerializeField] private float finishTime;
        [SerializeField] private RaceLapTracker laps = new();

        private float mistakeTimer;
        private float mistakeDuration;
        private float currentLateral;
        private int riderIndex;

        public string RiderName => riderName;
        public float Along => along;
        public float FinishTime => finishTime;
        public int Lap => laps.Lap;
        public bool Finished => laps.Finished;
        public float Speed => speed;

        public void Configure(
            string displayName,
            int index,
            CourseSurface courseSurface,
            float pace,
            float startAlong,
            float lateral)
        {
            riderName = displayName;
            riderIndex = index;
            surface = courseSurface;
            basePace = pace;
            gridAlong = startAlong;
            gridLateral = lateral;
            ResetRider();
        }

        public void ResetRider()
        {
            along = gridAlong;
            currentLateral = gridLateral;
            speed = 0f;
            finishTime = 0f;
            mistakeTimer = 4f + riderIndex * 1.17f;
            mistakeDuration = 0f;
            laps.Reset(along);
            ApplyPose();
        }

        public void Tick(float deltaTime, bool raceRunning, float playerRaceDistance, float raceTime)
        {
            if (surface == null || !raceRunning || Finished)
            {
                return;
            }

            float lapLength = surface.Definition.EndAlong;
            surface.Definition.TrySampleLine(along, out CourseLinePoint line);
            float aiDistance = (Lap - 1) * lapLength + along;
            float rubberBand = Mathf.Clamp((playerRaceDistance - aiDistance) / 200f, -.05f, .05f);
            mistakeTimer -= deltaTime;
            if (mistakeDuration > 0f)
            {
                mistakeDuration -= deltaTime;
            }
            else if (mistakeTimer <= 0f)
            {
                mistakeDuration = .7f + Mathf.Repeat(riderIndex * .317f + Lap * .23f, 1.1f);
                mistakeTimer = 7f + Mathf.Repeat(riderIndex * 2.41f + along * .013f, 12f);
            }

            float mistakePace = mistakeDuration > 0f ? .68f : 1f;
            float targetSpeed = 42f * (basePace + rubberBand) * line.recommendedSpeedScale * mistakePace;
            speed = Mathf.MoveTowards(speed, targetSpeed, 10f * deltaTime);
            along = Mathf.Repeat(along + speed * deltaTime, lapLength);
            if (laps.Update(along, lapLength, DustbowlFlatsNationalCourse.Laps, out bool finished) && finished)
            {
                finishTime = raceTime;
            }

            float weave = Mathf.Sin(along * .021f + riderIndex * 1.7f) * (1.1f + riderIndex * .08f);
            float mistakeLine = mistakeDuration > 0f ? Mathf.Sin(riderIndex * 9.1f) * 2.5f : 0f;
            currentLateral = Mathf.MoveTowards(currentLateral, weave + mistakeLine, 3.5f * deltaTime);
            ApplyPose();
        }

        public float EffectiveDistance(float lapLength, int totalLaps)
            => laps.EffectiveDistance(along, lapLength, totalLaps, finishTime);

        private void ApplyPose()
        {
            if (surface == null || surface.Definition == null
                || !surface.Definition.TrySampleLine(along, out CourseLinePoint line))
            {
                return;
            }

            float ahead = Mathf.Repeat(along + 2.5f, surface.Definition.EndAlong);
            surface.Definition.TrySampleLine(ahead, out CourseLinePoint next);
            Vector3 forward = next.center - line.center;
            forward.y = 0f;
            if (forward.sqrMagnitude < .001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            Vector3 right = new(forward.z, 0f, -forward.x);
            Vector3 query = line.center + right * currentLateral;
            GroundSample ground = surface.SampleGround(query);
            float visualJump = 0f;
            if (surface.TryGetFeature(along, 0f, out CourseFeature feature)
                && (feature.kind == CourseFeatureKind.Table || feature.kind == CourseFeatureKind.Tabletop))
            {
                float featureT = (along - feature.startAlong) / feature.length;
                if (featureT > .44f)
                {
                    visualJump = Mathf.Sin(Mathf.Clamp01((featureT - .44f) / .56f) * Mathf.PI)
                        * feature.height * .32f;
                }
            }

            transform.SetPositionAndRotation(
                ground.point + Vector3.up * (.42f + visualJump),
                Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, ground.normal).normalized, ground.normal));
        }
    }
}
