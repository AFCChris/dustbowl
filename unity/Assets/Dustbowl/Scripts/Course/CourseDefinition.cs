using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dustbowl.Course
{
    [CreateAssetMenu(fileName = "CourseDefinition", menuName = "Dustbowl/Course Definition")]
    public sealed class CourseDefinition : ScriptableObject
    {
        [SerializeField] private string courseId;
        [SerializeField] private string segmentId;
        [SerializeField] private string sourceReference;
        [SerializeField] private int sourceSeed;
        [SerializeField] private float sourceLapLength;
        [SerializeField] private CourseLinePoint[] linePoints = Array.Empty<CourseLinePoint>();
        [SerializeField] private CourseFeature[] features = Array.Empty<CourseFeature>();
        [SerializeField] private CourseSpawnHint[] spawnHints = Array.Empty<CourseSpawnHint>();

        public string CourseId => courseId;
        public string SegmentId => segmentId;
        public string SourceReference => sourceReference;
        public int SourceSeed => sourceSeed;
        public float SourceLapLength => sourceLapLength;
        public IReadOnlyList<CourseLinePoint> LinePoints => linePoints;
        public IReadOnlyList<CourseFeature> Features => features;
        public IReadOnlyList<CourseSpawnHint> SpawnHints => spawnHints;
        public float StartAlong => linePoints.Length == 0 ? 0f : linePoints[0].along;
        public float EndAlong => linePoints.Length == 0 ? 0f : linePoints[linePoints.Length - 1].along;

        public void Configure(
            string newCourseId,
            string newSegmentId,
            string newSourceReference,
            int newSourceSeed,
            float newSourceLapLength,
            CourseLinePoint[] newLinePoints,
            CourseFeature[] newFeatures,
            CourseSpawnHint[] newSpawnHints)
        {
            courseId = newCourseId;
            segmentId = newSegmentId;
            sourceReference = newSourceReference;
            sourceSeed = newSourceSeed;
            sourceLapLength = newSourceLapLength;
            linePoints = newLinePoints ?? Array.Empty<CourseLinePoint>();
            features = newFeatures ?? Array.Empty<CourseFeature>();
            spawnHints = newSpawnHints ?? Array.Empty<CourseSpawnHint>();
        }

        public bool TryGetFeature(float along, float padding, out CourseFeature feature)
        {
            for (int index = 0; index < features.Length; index++)
            {
                if (features[index].Contains(along, padding))
                {
                    feature = features[index];
                    return true;
                }
            }

            feature = default;
            return false;
        }

        public bool TrySampleLine(float along, out CourseLinePoint sample)
        {
            if (linePoints.Length < 2 || along < StartAlong || along > EndAlong)
            {
                sample = default;
                return false;
            }

            for (int index = 0; index < linePoints.Length - 1; index++)
            {
                CourseLinePoint a = linePoints[index];
                CourseLinePoint b = linePoints[index + 1];
                if (along > b.along && index < linePoints.Length - 2)
                {
                    continue;
                }

                float t = Mathf.InverseLerp(a.along, b.along, along);
                sample = new CourseLinePoint
                {
                    center = Vector3.LerpUnclamped(a.center, b.center, t),
                    along = Mathf.LerpUnclamped(a.along, b.along, t),
                    bank = Mathf.LerpUnclamped(a.bank, b.bank, t),
                    racingLineOffset = Mathf.LerpUnclamped(a.racingLineOffset, b.racingLineOffset, t),
                    recommendedSpeedScale = Mathf.LerpUnclamped(a.recommendedSpeedScale, b.recommendedSpeedScale, t)
                };
                return true;
            }

            sample = linePoints[linePoints.Length - 1];
            return true;
        }
    }
}
