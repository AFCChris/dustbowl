# Dustbowl physics reference

Status: revised canonical reference for behaviours to preserve during production development

Source baseline: `src/game.js`, build `v0.18 · Clean Racing Lines`, repository commit `80e7651`

## Critical rule

Dustbowl does not need a physically complete motorcycle. It needs a deterministic, code-controlled arcade bike whose outputs match the approved feel. Unity rigid bodies, `WheelCollider`, joints, marketplace vehicle controllers and ragdolls may be used selectively for queries or presentation, but must not become the unquestioned source of player-bike behaviour.

The commercial-quality ambition changes rendering, art, animation, audio and production architecture. It does not weaken this rule.

## Current architecture

The web controller stores a compact state:

- world position and velocity;
- yaw and orientation quaternion;
- grounded/crashed flags;
- airtime, flip accumulation and recent ground-climb rate;
- surface grip state;
- last good on-track position;
- visual-only suspension/jolt state.

The main update covers the whole elapsed frame and substeps at roughly 20 ms, with at most six substeps. A return-from-background frame is capped at 250 ms to avoid warping. This distinction matters: routine low frame rate must not make the race clock or bike run in slow motion, while a suspended application does not need to simulate the entire absence.

## Target architecture boundary

The Unity implementation should separate:

- authoritative controller state and deterministic rules;
- terrain/course sampling and collision queries;
- input actions;
- visual bike/rider rig and suspension;
- crash presentation;
- camera;
- VFX and surface feedback;
- audio;
- UI and race state.

All presentation systems consume a documented telemetry/state interface. Replacing a placeholder bike, rider skeleton, animation controller, particle effect or sound set must not require rewriting handling. Conversely, production animation and VFX must not write hidden forces into the controller.

## Numeric baseline from `src/game.js`

| Parameter | Current value | Meaning |
| --- | ---: | --- |
| `GRAV` | `22 m/s²` | deliberately strong arcade gravity |
| `MAX_SPEED` | `42 m/s` | engine ceiling before drag/slope effects |
| `RIDE_H` | `0.42 m` | ground-contact offset |
| Ground power | `32 m/s² × speed taper` | forward acceleration |
| Brake | up to `24 m/s²` | forward braking strength |
| Uphill gravity factor | `0.55` | preserves speed on jump faces |
| Steering rate | `2.2 rad/s × authority` | speed-shaped yaw response |
| Air pitch rate | `2.1 rad/s` | generous rescue/trick control |
| Air yaw rate | `1.5 rad/s` | steering-derived whip |
| Air roll rate | `0.8 rad/s` | steering-derived roll |
| Air drag | `exp(-0.06 × dt)` | light velocity decay |
| Ground follow spring | `90` | direct terrain following |
| Vertical damping | `16` | ground-follow damping |
| Crash tumble | `1.7 s` | current automatic respawn delay |

These values are calibration evidence, not engine-independent truths. Preserve resulting behaviour first.

## Terrain contract

### One height authority

In the web build, `terrainH(x, z)` is the single source of truth for both visible terrain and player ground queries. The target engine must preserve the invariant even if representation changes:

- generated render mesh and collision/query surface must derive from the same course/height data;
- authored features must be present in both;
- no independent vertex tweak may create invisible bumps or non-colliding visuals;
- tests must sample render/collision agreement at course features and random points.

The target engine may replace direct height queries with a generated mesh plus ray/shape casts, but the shared data source remains mandatory.

### Track as landform

The track is a graded, banked channel cut into natural terrain, with raised berm sides and a blend back to desert. It is not merely a texture or a corridor bounded by invisible walls. The current `onTrack()` value mainly controls loose-surface drag, making cuts slower rather than forbidden.

### Track profiling

The web build bakes a nearest-segment field on a four-metre grid, then locally refines it to return lateral offset, road height, bank and lap distance. This optimisation is web-specific, but its outputs form a useful target-engine course service:

`sample_track(world_position) → along, lateral, surface_height, bank, distance, section metadata`

The target implementation can use spatial indexing, curve baking or engine-native data structures. It must retain correct closing-segment lap distance and valid queries across the entire play area.

## Grounded motion contract

