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
python hunyuan3d-webapp/modeling_cli.py resume 96264b4f-316d-41e1-afd0-b15451e5f125 --output HappyToyV2/SourceArt/SchoolStudyStudents/generated --json
```

The model archives, interactive visual review, rigging, seated poses, Blender
sources, Unity integration and verification are still outstanding. Do not
consider the generated reference images proof of model or animation quality.

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
python hunyuan3d-webapp/modeling_cli.py resume 048b8dcc-d20c-4354-8b88-75eaad870879 --output HappyToyV2/SourceArt/SchoolStudyStudents/generated-ghost --json
```

The revised jobs still need visual review, rigging, editable Blender sources,
Unity integration and verification. Do not assume the output matches the input.

All three revised archives were collected successfully and verified by the CLI
on 2026-11-02; the archives preserve the generated model and generation record.
The interactive web review and subsequent finishing are still pending.
