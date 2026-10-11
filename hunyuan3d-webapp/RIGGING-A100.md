# A100 AI auto-rigging

Change: CHG-20261011-A100-005 / version 2026.10.11.6
Server: ai-server-04@192.168.0.16
Project: <checkout>/hunyuan3d-webapp

## Use
Open a completed generation result and use AI auto-rigging. The default uses a 50,000-face analysis mesh, transfers the predicted skeleton and skinning back to the original geometry/materials, and keeps the original generation GLB separately. Select lightweight output to retain the analysis mesh instead; budgets are 30,000 / 50,000 / 100,000 faces. Seed defaults to 1234; beam count is the official 10.

The clean rigged GLB download contains a skeleton and normalized vertex weights. The action preview and separate animated GLB contain Idle (3.2s), Wave (2.4s, when lateral branches are detected), Bow (2.8s), and Hop (2.4s). These are procedural presets applied to the AI-inferred skeleton, not an AI motion-generation model or a motion-capture walk cycle. Geometry-based lateral-branch classification uses glTF Y-up coordinates and skin influence; inspect unusual skeletons before production use. Mixamo remains an optional collapsed external workflow, including package export and result upload.

The browser selects Wave when available and autoplays after loading, except when reduced motion is enabled. Action selection, play/pause, reset, 0.5/1/1.5/2x speed, and a seekable timeline are available. The clean rig, animated model and original generation remain separate downloads. The preview preserves the exact original positions, UVs, indices and skin weights, resizing embedded textures to at most 2048px. The animated download keeps all original texture pixels. Mesh decimation was rejected after visual verification because generated UV islands were corrupted without texture baking. No decimation is used in the final action preview.

New rigging workers create actions automatically on CPU after AI inference. Existing completed example 60d905f2-6aa9-43cf-8d8f-80f8fae302c8 was backfilled from its validated AI rig without repeating inference. Direct viewing: /jobs/60d905f2-6aa9-43cf-8d8f-80f8fae302c8#motion-preview.

## Runtime
Official SkinTokens revision: 273b691d35989d71cd17ff2895fdc735097b92d1.
Model and Qwen config revisions: logs/rigging-models.json. Public checkpoints downloaded locally (about 1.6GB); no new gated-model approval required. All inference runs offline.
Dedicated uv-managed Python 3.11.15 / .venv-rig; Torch 2.7.0+cu126, FlashAttention 2.8.3, Transformers 4.57.6, Diffusers 0.35.1, NumPy 1.26.4, bpy 4.2.0, SciPy 1.15.3. Exact 151-package freeze: requirements-rigging-a100.txt. Existing Hunyuan and TRELLIS environments, GPU driver and 3D defaults unchanged.

GPU 1 only. One rigging executor queues requests and uses the same cross-process physical GPU lock as generation. CPU analysis is separate, then model inference/export runs in an isolated worker. Worker/helper process groups are stopped on timeout and parent death; CPU-only Blender helper binds only a dynamically selected loopback port. Existing GPU0 RTSP allocation (~7917MiB) remains present.

## Compatibility fixes
- Official bpy 4.2 cp311 Linux wheel incorrectly declares a cp39 tag inside WHEEL. Corrected only that metadata tag to the published cp311-cp311-manylinux_2_28_x86_64 filename, updated RECORD checksum, retained originals. No Blender binaries changed. uv pip check passes.
- Added upstream's undeclared SciPy dependency.
- Embedded bpy 4.2 segfaults during interpreter destruction after successful export on this host. Analysis helper validates the finished GLB and exits without running faulty global destructors. Blender server is independently terminated after its job.
- Deferred TokenRig model import in vendor/src/server/spec.py so the CPU-only Blender helper does not call CUDA device introspection. Scoped its HTTP server to loopback, configurable per-job port and bounded load timeout. Reproducible diff: backups/CHG-20261011-A100-003-rigging/skintokens-a100.patch.

