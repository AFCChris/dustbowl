# Dustbowl web behavioural baseline

Status: Stage 0 A/B reference

This document records the behaviour of the approved Three.js web game. It is a
reference for a later Unity migration, not an instruction to reproduce browser
architecture or visual quality. Unity must match these behaviours closely or
record an intentional, user-approved improvement.

## Baseline identity

- Reviewed commit: `80e76510f9a8fd34cb213e36fd54cba227a31bce`
- Permanent tag: `web-v0.18-behavioural-baseline`
- Build stamp at that commit: `v0.18 · Clean Racing Lines`
- Approved production-foundation commit: `6e5ed219cad93c084fec61a2fcc45996b034f33d`
- The production-foundation commit has the baseline as its direct parent and
  adds only the eight approved foundation documents.
- The baseline `src/game.js` is 2,658 lines. Line references below point to the
  Stage 0 instrumented source, whose controller and race calculations remain
  unchanged.

Verified before Stage 0 changes on 2026-09-03:

```text
npm run build       PASS
node tools/smoke.js PASS (four courses plus fallback)
node tools/test-ai.js PASS (47 assertions)
```

The smoke run emits Three.js's existing warning that legacy `build/three*.js`
files are deprecated. It is not a test failure and Stage 0 does not replace the
vendored/global Three.js architecture.

The same three checks passed again after Stage 0 instrumentation. A headless
development run also exercised telemetry sampling and the takeoff, landing,
wipeout and automatic-reset event schema without changing the checked-in test
scripts.

## Measurement conventions

- Source physics values use metres, seconds, radians and metres per second.
- Telemetry reports speeds in m/s, time in simulation seconds and orientation
  in degrees for readability.
- `speed` is the magnitude of three-dimensional velocity. `planarSpeed` and
  `approachSpeed` use X/Z velocity only.
- Surface alignment is `dot(bikeUp, terrainNormal)`. Its error is also reported
  as `acos(alignment)` in degrees.
- A subjective feel judgement is never inferred from telemetry. Metrics make
  runs comparable; the Unity feel gate remains a user A/B play decision.

## Ground controller

### Acceleration and top speed

