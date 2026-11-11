# Old shop freezer source

Reference generated with the built-in image generation tool on 2026-11-11.
Prompt: "Photorealistic game asset reference, a single old 1980s Korean neighborhood ice cream chest freezer, rectangular horizontal chest, aged chipped off-white enamel with a faded dark red rim and two sliding flat lids, subtle rust around the feet, freestanding closed object. Three-quarter front view shows front, right side and top clearly, orthographic appearance, isolated on plain neutral gray background, evenly lit, centered whole object, no ground shadow, no text, no logo, no people, no surrounding objects. Accurate realistic proportions, suitable as input for 3D reconstruction."

User-designated modeling service request `aafb156b-757d-45d1-a885-f1a4c74a0c4c`, job `a728f75f-2c9b-4570-a39f-cd6ad07dfbc9`, TRELLIS.2 1024 cascade, 4096 texture, seed 46321, GPU 1. The input is `reference.png`. Existing right-sidebar model viewer was not present; no interactive web inspection has been performed. Do not equate the CLI's readiness/success with visual approval.

The CLI downloaded a verified provenance archive; `generation/model.glb`,
`generation/metadata.json`, `generation/generation-record.json`, generation
request/result and processed input are extracted original sources. The original
has 975,900 faces and 4K base color. Blender 4.2.23 `prepare_freezer.py` packs
the editable `FreezerEditable.blend`, extracts `albedo.png`, and derives the
250,000-face `Freezer.fbx` while retaining original UVs and colors. Run it with
`blender -b --threads 2 --python-exit-code 1 --python prepare_freezer.py`.
The Unity `AlleyFreezerImport.Apply` editor method creates the game prefab and
URP material from that FBX and texture, without modifying the original.

The original, 35k, 125k, and 250k candidates were compared with the same
offline CPU camera using `render_freezer_review.py`. Both lower-budget meshes
exhibited prominent UV seam scratches; 250k substantially reduced the visible
artifacts. This remains a relatively heavy single prop; actual game rendering,
multiple viewpoints, GPU cost and performance are not yet established. The
model and reference do not contain third-party logos; this description does not
assert a third-party license for the generated output.
