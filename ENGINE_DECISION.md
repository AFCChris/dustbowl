# Dustbowl engine decision

Status: approved engine decision

Decision date: 2026-08-27

## Recommendation

Use **Unity 6.3 LTS with C# and the Universal Render Pipeline (URP)** for the long-term Dustbowl version. Keep the existing Three.js game available and maintained as the behavioural reference during development.

This supersedes the earlier provisional Godot recommendation. The revised foundation package and Unity migration are approved.

## Why the recommendation changed

The earlier decision correctly optimised for a compact custom controller, procedural terrain and a small AI-assisted development workflow. It treated Dustbowl primarily as a careful behavioural migration of a successful browser prototype.

The clarified goal is larger: preserve the prototype's arcade soul while building a visually impressive, polished modern indie game intended first for Windows/Steam, with Steam Deck compatibility and future Android and iOS releases. The engine must support not only the custom controller but also a demanding outdoor-content and production pipeline.

For that destination, Unity offers the better overall route. Its advantage is not that it should simulate the motorcycle. Its advantage is the surrounding production leverage: mature terrain and vegetation workflows, URP quality scaling, animation and procedural rigging, cameras, particles, profiling, build profiles and a much larger commercial/free asset ecosystem.

## Why Unity fits the actual target game

### Preserve the custom arcade controller

Unity does not require Dustbowl to use `WheelCollider`, a jointed rigid-body motorcycle or a marketplace vehicle controller. The player bike should remain a code-controlled arcade body using explicit state, physics queries and authored response curves. Unity's fixed update model and test framework can support repeatable controller behaviour while rendering, animation and crash presentation remain separate consumers.

### Strong outdoor-production path

Unity has a built-in Terrain workflow and URP Terrain Lit materials. Unity 6.3 also supports custom terrain shaders and materials through Shader Graph. This does not remove the need for Dustbowl's custom course shaping and shared render/collision authority, but it provides a mature base for terrain materials, vegetation, LOD, lighting and authoring tools.

### One scalable rendering strategy

URP is the recommended starting pipeline because it spans the intended hardware range better than HDRP: strong PC presentation, practical Steam Deck scaling and a credible mobile path. Separate quality tiers can change shadow distance/resolution, vegetation density, particle budgets, texture resolution, post-processing and terrain detail without forking gameplay.

### Animation, cameras and presentation

Unity's Animator, Animation Rigging package and constraint workflows support the layered solution Dustbowl needs: authored rider clips plus procedural hands/feet, counter-lean, suspension response, landings and impacts. Cinemachine provides a mature basis for chase, close, overhead, replay and race-presentation cameras. Ragdoll or hybrid crash presentation can be isolated from the authoritative arcade controller.

### Inputs and platforms

Unity's Input System can map gamepad, keyboard and later touch through one action layer. Build Profiles support platform-specific configurations, which suits a PC-first game that later needs Deck and mobile quality variants.

### Asset and specialist ecosystem

Unity's Asset Store materially improves the odds that a small/AI-assisted team can reach release quality. Terrain tools, vegetation, rocks, sky/weather, VFX utilities, UI helpers, audio tools, generic animation libraries and profiling aids can be evaluated instead of built from scratch. The hero bike, rider identity, handling, track design and final visual cohesion still need custom direction.

## Godot reconsidered fairly

Godot's visual ceiling is sufficient for a polished stylised 3D indie game. Godot 4.7 provides modern 3D lighting, shadows, animation and GPU particles, and projects such as the solo-developed *Road to Vostok* demonstrate that it can render convincing semi-detailed environments. The Godot showcase contains desktop and mobile releases, and tools such as Terrain3D provide large heightmap terrain, LOD, texture painting and foliage.

The concern is not that Godot would look bad. It is that Dustbowl would assume more tooling and integration risk. Godot's terrain path depends more heavily on community plugins or custom systems, its ready-made production asset ecosystem is smaller, and the project would likely need more bespoke work to reach the same outdoor-content, animation and multiplatform pipeline. The *Road to Vostok* developer's own account notes that large environments are possible but demand deep optimisation knowledge and custom tools.

Godot remains a credible fallback if open-source control becomes more important than production leverage, or if a Unity-specific blocker appears. It is no longer the primary recommendation.

## Dustbowl-specific decision matrix

Scores reflect the clarified project, not generic engine quality.

