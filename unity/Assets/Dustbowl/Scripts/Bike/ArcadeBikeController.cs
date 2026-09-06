using Dustbowl.Camera;
using Dustbowl.Course;
using Dustbowl.Input;
using Dustbowl.Telemetry;
using UnityEngine;

namespace Dustbowl.Bike
{
    [DisallowMultipleComponent]
    public sealed class ArcadeBikeController : MonoBehaviour
    {
        [SerializeField] private ArcadeBikeTuning tuning;
        [SerializeField] private DustbowlInputReader inputReader;
        [SerializeField] private DevelopmentTelemetryRecorder telemetry;
        [SerializeField] private CameraModeController cameraModes;
        [SerializeField] private CourseSurface currentSurface;
        [SerializeField] private BikeControllerState state;
        [SerializeField] private TakeoffTrigger lastTakeoffTrigger;
        [SerializeField] private float lastRecoveryDuration;

        private Vector3 tumbleRadiansPerSecond;
        private Vector3 lastSafePosition;
        private Quaternion lastSafeOrientation;
        private float spawnAlong;
        private float spawnLateral;
        private float wipeoutTimer;
        private double elapsedSeconds;
        private double wipeoutCommittedSeconds;
        private bool initialized;
        private int takeoffCount;
        private int landingCount;
        private bool raceControlEnabled = true;
        private bool hasThrottleModeOverride;
        private ThrottleMode throttleModeOverride;

        public ArcadeBikeTuning Tuning => tuning;

        /// <summary>
        /// Active throttle scheme: the front-end selection when one has been made,
        /// otherwise the tuning asset's default.
        /// </summary>
        public ThrottleMode ThrottleMode => hasThrottleModeOverride
            ? throttleModeOverride
            : (tuning == null || tuning.AutoThrottle ? ThrottleMode.Auto : ThrottleMode.Manual);

        public CourseSurface CurrentSurface => currentSurface;
        public BikeControllerState State => state;
        public TakeoffTrigger LastTakeoffTrigger => lastTakeoffTrigger;
        public float LastRecoveryDuration => lastRecoveryDuration;
        public int TakeoffCount => takeoffCount;
        public int LandingCount => landingCount;

        public void Configure(
            ArcadeBikeTuning bikeTuning,
            DustbowlInputReader reader,
            DevelopmentTelemetryRecorder recorder,
            CameraModeController modes)
        {
            tuning = bikeTuning;
            inputReader = reader;
            telemetry = recorder;
            cameraModes = modes;
        }

        public void SetSurfaceAndSpawn(CourseSurface surface, float along)
        {
            SetSurfaceAndSpawn(surface, along, 0f);
        }

        public void SetSurfaceAndSpawn(CourseSurface surface, float along, float lateral)
        {
            currentSurface = surface;
            spawnAlong = along;
            spawnLateral = lateral;
            InitializeAtSpawn();
        }

        public void SetRaceControlEnabled(bool enabled)
        {
            raceControlEnabled = enabled;
            state.controlsEnabled = enabled && state.motionState != BikeMotionState.Wipeout;
        }

        public void SetThrottleMode(ThrottleMode mode)
        {
            hasThrottleModeOverride = true;
            throttleModeOverride = mode;
        }

