# Dustbowl development and migration plan

Status: approved migration plan; Stage 3 arcade-controller slice implemented and awaiting user A/B feel review

Target: Unity 6.3 LTS, C# and URP

## Development principle

Do not merely migrate Dustbowl. Use the working web game as executable knowledge while building the foundations of a proper modern game.

This is a behavioural port, production-architecture redesign and eventual audiovisual transformation. It is not a line-by-line rewrite, a clean-slate rejection of the web prototype or a visual remaster of browser-quality assets. The existing Three.js version remains runnable as an A/B reference for feel, course logic, camera intent and race rules.

**Behavioural parity and visual parity are different goals.** The first is protected. The second is explicitly not desired.

## Platform order

1. Windows PC/Steam is the primary development and first-release platform.
2. Steam Deck is tested throughout and must influence controller, UI, performance and graphics-tier decisions.
3. Android and iOS are intended later release platforms.
4. Mobile-friendly architecture begins early, but touch UI and intensive mobile optimisation follow the proven PC core.
5. Gamepad is the primary feel reference; keyboard remains fully supported; touch follows.
6. Online multiplayer is outside the initial release scope.
7. Target 60 fps where practical.

## Proposed parallel repository shape

Create this only after approval:

```text
/
  web/                 current Three.js source/build/tooling, moved only in a separately approved step
  unity/               target-engine project
  reference/           telemetry, captures, comparison data and approved test metadata
  docs/                canonical documents, moved only if restructuring is approved
```

Because this task forbids restructuring, the current files remain in place. The first implementation step should add `unity/` beside the current tree. Moving the web files can happen only after its build, deployment and Replit workflows have been updated and verified.

## Baseline snapshot

The reviewed web baseline is commit `80e7651`, build string `v0.18 · Clean Racing Lines`.

Verified on 2026-08-27:

- `npm run build` succeeds;
- `node tools/smoke.js` succeeds for all four courses plus fallback selection;
- `node tools/test-ai.js` passes all 47 reported assertions;
- generated outputs remain gitignored;
- no npm dependencies are required.

Before implementation begins, tag the approved baseline or record its immutable commit in the development branch.

## System classification

| Current system | Classification | Long-term treatment |
| --- | --- | --- |
| Player controller feel and tuning | **Preserve behaviour closely** | explicit custom Unity controller; A/B tune against web |
| Ground/air state split | **Preserve behaviour closely** | retain clear authority and transitions |
| Climb-rate takeoff and two release triggers | **Preserve behaviour closely** | port before experimenting |
| Natural-crest jumping | **Preserve behaviour closely** | mandatory in the first behavioural prototype |
| Generous air pitch/whip and neutral settle | **Preserve behaviour closely** | reproduce intent and correction window |
| Landing wipeout boundaries and speed scrub | **Preserve behaviour closely** | baseline for four-tier landing system |
| Fast tumble and recovery | **Preserve behaviour closely** | 1.25–2.0 seconds to useful control |
| No persistent damage | **Preserve behaviour closely** | no core damage/repair system |
| Auto-throttle/manual option | **Preserve behaviour closely** | Unity Input System and saved settings |
| Chase, close and overhead camera intent | **Preserve behaviour closely** | rebuild and significantly polish with Cinemachine/custom camera layer |
| Track as cut/banked landform | **Preserve concept but redesign implementation** | authoritative course data drives Unity terrain/meshes/collision |
| `terrainH` single-source invariant | **Preserve concept but redesign implementation** | render, collision and sampling share generated data |
| Authored course layouts/section metadata | **Preserve concept but redesign implementation** | ScriptableObjects/data plus authoring and validation tools |
| Nearest-track baked field/`trackProfile` | **Preserve concept but redesign implementation** | curve/spatial service with equivalent outputs |
| On-line jump features | **Preserve concept but redesign implementation** | production track-feature system and high-quality geometry/materials |
| High-resolution feature strips | **Preserve concept but redesign implementation** | eliminate visible/collision mismatch through production terrain pipeline |
| Seven-rider AI, pace variation and mistakes | **Preserve concept but redesign implementation** | scalable AI, better racing lines, overtaking and animation |
| Soft ±5% rubber-banding | **Preserve behaviour initially, retune later** | competitiveness baseline, player-tested |
| Lap/sector gating, grid start and ranking | **Preserve behaviour closely** | port rules and regression tests |
| National calendar and results flow | **Preserve concept but redesign implementation** | modern event, UI and presentation flow |
| Primitive bike/rider models | **Replace** | temporary placeholders, then original production models and rig |
| Basic rider/suspension animation | **Replace** | authored clips plus procedural rig/suspension layers |
| Current crash tumble presentation | **Preserve timing, replace presentation** | polished hybrid animation/ragdoll while authority stays deterministic |
| Current terrain mesh/materials | **Replace** | higher-fidelity chunked terrain, layered materials and platform LOD |
| Rocks, shrubs and environmental dressing | **Replace** | art-directed vegetation/prop sets, instancing and LOD |
| Current sky, fog, lighting and shadows | **Replace** | original atmosphere and platform-scalable URP lighting |
| Current dust particles | **Preserve intent, replace implementation** | signature surface-aware VFX with scalable budgets |
| Current bike/audio synthesis | **Preserve feedback intent, replace implementation** | layered engine, surface, wind, impact, ambience and music |
| DOM/CSS HUD and menus | **Replace** | distinctive production UI, controller/Deck/mobile aware |
| Primitive AI rider visuals | **Replace** | production bikes, riders, animation, LOD and effects |
| One-IIFE/global architecture | **Replace** | modular C# assemblies, scenes/prefabs, data assets and services |
| `localStorage` preferences | **Replace** | platform-safe settings/save system |
| Global Three.js r160 build | **Web-specific / reference only** | retain in web version |
| HTML/PWA/Replit build and lifecycle code | **Web-specific / reference only** | retain for web deployment; not target architecture |
| Browser debug hook and screenshot endpoint | **Preserve concept but redesign implementation** | Unity telemetry, test scenes, capture and profiler hooks |
| Headless web smoke/AI scripts | **Preserve concept but redesign implementation** | keep web tests and add Unity EditMode/PlayMode tests |
| `attached_assets/` screenshots | **Reference only** | not production art assets |

