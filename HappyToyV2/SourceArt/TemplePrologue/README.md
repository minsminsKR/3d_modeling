# Temple prologue — implementation and remaining review

The new-story title entry now assembles the forest and temple, two days of
incense and prayer, the directional plume and gate, then prepares the existing
corridor. The two days share the same physical path. The second-day gate is
shown only when the player is not looking towards it; E on the gate begins the
masked transition. A separate suspend slot saves the phase, prayer time, player
pose and flashlight without replacing school or corridor checkpoint formats.
The existing corridor slot is consumed only after the new corridor is ready.
The background CPU/physics profile does not certify rendered appearance, audio
listening quality or player FPS; those reviews remain outstanding.

## Temple reference and generation

`temple/reference.png` was created using the built-in image generation tool.
Prompt: “Single isolated traditional small Korean Buddhist temple hall (법당),
complete freestanding building for 3D reconstruction, front three-quarter view
showing front and one side, realistic aged timber dancheong, dark grey tiled
roof, stone foundation, closed wooden lattice doors, modest rural mountain temple,
neutral diffuse studio lighting, plain white background, centered entire object
with generous margins, no trees, no people, no ground scenery, no text.
Physically plausible architecture, photorealistic material detail.”

Designated service: https://agrees-mic-icon-contribution.trycloudflare.com/
Request: `3f0897f2-2375-4f88-a040-039c6164ac33`
Job: `72e58e90-e6a0-485d-871b-d7b3e4c69dc1`
Model: TRELLIS.2, seed 1234, 1536 cascade, 12 steps, 8192 texture,
background removal enabled. The 1536 job failed on 2026-11-03 when CuMesh ran
out of GPU memory simplifying the 74-million-face remesh. No model resulted.
Retry with the same reference, seed and service: request
`31bafef0-612f-4e41-886d-1f82024795a8`, job
`8ce11907-69d7-4b74-ba4a-ded69ff731fc`, TRELLIS.2 1024 cascade, 4K texture.
The retry completed. The webapp front and side were inspected: roof, lattice
doors and stone base are present. The interior is not authored; this is an
exterior building. `temple/original.glb`, `processed.png`, `job.json` and
`metadata.json` are the downloaded source. `finish_temple.py` uses the shared
CorridorEntrance finishing recipe with Blender 4.0.2. It preserves the original
inside the packed editable blend and exports one 100,000-triangle derived mesh,
9m wide, and 4K PBR maps in `game-export/`. A 25,000-triangle second LOD failed
to reach its target (86,629 triangles remained) and was omitted. Front facing,
appearance of the reduced mesh and collision have not been verified. The
`TemplePrologueImport.ImportTemple` Unity batch importer creates the URP maps,
material and prefab from this export; the batch import succeeded, but the
prefab is now used by the outdoor prayer space; its in-game appearance and
front-facing orientation still require visual review.

## Pine reference and generation

`pine/reference.png` was created with the built-in image generation tool.
Prompt: “A single mature Korean red pine tree for realistic 3D game asset
reconstruction, full tree visible including trunk roots and entire irregular
wind-shaped crown, straight-on three-quarter view, weathered reddish bark and
dark green pine needles, asymmetrical natural branching, isolated on pure white
background, soft neutral studio light, no ground scenery, no shadows outside
the tree, no people, no text, no other trees. Photorealistic.”
Request `aad4512f-d2b9-4082-b1d0-939f0fd14b1c`, job
`25f9cb91-165b-4569-9b11-f61dc916bd4d`, Hunyuan3D 2.1, seed 1234,
texture on. The webapp result lost almost all foliage and was rejected; its
original and record are preserved in `pine/`. A replacement using TRELLIS.2
1024 cascade and 4K texture was submitted as request
`cc51c42e-431d-4392-bd03-5e2dcb87f4f2`, job
`4ff43435-9370-4552-9e84-30d96ac8ec80`. Its front and side were inspected in
the webapp: foliage and branches are present. The downloaded `original.glb`,
`trellis-job.json`, `trellis-metadata.json` and `trellis-processed.png` preserve
the result. An initial Blender attempt with a 75,000-triangle budget failed at
253,120 triangles. The later source-derived LOD0 has 157,748 triangles and the
distant LOD1 has 44,161. Both use a shared 2K atlas, Unity LODGroup and original
materials; the original and packed editable `pine.blend` are preserved. The
derived LODs have not yet had a game-camera appearance or runtime FPS review.