        public void InitializeAtSpawn()
        {
            if (tuning == null || currentSurface == null || currentSurface.Definition == null)
            {
                initialized = false;
                return;
            }

            if (!currentSurface.Definition.TrySampleLine(spawnAlong, out CourseLinePoint line))
            {
                spawnAlong = currentSurface.Definition.StartAlong;
                currentSurface.Definition.TrySampleLine(spawnAlong, out line);
            }

            Vector3 lineForward = currentSurface.SampleCourse(line.center).frame.forward;
            Vector3 lineRight = new(lineForward.z, 0f, -lineForward.x);
            GroundSample ground = currentSurface.SampleGround(line.center + lineRight * spawnLateral);
            Quaternion orientation = Quaternion.LookRotation(
                ground.course.frame.forward,
                ground.normal);
            state = new BikeControllerState
            {
                position = ground.point + Vector3.up * (tuning.RideHeight + 0.05f),
                velocity = Vector3.zero,
                orientation = orientation,
                motionState = BikeMotionState.Grounded,
                yawRadians = Mathf.Atan2(
                    ground.course.frame.forward.x,
                    ground.course.frame.forward.z),
                recentClimbRate = 0f,
                airborneSeconds = 0f,
                surfaceAmount = ground.trackBlend,
                visualLeanRadians = 0f,
                lastLandingOutcome = Stage3LandingOutcome.ShortContact,
                controlsEnabled = raceControlEnabled
            };
            wipeoutTimer = 0f;
            takeoffCount = 0;
            landingCount = 0;
            lastTakeoffTrigger = TakeoffTrigger.None;
            lastRecoveryDuration = 0f;
            lastSafePosition = state.position;
            lastSafeOrientation = state.orientation;
            initialized = true;
            ApplyTransform();
        }

        public void SimulateTick(PlayerInputSnapshot input, float deltaTime)
        {
            if (!initialized || tuning == null || currentSurface == null || deltaTime <= 0f)
            {
                return;
            }

            elapsedSeconds += deltaTime;
            state.tick++;
            if (!raceControlEnabled && state.motionState != BikeMotionState.Wipeout)
            {
                GroundSample heldGround = currentSurface.SampleGround(state.position);
                state.position.y = heldGround.point.y + tuning.RideHeight + 0.05f;
                state.velocity = Vector3.zero;
                state.controlsEnabled = false;
                ApplyTransform();
                RecordTick(default);
                return;
            }

            if (state.motionState == BikeMotionState.Wipeout)
            {
                TickWipeout(deltaTime);
            }
            else if (state.motionState == BikeMotionState.Airborne)
            {
                TickAirborne(input, deltaTime);
            }
            else
            {
                TickGrounded(input, deltaTime);
            }

            ApplyTransform();
            RecordTick(input);
        }

        public void ForceWipeout()
        {
            if (initialized && state.motionState != BikeMotionState.Wipeout)
            {
                CommitWipeout();
                ApplyTransform();
            }
        }

        public void ManualReset()
        {
            if (initialized)
            {
                RestoreControl(false, "manual-reset");
            }
        }

        private void Update()
        {
            if (!initialized || inputReader == null)
            {
                return;
            }

            PlayerInputSnapshot input = inputReader.Current;
            if (input.resetPressed)
            {
                ManualReset();
            }

            if (input.cameraNextPressed)
            {
                cameraModes?.Next();
            }
            else if (input.cameraPreviousPressed)
            {
                cameraModes?.Previous();
            }

            if (input.devWipeoutPressed)
            {
                ForceWipeout();
            }

            if (input.telemetryTogglePressed && telemetry != null)
            {
                if (telemetry.IsRecording)
                {
                    telemetry.EndCaptureAndSave();
                }
                else
                {
                    telemetry.BeginCapture($"Stage3-{currentSurface.Definition.SegmentId}");
                }
            }
        }

        private void FixedUpdate()
        {
            SimulateTick(inputReader != null ? inputReader.Current : default, Time.fixedDeltaTime);
        }

