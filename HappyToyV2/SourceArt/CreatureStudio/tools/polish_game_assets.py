"""Finish saved generated creatures; preserve the exact near skin and actions.

Blender 4.0.2 --background --factory-startup --threads 2 --python-exit-code 1
--python this.py -- --creature Baby (or Uncat/Cyclopse).
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
import numpy as np
from mathutils.bvhtree import BVHTree

HERE=Path(__file__).resolve().parent
STUDIO=HERE.parent
PROJECT=STUDIO.parents[1]
sys.path.insert(0,str(HERE))
import author_creature as author


def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def action_digest():
    data=[(a.name,[(f.data_path,f.array_index,[(tuple(k.co),tuple(k.handle_left),tuple(k.handle_right),k.interpolation)
          for k in f.keyframe_points]) for f in a.fcurves]) for a in bpy.data.actions]
    return hashlib.sha256(json.dumps(data,sort_keys=True).encode()).hexdigest()


def near_digest(mesh):
    h=hashlib.sha256()
    for collection,property,width,dtype in ((mesh.vertices,'co',3,'f4'),(mesh.loops,'vertex_index',1,'i4'),
                                          (mesh.uv_layers.active.data,'uv',2,'f4')):
        values=np.empty(len(collection)*width,dtype=dtype);collection.foreach_get(property,values);h.update(values.tobytes())
    h.update(json.dumps([[(g.group,g.weight) for g in v.groups] for v in mesh.vertices]).encode())
    return h.hexdigest()


def make_lod(first,level,ratio,rig,settings):
    old=bpy.data.objects.get(first.name.rsplit('LOD',1)[0]+'LOD'+str(level))
    if old:bpy.data.objects.remove(old,do_unlink=True)
    obj=first.copy();obj.data=first.data.copy();bpy.context.scene.collection.objects.link(obj)
    obj.name=first.name.rsplit('LOD',1)[0]+'LOD'+str(level)
    obj.hide_set(False);obj.hide_render=False
    for modifier in obj.modifiers:
        if modifier.type=='ARMATURE':modifier.show_viewport=False
    group=obj.vertex_groups.new(name='Distant face and support priority')
    names={g.index:g.name for g in obj.vertex_groups}
    for vertex in obj.data.vertices:
        priority=max([g.weight*(.85 if names[g.group].endswith('Head') else .5) for g in vertex.groups
                      if names[g.group].endswith(('Head','Hand','Foot'))]+[.1])
        if any(names[g.group].endswith(('Head','Hand','Foot')) and g.weight>.65 for g in vertex.groups):priority=1
        group.add([vertex.index],priority,'REPLACE')
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    modifier=obj.modifiers.new('Distant surface collapse','DECIMATE');modifier.ratio=ratio
    modifier.use_collapse_triangulate=True;modifier.vertex_group=group.name
    modifier.invert_vertex_group=True;modifier.vertex_group_factor=5
    with bpy.context.temp_override(object=obj,active_object=obj,selected_objects=[obj],selected_editable_objects=[obj]):
        bpy.ops.object.modifier_move_to_index(modifier=modifier.name,index=0)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    priority=obj.vertex_groups.get('Distant face and support priority')
    if priority:obj.vertex_groups.remove(priority)
    for modifier in obj.modifiers:
        if modifier.type=='ARMATURE':modifier.show_viewport=True
    world=[first.matrix_world@v.co for v in first.data.vertices]
    tree=BVHTree.FromPolygons(world,[list(p.vertices) for p in first.data.polygons])
    distances=np.array([tree.find_nearest(obj.matrix_world@v.co)[3] for v in obj.data.vertices])
    p99=float(np.percentile(distances,99));maximum=float(distances.max())
    if p99>settings['maximumDistantSurfaceP99Metres'] or maximum>settings['maximumDistantSurfaceErrorMetres']:
        if ratio<.97:
            print(f'Keeping more source surface for {obj.name}: ratio={ratio}, max={maximum}',flush=True)
            return make_lod(first,level,(1+ratio)/2,rig,settings)
        raise RuntimeError(f'Distant surface exceeded source distance guard: {obj.name} {p99} {maximum}')
    obj.data.calc_loop_triangles()
    return obj,dict(name=obj.name,ratio=ratio,vertices=len(obj.data.vertices),triangles=len(obj.data.loop_triangles),
                    sourceDistanceP99Metres=p99,sourceDistanceMaxMetres=maximum)


def nonmetal_game_maps(output,lods):
    reports=[]
    for path in list(output.glob('*metallic-smoothness.png'))+list(output.glob('*orm.png')):
        image=bpy.data.images.load(str(path),check_existing=False);image.colorspace_settings.name='Non-Color'
        pixels=np.empty(len(image.pixels),dtype='f4');image.pixels.foreach_get(pixels);pixels=pixels.reshape(-1,4)
        channel=2 if 'orm' in path.stem else 0
        before=float(pixels[:,channel].max());pixels[:,channel]=0;image.pixels.foreach_set(pixels.ravel())
        author.save_image_atomic(image,path);image.pack()
        reports.append(dict(file=path.name,previousMaximum=before,metallic=0,sha256=digest(path)))
    for material in {m for obj in lods for m in obj.data.materials if m}:
        nodes=material.node_tree.nodes;links=material.node_tree.links
        for bsdf in (n for n in nodes if n.type=='BSDF_PRINCIPLED'):
            for link in list(bsdf.inputs['Metallic'].links):links.remove(link)
            bsdf.inputs['Metallic'].default_value=0
    return reports


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--creature',choices=('Baby','Uncat','Cyclopse'),required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);key=args.creature
    settings=json.loads((STUDIO/'polish-settings.json').read_text());folder=STUDIO/key
    output=PROJECT/'Assets/Resources/EnemyStudio'/key;blend=folder/(key+'_Editable.blend')
    originals={p.name:digest(p) for p in folder.glob('*.glb')}
    protected_maps={p.name:digest(p) for p in output.glob('*.png')
                    if 'metallic-smoothness' not in p.name and not p.name.endswith('orm.png')}
    bpy.ops.wm.open_mainfile(filepath=str(blend),load_ui=False)
    first=bpy.data.objects[key+'_LOD0'];rig=next(m.object for m in first.modifiers if m.type=='ARMATURE')
    bpy.context.scene.frame_set(1);actions=action_digest();near=near_digest(first.data)
    rig.data.pose_position='REST';bpy.context.view_layer.update()
    lods=[first];rows=[]
    for level in (1,2):
        old=bpy.data.objects.get(key+'_LOD'+str(level))
        if old:bpy.data.objects.remove(old,do_unlink=True)
    first.data.calc_loop_triangles()
    for level,ratio in enumerate(settings['lodRatios'],1):
        obj,report=make_lod(first,level,ratio,rig,settings)
        if report['triangles']>len(first.data.loop_triangles)*(1-settings['minimumDistantTriangleReduction']):
            print('Keeping existing skin: safe distant reduction is too small for '+key,flush=True)
            bpy.data.objects.remove(obj,do_unlink=True)
        else:lods.append(obj);rows.append(report)
    ordered=sorted(zip(lods[1:],rows),key=lambda pair:pair[1]['triangles'],reverse=True)
    for index,(obj,row) in enumerate(ordered,1):obj.name='Pending distant '+str(index)
    for index,(obj,row) in enumerate(ordered,1):obj.name=key+'_LOD'+str(index);row['name']=obj.name
    lods=[first]+[pair[0] for pair in ordered];rows=[pair[1] for pair in ordered]
    # Game-only materials must not share the retained high source's slots.
    high=bpy.data.objects[key+'_EditableHigh']
    if set(first.data.materials)&set(high.data.materials):
        mapping={m:m.copy() for m in first.data.materials}
        for obj in lods:
            for index,material in enumerate(obj.data.materials):obj.data.materials[index]=mapping[material]
    maps=nonmetal_game_maps(output,lods)
    rig.data.pose_position='POSE';bpy.context.scene.frame_set(1)
    assert near==near_digest(first.data),'Near skin/UV/weights changed'
    assert actions==action_digest(),'Authored action keys changed'
    for path,value in originals.items():assert digest(folder/path)==value,'Generated original changed'
    for path,value in protected_maps.items():assert digest(output/path)==value,'Colour/normal/eye atlas changed'
    for obj in lods[1:]:obj.hide_render=True;obj.hide_set(True)
    bpy.ops.file.pack_all();bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(blend),compress=True,check_existing=False,relative_remap=False)
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
    for obj in lods:obj.hide_set(False);obj.select_set(True)
    bpy.context.view_layer.objects.active=rig
    for obj in (high,*lods):
        for modifier in obj.modifiers:
            if modifier.type=='ARMATURE':modifier.show_viewport=False
    temporary=output/('.'+key+'.polish.fbx')
    bpy.ops.export_scene.fbx(filepath=str(temporary),use_selection=True,object_types={'MESH','ARMATURE'},
        axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
        add_leaf_bones=False,use_armature_deform_only=True,bake_anim=True,bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,bake_anim_step=1,mesh_smooth_type='FACE',
        use_tspace=True,path_mode='AUTO',embed_textures=False)
    temporary.replace(output/(key+'.fbx'))
    report=dict(key=key,settings=settings,lods=rows,nearSkinSha256=near,actionSha256=actions,
        distanceProbes='Every retained distant rest vertex in world metres; not a bidirectional triangle-interior certificate',
        originalFiles=originals,protectedMaps=protected_maps,gameMaps=maps,fbxSha256=digest(output/(key+'.fbx')),
        blendSha256=digest(blend),scope='Derived distant copies and game metallic only; original near skin and actions unchanged')
    (folder/'polish-report.json').write_text(json.dumps(report,indent=2)+'\n')
    authored=json.loads((folder/'authoring-report.json').read_text());authored['fbx']['sha256']=report['fbxSha256']
    authored['gamePolish']='polish-report.json';(folder/'authoring-report.json').write_text(json.dumps(authored,indent=2)+'\n')
    print('CREATURE_GAME_POLISH_PASS '+key+' '+json.dumps(rows),flush=True)


if __name__=='__main__':main()
