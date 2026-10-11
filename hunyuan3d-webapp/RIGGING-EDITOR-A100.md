# A100 skeleton editor

CHG-20261011-A100-005 / version 2026.10.11.6
Server: ai-server-04@192.168.0.16
Project: <checkout>/hunyuan3d-webapp

## Use
Open a rigged generation result and choose **관절 편집하기**. Example:
http://192.168.0.16:5001/jobs/60d905f2-6aa9-43cf-8d8f-80f8fae302c8/rigging/edit

- Select a joint in the hierarchy or click its 3D marker. Move the XYZ gizmo or enter coordinates. Rename joints and change their parents. Root removal and hierarchy cycles are prevented.
- Add a child joint, delete a joint (children attach to its parent), or duplicate a limb branch. This edits bones; model geometry is not cloned. Assign the new branch to the corresponding existing mesh limb.
- Groups support arm/leg/head/other, 0–60 degree amplitude and 0–100% timing. A joint belongs to one group; optional descendant assignment is supported. 0/50% gives opposite timing. Initial arm suggestions come from the previous motion metadata; every joint remains editable.
- Preview all groups or one group using **동작 테스트**. Transparent model and selected-joint weight heatmap aid inspection. Structure changes require saving with AI weights before previewing their surface effect.
- **기존 가중치 유지** saves coordinate/name/group changes on CPU, rebinding the neutral pose to preserve shape. **AI 가중치 재계산** conditions TokenRig on the edited skeleton and computes new weights on physical GPU 1. Add/delete/reparent operations require AI mode. Large pivot moves should also use AI mode.
- Undo/redo retains up to 60 local steps. Unsaved edits prompt before navigation. Each save creates a new immutable run UUID. Version history can load older bones for editing or restore the active result; the original generation GLB is separate.

Current editor scope: one skeleton/skin, 2–128 joints, at most 32 user groups. TokenRig exports from this app use one skin. Mesh transforms must agree when several skinned mesh nodes share that skin. User coordinates, graph integrity, unique joint names, finite bounds, group membership, angle/timing, asset paths and optimistic version checks are validated server-side.

## Runtime and assets
- Existing .venv-rig, SkinTokens/TokenRig weights and offline model access reused; no additional AI model or credentials required.
- Three.js 0.186.1 / GLTFLoader / OrbitControls / TransformControls / RoomEnvironment, bundled locally with esbuild 0.25.12. Source and exact lock: editor-build/editor.js and package-lock.json. Runtime JS and licenses served under static/. No runtime CDN dependence.
- Editor loads the existing 2K texture preview when its geometry and node mapping match the full rig. Downloads retain original maps; rig edits use the original clean GLB on the server.
- app.py adds editor, skeleton, edit, versions and restore routes. rigging_editor.py performs validated GLB edits and exact inverse-bind updates. rigging_edit_runtime.py and scripts/rigging_edit_worker.py isolate CPU preparation. Existing rigging_worker.py supports use_skeleton=True for AI mode, using the same physical GPU1 lock and process guard as generation.
- editor-state.json persists group mapping and authored skeleton metadata. rigging-result.json publishes the completed version. Edited motions include GroupAction plus an individual Limb_<group ID> clip for every group, in addition to applicable standard presets. Motions remain procedural presets on the AI rig.

## Upstream compatibility corrections
Reproducible installer: scripts/fix_editor_upstream.py; diff and original parser under backups/CHG-20261011-A100-005-editor.

1. Use the actual Blender-created armature object and preserve custom armature names/uniqueness suffixes, instead of assuming an object named Armature.
2. Fully replace old vertex groups before assigning new weights. Upstream's remove(range(990)) retained old weights beyond vertex 990 and could address invalid indices on tiny meshes.
3. Conditioned transfer uses corresponding known skeleton joints for similarity registration. Geometry PCA can flip symmetric models when source and target vertex counts differ.
4. Conditioned prediction skips skeleton-trimming/collapse/delete and pose-perturbation augments. Newly added bones have zero old influence and must not be trimmed before AI computes their weights.
5. Validate ordered hierarchy and quantization residual in a common similarity frame, then restore authored joint positions/names exactly and rebind. TokenRig/Blender can export a uniform normalized pose scale. Geometry/UVs are not rescaled to compensate.

## Verification
- DATA-20261011-A100-005-EDITOR: original-neutral shape and source binary preserved; ten invalid graph/coordinate/group cases rejected; real CPU worker, asynchronous save, stale/duplicate rejection, version history, restoration and retained old downloads tested in isolated per-run stores.
- DATA-20261011-A100-005-EDITOR-GPU: actual skeleton-conditioned inference on the edited penguin (10 bones, 955,709 faces, 4K maps, 122s) and a synthetic four-arm/two-leg model (19 bones, six groups, 33s). Both use GPU1. Peak Torch allocation about 2013/2014MiB; reservations 2670/2794MiB. GPU0 RTSP unchanged.
- Authored positions and parent names restored; max joint error < 2e-5 (observed ~1e-16). Sampled surface positions unchanged within 5.96e-8. Neutral-pose linear blend skinning verified. Four arm and two leg influence sums positive; all six individual clips cause real vertex displacement (arms ~0.0654, legs ~0.0305 in normalized model units).
- Browser verified selection, real XYZ gizmo drag, numeric input, undo/redo, branch duplication to four groups, child add/delete, forced AI mode for structure changes, transparent/weight display, group preview, real production CPU save and restoration. The production UI test version d882d6ef-bbc3-4f4a-8826-a2a5d034fccf is retained in history; original 74961c0a-b5d6-4d1e-925a-c7d537cb1fa8 restored active.
- Existing 14-case rigging API and four-clip action/geometry/download suite pass. All 107 production generation jobs retained; default TRELLIS.2 1536/8K/12 unchanged. No new external UI context/review transmission; previously blocked 21st external calls were not retried.
- Final evidence: logs/rig-editor-validation.json, logs/rig-editor-gpu-validation.json, logs/rig-editor-web-validation.json, deployment-a100.json. Initial failed attempts remain under backups. Desktop browser verified; actual phone viewport coverage is recorded in the web report.

LAN validation example (synthetic, not a user generation job):
http://192.168.0.16:5001/static/validation/rig-editor-multi.html
