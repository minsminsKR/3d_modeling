# Temple prologue — work in progress

The prologue is not connected to the game yet. New-game entry, the forest,
prayer interaction, two-day presentation, directional smoke, gate transition,
pause and save behavior, and actual in-game visual/audio review remain to be
implemented and verified. Existing corridor checkpoint entry must remain intact.

## Temple reference and generation

`temple/reference.png` was created using the built-in image generation tool.
Prompt: “Single isolated traditional small Korean Buddhist temple hall (법당),
complete freestanding building for 3D reconstruction, front three-quarter view
showing front and one side, realistic aged timber dancheong, dark grey tiled
roof, stone foundation, closed wooden lattice doors, modest rural mountain temple,
neutral diffuse studio lighting, plain white background, centered entire object
with generous margins, no trees, no people, no ground scenery, no text.
Physically plausible architecture, photorealistic material detail.”

Designated service: https://agrees-mic-icon-contribution.trycloudflare.com/
Request: `3f0897f2-2375-4f88-a040-039c6164ac33`
Job: `72e58e90-e6a0-485d-871b-d7b3e4c69dc1`
Model: TRELLIS.2, seed 1234, 1536 cascade, 12 steps, 8192 texture,
background removal enabled. Submitted 2026-11-03; generation is pending.
Collect with the same request ID and server; preserve the original archive and
inspect the same job in the webapp before Blender finishing and Unity import.

`TemplePrologueSequence` is an unconnected, engine-independent interaction
sequence. Its CPU check is `dotnet run --project
HappyToyV2/Tools/quality/TemplePrologueTests.csproj` from the repository root.
It does not verify a playable prologue or presentation quality.
