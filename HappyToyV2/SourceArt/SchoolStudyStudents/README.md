# Study-room students — work in progress

Three fictional student reference images were generated using the built-in
image generation tool on 2026-11-01 for this project. They depict front-facing,
full-body, neutral A-pose school-uniform characters against a white background.
`student01.png` is a pale boy in a dark blazer; `student02.png` is a pale girl
with long black hair and a dark uniform; `student03.png` is a stockier boy in a
gray cardigan. The intended final use is seated characters whose heads follow
the player. These are reference images, not yet game-ready assets.

Modeling CLI request: `96264b4f-316d-41e1-afd0-b15451e5f125` on the designated
A100 service. Jobs in image order: `d9e07b38-e832-4002-b15f-457fe8af38a1`,
`c38f8df1-58d1-4182-ba9d-81c05ee0937a`,
`95820110-6431-4804-947d-d30f8b3d2c6d`. Submitted model `trellis2`, split GPU;
server defaults include seed 1234, 1536 cascade, 12 steps and 8192 textures.
Collect with the same request and output directory:

```
python hunyuan3d-webapp/modeling_cli.py resume 96264b4f-316d-41e1-afd0-b15451e5f125 --output HappyToyV2/Builds/ModelingCache/SchoolStudyStudents/generated --json
```

The earlier three model archives have also been collected and retained as
superseded source material. Interactive visual review, rigging, seated poses,
Blender sources, Unity integration and verification are still outstanding. Do
not consider the generated reference images proof of model or animation quality.

On 2026-11-01 the user requested a much more frightening ghost appearance.
The built-in image tool edited each original into `student01-ghost.png` through
`student03-ghost.png`, adding corpse-gray skin, hollow eyes, distorted faces and
weathered uniforms while keeping full-body rigging poses. These revised inputs
were submitted as request `048b8dcc-d20c-4354-8b88-75eaad870879`, using TRELLIS.2
and split GPU. Its jobs, in input order, are
`10c8d883-8918-44d3-bcd5-7cf85bffff26`,
`b1f35610-5dd0-4d0d-ac15-e9c109bba82f`, and
`0ac03514-069f-4afc-a8cc-90d4d86cff8f`.
Collect the revised results separately with:

```
python hunyuan3d-webapp/modeling_cli.py resume 048b8dcc-d20c-4354-8b88-75eaad870879 --output HappyToyV2/Builds/ModelingCache/SchoolStudyStudents/generated-ghost --json
```

The revised jobs still need visual review, rigging, editable Blender sources,
Unity integration and verification. Do not assume the output matches the input.

All three revised archives were collected successfully and verified by the CLI
on 2026-11-02; the archives preserve the generated model and generation record.
The three actual results were reviewed in the designated webapp from the front
and back on 2026-11-02. Their app-generated 28, 28 and 52-joint rigs were also
inspected in the web joint editor. The first rig's inferred vertical positions
were inverted: every joint's Y was corrected to `0.2 - originalY`, visually
checked against the body, and AI weights recalculated on GPU 1. Its edited run
is `ecd69124-6446-460a-b12e-69bbff01f29e`; the original run remains preserved.
`collect_rigs.py` and `collect_corrected_rig.py` verify the CLI receipts and
extract the original, AI analysis, edited inputs, joint edits and result records.

Blender 4.0.2 batch inspection records the actual bones under Builds. The first
seated female preview had severe hand distortion; the later preview uses the
existing SchoolWalker wrist-volume repair, but the skirt still shows imperfect
deformation. Similar detailed knee and clothing artifacts remain on the male
models. Source-derived 3 mm voxel surfaces and PBR rebaking reduce mesh holes;
the seated copies use a separate head pivot and 50k/12k/10k-triangle LODs.
`finished/student01.blend` through `student03.blend` are packed editable
sources. Actual Unity camera review found that the original sill hid the seated
figures and that the FBX skin bounds had an excessive scale-derived expansion;
both were corrected. The corridor view and close study-room view were captured
with the opt-in camera profile. The second model's skirt and the first and third
models' knees still show deformation artifacts; normal-camera captures and logic
tests do not certify animation quality or native-player FPS.

To reproduce the local sequence with the pinned Blender 4.0.2 executable, use
`preview_seating.py` with the following inputs, run `export_seated_probe.py`
for each job with `--repair --budget 50000`, then run `add_student_lods.py`
on each resulting `student-review.blend`. Run `pack_student_source.py` on each
resulting `student-lods.blend` with `--student 01`, `02`, or `03`, and finally
run `promote_students.py`. `SchoolStudyStudentsImport.Apply` builds prefabs from
the output FBX and PBR maps in Unity 6000.6.0f1. All intermediates are created
in `Builds/SchoolStudyStudentsReview`; the tracked rig GLBs are the inputs.

| Student | Job | `preview_seating.py` options after `--job JOB` |
| --- | --- | --- |
| 01 | `10c8d883-8918-44d3-bcd5-7cf85bffff26` | `--corrected --yaw 0 --hips bone_20,bone_24 --knees bone_21,bone_25 --hands bone_16,bone_15,bone_9,bone_8` |
| 02 | `b1f35610-5dd0-4d0d-ac15-e9c109bba82f` | `--yaw 0 --hips bone_20,bone_24 --knees bone_21,bone_25 --hands bone_16,bone_15,bone_9,bone_8` |
| 03 | `0ac03514-069f-4afc-a8cc-90d4d86cff8f` | `--yaw 0 --hips bone_44,bone_48 --knees bone_45,bone_49 --hands bone_28,bone_27,bone_9,bone_8 --bend 65` |