## API and persistence
POST /api/jobs/<id>/rigging with JSON {preserve_original: true, analysis_faces: 50000, seed: 1234}; returns 202. Duplicate active requests return 409. GET the same route for status. Results stay under instance/outputs/<id>/rigging/<run UUID>/; versioned preview/download URLs preserve previous outputs and refresh a rerun correctly. Original generation state and model files remain separate. Interrupted rigging records are recoverable as failed after server restart; rerun explicitly. Ready marker: logs/rigging-ready.json.

Action artifacts: rig-actions-v2.glb, preview-actions-v2.glb, motion-actions-v2.json. The manifest is published atomically after both GLBs pass skin validation. GET /jobs/<id>/rigging/action-preview?v=<run UUID> and /motion-download?v=<run UUID>; status exposes motion_clips and motion_download_url only after the manifest exists. Asset paths are confined to the corresponding job/run. Clean /download and legacy /preview remain available. The original jobs.json did not need modification for backfill; status derives action metadata from the sidecar.

## Action validation (CHG-20261011-A100-004)
- DATA-20261011-A100-004-MOTION: 4 real clips on the existing 10-bone, 724,083-vertex AI rig; no new AI inference or GPU allocation.
- Animated GLB 71,695,624 bytes, 955,709 faces and two 4096px maps. Preview 55,348,168 bytes, identical geometry/UV/weights and two 2048px maps.
- Eight sampled CPU skin-deformation checks passed (all clips in both assets); Wave limb-only displacement ~0.0464 relative to model height ~1.0014. This verifies bone-driven limb motion beyond global translation. Clip endpoints match neutral pose and quaternion tracks are normalized.
- Source GLB binary data exactly retained in the animated download; all original geometry/UV/index/weight accessors exactly retained in the preview. Clean rig SHA256 bc325d256ea46456373d28ed8d1774e05885305215cd33f9c166f45baa62387e.
- Three real API download content checks, four missing/invalid version path checks and Jinja result rendering passed in an isolated DATA store. Existing 14-case rigging API validation also passed. Evidence: logs/motion-a100-validation.json and logs/motion-web-validation.json.
- Browser checks cover autoplay, all action selections, play/pause, reset, speed and seek; actual textured model and jumping/waving poses are verified separately from structural GLB checks.

## Validation
- Official giraffe: 70 bones, 14,885 skinned vertices, 21,636 faces, 54s; full monitored GPU1 maximum 6259MiB; GPU0 stayed 7917MiB.
- Actual browser-submitted TRELLIS.2 result: 10 bones, 724,083 skinned vertices, original 955,709 faces and 4096px textures retained, 77s. Rigged GLB 71,676,720 bytes; model/preview/download HTTP200 and SHA256 matching. Browser rendered the model and played/stopped JointCheck; Mixamo collapsed/expanded correctly; no browser console errors.
- Lightweight actual generated model: 14 bones, 48,637 skinned vertices, <=30,000 faces, 57s; texture retained. Full telemetry in logs/rigging-validation-generated-trellis2-optimized.json.
- 14 isolated API cases passed: readiness/input validation, queue, duplicate rejection, original-generation state, restart recovery, real GLB downloads, retained old results and versioned rerun URLs. Executor stubbed; no inference in API harness.
- Parent-death cleanup test stopped both owned process-group members. Python/Jinja/JS syntax and dependency checks passed; service and LAN health passed; all 107 production jobs preserved. Existing 3D generation defaults remain TRELLIS.2 1536 / 8K / 12 Steps.
- Browser download-event automation timed out; direct HTTP attachment, size and file-integrity verification passed. The in-app viewport override did not resize this test tab; mobile CSS is present but actual phone-width rendering was not verified.
- Initial failed attempts (Blender shutdown / CPU-helper CUDA import) retained under backups. Final evidence: logs/rigging-a100-validation.json, logs/rigging-api-validation.json, logs/rigging-web-validation.json and deployment-a100.json.

## Skeleton editor (2026.10.11.6)
Completed rigging results support 3D joint selection/translation, numeric coordinates, parent edits, branch duplication, child add/delete, weight display, undo/redo, arbitrary limb groups with angle/timing, AI skin-only recomputation on GPU1, immutable save versions and restoration. Details and tested limits: RIGGING-EDITOR-A100.md. Existing Mixamo option retained.
