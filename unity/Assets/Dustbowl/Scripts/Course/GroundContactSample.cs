using System;
using UnityEngine;

namespace Dustbowl.Course
{
    [Serializable]
    public struct GroundContactSample
    {
        public bool hasContact;
        public Vector3 point;
        public Vector3 normal;
        public float surfaceAlignment;
        public float recentClimbRate;
        public float trackDistance;
        public float trackOffset;
    }
}