| Criterion | Weight | Unity | Godot | Reason |
| --- | ---: | ---: | ---: | --- |
| Custom arcade controller | 16% | 5 | 5 | both support explicit code-controlled behaviour |
| Outdoor terrain and vegetation pipeline | 16% | 5 | 3.5 | Unity's built-in terrain and ecosystem reduce production risk |
| Polished contemporary 3D presentation | 12% | 5 | 4 | both capable; Unity has the broader proven pipeline/tooling |
| Animation, procedural rigging and ragdoll workflow | 10% | 5 | 3.5 | Unity has mature integrated and marketplace options |
| PC/Steam and Steam Deck | 10% | 5 | 5 | both viable; Windows through Proton is practical on Deck |
| Android and iOS path | 10% | 5 | 4 | both export; Unity has broader mobile production history/tooling |
| Asset ecosystem | 10% | 5 | 3 | major Unity advantage for a small team |
| Codex and maintainability | 8% | 4 | 5 | Godot is lighter; Unity C# is highly toolable but project hygiene matters |
| Profiling and optimisation | 5% | 5 | 4 | Unity provides a deep mature profiling stack |
| Licensing/control | 3% | 3 | 5 | Godot is MIT/open source; Unity is commercial and terms must be monitored |
| **Weighted result** | **100%** | **4.86/5** | **4.14/5** | Unity leads for the expanded destination |

## Approved technical direction

- **Editor:** pin Unity 6.3 LTS to a specific patch version when the project is created.
- **Language:** C#.
- **Render pipeline:** URP, not HDRP, to support PC quality plus Deck/mobile scaling.
- **Primary development platform:** Windows PC.
- **Reference input:** gamepad; keyboard fully supported; touch later.
- **Player controller:** custom code-controlled arcade controller. No automatic `WheelCollider` or multi-rigidbody replacement.
- **Physics cadence:** begin at 60 fixed ticks per second and profile before changing.
- **Camera:** Cinemachine or an equally testable custom layer, with chase and overhead protected.
- **Animation:** authored base clips plus Animation Rigging/procedural layers driven by controller telemetry.
- **Terrain:** one authoritative course/height representation feeding render, collision and track queries; use engine/marketplace terrain tools only where they preserve that contract.
- **Platform profiles:** PC high, Steam Deck balanced, mobile scalable. They share gameplay rules.
- **Online multiplayer:** out of initial release scope.

## Platform strategy

1. Windows PC and Steam are the primary development and first-release targets.
2. Steam Deck is tested throughout, initially using the Windows build through Proton unless native Linux produces a demonstrated benefit.
3. Android and iOS are planned later release targets.
4. Mobile constraints influence data, shaders, LOD, draw calls, memory and input architecture from the beginning, but do not hold back the first PC feel prototype.
5. Gamepad is the feel-tuning reference. Keyboard remains complete. Touch follows the established core.
6. Target 60 fps where practical, with measured platform-specific quality tiers.

Steam Deck review requires more than frame rate: controller glyphs, automatic text entry where needed, readable UI at 1280×800, no desktop-only launcher and sensible default settings. Valve states that Windows builds can run through Proton and specifies a minimum nine-pixel character height at 1280×800 for Deck review.

An eventual iOS release will require Apple signing and an appropriate macOS/Xcode build path even if most development occurs on Windows.

## Achievable quality bar

### Prototype quality

The current web build and the first engine port may use primitive models, simple materials and diagnostic UI. This level is valid for behaviour testing only and is not a product target.

### Achievable indie release quality

Dustbowl should target a cohesive modern stylised-realism finish: excellent terrain readability, deliberate palettes, convincing light and atmosphere, responsive animation, dense but controlled dust, strong cameras, distinctive UI/audio and a small number of highly polished environments. The relevant benchmark is not asset count. It is whether every visible system appears intentional and supports speed and play.

Two useful proofs of scale are:

- *Lonely Mountains: Downhill*, presented by its two-person studio as a Unity game with custom bike physics, seamless outdoor tracks and low-poly art enhanced by modern lighting and post effects. Dustbowl should not copy it, but it demonstrates the power of a narrow visual identity plus custom handling.
- *Road to Vostok*, a solo-developed Godot project with convincing semi-detailed environments, demonstrates that small-team visual quality is possible in either engine and that art direction and optimisation matter more than engine marketing.

Unity's broader 2025 game showcase and Godot's annual showreels reinforce that both engines can ship distinctive commercial work. Dustbowl's chosen target is a focused, polished indie racing game, not a photoreal open-world simulator.