## Production-quality ladder

Each stage has a different definition of “done”. Later-stage polish must not be pulled forward in a way that prevents earlier gameplay proof.

### Stage 0: approve and freeze the reference

- Approve Unity, platform order and the revised foundation.
- Tag or record the web baseline.
- Capture named web runs for representative ground, jump, landing, crash and camera behaviours.
- Record initial performance/device expectations.
- Create an asset-licence register template before adopting marketplace content.

**Gate:** the behavioural reference and decision package are reproducible.

### Stage A: behavioural prototype

Goal: match or exceed the web game's soul with intentionally temporary visuals.

- Create the minimum Unity/URP project beside the untouched web version.
- Establish Input System actions with gamepad as reference and keyboard parity.
- Implement telemetry and automated pure-logic tests.
- Build a simple representative terrain segment.
- Port ground handling, natural-crest takeoff, air control and neutral settle.
- Implement Clean, Sketchy, Ugly and Wipeout plus validated fast recovery.
- Protect chase and overhead camera intent with temporary camera presentation.

**Allowed quality:** primitives, diagnostic materials, placeholder sounds and developer UI.

**Gate:** the user approves A/B feel. Do not advance because metrics alone look similar.

### Stage B: gameplay vertical slice

Goal: one complete race that demonstrates the intended final gameplay with one credible bike, rider and environment.

- One original, representative National track.
- One substantially improved bike and rider rig.
- Layered rider posture, suspension, landing and crash presentation.
- A small AI field with racing, overtaking, mistakes and reliable results.
- Complete start, race, pause, finish and replay-again flow.
- Gamepad-first HUD and camera work; keyboard complete.
- Initial soundscape and surface feedback.
- PC high-quality and Deck-balanced profiles both functional.

**Gate:** the slice is enjoyable as a small game, not merely a physics demonstration.

### Stage C: visual vertical slice

Goal: make a representative course section look and sound like a credible commercial indie release.

- Final-direction terrain materials, blending, decals and track wear.
- Art-directed lighting, shadows, sky, atmosphere and colour grading.
- Vegetation and environmental dressing with clear silhouettes and LOD.
- Signature dust, roost, landing and surface effects.
- Production-quality bike/rider material pass and animation polish.
- Distinctive menus, HUD, typography, transitions and race presentation.
- Layered bike audio, wind, impacts, ambience and initial music identity.
- Performance budgets validated on PC and Steam Deck.
- A low mobile quality prototype verifies that the architecture scales, without requiring touch completion.

**Gate:** captured footage can credibly represent Dustbowl's intended commercial quality. Fix asset-store collage, generic UI and visual noise before production.

### Stage D: production

Goal: build content through proven systems instead of reinventing foundations per track.

- Multiple visually distinct National tracks.
- Expanded AI field, difficulty and behaviour.
- Progression, event selection, records, settings and saves.
- Additional modes only after National is complete enough to support them.
- Reusable environment kits, terrain recipes and track-authoring tools.
- Content validation, automated regression tests and performance budgets.
- Final audio, music, UI and accessibility passes grow alongside content.

