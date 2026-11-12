# Village distant horizon

The route-scoped `AlleyVillageHorizon` generates a low-resolution, collision-free hillside behind the western edge of the village, and places 24 visual-only pines using the existing forest texture and pine material. It belongs to the shared approach route rather than the streamed village scene, so it is not removed by a village scene unload.

The skyline pine is derived from the project's existing `Assets/Resources/TemplePrologue/pine-distance-review.fbx`, SHA-256 `afab3402733d2bdc799e6f6a09fe03d9a904f1c158b19f6a6929ef79da644abb`. Blender 4.2.23 reduces and reprojects the original UV atlas, retaining the source trunk to avoid floating crowns. The original 40,697 triangles become 11,487 per instance. The output `pine-village-silhouette.fbx` SHA-256 is `c203ade9eee95b3342712b08a906568236fe46d9e15a4653f466e5eb0c531ee9`. The editable derived blend and recipe are included; the existing pine source and textures remain unchanged.

Reproduction requires Blender 4.2.23 and Python 3.11-compatible `fast-simplification==0.1.12` (MIT; license included). For example, install it into a project-local tool directory and invoke Blender with its system environment enabled:

```powershell
python -m pip install --only-binary=:all: --no-deps --platform win_amd64 --implementation cp --python-version 311 --abi cp311 --target HappyToyV2/Tools/.venv-pine311 fast-simplification==0.1.12
$env:PYTHONPATH = (Resolve-Path HappyToyV2/Tools/.venv-pine311).Path
blender -b --threads 2 --python-use-system-env --python-exit-code 1 --python HappyToyV2/SourceArt/TempleApproach/VillageHorizon/prepare_pine_silhouette.py
```

The focused D3D11 village and seam play-mode tests passed; the seam check actually unloads the village scene and confirms that the hillside survives. Both game-camera views were inspected. Material instancing is enabled, but native-player frame rate and actual instancing behavior were not measured. This is a distant silhouette improvement; the entire map is not claimed to match the reference.
