# Dustbowl '98

Arcade dirt-bike riding over procedural desert — a tribute to the open-terrain
motocross games of the late 90s. Working title.

No framework, no bundler, no install step. Three.js is vendored as a file and
the build is two scripts that concatenate text.

## Layout

```
src/shell.html      markup + all the CSS (HUD, menus, touch controls)
src/game.js         the entire game: terrain, physics, bike, audio, HUD
vendor/three.min.js Three.js r160, vendored so there's nothing to install
tools/              build scripts (plain node, no dependencies)
assets/             generated app icons
dist/               ← the deployable app. Upload this to a static host.
dustbowl98.html     single-file build for publishing as a Claude artifact
```

## Builds

```
npm run build          both of the below
npm run build:app      → dist/          the real app, installable on a phone
npm run build:artifact → dustbowl98.html  single file, for a Claude artifact
npm run icons          regenerate the app icons
npm run serve          preview dist/ at http://localhost:8099
```

The two builds differ in one way that matters: `dist/index.html` has its own
`<head>`, so it can carry the iOS meta tags that make **Add to Home Screen**
launch fullscreen. The artifact build can't — the artifact wrapper owns the
head — which is why the phone version has to be hosted. See `DEPLOY.md`.

## Where things are in `src/game.js`

| Section | What it does |
| --- | --- |
| noise / `baseHeight` | Procedural terrain. Physics and the mesh share this function, so they can't disagree. |
| track / `bakeTrackField` | The road: a closed spline through the gates, graded so it follows the land. Distance and road height are baked into a grid once — sampling the polyline per query was far too slow for a terrain build. |
| `onTrack()` | 1 on the racing line, 0 in the sand. Drives the off-road speed penalty, which is what stops cutting corners being free. |
| `ramps` / `rampHeight` | Jumps. **Unused on the Baja course** — they sat beside the racing line so going round was faster, i.e. an obstacle rather than a reward. Kept for the planned stunt mode. |
| `step()` | The arcade physics: suspension, grip, steering, flight, landings. |
| `takeoff()` | Launch velocity comes from the smoothed rate the ground is rising, *not* the terrain normal — the normal is garbage at a lip because it straddles the drop. Two triggers: a lip dropping away, or cresting a rise carrying more upward speed than gravity can cancel. Without the second, the bike is glued to the terrain and only artificial ramps give air. |
| `onLand()` | Landing angle decides clean landing vs wipeout, and scores the air. |
| `checkCheckpoint()` | Gate passes: you must cross the gate's plane *inside* the ring. |
| touch input | Floating thumbstick: steers on the ground, pitches in the air. |
| audio | WebAudio engine note. Several independent shutdowns — the render loop stopping must never leave it droning. |

## Notes for future work

- The physics is deliberately custom and arcade-tuned. Swapping in a real
  physics engine would mean re-tuning the entire feel from scratch.
- Terrain is one 320×320 grid (~205k triangles). Fine on desktop and on a
  15 Pro; would need chunking and LOD for older phones.
- Tracks are generated from a seed, so a track is effectively a short string —
  which is what would make sharing tracks cheap to add later.
