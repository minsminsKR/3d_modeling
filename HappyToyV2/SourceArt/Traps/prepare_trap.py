"""Blender 4.0.2: finish the retained webapp trap, bake PBR and articulate jaws.
No substitute base geometry is constructed. Run from HappyToyV2 with Blender
--background --factory-startup --python-exit-code 1 --python this-file.
"""
from pathlib import Path
import hashlib,json,math
import bpy,bmesh
import numpy as np
from mathutils import Vector

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[1]
DEST=PROJECT/'Assets/Resources/Traps'
PROFILE=json.loads((SOURCE/'authoring-profile.json').read_text())
original=SOURCE/'trap-original.glb'
def select(*objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.hide_set(False);obj.hide_render=False;obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[-1]
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(original))
originals=[o for o in bpy.context.scene.objects if o.type=='MESH']
archive=bpy.data.collections.new('Complete unmodified webapp source');bpy.context.scene.collection.children.link(archive)
work=bpy.data.collections.new('Derived game trap with editable mechanical parts');bpy.context.scene.collection.children.link(work)
copies=[]
for raw in originals:
    for collection in list(raw.users_collection):collection.objects.unlink(raw)
    archive.objects.link(raw)
    ob=raw.copy();ob.data=raw.data.copy();ob.parent=None;ob.matrix_world=raw.matrix_world.copy();work.objects.link(ob);copies.append(ob)
if not copies:raise ValueError('No webapp mesh')
select(*copies);bpy.ops.object.join();obj=bpy.context.object;obj.name='Trap game surface'
# Apply explicit reviewed orientation before normalization; source remains retained.
from mathutils import Euler
obj.matrix_world=Euler(tuple(math.radians(v) for v in PROFILE['rotationDegrees'])).to_matrix().to_4x4()@obj.matrix_world
points=[obj.matrix_world@Vector(c) for c in obj.bound_box]
low=Vector(tuple(min(v[i] for v in points) for i in range(3)));high=Vector(tuple(max(v[i] for v in points) for i in range(3)))
factor=PROFILE['widthMetres']/(high.x-low.x);centre=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))
obj.location=(obj.location-centre)*factor;obj.scale*=factor;select(obj);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
highpoly=obj.copy();highpoly.data=obj.data.copy();highpoly.name='Temporary original PBR bake surface';work.objects.link(highpoly)
for slot in highpoly.material_slots:
    if slot.material:slot.material=slot.material.copy()
original_faces=sum(len(o.data.polygons) for o in originals)
for layer in list(obj.data.uv_layers):obj.data.uv_layers.remove(layer)
remesh=obj.modifiers.new('Continuous metal derived from generated surfaces','REMESH');remesh.mode='VOXEL'
remesh.voxel_size=PROFILE['widthMetres']/PROFILE['voxelDivisor'];remesh.use_smooth_shade=True;remesh.use_remove_disconnected=False
bpy.ops.object.modifier_apply(modifier=remesh.name)
for attempt in range(4):
    faces=sum(len(p.vertices)-2 for p in obj.data.polygons)
    if faces<=PROFILE['triangleBudget']:break
    decimate=obj.modifiers.new('Game LOD preserving original silhouette','DECIMATE');decimate.ratio=PROFILE['triangleBudget']*.98/faces
    decimate.use_collapse_triangulate=True;decimate.delimit=set();bpy.ops.object.modifier_apply(modifier=decimate.name)
if len(obj.data.polygons)>PROFILE['triangleBudget']:raise ValueError('Trap triangle budget exceeded')
for p in obj.data.polygons:p.use_smooth=True
obj.data.use_auto_smooth=True;obj.data.auto_smooth_angle=math.radians(40)
select(obj);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.008);bpy.ops.object.mode_set(mode='OBJECT')
mat=bpy.data.materials.new('TR_Iron');mat.use_nodes=True;obj.data.materials.clear();obj.data.materials.append(mat)
for p in obj.data.polygons:p.material_index=0
nodes=mat.node_tree.nodes;links=mat.node_tree.links;shader=nodes.get('Principled BSDF');target=nodes.new('ShaderNodeTexImage');nodes.active=target
archive.hide_render=True;archive.hide_viewport=True
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=4
scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=.008;scene.render.bake.max_ray_distance=.025
DEST.mkdir(parents=True,exist_ok=True);images={};results={}
for channel,label in [('Base Color','baseColor'),('Roughness','roughness'),('Metallic','metallic'),(None,'normal')]:
    image=bpy.data.images.new('TR_'+label,width=2048,height=2048,alpha=True);image.colorspace_settings.name='sRGB' if label=='baseColor' else 'Non-Color'
    target.image=image;nodes.active=target;restores=[]
    if channel:
        for material in highpoly.data.materials:
            if not material or not material.use_nodes:continue
            n=material.node_tree.nodes;l=material.node_tree.links
            source=next(x for x in n if x.type=='BSDF_PRINCIPLED');output=next(x for x in n if x.type=='OUTPUT_MATERIAL')
            surface=output.inputs['Surface'];old=surface.links[0].from_socket if surface.is_linked else None
            emission=n.new('ShaderNodeEmission');value=source.inputs[channel]
            if value.is_linked:l.new(value.links[0].from_socket,emission.inputs['Color'])
            else:
                default=value.default_value;emission.inputs['Color'].default_value=tuple(default) if channel=='Base Color' else (default,default,default,1)
            l.new(emission.outputs[0],surface);restores.append((material,output,old,emission))
    select(highpoly,obj);print('BAKE_TRAP',label,flush=True);bpy.ops.object.bake(type='EMIT' if channel else 'NORMAL',margin=12)
    for material,output,old,emission in restores:
        if old:material.node_tree.links.new(old,output.inputs['Surface'])
        material.node_tree.nodes.remove(emission)
    image.file_format='PNG';image.filepath_raw=str(DEST/('TR_'+label+'.png'));image.save();image.pack();images[label]=image;results[label]=Path(image.filepath_raw).name

