# Alley and mountain approach

`References/user-alley.png`, `user-mountain.png` and
`user-village-mountain-diagram.png` are the user's visual
references provided in this session; they are not licensed here for reuse in
other products. The current geometry is an early interpretation, not a match
or visual quality approval. The alley uses existing GraphicsPbr surfaces; the
mountain reuses the existing TempleForest / TemplePrologue LOD assets and
terrain textures. The existing temple checkpoint and authored interactions
remain separate from the new-run-only approach.

The new-run route starts at (500, 0, 386), crosses the gradually dressed
village edge at z=414, follows the mountain slope and a connector to the
existing temple ground, and enters the temple at z=474 without a screen fade
or player teleport. The original temple remains in the base scene. Its rear
boundary is opened only for this walk-in route; old saves retain the closed
boundary. New approach checkpoints use the optional v4 extension and preserve
inventory, acquisition, completion and an explicit missing-animal bypass;
existing v2/v3 temple records are not reinterpreted.

`Village.unity` and `MountainPath.unity` are additive scene containers created
by `ApproachSceneAuthoring`. The player, inventory, event and temple stay in
the base scene. `TempleApproachFlow` starts mountain loading before the player
reaches it and holds a physical gate until ground is ready. At z>=468 the
village can unload; returning to z<=445 reloads it before a safety gate at
z=426. Scenes currently contain markers: world generation and resource
instantiation still occur synchronously on the main thread, so asynchronous
scene loading does not certify hitch-free streaming or a performance gain.

`AlleySnapperEncounter` provides a temporary shopkeeper conversation, pickup,
physical obstruction, and a marked ground target. Only an upward-facing first
contact near that target clears the obstacle; direct hits and distant throws do
not. Empty stock can be replenished. A sound-reactive retreat moves the dog to
the side before the blocker opens. The storefront uses a locally edited opening and a simple walk-in placeholder
interior; its owner is a primitive silhouette without voice or conversation
animation. Source, authoring and measured opening are in `Storefront/Interior/`. The dog has a CC0 bark recording
but still lacks approved idle, alert, barking and foot-locked run animation.
`ApproachAmbientSoundscape` blends existing refrigerator and forest recordings
with the CC0 cicada, distant bark and temporary wind-chime sources; attribution is in
`ThirdParty/Audio/VillageApproach/README.md`. The cicada recording is not
claimed to be Korean. The chime is a temporary multi-tone recording, not a verified Korean temple
recording. Television speech and other village recordings remain to be sourced. The short side trail is owned by `MountainForkDressing`.

The alley now has its own dusk sky, lighting and fog, blended over the village edge and restored to the existing
night environment on the mountain. The old shop facade and freezer
are generated assets; their inputs, editable sources and limitations are in
`Storefront/README.md` and `Freezer/README.md`. Street cracks, drains, signage,
wires and roof detail supplement the existing collision geometry. The mountain
adds irregular paving, lanterns, foliage and a view of the existing temple.
The route uses the actual temple instead of the old decorative hall, and its
connector slopes match their collision meshes. Background terrain and trees
are cleared only for the continuous approach; legacy temple layout can be
restored. Its outer bank roots and current in-game appearance still need GPU
visual review. The visual result still falls well short of the supplied
photorealistic references.

The animal uses a retargeted, morph-baked running candidate and turns toward its
retreat direction after the actual snapper impact. The original static model is
a fallback. Foot locking, deformation and visual quality remain incomplete;
see `../AlleyBeast/RunCandidate/README.md` for source, license and measurements.
A real-input PlayMode route exercised the shopkeeper, wrong and correct impacts,
mountain ascent and continuous temple entry. Earlier D3D11 game-camera stills and
24 motion samples were reviewed before the latest structural changes; these omit the HUD and are not a native player,
frame-rate or broad visual-quality certification. Interactive modeling-service
inspection was unavailable and remains outstanding.
