# Dustbowl Unity foundation

This Unity 6.3 LTS project is the Stage 1 foundation for the behavioural
migration. It intentionally contains no bike controller, gameplay tuning,
production art, AI or course port.

## Editor and packages

- Unity `6000.3.23f1`
- Universal Render Pipeline `17.3.0`
- Input System `1.20.0`
- Unity Test Framework `1.6.0`

The development target is Windows x86-64. The architecture keeps Steam Deck and
future mobile input/rendering constraints in view, but Stage 1 adds neither touch
controls nor platform-specific gameplay.

## Behaviour lab

Open `Assets/Dustbowl/Scenes/Dustbowl_BehaviourLab.unity`. The scene provides
only a primitive ground reference, placeholder bike mount, semantic input,
telemetry, simulation-clock and camera-mode infrastructure.

The fixed simulation interval is 1/60 second. Gamepad is the reference input;
keyboard bindings map to the same semantic actions.

Run the Edit Mode suite in Unity Test Runner to verify the foundation contract.
