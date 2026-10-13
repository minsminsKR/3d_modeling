"""Blender 4.0.2: preserve webapp originals, pack editable sources and export game LODs.

blender --background --factory-startup --python prepare_game_props.py -- --only onggi
No substitute geometry is created. Every game mesh is a decimated copy of its GLB.
"""
from pathlib import Path
import argparse
import hashlib
import json
import sys
import bpy
import numpy as np
from mathutils import Vector

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[1]
SETTINGS = {'jangseung': (1.4, 12000), 'onggi': (.65, 6000), 'dokkaebi-tile': (.48, 10000)}

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def image_for(socket, seen=None):
    seen = seen or set()
    for link in socket.links:
        node = link.from_node
        if node.name in seen: continue
        seen.add(node.name)
        if node.type == 'TEX_IMAGE': return node.image
        for item in node.inputs:
            image = image_for(item, seen)
            if image: return image
    return None

def save_texture(image, destination, packed=False):
    copied = image.copy()
    scale = min(1, 2048 / max(copied.size))
    if scale < 1: copied.scale(max(1, round(copied.size[0]*scale)), max(1, round(copied.size[1]*scale)))
    pixels = np.array(copied.pixels[:], dtype=np.float32).reshape((-1, 4))
    if packed:
        converted = np.zeros_like(pixels); converted[:, 0] = pixels[:, 2]; converted[:, 3] = 1-pixels[:, 1]
        pixels = converted
    # A fresh generated buffer avoids re-saving a packed glTF image's stale file
    # backing. Colour-space conversion remains the same as the source image.
    output = bpy.data.images.new('Unity PBR export', width=copied.size[0], height=copied.size[1], alpha=True)
    output.colorspace_settings.name = 'Non-Color' if packed else image.colorspace_settings.name
    output.pixels.foreach_set(pixels.ravel())
    output.file_format = 'PNG'; output.filepath_raw = str(destination); output.save()
    bpy.data.images.remove(output); bpy.data.images.remove(copied)

def prepare(key):
    original = SOURCE / (key + '-original.glb')
    height, budget = SETTINGS[key]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(original))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    if not meshes: raise RuntimeError('Generated original has no mesh: '+key)
    original_faces = sum(len(obj.data.polygons) for obj in meshes)
    original_collection = bpy.data.collections.new('Complete webapp original')
    bpy.context.scene.collection.children.link(original_collection)
    game_collection = bpy.data.collections.new('Unity game LOD')
    bpy.context.scene.collection.children.link(game_collection)
    game_meshes = []
    for original_mesh in meshes:
        for collection in list(original_mesh.users_collection): collection.objects.unlink(original_mesh)
        original_collection.objects.link(original_mesh)
        clone = original_mesh.copy(); clone.data = original_mesh.data.copy(); clone.matrix_world = original_mesh.matrix_world.copy()
        clone.parent = None; clone.name = 'KC_'+key; game_collection.objects.link(clone); game_meshes.append(clone)
    points = [obj.matrix_world @ Vector(corner) for obj in game_meshes for corner in obj.bound_box]
    low = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    high = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    factor = height/max(.00001, high.z-low.z)
    centre = Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))
    for obj in game_meshes:
        obj.location = (obj.location-centre)*factor; obj.scale *= factor
        bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active=obj
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        # glTF splits vertices along atlas seams. Weld coincident geometry while
        # retaining the per-loop UVs so decimation can meet the actual budget.
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.remove_doubles(threshold=.00002); bpy.ops.object.mode_set(mode='OBJECT')
        decimate = obj.modifiers.new('Reproducible game triangle budget','DECIMATE')
        decimate.ratio = min(1,budget/max(1,original_faces))
        bpy.ops.object.modifier_apply(modifier=decimate.name)
    destination = PROJECT/'Assets/Resources/KoreanCorridorProps'/key
    destination.mkdir(parents=True,exist_ok=True)
    mapped = {}; materials=[]
    for obj in game_meshes:
        for slot in obj.material_slots:
            if not slot.material: continue
            old = slot.material
            if old.name not in mapped:
                new = old.copy(); new.name='KC_'+key+'_'+str(len(materials)); mapped[old.name]=new
                shader=next(node for node in old.node_tree.nodes if node.type=='BSDF_PRINCIPLED')
                entry=dict(name=new.name,roughness=float(shader.inputs['Roughness'].default_value))
                for label, socket, packed in [('baseColor','Base Color',False),('metallicSmoothness','Roughness',True),('normal','Normal',False)]:
                    image=image_for(shader.inputs[socket])
                    if image:
                        name=new.name+'-'+label+'.png'; save_texture(image,destination/name,packed)
                        entry[label]=name
                if not entry.get('baseColor'): raise RuntimeError('Generated PBR base colour missing: '+key)
                materials.append(entry)
            slot.material=mapped[old.name]
    # Pack the full original maps in the editable source before export. Game
    # textures are derived outputs; the original GLB remains byte-identical.
    for image in bpy.data.images:
        if image.source == 'FILE': image.pack()
    original_collection.hide_render=True; original_collection.hide_viewport=True
    bpy.context.preferences.filepaths.save_version=0
    blend=SOURCE/(key+'.blend'); bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in game_meshes: obj.select_set(True)
    bpy.context.view_layer.objects.active=game_meshes[0]
    fbx=destination/'geometry.fbx'
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='RELATIVE',use_mesh_modifiers=True)
    report=dict(key=key,tool='Blender 4.0.2',original=original.name,originalSha256=sha(original),
        editable=blend.name,heightMetres=height,targetTriangles=budget,
        originalFaces=original_faces,gameFaces=sum(len(obj.data.polygons) for obj in game_meshes),
        fbx=fbx.relative_to(PROJECT).as_posix(),materials=materials,
        textures='Original full PBR embedded in original GLB and packed blend; game maps resized to at most 2048px',
        bounds=dict(width=(high.x-low.x)*factor,depth=(high.y-low.y)*factor,height=height))
    (destination/'asset.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    (SOURCE/(key+'-export.json')).write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('GAME_PROP '+json.dumps(report))

if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--only',choices=SETTINGS)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    for key in ([args.only] if args.only else SETTINGS): prepare(key)
