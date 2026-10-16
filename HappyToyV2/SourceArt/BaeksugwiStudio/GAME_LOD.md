# Derived game geometry

`derive_game_lod.py` reads `Baeksugwi_Run.blend` using pinned **Blender 4.0.2**
and its bundled NumPy. It writes only this source directory. It never overwrites
the original GLBs, full rig, source textures, Unity assets or title prefab.

From the repository root:

```powershell
blender --background --threads 1 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/BaeksugwiStudio/derive_game_lod.py
```

`game-lod-recipe.json` requests 400,000 triangles, with a protected
geometry budget of 400,000. Protecting the original surfaces takes
precedence over reaching the requested reduction ratio. The single game mesh derives
from copied original geometry. Coincident source seams are welded while their
per-corner UVs remain separate. Only coincident vertices with identical skin and
priority weights may be welded: the source has coincident corners with different
weights, and merging those caused 23mm of animated envelope error in a rejected
candidate. Collapse reduction protects the original
head front, eye/mouth-bearing face region and complete eighteen wrist-influenced
hand surfaces, including fingers and wrist transitions. Every arm receives soft
priority so a dense face cannot consume the budget while the limbs collapse.
The source extrema of nine actual Run poses are pinned as well. The same 4mm
protected-surface/contact and 12mm posed-envelope guards apply. Distances in the report identify the retained
Blender source geometry basis; final fitted geometry and culling are separately
checked in Unity. Original FBX import already limits skinning to four influences;
derived weights are normalized to four influences in 1/1024 bins.
The declared source forward axis is used: Head-to-MaskTip points down the chin
and must not be used as a face direction. There is no voxel reconstruction,
new atlas or replacement geometry. Collapse interpolates the original UV atlas.

The separate `Baeksugwi_GameLOD.blend` retains the original 63-bone rig, actions,
packed PBR and one editable game mesh. It excludes the full original mesh,
which remains available in the separate source file. `Baeksugwi_GameLOD.fbx`
exports only that mesh and the same rest skeleton. Existing Unity Run and
Head clips must continue to own animation; this FBX exports no replacement take.

`game-lod-identity.json` records the no-decimation control (surface and nine-pose
envelope errors are zero). `--identity-only` recreates it without writing models.
`game-lod-report.json` records source/recipe/output hashes, original action curve
hashes, triangle budgets, normalized skin weights, nearest-source surface errors,
protected-region errors, hand undersides and nine original Run pose bounds.
All source vertices must pass P95, P99 and maximum distance limits. Every derived
triangle centre and all three edge midpoints are measured against the original
surface in the reverse direction. A 40mm source-basis maximum edge guard prevents
long spanning plates, and the original smooth shading flags are retained.
Geometry or contact errors beyond the recipe limits stop generation. This report
does not certify eye aperture preservation, rendered appearance, Unity bone-bind
compatibility, culling bounds or performance. Those require separate import,
original-versus-derived rendered checks and the same actual gameplay measurement.

The final source candidate contains **397,726 triangles / 184,924 vertices**.
Every one of the 952,517 original vertices was checked: surface P95 is 1.264mm,
P99 is 1.942mm and maximum is 4.874mm. The 57,518 protected vertices have a
maximum error of .317mm. All 1,590,904 derived triangle-interior probes have a
maximum reverse error of 3.562mm. The maximum source-basis edge is 38.509mm
(hand 25.177mm, arm 36.634mm, head 33.696mm, body 38.509mm), below the unchanged
40mm limit. Rest and posed hand-underside errors are zero, the nine-pose envelope
maximum is .467mm, and every derived face retains smooth shading. These are
source geometry checks, not rendered appearance or frame-rate certification.

`BaeksugwiGameLodImport.Apply` copies the derived FBX to the separate
`Baeksugwi/GameLodModel` Resources key and creates `Baeksugwi/GameLod.asset`.
Its source/report hash checks reject missing or stale editable sources.
It binds each imported mesh's bone-name order to the original game clone and
verifies every rest bind matrix before replacing a mesh. The game-only profile
supplies the mesh and conservative local bounds enclosing 32 actual
run/head poses plus the retained original 32-pose hallway envelope.
`MaskHorrorVisual` applies it only to the encounter clone, disables offscreen
skinning on the game renderer, and keeps the original clips and materials.
The single-level LODGroup culls below screen height .008 with no crossfade.
The full `Baeksugwi.prefab`, its shared mesh identity, original title clone,
materials, imported clips and original actor/navigation dimensions remain intact.

`StudioWraithKeepsNavigationAndImportsEighteenSkinnedArms` checks original/title
mesh identity and the game level, 63 mapped bones, bind matrices and shared PBR.
`HallwayWraithFillsDoorEnvelopeKeepsOriginalAgentAndFitsClonedVisualOnlyOnce`
checks 32 actual poses against each renderer's culling bounds and the unchanged
hallway fit/agent. `StudioCorridorCloneUsesOneReplacementAndHidesTheFloatingFace`
forces the game LOD through the production URP camera with controlled isolation and
lighting, checks real GPU pixel contribution and the eye-bearing original atlas,
and writes a derived PBR image plus the retained original comparison. A white
silhouette render also requires IoU >= .95 and missing arm/hand-region fraction
<= .05 so the large protected face cannot conceal collapsed limbs.
These controlled checks are not an actual gameplay frame-rate measurement.

The local 2026-10-17 production-camera review of this final candidate passed:
silhouette IoU .9966071 and missing arm/hand fraction .001746149 across 25,771
reference region pixels. The two PBR renders were also visually reviewed; the
original curved arms and fingers remain present. Optional local transport-hashed
images are in `Builds/RevisionVisual-ShapeRepair-20261017-art-review/`.
This is Editor offscreen appearance evidence, separate from native player FPS.

A rejected 122,713-triangle candidate visibly destroyed the curved arms and hands,
despite preserving the protected face, contact extrema and posed bounds. Its
one-directional P95 sampled surface guard missed local 62mm errors and did not
check the interior of new triangles. Source FBX inspection confirmed smooth
normals and actual topology collapse: one wrist fell from 3,753 to 56 dominant
vertices and hand/arm edges grew from about 20mm to over 300mm. That candidate is
not suitable for gameplay. The stronger complete-hand, bidirectional surface and
triangle-interior guards address this failure; every later regeneration still
requires its own original-versus-derived Unity image review before release.

The 350k and 375k repair candidates passed their surface-distance checks but
were rejected for body edges of 42.704mm and 42.735mm. Increasing the body
collapse priority and the target to 400k made the final candidate pass without
loosening the 40mm guard or subdividing long edges after reduction.

A rejected 86,556-triangle middle-distance candidate moved the protected face
surface by 31mm, exceeding the 4mm guard. It is not exported or retained as a game
asset. The single-level reduction must preserve those surfaces instead of
trading facial shape for a second distance level.
