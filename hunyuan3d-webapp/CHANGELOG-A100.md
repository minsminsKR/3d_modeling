# A100 deployment changes

## CHG-20261023-A100-CLI-IO

- Reduced unchanged status/cookie writes and indexed server history. Local
  request lookups use the UUID directly; paged local history reads selected files.
- Added shared immutable ZIP snapshots, strong SHA-256 ETags, conditional HEAD/GET,
  HTTP Range/If-Range and a configurable 10 GiB cache limit. Source/rig/provenance
  changes invalidate a version; eviction and abandoned-build cleanup affect only
  generated cache files and protect active downloads.
- Added durable download checkpoints, prefix verification and process locks.
  Download/collect/resume reuse remaining bytes across client restarts, verify
  final archives and preserve verified results on corrupt replacement downloads.
- Added isolated state/history/cache/real-HTTP interruption suites and reproducible
  benchmarks. See [CLI-PERFORMANCE.md](CLI-PERFORMANCE.md) for results and scope.

## CHG-20261023-A100-CLI-AI

- Added read-only capabilities and doctor endpoints/CLI commands. They expose
  supported parameters, actual API defaults, upload limits and configuration /
  CUDA / storage / default-GPU readiness without loading generation models.
- Added a shared versioned error contract: stable code, retryable, next_action,
  request ID and supported details. CLI HTTP/network/argument/timeout/integrity
  failures and API validation errors are machine-readable. The existing gateway's
  authentication errors are translated by the CLI without changing its login.
- Added request-level collect/resume, complete remote-history pagination and
  incremental result collection. A failed job or download does not discard other
  successes; timeout results retain completed downloads and pending IDs.
  Existing archives are reused only after receipt and full-content validation.
  Failed jobs are not automatically regenerated; uncertain submissions replay
  only their original UUID and preserved inputs after checking server history.
- Invalid API numeric options and unavailable explicit GPU IDs now return input
  errors instead of silently changing the requested setting. Existing web form
  handling and production generation defaults remain unchanged.
- Added `scripts/validate_cli_agent.py`; retained existing CLI/API, upload,
  GPU-allocation and private-gateway regression suites. CLI-A100.md documents the
  helper files, diagnostics, structured errors, recovery and partial results.
- A100 Linux validation: all 119 checks passed (29 existing CLI/API, 27 new agent
  workflows, 12 PNG upload, 15 GPU allocation and 36 gateway checks), with no skips.
  Real HTTPS capabilities/doctor, authentication and invalid-GPU errors passed.
- A real two-image batch deliberately failed one invalid PNG while the public
  demo completed using reduced Hunyuan settings (5 steps, Octree 128, GPU 1,
  no texture). Its archive was collected despite the other failure; collect and
  resume reused the verified archive without creating replacement GPU jobs.
  Web result/saved-model links passed and all 139 previous jobs were unchanged.
  Two additional validation jobs were retained. Diagnosis itself runs no inference.

## CHG-20261023-A100-CLI

- Added `modeling_cli.py`: existing-gateway login, generation submission, status,
  bounded wait, verified provenance ZIP downloads, and local/remote history.
- `POST /api/jobs` reuses the existing web upload validation, generation defaults,
  GPU executor and `instance/jobs.json`. UUID request IDs and persisted input/form
  fingerprints recover interrupted submissions without duplicate inference.
- API-created jobs are retained from submission and appear in existing web result
  pages and saved models after completion. Failed submissions remain in history.
- Job listing, safe generation records and export archives include original
  inputs, applied settings and SHA-256 manifests; generated GLB, OBJ/MTL, textures
  and editable/rigging sidecars are preserved when present. Public job JSON no
  longer exposes internal file paths or submission fingerprints.
- Passwords are entered privately or read from an owner-controlled file. Existing
  HTTPS session/CSRF/Origin protection is reused without a new public port.
- Added isolated web/API and real HTTP subprocess regression tests in
  `scripts/validate_cli_api.py`. Installation and recovery: [CLI-A100.md](CLI-A100.md).
- A100 validation on 2026-10-23: 29 CLI/API tests and 63 existing web/gateway
  regression checks passed. A real HTTPS CLI login, reduced Hunyuan generation
  (5 steps, Octree 128, GPU 1, no texture), wait, verified ZIP download, history,
  replay and web home/result/saved-model checks passed. This smoke test checks
  functionality; the existing production quality defaults remain unchanged.