### Unrealistic expectation

AAA motocross presentation across many biomes, photoreal characters, cinematic damage, huge crowds, licensed vehicles, extensive modes and simultaneous PC/mobile parity is not realistic for a small AI-assisted team without major funding and specialist art, animation, audio and QA support. AI can accelerate iteration and tooling; it does not eliminate art direction, asset integration, device testing or production time.

### Practical production mix

| Source | Practical contribution | Boundary |
| --- | --- | --- |
| Unity-native systems | URP lighting/materials, Terrain foundation, particles, cameras, animation layers, input, UI, audio, profiling and builds | systems do not create a coherent art direction by themselves |
| Purchased/free assets | vegetation, rocks, props, base materials, skies, generic animation, sound libraries and production utilities | audit licence/URP/mobile support and transform them into one style |
| Procedural tools | terrain variation, track dressing, vegetation scatter, material masks, decals, LOD generation and validation | authored hero lines and landmarks still need design judgement |
| AI-assisted development | C# scaffolding, editor tools, tests, data conversion, parameter exploration, documentation, repetitive integration and QA support | generated output requires review; AI does not replace final art direction, animation, audio mix or device testing |
| Custom work | controller, hero bike/rider, signature tracks, UI identity, dust behaviour, audio character and final cohesion | focus scarce specialist time here rather than rebuilding generic systems |

## Asset strategy

### Probably custom or heavily customised

- player bike silhouette, materials and readable moving parts;
- rider identity, clothing and key animation set;
- arcade controller and landing/crash hierarchy;
- track layouts, jump shaping and track visual identities;
- terrain/course integration tools;
- Dustbowl UI language, logos and race presentation;
- signature dust behaviour and surface feedback;
- engine/bike audio character and final mix;
- opponent racing logic and gameplay tuning.

### Sensible marketplace/free starting points

- rocks, vegetation, background props and generic environmental dressing;
- base terrain materials and shader utilities;
- sky, cloud, weather and general post-processing tools;
- generic humanoid locomotion, reaction and ragdoll foundations;
- generic particles, decals and audio utility systems;
- UI icons, accessibility helpers and development tools;
- profiling, LOD and vegetation-placement utilities.

Purchased assets must be art-directed, retextured, recoloured, combined and performance-audited so the result does not look like an asset-store collage. Licences and mobile/URP support must be checked before adoption.

## Current references

- Unity 6.3 LTS and support window: <https://unity.com/releases/unity-6>
- Unity 6.3 terrain Shader Graph additions: <https://docs.unity3d.com/6000.5/Documentation/Manual/WhatsNewUnity63.html>
- Unity URP terrain material: <https://docs.unity3d.com/6000.3/Documentation/Manual/urp/shader-terrain-lit.html>
- Unity Input System: <https://docs.unity3d.com/6000.5/Documentation/Manual/com.unity.inputsystem.html>
- Unity Cinemachine: <https://docs.unity3d.com/6000.5/Documentation/Manual/com.unity.cinemachine.html>
- Unity Animation Rigging: <https://docs.unity3d.com/Packages/com.unity.animation.rigging@latest/index.html>
- Unity Android development and optimisation: <https://docs.unity3d.com/6000.3/Documentation/Manual/android-developing.html>
- Unity Personal's current USD 200,000 revenue/funding threshold: <https://unity.com/products/pricing-updates>
- Godot 4.7 feature overview: <https://docs.godotengine.org/en/4.7/about/list_of_features.html>
- Godot Terrain3D asset listing: <https://godotengine.org/asset-library/asset/3892>
- Road to Vostok developer's Godot assessment: <https://www.patreon.com/roadtovostok/posts/godot-port-1-90611929>
- Lonely Mountains development showcase: <https://discussions.unity.com/t/lonely-mountains-downhill-a-downhill-mountain-biking-game/665382>
- Steam Deck compatibility requirements: <https://partner.steamgames.com/doc/steamdeck/compat>

## Reconsideration triggers

Reopen the decision only for concrete evidence such as:

- a Unity licence/terms change that materially harms the project;
- a required tool or platform failing an early Unity spike;
- a specialist team joining with a demonstrably better Godot pipeline;
- Unity URP failing the measured PC/Deck/mobile quality-scaling prototype;
- scope changing back to a lightweight web-first personal game.
