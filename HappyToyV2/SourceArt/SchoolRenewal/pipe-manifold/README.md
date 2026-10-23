# School ceiling heating service module

New reference was generated with the built-in image generator, then submitted
through the authenticated modeling CLI to the designated A100 TRELLIS.2 service.
This is a new generated base mesh, not a primitive-built substitute or stock model.

- Request: `4810cde1-a314-459a-950e-a8c8747e3c42`
- Job: `3c8d0abc-5b34-44f0-862f-2a0e3e9ee49e`
- Seed: `2026102401`; split GPUs; `1024_cascade`, 12 steps, 4096 texture.
- `reference.png` and `reference-prompt.txt`: exact image-generator input intent.
- `original.glb`: unchanged service result, including embedded full-resolution PBR.
- `service/`: unchanged generation record, options, actual/processed inputs and metadata.
- `provenance.json`: archive/source hashes and source path mapping; cache ZIP is optional.

Original imported into Blender 4.0.2 has X/Y/Z extent
0.132946 / 1.001886 / 0.477117 and 926,962 triangles. Its long axis is Blender Y.
The game derivative rotates around Blender Z by 90 degrees, then normalizes the
long X axis to 2 metres. Hanging rods extend above the pipe body and may enter the
ceiling structure; the pipe body should retain player headroom. No collision or
game light is authored into this decorative model. Integration owns those rules.

Original provenance measurements are in `original-measurements.json`.
The integration owner records the actual webapp view inspection and Unity checks.

LOD budget adjustment: a 2,000-triangle distant derivative was attempted both
from the 8K near mesh and independently from the dense original. Bounded collapse
stopped at 6,619 and 6,577 triangles respectively because the generated assembly
contains nonmanifold structural junctions. The approved budgets are therefore
8K near / 7K distant; we preserve the pipes, flanges, valves and hangers rather
than cutting them away to satisfy an inappropriate budget. The actual diagnostic
measurements are in `component-inspection.json` and `independent-far-probe.json`.

License/source: new project-generated image and geometry. No third-party stock
model was used. Generation uses the same designated TRELLIS.2 service as the
project's other newly generated school props.
