from pathlib import Path
from huggingface_hub import hf_hub_download
p=Path(__file__).resolve().parents[1]
hf_hub_download('tencent/Hunyuan3D-Omni','model/pytorch_model_ema.bin',local_dir=p/'vendor/Hunyuan3D-Omni/local_models')
print('EMA_DOWNLOAD_COMPLETE',flush=True)
