# Korean utility pole candidate

TRELLIS.2 request 638c6667-e8b2-4e4f-beb8-4e758bb1020e, job 44c5308d-70a0-4b32-8778-a6c0c58b8ccd, 1024 cascade, 4096 texture, seed 1234. Original GLB, input image, metadata and generation record are preserved. ImageGen input prompt: isolated old Korean concrete utility pole, 1990s, weathered transformer box and yellow streetlamp arm, neutral background.

Run Blender 4.2.23 `blender -b --threads 2 --python-exit-code 1 --python prepare_pole.py`, then copy FBX and albedo to `Assets/Resources/AlleyUtilityPole/` and run Unity method `HappyToy.V2.Editor.AlleyUtilityPoleImport.Apply`. Blender reduces the generated mesh to 60,000 faces and saves the packed editable source.

Interactive 3D viewer review was unavailable because the browser tool was disabled. Game camera verification is separate.