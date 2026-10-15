# Game-only hallway presentation and broken doors

The reviewed webapp base, editable rig and imported animations are retained.
Only the in-game visual child is fitted. The original prefab, title model,
actor root, 0.3m NavMesh radius, 2.1m agent height and attack/contact rules keep
their existing sizes. The encounter's existing transformation owns its growth.

`BaeksugwiHallwayAuthoring.Apply` measures four run poses by eight head-writhe
poses from the retained original. The source envelope is about 2.31m wide and
1.61m tall. `hallway-presentation.json` and the Unity ScriptableObject under
`Assets/Resources/Baeksugwi/HallwayPresentation.asset` retain the exact fit.
The visual envelope targets 2.52m wide and 2.36m tall, leaving room inside the
2.58m doorway posts and under the lowest 2.44m lintel. Depth is 1.05x the source.
An absolute parent pivot preserves the prefab and prevents clone/rebind scale
accumulation. The legacy body pivot offset is compensated against the original
actor foot origin so the larger supports remain on the floor. This is cosmetic enlargement; no wider body-clearance query,
collider, rigidbody or navigation obstacle is introduced. Very tight corners
can still visually overlap the larger silhouette; the original walking shape
remains the navigation authority.

Closed doors in either school or corridor break when the wraith's real route
crosses the opening and the moving actor reaches it. Ordinary locks do not
stop it. Fully opened leaves are passed intact. Defeat, pause, trapped actors,
other floors, blocked sight/contact and non-door exit seals do not admit a break.
Each ordinary leaf is disabled exactly once; broken doors cannot close again.
The existing actual recorded wood-clap/metal-drop/metal-drag impact is reused,
with the originals, CC0 credits and reproducible recipe already retained in
ThirdParty/Audio and SourceArt/Audio. The door owns its positional impact voice.

`DoorShatterDebris` animates the modeled splintered panels, rails and small
timber shards retained in `SourceArt/DoorDebris`. The designated webapp generated
the original and PBR on GPU 0; Blender finishing keeps those surfaces and UVs.
Animation pauses with gameplay, settles onto the
actual floor and batches by material into a single renderer per broken door.
It creates no collider, rigidbody or NavMesh obstacle. Debris remains until the
run/door is reset, and disposes only its own combined meshes. Original shared
fragment meshes/materials remain reusable. The recorded GPU 0 job, input image,
prompt, original embedded PBR GLB, editable Blender source and finishing tools
travel with the repository; see `SourceArt/DoorDebris/README.md`.
Live launches also emit a short door-height wood-dust plume, followed by low
landing dust at .85 seconds. `DoorBreakDust` and its procedural lit/depth-soft
shader own the 3.8-second effect; pause freezes simulation and death removes it.
Restored debris uses Seal without Launch, so transient dust is never replayed.

School door saves add optional broken/locked booleans; old saves default to
intact/unlocked. Existing corridor broken fields remain compatible. Saving
stores the broken state rather than all temporary airborne poses. A restored
door recreates settled rubble silently. Clearance validation probes geometry
without reallocating or animating debris. Paused restore does not replay impacts.

Recreate the visual profile with pinned Unity 6000.6.0f1 through the Editor API:

```powershell
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.BaeksugwiHallwayAuthoring.Apply -quiet-diagnostics
python HappyToyV2/Tools/quality/run_feature_tests.py --profile wraith-hallway
python HappyToyV2/Tools/quality/verify_recorded_audio.py
```

The explicit authoring command renders only an asset review. Test reports,
review images and player builds are disposable and remain outside source control.
