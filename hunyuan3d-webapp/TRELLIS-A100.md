# TRELLIS.2 on A100

Current change: CHG-20261011-A100-009 / version 2026.10.11.10. Original native deployment: CHG-20261010-A100-003.
Remote: ai-server-04@192.168.0.16, <checkout>/hunyuan3d-webapp.
Web app: http://192.168.0.16:5001/

## Runtime and models

Official TRELLIS.2 revision 75fbf0183001ed9876c8dbb35de6b68552ee08bd is installed under vendor/TRELLIS.2. The isolated .venv-trellis uses Python 3.10 and Torch 2.6.0+cu124 with A100 native CUDA extensions. Hunyuan uses its separate .venv runtime.

Both DINOv3 and RMBG-2.0 access approvals and downloads are complete. Exact official model revisions are in logs/trellis-a100-models.json. Local pipeline-a100.json points to these local models and enables staged low-VRAM loading. Never put tokens in documentation, source files or chat.

## Usage

Select TRELLIS.2 in the web model menu. Native defaults: 1536 cascade, 12 steps per stage, 8K PBR, shape GPU 1 + material GPU 0, token cap 65536. Native PBR is always included. The quick 512 preset assigns independent image jobs to both cards; balanced/quality presets use split mode. Single GPU 1 remains selectable and retains cap 49152. VRAM is not combined between cards.

The maximum-quality tested mode is 1536 with actual 8192x8192 PBR maps. Native output decimation targets one million faces. UV export can take several minutes depending on topology. Input-dependent token limits may reduce a requested maximum resolution; the worker records actual resolution and reports a warning when that happens.

## Evidence

- DATA-20261010-A100-003: native 512 on GPU 0 and GPU 1 concurrently, 1024/4K, 1536/8K; all geometry, map dimensions and GLB checks passed. Recorded 19 simultaneous-GPU samples. Peak total memory: GPU 0 12386 MiB including RTSP; GPU 1 14447 MiB.
- DATA-20261010-A100-003-RMBG: native GPU background-removal test passed.
- DATA-20261010-A100-003-WEB: job 60d905f2-6aa9-43cf-8d8f-80f8fae302c8, 955709 faces, 54628340 bytes, 1024/4K, native metadata and HTTP preview/download checks passed. Actual browser rendering and model-specific controls verified.
- logs/trellis-a100-ready.json records the validated activation; logs/trellis-a100-validation.json and logs/trellis-a100-http-validation.json contain details.

Historical full single-GPU 0 high-resolution test was not executed. The later user-authorized split helper uses a bounded allocation while preserving RTSP; it does not run the whole high-resolution pipeline on shared GPU 0. No RTSP service was stopped.

## Reproducibility

scripts/setup_trellis_a100.sh and scripts/download_trellis_a100.py prepare the dedicated runtime and official local weights. scripts/validate_trellis_a100.py validates real native generation and preserves existing test directories; its --summarize mode assesses saved telemetry without rerunning inference. Existing test data and source/config backups are retained. The initial fixed-memory telemetry assessment was corrected to use measured idle baselines; actual native output checks passed throughout.

Source: https://github.com/microsoft/TRELLIS.2

## Historical quality defaults (CHG-20261011-A100-001; superseded by 009)

New jobs default to TRELLIS.2 / maximum 1536 / 8K PBR / 12 steps per stage / GPU 1 auto placement / background removal on / seed 1234 / max tokens 49152. GPU placement remains per physical card. This preset already passed real native generation with 14447 MiB total GPU 1 peak on the public demo; memory use and export time vary with input. 8K produces larger files and can take longer to export. Shape resolution may adapt to the token cap.

Environment keys: APP_DEFAULT_MODEL, TRELLIS_DEFAULT_GPU_MODE, TRELLIS_DEFAULT_PIPELINE, TRELLIS_DEFAULT_STEPS, TRELLIS_DEFAULT_TEXTURE_SIZE, TRELLIS_MAX_NUM_TOKENS, APP_DEFAULT_REMOVE_BACKGROUND, APP_DEFAULT_SEED. Screen defaults and omitted API fields share the same configuration. Explicit user selections still override defaults. Hunyuan selection uses its existing split / 50 steps / octree 512 / chunks 50000 / 768-4K defaults. Existing results are not regenerated.

Configuration/route tests used an isolated store and a stub executor (no inference repeated). Browser defaults/model switching and production health passed. Evidence: logs/quality-defaults-validation.json.


## Two-GPU native placement (CHG-20261011-A100-009)

Physical GPU 1 processes shape/mesh/export; GPU 0 processes material. Both physical app locks cover the native job. Only independent mesh repair and material decoding can overlap: dependent samplers remain sequential. This does not pool VRAM or change the model's parameter count. Quick 512 automatic mode runs independent images concurrently; high-quality split uses both cards for one image.

Every cross-GPU tensor passes through CPU memory because actual peer-copy correctness failed despite reported CUDA peer support. This host has SYS topology without NVLink. GPU 0's helper allocation is bounded while preserving its RTSP process; low available VRAM selects single-GPU fallback with a warning. Decoder OOM can retry on GPU 1. Single mode keeps cap49152; split cap65536 allows more native tokens but input-dependent adaptation may still reduce resolution.

Validation of the previously failing PNG: cache-safe single GPU produced actual1408/8K at cap49152; split produced actual1536/8K at cap65536, 988190 faces, 653.03s. These different resolutions are not a fair speed comparison. Same-input/seed/settings 512 tests took141.19s single versus138.29s split including startup. No 2x speed or artistic-quality claim follows from this small test.

Evidence: logs/trellis-dual-single512-validation.json, logs/trellis-dual-dual512-validation.json, logs/trellis-dual-high1536-validation.json, logs/trellis-dual-comparison.json, logs/trellis-dual-api-validation.json (15 checks), logs/trellis-peer-copy-observation.json. Registered result job: a2b07007-da21-466d-b147-0ccf19be6669.

Runtime adapter: scripts/trellis_device_pipeline.py; readiness/default gate: instance/trellis-dual-ready.json. Native upstream revision and weights remain unchanged. TRELLIS_SPLIT_MAX_NUM_TOKENS optionally controls split cap; TRELLIS_MAX_NUM_TOKENS controls single cap. Backups preserve original source and all failed diagnostic attempts.
