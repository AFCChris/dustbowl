# Dustbowl Unity foundation

This Unity 6.3 LTS project contains the approved Stage 1 foundation, Stage 2
course/terrain contract and the Stage 3 arcade-controller BehaviourLab slice.
Stage 3 uses development-only tuning, telemetry, terrain strips and placeholder
visuals. It intentionally contains no production art, AI, complete course port,
four-tier landing system or realistic vehicle simulation.

## Editor and packages

- Unity `6000.3.23f1`
- Universal Render Pipeline `17.3.0`
- Input System `1.20.0`
- Unity Test Framework `1.6.0`

The development target is Windows x86-64. The architecture keeps Steam Deck and
future mobile input/rendering constraints in view, but Stage 1 adds neither touch
controls nor platform-specific gameplay.

## Behaviour lab

Open `Assets/Dustbowl/Scenes/Dustbowl_BehaviourLab.unity`. The scene contains a
code-controlled placeholder bike, semantic input, telemetry, simulation-clock and
three playable camera modes. It contains the Stage 2 authored-tabletop segment and
an unfeatured rounded-crest test strip. Each strip's render mesh and collision
mesh are the same asset generated from `ICourseSurface`; the development probe
and gizmos expose the sampled contract.

The fixed simulation interval is 1/60 second. Gamepad is the reference input;
keyboard bindings map to the same semantic actions.

Run the Edit Mode and Play Mode suites in Unity Test Runner. The menu command
`Dustbowl > Stage 3 > Verify arcade controller` performs the complete scripted
foundation/course/controller verification. `Dustbowl > Stage 3 > Build Windows
development player` creates a review build outside the Unity project by default.

See `../UNITY_COURSE_TERRAIN_SPEC.md` for the course contract and
`../UNITY_CONTROLLER_STAGE3.md` for controls, source mapping, tuning,
verification, telemetry and known limitations.
