# Dustbowl game vision

Status: revised canonical product direction, pending foundation approval

Scope: defines what Dustbowl is and how product decisions are judged

Last reviewed: 2026-08-27

## One-sentence vision

Dustbowl is a polished modern arcade dirt-bike game that turns bright, dramatic outdoor landscapes into playgrounds of flowing lines, expressive bikes and riders, huge air, recoverable mistakes and funny crashes.

## Expanded ambition

Dustbowl began as a fun personal project intended for PC and phone. Its working web version proved that the central idea has real value. The ambition is now to build a proper contemporary indie game which could, if development succeeds, release commercially on Windows/Steam and later Android and iOS.

That raises the destination without changing the soul. The web/Replit implementation is a highly valuable gameplay prototype and behavioural reference. It is **not** the visual, animation, audio, interface, content or production-quality ceiling.

The project pursues two objectives simultaneously:

1. preserve the distinctive light, springy, forgiving arcade handling and terrain play;
2. raise every production discipline until Dustbowl looks, sounds and presents itself like a credible commercial game.

## Identity

Dustbowl is inspired by the *feel* and freedom of late-1990s open-terrain motocross games, especially *Microsoft Motocross Madness* (1998), but it is an original game. It must not copy original assets, branding, proprietary code, course layouts or other protectable expression. Historical games are references for emotional targets and useful genre ideas, not specifications to reproduce.

The current Three.js game has already established a distinct identity worth preserving:

- authored outdoor National courses cut into procedural desert terrain;
- accessible auto-throttle alongside manual throttle;
- a strong overhead camera as well as chase views;
- warm desert palettes, dust haze and a chunky late-1990s-inspired interface;
- a whole race playable with keyboard or touch;
- terrain features that create air naturally instead of relying only on freestanding ramps.

## Player promise

Within the first minute, the player should understand that Dustbowl wants them to go fast and try things. The bike should feel light, springy and responsive. A rise in the landscape should look like an invitation. A slightly bad approach should create a wobble, a sideways landing or a heroic save before it becomes a wipeout. A wipeout should be entertaining and should return the player to control before frustration has time to replace laughter.

The player is not expected to learn real motorcycle technique, manage component damage or fear experimentation.

## Design pillars

### 1. Arcade first

Controls, camera, terrain, physics and rules exist to create fun and readability. Realism remains only where it improves those qualities. Realistic behaviour that makes the bike heavy, fragile, obscure or slow to recover is not inherently an improvement.

### 2. Momentum and flow

The satisfying loop is accelerate, read the land, choose a line, launch, adjust, land and continue. The game should avoid needless interruptions. Corners and loose surfaces can demand attention, but the bike should retain a lively sense of forward momentum.

### 3. The terrain is a playground

Terrain is gameplay, not background scenery. Rises, bowls, berms, ridges, whoops, tabletops, drops and alternative lines should continually suggest playful possibilities. Major jumps should sit on rewarding lines rather than behave as slower obstacles that optimal racers avoid.

### 4. Drama before failure

Most mistakes should pass through readable, recoverable states: drift, wobble, compression, bounce, speed scrub, awkward pitch or an ugly landing. Wipeout is the final step in an escalation, not the default response to small errors.

### 5. Low punishment, high spectacle

Crashes are slapstick punctuation. They should produce a clear tumble, dust, camera reaction and concise callout, then restore play quickly. There is no persistent mechanical damage. The race clock may continue and momentum may be lost, but the game should not pile on menus, repair states or long animations.

### 6. Easy to start, rewarding to master

Auto-throttle, generous air control and forgiving landings make the game immediately playable. Mastery comes from carrying speed, choosing lines, controlling attitude, landing cleanly and racing consistently, not from wrestling with basic stability.

### 7. Commercial-quality presentation

Environments, terrain, materials, lighting, sky, vegetation, dust, bikes, riders, animation, audio, music, UI, cameras, AI and race presentation must eventually form one polished experience. Weak prototype rendering must not be preserved in the name of fidelity. Every effect should improve speed, terrain readability, physical expression or personality.

### 8. Scalable production

Windows/Steam is the primary development platform, Steam Deck is considered throughout, and Android/iOS remain intended later targets. Art, shaders, terrain, VFX, UI and content systems should support quality tiers rather than separate gameplay implementations.

## Landing hierarchy

Every meaningful landing belongs to one of four player-readable outcomes:

