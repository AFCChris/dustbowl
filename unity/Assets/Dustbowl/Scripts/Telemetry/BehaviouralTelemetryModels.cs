using System;
using Dustbowl.Bike;
using Dustbowl.Course;
using Dustbowl.Input;
using UnityEngine;

namespace Dustbowl.Telemetry
{
    [Serializable]
    public struct TelemetrySample
    {
        public ulong tick;
        public double elapsedSeconds;
        public PlayerInputSnapshot input;
        public BikeControllerState bike;
        public GroundContactSample ground;
    }

    [Serializable]
    public struct TakeoffEvent
    {
        public ulong tick;
        public double elapsedSeconds;
        public float approachSpeed;
        public float takeoffSpeed;
        public float recentClimbRate;
        public float launchVerticalVelocity;
        public Quaternion orientation;
        public Vector3 position;
        public float trackAlong;
        public float trackLateral;
        public TakeoffTrigger trigger;
    }

    [Serializable]
    public struct AirborneState
    {
        public ulong tick;
        public double elapsedSeconds;
        public float airborneDuration;
        public Vector3 velocity;
        public Quaternion orientation;
    }

    [Serializable]
    public struct LandingEvent
    {
        public ulong tick;
        public double elapsedSeconds;
        public float airborneDuration;
        public Quaternion touchdownOrientation;
        public float touchdownPitchDegrees;
        public float touchdownRollDegrees;
        public float touchdownYawDegrees;
        public float surfaceAlignment;
        public float alignmentErrorDegrees;
        public float impactDownwardSpeed;
        public LandingTier classification;
        public Stage3LandingOutcome stage3Outcome;
        public float travelAlignment;
        public float speedRetention;
        public Vector3 touchdownLocation;
    }

    [Serializable]
    public struct WipeoutEvent
    {
        public ulong tick;
        public double elapsedSeconds;
        public Vector3 position;
        public Quaternion orientation;
        public Vector3 velocity;
    }

    [Serializable]
    public struct ResetEvent
    {
        public ulong tick;
        public double elapsedSeconds;
        public Vector3 restoredPosition;
        public Quaternion restoredOrientation;
        public bool followsWipeout;
        public string reason;
    }

    [Serializable]
    public struct RecoveryEvent
    {
        public double wipeoutCommittedSeconds;
        public double usefulControlRestoredSeconds;
        public float recoveryDuration;
        public Vector3 restoredPosition;
    }

    public interface IBehaviouralTelemetrySink
    {
        void Record(TelemetrySample sample);
        void Record(TakeoffEvent takeoff);
        void Record(AirborneState airborne);
        void Record(LandingEvent landing);
        void Record(WipeoutEvent wipeout);
        void Record(ResetEvent reset);
        void Record(RecoveryEvent recovery);
    }
}