        private void TickGrounded(PlayerInputSnapshot input, float deltaTime)
        {
            GroundSample ground = currentSurface.SampleGround(state.position);
            float groundHeight = ground.point.y;
            if (state.position.y > groundHeight + tuning.RideHeight + tuning.ContactTolerance)
            {
                BeginTakeoff(TakeoffTrigger.ContactLoss, ground.course);
                TickAirborne(input, deltaTime);
                return;
            }

            state.position.y = Mathf.Max(state.position.y, groundHeight + tuning.RideHeight);
            Vector3 horizontalForward = new(Mathf.Sin(state.yawRadians), 0f, Mathf.Cos(state.yawRadians));
            Vector3 slopeForward = Vector3.ProjectOnPlane(horizontalForward, ground.normal);
            if (slopeForward.sqrMagnitude < 0.00001f)
            {
                slopeForward = horizontalForward;
            }

            slopeForward.Normalize();
            Vector3 right = Vector3.Cross(ground.normal, slopeForward).normalized;
            float forwardSpeed = Vector3.Dot(state.velocity, slopeForward);
            float speed = state.velocity.magnitude;
            float brake = Mathf.Clamp01(input.brake);
            float throttle = ArcadeBikeRules.ResolveThrottle(ThrottleMode, input.throttle, brake);

            if (throttle > 0f)
            {
                state.velocity += slopeForward
                    * (ArcadeBikeRules.EnginePower(forwardSpeed, tuning) * throttle * deltaTime);
            }

            if (brake > 0f)
            {
                float braking = forwardSpeed > 0f ? -tuning.ForwardBrake : tuning.ReverseDrive;
                state.velocity += slopeForward
                    * (braking * deltaTime * Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 3f) * brake);
            }

            Vector3 gravity = Vector3.down * tuning.Gravity;
            Vector3 slopeGravity = Vector3.ProjectOnPlane(gravity, ground.normal);
            state.velocity += slopeGravity * (tuning.SlopeGravityFactor * deltaTime);

            float steer = Mathf.Clamp(input.steer, -1f, 1f);
            float steerAuthority = ArcadeBikeRules.SteeringAuthority(speed, tuning);
            float direction = Mathf.Sign(Mathf.Abs(forwardSpeed) > 0.0001f ? forwardSpeed : 1f);
            state.yawRadians += steer * tuning.SteerRate * deltaTime * steerAuthority * direction;

            float lateralSpeed = Vector3.Dot(state.velocity, right);
            float grip = ArcadeBikeRules.ExponentialResponse(
                ArcadeBikeRules.GripRate(lateralSpeed, tuning),
                deltaTime);
            state.velocity += right * (-lateralSpeed * grip);

            state.surfaceAmount = ground.trackBlend;
            float drag = ArcadeBikeRules.DragCoefficient(throttle > 0f, state.surfaceAmount, tuning);
            state.velocity *= Mathf.Exp(-drag * deltaTime);
            state.velocity += state.velocity * (-tuning.SpeedShapedDrag * speed * deltaTime);

            float targetHeight = groundHeight + tuning.RideHeight;
            state.velocity.y += (targetHeight - state.position.y)
                * tuning.GroundFollowSpring
                * deltaTime;
            state.velocity.y -= state.velocity.y * Mathf.Clamp01(tuning.GroundVerticalDamping * deltaTime);
            state.position += state.velocity * deltaTime;

            float nextGroundHeight = currentSurface.SampleHeight(state.position);
            float groundRate = Mathf.Clamp(
                (nextGroundHeight - groundHeight) / deltaTime,
                -tuning.ClimbRateLimit,
                tuning.ClimbRateLimit);
            state.recentClimbRate = ArcadeBikeRules.UpdateClimbRate(
                state.recentClimbRate,
                groundHeight,
                nextGroundHeight,
                deltaTime,
                tuning);
            TakeoffTrigger release = ArcadeBikeRules.EvaluateTakeoff(
                state.position.y,
                nextGroundHeight,
                state.recentClimbRate,
                groundRate,
                deltaTime,
                tuning);

            if (release != TakeoffTrigger.None)
            {
                CourseSample nextCourse = currentSurface.SampleCourse(state.position);
                BeginTakeoff(release, nextCourse);
            }
            else
            {
                state.position.y = nextGroundHeight + tuning.RideHeight;
                if (state.velocity.y < 0f)
                {
                    state.velocity.y = 0f;
                }
            }

