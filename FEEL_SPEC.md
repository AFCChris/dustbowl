# Dustbowl feel specification

Status: revised canonical qualitative and presentation specification

Companion documents: `GAME_VISION.md`, `PHYSICS_REFERENCE.md`, `CRASH_AND_RESET_SPEC.md`

## Purpose

This document translates the game vision into behaviours a player can feel and a developer can test. Values labelled **baseline** come from the current web implementation. Values labelled **target** are starting acceptance bands for migration and must be tuned by A/B play, not treated as immutable simulation constants.

## Core feel statement

The bike is light, springy, fast and eager. It settles enough to be readable, but never feels glued to the ground. Steering is immediate without becoming twitchy at speed. Air control is generous. Landings favour continuation. Crashes are rare enough to make experimentation attractive and theatrical enough to make failure enjoyable. Production presentation amplifies every one of those qualities without making the controls heavier or less legible.

Gamepad is the primary tuning reference. Keyboard must achieve equivalent control intent. Touch is added after the established controller is approved rather than becoming the compromise around which the PC controller is designed.

## Ground handling

### Acceleration and speed

- **Baseline:** engine ceiling `42 m/s` (about 94 mph), with actual speed shaped by drag, slope and surface.
- **Baseline:** ground power starts at `32 m/s²` and tapers toward the ceiling.
- **Target:** the bike should feel meaningfully underway within one second and exciting within three seconds.
- **Target:** climbing a jump face should not drain so much speed that the jump stops working.
- Releasing throttle can slow the bike, but coast drag must not feel like an invisible brake.
- Auto-throttle remains a first-class accessibility option, not a temporary mobile workaround.

### Steering

- Low speed: enough authority to recover or line up, without pivoting unnaturally on the spot.
- Medium speed: quick, flowing direction changes and readable lean.
- High speed: authority tapers enough to avoid twitchiness, but the bike must still answer the player.
- Loose ground may widen lines and scrub speed; it should not make the controls feel disconnected.
- Visual front-wheel steer, rider lean and chassis lean may exaggerate the control response without changing the physical path.

### Grip and sliding

- The default state is controllable grip, not constant drift.
- Lateral speed should decay progressively. A small slide should become a visible moment the player can catch.
- Sliding should create dust and a readable bike/rider pose before it threatens failure.
- Off-track sand should cost speed and line quality. It should discourage free corner cutting without acting as a hard boundary.

### Springiness

- The bike follows terrain closely enough that small features are felt and jumps retain their faces.
- Visual suspension and rider motion sell compression and chatter but do not automatically feed back into the handling model.
- The bike should release from rounded rises as well as explicit jump lips. Open terrain must not feel dead or magnetised.

## Jump feel

### Takeoff

- Takeoff is earned from the shape of the land and carried approach speed.
- Both sudden drop-away lips and rounded crests can release the bike.
- Launch direction must use the recent climb of the ridden surface or an equivalent stable signal. A single terrain normal sampled across a lip is not authoritative.
- Bigger air should come from a readable combination of speed, face and crest shape. Simply making a flat deck longer must not be assumed to make a better jump.

### Scale

- Ordinary course features should regularly create short, useful air.
- Signature jumps should create enough hang time for the player to recognise and correct attitude.
- Large jumps should fill the frame and feel celebratory through camera, audio, dust and animation, without delaying the return to racing.

### Mid-air control

- Pitch control is deliberately generous: the player should be able to rescue a plausible nose-high or nose-low takeoff.
- Steering provides limited yaw/whip and roll influence in air.
- Neutral input gently settles the bike toward a landable attitude after a short delay.
- Holding ground throttle before takeoff must not secretly command nose-down pitch. Keyboard air commands should require a fresh post-takeoff press; analogue/self-centring input can remain direct.
- Air control should help recovery, but it must not make full rotations trivial or remove the satisfaction of preparing a jump correctly.

## Landing feel

Landing evaluation uses at least these signals:

- bike-up alignment with landing surface;
- vertical impact speed;
- forward alignment with travel/track direction;
- residual roll, pitch and yaw rate;
- contact context, including casing an edge or landing on severe cross-slope.

The current web build uses only surface alignment and vertical impact for failure, then applies a speed scrub. The target hierarchy adds intermediate presentation and handling states.

