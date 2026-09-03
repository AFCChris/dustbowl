# Dustbowl Codex handoff

Status: revised canonical handoff awaiting foundation approval

Read first: `AGENTS.md`

## Mission

Build the long-term Dustbowl version without losing the tuned arcade behaviour already present in the Three.js game, and raise it into a polished modern indie game. Treat the web implementation as valuable executable design evidence, not disposable prototype code and not a visual-quality target.

No engine migration has begun. The revised recommendation is Unity 6.3 LTS with C# and URP, awaiting user approval.

## Authority order

When sources disagree, use this order:

1. direct current user instruction;
2. `AGENTS.md` safety and workflow constraints;
3. `GAME_VISION.md` and `FEEL_SPEC.md` for product intent;
4. `CRASH_AND_RESET_SPEC.md` and `PHYSICS_REFERENCE.md` for behaviour;
5. approved decisions in `ENGINE_DECISION.md` and `MIGRATION_PLAN.md`;
6. live behaviour and source at the recorded web baseline;
7. `HANDOFF.md`, `README.md` and `replit.md` for legacy implementation knowledge.

`HANDOFF.md` contains excellent architectural detail but is partly stale. It still says the game has one unseeded track and no opponents. Current `src/game.js` contains four authored National courses, course-specific seeds, seven AI riders, grid/countdown, positions and full-field results.

## Reviewed baseline

- Repository: <https://github.com/AFCChris/dustbowl>
- Commit: `80e7651`
- Web build: `v0.18 · Clean Racing Lines`
- Main source: `src/game.js` (2,658 lines at review)
- UI source: `src/shell.html`
- Engine/render library: vendored Three.js r160
- Web architecture: single IIFE, global `THREE`, zero npm dependencies

Checks passing on 2026-08-27:

```text
npm run build
node tools/smoke.js
node tools/test-ai.js
```

The smoke test validates all four courses and fallback selection. The AI suite reports 47 passing assertions. Running the build generates gitignored `dist/` and `dustbowl98.html`.

## What exists now

- four authored National courses with distinct layouts, terrain seeds, palettes, laps and section metadata;
- shared procedural terrain and cut/banked track generation;
- tables, tabletops, whoops and ripples embedded in the racing line;
- custom ground, air, takeoff, landing, crash and reset logic;
- player bike/rider primitives with visual-only posture/suspension animation;
- seven simplified AI riders with near/far LOD, mistakes, recovery and rubber-banding;
- grid start and tokenised 3-2-1-GO countdown;
- lap sector gating, position, timing and results;
- chase, close and overhead cameras;
- keyboard, touch, auto/manual throttle and saved preferences;
- procedural dust and WebAudio;
- mobile-aware quality choices and installable hosted web build;
- debug hook plus headless smoke and AI tests.

The PNGs under `attached_assets/` are not referenced by the live build. They include historical UI/game captures and unrelated workflow screenshots; do not treat them as a production art pack. The `assets/icon-*.png` files are generated web app icons.

## Non-negotiable feel

- arcade, not simulation;
- light, springy and fast;
- big satisfying jumps;
- generous mid-air control;
- forgiving landings with Clean → Sketchy → Ugly → Wipeout escalation;
- funny crashes and about 1.25–2.0 seconds to restored control;
- no persistent mechanical damage;
- mistakes create drama and a correction window before failure;
- terrain behaves like a playground;
- realism survives only where it improves fun.

## Non-negotiable production ambition

- The web build is the behavioural reference, not the visual or production ceiling.
- Behavioural parity and visual parity are different. Protect the first; do not pursue the second.
- The Unity version should ultimately be a substantial upgrade in terrain, environments, materials, lighting, atmosphere, vegetation, bikes, riders, animation, VFX, audio, UI, cameras, AI and race presentation.
- Modern stylised realism, terrain readability, expressive motion and strong sense of speed are the visual direction.
- Temporary placeholder assets are expected in the behavioural prototype.
- Gameplay systems must depend on stable data/telemetry interfaces so models, rigs, materials, particles, sounds and UI can be replaced without rewriting handling.
- Generic third-party assets may accelerate production, but signature Dustbowl assets and final art direction must be custom or heavily customised.
- Windows/Steam is primary; Steam Deck is considered throughout; Android/iOS follow.
- Gamepad is the feel reference, keyboard remains complete and touch follows the core game.
- Online multiplayer is outside the initial release scope.
- Target 60 fps where practical.