metal=np.array(images['metallic'].pixels[:],dtype=np.float32).reshape((-1,4));rough=np.array(images['roughness'].pixels[:],dtype=np.float32).reshape((-1,4))
packed=bpy.data.images.new('TR_metallicSmoothness',width=2048,height=2048,alpha=True);packed.colorspace_settings.name='Non-Color'
metal[:,3]=1-rough[:,0];metal[:,1:3]=0;packed.pixels.foreach_set(metal.ravel());packed.file_format='PNG';packed.filepath_raw=str(DEST/'TR_metallicSmoothness.png');packed.save();packed.pack()
results['metallicSmoothness']=Path(packed.filepath_raw).name
for key in ['metallic','roughness']:(DEST/results.pop(key)).unlink()
target.image=images['baseColor'];links.new(target.outputs['Color'],shader.inputs['Base Color'])
for key,input_name in [('roughness','Roughness'),('metallic','Metallic')]:
    tex=nodes.new('ShaderNodeTexImage');tex.image=images[key];links.new(tex.outputs['Color'],shader.inputs[input_name])
tex=nodes.new('ShaderNodeTexImage');tex.image=images['normal'];normal=nodes.new('ShaderNodeNormalMap');links.new(tex.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],shader.inputs['Normal'])
bpy.data.objects.remove(highpoly,do_unlink=True)


# Separate existing triangles into mechanical pieces. Geometry comes exclusively
# from the generated base; small seams sit underneath its original hinge housings.
groups={name:[] for name in ['Base','JawFront','JawBack','PressurePlate']}
obj.data.update()
for poly in obj.data.polygons:
    x,y,z=poly.center
    radial=math.hypot(x,y)
    spine=abs(x)<PROFILE.get('baseSpineHalfWidth',.04) and abs(y)<PROFILE.get('baseSpineReach',.22) and z<PROFILE.get('baseSpineTop',.075)
    name='PressurePlate' if radial<PROFILE['plateRadius'] and z>PROFILE['plateMinHeight'] else 'Base' if spine else 'JawFront' if y>PROFILE['hingeStrip'] and radial>PROFILE['jawInnerRadius'] else 'JawBack' if y<-PROFILE['hingeStrip'] and radial>PROFILE['jawInnerRadius'] else 'Base'
    groups[name].append(poly.index)
parts=[]
for name,indices in groups.items():
    if len(indices)<12:raise ValueError('Mechanical part needs visual review: '+name)
    mesh=obj.data.copy();part=bpy.data.objects.new(name,mesh);work.objects.link(part)
    bm=bmesh.new();bm.from_mesh(mesh);bm.faces.ensure_lookup_table();bm.faces.index_update();keep=set(indices)
    bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.index not in keep],context='FACES')
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
    # Close only the new mechanical separation seams of the source-derived mesh.
    boundary=[e for e in bm.edges if e.is_boundary]
    if boundary and name.startswith('Jaw'):
        uv=bm.loops.layers.uv.active
        sample=[loop[uv].uv.copy() for edge in boundary for vert in edge.verts for loop in vert.link_loops] if uv else []
        cap_uv=sample[0] if sample else Vector((0,0))
        caps=bmesh.ops.holes_fill(bm,edges=boundary,sides=0)['faces']
        for face in caps:
            face.smooth=False
            if uv:
                for loop in face.loops:loop[uv].uv=cap_uv
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mesh);bm.free()
    if name.startswith('Jaw'):
        pivot=Vector((0,0,PROFILE['hingeHeight']))
        for v in mesh.vertices:v.co-=pivot
        part.location=pivot
    parts.append(part)
bpy.data.objects.remove(obj,do_unlink=True)
scene.render.fps=24
for part in parts:
    for frame,closed in [(1,0),(4,1),(121,1),(127,.55)]:
        if part.name.startswith('Jaw'):
            part.rotation_euler.x=math.radians(78*closed)*(1 if part.name=='JawFront' else -1)
            part.keyframe_insert(data_path='rotation_euler',frame=frame)
        elif part.name=='PressurePlate':
            part.location.z=-.014*closed;part.keyframe_insert(data_path='location',frame=frame)
scene.frame_start=1;scene.frame_end=145;scene.frame_set(1)
for image in bpy.data.images:
    if image.source=='FILE' and not image.packed_file:image.pack()
bpy.context.preferences.filepaths.save_version=0
select(*parts);bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Trap_Editable.blend'))
bpy.ops.export_scene.fbx(filepath=str(DEST/'geometry.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='RELATIVE',use_mesh_modifiers=True)
report=dict(tool='Blender 4.0.2',widthMetres=PROFILE['widthMetres'],source=original.name,sourceSHA256=sha(original),
    originalFaces=original_faces,gameFaces=sum(len(p.data.polygons) for p in parts),gameTriangles=sum(len(f.vertices)-2 for p in parts for f in p.data.polygons),parts={p.name:len(p.data.polygons) for p in parts},
    materials=[dict(name=mat.name,roughness=.55,**results)],editable='Trap_Editable.blend',animation='24fps editable open/snap/hold/release keys; runtime owns the exact 5s clock')
(DEST/'asset.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');(SOURCE/'export.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('TRAP_GAME_PROP_PASS '+json.dumps(report),flush=True)
