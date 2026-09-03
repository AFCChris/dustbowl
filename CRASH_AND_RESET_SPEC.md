# Dustbowl crash and reset specification

Status: revised behaviour and production-presentation specification

## Goal

A crash should cause a polished burst of comedy and drama, impose a small racing cost, and return the player to meaningful control almost immediately. It must never feel like a punishment screen.

## Landing-to-crash hierarchy

Crashing is the end of a progression, not a binary response to any imperfect contact.

| Tier | Physical response | Presentation | Control |
| --- | --- | --- | --- |
| Clean | composed compression, minimal scrub | crisp thud, small dust burst | uninterrupted |
| Sketchy | wobble/compression, mild scrub | stronger sound, rider reacts | retained |
| Ugly | bounce/fishtail, major scrub, short stabilisation | heavy thud, dust, brief camera emphasis | retained with a short recovery window |
| Wipeout | rider/bike tumble | `WIPEOUT`, dust and short camera jolt | temporarily disabled, then automatic reset |

An Ugly landing is a success with consequences. It must not be cosmetically identical to Clean, and it must not secretly remove control for so long that it is effectively a crash.

## Wipeout triggers

A wipeout may result from:

- severe landing-surface misalignment;
- high-impact landing combined with poor alignment;
- inverted or side-first contact;
- leaving the valid world or falling below a safety floor;
- future obstacle contact at clearly excessive severity;
- a failed recovery after an Ugly state continues to worsen.

Small steering errors, ordinary off-track travel, modest casing and low-speed tip angles should prefer Sketchy or Ugly outcomes.

## Wipeout sequence

Target duration is measured from wipeout recognition to useful player control, not merely to visual respawn.

| Phase | Target elapsed time | Requirement |
| --- | ---: | --- |
| Recognition | 0–0.10 s | immediate sound, dust/callout and visible loss of composure |
| Tumble | about 0.10–1.10/1.50 s | short, varied, readable and non-gory |
| Reposition/fade if needed | no more than about 0.25 s | hide teleport discontinuity without a loading screen |
| Control restored | **1.25–2.00 s total** | bike upright, facing course direction, player input active |

The current web reference tumbles for 1.7 seconds and then respawns. That is inside the approved total range, but the target build must verify the time to actual control.

## Tumble presentation

- Randomness may vary rotation and dust, but must not change the recovery time unpredictably.
- During behavioural prototyping, a simple animation or short-lived presentation rigid body is acceptable.
- Production should use an authored-to-ragdoll or hybrid presentation which gives the rider and bike weight, personality and variation without allowing simulation chaos to control respawn.
- The visible tumble must not be allowed to drag the authoritative player state far enough to make recovery location unsafe.
- Camera jolt is small and decays in a few frames. The camera remains capable of showing where the player will resume.
- Tone is slapstick and energetic, never injury-focused.
- Bike/rider separation, secondary motion, dust, scrape sounds and impact variation may raise spectacle, but gore, injury emphasis and long helpless sequences do not belong.
- PC can use richer ragdoll, particles and decals; Deck/mobile may use reduced simulation and VFX while preserving timing and readability.

The production crash rig must be replaceable independently of authoritative crash state. It receives the crash pose, velocity, surface and severity, then reports only completion/readiness signals. It does not decide race progress or recovery location.

## Respawn point selection

Maintain a rolling **last-good-course state**, not just a raw last transform.

A valid state:

- is on or safely adjacent to the intended route;
- has sufficient clearance from terrain and obstacles;
- is not on a jump lip, landing impact point, severe bank or start/finish ambiguity;
- includes forward course direction and lap-distance context;
- is recent enough to avoid a punitive rewind;
- does not advance the player farther around the course than legitimately reached.

Preferred selection order:

1. most recent validated last-good state;
2. nearest earlier safe sample on the course recovery path;
3. mode-specific fallback start position.

The bike respawns upright, aligned forward and stationary or with only a small safe roll speed. It must not immediately re-trigger the same crash.

## Race consequences

- Race and lap clocks continue through a wipeout.
- Forward momentum is lost.
- Position may be lost naturally to opponents.
- No extra time-penalty overlay is needed in the initial implementation.
- No persistent engine, tyre, suspension, rider or body damage exists.
- No repair animation, currency cost or inventory state follows the crash.

Manual reset uses the same safety rules but may use a mode-defined grid/recovery position. It must not become an exploit for progress or lap counting.

## State reset contract

On automatic recovery, clear:

- crashed/tumble state and timers;
- transient angular and vertical velocity;
- airborne time and trick rotation accumulation;
- landing recovery modifiers;
- active stunt combo where the mode requires it;
- stale camera impulse and contact flags;
- stale input edges that could immediately pitch or accelerate unexpectedly.

Preserve:

- race clock and legitimate lap progress;
- race position rules;
- player settings such as camera and throttle preference;
- non-mechanical progression owned by the event.

## Acceptance tests

1. Severe side landing produces Wipeout and restores control within 1.25–2.00 seconds.
2. Borderline landing produces Ugly at least some of the time under the agreed severity model, not an unexplained binary crash.
3. Respawn never places the bike inside terrain, across the track, at a jump lip or facing backwards.
4. Repeated crashes cannot add lap progress or move the player forward.
5. Countdown, pause, restart, quit and results transitions cancel stale crash/reset callbacks.
6. No mechanical-damage state persists after recovery.
7. Camera and audio return to normal without stuck impulse, drone or mute state.
8. Ten consecutive wipeouts feel varied visually but have consistent recovery timing.
9. High and reduced presentation tiers classify the same crash and restore control at the same time.
10. Replacing the rider/bike art or ragdoll rig does not change controller or race code.
