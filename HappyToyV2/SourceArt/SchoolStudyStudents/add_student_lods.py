"""Make two source-derived skinned LODs from a finished seated review mesh."""
import argparse
from pathlib import Path
import sys
import bpy
import bmesh

HERE=Path(__file__).resolve().parent;PROJECT=HERE.parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--job',required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
near=bpy.data.objects.get('Student seated game review');rig=bpy.data.objects.get('Seated student head rig')
if not near or not rig:raise RuntimeError('Expected finished student mesh and rig')
near.data.calc_loop_triangles()
if len(near.data.loop_triangles)>50000:raise RuntimeError('Near LOD exceeds budget')
lods=[near]
for index,budget in enumerate((12000,10000),1):
    obj=near.copy();obj.data=near.data.copy();obj.name='Student seated LOD'+str(index)
    bpy.context.scene.collection.objects.link(obj)
    for mod in obj.modifiers:
        if mod.type=='ARMATURE':mod.object=rig;mod.show_viewport=False
    bpy.ops.object.select_all(action='DESELECT');obj.hide_set(False);obj.select_set(True);bpy.context.view_layer.objects.active=obj
    for _ in range(5):
        obj.data.calc_loop_triangles();before=len(obj.data.loop_triangles)
        if before<=budget:break
        collapse=obj.modifiers.new('Source-derived LOD collapse','DECIMATE')
        collapse.ratio=max(.001,(budget-8)/before);collapse.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=collapse.name)
        obj.data.calc_loop_triangles()
        if len(obj.data.loop_triangles)>=before-4:break
    for mod in obj.modifiers:
        if mod.type=='ARMATURE':mod.show_viewport=True
    obj.data.calc_loop_triangles()
    if len(obj.data.loop_triangles)>budget:
        # Decimation cannot collapse isolated hair/cloth shards. At distance,
        # remove the smallest disconnected pieces rather than breach the budget.
        bm=bmesh.new();bm.from_mesh(obj.data);remaining=set(bm.faces);components=[]
        while remaining:
            first=remaining.pop();stack=[first];faces=[first]
            while stack:
                face=stack.pop()
                for edge in face.edges:
                    for other in edge.link_faces:
                        if other in remaining:remaining.remove(other);stack.append(other);faces.append(other)
            components.append((sum(f.calc_area() for f in faces),faces))
        for _,faces in sorted(components,key=lambda item:item[0]):
            if len(bm.faces)<=budget:break
            if len(faces)>=len(bm.faces):continue
            bmesh.ops.delete(bm,geom=faces,context='FACES')
        bm.to_mesh(obj.data);bm.free();obj.data.update();obj.data.calc_loop_triangles()
    if len(obj.data.loop_triangles)>budget:raise RuntimeError('LOD triangle budget exceeded: '+str(len(obj.data.loop_triangles)))
    lods.append(obj)
out=PROJECT/'Builds'/'SchoolStudyStudentsReview'/args.job/'export'
bpy.ops.object.select_all(action='DESELECT');rig.hide_set(False);rig.select_set(True)
for obj in lods:obj.hide_set(False);obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(out/'student.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},
    add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
for obj in lods[1:]:obj.hide_set(True);obj.hide_render=True
bpy.context.preferences.filepaths.save_version=0;bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'student-lods.blend'),compress=True)
print('STUDENT_LODS',args.job,[len(o.data.loop_triangles) for o in lods],flush=True)
