# User-reference joint detail

The user supplied `user-hand-dorsal-20261110.jpg` as a hand-placement
reference. Its SHA-256 is
`cd2ec8b31376b1c1483cf07ae3c7514fb3f90f0f6cec39eff8548df881c380a9`.
It is a user-supplied photo, not a CC0 third-party asset. It is retained locally
as the editable reference; do not imply permission to publish it separately.

The earlier v7 skin used a single front/back projection, so some visible lines
did not follow the finger joints. The reference image is high-pass filtered
locally to isolate fine crease detail, then manually selected regions around
the thumb IP and four fingers' PIP/DIP joints are mapped to their respective
neutral rig landmarks. The existing nail atlas supplies the base color. The
regions fade at their edges and toward the palm/side; the hand mesh, rig,
nail geometry, item transforms and animation channels are unchanged. The same
hand skin is mirrored for the other side.

The landmark correspondences are manual approximations on a single image; they
are not calibrated 3D measurements, a complete human skin scan, or a guarantee
that all creases remain anatomically correct in every pose. This change does
not address the existing item intersections or complete finger deformation.

## Reproduction

Use the project's pinned `numpy==1.26.4`, `Pillow==10.2.0` and Blender 4.2.23
LTS. From the repository root, with a new `WORK` directory, run Blender
hidden and at BelowNormal priority, two CPU threads:

```text
python HappyToyV2/SourceArt/AnatomicalHandsPrototype/ReferenceWrinkles/prepare_reference.py --work-dir WORK
BLENDER -b -t 2 --python-exit-code 1 --python HappyToyV2/SourceArt/AnatomicalHandsPrototype/ReferenceWrinkles/bake_reference.py -- WORK
```

`WORK/joint-detail-trial.png` is the final atlas; the checked-in copy is
`../draft-skin-reference-wrinkles.png` and the runtime copy is
`Assets/Resources/PlayerItems/hand/albedo.png`. All three matched SHA-256
`067ca522f29e6edab720c31a5c3c28b641b1d806c635c9e3e4c00309983cde83` in the
local reproduction. `../install_candidate.py` installs this final atlas.

## Validation — 2026-11-10

Blender CPU renders were reviewed in the neutral extended hand and a close
thumb view. Unity 6000.6.0f1 `player-hands` D3D11 passed EditMode 104/104 and
PlayMode 4/4 with desktop isolation. `HandPhotoWrinklesStudio-20261110` under
local `Builds/` contains Unity hand-camera stills. Those images isolate the
hand layer; they are not a full game-scene or FPS verification.
