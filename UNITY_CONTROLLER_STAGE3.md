# Dustbowl Unity controller — Stage 3

Status: implemented for user A/B review; not yet feel-approved

Engine: Unity `6000.3.23f1`, C#, URP, 60 Hz fixed simulation

Web reference: tag `web-v0.18-behavioural-baseline`, commit
`80e76510f9a8fd34cb213e36fd54cba227a31bce`

## Scope and gate

Stage 3 ports the first playable arcade-bike slice into the existing BehaviourLab.
It deliberately uses placeholder geometry and a code-controlled transform. There
is no `WheelCollider`, vehicle package, tyre/drivetrain simulation, bike
`Rigidbody`, multi-body assembly, AI, full-course port, polished art or final
ragdoll. Stage 4 owns the four-tier Clean/Sketchy/Ugly/Wipeout landing model and
any further controller refinement.

Passing automated checks means that deterministic rules and scene wiring are
intact. It does not establish feel parity. Advancement still requires the user to
play the web and Unity references A/B and approve the Unity slice.

## Controller architecture

- `ArcadeBikeController` owns the authoritative position, velocity, orientation,
  recent climb rate and explicit `Grounded`, `Airborne` and `Wipeout` lifecycle.
- `ArcadeBikeRules` contains pure calculations for power taper, steering
  authority, grip, drag, climb filtering, takeoff and the temporary landing
  boundary. EditMode tests exercise these rules without a scene.
- `ArcadeBikeTuning` is a ScriptableObject. The checked-in
  `WebReferenceArcadeBikeTuning.asset` exposes the reference constants in the
  Inspector without embedding presentation dependencies in handling code.
- `CourseSurface` remains the only sampled ground authority. Both Stage 3 strips
  derive their render mesh, collision mesh, height, normal and track profile from
  the same course data.
- `ArcadeBikeCamera`, `BehaviourLabScenarioController`, the development HUD and
  telemetry consume controller state but do not feed camera or placeholder visual
  state back into handling.

## Web behaviour mapping

The reference source locations below are in `src/game.js` at baseline commit
`80e7651`; Unity locations refer to files under
`unity/Assets/Dustbowl/Scripts/`.

| Behaviour | Web reference | Unity Stage 3 |
| --- | --- | --- |
| Power/top speed | `202–204`, `1939–1947`: 32 m/s² power tapered toward 42 m/s | Same constants and taper in `Bike/ArcadeBikeRules.cs`; the ceiling is emergent rather than a hard clamp. |
| Braking/reverse | `1939–1947`: 24 m/s² braking; 12 m/s² reverse below 3 m/s | Same values and condition in `Bike/ArcadeBikeController.cs`. |
| Steering | `1955–1964`: 2.2 rad/s, authority grows to full at 6 m/s then loses at most 55% across `42 × 1.5` m/s | Same speed-shaped function in `Bike/ArcadeBikeRules.cs`. |
| Lateral grip | `1959–1964`: response falls from 14 to 7 as lateral speed approaches 12 m/s | Same response and projected surface basis. |
| Slope assistance | `1948–1953`: 55% of gravity projected onto the ground plane | Same 0.55 factor. |
| Loose sand | `1966–1971`: throttle/coast drag 0.1/0.85, plus up to 0.62 loose drag and `0.004 × speed` | Same formula driven by continuous `trackBlend`, not a binary off-track trigger. |
| Ground following | `1916–1929`, `1973–1977`: 90 spring, 16 vertical damping, direct ride-height contact | Same provisional spring/damping and final direct snap to sampled ground plus 0.42 m ride height. |
| Climb-rate takeoff | `1979–1998`: ground rate clamped to ±40 m/s, response 11, lip gap 0.25 m, rounded-crest margin 0.6 m, launch capped at 26 m/s | Same filter, release tests and vertical-speed cap. |
| Air control | `2025–2050`: gravity 22, exponential drag 0.06, pitch/yaw/roll rates 2.1/1.5/0.8 rad/s | Same values. `AirWhip` uses right-stick X; steer is the keyboard-compatible fallback. |
| Neutral settling | `2041–2050`: begins after 0.2 s at response 1.5 | Same delay/rate, settling toward velocity-aligned forward and world-up-supported orientation. |
| Landing | `2062–2105`: contacts over 0.3 s are evaluated; wipeout below 0.35 alignment, or below 0.62 with impact over 20 m/s; accepted retention rises 0.5→1 by 0.85 alignment; vertical rebound 0.12 | Same temporary binary boundary and speed treatment. Short contacts remain unevaluated. Four named landing tiers are intentionally not invented in Stage 3. |
| Wipeout/reset | `1889–1906`, `2108–2120`: short tumble and reset after 1.7 s | Same 1.7 s authority interval; at 60 Hz observed restoration is 1.717 s. Presentation is a deterministic placeholder and resets to the last safe sampled point. |
| Cameras | `2200–2249`: chase, close and overhead with speed/air shaping | All three are playable first-class modes in `Camera/ArcadeBikeCamera.cs`; Stage 3 uses temporary custom transforms/FOV rather than final camera polish. |

## BehaviourLab reference strips

The scene contains two selectable, isolated development cases:

1. **D — Authored tabletop** uses the Stage 2 Dustbowl Flats reference segment
   and its authored jump feature.
2. **C — Natural rounded crest** is a straight 160 m strip with a smooth cosine
   landform: 60 m flat approach, 60 m rise-and-fall crest, 4.2 m height and 40 m
   runout. Its `CourseDefinition.Features` list is empty. It has no jump flag,
   volume, impulse, launch pad or scenario-triggered takeoff. Airborne transition
   comes only from the shared recent-climb/contact release algorithm.

