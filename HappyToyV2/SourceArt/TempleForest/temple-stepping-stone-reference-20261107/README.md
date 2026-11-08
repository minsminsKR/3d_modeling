# Individual temple stepping stone reference

Generated with the built-in image generation tool on 2026-11-07 as a proposed input to the designated A100 modeling service. The exact input is reference.png and the prompt is reference-prompt.txt. The TRELLIS.2 job completed; original.glb, service records, request/job IDs and settings are preserved here with hashes in provenance.json. The ZIP passed CRC and its actual input matched reference.png. Blender finishing, Unity integration, game-context visual approval, and performance validation remain pending. Follow the repository modeling workflow before using this result.

## CPU layout study

`stone_layout_cpu_probe.py` explores deterministic staggered and densified placement on a 3.4 m by 31 m footprint using hypothetical conservative ellipses. It prints coverage, sampled overlaps, center gaps, boundary variation and hypothetical triangle counts. Run `python stone_layout_cpu_probe.py` from any directory. It does not load the model, show actual silhouettes, measure Unity/GPU performance, or implement gameplay. The visual placement, surface contact and LOD strategy still require actual model and game review.

## Modeling tool notice

The requested modeling backend is TRELLIS.2; its repository-preserved tool notice is [TRELLIS.2-LICENSE.txt](../../CorridorEntrance/TRELLIS.2-LICENSE.txt). Backend and Blender tool licenses do not by themselves determine rights in generated outputs. The preserved input, prompt, job record and project terms document this asset separately.

## Interactive web review — 2026-11-08

The same job was opened in the existing web viewer and rotated through front, side, upper and underside views. It shows a broad, low, textured individual stone. This is not approval of fine texture fidelity, UV seams, game placement or performance. Blender finishing and game-context verification remain pending.

## Prepared Blender review recipes

`finish_stepping_stone_candidate.py` is an opt-in Blender 4.0.2 background recipe using the checked-in `../../CorridorEntrance/finish_entrance_assets.py`. It preserves the original GLB and embeds the unchanged full-resolution source and its 4K PBR in an editable `.blend`, normalizes width to 1 m without flattening or stretching, and exports 2K PBR plus three source-derived trial LODs. The candidate budgets are 6,000 / 2,000 / 600 triangles per stone; they are trial values, not measured scene cost or quality approval. The shared recipe welds coincident vertices and recalculates normals. Since the source has no normal map, none is manufactured.

The recipe fails rather than overwriting an existing `game-export/`, `.blend`, or `export.json`; an interrupted attempt may require preserving partial outputs elsewhere before a fresh run. The FBX may not automatically reference its PNG maps, so Unity imports must assign the checked-in-source-derived color and metallic/smoothness maps explicitly.

From the repository root, when background Blender work will not interfere with the user:

```powershell
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/temple-stepping-stone-reference-20261107/finish_stepping_stone_candidate.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/temple-stepping-stone-reference-20261107/verify_stepping_stone_candidate.py
```

The second script verifies file hashes, source/LOD triangle counts, finite geometry, UVs and normals, FBX reimport bounds/triangle counts/surface area, and bidirectional area-weighted nearest-surface samples. It writes `candidate-verification.json` only if a fresh report does not already exist. Random samples are incomplete evidence, not proof against every defect. The Python syntax has been checked, but neither recipe has been executed in Blender. Independent visual approval, Unity axis/material import, physical contact, path layout, flashlight and moving-camera appearance, LOD transitions and actual performance remain required before gameplay use.