- Deployment backed up the previous sources and all 138 existing job records,
  restarted only the idle GPU webapp and preserved those records exactly. One
  additional validation model was retained. Gateway, tunnel and model runtimes
  were not reconfigured.

## CHG-20261010-A100-001 (complete)
- Backed up original .env; converted Windows model paths to project vendor paths.
- Dedicated Python 3.10 venv, PyTorch 2.5.1 CUDA 12.4, CUDA extension build with toolkit 12.6.
- Default GPU 1, single generation worker; bind 0.0.0.0:5001.
- PBR preset: 6 views, 512 resolution, 2048 render, 4096 maps.
- Upstream 2.1 and Omni source checkouts retained under vendor.
- Existing jobs/outputs must be preserved during deployment.

- Migrated 305 Windows path references in jobs/metadata; original JSON files backed up.
- Disabled retention to preserve imported results.
- Fixed unreachable torch random seed initialization.
- Built custom_rasterizer (A100 sm_80) and mesh_inpaint_processor.
- Registered Gunicorn user service with lingering enabled.
- bpy 4.0 and ninja 1.11.1.1 official wheels contain incorrect/blank-separated WHEEL tags; metadata corrected to their published artifact filenames. Binary files were not changed. pip check now passes.
- First real Shape job completed and GLB geometry/download verified.
- Patched vendored 2.1 PBR loader to honor multiview_local_dir instead of duplicate downloads; corrected image list normalization. Original sources and reproducible git patch saved under backups.

### Validation completed
- Shape job 8399624f-5de2-488f-972a-d6438f4faa78: GLB 493292 bytes, geometry present, HTTP 200.
- PBR job d3aaa9cf-1159-45bc-a3cf-2c13bb81a3cc: used_texture=true, GLB 3831892 bytes, geometry present, HTTP 200; browser preview verified.
- Omni job 88488c67-f07d-4c32-8250-d75dd7ce6491: geometry and GLB download verified.
- DATA-20261010-A100-001: public upstream demo input, 5 steps / octree 128 for functional smoke testing; normal UI defaults remain 50 / 512.
- Background removal passed; model assets and compiled GPU extensions present; pip check passed.
- Service restart and /health passed; direct Windows LAN access passed; enabled service, Linger=yes.
- Original 92 jobs preserved; four validation jobs retained (including the first interrupted PBR download attempt).


## CHG-20261010-A100-002 (completed — version 2026.10.10.2)
- User requested two-A100 utilization and model/runtime updates.
- Latest upstream Hunyuan 2.1 / Omni commits confirmed unchanged from installed versions.
- TRELLIS.2 official release investigated; required DINOv3 and RMBG-2.0 access currently returns GatedRepoError (no authorized Hugging Face login).
- GPU 0 retains existing RTSP allocation (~8GB); GPU 1 is otherwise free. Topology SYS, no NVLink aggregation.
- Implemented isolated per-job and per-shape processes, cross-process GPU stage locks, two job workers, and split shape GPU 0 / PBR GPU 1 workflow.
- Fixed upstream checkpoint tensors retained after pipeline deletion by ending the dedicated shape process before unlocking the GPU.
- Added 6-view 512/768 PBR profiles with genuine 4K/8K exported maps; disabled upstream half-resolution export for app jobs and resized every selected enhanced view consistently.
- Default quality: 50 steps, Octree 512, chunks 50000, PBR 768/4K. High-resolution PBR is assigned to GPU 1; GPU 0 retains RTSP workload.
- Enabled official Omni EMA model variant; download helper updated for reproducibility.
- Bundled official model-viewer 4.4.0 locally with license and npm SHA512 verification.
- DATA-20261010-A100-002: five final real jobs passed, including independent two-GPU shapes, 50/512 shapes plus 4096 and 8192 PBR maps, and Omni EMA. Peak GPU memory 18856 / 38389 MiB; 24 telemetry samples recorded simultaneous active compute.
- First validation attempt exposed half-sized maps and retained shape weights; attempt preserved under backups/CHG-20261010-A100-002. Final evidence is logs/dual-a100-validation.json.
- Backups and updated vendored source patch saved under backups/CHG-20261010-A100-002. No imported jobs or outputs removed.

