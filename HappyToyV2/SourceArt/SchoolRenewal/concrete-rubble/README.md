# New school broken plaster / concrete rubble

New single-object reference and a new designated-service TRELLIS.2 base model.
The reference shows layered, visibly thick fractured plaster and concrete with
exposed aggregate, damp dark edges and traces of old institutional green paint.
The game derivative is an isolated low floor-debris patch; it must not introduce
collision or replace the stable school walkable floor.

- Request: `4810cde1-a314-459a-950e-a8c8747e3c43`
- Job: `3701cae2-3b9a-4417-b0cf-36e224e1d624`
- Seed: `2026102402`; split GPUs; `1024_cascade`, 12 steps, 4096 texture.
- `reference.png` / `reference-prompt.txt`: exact built-in image-generator input.
- `original.glb`: unchanged service base with embedded full-resolution PBR.
- `service/`: actual input, processed input and unchanged generation records.
- `provenance.json`: hashes and original job identity.
- Editable `<key>.blend`, source-derived LODs and atlas export settings are created
  by the shared `../finish_asset.py`; all exports retain the original source.

Original generation is an AI-created project asset, not third-party stock or a
primitive-built replacement. No new navigation or gameplay rules belong here.
Direct webapp review and Unity integration are recorded by the integration owner.

The requested 4K near collapse reached the dense generated topology floor at
6,576 triangles. Approved game budgets are 7K near / 6.6K distant, preserving the
actual broken faces and thickness. The school uses fewer multi-fragment clusters,
shared static geometry and distant source LODs for small debris to bound cost.
