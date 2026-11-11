# Korean plaster house candidate

TRELLIS.2 request 638c6667-e8b2-4e4f-beb8-4e758bb1020e, job dbe44d50-4c57-4ba2-bb86-b14ae87fddec, 1024 cascade, 4096 texture, seed 1234. Original GLB, input image, metadata and generation record are preserved. The input was generated with ImageGen from a prompt for a weathered 1990s South Korean single-story plaster home, dark slate roof, metal gate and barred windows, isolated in a three-quarter view.

Run Blender 4.2.23 `blender -b --threads 2 --python-exit-code 1 --python prepare_house.py`, then copy FBX and albedo to `Assets/Resources/AlleyPlasterHouse/` and run Unity method `HappyToy.V2.Editor.AlleyPlasterHouseImport.Apply`.

The source model has not been reviewed in the designated web 3D viewer because the browser tool was unavailable. Game camera and gameplay review must be recorded separately.
`prepare_far_lod.py` derives a 30,000-face static background mesh from the prepared 125,000-face FBX with Blender 4.2.23; copy `PlasterHouseFar.fbx` to `Assets/Resources/AlleyPlasterHouse/geometry-far.fbx`. The background instance reuses `PlasterHouse.mat` and its existing texture. The editable full-resolution source remains unchanged.