## CHG-20261010-A100-003 (complete - version 2026.10.11.1)
- Both DINOv3 and RMBG-2.0 account access approvals completed; official local model downloads and local pipeline configuration are ready. Exact model revisions recorded in logs/trellis-a100-models.json.
- Separate Python 3.10 / Torch 2.6.0+cu124 environment and native CUDA extensions retained; Hunyuan runtime stays on its separate Torch 2.5.1 environment.
- Native TRELLIS.2 generation and PBR export enabled after real inference validation: 512 on both physical GPUs; 1024 and 1536 on GPU 1; genuine 4096/8192 maps. Tested actual geometry resolutions matched all requested modes.
- DATA-20261010-A100-003: four native output checks passed, including simultaneous two-GPU 512 generation (19 telemetry samples), 1024/4K and 1536/8K. Peak total memory 12386 / 14447 MiB; GPU 0 peak includes the existing RTSP allocation.
- Replaced fixed Hunyuan-era telemetry thresholds with measured per-GPU baselines plus a 512 MiB rise and active utilization. Native output tests already passed; preserved original assessment under backups/CHG-20261010-A100-003/trellis-validation-before-telemetry-criteria-fix.json and reassessed the same recorded telemetry without repeating inference.
- DATA-20261010-A100-003-WEB: browser file chooser upload, TRELLIS.2 model-specific controls, real 1024/4K generation and downloads passed. Job 60d905f2-6aa9-43cf-8d8f-80f8fae302c8: 955709 faces, 54628340-byte GLB, HTTP 200 for preview and download, 706 seconds. Browser 3D rendering verified; no browser errors.
- Show generation model names in job pages; update model-specific GPU descriptions and single-GPU native display. Record actual native resolution and warn if upstream token limits reduce it.
- Gunicorn service restart/health and local/LAN access passed. All 107 jobs retained, including all imported jobs; retention remains disabled. No RTSP process was stopped.
- Optional GPU 0 1536/8K execution and server-side preparation were rejected by automatic approval review because GPU 0 shares the existing RTSP workload. Neither action ran; supported high-resolution native placement remains GPU 1. This does not block the enabled TRELLIS.2 feature.
- Completed at KST: 2026-10-11T00:24:30.900796+09:00. Evidence: logs/trellis-a100-validation.json, logs/trellis-a100-http-validation.json, deployment-a100.json.

### Historical preparation/access notes (superseded by completed status above)
- Official TRELLIS.2 source revision 75fbf0183001ed9876c8dbb35de6b68552ee08bd and public weights installed separately.
- Built an isolated Python 3.10 / Torch 2.6.0+cu124 environment and FlashAttention, nvdiffrast, nvdiffrec_render, CuMesh, FlexGEMM, and o_voxel CUDA dependencies for A100.
- CUDA backend imports and actual FlashAttention operation passed; dedicated environment pip check passed.
- Added native generation worker, whole-job physical GPU lock, 512/1024/1536 controls, and 4K/8K native PBR export options. Each TRELLIS job uses one physical GPU with low-VRAM staged loading.
- Added real inference validation script; planned DATA-20261010-A100-003 is not yet generated or validated.
- Valid Hugging Face server login and gated-model read token scope verified. Both DINOv3 and RMBG-2.0 authenticated config downloads still return HTTP 403: account model access approval is required.
- Selector and HTTP job creation remain disabled until native inference is validated; no ready marker exists.
- Syntax/templates, service restart/health, disabled-model HTTP 400 guard, existing 8K GLB download, and browser view passed. All 106 jobs preserved.
- Evidence: logs/trellis-a100-preparation.json. Source/private configuration backup: backups/CHG-20261010-A100-003.
- Remaining: authorized gated downloads, real native GPU/GLB/PBR tests, enable marker, and HTTP/browser generation validation. TRELLIS-A100.md documents the resume procedure.

### Model access update (2026-10-10T23:37:22.502354+09:00)
- RMBG-2.0 approved; downloaded official revision 5df4c9c76d8170882c34f6986e848ee07fd0ba43.
- DATA-20261010-A100-003-RMBG: official TRELLIS BiRefNet loader executed on physical GPU 1, 500x500 RGBA output with alpha range 0-255; peak Torch allocation 3209 MiB. Validation passed.
- DINOv3 returns HTTP 403 with explicit awaiting-author-review status. Full native 3D inference remains pending and selector stays disabled.

