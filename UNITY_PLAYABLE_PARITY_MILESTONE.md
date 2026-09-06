# Unity playable parity milestone

Status: implemented for user play review

Editor: Unity `6000.3.23f1`, C#, URP, Windows IL2CPP

## Outcome and selected course

This milestone replaces the rejected engineering-lab-only direction with one complete, recognizable Dustbowl race. It ports **Dustbowl Flats**, the original “classic” National, because its 1.51 km lap combines broad turns, elevation, natural rolling terrain, four table jumps, three whoop sections, one tabletop and one ripple section. It therefore exercises the player controller, AI, cameras, terrain following, authored features and race rules in one representative course.

The preserved web reference is tag `web-v0.18-behavioural-baseline` at `80e76510f9a8fd34cb213e36fd54cba227a31bce`. Live `src/game.js` remains untouched.

## First human-playtest corrections

The first Windows playtest identified four parity defects. This corrective pass remains part of the playable-parity milestone rather than starting a later enhancement stage.

- **Race direction:** Three.js is right-handed and Unity is left-handed. The original port copied web X/Z coordinates directly, reflecting the course winding. National course generation now converts web Z to Unity Z with an explicit `-1` handedness transform while retaining increasing `trackPts` order, start rotation, features, lap distance and forward sector crossing. Mapping the generated points back to web coordinates reproduces the reviewed clockwise web minimap loop.
- **Yellow surface intersections:** the broad desert renderer previously sampled uncut `DustbowlFlatsHeight` while the authoritative course/collision mesh sampled the track-cut `CourseSurface`. The desert therefore remained about the `1.6 m` track cut above the rider in some areas. Its mesh now samples the same authoritative height, sits `0.12 m` below it, and omits coarse desert cells through the protected course corridor. The high-resolution authoritative surface continues across that corridor, so the visible and physical lap no longer disagree.
- **Basic palette:** the placeholder scene now uses the actual Dustbowl Flats web palette for pale sand (`#E3BE86`), ochre shoulders (`#B4783C`), packed red dirt (`#8A4520`), dusk-blue sky (`#1D3A63`), warm haze (`#E8AC78`), sunlight, rocks and scrub. Terrain materials are non-metallic. This is a readability correction, not final production art.
- **Minimap:** `NationalRaceHud` now draws a lightweight course outline in web-map orientation, a start marker, a heading-aware player marker and seven color-coded live opponent markers. It reads the authoritative `CourseDefinition` and rider transforms rather than maintaining separate race progress.

## Presentation pass 1 (terrain, sky, lighting, HUD, minimap)

The second human review accepted race direction, geometry, steering, AI count and race flow but judged the colours, lighting, HUD typography and minimap substantially worse than the web build. This pass touches presentation only. Course generation, `CourseSurface`, `ArcadeBikeController`, `NationalAIRider`, `RaceLapTracker`, `NationalRaceManager`, cameras, bike/rider meshes and dust are unchanged; the web build remains the style reference.

- **Colour space:** the project now renders in linear colour space, which URP expects. Authored sRGB palette values are left as sRGB in materials, lights and render settings and converted by Unity. The sun drops from `2.5` to `1.55` because URP's lambert term no longer hides a `1/pi` factor.
- **Terrain:** the three terrain materials use generated albedo textures (`unity/Tools/generate-terrain-textures.js`) that port the web `groundColor()` mottling: pale sand with ochre and scrub blotches, ochre shoulders that darken toward the racing surface, and packed red-brown clay with twin wheel ruts. The racing ribbon has its own `NationalPackedDirt` material so the start gantry keeps the flat `NationalTrack` colour.
- **Sky and atmosphere:** `Dustbowl/Sky Gradient` is a small unlit skybox shader with dusk-blue zenith, dusty mid band, warm horizon haze and a sun disc aligned with `DesertSun`. Fog starts at `300 m` and completes at `1100 m` in the web haze colour; trilight ambient approximates the web hemisphere light.
- **Post-processing:** a global `Volume` (`Settings/DustbowlFlats_Presentation.asset`) applies neutral tonemapping, `+0.2` exposure, mild contrast/saturation, a warm filter and a soft vignette. The renderer receives URP `PostProcessData`, MSAA 4×, two shadow cascades, soft shadows and a `130 m` shadow distance. Graphics settings never change gameplay rules.
- **HUD:** `NationalRaceHud` keeps its `Configure(race, player)` API and read-only race queries but redraws the layout after the web shell: Barlow Condensed numerals with Share Tech Mono labels (both SIL OFL, see `unity/ASSET_REGISTER.md`), a cut-corner race panel with lap clock, best lap, lap-done percentage, lap count and position (mirroring the web stat block), a camera chip, an arc speedometer in mph, a bottom controls hint, a large drop-shadowed countdown and a results board that highlights the player row and the best lap. Everything scales from a `900 px` design height (minimum scale `0.8`) so nothing clips at 16:9, 16:10 or the Steam Deck's `1280×800`.
- **Minimap:** the map is a `236 px` framed panel (about `283 px` at 1080p) with a compass ring, the full course drawn as a thick outlined ribbon in web orientation, a start marker, seven coloured opponent dots and a rotating player chevron, using the same authoritative course line and rider transforms as before.

## Presentation pass 2 (minimap containment, dust/roost)

The third human review accepted the colour, lighting, sky and top-left HUD work but found the minimap route drawn across the gameplay view and the dust still blocky. Both are presentation-only fixes; race, course, controller, AI and lap code are untouched.

