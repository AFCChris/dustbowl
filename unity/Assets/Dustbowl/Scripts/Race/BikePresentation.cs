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
        private bool wasGrounded = true;

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
                // Larger, longer-lived puffs need fewer emissions than the old hard quads.
                ParticleSystem.EmissionModule emission = dust.emission;
                emission.rateOverTime = grounded ? Mathf.Clamp(speed * 1.1f, 0f, 34f) : 0f;

                // A landing kicks up one readable roost burst; purely visual.
                if (grounded && !wasGrounded && speed > 4f)
                {
                    dust.Emit(Mathf.Clamp(Mathf.RoundToInt(speed * .9f), 6, 22));
                }
            }

            wasGrounded = grounded;
        }
    }
}
