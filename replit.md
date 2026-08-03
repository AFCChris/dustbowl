# Dustbowl

Arcade dirt-bike game in Three.js — a homage to open-terrain motocross games of the late 90s. Three laps of a procedurally cut desert circuit, timed, with air-time scoring. Playable on desktop (keyboard) and mobile (touch).

## How to run

```
npm run serve
```

The workflow `Start application` runs `PORT=5000 npm run serve`, which previews `dist/` at port 5000. The workflow starts automatically.

## How to build

All changes go in `src/` — **never edit `dist/index.html` or `dustbowl98.html` directly, they are generated**.

```bash
npm run build        # builds both dist/ and dustbowl98.html
npm run build:app    # builds dist/ only
```

The build runs `new Function(gameSource)` as a parse check before writing anything. A syntax error in `game.js` produces a silent black screen in the browser; this guard catches it at build time.

## Stack

- No framework, no bundler, no install step
- Three.js r160 vendored at `vendor/three.min.js`
- Build scripts are plain Node in `tools/`

## Key files

| File | Purpose |
|---|---|
| `src/game.js` | Entire game (~1800 lines, one IIFE) |
| `src/shell.html` | All markup + CSS (HUD, menus, touch controls) |
| `vendor/three.min.js` | Three.js r160, vendored and pinned |
| `tools/build-app.js` | Builds `dist/` |
| `tools/build-artifact.js` | Builds `dustbowl98.html` (single-file) |
| `tools/serve.js` | Static file server for `dist/` |

## Important rules (from HANDOFF.md)

- Always follow `HANDOFF.md` — it contains the full architecture spec, known gaps, intended direction, and a checklist of things a fresh tool gets wrong.
- `terrainH(x,z)` is the single source of truth for ground height. Never adjust vertex heights at mesh-build time.
- Do not add a bundler, npm dependencies, ES modules, or un-vendor Three.js.
- Do not replace the custom physics with a physics engine.
- Do not clamp `dt` in `update()`.
- Bump the `BUILD` string near the top of `src/game.js` when shipping a change.

## User preferences

- Always follow all information in HANDOFF.md when making changes.
