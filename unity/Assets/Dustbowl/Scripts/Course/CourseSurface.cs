using System;
using UnityEngine;

namespace Dustbowl.Course
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class CourseSurface : MonoBehaviour, ICourseSurface
    {
        [SerializeField] private CourseDefinition definition;
        [SerializeField] private Mesh generatedMesh;

        public CourseDefinition Definition => definition;
        public Mesh GeneratedMesh => generatedMesh;

        public void Configure(CourseDefinition courseDefinition, Mesh sharedMesh)
        {
            definition = courseDefinition;
            generatedMesh = sharedMesh;
            ApplySharedMesh();
        }

        public float SampleHeight(Vector3 worldPosition)
        {
            EnsureDefinition();
            float natural = DustbowlFlatsHeight.Sample(worldPosition.x, worldPosition.z);
            CourseSample course = SampleCourse(worldPosition);
            float offset = Mathf.Abs(course.signedLateralDistance);
            float influenceLimit = DustbowlFlatsReferenceSegment.TrackHalfWidth
                + DustbowlFlatsReferenceSegment.BermWidth
                + DustbowlFlatsReferenceSegment.BlendWidth;
            if (!course.isValid || offset > influenceLimit)
            {
                return natural;
            }

            float featureHeight = SampleFeatureHeight(course.along, course.signedLateralDistance);
            float trackSurface = course.gradedRoadHeight
                - DustbowlFlatsReferenceSegment.TrackCut
                + course.bank * course.signedLateralDistance
                + featureHeight;

            if (offset <= DustbowlFlatsReferenceSegment.TrackHalfWidth)
            {
                return trackSurface;
            }

            if (offset <= DustbowlFlatsReferenceSegment.TrackHalfWidth + DustbowlFlatsReferenceSegment.BermWidth)
            {
                float bermT = (offset - DustbowlFlatsReferenceSegment.TrackHalfWidth)
                    / DustbowlFlatsReferenceSegment.BermWidth;
                return trackSurface + DustbowlFlatsReferenceSegment.BermHeight * SmoothStep01(bermT);
            }

            float blendT = Mathf.Clamp01(
                (offset - DustbowlFlatsReferenceSegment.TrackHalfWidth - DustbowlFlatsReferenceSegment.BermWidth)
                / DustbowlFlatsReferenceSegment.BlendWidth);
            float bermTop = trackSurface + DustbowlFlatsReferenceSegment.BermHeight;
            return Mathf.LerpUnclamped(bermTop, natural, SmoothStep01(blendT));
        }

        public Vector3 SampleNormal(Vector3 worldPosition)
        {
            float distance = DustbowlFlatsReferenceSegment.NormalSampleDistance;
            float left = SampleHeight(new Vector3(worldPosition.x - distance, 0f, worldPosition.z));
            float right = SampleHeight(new Vector3(worldPosition.x + distance, 0f, worldPosition.z));
            float down = SampleHeight(new Vector3(worldPosition.x, 0f, worldPosition.z - distance));
            float up = SampleHeight(new Vector3(worldPosition.x, 0f, worldPosition.z + distance));
            return new Vector3(left - right, 2f * distance, down - up).normalized;
        }

        public GroundSample SampleGround(Vector3 worldPosition)
        {
            CourseSample course = SampleCourse(worldPosition);
            float height = SampleHeight(worldPosition);
            Vector3 normal = SampleNormal(worldPosition);
            float trackBlend = course.isValid
                ? 1f - SmoothStep(
                    DustbowlFlatsReferenceSegment.TrackHalfWidth,
                    DustbowlFlatsReferenceSegment.TrackHalfWidth
                        + DustbowlFlatsReferenceSegment.BermWidth * 0.8f,
                    Mathf.Abs(course.signedLateralDistance))
                : 0f;

            return new GroundSample
            {
                point = new Vector3(worldPosition.x, height, worldPosition.z),
                normal = normal,
                slopeDegrees = Vector3.Angle(Vector3.up, normal),
                surfaceType = ClassifySurface(course),
                trackBlend = trackBlend,
                course = course
            };
        }

        public CourseSample SampleCourse(Vector3 worldPosition)
        {
            EnsureDefinition();
            if (definition.LinePoints.Count < 2)
            {
                return default;
            }

            float bestDistanceSquared = float.PositiveInfinity;
            int bestSegment = 0;
            float bestT = 0f;
            for (int index = 0; index < definition.LinePoints.Count - 1; index++)
            {
                CourseLinePoint a = definition.LinePoints[index];
                CourseLinePoint b = definition.LinePoints[index + 1];
                Vector2 start = new(a.center.x, a.center.z);
                Vector2 delta = new(b.center.x - a.center.x, b.center.z - a.center.z);
                Vector2 query = new(worldPosition.x, worldPosition.z);
                float lengthSquared = delta.sqrMagnitude;
                float t = lengthSquared > 0f
                    ? Mathf.Clamp01(Vector2.Dot(query - start, delta) / lengthSquared)
                    : 0f;
                Vector2 nearest = start + delta * t;
                float distanceSquared = (query - nearest).sqrMagnitude;
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestSegment = index;
                    bestT = t;
                }
            }

            CourseLinePoint first = definition.LinePoints[bestSegment];
            CourseLinePoint second = definition.LinePoints[bestSegment + 1];
            Vector3 planarDelta = new(second.center.x - first.center.x, 0f, second.center.z - first.center.z);
            float planarLength = Mathf.Max(planarDelta.magnitude, 0.0001f);
            Vector3 horizontalForward = planarDelta / planarLength;
            Vector3 horizontalRight = new(horizontalForward.z, 0f, -horizontalForward.x);
            Vector3 queryOffset = worldPosition - first.center;
            float signedLateral = Vector3.Dot(queryOffset, horizontalRight);
            float along = Mathf.LerpUnclamped(first.along, second.along, bestT);
            float gradedHeight = Mathf.LerpUnclamped(first.center.y, second.center.y, bestT);
            float bank = Mathf.LerpUnclamped(first.bank, second.bank, bestT);
            float centerSurface = gradedHeight
                - DustbowlFlatsReferenceSegment.TrackCut
                + SampleFeatureHeight(along, 0f);
            Vector3 center = new(
                Mathf.LerpUnclamped(first.center.x, second.center.x, bestT),
                centerSurface,
                Mathf.LerpUnclamped(first.center.z, second.center.z, bestT));
            Vector3 forward = new Vector3(
                second.center.x - first.center.x,
                second.center.y - first.center.y,
                second.center.z - first.center.z).normalized;
            Vector3 bankedRight = new Vector3(horizontalRight.x, bank, horizontalRight.z).normalized;
            Vector3 courseNormal = Vector3.Cross(forward, bankedRight).normalized;
            bool hasFeature = TryGetFeature(along, 0f, out CourseFeature feature);

            return new CourseSample
            {
                isValid = true,
                along = along,
                signedLateralDistance = signedLateral,
                distanceFromCenter = Mathf.Sqrt(bestDistanceSquared),
                gradedRoadHeight = gradedHeight,
                centerlineHeight = centerSurface,
                bank = bank,
                racingLineOffset = Mathf.LerpUnclamped(first.racingLineOffset, second.racingLineOffset, bestT),
                recommendedSpeedScale = Mathf.LerpUnclamped(
                    first.recommendedSpeedScale,
                    second.recommendedSpeedScale,
                    bestT),
                frame = new CourseFrame
                {
                    center = center,
                    forward = forward,
                    right = bankedRight,
                    surfaceNormal = courseNormal
                },
                hasNearbyFeature = hasFeature,
                nearbyFeature = feature
            };
        }

        public bool TryGetFeature(float along, float padding, out CourseFeature feature)
        {
            EnsureDefinition();
            return definition.TryGetFeature(along, padding, out feature);
        }

        private SurfaceType ClassifySurface(CourseSample course)
        {
            if (!course.isValid)
            {
                return SurfaceType.NaturalSand;
            }

            float offset = Mathf.Abs(course.signedLateralDistance);
            if (offset <= DustbowlFlatsReferenceSegment.TrackHalfWidth)
            {
                return SurfaceType.PackedTrack;
            }

            if (offset <= DustbowlFlatsReferenceSegment.TrackHalfWidth + DustbowlFlatsReferenceSegment.BermWidth)
            {
                return SurfaceType.Berm;
            }

            if (offset <= DustbowlFlatsReferenceSegment.TrackHalfWidth
                + DustbowlFlatsReferenceSegment.BermWidth
                + DustbowlFlatsReferenceSegment.BlendWidth)
            {
                return SurfaceType.LooseSand;
            }

            return SurfaceType.NaturalSand;
        }

        private float SampleFeatureHeight(float along, float signedLateral)
        {
            if (!TryGetFeature(along, 0f, out CourseFeature feature))
            {
                return 0f;
            }

            float lateral = 1f - SmoothStep(
                DustbowlFlatsReferenceSegment.TrackHalfWidth * 0.6f,
                DustbowlFlatsReferenceSegment.TrackHalfWidth * 1.1f,
                Mathf.Abs(signedLateral));
            float distance = along - feature.startAlong;
            float t = distance / feature.length;
            switch (feature.kind)
            {
                case CourseFeatureKind.Whoops:
                case CourseFeatureKind.Ripples:
                    int count = Mathf.RoundToInt(feature.length / feature.wavelength);
                    return (0.5f - 0.5f * Mathf.Cos(t * count * Mathf.PI * 2f))
                        * feature.height
                        * lateral
                        * Mathf.Sin(t * Mathf.PI);
                case CourseFeatureKind.Tabletop:
                    float ramp = SmoothStep(0f, 0.26f, t) * (1f - SmoothStep(0.74f, 1f, t));
                    return ramp * feature.height * lateral;
                default:
                    return Mathf.Pow(Mathf.Sin(Mathf.PI * t), 1.35f) * feature.height * lateral;
            }
        }

        private void OnValidate()
        {
            ApplySharedMesh();
        }

        private void ApplySharedMesh()
        {
            if (generatedMesh == null)
            {
                return;
            }

            GetComponent<MeshFilter>().sharedMesh = generatedMesh;
            GetComponent<MeshCollider>().sharedMesh = generatedMesh;
        }

        private void EnsureDefinition()
        {
            if (definition == null)
            {
                throw new InvalidOperationException("CourseSurface requires an authoritative CourseDefinition.");
            }
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return SmoothStep01(t);
        }

        private static float SmoothStep01(float value)
        {
            return value * value * (3f - 2f * value);
        }
    }
}
