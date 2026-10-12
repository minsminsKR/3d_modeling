# Creature authoring tools

See `../README.md` for pinned setup, source provenance and the complete sequence.
The default input is the original app-generated GLB plus the checked-in body plan
and source-order weights. `fit_round_cyclopse.py` and `fit_baby_body.py` record
reviewed anatomical positions; `weight_body_surface.py` preserves coincident
UV-seam ownership. `reduce_surface_blender.py` creates game-only continuous
geometry for Uncat/Baby. `smooth_game_skin.py` regularizes Uncat game skin.
`bake_surface_atlas.py` transfers generated source PBR/custom normals onto the
new atlas using source-surface BVH queries and actual game Mikk tangent frames.
Small Cyclopse source geometry is copied with its original UV/normals instead.
`author_creature.py` packs editable high/game meshes and exact own actions.
`finish_creature.py` supports subsequent clips-only adjustments.
`author_game_eyes.py` maps source anatomical landmarks to actual game triangles;
`export_eye_masks.py` emits masks/profiles with bottom-left Unity/Blender UVs.
Preview and diagnostic outputs are optional; preserve original GLBs, `.blend`,
plans, weights, textures and source generators in Git/LFS.