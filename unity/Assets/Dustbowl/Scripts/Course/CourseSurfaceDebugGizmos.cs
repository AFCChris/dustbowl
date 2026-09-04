using UnityEngine;

namespace Dustbowl.Course
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CourseSurfaceDebugGizmos : MonoBehaviour
    {
        [SerializeField] private CourseSurface courseSurface;
        [SerializeField] private bool showCenterline = true;
        [SerializeField] private bool showTrackEdges = true;
        [SerializeField] private bool showFrames = true;
        [SerializeField] private bool showFeatures = true;

        public void Configure(CourseSurface surface)
        {
            courseSurface = surface;
        }

        private void OnDrawGizmos()
        {
            if (courseSurface == null || courseSurface.Definition == null)
            {
                return;
            }

            CourseDefinition definition = courseSurface.Definition;
            CourseLinePoint? previous = null;
            for (float along = definition.StartAlong; along <= definition.EndAlong + 0.01f; along += 4f)
            {
                float clampedAlong = Mathf.Min(along, definition.EndAlong);
                if (!definition.TrySampleLine(clampedAlong, out CourseLinePoint line))
                {
                    continue;
                }

                CourseSample sample = courseSurface.SampleCourse(line.center);
                Vector3 center = sample.frame.center + Vector3.up * 0.08f;
                Vector3 horizontalRight = new(sample.frame.right.x, 0f, sample.frame.right.z);
                horizontalRight.Normalize();

                if (showCenterline && previous.HasValue)
                {
                    CourseSample prior = courseSurface.SampleCourse(previous.Value.center);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(prior.frame.center + Vector3.up * 0.08f, center);
                }

                if (showTrackEdges)
                {
                    Gizmos.color = new Color(1f, 0.45f, 0.08f);
                    DrawSurfaceMarker(center + horizontalRight * DustbowlFlatsReferenceSegment.TrackHalfWidth);
                    DrawSurfaceMarker(center - horizontalRight * DustbowlFlatsReferenceSegment.TrackHalfWidth);
                }

                if (showFrames && Mathf.RoundToInt(clampedAlong - definition.StartAlong) % 16 == 0)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawRay(center, sample.frame.forward * 3f);
                    Gizmos.color = Color.red;
                    Gizmos.DrawRay(center, sample.frame.right * 3f);
                    Gizmos.color = Color.green;
                    Gizmos.DrawRay(center, sample.frame.surfaceNormal * 3f);
                }

                previous = line;
            }

            if (!showFeatures)
            {
                return;
            }

            foreach (CourseFeature feature in definition.Features)
            {
                DrawFeatureMarker(feature.startAlong, new Color(0.2f, 1f, 1f));
                DrawFeatureMarker(feature.EndAlong, new Color(1f, 0.2f, 1f));
            }
        }

        private void DrawSurfaceMarker(Vector3 position)
        {
            position.y = courseSurface.SampleHeight(position) + 0.08f;
            Gizmos.DrawSphere(position, 0.14f);
        }

        private void DrawFeatureMarker(float along, Color color)
        {
            if (!courseSurface.Definition.TrySampleLine(along, out CourseLinePoint line))
            {
                return;
            }

            Vector3 point = line.center;
            point.y = courseSurface.SampleHeight(point);
            Gizmos.color = color;
            Gizmos.DrawWireCube(point + Vector3.up * 2f, new Vector3(1f, 4f, 1f));
        }
    }
}
