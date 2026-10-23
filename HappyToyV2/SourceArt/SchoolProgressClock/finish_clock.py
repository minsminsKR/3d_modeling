"""Blender 4.0.2; source-derived school wall clock, untouched original and PBR.

blender -b --factory-startup --threads 2 --python-exit-code 1 --python HappyToyV2/SourceArt/SchoolProgressClock/finish_clock.py
In the disposable Blender MCP session: finish(keep_editor_session=True).
"""
from pathlib import Path
import json
import runpy
import bpy
import bmesh
from mathutils import Matrix,Vector

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[1]
OUTPUT=PROJECT/'Assets/Resources/SchoolProgressClock'

def export_lods(objects,tools):
    levels=[]
    for index,obj in enumerate(objects):
        was_hidden=obj.hide_get();was_render_hidden=obj.hide_render
        obj.hide_set(False);obj.hide_render=False
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        filename=f'clock-lod{index}.fbx';path=OUTPUT/filename
        bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
            bake_anim=False,add_leaf_bones=False,path_mode='RELATIVE',use_mesh_modifiers=True)
        a,b=tools['bounds']([obj])
        levels.append(dict(file=filename,targetTriangles=(12000,8000)[index],triangles=tools['triangles'](obj),
            vertices=len(obj.data.vertices),boundsMin=list(a),boundsMax=list(b),sha256=tools['sha'](path)))
        obj.hide_set(was_hidden);obj.hide_render=was_render_hidden
    return levels

def finish(keep_editor_session=False,reuse_existing=False):
    if bpy.app.version[:3]!=(4,0,2):raise RuntimeError('Use pinned Blender 4.0.2')
    tools=runpy.run_path(str(PROJECT/'SourceArt/CorridorEntrance/finish_entrance_assets.py'),run_name='clock_shared_helpers')
    bake=runpy.run_path(str(PROJECT/'SourceArt/SchoolLoop/rebake_music_lod.py'),run_name='clock_atlas')['bake']
    original_path=SOURCE/'clock/original.glb';destination=OUTPUT/'clock';destination.mkdir(parents=True,exist_ok=True)
    if reuse_existing:
        originals=[o for c in bpy.data.collections if c.name.startswith('Complete unchanged webapp original')
            for o in c.objects if o.type=='MESH']
        near=bpy.data.objects.get('CE_clock_LOD0')
        if len(originals)!=1 or near is None:raise RuntimeError('No matching interrupted source-derived clock session')
        for obj in list(bpy.context.scene.objects):
            if obj not in originals+[near]:bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.orphans_purge(do_recursive=True)
    else:
        if keep_editor_session:
            for obj in list(bpy.context.scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
            bpy.data.orphans_purge(do_recursive=True)
        else:bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(original_path))
        originals=[o for o in bpy.context.scene.objects if o.type=='MESH']
        if len(originals)!=1:raise RuntimeError('Clock expects one original PBR primitive')
        collection=bpy.data.collections.new('Complete unchanged webapp original')
        bpy.context.scene.collection.children.link(collection)
        for obj in originals:
            for owner in list(obj.users_collection):owner.objects.unlink(obj)
            collection.objects.link(obj)
        near=originals[0].copy();near.data=originals[0].data.copy();near.parent=None
        near.matrix_world=originals[0].matrix_world.copy();bpy.context.scene.collection.objects.link(near)
        near.name='CE_clock_LOD0'
    original=originals[0];original.hide_set(False);original.hide_render=False
    low,high=tools['bounds'](originals)
    matrix=Matrix.Scale(1.4/(high.x-low.x),4)@Matrix.Translation(-Vector(((low.x+high.x)*.5,(low.y+high.y)*.5,low.z)))
    if not reuse_existing:
        near.matrix_world=matrix@near.matrix_world
        bpy.ops.object.select_all(action='DESELECT');near.select_set(True);bpy.context.view_layer.objects.active=near
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        bm=bmesh.new();bm.from_mesh(near.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0001)
        bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.000001)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(near.data);bm.free();near.data.update()
        tools['simplify'](near,12000)
    if reuse_existing and near.data.materials[0].name.startswith('CE_clock_Near') and (destination/'CE_clock_Near-metallicSmoothness.png').is_file():
        materials=[dict(name=near.data.materials[0].name,baseColor='CE_clock_Near-baseColor.png',
            metallicSmoothness='CE_clock_Near-metallicSmoothness.png',roughness=1.0,metallic=1.0,
            baseColorFactor=[1,1,1,1],normalScale=1,occlusionStrength=1)]
    else:materials=[bake(near,original,matrix,destination,'CE_clock_Near',1024,tools['save_pixels'])]
    # Preserve the thin housing surfaces; voxel reconstruction erases its panels.
    far=near.copy();far.data=near.data.copy();far.parent=None;far.name='CE_clock_LOD1'
    bpy.context.scene.collection.objects.link(far)
    far.hide_render=False;far.hide_set(False)
    bpy.ops.object.select_all(action='DESELECT');far.select_set(True);bpy.context.view_layer.objects.active=far
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    tools['simplify'](far,8000)
    old=bpy.data.materials.get('CE_clock_Distant')
    if old and old.users==0:bpy.data.materials.remove(old)
    materials.append(bake(far,original,matrix,destination,'CE_clock_Distant',1024,tools['save_pixels']))
    levels=export_lods((near,far),tools)
    a,b=tools['bounds']([near])
    report=dict(key='clock',tool='Blender 4.0.2',original='original.glb',originalSha256=tools['sha'](original_path),
        editable='clock/clock.blend',normalizedAxis='width',normalizedMetres=1.4,widthMetres=b.x-a.x,
        heightMetres=b.z-a.z,depthMetres=b.y-a.y,textureMaxSize=1024,originalTriangles=tools['triangles'](original),
        lods=levels,materials=materials,finishing='Source-derived bounded collapse with thin housing surfaces preserved; both LODs rebaked from unchanged original colour/ORM.',
        front='Generated display faces Blender +Y; confirm Unity facing in actual school camera')
    for path in (SOURCE/'clock/export.json',destination/'asset.json'):
        path.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    for path in destination.glob('CE_clock_0-*.png'):path.unlink()
    bpy.ops.file.pack_all()
    for name,path in (('finish_clock.py',Path(__file__)),('finish_entrance_assets.py',PROJECT/'SourceArt/CorridorEntrance/finish_entrance_assets.py'),
        ('rebake_music_lod.py',PROJECT/'SourceArt/SchoolLoop/rebake_music_lod.py')):
        bpy.data.texts.new('SOURCE - '+name).write(path.read_text(encoding='utf-8'))
    for obj in originals:obj.hide_render=True;obj.hide_set(True)
    far.hide_render=True;far.hide_set(True)
    near.hide_render=False;near.hide_set(False)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'clock/clock.blend'),compress=True)
    print('SCHOOL_PROGRESS_CLOCK_DERIVATION_PASS',json.dumps(report),flush=True)

if __name__=='__main__':finish()
