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


The village also has a separate visual-only rear ground slab owned by
`AlleyApproachWorld`. It overlaps the existing shop-yard slab and western
hillside edge to cover the previously visible sky wedge behind the houses.
The added slab is 2 cm below the existing slab to avoid coplanar overlap.
A focused D3D11 village camera and a test-only ray through the former wedge
were checked. The slab adds no gameplay collider and unloads with the village;
this single sampled view is not proof of every possible horizon angle.

Both visual-only rear ground slabs use physically scaled UVs for the existing
`concrete-rough` PBR texture. Previously each roughly 48-metre slab stretched
one texture over its full surface, making the yard appear blurred. The focused
village D3D11 play-mode check validates the UV coverage and captures the shop
rear camera. Existing colliders and terrain dimensions are unchanged.


A separate visual-only, physically tiled rear-right yard extends beneath the
slate house beyond the rear gate, overlapping the road from below to avoid a
sky gap. It introduces no ground collider. The focused village test checks
that it covers the new house footprint and that neighboring houses do not
overlap. It is not a navigable area.
