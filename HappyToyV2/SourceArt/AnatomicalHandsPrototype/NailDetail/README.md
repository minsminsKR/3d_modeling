# Hand nail detail

The intermediate atlas `../draft-skin-2048-nails.png` adds short nail detail to
the existing v7 skin atlas. It is baked into the existing UV and deforms with
the hand; the temporary fitting mesh is not part of the game. The thumb patch
was subsequently recentered on the projected fingertip surface and aligned
with its longitudinal axis after comparing the user photograph and extended hand. The source is not a human
scan, and this addition does not resolve the remaining stylized hand shape,
finger deformation or incomplete prop contact.

## Provenance

* Nail mesh and diffuse map: Mindfront, `mindfront_nails_01_short`, from the
  [MakeHuman Community bodyparts04 CC0 pack](https://static.makehumancommunity.org/assets/assetpacks/bodyparts04.html).
  Original archive `bodyparts04_cc0.zip` SHA-256:
  `60ae7536f143d087a7e3ae9b05ef86b628ccfbc63f0e3cd10694defb3974b025`.
  The source OBJ and texture used by this bake are also included under `Source`.
* `Source/makehuman-base.obj` is MakeHuman's CC0 base mesh, used only to read
  joint landmarks for aligning the nail reference. Source repository:
  https://github.com/makehumancommunity/makehuman/tree/a8bc2d54ff0ac92e78ff71431b1023eda42bf482/makehuman/data/3dobjs
  (SHA-256 `8e761e6624b8f54536409135d1636da63b32486a90d4897f84e121d144f6fb4c`).
  The official license is in `Source/MAKEHUMAN_LICENSE_ASSETS.md`; source OBJ
  headers also identify their CC0 release. No MakeHuman program code is shipped.
* The existing Blender Studio hand and generated projection skin have their
  separate provenance in the parent README.

## Reproduction

From the repository root, create a fresh scratch directory and run the commands
below with Python dependencies `numpy==1.26.4`, `scipy==1.11.4` and
`Pillow==10.2.0`, then Blender 4.2.23 LTS in hidden background mode with two
threads and BelowNormal priority. `WORK` and `BLENDER` below are placeholders,
not required workstation paths. The base and nail sources are already checked
in; do not download from personal caches. `fit_nails.py` maps original joint
landmarks to the existing rig; `bake_nails.py` fits the five surfaces and bakes
them to the existing UV; `compose_nails.py` blends the baked result into v7.

```text
python HappyToyV2/SourceArt/AnatomicalHandsPrototype/NailDetail/fit_nails.py --output-dir WORK
BLENDER -b -t 2 --python-exit-code 1 --python HappyToyV2/SourceArt/AnatomicalHandsPrototype/NailDetail/bake_nails.py -- WORK
python HappyToyV2/SourceArt/AnatomicalHandsPrototype/NailDetail/compose_nails.py --work-dir WORK --output WORK/result.png
```

CPU bake sampling can differ by one 8-bit color value on a few pixels between
runs. The nail-stage atlas SHA-256 is
`325dad437098073a86833b706de42da8f7a55eacbfba09293295a05ad4033ad4`.
The runtime atlas additionally includes the user-reference joint detail from
`../ReferenceWrinkles/`; `../install_candidate.py` installs that final atlas.
Its other FBX and pose inputs remain the previously installed sources. Run
`PlayerItemsImport.ApplyHand` when reimporting the complete candidate.

## Surface alignment — 2026-11-10

The thumb source is first expressed in its principal width/length axes; independent
world-X/Z rescaling previously skewed the diagonal nail. Its new basis follows
the dorsal fingertip rather than the thumb bone alone. Run `measure_thumb_surface.py`
in Blender background mode to reproduce the 0.1 mm-spaced surface-section
measurements: at the nail center and 5 mm either side, silhouette midpoints are
0.45 mm, 0.85 mm and 0.85 mm from the chosen centerline. These are projected
mesh measurements, not anatomical measurements of the partly cropped photo.
The thumbnail is still a baked detail; it has no independent 3D nail geometry.

Unity `player-hands` D3D11 passed EditMode 104/104 and PlayMode 4/4. In the
actual corridor test fixture, separate gameplay world-camera and hand-camera
frames were captured at the same idle poses and composited for visual review.
Those composites omit the HUD and are not native player framebuffer captures.
The thumb is partly obscured in held-item poses; the neutral Blender close-up
and Unity open-hand view are separate visual checks.

## Earlier validation and limitations

Unity 6000.6.0f1 D3D11 hand-camera review was run hidden with desktop-input
isolation. `player-hands` EditMode passed 104/104; PlayMode initially passed
3/4, with the item-wheel input-release movement assertion failing. The failed
case passed 1/1 on a selected retry. This is not a new 4/4 full-profile run.
The optional hand-camera stills in local `Builds/HandNailStudio-20261110/`
show the nails in the open pose; in actual held poses some are obscured.
These are isolated hand renders, not full-scene gameplay or FPS evidence.

An unrelated skin-weight blend candidate passed `hand-animation-logic`
112/112 EditMode and 12/12 PlayMode and `player-hands` 104/104 and 4/4,
but was not retained: visible improvement was too small and some deformations
worsened. A direct MakeHuman skin transfer and projected mesh nail experiment
were also rejected for UV seams and surface intersections. Those checks do not
certify this atlas as a complete high-quality hand; wrist, finger articulation,
flashlight grip and talisman contact remain open.