## CHG-20261011-A100-001 (complete - version 2026.10.11.2)
- User requested server-appropriate defaults based on completed generation results, with earlier preference for quality.
- Applied the highest validated native preset as the new-job default: TRELLIS.2, 1536 cascade, 8192 PBR, 12 steps per stage, 49152 max tokens, background removal on and seed 1234.
- Native auto placement uses physical GPU 1 at 1024/1536; choosing 512 permits previously validated independent two-GPU placement. No GPU 0 high-resolution inference was enabled or executed.
- Retained Hunyuan 50/512/50000 and 768/4K defaults. Switching to Hunyuan restores split shape GPU 0 / texture GPU 1; switching to TRELLIS restores its auto mode.
- Centralized environment-driven web/API defaults and exposed active defaults in health diagnostics. Explicit submitted parameters continue to override defaults.
- Added an explicit unchecked background-removal form value so default-on requests and deliberate checkbox-off requests are distinguished correctly.
- DATA-20261011-A100-001: isolated Flask route tests with executor stubbed passed for omitted-field defaults, explicit background off, checked checkbox values, Hunyuan split defaults and explicit parameter overrides. No model inference repeated; preset inference evidence is the existing DATA-20261010-A100-003 1536/8K success (GPU 1 total peak 14447 MiB).
- Actual browser initial selections, model GPU-mode switches and no-error console verification passed. Production restart/health passed; all 107 production jobs retained.
- Backup: backups/CHG-20261011-A100-001, including private .env mode 0600. Evidence: logs/quality-defaults-validation.json.
- Quality-first selection within tested settings; higher map size increases file size and export time and does not guarantee better perceptual quality for every input.
- Applied at KST: 2026-10-11T00:52:49.405782+09:00.


## CHG-20261011-A100-002 (complete - version 2026.10.11.3)
- Redesigned generation-page parameter UI: smaller heading, image/settings columns, consistent 44px fields, model-specific fast/balanced/quality presets, selected-preset/custom indicator and live settings summaries.
- Moved Steps/Seed/GPU and Hunyuan Guidance/Chunks into a keyboard-accessible advanced disclosure. Added reset without removing selected images; native PBR remains mandatory.
- Retained existing green/cream tokens, self-hosted model-viewer and saved/recent components. New styles are generation-page scoped; responsive layout verified down to 320px, including readable recent-work rows.
- Added image thumbnails and selected-image count to the existing chooser/drop/paste flow; existing removal retained and object URLs released on rerender.
- Disabled hidden model fields to avoid invisible validation failures; advanced invalid fields open automatically. Preserved GPU0 high-resolution server guards and disabled corresponding options in the UI.
- Fixed model-select input/change event ordering so forced native PBR does not overwrite a previous Hunyuan texture-off choice.
- DATA-20261011-A100-002-UI: nine isolated Flask cases passed with executor stubbed, seven captured requests and two GPU0 guards; no actual model inference. Initial validation harness template-root issue repaired before tests passed; production app unaffected.
- Actual browser checks passed: native presets/default, manual/custom state, Hunyuan and Omni transitions, per-model default GPU, texture state, reset, image preview/deletion, keyboard Enter, invalid advanced disclosure, 390px/320px no horizontal overflow; no console errors.
- 21st context initialization completed locally. Project-context search and review rejected by automatic approval review due to possible external code/derived metadata transmission. Context-free catalog requires login. No external component code or generated drafts used; direct local and browser review completed.
- Backups: backups/CHG-20261011-A100-002-ui. Restart performed only with empty queues. Health, static resources, saved page and existing job page HTTP 200; all 107 production jobs retained. Backend defaults, GPU service behavior, models and runtime dependencies unchanged.
- Evidence: logs/generator-ui-validation.json, deployment-a100.json. Completed at KST: 2026-10-11T01:27:00.447474+09:00.