The automated 60 Hz verification observed:

| Strip | Trigger reported | Takeoff speed | Recent climb at reported takeoff | Meaningful airtime | Temporary result |
| --- | --- | ---: | ---: | ---: | --- |
| Authored tabletop | Rounded crest | 29.35 m/s | -0.08 m/s | 0.40 s | Accepted |
| Natural rounded crest | Rounded crest | 30.73 m/s | 1.16 m/s | 0.45 s | Accepted |

These figures establish deterministic Unity reference runs, not a claim that the
two strips are equivalent to a specific measured web run. The repository's Stage
0 web telemetry package defines fields but contains no checked-in captured run
with which to calculate per-scenario numeric deltas.

## Controls

Auto-throttle is enabled in the reference tuning asset, matching the default web
identity. Manual throttle input is still mapped.

| Action | Gamepad | Keyboard |
| --- | --- | --- |
| Throttle | Right trigger | `W` / Up Arrow |
| Brake/reverse | Left trigger | `S` / Down Arrow |
| Ground steer | Left stick X | `A` / `D` |
| Air pitch | Right stick Y | `F` / `R` |
| Air whip/yaw | Right stick X | `Q` / `E` |
| Reset | North face button | Backspace |
| Next/previous camera | Right/left shoulder | `C` / `V` |
| Switch tabletop/crest | Select | Tab |
| Start/stop telemetry capture | Left-stick press | F9 |
| Force development wipeout | Right-stick press | `K` |

The development HUD identifies the active strip, motion state, speed, recent
climb rate, airtime, surface, last landing result, camera and telemetry state.

## Telemetry

Telemetry is available only in the Editor or a Development Build. Starting a
scenario begins a named in-memory capture. F9/Gamepad left-stick press ends and saves the
current capture, or starts a new one when idle. JSON Lines files are written below
`Application.persistentDataPath/DustbowlTelemetry`; the HUD shows the most recent
path.

Records include fixed-tick input/state samples plus takeoff, airborne, landing,
wipeout, reset and recovery events. Capturable comparison fields include approach
and takeoff speed, recent climb rate, launch vertical velocity, airtime, pitch at
touchdown, alignment error, impact/downward speed, landing outcome, retained
speed and wipeout-to-restored-control time.

## Verification and reproducibility

From the Unity project, the editor menu commands are:

- `Dustbowl > Stage 3 > Configure arcade controller`
- `Dustbowl > Stage 3 > Verify arcade controller`
- `Dustbowl > Stage 3 > Build Windows development player`

The verifier checks Stage 1 and Stage 2 invariants first, then checks the
code-controlled player, absence of vehicle rigid bodies, shared crest
render/collision mesh, empty crest feature list, meaningful tabletop and crest
flights, natural recent-climb release, 1.7-second recovery and the full three-mode
camera cycle.

EditMode tests protect deterministic constants, equations, course sampling,
render/collision agreement, takeoff/landing rules, recovery timing and semantic
input bindings. The PlayMode test loads the real BehaviourLab scene and verifies
movement, natural-crest flight/landing, camera cycling and wipeout recovery under
Unity's actual fixed-update loop.

Verification on 2026-09-04 with Unity `6000.3.23f1`:

- Stage 1, Stage 2 and Stage 3 scripted editor verifiers: passed;
- EditMode: 23/23 passed;
- PlayMode: 1/1 passed in 5.93 s;
- Windows IL2CPP Development Build: could not be produced because the host lacks
  Visual Studio C++ build tools and Windows SDK 10.0.19041 or newer. Unity's
  Windows Build Support (IL2CPP) module is installed, but those external native
  compiler prerequisites are also required. No project setting was changed to
  work around the missing toolchain.

## Numeric differences and known limitations

- Fixed simulation is exactly 1/60 s. The web loop applies its full-frame
  controller logic inside capped substeps, so unusual low-frame-rate trajectories
  are not expected to be numerically identical. Normal 60 fps comparison is the
  Stage 3 target.
- Recovery is configured as 1.700 s and observed after 103 fixed ticks (1.717 s),
  a +0.017 s scheduling quantisation versus the web threshold.
- Terrain-follow orientation uses an explicit quaternion response in Unity. Small
  Euler/order differences versus Three.js can affect alignment at edge cases.
- The Unity reference strips do not reproduce an entire web course, so there is no
  like-for-like lap, race, AI or broad terrain comparison in this stage.
- The temporary landing result is only `Accepted` or `Wipeout` (with a
  `ShortContact` diagnostic). Sketchy and Ugly are deferred to Stage 4 as required.
- The wipeout tumble is deterministic placeholder motion, not the web's random
  tumble presentation and not a final ragdoll.
- Camera composition is playable but temporary. It omits final collision,
  occlusion, shake, crash jolt, polish and settings persistence.
- Reset uses the most recent safe point under 16° slope. Production checkpoint
  lifecycle, lap integration and invalid-location recovery are later work.
- There is no automated claim for maximum practical pitch correction, subjective
  steering weight, jump satisfaction, camera comfort or landing generosity.
  Those require human A/B play judgement.

## User review run

Open `unity/Assets/Dustbowl/Scenes/Dustbowl_BehaviourLab.unity` or launch the
Windows development build. Compare neutral and corrective passes on both strips;
try the loose-sand edges; cycle all cameras, especially Overhead; force or provoke
a wipeout; and save telemetry for representative runs. Judge acceleration,
high-speed steering, crest release, jump arc, air authority, neutral settle,
accepted/wipeout boundary, restoration speed and camera readability against the
web reference. Do not approve Stage 4 from automated results alone.
