"""Re-evaluate the V1 weight field at the cleaned surface, in a separate candidate."""
import bpy, json, math
from pathlib import Path
from mathutils.kdtree import KDTree

root=Path(__file__).resolve().parent.parent
out=root/'Assets/Art/CandidateShapeSkin/Cyclopse'
out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Art/V1/Cyclopse/Walking.fbx'))
source=next(o for o in bpy.context.scene.objects if o.type=='MESH')
names={g.index:g.name for g in source.vertex_groups}
weights=[{names[g.group]:g.weight for g in v.groups} for v in source.data.vertices]
tree=KDTree(len(weights))
for v in source.data.vertices:tree.insert(v.co,v.index)
tree.balance()
report={}
for clip in ('Walking','Run'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Art/CandidateShape/Cyclopse'/(clip+'.fbx')))
    obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    mesh=obj.data
    uv_before=[tuple(loop.uv) for loop in mesh.uv_layers.active.data]
    positions=[tuple(v.co) for v in mesh.vertices]
    for group in obj.vertex_groups:group.remove(list(range(len(mesh.vertices))))
    for v in mesh.vertices:
        blended={}
        for co,index,distance in tree.find_range(v.co,.24):
            influence=math.exp(-distance*distance/(2*.08*.08))
            for name,weight in weights[index].items():blended[name]=blended.get(name,0)+weight*influence
        assert blended,'No source weights near cleaned vertex'
        total=sum(blended.values())
        kept={name:weight/total for name,weight in blended.items() if weight/total>1e-6}
        normalizer=sum(kept.values())
        for name,weight in kept.items():
            group=obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
            group.add([v.index],weight/normalizer,'REPLACE')
    assert positions==[tuple(v.co) for v in mesh.vertices]
    assert uv_before==[tuple(loop.uv) for loop in mesh.uv_layers.active.data]
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    start,end=rig.animation_data.action.frame_range
    bpy.context.scene.frame_start=math.floor(start);bpy.context.scene.frame_end=math.ceil(end)
    bpy.context.scene.frame_set(math.floor(start))
    if clip=='Walking':bpy.ops.wm.save_as_mainfile(filepath=str(root/'SourceArt/Cyclopse_candidate_shape_skin.blend'))
    bpy.ops.export_scene.fbx(filepath=str(out/(clip+'.fbx')),object_types={'ARMATURE','MESH'},add_leaf_bones=False,
        bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
    report[clip]=dict(vertices=len(mesh.vertices),uv_preserved=True,cleaned_geometry_preserved=True,frames=[start,end])
(root/'Verification/cyclopse-full-clip/shape-skin.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