1. Sample the ground and a stable local surface normal.
2. Determine grounded state with a small contact tolerance.
3. Project the facing direction onto the surface to create forward/right axes.
4. Apply tapered engine force and braking along the surface.
5. Apply only part of gravity along an uphill slope so jump approaches keep energy.
6. Shape steering authority by speed.
7. Dampen lateral velocity progressively, with weaker grip as the slide grows.
8. Apply rolling/aero drag and additional loose-sand drag.
9. Follow the shared ground surface without filtering away short features.
10. Track recent ground height rate for stable takeoff calculation.

Do not add simulated suspension to the controller simply because a target engine offers it. The earlier filtered-chassis implementation swallowed chatter and softened jump faces. Suspension compression is currently presentation-only.

## Takeoff contract

Two release paths are required:

- **Drop-away:** the next ground position falls sufficiently below the bike.
- **Crest release:** the smoothed recent climb rate carries the bike upward faster than gravity and the next surface can follow.

At takeoff, vertical velocity inherits the recent climb rate, capped in the current build at `26 m/s`. A raw normal at a sharp lip is not reliable because its sample spans the discontinuity.

The target engine should initially port this algorithm explicitly. Alternative ray/sweep arrangements may replace it only after producing equal or better A/B results on both sharp lips and rounded natural crests.

## Airborne motion contract

- apply gravity and light drag;
- allow deliberate pitch, yaw/whip and roll influence;
- separate pre-takeoff throttle holding from post-takeoff pitch intent;
- after a short neutral period, softly settle toward a landable yaw-level orientation;
- detect contact with the same terrain authority used for rendering;
- evaluate, do not merely snap, the landing.

## Landing contract

Current code computes:

- `align = bike_up · ground_normal`;
- `impact = max(0, -vertical_velocity)`.

It wipes out below alignment `0.35`, or below `0.62` when impact exceeds `20 m/s`. Otherwise it scrubs horizontal speed between 50% and 100% based on alignment.

The migration must preserve those outer reference boundaries initially and add the four-outcome severity model in `FEEL_SPEC.md`. Landing response should be driven by a single severity/result object so physics, animation, sound, camera and UI cannot disagree.

Suggested result payload:

```text
LandingResult
  tier: CLEAN | SKETCHY | UGLY | WIPEOUT
  severity: 0.0..1.0
  alignment
  impact_speed
  travel_alignment
  speed_retention
  recovery_assist_seconds
```

## Crash and respawn contract

See `CRASH_AND_RESET_SPEC.md`. Player control is disabled during tumble, the race clock continues, and recovery uses a validated recent course position facing forward. No damage state survives the reset.

## AI reference

The current seven-rider field deliberately uses a cheaper model:

- near the player: simplified terrain-following controller;
- beyond 250 m: analytical centreline advancement;
- authored per-section pace;
- occasional recoverable mistakes;
- soft ±5% rubber-banding;
- automatic recovery from sustained excursions;
- soft rider repulsion rather than hard collision blocking.

This behaviour should be preserved conceptually, but AI is not part of the first feel vertical slice. Player-controller parity comes first.

## Determinism and instrumentation

The target prototype should expose a debug telemetry record per physics tick:

- time/tick;
- position, velocity and speed;
- grounded state and ground height;
- surface normal and track lateral/along values;
- input values;
- climb rate and takeoff trigger;
- airtime;
- orientation and landing metrics;
- landing tier and crash/reset timestamps.

Record named test runs with fixed course data and scripted inputs. Exact cross-engine determinism is not required, but repeatability inside the target engine is.

Gamepad is the reference input for feel captures, with raw action values recorded. Keyboard and later touch map to the same semantic actions.

## Platform and performance contract

- Begin with a 60 Hz fixed simulation and measure before changing it.
- Graphics tiers may alter terrain detail, vegetation, shadows, particles, materials and resolution; they must not change handling constants or race timing.
- PC is the first tuning platform, but controller code must avoid assumptions that make Steam Deck or mobile impractical.
- AI, particles and animation use distance/detail levels that do not alter legitimate race progress.
- Suspension and ragdoll presentation can use simplified mobile variants while landing classification and reset timing remain identical.

## Prohibited assumptions

- “Real rigid-body physics will automatically feel better.”
- “Wheel colliders are the correct starting point because the vehicle is a motorcycle.”
- “A terrain normal is always the correct launch vector.”
- “More suspension simulation is more authentic and therefore better.”
- “A fixed timestep permits dropping elapsed time without affecting racing.”
- “A successful landing needs no intermediate severity state.”
- “Production-quality animation is allowed to become the physics model.”
- “A marketplace vehicle system is lower risk than preserving the proven custom controller.”
