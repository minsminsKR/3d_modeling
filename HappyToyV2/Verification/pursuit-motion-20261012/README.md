# Distinct grotesque pursuit actions — 2026-10-12

When an existing AI actor enters Chase, V1MonsterMotion now plays its updated
CreatureStudio chase action. Uncat pursues on hands/feet with lateral neck snaps;
Cyclopse runs upright with an off-beat head roll; Baby crawls urgently on hands/knees
with faster head drops/rebounds. The two-stride loops separate head accents from
footfalls. Patrol curves and Baby cry remain byte-identical to the prior assets.

These are animation-only finishes of the committed modeling-webapp originals.
The existing geometry, UV/PBR, semantic skeleton, navigation capsule and school
Hwacat identity are retained. Editable packed Blender sources, FBXs, Unity clips,
profile settings and authoring reports accompany the change. The import pipeline
samples 32 phases to include the new neck extrema in culling/support bounds.

## Reproduce

Use Blender 4.0.2 and Unity 6000.6.0f1. Start in the repository root:

```powershell
foreach ($creatureKey in @('Uncat','Cyclopse','Baby')) {
    blender --background --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/CreatureStudio/tools/finish_creature.py -- --creature $creatureKey --animation-only
}
Push-Location HappyToyV2
unity run . --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.EnemyStudioGameImport.Apply
unity test . --editor-version 6000.6.0f1 --mode PlayMode --filter 'CreatureStudio;ModernThreeEnemiesUseTheirOwnClipsDeformAndGroundAtEverySampledPhase' --output Verification/pursuit-motion-20261012/playmode-pass.xml
unity build . --editor-version 6000.6.0f1 --target StandaloneWindows64 --execute-method HappyToy.V2.Editor.QualityValidation.BuildWindows --args '-v2-build-output Builds/Local-PursuitMotion-20261012'
Pop-Location
```

Review each creature's body-rig-plan overrides before changing defaults;
Cyclopse's chase duration now covers two full strides.
Blender/Python dependencies are pinned in SourceArt/CreatureStudio/README.md.
GIF previews additionally use Pillow 10.2.0.

Run the new player with `-quiet-diagnostics -v2-creature-studio-output <output>`.
Use an isolated output folder outside source commits for the diagnostic save profile.
Then run `python HappyToyV2/Tools/quality/preview_creature_pursuit.py <output>`
to reproduce the three GIFs and contact sheet. The GIF timing follows the native
source clip at speed 1; normal play scales clip speed to actual navigation velocity.

## Evidence and limits

- playmode-pass.xml: four real-scene cases pass. The new case checks Chase
  selection, >18-degree head rotation versus quiet patrol, seamless loops,
  unchanged joint scale, pause freeze, and return to patrol/idle cry. The other
  cases cover three corridor actors, distinct school Hwacat, and sampled floor contact.
- source-binding.json: all three original source palettes/weights/UV seams pass.
- poses-*/pose-audit.json: read-only deformed game-skin measurements, including
  existing reconstructed-surface strain; these are descriptive, not blanket skin QA.
- native/creature-studio.json: 311 controlled native checks pass, including 32
  ground phases/action, own materials/clips and visibly distinct head motion.
- native/diagnostic-audio-silence.json: process mute verified, listener kept at
  zero, original session mute/preferences restored on exit.
- build-inputs.json: current 2,863 Assets/Packages/ProjectSettings input hashes and
  executable/DLL/credits pass. build.provenance.json honestly records the dirty
  pre-commit build; local-source-revision.json in the player records the final commit.
- native/pursuit-contact-sheet.png and the three GIFs were reviewed for rendered
  face orientation, observable neck changes, supported limbs and framing. They
  use the actual player camera in the real corridor; AI is disabled in this art
  fixture. They do not certify a full survival run, hardware performance or perceived fear.

Raw capture frames, failed attempts and logs are optional reproduction outputs.
NUnit's repeated image payloads were hash-verified before removal; JSON evidence
and outcomes are retained. Original meshes were not regenerated. This work is
committed locally; a new push requires an explicit user request.