**Gate:** planned launch content is feature-complete and consistently polished.

### Stage E: platform optimisation and release preparation

Goal: ship each platform deliberately rather than relying on one build to fit all.

- Windows/Steam performance, packaging, achievements/store integration and QA.
- Steam Deck Proton testing, 1280×800 UI, controller glyphs, suspend/resume and default profile.
- Android profiling across representative GPU/memory tiers, thermal behaviour, package size and touch UI.
- iOS device profiling, Metal/shader validation, signing, Xcode pipeline, package size and touch UI.
- Platform-specific texture compression, shader stripping, LOD, shadows, vegetation and VFX budgets.
- Store compliance, privacy, crash reporting, accessibility and release QA.

Steam Deck work begins earlier; this stage completes certification/readiness. Mobile release follows PC when quality and controls survive realistic devices.

## Visual identity

Dustbowl should use **modern stylised realism**:

- sunlit, readable terrain with strong large-scale shapes;
- a distinctive warm mineral palette offset by bold bike/rider colours;
- dramatic skies, long atmospheric depth and carefully controlled haze;
- richly layered dirt surfaces without photoreal texture noise;
- graphic dust plumes that remain readable at speed;
- expressive rider silhouettes and exaggerated but convincing suspension motion;
- clear impacts, compression and deformation cues;
- subtle late-1990s/early-2000s racing attitude in typography, colour blocks, camera energy and music, without imitating Microsoft;
- visual clarity over maximum object density.

Distinctiveness should come from the combination of carved “playground” terrain, heroic dust silhouettes, bold rider colour coding, powerful overhead views, course-specific time-of-day/weather moods and a consistent graphic UI language. Dustbowl should be identifiable from a screenshot even without its logo.

## Asset integration rules

- Placeholders are expected in Stages A and early B.
- Gameplay code reads abstract telemetry/state and must not depend on a particular skeleton, mesh or particle prefab.
- Production models, rigs, audio and VFX must be replaceable without rewriting controller logic.
- Every third-party asset is checked for licence, URP compatibility, mobile viability, source availability, update health and visual fit.
- Generic assets may fill the world; signature assets define Dustbowl.
- Rework colour, materials, scale, collision, LOD and naming so mixed packs become one art direction.
- Avoid large framework assets that seize ownership of input, physics, cameras or save systems without a specific approved need.

## A/B behavioural protocol

For each controller milestone:

1. use the same named web course segment and equivalent camera;
2. record approach speed, takeoff speed, airtime, peak height, landing metrics, result and reset time;
3. compare neutral and corrective-input runs;
4. capture side-by-side footage where useful;
5. record every intentional difference and why it is better;
6. obtain user approval before removing compatibility code or materially changing protected boundaries.

The Unity version should eventually look dramatically better in those comparisons. Visual difference is expected; behavioural loss is not.

## Performance budgets

Budgets are established with profiling, not optimism.

- PC reference: 60 fps at the selected target resolution and quality tier.
- Steam Deck: 60 fps target where practical; a fallback lower target requires explicit approval and excellent frame pacing.
- Mobile: device tiers with separate material, shadow, foliage, VFX and resolution settings.
- Physics and race rules remain consistent across graphics tiers.
- Terrain, vegetation, AI and VFX must have explicit CPU, GPU and memory measurements before content scales.

## Major risks

| Risk | Control |
| --- | --- |
| Unity vehicle systems replace the arcade soul | custom-controller rule, A/B gate and `AGENTS.md` |
| Prototype visuals become permanent | quality ladder, visual slice gate and explicit replacement classifications |
| Asset-store collage | art bible, material/palette unification and signature custom assets |
| PC visuals cannot scale to mobile | URP, early quality tiers, LOD/data separation and mobile spike in Stage C |
| Terrain render and collision diverge | one data authority and automated agreement samples |
| Rider animation consumes disproportionate time | layered animation, limited key set, procedural rigging and specialist help where valuable |
| AI scope expands before core quality | small field in slice; full field only after systems prove out |
| Steam Deck is treated as late port | continuous device testing and Deck profile from Stage B |
| Unity package/version churn | pin LTS/versions, minimal packages and upgrade gates |
| Third-party asset abandonment/licensing | asset register, source review and replaceable integration boundaries |
| iOS build pipeline is discovered too late | document macOS/Xcode/signing need and perform an early empty-build spike |

## Rollback

The web implementation remains playable and deployable throughout. A failed Unity experiment is isolated to the target-engine directory/branch and must never require reconstructing the Replit-derived game.