`GRAV = 22`, `MAX_SPEED = 42` and `RIDE_H = 0.42` are defined at
[`src/game.js` lines 202–204](src/game.js#L202). `MAX_SPEED` is an engine-curve
reference, not a hard clamp. With throttle, power is
`32 * (1 - clamp(forwardSpeed / MAX_SPEED, 0, 1))` and is applied along the
slope-projected forward direction
([`src/game.js` line 1946](src/game.js#L1946)). Power therefore tapers linearly
to zero at 42 m/s of projected forward speed. Actual terminal speed is
emergent from slope, steering loss and drag; downhill speed can exceed 42 m/s.

Auto-throttle supplies full gas unless braking or crashed. Manual throttle is
also available. Both are locked to zero during the countdown
([`src/game.js` lines 1875–1885](src/game.js#L1875)). Forward braking applies
`-24 m/s²`, reverse applies `12 m/s²`, and the effect ramps in below 3 m/s
([`src/game.js` line 1950](src/game.js#L1950)).

With throttle the base exponential rolling drag coefficient is `0.1`; while
coasting it is `0.85`. A further speed-shaped loss subtracts velocity scaled by
`0.004 * speed * dt`
([`src/game.js` lines 1974–1976](src/game.js#L1974)). These losses, rather than
a velocity cap, shape the achieved top speed.

### Speed-shaped steering and grip

Ground yaw rate is `2.2 rad/s * steerInput * authority`, signed for forward or
reverse travel. Authority is:

```text
clamp(speed / 6, 0, 1)
* (1 - clamp(speed / (42 * 1.5), 0, 0.55))
```

This suppresses steering near rest and reduces, but does not remove, authority
at high speed ([`src/game.js` line 1961](src/game.js#L1961)). Bike lean is a
separate smoothed visual/orientation target capped at `0.55 rad`, scaled to full
by 16 m/s ([`src/game.js` line 2008](src/game.js#L2008)).

Lateral velocity is exponentially removed. The nominal grip rate is `14`; it
falls toward `7` as lateral slip reaches 12 m/s
([`src/game.js` lines 1965–1970](src/game.js#L1965)). This is an arcade grip
algorithm, not tyre simulation.

### Off-track and loose sand

The racing surface has an 8.5 m half-width. `onTrack()` blends from 1 to 0
between lateral offsets 8.5 m and 14.1 m (`8.5 + 7 * 0.8`)
([`src/game.js` lines 245–248](src/game.js#L245),
[`src/game.js` lines 322–326](src/game.js#L322)). Loose-sand drag adds up to
`0.62` to the exponential drag coefficient
([`src/game.js` lines 1973–1976](src/game.js#L1973)). There is no invisible
off-track speed penalty or wall: the 4.2 m berm and surrounding landform also
affect the physical route through `terrainH()`.

### Terrain following and slope assistance

`terrainH(x,z)` is the single rendered/physical height authority. It combines
natural terrain, the cut and banked track, track features and retained ramp
height ([`src/game.js` lines 362–382](src/game.js#L362)). The normal is a central
difference sampled 0.7 m either side
([`src/game.js` lines 385–391](src/game.js#L385)).

Ground contact is accepted within `RIDE_H + 0.06 m`. The chassis follows the
height target using a vertical spring factor of 90 and damping factor of 16,
then snaps to the sampled surface when release conditions are not met
([`src/game.js` lines 1920–1928](src/game.js#L1920),
[`src/game.js` lines 1979–2006](src/game.js#L1979)). The filtered
`groundSmooth`/`jolt` path drives animation only and never handling.

Only 55% of gravity projected along the ground plane is applied. This slope
assistance preserves enough uphill momentum to make jump faces rewarding
([`src/game.js` lines 1953–1958](src/game.js#L1953)).

## Takeoff and air control

### Recent-climb-rate takeoff

After grounded motion, the next terrain height produces a ground-rise rate
clamped to `[-40, 40] m/s`. `S.climb` exponentially follows it at rate 11
([`src/game.js` lines 1984–1988](src/game.js#L1984)). At release, vertical
velocity becomes at least `min(S.climb, 26)`; a lip normal is deliberately not
used ([`src/game.js` lines 1302–1314](src/game.js#L1302)).

There are two explicit release tests
([`src/game.js` lines 1990–2002](src/game.js#L1990)):

1. **Lip/contact-drop release:** the moved chassis is more than 0.25 m above
   the next ride-height surface.
2. **Natural rounded-crest release:** `S.climb - GRAV * dt` remains more than
   `groundRate + 0.6`, so carried upward motion beats the falling ground.

The general grounded-to-air contact transition can also call the same takeoff
path. Stage 0 telemetry names the observed trigger `lip`, `rounded-crest`,
`lip+crest` or `contact-loss` without changing the release decision.

### Authored jumps

Authored features live on the racing line and feed `terrainH`; they are not
separate collision ramps. `table` uses a rounded
`sin(pi*t)^1.35` crest, `tabletop` uses a steep 26% face, flat deck and steep
final 26%, and `whoops`/`ripples` use repeated cosine waves faded at both ends
([`src/game.js` lines 332–359](src/game.js#L332)). The current free-standing
`ramps` array is empty. Authored and natural landforms share the same
recent-climb launch rule.

### Airborne pitch, whip and settling

Air applies gravity 22 and isotropic exponential drag at rate `0.06`
([`src/game.js` lines 2031–2036](src/game.js#L2031)). Pitch authority is
`2.1 rad/s`. Keyboard throttle/brake inputs affect pitch only when pressed after
takeoff, preventing a held auto-throttle key from silently tipping the bike;
the self-centring touch stick is exempt from that guard
([`src/game.js` lines 1880–1888](src/game.js#L1880),
[`src/game.js` line 2038](src/game.js#L2038)).

Air steering simultaneously applies yaw at `1.5 rad/s` and roll at `0.8 rad/s`.
Yaw is also accumulated into the bike's heading
([`src/game.js` lines 2038–2045](src/game.js#L2038)). This is the current
airborne whip behaviour; there is no separate trick-mode yaw controller.

With neutral pitch and steer after 0.2 s airborne, orientation exponentially
settles toward upright at the current yaw with rate 1.5
([`src/game.js` lines 2047–2051](src/game.js#L2047)). It does not snap level.

## Landing, crash and reset

### Landing evaluation and speed retention

At contact, the implementation records airborne time, surface alignment and
downward speed (`max(0, -velocity.y)`). Only flights longer than 0.3 s enter the
scored/wipeout evaluation
([`src/game.js` lines 2069–2083](src/game.js#L2069)). The current wipeout
boundary is exactly:

```text
alignment < 0.35
OR (alignment < 0.62 AND downward impact speed > 20 m/s)
```

All non-wipeout contacts continue. Horizontal speed is multiplied by a scrub
factor interpolated from 0.5 at alignment 0.35 to 1.0 at alignment 0.85; better
alignment retains full speed. Vertical velocity rebounds upward at 12% of its
pre-contact magnitude ([`src/game.js` lines 2100–2108](src/game.js#L2100)).

The live web game does **not** produce separate `Clean`, `Sketchy` and `Ugly`
states. It has accepted landings with a continuous speed scrub and a `WIPEOUT`
state. Those three names describe the approved future escalation model, not
three current classifications. Stage 0 telemetry therefore reports
`accepted`, `wipeout`, or `short-contact` and retains the raw alignment, impact
and speed data needed to compare boundary runs honestly.

### Wipeout, tumble and restored control

On wipeout, velocity is halved, vertical velocity is set to 4 m/s, and random
tumble rates are selected in ranges based on 9/6/9 rad/s for X/Y/Z
([`src/game.js` lines 2116–2127](src/game.js#L2116)). Ground hits multiply
velocity by 0.55, rebound vertical speed at 25%, and damp tumble by 0.6
([`src/game.js` lines 1894–1905](src/game.js#L1894)). Automatic recovery occurs
when `crashT > 1.7 s` ([`src/game.js` line 1910](src/game.js#L1910)). The clock
continues during the crash.

Automatic respawn projects the last grounded position back to nearest track
distance, faces the bike along the course, places it 0.6 m above normal ride
height, zeros velocity and immediately re-enables control
([`src/game.js` lines 1342–1372](src/game.js#L1342)). `lastGoodPos` is updated
on every grounded step, including loose sand; its name does not mean an
on-track-only checkpoint. Falling below Y = -60 also performs an automatic
respawn without first committing a wipeout. Manual `R` reset uses the grid-side
reset path ([`src/game.js` line 1068](src/game.js#L1068)).

## Cameras

The selected camera is persisted and cycles `CHASE`, `CLOSE`, `OVERHEAD`
([`src/game.js` lines 2207–2218](src/game.js#L2207)). All three look toward a
target 4 m forward and 1.2 m above the bike; that target rises by up to 2.2 m
with airtime. Position follows at exponential rate 7 normally or 3 while
crashed. FOV follows at rate 4 from 60° toward an added 9° at maximum reference
speed plus up to 3° for airtime
([`src/game.js` lines 2241–2257](src/game.js#L2241)).

- **Chase:** 2.5 m above the bike and `6.4 + 0.1 * speed` metres behind. The
  desired camera position is kept at least 1.4 m above terrain.
- **Close:** 1.8 m above and 4.2 m behind, without the speed-shaped pullback.
  It uses the same terrain clearance, target and FOV rules.
- **Overhead:** nominally 22 m above and 6 m behind. Airtime creates
  `airPull = clamp(airTime * 0.5, 0, 3.5)`, lowering and moving the camera closer
  so jumps fill more of the frame
  ([`src/game.js` lines 2223–2240](src/game.js#L2223)). It deliberately does not
  use the chase/close terrain-floor calculation.

A crash adds a small random visual-only camera displacement which decays at
rate 10. It does not affect physics.

## Race, checkpoint, countdown and AI rules

### Player lap and sector rules

`checkCheckpoint()` is a legacy name. There are no checkpoint gates. The player
must first occupy the 45–75% lap-distance sector, then cross forward from above
80% to below 20%. Only that wrapped crossing records a lap; the sector flag is
cleared after use ([`src/game.js` lines 2432–2446](src/game.js#L2432)). Lap
counts are course-specific. Reversing across start/finish without visiting the
far sector cannot farm laps.

### Countdown

The start sequence is `3`, `2`, `1`, `GO!`: the numbered phases are 900 ms
apart and the overlay clears 700 ms after `GO!`
([`src/game.js` lines 1684–1709](src/game.js#L1684)). During the entire sequence
player throttle is zero and `updateAI()` returns without advancing riders. At
the clear/GO release moment, player and AI lap clocks and the race clock are
resynchronised so countdown wall time is excluded. A monotonically increasing
token makes callbacks from quit/restart sessions stale rather than allowing
them to release a newer countdown.

### Player-visible AI race rules

- There are seven AI riders plus the player, with grid slots staggered from 8
  to 26 m behind the lap end ([`src/game.js` lines 1398–1411](src/game.js#L1398)).
- Base pace spans 0.78–0.96 of reference speed. Course section `aiSpeed` values
  further shape local pace ([`src/game.js` line 1474](src/game.js#L1474),
  [`src/game.js` lines 1550–1555](src/game.js#L1550)).
- Riders within 250 m use simplified terrain physics; farther riders advance
  analytically on the centreline. Full-sim off-track riders are recovered after
  0.55 s of sustained excursion with a speed cap of 45% of `MAX_SPEED`
  ([`src/game.js` lines 1718–1786](src/game.js#L1718)).
- Mistakes briefly reduce pace to 68% and offset the target line for 0.7–1.8 s;
  subsequent mistakes are scheduled 4–19 s later
  ([`src/game.js` lines 1801–1810](src/game.js#L1801)).
- Rubber-banding softly targets at most ±5% of each AI's base pace based on a
  200 m distance scale, approached at exponential rate 0.6
  ([`src/game.js` lines 1812–1819](src/game.js#L1812)).
- AI lap counting uses the same 45–75% sector concept and a forward wrap. Far
  AI uses analytical wrap; near AI rejects implausible nearest-track jumps.
  Finished riders rank ahead of unfinished riders; earlier finish time wins,
  while unfinished riders rank by completed laps plus along-track distance
  ([`src/game.js` lines 1624–1653](src/game.js#L1624)).
- Player/AI and AI/AI proximity applies soft velocity repulsion, not hard bike
  collision ([`src/game.js` lines 1821–1845](src/game.js#L1821)).

## Development-only telemetry

Stage 0 adds a recorder under the existing `window.__dbg` diagnostics hook
([`src/game.js` lines 2263–2403](src/game.js#L2263),
[`src/game.js` line 2809](src/game.js#L2809)). It is disabled by default, has no
UI, and no recorded value is read by gameplay. When enabled, it stores a bounded
36,000 frame-sample buffer and 2,000 event buffer in memory.

Use it from browser developer tools:

```js
__dbg.telemetry.start('C Natural rounded-crest jump')
__dbg.telemetry.mark('approach begins')
// perform the run
const run = __dbg.telemetry.stop()
__dbg.telemetry.download('C-natural-rounded-crest.json')
```

`clear()`, `snapshot()` and `mark(label, data)` are also available. A session
records course/build identity; position; height above terrain; 3D and planar
speed; vertical velocity; grounded/crashed state; airtime; recent climb rate;
surface amount; track distance/offset; control inputs; pitch/yaw/roll; camera
mode, position and FOV.

Takeoff events record approach planar speed, post-launch 3D speed, recent climb
rate, launch vertical velocity, orientation and release trigger. Landing events
record airborne duration, touchdown orientation, surface alignment/error,
downward impact speed and the live classification. Wipeout/reset events record
time from wipeout commit to the frame that respawn restores controller
authority.

Limitations:

- approach speed is instantaneous planar speed at release, not wheel RPM or an
  average over an arbitrary approach window;
- `surface` is the most recent grounded `onTrack()` sample while airborne;
- impact is downward world velocity, not a resolved contact impulse;
- Euler angles can wrap and are not a substitute for the raw quaternion when
  analysing full flips;
- restored control means the controller is enabled after respawn, not that the
  stationary bike has regained race speed;
- manual driving cannot hit a threshold identically every time, so retain raw
  metrics and video alongside representative runs;
- subjective camera comfort, speed sensation, jump satisfaction and landing
  generosity cannot be measured cleanly and require user A/B play judgement.

## Named behavioural reference scenarios

Run with the tagged web build, a fixed course selection, auto-throttle setting,
camera and input device recorded in the filename or session marker. Prefer
Dustbowl Flats unless a scenario names another course. Start telemetry before
the approach, use markers for input changes, and retain the JSON plus video when
visual judgement matters.

### A. Flat-ground acceleration run

Use the calm Dustbowl Flats start straight, auto-throttle on, no steer or brake.
Begin at rest after GO and continue until speed stabilises or terrain stops
being representative. Compare speed-over-time and acknowledge that slope/drag
make achieved top speed emergent; 42 m/s is not a clamp.

### B. Medium-speed sweeping turn

Enter a broad, jump-free bend at a recorded 18–25 m/s. Hold one repeatable
partial steering input, then release. Compare yaw response, lateral speed,
lean, exit speed and whether the route remains inside the blended road edge.

### C. Natural rounded-crest jump

Use an unfeatured terrain crest. Confirm the takeoff event says
`rounded-crest` and that `trackAlong` is outside every authored feature range.
Hold neutral input through the approach and flight. Compare approach/takeoff
speed, climb rate, launch vertical velocity, airtime and touchdown data.

### D. Authored jump/tabletop

Use the first Dustbowl Flats feature whose `kind` is `tabletop` (discoverable as
`__dbg.features.find(f => f.kind === 'tabletop')`). Approach on line with neutral
air input. Record whether release is `lip`, `lip+crest` or `contact-loss`, plus
launch, airtime and landing metrics. Retain video to judge casing/clearance.

### E. Neutral-input airborne run

Use the same authored jump as D. Release all pitch/steer controls before
takeoff and keep them neutral. Confirm the neutral settle begins only after
0.2 s airborne and compare touchdown attitude and speed retention.

### F. Maximum practical corrective pitch run

Repeat D, then apply full nose-up or nose-down input only after takeoff and hold
until just before contact. Mark press/release times. Compare the 2.1 rad/s
response, touchdown pitch, alignment, impact and classification. There is no
attitude clamp, so “practical” ends before deliberately completing a flip.

### G. Clean landing

Use D or E and target alignment at or above 0.85, where the live scrub reaches
1.0. Record impact, attitude and retained horizontal speed. Telemetry still
labels this `accepted`; “Clean” is the named reference scenario.

### H. Sketchy-boundary landing

Introduce a small late correction and target an accepted landing just below
alignment 0.85, where continuous speed scrub first becomes visible. The live
game has no `Sketchy` state; preserve raw metrics and video as a future tier
design reference.

### I. Ugly-boundary landing

Target the harshest repeatably accepted contact near a wipeout boundary: either
alignment just above 0.35 with impact at or below 20 m/s, or alignment at/above
0.62 with a harder impact. The live result remains `accepted`, usually with
heavy speed scrub; it is not classified `Ugly`.

### J. Wipeout-boundary landing

Cross one boundary by the smallest practical amount: alignment below 0.35, or
alignment below 0.62 with downward impact above 20 m/s. Confirm a `landing`
event classified `wipeout`, immediately followed by `wipeout-commit`.

### K. Crash-to-reset timing

Continue directly from J with no reset input. Measure
`wipeoutToRestoredControl` on `automatic-reset`; expect just over 1.7 s within
substep/frame quantisation. Confirm the bike is stationary, aligned to the
course near the last grounded location, and control is immediately available.

### L. Overhead-camera gameplay run

Select `OVERHEAD` before GO and complete at least one representative turn and
jump. Compare camera position/FOV samples and video for readability, jump
tightening, target tracking and sense of speed. Subjective camera quality is a
user judgement, not an automated pass/fail metric.

### M. Off-track cut / loose-sand behaviour

Leave a broad turn across the blended edge, remain beyond the berm long enough
to reach `surface = 0`, then return without manual reset. Compare track offset,
surface value and speed loss against an on-line run. Confirm there is drag and
landform resistance but no scripted player teleport, lap credit shortcut or
invisible wall.

## Deterministic checks and feel gate

Automated checks protect parse/build integrity, course/profile invariants,
deterministic lap/ranking/countdown rules and the telemetry interface. They do
not prove acceleration character, steering confidence, jump satisfaction,
landing generosity or camera feel. Any future Unity parity claim requires the
user to play named web and Unity scenarios A/B and approve the result.

## Known documentation/live-code inconsistencies

- Stage 1 resolves the stale foundation status lines: Unity 6.3 LTS, C# and URP
  are the approved production direction. The Stage 0 behavioural record itself
  remains unchanged.
- `HANDOFF.md` describes an older one-track time-trial with no opponents and
  says there is no automated suite. The live baseline has four authored
  National courses, seven AI riders and the smoke/AI scripts. `CODEX_HANDOFF.md`
  already marks `HANDOFF.md` as partly stale.
- The approved feel documents name Clean → Sketchy → Ugly → Wipeout as the
  desired production model. The live implementation only distinguishes
  accepted contacts from wipeouts and continuously scrubs accepted speed.
- `HANDOFF.md`'s approximate 1,800-line source map is historical. The immutable
  reviewed baseline contains 2,658 lines; Stage 0 instrumentation adds lines
  without altering the baseline tag.
