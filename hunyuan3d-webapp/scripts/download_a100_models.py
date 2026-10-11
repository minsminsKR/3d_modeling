from pathlib import Path
from huggingface_hub import snapshot_download, hf_hub_download
import urllib.request
p=Path(__file__).resolve().parents[1]
r=p/'vendor/Hunyuan3D-2.1'
for sub,target in [('hunyuan3d-dit-v2-1',r/'local_models/tencent/Hunyuan3D-2.1'),('hunyuan3d-paintpbr-v2-1',r/'local_models/Hunyuan3D-2.1')]:
    print('Downloading',sub,flush=True)
    snapshot_download('tencent/Hunyuan3D-2.1',allow_patterns=[sub+'/*'],local_dir=target,max_workers=4)
print('Downloading DINOv2',flush=True)
snapshot_download('facebook/dinov2-giant',ignore_patterns=['*.bin','*.msgpack','*.h5'],local_dir=r/'local_models/facebook--dinov2-giant',max_workers=4)
dest=r/'hy3dpaint/ckpt/RealESRGAN_x4plus.pth'
dest.parent.mkdir(parents=True,exist_ok=True)
if not dest.exists(): urllib.request.urlretrieve('https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.0/RealESRGAN_x4plus.pth',dest)
print('Downloading Omni',flush=True)
snapshot_download('tencent/Hunyuan3D-Omni',local_dir=p/'vendor/Hunyuan3D-Omni/local_models',ignore_patterns=['*.mp4','*.png','*.jpg','*_ema.bin'],max_workers=4)
print('Downloading Omni EMA',flush=True)
hf_hub_download('tencent/Hunyuan3D-Omni','model/pytorch_model_ema.bin',local_dir=p/'vendor/Hunyuan3D-Omni/local_models')
print('DOWNLOAD_COMPLETE',flush=True)
