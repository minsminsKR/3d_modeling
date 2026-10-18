"""Reviewed game skin/UV finishing. Blender 4.0.2; generated originals untouched."""
import json
import sys
from pathlib import Path
import bpy
import numpy as np
from mathutils.bvhtree import BVHTree

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
sys.path.insert(0,str(PROJECT/'SourceArt/CreatureStudio/tools'))
import bake_surface_atlas as atlas


def finish(high,obj,rig,repaint=True):
    rig.data.pose_position='REST'
    bpy.context.view_layer.update()
    # The surface has many hair islands; a 0.003 chart margin consumed almost
    # the whole old atlas. Pack with a texel-sized gutter before source transfer.
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    if repaint:
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=1.4,island_margin=.00005,correct_aspect=True,scale_to_bounds=True)
        bpy.ops.object.mode_set(mode='OBJECT')
    hp=atlas.array(high.data.vertices,'co',3);ht,hl=atlas.triangles(high.data)
    hu=atlas.array(high.data.uv_layers.active.data,'uv',2)
    tree=BVHTree.FromPolygons(hp.tolist(),ht.tolist(),all_triangles=True)
    bsdf=next(n for n in high.data.materials[0].node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    image=atlas.linked_image(bsdf.inputs['Base Color'])
    pixels=np.empty(len(image.pixels),dtype='f4');image.pixels.foreach_get(pixels)
    pixels=(np.clip(pixels.reshape(image.size[1],image.size[0],4),0,1)*255).astype('u1')
    def region(point,color):
        x,y,z=point
        if z<.19 and abs(x)<.20:return 'LeftFootSurface' if x>0 else 'RightFootSurface'
        if z>1.78 or (z>.70 and color.max()<.13 and color.max()-color.min()<.05):return 'Head'
        if .85<z<1.68 and abs(x)>.18:return 'Left' if x>0 else 'Right'
        return 'Body'
    # The generated hanging hair/sleeves are fused onto the robe. Split their
    # shared game vertices at the real existing surface boundary; otherwise
    # unrelated head/arm rotations stretch the joining triangles into sails.
    old=obj.data;vertices=[];faces=[];uvs=[];labels=[];mapping={}
    for poly in old.polygons:
        center=poly.center;found=tree.find_nearest(center)
        w=atlas.barycentric(np.array([found[0]]),hp[ht[found[2]]][None])[0]
        uv=(hu[hl[found[2]]]*w[:,None]).sum(0)
        label=region(obj.matrix_world@center,atlas.sample(pixels,np.array([uv]))[0,:3])
        face=[]
        for loop in poly.loop_indices:
            original=old.loops[loop].vertex_index;key=(original,label)
            if key not in mapping:
                mapping[key]=len(vertices);vertices.append(tuple(old.vertices[original].co));labels.append(label)
            face.append(mapping[key]);uvs.append(tuple(old.uv_layers.active.data[loop].uv))
        faces.append(face)
    mesh=bpy.data.meshes.new('Lady reviewed separate robe sleeves hair');mesh.from_pydata(vertices,[],faces)
    layer=mesh.uv_layers.new(name='Lady atlas')
    for loop,uv in zip(layer.data,uvs):loop.uv=uv
    for material in old.materials:mesh.materials.append(material)
    for poly in mesh.polygons:poly.use_smooth=True
    obj.data=mesh
    for group in list(obj.vertex_groups):obj.vertex_groups.remove(group)
    groups={bone.name:obj.vertex_groups.new(name=bone.name) for bone in rig.data.bones}
    bins={};hair_count=0
    def blend(z,levels):
        if z<=levels[0][0]:return {levels[0][1]:1.}
        for (lo,a),(hi,b) in zip(levels,levels[1:]):
            if z<=hi:
                t=(z-lo)/(hi-lo);t=t*t*(3-2*t)
                return {a:1-t,b:t}
        return {levels[-1][1]:1.}
    for vertex in obj.data.vertices:
        point=obj.matrix_world@vertex.co;x,y,z=point
        found=tree.find_nearest(vertex.co);ids=ht[found[2]]
        w=atlas.barycentric(np.array([found[0]]),hp[ids][None])[0]
        uv=(hu[hl[found[2]]]*w[:,None]).sum(0)
        color=atlas.sample(pixels,np.array([uv]))[0,:3]
        label=labels[vertex.index]
        body=blend(z,[(1.18,'Hips'),(1.36,'Spine'),(1.48,'Spine1'),(1.61,'Spine2'),(1.72,'Neck'),(1.8,'Head')])
        side='Left' if x>0 else 'Right'
        if label.endswith('FootSurface'):
            side='Left' if label.startswith('Left') else 'Right'
            body=blend(z,[(.07,side+'ToeBase'),(.20,side+'Foot'),(.23,side+'Leg')])
        elif label in ('Left','Right'):
            side=label
            arm=blend(z,[(.94,side+'Hand'),(1.17,side+'ForeArm'),(1.43,side+'Arm'),(1.67,side+'Shoulder')])
            body=arm
        if label=='Head':body={'Head':1.};hair_count+=1
        for name,value in body.items():
            amount=round(value*1024)
            if amount:bins.setdefault((name,amount),[]).append(vertex.index)
    for (name,amount),ids in bins.items():groups[name].add(ids,amount/1024,'REPLACE')
    output=PROJECT/'Assets/Resources/Lady'
    report=atlas.bake(high,obj,output,2048) if repaint else json.loads((HERE/'surface-report.json').read_text(encoding='utf-8'))
    report.update(vertices=len(obj.data.vertices),triangles=len(obj.data.polygons),
                  skin='Reviewed smooth anatomical robe/sleeve/hand/foot branches; rigid head and black hair',
                  rigidHeadAndHairGameVertices=hair_count,sourceSkinEditedOnlyForGame=True,chartMargin=.00005)
    report['fusedGameSurfaceSeamsSeparated']=True
    if report['atlasSurfaceSamples']<500000:raise ValueError('Game atlas has insufficient real surface coverage')
    (HERE/'surface-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    rig.data.pose_position='POSE'
    return report


def refine_existing():
    import author_lady as author
    bpy.ops.wm.open_mainfile(filepath=str(HERE/'Lady_Editable.blend'))
    rig=bpy.data.objects['Lady_Rig'];fit=bpy.data.objects['Lady_Fit']
    first=bpy.data.objects['Lady_LOD0']
    for i in (1,2):bpy.data.objects.remove(bpy.data.objects['Lady_LOD'+str(i)],do_unlink=True)
    bpy.context.scene.frame_set(1)
    finish(bpy.data.objects['Lady_SourceHigh'],first,rig,repaint='--skin-only' not in sys.argv)
    lods=[first]+[author.copy_lod(first,i,limit/len(first.data.polygons)) for i,limit in ((1,45000),(2,14000))]
    profile=json.loads((HERE/'motion-profile.json').read_text(encoding='utf-8'))
    rig.animation_data_clear()
    for old in list(bpy.data.actions):bpy.data.actions.remove(old)
    action,schedules=author.author_action(rig,profile)
    report=json.loads((HERE/'authoring-report.json').read_text(encoding='utf-8'))
    report.update(motionSchedules=schedules,gameSkinTransferReport='surface-report.json',
                  lods=[dict(name=o.name,vertices=len(o.data.vertices),triangles=len(o.data.polygons)) for o in lods])
    (HERE/'authoring-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    bpy.context.scene.frame_set(1)
    for obj in lods[1:]:obj.hide_render=True;obj.hide_set(True)
    bpy.ops.file.pack_all();bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Lady_Editable.blend'))
    author.export_game(fit,rig,lods)
    for obj in lods[1:]:obj.hide_render=True;obj.hide_set(True)
    author.render_preview(fit,rig,PROJECT/'Builds/LadyReview-20261018')
