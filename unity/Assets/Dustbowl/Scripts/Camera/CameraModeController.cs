using System;
using UnityEngine;

namespace Dustbowl.Camera
{
    public enum CameraMode
    {
        Chase,
        Close,
        Overhead
    }

    [DisallowMultipleComponent]
    public sealed class CameraModeController : MonoBehaviour
    {
        [SerializeField] private CameraMode currentMode = CameraMode.Chase;

        public event Action<CameraMode> ModeChanged;
        public CameraMode CurrentMode => currentMode;

        public bool Supports(CameraMode mode)
        {
            return mode is CameraMode.Chase or CameraMode.Close or CameraMode.Overhead;
        }

        public void Next()
        {
            Set((CameraMode)(((int)currentMode + 1) % Enum.GetValues(typeof(CameraMode)).Length));
        }

        public void Previous()
        {
            int count = Enum.GetValues(typeof(CameraMode)).Length;
            Set((CameraMode)(((int)currentMode - 1 + count) % count));
        }

        public void Set(CameraMode mode)
        {
            if (!Supports(mode) || currentMode == mode)
            {
                return;
            }

            currentMode = mode;
            ModeChanged?.Invoke(currentMode);
        }
    }
}