| Outcome | Player experience | Provisional entry condition | Momentum result |
| --- | --- | --- | --- |
| Clean | planted, satisfying compression, immediate drive | strong surface alignment, moderate impact, low sideways error | retain about 90–100% horizontal speed |
| Sketchy | wobble or short compression, clearly saveable | moderate alignment/error or firm impact | retain about 72–92% |
| Ugly | dramatic bounce, fishtail or near-highside, still controllable | poor but survivable alignment, hard impact or combined errors below wipeout | retain about 45–75%; brief stability assist |
| Wipeout | obvious tumble and dust, then fast reset | severe misalignment, extreme impact, inverted/side contact or compounded ugly conditions | momentum lost; clock continues |

Initial threshold mapping from the web build:

- Current wipeout: alignment dot below `0.35`, or below `0.62` with downward impact above `20 m/s`.
- Current successful-landing scrub: scales from 50% at alignment `0.35` to 100% near `0.85`.
- Migration starting point: retain those outer wipeout boundaries until A/B tests justify a change; split the current successful range into Clean, Sketchy and Ugly.

No single threshold should cause surprising failure at its boundary. Use short hysteresis, blended severity or combined-error scoring where practical.

## Mistake escalation

The preferred sequence is:

`line error → visible instability → player correction window → momentum cost → ugly save → wipeout only if severity continues`

Examples of drama before failure:

- a landing slightly sideways produces a fishtail before it produces a crash;
- a nose-heavy landing compresses and bounces, giving the player a brief recovery window;
- excess corner speed produces widening line, dust and speed scrub before a fall;
- casing a tabletop produces a heavy thud and ugly landing, not an automatic wipeout every time.

## Camera and presentation

- Chase camera communicates speed, lean, landing and nearby riders.
- Close chase gives intimacy without making terrain unreadable.
- Overhead is a defining Dustbowl view and must survive migration.
- Field of view grows modestly with speed and big air.
- Camera collision prevents terrain clipping.
- Camera jolt is short and visual only. It must not obscure the recovery moment.
- The bike and rider animation can exaggerate posture, compression and counter-lean as long as physical state is not silently changed.
- Production cameras should add terrain anticipation, landing readability, opponent awareness and controlled spectacle without stealing input authority.
- Replays, starts, finishes and event presentation may use more cinematic cameras, but racing views prioritise control.

## Audiovisual feel contract

The target engine is expected to look and sound dramatically better than the web build. Presentation is judged by whether it strengthens the controller:

- suspension travel, tyre contact and rider posture make terrain response visible;
- dirt, decals and particles distinguish packed line, loose sand, braking, sliding and landing;
- lighting and atmosphere preserve track readability rather than hiding it in cinematic contrast;
- speed is communicated through camera motion, FOV, nearby ground flow, dust, wind and engine load;
- each landing tier has a distinct pose, sound, dust response and momentum read;
- opponents are readable by silhouette, colour, line choice and animation;
- UI and audio communicate race state without covering terrain or overwhelming the player.

Prototype meshes, flat materials and diagnostic UI are acceptable only in the behavioural stage. They do not define the intended style.

## Audio and haptics direction

- Engine pitch and load communicate speed and throttle.
- Wind reinforces high speed and air.
- Landing sounds distinguish Clean, Sketchy, Ugly and Wipeout.
- Future controller haptics should reinforce contact severity, not vibrate constantly.
- Audio must stop reliably when play stops or the application loses lifecycle ownership.
- The production mix should include layered engine load/RPM, drivetrain character, tyre and surface noise, wind, suspension/impact, dust/roost, opponent presence, ambience, UI and music.
- Music should carry confident arcade-racing personality while leaving room for engine and terrain feedback.

## Acceptance method

Feel cannot be approved by code review alone. Every material controller change requires:

1. the same representative line ridden in the current web build and target build;
2. video or telemetry comparison for approach speed, takeoff, airtime, landing outcome and time back to full speed;
3. at least one no-input landing and one deliberate mid-air correction;
4. user judgement recorded as better, equivalent or worse, with a short reason;
5. no replacement of an accepted arcade behaviour solely because the alternative is more realistic.

Once behavioural parity is approved, separate visual-slice acceptance asks whether a representative capture looks and sounds like a cohesive contemporary indie release. Behavioural approval does not approve placeholder art, and visual approval must not silently alter the controller.

Target 60 fps where practical on PC and Steam Deck. Graphics quality may scale; physics rules, control response and race timing must not.

The web build is a behavioural reference, not a demand for frame-perfect numeric equivalence.
