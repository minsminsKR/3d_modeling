from pathlib import Path
from PIL import Image
from rembg import new_session,remove
p=Path(__file__).resolve().parents[1]
session=new_session(providers=['CPUExecutionProvider'])
image=Image.open(p/'vendor/Hunyuan3D-2.1/assets/demo.png').convert('RGB')
result=remove(image,session=session)
assert result.mode=='RGBA' and result.getchannel('A').getextrema()[0]<255
result.save(p/'logs/DATA-20261010-A100-001-background.png')
print('BACKGROUND_COMPLETE',result.size,flush=True)
