# School connector presentation — 2026-10-24

The supplied gameplay screenshots record the red wall marker, a fluorescent
housing intersecting the gate lintel and the empty extended connector legs.
These are reference inputs; the game does not load them.

`SchoolExitSign` reuses the preserved generated `SchoolProgressClock/clock-visual`
enclosure, its two shared immutable LOD meshes and PBR maps. The original model,
input, job record and packed editable Blender file remain in
`SourceArt/SchoolProgressClock`. The enclosure is fitted to .96 × .36 × .12 m;
the change replaces its display with two native vector-mesh diffusers and
running-person/direction artwork, bundled Korean lettering, two suspension rods
and a short (.18 intensity / 1.65 m) green spill. No new base model was generated.
The readable faces are independent of font support for symbol glyphs. The sign
is environmental guidance and does not add an exit interaction or clock logic.

`SchoolLoopArchitecture` moves the full gate-side lamp to x=-6.8 m, clears the
beam and stored lock, rotates the vertical-leg fixtures along the route, and
continues the actual damaged-panel roof construction into the new bends.
`SchoolStructuralDecay` extends the existing CC0 acquired rubble/aggregate scans,
raised paint fractures, thick plaster debris and exposed ceiling lath into both
legs. Their original scans, attribution, editable files and derivation tools
remain in `SourceArt/SchoolHorror` and its referenced third-party directories.
`SchoolDecayDressing` reuses the authored archive shelves, cabinets and classroom
chairs, printed Korean notices and torn paper. Small transparent water films
use the existing school PBR normal/mask maps and production planar reflection.
The deterministic placement and native geometry are editable in those sources.

All acquired and generated meshes remain shared; generated fragments are static
and use the existing spatial batching. Furniture stays at the walls, the centre
route remains clear, and cosmetic rubble/paper/water add no colliders. No common
render pipeline settings, exposure, traversal rules or save version change.

Run `python HappyToyV2/Tools/quality/run_feature_tests.py --profile school-connectors`
from the repository root. This includes the existing loop/clock/save/normal-camera
brightness/gate checks, real walking through both opaque bends, plus fixture-beam
clearance and the new presentation checks. Set
`HAPPYTOY_SCHOOL_CONNECTOR_REVIEW_DIR` to a directory under `HappyToyV2/Builds` to
save four production-camera review frames. Automatic tests use the project's
quiet verification guard; `--no-desktop-interaction` also isolates test input.

CLI authentication on this machine was separately completed using an owner-only
password input dialog; `capabilities` and `doctor` returned success and `ready`.
Only the normal CLI session is retained outside the repository. Credentials and
session cookies are not source assets and are not part of this handoff.
