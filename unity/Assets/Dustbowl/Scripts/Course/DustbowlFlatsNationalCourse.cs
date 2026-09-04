using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dustbowl.Course
{
    /// <summary>
    /// Deterministic Unity reconstruction of the canonical v0.18 Dustbowl Flats
    /// centreline and authored feature data in src/game.js.
    /// </summary>
    public static class DustbowlFlatsNationalCourse
    {
        public const string CourseId = "flats";
        public const string CourseName = "Dustbowl Flats";
        public const string SegmentId = "flats-national-complete";
        public const string SourceReference = "web-v0.18-behavioural-baseline:src/game.js";
        public const float ExpectedLapLength = 1510.540817f;
        public const int Laps = 3;
        public const int RiderCount = 8;

        private const float LayoutScaleX = 286f;
        private const float LayoutScaleZ = 238f;
        private const float Spacing = 2.2f;
        private const int StartSection = 2;
        private const float StartFraction = 0.18f;

        private static readonly Vector2[] Layout =
        {
            new(-0.90f, -0.15f), new(-0.70f, -0.70f), new(-0.08f, -0.88f),
            new(0.58f, -0.72f), new(0.92f, -0.20f), new(0.78f, 0.42f),
            new(0.28f, 0.80f), new(-0.36f, 0.86f), new(-0.86f, 0.50f)
        };

        private static readonly Section[] Sections =
        {
            new(52f, 92f, 0f, .10f, 1f, Feature("table", .35f, 30f, 1.9f)),
            new(68f, 126f, -4f, .05f, 1.03f, Feature("whoops", .36f, 46f, 1.3f, 16f)),
            new(74f, 132f, 3f, 0f, 1.05f, Feature("table", .48f, 29f, 2.6f)),
            new(48f, 104f, 8f, .20f, .94f, Feature("whoops", .42f, 50f, 1.4f, 17f)),
            new(55f, 118f, 12f, .12f, .98f, Feature("tabletop", .40f, 36f, 3.6f)),
            new(76f, 134f, 6f, 0f, 1.04f, Feature("ripples", .34f, 46f, .62f, 4.2f)),
            new(70f, 122f, 1f, .06f, 1.02f, Feature("table", .44f, 30f, 2.2f)),
            new(50f, 112f, -3f, .18f, .95f, Feature("whoops", .38f, 46f, 1.35f, 16f)),
            new(46f, 106f, 0f, .22f, .93f, Feature("table", .52f, 27f, 4.2f))
        };

        public static void Populate(CourseDefinition definition)
        {
            Build(out CourseLinePoint[] line, out CourseFeature[] features);
            definition.Configure(
                CourseId,
                SegmentId,
                SourceReference,
                0,
                ExpectedLapLength,
                line,
                features,
                CreateSpawnHints(line[line.Length - 1].along));
        }

        public static void Build(out CourseLinePoint[] linePoints, out CourseFeature[] courseFeatures)
        {
            int count = Layout.Length;
            var checkpoints = new Checkpoint[count];
            for (int index = 0; index < count; index++)
            {
                checkpoints[index] = new Checkpoint
                {
                    position = new Vector2(Layout[index].x * LayoutScaleX, Layout[index].y * LayoutScaleZ)
                };
            }

            var points = new List<MutablePoint>(700);
            for (int index = 0; index < count; index++)
            {
                Checkpoint previous = checkpoints[(index - 1 + count) % count];
                Checkpoint current = checkpoints[index];
                Checkpoint next = checkpoints[(index + 1) % count];
                Checkpoint after = checkpoints[(index + 2) % count];
                Section section = Sections[index];
                Section nextSection = Sections[(index + 1) % count];
                Vector2 tangentA = Tangent(previous.position, current.position, next.position, section);
                Vector2 tangentB = Tangent(current.position, next.position, after.position, nextSection);
                int steps = Mathf.Max(6, Mathf.RoundToInt(Vector2.Distance(current.position, next.position) / Spacing));
                checkpoints[index].pointIndex = points.Count;
                for (int step = 0; step < steps; step++)
                {
                    float t = step / (float)steps;
                    points.Add(new MutablePoint
                    {
                        position = Hermite(current.position, next.position, tangentA, tangentB, t),
                        recommendedSpeed = Mathf.Lerp(
                            section.aiSpeed * (1f - section.braking * .18f),
                            nextSection.aiSpeed * (1f - nextSection.braking * .18f),
                            t)
                    });
                }
            }

            for (int sectionIndex = 0; sectionIndex < count; sectionIndex++)
            {
                int start = checkpoints[sectionIndex].pointIndex;
                int end = sectionIndex == count - 1 ? points.Count : checkpoints[sectionIndex + 1].pointIndex;
                for (int pointIndex = start; pointIndex < end; pointIndex++)
                {
                    float t = (pointIndex - start) / (float)Mathf.Max(1, end - start);
                    MutablePoint point = points[pointIndex];
                    float elevation = Mathf.Lerp(
                        Sections[sectionIndex].elevation,
                        Sections[(sectionIndex + 1) % count].elevation,
                        SmoothStep01(t));
                    point.height = DustbowlFlatsHeight.Sample(point.position.x, point.position.y) + elevation;
                }
            }

            SmoothCircular(points, 9, point => point.height, (point, value) => point.height = value);
            float rawLength = CalculateAlong(points);
            for (int index = 0; index < points.Count; index++)
            {
                MutablePoint a = points[(index - 1 + points.Count) % points.Count];
                MutablePoint b = points[index];
                MutablePoint c = points[(index + 1) % points.Count];
                Vector2 incoming = (b.position - a.position).normalized;
                Vector2 outgoing = (c.position - b.position).normalized;
                float cross = incoming.x * outgoing.y - incoming.y * outgoing.x;
                b.bank = Mathf.Clamp(cross * 9f, -.42f, .42f);
            }

            SmoothCircular(points, 12, point => point.bank, (point, value) => point.bank = value);

            var features = new List<MutableFeature>(count);
            for (int sectionIndex = 0; sectionIndex < count; sectionIndex++)
            {
                int startIndex = checkpoints[sectionIndex].pointIndex;
                int endIndex = sectionIndex == count - 1 ? points.Count : checkpoints[sectionIndex + 1].pointIndex;
                float start = points[startIndex].along;
                float end = sectionIndex == count - 1 ? rawLength : points[endIndex].along;
                SectionFeature source = Sections[sectionIndex].feature;
                features.Add(new MutableFeature(
                    sectionIndex,
                    source,
                    Mathf.Repeat(start + source.fraction * Mathf.Max(0f, end - start - source.length), rawLength)));
            }

            int sectionFirst = checkpoints[StartSection].pointIndex;
            int sectionEnd = checkpoints[StartSection + 1].pointIndex;
            int startPoint = Mathf.Clamp(
                sectionFirst + Mathf.RoundToInt((sectionEnd - sectionFirst) * Mathf.Clamp(StartFraction, .08f, .92f)),
                sectionFirst,
                Mathf.Max(sectionFirst, sectionEnd - 1));
            float oldStartAlong = points[startPoint].along;
            if (startPoint > 0)
            {
                var rotated = new List<MutablePoint>(points.Count);
                rotated.AddRange(points.GetRange(startPoint, points.Count - startPoint));
                rotated.AddRange(points.GetRange(0, startPoint));
                points = rotated;
            }

            float lapLength = CalculateAlong(points);
            linePoints = new CourseLinePoint[points.Count + 1];
            for (int index = 0; index < points.Count; index++)
            {
                MutablePoint point = points[index];
                linePoints[index] = new CourseLinePoint(
                    point.position.x,
                    point.height,
                    point.position.y,
                    point.along,
                    point.bank,
                    0f,
                    point.recommendedSpeed);
            }

            CourseLinePoint first = linePoints[0];
            linePoints[linePoints.Length - 1] = new CourseLinePoint(
                first.center.x,
                first.center.y,
                first.center.z,
                lapLength,
                first.bank,
                first.racingLineOffset,
                first.recommendedSpeedScale);

            courseFeatures = new CourseFeature[features.Count];
            for (int index = 0; index < features.Count; index++)
            {
                MutableFeature feature = features[index];
                SectionFeature source = feature.source;
                courseFeatures[index] = new CourseFeature
                {
                    id = $"flats-section-{feature.section}-{source.kind}",
                    kind = ParseKind(source.kind),
                    startAlong = Mathf.Repeat(feature.along - oldStartAlong, lapLength),
                    length = source.length,
                    height = source.height,
                    wavelength = source.wavelength,
                    sourceSection = $"Dustbowl Flats section {feature.section} (zero-based)"
                };
            }
        }

        private static CourseSpawnHint[] CreateSpawnHints(float lapLength)
        {
            float[] distances = { 2f, 8f, 8f, 14f, 14f, 20f, 20f, 26f };
            float[] laterals = { 0f, 2.5f, -2.5f, 2.5f, -2.5f, 2.5f, -2.5f, 0f };
            var hints = new CourseSpawnHint[distances.Length];
            for (int index = 0; index < hints.Length; index++)
            {
                hints[index] = new CourseSpawnHint
                {
                    id = index == 0 ? "player-grid" : $"ai-grid-{index}",
                    along = lapLength - distances[index],
                    lateralOffset = laterals[index],
                    safeForReset = index == 0
                };
            }

            return hints;
        }

        private static Vector2 Tangent(Vector2 previous, Vector2 current, Vector2 next, Section section)
        {
            Vector2 direction = (next - previous).normalized;
            float incoming = Vector2.Distance(current, previous);
            float outgoing = Vector2.Distance(next, current);
            float authored = section.cornerRadius + section.straightLength * .18f;
            return direction * Mathf.Min(authored, incoming * .46f, outgoing * .46f);
        }

        private static Vector2 Hermite(Vector2 a, Vector2 b, Vector2 tangentA, Vector2 tangentB, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * a
                + (t3 - 2f * t2 + t) * tangentA
                + (-2f * t3 + 3f * t2) * b
                + (t3 - t2) * tangentB;
        }

        private static float CalculateAlong(List<MutablePoint> points)
        {
            float length = 0f;
            for (int index = 0; index < points.Count; index++)
            {
                points[index].along = length;
                length += Vector2.Distance(points[index].position, points[(index + 1) % points.Count].position);
            }

            return length;
        }

        private static void SmoothCircular(
            List<MutablePoint> points,
            int passes,
            Func<MutablePoint, float> read,
            Action<MutablePoint, float> write)
        {
            var values = new float[points.Count];
            for (int pass = 0; pass < passes; pass++)
            {
                for (int index = 0; index < points.Count; index++)
                {
                    values[index] = read(points[index]);
                }

                for (int index = 0; index < points.Count; index++)
                {
                    write(points[index],
                        values[(index - 1 + points.Count) % points.Count] * .25f
                        + values[index] * .5f
                        + values[(index + 1) % points.Count] * .25f);
                }
            }
        }

        private static CourseFeatureKind ParseKind(string kind)
        {
            return kind switch
            {
                "whoops" => CourseFeatureKind.Whoops,
                "ripples" => CourseFeatureKind.Ripples,
                "tabletop" => CourseFeatureKind.Tabletop,
                _ => CourseFeatureKind.Table
            };
        }

        private static float SmoothStep01(float value) => value * value * (3f - 2f * value);

        private static SectionFeature Feature(string kind, float fraction, float length, float height, float wavelength = 0f)
            => new(kind, fraction, length, height, wavelength);

        private sealed class MutablePoint
        {
            public Vector2 position;
            public float height;
            public float bank;
            public float along;
            public float recommendedSpeed;
        }

        private struct Checkpoint
        {
            public Vector2 position;
            public int pointIndex;
        }

        private readonly struct MutableFeature
        {
            public MutableFeature(int featureSection, SectionFeature featureSource, float featureAlong)
            {
                section = featureSection;
                source = featureSource;
                along = featureAlong;
            }

            public readonly int section;
            public readonly SectionFeature source;
            public readonly float along;
        }

        private readonly struct SectionFeature
        {
            public SectionFeature(string featureKind, float at, float featureLength, float featureHeight, float wave)
            {
                kind = featureKind;
                fraction = at;
                length = featureLength;
                height = featureHeight;
                wavelength = wave;
            }

            public readonly string kind;
            public readonly float fraction;
            public readonly float length;
            public readonly float height;
            public readonly float wavelength;
        }

        private readonly struct Section
        {
            public Section(float radius, float straight, float y, float brake, float speed, SectionFeature sectionFeature)
            {
                cornerRadius = radius;
                straightLength = straight;
                elevation = y;
                braking = brake;
                aiSpeed = speed;
                feature = sectionFeature;
            }

            public readonly float cornerRadius;
            public readonly float straightLength;
            public readonly float elevation;
            public readonly float braking;
            public readonly float aiSpeed;
            public readonly SectionFeature feature;
        }
    }
}
