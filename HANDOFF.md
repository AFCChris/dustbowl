# Dustbowl — handoff spec

For whoever (or whatever) picks this up next, in Replit or anywhere else. Assumes
you have the code and none of the history. `README.md` is the short tour; this is
the part that isn't in the code comments.

**What it is:** an arcade dirt-bike game in Three.js — a homage to the open-terrain
motocross games of the late 90s. Three laps of a procedurally cut desert circuit,
timed, with air-time scoring. Playable on desktop with a keyboard and on a phone
with touch controls. Working title.

**Where it stands:** feature-complete as a single-track time-attack. It runs, it's
tuned, it holds 60fps on a modern phone. It is not finished as a *game* — see
[Known gaps](#known-gaps).

---

## 1. Architecture

The whole thing is deliberately primitive: **no framework, no bundler, no
dependencies, no install step.** Three.js is a vendored file. The "build" is two
Node scripts that concatenate text. `package.json` has no `dependencies` block and
`npm install` is not a step anyone needs to run.

Do not "modernise" this without a reason. There is no module graph to untangle, no
transpile step to debug, and the game ships as one HTML file that can be dropped on
any static host or pasted into an artifact panel. That property is the point.

```
src/shell.html        markup + ALL the CSS (HUD, menus, touch controls)
src/game.js           the entire game (~1800 lines, one IIFE)
vendor/three.min.js   Three.js r160, vendored
tools/                build scripts (plain node, zero deps)
assets/               generated app icons (PNGs hand-encoded by tools/make-icons.js)

dist/                 GENERATED — the deployable app
dustbowl98.html       GENERATED — single-file build for a Claude artifact
dustbowl-app.zip      GENERATED — zipped dist/, for upload convenience
```

### Generated files are generated

`dist/index.html`, `dustbowl98.html` and the zip are **build outputs**. Editing them
is the single easiest mistake to make here, because they look like source — they're
plain readable HTML with the game inlined. Every change goes in `src/`, then:

```bash
npm run build
```

`npm run build:app` alone writes `dist/`; `build:artifact` alone writes
`dustbowl98.html`; `npm run serve` previews `dist/` on `http://localhost:8099`
exactly as a static host would.

`tools/build-app.js` runs `new Function(gameSource)` before writing anything and
aborts the build on a parse error. That guard exists because a syntax error in
`game.js` fails *silently* in the browser — the page loads, Three.js is present, the
canvas stays black, and nothing logs anything obvious. Don't remove it.

### The two builds differ in exactly one way

`dist/index.html` has its own `<head>`, so it can carry the iOS meta tags that make
**Add to Home Screen** launch fullscreen. The artifact build can't — the artifact
wrapper owns the head. That's why the phone version has to be hosted, and it's
documented for the end user in `DEPLOY.md` (copied into `dist/README.md` by the
build).

Consequences worth knowing:

- `src/shell.html` starts with a bare `<title>`, which `build-app.js` strips out and
  replaces from its own head. Keep that tag on line 1 and keep it on one line, or
  the regex misses it and you get two titles.
- `game.js` writes the `<meta name="viewport">` tag itself, from JS, at startup. It
  looks redundant next to the one in the app build's head — it isn't. Without
  `viewport-fit=cover` set from inside the document, `env(safe-area-inset-*)` reads
  zero in the artifact and the HUD sits under the Dynamic Island.

### Load order and globals

`shell.html` → `three.min.js` → `game.js`, three plain `<script>` tags, no modules,
no `defer`. `game.js` is one IIFE that assumes `THREE` is a global and that the DOM
already exists (it runs `document.querySelector` at top level). It is not
importable and does not export anything except a debug hook.

### Running it in Replit

Static HTML/CSS/JS Repl, contents of `dist/` at the root, `index.html` at the top
level. Deploy → Static. The Node build scripts run fine in a Replit Node environment
if you want the source tree there too; they need no network and no packages. See
`DEPLOY.md` for the phone install path.

---

## 2. `src/game.js` — the map

Read in this order. Everything is top-level and runs at load; there's no init
function.

| Lines (approx) | Section | Notes |
| --- | --- | --- |
| 28–101 | noise, `baseHeight` | Value noise + fBm. Deterministic integer hash, no seed input. |
| 103–245 | track cut, `bakeTrackField`, `trackProfile`, `onTrack`, `trackFeature`, `terrainH` | The core. See below. |
| 256–330 | renderer, sky, lights | |
| 318–443 | course layout | Spline, grading, banking, feature placement. Runs once in a block. |
| 445–641 | terrain meshes | Base grid, ramp patches, high-res feature strips. |
| 643–726 | scenery + start gantry | |
| 728–845 | bike model, rider, dust | |
| 847–954 | input (keyboard + touch stick) | |
| 956–1057 | WebAudio | |
| 1059–1156 | bike state `S`, race state `G`, respawn | |
| 1158–1399 | `step()`, `onLand()`, `crash()` | The physics. |
| 1401–1494 | HUD, minimap, camera | |
| 1496–1689 | `frame()`, `checkCheckpoint()`, `update()` | |
| 1691–1797 | lifecycle, buttons, `window.__dbg` | |

### The one invariant that matters

**`terrainH(x, z)` is the single source of truth for the ground.** The physics
samples it directly, and the mesh is built by sampling it. They cannot disagree,
because there is only one of it. Any change to how the ground is shaped goes in
`terrainH` / `baseHeight` / `trackFeature`, never in the mesh-building code. If you
ever find yourself adjusting a vertex height at mesh time, you've introduced a
visual/physical mismatch and the bike will start hitting invisible things.

### The track is a landform, not a texture

It is a **channel cut into the desert**: the racing surface sits `TRACK_CUT` metres
below the natural land, with `BERM_H`-high raised sides and banked corners. The
shape of the land is what keeps you on the track. There is no invisible wall and no
scripted penalty doing that job. `onTrack()` returns 1 on the racing line and 0 in
the sand, and drives only a **drag penalty** on loose ground — that's what makes
cutting a corner cost you rather than being free.

### `trackProfile` — two traps

```js
const p = trackProfile(x, z);   // → { s, y, bank, along, off } or null
```

1. **It returns a shared mutable singleton** (`_p`). Every call overwrites the same
   object. Never hold a reference across another call, never store one in an array.
   This is a deliberate allocation-avoidance choice — it's called hundreds of
   thousands of times during the terrain bake and once per physics step.
2. **It depends on `bakeTrackField()` having run.** The bake writes a 4m grid of
   nearest-polyline-point indices covering ±520m; `trackProfile` seeds off that grid
   and refines against four neighbouring segments. Walking the polyline per query
   instead is not "a bit slower", it makes the terrain build take minutes. If you
   move the track outside that ±520m box, or grow `WORLD`/`PLAY_R`, update
   `FIELD_MIN` / `FIELD_N` to match or the field silently returns `null` (= "not near
   the track") over the new area, and the road stops existing there.

Also: the lap distance `along` gets a special case on the closing segment (it
interpolates to `trackLen`, not to point 0's `along` of 0). Without it, lap distance
runs backwards through the entire track at the start/finish line and lap detection
breaks. Don't "simplify" that line.

### Air comes from the land, not from ramps

This is the biggest non-obvious design decision in the codebase, and it was arrived
at by trying the other thing first.

- The `ramps` array and `rampHeight()` still exist but **are empty and unused on this
  course**. Freestanding ramps sat beside the racing line, so riding around them was
  always faster — they were an obstacle, not a reward. The system is kept for the
  planned stunt mode. The ramp-mesh block builds an empty geometry when `ramps` is
  empty; that's harmless, not a bug.
- Jumps are now `features` **on the racing line**, inside the channel, so they can't
  be avoided: `table` (a hump), `tabletop` (steep face, flat deck, steep landing —
  clear it or case it), `whoops`, `ripples`. The lap plan is hand-authored in the
  `plan` array and spaced evenly, with a `worstBank()` check nudging features clear
  of banked corners so the corners stay raceable.
- A jump needs **curvature at the crest** to throw you. A flat-topped ramp gives you
  a ride-up-and-along and *less* air the taller it is. Hence `sin(πt)^1.35` for
  `table`.

`takeoff()` derives launch velocity from **`S.climb`, the smoothed rate the ground
has been rising under the wheels** — *not* from the terrain normal. At a lip the
normal is garbage (it straddles the drop) and `vel.y` is ~0 because the chassis hugs
the surface. There are two takeoff triggers, in `step()`:

1. the surface drops away faster than the bike can follow — a lip;
2. you crest a rise carrying more upward speed than gravity can cancel.

Without (2) the bike is glued to the terrain and only artificial ramps ever produce
air, which is precisely what made the open desert feel dead.

### Suspension is fake, and deliberately so

An earlier version low-passed the chassis height to model suspension travel. It
swallowed the chatter section whole and softened the jump faces; the bike felt
better without it. The wheels now follow `terrainH` directly. The filtered line
(`S.groundSmooth`, `S.jolt`) survives **for the rider-shake animation only** and must
not be allowed to feed back into handling.

Related: the main terrain grid has ~4.5m cells and physically cannot draw a 4m
chatter bump, so each feature section gets its own **high-resolution strip laid over
the base mesh** (`polygonOffset`, not a Y offset). Without it riders get bounced by
bumps they can't see. If you add a new feature kind, add it to that strip pass too.

### Physics tuning is bespoke

`step()` is arcade-tuned by feel, not simulated: partial gravity along slopes so you
keep enough speed up a jump face to launch, speed-dependent steering authority,
exponential grip recovery, landing angle deciding clean-vs-wipeout. **Swapping in a
real physics engine (cannon/rapier/ammo) means re-tuning the entire feel from
scratch** — it is not a drop-in improvement and it would throw away most of what has
been tuned here.

`update()` substeps at ~20ms but always covers the **whole** frame. An earlier
version clamped `dt`, which made the bike *and the race clock* run slower than real
time below 20fps. A slow frame must cost smoothness, never speed.

### Lap counting is not gate-based

`checkCheckpoint()` is a legacy name. It counts a lap when you cross the
start/finish plane forwards, guarded by a sector flag (`sectorSeen`) that requires
you to have been round the far side of the lap first — that's what stops reversing
over the line to farm laps. The `checkpoints` array now only supplies **control
points for the spline**. There are no gate rings in the world, and `arrowPivot` (the
old direction arrow) is created but permanently `visible = false` — dead code kept
because a stunt/free-ride mode would want it back.

The title screen still says "8 gates" and the key list still says `R` = "Reset to
last gate". Both are stale copy.

### Audio has several independent kill paths, on purpose

The engine note is written from the render loop. If the loop stops — tab hidden,
artifact panel closed, iframe detached — the gain would freeze at its last value and
drone forever. So there's `pagehide`, `beforeunload`, `visibilitychange`, *and* a
400ms `setInterval` watchdog that cuts the sound if `lastFrameAt` is more than 800ms
stale (a hidden iframe keeps timers running but may never fire `visibilitychange`,
which follows the *top* document). None of these depend on the render loop still
running. Keep it that way; a droning engine after the page is gone is the worst bug
this thing can have.

`initAudio()` is only called from `startGame()` / the sound chip, i.e. from a user
gesture — required by autoplay policy.

### Mobile decisions are made once, at load

`MOBILE` (coarse pointer or short screen) picks terrain resolution, shadow type, fog
distances, particle counts and scenery density **before the terrain is baked**.
Toggling it at runtime does nothing useful; changing it means a reload. `touchMode`
is separate and *is* runtime-switchable (there's a one-shot `touchstart` listener as
insurance for embedded frames where the media query lies).

Landscape is enforced by a CSS nag overlay on touch devices, not by the Orientation
API.

### Debug hook

`window.__dbg` exposes state, `step`, `terrainH`, `trackProfile`, the renderer, and
the lifecycle functions. It exists to drive the sim headlessly from an automated
browser. `tools/serve.js` also accepts `POST /shot?n=name` and writes the posted
data-URL to disk as a JPEG — dev-only, for capturing rendered frames. Neither is
shipped behind a flag; both are harmless but they are the reason those odd hooks
exist.

---

## 3. What works

- Three-lap timed race with lap times, best lap, results screen, restart, quit.
- Procedural desert (1100m, ~205k triangles at desktop resolution) with a cut,
  banked, feature-laden circuit generated on top of it.
- Arcade physics: launch, flight, pitch/whip control, clean-landing vs wipeout,
  crash tumble, auto-respawn onto the track facing the right way.
- Air-time and flip scoring with combo multiplier and callout banners.
- Keyboard and touch control. Touch is a floating thumbstick (steers on the ground,
  pitches in the air) plus a brake pedal, with auto-throttle on by default and a
  manual-throttle toggle persisted to `localStorage` under the `dustbowl.` prefix.
- Three cameras, minimap, speedo, air meter, on-screen FPS + per-frame work readout.
- iOS home-screen install path, safe-area-aware HUD, landscape nag.
- Audio: synthesised engine note, wind, landing thud, chimes. All WebAudio, no
  assets.

Verified by hand on desktop and a modern iPhone. There is **no automated test suite
and no lint/CI** — the FPS readout on screen is the performance test, and the build's
parse check is the only static gate.

---

## 4. Known gaps

Ordered roughly by how much they'd matter to a player.

- **One track, and it isn't actually seeded.** The README says a track is
  effectively a short string. That's the *intent*, not the state: the layout uses
  hardcoded constants inside `hash2()` calls, there is no seed parameter, and
  `let seedA = 0.4` in the course-layout block is dead. Threading a real seed through
  `hash2` and the checkpoint layout is the single highest-value change available and
  is not a large one.
- **No opponents, no ghost, no persistence.** Best lap lives in memory only and is
  wiped by "Restart run". Nothing is written to `localStorage` except the throttle
  preference.
- **Stunt mode is scaffolding only.** `body.mode-race` is set at startup and never
  cleared; the stunt-score HUD is CSS-hidden by it; the ramp system is present but
  fed an empty array. Scoring is fully implemented underneath — it just isn't shown
  in a race.
- **Scenery is decoration.** Rocks and bushes have no collision. They're placed with
  a clearance radius so they never sit on the track, which is what stops this being
  obvious.
- **No penalty for falling off.** Crash → 1.7s tumble → respawn at your last
  on-track position, facing forwards, with the clock still running. That's the only
  cost.
- **Terrain doesn't scale down.** One flat grid, no chunking, no LOD. Fine on
  desktop and a recent iPhone; an older phone will struggle and the only lever is
  `SEG`.
- **The PWA doesn't work offline.** There's a manifest and icons but no service
  worker, so "Add to Home Screen" gives you a fullscreen launcher, not an offline
  app.
- **Stale UI copy**: "8 gates", "Reset to last gate" (there are no gates any more).
- `BUILD` (`src/game.js`, near the top) is a hand-edited string shown on the title
  screen so you can tell at a glance whether a deploy actually landed. Bump it when
  you ship, or it lies.

---

## 5. Intended direction

Nothing here is committed to; it's where the design was pointing when work paused.

1. **Seeded tracks.** Make the seed a real parameter, surface it, let a track be
   shared as a short string. Cheap, and it turns one course into infinite courses.
2. **Stunt / free-ride mode.** The second mode the code is already shaped for:
   clear `mode-race`, show the stunt score, populate `ramps`, drop the lap structure.
   This is what the ramp system and the dead direction arrow are being kept for.
3. **Chunking + LOD**, if older phones become a target.
4. Race polish that follows from (1): saved best times per seed, a ghost, then
   opponents.

The aesthetic target is a late-90s console racer — dust haze, warm sunset palette,
condensed display type, chunky HUD panels. The visual language is consistent across
`shell.html` and the in-world colours; keep new UI inside it.

---

## 6. Things a fresh tool gets wrong

A checklist, because most of these look like improvements.

1. Editing `dist/index.html` or `dustbowl98.html` instead of `src/`. They're
   generated. Your change will vanish on the next build.
2. Adding a bundler, npm dependencies, ES modules, or an npm-installed Three.js.
   The zero-install, single-file property is a requirement, not an accident.
3. Upgrading or un-vendoring `vendor/three.min.js`. It's pinned at r160 and the code
   uses the global `THREE`, not imports.
4. Replacing the custom physics with a physics engine. Guaranteed total re-tune.
5. Storing the object returned by `trackProfile()`. It's a shared singleton.
6. Deriving takeoff velocity from the terrain normal because it "should" be the
   normal. It's unusable at a lip; use `S.climb`.
7. Re-adding a low-pass filter on chassis height as "proper suspension". It was
   there, it killed the chatter and the jump faces, it was removed on purpose.
8. Adjusting terrain heights at mesh-build time instead of in `terrainH`. Splits the
   visual and physical worlds.
9. Making jumps taller/longer to get more air. Air comes from crest curvature; a
   longer flat-topped jump gives *less*.
10. Clamping `dt` in `update()` to "stabilise" the physics. That desynchronises the
    race clock from real time on slow frames.
11. Removing one of the audio shutdown paths as redundant. They cover different
    failure modes; the interval watchdog covers the case none of the events do.
12. Deleting the ramp system, `arrowPivot`, or the `checkpoints` array as dead code.
    All three are deliberately retained for the planned second mode.
13. Trusting `checkCheckpoint()`'s name — it's a start/finish plane crossing, not a
    gate check.
14. Extending the world or moving the track without widening the `FIELD_MIN` /
    `FIELD_N` bake grid. Fails silently: the road just stops existing out there.
15. Removing the `new Function()` parse guard in `build-app.js`. A syntax error in
    `game.js` produces a black screen with no useful error.
16. Removing the JS-set viewport meta as duplicated by the app build's head. It's
    what makes safe-area insets work in the artifact build.
