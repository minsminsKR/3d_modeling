# Reprojected stepping stone LODs

These assets retain the original webapp mesh and PBR source in the parent folder.
The first shared-UV decimated candidate had conspicuous gray texture triangles;
use the separately unwrapped, reprojected assets in this folder instead. Do not
use the older `game-export` candidate without correcting that defect.

Each of the three LODs has its own 2048 x 2048 base-color atlas and packed
metallic/smoothness atlas (R = metallic, A = 1 - roughness; G and B zero). Bind
both textures to the corresponding LOD material explicitly in Unity. The source
contains no normal map. The packed editable `.blend` preserves the generated
original, the prior candidates and the new LODs. `rebake-report.json` records
hashes, dimensions, triangle counts, projection coverage and FBX reimport checks.

Blender 4.0.2 CPU/Cycles sampled the original PBR material onto independently
Smart-UV-unwrapped LODs with selected-to-active emission, 0.015 m cage extrusion,
0.05 m maximum ray distance and 16 px padding. At 1 m long, LOD 0/1/2 contain
5,999 / 1,999 / 599 triangles. For coverage, a white emission map was sampled
inside UV islands eroded by two pixels; all 2,089,909 / 2,159,647 / 2,178,620
interior pixels received source projection. A corresponding independent test on
the other stone produced 100% misses when its source was moved 10 m away.
Original versus three LOD CPU renders from top and low angles revealed no large
gray triangles; this is not a substitute for Unity material, lighting or in-game
performance inspection. The 3 x 2 RGBA 2K atlases have 96 MiB uncompressed base
levels; actual GPU memory depends on Unity platform compression and mipmaps.

Reproduction: invoke `rebake_stone_trial.py` in Blender 4.0.2 background mode,
with `-- <parent source folder> <new temporary output folder>`, where the second
folder must not yet exist. Run `verify_long_rebaked_fbx.py` under Blender with
`-- <temporary output folder>` and `validate_long_coverage.py` under Python 3
with Pillow and NumPy on that same folder. `preview_long_rebaked.py` renders a
comparison there in Blender CPU mode. The publish helper `publish_long_rebake.py`
accepts `<temporary output folder> <parent source folder>` and checks source and
output hashes, coverage and FBX proof before creating a new `rebaked` directory;
it refuses to overwrite one. Use a separate source copy for reproduction.
The project helper `HappyToyV2/Tools/quality/run_isolated_desktop.py` supports
hidden, BelowNormal Blender operation without disturbing the desktop.
