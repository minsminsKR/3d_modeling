# Gate-free Korean wall section

This section derives only from the existing designated-modeling-service wall
`../KoreanWallCandidate/` (request `1c7bb725-dc4b-47d2-acd9-791902190e58`,
job `b866f8f9-a828-4469-8334-a24c8321745c`). The original generation input,
GLB, textures and provenance remain there. On 2026-11-12 the original job was
rotated in the designated web 3D viewer to inspect its front, side and eaves.

`prepare_wall_segment.py` imports the reviewed runtime FBX
`Assets/Resources/AlleyKoreanWall/geometry.fbx` (SHA-256
`682638bc6d7ca5715ca3fa3796fe86d86722ab99db0afcee72f7dfe78dab691b`),
bakes its transform, clips the gate-free interval x=-1.25..-0.25, recentres it,
preserves its UV atlas, and exports `wall-segment.fbx` in the same Resources
folder. It does not regenerate or overwrite the original. Blender 4.2.23 CPU
rendering was used to compare the retained and cut-away sections. The packed,
editable `WallSectionEditable.blend` has 22,226 triangles and one UV layer.

From the project root, regenerate with Blender 4.2.23:

```
blender -b --threads 2 --python-exit-code 1 --python SourceArt/TempleApproach/KoreanWallSection/prepare_wall_segment.py
```

`AlleyAuthoredWallSections` places three two-metre visual pieces along the left
curve, alternating the original front/back surfaces; the existing independent
curved colliders remain unchanged. It reuses `AlleyKoreanWall/KoreanWall.mat`
and its original texture maps. `temple-approach-village-visual` checks the
actual visible mesh against the collision at three midpoints (tolerance 0.25m)
and records D3D11 gameplay-camera views. `temple-approach-seam-visual` checks
real input through the shop, encounter and mountain boundary. These checks do
not prove native-player appearance, measured FPS or reference-level fidelity.

## Curved-lane gate

`prepare_gate_segment.py` uses the same hash-locked reviewed original FBX,
clips the x=0.05..1.12 interval containing the gate and its masonry pillars,
and exports `Assets/Resources/AlleyKoreanWall/gate-segment.fbx` while saving
`GateSectionEditable.blend` with packed textures. The editable mesh has
33,565 triangles and one UV layer. Run the script with the same Blender 4.2.23
command as above, substituting `prepare_gate_segment.py`.

`AlleyAuthoredGate` places this static, non-interactive gate on the right-hand
curve. It compresses the scan's excessive depth so the rendered door aligns
with the pre-existing collision. `AlleyApproachWorld` splits only the visual
wall at the gate's exact boundaries and retains all original wall colliders;
the previous placeholder remains a fallback when the asset is unavailable.
The focused village D3D11 test checks three door-face rays against the retained
wall collision (tolerance 0.30m) and captures the gameplay camera; the focused
seam test walks through the existing shop and encounter to the mountain boundary.
These checks do not establish standalone build appearance or measured FPS.

## Shop courtyard return

The same gate-free section is reused twice to connect the rear courtyard wall
to the alley's existing rear boundary. The pieces overlap slightly at their
join, use alternate faces, and add no colliders; the pre-existing rear boundary
continues to govern navigation. If the courtyard or source asset is absent,
the decorative return is omitted. The focused village PlayMode check covers
the joints, their lane clearance and the game-camera appearance. Each section
uses the existing 22,226-triangle mesh; actual GPU frame cost is unmeasured.

## Rear residential gate

The complete existing `AlleyKoreanWall/KoreanWall` model now supplies the
visual surface at the alley start's rear boundary. Its reverse face puts the
closed green gate away from the nearby courtyard gate and shows the handle.
The model is fitted to the retained rear box collider; a small visual-only
concrete corner pier covers the join to the courtyard return. If the prefab is
missing, the previously authored weathered rear-wall sections remain the
fallback. A focused D3D11 village PlayMode check sampled the rear surface against
the original collider, checked the corner bounds and captured the game camera.
This is a static, non-interactive gate; native-player FPS was not measured.
