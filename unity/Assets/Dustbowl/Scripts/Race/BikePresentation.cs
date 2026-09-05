using Dustbowl.Bike;
using UnityEngine;

namespace Dustbowl.Race
{
    [DisallowMultipleComponent]
    public sealed class BikePresentation : MonoBehaviour
    {
        [SerializeField] private ArcadeBikeController player;
        [SerializeField] private NationalAIRider opponent;
        [SerializeField] private Transform frontWheel;
        [SerializeField] private Transform rearWheel;
        [SerializeField] private Transform riderBody;
        [SerializeField] private ParticleSystem dust;

        private float wheelAngle;

        public void Configure(
            ArcadeBikeController playerController,
            NationalAIRider aiController,
            Transform front,
            Transform rear,
            Transform rider,
            ParticleSystem dustTrail)
        {
            player = playerController;
            opponent = aiController;
            frontWheel = front;
            rearWheel = rear;
            riderBody = rider;
            dust = dustTrail;
        }

        private void Update()
        {
            float speed;
            bool grounded;
            float steerLean;
            if (player != null)
            {
                BikeControllerState state = player.State;
                speed = new Vector2(state.velocity.x, state.velocity.z).magnitude;
                grounded = state.motionState == BikeMotionState.Grounded;
                steerLean = state.visualLeanRadians;
            }
            else if (opponent != null)
            {
                speed = opponent.Speed;
                grounded = true;
                steerLean = Mathf.Sin(opponent.Along * .02f) * .12f;
            }
            else
            {
                return;
            }

            wheelAngle -= speed / .44f * Mathf.Rad2Deg * Time.deltaTime;
            if (frontWheel != null) frontWheel.localRotation = Quaternion.Euler(wheelAngle, 0f, 90f);
            if (rearWheel != null) rearWheel.localRotation = Quaternion.Euler(wheelAngle, 0f, 90f);
            if (riderBody != null)
            {
                riderBody.localRotation = Quaternion.Euler(-12f - speed * .22f, 0f, -steerLean * 32f);
            }

            if (dust != null)
            {
                float dustAmount = grounded ? Mathf.Clamp01((speed - 2f) / 24f) : 0f;
                ParticleSystem.EmissionModule emission = dust.emission;
                emission.rateOverTime = dustAmount * 38f;
                emission.rateOverDistance = dustAmount * 1.25f;
            }
        }
    }
}