## CHG-20261011-A100-003 (complete - version 2026.10.11.4)
- Installed official SkinTokens / TokenRig 273b691d35989d71cd17ff2895fdc735097b92d1 in independent Python3.11.15 / Torch2.7.0+cu126 / FA2.8.3 environment. Local checkpoint/Qwen config revisions recorded; inference offline. Existing generation runtime, driver and defaults retained.
- Added isolated CPU analysis and GPU1 skeleton/skinning workers, 30k/50k/100k analysis budgets, original-material/geometry transfer or lightweight output, normalized GLB validation and a separate JointCheck preview. Original downloads stay intact.
- Integrated asynchronous rigging API/status into completed job pages with duplicate prevention, single rig executor, shared physical GPU1 stage lock, bounded worker lifetime, parent-death process-group cleanup and restart recovery. Versioned per-run model/preview/download routes retain historical successful files.
- Added AI auto-rigging UI, seed advanced control, current status, bone/vertex/face metrics, play/stop and download. Existing Mixamo export/import retained as a collapsed option; no Adobe login automation added. Reused project form/panel/model-viewer primitives; no external UI components installed.
- Corrected official bpy wheel's incorrect cp39 tag to its published cp311 artifact tag and RECORD; added undeclared SciPy. Validated bpy analysis output before bypassing crashing interpreter destructors. Deferred upstream model imports for CPU Blender helper and confined it to dynamic loopback ports. Original files/failed logs and reproducible vendor patch retained in backups/CHG-20261011-A100-003-rigging.
- DATA-20261011-A100-003-RIG: official giraffe 70 bones / 14885 skinned vertices / 54s. Actual web TRELLIS.2 output 10 bones / 724083 skinned vertices / original 955709 faces / 4K maps / 77s. Lightweight native output 14 bones / 48637 skinned vertices / <=30k faces / 57s. Original generation state unchanged.
- 14 isolated API checks passed; no inference in API harness. Real file downloads HTTP200, hashes, normalized weights and nine valid JointCheck TRS channels verified. Browser rendered, played/stopped animation and toggled Mixamo; no console errors. Browser download-event capture timed out but direct HTTP attachment/integrity passed. In-app viewport override did not apply to this tab; mobile rendering not claimed as verified.
- Dependency/syntax/process cleanup, idle service restart and health checks passed. All107 production jobs retained; GPU0 stayed7917MiB existing RTSP. No GPU0 rigging or other high-resolution GPU0 test ran.
- Evidence: RIGGING-A100.md, logs/rigging-a100-validation.json, deployment-a100.json. Completed at KST: 2026-10-11T02:39:41.597117+09:00.

## CHG-20261011-A100-004 (complete - version 2026.10.11.5)
- User requested visible action motion on the AI-rigged generation result.
- Added procedural Idle/Wave/Bow/Hop action clips on SkinTokens-inferred bones. No additional AI motion model or inference run; geometric lateral-branch mapping and world-axis rotations use the actual AI skin.
- Future AI rigging workers automatically create full-resolution animated GLB and CPU-only web preview after inference. Backfilled the existing 10-bone penguin run without changing jobs.json or original generation/rigged GLBs.
- Added action selection, autoplay after load (except reduced motion), play/pause, reset, 0.5/1/1.5/2x speed, keyboard/seekable timeline and separate animated download. Corrected model-viewer/Three AnimationMixer timeScale behavior during seeking. Mixamo retained as a collapsed option.
- Rejected an initial decimated preview after actual browser QA showed UV-island corruption. Final preview preserves positions, UVs, indices, weights and bone mapping exactly and only resizes texture pixels to 2K. Animated download retains all original image bytes and mesh data. Original unanimated rig remains downloadable.
- DATA-20261011-A100-004-MOTION: full GLB 71,695,624 bytes, preview 55,348,168 bytes; 955,709 faces / 724,083 vertices / 10 bones. Full maps 4096px; preview 2048px. Eight real skin-deformation checks passed, including lateral-bone deformation distinct from whole-model movement. Neutral loop endpoints and quaternion normalization passed.
- Three actual download-content comparisons, four invalid/missing version path guards, Jinja rendering and the existing 14-case rigging API suite passed in isolated DATA stores. Python/JS checks and actual textured browser playback passed; no browser console errors. Current mobile viewport override limitation remains explicitly unverified.
- Production service active; all 107 production jobs and TRELLIS.2 1536/8K/12 defaults preserved. No GPU inference repeated and RTSP GPU0 allocation unchanged.
- Evidence: logs/motion-a100-validation.json, logs/motion-web-validation.json, deployment-a100.json. Backup: backups/CHG-20261011-A100-004-motion. Decimation experiments retained as historical actions-v1 assets but not served by the final action preview.

