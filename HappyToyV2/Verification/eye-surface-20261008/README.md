# Original-eye surface emission

Cyclopse has exactly one central original eye. The five other hostile faces retain
their two original eye regions. Original face meshes, BaseMaps, pupils/stone
eyeballs, normals, UVs and animation sources remain unchanged. Graded emission is
sampled on the original material surface; no eye domes, discs, sockets, extra
renderers, colliders or lights are added. LanternMask's actual apertures stay open.

Authoring is in `SourceArt/ThreatEyes`: measured anatomical regions, seam-aware
UV mask generator, source profiles, validation, tool pins and asset provenance.
Resources contain the six complete mask/profile pairs and Unity import metadata.
`ThreatEyeMaskImport` enforces linear Texture2D masks and checks actual resource
loads. Resources shader anchors retain the inherited surface keyword combinations
with emission in a normal Windows player build.

The verification files record the final PlayMode regression results, native
original-texture preservation gallery and matching release build inputs. The
gallery makes 30 paired views: each of six actual production faces at 3m with
torch on/off, 15m in darkness, from behind, and behind a solid panel. Emission is
toggled through indexed material property blocks; the original renderer stays
enabled. Central original RGB/detail comparisons use an emission-off baseline,
including white stone eyes. Dark-pupil contrast applies when the baseline has a
dark pupil. Representative PNGs are retained here; the full gallery can be
regenerated using the command below.

All solid panels and solid rear heads occlude the glow. The mask's real open
apertures can expose their original inner lips from behind; any such pixels must
remain confined to the two aperture regions. No mesh is added to fill those holes.

These are controlled render fixtures. They do not certify enemy AI, survival,
audio or performance. PlayMode separately checks anatomical eye count, original
mesh/material preservation, head attachment, paused checkpoint/physics/navigation
stability, scene reload, material lifetime and a later material owner's properties.
The authored scene is not saved by the build validation.

From the project root, use Unity 6000.6.0f1 with the pinned project packages:

```powershell
unity run . --editor-version 6000.6.0f1 --timeout 1500 -- -executeMethod HappyToy.V2.Editor.QualityValidation.BuildWindows -v2-release-player -v2-build-output Builds/EyeSurface-Player
unity test . --mode PlayMode --filter 'EveryHostileFacePreservesOriginalEyeSurfaceAndPupilTexturesWithOneCyclopseEyeAndNoAddedGeometry;NaturalEyeEmissionTogglePreservesPausedChapterCheckpointPhysicsAndSourceAssetsAcrossReload;NaturalEyeLifecycleRestoresCallerMaterialAndIndexedPropertiesAndHonoursLaterMaterialOwner' --editor-version 6000.6.0f1 --output Verification/eye-surface-tests.xml
Builds/EyeSurface-Player/HappyToyV2.exe -v2-threat-visual-output Verification/eye-surface-gallery
python SourceArt/ThreatEyes/validate_visual_sources.py
# After staging required sources:
python Tools/quality/check_source_completeness.py
```

The gallery is opt-in through that argument and quits after writing its report.
Normal gameplay preserves existing monster activation, chapter/save schemas and
original eye material ownership. Builds and the full transient gallery are local,
reproducible outputs, outside source commits.
