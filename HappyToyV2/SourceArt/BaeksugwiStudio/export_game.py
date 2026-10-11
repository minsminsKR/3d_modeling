"""Export the reviewed full-resolution rig for Unity and its 2K PBR maps.
Blender 4.0.2: blender --background --python export_game.py
"""
from pathlib import Path
import bpy,json,hashlib
HERE=Path(__file__).resolve().parent
OUTPUT=HERE.parents[1]/'Assets/Resources/Baeksugwi'
OUTPUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(HERE/'Baeksugwi_Run.blend'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers)]
assert len(meshes)==1
rig.animation_data.action=bpy.data.actions['Run_InPlace']
bpy.context.scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for o in meshes+[rig]:o.select_set(True)
bpy.context.view_layer.objects.active=rig
target=OUTPUT/'Baeksugwi.fbx'
bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'MESH','ARMATURE'},
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',global_scale=1,
    add_leaf_bones=False,use_armature_deform_only=True,bake_anim=True,
    bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
    bake_anim_step=1,mesh_smooth_type='FACE',use_tspace=True,path_mode='AUTO',embed_textures=False)
report=dict(source='Baeksugwi_Run.blend',sourceSha256=hashlib.sha256((HERE/'Baeksugwi_Run.blend').read_bytes()).hexdigest(),
    fbx='Assets/Resources/Baeksugwi/Baeksugwi.fbx',fbxSha256=hashlib.sha256(target.read_bytes()).hexdigest(),
    bones=len(rig.data.bones),vertices=sum(len(o.data.vertices) for o in meshes),clip='Run_InPlace',
    note='Exact reviewed mesh and skin; in-place clip lets the original NavMeshAgent own locomotion.')
(HERE/'game-export.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('UNITY_FBX_EXPORTED',report,flush=True)
