# Player hand replacement — 2026-10-24

The user's actual grip photograph is `References/user-grips.jpg`. Left hand:
wrap the flashlight shaft with its head directed upward. Right hand: hold the
snapper in an upright, loose closed grip, rather than on a palm-up tray.

## Original generation and inspection

The new base was generated through the designated A100 modeling CLI, not a
primitive hand or another provider. Request `72ba34a8-f692-44fc-b448-7ce47c1dc225`,
job `05c59e51-fe74-4da5-a864-1e115ca917be`, TRELLIS.2 1536 cascade, 24 steps,
8K PBR, seed 20261024, split geometry GPU 1 / material GPU 0. `generation-request.json`
and `Originals/generation-record.json` preserve actual inputs, settings and hashes.
CLI collection verified the archive; there were no failed/pending jobs or download
errors. `Originals/model.glb` is the unchanged 921,038-triangle generated original.

The same job was opened in the sidebar webapp and rotated through front, side and
back. All five fingers were separate; generated palm thickness and faceted-looking
skin required finishing. The generated texture did not meet close-up skin quality.
It remains untouched inside the original GLB. The game uses two-sided photographic
reference projection instead. Both skin reference images and their exact built-in
image_gen prompts are retained. These are generated reference photographs, not
claims of a real human scan. The actual user grip image is retained separately.

Additional visual/anatomical references (no downloaded mesh is used):
- [Artec's real hand scan](https://www.artec3d.com/3d-models/top-hand), for nail borders,
  webbing and dorsal detail.
- [Grasp posture and hand skin deformation](https://doi.org/10.1038/s41598-023-48658-5),
  for cylindrical contact and different palm/finger deformation.

## Editable finishing and reproduction

Use Blender **4.0.2** and Python **3.11** with the scoped `requirements.txt`.
The packed editable source is `hand.blend`. The Blender MCP connection executes the
same scripts; only a disposable authoring scene is cleared. No user scene is saved.

From the repository root:

```powershell
python -m pip install -r HappyToyV2/SourceArt/PlayerHandsV2/requirements.txt
python HappyToyV2/SourceArt/PlayerHandsV2/prepare_projection.py
blender -b --python-exit-code 1 --python HappyToyV2/SourceArt/PlayerHandsV2/finish_hand.py --python HappyToyV2/SourceArt/PlayerHandsV2/reproject_skin.py
python HappyToyV2/SourceArt/PlayerHandsV2/finish_skin.py
blender -b HappyToyV2/SourceArt/PlayerHandsV2/hand.blend --python-exit-code 1 --python HappyToyV2/SourceArt/PlayerHandsV2/finalize_blend.py
```

In pinned Unity 6000.6.0f1, `HappyToy.V2.Editor.PlayerItemsImport.ApplyHand` imports
the preserved `Assets/Resources/PlayerItems/hand` GUIDs. `PlayerHandsPreview.ImportHandAndRender`
adds an opt-in silent hand-camera review. The old PlayerItems authoring entry points
delegate hand rebuilding to this replacement; older original inputs remain available.

The recipe retains the generated UVs, sculpts excessive palm depth, welds position
seams, clears imported split normals, smooths the surface and derives a 140,000-triangle
game mesh. It authors 23 bones and normalized, at-most-three-influence weights.
Ten pose-space radial corrective shapes compensate knuckle collapse during linear
skinning. `HandSkinDeformation` applies them only after the final runtime contact solve.
Editable neutral, cylinder, loose-snapper and release actions remain in Blender.

Neutral front/back reference photos are projected onto the retained UVs with a
soft side blend and a CPU emission bake. Deterministic foreground padding prevents
background spill. Game colour/normal/roughness maps are 4K; original generated
8K PBR remains in the original GLB. The normal is a subtle colour-detail derivative,
not a claim of measured displacement. Dielectric GGX and a small warm diffuse wrap
replace the old close-up Phong response. The wrap is a skin-lighting approximation,
not full multi-layer subsurface scattering. Other item shader branches remain intact.

## Verification

`python HappyToyV2/Tools/quality/run_feature_tests.py --profile player-hands --graphics-api d3d11 --no-desktop-interaction`

The profile checks actual loaded hands, flashlight shaft triangle contact, thumb
opposition, paper pinch, the loose snapper grip, trap support, real throw/recovery,
pause, aspect ratios, the item wheel and the monochrome finale's coloured paper.
Review stills are isolated hand-layer renders, not native gameplay FPS measurements.
Final results, remaining limitations and reviewed frames are recorded alongside
the verified build, outside required authoring sources.
