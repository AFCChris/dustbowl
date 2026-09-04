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

    public enum Stage3LandingOutcome
    {
        ShortContact,
        Accepted,
        Wipeout
    }

    public enum TakeoffTrigger
    {
        None,
        ContactLoss,
        Lip,
        RoundedCrest,
        LipAndRoundedCrest
    }

    [Serializable]
    public struct Stage3LandingResult
    {
        public Stage3LandingOutcome outcome;
        public float surfaceAlignment;
        public float impactDownwardSpeed;
        public float speedRetention;
    }

    [Serializable]
    public struct BikeControllerState
    {
        public ulong tick;
        public Vector3 position;
        public Vector3 velocity;
        public Quaternion orientation;
        public BikeMotionState motionState;
        public float yawRadians;
        public float recentClimbRate;
        public float airborneSeconds;
        public float surfaceAmount;
        public float visualLeanRadians;
        public Stage3LandingOutcome lastLandingOutcome;
        public bool controlsEnabled;
    }
}
