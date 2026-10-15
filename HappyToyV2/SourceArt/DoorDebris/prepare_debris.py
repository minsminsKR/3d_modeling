"""Blender 4.0.2 CPU finishing of retained GPU-0 timber; no replacement base.
Preserves original UV/PBR, separates connected timber, decimates and packs source.
"""
from pathlib import Path
import bpy,bmesh,json,math,hashlib,struct
import numpy as np
from mathutils import Vector,Euler

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[1]
DEST=PROJECT/'Assets/Resources/DoorDebris'
PROFILE=json.loads((SOURCE/'authoring-profile.json').read_text())
ORIGINAL=SOURCE/'door-debris-original.glb'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def select(*objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.hide_set(False);o.select_set(True)
    bpy.context.view_layer.objects.active=objects[-1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(ORIGINAL))
raw=[o for o in bpy.context.scene.objects if o.type=='MESH']
archive=bpy.data.collections.new('Complete original GPU 0 generated mesh and PBR');bpy.context.scene.collection.children.link(archive)
work=bpy.data.collections.new('Editable game fragments in floor layout');bpy.context.scene.collection.children.link(work)
copies=[]
for obj in raw:
    for collection in list(obj.users_collection):collection.objects.unlink(obj)
    archive.objects.link(obj)
    copy=obj.copy();copy.data=obj.data.copy();copy.parent=None;copy.matrix_world=obj.matrix_world.copy();work.objects.link(copy);copies.append(copy)
original_faces=sum(len(o.data.polygons) for o in raw)
select(*copies);bpy.ops.object.join();obj=bpy.context.object
obj.matrix_world=Euler(tuple(math.radians(v) for v in PROFILE['rotationDegrees'])).to_matrix().to_4x4()@obj.matrix_world
select(obj);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bm.to_mesh(obj.data);bm.free()
select(obj);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='LOOSE');bpy.ops.object.mode_set(mode='OBJECT')
parts=[o for o in work.objects if o.type=='MESH']
for part in list(parts):
    points=[v.co for v in part.data.vertices];size=[max(v[i] for v in points)-min(v[i] for v in points) for i in range(3)]
    if len(part.data.polygons)<PROFILE['minimumSourceFaces'] or max(size)<PROFILE['minimumSourceSize']:
        parts.remove(part);bpy.data.objects.remove(part,do_unlink=True)
if len(parts)<8:raise ValueError('Review generated connected components before export: '+str(len(parts)))
points=[v.co for part in parts for v in part.data.vertices]
low=Vector(tuple(min(v[i] for v in points) for i in range(3)));high=Vector(tuple(max(v[i] for v in points) for i in range(3)))
factor=PROFILE['widthMetres']/(high.x-low.x);centre=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))
total=sum(len(p.data.polygons) for p in parts)
highparts=[]
for index,part in enumerate(sorted(parts,key=lambda p:(-len(p.data.polygons),p.name))):
    part.name='Timber_%02d'%index
    for v in part.data.vertices:v.co=(v.co-centre)*factor
    bottom=min(v.co.z for v in part.data.vertices)
    for v in part.data.vertices:v.co.z-=bottom
    high=part.copy();high.data=part.data.copy();high.name='Temporary source-detail bake surface';work.objects.link(high);highparts.append(high)
    # Keep long torn ends and original texture UVs; no remesh or replacement box.
    target=max(120,int(PROFILE['triangleBudget']*len(part.data.polygons)/total))
    current=sum(len(p.vertices)-2 for p in part.data.polygons)
    if current>target:
        select(part);modifier=part.modifiers.new('Source surface game LOD','DECIMATE');modifier.ratio=target/current
        modifier.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=modifier.name)
    # Floor every separate fragment, including small splinters, while retaining
    # its generated shape and the original scattered X/Y composition.
    part.data.update()

DEST.mkdir(parents=True,exist_ok=True)
rawbytes=ORIGINAL.read_bytes();json_size=struct.unpack_from('<I',rawbytes,12)[0]
gltf=json.loads(rawbytes[20:20+json_size]);binary_start=20+json_size+8
gmat=gltf['materials'][0];pbr=gmat['pbrMetallicRoughness']
def source_image(info,label,linear=False):
    texture=gltf['textures'][info['index']];item=gltf['images'][texture['source']];view=gltf['bufferViews'][item['bufferView']]
    suffix='.png' if item.get('mimeType')=='image/png' else '.jpg'
    path=DEST/('_source-'+label+suffix);offset=binary_start+view.get('byteOffset',0)
    path.write_bytes(rawbytes[offset:offset+view['byteLength']]);image=bpy.data.images.load(str(path),check_existing=False)
    image.colorspace_settings.name='Non-Color' if linear else 'sRGB';image.scale(PROFILE['textureSize'],PROFILE['textureSize']);return image,path