## Web invariants worth protecting

- Visible and physical terrain share one authority (`terrainH` in the web build).
- The track is a landform, not an invisible-walled texture.
- Jumps on racing lines are rewards; unused freestanding ramps are retained for future stunt use.
- Takeoff uses recent ground-climb rate and supports lip plus rounded-crest release.
- Visual suspension does not feed back into physics.
- Normal low frame rate must not slow race time or simulation time.
- Lap crossings require the far-side sector gate.
- Countdown callbacks are tokenised so restart/quit cancels stale events.
- The overhead camera and auto-throttle are part of Dustbowl's identity.

## First five implementation tasks after approval

1. **Freeze and capture the behavioural baseline.** Tag or record commit `80e7651`; capture named web runs with documented input for natural crest, authored jump, off-track cut, each landing tier boundary and crash recovery, then define the equivalent gamepad actions for Unity.
2. **Create the minimal Unity/URP foundation.** Add `unity/` without moving web files; pin a Unity 6.3 LTS patch and package versions; configure Input System actions, 60 Hz fixed update, test assemblies, telemetry and PC/Deck build profiles.
3. **Build the authoritative course/terrain contract.** Define C# interfaces and ScriptableObject/data formats for height, normal, along/lateral track profile and features; prove render/collision agreement on one Dustbowl Flats segment.
4. **Port the custom arcade controller.** Implement grounded motion, slope assistance, speed-shaped steering, loose-sand drag, climb-rate takeoff, air control and neutral settle. Do not begin with `WheelCollider`, a multi-body motorcycle or a vehicle framework.
5. **Complete and approve Stage A.** Implement Clean/Sketchy/Ugly/Wipeout, validated 1.25–2.0-second recovery and temporary chase/overhead cameras; run the user A/B feel review before beginning polished assets, AI parity or full courses.

After task 5, Stage B deliberately introduces one credible bike, rider, environment and race. Stage C then proves the commercial visual/audio target before broad content production.

## Working method

- Inspect before editing; preserve unrelated user changes.
- Keep web and target checks separate and runnable.
- Make one feel hypothesis per change where possible.
- Add a deterministic or headless regression check for pure rules.
- Use telemetry plus play judgement; numbers cannot approve feel alone.
- State every intentional divergence from the web behaviour.
- Stop for user review at migration gates in `MIGRATION_PLAN.md`.
- Keep behavioural, gameplay-slice and visual-slice approval separate.
- Maintain an asset register with source, licence, URP/mobile support, version and replacement risk.
- Profile on real PC and Steam Deck hardware before scaling content.

## Open decisions for the user

The following do not block this documentation package but should be answered before or during Stages 0–B:

- whether the current web build must remain publicly deployed indefinitely or only through migration;
- whether National race is definitely the first production mode after the feel slice;
- preferred balance inside modern stylised realism: cleaner/graphic or more textured/natural;
- initial art/audio budget and willingness to use paid Unity assets or freelancers;
- target PC minimum/recommended hardware and acceptable Steam Deck fallback if 60 fps proves impractical;
- preferred gamepad family for glyph/reference testing;
- whether iOS/Android should be premium, ad-supported or decided after the PC game;
- desired Windows/Steam launch scope: number of tracks, riders and modes;
- whether replay/photo mode is important enough to design into the camera architecture early.

## Stop conditions

Stop and ask before:

- switching the target engine or language;
- introducing a vehicle physics package or replacing the custom controller;
- moving/deleting the web project;
- changing the accepted landing/crash timing boundaries materially;
- copying content from *Motocross Madness*;
- beginning console, multiplayer, monetisation or account-system work;
- adopting a large Unity framework or marketplace controller that takes ownership of physics, input, camera or saves;
- treating placeholder graphics as approved art direction;
- declaring target-engine parity without a user A/B feel review.
- declaring production quality without the gameplay and visual vertical-slice gates.
