# Damp school archive shelf

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
settings, editable packed `archive-shelf.blend`, and source-derived Unity FBX/PBR
exports under `Assets/Resources/SchoolRenewal/archive-shelf/`.

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

--key archive-shelf --axis z --metres 1.25 --budgets 6000,2200 --texture 2048 --rotation-z 90 --far-from-source

The front was established from actual orthogonal model views. The final open/front
side is Blender +Y, converted to Unity -Z by the standard FBX axis mapping.

The unretopologized source clamped at 18,812 triangles after welding/collapse;
an independent raw-no-weld probe still clamped at 15,610 with 375,731 boundary
edges. A temporary 20K/19K version revealed edge tearing and was superseded.
`finish_geometry.py` reconstructs only the generated game copy at 3 mm, retaining
the actual three compartments and back panel. It verifies open compartment rays
and all four visible board envelopes (27–39mm thick). The unchanged dense source
still supplies colour/ORM and tangent normals.

The retopologized 2K far target stopped at 2,174 triangles even when independently
derived; final caps are 6K/2.2K. `export.json` records actual output counts,
geometry checks and hashes. Independent measurements remain in
`raw-collapse-probe.json`. The generated boards have invisible hollow internal
shells; recorded thickness describes their visible outer board envelope.