## CHG-20261011-A100-005 (complete - version 2026.10.11.6)
- Added a self-hosted Three.js skeleton editor to completed rigging results: actual joint/gizmo selection and drag, XYZ/name/parent edits, limb branch duplication, child add/delete, transparent view and selected-joint weight display, undo/redo.
- Added arbitrary arm/leg/head/other groups with amplitude/timing, whole-group and individual-group preview and exported GroupAction/Limb_* clips. Up to 128 joints and 32 groups; source-generated rig is not constrained to two arms.
- CPU saves preserve old weights and rebind rest pose. Add/delete/reparent edits require existing-skeleton-conditioned TokenRig skin-only inference on GPU1. Known hierarchy/quantization are validated, then authored coordinates and labels restored exactly. 2K editor texture preview retains original geometry/UVs/node indices; original maps retained in downloads.
- Corrected upstream custom-armature object naming, complete old-weight initialization, conditioned skeleton registration instead of ambiguous symmetric geometry PCA, and trimming of new zero-influence bones. Reproducible parser patch and pre-editor SHA-verified module backup retained under backups/CHG-20261011-A100-005-editor.
- Versioned asynchronous save, history/loading/active-result restoration, original downloads, stale/duplicate rejection, shape/range/group/path guards and original generation state preserved. Ready marker/model/runtime environments unchanged; no extra AI model installed. Three.js 0.186.1/esbuild 0.25.12 exact lock and local bundle with licenses retained.
- DATA-20261011-A100-005-EDITOR: real CPU worker and isolated API save/history/restore/download tests passed, including ten rejected invalid edits and neutral/source binary checks. Independent store UUID per test preserves earlier artifacts.
- DATA-20261011-A100-005-EDITOR-GPU: real edited-penguin skin-only inference (10 bones, 955709 faces, 4K maps, 122s) and four-arm/two-leg synthetic inference (19 bones, six positive-influence groups, 33s) passed on GPU1. Torch allocation about 2013/2014MiB; reservations 2670/2794MiB. All six individual limb clips move vertices (arms about .0654, legs about .0305 normalized units).
- Authored joint error ~1e-16; sampled original geometry error <=5.96e-8; neutral pose preserved. Native browser confirms original textures/weight display, group motion, actual drag, point/structure editing, real production CPU save and restore, with no browser errors or desktop horizontal overflow. Phone viewport is not claimed verified.
- Production test version d882d6ef-bbc3-4f4a-8826-a2a5d034fccf retained in history; original rig version 74961c0a-b5d6-4d1e-925a-c7d537cb1fa8 restored active. All 107 generation jobs and quality defaults retained; GPU0 RTSP remains 7917MiB. Service active, generation/rigging queues idle.
- Existing 14-case rigging API and action/geometry/download checks pass. Evidence: logs/rig-editor-validation.json, logs/rig-editor-gpu-validation.json, logs/rig-editor-web-validation.json and deployment-a100.json. Validation example: /static/validation/rig-editor-multi.html.