base,tmp=source_image(pbr['baseColorTexture'],'colour')
base.file_format='PNG';base.filepath_raw=str(DEST/'DD_baseColor.png');base.save();base.pack();tmp.unlink()
mr,tmp=source_image(pbr['metallicRoughnessTexture'],'mr',True)
values=np.array(mr.pixels[:],dtype=np.float32).reshape((-1,4));rough=values[:,1].copy();metal=values[:,2].copy()
values[:,0]=metal;values[:,1:3]=0;values[:,3]=1-rough
packed=bpy.data.images.new('DD_metallicSmoothness',width=PROFILE['textureSize'],height=PROFILE['textureSize'],alpha=True)
packed.colorspace_settings.name='Non-Color';packed.pixels.foreach_set(values.ravel());packed.file_format='PNG';packed.filepath_raw=str(DEST/'DD_metallicSmoothness.png');packed.save();packed.pack();mr.pack();tmp.unlink()
normal=None
if 'normalTexture' in gmat:
    normal,tmp=source_image(gmat['normalTexture'],'normal',True);normal.file_format='PNG';normal.filepath_raw=str(DEST/'DD_normal.png');normal.save();normal.pack();tmp.unlink()
else:
    # Project the dense generated splinter surface into the unchanged atlas.
    # This retains fine relief after LOD reduction; all Cycles work is CPU-only.
    lowparts=[]
    for part in parts:
        low=part.copy();low.data=part.data.copy();work.objects.link(low);lowparts.append(low)
    select(*lowparts);bpy.ops.object.join();low=bpy.context.object
    select(*highparts);bpy.ops.object.join();high=bpy.context.object
    bake_mat=bpy.data.materials.new('Temporary normal bake target');bake_mat.use_nodes=True
    low.data.materials.clear();low.data.materials.append(bake_mat)
    for face in low.data.polygons:face.material_index=0
    normal=bpy.data.images.new('DD_normal',width=PROFILE['textureSize'],height=PROFILE['textureSize'],alpha=False)
    normal.colorspace_settings.name='Non-Color'
    target=bake_mat.node_tree.nodes.new('ShaderNodeTexImage');target.image=normal;bake_mat.node_tree.nodes.active=target
    archive.hide_render=True
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1
    scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=.003;scene.render.bake.max_ray_distance=.015
    select(high,low);print('BAKE_GENERATED_TIMBER_RELIEF_CPU',flush=True);bpy.ops.object.bake(type='NORMAL',margin=8)
    normal.file_format='PNG';normal.filepath_raw=str(DEST/'DD_normal.png');normal.save();normal.pack()
    bpy.data.objects.remove(low,do_unlink=True);bpy.data.objects.remove(high,do_unlink=True)
    highparts=[]
for high in highparts:bpy.data.objects.remove(high,do_unlink=True)
material=parts[0].data.materials[0].copy();material.name='Generated timber game PBR'
if normal:
    nodes=material.node_tree.nodes;links=material.node_tree.links;shader=nodes.get('Principled BSDF')
    texture=nodes.new('ShaderNodeTexImage');texture.image=normal;node=nodes.new('ShaderNodeNormalMap')
    links.new(texture.outputs['Color'],node.inputs['Color']);links.new(node.outputs['Normal'],shader.inputs['Normal'])
for part in parts:
    part.data.materials.clear();part.data.materials.append(material)
    for face in part.data.polygons:face.material_index=0
archive.hide_render=True;archive.hide_viewport=True
for image in bpy.data.images:
    if image.source=='FILE' and not image.packed_file:image.pack()
bpy.context.preferences.filepaths.save_version=0
select(*parts);bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'DoorDebris_Editable.blend'))
bpy.ops.export_scene.fbx(filepath=str(DEST/'geometry.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='RELATIVE',use_mesh_modifiers=True)
report=dict(tool='Blender 4.0.2 (CPU)',source=ORIGINAL.name,sourceSHA256=sha(ORIGINAL),referenceSHA256=sha(SOURCE/'reference.png'),
    parts=len(parts),originalFaces=original_faces,gameTriangles=sum(len(f.vertices)-2 for p in parts for f in p.data.polygons),
    baseColor='DD_baseColor.png',metallicSmoothness='DD_metallicSmoothness.png',normal='DD_normal.png' if normal else None,
    widthMetres=PROFILE['widthMetres'],editable='DoorDebris_Editable.blend',partTriangles={p.name:sum(len(f.vertices)-2 for f in p.data.polygons) for p in parts})
for path in [SOURCE/'export.json',DEST/'asset.json']:path.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('MODELED_DEBRIS_EXPORT_PASS '+json.dumps(report),flush=True)
