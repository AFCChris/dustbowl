# Unity course and terrain contract

Status: Stage 2 reference implementation for Unity 6.3 LTS

Source baseline: `web-v0.18-behavioural-baseline` at `80e76510f9a8fd34cb213e36fd54cba227a31bce`

Representative slice: Dustbowl Flats, source distance `331.258291`–`452.969529` metres, containing the complete section-4 tabletop and its approach and landing.

## Authority rule

`CourseDefinition` and `CourseSurface` are the single data and sampling authority. The scene does not use Unity Terrain, a hand-shaped collision proxy or a separate render-height source.

```text
tagged web constants and baked line samples
                    |
                    v
            CourseDefinition asset
                    |
                    v
        CourseSurface / ICourseSurface
          |         |          |
          v         v          v
       queries   render mesh  collider mesh
                              (same Mesh asset)
```

The generated mesh stores vertices returned by `ICourseSurface.SampleHeight`. The scene's `MeshFilter` and `MeshCollider` reference the same persistent mesh asset. Stage 3 controller code must query `ICourseSurface`; it must not infer a second gameplay surface from render geometry or introduce another height function.

## Runtime contract

`ICourseSurface` provides:

- height and normalized terrain normal at a world position;
- a complete ground sample containing point, normal, slope, surface type and track blend;
- nearest course position, distance along, signed lateral offset and distance from centre;
- a local course frame containing centre, forward, right and surface normal;
- graded road height, visible centreline height and bank slope;
- feature lookup by distance along;
- inert racing-line offset and recommended-speed metadata reserved for later AI work.

`CourseDefinition` provides stable course/segment identity, source provenance, source seed, source lap length, ordered line samples, feature metadata and spawn/reset hints. The Stage 2 spawn hint marks a known-safe segment entry; it is data only and implements no reset behaviour.

Surface types are `PackedTrack`, `Berm`, `LooseSand` and `NaturalSand`. These are classifications for later controller/VFX/audio consumers, not Stage 2 handling rules.

## Tagged web implementation mapped

Line references below are to `src/game.js` at the tagged baseline, not current mutable line numbers.

- Dustbowl Flats source definition, seed, layout and sections: lines 73–96.
- Representative tabletop: line 85, `at: 0.40`, `len: 36`, `height: 3.6` in section 4 (zero-based).
- Procedural noise and FBM: lines 28–52; terrain base height: lines 227–237.
- Track constants: lines 245–253: half-width `8.5`, berm width `7`, berm height `4.2`, track cut `1.6`, blend width `12`, maximum bank `0.42` and field grid `4`.
- Nearest track, along and signed lateral calculation: lines 259–318. Positive lateral remains the source implementation's right side of travel.
- Track blend: lines 321–326.
- Feature profiles and lateral fade: lines 328–359. The tabletop keeps the source smoothstep faces at `0`–`0.26` and `0.74`–`1`.
- Combined natural/graded/feature/berm/blend surface: lines 362–381.
- Terrain normal: lines 384–390, using central differences at `0.7` metres.
- Source spline bake, `2.2` metre target spacing, nine grade-smoothing passes, bank derivation/smoothing and feature placement: lines 457–605.
- Web render terrain sampling: lines 663–683; feature detail strips sampling the same terrain function: lines 746–790.

The frozen slice contains 49 consecutive tagged centreline samples, including grade, source distance, bank and the player-visible AI speed scale. The source lap length (`1510.540817` metres) remains provenance only; this Stage 2 scene does not implement a lap.

## Representative segment

The Flats tabletop slice was chosen because a single compact area exercises:

- graded track over procedural natural terrain;
- changing bank and signed lateral samples;
- packed track, berm, blend/loose-sand and natural-sand bands;
- a complete authored feature with approach, takeoff face, deck, landing face and run-out;
- meaningful geometry for later ground-following, recent-climb-rate takeoff and landing comparisons.

The segment is intentionally not a complete race course. It adds no checkpoints, laps, AI racers, controller, crash rules or camera controller.

## Deliberate approximations

- JavaScript double-precision outputs are stored as Unity floats. Tagged height checkpoints are protected with a `0.025` metre tolerance.
- The 49 tagged web samples are joined piecewise-linearly instead of porting the complete closed Hermite course bake. This preserves the reviewed slice without prematurely importing the whole course system.
- The development mesh uses approximately one-metre spacing along and one-metre spacing laterally over a 64-metre-wide strip. Its collision surface is therefore the linear interpolation of authoritative samples, while direct queries remain continuous.
- The source's nearest-track acceleration field and four-segment refinement are replaced by a deterministic exhaustive search over this small slice. Full-course scaling will require a spatial index without changing query results.
- The scene material and primitive bike marker remain explicit placeholders.

## Natural-crest takeoff risk

The chosen slice is dominated by an authored tabletop. It proves feature and terrain authority, but it does not by itself validate a representative unfeatured rounded crest. The eventual full-course bake must preserve the source's grade smoothing before Stage 3 takeoff work is approved. Controllers must derive recent climb rate from sequential authoritative height/contact samples; a mesh triangle normal is not a substitute. Mesh resolution and collider interpolation can change a crest derivative, so a dedicated tagged natural-crest comparison must be added when the one-bike feel slice reaches that behaviour.

## Debug and verification

`CourseSurfaceDebugGizmos` draws the centreline, track boundaries, course frames and feature endpoints in the Scene view. `CourseSurfaceProbe` is development-only and reports world position, sampled height/normal/slope, along, lateral, surface class, feature and frame vectors. Neither component affects simulation state.

Edit Mode tests protect deterministic facts:

- tagged reference heights;
- normalized upward normals;
- centre and signed-lateral consistency;
- surface-band classification;
- tabletop metadata;
- deterministic generation;
- mesh vertices agreeing with sampled height;
- renderer and collider sharing one mesh;
- absence of `WheelCollider`, rigid-body bike physics and the Unity vehicles module.

These tests do not claim subjective feel parity. Stage 3 still requires user A/B play judgement against the web reference.

## Scaling rules

Future course work should preserve the public query contract while replacing the small arrays with chunked data and a deterministic spatial index. Render and collision chunks must always be generated from the same course authority and source revision. Additional AI lines, reset points and authored features belong in course data, not in controller conditionals. Full courses must add validation for continuity, closed-loop along distance, checkpoint ordering, safe reset hints and render/collider chunk seams before content scales broadly.
