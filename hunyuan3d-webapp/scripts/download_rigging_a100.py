"""Download the exact public TokenRig checkpoints and Qwen config used on A100."""
from pathlib import Path
from huggingface_hub import hf_hub_download, snapshot_download

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT / 'vendor/SkinTokens'
for name in ('experiments/skin_vae_2_10_32768/last.ckpt',
             'experiments/articulation_xl_quantization_256_token_4/grpo_1400.ckpt'):
    hf_hub_download('VAST-AI/SkinTokens', name, revision='79736cad0fd84de384d5eede659b4ebd24effe33', local_dir=REPO)
snapshot_download('Qwen/Qwen3-0.6B', revision='c1899de289a04d12100db370d81485cdf75e47ca',
                  local_dir=REPO / 'models/Qwen3-0.6B', ignore_patterns=['*.bin', '*.safetensors'])
print('RIGGING_MODELS_READY')
