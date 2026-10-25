# Authored first-person hand animation

This iteration reuses the accepted, restored `SourceArt/PlayerItems/hand.blend`
and its original designated-service model/provenance. It does not apply the
rejected `SourceArt/PlayerHandsV2` mesh, skins, textures or scripts. No new base
mesh or generator is involved. The existing skin surface, vertex coordinates,
UVs, nails, finger skeleton positions and textures remain unchanged.

The purpose is animation and contact quality. This does not turn the retained
generated skin topology into a scan or establish commercial-quality art approval.

## Reproduction

Use Blender **4.0.2**, built-in Python and mathutils; no extra Python packages.
From repository root, on a disposable hidden background process with two threads
and BelowNormal priority:

```powershell
blender --background --factory-startup -noaudio --threads 2 --python-exit-code 1 --python HappyToyV2/SourceArt/PlayerHandAnimation/author_hand_animation.py
```

The recipe opens only the named tracked source. It writes:

- `hand-animation.blend`: packed editable armature, original skin, independent
  forearm/wrist and actual keyed Blender Actions.
- `Assets/Resources/PlayerHandAnimation/geometry.fbx`: retained game skin and rig.
- `Assets/Resources/PlayerHandAnimation/poses.json`: poses and sampled Actions.
- `authoring-report.json`: actual source/output hashes, geometry, weights and
  measured skeletal contact targets/residuals.

The existing `SourceArt/PlayerItems` inputs remain required tracked sources. Each
export is tied to the precise original blend hash. Blender auto-backups such as
`.blend1`, temporary authoring logs and renders are optional ignored outputs.

## Rig and skin

The retained skin has 40,586 vertices / 80,960 triangles. Its roughly 28.5cm
forearm was controlled by one long wrist bone. The new rig separates a 22.5cm
proximal forearm control (elbow-end swing/pronation) from a 6cm wrist segment.
The original skin is reweighted only where it had wrist influence. The palm,
MCP/PIP/DIP chains, thumb CMC/MCP/IP, nondeforming tips and contact marker remain.
There are 24 bones, 18 deforming; the current maximum is three influences, no
unweighted vertices and maximum weight-sum error below 5e-8. Linear skinning is
used in both Blender and Unity, so the source does not hide deformation with
Blender-only dual quaternion skinning. A shoulder/upper-arm surface is outside
this camera-local viewmodel; no invisible body IK is claimed.

Each axis is bounded per bone in the JSON. Flexion is local +X, spread +Z and
thumb opposition +Y in Unity, applied as:

`bindLocalRotation * Rz(spread) * Ry(twist) * Rx(curl)`.

Blender's exported handedness reverses the Y/Z angle signs. Its bind quaternion
is labelled `sourceBlenderRestLocalRotation` for source inspection only. Runtime
must capture each actual imported FBX transform's bind rotation instead of
assuming that the Blender quaternion is a Unity quaternion. Reflection of the
opposite hand remains the existing presenter root's responsibility.

The FBX is exported with **all bone matrix bases at identity** and a neutral
armature object transform. Only afterward is the Relaxed pose restored for the
editable `.blend` preview. Blender's non-animation FBX export otherwise bakes
that current preview into the imported local bone bind basis, causing Unity to
apply the grip on top of an already curled hand. `expectedUnityRestLocalRotation`
and the report's `neutralExport` retain exact conversion quaternions and parent
names so the importer/tests can reject that double-curl regression.

## Poses and Actions

There are seven complete poses: Open, Relaxed, Flashlight, Snapper, Incense,
Talisman and Trap. Item-specific contacts are solved offline on the actual
retained skeleton with anatomical limits and a small posture regularizer, then
saved into real keyframed Blender Actions. Every item also has Equip, Unequip
and Use Actions: **22 newly authored Actions** in total, in addition to retained
historical source actions. Ring/little brace before the index and thumb during
equip. Throw release opens digits at different times instead of one global curl.

Use timelines, in simulation seconds:

| Item | Duration | Logical commit |
|---|---:|---:|
| Flashlight | 0.36 | 0.16 switch |
| Snapper | 0.85 | 0.32 release |
| Incense | 0.65 | 0.26 light |
| Talisman | 0.75 | 0.30 activate |
| Trap | 1.15 | 0.68 place |

Equip/Unequip each last 0.44 seconds. Actions author armature object movement,
forearm/wrist posture and per-digit rotations; exact Unity socket translations
and Euler degrees are retained alongside Blender preview transforms. JSON samples
are 30Hz plus exact endpoint/commit samples, derived from the saved curves with
auto-clamped Bezier interpolation. A runtime must advance by simulation time,
interpolate samples and dispatch gameplay at the single logical commit, while
preserving pause/time-stop behavior. It must not count the camera frames.
Socket custom properties are authored explicitly as floats and sampled directly
from saved FCurves. The recipe compares them against the actual Blender object
translation/rotation curves and rejects lost sub-metre movement or fractional
flashlight rotation; integer ID-property coercion must never erase these tracks.

JSON vectors are `{x,y,z}` objects. Clip `position` is a camera-local socket
offset in metres; `rotationEuler` is in Unity degrees. Bone angles are degrees
relative to bind. `hand` is Left for flashlight and Right for the other items.
The flashlight pose additionally includes a palm-marker-relative `handleOffset`
of `{x:0,y:0.020,z:0.025}` and handle radius 0.024m in hand local coordinates:
+Y toward fingertips and +Z palmar. This moves the barrel toward the upper palm
so a short little finger is not forced into an excessive hooked curl. Use the
actual flashlight model's shaft center to place its object origin from that
barrel center. Small props should be aligned to the actual pinched skin/tip
midpoint; skeletal target error alone is not a surface collision certificate.

## Review and limits

Optional still review uses the actual retained skin plus the already authored
item models, with a CPU Cycles device and two render threads:

```powershell
blender --background --factory-startup -noaudio --threads 2 --python-exit-code 1 --python HappyToyV2/SourceArt/PlayerHandAnimation/review_cpu.py
```

Outputs are under ignored `Builds/HandAnimationAuthoring/review/`. This is an
isolated Blender skin/item pairing review, not Unity gameplay, performance or
final visual approval. The loader selects the game pellet and the four actual
trap game parts explicitly, excluding generation originals that may remain in
their source blends. The physical trap review uses its true 0.55m width.
The runtime presenter owns final hand roots, viewmodel camera framing, held
prop orientation and world release/placement continuity; those require their
own integrated checks. In particular, full-surface finger/prop penetration,
runtime source-axis conversion and the full hand transfer cannot be inferred
solely from an end-bone target distance. The report preserves residuals honestly.
