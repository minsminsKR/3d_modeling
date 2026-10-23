# A100 deployment

## Shared web / CLI workspace (2026-10-23)

[CLI-A100.md](CLI-A100.md) documents the lightweight Python client. It submits to
this same running service, GPU queue and durable job store. CLI jobs appear in
the existing web result pages and saved models. No local GPU installation is
needed. The existing HTTPS gateway login and browser access remain supported.
Server: ai-server-04@192.168.0.16
Project: <checkout>/hunyuan3d-webapp
Change: CHG-20261011-A100-010 / version 2026.10.11.11
Validation data: DATA-20261010-A100-001 (upstream assets/demo.png, reduced smoke-test settings)

## Access
http://192.168.0.16:5001
Default model: TRELLIS.2, 1536 cascade / 8K PBR / 12 Steps / two-GPU shape 1 + material 0 / token cap 65536.
Hunyuan models: split shape GPU 0 / texture GPU 1; two isolated generation workers.
Auto mode assigns independent standard/shape-only jobs to both cards.
Each stage has a shared physical-GPU lock; native TRELLIS split holds both locks. GPU 0 retains about 8GB RTSP allocation and the native helper has a bounded allocator. No 80GB unified VRAM mode is used. Single-GPU native mode keeps token cap 49152; quick 512 preset uses independent two-GPU automatic jobs. See TRELLIS-A100.md for evidence and transfer behavior.
Shape steps / octree remain selectable from the web form.
Hunyuan quality defaults: 50 shape steps, Octree 512, 50000 decoding chunks.
Hunyuan PBR defaults: 6 views, 768 resolution, 2048 render, genuine 4096 texture maps.
Profiles: standard 512/4K; detail 768/4K; ultra 768/8K.
Omni uses the official EMA checkpoint. Model-viewer 4.4.0 is served locally.

## Service
systemctl --user status hunyuan3d-webapp
systemctl --user restart hunyuan3d-webapp
journalctl --user -u hunyuan3d-webapp -n 100
The user service is enabled with lingering for boot startup.
Gunicorn uses one process and four threads; multiple processes would split the in-memory queue.

## Environment
Python 3.10 .venv; torch 2.5.1+cu124; CUDA toolkit /usr/local/cuda (12.6).
requirements-a100.txt is the Linux dependency manifest.
Blender bpy 4.0 is installed from the official Blender archive because it was removed from PyPI.
requirements-a100.lock.txt records the installed environment after validation.
Upstream source: vendor/Hunyuan3D-2.1 and vendor/Hunyuan3D-Omni.
Weights: each upstream checkout's local_models directory.
Do not move .venv; recreate it at the destination instead.

## Setup / validation
bash scripts/setup_a100.sh
.venv-download/bin/python scripts/download_a100_models.py
.venv/bin/python scripts/check_env.py
.venv/bin/python scripts/smoke_a100.py
The smoke script creates real test jobs and preserves results under instance/outputs.
logs/smoke-a100.json records actual outcomes.

## Preserved data
Original .env: backups/env-before-a100-20261010 (private).
Original job/metadata JSONs: backups/imported-paths.
Imported Windows paths were migrated to this server.
Retention is disabled (HUNYUAN_UNSAVED_JOB_RETENTION_DAYS=0).
Application outputs and uploads stay under instance.
Logs: logs/webapp.log, logs/access.log, logs/setup-a100.log, logs/download-models.log.

## Verified on 2026-10-10
Shape, PBR texture, Omni, background removal, GLB downloads and browser PBR preview passed.
Functional smoke tests use 5 steps / octree 128; these tests do not measure production-quality generation.
Service restart and LAN health check passed. See deployment-a100.json and CHANGELOG-A100.md.

## Two-GPU validation
.venv/bin/python scripts/validate_dual_a100.py
Final evidence: logs/dual-a100-validation.json (DATA-20261010-A100-002).
Both 50-step / Octree-512 PBR tests passed with 4096 and 8192 maps in the downloaded GLB.
Peak total GPU allocation was 18.4 / 37.5 GiB on GPU 0 / 1; GPU 0 includes RTSP usage.
Inputs can change memory needs. Use the standard profile for lower texture memory.
See MODEL-UPDATES-A100.md for newer-model review and pending TRELLIS.2 integration.

## TRELLIS.2 enabled (CHG-20261010-A100-003)

Deployment 2026.10.11.2 enables official TRELLIS.2-4B with local DINOv3/RMBG-2.0 models. In the web form, select TRELLIS.2; its default settings are 1536 cascade, 12 steps per stage, 8K PBR, automatic placement on physical GPU 1. Native 512 automatic mode supports independent jobs on both GPUs. Native 1024/1536 uses GPU 1 while GPU 0 retains RTSP. Each native job uses one physical card with staged low-VRAM model loading; memory is not pooled.

