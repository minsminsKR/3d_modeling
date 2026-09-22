"""Experimental seam-consistent shading, preserving positions, UVs and skin weights."""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector

root=Path(__file__).resolve().parent.parent
out=root/'Assets/Art/CandidateNormals/Cyclopse'
out.mkdir(parents=True,exist_ok=True)
report={}
for clip in ['Walking','Run']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Art/Candidate/Cyclopse'/(clip+'.fbx')))
    obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    mesh=obj.data
    def key(v): return tuple(round(c,6) for c in v.co)
    accumulated={}
    for polygon in mesh.polygons:
        for index in polygon.vertices:
            k=key(mesh.vertices[index])
            accumulated[k]=accumulated.get(k,Vector())+polygon.normal*polygon.area
    normals=[accumulated[key(v)].normalized() for v in mesh.vertices]
    mesh.normals_split_custom_set_from_vertices(normals)
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    start,end=rig.animation_data.action.frame_range
    bpy.context.scene.frame_start=math.floor(start)
    bpy.context.scene.frame_end=math.ceil(end)
    bpy.context.scene.frame_set(math.floor(start))
    report[clip]=dict(vertices=len(mesh.vertices),unique_positions=len(accumulated),frames=[start,end])
    if clip=='Walking':bpy.ops.wm.save_as_mainfile(filepath=str(root/'SourceArt/Cyclopse_candidate_normals.blend'))
    bpy.ops.export_scene.fbx(filepath=str(out/(clip+'.fbx')),object_types={'ARMATURE','MESH'},add_leaf_bones=False,
        bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
(root/'Verification/cyclopse-full-clip/normals-candidate.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))