- **Minimap root cause:** the route, ring and chevron were rotated quads positioned with `GUIUtility.RotateAroundPivot`. That helper composes its rotation on the screen side of `GUI.matrix` without scaling the pivot, so under the HUD's resolution scale (`Screen.height / 900`, i.e. `1.2` at 1080p) every segment swung around a point hundreds of pixels away from the panel and the route sprayed across the view. The same fault displaced the speedometer arc.
- **Minimap fix:** `NationalRaceHud.BuildCourseMapTexture` now rasterises the whole loop (dark outline, light fill) and the compass ring once into a `512²` texture whose pixels map 1:1 onto the panel's inner rect, so the course cannot leave the panel by construction. Start and opponent dots are drawn inside a `GUI.BeginGroup` clip and clamped to the inner rect; the player chevron is clamped so its rotated footprint stays inside. Rotation now goes through `RotateAround`, which multiplies the rotation into `GUI.matrix` on the design side, fixing the speedo arc as well. An EditMode test bakes the map from the real course data and asserts nothing touches the edge while the loop fills more than half the map in both axes.
- **Dust root cause:** `NationalDust` was an opaque URP/Lit material with no texture, so every particle was a hard-edged, sun-lit solid square, and the systems had no fade, growth or rotation over lifetime; particles popped in and out and intersected the ground with sharp lines.
- **Dust fix:** `NationalDust` is now `Universal Render Pipeline/Particles/Unlit`, alpha blended, with soft particles (`0.05–0.6 m` depth fade), camera fading (`0.35–1.6 m`) and a generated four-puff sprite sheet (`unity/Tools/generate-dust-sprite.js`). All eight `DustTrail` systems emit fewer, larger puffs (`0.55–1.15 m`, `0.9–1.7 s`) that fade in and out, grow `0.45× → 1.35×`, spin slowly, pick a random puff row, are slowed by velocity damping and billow with low-quality noise; `maxParticles` drops from `220` to `110` per bike. `BikePresentation` emits at `1.1 × speed` (cap `34/s`) and adds a single landing burst. Soft particles require the URP camera depth texture, which is now enabled; this is the only added render cost.

## Architecture

- `DustbowlFlatsNationalCourse` deterministically rebuilds the web course's nine normalized layout points, Hermite centreline, seeded land height, nine-pass grade smoothing, twelve-pass bank smoothing, authored start rotation, section speed hints and all nine feature zones.
- `CourseDefinition` is the serialized course authority. The closed line has 684 unique samples plus a duplicate closing sample and measures `1510.541 m`.
- `CourseSurface` remains the common gameplay query for centreline, lateral distance, surface blend, height and normal. Its render mesh and mesh collider share the same generated asset; the controller does not depend on `WheelCollider` or a bike `Rigidbody`.
- `ArcadeBikeController` remains explicit Grounded/Airborne/Wipeout code. This milestone adds only a race-start control gate and laterally offset grid spawn support to that controller.
- `NationalRaceManager` owns countdown, race time, player lap-sector validation, position ranking, finish and restart.
- `NationalAIRider` provides seven deterministic, pace-varied opponents. They follow the authoritative line, respect section pace hints, vary their line, make brief pace mistakes, receive soft five-percent gap adjustment, traverse authored terrain and visually release over major jump downsides.
- `RaceLapTracker` is shared deterministic lap logic. A rider must visit the 45–75 percent sector before an 80-to-20 percent start-line wrap counts.
- `ArcadeBikeCamera` and `CameraModeController` keep Chase, Close and Overhead as equal playable modes.
- `NationalRaceHud` presents countdown, lap, position, speed, race time, active camera, live eight-rider minimap and an eight-rider results board.
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

`C:\Users\chris\Documents\DustbowlBuilds\PlayableParityPlaytestFix1\Dustbowl.exe`

Presentation passes 1 and 2 were authored on a Linux agent host without the Unity Editor. Its scene, material, project-settings and URP asset edits were made directly in the serialized YAML and mirrored in `PlayableParitySetup.Configure`, all GUID references were cross-checked against `.meta` files, and the C# was compiled with the .NET 8 compiler against Unity API stubs. The EditMode tests `PresentationPassWiresSkyLightingPostProcessingTerrainTexturesAndHudFonts`, `MinimapCourseBakeStaysInsideItsPanelAndDrawsTheWholeLoop` and `DustRoostUsesSoftTexturedTransparentParticlesOnAllEightBikes`, plus a fresh Windows Development Build, must be run on the Windows workstation before these passes are treated as verified.

Final verification on 2026-09-04 passed all Stage 1–3 and playable-parity checks, all 30 EditMode tests and all 3 PlayMode tests. The correction-specific checks verify web-equivalent course winding, the entire closed course corridor against the broad desert mesh, all eight live minimap markers, opponent marker movement and Chase/Close/Overhead cycling. The IL2CPP player remained live during smoke launch, rendered the corrected National scene and minimap in a direct window capture under Unity `6000.3.23f1` with D3D12, Input System and PhysX, and emitted no runtime exception in its launch log.

## Known differences from the web game

- The full course data and construction order are ported; Unity uses direct segment queries rather than the web build's baked nearest-point field.
- The player retains the Stage 3 Unity controller's practical interpretation of the web constants. It is not claimed to have passed the feel gate merely because deterministic checks pass.
- Unity opponents use a stable centreline/pace simulation rather than the web AI's near-player full controller, far-player analytical LOD, pairwise repulsion and off-track recovery. Their major-jump release is presentation-only at this milestone.
- Landing remains the current short-contact/accepted/wipeout implementation. The future Clean/Sketchy/Ugly/Wipeout shared result is intentionally not smuggled into this redirected milestone.
- The web's four-course event selector, title flow, pause/settings UI, generated engine audio and musical/chime hooks are not yet ported. This build opens directly into Dustbowl Flats; its functional minimap is now present.
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
