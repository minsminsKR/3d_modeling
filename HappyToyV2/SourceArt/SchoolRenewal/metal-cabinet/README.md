# Abandoned school steel cabinet

New static environmental model generated specifically for HappyToyV2's abandoned
South Korean school chapter. This is an AI-generated asset, not a purchased asset
pack or a photographic scan. No external furniture mesh is substituted.

The input image was generated with the built-in imagegen tool and visually
reviewed before submission. See `reference-prompt.txt` for the exact prompt and
`generation-settings.json` for the service origin, request UUID, job ID and
explicit TRELLIS.2 options. All four furniture jobs share the preserved request
`0d94581b-051e-47be-b7dd-9e27d1debca3`.

Required handoff consists of the unchanged generated `original.glb`, the server
`generation-record.json`, original PBR/authoring outputs, the input image and
settings, editable packed `metal-cabinet.blend`, and source-derived Unity FBX/PBR
exports under `Assets/Resources/SchoolRenewal/metal-cabinet/`.

Use the repository's pinned Blender 4.0.2 and the shared
`SourceArt/SchoolRenewal/finish_asset.py` recipe. The Blender source retains the
unchanged complete generation and two optimized game LODs. Generated PBR surface
information is transferred to coherent game UVs; the original maps remain packed
at their original resolution. Unity rendering and collision are owned by the
school renewal importer/assembly rather than embedded physics in this model.

Web visual inspection and final export dimensions/triangle budgets are recorded
in the generation review and `export.json` after finishing. Server ZIP receipts
are retained with the source; resumable ZIP/download caches remain outside source
control under `Builds/SchoolRenewalDownloads/furniture/`.

Finishing arguments for `finish_asset.py` after the Blender `--` separator:

--key metal-cabinet --axis z --metres 1.8 --budgets 8000,6500 --texture 2048 --rotation-z 180 --far-from-source

The front was established from actual orthogonal model views. The final open/front
side is Blender +Y, converted to Unity -Z by the standard FBX axis mapping.

The original segmented 8K attempt clamped at 16,812 triangles. A temporary 18K/17K
version revealed tearing at vents and door edges, so it was superseded with
source-derived 3 mm surface retopology in `finish_geometry.py`. The closed generated
cabinet silhouette, handles and legs remain, and an underside centre ray verifies
clearance below the body. Original high geometry transfers vents and PBR detail.

The repaired near 8K succeeds; the first 2.5K far attempt clamped at 6,376 triangles.
Final caps are 8K/6.5K. `export.json` records actual output counts and the
source-derived geometry checks; no handmade cabinet replaces the generated base.