            state.visualLeanRadians = Mathf.Lerp(
                state.visualLeanRadians,
                -steer * Mathf.Clamp01(speed / tuning.VisualLeanSpeed) * tuning.VisualLeanLimit,
                ArcadeBikeRules.ExponentialResponse(tuning.VisualLeanRate, deltaTime));
            horizontalForward = new Vector3(
                Mathf.Sin(state.yawRadians),
                0f,
                Mathf.Cos(state.yawRadians));
            slopeForward = Vector3.ProjectOnPlane(horizontalForward, ground.normal).normalized;
            Quaternion groundOrientation = Quaternion.LookRotation(slopeForward, ground.normal)
                * Quaternion.AngleAxis(state.visualLeanRadians * Mathf.Rad2Deg, Vector3.forward);
            state.orientation = Quaternion.Slerp(
                state.orientation,
                groundOrientation,
                ArcadeBikeRules.ExponentialResponse(tuning.GroundOrientationRate, deltaTime));

            GroundSample movedGround = currentSurface.SampleGround(state.position);
            if (state.motionState == BikeMotionState.Grounded
                && movedGround.surfaceType == SurfaceType.PackedTrack
                && movedGround.slopeDegrees <= tuning.SafeResetSlope
                && !movedGround.course.hasNearbyFeature)
            {
                lastSafePosition = state.position;
                lastSafeOrientation = state.orientation;
            }
        }

        private void TickAirborne(PlayerInputSnapshot input, float deltaTime)
        {
            state.airborneSeconds += deltaTime;
            state.velocity.y -= tuning.Gravity * deltaTime;
            state.velocity *= Mathf.Exp(-tuning.AirDrag * deltaTime);
            state.position += state.velocity * deltaTime;

            float pitch = Mathf.Clamp(input.airPitch, -1f, 1f);
            float whip = Mathf.Abs(input.airWhip) > 0.001f
                ? Mathf.Clamp(input.airWhip, -1f, 1f)
                : Mathf.Clamp(input.steer, -1f, 1f);
            float pitchRadians = pitch * tuning.AirPitchRate * deltaTime;
            float yawRadians = whip * tuning.AirYawRate * deltaTime;
            float rollRadians = -whip * tuning.AirRollRate * deltaTime;
            state.orientation *= Quaternion.Euler(
                pitchRadians * Mathf.Rad2Deg,
                yawRadians * Mathf.Rad2Deg,
                rollRadians * Mathf.Rad2Deg);
            state.yawRadians += yawRadians;

            if (Mathf.Abs(pitch) < 0.001f
                && Mathf.Abs(whip) < 0.001f
                && state.airborneSeconds > tuning.NeutralSettleDelay)
            {
                Quaternion level = Quaternion.Euler(0f, state.yawRadians * Mathf.Rad2Deg, 0f);
                state.orientation = Quaternion.Slerp(
                    state.orientation,
                    level,
                    ArcadeBikeRules.ExponentialResponse(tuning.NeutralSettleRate, deltaTime));
            }

            GroundSample ground = currentSurface.SampleGround(state.position);
            if (state.position.y <= ground.point.y + tuning.RideHeight)
            {
                state.position.y = ground.point.y + tuning.RideHeight;
                HandleLanding(ground);
            }

            telemetry?.Record(new AirborneState
            {
                tick = state.tick,
                elapsedSeconds = elapsedSeconds,
                airborneDuration = state.airborneSeconds,
                velocity = state.velocity,
                orientation = state.orientation
            });
        }