1. **Clean**: aligned, composed and momentum-preserving.
2. **Sketchy**: visibly imperfect but controlled; a small wobble, compression or speed loss.
3. **Ugly**: a dramatic save with substantial scrub, bounce, yaw or instability, but the player remains in control.
4. **Wipeout**: the bike and rider tumble; recovery is automatic and fast.

This hierarchy replaces a simple success/failure mindset. The exact numeric thresholds are tuning values, not the vision itself.

## Initial long-term mode direction

Development should deepen National before spreading effort across multiple shallow modes.

- **National**: the foundation. Outdoor circuits, multiple courses, AI fields, starts, positions and results.
- **Supercross**: compact stadium tracks with dense rhythm sections and close racing.
- **Baja**: wide-open desert, quarry and canyon events, including point-to-point possibilities.
- **Stunt Park**: big-air and trick play that can reuse the retained ramp and score concepts.
- **Track editor**: a later possibility after the course representation and validation rules are stable.

The current web build already contains four National courses and seven AI riders. These are reference implementations, not reasons to skip a feel-first vertical slice in the target engine.

## Original visual identity

Dustbowl should pursue **modern stylised realism**, not photorealism:

- bright outdoor environments with bold, readable landforms;
- warm mineral colours, sunlit dust and strong course-specific moods;
- dramatic skies, long atmospheric depth and carefully controlled haze;
- layered terrain materials that show packed lines, loose dirt, braking wear and landing zones;
- excellent dust and roost silhouettes which remain readable at speed;
- expressive riders and bikes with exaggerated but convincing suspension and impact response;
- strong colour coding and silhouettes for opponents;
- energetic cameras and graphic race presentation;
- a touch of late-1990s/early-2000s arcade-racing attitude in typography, music and colour blocking without copying Microsoft.

The distinctive combination should be carved playground terrain, heroic dust, expressive riders, bold overhead views and a recognisable graphic interface. A screenshot should read as Dustbowl even without the logo.

Visual clarity takes priority over excessive micro-detail. Purchased and free assets may accelerate production, but they must be art-directed, recoloured, reworked and performance-scaled so the world does not look like unrelated asset packs.

## First target experience

Before full migration, the target engine must prove one bike on one representative desert test course with:

- acceleration, braking and responsive steering;
- terrain-following ground motion;
- natural and authored takeoffs;
- generous pitch and limited whip control in the air;
- all four landing outcomes;
- funny tumble and automatic reset;
- approximately 1.25–2.0 seconds from wipeout to restored control;
- chase and overhead cameras;
- instrumentation that makes comparison with the web build possible.

Gamepad is the primary feel-tuning input. Keyboard remains fully supported. Touch follows once the core game is established.

This first test may use placeholders. It proves behaviour, not final appearance. It must be followed by a gameplay vertical slice and then a visual vertical slice before content production.

## Production-quality destination

The realistic target is a focused, polished stylised indie racing game with a small number of memorable environments, not a miniature AAA open-world motocross simulator. The quality bar is cohesion and intention:

- terrain that is enjoyable to read at speed;
- lighting and atmosphere strong enough to make each course memorable;
- one excellent hero bike and rider before a large roster;
- layered animation that sells control, suspension, landings and crashes;
- signature dust, surface and audio feedback;
- modern gamepad-first UI and race presentation;
- reliable 60 fps targets and quality tiers.

AI-assisted development, marketplace assets and procedural tools can extend a small team, but they do not replace art direction, specialist animation/audio work, device testing or disciplined scope.

## Non-goals for the migration foundation

- realistic multi-body motorcycle simulation;
- persistent mechanical damage, fuel, maintenance or repair;
- copying *Motocross Madness* content;
- immediate parity with every web feature;
- deleting or freezing the web version;
- selecting an engine feature merely because it is more physically realistic.
- preserving browser-prototype graphics, models, UI, animation, effects or architecture as the visual target;
- photoreal AAA scope, licensed bikes, huge crowds or online multiplayer in the initial release.

## Decision test

When a proposed change is disputed, ask in this order:

1. Does it make the bike more enjoyable and readable moment to moment?
2. Does it preserve momentum, experimentation and quick recovery?
3. Does it make terrain more playful or racing more interesting?
4. Does it strengthen Dustbowl's own identity?
5. Only then: is it more realistic?

If realism conflicts with the first four answers, realism loses.
