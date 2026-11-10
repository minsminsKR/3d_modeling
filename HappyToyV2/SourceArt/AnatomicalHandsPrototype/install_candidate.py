"""Install exported prototype and its draft maps; run Unity PlayerItemsImport.ApplyHand after.
Requires Pillow as specified in Tools/requirements-authoring.txt.
"""
from pathlib import Path
import shutil
from PIL import Image
s=Path(__file__).resolve().parent
p=s.parents[1]
shutil.copyfile(s/'candidate-geometry.fbx',p/'Assets/Resources/PlayerHandAnimation/geometry.fbx')
shutil.copyfile(s/'draft-skin-2048-nails.png',p/'Assets/Resources/PlayerItems/hand/albedo.png')
shutil.copyfile(s/'candidate-retargeted-poses.json',p/'Assets/Resources/PlayerHandAnimation/poses.json')
Image.new('RGB',(4,4),(0,180,0)).save(p/'Assets/Resources/PlayerItems/hand/metalrough.png')
Image.new('RGB',(4,4),(128,128,255)).save(p/'Assets/Resources/PlayerItems/hand/skin-normal.png')
