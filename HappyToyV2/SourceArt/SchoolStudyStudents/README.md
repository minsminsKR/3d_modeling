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