The rejected Hunyuan result also included independent initial and remeshed
geometry plus an OBJ/MTL with separate albedo, metallic and roughness images.
Those unchanged service originals and their SHA-256 hashes are retained under
`pine/hunyuan-original-assets/`; the textured GLB, input images, full job
record and metadata remain in `pine/`. They are preserved for authoring and
comparison only, and do not enable the rejected tree in the game.

`TemplePrologueSequence` is an engine-independent interaction sequence used by
the runtime director. Its CPU check is `dotnet run --project
HappyToyV2/Tools/quality/TemplePrologueTests.csproj` from the repository root.
It does not verify a playable prologue or presentation quality.

The sequence accepts explicitly numbered, validated phase values; its separate
checkpoint owner stores them without changing corridor checkpoints. The CPU
test covers each restored phase and its next action. `inspect_pine.py` reports original and welded mesh topology
without exporting or modifying the source; run it in Blender 4.0.2 background.
The preserved TRELLIS mesh has 961,875 faces and 1,136,608 boundary edges
before welding. At the existing 0.00002 Blender-unit weld tolerance it has
954,712 faces, 10,144 boundary edges and 27,894 non-manifold edges. These
counts identify difficult topology, but do not establish that topology alone
caused the decimator's 253,120-face result. The later reductions and atlas
projection are documented below; visual and performance review remain required.

`TemplePrologueDirector` now coordinates the interaction
sequence and a four-simulation-second prayer clock, with an explicit hold
input and validated restoration inputs. The player equips the existing hand
asset before its placement action; the incense appears at the release marker,
and the prayer clock begins only after placement. Checkpoint v2 records whether
placement happened; v1 saves migrate and unfinished placements restart safely.
`temple-prologue-logic` checks the
actual component's hold and phase transition; the standalone CPU check also
exercises invalid time and restoration. `temple-prologue-flow` adds the real
title, physical E focus, directional smoke, both days, gate and corridor,
pause, checkpoint and conflict protection. `temple-prologue-regressions`
covers selected existing school/corridor storage and title-entry contracts.

### Rebuilding the pine LODs

Run `finish_pine.py` with Blender 4.0.2 in background. It welds coincident source
vertices, splits non-manifold edges and reduces the derived LOD0 while keeping
the original collection and packed textures. Then run `finish_pine_lod.py` with
`fast-simplification==0.1.12` (MIT, https://github.com/pyvista/fast-simplification)
available to Blender's Python 3.10. On Windows x64, one reproducible setup from
the repository root is:

```powershell
python -m pip install --python-version 310 --only-binary=:all: --no-deps --target HappyToyV2/Tools/.venv-pine fast-simplification==0.1.12
$env:PYTHONPATH = (Resolve-Path HappyToyV2/Tools/.venv-pine).Path
& 'C:/Program Files/Blender Foundation/Blender 4.0/blender.exe' -b --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TemplePrologue/finish_pine_lod.py
```

LOD1 projects each reduced triangle onto the nearest LOD0 triangle's UV atlas;
the recorded centroid projection distance is 0.0243m at the 95th percentile,
0.262m maximum. These geometric numbers do not prove that foliage or texture
seams look correct. `TemplePrologueImport.ImportPine` imports both levels and
`QualityValidation.BuildWindows` checks their presence and the 50k LOD1 budget.
First-person render, subjective hearing and FPS evaluation remain separate.