## CHG-20261011-A100-006 (complete - version 2026.10.11.7)
- Added an independently deployed password-protected streaming gateway on loopback 5002 and HTTPS Cloudflare Quick Tunnel. Existing GPU app, LAN endpoint and other server services remain unchanged. Only the authenticated gateway is externally proxied; metrics bind loopback 5004.
- Secure/HttpOnly/Strict cookies, scrypt password hash, 12-hour sessions, login/logout CSRF, same-HTTPS-Origin mutation guard, eight failed attempts/15min/IP, host allowlist, all-asset/API authentication, no public caching/indexing and HTTP-to-HTTPS redirection. Unknown /access/* paths do not bypass authentication. Credentials/cookies are not passed to the GPU app.
- Proxy preserves streaming binary bytes, Range/HEAD, attachment headers, request bodies and relative redirects. Public uploads capped at 90MiB. No new model/GPU inference; 107 original jobs, app.py SHA and quality defaults preserved.
- Official cloudflared 2026.10.0 installed from Cloudflare GitHub with release SHA256 d33ff2d14475178d2012c2c56beba87389ac5ded27649519f198a7d3134a99db verified. Both new user services enabled. No router/firewall/SSH/RTSP changes.
- DATA-20261011-A100-006-GATEWAY: 32 auth/CSRF/path/cookie/rate-limit/upload/real-streaming checks. Sixteen live loopback checks and eighteen real HTTPS checks pass, including protected GLB bytes and two-GPU backend connectivity. Real browser login and production page load confirmed without console errors. Password remains in owner-only server file and Windows-user/SYSTEM-only local delivery file; never printed in logs/chat.
- Current external URL: https://YOUR-TUNNEL-HOST.example. This is a temporary Quick Tunnel hostname and changes on tunnel restart; no uptime guarantee. Stable production hostname requires user-controlled Cloudflare account/domain and Named Tunnel. No third-party account created or terms accepted.
- Shared-password users share the existing workspace/models/actions. Original LAN address retains its existing access behavior. Evidence: logs/private-gateway-validation.json, logs/private-access-local-validation.json, logs/private-access-public-validation.json, logs/private-access-web-validation.json. Documentation: PRIVATE-ACCESS-A100.md.

## CHG-20261011-A100-007 (complete - version 2026.10.11.8)
- Fixed image uploads with non-ASCII stems: secure_filename('캐릭터.png') previously became 'png', then extension validation rejected valid PNG with HTTP 400. Validate the original basename extension, sanitize/truncate the stem independently, use image as fallback, and append the validated lowercase extension.
- Preserve the original basename in source_filename for readable job names. Actual disk paths remain confined to separate UUID upload directories; original PNG bytes and RGBA/transparent pixels are unchanged.
- DATA-20261011-A100-007-PNG: twelve real isolated multipart/storage checks cover Korean/uppercase/emoji/Japanese names, Windows/traversal paths, hidden/long names, invalid suffix rejection and decoded alpha pixels. Executor captures requests without GPU inference. Two authenticated public HTTPS PNG uploads pass extension validation and stop deliberately at invalid model validation before production job creation.
- Production jobs before/after public checks: 107/107. Service restarted only after generation and rigging queues were idle. Existing public URL, credentials, gateway, model/quality settings retained. Evidence: logs/png-upload-validation.json. Backup: backups/CHG-20261011-A100-007-png.

## CHG-20261011-A100-008 (complete - version 2026.10.11.9)
- Corrected private gateway redirect forwarding. requests.Session consumes redirect response content while computing Response.next even with allow_redirects=False. Streaming the raw response thereafter sent zero bytes under the upstream Content-Length, producing Cloudflare EOF/1033 after a successful image POST.
- Reproduced the failure with a new redirect-body-length test against the previous gateway. Redirects now forward the cached body with recalculated Content-Length and removed content-encoding; full models/downloads retain raw streaming. Relative redirect targets and authentication preserved.
- DATA-20261011-A100-008-REDIRECT: 36 auth/path/streaming/GET-and-POST-redirect tests pass, including actual HTTP sockets and byte-length integrity. Live protected Cloudflare GET 308 returns 313 complete bytes and redirects to the user's actual job, whose page returns HTTP 200.
- Existing hostname and credentials retained. Restarted only the gateway, leaving the GPU webapp/tunnel and user's TRELLIS.2 GPU1 job 80cf271f-bc1f-41fd-b5c5-bef44908e4f9 running. Production job count 108; no duplicate generation or GPU inference added.
- Reviewed existing hosted study apps via Sites read-only metadata: they use chatgpt.site hosting. No study app or audience modified, no new Site/domain/account created. Fixed-hostname migration still separate from this proxy correctness fix.
- Evidence: logs/private-gateway-redirect-validation.json, logs/private-gateway-redirect-web-validation.json. Backup: backups/CHG-20261011-A100-008-redirect.


## CHG-20261011-A100-009 (complete - version 2026.10.11.10)
- User-authorized native TRELLIS.2 two-GPU generation: physical GPU 1 shape/mesh/export, physical GPU 0 material sampling/decoding; independent mesh repair can overlap material decoding. Both GPU locks and queue counters are held/released; fast 512 automatic jobs still distribute separately.
- Added project adapter without modifying official weights/vendor source. Reclaims sparse scratch and allocator caches before native repair/export; skips no-op simplification. Single GPU 1 remains available.
- This server reports CUDA peer access but direct GPU 1-to-0 tensor copies reproduced all-zero/corrupt guides. Synchronous CPU staging now covers all cross-GPU conditioning/features/coordinates/topology. No driver/IOMMU/PCIe/RTSP configuration was changed. Evidence: logs/trellis-peer-copy-observation.json, debug_shape_transfer.py.
- GPU 0 helper allocator capped at min(70%, current-free-minus-4GiB/total). Existing RTSP allocation retained. Low-free helper falls back to single GPU and 49152 tokens; material-decoder OOM retries existing sampled codes on GPU 1 without regenerating/downscaling PBR.
- Defaults: 1536 cascade / 12 steps per stage / 8K / two-GPU split / 65536 max tokens / background removal / seed 1234. Single GPU cap stays 49152. Web/API defaults and result GPU labels updated.
- DATA-20261011-A100-009-DUAL: same PNG as failed job 80cf271f; single512 and dual512 actual 512, 4K maps passed. Small-test wall times 141.19s vs 138.29s; not evidence of 2x acceleration. Shape extent difference <0.005%, face-count difference <0.041%; nonconstant PBR maps verified.
- High split test: actual 1536, two 8192 maps, 988190 faces, 199625240 bytes, generation 653.03s; peak total memory {'0': 33616, '1': 37667}. Material decode fallback: True. Report: logs/trellis-dual-high1536-validation.json.
- User-created single-GPU recovery b41e251c succeeded at actual 1408/8K with cap49152 and new cache cleanup (545.59s); it is not a dual-GPU result. Original failure was one-GPU native repair OOM.
- DATA-20261011-A100-009-API: 15 isolated multipart/default/physical-placement/queue/native-guard checks passed. Failed diagnostic attempts and original sources retained under backups/CHG-20261011-A100-009-dual-gpu.
- Validated high output registered as completed job a2b07007-da21-466d-b147-0ccf19be6669 without rerunning inference. All 109 prior job IDs preserved; originals/rigs and private gateway/tunnel untouched. GPU workers returned to measured idle baselines.
- Public browser verification completed: default split 1536/8K/12, quick-preset automatic two-GPU distribution, reset defaults, result GPU labels/fallback warning, actual textured 3D rendering; no browser errors. Preview/download GLB range/version/199625240-byte length and changed-source hashes passed. All three web/gateway/tunnel services remain active. Evidence: logs/trellis-dual-browser-validation.json and logs/trellis-dual-deploy-validation.json. Screenshot: outputs/dual-gpu-20261011/a100-dual-gpu-result.png.


### CHG-20261011-A100-010 preparation — activation awaiting explicit public-access approval
- Prepared configurable gateway access with separate access-policy.json boolean; no policy defaults to password protection. Changed private_gateway.py on disk, but did not restart its service or activate a public policy. Existing credentials remain untouched.
- Protected-mode 36 regression checks and passwordless fixture 18 checks passed; no real GPU work submitted. Code and docs backed up under backups/CHG-20261011-A100-010-passwordless.
- Automatic approval review rejected activate_passwordless.py before execution because permanent password removal broadly exposes prior results/downloads/GPU operations, and the owner must explicitly confirm after understanding this scope. Requested that approval; no bypass was attempted.
- Verified current external anonymous home still redirects302 to login, API401, and no access-policy exists. Current public address and running app/gateway/tunnel remain unchanged. Evidence: logs/passwordless-access-preparation.json, logs/passwordless-gateway-validation.json.


## CHG-20261011-A100-010 (complete - version 2026.10.11.11)
- Final scope: owner-selected simple password with authentication retained. Owner superseded the rejected anonymous-access request. Restored protected gateway source; no access-policy was activated.
- Rotated scrypt password hash and auth_epoch using protected password-next input, preserving secret_key. Updated mode0600 owner delivery file; old credentials backed up mode0600 inside a mode0700 backup directory. Password content is absent from source/logs/docs.
- Restarted gateway only. Public new-password login303/home200 and revoked old-session302 passed; anonymous home302/API401 remain protected. GPU app/tunnel PIDs and app.py hash unchanged; all 110 jobs preserved. No GPU inference submitted.
- DATA-20261011-A100-010-PASSWORD: logs/access-password-rotation-validation.json; existing URL https://YOUR-TUNNEL-HOST.example retained.

## CHG-20261011-A100-011 · GitHub source handoff

- Exported the validated 2026.10.11.11 runtime to the existing minsminsKR/3d_modeling repository.
- Included editable rig editor source, locked build tools, local viewer license, synthetic QA fixture, upstream revision pins and Hunyuan/SkinTokens compatibility patches.
- Added relative-path clone/setup instructions and A100 environment template; corrected setup working directory, pinned extension cloning and the isolated dual API test source path.
- Excluded passwords, tokens, live .env, credentials, models, user jobs, logs, backups and environments. Public docs omit the active tunnel hostname.
- This source handoff does not restart or redeploy the running GPU app. Server validation logs remain server-local; they are historical evidence, not bundled test inputs.
