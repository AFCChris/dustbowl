using UnityEngine;
using UnityEngine.InputSystem;

namespace Dustbowl.Input
{
    [DisallowMultipleComponent]
    public sealed class DustbowlInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;

        private InputAction throttle;
        private InputAction brake;
        private InputAction steer;
        private InputAction airPitch;
        private InputAction airWhip;
        private InputAction reset;
        private InputAction cameraNext;
        private InputAction cameraPrevious;

        public PlayerInputSnapshot Current { get; private set; }
        public InputActionAsset Actions => actions;

        public void Configure(InputActionAsset actionAsset)
        {
            actions = actionAsset;
            ResolveActions();
        }

        private void OnEnable()
        {
            ResolveActions();
            actions?.Enable();
        }

        private void OnDisable()
        {
            actions?.Disable();
        }

        private void Update()
        {
            if (actions == null)
            {
                Current = default;
                return;
            }

            Current = new PlayerInputSnapshot
            {
                throttle = throttle.ReadValue<float>(),
                brake = brake.ReadValue<float>(),
                steer = steer.ReadValue<float>(),
                airPitch = airPitch.ReadValue<float>(),
                airWhip = airWhip.ReadValue<float>(),
                resetPressed = reset.WasPressedThisFrame(),
                cameraNextPressed = cameraNext.WasPressedThisFrame(),
                cameraPreviousPressed = cameraPrevious.WasPressedThisFrame()
            };
        }

        private void ResolveActions()
        {
            if (actions == null)
            {
                return;
            }

            InputActionMap player = actions.FindActionMap("Player", true);
            throttle = player.FindAction("Throttle", true);
            brake = player.FindAction("Brake", true);
            steer = player.FindAction("Steer", true);
            airPitch = player.FindAction("AirPitch", true);
            airWhip = player.FindAction("AirWhip", true);
            reset = player.FindAction("Reset", true);
            cameraNext = player.FindAction("CameraNext", true);
            cameraPrevious = player.FindAction("CameraPrevious", true);
        }
    }
}
