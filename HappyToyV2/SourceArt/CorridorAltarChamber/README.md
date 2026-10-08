# Distant classroom offering chamber

The corridor's five memories are offered in a large abandoned classroom beyond
the generated maze. `CorridorAltarChamber.Prepare(run, chamberRoot)` adds only its
owned presentation and correctly sized prop physics; `CorridorRun` owns the real
room floor, walls, ceiling, connecting doorway and the final navigation bake.

The local contract is a **12 x 10 x 3.4 metre** room, floor at Y=0, entry at Z=-5,
and the ritual teacher table at Z=2.8. The central approach is at least 3.2m wide
until the table's front. The five paper places follow `run.Recovered`; they get
a quiet warm reflection when all five memories have actually been recovered.
The altar is one enabled `Kind.Exit` interaction, ID `corridor-offering`, with a
physical chest collider. Its world aim is exposed through `OfferingAim`.

## Editable art and regeneration

Blender **4.0.2** is pinned. Runtime uses FBX and does not need Blender installed.
The four `Models/*.blend` sources keep individually named editable parts, UVs and
physical material slots; no lights, cameras, collider settings or animations are
embedded. Existing attributed physical surfaces in
`Assets/Resources/GraphicsPbr` are linked by repository-relative paths. The
original chalkboard surface is tracked in
`Assets/Resources/CorridorAltarChamber/Chalkboard`.

To regenerate the original surface from the HappyToyV2 directory, use Python
**3.11.7**, Pillow **10.2.0** and NumPy **1.26.4**:

```powershell
python -m pip install Pillow==10.2.0 numpy==1.26.4
python SourceArt/CorridorAltarChamber/author_chalkboard.py
blender --background --python-exit-code 1 --python SourceArt/CorridorAltarChamber/author_models.py
```

The script derives all paths from its repository location. No machine path,
untracked cache or other checkout is required. The existing bundled Korean OFL
font supplies the chalk glyph outlines; its license and source remain in
`Assets/Resources/Fonts`. No downloaded image is used in the new texture set.
The four correlated chalkboard maps represent actual original enamel colour,
normal, occlusion and metallic/smoothness data, rather than duplicated albedo.
The enamel face is a real 10mm modeled panel set in front of the wood backing,
with a unique whole-board UV rectangle. Its source-plan X UV compensates for
Unity's FBX handedness conversion, so the final classroom lettering is readable.
To regenerate only that model, append `-- --models lesson-blackboard` to the
Blender authoring command; the other three model records remain unchanged.

Read-only editable/FBX validation, with the report outside source directories:

```powershell
blender --background --python-exit-code 1 --python SourceArt/CorridorAltarChamber/validate_sources.py -- --output <absolute-optional-review-output>/source-proof.json
```

Optional studio previews can be generated with `author_models.py --
--preview-dir <absolute-optional-review-output>`. The preview cameras/lights are
added only after the pristine source is saved; no review outputs are required to
continue editing. A review staging mirror can read existing dependency textures
through `--resource-project <project>` without altering that project. Final
source image links still target their own project's tracked Resources.

## What to inspect in the actual game

- Enter through the physical doorway: the worn school desks/chairs sit along
  the two flanks and the distant table is framed by unequal hanging paper seals.
- With the production torch, inspect the teacher table's worn plank edges,
  recessed field joinery, drawer bail pulls, rusty fasteners, creased cloth and
  old attendance ledger. The five paper places sit on its actual desktop.
- Read the erased attendance lesson on the large chalkboard. Small chalk stubs,
  the felt eraser and chipped tray should read at their real classroom scale.
- One failing lesson lamp casts a restricted cold cone onto the board; actual
  chalk pigment has higher albedo than the dark enamel. The lesson stays visible
  from the doorway and altar approach without making the room uniformly bright.
- Cold window-side spill and two narrow red pools should preserve old school
  enamel, wood and paper colour. It is intentionally darker between those pools.
- Inspect the slight floor soaking and restrained wall runs; the entry and
  central route remain clear. After five real memory pickups, the five places
  warm up and the plaque changes to the offering instruction.

The existing classroom furniture is cloned as mesh-only visual data from the
preserved authored school scene. The original school models, materials,
transforms, colliders and scripts are never changed. New models are statically
batched by physical material; the actual finite chest and school furniture
proxies contribute to the caller's PhysicsColliders navigation bake.

Source/FBX proof is separate from native Unity import, camera acceptance and
navigation checks. The root's controlled native altar capture explicitly places
its observer and freezes actors; it is an art review, not a survival playtest.
