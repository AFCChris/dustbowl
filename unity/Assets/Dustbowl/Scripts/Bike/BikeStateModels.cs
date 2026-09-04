using System;
using UnityEngine;

namespace Dustbowl.Bike
{
    public enum BikeMotionState
    {
        Grounded,
        Airborne,
        Wipeout,
        Recovering
    }

    public enum LandingTier
    {
        Unclassified,
        Clean,
        Sketchy,
        Ugly,
        Wipeout
    }

    [Serializable]
    public struct BikeControllerState
    {
        public ulong tick;
        public Vector3 position;
        public Vector3 velocity;
        public Quaternion orientation;
        public BikeMotionState motionState;
    }
}
