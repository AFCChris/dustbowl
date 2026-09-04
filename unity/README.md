# Dustbowl Unity foundation

This Unity 6.3 LTS project contains the approved Stage 1 foundation and the
Stage 2 course/terrain contract. It intentionally contains no bike controller,
gameplay tuning, production art, AI or complete course port.

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
placeholder bike mount, semantic input, telemetry, simulation-clock and
camera-mode infrastructure plus one Dustbowl Flats reference segment. Its
render mesh and collision mesh are the same asset generated from
`ICourseSurface`; the development probe and gizmos expose the sampled contract.

The fixed simulation interval is 1/60 second. Gamepad is the reference input;
keyboard bindings map to the same semantic actions.

Run the Edit Mode suite in Unity Test Runner to verify the foundation and
course/terrain contracts. See `../UNITY_COURSE_TERRAIN_SPEC.md` for source
mapping, approximations and Stage 3 constraints.
