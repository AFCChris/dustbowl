using Dustbowl.Bike;
using UnityEngine;

namespace Dustbowl.Camera
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class ArcadeBikeCamera : MonoBehaviour
    {
        [SerializeField] private ArcadeBikeController bike;
        [SerializeField] private CameraModeController cameraModes;

        private Vector3 lookTarget;
        private bool initialized;

        public void Configure(ArcadeBikeController controller, CameraModeController modes)
        {
            bike = controller;
            cameraModes = modes;
            initialized = false;
        }

        /// <summary>Re-acquires the bike instantly on the next frame (grid resets, menu returns).</summary>
        public void Snap()
        {
            initialized = false;
        }

        private void LateUpdate()
        {
            if (bike == null || bike.CurrentSurface == null || cameraModes == null)
            {
                return;
            }

            BikeControllerState state = bike.State;
            float speed = state.velocity.magnitude;
            Vector3 forward = new(Mathf.Sin(state.yawRadians), 0f, Mathf.Cos(state.yawRadians));
            float airPull = Mathf.Clamp(state.airborneSeconds * 0.5f, 0f, 3.5f);
            Vector3 wanted;
            if (cameraModes.CurrentMode == CameraMode.Overhead)
            {
                wanted = state.position
                    - forward * (6f - airPull * 0.4f)
                    + Vector3.up * (22f - airPull);
            }
            else
            {
                float back = cameraModes.CurrentMode == CameraMode.Chase ? 6.4f + speed * 0.1f : 4.2f;
                float up = cameraModes.CurrentMode == CameraMode.Chase ? 2.5f : 1.8f;
                wanted = state.position - forward * back + Vector3.up * up;
                float terrainFloor = bike.CurrentSurface.SampleHeight(wanted) + 1.4f;
                wanted.y = Mathf.Max(wanted.y, terrainFloor);
            }

            Vector3 targetWanted = state.position
                + forward * 4f
                + Vector3.up * (1.2f + Mathf.Clamp(state.airborneSeconds * 0.9f, 0f, 2.2f));
            if (!initialized)
            {
                transform.position = wanted;
                lookTarget = targetWanted;
                initialized = true;
            }
            else
            {
                float followRate = state.motionState == BikeMotionState.Wipeout ? 3f : 7f;
                transform.position = Vector3.Lerp(
                    transform.position,
                    wanted,
                    1f - Mathf.Exp(-followRate * Time.deltaTime));
                lookTarget = Vector3.Lerp(
                    lookTarget,
                    targetWanted,
                    1f - Mathf.Exp(-9f * Time.deltaTime));
            }

            transform.LookAt(lookTarget);
            UnityEngine.Camera cameraComponent = GetComponent<UnityEngine.Camera>();
            float desiredFov = 60f
                + Mathf.Clamp01(speed / bike.Tuning.EngineCeiling) * 9f
                + Mathf.Clamp(state.airborneSeconds * 0.8f, 0f, 3f);
            cameraComponent.fieldOfView = Mathf.Lerp(
                cameraComponent.fieldOfView,
                desiredFov,
                1f - Mathf.Exp(-4f * Time.deltaTime));
        }
    }
}