        private void BeginTakeoff(TakeoffTrigger trigger, CourseSample course)
        {
            float approachSpeed = new Vector2(state.velocity.x, state.velocity.z).magnitude;
            state.velocity.y = Mathf.Max(
                state.velocity.y,
                Mathf.Min(state.recentClimbRate, tuning.MaximumLaunchVerticalSpeed));
            state.motionState = BikeMotionState.Airborne;
            state.airborneSeconds = 0f;
            state.controlsEnabled = raceControlEnabled;
            lastTakeoffTrigger = trigger;
            takeoffCount++;
            telemetry?.Record(new TakeoffEvent
            {
                tick = state.tick,
                elapsedSeconds = elapsedSeconds,
                approachSpeed = approachSpeed,
                takeoffSpeed = state.velocity.magnitude,
                recentClimbRate = state.recentClimbRate,
                launchVerticalVelocity = state.velocity.y,
                orientation = state.orientation,
                position = state.position,
                trackAlong = course.along,
                trackLateral = course.signedLateralDistance,
                trigger = trigger
            });
        }

        private void HandleLanding(GroundSample ground)
        {
            float airborne = state.airborneSeconds;
            float alignment = Vector3.Dot(state.orientation * Vector3.up, ground.normal);
            float impact = Mathf.Max(0f, -state.velocity.y);
            Stage3LandingResult result = ArcadeBikeRules.EvaluateLanding(
                airborne,
                alignment,
                impact,
                tuning);
            Vector3 bikeForward = Vector3.ProjectOnPlane(state.orientation * Vector3.forward, Vector3.up);
            Vector3 courseForward = Vector3.ProjectOnPlane(ground.course.frame.forward, Vector3.up);
            float travelAlignment = bikeForward.sqrMagnitude > 0.0001f && courseForward.sqrMagnitude > 0.0001f
                ? Vector3.Dot(bikeForward.normalized, courseForward.normalized)
                : 0f;
            Vector3 euler = state.orientation.eulerAngles;
            telemetry?.Record(new LandingEvent
            {
                tick = state.tick,
                elapsedSeconds = elapsedSeconds,
                airborneDuration = airborne,
                touchdownOrientation = state.orientation,
                touchdownPitchDegrees = NormalizeEuler(euler.x),
                touchdownRollDegrees = NormalizeEuler(euler.z),
                touchdownYawDegrees = NormalizeEuler(euler.y),
                surfaceAlignment = alignment,
                alignmentErrorDegrees = Mathf.Acos(Mathf.Clamp(alignment, -1f, 1f)) * Mathf.Rad2Deg,
                impactDownwardSpeed = impact,
                classification = result.outcome == Stage3LandingOutcome.Wipeout
                    ? LandingTier.Wipeout
                    : LandingTier.Unclassified,
                stage3Outcome = result.outcome,
                travelAlignment = travelAlignment,
                speedRetention = result.speedRetention,
                touchdownLocation = state.position
            });
            landingCount++;
            state.lastLandingOutcome = result.outcome;
            state.airborneSeconds = 0f;
            state.recentClimbRate = 0f;

            if (result.outcome == Stage3LandingOutcome.Wipeout)
            {
                CommitWipeout();
                return;
            }

            Vector3 horizontal = new(state.velocity.x, 0f, state.velocity.z);
            horizontal *= result.speedRetention;
            state.velocity = new Vector3(
                horizontal.x,
                Mathf.Max(-state.velocity.y * tuning.LandingVerticalRebound, 0f),
                horizontal.z);
            Vector3 facing = Vector3.ProjectOnPlane(state.orientation * Vector3.forward, Vector3.up);
            if (Mathf.Abs(facing.x) + Mathf.Abs(facing.z) > 0.05f)
            {
                state.yawRadians = Mathf.Atan2(facing.x, facing.z);
            }

            state.motionState = BikeMotionState.Grounded;
            state.controlsEnabled = raceControlEnabled;
        }

        private void CommitWipeout()
        {
            state.motionState = BikeMotionState.Wipeout;
            state.controlsEnabled = false;
            wipeoutTimer = 0f;
            wipeoutCommittedSeconds = elapsedSeconds;
            state.velocity *= 0.5f;
            state.velocity.y = 4f;
            float direction = state.tick % 2 == 0 ? 1f : -1f;
            tumbleRadiansPerSecond = new Vector3(4.5f * direction, 3f, -4.5f * direction);
            telemetry?.Record(new WipeoutEvent
            {
                tick = state.tick,
                elapsedSeconds = elapsedSeconds,
                position = state.position,
                orientation = state.orientation,
                velocity = state.velocity
            });
        }

