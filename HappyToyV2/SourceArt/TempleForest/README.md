# Temple forest environment

`reference/target-atmosphere.png` is the image supplied by the user on
2026-11-05 (SHA-256 `cd0caee5cafd6c14ae37eec40b08860f37e01a5b3fc8030d912f13a098d2d1ae`).
It is an art-direction reference, not a game background or projected texture.
The project uses Unity 6000.6.0f1 with URP 17.6.0.

The earlier forest used one repeated pine model, a flat soil plane, a single
flat stone strip, fixed lights and global fog. The intended scene needs a
recognisable temple framed by varied old trees, weathered wet stone, bounded
ground haze, sparse offerings and a distant ambiguous figure. Keep these as
world objects so they work from a moving first-person camera; preserve the
existing physical route, interaction targets and saved positions.

The generated hero tree's original GLB, reference, generation job and editable
Blender source are in `ancient-zelkova/`. Two trees use it in ordinary play;
their original trunk collision remains in place. Four sparse `fern-clump/`
instances supply undergrowth outside the route. Both have two shared LODs and
reproducible Unity importers. The remaining pines use the separately preserved
trunk-improved `TemplePrologue/pine-review` asset. Six more outer pines share
that mesh, use cheaper distant LODs and defer any trunk collider overlapping a
restored player until the player has cleared it.
Generated-model source and texture dependencies live in this directory; the
modeling service's download cache is not a handoff dependency.

The floor, guardian stones, hanging paper and loose rocks reuse authored assets
already in this project. Their modules own their runtime materials and meshes.
The low mist uses bounded world-space volumes with scene-depth occlusion, rather
than a background image or a camera-attached plane. The player camera, render
environment, school atmosphere, pause and checkpoint boundaries must be checked
when modifying this presentation. Neither scripted camera captures nor
offscreen timings establish subjective audio quality or displayed FPS.

## 2026-11-05 verification and remaining work

The isolated Windows player reached the corridor after both prayers; its
forest walk produced nine footstep events and the held incense disappeared
after placement. The spawn, lowered camera, side and temple approaches were
inspected. The full temple flow profile passed 41 PlayMode cases before the
last forest-only placement adjustments; the final forest profile passed all
12 cases, including old-save clearance and original trunk collision. School
brightness passed separately. Each profile also passed its 100 core cases.

On a GeForce RTX 3090 Ti, the final isolated 1920x1080 offscreen audit reported
about 198 synchronous renders/s at spawn and 233 at the approach. This includes
readback and audit overhead, is affected by concurrent load, had no GPU timing
samples and is not displayed frame rate. The earlier baseline measured about
286 and 290 in the same kind of audit; it is a reference, not a controlled
frame-rate comparison.

The target image's dense, varied ground cover, continuous irregular wet
stone, detailed temple weathering and soft depth remain substantially more
developed than the current result. The path retains visible large gaps; the
generated tree and fern sources do not include normal maps. More varied
authored ground, temple materials, vegetation and lighting need further art
and hardware review before claiming reference-level fidelity.

## Subsequent forest refinement

`forest-earth/` preserves a new built-in image-generation albedo and its exact
prompt; Unity derives an approximate normal map from that same source at
import. The image is used as a tiled material on the existing ground mesh, not
as a background image. `TempleForestUndergrowth` adds 30 visual-only fern
instances with separately authored lower LODs, and `TempleStoneLanterns` adds
two generated 3D stone lanterns. The original GLB, reference, editable Blender
file, textures, job record and reproduction scripts for the lantern are under
`stone-lantern/`. `TempleForestNightSky` is a directional procedural sky; its
material and the prior camera/global sky are restored when the temple ends.

The 2026-11-05 refinement also introduces the generated temple exterior in
`temple-hall-v2/` and the open bronze incense vessel in `incense-vessel/`.
The existing physical wall and the incense placement coordinate are retained;
the source-derived ash surface was checked against the placed incense. Existing
rock LODs form asymmetric low banks and the authored fern materials share a
muted runtime finish. The path's original visual stone relief now has an
upward-facing, shallow stone bed, while its original physics and footstep
region remain authoritative. The translucent puddles are world-space meshes.

The original generated pine was also reprocessed for its near LOD by
protecting branches during decimation; its source and measured trade-offs
are in `../TemplePrologue/pine-branches-review/`. The protected middle LOD is
documented in `../TemplePrologue/pine-branches-far-review/`; the original
low-cost LOD is retained for very distant trees, as are trunk collisions.

The generated `temple-stone-cluster/` and `wide-zelkova/` remain opt-in review
candidates: the stone cluster revealed conspicuous rectangular seams in the
native eye camera, and the safe tree LOD was too expensive to justify its
appearance. A separate depth arrangement of existing trees remains opt-in as
well; its repeated silhouettes and additional geometry did not meaningfully
resolve the empty horizon. Do not mistake these review assets for production
art. Their original GLBs, references, editing sources, diagnostics and the
specific limitations are preserved in their source directories.

The production arrangement was reviewed on an RTX 3090 Ti in an isolated
Windows batch player from spawn, low and side views, at the prayer space, and
through both incense placements and the gate-to-corridor route. The audit
submits a 1920x1080 offscreen camera render and reads back a pixel on every
sample; its rate includes audit/readback overhead and concurrent load and is
not displayed FPS. No GPU timestamp samples were available. The screen still
falls substantially short of the reference: tree silhouettes repeat and the
very-distant foliage remains angular, the horizon is sparse, and the stone path and
ground need richer non-repeating art and more convincing wet reflections.
