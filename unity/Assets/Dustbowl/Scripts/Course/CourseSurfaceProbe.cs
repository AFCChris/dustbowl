using UnityEngine;

namespace Dustbowl.Course
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CourseSurfaceProbe : MonoBehaviour
    {
        [SerializeField] private CourseSurface courseSurface;
        [SerializeField] private GroundSample currentSample;

        public GroundSample CurrentSample => currentSample;

        public void Configure(CourseSurface surface)
        {
            courseSurface = surface;
            Refresh();
        }

        [ContextMenu("Log current course sample")]
        public void LogCurrentSample()
        {
            Refresh();
            Debug.Log(
                $"Course probe position={transform.position:F3}, height={currentSample.point.y:F3}, "
                + $"normal={currentSample.normal:F3}, slope={currentSample.slopeDegrees:F2} deg, "
                + $"along={currentSample.course.along:F3}, lateral={currentSample.course.signedLateralDistance:F3}, "
                + $"surface={currentSample.surfaceType}, feature="
                + $"{(currentSample.course.hasNearbyFeature ? currentSample.course.nearbyFeature.id : "none")}, "
                + $"forward={currentSample.course.frame.forward:F3}, right={currentSample.course.frame.right:F3}",
                this);
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                Refresh();
            }
        }

        private void OnValidate()
        {
            Refresh();
        }

        private void OnDrawGizmos()
        {
            Refresh();
            if (courseSurface == null || !currentSample.course.isValid)
            {
                return;
            }

            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, currentSample.point);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(currentSample.point, currentSample.normal * 3f);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(currentSample.point, currentSample.course.frame.forward * 3f);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(currentSample.point, currentSample.course.frame.right * 3f);
        }

        private void Refresh()
        {
            if (courseSurface == null || courseSurface.Definition == null)
            {
                return;
            }

            currentSample = courseSurface.SampleGround(transform.position);
        }
    }
}
