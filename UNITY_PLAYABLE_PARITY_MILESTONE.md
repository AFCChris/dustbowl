# Unity playable parity milestone

Status: implemented for user play review

Editor: Unity `6000.3.23f1`, C#, URP, Windows IL2CPP

## Outcome and selected course

This milestone replaces the rejected engineering-lab-only direction with one complete, recognizable Dustbowl race. It ports **Dustbowl Flats**, the original “classic” National, because its 1.51 km lap combines broad turns, elevation, natural rolling terrain, four table jumps, three whoop sections, one tabletop and one ripple section. It therefore exercises the player controller, AI, cameras, terrain following, authored features and race rules in one representative course.

The preserved web reference is tag `web-v0.18-behavioural-baseline` at `80e76510f9a8fd34cb213e36fd54cba227a31bce`. Live `src/game.js` remains untouched.

## Architecture

- `DustbowlFlatsNationalCourse` deterministically rebuilds the web course's nine normalized layout points, Hermite centreline, seeded land height, nine-pass grade smoothing, twelve-pass bank smoothing, authored start rotation, section speed hints and all nine feature zones.
- `CourseDefinition` is the serialized course authority. The closed line has 684 unique samples plus a duplicate closing sample and measures `1510.541 m`.
- `CourseSurface` remains the common gameplay query for centreline, lateral distance, surface blend, height and normal. Its render mesh and mesh collider share the same generated asset; the controller does not depend on `WheelCollider` or a bike `Rigidbody`.
- `ArcadeBikeController` remains explicit Grounded/Airborne/Wipeout code. This milestone adds only a race-start control gate and laterally offset grid spawn support to that controller.
- `NationalRaceManager` owns countdown, race time, player lap-sector validation, position ranking, finish and restart.
- `NationalAIRider` provides seven deterministic, pace-varied opponents. They follow the authoritative line, respect section pace hints, vary their line, make brief pace mistakes, receive soft five-percent gap adjustment, traverse authored terrain and visually release over major jump downsides.
- `RaceLapTracker` is shared deterministic lap logic. A rider must visit the 45–75 percent sector before an 80-to-20 percent start-line wrap counts.
- `ArcadeBikeCamera` and `CameraModeController` keep Chase, Close and Overhead as equal playable modes.
- `NationalRaceHud` presents countdown, lap, position, speed, race time, active camera and an eight-rider results board.
- `BikePresentation` is visual-only: wheel motion, rider posture and surface dust never feed back into handling.

The earlier BehaviourLab scene and Stage 1–3 assets remain available as regression infrastructure.

## Ported race and presentation systems

- eight-slot staggered starting grid matching the web spacing;
- `3`, `2`, `1`, `GO!` countdown with controls held until GO;
- three valid laps, sector-gated start/finish crossing, lap times and best lap;
- live `1/8` through `8/8` player position;
- seven opponents that start, progress, complete all laps and enter ranking;
- complete results overlay and Enter/gamepad-South restart;
- player natural-crest and authored-feature takeoff, air pitch/whip, accepted/wipeout landing, tumble and automatic recovery;
- Chase, Close and Overhead camera cycling;
- composite procedural motocross bikes and articulated rider silhouettes in eight colors;
- packed dirt ribbon, sculpted shoulders/berms, broad seeded desert, dusk lighting/fog, start gantry, finish stripe, course flags, rocks and scrub;
- speed-driven dust trails on all eight bikes;
- development telemetry inherited from Stage 3.

## Controls

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Steer | `A` / `D` | left stick |
| Brake | `S` or Down | left trigger |
| Manual throttle when auto-throttle is disabled | `W` or Up | right trigger |
| Air pitch | `R` / `F` | right stick vertical |
| Air whip/yaw | `Q` / `E` | right stick horizontal |
| Next / previous camera | `C` / `V` | right / left shoulder |
| Reset | Backspace | Y / north button |
| Restart after results | Enter | A / south button |
| Toggle telemetry capture | F9 | Start |

Auto-throttle is enabled by the current reference tuning and releases only after GO. Braking suppresses auto-throttle.

## Verification and build

From a command prompt, use Unity `6000.3.23f1` with the `unity` directory as `-projectPath`.

- Run all foundation and milestone checks: `-batchmode -nographics -quit -executeMethod Dustbowl.Editor.PlayableParitySetup.VerifyAllStages`
- Run EditMode tests: `-batchmode -nographics -runTests -testPlatform EditMode -testResults <path>`
- Run PlayMode tests: `-batchmode -nographics -runTests -testPlatform PlayMode -testResults <path>`
- Build Windows IL2CPP Development Player: `-batchmode -nographics -quit -executeMethod Dustbowl.Editor.PlayableParitySetup.BuildWindowsDevelopment -parityBuildPath <absolute-output.exe>`

Build output must remain outside the repository.

Verified Windows Development Build:

`C:\Users\chris\Documents\DustbowlBuilds\PlayableParity\Dustbowl.exe`

Final verification on 2026-09-04 passed all Stage 1–3 and playable-parity checks, all 27 EditMode tests and all 3 PlayMode tests. The IL2CPP player remained live through a 12-second smoke launch, initialized the National scene under Unity `6000.3.23f1` with D3D12, Input System and PhysX, and emitted no runtime exception in its launch log.

## Known differences from the web game

- The full course data and construction order are ported; Unity uses direct segment queries rather than the web build's baked nearest-point field.
- The player retains the Stage 3 Unity controller's practical interpretation of the web constants. It is not claimed to have passed the feel gate merely because deterministic checks pass.
- Unity opponents use a stable centreline/pace simulation rather than the web AI's near-player full controller, far-player analytical LOD, pairwise repulsion and off-track recovery. Their major-jump release is presentation-only at this milestone.
- Landing remains the current short-contact/accepted/wipeout implementation. The future Clean/Sketchy/Ugly/Wipeout shared result is intentionally not smuggled into this redirected milestone.
- The web's four-course event selector, title flow, pause/settings UI, minimap, generated engine audio and musical/chime hooks are not yet ported. This build opens directly into Dustbowl Flats.
- Bikes, riders, vegetation, terrain materials and HUD are deliberately replaceable development art, not final production assets or animation.
- There is no bike-to-bike collision authority. Positions come from race distance and riders may visually overlap.

## Remaining limitations

- User play review is required for steering sign/authority, speed perception, jump readability, air correction, landing forgiveness, camera comfort and overall fun. Tests cannot approve these.
- AI is race-functional but not production racing AI; overtaking, avoidance, collisions, recovery and jump physics need a later reviewed pass.
- The player has no pause menu, settings screen, audio mix, controller glyph switching or accessibility options yet.
- Environment dressing is intentionally sparse and has no LOD or measured Steam Deck/mobile budget.
- Crash presentation is the temporary deterministic tumble, not the final animation/ragdoll solution.
- Only Dustbowl Flats is included in this Unity milestone.

## User review checklist

1. Launch the Windows Development Build and confirm it reads immediately as a Dustbowl National rather than a test lab.
2. Confirm the grid contains the player and seven readable opponents and that nobody moves before GO.
3. Complete or sample a lap in Chase, Close and Overhead; judge steering, speed, terrain readability and camera comfort.
4. Ride both natural crests and authored jump zones with neutral and corrective air input.
5. Leave the packed line and confirm loose terrain meaningfully scrubs speed without an invisible hard penalty.
6. Trigger a bad landing and confirm the automatic reset restores useful control in about `1.7 s`.
7. Confirm lap and position display are comprehensible while racing.
8. Complete three laps, inspect the eight-rider results board and restart.

Do not begin the next enhancement milestone until this review is returned.
