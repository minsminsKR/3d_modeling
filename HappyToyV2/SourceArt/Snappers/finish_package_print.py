"""Apply the reproducible reference-print finishing pass to an existing baked package blend."""
from pathlib import Path
import sys,json
import bpy
SOURCE=Path(__file__).resolve().parent;sys.path.insert(0,str(SOURCE))
import prepare_game_props as authoring
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'package.blend'))
obj=bpy.data.objects['SN_package'];destination=authoring.PROJECT/'Assets/Resources/Snappers/package'
entry=authoring.apply_package_print(obj,destination)
report=json.loads((destination/'asset.json').read_text(encoding='utf-8'));report['materials']=[v for v in report['materials'] if v['name']!=entry['name']]+[entry]
report['printFinishing']='Broad carton faces reproject the legible package-reference.png front panel; side/edge PBR and original generated GLB retained.'
report['gameFaces']=len(obj.data.polygons)
bpy.context.preferences.filepaths.save_version=0;authoring.select(obj);bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'package.blend'))
bpy.ops.export_scene.fbx(filepath=str(destination/'geometry.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='RELATIVE',use_mesh_modifiers=True)
for path in [destination/'asset.json',SOURCE/'package-export.json']:path.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