Real tests passed for 512 on both GPUs, 1024/4K, 1536/8K, browser upload, GLB/PBR downloads and 3D preview. Native export keeps up to one million faces; UV export time varies with mesh topology (the browser test took 11m46s). All imported jobs and earlier Hunyuan results remain available. See TRELLIS-A100.md, CHANGELOG-A100.md and deployment-a100.json for exact revisions and evidence.

## Quality defaults (CHG-20261011-A100-001)

New jobs default to TRELLIS.2 / maximum 1536 / 8K PBR / 12 steps per stage / GPU 1 auto placement / background removal on / seed 1234 / max tokens 49152. GPU placement remains per physical card. This preset already passed real native generation with 14447 MiB total GPU 1 peak on the public demo; memory use and export time vary with input. 8K produces larger files and can take longer to export. Shape resolution may adapt to the token cap.

Environment keys: APP_DEFAULT_MODEL, TRELLIS_DEFAULT_GPU_MODE, TRELLIS_DEFAULT_PIPELINE, TRELLIS_DEFAULT_STEPS, TRELLIS_DEFAULT_TEXTURE_SIZE, TRELLIS_MAX_NUM_TOKENS, APP_DEFAULT_REMOVE_BACKGROUND, APP_DEFAULT_SEED. Screen defaults and omitted API fields share the same configuration. Explicit user selections still override defaults. Hunyuan selection uses its existing split / 50 steps / octree 512 / chunks 50000 / 768-4K defaults. Existing results are not regenerated.

Configuration/route tests used an isolated store and a stub executor (no inference repeated). Browser defaults/model switching and production health passed. Evidence: logs/quality-defaults-validation.json.


## Generation settings UI (2026.10.11.3)
- Compact two-column input/settings workspace with responsive single-column mobile layout.
- Quality presets show real model-specific parameters. Direct edits show Custom settings; reset restores the selected model's server defaults while retaining chosen images.
- Main controls show shape resolution, map quality, background removal and PBR. Advanced disclosure holds Seed, Steps and GPU; Hunyuan adds Guidance and Chunks.
- Image chooser/drop/paste and deletion remain available, with thumbnail preview and image count. Sidebar and submit area summarize the selected settings.
- Hidden model fields are disabled, mandatory native PBR stays on, GPU 0 high-resolution restrictions remain enforced, and invalid advanced fields open the disclosure automatically.
- Existing green/cream tokens and saved/recent model components reused. No external UI component/library installed; 21st catalog requires login and external-context/review calls were rejected by automatic approval review. Direct browser and isolated Flask request checks used.
- DATA-20261011-A100-002-UI: nine isolated serialization/guard checks, no inference; desktop / 390px / 320px, keyboard, model changes, custom values, reset, preview/deletion and native input validation verified.
- Evidence: logs/generator-ui-validation.json. Production jobs remain 107; generation defaults, dependencies and model weights are unchanged.


## AI auto-rigging (2026.10.11.4)
Completed results now support local SkinTokens / TokenRig auto-rigging on GPU1. Defaults: 50k analysis faces, transfer to original geometry/materials, seed1234. Download a rigged GLB and preview joint deformation; optional lightweight output and existing Mixamo workflow retained. Details, setup, compatibility fixes and actual validation: RIGGING-A100.md.

## Rigged action preview (2026.10.11.5)
AI rigging results now receive procedural Idle / Wave / Bow / Hop presets automatically; Wave is included when suitable lateral branches are detected. Select an action, play/pause, reset, seek or set 0.5/1/1.5/2x speed in the result page. Autoplay respects reduced motion. Download the animated GLB separately from the clean rig and original generated GLB. The preview retains original mesh/UV/weights and uses at most 2K maps; animated downloads retain original maps. Existing penguin example: /jobs/60d905f2-6aa9-43cf-8d8f-80f8fae302c8#motion-preview. This is AI rigging plus action presets, not a new AI motion-generation model. Mixamo remains optional. CHG-20261011-A100-004 / DATA-20261011-A100-004-MOTION; evidence logs/motion-a100-validation.json and logs/motion-web-validation.json.

## Skeleton editor (2026.10.11.6)
Completed rigging results support 3D joint selection/translation, numeric coordinates, parent edits, branch duplication, child add/delete, weight display, undo/redo, arbitrary limb groups with angle/timing, AI skin-only recomputation on GPU1, immutable save versions and restoration. Details and tested limits: RIGGING-EDITOR-A100.md. Existing Mixamo option retained.

## 외부 HTTPS 접속 · 비밀번호 보호

현재 외부 주소: https://YOUR-TUNNEL-HOST.example

비밀번호가 있어야 생성·리깅·다운로드를 사용할 수 있습니다. URL을 아는 것만으로 사용할 수 없습니다. 로그인한 사용자는 기존 작업 공간을 공유합니다. 임시 호스트는 터널 재시작 시 바뀌며, 고정 주소에는 사용자 도메인과 Cloudflare 계정이 필요합니다. 자세한 내용과 중지 방법은 [PRIVATE-ACCESS-A100.md](PRIVATE-ACCESS-A100.md)를 참조하세요.
