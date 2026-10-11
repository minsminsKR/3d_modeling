"""Download official public weights, then require authorized gated-model access."""
import json
from pathlib import Path

from huggingface_hub import hf_hub_download, snapshot_download

ROOT = Path(__file__).resolve().parents[1]
MODELS = ROOT / 'vendor/TRELLIS.2/local_models'
model = MODELS / 'TRELLIS.2-4B'
snapshot_download('microsoft/TRELLIS.2-4B', local_dir=model, ignore_patterns=['*.png', '*.jpg', '*.mp4'], max_workers=3)
decoder = MODELS / 'TRELLIS-image-large'
for suffix in ['json', 'safetensors']:
    hf_hub_download('microsoft/TRELLIS-image-large', 'ckpts/ss_dec_conv3d_16l8_fp16.' + suffix, local_dir=decoder)
print('PUBLIC_TRELLIS_WEIGHTS_READY', flush=True)

# These checks intentionally fail until both access approvals and login exist.
gated = [('facebook/dinov3-vitl16-pretrain-lvd1689m', MODELS / 'DINOv3'), ('briaai/RMBG-2.0', MODELS / 'RMBG-2.0')]
for repo, directory in gated:
    snapshot_download(repo, local_dir=directory, ignore_patterns=['*.png', '*.jpg', '*.mp4', '*.onnx', '*.pth', '*.bin'], max_workers=3)
config = json.loads((model / 'pipeline.json').read_text())
args = config['args']
args['models']['sparse_structure_decoder'] = str(decoder / 'ckpts/ss_dec_conv3d_16l8_fp16')
args['image_cond_model']['args']['model_name'] = str(MODELS / 'DINOv3')
args['rembg_model']['args']['model_name'] = str(MODELS / 'RMBG-2.0')
args['low_vram'] = True
(model / 'pipeline-a100.json').write_text(json.dumps(config, indent=2))
print('TRELLIS_MODEL_DOWNLOAD_COMPLETE', flush=True)
