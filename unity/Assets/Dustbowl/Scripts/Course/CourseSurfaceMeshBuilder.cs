using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dustbowl.Course
{
    public static class CourseSurfaceMeshBuilder
    {
        public const float AlongSpacing = 1f;
        public const float LateralHalfExtent = 32f;
        public const int LateralSegments = 64;

        public static Mesh Build(ICourseSurface surface)
        {
            if (surface?.Definition == null || surface.Definition.LinePoints.Count < 2)
            {
                throw new ArgumentException("A populated course surface is required.", nameof(surface));
            }

            CourseDefinition definition = surface.Definition;
            float length = definition.EndAlong - definition.StartAlong;
            int alongSegments = Mathf.Max(1, Mathf.CeilToInt(length / AlongSpacing));
            int columns = alongSegments + 1;
            int rows = LateralSegments + 1;
            var vertices = new Vector3[columns * rows];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[alongSegments * LateralSegments * 6];

            for (int column = 0; column < columns; column++)
            {
                float alongT = column / (float)alongSegments;
                float along = Mathf.Lerp(definition.StartAlong, definition.EndAlong, alongT);
                if (!definition.TrySampleLine(along, out CourseLinePoint line))
                {
                    throw new InvalidOperationException($"No line sample exists at {along:F3} m.");
                }

                Vector3 horizontalForward = GetHorizontalForward(definition, along);
                Vector3 horizontalRight = new(horizontalForward.z, 0f, -horizontalForward.x);
                for (int row = 0; row < rows; row++)
                {
                    float lateralT = row / (float)LateralSegments;
                    float lateral = Mathf.Lerp(-LateralHalfExtent, LateralHalfExtent, lateralT);
                    Vector3 world = line.center + horizontalRight * lateral;
                    world.y = surface.SampleHeight(world);
                    int index = column * rows + row;
                    vertices[index] = world;
                    uv[index] = new Vector2(lateralT, alongT);
                }
            }

            int triangle = 0;
            for (int column = 0; column < alongSegments; column++)
            {
                for (int row = 0; row < LateralSegments; row++)
                {
                    int current = column * rows + row;
                    int nextAlong = current + rows;
                    triangles[triangle++] = current;
                    triangles[triangle++] = nextAlong;
                    triangles[triangle++] = current + 1;
                    triangles[triangle++] = current + 1;
                    triangles[triangle++] = nextAlong;
                    triangles[triangle++] = nextAlong + 1;
                }
            }

            var mesh = new Mesh
            {
                name = "DustbowlFlats_TabletopReference_AuthoritativeMesh",
                indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = vertices,
                triangles = triangles,
                uv = uv
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static int DeterministicSignature(Mesh mesh)
        {
            unchecked
            {
                int hash = 17;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    hash = hash * 31 + Mathf.RoundToInt(vertex.x * 1000f);
                    hash = hash * 31 + Mathf.RoundToInt(vertex.y * 1000f);
                    hash = hash * 31 + Mathf.RoundToInt(vertex.z * 1000f);
                }

                foreach (int index in mesh.triangles)
                {
                    hash = hash * 31 + index;
                }

                return hash;
            }
        }

        private static Vector3 GetHorizontalForward(CourseDefinition definition, float along)
        {
            float beforeAlong = Mathf.Max(definition.StartAlong, along - 0.5f);
            float afterAlong = Mathf.Min(definition.EndAlong, along + 0.5f);
            definition.TrySampleLine(beforeAlong, out CourseLinePoint before);
            definition.TrySampleLine(afterAlong, out CourseLinePoint after);
            Vector3 forward = new(after.center.x - before.center.x, 0f, after.center.z - before.center.z);
            return forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector3.forward;
        }
    }
}
