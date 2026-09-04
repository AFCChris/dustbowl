using System;
using UnityEngine;

namespace Dustbowl.Course
{
    public static class RoundedCrestReference
    {
        public const string CourseId = "behaviour-lab";
        public const string SegmentId = "natural-rounded-crest";
        public const string SourceReference = "Stage 3 development-only recent-climb-rate validation strip";
        public const float BaseHeight = 8f;
        public const float Length = 160f;
        public const float CrestStart = 60f;
        public const float CrestLength = 60f;
        public const float CrestHeight = 4.2f;
        public const float PackedHalfWidth = 10f;
        public const float LooseHalfWidth = 16f;
        public const float CenterX = 340f;
        public const float StartZ = -110f;
        public const float SpawnAlong = 5f;

        public static float SampleHeight(float along)
        {
            if (along <= CrestStart || along >= CrestStart + CrestLength)
            {
                return BaseHeight;
            }

            float t = Mathf.Clamp01((along - CrestStart) / CrestLength);
            return BaseHeight + CrestHeight * (0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f));
        }

        public static void Populate(CourseDefinition definition)
        {
            const float spacing = 4f;
            int count = Mathf.RoundToInt(Length / spacing) + 1;
            var points = new CourseLinePoint[count];
            for (int index = 0; index < count; index++)
            {
                float along = index * spacing;
                points[index] = new CourseLinePoint(
                    CenterX,
                    SampleHeight(along),
                    StartZ + along,
                    along,
                    0f,
                    0f,
                    1f);
            }

            definition.Configure(
                CourseId,
                SegmentId,
                SourceReference,
                0,
                Length,
                points,
                Array.Empty<CourseFeature>(),
                new[]
                {
                    new CourseSpawnHint
                    {
                        id = "crest-approach",
                        along = SpawnAlong,
                        lateralOffset = 0f,
                        safeForReset = true
                    }
                });
        }
    }
}
