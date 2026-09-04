using System;
using UnityEngine;

namespace Dustbowl.Course
{
    public enum SurfaceType
    {
        NaturalSand,
        PackedTrack,
        Berm,
        LooseSand
    }

    public enum CourseSurfaceModel
    {
        DustbowlFlatsReference,
        RoundedCrestDevelopment,
        DustbowlFlatsNational
    }

    public enum CourseFeatureKind
    {
        Table,
        Tabletop,
        Whoops,
        Ripples,
        NaturalCrest
    }

    [Serializable]
    public struct CourseFrame
    {
        public Vector3 center;
        public Vector3 forward;
        public Vector3 right;
        public Vector3 surfaceNormal;
    }

    [Serializable]
    public struct CourseFeature
    {
        public string id;
        public CourseFeatureKind kind;
        public float startAlong;
        public float length;
        public float height;
        public float wavelength;
        public string sourceSection;

        public float EndAlong => startAlong + length;

        public bool Contains(float along, float padding = 0f)
        {
            return along >= startAlong - padding && along <= EndAlong + padding;
        }
    }

    [Serializable]
    public struct CourseLinePoint
    {
        public Vector3 center;
        public float along;
        public float bank;
        public float racingLineOffset;
        public float recommendedSpeedScale;

        public CourseLinePoint(
            float x,
            float y,
            float z,
            float distanceAlong,
            float bankSlope,
            float lineOffset,
            float speedScale)
        {
            center = new Vector3(x, y, z);
            along = distanceAlong;
            bank = bankSlope;
            racingLineOffset = lineOffset;
            recommendedSpeedScale = speedScale;
        }
    }

    [Serializable]
    public struct CourseSpawnHint
    {
        public string id;
        public float along;
        public float lateralOffset;
        public bool safeForReset;
    }

    [Serializable]
    public struct CourseSample
    {
        public bool isValid;
        public float along;
        public float signedLateralDistance;
        public float distanceFromCenter;
        public float gradedRoadHeight;
        public float centerlineHeight;
        public float bank;
        public float racingLineOffset;
        public float recommendedSpeedScale;
        public CourseFrame frame;
        public bool hasNearbyFeature;
        public CourseFeature nearbyFeature;
    }

    [Serializable]
    public struct GroundSample
    {
        public Vector3 point;
        public Vector3 normal;
        public float slopeDegrees;
        public SurfaceType surfaceType;
        public float trackBlend;
        public CourseSample course;
    }

    public interface ICourseSurface
    {
        CourseDefinition Definition { get; }
        float SampleHeight(Vector3 worldPosition);
        Vector3 SampleNormal(Vector3 worldPosition);
        GroundSample SampleGround(Vector3 worldPosition);
        CourseSample SampleCourse(Vector3 worldPosition);
        bool TryGetFeature(float along, float padding, out CourseFeature feature);
    }
}
