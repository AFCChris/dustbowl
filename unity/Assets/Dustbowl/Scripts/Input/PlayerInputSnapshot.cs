using System;

namespace Dustbowl.Input
{
    [Serializable]
    public struct PlayerInputSnapshot
    {
        public float throttle;
        public float brake;
        public float steer;
        public float airPitch;
        public float airWhip;
        public bool resetPressed;
        public bool cameraNextPressed;
        public bool cameraPreviousPressed;
    }
}
