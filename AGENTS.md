# Dustbowl agent instructions

## Read order

Before changing the project, read:

1. `GAME_VISION.md`
2. `FEEL_SPEC.md`
3. `PHYSICS_REFERENCE.md`
4. `CRASH_AND_RESET_SPEC.md`
5. `ENGINE_DECISION.md`
6. `MIGRATION_PLAN.md`
7. `CODEX_HANDOFF.md`
8. `HANDOFF.md` for detailed legacy-web knowledge

Direct user instructions override these documents. If documents disagree, follow the authority order in `CODEX_HANDOFF.md` and report the conflict.

## Prime directive

Dustbowl is an arcade game. Do not automatically replace its custom bike behaviour with realistic motorcycle physics, a rigid-body assembly, wheel colliders, joints or a third-party vehicle controller. “More realistic” is not evidence that a change is better.

The required feel is light, springy, fast and forgiving, with big air, generous mid-air correction, dramatic recoverable mistakes, funny wipeouts and very fast return to play. Realism remains only when it improves fun, control or readability.

## Production-quality directive

The web build is a behavioural reference, not a visual ceiling.

- Do not mistake prototype graphics for the intended visual style.
- Do not preserve weak rendering, UI, models, animation, effects or audio merely for fidelity to the web build.
- Behavioural parity and visual parity are different things.
- The target version should ultimately be a substantial audiovisual and production-quality upgrade.
- Use temporary placeholders during behavioural work.
- Keep gameplay code independent of specific meshes, rigs, materials, particles, audio clips and UI prefabs so production assets can replace them without controller rewrites.
- Target modern stylised realism: bright readable terrain, dramatic atmosphere, strong dust, expressive riders/bikes, convincing exaggerated suspension and clear speed.
- Art-direct third-party assets into one Dustbowl identity; do not ship an asset-store collage.

## Approval gates

- Do not begin engine migration until the user approves the migration package and engine choice.
- Do not remove, relocate or rewrite the current web version without explicit approval.
- Do not port all courses, full AI or broad production content before the one-bike/one-course feel slice passes user A/B review.
- After behaviour approval, require a gameplay vertical slice and then a visual vertical slice before production.
- Ask before changing target engine, language, pinned engine version or first-platform scope.

## Web reference rules

- Treat the Three.js implementation as valuable reference material.
- Never edit generated `dist/` or `dustbowl98.html`; change `src/` and rebuild.
- Preserve the web version as a runnable A/B baseline during migration.
- For web changes, follow legacy invariants in `HANDOFF.md`, including `terrainH`, `trackProfile`, climb-rate takeoff, full-frame substeps, lap sector gating and independent audio shutdown paths.
- Run `npm run build`, `node tools/smoke.js` and `node tools/test-ai.js` after relevant web changes.

## Target-controller rules

- The approved target, if the foundation is accepted, is Unity 6.3 LTS, C# and URP.
- Start with a code-controlled player body and explicit state.
- Do not begin with Unity `WheelCollider`, a jointed multi-rigidbody motorcycle or a marketplace vehicle controller.
- Render and collision terrain must derive from the same course/height data.
- Port the existing outer feel boundaries before experimenting.
- Keep visual suspension, rider animation and camera effects out of authoritative handling unless a measured, approved feel change requires otherwise.
- Implement Clean → Sketchy → Ugly → Wipeout as one shared landing result used by physics, animation, audio, camera and UI.
- Restore useful control approximately 1.25–2.0 seconds after wipeout.
- Never add persistent mechanical damage to the core game without explicit user approval.

## Platform and input rules

- Windows/Steam is primary.
- Test Steam Deck throughout; do not treat it as a late port.
- Mobile constraints influence architecture, LOD and asset budgets from the beginning, but touch and intensive mobile optimisation follow the PC core.
- Gamepad is the reference input for feel tuning.
- Keyboard remains fully supported.
- Touch maps to the same semantic actions later.
- Online multiplayer is outside initial scope.
- Target 60 fps where practical; graphics tiers must not change gameplay rules.

## Verification

- Compare target behaviour with named runs in the web build.
- Record approach speed, takeoff, airtime, landing metrics/outcome and reset time.
- Test both neutral and corrective air input.
- Add headless tests for course validation, lap gating, landing classification and lifecycle cancellation.
- Do not claim feel parity from automated tests alone; user play judgement is a release gate.
- Do not claim production quality from controller parity; the visual vertical slice has its own approval gate.
- Profile terrain, vegetation, AI, animation and VFX on target hardware before scaling content.

## Scope and originality

- Dustbowl may be inspired by the feel of late-1990s motocross games, but must remain original.
- Do not copy *Motocross Madness* assets, branding, proprietary code, course layouts or protectable content.
- Preserve unrelated user changes and avoid broad repository restructuring.
- Explain any intentional deviation from the canonical specifications before or with the change.