        private void TickWipeout(float deltaTime)
        {
            wipeoutTimer += deltaTime;
            state.velocity.y -= tuning.Gravity * deltaTime;
            state.position += state.velocity * deltaTime;
            float groundHeight = currentSurface.SampleHeight(state.position);
            if (state.position.y < groundHeight + 0.5f)
            {
                state.position.y = groundHeight + 0.5f;
                state.velocity *= tuning.WipeoutGroundDamping;
                state.velocity.y = Mathf.Abs(state.velocity.y) * tuning.WipeoutBounce;
                tumbleRadiansPerSecond *= tuning.WipeoutTumbleDamping;
            }

            state.orientation *= Quaternion.Euler(tumbleRadiansPerSecond * (Mathf.Rad2Deg * deltaTime));
            if (wipeoutTimer >= tuning.WipeoutDuration)
            {
                RestoreControl(true, "automatic-wipeout-reset");
            }
        }

        private void RestoreControl(bool followsWipeout, string reason)
        {
            GroundSample ground = currentSurface.SampleGround(lastSafePosition);
            state.position = new Vector3(
                lastSafePosition.x,
                ground.point.y + tuning.RideHeight + 0.05f,
                lastSafePosition.z);
            state.velocity = Vector3.zero;
            state.orientation = lastSafeOrientation;
            Vector3 forward = Vector3.ProjectOnPlane(state.orientation * Vector3.forward, Vector3.up);
            state.yawRadians = Mathf.Atan2(forward.x, forward.z);
            state.motionState = BikeMotionState.Grounded;
            state.controlsEnabled = raceControlEnabled;
            state.airborneSeconds = 0f;
            state.recentClimbRate = 0f;
            state.visualLeanRadians = 0f;
            wipeoutTimer = 0f;
            lastRecoveryDuration = followsWipeout
                ? (float)(elapsedSeconds - wipeoutCommittedSeconds)
                : 0f;
            telemetry?.Record(new ResetEvent
            {
                tick = state.tick,
                elapsedSeconds = elapsedSeconds,
                restoredPosition = state.position,
                restoredOrientation = state.orientation,
                followsWipeout = followsWipeout,
                reason = reason
            });
            if (followsWipeout)
            {
                telemetry?.Record(new RecoveryEvent
                {
                    wipeoutCommittedSeconds = wipeoutCommittedSeconds,
                    usefulControlRestoredSeconds = elapsedSeconds,
                    recoveryDuration = lastRecoveryDuration,
                    restoredPosition = state.position
                });
            }

            ApplyTransform();
        }

        private void RecordTick(PlayerInputSnapshot input)
        {
            GroundSample ground = currentSurface.SampleGround(state.position);
            telemetry?.Record(new TelemetrySample
            {
                tick = state.tick,
                elapsedSeconds = elapsedSeconds,
                input = input,
                bike = state,
                ground = new GroundContactSample
                {
                    hasContact = state.motionState == BikeMotionState.Grounded,
                    point = ground.point,
                    normal = ground.normal,
                    surfaceAlignment = Vector3.Dot(state.orientation * Vector3.up, ground.normal),
                    recentClimbRate = state.recentClimbRate,
                    trackDistance = ground.course.distanceFromCenter,
                    trackOffset = ground.course.signedLateralDistance
                }
            });
        }

        private void ApplyTransform()
        {
            transform.SetPositionAndRotation(state.position, state.orientation);
        }

        private static float NormalizeEuler(float degrees)
        {
            return degrees > 180f ? degrees - 360f : degrees;
        }
    }
}
