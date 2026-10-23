# Low school timber cabinet

Additional new generated school cupboard for the low corridor furniture positions.
It is separate from the newly generated tall metal cabinet and uses its own exact
input, prompt, request UUID and job ID in `generation-settings.json`.

This is an AI-generated asset made specifically for HappyToyV2; it is not a purchased
asset pack or a photographic scan. The preserved input was generated with imagegen,
then submitted through the designated service CLI. Source collection uses
`collect_furniture_sources.py`; the verified ZIP cache stays in Builds.

The unchanged `original.glb`, server generation record/PBR, image/prompt/settings,
packed editable `timber-cabinet.blend`, coherent game UV/normal baking tools and
Unity resources are the portable handoff. The shared `finish_asset.py` records final
scale, rotation, two LOD budgets and actual output hashes in `export.json`.

Target height is .85m for the low abandoned corridor storage. The school assembly
owns simple collision and placement; the visual model contains no added physics.

Finishing arguments after the Blender `--` separator:

--key timber-cabinet --axis z --metres .85 --budgets 8000,3500 --texture 2048 --rotation-z 180 --far-from-source

Actual orthogonal source views establish the front as Blender -Y. The 180° turn
makes the final authored front Blender +Y / Unity -Z. `finish_geometry.py` applies
source-derived 3 mm surface retopology to prevent the segmented generated cabinet
surfaces from tearing under collapse; all game detail still comes from the
unchanged dense model. It validates the real door surface and silhouette bounds.
