# Superseded and experimental inputs

The current round Cyclopse uses `reference-standing-round.png`, `source-webapp-original.glb`, canonical `source-original.glb`, `body-rig-plan.json` and `Cyclopse_Editable.blend`. Its AI provenance is `source-webapp-rigged.glb` and `webapp-rig-inspection.json`.

`reference.png`, `source-rigged.glb`, `rig-inspection.json` and `eye-surgery.json` describe the previous tall bear design. They are retained historical inputs, not current runtime sources. The redundant temporary `legacy-tall-bear/` copies were hash-verified against current sources/committed history and preserved outside required sources.

The rejected Cyclopse `game-surface.npz/json` experiment was preserved outside required sources. `tools/reduce_game_surface.py` retains the optional QEM experiment generator; the final Cyclopse game mesh retains the original topology. The current Uncat/Baby game surfaces are produced by `reduce_surface_blender.py`.
